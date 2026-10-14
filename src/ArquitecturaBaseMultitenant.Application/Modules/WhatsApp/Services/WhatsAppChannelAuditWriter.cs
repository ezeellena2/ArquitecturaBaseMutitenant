using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Services;

/// <summary>
/// El evento que exige un número (global, ADR 0050) en la misma transacción: del Dueño o del operador que lo cambió, o del
/// sistema cuando lo cambia Meta o el arranque. El resumen lleva estados y marcas, nunca el número, el token ni el PIN.
/// </summary>
internal sealed class WhatsAppChannelAuditWriter(ICurrentUser user, ISecurityEventRepository events, TimeProvider clock, IRequestInfo request)
{
    internal void Record(WhatsAppChannel channel, string reason, SecurityEventChanges changes, bool bySystem = false)
    {
        ArgumentNullException.ThrowIfNull(channel);
        var nowUtc = clock.GetUtcNow().UtcDateTime;
        var actor = bySystem ? null : user.UserId;
        SecurityEvent securityEvent = channel.OrganizationId is { } organizationId
            ? actor is { } member && user.Access == Access.Business
                ? SecurityEvent.ForOrganizationModule(member, organizationId, reason, nowUtc)
                : SecurityEvent.ForTenantFeatures(AuditActorKind.System, Guid.Empty, organizationId, reason, nowUtc)
            : actor is { } operatorId && user.Access == Access.Platform
                ? SecurityEvent.ForPlatformSettings(AuditActorKind.PlatformOperator, operatorId, reason, nowUtc)
                : SecurityEvent.ForPlatformSettings(AuditActorKind.System, Guid.Empty, reason, nowUtc);
        events.Add(securityEvent.WithEvidence(changes, bySystem ? null : request.IpAddress));
    }

    internal static SecurityEventChanges Status(WhatsAppNumberStatus? before, WhatsAppNumberStatus? after)
    {
        var changes = new SecurityEventChanges();
        changes.AddEnum("whatsappNumberStatus", before, after);
        return changes;
    }
}
