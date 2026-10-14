using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp;

/// <summary>
/// El número vigente de Plataforma en memoria, para las preguntas sincrónicas de ingreso, códigos y avisos. La primera
/// vez, en el arranque o en el primer uso, crea el número desde la configuración si hace falta (decisión 3) y lo lee;
/// después se relee tras cada cambio confirmado en esta instancia y cada pocos segundos para ver las demás.
/// </summary>
internal sealed class WhatsAppPlatformChannelState(IServiceScopeFactory scopes, IOptions<WhatsAppOptions> options)
    : IWhatsAppChannelCache, IDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private volatile bool loaded;
    private WhatsAppPlatformChannelRow? current;

    public WhatsAppPlatformChannelRow? Current
    {
        get
        {
            // Una sola vez por proceso; el pedido no tiene contexto de sincronización que bloquear.
            if (!loaded) LoadAsync(CancellationToken.None).GetAwaiter().GetResult();
            return Volatile.Read(ref current);
        }
    }

    public async Task LoadAsync(CancellationToken ct)
    {
        if (loaded) return;
        await gate.WaitAsync(ct);
        try
        {
            if (loaded) return;
            // El exportador de OpenAPI arma el host sin base.
            if (System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name is not "GetDocument.Insider")
            {
                var settings = options.Value;
                await using (var scope = scopes.CreateAsyncScope())
                    if (scope.ServiceProvider.GetService<IWhatsAppChannelBootstrapService>() is { } bootstrap)
                        await bootstrap.EnsurePlatformAsync(new WhatsAppPlatformConfiguration(settings.PhoneNumberId, settings.BusinessAccountId,
                            settings.DisplayPhoneNumber, settings.AccessToken), ct);
                await ReadAsync(ct);
            }
            loaded = true;
        }
        finally { gate.Release(); }
    }

    public async Task RefreshAsync(CancellationToken ct)
    {
        if (!loaded) await LoadAsync(ct);
        else await ReadAsync(ct);
    }

    public void Dispose() => gate.Dispose();

    private async Task ReadAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var row = await scope.ServiceProvider.GetRequiredService<IWhatsAppChannelReader>().FindPlatformStateAsync(ct);
        Volatile.Write(ref current, row);
    }
}
