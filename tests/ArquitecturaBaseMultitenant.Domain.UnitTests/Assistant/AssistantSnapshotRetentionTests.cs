using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Domain.UnitTests.Assistant;

public sealed class AssistantSnapshotRetentionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Purging_content_clears_personal_references_and_keeps_the_original_technical_binding(bool platform)
    {
        var owner = Guid.CreateVersion7(); var key = Guid.CreateVersion7(); var conversation = Guid.CreateVersion7();
        var subject = Guid.CreateVersion7(); var nowUtc = new DateTime(2026, 10, 11, 0, 0, 0, DateTimeKind.Utc);
        var supplied = new[] { subject, subject, Guid.Empty };
        if (platform)
        {
            var row = PlatformAssistantDurableOperation.Create(owner, key, "body", "/quick", conversation, "quick", 200, null, "{}", "people", nowUtc, supplied);
            Assert.Equal(new[] { subject }, row.SubjectUserIds);
            supplied[0] = Guid.CreateVersion7();
            Assert.Equal(subject, Assert.Single(row.SubjectUserIds));
            row.RemoveContent(); row.RemoveContent();
            Assert.Null(row.SnapshotJson); Assert.Null(row.QuickKey); Assert.Empty(row.SubjectUserIds);
            Assert.Equal(key, row.Key); Assert.Equal("body", row.BodyHash); Assert.Equal("/quick", row.Route);
            Assert.Equal(conversation, row.ConversationId); Assert.Equal("quick", row.Kind); Assert.Equal(nowUtc, row.CreatedAtUtc);
        }
        else
        {
            var row = AssistantDurableOperation.Create(owner, Access.Business, key, "body", "/quick", conversation, "quick", 200, null, "{}", "people", nowUtc, supplied);
            Assert.Equal(new[] { subject }, row.SubjectUserIds);
            supplied[0] = Guid.CreateVersion7();
            Assert.Equal(subject, Assert.Single(row.SubjectUserIds));
            row.RemoveContent(); row.RemoveContent();
            Assert.Null(row.SnapshotJson); Assert.Null(row.QuickKey); Assert.Empty(row.SubjectUserIds);
            Assert.Equal(key, row.Key); Assert.Equal("body", row.BodyHash); Assert.Equal("/quick", row.Route);
            Assert.Equal(conversation, row.ConversationId); Assert.Equal("quick", row.Kind); Assert.Equal(nowUtc, row.CreatedAtUtc);
        }
    }

    [Fact]
    public void Snapshot_expiration_is_fixed_at_twenty_four_hours_from_creation()
    {
        var nowUtc = new DateTime(2026, 10, 11, 0, 0, 0, DateTimeKind.Utc);
        var operation = Guid.CreateVersion7(); var tenant = Guid.CreateVersion7();
        var privateIndex = AssistantSnapshotRetentionIndex.Create(operation, tenant, nowUtc);
        var platformIndex = AssistantSnapshotRetentionIndex.Create(Guid.CreateVersion7(), null, nowUtc);
        Assert.Equal(nowUtc.AddHours(24), privateIndex.ExpiresAtUtc);
        Assert.Equal(nowUtc.AddHours(24), platformIndex.ExpiresAtUtc);
        Assert.Equal(operation, privateIndex.OperationId); Assert.Equal(tenant, privateIndex.ScopeTenantId);
        Assert.Null(platformIndex.ScopeTenantId);
        Assert.Throws<ArgumentException>(() => AssistantSnapshotRetentionIndex.Create(operation, tenant, DateTime.SpecifyKind(nowUtc, DateTimeKind.Unspecified)));
    }
}
