using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.ReferenceData;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TenantEntity = ArquitecturaBaseMultitenant.Domain.Tenancy.Tenant;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Configurations.Tenant;

/// <summary>Mapea empresas como datos privados de una organización y conserva su versión PostgreSQL.</summary>
internal sealed class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> builder)
    {
        builder.ToTable("Companies", Schemas.Tenant);
        builder.HasKey(company => new { company.TenantId, company.Id });
        builder.Property(company => company.Name).HasMaxLength(TextLimits.OrganizationName).IsRequired();
        builder.Property(company => company.TimeZoneId).IsRequired();
        builder.Property(company => company.Status).HasConversion<string>().HasMaxLength(TextLimits.ShortCode)
            .HasDefaultValue(CompanyStatus.Active).IsRequired();
        builder.Property(company => company.TaxCountry).HasColumnName("TaxCountry").HasColumnType("character(2)");
        builder.Property(company => company.TaxType).HasColumnName("TaxType");
        builder.Property(company => company.TaxNumber).HasColumnName("TaxNumber");
        builder.Ignore(company => company.TaxId);
        builder.HasIndex(company => new { company.TenantId, company.Name, company.Id });
        builder.HasIndex(company => new
        {
            company.TenantId, company.TaxCountry, company.TaxType, company.TaxNumber,
        }).IsUnique().HasFilter("\"TaxCountry\" IS NOT NULL").HasDatabaseName(UniqueKeys.CompanyTaxId);
        builder.HasOne<TenantEntity>().WithMany()
            .HasForeignKey(company => company.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ReferenceTimeZone>().WithMany()
            .HasForeignKey(company => company.TimeZoneId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Country>().WithMany()
            .HasForeignKey(company => company.TaxCountry).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<TaxIdType>().WithMany()
            .HasForeignKey(company => company.TaxType).OnDelete(DeleteBehavior.Restrict);
    }
}
