using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant.Persistence.Configurations;

/// <summary>
/// La propuesta de un turno (ADR 0068 §8), privada y con RLS, sin auditoría de entidad porque su contenido es de un chat, como
/// los mensajes. Se borra con su mensaje, que se borra con su conversación.
/// </summary>
internal sealed class AssistantProposalConfiguration : IEntityTypeConfiguration<AssistantProposal>
{
    public void Configure(EntityTypeBuilder<AssistantProposal> builder)
    {
        builder.ToTable("AssistantProposals", Schemas.Tenant);
        builder.HasKey(value => new { value.TenantId, value.Id });
        builder.HasOne<ArquitecturaBaseMultitenant.Domain.Tenancy.Tenant>().WithMany().HasForeignKey(value => value.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AssistantMessage>().WithMany().HasForeignKey(value => new { value.TenantId, value.ConversationId, value.MessageId })
            .HasPrincipalKey(value => new { value.TenantId, value.ConversationId, value.Id }).OnDelete(DeleteBehavior.Cascade);
        builder.Property(value => value.Kind).HasMaxLength(TextLimits.ShortName).IsRequired();
        builder.Property(value => value.ActionId).HasMaxLength(AssistantProposal.ActionIdLength).IsRequired();
        builder.Property(value => value.Values).HasColumnType("jsonb").IsRequired();
        builder.Property(value => value.BaseVersion).HasConversion<long?>().HasColumnType("bigint");
        builder.Property(value => value.State).HasConversion<string>().HasMaxLength(TextLimits.ShortName).IsRequired();
        builder.Property(value => value.AttemptKeys).HasColumnType("jsonb").IsRequired();
        builder.Property(value => value.AppliedLocation).HasMaxLength(AssistantProposal.AppliedLocationLength);
        builder.Ignore(value => value.ActionKey);
        // Reemplazar dentro de su clase: la propuesta nueva de una conversación.
        builder.HasIndex(value => new { value.TenantId, value.ConversationId, value.Kind }).HasFilter("\"State\" = 'New'");
        builder.HasIndex(value => new { value.TenantId, value.ConversationId, value.MessageId }).IsUnique();
    }
}
