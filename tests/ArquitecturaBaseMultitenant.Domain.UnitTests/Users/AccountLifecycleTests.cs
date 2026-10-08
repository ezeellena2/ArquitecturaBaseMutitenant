using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Legal;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Domain.UnitTests.Users;

public sealed class AccountLifecycleTests
{
    private static readonly DateTime NowUtc = new(2026, 10, 8, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Operator_deletion_preserves_suspension_and_reactivation_restores_pending_deletion()
    {
        var suspended = new AccountLifecycle(UserStatus.Suspended);
        Assert.True(suspended.RequestDeletion("Pedido", NowUtc, 30).IsFailure);
        var pending = suspended.RequestDeletion("Pedido", NowUtc, 30, Guid.CreateVersion7()).Value;
        Assert.Equal(UserStatus.Suspended, pending.Status);
        Assert.Equal(AccountErrors.Suspended.Code, pending.CancelDeletion(NowUtc).Error.Code);
        Assert.Equal(UserStatus.PendingDeletion, pending.Reactivate().Value.Status);
    }

    [Fact]
    public void Grace_boundary_cannot_be_cancelled_and_can_be_completed()
    {
        var pending = new AccountLifecycle(UserStatus.Active).RequestDeletion("Pedido", NowUtc, 30).Value;
        Assert.Equal(AccountDeletionErrors.GraceExpired.Code,
            pending.CancelDeletion(NowUtc.AddDays(30)).Error.Code);
        Assert.True(pending.CompleteDeletion(NowUtc.AddDays(30).AddTicks(-1)).IsFailure);
        var deleted = pending.CompleteDeletion(NowUtc.AddDays(30)).Value;
        Assert.Equal(UserStatus.Deleted, deleted.Status);
        Assert.Equal(NowUtc.AddDays(30), deleted.DeletedAtUtc);
        Assert.Same(deleted, deleted.CompleteDeletion(NowUtc.AddDays(31)).Value);
    }

    [Fact]
    public void Cancellation_clears_the_request_and_invalid_transitions_preserve_the_snapshot()
    {
        var active = new AccountLifecycle(UserStatus.Active);
        Assert.True(active.Reactivate().IsFailure);
        var pending = active.RequestDeletion("Pedido", NowUtc, 30).Value;
        Assert.Equal(AccountDeletionErrors.AlreadyPending.Code,
            pending.RequestDeletion("Otro", NowUtc, 30).Error.Code);
        var cancelled = pending.CancelDeletion(NowUtc).Value;
        Assert.Equal(UserStatus.Active, cancelled.Status);
        Assert.Null(cancelled.DeletionScheduledForUtc);
        Assert.Null(cancelled.DeletionReason);
        Assert.Null(cancelled.DeletionRequestedAtUtc);
        Assert.Equal(UserStatus.Active, active.Status);
    }

    [Fact]
    public void Lifecycle_rejects_non_utc_instants()
    {
        var active = new AccountLifecycle(UserStatus.Active);
        Assert.Throws<ArgumentException>(() => active.RequestDeletion("Pedido",
            DateTime.SpecifyKind(NowUtc, DateTimeKind.Unspecified), 30));
    }
}
