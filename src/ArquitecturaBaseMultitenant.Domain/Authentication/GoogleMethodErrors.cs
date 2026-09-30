using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Domain.Authentication;

public static class GoogleMethodErrors
{
    public static readonly Error AlreadyUsed = Error.Conflict("Identity.Google.AlreadyUsed", "Google is associated with another account.");
}
