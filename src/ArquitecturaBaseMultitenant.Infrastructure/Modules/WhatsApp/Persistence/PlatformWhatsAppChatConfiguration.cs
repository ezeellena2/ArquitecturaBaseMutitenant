using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp.Persistence;

/// <summary>Las consultas al número de Plataforma; no tienen bandejas ni contactos.</summary>
internal sealed class PlatformWhatsAppChatConfiguration : IEntityTypeConfiguration<PlatformWhatsAppChat>
{
    public void Configure(EntityTypeBuilder<PlatformWhatsAppChat> builder)
    {
        builder.HasBaseType((Type?)null);
        builder.ToTable("PlatformWhatsAppChats", Schemas.Platform);
        builder.HasKey(value => value.Id);
        WhatsAppChatMapping.Configure(builder);
        builder.HasIndex(value => new { value.ChannelId, value.WaId }).IsUnique();
        builder.HasIndex(value => new { value.LastMessageAtUtc, value.Id });
        builder.HasIndex(value => new { value.TeamId, value.WaitingSinceUtc });
    }
}
