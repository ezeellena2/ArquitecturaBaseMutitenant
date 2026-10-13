using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant.Persistence.Configurations;

internal sealed class AssistantConversationConfiguration : IEntityTypeConfiguration<AssistantConversation>
{
    public void Configure(EntityTypeBuilder<AssistantConversation> builder)
    {
        builder.HasBaseType((Type?)null);
        builder.ToTable("AssistantConversations", Schemas.Tenant);
        builder.HasKey(value => new { value.TenantId, value.Id });
        builder.HasOne<ArquitecturaBaseMultitenant.Domain.Tenancy.Tenant>().WithMany().HasForeignKey(value => value.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(value => value.Title).HasMaxLength(TextLimits.AssistantTitle);
        builder.Property(value => value.Access).HasConversion<string>().HasMaxLength(TextLimits.ShortName);
        builder.HasIndex(value => new { value.TenantId, value.OwnerUserId, value.Access, value.LastActivityAtUtc, value.Id });
        builder.HasIndex(value => new { value.TenantId, value.LastActivityAtUtc, value.Id });
    }
}
