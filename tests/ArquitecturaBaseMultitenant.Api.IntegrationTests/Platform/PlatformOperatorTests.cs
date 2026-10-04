using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using ArquitecturaBaseMultitenant.Application.Models.Messaging;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Platform;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Platform;

/// <summary>
/// Operadores de Plataforma (ADR 0042): se invitan por correo con permisos elegidos uno por uno, su identidad es solo
/// de Plataforma (nunca entran como persona ni a una organización) y sus permisos se cambian o se quitan con versión.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class PlatformOperatorTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task An_invited_operator_has_only_platform_access_with_the_chosen_permissions()
    {
        var (manager, managerId) = await OperatorAsync(PlatformOperators.All);
        var email = $"operador-{Guid.NewGuid():N}@example.test";
        var outbox = await OutboxIdsAsync();
        var key = Guid.NewGuid().ToString("D");
        var body = new { email, permissions = new[] { PlatformPermissions.Accounts.Read, PlatformPermissions.Tenants.Read } };

        using var invited = await SendAsync(manager, HttpMethod.Post, "/api/platform/operators/invitations", body, key);
        Assert.Equal(HttpStatusCode.Created, invited.StatusCode);
        var id = (await JsonAsync(invited)).GetProperty("id").GetGuid();
        using var replayed = await SendAsync(manager, HttpMethod.Post, "/api/platform/operators/invitations", body, key);
        Assert.Equal("true", Assert.Single(replayed.Headers.GetValues("Idempotent-Replayed")));

        // Aparece enseguida en Cuentas como operador, con los permisos en el orden del catálogo.
        var detail = await DetailAsync(manager, id);
        Assert.Equal("Operator", detail.GetProperty("kind").GetString());
        Assert.Equal("Active", detail.GetProperty("state").GetString());
        Assert.False(detail.GetProperty("hasPersonalSpace").GetBoolean());
        Assert.Equal([PlatformPermissions.Tenants.Read, PlatformPermissions.Accounts.Read],
            detail.GetProperty("operatorPermissions").EnumerateArray().Select(item => item.GetString()).ToArray());
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            var db = services.GetRequiredService<ApplicationDbContext>();
            var user = await db.Users.AsNoTracking().SingleAsync(item => item.Id == id, Ct);
            Assert.True(user.IsPlatformOperator);
            var method = await db.LoginMethods.AsNoTracking().SingleAsync(item => item.UserId == id, Ct);
            Assert.Equal(email, method.Value);
            Assert.True(method.IsPrimary);
            Assert.NotNull(method.VerifiedAtUtc);
            var ev = await db.SecurityEvents.AsNoTracking()
                .SingleAsync(item => item.Type == SecurityEventType.PlatformOperatorInvited && item.TargetAccountId == id, Ct);
            Assert.Equal(managerId, ev.ActorId);
            Assert.Equal(AuditActorKind.PlatformOperator, ev.ActorKind);
            Assert.DoesNotContain(email, ev.Changes);
            using var changes = JsonDocument.Parse(ev.Changes);
            Assert.True(changes.RootElement.GetProperty(PlatformPermissions.Accounts.Read).GetProperty("after").GetBoolean());
            var notice = Assert.Single(await EmailsAsync(services, email, outbox));
            Assert.Equal("Te invitaron como operador", notice.Subject);
        }

        // Entra con código: Plataforma sí; como persona o empresa, nunca, y no se le crea un espacio personal.
        using var invitedClient = Client();
        await SignInAsync(invitedClient, email);
        await AccountJourney.AuthorizeAsync(invitedClient, "platform", Ct);
        invitedClient.DefaultRequestHeaders.Authorization = null;
        foreach (var access in new[] { "consumer", "business" })
        {
            using var denied = await invitedClient.GetAsync(AuthorizePath(access), Ct);
            Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode);
            var location = Uri.UnescapeDataString(denied.Headers.Location!.ToString());
            Assert.Contains("error=access_denied", location, StringComparison.Ordinal);
            Assert.Contains(AccessErrors.PlatformOnlyCode, location, StringComparison.Ordinal);
        }
        await using (var scope = factory.Services.CreateAsyncScope())
            Assert.False(await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().UserTenantAccesses
                .AnyAsync(access => access.UserId == id, Ct));
        Assert.False((await DetailAsync(manager, id)).GetProperty("hasPersonalSpace").GetBoolean());
    }

    [Fact]
    public async Task Invitations_reject_accounts_that_already_exist_and_invalid_permissions()
    {
        var (manager, _) = await OperatorAsync([PlatformPermissions.Operators.Manage, PlatformPermissions.Accounts.Read]);
        using var person = Client();
        var existing = await AccountJourney.RegisterAsync(factory, person, Ct);

        using var taken = await PlatformJourney.PostAsync(manager, "/api/platform/operators/invitations",
            new { email = existing.Email.Value, permissions = new[] { PlatformPermissions.Audit.Read } }, Ct);
        Assert.Equal(HttpStatusCode.Conflict, taken.StatusCode);
        var problem = await JsonAsync(taken);
        Assert.Equal(PlatformOperatorErrors.AccountExistsCode, problem.GetProperty("code").GetString());
        Assert.Equal("Ese correo ya tiene una cuenta. Un operador no puede tener perfiles de persona.",
            problem.GetProperty("detail").GetString());

        var free = $"operador-{Guid.NewGuid():N}@example.test";
        foreach (var (permissions, code) in new (string[] Permissions, string Code)[]
                 {
                     ([], PlatformOperatorErrors.PermissionsRequiredCode),
                     (["platform.desconocido"], PlatformOperatorErrors.PermissionNotFoundCode),
                     ([PlatformPermissions.Audit.Read, PlatformPermissions.Audit.Read], PlatformOperatorErrors.PermissionRepeatedCode),
                 })
        {
            using var invalid = await PlatformJourney.PostAsync(manager, "/api/platform/operators/invitations",
                new { email = free, permissions }, Ct);
            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
            Assert.Equal(code, (await JsonAsync(invalid)).GetProperty("code").GetString());
        }

        var (reader, _) = await OperatorAsync([PlatformPermissions.Accounts.Read]);
        using var forbidden = await PlatformJourney.PostAsync(reader, "/api/platform/operators/invitations",
            new { email = free, permissions = new[] { PlatformPermissions.Audit.Read } }, Ct);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().LoginMethods
            .AnyAsync(method => method.Value == free, Ct));
    }

    [Fact]
    public async Task Permissions_change_and_are_removed_with_version_reason_and_events()
    {
        var (manager, managerId) = await OperatorAsync(PlatformOperators.All);
        var email = $"operador-{Guid.NewGuid():N}@example.test";
        using var invited = await PlatformJourney.PostAsync(manager, "/api/platform/operators/invitations",
            new { email, permissions = new[] { PlatformPermissions.Tenants.Read } }, Ct);
        Assert.Equal(HttpStatusCode.Created, invited.StatusCode);
        var id = (await JsonAsync(invited)).GetProperty("id").GetGuid();
        var path = $"/api/platform/accounts/{id:D}/operator";
        var version = (await DetailAsync(manager, id)).GetProperty("operatorVersion").GetUInt32();

        using var changed = await SendAsync(manager, HttpMethod.Put, path, new
        {
            version, reason = "Suma la auditoría",
            permissions = new[] { PlatformPermissions.Tenants.Read, PlatformPermissions.Audit.Read },
        }, Guid.NewGuid().ToString("D"));
        Assert.Equal(HttpStatusCode.NoContent, changed.StatusCode);
        var detail = await DetailAsync(manager, id);
        Assert.Equal([PlatformPermissions.Tenants.Read, PlatformPermissions.Audit.Read],
            detail.GetProperty("operatorPermissions").EnumerateArray().Select(item => item.GetString()).ToArray());
        using var stale = await SendAsync(manager, HttpMethod.Put, path, new
        {
            version, reason = "Otra vez", permissions = new[] { PlatformPermissions.Tenants.Read },
        }, Guid.NewGuid().ToString("D"));
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using var own = await SendAsync(manager, HttpMethod.Put, $"/api/platform/accounts/{managerId:D}/operator", new
        {
            version = (await DetailAsync(manager, managerId)).GetProperty("operatorVersion").GetUInt32(),
            reason = "Me saco permisos", permissions = new[] { PlatformPermissions.Tenants.Read },
        }, Guid.NewGuid().ToString("D"));
        Assert.Equal(HttpStatusCode.Forbidden, own.StatusCode);
        Assert.Equal(UserErrors.CannotModifySelfCode, (await JsonAsync(own)).GetProperty("code").GetString());

        using var removed = await SendAsync(manager, HttpMethod.Delete, path, new
        {
            version = detail.GetProperty("operatorVersion").GetUInt32(), reason = "Dejó el equipo",
        }, Guid.NewGuid().ToString("D"));
        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        var after = await DetailAsync(manager, id);
        Assert.Equal("Person", after.GetProperty("kind").GetString());
        Assert.Equal(JsonValueKind.Null, after.GetProperty("operatorPermissions").ValueKind);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.False((await db.Users.AsNoTracking().SingleAsync(user => user.Id == id, Ct)).IsPlatformOperator);
        Assert.False(await db.PlatformPermissionAssignments.AnyAsync(assignment => assignment.UserId == id, Ct));
        var changes = await db.SecurityEvents.AsNoTracking()
            .SingleAsync(item => item.Type == SecurityEventType.PlatformOperatorPermissionsChanged && item.TargetAccountId == id, Ct);
        Assert.Equal("Suma la auditoría", changes.Reason);
        using var diff = JsonDocument.Parse(changes.Changes);
        Assert.False(diff.RootElement.GetProperty(PlatformPermissions.Audit.Read).GetProperty("before").GetBoolean());
        Assert.True(diff.RootElement.GetProperty(PlatformPermissions.Audit.Read).GetProperty("after").GetBoolean());
        Assert.False(diff.RootElement.TryGetProperty(PlatformPermissions.Tenants.Read, out _));
        Assert.True(await db.SecurityEvents.AnyAsync(item => item.Type == SecurityEventType.PlatformOperatorRemoved
            && item.TargetAccountId == id && item.Reason == "Dejó el equipo", Ct));
    }

    private HttpClient Client() =>
        factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });

    private async Task<(HttpClient Client, Guid Id)> OperatorAsync(IReadOnlyCollection<string> permissions)
    {
        var client = Client();
        var account = await AccountJourney.RegisterAsync(factory, client, Ct);
        await PlatformOperators.GrantAsync(factory.Services, account.UserId, permissions, Ct);
        await AccountJourney.AuthorizeAsync(client, "platform", Ct);
        return (client, account.UserId);
    }

    private async Task SignInAsync(HttpClient client, string email)
    {
        using var requested = await AccountJourney.PostAsync(client, "/api/auth/login-code", new { email }, Ct);
        Assert.Equal(HttpStatusCode.Accepted, requested.StatusCode);
        var code = await PickupCodeReader.ReadAsync(factory.Services, email, Ct);
        using var verified = await AccountJourney.PostAsync(client, "/api/auth/login-code/verify",
            new { email, code, returnUrl = "/invitacion" }, Ct);
        Assert.Equal(HttpStatusCode.OK, verified.StatusCode);
    }

    private static string AuthorizePath(string access) =>
        "/connect/authorize?client_id=web&response_type=code" +
        "&redirect_uri=https%3A%2F%2Flocalhost%3A5174%2Fauth%2Fcallback&scope=openid%20profile%20email%20api" +
        "&code_challenge=AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA&code_challenge_method=S256&access=" + access;

    private static Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method, string path, object body,
        string key)
    {
        var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("Idempotency-Key", key);
        return client.SendAsync(request, Ct);
    }

    private static async Task<JsonElement> DetailAsync(HttpClient client, Guid id)
    {
        using var response = await client.GetAsync($"/api/platform/accounts/{id:D}", Ct);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct)).RootElement.Clone();
    }

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct)).RootElement.Clone();

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
