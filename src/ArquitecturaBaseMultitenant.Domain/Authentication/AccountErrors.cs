using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Domain.Authentication;

/// <summary>
/// Define los rechazos por una cuenta bloqueada temporalmente, suspendida o con baja pendiente. Los flujos
/// de ingreso usan estos motivos antes de conceder una sesión.
/// </summary>
public static class AccountErrors
{
    public const string LockedOutCode = "Identity.Account.LockedOut";
    public const string SuspendedCode = "Identity.Account.Suspended";
    public const string PendingDeletionCode = "Identity.Account.PendingDeletion";

    public static readonly Error LockedOut = Error.TooManyRequests(LockedOutCode, "The account is temporarily locked.");

    /// <summary>
    /// El mismo bloqueo, con los segundos que faltan (E9): el segundo factor sí informa el plazo y la API lo repite en
    /// Retry-After.
    /// </summary>
    public static Error LockedOutFor(int retryAfterSeconds) => LockedOut with
    {
        Metadata = new Dictionary<string, object?> { [LoginCodeErrors.RetryAfterKey] = Math.Max(1, retryAfterSeconds) },
    };
    public static readonly Error Suspended = Error.Forbidden(SuspendedCode, "The account is suspended.");
    public static readonly Error PendingDeletion = Error.Forbidden(PendingDeletionCode, "The account is pending deletion.");
}
