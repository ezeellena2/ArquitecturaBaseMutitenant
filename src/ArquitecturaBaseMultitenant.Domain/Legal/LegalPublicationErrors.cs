using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Domain.Legal;

/// <summary>Errores de Plataforma › Documentos legales al publicar una versión nueva.</summary>
public static class LegalPublicationErrors
{
    public const string EffectiveDatePastCode = "Legal.Publication.EffectiveDatePast";
    public const string CulturesMismatchCode = "Legal.Publication.CulturesMismatch";

    /// <summary>Una vigencia anterior a hoy dejaría la versión sin regir nunca, detrás de la vigente.</summary>
    public static readonly Error EffectiveDatePast = Error.Validation(EffectiveDatePastCode,
        "The effective date cannot be earlier than today.");

    public static readonly Error CulturesMismatch = Error.Validation(CulturesMismatchCode,
        "The text is required once in every enabled culture.");
}
