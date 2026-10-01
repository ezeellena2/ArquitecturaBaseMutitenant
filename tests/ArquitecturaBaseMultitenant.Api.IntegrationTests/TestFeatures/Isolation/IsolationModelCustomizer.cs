using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.Isolation;

/// <summary>
/// Agrega al modelo de tests entidades privadas, públicas y compartidas con sus claves e índices. Mantiene
/// estas piezas fuera del modelo y las migraciones productivas.
/// </summary>
internal sealed class IsolationModelCustomizer(ModelCustomizerDependencies dependencies) : ModelCustomizer(dependencies)
{
    public override void Customize(ModelBuilder modelBuilder, DbContext context)
    {
        var widget = modelBuilder.Entity<Widget>().ToTable("Widgets", Schemas.Tenant);
        widget.HasKey(entity => new { entity.TenantId, entity.Id });
        widget.HasIndex(entity => new { entity.TenantId, entity.Name, entity.Id });
        widget.HasIndex(entity => new { entity.TenantId, entity.CreatedAtUtc, entity.Id });

        var poster = modelBuilder.Entity<Poster>().ToTable("Posters", Schemas.PublicSite);
        poster.HasKey(entity => new { entity.BusinessTenantId, entity.Id });

        var deal = modelBuilder.Entity<Deal>().ToTable("Deals", Schemas.Engagement);
        deal.HasKey(entity => new { entity.ConsumerTenantId, entity.BusinessTenantId, entity.Id });
        deal.HasIndex(entity => new { entity.BusinessTenantId, entity.Id });
        base.Customize(modelBuilder, context);
    }
}

/// <summary>Deriva del contexto real para incorporar las tablas de aislamiento exclusivas de tests.</summary>
internal sealed class IsolationApplicationDbContext(
    DbContextOptions<IsolationApplicationDbContext> options,
    ITenantContext tenantContext) : ApplicationDbContext(options, tenantContext);
