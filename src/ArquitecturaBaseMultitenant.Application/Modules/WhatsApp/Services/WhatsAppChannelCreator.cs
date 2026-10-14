using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Services;

/// <summary>
/// Da de alta un número que Meta ya conectó, dentro de la transacción del servicio: bajo el lock del número, revisa que
/// no esté vigente en otro lado, cifra token y PIN, y deja el evento. El de Plataforma reemplaza al anterior.
/// </summary>
internal sealed class WhatsAppChannelCreator(IWhatsAppChannelRepository channels, IWhatsAppTokenProtector protector,
    WhatsAppChannelAuditWriter audit, ITenantContext tenant, ITenantSettingsReader settings, TimeProvider clock)
{
    private const string Reason = "WhatsApp number connected";

    internal async Task<Result<WhatsAppChannel>> ConnectOrganizationAsync(Guid? companyId, WhatsAppSignupInput input,
        WhatsAppSignupResult signup, CancellationToken ct)
    {
        var organizationId = tenant.RequiredTenantId;
        await channels.LockNumberAsync(input.PhoneNumberId, ct);
        if (await channels.GetActiveAsync(input.PhoneNumberId, ct) is not null) return WhatsAppErrors.NumberAlreadyConnected;
        var zone = (await settings.FindCurrentAsync(ct))?.DefaultTimeZoneId ?? WhatsAppChannelBootstrapService.PlatformUsageTimeZoneId;
        return Add(WhatsAppChannel.Connect(organizationId, companyId, input.PhoneNumberId, input.WabaId, signup.DisplayNumber,
            signup.VerifiedName, input.Coexistence, signup.HasPayment, WhatsAppChannelSource.Signup, zone, clock.GetUtcNow().UtcDateTime), signup);
    }

    internal async Task<Result<WhatsAppChannel>> ConnectPlatformAsync(WhatsAppSignupInput input, WhatsAppSignupResult signup, CancellationToken ct)
    {
        await channels.LockNumberAsync(input.PhoneNumberId, ct);
        var current = await channels.GetPlatformAsync(null, ct);
        var active = await channels.GetActiveAsync(input.PhoneNumberId, ct);
        if (active is not null && active.Id != current?.Id) return WhatsAppErrors.NumberAlreadyConnected;
        var nowUtc = clock.GetUtcNow().UtcDateTime;
        if (current is not null)
        {
            var before = current.Status;
            current.Disconnect(nowUtc);
            channels.MarkUpdated(current);
            audit.Record(current, Reason, WhatsAppChannelAuditWriter.Status(before, current.Status));
        }
        return Add(WhatsAppChannel.Connect(null, null, input.PhoneNumberId, input.WabaId, signup.DisplayNumber, signup.VerifiedName,
            input.Coexistence, signup.HasPayment, WhatsAppChannelSource.Signup, WhatsAppChannelBootstrapService.PlatformUsageTimeZoneId, nowUtc), signup);
    }

    private WhatsAppChannel Add(WhatsAppChannel channel, WhatsAppSignupResult signup)
    {
        channel.ProtectSecrets(protector.Protect(channel.Id, signup.Token), signup.Pin is null ? null : protector.Protect(channel.Id, signup.Pin));
        channels.Add(channel);
        audit.Record(channel, Reason, WhatsAppChannelAuditWriter.Status(null, channel.Status));
        return channel;
    }
}
