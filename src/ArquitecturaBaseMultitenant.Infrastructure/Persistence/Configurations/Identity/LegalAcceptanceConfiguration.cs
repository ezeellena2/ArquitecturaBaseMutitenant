using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Legal;
using ArquitecturaBaseMultitenant.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Configurations.Identity;

/// <summary>Mapea qué versión legal aceptó cada cuenta y cuándo. Conserva una aceptación única por documento y usuario para probar el consentimiento.</summary>
internal sealed class LegalAcceptanceConfiguration : IEntityTypeConfiguration<LegalAcceptance>
{
    public void Configure(EntityTypeBuilder<LegalAcceptance> builder)
    {
        builder.ToTable("LegalAcceptances", Schemas.Identity);
        builder.HasKey(acceptance => acceptance.Id);
        builder.Property(acceptance => acceptance.IpAddress).HasMaxLength(TextLimits.ShortName);
        builder.Property(acceptance => acceptance.UserAgent).HasMaxLength(TextLimits.Description);
        builder.HasIndex(acceptance => new { acceptance.UserId, acceptance.LegalDocumentId }).IsUnique();
        builder.HasOne<ApplicationUser>().WithMany()
            .HasForeignKey(acceptance => acceptance.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<LegalDocument>().WithMany()
            .HasForeignKey(acceptance => acceptance.LegalDocumentId).OnDelete(DeleteBehavior.Restrict);
        foreach (var property in builder.Metadata.GetProperties().Where(property =>
            property.Name is not (nameof(LegalAcceptance.IpAddress) or nameof(LegalAcceptance.UserAgent))))
        {
            property.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        }
    }
}
