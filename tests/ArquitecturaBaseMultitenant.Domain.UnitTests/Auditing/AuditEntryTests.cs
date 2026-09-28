using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Common;

namespace ArquitecturaBaseMultitenant.Domain.UnitTests.Auditing;

public sealed class AuditEntryTests
{
    [Fact]
    public void Entry_preserves_actor_tenant_action_time_and_diff_without_public_mutation()
    {
        var tenantId = Guid.CreateVersion7();
        var actorId = Guid.CreateVersion7();
        var entityId = Guid.CreateVersion7();
        const string changes = "{\"Name\":{\"old\":\"A\",\"new\":\"B\"}}";
        var occurredAtUtc = new DateTime(2026, 9, 28, 12, 0, 0, DateTimeKind.Utc);

        var entry = AuditEntry.Create(tenantId, AuditActorKind.User, actorId,
            AuditAction.Updated, "Widget", entityId, changes, occurredAtUtc);

        Assert.Equal(tenantId, entry.TenantId);
        Assert.Equal(AuditActorKind.User, entry.ActorKind);
        Assert.Equal(actorId, entry.ActorId);
        Assert.Equal(AuditAction.Updated, entry.Action);
        Assert.Equal("Widget", entry.EntityType);
        Assert.Equal(entityId, entry.EntityId);
        Assert.Equal(changes, entry.Changes);
        Assert.Equal(occurredAtUtc, entry.OccurredAtUtc);
        Assert.IsAssignableFrom<ITenantOwned>(entry);
        Assert.All(typeof(AuditEntry).GetProperties(), property => Assert.Null(property.GetSetMethod()));
        Assert.Null(typeof(AuditEntry).GetMethod("Remove"));
    }

    [Fact]
    public void Entry_rejects_a_non_utc_instant()
    {
        var occurredAt = new DateTime(2026, 9, 28, 12, 0, 0, DateTimeKind.Local);

        Assert.Throws<ArgumentException>(() => AuditEntry.Create(Guid.CreateVersion7(),
            AuditActorKind.System, null, AuditAction.Custom, "Export", Guid.CreateVersion7(), "{}", occurredAt));
    }
}
