using ArquitecturaBaseMultitenant.Domain.ReferenceData;
using ArquitecturaBaseMultitenant.Domain.Settings;
using TenantEntity = ArquitecturaBaseMultitenant.Domain.Tenancy.Tenant;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Configurations.Tenant;

internal sealed class TenantSettingsConfiguration : IEntityTypeConfiguration<TenantSettings>
{
    public void Configure(EntityTypeBuilder<TenantSettings> builder)
    {
        builder.ToTable("TenantSettings", Schemas.Tenant);
        builder.HasKey(settings => new { settings.TenantId, settings.Id });
        builder.Property(settings => settings.DefaultCulture).IsRequired();
        builder.Property(settings => settings.DefaultTimeZoneId).IsRequired();
        builder.Property(settings => settings.DefaultCurrency).IsRequired();
        builder.HasIndex(settings => settings.TenantId).IsUnique();
        builder.HasOne<TenantEntity>().WithMany()
            .HasForeignKey(settings => settings.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Culture>().WithMany()
            .HasForeignKey(settings => settings.DefaultCulture).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ReferenceTimeZone>().WithMany()
            .HasForeignKey(settings => settings.DefaultTimeZoneId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Currency>().WithMany()
            .HasForeignKey(settings => settings.DefaultCurrency).OnDelete(DeleteBehavior.Restrict);
    }
}
