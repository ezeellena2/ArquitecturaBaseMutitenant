using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Domain.Authentication;

public static class LoginLinkErrors
{
    public const string InvalidCode = "Auth.LoginLink.Invalid";
    public const string ExpiredCode = "Auth.LoginLink.Expired";
    public const string AlreadyUsedCode = "Auth.LoginLink.AlreadyUsed";
    public const string TooManyRequestsCode = "Auth.LoginLink.TooManyRequests";
    public static readonly Error Invalid = Error.Validation(InvalidCode, "This link no longer works.");
    public static readonly Error Expired = Error.Validation(ExpiredCode, "This link no longer works.");
    public static readonly Error AlreadyUsed = Error.Validation(AlreadyUsedCode, "This link no longer works.");
    public static Error TooManyRequests(int retryAfterSeconds) => Error.TooManyRequests(TooManyRequestsCode,
        "Wait a moment before requesting another link.", new Dictionary<string, object?> { [LoginCodeErrors.RetryAfterKey] = retryAfterSeconds });
}
