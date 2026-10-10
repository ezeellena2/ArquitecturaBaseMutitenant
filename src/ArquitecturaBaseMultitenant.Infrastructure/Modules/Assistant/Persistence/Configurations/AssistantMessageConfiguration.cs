using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant.Persistence.Configurations;

internal sealed class AssistantMessageConfiguration : IEntityTypeConfiguration<AssistantMessage>
{
    public void Configure(EntityTypeBuilder<AssistantMessage> builder)
    {
        builder.ToTable("AssistantMessages", Schemas.Tenant);
        builder.HasKey(value => new { value.TenantId, value.Id });
        builder.HasAlternateKey(value => new { value.TenantId, value.ConversationId, value.Id });
        builder.HasOne<ArquitecturaBaseMultitenant.Domain.Tenancy.Tenant>().WithMany().HasForeignKey(value => value.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(value => value.Role).HasMaxLength(TextLimits.ShortName).IsRequired();
        builder.Property(value => value.Status).HasMaxLength(TextLimits.ShortName).IsRequired();
        builder.Property(value => value.Text).HasMaxLength(TextLimits.LegalDocument);
        builder.Property(value => value.ToolKey).HasMaxLength(TextLimits.ShortName);
        builder.Property(value => value.ToolState).HasMaxLength(TextLimits.ShortName);
        builder.Property(value => value.LinksJson).HasColumnType("jsonb");
        builder.HasOne<AssistantConversation>().WithMany().HasForeignKey(value => new { value.TenantId, value.ConversationId }).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(value => new { value.TenantId, value.ConversationId, value.CreatedAtUtc, value.Id });
    }
}
