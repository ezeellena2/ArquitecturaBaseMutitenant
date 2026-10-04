using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Common.Formatting;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using ArquitecturaBaseMultitenant.Application.Models.Messaging;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Platform;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Platform;

/// <summary>
/// Operadores de Plataforma (ADR 0042): se invitan por correo con permisos elegidos uno por uno; la invitación queda
/// pendiente hasta el primer ingreso con código a Plataforma y vence a los 7 días; se reenvía o se revoca. Su identidad
/// es solo de Plataforma (nunca entran como persona ni a una organización) y sus permisos se cambian o se quitan con
/// versión.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class PlatformOperatorTests(ApiFactory factory)
{
    private const string PlatformReturn = "/connect/authorize?access=platform";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task An_invitation_stays_pending_without_sessions_until_the_first_code_sign_in_to_platform()
    {
        var (manager, managerId) = await OperatorAsync(PlatformOperators.All);
        await RenameAsync(managerId, "Martín Sosa");
        var email = $"operador-{Guid.NewGuid():N}@example.test";
        var outbox = await OutboxIdsAsync();
        var key = Guid.NewGuid().ToString("D");
        var body = new { email, permissions = new[] { PlatformPermissions.Accounts.Read, PlatformPermissions.Tenants.Read } };
        var before = Now();

        using var invited = await SendAsync(manager, HttpMethod.Post, "/api/platform/operators/invitations", body, key);
        Assert.Equal(HttpStatusCode.Created, invited.StatusCode);
        var invitation = await JsonAsync(invited);
        var id = invitation.GetProperty("id").GetGuid();
        var expiresAtUtc = invitation.GetProperty("expiresAtUtc").GetDateTime();
        Assert.InRange(expiresAtUtc, before.AddDays(7), Now().AddDays(7));
        using var replayed = await SendAsync(manager, HttpMethod.Post, "/api/platform/operators/invitations", body, key);
        Assert.Equal("true", Assert.Single(replayed.Headers.GetValues("Idempotent-Replayed")));

        // En Cuentas es una «Invitación pendiente» de un operador, con su vencimiento, y cuenta en el filtro Estado.
        var detail = await DetailAsync(manager, id);
        Assert.Equal("Operator", detail.GetProperty("kind").GetString());
        Assert.Equal("Invited", detail.GetProperty("state").GetString());
        Assert.Equal(expiresAtUtc, detail.GetProperty("invitationExpiresAtUtc").GetDateTime());
        Assert.False(detail.GetProperty("hasPersonalSpace").GetBoolean());
        Assert.Equal([PlatformPermissions.Tenants.Read, PlatformPermissions.Accounts.Read],
            detail.GetProperty("operatorPermissions").EnumerateArray().Select(item => item.GetString()).ToArray());
        var row = Assert.Single(await RowsAsync(manager, $"state=Invited&search={Uri.EscapeDataString(email)}"));
        Assert.Equal(id, row.GetProperty("id").GetGuid());
        Assert.Equal(expiresAtUtc, row.GetProperty("invitationExpiresAtUtc").GetDateTime());
        Assert.Equal(1, await StateCountAsync(manager, email, "Invited"));

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

            // Lienzo v90, Mensajes › «Invitación como operador»: quién invita, los permisos por nombre y el
            // vencimiento en la zona de la cuenta invitada. Sale solo por correo.
            var formatter = services.GetRequiredService<DisplayFormatter>();
            var expires = formatter.FormatDate(expiresAtUtc, await formatter.CreateAsync("es-AR", user.TimeZoneId, Ct));
            var notice = Assert.Single(await EmailsAsync(services, email, outbox));
            Assert.Equal("Martín Sosa te invitó como operador de Arquitectura Base", notice.Subject);
            foreach (var text in new[]
                     {
                         "Te invitaron como operador",
                         "Hola. Martín Sosa te invitó a sumarte como operador de la plataforma Arquitectura Base.",
                         "Permisos: Ver organizaciones y Ver cuentas",
                         "Vence: " + expires,
                         "Ingresar: https://",
                         "Para entrar, usá este correo antes de que venza la invitación: te vamos a mandar un código de acceso.",
                         "Esta cuenta es solo para operar la plataforma: no se usa como persona ni para entrar a una organización.",
                         "Si no esperabas este correo, podés ignorarlo. Sin el código, nadie puede entrar a tu cuenta.",
                     })
                Assert.Contains(text, notice.TextBody, StringComparison.Ordinal);
            Assert.Contains("/plataforma", notice.TextBody, StringComparison.Ordinal);
            Assert.All(await db.OutboxMessages.AsNoTracking().Where(message => message.UserId == id)
                .Select(message => message.Channel).ToArrayAsync(Ct), channel => Assert.Equal("email", channel));
        }

        // Pendiente, por otro acceso no entra ni queda con sesión.
        using var invitedClient = Client();
        foreach (var returnUrl in new[] { "/connect/authorize?access=consumer", "/invitacion" })
        {
            using var elsewhere = await SignInAsync(invitedClient, email, returnUrl);
            Assert.Equal(HttpStatusCode.Forbidden, elsewhere.StatusCode);
            Assert.Equal(AccessErrors.PlatformOnlyCode, (await JsonAsync(elsewhere)).GetProperty("code").GetString());
        }
        using (var noSession = await invitedClient.GetAsync(AuthorizePath("platform"), Ct))
        {
            // Sin sesión, la autorización no entrega un código: manda a ingresar.
            var location = noSession.Headers.Location?.OriginalString ?? string.Empty;
            var query = location.Contains('?', StringComparison.Ordinal) ? location[location.IndexOf('?', StringComparison.Ordinal)..] : string.Empty;
            Assert.False(QueryHelpers.ParseQuery(query).ContainsKey("code"));
        }
        Assert.Equal("Invited", (await DetailAsync(manager, id)).GetProperty("state").GetString());

        // El primer ingreso con código a Plataforma acepta la invitación: Plataforma sí; como persona o empresa,
        // nunca, y no se le crea un espacio personal.
        using (var accepted = await SignInAsync(invitedClient, email, PlatformReturn))
            Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        await AccountJourney.AuthorizeAsync(invitedClient, "platform", Ct);
        var active = await DetailAsync(manager, id);
        Assert.Equal("Active", active.GetProperty("state").GetString());
        Assert.Equal(JsonValueKind.Null, active.GetProperty("invitationExpiresAtUtc").ValueKind);
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
    public async Task An_expired_invitation_cannot_sign_in_until_resending_renews_it_for_seven_days()
    {
        var (manager, managerId) = await OperatorAsync(PlatformOperators.All);
        var (id, email) = await InviteAsync(manager, [PlatformPermissions.Tenants.Read]);
        Assert.Equal("Invited", (await DetailAsync(manager, id)).GetProperty("state").GetString());
        await ExpireAsync(id);

        Assert.Equal("InvitationExpired", (await DetailAsync(manager, id)).GetProperty("state").GetString());
        Assert.Single(await RowsAsync(manager, $"state=InvitationExpired&search={Uri.EscapeDataString(email)}"));
        Assert.Equal(1, await StateCountAsync(manager, email, "InvitationExpired"));

        // Vencida no se entra: el pedido de código responde como siempre, pero no sale ningún código.
        var outbox = await OutboxIdsAsync();
        using var invitedClient = Client();
        using (var requested = await AccountJourney.PostAsync(invitedClient, "/api/auth/login-code", new { email }, Ct))
            Assert.Equal(HttpStatusCode.Accepted, requested.StatusCode);
        await using (var scope = factory.Services.CreateAsyncScope())
            Assert.Empty(await EmailsAsync(scope.ServiceProvider, email, outbox));

        // «Reenviar invitación» renueva los 7 días y manda el correo de nuevo.
        var before = Now();
        using var resent = await SendAsync(manager, HttpMethod.Post, $"/api/platform/operators/{id:D}/invitation/resend",
            null, Guid.NewGuid().ToString("D"));
        Assert.Equal(HttpStatusCode.OK, resent.StatusCode);
        var renewed = await JsonAsync(resent);
        Assert.Equal(id, renewed.GetProperty("id").GetGuid());
        Assert.InRange(renewed.GetProperty("expiresAtUtc").GetDateTime(), before.AddDays(7), Now().AddDays(7));
        var detail = await DetailAsync(manager, id);
        Assert.Equal("Invited", detail.GetProperty("state").GetString());
        Assert.Equal(renewed.GetProperty("expiresAtUtc").GetDateTime(), detail.GetProperty("invitationExpiresAtUtc").GetDateTime());
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            var notice = Assert.Single(await EmailsAsync(services, email, outbox));
            Assert.Contains("te invitó como operador de", notice.Subject, StringComparison.Ordinal);
            var ev = await services.GetRequiredService<ApplicationDbContext>().SecurityEvents.AsNoTracking()
                .SingleAsync(item => item.Type == SecurityEventType.PlatformOperatorInvitationResent && item.TargetAccountId == id, Ct);
            Assert.Equal(managerId, ev.ActorId);
            Assert.False(string.IsNullOrWhiteSpace(ev.Reason));
            using var changes = JsonDocument.Parse(ev.Changes);
            Assert.True(changes.RootElement.TryGetProperty("invitation.expiresAtUtc", out _));
        }

        using (var accepted = await SignInAsync(invitedClient, email, PlatformReturn))
            Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        Assert.Equal("Active", (await DetailAsync(manager, id)).GetProperty("state").GetString());

        // Una invitación aceptada ya no se reenvía ni se revoca.
        using var again = await SendAsync(manager, HttpMethod.Post, $"/api/platform/operators/{id:D}/invitation/resend",
            null, Guid.NewGuid().ToString("D"));
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal(PlatformOperatorErrors.NoPendingInvitationCode, (await JsonAsync(again)).GetProperty("code").GetString());
        using var revokeAccepted = await manager.DeleteAsync($"/api/platform/operators/{id:D}/invitation", Ct);
        Assert.Equal(HttpStatusCode.Conflict, revokeAccepted.StatusCode);
    }

    [Fact]
    public async Task Revoking_an_invitation_frees_the_email_and_takes_the_account_out_of_accounts()
    {
        var (manager, managerId) = await OperatorAsync(PlatformOperators.All);
        var (id, email) = await InviteAsync(manager, [PlatformPermissions.Audit.Read]);
        Assert.Equal("Invited", (await DetailAsync(manager, id)).GetProperty("state").GetString());

        using var revoked = await manager.DeleteAsync($"/api/platform/operators/{id:D}/invitation", Ct);
        Assert.Equal(HttpStatusCode.NoContent, revoked.StatusCode);

        using (var gone = await manager.GetAsync($"/api/platform/accounts/{id:D}", Ct))
            Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);
        Assert.Empty(await RowsAsync(manager, $"search={Uri.EscapeDataString(email)}"));
        Assert.Empty(await RowsAsync(manager, $"state=Deleted&search={Uri.EscapeDataString(email)}"));
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.False(await db.LoginMethods.AnyAsync(method => method.UserId == id || method.Value == email, Ct));
            Assert.False(await db.PlatformPermissionAssignments.AnyAsync(assignment => assignment.UserId == id, Ct));
            var user = await db.Users.AsNoTracking().SingleAsync(item => item.Id == id, Ct);
            Assert.Equal(UserStatus.Deleted, user.Status);
            Assert.False(user.IsPlatformOperator);
            Assert.Null(user.Email);
            var ev = await db.SecurityEvents.AsNoTracking()
                .SingleAsync(item => item.Type == SecurityEventType.PlatformOperatorInvitationRevoked && item.TargetAccountId == id, Ct);
            Assert.Equal(managerId, ev.ActorId);
            Assert.False(string.IsNullOrWhiteSpace(ev.Reason));
            using var changes = JsonDocument.Parse(ev.Changes);
            Assert.False(changes.RootElement.GetProperty(PlatformPermissions.Audit.Read).GetProperty("after").GetBoolean());
        }

        using (var twice = await manager.DeleteAsync($"/api/platform/operators/{id:D}/invitation", Ct))
            Assert.Equal(HttpStatusCode.NotFound, twice.StatusCode);
        using (var resendRevoked = await SendAsync(manager, HttpMethod.Post,
                   $"/api/platform/operators/{id:D}/invitation/resend", null, Guid.NewGuid().ToString("D")))
            Assert.Equal(HttpStatusCode.NotFound, resendRevoked.StatusCode);

        // El correo queda libre: se lo puede volver a invitar.
        using var invitedAgain = await PlatformJourney.PostAsync(manager, "/api/platform/operators/invitations",
            new { email, permissions = new[] { PlatformPermissions.Audit.Read } }, Ct);
        Assert.Equal(HttpStatusCode.Created, invitedAgain.StatusCode);
        Assert.NotEqual(id, (await JsonAsync(invitedAgain)).GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task A_pending_invitation_only_takes_resend_or_revoke_and_needs_operators_manage()
    {
        var (manager, _) = await OperatorAsync(PlatformOperators.All);
        var (id, _) = await InviteAsync(manager, [PlatformPermissions.Operators.Manage]);
        var detail = await DetailAsync(manager, id);
        var version = detail.GetProperty("version").GetUInt32();
        var operatorVersion = detail.GetProperty("operatorVersion").GetUInt32();

        foreach (var (method, path, body) in new (HttpMethod, string, object)[]
                 {
                     (HttpMethod.Post, $"/api/platform/accounts/{id:D}/suspend", new { version, reason = "Prueba" }),
                     (HttpMethod.Post, $"/api/platform/accounts/{id:D}/revoke-sessions", new { version, reason = "Prueba" }),
                     (HttpMethod.Post, $"/api/platform/accounts/{id:D}/deletion", new { version, reason = "Prueba" }),
                     (HttpMethod.Put, $"/api/platform/accounts/{id:D}/operator",
                         new { version = operatorVersion, reason = "Prueba", permissions = new[] { PlatformPermissions.Audit.Read } }),
                     (HttpMethod.Delete, $"/api/platform/accounts/{id:D}/operator", new { version = operatorVersion, reason = "Prueba" }),
                 })
        {
            using var refused = await SendAsync(manager, method, path, body, Guid.NewGuid().ToString("D"));
            Assert.True(refused.StatusCode == HttpStatusCode.Conflict, $"{method} {path}: {(int)refused.StatusCode}");
            Assert.Equal(PlatformOperatorErrors.InvitationPendingCode, (await JsonAsync(refused)).GetProperty("code").GetString());
        }
        Assert.Equal("Invited", (await DetailAsync(manager, id)).GetProperty("state").GetString());

        var (reader, _) = await OperatorAsync([PlatformPermissions.Accounts.Read, PlatformPermissions.Accounts.Manage]);
        using (var resend = await SendAsync(reader, HttpMethod.Post, $"/api/platform/operators/{id:D}/invitation/resend",
                   null, Guid.NewGuid().ToString("D")))
            Assert.Equal(HttpStatusCode.Forbidden, resend.StatusCode);
        using (var revoke = await reader.DeleteAsync($"/api/platform/operators/{id:D}/invitation", Ct))
            Assert.Equal(HttpStatusCode.Forbidden, revoke.StatusCode);
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
        var (id, email) = await InviteAsync(manager, [PlatformPermissions.Tenants.Read]);
        using (var invitedClient = Client())
        using (var accepted = await SignInAsync(invitedClient, email, PlatformReturn))
            Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
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

    private DateTime Now() => factory.Services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;

    private async Task<(HttpClient Client, Guid Id)> OperatorAsync(IReadOnlyCollection<string> permissions)
    {
        var client = Client();
        var account = await AccountJourney.RegisterAsync(factory, client, Ct);
        await PlatformOperators.GrantAsync(factory.Services, account.UserId, permissions, Ct);
        await AccountJourney.AuthorizeAsync(client, "platform", Ct);
        return (client, account.UserId);
    }

    private static async Task<(Guid Id, string Email)> InviteAsync(HttpClient manager, string[] permissions)
    {
        var email = $"operador-{Guid.NewGuid():N}@example.test";
        using var invited = await PlatformJourney.PostAsync(manager, "/api/platform/operators/invitations",
            new { email, permissions }, Ct);
        Assert.Equal(HttpStatusCode.Created, invited.StatusCode);
        return ((await JsonAsync(invited)).GetProperty("id").GetGuid(), email);
    }

    /// <summary>
    /// Ingresa con código; la respuesta de la verificación queda para ver su resultado. Los códigos anteriores del
    /// correo se corren una hora para que la espera entre pedidos no frene el nuevo.
    /// </summary>
    private async Task<HttpResponseMessage> SignInAsync(HttpClient client, string email, string returnUrl)
    {
        await using (var connection = new NpgsqlConnection(factory.AdminConnectionString))
        {
            await connection.OpenAsync(Ct);
            await using var command = new NpgsqlCommand("""
                UPDATE identity."LoginCodes" SET "CreatedAtUtc" = "CreatedAtUtc" - interval '1 hour'
                WHERE "Destination" = @email
                """, connection);
            command.Parameters.AddWithValue("email", email);
            await command.ExecuteNonQueryAsync(Ct);
        }
        using var requested = await AccountJourney.PostAsync(client, "/api/auth/login-code", new { email }, Ct);
        Assert.Equal(HttpStatusCode.Accepted, requested.StatusCode);
        var code = await PickupCodeReader.ReadAsync(factory.Services, email, Ct);
        return await AccountJourney.PostAsync(client, "/api/auth/login-code/verify", new { email, code, returnUrl }, Ct);
    }

    private async Task RenameAsync(Guid accountId, string displayName)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Users
            .Where(user => user.Id == accountId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(user => user.DisplayName, displayName), Ct));
    }

    /// <summary>Lleva el vencimiento de la invitación a ayer, como si hubieran pasado los 7 días.</summary>
    private async Task ExpireAsync(Guid accountId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var expiredAtUtc = Now().AddDays(-1);
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Users
            .Where(user => user.Id == accountId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(user => user.OperatorInvitationExpiresAtUtc, expiredAtUtc), Ct));
    }

    private static string AuthorizePath(string access) =>
        "/connect/authorize?client_id=web&response_type=code" +
        "&redirect_uri=https%3A%2F%2Flocalhost%3A5174%2Fauth%2Fcallback&scope=openid%20profile%20email%20api" +
        "&code_challenge=AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA&code_challenge_method=S256&access=" + access;

    private static Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method, string path, object? body,
        string key)
    {
        var request = new HttpRequestMessage(method, path);
        if (body is not null) request.Content = JsonContent.Create(body);
        request.Headers.Add("Idempotency-Key", key);
        return client.SendAsync(request, Ct);
    }

    private static async Task<JsonElement> DetailAsync(HttpClient client, Guid id)
    {
        using var response = await client.GetAsync($"/api/platform/accounts/{id:D}", Ct);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct)).RootElement.Clone();
    }

    private static async Task<JsonElement[]> RowsAsync(HttpClient client, string query)
    {
        using var response = await client.GetAsync($"/api/platform/accounts/paged?{query}", Ct);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
        return [.. (await JsonAsync(response)).GetProperty("items").EnumerateArray()];
    }

    private static async Task<int> StateCountAsync(HttpClient client, string email, string state)
    {
        using var response = await client.GetAsync(
            $"/api/platform/accounts/filter-counts?search={Uri.EscapeDataString(email)}", Ct);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
        return (await JsonAsync(response)).GetProperty("states").EnumerateArray()
            .Where(item => item.GetProperty("state").GetString() == state)
            .Select(item => item.GetProperty("count").GetInt32()).SingleOrDefault();
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
