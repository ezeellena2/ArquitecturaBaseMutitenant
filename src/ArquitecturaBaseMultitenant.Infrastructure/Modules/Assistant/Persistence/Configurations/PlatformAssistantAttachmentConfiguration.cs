using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant.Persistence.Configurations;

internal sealed class PlatformAssistantAttachmentConfiguration : IEntityTypeConfiguration<PlatformAssistantAttachment>
{
    public void Configure(EntityTypeBuilder<PlatformAssistantAttachment> builder)
    {
        builder.ToTable("PlatformAssistantAttachments", Schemas.Platform);
        builder.HasKey(value => value.Id);
        builder.Property(value => value.FileName).HasMaxLength(TextLimits.Description).IsRequired();
        builder.Property(value => value.ContentType).HasMaxLength(TextLimits.ShortName).IsRequired();
        builder.Property(value => value.StorageKey).HasMaxLength(TextLimits.WebAddress).IsRequired();
        builder.HasOne<PlatformAssistantConversation>().WithMany().HasForeignKey(value => value.ConversationId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(value => new { value.ConversationId, value.CreatedAtUtc, value.Id });
        builder.HasOne<PlatformAssistantMessage>().WithMany().HasForeignKey(value => new { value.ConversationId, value.MessageId })
            .HasPrincipalKey(value => new { value.ConversationId, value.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}
