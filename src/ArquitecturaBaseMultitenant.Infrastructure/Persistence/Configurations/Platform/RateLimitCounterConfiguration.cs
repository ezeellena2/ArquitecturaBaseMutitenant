using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Configurations.Platform;

/// <summary>
/// Mapea los contadores durables de cuotas (E9): la clave del contador, cuánto lleva y cuándo termina su ventana.
/// Infraestructura global, sin datos de negocio ni IP completas.
/// </summary>
internal sealed class RateLimitCounterConfiguration : IEntityTypeConfiguration<RateLimitCounter>
{
    public void Configure(EntityTypeBuilder<RateLimitCounter> builder)
    {
        builder.ToTable("RateLimitCounters", Schemas.Platform);
        builder.HasKey(counter => counter.Key);
        builder.Property(counter => counter.Key).HasMaxLength(RateLimitCounter.KeyMaxLength);
        builder.HasIndex(counter => counter.ExpiresAtUtc);
    }
}
