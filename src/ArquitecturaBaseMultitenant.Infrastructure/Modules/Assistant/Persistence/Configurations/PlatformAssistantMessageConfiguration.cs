using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant.Persistence.Configurations;

internal sealed class PlatformAssistantMessageConfiguration : IEntityTypeConfiguration<PlatformAssistantMessage>
{
    public void Configure(EntityTypeBuilder<PlatformAssistantMessage> builder)
    {
        builder.HasBaseType((Type?)null);
        builder.ToTable("PlatformAssistantMessages", Schemas.Platform);
        builder.HasKey(value => value.Id);
        builder.HasAlternateKey(value => new { value.ConversationId, value.Id });
        builder.Property(value => value.Role).HasMaxLength(TextLimits.ShortName).IsRequired();
        builder.Property(value => value.Status).HasConversion<string>().HasMaxLength(TextLimits.ShortName).IsRequired();
        builder.Property(value => value.Text).HasMaxLength(TextLimits.LegalDocument);
        builder.Property(value => value.ToolKey).HasMaxLength(TextLimits.ShortName);
        builder.Property(value => value.ToolState).HasConversion<string>().HasMaxLength(TextLimits.ShortName);
        builder.Property(value => value.LinksJson).HasColumnType("jsonb");
        builder.Property(value => value.SubjectUserIds).HasColumnType("uuid[]");
        builder.HasIndex(value => value.SubjectUserIds).HasMethod("gin");
        builder.HasOne<PlatformAssistantConversation>().WithMany().HasForeignKey(value => value.ConversationId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(value => new { value.ConversationId, value.CreatedAtUtc, value.Id });
    }
}
