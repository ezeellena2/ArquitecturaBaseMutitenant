using ArquitecturaBaseMultitenant.Domain.ReferenceData;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Configurations.Platform.ReferenceData;

/// <summary>Mapea las zonas IANA globales con su ID natural. Se usa como referencia para preferencias y países, sin un identificador artificial.</summary>
internal sealed class TimeZoneConfiguration : IEntityTypeConfiguration<ReferenceTimeZone>
{
    public void Configure(EntityTypeBuilder<ReferenceTimeZone> builder)
    {
        builder.ToTable("TimeZones", "platform");
        builder.HasKey(zone => zone.Id);
        builder.Property(zone => zone.Id).IsRequired();
    }
}
