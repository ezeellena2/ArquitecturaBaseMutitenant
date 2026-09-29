using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.Isolation;

internal sealed class IsolationModelCustomizer(ModelCustomizerDependencies dependencies) : ModelCustomizer(dependencies)
{
    public override void Customize(ModelBuilder modelBuilder, DbContext context)
    {
        var widget = modelBuilder.Entity<Widget>().ToTable("Widgets", Schemas.Tenant);
        widget.HasKey(entity => new { entity.TenantId, entity.Id });
        widget.HasIndex(entity => new { entity.TenantId, entity.Name, entity.Id });

        var poster = modelBuilder.Entity<Poster>().ToTable("Posters", Schemas.PublicSite);
        poster.HasKey(entity => new { entity.BusinessTenantId, entity.Id });

        var deal = modelBuilder.Entity<Deal>().ToTable("Deals", Schemas.Engagement);
        deal.HasKey(entity => new { entity.ConsumerTenantId, entity.BusinessTenantId, entity.Id });
        deal.HasIndex(entity => new { entity.BusinessTenantId, entity.Id });
        base.Customize(modelBuilder, context);
    }
}

internal sealed class IsolationApplicationDbContext(
    DbContextOptions<IsolationApplicationDbContext> options,
    ITenantContext tenantContext) : ApplicationDbContext(options, tenantContext);
