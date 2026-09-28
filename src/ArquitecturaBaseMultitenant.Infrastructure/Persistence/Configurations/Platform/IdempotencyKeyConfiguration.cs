using ArquitecturaBaseMultitenant.Infrastructure.Idempotency;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Configurations.Platform;

internal sealed class IdempotencyKeyConfiguration : IEntityTypeConfiguration<IdempotencyKey>
{
    public void Configure(EntityTypeBuilder<IdempotencyKey> builder)
    {
        builder.ToTable("IdempotencyKeys", Schemas.Platform);
        builder.HasKey(key => key.Id);
        builder.Property(key => key.BodyHash).IsRequired();
        builder.Property(key => key.Route).IsRequired();
        builder.HasIndex(key => new { key.TenantId, key.UserId, key.Key })
            .IsUnique()
            .AreNullsDistinct(false);
        builder.HasIndex(key => key.ExpiresAtUtc);
    }
}
