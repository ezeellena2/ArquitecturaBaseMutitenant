using ArquitecturaBaseMultitenant.Domain.ReferenceData;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Configurations.Platform.ReferenceData;

/// <summary>Mapea los patrones de fecha, número y moneda que usan los formatos compartidos. La cultura tiene un país y puede indicar otra cultura como caída.</summary>
internal sealed class CultureConfiguration : IEntityTypeConfiguration<Culture>
{
    public void Configure(EntityTypeBuilder<Culture> builder)
    {
        builder.ToTable("Cultures", "platform");
        builder.HasKey(culture => culture.Code);
        builder.Property(culture => culture.Code).IsRequired();
        builder.Property(culture => culture.LanguageCode).IsRequired();
        builder.Property(culture => culture.CountryCode).HasColumnType("character(2)").IsRequired();
        builder.Property(culture => culture.DatePattern).IsRequired();
        builder.Property(culture => culture.TimePattern).IsRequired();
        builder.Property(culture => culture.DateTimePattern).IsRequired();
        builder.Property(culture => culture.LongDatePattern).IsRequired();
        builder.Property(culture => culture.AmDesignator).IsRequired();
        builder.Property(culture => culture.PmDesignator).IsRequired();
        builder.Property(culture => culture.DecimalSeparator).IsRequired();
        builder.Property(culture => culture.GroupSeparator).IsRequired();
        builder.Property(culture => culture.CurrencyPattern).IsRequired();
        builder.Property(culture => culture.PercentPattern).IsRequired();
        builder.HasOne<Country>().WithMany().HasForeignKey(culture => culture.CountryCode)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Culture>().WithMany().HasForeignKey(culture => culture.FallbackCulture)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
