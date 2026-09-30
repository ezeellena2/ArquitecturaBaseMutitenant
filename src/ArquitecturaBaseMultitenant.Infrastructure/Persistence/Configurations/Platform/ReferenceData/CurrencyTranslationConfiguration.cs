using ArquitecturaBaseMultitenant.Domain.ReferenceData;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Configurations.Platform.ReferenceData;

/// <summary>Mapea nombre, plural y símbolo de presentación de cada moneda por cultura. La moneda y la cultura se validan mediante claves foráneas globales.</summary>
internal sealed class CurrencyTranslationConfiguration : IEntityTypeConfiguration<CurrencyTranslation>
{
    public void Configure(EntityTypeBuilder<CurrencyTranslation> builder)
    {
        builder.ToTable("CurrencyTranslations", "platform");
        builder.HasKey(translation => new { translation.CurrencyCode, translation.Culture });
        builder.Property(translation => translation.CurrencyCode).HasColumnType("character(3)").IsRequired();
        builder.Property(translation => translation.Culture).IsRequired();
        builder.Property(translation => translation.Name).IsRequired();
        builder.Property(translation => translation.NamePlural).IsRequired();
        builder.Property(translation => translation.DisplaySymbol).IsRequired();
        builder.HasOne<Currency>().WithMany().HasForeignKey(translation => translation.CurrencyCode)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Culture>().WithMany().HasForeignKey(translation => translation.Culture)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
