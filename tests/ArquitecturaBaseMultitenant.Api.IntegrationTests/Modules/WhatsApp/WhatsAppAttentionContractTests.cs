using System.Net;
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
    public async Task Routes_answer_not_implemented_behind_the_module_and_404_without_it()
    {
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var owner = await AccountJourney.RegisterAsync(factory, client, Ct);
        var tenant = await OrganizationJourney.StageAsync(factory, owner, "Contrato WhatsApp", Ct);
        await AccountJourney.AuthorizeAsync(client, "business", Ct, tenant);
        using (var off = await client.GetAsync("/api/organization/whatsapp/numbers", Ct))
            Assert.Equal(HttpStatusCode.NotFound, off.StatusCode);
        await EnableAsync(tenant);
        foreach (var path in new[] { "/api/organization/whatsapp/numbers", "/api/organization/whatsapp/customers/conversations", "/api/organization/whatsapp/broadcasts" })
        {
            using var response = await client.GetAsync(path, Ct);
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
