using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Domain.Settings;

/// <summary>
/// Configuración global única para los modos de registro, el límite de organizaciones, la gracia de baja, los correos
/// de contacto y las cuotas compartidas (E9). Los correos quedan vacíos hasta que un operador los carga.
/// </summary>
public sealed class PlatformSettings : Entity, IAuditable, IVersioned
{
    public const string SingletonIdValue = "00000000-0000-0000-0000-000000000001";
    public const int DeletionGraceDays = 30;
    public static readonly Guid SingletonId = new(SingletonIdValue);

    private PlatformSettings() : base(SingletonId) { }

    public ConsumerSignupMode ConsumerSignup { get; private set; }
    public BusinessSignupMode BusinessSignup { get; private set; }
    public int MaxOwnedOrganizations { get; private set; }
    public int AccountDeletionGraceDays { get; private set; }
    public Email? SalesEmail { get; private set; }
    public Email? SupportEmail { get; private set; }
    public PlatformRateLimits RateLimits { get; private set; } = PlatformRateLimits.Initial;
    /// <summary>«Privacidad y retención» (decisión 272): cuánto se guardan los ingresos, los códigos y las invitaciones cerradas.</summary>
    public int LoginAuditRetentionMonths { get; private set; } = PlatformSettingsLimits.DefaultLoginAuditRetentionMonths;
    public int LoginCodeRetentionDays { get; private set; } = PlatformSettingsLimits.DefaultLoginCodeRetentionDays;
    public int ClosedInvitationRetentionDays { get; private set; } = PlatformSettingsLimits.DefaultClosedInvitationRetentionDays;
    /// <summary>
    /// «Nombre y correos» (decisión 273): null mientras Plataforma no los guarda, y entonces valen <c>Email:AppName</c> y
    /// <c>Email:Smtp:FromName</c> (0060). El logo es null mientras no se guarda (vale <c>Email:LogoUrl</c>) y vacío si Plataforma lo
    /// quitó: así no vuelve el de arranque (<see cref="EffectiveLogoUrl"/>). Sin logo, los correos llevan solo el nombre.
    /// </summary>
    public string? AppName { get; private set; }
    public string? SenderName { get; private set; }
    public string? EmailLogoUrl { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public Guid? CreatedBy { get; private set; }
    public DateTime? ModifiedAtUtc { get; private set; }
    public Guid? ModifiedBy { get; private set; }
    public uint Version { get; private set; }

    /// <summary>Sin <paramref name="rateLimits"/> arranca con <see cref="PlatformRateLimits.Initial"/>.</summary>
    public static PlatformSettings Create(ConsumerSignupMode consumerSignup, BusinessSignupMode businessSignup,
        int maxOwnedOrganizations, PlatformRateLimits? rateLimits = null)
    {
        if (!Enum.IsDefined(consumerSignup))
        {
            throw new ArgumentOutOfRangeException(nameof(consumerSignup));
        }

        if (!Enum.IsDefined(businessSignup))
        {
            throw new ArgumentOutOfRangeException(nameof(businessSignup));
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(maxOwnedOrganizations, 1);
        if (rateLimits is not null && !rateLimits.IsValid())
        {
            throw new ArgumentOutOfRangeException(nameof(rateLimits));
        }

        return new PlatformSettings
        {
            ConsumerSignup = consumerSignup,
            BusinessSignup = businessSignup,
            MaxOwnedOrganizations = maxOwnedOrganizations,
            AccountDeletionGraceDays = DeletionGraceDays,
            RateLimits = rateLimits ?? PlatformRateLimits.Initial,
        };
    }

    /// <summary>Sin <paramref name="rateLimits"/> se conservan las cuotas guardadas; con ellas, se reemplazan las doce.</summary>
    public Result Update(ConsumerSignupMode consumerSignup, BusinessSignupMode businessSignup, int maxOwnedOrganizations,
        Email salesEmail, Email supportEmail, PlatformRateLimits? rateLimits = null)
    {
        if (!Enum.IsDefined(consumerSignup) || !Enum.IsDefined(businessSignup) || maxOwnedOrganizations < 1
            || salesEmail is null || supportEmail is null || rateLimits?.IsValid() == false)
            return SettingsErrors.InvalidPlatformConfiguration;
        ConsumerSignup = consumerSignup;
        BusinessSignup = businessSignup;
        MaxOwnedOrganizations = maxOwnedOrganizations;
        SalesEmail = salesEmail;
        SupportEmail = supportEmail;
        RateLimits = rateLimits ?? RateLimits;
        return Result.Success();
    }

    /// <summary>Los plazos de retención del núcleo que usan las purgas de la ADR 0062.</summary>
    public IdentityRetentionPeriods RetentionPeriods =>
        new(LoginAuditRetentionMonths, LoginCodeRetentionDays, ClosedInvitationRetentionDays);

    /// <summary>
    /// Los plazos (272) y el nombre, el remitente y el logo de los correos (273): lo que falta (null) conserva lo guardado, y
    /// un logo vacío lo quita (queda vacío, no null). Nada cambia si un valor está fuera de sus cotas.
    /// </summary>
    public Result UpdateRules(int? loginAuditRetentionMonths, int? loginCodeRetentionDays, int? closedInvitationRetentionDays,
        string? appName, string? senderName, string? emailLogoUrl)
    {
        if (loginAuditRetentionMonths is < PlatformSettingsLimits.MinLoginAuditRetentionMonths
                or > PlatformSettingsLimits.MaxLoginAuditRetentionMonths
            || loginCodeRetentionDays is < PlatformSettingsLimits.MinLoginCodeRetentionDays
                or > PlatformSettingsLimits.MaxLoginCodeRetentionDays
            || closedInvitationRetentionDays is < PlatformSettingsLimits.MinClosedInvitationRetentionDays
                or > PlatformSettingsLimits.MaxClosedInvitationRetentionDays
            || appName is not null && (string.IsNullOrWhiteSpace(appName) || appName.Length > PlatformSettingsLimits.NameMaxLength)
            || senderName is not null && (string.IsNullOrWhiteSpace(senderName) || senderName.Length > PlatformSettingsLimits.NameMaxLength)
            || !string.IsNullOrEmpty(emailLogoUrl) && !IsSecureUrl(emailLogoUrl))
            return SettingsErrors.InvalidPlatformConfiguration;
        LoginAuditRetentionMonths = loginAuditRetentionMonths ?? LoginAuditRetentionMonths;
        LoginCodeRetentionDays = loginCodeRetentionDays ?? LoginCodeRetentionDays;
        ClosedInvitationRetentionDays = closedInvitationRetentionDays ?? ClosedInvitationRetentionDays;
        AppName = appName?.Trim() ?? AppName;
        SenderName = senderName?.Trim() ?? SenderName;
        EmailLogoUrl = emailLogoUrl ?? EmailLogoUrl;
        return Result.Success();
    }

    /// <summary>
    /// El logo con que salen los correos y que muestra la pantalla: el guardado; ninguno si Plataforma lo quitó (vacío); y mientras
    /// no lo guardó (null), el de arranque, <paramref name="bootstrapLogoUrl"/>.
    /// </summary>
    public static string? EffectiveLogoUrl(string? savedLogoUrl, string? bootstrapLogoUrl) =>
        savedLogoUrl is null ? bootstrapLogoUrl : savedLogoUrl.Length == 0 ? null : savedLogoUrl;

    /// <summary>Una dirección absoluta <c>https://</c>, hasta <see cref="PlatformSettingsLimits.LogoUrlMaxLength"/> caracteres.</summary>
    public static bool IsSecureUrl(string? url) =>
        url is { Length: > 0 and <= PlatformSettingsLimits.LogoUrlMaxLength }
        && Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps;

    public Result CanRegisterConsumer() => CanRegisterConsumer(ConsumerSignup);

    public static Result CanRegisterConsumer(ConsumerSignupMode mode) =>
        mode == ConsumerSignupMode.Open ? Result.Success() : SignupErrors.Closed;

    public Result CanRegisterBusiness() => CanRegisterBusiness(BusinessSignup);

    public static Result CanRegisterBusiness(BusinessSignupMode mode) =>
        mode == BusinessSignupMode.Closed ? SignupErrors.BusinessClosed : Result.Success();

    public Result CanCreateBusinessOrganization(int ownedOrganizationCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(ownedOrganizationCount);
        var signup = CanRegisterBusiness();
        if (signup.IsFailure) return signup;
        // El tope viaja en el error para que el registro diga «Llegaste al límite de N organizaciones propias».
        return ownedOrganizationCount >= MaxOwnedOrganizations
            ? BusinessSignupErrors.LimitReached with { Metadata = new Dictionary<string, object?> { ["limit"] = MaxOwnedOrganizations } }
            : Result.Success();
    }
}
