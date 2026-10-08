using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Domain.Authentication;

public static class LoginChannelErrors
{
    public const string UnavailableCode = "Auth.LoginMethod.ChannelUnavailable";
    public static readonly Error Unavailable = Error.Validation(UnavailableCode, "That sign-in method is unavailable.");
}
