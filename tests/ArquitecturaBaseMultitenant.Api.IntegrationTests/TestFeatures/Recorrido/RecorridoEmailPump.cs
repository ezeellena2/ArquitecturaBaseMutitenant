using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using ArquitecturaBaseMultitenant.Infrastructure.Messaging;
using ArquitecturaBaseMultitenant.Infrastructure.Messaging.Email;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.Recorrido;

/// <summary>Despacha sólo Email al pickup real; no reclama WhatsApp ni ejecuta avisos de recuperación.</summary>
public static class RecorridoEmailPump
{
    public static async Task<int> ProcessOnceAsync(IServiceProvider services, CancellationToken ct)
    {
        await using var scope = services.CreateAsyncScope();
        var provider = scope.ServiceProvider;
        if (!provider.GetRequiredService<IHostEnvironment>().IsEnvironment("Testing")
            || provider.GetRequiredService<IOptions<EmailOptions>>().Value.Delivery != EmailDelivery.PickupDirectory)
            throw new InvalidOperationException("Controlled email pump requires Testing pickup.");
        var sender = provider.GetServices<IChannelSender>().Single(value => value.Key == "email");
        return await provider.GetRequiredService<OutboxDeliveryProcessor>().ProcessOnceAsync([sender], ct);
    }
}
