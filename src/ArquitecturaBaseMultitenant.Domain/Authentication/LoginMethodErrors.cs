using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Domain.Authentication;

public static class LoginMethodErrors
{
    public const string NotVerifiedCode = "Auth.LoginMethod.NotVerified";

    public static readonly Error NotVerified = Error.Conflict(NotVerifiedCode, "The login method has not been verified.");
}
