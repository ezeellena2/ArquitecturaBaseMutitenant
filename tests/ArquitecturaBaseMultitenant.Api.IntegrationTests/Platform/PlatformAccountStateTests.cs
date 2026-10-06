using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Platform;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Infrastructure.Identity;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Platform;

/// <summary>
/// Suspender, reactivar y cerrar sesiones de una cuenta desde Plataforma, con motivo y evento. No llevan versión: las
/// protege el estado de cada transición, bajo el lock de la cuenta.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class PlatformAccountStateTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Suspension_revokes_every_session_reactivation_restores_access_and_revoke_keeps_the_state()
    {
        using var anonymous = Client(factory);
        foreach (var action in new[] { "suspend", "reactivate", "revoke-sessions" })
        {
            using var response = await PlatformJourney.PostAsync(anonymous, $"/api/platform/accounts/{Guid.NewGuid():D}/{action}",
                new { reason = "Sin sesión" }, Ct);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        using var platform = Client(factory);
        var operatorAccount = await AccountJourney.RegisterAsync(factory, platform, Ct);
        await PlatformOperators.GrantAsync(factory.Services, operatorAccount.UserId, PlatformOperators.All, Ct);
        await AccountJourney.AuthorizeAsync(platform, "platform", Ct);
        using var reader = Client(factory);
        var readerAccount = await AccountJourney.RegisterAsync(factory, reader, Ct);
        await PlatformOperators.GrantAsync(factory.Services, readerAccount.UserId, PlatformOperators.Reduced, Ct);
        await AccountJourney.AuthorizeAsync(reader, "platform", Ct);
        using var person = Client(factory);
        var account = await AccountJourney.RegisterAsync(factory, person, Ct);
        var path = $"/api/platform/accounts/{account.UserId:D}";

        using var withoutPermission = await PlatformJourney.PostAsync(reader, path + "/suspend",
            new { reason = "Sin permiso" }, Ct);
        Assert.Equal(HttpStatusCode.Forbidden, withoutPermission.StatusCode);
        using var withoutReason = await PlatformJourney.PostAsync(platform, path + "/suspend", new { reason = " " }, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, withoutReason.StatusCode);
        using var missing = await PlatformJourney.PostAsync(platform, $"/api/platform/accounts/{Guid.NewGuid():D}/suspend",
            new { reason = "Denuncia" }, Ct);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);

        using var suspended = await PlatformJourney.PostAsync(platform, path + "/suspend",
            new { reason = "Denuncia de uso indebido." }, Ct);
        Assert.Equal(HttpStatusCode.NoContent, suspended.StatusCode);
        using var personAfterSuspension = await person.GetAsync("/api/me", Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, personAfterSuspension.StatusCode);
        var suspendedDetail = await DetailAsync(platform, account.UserId);
        Assert.Equal("Suspended", suspendedDetail.GetProperty("state").GetString());
        Assert.Equal("Denuncia de uso indebido.", suspendedDetail.GetProperty("lastStateChange").GetProperty("reason").GetString());
        using var twice = await PlatformJourney.PostAsync(platform, path + "/suspend",
            new { reason = "Otra vez" }, Ct);
        Assert.Equal(HttpStatusCode.Conflict, twice.StatusCode);
        Assert.Equal(UserErrors.InvalidTransitionCode, await CodeAsync(twice));

        using var reactivated = await PlatformJourney.PostAsync(platform, path + "/reactivate",
            new { reason = "Revisión terminada." }, Ct);
        Assert.Equal(HttpStatusCode.NoContent, reactivated.StatusCode);
        Assert.Equal("Active", (await DetailAsync(platform, account.UserId)).GetProperty("state").GetString());
        // Reactivar solo sirve para una cuenta suspendida: repetirlo da el error de estado, no un conflicto de versión.
        using var reactivatedTwice = await PlatformJourney.PostAsync(platform, path + "/reactivate",
            new { reason = "Otra vez" }, Ct);
        Assert.Equal(HttpStatusCode.Conflict, reactivatedTwice.StatusCode);
        Assert.Equal(UserErrors.InvalidTransitionCode, await CodeAsync(reactivatedTwice));
        await SignInAgainAsync(person, account);
        using var personAfterReactivation = await person.GetAsync("/api/me", Ct);
        Assert.Equal(HttpStatusCode.OK, personAfterReactivation.StatusCode);

        var revokeKey = Guid.NewGuid().ToString("D");
        var revokeBody = new { reason = "Pidió cerrar sus sesiones." };
        using var revoked = await PostWithKeyAsync(platform, path + "/revoke-sessions", revokeBody, revokeKey);
        Assert.Equal(HttpStatusCode.NoContent, revoked.StatusCode);
        using var replay = await PostWithKeyAsync(platform, path + "/revoke-sessions", revokeBody, revokeKey);
        Assert.Equal("true", Assert.Single(replay.Headers.GetValues("Idempotent-Replayed")));
        using var personAfterRevocation = await person.GetAsync("/api/me", Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, personAfterRevocation.StatusCode);
        Assert.Equal("Active", (await DetailAsync(platform, account.UserId)).GetProperty("state").GetString());

        await using var scope = factory.Services.CreateAsyncScope();
        var events = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().SecurityEvents.AsNoTracking()
            .Where(item => item.TargetAccountId == account.UserId && item.ActorKind == AuditActorKind.PlatformOperator)
            .OrderBy(item => item.OccurredAtUtc).ToArrayAsync(Ct);
        Assert.Equal([SecurityEventType.AccountSuspended, SecurityEventType.AccountReactivated, SecurityEventType.AccountSessionsRevoked],
            events.Select(item => item.Type));
        Assert.All(events, item => Assert.Equal(operatorAccount.UserId, item.ActorId));
        Assert.Equal("Denuncia de uso indebido.", events[0].Reason);
        Assert.Contains("\"status\"", events[0].Changes);
        Assert.Contains("Suspended", events[0].Changes);
        Assert.Equal("{}", events[2].Changes);
        Assert.All(events, item => Assert.DoesNotContain(account.Email.Value, item.Changes));
        Assert.Equal(events[0].OccurredAtUtc, suspendedDetail.GetProperty("lastStateChange").GetProperty("atUtc").GetDateTime());
        var last = (await DetailAsync(platform, account.UserId)).GetProperty("lastStateChange");
        Assert.Equal(events[1].OccurredAtUtc, last.GetProperty("atUtc").GetDateTime());
        Assert.Equal("Revisión terminada.", last.GetProperty("reason").GetString());
        Assert.Equal("Reactivó la cuenta", last.GetProperty("action").GetString());
    }

    [Fact]
    public async Task Reactivating_a_suspended_account_with_a_scheduled_deletion_returns_it_to_pending_deletion()
    {
        using var platform = Client(factory);
        var operatorAccount = await AccountJourney.RegisterAsync(factory, platform, Ct);
        await PlatformOperators.GrantAsync(factory.Services, operatorAccount.UserId, PlatformOperators.All, Ct);
        await AccountJourney.AuthorizeAsync(platform, "platform", Ct);
        var user = ApplicationUser.Create("Baja pendiente", "es-AR", "America/Argentina/Buenos_Aires").Value;
        Assert.True(user.Suspend().IsSuccess);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            var now = services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
            Assert.True(user.RequestDeletion("Pedido legal", now, 30, operatorAccount.UserId).IsSuccess);
            await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ =>
            {
                services.GetRequiredService<ApplicationDbContext>().Users.Add(user);
                return Task.FromResult(Result.Success());
            }, CommitPolicy.OnSuccess, Ct);
        }

        using var reactivated = await PlatformJourney.PostAsync(platform, $"/api/platform/accounts/{user.Id:D}/reactivate",
            new { reason = "Se levanta la suspensión." }, Ct);

        Assert.Equal(HttpStatusCode.NoContent, reactivated.StatusCode);
        var detail = await DetailAsync(platform, user.Id);
        Assert.Equal("PendingDeletion", detail.GetProperty("state").GetString());
        Assert.True(detail.GetProperty("deletion").GetProperty("requestedByPlatform").GetBoolean());
    }

    [Fact]
    public async Task The_last_active_operator_manager_stays_and_an_operator_cannot_suspend_itself()
    {
        await using var isolated = new ApiFactory();
        await isolated.InitializeAsync();
        using var manager = Client(isolated);
        var managerAccount = await AccountJourney.RegisterAsync(isolated, manager, Ct);
        await PlatformOperators.GrantAsync(isolated.Services, managerAccount.UserId, PlatformOperators.All, Ct);
        using var support = Client(isolated);
        var supportAccount = await AccountJourney.RegisterAsync(isolated, support, Ct);
        await PlatformOperators.GrantAsync(isolated.Services, supportAccount.UserId,
            [PlatformPermissions.Accounts.Read, PlatformPermissions.Accounts.Manage], Ct);
        await AccountJourney.AuthorizeAsync(support, "platform", Ct);
        // Una invitación pendiente con operators.manage no cuenta: esa persona todavía no entró (ADR 0042).
        await AccountJourney.AuthorizeAsync(manager, "platform", Ct);
        using (var pending = await PlatformJourney.PostAsync(manager, "/api/platform/operators/invitations",
                   new { email = $"pendiente-{Guid.NewGuid():N}@example.test", permissions = new[] { PlatformPermissions.Operators.Manage } }, Ct))
            Assert.Equal(HttpStatusCode.Created, pending.StatusCode);
        var path = $"/api/platform/accounts/{managerAccount.UserId:D}/suspend";

        using var lastManager = await PlatformJourney.PostAsync(support, path,
            new { reason = "Revisión" }, Ct);
        Assert.Equal(HttpStatusCode.Conflict, lastManager.StatusCode);
        Assert.Equal(PlatformOperatorErrors.LastManagerCode, await CodeAsync(lastManager));
        Assert.Equal("Active", (await DetailAsync(support, managerAccount.UserId)).GetProperty("state").GetString());

        using var self = await PlatformJourney.PostAsync(support, $"/api/platform/accounts/{supportAccount.UserId:D}/suspend",
            new { reason = "Prueba" }, Ct);
        Assert.Equal(HttpStatusCode.Forbidden, self.StatusCode);
        Assert.Equal(UserErrors.CannotModifySelfCode, await CodeAsync(self));

        using var second = Client(isolated);
        var secondManager = await AccountJourney.RegisterAsync(isolated, second, Ct);
        await PlatformOperators.GrantAsync(isolated.Services, secondManager.UserId, [PlatformPermissions.Operators.Manage], Ct);
        using var allowed = await PlatformJourney.PostAsync(support, path,
            new { reason = "Revisión" }, Ct);
        Assert.Equal(HttpStatusCode.NoContent, allowed.StatusCode);
    }

    /// <summary>
    /// Las acciones sobre una cuenta no llevan versión: la fila de la cuenta cambia con un ingreso, con los intentos
    /// fallidos y su reinicio, con el contacto principal y con el sello de seguridad, y su versión daría un 409 falso.
    /// Cada acción llega con una ficha leída antes de que la fila cambiara y no choca (docs/rules/concurrencia.md).
    /// </summary>
    [Fact]
    public async Task A_write_to_the_account_between_the_sheet_and_the_action_does_not_conflict()
    {
        using var platform = Client(factory);
        var operatorAccount = await AccountJourney.RegisterAsync(factory, platform, Ct);
        await PlatformOperators.GrantAsync(factory.Services, operatorAccount.UserId, PlatformOperators.All, Ct);
        await AccountJourney.AuthorizeAsync(platform, "platform", Ct);
        using var person = Client(factory);
        var account = await AccountJourney.RegisterAsync(factory, person, Ct);
        var path = $"/api/platform/accounts/{account.UserId:D}";

        // Suspender: con la ficha abierta, la persona escribe mal el código y después ingresa.
        var seen = await VersionAsync(platform, account.UserId);
        await SignInAgainAsync(person, account, mistypeFirst: true);
        Assert.NotEqual(seen, await VersionAsync(platform, account.UserId));
        using var suspended = await PlatformJourney.PostAsync(platform, path + "/suspend",
            new { reason = "Denuncia de uso indebido." }, Ct);
        Assert.Equal(HttpStatusCode.NoContent, suspended.StatusCode);
        Assert.Equal("Suspended", (await DetailAsync(platform, account.UserId)).GetProperty("state").GetString());

        // Reactivar: la fila cambia otra vez antes de actuar.
        seen = await VersionAsync(platform, account.UserId);
        await WriteToTheAccountAsync(account);
        Assert.NotEqual(seen, await VersionAsync(platform, account.UserId));
        using var reactivated = await PlatformJourney.PostAsync(platform, path + "/reactivate",
            new { reason = "Revisión terminada." }, Ct);
        Assert.Equal(HttpStatusCode.NoContent, reactivated.StatusCode);
        Assert.Equal("Active", (await DetailAsync(platform, account.UserId)).GetProperty("state").GetString());

        // Cerrar sesiones.
        seen = await VersionAsync(platform, account.UserId);
        await WriteToTheAccountAsync(account);
        Assert.NotEqual(seen, await VersionAsync(platform, account.UserId));
        using var revoked = await PlatformJourney.PostAsync(platform, path + "/revoke-sessions",
            new { reason = "Pidió cerrar sus sesiones." }, Ct);
        Assert.Equal(HttpStatusCode.NoContent, revoked.StatusCode);

        // La baja.
        seen = await VersionAsync(platform, account.UserId);
        await WriteToTheAccountAsync(account);
        Assert.NotEqual(seen, await VersionAsync(platform, account.UserId));
        using var requested = await PlatformJourney.PostAsync(platform, path + "/deletion",
            new { reason = "Pedido legal recibido por correo." }, Ct);
        Assert.Equal(HttpStatusCode.NoContent, requested.StatusCode);
        Assert.Equal("PendingDeletion", (await DetailAsync(platform, account.UserId)).GetProperty("state").GetString());
    }

    /// <summary>
    /// Sin versión, lo único que separa dos acciones a la vez es el estado, bajo el lock de la cuenta: una gana y la otra
    /// recibe el error de estado, nunca un conflicto de versión. Cerrar sesiones no cambia el estado: repetirla no choca.
    /// </summary>
    [Fact]
    public async Task Two_contradictory_actions_at_once_one_wins_and_the_other_gets_the_state_error()
    {
        using var platform = Client(factory);
        var operatorAccount = await AccountJourney.RegisterAsync(factory, platform, Ct);
        await PlatformOperators.GrantAsync(factory.Services, operatorAccount.UserId, PlatformOperators.All, Ct);
        await AccountJourney.AuthorizeAsync(platform, "platform", Ct);
        using var person = Client(factory);
        var account = await AccountJourney.RegisterAsync(factory, person, Ct);
        var path = $"/api/platform/accounts/{account.UserId:D}";

        var revocations = await TwoAtOnceAsync(platform, path + "/revoke-sessions");
        Assert.All(revocations, response => Assert.Equal(HttpStatusCode.NoContent, response.StatusCode));
        foreach (var revocation in revocations) revocation.Dispose();

        await AssertOneWinsAsync(platform, path + "/suspend", UserErrors.InvalidTransitionCode);
        Assert.Equal("Suspended", (await DetailAsync(platform, account.UserId)).GetProperty("state").GetString());
        await AssertOneWinsAsync(platform, path + "/reactivate", UserErrors.InvalidTransitionCode);
        Assert.Equal("Active", (await DetailAsync(platform, account.UserId)).GetProperty("state").GetString());
        await AssertOneWinsAsync(platform, path + "/deletion", "Legal.AccountDeletion.AlreadyPending");
        Assert.Equal("PendingDeletion", (await DetailAsync(platform, account.UserId)).GetProperty("state").GetString());
    }

    private static HttpClient Client(ApiFactory api) =>
        api.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });

    /// <summary>Dos pedidos iguales a la vez, cada uno con su clave de idempotencia.</summary>
    private static Task<HttpResponseMessage[]> TwoAtOnceAsync(HttpClient platform, string path) =>
        Task.WhenAll(
            PlatformJourney.PostAsync(platform, path, new { reason = "Primer pedido." }, Ct),
            PlatformJourney.PostAsync(platform, path, new { reason = "Segundo pedido." }, Ct));

    private static async Task AssertOneWinsAsync(HttpClient platform, string path, string loserCode)
    {
        var both = await TwoAtOnceAsync(platform, path);
        try
        {
            Assert.Equal([HttpStatusCode.NoContent, HttpStatusCode.Conflict], both.Select(response => response.StatusCode).Order());
            Assert.Equal(loserCode, await CodeAsync(both.Single(response => response.StatusCode == HttpStatusCode.Conflict)));
        }
        finally
        {
            foreach (var response in both) response.Dispose();
        }
    }

    /// <summary>Lo que otro ingreso le hace a la fila de la cuenta: cuenta un intento fallido, lo reinicia y fija el contacto.</summary>
    private async Task WriteToTheAccountAsync(AccountJourney account)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
        {
            await services.GetRequiredService<IUserRepository>().SetPrimaryContactAsync(account.UserId, account.Email, null, ct);
            var signIn = services.GetRequiredService<ISignInService>();
            await signIn.RegisterFailedAttemptAsync(account.UserId, ct);
            await signIn.ResetFailedAttemptsAsync(account.UserId, ct);
            return Result.Success();
        }, CommitPolicy.OnSuccess, Ct);
    }

    private static async Task<JsonElement> DetailAsync(HttpClient client, Guid accountId)
    {
        using var response = await client.GetAsync($"/api/platform/accounts/{accountId:D}", Ct);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct)).RootElement.Clone();
    }

    private async Task<uint> VersionAsync(HttpClient client, Guid accountId)
    {
        Assert.False((await DetailAsync(client, accountId)).TryGetProperty("version", out _));
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Users.AsNoTracking()
            .Where(user => user.Id == accountId).Select(user => user.Version).SingleAsync(Ct);
    }

    private static async Task<string?> CodeAsync(HttpResponseMessage response)
    {
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        return problem.RootElement.GetProperty("code").GetString();
    }

    private static async Task<HttpResponseMessage> PostWithKeyAsync(HttpClient client, string path, object body, string key)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("Idempotency-Key", key);
        return await client.SendAsync(request, Ct);
    }

    // La suspensión renovó el sello de seguridad: hay que volver a ingresar con un código nuevo. Con mistypeFirst, la
    // persona escribe mal el código una vez antes del bueno: cuenta un intento fallido y lo reinicia al ingresar.
    private async Task SignInAgainAsync(HttpClient client, AccountJourney account, bool mistypeFirst = false)
    {
        await using (var connection = new NpgsqlConnection(factory.AdminConnectionString))
        {
            await connection.OpenAsync(Ct);
            await using var command = new NpgsqlCommand("""
                UPDATE identity."LoginCodes" SET "CreatedAtUtc" = "CreatedAtUtc" - interval '1 hour'
                WHERE "Destination" = @email
                """, connection);
            command.Parameters.AddWithValue("email", account.Email.Value);
            await command.ExecuteNonQueryAsync(Ct);
        }
        client.DefaultRequestHeaders.Authorization = null;
        using var requested = await AccountJourney.PostAsync(client, "/api/auth/login-code", new { email = account.Email.Value }, Ct);
        Assert.Equal(HttpStatusCode.Accepted, requested.StatusCode);
        var code = await PickupCodeReader.ReadAsync(factory.Services, account.Email.Value, Ct);
        if (mistypeFirst)
        {
            using var mistyped = await AccountJourney.PostAsync(client, "/api/auth/login-code/verify",
                new { email = account.Email.Value, code = code == "000000" ? "000001" : "000000", returnUrl = "/invitacion" }, Ct);
            Assert.False(mistyped.IsSuccessStatusCode);
        }

        using var verified = await AccountJourney.PostAsync(client, "/api/auth/login-code/verify",
            new { email = account.Email.Value, code, returnUrl = "/invitacion" }, Ct);
        Assert.Equal(HttpStatusCode.OK, verified.StatusCode);
        await AccountJourney.AuthorizeAsync(client, "consumer", Ct);
    }
}
