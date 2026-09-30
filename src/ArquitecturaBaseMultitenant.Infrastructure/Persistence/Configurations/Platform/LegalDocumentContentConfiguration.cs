using ArquitecturaBaseMultitenant.Domain.Legal;
using ArquitecturaBaseMultitenant.Domain.ReferenceData;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Configurations.Platform;

/// <summary>Mapea el texto de una versión legal por cultura. El documento se elimina con su contenido; la cultura debe seguir existiendo en el catálogo.</summary>
internal sealed class LegalDocumentContentConfiguration : IEntityTypeConfiguration<LegalDocumentContent>
{
    public void Configure(EntityTypeBuilder<LegalDocumentContent> builder)
    {
        builder.ToTable("LegalDocumentContents", Schemas.Platform);
        builder.HasKey(content => new { content.LegalDocumentId, content.Culture });
        builder.Property(content => content.Culture).IsRequired();
        builder.Property(content => content.Text).HasColumnType("text").IsRequired();
        builder.HasOne<LegalDocument>().WithMany()
            .HasForeignKey(content => content.LegalDocumentId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Culture>().WithMany()
            .HasForeignKey(content => content.Culture).OnDelete(DeleteBehavior.Restrict);
        foreach (var property in builder.Metadata.GetProperties())
        {
            property.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        }
    }
}
