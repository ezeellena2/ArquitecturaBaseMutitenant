using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Configurations.Tenant;

/// <summary>Mapea la pertenencia a una empresa como dato privado y versionado de la organización.</summary>
internal sealed class CompanyMembershipConfiguration : IEntityTypeConfiguration<CompanyMembership>
{
    public void Configure(EntityTypeBuilder<CompanyMembership> builder)
    {
        builder.ToTable("CompanyMemberships", Schemas.Tenant);
        builder.HasKey(membership => new { membership.TenantId, membership.Id });
        builder.Property(membership => membership.Status).HasConversion<string>()
            .HasMaxLength(TextLimits.ShortName).IsRequired();
        builder.HasIndex(membership => new { membership.TenantId, membership.CompanyId, membership.MemberId })
            .IsUnique();
        builder.HasOne<Company>().WithMany()
            .HasForeignKey(membership => new { membership.TenantId, membership.CompanyId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Member>().WithMany()
            .HasForeignKey(membership => new { membership.TenantId, membership.MemberId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
