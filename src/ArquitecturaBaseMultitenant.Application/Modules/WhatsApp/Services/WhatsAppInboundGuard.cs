using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Application.Services.Identity;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Services;
internal sealed class WhatsAppInboundGuard(IWhatsAppMessageRepository messages, IExternalLoginLock identities,
    ILoginMethodRepository methods, IWhatsAppContactRepository contacts, IUserRepository users, LoginMethodAvailabilityPolicy availability, TimeProvider clock)
{
    internal async Task<WhatsAppInboundContext?> LockAsync(WhatsAppInboundClaim claim, CancellationToken ct)
    {
        var hint = await messages.GetProcessingHintAsync(claim.MessageId, ct);
        if (hint is null) return null;
        if (hint.WaId is { } waId) await identities.LockPhoneAsync(PhoneNumber.Create(waId).Value, ct);
        if (hint.OriginalUserId is { } owner) await methods.LockUserAsync(owner, ct);
        if (hint.WaId is { } contactNumber) await contacts.LockAsync(contactNumber, ct);
        var message = await messages.GetForUpdateAsync(claim.MessageId, ct);
        var nowUtc = clock.GetUtcNow().UtcDateTime;
        if (message is null || message.Direction != Domain.WhatsApp.WhatsAppMessageDirection.Inbound
            || message.LeaseId != claim.LeaseId || message.LeaseUntilUtc <= nowUtc || message.ProcessedAtUtc is not null) return null;
        var contact = hint.WaId is { } sender ? await contacts.GetAsync(sender, ct) : null;
        var account = message.OriginalUserId is { } userId ? await users.GetByIdAsync(userId, ct) : null;
        var method = message.OriginalMethodId is { } methodId ? await methods.GetByIdAsync(methodId, ct) : null;
        var valid = method is { Type: LoginMethodType.Phone, VerifiedAtUtc: not null } && method.UserId == message.OriginalUserId
            && method.Value == contact?.WaId && (await availability.AvailableAsync([method], ct)).Count != 0;
        return new(message, contact, account, valid ? method : null, valid);
    }
}
