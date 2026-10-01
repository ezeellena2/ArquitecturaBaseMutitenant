using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Domain.Legal;

/// <summary>
/// Reúne los rechazos por documentos legales pendientes, inexistentes o cuya versión cambió. Permite pedir
/// una nueva aceptación sin perder el motivo del rechazo.
/// </summary>
public static class LegalErrors
{
    public const string AcceptanceRequiredCode = "Legal.AcceptanceRequired";
    public const string DocumentNotFoundCode = "Legal.Document.NotFound";
    public static readonly Error AcceptanceRequired = Error.Forbidden(AcceptanceRequiredCode,
        "The current legal documents must be accepted.");
    public static readonly Error VersionChanged = Error.Conflict("Legal.Document.VersionChanged",
        "The legal document version changed.");

    public static readonly Error DocumentNotFound = Error.NotFound(DocumentNotFoundCode,
        "The legal document is not available.");
}
