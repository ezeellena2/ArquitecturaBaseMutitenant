using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Domain.Assistant;

public sealed class AssistantNoticeAcceptance : Entity
{
    private AssistantNoticeAcceptance() { }
    public Guid OwnerUserId { get; private set; }
    public Access Access { get; private set; }
    public string NoticeVersion { get; private set; } = string.Empty;
    public DateTime AcceptedAtUtc { get; private set; }

    public static AssistantNoticeAcceptance Create(Guid ownerUserId, Access access, string noticeVersion, DateTime nowUtc)
    {
        if (ownerUserId == Guid.Empty) throw new ArgumentException("An owner is required.", nameof(ownerUserId));
        if (!Enum.IsDefined(access)) throw new ArgumentOutOfRangeException(nameof(access));
        ArgumentException.ThrowIfNullOrWhiteSpace(noticeVersion);
        if (nowUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("UTC is required.", nameof(nowUtc));
        return new AssistantNoticeAcceptance { OwnerUserId = ownerUserId, Access = access, NoticeVersion = noticeVersion, AcceptedAtUtc = nowUtc };
    }
}
