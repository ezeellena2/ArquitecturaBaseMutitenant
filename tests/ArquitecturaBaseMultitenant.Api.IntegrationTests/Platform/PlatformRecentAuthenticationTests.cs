using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Common.Exceptions;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Platform;

/// <summary>
/// La prueba reciente de las acciones sensibles de Plataforma (E9, P1: 10 minutos). Un 403 sin prueba no reserva ni
/// consume la clave de idempotencia: después de confirmar, la misma clave con el mismo cuerpo ejecuta una sola vez. La
/// prueba es de una sesión de Plataforma (otra pestaña de esa sesión la comparte, otra sesión no), la falta de permiso
/// responde antes, las lecturas no la piden y la transacción que escribe la vuelve a exigir.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class PlatformRecentAuthenticationTests(ApiFactory factory)
{
    private const string Invitations = "/api/platform/operators/invitations";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Without_a_recent_proof_the_403_keeps_the_key_and_the_same_operation_runs_once_after_confirming()
    {
        var (manager, managerId) = await OperatorAsync(PlatformOperators.All);
        await ExpireProofAsync(managerId);
        var email = $"reciente-{Guid.NewGuid():N}@example.test";
        var body = new { email, permissions = new[] { PlatformPermissions.Tenants.Read } };
        var key = Guid.NewGuid();

        using (var denied = await SendAsync(manager, Invitations, body, key))
        {
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
            var problem = await JsonAsync(denied);
            Assert.Equal(PlatformReauthenticationErrors.RequiredCode, problem.GetProperty("code").GetString());
            Assert.Equal("Confirmá que sos vos para continuar.", problem.GetProperty("detail").GetString());
        }
        Assert.Equal(0, await AccountsByEmailAsync(email));

        var proof = await ReauthenticateAsync(manager, managerId);
        Assert.Equal(TimeSpan.FromMinutes(10), proof.RecentUntilUtc - proof.VerifiedAtUtc);

        Guid invitedId;
        using (var created = await SendAsync(manager, Invitations, body, key))
        {
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            Assert.False(created.Headers.Contains("Idempotent-Replayed"));
            invitedId = (await JsonAsync(created)).GetProperty("id").GetGuid();
        }
        using (var replayed = await SendAsync(manager, Invitations, body, key))
        {
            Assert.Equal(HttpStatusCode.Created, replayed.StatusCode);
            Assert.Equal("true", Assert.Single(replayed.Headers.GetValues("Idempotent-Replayed")));
            Assert.Equal(invitedId, (await JsonAsync(replayed)).GetProperty("id").GetGuid());
        }
        Assert.Equal(1, await AccountsByEmailAsync(email));
    }

    [Fact]
    public async Task A_reauthentication_replay_keeps_its_original_expiry_and_leaves_an_audit_event()
    {
        var (client, userId) = await OperatorAsync([PlatformPermissions.Accounts.Read]);
        var key = Guid.NewGuid();
        var body = new { method = "authenticator", code = await PlatformFactorJourney.CurrentCodeAsync(factory.Services, userId, Ct) };
        JsonElement first;
        using (var confirmed = await SendAsync(client, "/api/platform/reauthentication", body, key))
        {
            Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);
            Assert.True(confirmed.Headers.CacheControl?.NoStore);
            first = await JsonAsync(confirmed);
        }
        using (var replayed = await SendAsync(client, "/api/platform/reauthentication", body, key))
        {
            Assert.Equal(HttpStatusCode.OK, replayed.StatusCode);
            Assert.Equal("true", Assert.Single(replayed.Headers.GetValues("Idempotent-Replayed")));
            var again = await JsonAsync(replayed);
            Assert.Equal(first.GetProperty("verifiedAtUtc").GetDateTime(), again.GetProperty("verifiedAtUtc").GetDateTime());
            Assert.Equal(first.GetProperty("recentUntilUtc").GetDateTime(), again.GetProperty("recentUntilUtc").GetDateTime());
        }

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(1, await db.SecurityEvents.CountAsync(item => item.Type == SecurityEventType.PlatformReauthenticated
            && item.TargetAccountId == userId, Ct));
    }

    [Fact]
    public async Task The_proof_belongs_to_one_platform_session_and_another_tab_of_it_shares_it()
    {
        var (first, userId) = await OperatorAsync(PlatformOperators.All);
        var email = await EmailAsync(userId);
        using var second = Client();
        await PlatformFactorJourney.SignInAsync(factory.Services, second, email, userId, Ct);
        await ExpireProofAsync(userId);

        await ReauthenticateAsync(first, userId);
        using (var allowed = await InviteAsync(first)) Assert.Equal(HttpStatusCode.Created, allowed.StatusCode);
        using (var other = await InviteAsync(second))
            Assert.Equal(PlatformReauthenticationErrors.RequiredCode, (await JsonAsync(other)).GetProperty("code").GetString());

        // Otra pestaña de la misma sesión pide sus tokens con la misma cookie: comparte la prueba.
        await AccountJourney.AuthorizeAsync(first, "platform", Ct);
        using (var tab = await InviteAsync(first)) Assert.Equal(HttpStatusCode.Created, tab.StatusCode);
    }

    [Fact]
    public async Task Missing_permissions_answer_first_and_reads_never_ask_for_the_proof()
    {
        var (reader, readerId) = await OperatorAsync([PlatformPermissions.Accounts.Read, PlatformPermissions.Tenants.Read]);
        await ExpireProofAsync(readerId);
        using (var forbidden = await InviteAsync(reader))
        {
            Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
            Assert.Equal("Http.Forbidden", (await JsonAsync(forbidden)).GetProperty("code").GetString());
        }
        foreach (var path in new[] { "/api/platform/accounts/paged", "/api/platform/tenants/paged" })
        {
            using var read = await reader.GetAsync(path, Ct);
            Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        }
    }

    [Fact]
    public async Task The_writing_transaction_requires_the_proof_again_with_the_session_and_stamp()
    {
        var (_, userId) = await OperatorAsync([PlatformPermissions.Accounts.Read]);
        Guid sessionId;
        await using (var scope = factory.Services.CreateAsyncScope())
            sessionId = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().PlatformSessions
                .Where(session => session.UserId == userId).Select(session => session.Id).SingleAsync(Ct);

        Assert.True((await WriteRequiringProofAsync(sessionId)).IsSuccess);
        await ExpireProofAsync(userId);
        await Assert.ThrowsAsync<PlatformReauthenticationRequiredException>(() => WriteRequiringProofAsync(sessionId));

        // Una prueba vigente con otro sello (se cerraron las sesiones o se restableció el factor) tampoco alcanza.
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var nowUtc = scope.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
            await db.PlatformSessions.Where(session => session.Id == sessionId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(session => session.RecentProofUntilUtc, nowUtc.AddMinutes(5)), Ct);
            Assert.True((await WriteRequiringProofAsync(sessionId)).IsSuccess);
            await db.Users.Where(user => user.Id == userId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(user => user.SecurityStamp, Guid.NewGuid().ToString("N")), Ct);
        }
        await Assert.ThrowsAsync<PlatformReauthenticationRequiredException>(() => WriteRequiringProofAsync(sessionId));
    }

    private async Task<Result> WriteRequiringProofAsync(Guid sessionId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        services.GetRequiredService<IRecentPlatformProofContext>().Require(sessionId);
        return await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(
            _ => Task.FromResult(Result.Success()), CommitPolicy.OnSuccess, Ct);
    }

    private async Task<(DateTime VerifiedAtUtc, DateTime RecentUntilUtc)> ReauthenticateAsync(HttpClient client, Guid userId)
    {
        var body = new { method = "authenticator", code = await PlatformFactorJourney.CurrentCodeAsync(factory.Services, userId, Ct) };
        using var confirmed = await SendAsync(client, "/api/platform/reauthentication", body, Guid.NewGuid());
        Assert.True(confirmed.StatusCode == HttpStatusCode.OK, await confirmed.Content.ReadAsStringAsync(Ct));
        var proof = await JsonAsync(confirmed);
        return (proof.GetProperty("verifiedAtUtc").GetDateTime(), proof.GetProperty("recentUntilUtc").GetDateTime());
    }

    private static Task<HttpResponseMessage> InviteAsync(HttpClient client) => SendAsync(client, Invitations,
        new { email = $"reciente-{Guid.NewGuid():N}@example.test", permissions = new[] { PlatformPermissions.Tenants.Read } },
        Guid.NewGuid());

    private async Task ExpireProofAsync(Guid userId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var nowUtc = scope.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
        await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().PlatformSessions
            .Where(session => session.UserId == userId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(session => session.RecentProofUntilUtc, nowUtc.AddMinutes(-1)), Ct);
    }

    private async Task<int> AccountsByEmailAsync(string email)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().LoginMethods
            .CountAsync(method => method.Value == email, Ct);
    }

    private async Task<string> EmailAsync(Guid userId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().LoginMethods
            .Where(method => method.UserId == userId && method.IsPrimary).Select(method => method.Value).SingleAsync(Ct);
    }

    private async Task<(HttpClient Client, Guid Id)> OperatorAsync(IReadOnlyCollection<string> permissions)
    {
        var client = Client();
        var account = await AccountJourney.RegisterAsync(factory, client, Ct);
        await PlatformOperators.GrantAsync(factory.Services, account.UserId, permissions, Ct);
        await AccountJourney.AuthorizeAsync(client, "platform", Ct);
        return (client, account.UserId);
    }

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, string path, object body, Guid key)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("Idempotency-Key", key.ToString("D"));
        return await client.SendAsync(request, Ct);
    }

    private HttpClient Client() => factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        return document.RootElement.Clone();
    }
}
