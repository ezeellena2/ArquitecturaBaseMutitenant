using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Configurations.Platform;

/// <summary>Mapea los eventos globales de seguridad, con búsquedas por actor u organización. Estos eventos acompañan cambios que no pertenecen al audit log de un tenant.</summary>
internal sealed class SecurityEventConfiguration : IEntityTypeConfiguration<SecurityEvent>
{
    public void Configure(EntityTypeBuilder<SecurityEvent> builder)
    {
        builder.ToTable("SecurityEvents", Schemas.Platform);
        builder.HasKey(securityEvent => securityEvent.Id);
        builder.Property(securityEvent => securityEvent.Type).HasConversion<string>().HasMaxLength(TextLimits.ShortName);
        builder.Property(securityEvent => securityEvent.ActorKind).HasConversion<string>().HasMaxLength(TextLimits.ShortName);
        builder.Property(securityEvent => securityEvent.Reason).HasMaxLength(TextLimits.Description);
        builder.Property(securityEvent => securityEvent.Changes).HasColumnType("jsonb").HasDefaultValueSql("'{}'::jsonb");
        builder.Property(securityEvent => securityEvent.MaskedIpAddress).HasMaxLength(TextLimits.IpAddress);
        builder.HasIndex(securityEvent => new { securityEvent.ActorId, securityEvent.OccurredAtUtc });
        builder.HasIndex(securityEvent => new { securityEvent.TargetTenantId, securityEvent.OccurredAtUtc });
        builder.HasIndex(securityEvent => new { securityEvent.TargetAccountId, securityEvent.OccurredAtUtc });
        // El listado global de Auditoría recorre por fecha e Id, con cursor.
        builder.HasIndex(securityEvent => new { securityEvent.OccurredAtUtc, securityEvent.Id });
        foreach (var property in builder.Metadata.GetProperties())
        {
            property.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        }
    }
}
