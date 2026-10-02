using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Domain.Tenancy;

public static class CompanyMembershipErrors
{
    public const string NotFoundCode = "Companies.Membership.NotFound";
    public const string AlreadyExistsCode = "Companies.Membership.AlreadyExists";
    public const string MemberInactiveCode = "Companies.Membership.MemberInactive";
    public const string LastCompanyAdminCode = "Companies.Membership.LastCompanyAdmin";

    public static readonly Error NotFound = Error.NotFound(NotFoundCode, "The company membership was not found.");
    public static readonly Error AlreadyExists = Error.Conflict(AlreadyExistsCode, "The member already belongs to this company.");
    public static readonly Error MemberInactive = Error.Conflict(MemberInactiveCode, "The organization member is not active.");
    public static readonly Error LastCompanyAdmin = Error.Conflict(LastCompanyAdminCode,
        "The only active company administrator cannot be removed.");
}
