using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Services.Identity;
namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Services;
/// <summary>Reserva el ledger en la UoW del emisor; no guarda un destino ni consume quota en replay.</summary>
internal sealed class WhatsAppCodeQuotaIssuer(IWhatsAppCodeQuotaRepository quotas, IWhatsAppChannelSettings settings,
    TimeProvider clock, ILoginMethodReader methods, LoginMethodAvailabilityPolicy availability,
    IRequestInfo request, IWhatsAppQuotaIpHasher ipHasher) : ILoginCodeAdmission
{
    public string Channel => "whatsapp";
    public async Task<Result> ReserveAsync(LoginCodeDestination destination, LoginCodePurpose purpose, Guid? ownerId, CancellationToken ct)
    {
        if (!settings.IsEnabled || destination.Channel != Channel) return LoginChannelErrors.Unavailable;
        // El llamador ya adquirió identidad → cuenta → destino. Nunca alcanza un UserId o propósito del cliente.
        var protectedLogin = false;
        if (purpose == LoginCodePurpose.Login && ownerId is { } userId)
        {
            var method = (await methods.ListByUserIdAsync(userId, ct)).SingleOrDefault(value =>
                value.Type == LoginMethodType.Phone && value.Value == destination.Value);
            protectedLogin = method is not null && await availability.CanReceiveCodeAsync(userId, method, [], ct);
        }
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
        var isNew = quota is null;
        quota ??= WhatsAppCodeQuota.Create(phoneNumberId, dayUtc);
        var ipKey = protectedLogin ? null : ipHasher.Hash(request.IpAddress, phoneNumberId, dayUtc);
        var ip = ipKey is null ? null : await quotas.GetIpAsync(quota.Id, ipKey, ct);
        var reserved = quota.Reserve(settings.DailyAuthCodeLimit, settings.DailyNewAuthCodeLimit, protectedLogin,
            ip?.ReservedCount ?? 0, settings.DailyNewAuthCodeIpLimit, nowUtc);
        if (reserved.IsFailure) return reserved; // OnAnyResult tampoco persiste una fila vacía.
        if (isNew) quotas.Add(quota);
        if (ipKey is not null)
        {
            if (ip is null) { ip = WhatsAppCodeQuotaIp.Create(quota.Id, ipKey); quotas.Add(ip); }
            ip.Reserve();
        }
        return reserved;
    }
}
