using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Domain.Authentication;

public static class ReauthErrors
{
    public const string InvalidCode = "Identity.Reauth.Invalid";
    public const string ExpiredCode = "Identity.Reauth.Expired";
    public const string OtherMethodRequiredCode = "Identity.Reauth.OtherMethodRequired";

    public static readonly Error Invalid = Error.Forbidden(InvalidCode, "The reauthentication ticket is invalid.");
    public static readonly Error Expired = Error.Forbidden(ExpiredCode, "The reauthentication ticket has expired.");
    public static readonly Error OtherMethodRequired = Error.Conflict(OtherMethodRequiredCode,
        "A different verified login method is required.");
}

