using System.Globalization;
using System.Net;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Messaging;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Messaging;

/// <summary>
/// ADR 0063 y decisión 256 por las rutas reales: con un tope lleno, cada pedido que manda un código o una invitación por
/// correo responde 429 <c>Messaging.Email.DailyLimit</c> con <c>retryAfter</c> y <c>Retry-After</c>, y no suma nada. Cada
/// organización tiene su tope y el global es de todas. Los contadores se llenan por SQL, con la clave que usa la API.
/// </summary>
public sealed class EmailQuotaEndpointsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const int Full = 1_000_000;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Each_route_that_sends_an_email_answers_429_with_retry_after_while_the_global_is_full()
    {
        using var ownerClient = Client();
        var owner = await AccountJourney.RegisterAsync(factory, ownerClient, Ct);
        await OrganizationJourney.StageAsync(factory, owner, "Topes de correo", Ct);
        await AccountJourney.AuthorizeAsync(ownerClient, "business", Ct);
        var companyId = await OrganizationJourney.CreateCompanyAsync(ownerClient, "Topes Norte S.A.", Ct);
        var companyRoleId = (await JsonAsync(ownerClient, "/api/roles/paged?scope=Company&page=1&pageSize=50"))
            .GetProperty("items").EnumerateArray().Single(role => role.GetProperty("isSystemRole").GetBoolean())
            .GetProperty("id").GetGuid();
        var invitationId = await InviteAsync(ownerClient, [new { companyId, roleIds = new[] { companyRoleId } }]);
        using var personClient = Client();
        var person = await AccountJourney.RegisterAsync(factory, personClient, Ct);
        using var operatorClient = Client();
        var operatorAccount = await AccountJourney.RegisterAsync(factory, operatorClient, Ct);
        await PlatformOperators.GrantAsync(factory.Services, operatorAccount.UserId, PlatformOperators.All, Ct);
        await AccountJourney.AuthorizeAsync(operatorClient, "platform", Ct);
        Guid operatorInvitationId;
        using (var invited = await PlatformJourney.PostAsync(operatorClient, "/api/platform/operators/invitations",
                   new { email = NewEmail(), permissions = new[] { PlatformPermissions.Audit.Read } }, Ct))
        {
            Assert.True(invited.StatusCode == HttpStatusCode.Created, await invited.Content.ReadAsStringAsync(Ct));
            operatorInvitationId = (await JsonAsync(invited)).GetProperty("id").GetGuid();
        }

        // El código del alta de la persona es de hace un momento: se corre, así el reenvío no espera por su destino.
        await using (var scope = factory.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().LoginCodes
                .Where(code => code.Destination == person.Email.Value)
                .ExecuteUpdateAsync(setters => setters.SetProperty(code => code.CreatedAtUtc, code => code.CreatedAtUtc.AddHours(-1)), Ct);
        await FillAsync(MessageQuotaKey.Global);
        try
        {
            var before = await CountAsync();
            (HttpClient Client, string Path, object Body)[] requests =
            [
                (Client(), "/api/auth/login-code", new { email = person.Email.Value }),
                (Client(), "/api/auth/signup", new { email = NewEmail(), acceptedTerms = true, culture = "es-AR", timeZoneId = "America/Argentina/Buenos_Aires" }),
                (personClient, "/api/me/login-methods", new { email = NewEmail() }),
                (ownerClient, "/api/users/invitations", new { email = NewEmail(), roleIds = Array.Empty<Guid>(), companies = Array.Empty<object>() }),
                (ownerClient, $"/api/users/invitations/{invitationId:D}/resend", new { }),
                (ownerClient, $"/api/companies/{companyId:D}/invitations/{invitationId:D}/resend", new { }),
                (operatorClient, "/api/platform/operators/invitations", new { email = NewEmail(), permissions = new[] { PlatformPermissions.Audit.Read } }),
                (operatorClient, $"/api/platform/operators/{operatorInvitationId:D}/invitation/resend", new { }),
                (operatorClient, "/api/platform/tenants", new { name = "Topes " + Guid.NewGuid().ToString("N")[..8], firstCompanyName = "Primera S.A.", ownerEmail = NewEmail(), culture = "es-AR", timeZoneId = "America/Argentina/Buenos_Aires" }),
            ];
            foreach (var (client, path, body) in requests)
            {
                using var response = await PlatformJourney.PostAsync(client, path, body, Ct);
                await AssertLimitedAsync(response, path);
            }
            // Ningún rechazo sumó en un contador.
            Assert.Equal(before, await CountAsync());
        }
        finally
        {
            await ClearAsync(MessageQuotaKey.Global);
        }

        // Con lugar, la invitación sale y suma en quien invita, en la organización y en el global.
        using var issued = await AccountJourney.PostAsync(ownerClient, "/api/users/invitations",
            new { email = NewEmail(), roleIds = Array.Empty<Guid>(), companies = Array.Empty<object>() }, Ct);
        Assert.True(issued.StatusCode == HttpStatusCode.Created, await issued.Content.ReadAsStringAsync(Ct));
        // La primera invitación del Dueño y esta: los rechazos del medio no sumaron.
        Assert.Equal(2, await CountAsync(Key(MessageQuotaScope.Inviter, owner.UserId)));
    }

    [Fact]
    public async Task Each_organization_has_its_own_limit_and_the_global_is_shared()
    {
        using var firstClient = Client();
        var first = await AccountJourney.RegisterAsync(factory, firstClient, Ct);
        var firstId = await OrganizationJourney.StageAsync(factory, first, "Primera con tope", Ct);
        await AccountJourney.AuthorizeAsync(firstClient, "business", Ct);
        using var secondClient = Client();
        var second = await AccountJourney.RegisterAsync(factory, secondClient, Ct);
        var secondId = await OrganizationJourney.StageAsync(factory, second, "Segunda con tope", Ct);
        await AccountJourney.AuthorizeAsync(secondClient, "business", Ct);

        await FillAsync(Key(MessageQuotaScope.Organization, firstId));
        using (var limited = await PlatformJourney.PostAsync(firstClient, "/api/users/invitations",
                   new { email = NewEmail(), roleIds = Array.Empty<Guid>(), companies = Array.Empty<object>() }, Ct))
            await AssertLimitedAsync(limited, "first organization");
        await InviteAsync(secondClient, []);
        Assert.Equal(1, await CountAsync(Key(MessageQuotaScope.Organization, secondId)));

        await FillAsync(MessageQuotaKey.Global);
        try
        {
            using var shared = await PlatformJourney.PostAsync(secondClient, "/api/users/invitations",
                new { email = NewEmail(), roleIds = Array.Empty<Guid>(), companies = Array.Empty<object>() }, Ct);
            await AssertLimitedAsync(shared, "second organization");
        }
        finally
        {
            await ClearAsync(MessageQuotaKey.Global);
        }
        Assert.Equal(1, await CountAsync(Key(MessageQuotaScope.Organization, secondId)));
    }

    private static async Task AssertLimitedAsync(HttpResponseMessage response, string path)
    {
        var text = await response.Content.ReadAsStringAsync(Ct);
        Assert.True(response.StatusCode == HttpStatusCode.TooManyRequests, path + ": " + (int)response.StatusCode + " " + text);
        using var body = JsonDocument.Parse(text);
        Assert.Equal(EmailQuotaErrors.DailyLimitCode, body.RootElement.GetProperty("code").GetString());
        var seconds = body.RootElement.GetProperty(EmailQuotaErrors.RetryAfterKey).GetInt32();
        Assert.InRange(seconds, 24 * 3600, 25 * 3600);
        Assert.Equal(seconds.ToString(CultureInfo.InvariantCulture), Assert.Single(response.Headers.GetValues("Retry-After")));
    }

    private static async Task<Guid> InviteAsync(HttpClient client, object[] companies)
    {
        using var issued = await AccountJourney.PostAsync(client, "/api/users/invitations",
            new { email = NewEmail(), roleIds = Array.Empty<Guid>(), companies }, Ct);
        Assert.True(issued.StatusCode == HttpStatusCode.Created, await issued.Content.ReadAsStringAsync(Ct));
        return (await JsonAsync(issued)).GetProperty("invitationId").GetGuid();
    }

    private MessageQuotaKey Key(MessageQuotaScope scope, Guid id) =>
        new(scope, factory.Services.GetRequiredService<IMessageQuotaKeyHasher>().ForSubject(scope, id));

    // El contador lleno en la hora actual: libera lugar recién cuando esa hora sale de la ventana, en 24 a 25 horas.
    private async Task FillAsync(MessageQuotaKey key)
    {
        var hour = MessageQuotaWindow.HourOf(factory.Services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime);
        await using var scope = factory.Services.CreateAsyncScope();
        var scopeName = key.Scope.ToString();
        await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.ExecuteSqlAsync($"""
            INSERT INTO identity."MessageQuotaBuckets" ("Scope", "KeyHash", "HourUtc", "Count")
            VALUES ({scopeName}, {key.KeyHash}, {hour}, {Full})
            ON CONFLICT ("Scope", "KeyHash", "HourUtc") DO UPDATE SET "Count" = {Full}
            """, Ct);
    }

    private async Task ClearAsync(MessageQuotaKey key)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var scopeName = key.Scope.ToString();
        await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.ExecuteSqlAsync(
            $"""DELETE FROM identity."MessageQuotaBuckets" WHERE "Scope" = {scopeName} AND "KeyHash" = {key.KeyHash}""", Ct);
    }

    private async Task<int> CountAsync(MessageQuotaKey? key = null)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var buckets = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().MessageQuotaBuckets.AsNoTracking();
        return key is null
            ? await buckets.SumAsync(bucket => bucket.Count, Ct)
            : await buckets.Where(bucket => bucket.Scope == key.Scope && bucket.KeyHash == key.KeyHash).SumAsync(bucket => bucket.Count, Ct);
    }

    private static async Task<JsonElement> JsonAsync(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path, Ct);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
        return await JsonAsync(response);
    }

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        return document.RootElement.Clone();
    }

    private static string NewEmail() => $"tope-{Guid.NewGuid():N}@example.test";

    private HttpClient Client() =>
        factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
}
