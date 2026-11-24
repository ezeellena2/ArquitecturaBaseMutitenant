using ArquitecturaBaseMultitenant.Domain.Messaging;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence;

/// <summary>
/// Lo que sumó un tope de mensajes en una hora (ADR 0063): qué cuenta, la clave del contador (un HMAC de la dirección, la
/// cuenta o la organización; <c>all</c> en el global) y cuántos. Global y sin RLS, como <c>identity.WhatsAppCodeQuotas</c>:
/// no nombra a nadie en claro. Lo escribe <c>MessageQuotaRepository</c> y lo purga <c>IdentityRetentionWorker</c> cuando
/// ya salió de la ventana.
/// </summary>
internal sealed class MessageQuotaBucket
{
    internal const int KeyHashMaxLength = 64;

    private MessageQuotaBucket() => KeyHash = string.Empty;

    public MessageQuotaScope Scope { get; private set; }

    public string KeyHash { get; private set; }

    public DateTime HourUtc { get; private set; }

    public int Count { get; private set; }
}
