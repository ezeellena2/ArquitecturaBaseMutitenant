using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Domain.Authentication;

/// <summary>
/// Define el rechazo del registro personal cuando la plataforma cerró esa puerta. Su código permite mostrar
/// el mismo motivo desde cualquier flujo de alta.
/// </summary>
public static class SignupErrors
{
    public const string PhoneTakenCode = "Auth.Signup.PhoneTaken";
    public static readonly Error PhoneTaken = Error.Validation(PhoneTakenCode, "You already have an account with that number.");
    public const string ClosedCode = "Auth.Signup.Closed";
    public const string BusinessClosedCode = "Auth.BusinessSignup.Closed";
    public const string EmailTakenCode = "Auth.Signup.EmailTaken";
    public static readonly Error EmailTaken = Error.Conflict(EmailTakenCode, "You already have an account with that email address.");
    public static readonly Error Closed = Error.Forbidden(ClosedCode, "Personal signup is closed.");
    public static readonly Error BusinessClosed = Error.Forbidden(BusinessClosedCode,
        "Business signup is closed.");
}
