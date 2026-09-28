using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Domain.Tenancy;

public static class AccessErrors
{
    public const string WrongCode = "Tenancy.Access.Wrong";
    public const string NotMemberCode = "Tenancy.Access.NotMember";
    public const string ConsumerCannotCreateBusinessCode = "Tenancy.Access.ConsumerCannotCreateBusiness";

    public static readonly Error Wrong = Error.Forbidden(WrongCode, "The selected access cannot perform this operation.");
    public static readonly Error NotMember = Error.Forbidden(NotMemberCode, "The user is not a member of this organization.");
    public static readonly Error ConsumerCannotCreateBusiness = Error.Forbidden(ConsumerCannotCreateBusinessCode, "The consumer access cannot create a business.");
}
