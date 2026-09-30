using ArquitecturaBaseMultitenant.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Conventions;

/// <summary>Vincula la versión de cada entidad editable a xmin de PostgreSQL. Así EF detecta escrituras concurrentes sin una columna de versión mantenida a mano.</summary>
internal static class VersionedConvention
{
    public static void Apply(ModelBuilder modelBuilder)
    {
        foreach (var entity in modelBuilder.Model.GetEntityTypes()
                     .Where(entity => entity.BaseType is null && typeof(IVersioned).IsAssignableFrom(entity.ClrType)))
        {
            modelBuilder.Entity(entity.ClrType)
                .Property<uint>(nameof(IVersioned.Version))
                .IsRowVersion();
        }
    }
}
