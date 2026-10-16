using ArquitecturaBaseMultitenant.Infrastructure.Caching;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Configurations.Platform;

/// <summary>
/// Mapea la generación durable de cada etiqueta de caché (ADR 0052): la etiqueta con su alcance como clave y un entero
/// que sólo avanza. Infraestructura global, sin datos de negocio.
/// </summary>
internal sealed class CacheTagVersionConfiguration : IEntityTypeConfiguration<CacheTagVersion>
{
    public void Configure(EntityTypeBuilder<CacheTagVersion> builder)
    {
        builder.ToTable("CacheTagVersions", Schemas.Platform);
        builder.HasKey(version => version.Tag);
        builder.Property(version => version.Tag).HasMaxLength(CacheTags.MaxLength);
    }
}
