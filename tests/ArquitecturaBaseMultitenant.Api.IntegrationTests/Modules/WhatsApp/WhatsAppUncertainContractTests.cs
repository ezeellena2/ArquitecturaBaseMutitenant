using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Resources;
using System.Text.Json;
using System.Text.Json.Nodes;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Features;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.WhatsApp;

/// <summary>
/// Contrato de la segunda parte de la base de la 12 (K1) en WhatsApp: «Sin confirmar» y su revisión en las tres bases, los
/// motivos de lo que no salió, el tope de códigos por cuenta y las dos configuraciones (B18).
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class WhatsAppUncertainContractTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public void Review_route_is_published_in_the_three_bases_without_body_or_idempotency()
    {
        var document = Contract();
        foreach (var (prefix, permission) in new[]
        {
            ("/api/organization/whatsapp/customers", "whatsapp.customers"), ("/api/organization/whatsapp/internal", "whatsapp.internal"),
            ("/api/platform/whatsapp", "platform.whatsapp.attend"),
        })
        {
            var review = document["paths"]![prefix + "/conversations/{id}/messages/{messageId}/review"]!["post"]!;
            Assert.Equal([permission], review["x-permissions"]!.AsArray().Select(value => value!.GetValue<string>()));
            Assert.Null(review["requestBody"]);
            Assert.Equal(["id", "messageId"], review["parameters"]!.AsArray().Select(value => value!["name"]!.GetValue<string>()));
            Assert.Equal("#/components/schemas/WhatsAppChatMessageResponse",
                review["responses"]!["200"]!["content"]!["application/json"]!["schema"]!["$ref"]!.GetValue<string>());
            var codes = review["x-error-codes"]!.AsArray().Select(value => value!["status"]!.GetValue<int>() + " " + value["code"]!.GetValue<string>()).ToArray();
            Assert.Contains("404 " + WhatsAppErrors.ConversationNotFoundCode, codes);
            Assert.Contains("409 " + WhatsAppErrors.MessageNotUncertainCode, codes);
            Assert.DoesNotContain("501 " + ContractErrors.NotImplementedCode, codes);
        }
        var schemas = document["components"]!["schemas"]!;
        Assert.Contains("Uncertain", schemas["WhatsAppChatMessageStatus"]!["enum"]!.AsArray().Select(value => value?.GetValue<string>()));
        Assert.Contains("Uncertain", schemas["WhatsAppRecipientStatus"]!["enum"]!.AsArray().Select(value => value?.GetValue<string>()));
        var message = schemas["WhatsAppChatMessageResponse"]!["properties"]!;
        Assert.Equal("date-time", message["reviewedAtUtc"]!["format"]!.GetValue<string>());
        Assert.NotNull(message["reviewedByName"]);
        Assert.All(WhatsAppSendFailures.All, code => Assert.Contains(code, message["failureReason"]!["description"]!.GetValue<string>(), StringComparison.Ordinal));
    }

    [Fact]
    public void Settings_routes_publish_their_permissions_limits_and_errors()
    {
        var document = Contract();
        var platform = document["paths"]!["/api/platform/whatsapp/settings"]!;
        var organization = document["paths"]!["/api/organization/whatsapp/settings"]!;
        Assert.Equal(["platform.settings.manage"], Permissions(platform["get"]!));
        Assert.Equal(["platform.settings.manage"], Permissions(platform["put"]!));
        Assert.Equal(["settings.read"], Permissions(organization["get"]!));
        Assert.Equal(["settings.manage"], Permissions(organization["put"]!));
        Assert.Equal("installed-management", platform["get"]!["x-module-gate"]!.GetValue<string>());
        Assert.Equal("feature:" + WhatsAppFeature.Key, organization["get"]!["x-module-gate"]!.GetValue<string>());
        foreach (var put in new[] { platform["put"]!, organization["put"]! })
        {
            var codes = put["x-error-codes"]!.AsArray().Select(value => value!["code"]!.GetValue<string>()).ToArray();
            Assert.Contains("General.ConcurrencyConflict", codes);
            Assert.Contains("Validation.Failed", codes);
            Assert.DoesNotContain(ContractErrors.NotImplementedCode, codes);
            Assert.Null(put["parameters"]?.AsArray().FirstOrDefault(value => value!["name"]!.GetValue<string>() == "Idempotency-Key"));
        }
        var schemas = document["components"]!["schemas"]!;
        var platformBody = schemas["UpdatePlatformWhatsAppSettingsHttpRequest"]!["properties"]!;
        Assert.Equal(1, platformBody["allowedCountries"]!["minItems"]!.GetValue<int>());
        Assert.True(platformBody["allowedCountries"]!["uniqueItems"]!.GetValue<bool>());
        foreach (var range in new[] { platformBody["chatRetentionDays"]!, platformBody["maxOrganizationChatRetentionDays"]!,
            schemas["UpdateWhatsAppOrganizationSettingsHttpRequest"]!["properties"]!["chatRetentionDays"]! })
        {
            Assert.Equal(WhatsAppSettingsLimits.MinChatRetentionDays, range["minimum"]!.GetValue<int>());
            Assert.Equal(WhatsAppSettingsLimits.MaxChatRetentionDays, range["maximum"]!.GetValue<int>());
        }
        Assert.Equal(["allowedCountries", "chatRetentionDays", "maxOrganizationChatRetentionDays", "version"],
            schemas["PlatformWhatsAppSettingsResponse"]!["required"]!.AsArray().Select(value => value!.GetValue<string>()).Order(StringComparer.Ordinal));
        Assert.Equal(["chatRetentionDays", "version"],
            schemas["WhatsAppOrganizationSettingsResponse"]!["required"]!.AsArray().Select(value => value!.GetValue<string>()).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Account_code_limit_and_every_send_failure_have_both_texts()
    {
        var paths = Contract()["paths"]!;
        foreach (var path in new[] { "/api/me/login-methods", "/api/me/login-methods/{methodId}/code" })
        {
            var error = Assert.Single(paths[path]!["post"]!["x-error-codes"]!.AsArray(),
                value => value!["code"]!.GetValue<string>() == WhatsAppCodeErrors.AccountDailyLimitCode);
            Assert.Equal(429, error!["status"]!.GetValue<int>());
            Assert.NotEqual(error["es"]!.GetValue<string>(), error["en"]!.GetValue<string>());
        }
        var texts = new ResourceManager("ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Resources.WhatsAppTexts", typeof(WhatsAppWebhookRequest).Assembly);
        foreach (var code in WhatsAppSendFailures.All)
        {
            var key = "Send.Failure." + code["WhatsApp.Send.".Length..];
            var spanish = texts.GetString(key, CultureInfo.GetCultureInfo("es"));
            var english = texts.GetString(key, CultureInfo.GetCultureInfo("en"));
            Assert.False(string.IsNullOrWhiteSpace(spanish), key);
            Assert.NotEqual(spanish, english);
        }
    }

    [Fact]
    public async Task Review_answers_behind_access_and_module_and_settings_keep_their_version()
    {
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var owner = await AccountJourney.RegisterAsync(factory, client, Ct);
        var tenant = await OrganizationJourney.StageAsync(factory, owner, "Contrato WhatsApp sin confirmar", Ct);
        await AccountJourney.AuthorizeAsync(client, "business", Ct, tenant);
        var review = $"/api/organization/whatsapp/customers/conversations/{Guid.CreateVersion7()}/messages/{Guid.CreateVersion7()}/review";
        using (var off = await client.PostAsync(review, null, Ct))
            Assert.Equal(HttpStatusCode.NotFound, off.StatusCode);
        await EnableAsync(tenant);
        using (var reviewed = await client.PostAsync(review, null, Ct))
        {
            Assert.Equal(HttpStatusCode.NotFound, reviewed.StatusCode);
            using var problem = JsonDocument.Parse(await reviewed.Content.ReadAsStringAsync(Ct));
            Assert.Equal(WhatsAppErrors.ConversationNotFoundCode, problem.RootElement.GetProperty("code").GetString());
        }
        using (var settings = await client.GetAsync("/api/organization/whatsapp/settings", Ct))
        {
            Assert.Equal(HttpStatusCode.OK, settings.StatusCode);
            using var body = JsonDocument.Parse(await settings.Content.ReadAsStringAsync(Ct));
            Assert.Equal(WhatsAppSettingsLimits.DefaultChatRetentionDays, body.RootElement.GetProperty("chatRetentionDays").GetInt32());
            Assert.Equal(0u, body.RootElement.GetProperty("version").GetUInt32());
        }
        using (var update = await client.PutAsJsonAsync("/api/organization/whatsapp/settings", new { chatRetentionDays = 30, version = 0 }, Ct))
            Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        using (var saved = await client.GetAsync("/api/organization/whatsapp/settings", Ct))
        {
            using var body = JsonDocument.Parse(await saved.Content.ReadAsStringAsync(Ct));
            Assert.Equal(30, body.RootElement.GetProperty("chatRetentionDays").GetInt32());
            Assert.NotEqual(0u, body.RootElement.GetProperty("version").GetUInt32());
        }
        using (var stale = await client.PutAsJsonAsync("/api/organization/whatsapp/settings", new { chatRetentionDays = 60, version = 0 }, Ct))
        {
            Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
            using var problem = JsonDocument.Parse(await stale.Content.ReadAsStringAsync(Ct));
            Assert.Equal("General.ConcurrencyConflict", problem.RootElement.GetProperty("code").GetString());
        }
        using (var outside = await client.PutAsJsonAsync("/api/organization/whatsapp/settings",
            new { chatRetentionDays = WhatsAppSettingsLimits.MaxChatRetentionDays + 1, version = 0 }, Ct))
            Assert.Equal(HttpStatusCode.BadRequest, outside.StatusCode);
    }

    private async Task EnableAsync(Guid tenantId)
    {
        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ =>
        {
            services.GetRequiredService<ITenantFeatureRepository>().Add(TenantFeature.Create(tenantId, WhatsAppFeature.Key, true, TenantFeatureSource.Manual, null).Value);
            services.GetRequiredService<ISecurityEventRepository>().Add(SecurityEvent.ForTenantFeatures(AuditActorKind.System, Guid.Empty,
                tenantId, "WhatsApp uncertain contract fixture", services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime));
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct);
    }

    private static string[] Permissions(JsonNode operation) => [.. operation["x-permissions"]!.AsArray().Select(value => value!.GetValue<string>())];

    private static JsonNode Contract()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ArquitecturaBaseMultitenant.slnx"))) directory = directory.Parent;
        var root = directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
        return JsonNode.Parse(File.ReadAllText(Path.Combine(root, "docs", "contracts", "openapi.json")))!;
    }
}
