using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Domain.Legal;

public static class AccountDeletionErrors
{
    public static readonly Error PlatformOperator = Error.Forbidden("Legal.AccountDeletion.PlatformOperator",
        "A platform operator cannot request their own deletion.");
    public static readonly Error AlreadyPending = Error.Conflict("Legal.AccountDeletion.AlreadyPending",
        "The account deletion was already requested.");
    public static readonly Error Blocked = Error.Conflict("Legal.AccountDeletion.Blocked",
        "A module blocks the account deletion.");
    public static readonly Error ReauthRequired = Error.Forbidden("Legal.AccountDeletion.ReauthRequired",
        "A recent proof of account ownership is required.");
    public static readonly Error GraceExpired = Error.Conflict("Legal.AccountDeletion.GraceExpired",
        "The account deletion grace period has expired.");
    public static readonly Error InvalidReason = Error.Validation("Legal.AccountDeletion.InvalidReason",
        "The deletion reason is required and must fit the description limit.");
}
