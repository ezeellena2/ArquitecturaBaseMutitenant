using ArquitecturaBaseMultitenant.Domain.Auditing;
using System.Text.Json;

namespace ArquitecturaBaseMultitenant.Domain.UnitTests.Auditing;

/// <summary>
/// Comprueba que los eventos de cuenta conserven el tipo, actor e instante UTC. Evita incluir
/// identificadores personales en ese registro de seguridad.
/// </summary>
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
        Assert.Equal(actorId, securityEvent.TargetAccountId);
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

    [Fact]
    public void Organization_membership_event_records_actor_and_target_without_personal_data()
    {
        var actorId = Guid.CreateVersion7();
        var tenantId = Guid.CreateVersion7();
        var accountId = Guid.CreateVersion7();
        var occurredAtUtc = new DateTime(2026, 10, 2, 13, 0, 0, DateTimeKind.Utc);

        var securityEvent = SecurityEvent.ForOrganizationMembershipEnded(actorId, tenantId, accountId, occurredAtUtc);

        Assert.Equal(SecurityEventType.OrganizationMembershipEnded, securityEvent.Type);
        Assert.Equal(AuditActorKind.User, securityEvent.ActorKind);
        Assert.Equal(actorId, securityEvent.ActorId);
        Assert.Equal(tenantId, securityEvent.TargetTenantId);
        // La ficha de Plataforma muestra cuándo terminó la membresía de esa cuenta.
        Assert.Equal(accountId, securityEvent.TargetAccountId);
        Assert.Equal(occurredAtUtc, securityEvent.OccurredAtUtc);
        Assert.Null(securityEvent.Reason);
        Assert.Throws<ArgumentException>(() =>
            SecurityEvent.ForOrganizationMembershipEnded(actorId, tenantId, Guid.Empty, occurredAtUtc));
    }

    [Theory]
    [InlineData("203.0.113.27", "203.0.113.*")]
    [InlineData("::ffff:203.0.113.27", "203.0.113.*")]
    [InlineData("2001:db8:abcd:12::9876", "2001:db8:abcd:*")]
    [InlineData("invalid address", null)]
    [InlineData(null, null)]
    public void Evidence_keeps_only_technical_changes_and_masks_the_ip(string? ip, string? expected)
    {
        var actor = Guid.CreateVersion7();
        var tenant = Guid.CreateVersion7();
        var now = new DateTime(2026, 10, 3, 12, 0, 0, DateTimeKind.Utc);
        var changes = new SecurityEventChanges();
        changes.AddEnum("status", Domain.Tenancy.TenantStatus.Active, Domain.Tenancy.TenantStatus.Suspended);
        changes.AddBoolean("publicPage.publishBlocked", false, false);
        var item = SecurityEvent.ForTenantAdministration(SecurityEventType.OrganizationSuspended,
            actor, tenant, "Revisión interna", now).WithEvidence(changes, ip);

        Assert.Equal(tenant, item.TargetTenantId);
        Assert.Null(item.TargetAccountId);
        Assert.Equal(expected, item.MaskedIpAddress);
        using var json = JsonDocument.Parse(item.Changes);
        var change = Assert.Single(json.RootElement.EnumerateObject());
        Assert.Equal("status", change.Name);
        Assert.Equal("Active", change.Value.GetProperty("before").GetString());
        Assert.Equal("Suspended", change.Value.GetProperty("after").GetString());
        Assert.DoesNotContain("Revisión interna", item.Changes);
    }
}
