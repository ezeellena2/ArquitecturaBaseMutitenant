using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Services.Identity;
using ArquitecturaBaseMultitenant.Application.Services.Messaging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using ArquitecturaBaseMultitenant.Domain.Messaging;
namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Services;
/// <summary>
/// Reserva el ledger en la UoW del emisor; no guarda un destino ni consume quota en replay. Desde Mi cuenta, cada cuenta
/// pide además hasta <see cref="IWhatsAppChannelSettings.DailyAccountCodeLimit"/> códigos en las últimas 24 horas (ADR 0063),
/// contados por horas en <c>identity.MessageQuotaBuckets</c> y sumados solo si el ledger del día también tiene lugar.
/// </summary>
internal sealed class WhatsAppCodeQuotaIssuer(IWhatsAppCodeQuotaRepository quotas, IWhatsAppChannelSettings settings,
    TimeProvider clock, ILoginMethodReader methods, LoginMethodAvailabilityPolicy availability,
    IRequestInfo request, IWhatsAppQuotaIpHasher ipHasher, MessageQuotaGuard accountQuota,
    IMessageQuotaKeyHasher accountKeys) : ILoginCodeAdmission
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
        var account = await CheckAccountAsync(purpose, ownerId, ct);
        var quota = await quotas.GetAsync(phoneNumberId, dayUtc, ct);
        var isNew = quota is null;
        quota ??= WhatsAppCodeQuota.Create(phoneNumberId, dayUtc);
        var ipKey = protectedLogin ? null : ipHasher.Hash(request.IpAddress, phoneNumberId, dayUtc);
        var ip = ipKey is null ? null : await quotas.GetIpAsync(quota.Id, ipKey, ct);
        var (limit, newLimit, ipNewCount, ipLimit) = (settings.DailyAuthCodeLimit, settings.DailyNewAuthCodeLimit,
            ip?.ReservedCount ?? 0, settings.DailyNewAuthCodeIpLimit);
        if (account?.Exceeded is not null)
            // Con el día también lleno, responde el que libera lugar más tarde: esperar solo a la cuenta no alcanza.
            return quota.CheckRoom(limit, newLimit, protectedLogin, ipNewCount, ipLimit, nowUtc).IsFailure
                && WhatsAppCodeErrors.SecondsUntilNextDay(nowUtc) > account.RetryAfterSeconds
                    ? WhatsAppCodeErrors.DailyLimit(nowUtc)
                    : WhatsAppCodeErrors.AccountDailyLimit(account.RetryAfterSeconds);
        var reserved = quota.Reserve(limit, newLimit, protectedLogin, ipNewCount, ipLimit, nowUtc);
        if (reserved.IsFailure) return reserved; // OnAnyResult tampoco persiste una fila vacía.
        if (isNew) quotas.Add(quota);
        if (ipKey is not null)
        {
            if (ip is null) { ip = WhatsAppCodeQuotaIp.Create(quota.Id, ipKey); quotas.Add(ip); }
            ip.Reserve();
        }
        if (account is not null) await accountQuota.RecordAsync(account, ct);
        return reserved;
    }

    // Desde Mi cuenta: el código que verifica un número es de la cuenta que lo pide. Va después del lock del ledger.
    private async Task<MessageQuotaCheck?> CheckAccountAsync(LoginCodePurpose purpose, Guid? ownerId, CancellationToken ct)
    {
        if (purpose != LoginCodePurpose.VerifyDestination || ownerId is not { } accountId) return null;
        var key = new MessageQuotaKey(MessageQuotaScope.WhatsAppCodeAccount,
            accountKeys.ForSubject(MessageQuotaScope.WhatsAppCodeAccount, accountId));
        return await accountQuota.CheckAsync([new MessageQuotaLimit(key, settings.DailyAccountCodeLimit)], ct);
    }
}
