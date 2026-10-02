using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Domain.Authentication;

public static class BusinessSignupErrors
{
    public const string LimitReachedCode = "Tenancy.Organization.LimitReached";
    public const string IdentityRequiredCode = "Auth.BusinessSignup.IdentityRequired";
    public const string ActiveSpaceCode = "Auth.BusinessSignup.ActiveSpace";

    public static readonly Error LimitReached = Error.Conflict(LimitReachedCode,
        "The account has reached its organization limit.");
    public static readonly Error IdentityRequired = Error.Unauthorized(IdentityRequiredCode,
        "Sign in before creating an organization.");
    public static readonly Error ActiveSpace = Error.Forbidden(ActiveSpaceCode,
        "Create an organization from the account session without an active space.");
}
