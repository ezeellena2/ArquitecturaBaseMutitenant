using ArquitecturaBaseMultitenant.Domain.ReferenceData;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Configurations.Platform.ReferenceData;

/// <summary>Mapea la relación de muchos a muchos entre zonas IANA y países. Una zona compartida conserva todos sus países, en lugar de elegir uno solo.</summary>
internal sealed class TimeZoneCountryConfiguration : IEntityTypeConfiguration<TimeZoneCountry>
{
    public void Configure(EntityTypeBuilder<TimeZoneCountry> builder)
    {
        builder.ToTable("TimeZoneCountries", "platform");
        builder.HasKey(link => new { link.TimeZoneId, link.CountryCode });
        builder.Property(link => link.TimeZoneId).IsRequired();
        builder.Property(link => link.CountryCode).HasColumnType("character(2)").IsRequired();
        builder.HasOne<ReferenceTimeZone>().WithMany().HasForeignKey(link => link.TimeZoneId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Country>().WithMany().HasForeignKey(link => link.CountryCode)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
