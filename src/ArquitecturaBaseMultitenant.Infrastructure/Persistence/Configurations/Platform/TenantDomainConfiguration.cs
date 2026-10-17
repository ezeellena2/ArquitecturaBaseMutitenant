using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TenantEntity = ArquitecturaBaseMultitenant.Domain.Tenancy.Tenant;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Configurations.Platform;

/// <summary>Guarda reservas globales de dominio con unicidad y vínculo a la organización.</summary>
internal sealed class TenantDomainConfiguration : IEntityTypeConfiguration<TenantDomain>
{
    public void Configure(EntityTypeBuilder<TenantDomain> builder)
    {
        builder.ToTable("TenantDomains", Schemas.Platform);
        builder.HasKey(domain => domain.Id);
        builder.Property(domain => domain.Domain).HasMaxLength(OrganizationEmailDomain.MaxLength).IsRequired();
        builder.Property(domain => domain.VerificationToken).HasMaxLength(TenantDomain.MaxVerificationTokenLength)
            .IsRequired();
        builder.Property(domain => domain.Status).HasConversion<string>().HasMaxLength(TextLimits.ShortCode).IsRequired();
        builder.Property(domain => domain.CreatedAtUtc).IsRequired();
        builder.Ignore(domain => domain.TxtRecordValue);
        builder.HasIndex(domain => domain.Domain).IsUnique()
            .HasFilter("\"Status\" <> 'Removed'").HasDatabaseName(UniqueKeys.TenantDomain);
        builder.HasIndex(domain => new { domain.OrganizationId, domain.Status });
        builder.HasIndex(domain => new { domain.OrganizationId, domain.Domain, domain.Id });
        builder.HasOne<TenantEntity>().WithMany()
            .HasForeignKey(domain => domain.OrganizationId).OnDelete(DeleteBehavior.Restrict);
    }
}
