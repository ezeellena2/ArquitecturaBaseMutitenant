using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Domain.Authentication;

public static class SignupErrors
{
    public const string ClosedCode = "Auth.Signup.Closed";
    public const string EmailTakenCode = "Auth.Signup.EmailTaken";

    public static readonly Error Closed = Error.Forbidden(ClosedCode, "Personal signup is closed.");
    public static readonly Error EmailTaken = Error.Conflict(EmailTakenCode, "The email address already belongs to an account.");
}
