using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Domain.Tenancy;

public static class MemberErrors
{
    public const string InactiveCode = "Tenancy.Member.Inactive";
    public const string InvalidTransitionCode = "Tenancy.Member.InvalidTransition";

    public static readonly Error Inactive = Error.Forbidden(InactiveCode, "The membership is inactive.");
    public static readonly Error InvalidTransition = Error.Conflict(InvalidTransitionCode, "The membership cannot change to that state.");
}
