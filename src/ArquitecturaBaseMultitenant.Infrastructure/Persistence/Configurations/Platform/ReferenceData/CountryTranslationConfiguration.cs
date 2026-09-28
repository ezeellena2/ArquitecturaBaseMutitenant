using ArquitecturaBaseMultitenant.Domain.ReferenceData;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Configurations.Platform.ReferenceData;

internal sealed class CountryTranslationConfiguration : IEntityTypeConfiguration<CountryTranslation>
{
    public void Configure(EntityTypeBuilder<CountryTranslation> builder)
    {
        builder.ToTable("CountryTranslations", "platform");
        builder.HasKey(translation => new { translation.CountryCode, translation.Culture });
        builder.Property(translation => translation.CountryCode).HasColumnType("character(2)").IsRequired();
        builder.Property(translation => translation.Culture).IsRequired();
        builder.Property(translation => translation.Name).IsRequired();
        builder.HasOne<Country>().WithMany().HasForeignKey(translation => translation.CountryCode)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Culture>().WithMany().HasForeignKey(translation => translation.Culture)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
