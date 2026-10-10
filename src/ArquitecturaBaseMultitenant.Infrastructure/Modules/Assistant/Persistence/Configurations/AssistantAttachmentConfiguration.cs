using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant.Persistence.Configurations;

internal sealed class AssistantAttachmentConfiguration : IEntityTypeConfiguration<AssistantAttachment>
{
    public void Configure(EntityTypeBuilder<AssistantAttachment> builder)
    {
        builder.ToTable("AssistantAttachments", Schemas.Tenant);
        builder.HasKey(value => new { value.TenantId, value.Id });
        builder.HasOne<ArquitecturaBaseMultitenant.Domain.Tenancy.Tenant>().WithMany().HasForeignKey(value => value.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(value => value.FileName).HasMaxLength(TextLimits.Description).IsRequired();
        builder.Property(value => value.ContentType).HasMaxLength(TextLimits.ShortName).IsRequired();
        builder.Property(value => value.StorageKey).HasMaxLength(TextLimits.WebAddress).IsRequired();
        builder.HasOne<AssistantConversation>().WithMany().HasForeignKey(value => new { value.TenantId, value.ConversationId }).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(value => new { value.TenantId, value.ConversationId, value.CreatedAtUtc, value.Id });
        builder.HasOne<AssistantMessage>().WithMany().HasForeignKey(value => new { value.TenantId, value.ConversationId, value.MessageId })
            .HasPrincipalKey(value => new { value.TenantId, value.ConversationId, value.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}
