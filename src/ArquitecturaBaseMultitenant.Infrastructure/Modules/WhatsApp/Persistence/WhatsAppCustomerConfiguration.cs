using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp.Persistence;

/// <summary>Contactos privados de la organización: un número por organización, con etiquetas y consentimiento.</summary>
internal sealed class WhatsAppCustomerConfiguration : IEntityTypeConfiguration<WhatsAppCustomer>
{
    public void Configure(EntityTypeBuilder<WhatsAppCustomer> builder)
    {
        builder.ToTable("WhatsAppCustomers", Schemas.Tenant);
        builder.HasKey(value => new { value.TenantId, value.Id });
        builder.HasOne<ArquitecturaBaseMultitenant.Domain.Tenancy.Tenant>().WithMany().HasForeignKey(value => value.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(value => value.WaId).HasMaxLength(PhoneNumber.MaxLength).IsRequired();
        builder.Property(value => value.Name).HasMaxLength(TextLimits.PersonDisplayName);
        builder.Property(value => value.Tags).HasField("tags").UsePropertyAccessMode(PropertyAccessMode.Field).HasColumnType("text[]").IsRequired();
        builder.Property(value => value.Consent).HasConversion<string>().HasMaxLength(TextLimits.ShortName);
        builder.Property(value => value.ConsentSource).HasConversion<string>().HasMaxLength(TextLimits.ShortName);
        builder.HasIndex(value => new { value.TenantId, value.WaId }).IsUnique();
        builder.HasIndex(value => new { value.TenantId, value.Name, value.Id });
        builder.HasIndex(value => new { value.TenantId, value.Consent, value.Id });
        builder.HasIndex(value => value.Tags).HasMethod("gin");
    }
}
