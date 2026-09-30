using ArquitecturaBaseMultitenant.Domain.ReferenceData;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Configurations.Platform.ReferenceData;

/// <summary>Mapea los tipos de identificación fiscal asociados a un país y sus reglas de presentación. El código natural es la clave del catálogo.</summary>
internal sealed class TaxIdTypeConfiguration : IEntityTypeConfiguration<TaxIdType>
{
    public void Configure(EntityTypeBuilder<TaxIdType> builder)
    {
        builder.ToTable("TaxIdTypes", "platform");
        builder.HasKey(type => type.Code);
        builder.Property(type => type.Code).IsRequired();
        builder.Property(type => type.CountryCode).HasColumnType("character(2)").IsRequired();
        builder.Property(type => type.Label).IsRequired();
        builder.Property(type => type.Mask).IsRequired();
        builder.Property(type => type.ValidatorKey).IsRequired();
        builder.Property(type => type.AppliesTo).IsRequired();
        builder.HasOne<Country>().WithMany().HasForeignKey(type => type.CountryCode)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
