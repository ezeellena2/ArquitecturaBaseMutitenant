using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Services;
/// <summary>Reserva el ledger en la UoW del emisor; no guarda un destino ni consume quota en replay.</summary>
internal sealed class WhatsAppCodeQuotaIssuer(IWhatsAppCodeQuotaRepository quotas, IWhatsAppChannelSettings settings,
    TimeProvider clock) : ILoginCodeAdmission
{
    public string Channel => "whatsapp";
    public async Task<Result> ReserveAsync(LoginCodeDestination destination, CancellationToken ct)
    {
        if (!settings.IsEnabled || destination.Channel != Channel) return LoginChannelErrors.Unavailable;
        var nowUtc = clock.GetUtcNow().UtcDateTime;
        var dayUtc = DateOnly.FromDateTime(nowUtc);
        var phoneNumberId = settings.PhoneNumberId!;
        while (true)
        {
            await quotas.LockAsync(phoneNumberId, dayUtc, ct);
            nowUtc = clock.GetUtcNow().UtcDateTime;
            var currentDay = DateOnly.FromDateTime(nowUtc);
            if (currentDay == dayUtc) break;
            dayUtc = currentDay;
        }
        var quota = await quotas.GetAsync(phoneNumberId, dayUtc, ct);
        if (quota is null) { quota = WhatsAppCodeQuota.Create(phoneNumberId, dayUtc); quotas.Add(quota); }
        return quota.Reserve(settings.DailyAuthCodeLimit, nowUtc);
    }
}
