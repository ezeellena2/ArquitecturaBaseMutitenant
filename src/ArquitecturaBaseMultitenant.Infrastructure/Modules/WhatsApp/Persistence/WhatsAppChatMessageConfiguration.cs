using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp.Persistence;

/// <summary>Mensajes de las conversaciones de la organización; un identificador de Meta entra una sola vez.</summary>
internal sealed class WhatsAppChatMessageConfiguration : IEntityTypeConfiguration<WhatsAppChatMessage>
{
    public void Configure(EntityTypeBuilder<WhatsAppChatMessage> builder)
    {
        builder.HasBaseType((Type?)null);
        builder.ToTable("WhatsAppChatMessages", Schemas.Tenant);
        builder.HasKey(value => new { value.TenantId, value.Id });
        builder.HasOne<ArquitecturaBaseMultitenant.Domain.Tenancy.Tenant>().WithMany().HasForeignKey(value => value.TenantId).OnDelete(DeleteBehavior.Restrict);
        WhatsAppChatMapping.ConfigureMessage(builder);
        builder.HasOne<WhatsAppChat>().WithMany().HasForeignKey(value => new { value.TenantId, value.ChatId }).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(value => new { value.TenantId, value.ChatId, value.CreatedAtUtc, value.Id });
        builder.HasIndex(value => new { value.TenantId, value.Wamid }).IsUnique().HasFilter("\"Wamid\" IS NOT NULL");
        builder.HasIndex(value => new { value.TenantId, value.OutboxMessageId });
    }
}
