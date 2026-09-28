using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Domain.Users;

public static class PhoneErrors
{
    public const string InvalidCode = "Users.Phone.Invalid";

    public static readonly Error Invalid = Error.Validation(InvalidCode, "The phone number is not valid.");
}
