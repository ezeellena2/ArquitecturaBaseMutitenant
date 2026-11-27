using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Models;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Features;
using ArquitecturaBaseMultitenant.Domain.Forms;
using ArquitecturaBaseMultitenant.Domain.Results;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.Forms;

/// <summary>Lo que arman las pruebas de las rutas de Formularios: el módulo prendido y una organización con su dueño.</summary>
internal static class FormsTestData
{
    internal const string Catalogs = "/api/organization/forms/catalogs";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    internal static HttpClient Client(ApiFactory factory) =>
        factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });

    /// <summary>Prende <c>forms</c> en la organización, como lo deja Plataforma, con su evento de seguridad.</summary>
    internal static async Task EnableAsync(ApiFactory factory, Guid tenantId)
    {
        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var saved = await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ =>
        {
            services.GetRequiredService<ITenantFeatureRepository>().Add(
                TenantFeature.Create(tenantId, "forms", true, TenantFeatureSource.Manual, null).Value);
            services.GetRequiredService<ISecurityEventRepository>().Add(SecurityEvent.ForTenantFeatures(AuditActorKind.System,
                Guid.Empty, tenantId, "Forms fixture grant", services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime));
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct);
        Assert.True(saved.IsSuccess, saved.IsFailure ? saved.Error.Code : null);
    }

    /// <summary>Una organización nueva con <c>forms</c> prendido y el cliente de su dueño, que entra como empresa.</summary>
    internal static async Task<(HttpClient Client, Guid TenantId, Guid OwnerId)> BusinessAsync(ApiFactory factory, string name)
    {
        var client = Client(factory);
        var owner = await AccountJourney.RegisterAsync(factory, client, Ct);
        var tenantId = await OrganizationJourney.StageAsync(factory, owner, name + " " + Guid.NewGuid().ToString("N")[..8], Ct);
        await EnableAsync(factory, tenantId);
        await AccountJourney.AuthorizeAsync(client, "business", Ct, tenantId);
        return (client, tenantId, owner.UserId);
    }

    /// <summary>Un catálogo a mano con sus opciones, guardado por el servicio con la organización en el alcance.</summary>
    internal static async Task<FormCatalogResponse> CreateManualAsync(ApiFactory factory, Guid tenantId, string name,
        params (string Code, string Text)[] items)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        using var tenant = services.GetRequiredService<ITenantScope>().Enter(tenantId);
        var created = await services.GetRequiredService<IFormCatalogService>().CreateAsync(new CreateFormCatalogRequest(name, null,
            FormCatalogSource.Manual, null,
            [.. items.Select((item, index) => new FormCatalogItemInput(null, $"n{index}", item.Code, item.Text, null, null, true))],
            null, null, null), Ct);
        Assert.True(created.IsSuccess, created.IsFailure ? created.Error.Code : null);
        return created.Value.Catalog;
    }
}
