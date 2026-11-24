using ArquitecturaBaseMultitenant.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Configurations.Identity;

/// <summary>
/// Los contadores por hora de los topes de mensajes (ADR 0063): uno por tope, clave y hora, global y sin RLS. El índice por
/// hora lo recorre la purga.
/// </summary>
internal sealed class MessageQuotaBucketConfiguration : IEntityTypeConfiguration<MessageQuotaBucket>
{
    public void Configure(EntityTypeBuilder<MessageQuotaBucket> builder)
    {
        builder.ToTable("MessageQuotaBuckets", Schemas.Identity);
        builder.HasKey(bucket => new { bucket.Scope, bucket.KeyHash, bucket.HourUtc });
        builder.Property(bucket => bucket.Scope).HasConversion<string>().HasMaxLength(TextLimits.ShortName);
        builder.Property(bucket => bucket.KeyHash).HasMaxLength(MessageQuotaBucket.KeyHashMaxLength);
        builder.HasIndex(bucket => bucket.HourUtc);
    }
}
