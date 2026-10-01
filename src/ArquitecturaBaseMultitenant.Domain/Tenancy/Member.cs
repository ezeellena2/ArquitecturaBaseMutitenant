using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Domain.Tenancy;

/// <summary>Vincula una identidad global con un espacio privado y conserva su estado y fecha de incorporación.</summary>
public sealed class Member : Entity, ITenantOwned
{
    private Member()
    {
    }

    private Member(Guid userId)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("The user id cannot be empty.", nameof(userId));
        }

        UserId = userId;
        Status = MemberStatus.Invited;
    }

    public Guid TenantId { get; private set; }

    public Guid? UserId { get; private set; }

    public MemberStatus Status { get; private set; }
    public MemberRemovalReason? RemovalReason { get; private set; }

    public DateTime? JoinedAtUtc { get; private set; }

    public static Member Invite(Guid userId) => new(userId);

    public static Member Invite() => new() { Status = MemberStatus.Invited };

    public Result AssignUser(Guid userId)
    {
        if (userId == Guid.Empty) throw new ArgumentException("The user id cannot be empty.", nameof(userId));
        if (Status != MemberStatus.Invited || UserId is not null) return MemberErrors.InvalidTransition;
        UserId = userId;
        return Result.Success();
    }

    public Result Activate(DateTime joinedAtUtc)
    {
        if (joinedAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("The join instant must be UTC.", nameof(joinedAtUtc));
        }

        if (Status is not (MemberStatus.Invited or MemberStatus.Inactive))
        {
            return MemberErrors.InvalidTransition;
        }

        if (UserId is null) return MemberErrors.IdentityRequired;

        JoinedAtUtc ??= joinedAtUtc;
        Status = MemberStatus.Active;
        return Result.Success();
    }

    public Result Deactivate()
    {
        if (Status != MemberStatus.Active)
        {
            return MemberErrors.InvalidTransition;
        }

        Status = MemberStatus.Inactive;
        return Result.Success();
    }

    public Result Remove(MemberRemovalReason? reason = null)
    {
        if (Status == MemberStatus.Removed)
        {
            return MemberErrors.InvalidTransition;
        }

        Status = MemberStatus.Removed;
        RemovalReason = reason;
        return Result.Success();
    }
}
