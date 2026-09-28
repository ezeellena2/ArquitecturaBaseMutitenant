using System.Globalization;
using System.Resources;

namespace ArquitecturaBaseMultitenant.Application.Resources;

public static class ValidationTexts
{
    internal static ResourceManager ResourceManager { get; } =
        new("ArquitecturaBaseMultitenant.Application.Resources.Validation", typeof(ValidationTexts).Assembly);

    public static string Required => Get(nameof(Required));

    public static string MaxLength => Get(nameof(MaxLength));

    public static string EmailInvalid => Get(nameof(EmailInvalid));

    public static string PageInvalid => Get(nameof(PageInvalid));

    public static string PageSizeInvalid => Get(nameof(PageSizeInvalid));

    public static string SortNotAllowed => Get(nameof(SortNotAllowed));

    public static string ReturnUrlInvalid => Get(nameof(ReturnUrlInvalid));

    public static string LoginCodeFormat => Get(nameof(LoginCodeFormat));

    public static string LoginCodeFormatWhatsApp => Get(nameof(LoginCodeFormatWhatsApp));

    public static string EmailOrPhone => Get(nameof(EmailOrPhone));

    public static string CountryInvalid => Get(nameof(CountryInvalid));

    public static string LoginLinkTokenFormat => Get(nameof(LoginLinkTokenFormat));

    public static string RegistrationModeInvalid => Get(nameof(RegistrationModeInvalid));

    public static string PermissionUnknown => Get(nameof(PermissionUnknown));

    public static string CultureInvalid => Get(nameof(CultureInvalid));

    public static string TimeZoneInvalid => Get(nameof(TimeZoneInvalid));

    public static string CreatedWithinDaysInvalid => Get(nameof(CreatedWithinDaysInvalid));

    public static string InvitationChannelInvalid => Get(nameof(InvitationChannelInvalid));

    public static string InvitationEmailRequired => Get(nameof(InvitationEmailRequired));

    public static string InvitationPhoneRequired => Get(nameof(InvitationPhoneRequired));

    public static string InvitationWhatsAppUnavailable => Get(nameof(InvitationWhatsAppUnavailable));

    private static string Get(string key) => ResourceManager.GetString(key, CultureInfo.CurrentUICulture) ?? key;
}
