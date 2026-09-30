using ArquitecturaBaseMultitenant.Domain.ReferenceData;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Configurations.Platform.ReferenceData;

/// <summary>Mapea los países ISO y sus asociaciones opcionales con moneda y zona predeterminadas. La falta de esas asociaciones se conserva como dato ausente.</summary>
internal sealed class CountryConfiguration : IEntityTypeConfiguration<Country>
{
    public void Configure(EntityTypeBuilder<Country> builder)
    {
        builder.ToTable("Countries", "platform");
        builder.HasKey(country => country.Code);
        builder.Property(country => country.Code).HasColumnType("character(2)").IsRequired();
        builder.Property(country => country.Alpha3).HasColumnType("character(3)").IsRequired();
        builder.Property(country => country.NumericCode).HasColumnType("character(3)").IsRequired();
        builder.Property(country => country.DefaultCurrencyCode).HasColumnType("character(3)");
        builder.HasOne<Currency>().WithMany().HasForeignKey(country => country.DefaultCurrencyCode)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ReferenceTimeZone>().WithMany().HasForeignKey(country => country.DefaultTimeZoneId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
