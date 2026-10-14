using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp.Persistence;

internal sealed class PlatformWhatsAppChatMessageConfiguration : IEntityTypeConfiguration<PlatformWhatsAppChatMessage>
{
    public void Configure(EntityTypeBuilder<PlatformWhatsAppChatMessage> builder)
    {
        builder.HasBaseType((Type?)null);
        builder.ToTable("PlatformWhatsAppChatMessages", Schemas.Platform);
        builder.HasKey(value => value.Id);
        WhatsAppChatMapping.ConfigureMessage(builder);
        builder.HasOne<PlatformWhatsAppChat>().WithMany().HasForeignKey(value => value.ChatId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(value => new { value.ChatId, value.CreatedAtUtc, value.Id });
        builder.HasIndex(value => value.Wamid).IsUnique().HasFilter("\"Wamid\" IS NOT NULL");
        builder.HasIndex(value => value.OutboxMessageId);
    }
}
