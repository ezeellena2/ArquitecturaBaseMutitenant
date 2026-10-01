using System.Text.Json;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Invitations;
using ArquitecturaBaseMultitenant.Application.Services.Invitations;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Invitations;
using ArquitecturaBaseMultitenant.Domain.Legal;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.RealE2ESetup;

/// <summary>Orden local del runner, bajo el guard aislado del preparador; nunca agrega una ruta HTTP.</summary>
internal static class InvitationCommand
{
    public static async Task TryExecuteAsync(IServiceProvider services, string readyFile, Guid tenantId,
        Guid inviterId, CancellationToken ct)
    {
        var commandFile = Path.ChangeExtension(readyFile, ".invitations.json");
        var completedFile = commandFile + ".done";
        if (!File.Exists(commandFile) || File.Exists(completedFile)) return;
        using var command = JsonDocument.Parse(await File.ReadAllTextAsync(commandFile, ct));
        var email = Email.Create(command.RootElement.GetProperty("email").GetString());
        if (email.IsFailure || !email.Value.Value.EndsWith("@example.test", StringComparison.Ordinal))
            throw new InvalidOperationException("Only an isolated test mailbox is allowed.");
        await using var scope = services.CreateAsyncScope();
        var provider = scope.ServiceProvider;
        var kind = command.RootElement.GetProperty("kind").GetString();
        object result;
        if (kind == "emit")
        {
            if (command.RootElement.GetProperty("existingAccount").GetBoolean())
                await CreateRecipientAsync(provider, email.Value, ct);
            using var tenantScope = provider.GetRequiredService<ITenantScope>().Enter(tenantId);
            var issued = await provider.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(
                token => provider.GetRequiredService<InvitationIssuer>().IssueAsync(
                    new IssueInvitationRequest(email.Value, inviterId, InvitationChannel.Email), token), CommitPolicy.OnSuccess, ct);
            if (issued.IsFailure) throw new InvalidOperationException("The isolated invitation could not be issued.");
            result = new { invitationId = issued.Value, tenantId };
        }
        else if (kind == "inspect")
        {
            var userId = await provider.GetRequiredService<IUserLookup>()
                .FindVerifiedUserIdAsync(LoginMethodType.Email, email.Value.Value, ct);
            if (userId is null) throw new InvalidOperationException("The invitation did not create or bind an identity.");
            var accesses = await provider.GetRequiredService<IUserTenantAccessReader>().ListForUserAsync(userId.Value, ct);
            using var tenantScope = provider.GetRequiredService<ITenantScope>().Enter(tenantId);
            var invitationId = command.RootElement.GetProperty("invitationId").GetGuid();
            var invitation = await provider.GetRequiredService<IInvitationReader>().FindByIdAsync(invitationId, ct)
                ?? throw new InvalidOperationException("The isolated invitation was not found.");
            result = new
            {
                personalSpaces = accesses.Count(row => row.Kind == TenantKind.Personal),
                activeBusinessAccesses = accesses.Count(row => row.TenantId == tenantId && row.MemberStatus == MemberStatus.Active),
                accepted = invitation.Status == InvitationStatus.Accepted,
                boundToIdentity = invitation.MemberUserId == userId,
            };
        }
        else throw new InvalidOperationException("Unsupported isolated invitation command.");
        await File.WriteAllTextAsync(completedFile, JsonSerializer.Serialize(result), ct);
    }

    private static Task<Result> CreateRecipientAsync(IServiceProvider services, Email email, CancellationToken ct) =>
        services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async token =>
        {
            var users = services.GetRequiredService<IUserRepository>();
            var user = await users.CreateAsync("Persona invitada E2E", "es-AR", "America/Argentina/Buenos_Aires", token);
            var nowUtc = services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
            var method = LoginMethod.CreateEmail(user.Id, email);
            method.Verify(nowUtc);
            if (method.MakePrimary().IsFailure) throw new InvalidOperationException("A verified primary method is required.");
            services.GetRequiredService<ILoginMethodRepository>().Add(method);
            await users.SetPrimaryEmailAsync(user.Id, email, token);
            var legal = services.GetRequiredService<ILegalRepository>();
            foreach (var documentKind in Enum.GetValues<LegalDocumentKind>())
            {
                var document = await legal.GetCurrentDocumentAsync(documentKind, nowUtc, token)
                    ?? throw new InvalidOperationException("Current legal documents are required.");
                legal.AddAcceptance(LegalAcceptance.Create(user.Id, document, nowUtc, null, null));
            }
            return Result.Success();
        }, CommitPolicy.OnSuccess, ct);
}

internal sealed class E2EPublicOrigin : IPublicOrigin
{
    public Uri Value { get; } = new("https://localhost:5174/");
}
