using ArquitecturaBaseMultitenant.Domain.Common;

namespace ArquitecturaBaseMultitenant.Domain.Legal;

/// <summary>Prueba qué versión legal aceptó una cuenta y cuándo; la baja puede retirar sus metadatos personales sin borrar la prueba.</summary>
public sealed class LegalAcceptance : Entity
{
    private LegalAcceptance() { }

    public Guid UserId { get; private set; }
    public Guid LegalDocumentId { get; private set; }
    public int Version { get; private set; }
    public DateTime AcceptedAtUtc { get; private set; }
    public string? IpAddress { get; private set; }
    public string? UserAgent { get; private set; }

    public static LegalAcceptance Create(Guid userId, LegalDocument document, DateTime acceptedAtUtc,
        string? ipAddress, string? userAgent)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("The user id cannot be empty.", nameof(userId));
        }

        ArgumentNullException.ThrowIfNull(document);
        if (acceptedAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("The acceptance instant must be UTC.", nameof(acceptedAtUtc));
        }

        if (acceptedAtUtc < document.EffectiveAtUtc)
        {
            throw new ArgumentException("The legal document is not effective yet.", nameof(acceptedAtUtc));
        }

        return new LegalAcceptance
        {
            UserId = userId,
            LegalDocumentId = document.Id,
            Version = document.Version,
            AcceptedAtUtc = acceptedAtUtc,
            IpAddress = ipAddress,
            UserAgent = userAgent,
        };
    }

    /// <summary>La baja conserva la prueba de aceptación y borra los metadatos de la petición.</summary>
    public void ClearPersonalMetadata()
    {
        IpAddress = null;
        UserAgent = null;
    }
}
