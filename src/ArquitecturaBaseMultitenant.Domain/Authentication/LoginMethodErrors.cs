using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Domain.Authentication;

public static class LoginMethodErrors
{
    public const string AlreadyUsedCode = "Identity.LoginMethod.AlreadyUsed";
    public static readonly Error NotFound = Error.NotFound("Identity.LoginMethod.NotFound", "The method was not found.");
    public static readonly Error AlreadyVerified = Error.Conflict("Identity.LoginMethod.AlreadyVerified", "The method is already verified.");
    public static ValidationError AlreadyUsed => new(AlreadyUsedCode, "The email is already in use.",
        new Dictionary<string, string[]> { ["email"] = [AlreadyUsedCode] });
    public const string NotVerifiedCode = "Auth.LoginMethod.NotVerified";

    public static readonly Error NotVerified = Error.Conflict(NotVerifiedCode, "The login method has not been verified.");
}
