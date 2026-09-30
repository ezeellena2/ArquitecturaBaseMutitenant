using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Seed;

/// <summary>Abre un scope técnico para ejecutar el seed global al arrancar. DatabaseSeeder coordina el lock y la transacción para que varias réplicas puedan iniciar juntas.</summary>
public static class SeedExtensions
{
    /// <summary>Siembra datos globales al arrancar; idempotente y seguro ante dos réplicas concurrentes.</summary>
    public static async Task SeedDatabaseAsync(this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<DatabaseSeeder>().SeedAsync(cancellationToken);
    }
}
