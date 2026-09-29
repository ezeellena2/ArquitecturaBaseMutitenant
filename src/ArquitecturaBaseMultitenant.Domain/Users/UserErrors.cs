using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Domain.Users;

public static class UserErrors
{
    public const string InvalidDisplayNameCode = "Users.User.InvalidDisplayName";
    public const string InvalidTransitionCode = "Users.User.InvalidTransition";

    public static readonly Error InvalidDisplayName = Error.Validation(InvalidDisplayNameCode, "The display name is too long.");
    public static readonly Error InvalidTransition = Error.Conflict(InvalidTransitionCode, "The account cannot change to that state.");
}
