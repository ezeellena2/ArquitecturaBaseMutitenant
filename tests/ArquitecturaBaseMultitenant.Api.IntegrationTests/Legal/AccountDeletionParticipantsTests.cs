using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Legal;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Legal;
using ArquitecturaBaseMultitenant.Application.Services.Auth;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Legal;
using ArquitecturaBaseMultitenant.Domain.Messaging;
using ArquitecturaBaseMultitenant.Domain.Platform;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Settings;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Legal;

/// <summary>
/// Comprueba que cada participante limpie únicamente los datos de la cuenta eliminada. Repetir la limpieza
/// no debe ampliar el alcance ni fallar por datos ya retirados.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class AccountDeletionParticipantsTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("MembershipDeletionParticipant", TenantKind.Business)]
    [InlineData("PersonalSpaceDeletionParticipant", TenantKind.Personal)]
    [InlineData("RecoveryDeletionParticipant", null)]
    public async Task Deletion_rules_require_the_callers_transaction_and_matching_tenant_scope(
        string participantName, TenantKind? tenantKind)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var participant = Assert.Single(services.GetServices<IAccountDeletionParticipant>(),
            candidate => candidate.GetType().Name == participantName);
        var context = new AccountDeletionContext(Guid.CreateVersion7(),
            services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime,
            tenantKind is null ? null : Guid.CreateVersion7(), tenantKind);
        await Assert.ThrowsAsync<InvalidOperationException>(() => participant.ExecuteAsync(context, Ct));

        if (context.TenantId is not null)
        {
            using var wrongTenant = services.GetRequiredService<ITenantScope>().Enter(Guid.CreateVersion7());
            await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
            {
                await Assert.ThrowsAsync<InvalidOperationException>(() => participant.ExecuteAsync(context, ct));
                return Result.Success();
            }, CommitPolicy.OnSuccess, Ct);
        }
    }

    [Fact]
    public async Task Current_participants_clean_only_the_account_data_and_can_run_twice()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
            { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        client.DefaultRequestHeaders.UserAgent.ParseAdd("AccountDeletionTest/1.0");
        var account = await AccountJourney.RegisterAsync(factory, client, Ct);
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<ApplicationDbContext>();
        var work = services.GetRequiredService<IUnitOfWork>();
        var tenantScope = services.GetRequiredService<ITenantScope>();
        var nowUtc = services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
        var business = Tenant.CreateBusiness("Empresa prueba baja", false);
        business.Activate();
        using (tenantScope.Enter(business.Id))
            await work.ExecuteInTransactionAsync(ct =>
            {
                services.GetRequiredService<TenantSpaceProvisioner>().Stage(business,
                    TenantSettings.Create("es-AR", "America/Argentina/Buenos_Aires", "ARS"), [account.UserId]);
                return Task.FromResult(Result.Success());
            }, CommitPolicy.OnSuccess, Ct);
        var ownedMessage = OutboxMessage.Enqueue("test", "owned", nowUtc, account.UserId);
        var otherMessage = OutboxMessage.Enqueue("test", "other", nowUtc, Guid.CreateVersion7());
        var newEmail = Email.Create("nuevo-" + Guid.NewGuid().ToString("N") + "@example.test").Value;
        var ownedRecovery = AccountRecoveryRequest.Submit(Guid.CreateVersion7(), LoginMethodType.Email,
            account.Email.Value, newEmail, account.UserId, nowUtc);
        var otherRecovery = AccountRecoveryRequest.Submit(Guid.CreateVersion7(), LoginMethodType.Email,
            "otra@example.test", newEmail, Guid.CreateVersion7(), nowUtc);
        await work.ExecuteInTransactionAsync(ct =>
        {
            context.OutboxMessages.AddRange(ownedMessage, otherMessage);
            context.AccountRecoveryRequests.AddRange(ownedRecovery, otherRecovery);
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct);
        var participants = services.GetServices<IAccountDeletionParticipant>().ToArray();
        string[] core = ["MembershipDeletionParticipant", "PersonalSpaceDeletionParticipant", "LegalAcceptanceDeletionParticipant",
            "OutboxDeletionParticipant", "RecoveryDeletionParticipant", "EngagementDeletionParticipant"];
        var expected = new List<string>(core);
        var application = typeof(TenantSpaceProvisioner).Assembly;
        foreach (var module in new[] { "WhatsApp", "Assistant" })
            if (application.GetType($"ArquitecturaBaseMultitenant.Application.Modules.{module}.{module}Module") is not null)
                expected.Add(module + "AccountDeletionParticipant");
        Assert.Equal(expected.Order(StringComparer.Ordinal), participants.Select(p => p.GetType().Name).Order(StringComparer.Ordinal));
        var membershipParticipant = Assert.Single(participants,
            participant => participant.GetType().Name == "MembershipDeletionParticipant");
        Assert.All(await context.LegalAcceptances.AsNoTracking().Where(row => row.UserId == account.UserId)
            .ToArrayAsync(Ct), row => Assert.Equal("AccountDeletionTest/1.0", row.UserAgent));
        var global = new AccountDeletionContext(account.UserId, nowUtc);
        await work.ExecuteInTransactionAsync(async ct =>
        {
            foreach (var participant in participants)
            {
                var blockers = await participant.CheckAsync(global, ct);
                if (ReferenceEquals(participant, membershipParticipant))
                    Assert.Equal(AccountDeletionErrors.LastAdmin, Assert.Single(blockers));
                else
                    Assert.Empty(blockers);
                await participant.OnRequestedAsync(global, ct);
                await participant.OnCancelledAsync(global, ct);
            }
            return Result.Success();
        }, CommitPolicy.OnSuccess, Ct);
        Assert.Equal(OutboxStatus.Cancelled, ownedMessage.Status);
        Assert.Equal(OutboxStatus.Pending, otherMessage.Status);
        var targets = await services.GetRequiredService<IAccountDeletionTenantReader>().ListAsync(account.UserId, Ct);
        Assert.Contains(targets, target => target.Kind == TenantKind.Personal);
        Assert.Contains(targets, target => target.TenantId == business.Id && target.Kind == TenantKind.Business);
        foreach (var target in targets.Where(target => target.Kind == TenantKind.Personal || target.TenantId == business.Id))
        {
            using (tenantScope.Enter(target.TenantId))
            {
                var entriesBefore = await context.Set<AuditEntry>().AsNoTracking().Select(row => row.Id).ToArrayAsync(Ct);
                for (var repeat = 0; repeat < 2; repeat++)
                    await work.ExecuteInTransactionAsync(async ct =>
                    {
                        foreach (var participant in participants)
                            await participant.ExecuteAsync(global with { TenantId = target.TenantId, TenantKind = target.Kind }, ct);
                        return Result.Success();
                    }, CommitPolicy.OnSuccess, Ct);
                if (target.Kind == TenantKind.Personal)
                {
                    Assert.Empty(await context.Members.AsNoTracking().ToArrayAsync(Ct));
                    Assert.Empty(await context.TenantSettings.AsNoTracking().ToArrayAsync(Ct));
                    Assert.Equal(TenantStatus.Closed, (await context.Tenants.SingleAsync(row => row.Id == target.TenantId, Ct)).Status);
                }
                else
                {
                    var member = await context.Members.AsNoTracking().SingleAsync(row => row.UserId == account.UserId, Ct);
                    Assert.Equal(MemberStatus.Removed, member.Status);
                    Assert.Equal(MemberRemovalReason.AccountDeleted, member.RemovalReason);
                    Assert.Empty(await context.RoleAssignments.AsNoTracking().Where(row => row.MemberId == member.Id).ToArrayAsync(Ct));
                    // Una sola entrada, «Quitó a …», como cualquier salida de la organización: el motivo va en su diff y
                    // nadie suma otra a mano. Repetir la limpieza no deja otra.
                    var removal = Assert.Single(await context.Set<AuditEntry>().AsNoTracking()
                        .Where(row => !entriesBefore.Contains(row.Id)).ToArrayAsync(Ct));
                    Assert.Equal((AuditAction.Updated, nameof(Member), member.Id),
                        (removal.Action, removal.EntityType, removal.EntityId));
                    using var changes = JsonDocument.Parse(removal.Changes);
                    Assert.Equal((int)MemberRemovalReason.AccountDeleted,
                        changes.RootElement.GetProperty(nameof(Member.RemovalReason)).GetProperty("new").GetInt32());
                    await work.ExecuteInTransactionAsync(async ct =>
                    {
                        // Aunque el alcance sea correcto, DELETE queda prohibido para una empresa.
                        Assert.Equal(0, await context.Database.ExecuteSqlInterpolatedAsync(
                            $"DELETE FROM tenant.\"Members\" WHERE \"UserId\" = {account.UserId}", ct));
                        Assert.Equal(0, await context.Database.ExecuteSqlRawAsync(
                            "DELETE FROM tenant.\"TenantSettings\"", ct));
                        return Result.Success();
                    }, CommitPolicy.OnSuccess, Ct);
                }
            }
        }
        await work.ExecuteInTransactionAsync(async ct =>
        {
            foreach (var participant in participants) await participant.ExecuteAsync(global, ct);
            return Result.Success();
        }, CommitPolicy.OnSuccess, Ct);
        // Los pedidos de «Recuperar mi cuenta» de la cuenta se cierran y sus datos personales se borran.
        Assert.False(await context.AccountRecoveryRequests.AnyAsync(row => row.Id == ownedRecovery.Id, Ct));
        Assert.True(await context.AccountRecoveryRequests.AnyAsync(row => row.Id == otherRecovery.Id, Ct));
        var acceptances = await context.LegalAcceptances.AsNoTracking().Where(row => row.UserId == account.UserId).ToArrayAsync(Ct);
        Assert.Equal(2, acceptances.Length);
        Assert.All(acceptances, row => { Assert.Null(row.IpAddress); Assert.Null(row.UserAgent); });
    }
}
