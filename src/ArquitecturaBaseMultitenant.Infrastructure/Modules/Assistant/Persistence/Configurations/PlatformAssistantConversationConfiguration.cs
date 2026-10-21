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
        builder.HasBaseType((Type?)null);
        builder.ToTable("PlatformAssistantConversations", Schemas.Platform);
        builder.HasKey(value => value.Id);
        builder.Ignore(value => value.Title);
        builder.Ignore(value => value.LastActivityAtUtc);
        builder.Property(value => value.ManualTitle).HasColumnName("Title").HasMaxLength(TextLimits.AssistantTitle);
        builder.Navigation(value => value.Activity).IsRequired().AutoInclude();
    }
}
