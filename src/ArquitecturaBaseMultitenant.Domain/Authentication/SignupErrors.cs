using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Domain.Authentication;

/// <summary>
/// Define el rechazo del registro personal cuando la plataforma cerró esa puerta. Su código permite mostrar
/// el mismo motivo desde cualquier flujo de alta.
/// </summary>
public static class SignupErrors
{
    public const string ClosedCode = "Auth.Signup.Closed";
    public const string BusinessClosedCode = "Auth.BusinessSignup.Closed";
    public static readonly Error Closed = Error.Forbidden(ClosedCode, "Personal signup is closed.");
    public static readonly Error BusinessClosed = Error.Forbidden(BusinessClosedCode,
        "Business signup is closed.");
}
