using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp.Persistence;

/// <summary>La configuración de WhatsApp de cada organización (272): una fila por organización, privada y con RLS.</summary>
internal sealed class WhatsAppOrganizationSettingsConfiguration : IEntityTypeConfiguration<WhatsAppOrganizationSettings>
{
    public void Configure(EntityTypeBuilder<WhatsAppOrganizationSettings> builder)
    {
        builder.ToTable("WhatsAppOrganizationSettings", Schemas.Tenant, table =>
            table.HasCheckConstraint("CK_WhatsAppOrganizationSettings_ChatRetentionDays",
                $"\"ChatRetentionDays\" BETWEEN {WhatsAppSettingsLimits.MinChatRetentionDays} AND {WhatsAppSettingsLimits.MaxChatRetentionDays}"));
        builder.HasKey(value => new { value.TenantId, value.Id });
        builder.HasOne<ArquitecturaBaseMultitenant.Domain.Tenancy.Tenant>().WithMany().HasForeignKey(value => value.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(value => value.TenantId).IsUnique();
    }
}
