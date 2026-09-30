using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Domain.Legal;

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
