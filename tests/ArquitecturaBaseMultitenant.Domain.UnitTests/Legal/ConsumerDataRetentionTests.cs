using ArquitecturaBaseMultitenant.Domain.Legal;

namespace ArquitecturaBaseMultitenant.Domain.UnitTests.Legal;

/// <summary>Protege el vencimiento y la finalización durable de una retención repetida.</summary>
public sealed class ConsumerDataRetentionTests
{
    [Fact]
    public void Replacement_at_the_deadline_is_idempotent_and_keeps_the_original_deadline()
    {
        var deadline = new DateTime(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc);
        var retention = ConsumerDataRetention.Schedule("engagement.Deals", Guid.CreateVersion7(),
            Guid.CreateVersion7(), Guid.CreateVersion7(), "Test.LegalRetention", deadline);
        Assert.Throws<InvalidOperationException>(() => retention.MarkCopyReplaced(deadline.AddTicks(-1)));
        Assert.Null(retention.CopyReplacedAtUtc);
        retention.MarkCopyReplaced(deadline);
        retention.MarkCopyReplaced(deadline.AddDays(1));
        Assert.Equal(deadline, retention.RetainUntilUtc);
        Assert.Equal(deadline, retention.CopyReplacedAtUtc);
    }

    [Fact]
    public void A_retention_cannot_store_an_ambiguous_time()
    {
        Assert.Throws<ArgumentException>(() => ConsumerDataRetention.Schedule("engagement.Deals", Guid.CreateVersion7(),
            Guid.CreateVersion7(), Guid.CreateVersion7(), "Test.LegalRetention", new DateTime(2026, 10, 7)));
    }
}
