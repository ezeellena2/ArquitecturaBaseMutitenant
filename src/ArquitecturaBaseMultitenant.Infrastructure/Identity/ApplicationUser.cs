using System.Globalization;
using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;
using Microsoft.AspNetCore.Identity;

namespace ArquitecturaBaseMultitenant.Infrastructure.Identity;

/// <summary>
/// Identidad global. Los métodos verificados viven en identity.LoginMethods;
/// las propiedades heredadas Email y PhoneNumber son solo copias del método principal.
/// </summary>
public sealed class ApplicationUser : IdentityUser<Guid>
{
    public string? DisplayName { get; private set; }
    public string Culture { get; private set; } = string.Empty;
    public string TimeZoneId { get; private set; } = string.Empty;
    public UserStatus Status { get; private set; } = UserStatus.Active;
    public bool IsPlatformOperator { get; private set; }
    public Guid? LastBusinessTenantId { get; private set; }
    public DateTime? DeletionRequestedAtUtc { get; private set; }
    public DateTime? DeletionScheduledForUtc { get; private set; }
    public string? DeletionReason { get; private set; }
    public Guid? DeletionRequestedByOperatorId { get; private set; }
    public DateTime? DeletedAtUtc { get; private set; }

    public static Result<ApplicationUser> Create(string? displayName, string culture, string timeZoneId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(culture);
        ArgumentException.ThrowIfNullOrWhiteSpace(timeZoneId);
        if (!IsValidDisplayName(displayName))
        {
            return UserErrors.InvalidDisplayName;
        }

        var user = new ApplicationUser
        {
            Id = Guid.CreateVersion7(),
            DisplayName = displayName,
            Culture = culture,
            TimeZoneId = timeZoneId,
        };
        user.UserName = user.Id.ToString("D", CultureInfo.InvariantCulture);
        return user;
    }

    public Result Rename(string? displayName)
    {
        if (!IsValidDisplayName(displayName))
        {
            return UserErrors.InvalidDisplayName;
        }

        DisplayName = displayName;
        return Result.Success();
    }

    public Result Suspend()
    {
        if (Status != UserStatus.Active)
        {
            return UserErrors.InvalidTransition;
        }

        Status = UserStatus.Suspended;
        return Result.Success();
    }

    public Result Reactivate()
    {
        if (Status != UserStatus.Suspended)
        {
            return UserErrors.InvalidTransition;
        }

        Status = DeletionScheduledForUtc is null ? UserStatus.Active : UserStatus.PendingDeletion;
        return Result.Success();
    }

    public void RememberBusinessTenant(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("The business tenant id cannot be empty.", nameof(tenantId));
        }

        LastBusinessTenantId = tenantId;
    }

    private static bool IsValidDisplayName(string? displayName) =>
        displayName is null || displayName.Length <= TextLimits.PersonName;
}
