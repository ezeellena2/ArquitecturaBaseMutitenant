using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Legal;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Auth;

/// <summary>
/// Comprueba documentos legales públicos y el perfil autenticado. Protege los permisos de cada acceso y que la
/// lectura de Plataforma refleje exactamente la asignación persistida, incluso después de revocar permisos.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class MeTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("es-AR", "Dueño")]
    [InlineData("en-US", "Owner")]
    public async Task Business_profile_translates_the_persisted_system_owner_role_in_request_language(
        string culture, string expectedRole)
    {
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var owner = await AccountJourney.RegisterAsync(factory, client, Ct);
        var tenantId = await OrganizationJourney.StageAsync(factory, owner, "Rol del perfil", Ct);
        await AccountJourney.AuthorizeAsync(client, "business", Ct, tenantId);
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd(culture);

        var profile = await ReadMeAsync(client);

        var organization = Assert.Single(profile.GetProperty("organizations").EnumerateArray());
        Assert.Equal(tenantId, organization.GetProperty("id").GetGuid());
        Assert.Equal(expectedRole, organization.GetProperty("roleName").GetString());
    }

    [Fact]
    public async Task Business_me_exposes_organization_country_without_settings_permission_and_keeps_personal_culture()
    {
        using var ownerClient = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        using var memberClient = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var owner = await AccountJourney.RegisterAsync(factory, ownerClient, Ct);
        var member = await AccountJourney.RegisterAsync(factory, memberClient, Ct);
        Assert.Equal(JsonValueKind.Null, (await ReadMeAsync(memberClient)).GetProperty("organizationCountryCode").ValueKind);
        var tenantId = await OrganizationJourney.StageAsync(factory, owner, "País del perfil de empresa", Ct);
        await OrganizationJourney.AddActiveMemberAsync(factory, tenantId, member.UserId, Ct);
        await AccountJourney.AuthorizeAsync(memberClient, "business", Ct, tenantId);

        var initial = await ReadMeAsync(memberClient);
        Assert.Equal("AR", initial.GetProperty("organizationCountryCode").GetString());
        Assert.Empty(initial.GetProperty("permissions").EnumerateArray());
        using (var forbidden = await memberClient.GetAsync("/api/organization/settings", Ct))
            Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        await AccountJourney.AuthorizeAsync(ownerClient, "business", Ct, tenantId);
        using var settingsRead = await ownerClient.GetAsync("/api/organization/settings", Ct);
        Assert.Equal(HttpStatusCode.OK, settingsRead.StatusCode);
        using var settingsBody = (await settingsRead.Content.ReadFromJsonAsync<JsonDocument>(Ct))!;
        var settings = settingsBody.RootElement;
        using var changed = await ownerClient.PutAsJsonAsync("/api/organization/settings", new
        {
            name = settings.GetProperty("name").GetString(),
            defaultCulture = "en-US",
            defaultTimeZoneId = settings.GetProperty("defaultTimeZoneId").GetString(),
            defaultCurrency = settings.GetProperty("defaultCurrency").GetString(),
            version = settings.GetProperty("version").GetUInt32(),
        }, Ct);
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);

        var updated = await ReadMeAsync(memberClient);
        Assert.Equal("US", updated.GetProperty("organizationCountryCode").GetString());
        Assert.Equal("es-AR", updated.GetProperty("culture").GetString());
        Assert.Equal(tenantId, updated.GetProperty("activeTenantId").GetGuid());
        Assert.Empty(updated.GetProperty("permissions").EnumerateArray());
        await AccountJourney.AuthorizeAsync(memberClient, "consumer", Ct);
        Assert.Equal(JsonValueKind.Null, (await ReadMeAsync(memberClient)).GetProperty("organizationCountryCode").ValueKind);
    }

    [Theory]
    [InlineData("terms", "es-AR", "DOCUMENTO DE DEMOSTRACIÓN.")]
    [InlineData("privacy", "en-US", "DEMONSTRATION DOCUMENT.")]
    public async Task Current_legal_document_is_public_in_the_requested_culture(
        string kind, string culture, string expectedPrefix)
    {
        await using var isolated = new ApiFactory();
        await isolated.InitializeAsync();
        await isolated.Services.SeedDatabaseAsync(Ct);
        await using var scope = isolated.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var nowUtc = scope.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
        var documentKind = Enum.Parse<LegalDocumentKind>(kind, ignoreCase: true);
        var current = await context.LegalDocuments.AsNoTracking()
            .Where(row => row.Kind == documentKind && row.EffectiveAtUtc <= nowUtc)
            .OrderByDescending(row => row.EffectiveAtUtc).ThenByDescending(row => row.Version)
            .FirstAsync(Ct);
        var content = await context.LegalDocumentContents.AsNoTracking()
            .SingleAsync(row => row.LegalDocumentId == current.Id && row.Culture == culture, Ct);
        using var client = isolated.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/legal/{kind}");
        request.Headers.AcceptLanguage.ParseAdd(culture);

        using var response = await client.SendAsync(request, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var document = await response.Content.ReadFromJsonAsync<LegalBody>(Ct);
        Assert.NotNull(document);
        Assert.Equal(current.Id, document.Id);
        Assert.Equal(current.Version, document.Version);
        Assert.Equal(culture, document.Culture);
        Assert.Equal(content.Culture, document.Culture);
        Assert.Equal(content.Text, document.Text);
        Assert.StartsWith(expectedPrefix, document.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Me_routes_require_an_authenticated_access()
    {
        using var client = factory.CreateClient();

        using var get = await client.GetAsync("/api/me", Ct);
        using var put = await client.PutAsJsonAsync("/api/me",
            new { displayName = "Ana", culture = "en-US", timeZoneId = "America/Argentina/Buenos_Aires" }, Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, get.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, put.StatusCode);
    }

    [Fact]
    public async Task Platform_me_reads_exactly_the_current_persisted_permissions_without_caching()
    {
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var account = await AccountJourney.RegisterAsync(factory, client, Ct);
        string[] assigned = [PlatformPermissions.Tenants.Read, PlatformPermissions.Audit.Read];
        await PlatformOperators.GrantAsync(factory.Services, account.UserId, assigned, Ct);
        await AccountJourney.AuthorizeAsync(client, "platform", Ct);

        var initial = await ReadMeAsync(client);

        Assert.Equal(JsonValueKind.Null, initial.GetProperty("organizationCountryCode").ValueKind);
        Assert.Equal(assigned, PlatformPermissionsOf(initial));
        Assert.Empty(initial.GetProperty("effectivePermissions").GetProperty("organization").EnumerateArray());
        Assert.Empty(initial.GetProperty("effectivePermissions").GetProperty("companies").EnumerateObject());
        Assert.Empty(initial.GetProperty("permissions").EnumerateArray());

        await PlatformOperators.GrantAsync(factory.Services, account.UserId, [PlatformPermissions.Audit.Read], Ct);
        Assert.Equal([PlatformPermissions.Audit.Read], PlatformPermissionsOf(await ReadMeAsync(client)));

        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
        {
            var assignments = services.GetRequiredService<IPlatformPermissionAssignmentRepository>();
            var assignment = await assignments.GetByUserIdAsync(account.UserId, ct);
            Assert.NotNull(assignment);
            assignments.Remove(assignment);
            return Result.Success();
        }, CommitPolicy.OnSuccess, Ct);
        Assert.Empty(PlatformPermissionsOf(await ReadMeAsync(client)));
    }

    [Fact]
    public async Task Consumer_and_business_me_have_no_platform_permissions_and_keep_the_flat_organization_list()
    {
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var account = await AccountJourney.RegisterAsync(factory, client, Ct);

        Assert.Empty(PlatformPermissionsOf(await ReadMeAsync(client)));

        var tenantId = await OrganizationJourney.StageAsync(factory, account, "Permisos del perfil", Ct);
        await AccountJourney.AuthorizeAsync(client, "business", Ct, tenantId);
        var business = await ReadMeAsync(client);
        var organizationPermissions = business.GetProperty("effectivePermissions").GetProperty("organization")
            .EnumerateArray().Select(permission => permission.GetString()).ToArray();

        Assert.Empty(PlatformPermissionsOf(business));
        Assert.NotEmpty(organizationPermissions);
        Assert.Equal(organizationPermissions, business.GetProperty("permissions")
            .EnumerateArray().Select(permission => permission.GetString()));
    }

    private static async Task<JsonElement> ReadMeAsync(HttpClient client)
    {
        using var response = await client.GetAsync("/api/me", Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await response.Content.ReadFromJsonAsync<JsonDocument>(Ct);
        return json!.RootElement.Clone();
    }

    private static string?[] PlatformPermissionsOf(JsonElement profile) =>
        profile.GetProperty("effectivePermissions").GetProperty("platform")
            .EnumerateArray().Select(permission => permission.GetString()).ToArray();

    private sealed record LegalBody(Guid Id, int Version, string Culture, string Text);
}
