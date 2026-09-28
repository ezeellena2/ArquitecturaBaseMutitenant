using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Domain.Users;

public static class EmailErrors
{
    public const string InvalidCode = "Users.Email.Invalid";

    public static readonly Error Invalid = Error.Validation(InvalidCode, "The email address is not valid.");
}
