using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp.Persistence;

/// <summary>Conversaciones privadas de la organización: una por número, remitente y bandeja. Se borran con su contacto (D4).</summary>
internal sealed class WhatsAppChatConfiguration : IEntityTypeConfiguration<WhatsAppChat>
{
    public void Configure(EntityTypeBuilder<WhatsAppChat> builder)
    {
        builder.HasBaseType((Type?)null);
        builder.ToTable("WhatsAppChats", Schemas.Tenant);
        builder.HasKey(value => new { value.TenantId, value.Id });
        builder.HasOne<ArquitecturaBaseMultitenant.Domain.Tenancy.Tenant>().WithMany().HasForeignKey(value => value.TenantId).OnDelete(DeleteBehavior.Restrict);
        WhatsAppChatMapping.Configure(builder);
        builder.Property(value => value.Inbox).HasConversion<string>().HasMaxLength(TextLimits.ShortName);
        builder.HasIndex(value => new { value.TenantId, value.ChannelId, value.WaId, value.Inbox }).IsUnique();
        builder.HasIndex(value => new { value.TenantId, value.Inbox, value.LastMessageAtUtc, value.Id });
        builder.HasIndex(value => new { value.TenantId, value.Inbox, value.TeamId, value.WaitingSinceUtc });
        builder.HasIndex(value => new { value.TenantId, value.CustomerId });
        builder.HasOne<WhatsAppCustomer>().WithMany().HasForeignKey(value => new { value.TenantId, value.CustomerId }).OnDelete(DeleteBehavior.Cascade);
    }
}
