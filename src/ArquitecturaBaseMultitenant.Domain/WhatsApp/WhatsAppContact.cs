using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
namespace ArquitecturaBaseMultitenant.Domain.WhatsApp;

/// <summary>Contacto técnico global. Solo una posesión verificada vincula el número a una cuenta.</summary>
public sealed class WhatsAppContact : Entity, IVersioned
{
    private WhatsAppContact() { WaId = string.Empty; }
    public string WaId { get; private set; }
    public Guid? MethodId { get; private set; }
    public Guid? UserId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? LastInboundAtUtc { get; private set; }
    public uint Version { get; private set; }

    public static WhatsAppContact Create(PhoneNumber phone, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(phone);
        if (nowUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("UTC required.", nameof(nowUtc));
        return new WhatsAppContact { WaId = phone.Value, CreatedAtUtc = nowUtc };
    }
    public void Link(Guid methodId, Guid userId)
    {
        if (methodId == Guid.Empty || userId == Guid.Empty) throw new ArgumentException("A verified method and user are required.");
        MethodId = methodId; UserId = userId;
    }
    public void Unlink() { MethodId = null; UserId = null; }
    public void RecordInbound(DateTime occurredAtUtc)
    {
        if (occurredAtUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("UTC required.", nameof(occurredAtUtc));
        if (LastInboundAtUtc is null || LastInboundAtUtc < occurredAtUtc) LastInboundAtUtc = occurredAtUtc;
    }
}
