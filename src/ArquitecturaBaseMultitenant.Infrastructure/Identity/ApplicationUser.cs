using System.Globalization;
using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Legal;
using ArquitecturaBaseMultitenant.Domain.Platform;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;
using Microsoft.AspNetCore.Identity;

namespace ArquitecturaBaseMultitenant.Infrastructure.Identity;

/// <summary>
/// Identidad global. Los métodos verificados viven en identity.LoginMethods;
/// las propiedades heredadas Email y PhoneNumber son solo copias del método principal.
/// </summary>
public sealed class ApplicationUser : IdentityUser<Guid>, IVersioned
{
    public uint Version { get; private set; }
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
    public DateTime? CreatedAtUtc { get; private set; }
    public Guid? DeletionLeaseId { get; private set; }
    public DateTime? DeletionLeaseExpiresAtUtc { get; private set; }

    /// <summary>
    /// ADR 0042: vencimiento de la invitación de operador sin aceptar. Es null en una cuenta común y en un operador
    /// que ya entró; en una cuenta eliminada marca una invitación revocada, que no figura en Cuentas.
    /// </summary>
    public DateTime? OperatorInvitationExpiresAtUtc { get; private set; }

    public static Result<ApplicationUser> Create(string? displayName, string culture, string timeZoneId,
        DateTime? createdAtUtc = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(culture);
        ArgumentException.ThrowIfNullOrWhiteSpace(timeZoneId);
        if (createdAtUtc is { } created) RequireUtc(created);
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
            CreatedAtUtc = createdAtUtc,
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

    public void UpdatePreferences(string culture, string timeZoneId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(culture);
        ArgumentException.ThrowIfNullOrWhiteSpace(timeZoneId);
        Culture = culture;
        TimeZoneId = timeZoneId;
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

    public void GrantPlatformOperator() => IsPlatformOperator = true;

    public void RevokePlatformOperator() => IsPlatformOperator = false;

    private bool HasPendingOperatorInvitation => Status == UserStatus.Active && OperatorInvitationExpiresAtUtc is not null;

    /// <summary>ADR 0042: la cuenta nueva de un operador invitado queda pendiente hasta su primer ingreso.</summary>
    public void InvitePlatformOperator(DateTime expiresAtUtc)
    {
        RequireUtc(expiresAtUtc);
        if (Status != UserStatus.Active || IsPlatformOperator || OperatorInvitationExpiresAtUtc is not null)
            throw new InvalidOperationException("Only a new account can be invited as a platform operator.");
        IsPlatformOperator = true;
        OperatorInvitationExpiresAtUtc = expiresAtUtc;
    }

    /// <summary>Reenviar: una invitación sin aceptar, vigente o vencida, vence de nuevo en la fecha dada.</summary>
    public Result RenewOperatorInvitation(DateTime expiresAtUtc)
    {
        RequireUtc(expiresAtUtc);
        if (!HasPendingOperatorInvitation) return PlatformOperatorErrors.NoPendingInvitation;
        OperatorInvitationExpiresAtUtc = expiresAtUtc;
        return Result.Success();
    }

    /// <summary>El primer ingreso con código a Plataforma acepta la invitación, si todavía no venció.</summary>
    public Result AcceptOperatorInvitation(DateTime acceptedAtUtc)
    {
        RequireUtc(acceptedAtUtc);
        if (!HasPendingOperatorInvitation || acceptedAtUtc >= OperatorInvitationExpiresAtUtc)
            return UserErrors.InvalidTransition;
        OperatorInvitationExpiresAtUtc = null;
        return Result.Success();
    }

    /// <summary>
    /// Revocar: la identidad de una invitación sin aceptar queda eliminada y anónima, sin pasar por la gracia. Conserva
    /// el vencimiento como marca de invitación revocada, que no figura en Cuentas.
    /// </summary>
    public Result DiscardOperatorInvitation(DateTime discardedAtUtc, string deletedDisplayName)
    {
        RequireUtc(discardedAtUtc);
        ArgumentException.ThrowIfNullOrWhiteSpace(deletedDisplayName);
        if (!HasPendingOperatorInvitation) return PlatformOperatorErrors.NoPendingInvitation;
        Anonymize(discardedAtUtc, deletedDisplayName);
        return Result.Success();
    }

    public Result RequestDeletion(string reason, DateTime requestedAtUtc, int graceDays, Guid? operatorId = null)
    {
        RequireUtc(requestedAtUtc);
        ArgumentOutOfRangeException.ThrowIfLessThan(graceDays, 1);
        if (DeletionScheduledForUtc is not null) return AccountDeletionErrors.AlreadyPending;
        if (Status != UserStatus.Active && !(Status == UserStatus.Suspended && operatorId is not null))
            return UserErrors.InvalidTransition;
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > TextLimits.Description)
            return AccountDeletionErrors.InvalidReason;
        DeletionRequestedAtUtc = requestedAtUtc;
        DeletionScheduledForUtc = requestedAtUtc.AddDays(graceDays);
        DeletionReason = reason;
        DeletionRequestedByOperatorId = operatorId;
        if (Status == UserStatus.Active) Status = UserStatus.PendingDeletion;
        return Result.Success();
    }

    public Result CancelDeletion(DateTime cancelledAtUtc)
    {
        RequireUtc(cancelledAtUtc);
        if (Status == UserStatus.Suspended) return AccountErrors.Suspended;
        if (Status != UserStatus.PendingDeletion || DeletionScheduledForUtc is null) return UserErrors.InvalidTransition;
        if (cancelledAtUtc >= DeletionScheduledForUtc) return AccountDeletionErrors.GraceExpired;
        Status = UserStatus.Active;
        DeletionRequestedAtUtc = null;
        DeletionScheduledForUtc = null;
        DeletionReason = null;
        DeletionRequestedByOperatorId = null;
        return Result.Success();
    }

    public Result CompleteDeletion(DateTime deletedAtUtc, string deletedDisplayName)
    {
        RequireUtc(deletedAtUtc);
        ArgumentException.ThrowIfNullOrWhiteSpace(deletedDisplayName);
        if (Status == UserStatus.Deleted) return Result.Success();
        if (Status is not (UserStatus.PendingDeletion or UserStatus.Suspended)
            || DeletionScheduledForUtc is null || deletedAtUtc < DeletionScheduledForUtc) return UserErrors.InvalidTransition;
        Anonymize(deletedAtUtc, deletedDisplayName);
        return Result.Success();
    }

    private void Anonymize(DateTime deletedAtUtc, string deletedDisplayName)
    {
        Status = UserStatus.Deleted;
        DeletedAtUtc = deletedAtUtc;
        DisplayName = deletedDisplayName;
        Email = NormalizedEmail = PhoneNumber = PasswordHash = null;
        EmailConfirmed = PhoneNumberConfirmed = TwoFactorEnabled = IsPlatformOperator = false;
        Culture = TimeZoneId = string.Empty;
        LastBusinessTenantId = null;
        DeletionReason = null;
        SecurityStamp = Guid.CreateVersion7().ToString("N");
        DeletionLeaseId = null;
        DeletionLeaseExpiresAtUtc = null;
        LockoutEnd = null;
        AccessFailedCount = 0;
    }

    private static void RequireUtc(DateTime instantUtc)
    {
        if (instantUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("The instant must be UTC.", nameof(instantUtc));
    }

    private static bool IsValidDisplayName(string? displayName) =>
        displayName is null || displayName.Length <= TextLimits.PersonName;
}
