using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Domain.Platform;

public static class PlatformAuditErrors
{
    public const string NotFoundCode = "Platform.Audit.NotFound";

    public static readonly Error NotFound = Error.NotFound(NotFoundCode, "The security event does not exist.");
}
