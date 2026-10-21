using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant.Persistence.Configurations;

internal sealed class PlatformAssistantConversationActivityConfiguration : IEntityTypeConfiguration<PlatformAssistantConversationActivity>
{
    public void Configure(EntityTypeBuilder<PlatformAssistantConversationActivity> builder)
    {
        builder.HasBaseType((Type?)null);
        builder.ToTable("PlatformAssistantConversationActivities", Schemas.Platform);
        builder.HasKey(value => value.Id);
        builder.Property(value => value.Id).ValueGeneratedNever();
        builder.HasOne<PlatformAssistantConversation>().WithOne(value => value.Activity)
            .HasForeignKey<PlatformAssistantConversationActivity>(value => value.Id).OnDelete(DeleteBehavior.Cascade);
        builder.Property(value => value.AutomaticTitle).HasMaxLength(TextLimits.AssistantTitle);
        builder.Property(value => value.Access).HasConversion<string>().HasMaxLength(TextLimits.ShortName);
        builder.Property(value => value.OwnerUserId).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        builder.Property(value => value.Access).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        builder.HasIndex(value => new { value.OwnerUserId, value.LastActivityAtUtc, value.Id });
    }
}
