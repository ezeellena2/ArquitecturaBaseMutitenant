using ArquitecturaBaseMultitenant.Domain.Auditing;

namespace ArquitecturaBaseMultitenant.Domain.UnitTests.Auditing;

public sealed class SecurityEventTests
{
    [Fact]
    public void Account_event_preserves_actor_type_and_utc_instant_without_personal_identifiers()
    {
        var actorId = Guid.CreateVersion7();
        var occurredAtUtc = new DateTime(2026, 9, 29, 13, 0, 0, DateTimeKind.Utc);

        var securityEvent = SecurityEvent.ForAccount(SecurityEventType.LoginMethodChanged, actorId, occurredAtUtc);

        Assert.Equal(SecurityEventType.LoginMethodChanged, securityEvent.Type);
        Assert.Equal(AuditActorKind.User, securityEvent.ActorKind);
        Assert.Equal(actorId, securityEvent.ActorId);
        Assert.Equal(occurredAtUtc, securityEvent.OccurredAtUtc);
        Assert.Null(securityEvent.TargetTenantId);
        Assert.Null(securityEvent.Reason);
        Assert.Null(typeof(SecurityEvent).GetProperty("Email"));
        Assert.Null(typeof(SecurityEvent).GetProperty("PhoneNumber"));
        Assert.Null(typeof(SecurityEvent).GetProperty("Code"));
        Assert.All(typeof(SecurityEvent).GetProperties(), property => Assert.Null(property.GetSetMethod()));
    }

    [Fact]
    public void Account_event_rejects_local_time()
    {
        var local = new DateTime(2026, 9, 29, 13, 0, 0, DateTimeKind.Local);

        Assert.Throws<ArgumentException>(() => SecurityEvent.ForAccount(
            SecurityEventType.AccountDeletionRequested, Guid.CreateVersion7(), local));
    }
}
