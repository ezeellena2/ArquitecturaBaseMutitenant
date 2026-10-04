using System.Net;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using ArquitecturaBaseMultitenant.Application.Models.Messaging;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Platform;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Platform;

/// <summary>
/// Plataforma › Recuperaciones: el operador con <c>platform.recoveries.manage</c> revisa los pedidos, aprueba (el
/// correo nuevo pasa a ser el principal, sin emitir sesión) o rechaza, siempre con motivo y versión.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class PlatformRecoveryTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Approving_makes_the_new_email_the_primary_method_and_the_person_enters_with_it()
    {
        using var person = Client();
        var account = await AccountJourney.RegisterAsync(factory, person, Ct);
        await RecoveryJourney.AgeCodesAsync(factory, account.Email.Value, Ct);
        var newEmail = $"recupera-{Guid.NewGuid():N}@example.test";
        var accountOutbox = await OutboxIdsAsync();
        using var anonymous = Client();
        await RecoveryJourney.SubmitAsync(factory, anonymous, newEmail, account.Email.Value, Ct);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            // A la cuenta le avisan por sus métodos; a quien hizo el pedido, nada que la delate.
            var warning = Assert.Single(await EmailsAsync(scope.ServiceProvider, account.Email.Value, accountOutbox));
            Assert.Equal("Alguien pidió recuperar tu cuenta", warning.Subject);
        }

        using var platform = await OperatorAsync([PlatformPermissions.Recoveries.Manage]);
        var row = await RowAsync(platform, newEmail);
        Assert.Equal(account.UserId, row.GetProperty("accountId").GetGuid());
        Assert.Equal("Pending", row.GetProperty("status").GetString());
        Assert.Equal("Email", row.GetProperty("previousMethodType").GetString());
        Assert.Equal(account.Email.Value[..2] + "•••@example.test", row.GetProperty("previousMethod").GetString());
        Assert.Equal("re•••@example.test", row.GetProperty("newMethod").GetString());
        var counts = await GetAsync(platform, "filter-counts?search=" + Uri.EscapeDataString(newEmail));
        Assert.Equal(1, counts.GetProperty("total").GetInt32());
        Assert.Equal(1, counts.GetProperty("statuses").EnumerateArray()
            .Single(item => item.GetProperty("status").GetString() == "Pending").GetProperty("count").GetInt32());
        var id = row.GetProperty("id").GetGuid();

        using var withoutReason = await PlatformJourney.PostAsync(platform, $"/api/platform/recoveries/{id:D}/approve",
            new { version = row.GetProperty("version").GetUInt32(), reason = " " }, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, withoutReason.StatusCode);
        var newOutbox = await OutboxIdsAsync();
        using var approved = await PlatformJourney.PostAsync(platform, $"/api/platform/recoveries/{id:D}/approve",
            new { version = row.GetProperty("version").GetUInt32(), reason = "Confirmó sus datos por videollamada." }, Ct);
        Assert.Equal(HttpStatusCode.NoContent, approved.StatusCode);
        Assert.False(approved.Headers.Contains("Set-Cookie"));
        var resolved = await RowAsync(platform, newEmail);
        Assert.Equal("Approved", resolved.GetProperty("status").GetString());
        Assert.Equal("Confirmó sus datos por videollamada.", resolved.GetProperty("reason").GetString());
        Assert.NotEqual(JsonValueKind.Null, resolved.GetProperty("decidedAtUtc").ValueKind);
        using var again = await PlatformJourney.PostAsync(platform, $"/api/platform/recoveries/{id:D}/approve",
            new { version = resolved.GetProperty("version").GetUInt32(), reason = "Otra vez" }, Ct);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal(AccountRecoveryErrors.AlreadyResolvedCode, await CodeAsync(again));

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            var db = services.GetRequiredService<ApplicationDbContext>();
            var methods = await db.LoginMethods.AsNoTracking().Where(method => method.UserId == account.UserId).ToArrayAsync(Ct);
            var added = Assert.Single(methods, method => method.Value == newEmail);
            Assert.NotNull(added.VerifiedAtUtc);
            Assert.True(added.IsPrimary);
            Assert.Single(methods, method => method.IsPrimary);
            var ev = await db.SecurityEvents.AsNoTracking()
                .SingleAsync(item => item.Type == SecurityEventType.AccountRecoveryApproved && item.TargetAccountId == account.UserId, Ct);
            Assert.Equal(AuditActorKind.PlatformOperator, ev.ActorKind);
            Assert.Equal("Confirmó sus datos por videollamada.", ev.Reason);
            Assert.DoesNotContain(newEmail, ev.Changes);
            var notice = Assert.Single(await EmailsAsync(services, newEmail, newOutbox));
            Assert.Equal("Recuperaste tu cuenta", notice.Subject);
            Assert.DoesNotContain("videollamada", notice.TextBody);
        }

        // Con el correo nuevo ya puede entrar.
        await RecoveryJourney.AgeCodesAsync(factory, newEmail, Ct);
        using var login = await AccountJourney.PostAsync(anonymous, "/api/auth/login-code", new { email = newEmail }, Ct);
        Assert.Equal(HttpStatusCode.Accepted, login.StatusCode);
        var code = await PickupCodeReader.ReadAsync(factory.Services, newEmail, Ct);
        using var verified = await AccountJourney.PostAsync(anonymous, "/api/auth/login-code/verify",
            new { email = newEmail, code, returnUrl = "/invitacion" }, Ct);
        Assert.Equal(HttpStatusCode.OK, verified.StatusCode);
    }

    [Fact]
    public async Task A_request_without_an_account_can_only_be_rejected()
    {
        using var anonymous = Client();
        var newEmail = $"sin-cuenta-{Guid.NewGuid():N}@example.test";
        await RecoveryJourney.SubmitAsync(factory, anonymous, newEmail, $"nadie-{Guid.NewGuid():N}@example.test", Ct);
        using var platform = await OperatorAsync([PlatformPermissions.Recoveries.Manage]);
        var row = await RowAsync(platform, newEmail);
        Assert.Equal(JsonValueKind.Null, row.GetProperty("accountId").ValueKind);
        var id = row.GetProperty("id").GetGuid();
        var version = row.GetProperty("version").GetUInt32();

        using var approved = await PlatformJourney.PostAsync(platform, $"/api/platform/recoveries/{id:D}/approve",
            new { version, reason = "Parece suya" }, Ct);
        Assert.Equal(HttpStatusCode.Conflict, approved.StatusCode);
        Assert.Equal(AccountRecoveryErrors.NoAccountCode, await CodeAsync(approved));
        var outbox = await OutboxIdsAsync();
        using var rejected = await PlatformJourney.PostAsync(platform, $"/api/platform/recoveries/{id:D}/reject",
            new { version, reason = "No hay ninguna cuenta con el método de antes." }, Ct);
        Assert.Equal(HttpStatusCode.NoContent, rejected.StatusCode);
        var resolved = await RowAsync(platform, newEmail);
        Assert.Equal("Rejected", resolved.GetProperty("status").GetString());
        using var again = await PlatformJourney.PostAsync(platform, $"/api/platform/recoveries/{id:D}/reject",
            new { version = resolved.GetProperty("version").GetUInt32(), reason = "Otra vez" }, Ct);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<ApplicationDbContext>();
        var ev = await db.SecurityEvents.AsNoTracking().OrderByDescending(item => item.OccurredAtUtc)
            .FirstAsync(item => item.Type == SecurityEventType.AccountRecoveryRejected
                && item.Reason == "No hay ninguna cuenta con el método de antes.", Ct);
        Assert.Null(ev.TargetAccountId);
        Assert.False(await db.LoginMethods.AnyAsync(method => method.Value == newEmail, Ct));
        var notice = Assert.Single(await EmailsAsync(services, newEmail, outbox));
        Assert.Equal("No pudimos recuperar tu cuenta", notice.Subject);
    }

    [Fact]
    public async Task Approval_stops_when_the_new_email_now_has_an_account_and_only_recovery_managers_review()
    {
        using var person = Client();
        var account = await AccountJourney.RegisterAsync(factory, person, Ct);
        var newEmail = $"ocupado-{Guid.NewGuid():N}@example.test";
        using var anonymous = Client();
        await RecoveryJourney.SubmitAsync(factory, anonymous, newEmail, account.Email.Value, Ct);
        await RecoveryJourney.AgeCodesAsync(factory, newEmail, Ct);
        using var other = Client();
        await AccountJourney.RegisterAsync(factory, other, Ct, ArquitecturaBaseMultitenant.Domain.ValueObjects.Email.Create(newEmail).Value);

        using var platform = await OperatorAsync([PlatformPermissions.Recoveries.Manage]);
        var row = await RowAsync(platform, newEmail);
        using var approved = await PlatformJourney.PostAsync(platform,
            $"/api/platform/recoveries/{row.GetProperty("id").GetGuid():D}/approve",
            new { version = row.GetProperty("version").GetUInt32(), reason = "Parece suya" }, Ct);
        Assert.Equal(HttpStatusCode.Conflict, approved.StatusCode);
        Assert.Equal(AccountRecoveryErrors.MethodInUseCode, await CodeAsync(approved));

        using var noSession = Client();
        using var unauthenticated = await noSession.GetAsync("/api/platform/recoveries/paged", Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, unauthenticated.StatusCode);
        using var consumer = await person.GetAsync("/api/platform/recoveries/paged", Ct);
        Assert.Equal(HttpStatusCode.Forbidden, consumer.StatusCode);
        using var reader = await OperatorAsync([PlatformPermissions.Accounts.Read]);
        using var forbidden = await reader.GetAsync("/api/platform/recoveries/paged", Ct);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    [Fact]
    public async Task A_recovery_for_an_operator_account_cannot_be_approved()
    {
        // Si no, quien solo revisa recuperaciones podría darse la cuenta de un operador con más permisos.
        using var target = await OperatorAsync([PlatformPermissions.Operators.Manage]);
        var targetEmail = await EmailOfAsync(target);
        var newEmail = $"toma-{Guid.NewGuid():N}@example.test";
        using var anonymous = Client();
        await RecoveryJourney.SubmitAsync(factory, anonymous, newEmail, targetEmail, Ct);
        using var reviewer = await OperatorAsync([PlatformPermissions.Recoveries.Manage]);
        var row = await RowAsync(reviewer, newEmail);
        var id = row.GetProperty("id").GetGuid();

        using var approved = await PlatformJourney.PostAsync(reviewer, $"/api/platform/recoveries/{id:D}/approve",
            new { version = row.GetProperty("version").GetUInt32(), reason = "Parece suya" }, Ct);

        Assert.Equal(HttpStatusCode.Conflict, approved.StatusCode);
        Assert.Equal(AccountRecoveryErrors.OperatorAccountCode, await CodeAsync(approved));
        await using var scope = factory.Services.CreateAsyncScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().LoginMethods
            .AnyAsync(method => method.Value == newEmail, Ct));
    }

    private static async Task<string> EmailOfAsync(HttpClient client)
    {
        using var me = await client.GetAsync("/api/me", Ct);
        Assert.True(me.StatusCode == HttpStatusCode.OK, await me.Content.ReadAsStringAsync(Ct));
        return JsonDocument.Parse(await me.Content.ReadAsStringAsync(Ct)).RootElement.GetProperty("email").GetString()!;
    }

    private HttpClient Client() =>
        factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });

    private async Task<HttpClient> OperatorAsync(IReadOnlyCollection<string> permissions)
    {
        var client = Client();
        var account = await AccountJourney.RegisterAsync(factory, client, Ct);
        await PlatformOperators.GrantAsync(factory.Services, account.UserId, permissions, Ct);
        await AccountJourney.AuthorizeAsync(client, "platform", Ct);
        return client;
    }

    private static async Task<JsonElement> GetAsync(HttpClient client, string path)
    {
        using var response = await client.GetAsync("/api/platform/recoveries/" + path, Ct);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct)).RootElement.Clone();
    }

    private static async Task<JsonElement> RowAsync(HttpClient client, string newEmail) =>
        Assert.Single((await GetAsync(client, "paged?search=" + Uri.EscapeDataString(newEmail)))
            .GetProperty("items").EnumerateArray().ToArray());

    private static async Task<string?> CodeAsync(HttpResponseMessage response)
    {
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        return problem.RootElement.GetProperty("code").GetString();
    }

    private async Task<Guid[]> OutboxIdsAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().OutboxMessages.AsNoTracking()
            .Select(message => message.Id).ToArrayAsync(Ct);
    }

    private static async Task<EmailMessage[]> EmailsAsync(IServiceProvider services, string to, Guid[] before)
    {
        var protector = services.GetRequiredService<IPayloadProtector>();
        var payloads = await services.GetRequiredService<ApplicationDbContext>().OutboxMessages.AsNoTracking()
            .Where(message => message.Channel == "email" && !before.Contains(message.Id))
            .Select(message => message.EncryptedPayload).ToArrayAsync(Ct);
        return payloads.Select(payload => JsonSerializer.Deserialize<EmailMessage>(protector.Unprotect(payload))!)
            .Where(message => string.Equals(message.To, to, StringComparison.OrdinalIgnoreCase)).ToArray();
    }
}
