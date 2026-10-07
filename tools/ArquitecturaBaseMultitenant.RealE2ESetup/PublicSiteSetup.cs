using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Services.Auth;
using ArquitecturaBaseMultitenant.Domain.PublicSite;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Settings;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.RealE2ESetup;

/// <summary>Publicaciones propias del E2E efímero, sin alterar los perfiles ni la página que editan los recorridos anteriores.</summary>
internal static class PublicSiteSetup
{
    public static async Task PrepareAsync(IServiceProvider services, CancellationToken ct)
    {
        if (Environment.GetEnvironmentVariable("MT_E2E_ISOLATED") != "1")
            throw new InvalidOperationException("Public site fixtures require isolated E2E mode.");

        Guid ownerId;
        await using (var ownerScope = services.CreateAsyncScope())
        {
            var provider = ownerScope.ServiceProvider;
            var owner = await provider.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async token =>
                Result.Success(await provider.GetRequiredService<IUserRepository>().CreateAsync(
                    "Preparador público E7", "es-AR", "UTC", token)), CommitPolicy.OnSuccess, ct);
            if (owner.IsFailure) throw new InvalidOperationException("The public site fixture owner is required.");
            ownerId = owner.Value.Id;
        }

        foreach (var suspended in new[] { false, true })
        {
            await using var fixtureScope = services.CreateAsyncScope();
            var provider = fixtureScope.ServiceProvider;
            var tenant = Tenant.CreateBusiness(suspended ? "Organización suspendida E7" : "Organización pública E7", false);
            if (tenant.Activate().IsFailure
                || tenant.SetSlug(TenantSlug.Create(suspended ? "suspendida-e2e-etapa7" : "publica-e2e-etapa7").Value).IsFailure)
                throw new InvalidOperationException("The public site fixture tenant must be active with a valid slug.");
            provider.GetRequiredService<ITenantAccessInitializer>().SetFromAccess(tenant.Id, TenantKind.Business);
            var nowUtc = provider.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
            var result = await provider.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(token =>
            {
                provider.GetRequiredService<TenantSpaceProvisioner>().Stage(tenant,
                    TenantSettings.Create("es-AR", "UTC", "ARS"), [ownerId]);
                var page = PublicPage.Create();
                if (page.UpdateContent("Compañía pública E2E", "Publicación real del recorrido E7", null, null, null, null).IsFailure
                    || page.Publish(nowUtc).IsFailure)
                    throw new InvalidOperationException("The E2E public page must be published.");
                provider.GetRequiredService<IPublicPageRepository>().Add(page);
                if (suspended && tenant.Suspend().IsFailure)
                    throw new InvalidOperationException("The unavailable fixture must be suspended.");
                return Task.FromResult(Result.Success());
            }, CommitPolicy.OnSuccess, ct);
            if (result.IsFailure) throw new InvalidOperationException("The E2E public site fixture could not be saved.");
        }
    }
}
