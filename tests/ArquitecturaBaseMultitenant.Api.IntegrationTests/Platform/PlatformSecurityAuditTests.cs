using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Platform;

/// <summary>
/// Plataforma › Auditoría: los SecurityEvent y los ingresos de identity.LoginAudits, del más nuevo al más viejo, por
/// cursor estable, con textos traducidos y sin correos, teléfonos, códigos ni enlaces completos. El segundo factor sigue
/// a Auditoria-Plat de la v125: una fila por acción.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class PlatformSecurityAuditTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Events_are_listed_newest_first_by_cursor_with_translated_texts_and_a_detail()
    {
        using var platform = await OperatorAsync([PlatformPermissions.Audit.Read, PlatformPermissions.Accounts.Manage,
            PlatformPermissions.Accounts.Read]);
        using var person = Client();
        var account = await AccountJourney.RegisterAsync(factory, person, Ct);
        var tag = "auditoria-" + Guid.NewGuid().ToString("N")[..8];
        foreach (var (action, reason) in new[] { ("suspend", "1"), ("reactivate", "2"), ("revoke-sessions", "3") })
        {
            using var done = await PlatformJourney.PostAsync(platform, $"/api/platform/accounts/{account.UserId:D}/{action}",
                new { reason = $"{tag} {reason}" }, Ct);
            Assert.Equal(HttpStatusCode.NoContent, done.StatusCode);
        }

        var first = await GetAsync(platform, $"?search={tag}&limit=2");
        var items = first.GetProperty("items").EnumerateArray().ToArray();
        Assert.Equal(2, items.Length);
        Assert.True(first.GetProperty("hasMore").GetBoolean());
        Assert.Equal($"{tag} 3", items[0].GetProperty("reason").GetString());
        Assert.Equal($"{tag} 2", items[1].GetProperty("reason").GetString());
        var second = await GetAsync(platform, $"?search={tag}&limit=2&after={Uri.EscapeDataString(first.GetProperty("nextCursor").GetString()!)}");
        var last = Assert.Single(second.GetProperty("items").EnumerateArray().ToArray());
        Assert.False(second.GetProperty("hasMore").GetBoolean());
        Assert.Equal($"{tag} 1", last.GetProperty("reason").GetString());

        var suspended = last;
        Assert.Equal("accounts", suspended.GetProperty("type").GetString());
        Assert.Equal("AccountSuspended", suspended.GetProperty("action").GetString());
        Assert.Equal("Suspendió la cuenta", suspended.GetProperty("summary").GetString());
        Assert.Equal("Operador", suspended.GetProperty("actorRole").GetString());
        Assert.Equal(account.UserId, suspended.GetProperty("targetAccountId").GetGuid());
        Assert.Equal(account.Email.Value[..2] + "•••@example.test", suspended.GetProperty("target").GetString());
        Assert.Empty((await GetAsync(platform, $"?search={tag}&type=organizations")).GetProperty("items").EnumerateArray());
        Assert.Equal(3, (await GetAsync(platform, $"?search={tag}&type=accounts")).GetProperty("items").GetArrayLength());

        var detail = await GetAsync(platform, "/" + suspended.GetProperty("id").GetGuid().ToString("D"));
        Assert.Equal("Suspendió la cuenta", detail.GetProperty("summary").GetString());
        Assert.Equal($"{tag} 1", detail.GetProperty("reason").GetString());
        var raw = JsonSerializer.Serialize(first) + JsonSerializer.Serialize(detail);
        Assert.DoesNotContain(account.Email.Value, raw, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Sign_ins_come_from_login_audits_without_addresses_or_codes()
    {
        using var platform = await OperatorAsync([PlatformPermissions.Audit.Read]);
        using var person = Client();
        var account = await AccountJourney.RegisterAsync(factory, person, Ct);
        await RecoveryJourney.AgeCodesAsync(factory, account.Email.Value, Ct);
        person.DefaultRequestHeaders.Authorization = null;
        using var requested = await AccountJourney.PostAsync(person, "/api/auth/login-code", new { email = account.Email.Value }, Ct);
        Assert.Equal(HttpStatusCode.Accepted, requested.StatusCode);
        var code = await PickupCodeReader.ReadAsync(factory.Services, account.Email.Value, Ct);
        var wrong = code == "000000" ? "111111" : "000000";
        using var failed = await AccountJourney.PostAsync(person, "/api/auth/login-code/verify",
            new { email = account.Email.Value, code = wrong, returnUrl = "/invitacion" }, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, failed.StatusCode);
        using var verified = await AccountJourney.PostAsync(person, "/api/auth/login-code/verify",
            new { email = account.Email.Value, code, returnUrl = "/invitacion" }, Ct);
        Assert.Equal(HttpStatusCode.OK, verified.StatusCode);

        var page = await GetAsync(platform, $"?type=signins&accountId={account.UserId:D}");
        var items = page.GetProperty("items").EnumerateArray().ToArray();
        Assert.Contains(items, item => item.GetProperty("summary").GetString() == "Ingresó con un código por correo");
        var failure = Assert.Single(items, item => item.GetProperty("action").GetString() == "SignInFailed");
        Assert.Equal("No pudo ingresar: El código no es válido.", failure.GetProperty("summary").GetString());
        Assert.All(items, item =>
        {
            Assert.Equal("signins", item.GetProperty("type").GetString());
            Assert.Equal(JsonValueKind.Null, item.GetProperty("ipAddress").ValueKind);
        });
        var raw = JsonSerializer.Serialize(page);
        Assert.DoesNotContain(account.Email.Value, raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(code, raw, StringComparison.Ordinal);
    }

    // E9 (I1): los automáticos del segundo factor son ingresos, sin motivo; el restablecimiento es de la cuenta
    // restablecida y conserva el motivo que escribió el operador.
    [Fact]
    public async Task Second_factor_events_are_one_row_per_action_as_on_the_board()
    {
        using var auditor = await OperatorAsync([PlatformPermissions.Audit.Read]);
        var (manager, _) = await OperatorWithIdAsync(PlatformOperators.All);
        var (target, targetId) = await OperatorWithIdAsync([PlatformPermissions.Accounts.Read]);
        using (manager)
        using (target)
        {
            await ReauthenticateAsync(target, "authenticator", await PlatformFactorJourney.CurrentCodeAsync(factory.Services, targetId, Ct));
            await ReauthenticateAsync(target, "recoveryCode", await RecoveryCodeAsync(targetId));
            uint version;
            using (var detail = await manager.GetAsync($"/api/platform/accounts/{targetId:D}", Ct))
                version = (await detail.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("operatorVersion").GetUInt32();
            using var resetting = await SendAsync(manager, $"/api/platform/accounts/{targetId:D}/authenticator/reset",
                new { version, reason = "Perdió el celular" });
            Assert.True(resetting.IsSuccessStatusCode, await resetting.Content.ReadAsStringAsync(Ct));
        }

        var signIns = (await GetAsync(auditor, $"?type=signins&accountId={targetId:D}")).GetProperty("items").EnumerateArray().ToArray();
        Assert.Equal(["Confirmó con un código de recuperación", "Confirmó con el autenticador", "Configuró el autenticador"],
            signIns.Where(item => item.GetProperty("action").GetString()!.StartsWith("Platform", StringComparison.Ordinal))
                .Select(item => item.GetProperty("summary").GetString()));
        Assert.All(signIns, item =>
        {
            Assert.Equal("signins", item.GetProperty("type").GetString());
            Assert.Equal(JsonValueKind.Null, item.GetProperty("reason").ValueKind);
        });
        var reset = Assert.Single((await GetAsync(auditor, $"?type=accounts&accountId={targetId:D}")).GetProperty("items")
            .EnumerateArray(), item => item.GetProperty("action").GetString() == "PlatformAuthenticatorReset");
        Assert.Equal("Restableció el autenticador", reset.GetProperty("summary").GetString());
        Assert.Equal(targetId, reset.GetProperty("targetAccountId").GetGuid());
        Assert.Equal("Perdió el celular", reset.GetProperty("reason").GetString());
        Assert.DoesNotContain((await GetAsync(auditor, $"?type=operators&accountId={targetId:D}")).GetProperty("items")
            .EnumerateArray(), item => item.GetProperty("action").GetString()!.Contains("Authenticator", StringComparison.Ordinal)
                || item.GetProperty("action").GetString() is "PlatformRecoveryCodeUsed" or "PlatformReauthenticated");
    }

    [Fact]
    public async Task Only_operators_with_audit_read_query_and_a_bad_cursor_is_rejected()
    {
        using var anonymous = Client();
        using var unauthenticated = await anonymous.GetAsync("/api/platform/audit", Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, unauthenticated.StatusCode);
        using var person = Client();
        await AccountJourney.RegisterAsync(factory, person, Ct);
        using var consumer = await person.GetAsync("/api/platform/audit", Ct);
        Assert.Equal(HttpStatusCode.Forbidden, consumer.StatusCode);
        using var reader = await OperatorAsync([PlatformPermissions.Accounts.Read]);
        using var forbidden = await reader.GetAsync("/api/platform/audit", Ct);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        using var auditor = await OperatorAsync([PlatformPermissions.Audit.Read]);
        using var badCursor = await auditor.GetAsync("/api/platform/audit?after=no-es-un-cursor", Ct);
        Assert.Equal(HttpStatusCode.BadRequest, badCursor.StatusCode);
        using var badType = await auditor.GetAsync("/api/platform/audit?type=otra-cosa", Ct);
        Assert.Equal(HttpStatusCode.BadRequest, badType.StatusCode);
        using var missing = await auditor.GetAsync($"/api/platform/audit/{Guid.CreateVersion7():D}", Ct);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    private HttpClient Client() =>
        factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });

    private async Task<HttpClient> OperatorAsync(IReadOnlyCollection<string> permissions) =>
        (await OperatorWithIdAsync(permissions)).Client;

    private async Task<(HttpClient Client, Guid Id)> OperatorWithIdAsync(IReadOnlyCollection<string> permissions)
    {
        var client = Client();
        var account = await AccountJourney.RegisterAsync(factory, client, Ct);
        await PlatformOperators.GrantAsync(factory.Services, account.UserId, permissions, Ct);
        await AccountJourney.AuthorizeAsync(client, "platform", Ct);
        return (client, account.UserId);
    }

    private static async Task ReauthenticateAsync(HttpClient client, string method, string code)
    {
        using var confirmed = await SendAsync(client, "/api/platform/reauthentication", new { method, code });
        Assert.True(confirmed.StatusCode == HttpStatusCode.OK, await confirmed.Content.ReadAsStringAsync(Ct));
    }

    /// <summary>Un código de recuperación conocido, guardado como lo guarda el alta.</summary>
    private async Task<string> RecoveryCodeAsync(Guid userId)
    {
        const string code = "AUDI-TORA";
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var hash = services.GetRequiredService<IPlatformFactorSecrets>().HashRecoveryCode(userId, code);
        var saved = await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ =>
        {
            services.GetRequiredService<ApplicationDbContext>().Add(PlatformRecoveryCode.Issue(userId, hash,
                services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime));
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct);
        Assert.True(saved.IsSuccess);
        return code;
    }

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, string path, object body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("D"));
        return await client.SendAsync(request, Ct);
    }

    private static async Task<JsonElement> GetAsync(HttpClient client, string suffix)
    {
        using var response = await client.GetAsync("/api/platform/audit" + suffix, Ct);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct)).RootElement.Clone();
    }
}
