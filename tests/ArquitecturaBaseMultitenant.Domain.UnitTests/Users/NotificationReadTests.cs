using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Domain.UnitTests.Users;

public sealed class NotificationReadTests
{
    private static readonly DateTime NowUtc = new(2026, 11, 8, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Marking_keeps_the_account_the_notice_and_the_moment()
    {
        var userId = Guid.NewGuid();
        var id = new string('a', TextLimits.NotificationId);

        var read = NotificationRead.Mark(userId, id, NowUtc);

        Assert.Equal((userId, id, NowUtc), (read.UserId, read.NotificationId, read.ReadAtUtc));
        Assert.NotEqual(Guid.Empty, read.Id);
    }

    [Fact]
    public void Marking_without_account_notice_or_utc_instant_is_a_bug()
    {
        Assert.Throws<ArgumentException>(() => NotificationRead.Mark(Guid.Empty, "a", NowUtc));
        Assert.Throws<ArgumentException>(() => NotificationRead.Mark(Guid.NewGuid(), " ", NowUtc));
        Assert.Throws<ArgumentException>(() =>
            NotificationRead.Mark(Guid.NewGuid(), new string('a', TextLimits.NotificationId + 1), NowUtc));
        Assert.Throws<ArgumentException>(() =>
            NotificationRead.Mark(Guid.NewGuid(), "a", DateTime.SpecifyKind(NowUtc, DateTimeKind.Local)));
    }
}
