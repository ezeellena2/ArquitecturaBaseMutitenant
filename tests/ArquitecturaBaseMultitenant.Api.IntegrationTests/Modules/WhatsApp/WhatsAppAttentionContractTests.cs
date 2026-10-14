using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Caching;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Features;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.WhatsApp;

/// <summary>Contrato de la E11 publicado antes de implementarlo: rutas en 501 detrás de su acceso, permiso y módulo.</summary>
[Collection(ApiTestGroup.Name)]
public sealed class WhatsAppAttentionContractTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public void Every_attention_route_declares_access_permission_module_and_its_errors()
    {
        var document = JsonNode.Parse(File.ReadAllText(Path.Combine(FindRepositoryRoot(), "docs", "contracts", "openapi.json")))!;
        var own = document["paths"]!.AsObject().Where(path => path.Key.StartsWith("/api/organization/whatsapp/", StringComparison.Ordinal)
            || path.Key.StartsWith("/api/platform/whatsapp/", StringComparison.Ordinal) || path.Key.StartsWith("/api/me/whatsapp/", StringComparison.Ordinal)).ToArray();
        Assert.NotEmpty(own);
        foreach (var (path, item) in own)
            foreach (var (method, operation) in item!.AsObject().Where(value => value.Value?["responses"] is not null))
            {
                var access = operation!["x-access"]!.GetValue<string>();
                Assert.Equal(path.StartsWith("/api/me/", StringComparison.Ordinal) ? "consumer" : path.StartsWith("/api/platform/", StringComparison.Ordinal) ? "platform" : "business", access);
                var permissions = operation["x-permissions"]!.AsArray().Select(value => value!.GetValue<string>()).ToArray();
                // El contador del menú suma las bandejas que la persona puede ver: no declara un permiso único.
                if (access == "consumer" || path == "/api/organization/whatsapp/untaken") Assert.Empty(permissions);
                else Assert.Single(permissions);
                Assert.Equal(access == "platform" ? "installed-management" : "feature:" + WhatsAppFeature.Key, operation["x-module-gate"]!.GetValue<string>());
                Assert.NotNull(operation["x-error-codes"]);
                if (method == "post" && operation["responses"]!["201"] is not null)
                    Assert.Contains(operation["parameters"]!.AsArray(), parameter => parameter!["name"]!.GetValue<string>() == "Idempotency-Key");
            }
    }

    [Fact]
    public void Contract_changes_of_the_stage_publish_their_shapes_limits_and_permissions()
    {
        var document = JsonNode.Parse(File.ReadAllText(Path.Combine(FindRepositoryRoot(), "docs", "contracts", "openapi.json")))!;
        var schemas = document["components"]!["schemas"]!;
        // CC10: Money como lo escribe MoneyJsonConverter.
        var money = schemas["Money"]!;
        Assert.Equal(["amount", "currency"], money["required"]!.AsArray().Select(value => value!.GetValue<string>()).Order(StringComparer.Ordinal));
        Assert.Equal("decimal", money["properties"]!["amount"]!["format"]!.GetValue<string>());
        Assert.Equal("^[A-Z]{3}$", money["properties"]!["currency"]!["pattern"]!.GetValue<string>());
        // CC12 y CC19: topes de las listas nuevas.
        Assert.Equal(WhatsAppFeature.MaxTeamMembers, schemas["UpdateWhatsAppTeamHttpRequest"]!["properties"]!["memberIds"]!["maxItems"]!.GetValue<int>());
        Assert.Equal(WhatsAppFeature.MaxTeamMembers, schemas["WhatsAppTeamDetailResponse"]!["properties"]!["members"]!["maxItems"]!.GetValue<int>());
        Assert.Equal(WhatsAppFeature.MaxMemberPreview, schemas["WhatsAppTeamResponse"]!["properties"]!["memberPreview"]!["maxItems"]!.GetValue<int>());
        Assert.Equal(WhatsAppFeature.MaxTemplateLanguages, schemas["WhatsAppTemplateResponse"]!["properties"]!["languages"]!["maxItems"]!.GetValue<int>());
        // CC25 y CC26: el teléfono de una invitación por teléfono y el código por WhatsApp en la invitación.
        Assert.NotNull(schemas["OrganizationUserHttpResponse"]!["properties"]!["phone"]);
        Assert.NotNull(schemas["InvitationPreviewResponse"]!["properties"]!["phone"]);
        Assert.Equal("boolean", schemas["InvitationPreviewResponse"]!["properties"]!["canUsePhoneCode"]!["type"]!.GetValue<string>());
        // CC27: sin número conectado, una plantilla responde 409 NumberRequired (en organización y en Plataforma).
        foreach (var path in new[] { "/api/organization/whatsapp/templates", "/api/platform/whatsapp/templates" })
        {
            var error = Assert.Single(document["paths"]![path]!["post"]!["x-error-codes"]!.AsArray(),
                value => value!["code"]!.GetValue<string>() == WhatsAppErrors.TemplateNumberRequiredCode);
            Assert.Equal(409, error!["status"]!.GetValue<int>());
        }
        // CC23: duplicar con los valores de la difusión y el par nombre y ejemplo de cada variable.
        var broadcastVariables = schemas["WhatsAppBroadcastResponse"]!["properties"]!["variables"]!;
        Assert.Equal(WhatsAppFeature.MaxTemplateVariables, broadcastVariables["maxItems"]!.GetValue<int>());
        Assert.Equal(WhatsAppTextLimits.TemplateVariable, broadcastVariables["items"]!["maxLength"]!.GetValue<int>());
        Assert.EndsWith("/WhatsAppTemplateVariableResponse", schemas["WhatsAppTemplateResponse"]!["properties"]!["variables"]!["items"]!["$ref"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Equal(WhatsAppTextLimits.TemplateVariable, schemas["WhatsAppTemplateVariableResponse"]!["properties"]!["example"]!["maxLength"]!.GetValue<int>());
        // CC24: cada idioma con su cultura del catálogo y su categoría.
        Assert.EndsWith("/WhatsAppTemplateLanguageResponse", schemas["WhatsAppTemplateResponse"]!["properties"]!["languages"]!["items"]!["$ref"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Equal(["category", "culture"], schemas["WhatsAppTemplateLanguageResponse"]!["required"]!.AsArray().Select(value => value!.GetValue<string>()).Order(StringComparer.Ordinal));
        // CC22: cada etiqueta, en el contacto, la importación y las consultas, tiene el largo de WhatsAppTextLimits.Tag.
        Assert.Equal(WhatsAppTextLimits.Tag, schemas["UpdateWhatsAppContactHttpRequest"]!["properties"]!["tags"]!["items"]!["maxLength"]!.GetValue<int>());
        Assert.Equal(WhatsAppTextLimits.Tag, schemas["WhatsAppAudienceInput"]!["properties"]!["tag"]!["maxLength"]!.GetValue<int>());
        foreach (var path in new[] { "/api/organization/whatsapp/contacts/import", "/api/organization/whatsapp/contacts/import/preview" })
            Assert.Equal(WhatsAppTextLimits.Tag, document["paths"]![path]!["post"]!["requestBody"]!["content"]!["multipart/form-data"]!["schema"]!["properties"]!["Tag"]!["maxLength"]!.GetValue<int>());
        foreach (var path in new[] { "/api/organization/whatsapp/contacts", "/api/organization/whatsapp/broadcasts/estimate" })
            Assert.Equal(WhatsAppTextLimits.Tag, document["paths"]![path]!["get"]!["parameters"]!.AsArray()
                .Single(parameter => parameter!["name"]!.GetValue<string>() == "Tag")!["schema"]!["maxLength"]!.GetValue<int>());
        // CC17: duplicar pasa al front.
        Assert.DoesNotContain(document["paths"]!.AsObject(), path => path.Key.EndsWith("/duplicate", StringComparison.Ordinal));
        // CC11, CC14 y CC21: cada opción nueva con el permiso de su pantalla.
        foreach (var (path, permission) in new[]
        {
            ("/api/organization/whatsapp/teams/member-options", "whatsapp.manage"), ("/api/platform/whatsapp/teams/member-options", "platform.whatsapp.manage"),
            ("/api/organization/whatsapp/customers/inbox-teams", "whatsapp.customers"), ("/api/organization/whatsapp/internal/inbox-teams", "whatsapp.internal"),
            ("/api/platform/whatsapp/inbox-teams", "platform.whatsapp.attend"), ("/api/organization/whatsapp/numbers/company-options", "whatsapp.manage"),
        })
            Assert.Equal([permission], document["paths"]![path]!["get"]!["x-permissions"]!.AsArray().Select(value => value!.GetValue<string>()));
    }

    [Fact]
    public async Task Routes_answer_not_implemented_behind_the_module_and_404_without_it()
    {
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var owner = await AccountJourney.RegisterAsync(factory, client, Ct);
        var tenant = await OrganizationJourney.StageAsync(factory, owner, "Contrato WhatsApp", Ct);
        await AccountJourney.AuthorizeAsync(client, "business", Ct, tenant);
        using (var off = await client.GetAsync("/api/organization/whatsapp/numbers", Ct))
            Assert.Equal(HttpStatusCode.NotFound, off.StatusCode);
        await EnableAsync(tenant);
        // Lo que todavía no está programado (B5c, «Cargar teléfonos»).
        foreach (var path in new[] { "/api/organization/whatsapp/phone-imports/preview" })
        {
            using var response = await client.PostAsJsonAsync(path, new { phones = Array.Empty<object>(), roleIds = Array.Empty<Guid>() }, Ct);
            Assert.Equal(HttpStatusCode.NotImplemented, response.StatusCode);
            using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
            Assert.Equal(ContractErrors.NotImplementedCode, problem.RootElement.GetProperty("code").GetString());
        }
    }

    private async Task EnableAsync(Guid tenantId)
    {
        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ =>
        {
            services.GetRequiredService<ITenantFeatureRepository>().Add(TenantFeature.Create(tenantId, WhatsAppFeature.Key, true, TenantFeatureSource.Manual, null).Value);
            services.GetRequiredService<ISecurityEventRepository>().Add(SecurityEvent.ForTenantFeatures(AuditActorKind.System, Guid.Empty,
                tenantId, "WhatsApp contract fixture", services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime));
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct);
        await services.GetRequiredService<ITenantFeatureCache>().InvalidateAsync(tenantId, Ct);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ArquitecturaBaseMultitenant.slnx"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
