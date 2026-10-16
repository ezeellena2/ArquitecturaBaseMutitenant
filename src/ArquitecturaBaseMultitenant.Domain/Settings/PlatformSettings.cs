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
