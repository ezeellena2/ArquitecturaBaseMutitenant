using ArquitecturaBaseMultitenant.Application.Models.Idempotency;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.Modules.Assistant;

public sealed class AssistantSnapshotRetentionPolicyTests
{
    [Theory]
    [InlineData(-1, true)]
    [InlineData(0, false)]
    [InlineData(1, false)]
    public void Original_content_expires_at_exactly_twenty_four_hours(long offsetTicks, bool expected)
    {
        var created = new DateTime(2026, 10, 11, 12, 0, 0, DateTimeKind.Utc);
        var row = new AssistantDurableOperationRow(new IdempotentReference("assistant.quick", Guid.NewGuid(), 200, null),
            Guid.NewGuid(), "hash", "/route", "{}", "owners", 42, created);
        var now = created.AddHours(24).AddTicks(offsetTicks);
        Assert.Equal(expected, AssistantSnapshotRetentionPolicy.HasContent(row, now));
        Assert.False(AssistantSnapshotRetentionPolicy.HasContent(row with { SnapshotJson = null }, now));
        Assert.Equal(created, row.CreatedAtUtc);
    }
}
