using ArquitecturaBaseMultitenant.Domain.Common;

namespace ArquitecturaBaseMultitenant.Domain.Assistant;

/// <summary>
/// 0064 §11: el valor de control de una clave del anillo de Data Protection de la instalación, que escribe la primera copia que
/// la tiene. Su id es el de la clave. Una copia cuyo valor para la misma clave no coincide tiene otro anillo (otras claves bajo el
/// mismo id, otro nombre de aplicación u otro certificado) y no queda lista. No revela la clave ni lo que protege, y nunca se
/// borra para que pase otro anillo.
/// </summary>
public sealed class AiKeyRingControl : Entity
{
    /// <summary>El sello de una etiqueta fija con la clave, con su HMAC-SHA256: entra holgado.</summary>
    public const int MaximumControlBytes = 512;

    private AiKeyRingControl() { }
    private AiKeyRingControl(Guid keyId) : base(keyId) { }

    /// <summary>El id de la clave del anillo; es la clave primaria (<c>KeyId</c>).</summary>
    public Guid KeyId => Id;
    public byte[] Control { get; private set; } = [];
    public DateTime CreatedAtUtc { get; private set; }

    public static AiKeyRingControl Create(Guid keyId, byte[] control, DateTime createdAtUtc)
    {
        if (keyId == Guid.Empty || control is not { Length: > 0 and <= MaximumControlBytes } || createdAtUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("A key ring control requires a key identifier, a bounded value and a UTC timestamp.");
        return new AiKeyRingControl(keyId) { Control = [.. control], CreatedAtUtc = createdAtUtc };
    }

    public override string ToString() => "AiKeyRingControl { control: [REDACTED] }";
}
