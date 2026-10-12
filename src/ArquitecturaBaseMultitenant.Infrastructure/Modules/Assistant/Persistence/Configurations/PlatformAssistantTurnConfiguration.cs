using UniqueKeys = ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence.UniqueKeys;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant.Persistence.Configurations;

internal sealed class PlatformAssistantTurnConfiguration : IEntityTypeConfiguration<PlatformAssistantTurn>
{
    public void Configure(EntityTypeBuilder<PlatformAssistantTurn> builder)
    {
        builder.ToTable("PlatformAssistantTurns", Schemas.Platform);
        builder.HasKey(value => value.Id);
        builder.Property(value => value.Status).HasMaxLength(TextLimits.ShortName).IsRequired();
        builder.HasOne<PlatformAssistantConversation>().WithMany().HasForeignKey(value => value.ConversationId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(value => new { value.ConversationId, value.CreatedAtUtc, value.Id });
        builder.HasIndex(value => value.ConversationId).IsUnique().HasDatabaseName(UniqueKeys.PlatformAssistantActiveTurn).HasFilter("\"Status\" = 'Pending' OR \"Status\" = 'Running'");
        builder.Property(value => value.EventsJson).HasColumnType("jsonb");
        builder.Property(value => value.ContextMessageIds).HasColumnType("uuid[]");
        builder.Property(value => value.AdmissionVersion).HasConversion<long?>().HasColumnType("bigint");
    }
}
