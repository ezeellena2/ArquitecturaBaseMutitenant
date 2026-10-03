using TenantEntity = ArquitecturaBaseMultitenant.Domain.Tenancy.Tenant;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Configurations.Platform;

/// <summary>Separa los agregados de la fila cuya versión protege las ediciones de organización.</summary>
internal sealed class TenantCountersConfiguration : IEntityTypeConfiguration<TenantCounters>
{
    public void Configure(EntityTypeBuilder<TenantCounters> builder)
    {
        builder.ToTable("TenantCounters", Schemas.Platform);
        builder.HasKey(counters => counters.TenantId);
        builder.HasOne<TenantEntity>().WithOne().HasForeignKey<TenantCounters>(counters => counters.TenantId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Property(counters => counters.CompanyCount).HasDefaultValue(0);
        builder.Property(counters => counters.MemberCount).HasDefaultValue(0);
        foreach (var property in builder.Metadata.GetProperties())
            property.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
    }
}
