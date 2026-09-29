using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Configurations.Platform;

internal sealed class SecurityEventConfiguration : IEntityTypeConfiguration<SecurityEvent>
{
    public void Configure(EntityTypeBuilder<SecurityEvent> builder)
    {
        builder.ToTable("SecurityEvents", Schemas.Platform);
        builder.HasKey(securityEvent => securityEvent.Id);
        builder.Property(securityEvent => securityEvent.Type).HasConversion<string>().HasMaxLength(TextLimits.ShortName);
        builder.Property(securityEvent => securityEvent.ActorKind).HasConversion<string>().HasMaxLength(TextLimits.ShortName);
        builder.Property(securityEvent => securityEvent.Reason).HasMaxLength(TextLimits.Description);
        builder.HasIndex(securityEvent => new { securityEvent.ActorId, securityEvent.OccurredAtUtc });
        builder.HasIndex(securityEvent => new { securityEvent.TargetTenantId, securityEvent.OccurredAtUtc });
        foreach (var property in builder.Metadata.GetProperties())
        {
            property.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        }
    }
}
