using ArquitecturaBaseMultitenant.Domain.ReferenceData;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Configurations.Platform.ReferenceData;

internal sealed class TimeZoneConfiguration : IEntityTypeConfiguration<ReferenceTimeZone>
{
    public void Configure(EntityTypeBuilder<ReferenceTimeZone> builder)
    {
        builder.ToTable("TimeZones", "platform");
        builder.HasKey(zone => zone.Id);
        builder.Property(zone => zone.Id).IsRequired();
    }
}
