using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Auditing;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Services;

/// <summary>
/// El evento que exigen el bot y los equipos de Plataforma (globales, ADR 0050) en la misma transacción: el operador que los
/// cambió y un resumen de cantidades, nunca textos ni nombres.
/// </summary>
internal sealed class PlatformWhatsAppAuditWriter(ICurrentUser user, ISecurityEventRepository events, IRequestInfo request, TimeProvider clock)
{
    internal void Record(string reason, SecurityEventChanges changes) =>
        events.Add(SecurityEvent.ForPlatformSettings(AuditActorKind.PlatformOperator, user.UserId!.Value, reason, clock.GetUtcNow().UtcDateTime)
            .WithEvidence(changes, request.IpAddress));
}
