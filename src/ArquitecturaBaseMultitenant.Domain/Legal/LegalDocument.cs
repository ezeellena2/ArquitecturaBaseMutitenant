using ArquitecturaBaseMultitenant.Domain.Common;

namespace ArquitecturaBaseMultitenant.Domain.Legal;

public sealed class LegalDocument : Entity
{
    private LegalDocument() { }

    public LegalDocumentKind Kind { get; private set; }
    public int Version { get; private set; }
    public DateTime EffectiveAtUtc { get; private set; }

    public static LegalDocument Create(LegalDocumentKind kind, int version, DateTime effectiveAtUtc)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(version, 1);
        if (effectiveAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("The effective instant must be UTC.", nameof(effectiveAtUtc));
        }

        return new LegalDocument { Kind = kind, Version = version, EffectiveAtUtc = effectiveAtUtc };
    }
}
