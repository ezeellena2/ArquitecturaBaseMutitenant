using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence;

/// <summary>Permite generar migraciones sin arrancar la Api ni abrir conexiones.</summary>
public sealed class DesignTimeApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        // Solo describe el proveedor: el scaffolding no conecta a esta dirección.
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=appdb;Username=mt_owner")
            .Options;
        return new ApplicationDbContext(options, new TenantContext());
    }
}
