using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant.Persistence.Configurations;

internal sealed class AssistantTurnWorkConfiguration : IEntityTypeConfiguration<AssistantTurnWork>
{
    public void Configure(EntityTypeBuilder<AssistantTurnWork> builder)
    {
        builder.ToTable("AssistantTurnWorks", Schemas.Platform);
        builder.HasKey(row => row.Id);
        builder.Ignore(row => row.IsTerminal);
        builder.Property(row => row.Access).HasConversion<string>().HasMaxLength(TextLimits.ShortName);
        builder.Property(row => row.TenantKind).HasConversion<string>().HasMaxLength(TextLimits.ShortName);
        builder.Property(row => row.Funding).HasConversion<string>().HasMaxLength(TextLimits.ShortName);
        builder.Property(row => row.Status).HasConversion<string>().HasMaxLength(TextLimits.ShortName);
        builder.Property(row => row.Effort).HasMaxLength(TextLimits.ShortName);
        builder.Property(row => row.ProviderModelId).HasMaxLength(TextLimits.ShortName);
        builder.Property(row => row.Culture).HasMaxLength(TextLimits.ShortName);
        builder.Property(row => row.TimeZoneId).HasMaxLength(TextLimits.ShortName);
        builder.Property(row => row.EngineVersion).HasDefaultValue(1);
        builder.HasIndex(row => new { row.Status, row.LeaseExpiresAtUtc, row.CreatedAtUtc, row.Id });
        // 0064 §5 y §6: el worker busca solo lo pendiente o en curso, en su orden, sin recorrer lo terminado.
        builder.HasIndex(row => new { row.CreatedAtUtc, row.Id }).HasDatabaseName("IX_AssistantTurnWorks_Open")
            .HasFilter("\"Status\" = 'Pending' OR \"Status\" = 'Running'");
        builder.HasIndex(row => new { row.OwnerUserId, row.ScopeTenantId, row.Id });
    }
}
