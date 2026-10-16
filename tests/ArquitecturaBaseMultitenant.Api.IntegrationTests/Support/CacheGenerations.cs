using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;

/// <summary>
/// Para una prueba que cambia datos cacheados con SQL, por fuera del guardado de la aplicación: avanza las generaciones
/// de sus etiquetas como lo hace el commit (ADR 0052), así la próxima lectura ya no usa la entrada anterior. Las
/// etiquetas salen de <c>CacheTags</c>.
/// </summary>
internal static class CacheGenerations
{
    internal static async Task AdvanceAsync(IServiceProvider services, CancellationToken ct, params string[] tags)
    {
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        foreach (var tag in tags.Order(StringComparer.Ordinal))
            await context.Database.ExecuteSqlAsync($"""
                INSERT INTO platform."CacheTagVersions" ("Tag", "Generation") VALUES ({tag}, 1)
                ON CONFLICT ("Tag") DO UPDATE SET "Generation" = platform."CacheTagVersions"."Generation" + 1
                """, ct);
    }
}
