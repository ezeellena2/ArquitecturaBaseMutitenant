using ArquitecturaBaseMultitenant.Domain.Common;

namespace ArquitecturaBaseMultitenant.Domain.Forms;

/// <summary>
/// La barrera de una persona para las operaciones de archivo de los catálogos (C11): una por persona en la organización, que se
/// toma sin esperar, vence sola y se suelta al terminar. Es técnica, sin auditoría; se toma con un <c>INSERT … ON CONFLICT</c> que
/// pisa solo una vencida.
/// </summary>
public sealed class FormWorkLease : Entity, ITenantOwned
{
    /// <summary>Cuánto vive una barrera si el proceso se cae sin soltarla.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(2);

    private FormWorkLease() { }
    public Guid TenantId { get; private set; }
    public Guid UserId { get; private set; }
    public FormWorkLeaseKind Kind { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }

    public static FormWorkLease Create(Guid userId, FormWorkLeaseKind kind, DateTime nowUtc)
    {
        if (userId == Guid.Empty) throw new ArgumentException("A user is required.", nameof(userId));
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        if (nowUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("UTC required.", nameof(nowUtc));
        return new FormWorkLease { UserId = userId, Kind = kind, ExpiresAtUtc = nowUtc + Lifetime };
    }

    public bool IsExpired(DateTime nowUtc)
    {
        if (nowUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("UTC required.", nameof(nowUtc));
        return ExpiresAtUtc <= nowUtc;
    }
}

/// <summary>Qué operación de archivo tiene tomada la barrera.</summary>
public enum FormWorkLeaseKind
{
    Preview,
    Apply,
    Export,
}
