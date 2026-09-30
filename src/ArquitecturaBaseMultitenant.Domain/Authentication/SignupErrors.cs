using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Domain.Authentication;

public static class SignupErrors
{
    public const string ClosedCode = "Auth.Signup.Closed";
    public static readonly Error Closed = Error.Forbidden(ClosedCode, "Personal signup is closed.");
}
