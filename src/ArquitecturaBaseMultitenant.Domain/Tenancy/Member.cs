using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Domain.Tenancy;

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

    public Guid UserId { get; private set; }

    public MemberStatus Status { get; private set; }

    public DateTime? JoinedAtUtc { get; private set; }

    public static Member Invite(Guid userId) => new(userId);

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

    public Result Remove()
    {
        if (Status == MemberStatus.Removed)
        {
            return MemberErrors.InvalidTransition;
        }

        Status = MemberStatus.Removed;
        return Result.Success();
    }
}
