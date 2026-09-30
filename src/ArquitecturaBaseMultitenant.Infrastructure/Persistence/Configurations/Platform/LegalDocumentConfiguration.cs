using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Legal;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Configurations.Platform;

/// <summary>Mapea las versiones publicadas de términos y privacidad. Una versión es única por tipo y se busca por fecha de vigencia.</summary>
internal sealed class LegalDocumentConfiguration : IEntityTypeConfiguration<LegalDocument>
{
    public void Configure(EntityTypeBuilder<LegalDocument> builder)
    {
        builder.ToTable("LegalDocuments", Schemas.Platform);
        builder.HasKey(document => document.Id);
        builder.Property(document => document.Kind).HasConversion<string>().HasMaxLength(TextLimits.ShortName);
        builder.HasIndex(document => new { document.Kind, document.Version }).IsUnique();
        builder.HasIndex(document => new { document.Kind, document.EffectiveAtUtc });
        foreach (var property in builder.Metadata.GetProperties())
        {
            property.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        }
    }
}
