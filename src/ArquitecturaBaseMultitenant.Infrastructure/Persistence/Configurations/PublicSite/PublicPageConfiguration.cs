using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.PublicSite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TenantEntity = ArquitecturaBaseMultitenant.Domain.Tenancy.Tenant;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Configurations.PublicSite;

internal sealed class PublicPageConfiguration : IEntityTypeConfiguration<PublicPage>
{
    public void Configure(EntityTypeBuilder<PublicPage> builder)
    {
        builder.ToTable("PublicPages", Schemas.PublicSite);
        builder.HasKey(page => new { page.BusinessTenantId, page.Id });
        builder.Property(page => page.Name).HasMaxLength(TextLimits.OrganizationName).IsRequired();
        builder.Property(page => page.Description).HasMaxLength(TextLimits.Description);
        builder.Property(page => page.LogoBytes).HasColumnType("bytea");
        builder.Property(page => page.LogoContentType).HasMaxLength(20);
        builder.Property(page => page.ContactEmail).HasMaxLength(254);
        builder.Property(page => page.ContactPhone).HasMaxLength(100);
        builder.Property(page => page.Address).HasMaxLength(500);
        builder.Property(page => page.Website).HasMaxLength(2048);
        builder.Property(page => page.PublishBlockedReason).HasMaxLength(TextLimits.Description);
        builder.HasIndex(page => page.BusinessTenantId).IsUnique();
        builder.HasOne<TenantEntity>().WithMany()
            .HasForeignKey(page => page.BusinessTenantId).OnDelete(DeleteBehavior.Restrict);
    }
}
