using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Legal;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Domain.Users;

/// <summary>Reglas de estado y fechas de la cuenta global, independientes del adaptador Identity.</summary>
public sealed record AccountLifecycle(UserStatus Status, DateTime? DeletionRequestedAtUtc = null,
    DateTime? DeletionScheduledForUtc = null, string? DeletionReason = null,
    Guid? DeletionRequestedByOperatorId = null, DateTime? DeletedAtUtc = null)
{
    public Result<AccountLifecycle> Suspend() => Status == UserStatus.Active
        ? this with { Status = UserStatus.Suspended } : UserErrors.InvalidTransition;

    public Result<AccountLifecycle> Reactivate() => Status == UserStatus.Suspended
        ? this with { Status = DeletionScheduledForUtc is null ? UserStatus.Active : UserStatus.PendingDeletion }
        : UserErrors.InvalidTransition;

    public Result<AccountLifecycle> RequestDeletion(string reason, DateTime requestedAtUtc, int graceDays,
        Guid? operatorId = null)
    {
        RequireUtc(requestedAtUtc);
        ArgumentOutOfRangeException.ThrowIfLessThan(graceDays, 1);
        if (DeletionScheduledForUtc is not null) return AccountDeletionErrors.AlreadyPending;
        if (Status != UserStatus.Active && !(Status == UserStatus.Suspended && operatorId is not null))
            return UserErrors.InvalidTransition;
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > TextLimits.Description)
            return AccountDeletionErrors.InvalidReason;
        return this with
        {
            Status = Status == UserStatus.Active ? UserStatus.PendingDeletion : Status,
            DeletionRequestedAtUtc = requestedAtUtc, DeletionScheduledForUtc = requestedAtUtc.AddDays(graceDays),
            DeletionReason = reason, DeletionRequestedByOperatorId = operatorId,
        };
    }

    public Result<AccountLifecycle> CancelDeletion(DateTime cancelledAtUtc)
    {
        RequireUtc(cancelledAtUtc);
        if (Status == UserStatus.Suspended) return AccountErrors.Suspended;
        if (Status != UserStatus.PendingDeletion || DeletionScheduledForUtc is null) return UserErrors.InvalidTransition;
        if (cancelledAtUtc >= DeletionScheduledForUtc) return AccountDeletionErrors.GraceExpired;
        return this with { Status = UserStatus.Active, DeletionRequestedAtUtc = null,
            DeletionScheduledForUtc = null, DeletionReason = null, DeletionRequestedByOperatorId = null };
    }

    public Result<AccountLifecycle> CompleteDeletion(DateTime deletedAtUtc)
    {
        RequireUtc(deletedAtUtc);
        if (Status == UserStatus.Deleted) return this;
        if (Status is not (UserStatus.PendingDeletion or UserStatus.Suspended)
            || DeletionScheduledForUtc is null || deletedAtUtc < DeletionScheduledForUtc) return UserErrors.InvalidTransition;
        return this with { Status = UserStatus.Deleted, DeletedAtUtc = deletedAtUtc, DeletionReason = null };
    }

    private static void RequireUtc(DateTime instantUtc)
    {
        if (instantUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("The instant must be UTC.", nameof(instantUtc));
    }
}
