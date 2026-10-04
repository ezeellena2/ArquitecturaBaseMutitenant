using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Features;
using TenantEntity = ArquitecturaBaseMultitenant.Domain.Tenancy.Tenant;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Configurations.Platform;

/// <summary>Habilitaciones globales por espacio, únicas por clave y sin RLS.</summary>
internal sealed class TenantFeatureConfiguration : IEntityTypeConfiguration<TenantFeature>
{
    public void Configure(EntityTypeBuilder<TenantFeature> builder)
    {
        builder.ToTable("TenantFeatures", Schemas.Platform);
        builder.HasKey(row => row.Id);
        builder.Property(row => row.FeatureKey).HasMaxLength(TextLimits.ShortName);
        builder.Property(row => row.Source).HasConversion<string>().HasMaxLength(TextLimits.ShortName);
        builder.HasIndex(row => new { row.TenantId, row.FeatureKey }).IsUnique()
            .HasDatabaseName(UniqueKeys.TenantFeature);
        builder.HasOne<TenantEntity>().WithMany().HasForeignKey(row => row.TenantId).OnDelete(DeleteBehavior.Restrict);
    }
}
