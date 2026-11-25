using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Messaging;
using ArquitecturaBaseMultitenant.Domain.Platform;
using ArquitecturaBaseMultitenant.Domain.Settings;
using ArquitecturaBaseMultitenant.Domain.Tenancy;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Contracts;

/// <summary>
/// Contrato de la segunda parte de la base de la 12 (K1) en el núcleo: los 403 de la 0061, el 429 de los topes de correo,
/// includes y grantable, el estado de entrega de una invitación, los plazos y el nombre de Plataforma y la hora de cada país.
/// WhatsApp y el asistente prueban lo suyo en su módulo.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class Base12SecondPartContractTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public void Delegation_routes_publish_their_403_with_both_texts()
    {
        var paths = Contract()["paths"]!;
        foreach (var (path, method, codes) in new (string, string, string[])[]
        {
            ("/api/roles", "post", [RoleErrors.PermissionsNotHeldCode]),
            ("/api/roles/{id}", "put", [RoleErrors.PermissionsNotHeldCode]),
            ("/api/users/invitations", "post", [MemberErrors.RolesNotHeldCode]),
            ("/api/users/{memberId}", "put", [MemberErrors.RolesNotHeldCode, MemberErrors.HasMorePermissionsCode]),
            ("/api/users/{memberId}/status", "put", [MemberErrors.HasMorePermissionsCode]),
            ("/api/users/{memberId}", "delete", [MemberErrors.HasMorePermissionsCode]),
            ("/api/companies/{companyId}/members", "post", [CompanyMembershipErrors.RolesNotHeldCode]),
            ("/api/companies/{companyId}/members/{memberId}/roles", "put", [CompanyMembershipErrors.RolesNotHeldCode, CompanyMembershipErrors.HasMorePermissionsCode]),
            ("/api/companies/{companyId}/members/{memberId}/status", "put", [CompanyMembershipErrors.HasMorePermissionsCode]),
            ("/api/platform/operators/invitations", "post", [PlatformOperatorErrors.PermissionsNotHeldCode]),
            ("/api/platform/accounts/{id}/operator", "put", [PlatformOperatorErrors.PermissionsNotHeldCode]),
            ("/api/platform/accounts/{id}/suspend", "post", [PlatformOperatorErrors.HasMorePermissionsCode]),
            ("/api/platform/accounts/{id}/reactivate", "post", [PlatformOperatorErrors.HasMorePermissionsCode]),
            ("/api/platform/accounts/{id}/deletion", "post", [PlatformOperatorErrors.HasMorePermissionsCode]),
            ("/api/platform/accounts/{id}/operator", "delete", [PlatformOperatorErrors.HasMorePermissionsCode]),
        })
        {
            var operation = paths[path]![method]!;
            foreach (var code in codes)
            {
                var error = Assert.Single(operation["x-error-codes"]!.AsArray(), value => value!["code"]!.GetValue<string>() == code);
                Assert.Equal(403, error!["status"]!.GetValue<int>());
                Assert.False(string.IsNullOrWhiteSpace(error["es"]!.GetValue<string>()));
                Assert.NotEqual(code, error["es"]!.GetValue<string>());
                Assert.NotEqual(error["es"]!.GetValue<string>(), error["en"]!.GetValue<string>());
                Assert.NotNull(operation["responses"]!["403"]!["content"]!["application/problem+json"]!["examples"]![code + ".es"]);
            }
        }
        // Lo incluido (260): un rol y un operador guardan lo pedido más lo incluido.
        Assert.Contains("lo incluido", paths["/api/roles"]!["post"]!["description"]!.GetValue<string>(), StringComparison.Ordinal);
    }

    [Fact]
    public void Email_codes_and_invitations_publish_the_daily_limit_with_retry_after()
    {
        var paths = Contract()["paths"]!;
        string[] routes =
        [
            "/api/auth/login-code", "/api/auth/signup", "/api/auth/business-signup/code", "/api/account-recovery/code",
            "/api/me/login-methods", "/api/me/login-methods/{methodId}/code", "/api/users/invitations",
            "/api/users/invitations/{invitationId}/resend", "/api/companies/{companyId}/members",
            "/api/companies/{companyId}/invitations/{invitationId}/resend", "/api/platform/operators/invitations",
            "/api/platform/operators/{id}/invitation/resend", "/api/platform/tenants",
        ];
        foreach (var path in routes)
        {
            var operation = paths[path]!["post"]!;
            var error = Assert.Single(operation["x-error-codes"]!.AsArray(), value => value!["code"]!.GetValue<string>() == EmailQuotaErrors.DailyLimitCode);
            Assert.Equal(429, error!["status"]!.GetValue<int>());
            var limited = operation["responses"]!["429"]!;
            Assert.Equal(60, limited["content"]!["application/problem+json"]!["examples"]![EmailQuotaErrors.DailyLimitCode + ".es"]!["value"]!["retryAfter"]!.GetValue<int>());
            Assert.NotNull(limited["headers"]!["Retry-After"]);
        }
        // Un tope que no es de correo no cae en las rutas que no mandan correos.
        Assert.DoesNotContain(paths["/api/roles"]!["post"]!["x-error-codes"]!.AsArray(), value => value!["code"]!.GetValue<string>() == EmailQuotaErrors.DailyLimitCode);
    }

    [Fact]
    public void Reads_publish_includes_grantable_delivery_and_the_new_platform_fields()
    {
        var document = Contract();
        var schemas = document["components"]!["schemas"]!;
        // 260 y 269: includes y grantable en cada permiso y grantable en cada rol, siempre presentes.
        var permission = schemas["PermissionItem"]!;
        Assert.Contains("includes", Required(permission));
        Assert.Contains("grantable", Required(permission));
        Assert.Equal("string", permission["properties"]!["includes"]!["items"]!["type"]!.GetValue<string>());
        Assert.Contains("grantable", Required(schemas["RoleResponse"]!));
        Assert.Equal(["code", "grantable", "includes"], Required(schemas["PlatformPermissionItem"]!));
        var catalog = document["paths"]!["/api/platform/permissions"]!["get"]!;
        Assert.Equal("#/components/schemas/PlatformPermissionItem",
            catalog["responses"]!["200"]!["content"]!["application/json"]!["schema"]!["items"]!["$ref"]!.GetValue<string>());

        // 0066 §4: el estado de entrega, o null sin invitación pendiente.
        Assert.Equal(["Failed", "Pending", "Sent", "Uncertain"], schemas["InvitationDeliveryStatus"]!["enum"]!.AsArray()
            .Where(value => value is not null).Select(value => value!.GetValue<string>()).Order(StringComparer.Ordinal));
        foreach (var schema in new[] { "OrganizationUserHttpResponse", "CompanyMemberHttpResponse", "PlatformAccountResponse", "PlatformAccountDetailResponse" })
        {
            var delivery = schemas[schema]!["properties"]!["invitationDelivery"]!;
            Assert.Contains(delivery["oneOf"]!.AsArray(), value => value!["$ref"]?.GetValue<string>() == "#/components/schemas/InvitationDeliveryStatus");
            Assert.DoesNotContain("invitationDelivery", Required(schemas[schema]!));
        }

        // 272 y 273: los plazos y el nombre de los correos, con sus cotas en el PUT.
        Assert.Superset(new HashSet<string>(["loginAuditRetentionMonths", "loginCodeRetentionDays", "closedInvitationRetentionDays", "appName", "senderName"]),
            Required(schemas["PlatformSettingsResponse"]!).ToHashSet());
        Assert.NotNull(schemas["PlatformSettingsResponse"]!["properties"]!["emailLogoUrl"]);
        // «Así llega» muestra el «De» completo: la dirección la fija la instalación y no se edita (integración de la base 2).
        Assert.NotNull(schemas["PlatformSettingsResponse"]!["properties"]!["senderAddress"]);
        Assert.Null(schemas["UpdatePlatformSettingsHttpRequest"]!["properties"]!["senderAddress"]);
        var update = schemas["UpdatePlatformSettingsHttpRequest"]!["properties"]!;
        AssertRange(update["loginAuditRetentionMonths"]!, PlatformSettingsLimits.MinLoginAuditRetentionMonths, PlatformSettingsLimits.MaxLoginAuditRetentionMonths);
        AssertRange(update["loginCodeRetentionDays"]!, PlatformSettingsLimits.MinLoginCodeRetentionDays, PlatformSettingsLimits.MaxLoginCodeRetentionDays);
        AssertRange(update["closedInvitationRetentionDays"]!, PlatformSettingsLimits.MinClosedInvitationRetentionDays, PlatformSettingsLimits.MaxClosedInvitationRetentionDays);
        Assert.Equal(PlatformSettingsLimits.NameMaxLength, update["appName"]!["maxLength"]!.GetValue<int>());
        Assert.Equal(PlatformSettingsLimits.NameMaxLength, update["senderName"]!["maxLength"]!.GetValue<int>());
        Assert.Equal("#/components/schemas/PlatformAuditListResponse", document["paths"]!["/api/platform/audit"]!["get"]!["responses"]!["200"]!
            ["content"]!["application/json"]!["schema"]!["$ref"]!.GetValue<string>());
        Assert.Equal(["hasMore", "items", "loginAuditRetentionMonths"], Required(schemas["PlatformAuditListResponse"]!));

        // 262: la hora del país de cada zona.
        Assert.NotNull(schemas["TimeZoneReferenceHttpResponse"]!["properties"]!["countryTimeName"]);
    }

    [Fact]
    public async Task Permissions_answer_what_they_include_and_what_the_owner_can_give()
    {
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var owner = await AccountJourney.RegisterAsync(factory, client, Ct);
        var tenant = await OrganizationJourney.StageAsync(factory, owner, "Contrato base 12 parte 2", Ct);
        await AccountJourney.AuthorizeAsync(client, "business", Ct, tenant);
        var groups = await client.GetFromJsonAsync<JsonArray>("/api/permissions", Ct);
        var items = groups!.SelectMany(group => group!["permissions"]!.AsArray()).ToArray();
        Assert.Equal(Permissions.All.Count, items.Length);
        // 260: lo que incluye cada permiso, del catálogo; el Dueño puede dar todo (269 y ADR 0061, B1).
        Assert.All(items, item =>
        {
            Assert.Equal(Permissions.Inclusions.Of(item!["code"]!.GetValue<string>()),
                item["includes"]!.AsArray().Select(code => code!.GetValue<string>()));
            Assert.True(item["grantable"]!.GetValue<bool>());
        });
        Assert.Equal(["users.read"], items.Single(item => item!["code"]!.GetValue<string>() == "users.manage")!
            ["includes"]!.AsArray().Select(code => code!.GetValue<string>()));
        var roles = await client.GetFromJsonAsync<JsonObject>("/api/roles/paged", Ct);
        Assert.All(roles!["items"]!.AsArray(), role => Assert.True(role!["grantable"]!.GetValue<bool>()));
    }

    [Fact]
    public async Task Platform_catalog_lists_every_permission_for_who_reads_accounts()
    {
        using var reader = await OperatorAsync([PlatformPermissions.Accounts.Read]);
        var catalog = await reader.GetFromJsonAsync<JsonArray>("/api/platform/permissions", Ct) ?? [];
        Assert.Equal(PlatformPermissions.All, catalog.Select(item => item!["code"]!.GetValue<string>()));
        Assert.All(catalog, item =>
        {
            Assert.Equal(PlatformPermissions.Inclusions.Of(item!["code"]!.GetValue<string>()),
                item["includes"]!.AsArray().Select(code => code!.GetValue<string>()));
            // 269 y ADR 0061: puede dar solo lo que tiene.
            Assert.Equal(item["code"]!.GetValue<string>() == PlatformPermissions.Accounts.Read,
                item["grantable"]!.GetValue<bool>());
        });
        Assert.Equal(["platform.accounts.read"], catalog.Single(item => item!["code"]!.GetValue<string>() ==
            "platform.accounts.manage")!["includes"]!.AsArray().Select(code => code!.GetValue<string>()));

        using var other = await OperatorAsync([PlatformPermissions.Audit.Read]);
        using var forbidden = await other.GetAsync("/api/platform/permissions", Ct);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    [Theory]
    [InlineData("es-AR", "hora de Argentina")]
    [InlineData("en-US", "Argentina time")]
    public async Task Each_time_zone_names_the_time_of_its_country(string culture, string expected)
    {
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/reference-data/time-zones");
        request.Headers.AcceptLanguage.ParseAdd(culture);
        using var response = await client.SendAsync(request, Ct);
        var zones = await response.Content.ReadFromJsonAsync<JsonArray>(Ct);
        var buenosAires = Assert.Single(zones!, zone => zone!["id"]!.GetValue<string>() == "America/Argentina/Buenos_Aires");
        Assert.Equal(expected, buenosAires!["countryTimeName"]!.GetValue<string>());
    }

    private async Task<HttpClient> OperatorAsync(IReadOnlyCollection<string> permissions)
    {
        var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var account = await AccountJourney.RegisterAsync(factory, client, Ct);
        await PlatformOperators.GrantAsync(factory.Services, account.UserId, permissions, Ct);
        await AccountJourney.AuthorizeAsync(client, "platform", Ct);
        return client;
    }

    private static void AssertRange(JsonNode property, int minimum, int maximum)
    {
        Assert.Equal(minimum, property["minimum"]!.GetValue<int>());
        Assert.Equal(maximum, property["maximum"]!.GetValue<int>());
    }

    private static string[] Required(JsonNode schema) =>
        [.. schema["required"]!.AsArray().Select(value => value!.GetValue<string>()).Order(StringComparer.Ordinal)];

    private static JsonNode Contract()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ArquitecturaBaseMultitenant.slnx")))
            directory = directory.Parent;
        var root = directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
        return JsonNode.Parse(File.ReadAllText(Path.Combine(root, "docs", "contracts", "openapi.json")))!;
    }
}
