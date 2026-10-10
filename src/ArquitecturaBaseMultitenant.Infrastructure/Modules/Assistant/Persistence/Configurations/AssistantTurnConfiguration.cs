using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant.Persistence.Configurations;

internal sealed class AssistantTurnConfiguration : IEntityTypeConfiguration<AssistantTurn>
{
    public void Configure(EntityTypeBuilder<AssistantTurn> builder)
    {
        builder.ToTable("AssistantTurns", Schemas.Tenant);
        builder.HasKey(value => new { value.TenantId, value.Id });
        builder.HasOne<ArquitecturaBaseMultitenant.Domain.Tenancy.Tenant>().WithMany().HasForeignKey(value => value.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(value => value.Status).HasMaxLength(TextLimits.ShortName).IsRequired();
        builder.HasOne<AssistantConversation>().WithMany().HasForeignKey(value => new { value.TenantId, value.ConversationId }).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(value => new { value.TenantId, value.ConversationId, value.CreatedAtUtc, value.Id });
    }
}
