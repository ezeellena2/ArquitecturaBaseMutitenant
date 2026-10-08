using ArquitecturaBaseMultitenant.Application.Common.Exceptions;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Domain.Results;
using Microsoft.Extensions.DependencyInjection;
namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp.Webhook;
internal sealed class WhatsAppWebhookReplay(IServiceScopeFactory scopes) : IWhatsAppWebhookReplay
{
    private static readonly AsyncLocal<bool> Replaying = new();
    public async Task<Result> ReplayAsync(WhatsAppWebhookRequest request, CancellationToken ct)
    {
        if (Replaying.Value) throw new UniqueConstraintViolationException("The webhook replay collided again.");
        Replaying.Value = true;
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<IWhatsAppWebhookService>().ReceiveAsync(request, ct);
        }
        finally { Replaying.Value = false; }
    }
}
