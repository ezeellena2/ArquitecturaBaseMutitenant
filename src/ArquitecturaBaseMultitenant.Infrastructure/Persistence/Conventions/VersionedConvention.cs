using ArquitecturaBaseMultitenant.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Conventions;

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
