using ArquitecturaBaseMultitenant.Domain.ReferenceData;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Configurations.Platform.ReferenceData;

/// <summary>Mapea el catálogo ISO de monedas con código natural y unidades menores. Es la referencia de los montos, sin una lista codificada en los servicios.</summary>
internal sealed class CurrencyConfiguration : IEntityTypeConfiguration<Currency>
{
    public void Configure(EntityTypeBuilder<Currency> builder)
    {
        builder.ToTable("Currencies", "platform");
        builder.HasKey(currency => currency.Code);
        builder.Property(currency => currency.Code).HasColumnType("character(3)").IsRequired();
        builder.Property(currency => currency.NumericCode).HasColumnType("character(3)").IsRequired();
        builder.Property(currency => currency.Symbol).IsRequired();
    }
}
