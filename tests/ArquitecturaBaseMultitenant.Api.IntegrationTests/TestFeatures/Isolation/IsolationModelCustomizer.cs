using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.Isolation;

internal sealed class IsolationModelCustomizer(ModelCustomizerDependencies dependencies) : ModelCustomizer(dependencies)
{
    public override void Customize(ModelBuilder modelBuilder, DbContext context)
    {
        modelBuilder.Entity<Widget>().ToTable("Widgets", Schemas.Tenant);
        modelBuilder.Entity<Poster>().ToTable("Posters", Schemas.PublicSite);
        modelBuilder.Entity<Deal>().ToTable("Deals", Schemas.Engagement);
        base.Customize(modelBuilder, context);
    }
}

internal sealed class IsolationApplicationDbContext(
    DbContextOptions<IsolationApplicationDbContext> options,
    ITenantContext tenantContext) : ApplicationDbContext(options, tenantContext);
