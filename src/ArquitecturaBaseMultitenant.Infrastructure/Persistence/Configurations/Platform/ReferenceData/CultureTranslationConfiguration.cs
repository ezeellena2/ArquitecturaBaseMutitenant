using ArquitecturaBaseMultitenant.Domain.ReferenceData;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Configurations.Platform.ReferenceData;

/// <summary>Mapea el nombre de una cultura mostrado en otra cultura. Ambas referencias apuntan al catálogo global de perfiles.</summary>
internal sealed class CultureTranslationConfiguration : IEntityTypeConfiguration<CultureTranslation>
{
    public void Configure(EntityTypeBuilder<CultureTranslation> builder)
    {
        builder.ToTable("CultureTranslations", "platform");
        builder.HasKey(translation => new { translation.CultureCode, translation.DisplayCulture });
        builder.Property(translation => translation.CultureCode).IsRequired();
        builder.Property(translation => translation.DisplayCulture).IsRequired();
        builder.Property(translation => translation.Name).IsRequired();
        builder.HasOne<Culture>().WithMany().HasForeignKey(translation => translation.CultureCode)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Culture>().WithMany().HasForeignKey(translation => translation.DisplayCulture)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
