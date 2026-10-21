using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant.Persistence.Configurations;

internal sealed class AssistantConversationActivityConfiguration : IEntityTypeConfiguration<AssistantConversationActivity>
{
    public void Configure(EntityTypeBuilder<AssistantConversationActivity> builder)
    {
        builder.HasBaseType((Type?)null);
        builder.ToTable("AssistantConversationActivities", Schemas.Tenant);
        builder.HasKey(value => new { value.TenantId, value.Id });
        builder.Property(value => value.Id).ValueGeneratedNever();
        builder.HasOne<AssistantConversation>().WithOne(value => value.Activity)
            .HasForeignKey<AssistantConversationActivity>(value => new { value.TenantId, value.Id }).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<ArquitecturaBaseMultitenant.Domain.Tenancy.Tenant>().WithMany()
            .HasForeignKey(value => value.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(value => value.AutomaticTitle).HasMaxLength(TextLimits.AssistantTitle);
        builder.Property(value => value.Access).HasConversion<string>().HasMaxLength(TextLimits.ShortName);
        builder.Property(value => value.OwnerUserId).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        builder.Property(value => value.Access).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        builder.HasIndex(value => new { value.TenantId, value.OwnerUserId, value.Access, value.LastActivityAtUtc, value.Id });
        builder.HasIndex(value => new { value.TenantId, value.LastActivityAtUtc, value.Id });
    }
}
