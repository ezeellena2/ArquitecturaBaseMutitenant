using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Domain.Tenancy;

public static class CompanyErrors
{
    public const string NameRequiredCode = "Tenancy.Company.NameRequired";
    public const string NameTooLongCode = "Tenancy.Company.NameTooLong";
    public const string TimeZoneRequiredCode = "Tenancy.Company.TimeZoneRequired";

    public static readonly Error NameRequired = Error.Validation(NameRequiredCode, "A company name is required.");
    public static readonly Error NameTooLong = Error.Validation(NameTooLongCode, "The company name is too long.");
    public static readonly Error TimeZoneRequired = Error.Validation(TimeZoneRequiredCode, "A time zone is required.");
}
