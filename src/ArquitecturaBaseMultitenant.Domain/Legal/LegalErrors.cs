using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Domain.Legal;

public static class LegalErrors
{
    public const string AcceptanceRequiredCode = "Legal.AcceptanceRequired";
    public const string DocumentNotFoundCode = "Legal.Document.NotFound";

    public static readonly Error DocumentNotFound = Error.NotFound(DocumentNotFoundCode,
        "The legal document is not available.");
}
