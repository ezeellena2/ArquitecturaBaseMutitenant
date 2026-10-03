using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Domain.PublicSite;

public static class PublicPageErrors
{
    public const string NameRequiredCode = "PublicSite.PublicPage.NameRequired";
    public const string NameTooLongCode = "PublicSite.PublicPage.NameTooLong";
    public const string DescriptionTooLongCode = "PublicSite.PublicPage.DescriptionTooLong";
    public const string ContactTooLongCode = "PublicSite.PublicPage.ContactTooLong";
    public const string InvalidLogoCode = "PublicSite.PublicPage.InvalidLogo";
    public const string PublishBlockedCode = "PublicSite.PublicPage.PublishBlocked";
    public const string BlockReasonRequiredCode = "PublicSite.PublicPage.BlockReasonRequired";
    public const string NotFoundCode = "PublicSite.PublicPage.NotFound";
    public const string VersionConflictCode = "PublicSite.PublicPage.VersionConflict";

    public static readonly Error NameRequired = Error.Validation(NameRequiredCode, "A public page name is required.");
    public static readonly Error NameTooLong = Error.Validation(NameTooLongCode, "The public page name is too long.");
    public static readonly Error DescriptionTooLong = Error.Validation(DescriptionTooLongCode, "The public page description is too long.");
    public static readonly Error ContactTooLong = Error.Validation(ContactTooLongCode, "A public page contact field is too long.");
    public static readonly Error InvalidLogo = Error.Validation(InvalidLogoCode, "The public page logo is invalid.");
    public static readonly Error PublishBlocked = Error.Conflict(PublishBlockedCode, "The platform has blocked this public page.");
    public static readonly Error BlockReasonRequired = Error.Validation(BlockReasonRequiredCode, "A moderation block reason is required.");
    public static readonly Error NotFound = Error.NotFound(NotFoundCode, "The public page was not found.");
    public static readonly Error VersionConflict = Error.Conflict(VersionConflictCode, "The public page has changed since it was loaded.");
}
