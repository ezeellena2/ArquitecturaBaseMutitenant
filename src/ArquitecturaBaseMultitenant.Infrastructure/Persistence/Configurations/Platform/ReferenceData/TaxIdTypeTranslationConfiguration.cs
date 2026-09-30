using ArquitecturaBaseMultitenant.Domain.ReferenceData;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Configurations.Platform.ReferenceData;

/// <summary>Mapea el nombre localizado de cada tipo fiscal por cultura. Mantiene una sola traducción por combinación de tipo e idioma.</summary>
internal sealed class TaxIdTypeTranslationConfiguration : IEntityTypeConfiguration<TaxIdTypeTranslation>
{
    public void Configure(EntityTypeBuilder<TaxIdTypeTranslation> builder)
    {
        builder.ToTable("TaxIdTypeTranslations", "platform");
        builder.HasKey(translation => new { translation.TaxIdTypeCode, translation.Culture });
        builder.Property(translation => translation.TaxIdTypeCode).IsRequired();
        builder.Property(translation => translation.Culture).IsRequired();
        builder.Property(translation => translation.Name).IsRequired();
        builder.HasOne<TaxIdType>().WithMany().HasForeignKey(translation => translation.TaxIdTypeCode)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Culture>().WithMany().HasForeignKey(translation => translation.Culture)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
