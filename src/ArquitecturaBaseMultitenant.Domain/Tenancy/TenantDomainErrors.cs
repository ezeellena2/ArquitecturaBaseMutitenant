using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Domain.Tenancy;

public static class TenantDomainErrors
{
    public const string InvalidDomainCode = "Settings.TenantDomain.InvalidDomain";
    public const string AlreadyExistsCode = "Settings.TenantDomain.AlreadyExists";
    public const string NotFoundCode = "Settings.TenantDomain.NotFound";
    public const string AlreadyVerifiedCode = "Settings.TenantDomain.AlreadyVerified";
    public const string TxtNotFoundCode = "Settings.TenantDomain.TxtNotFound";
    public const string DnsUnavailableCode = "Settings.TenantDomain.DnsUnavailable";
    public const string AlreadyRemovedCode = "Settings.TenantDomain.AlreadyRemoved";

    public static readonly Error InvalidDomain = Error.Validation(InvalidDomainCode, "The email domain is invalid.");
    public static readonly Error AlreadyExists = Error.Conflict(AlreadyExistsCode,
        "The email domain is already registered.");
    public static readonly Error NotFound = Error.NotFound(NotFoundCode, "The email domain was not found.");
    public static readonly Error AlreadyVerified = Error.Conflict(AlreadyVerifiedCode,
        "The email domain is already verified.");
    public static readonly Error TxtNotFound = Error.Conflict(TxtNotFoundCode,
        "The verification TXT record was not found.");
    public static readonly Error DnsUnavailable = Error.ServiceUnavailable(DnsUnavailableCode,
        "The DNS verification service is unavailable.");
    public static readonly Error AlreadyRemoved = Error.Conflict(AlreadyRemovedCode,
        "The email domain has already been removed.");
}
