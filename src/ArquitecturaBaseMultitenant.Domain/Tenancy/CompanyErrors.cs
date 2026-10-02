using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Domain.Tenancy;

public static class CompanyErrors
{
    public const string NameRequiredCode = "Tenancy.Company.NameRequired";
    public const string NameTooLongCode = "Tenancy.Company.NameTooLong";
    public const string TimeZoneRequiredCode = "Tenancy.Company.TimeZoneRequired";
    public const string TimeZoneUnavailableCode = "Tenancy.Company.TimeZoneUnavailable";
    public const string TaxIdAlreadyUsedCode = "Tenancy.Company.TaxIdAlreadyUsed";
    public const string NotFoundCode = "Tenancy.Company.NotFound";

    public static readonly Error NameRequired = Error.Validation(NameRequiredCode, "A company name is required.");
    public static readonly Error NameTooLong = Error.Validation(NameTooLongCode, "The company name is too long.");
    public static readonly Error TimeZoneRequired = Error.Validation(TimeZoneRequiredCode, "A time zone is required.");
    public static readonly Error TimeZoneUnavailable = Error.Validation(TimeZoneUnavailableCode, "The time zone is unavailable.");
    public static readonly Error TaxIdAlreadyUsed = Error.Conflict(TaxIdAlreadyUsedCode, "The tax identification number is already registered.");
    public static readonly Error NotFound = Error.NotFound(NotFoundCode, "The company was not found.");
}
