using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
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

/// <summary>Suspender, reactivar y cerrar sesiones de una cuenta desde Plataforma, con motivo, versión y evento.</summary>
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
                new { version = 1u, reason = "Sin sesión" }, Ct);
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

        var original = await VersionAsync(platform, account.UserId);
        using var withoutPermission = await PlatformJourney.PostAsync(reader, path + "/suspend",
            new { version = original, reason = "Sin permiso" }, Ct);
        Assert.Equal(HttpStatusCode.Forbidden, withoutPermission.StatusCode);
        using var withoutReason = await PlatformJourney.PostAsync(platform, path + "/suspend", new { version = original, reason = " " }, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, withoutReason.StatusCode);
        using var withoutVersion = await PlatformJourney.PostAsync(platform, path + "/suspend", new { reason = "Denuncia" }, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, withoutVersion.StatusCode);
        using var missing = await PlatformJourney.PostAsync(platform, $"/api/platform/accounts/{Guid.NewGuid():D}/suspend",
            new { version = original, reason = "Denuncia" }, Ct);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);

        using var suspended = await PlatformJourney.PostAsync(platform, path + "/suspend",
            new { version = original, reason = "Denuncia de uso indebido." }, Ct);
        Assert.Equal(HttpStatusCode.NoContent, suspended.StatusCode);
        using var personAfterSuspension = await person.GetAsync("/api/me", Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, personAfterSuspension.StatusCode);
        Assert.Equal("Suspended", (await DetailAsync(platform, account.UserId)).GetProperty("state").GetString());
        using var twice = await PlatformJourney.PostAsync(platform, path + "/suspend",
            new { version = await VersionAsync(platform, account.UserId), reason = "Otra vez" }, Ct);
        Assert.Equal(HttpStatusCode.Conflict, twice.StatusCode);
        Assert.Equal(UserErrors.InvalidTransitionCode, await CodeAsync(twice));

        using var stale = await PlatformJourney.PostAsync(platform, path + "/reactivate",
            new { version = original, reason = "Versión vieja" }, Ct);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using var reactivated = await PlatformJourney.PostAsync(platform, path + "/reactivate",
            new { version = await VersionAsync(platform, account.UserId), reason = "Revisión terminada." }, Ct);
        Assert.Equal(HttpStatusCode.NoContent, reactivated.StatusCode);
        Assert.Equal("Active", (await DetailAsync(platform, account.UserId)).GetProperty("state").GetString());
        await SignInAgainAsync(person, account);
        using var personAfterReactivation = await person.GetAsync("/api/me", Ct);
        Assert.Equal(HttpStatusCode.OK, personAfterReactivation.StatusCode);

        var beforeRevocation = await VersionAsync(platform, account.UserId);
        var revokeKey = Guid.NewGuid().ToString("D");
        var revokeBody = new { version = beforeRevocation, reason = "Pidió cerrar sus sesiones." };
        using var revoked = await PostWithKeyAsync(platform, path + "/revoke-sessions", revokeBody, revokeKey);
        Assert.Equal(HttpStatusCode.NoContent, revoked.StatusCode);
        using var replay = await PostWithKeyAsync(platform, path + "/revoke-sessions", revokeBody, revokeKey);
        Assert.Equal("true", Assert.Single(replay.Headers.GetValues("Idempotent-Replayed")));
        using var personAfterRevocation = await person.GetAsync("/api/me", Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, personAfterRevocation.StatusCode);
        Assert.Equal("Active", (await DetailAsync(platform, account.UserId)).GetProperty("state").GetString());
        Assert.NotEqual(beforeRevocation, await VersionAsync(platform, account.UserId));

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
            new { version = await VersionAsync(platform, user.Id), reason = "Se levanta la suspensión." }, Ct);

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
            new { version = await VersionAsync(support, managerAccount.UserId), reason = "Revisión" }, Ct);
        Assert.Equal(HttpStatusCode.Conflict, lastManager.StatusCode);
        Assert.Equal(PlatformOperatorErrors.LastManagerCode, await CodeAsync(lastManager));
        Assert.Equal("Active", (await DetailAsync(support, managerAccount.UserId)).GetProperty("state").GetString());

        using var self = await PlatformJourney.PostAsync(support, $"/api/platform/accounts/{supportAccount.UserId:D}/suspend",
            new { version = await VersionAsync(support, supportAccount.UserId), reason = "Prueba" }, Ct);
        Assert.Equal(HttpStatusCode.Forbidden, self.StatusCode);
        Assert.Equal(UserErrors.CannotModifySelfCode, await CodeAsync(self));

        using var second = Client(isolated);
        var secondManager = await AccountJourney.RegisterAsync(isolated, second, Ct);
        await PlatformOperators.GrantAsync(isolated.Services, secondManager.UserId, [PlatformPermissions.Operators.Manage], Ct);
        using var allowed = await PlatformJourney.PostAsync(support, path,
            new { version = await VersionAsync(support, managerAccount.UserId), reason = "Revisión" }, Ct);
        Assert.Equal(HttpStatusCode.NoContent, allowed.StatusCode);
    }

    private static HttpClient Client(ApiFactory api) =>
        api.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });

    private static async Task<JsonElement> DetailAsync(HttpClient client, Guid accountId)
    {
        using var response = await client.GetAsync($"/api/platform/accounts/{accountId:D}", Ct);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct)).RootElement.Clone();
    }

    private static async Task<uint> VersionAsync(HttpClient client, Guid accountId) =>
        (await DetailAsync(client, accountId)).GetProperty("version").GetUInt32();

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

    // La suspensión renovó el sello de seguridad: hay que volver a ingresar con un código nuevo.
    private async Task SignInAgainAsync(HttpClient client, AccountJourney account)
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
        using var verified = await AccountJourney.PostAsync(client, "/api/auth/login-code/verify",
            new { email = account.Email.Value, code, returnUrl = "/invitacion" }, Ct);
        Assert.Equal(HttpStatusCode.OK, verified.StatusCode);
        await AccountJourney.AuthorizeAsync(client, "consumer", Ct);
    }
}
