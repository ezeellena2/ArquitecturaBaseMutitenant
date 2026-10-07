using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.PublicSite;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
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
        builder.Property(page => page.LogoContentType).HasMaxLength(TextLimits.ShortCode);
        builder.Property(page => page.ContactEmail).HasMaxLength(Email.MaxLength);
        builder.Property(page => page.Address).HasMaxLength(TextLimits.Description);
        builder.Property(page => page.Website).HasMaxLength(TextLimits.WebAddress);
        builder.Property(page => page.PublishBlockedReason).HasMaxLength(TextLimits.Description);
        builder.HasIndex(page => page.BusinessTenantId).IsUnique().HasDatabaseName(UniqueKeys.PublicPage);
        builder.HasIndex(page => new { page.Name, page.Id });
        builder.HasOne<TenantEntity>().WithMany()
            .HasForeignKey(page => page.BusinessTenantId).OnDelete(DeleteBehavior.Restrict);
    }
}
