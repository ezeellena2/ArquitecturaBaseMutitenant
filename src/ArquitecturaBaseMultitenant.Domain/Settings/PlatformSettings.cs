using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Domain.Settings;

public sealed class PlatformSettings : Entity, IAuditable
{
    public const string SingletonIdValue = "00000000-0000-0000-0000-000000000001";
    public const int DeletionGraceDays = 30;
    public static readonly Guid SingletonId = new(SingletonIdValue);

    private PlatformSettings() : base(SingletonId) { }

    public ConsumerSignupMode ConsumerSignup { get; private set; }
    public BusinessSignupMode BusinessSignup { get; private set; }
    public int MaxOwnedOrganizations { get; private set; }
    public int AccountDeletionGraceDays { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public Guid? CreatedBy { get; private set; }
    public DateTime? ModifiedAtUtc { get; private set; }
    public Guid? ModifiedBy { get; private set; }

    public static PlatformSettings Create(ConsumerSignupMode consumerSignup, BusinessSignupMode businessSignup,
        int maxOwnedOrganizations)
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

        return new PlatformSettings
        {
            ConsumerSignup = consumerSignup,
            BusinessSignup = businessSignup,
            MaxOwnedOrganizations = maxOwnedOrganizations,
            AccountDeletionGraceDays = DeletionGraceDays,
        };
    }

    public Result CanRegisterConsumer() => CanRegisterConsumer(ConsumerSignup);

    public static Result CanRegisterConsumer(ConsumerSignupMode mode) =>
        mode == ConsumerSignupMode.Open ? Result.Success() : SignupErrors.Closed;
}
