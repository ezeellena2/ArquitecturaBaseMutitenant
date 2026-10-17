using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp.Persistence;

/// <summary>Las copias de una conversación vinculada: se van con el vínculo.</summary>
internal sealed class WhatsAppLinkedMessageConfiguration : IEntityTypeConfiguration<WhatsAppLinkedMessage>
{
    public void Configure(EntityTypeBuilder<WhatsAppLinkedMessage> builder)
    {
        builder.ToTable("WhatsAppLinkedMessages", Schemas.Engagement);
        builder.HasKey(value => value.Id);
        builder.Property(value => value.Direction).HasConversion<string>().HasMaxLength(TextLimits.ShortName);
        builder.Property(value => value.MediaKind).HasConversion<string>().HasMaxLength(TextLimits.ShortName);
        builder.Property(value => value.Text).HasMaxLength(WhatsAppTextLimits.MessageText);
        builder.HasIndex(value => new { value.LinkId, value.CreatedAtUtc, value.Id });
        builder.HasIndex(value => value.ConsumerTenantId);
        builder.HasIndex(value => value.BusinessTenantId);
        builder.HasOne<WhatsAppConversationLink>().WithMany().HasForeignKey(value => value.LinkId).OnDelete(DeleteBehavior.Cascade);
    }
}
