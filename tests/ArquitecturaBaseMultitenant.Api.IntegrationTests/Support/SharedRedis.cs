using Testcontainers.Redis;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;

/// <summary>
/// El Redis efímero de toda la corrida (ADR 0052), con la imagen del AppHost. Lo comparten todos los hosts de prueba: cada
/// base nueva tiene su propia identidad en las claves, así dos fixtures nunca leen entradas de la otra. Testcontainers lo
/// borra al terminar el proceso.
/// </summary>
internal static class SharedRedis
{
    private static readonly Lazy<Task<RedisContainer>> Container = new(StartAsync);

    internal static async Task<string> ConnectionStringAsync() => (await Container.Value).GetConnectionString();

    private static async Task<RedisContainer> StartAsync()
    {
        var container = new RedisBuilder("redis:8.6").Build();
        await container.StartAsync();
        return container;
    }
}
