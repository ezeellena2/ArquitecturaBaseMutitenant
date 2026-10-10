using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant.Persistence.Configurations;

internal sealed class PlatformAssistantConversationConfiguration : IEntityTypeConfiguration<PlatformAssistantConversation>
{
    public void Configure(EntityTypeBuilder<PlatformAssistantConversation> builder)
    {
        builder.ToTable("PlatformAssistantConversations", Schemas.Platform);
        builder.HasKey(value => value.Id);
        builder.Property(value => value.Title).HasMaxLength(TextLimits.AssistantTitle);
        builder.HasIndex(value => new { value.OwnerUserId, value.LastActivityAtUtc, value.Id });
    }
}
