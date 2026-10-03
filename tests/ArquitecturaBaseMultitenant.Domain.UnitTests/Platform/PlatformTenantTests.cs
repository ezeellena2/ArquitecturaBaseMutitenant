using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Tenancy;

namespace ArquitecturaBaseMultitenant.Domain.UnitTests.Platform;

public sealed class PlatformTenantTests
{
    [Fact]
    public void Registration_metadata_is_utc_immutable_and_versioned()
    {
        var tenant = Tenant.CreateBusiness("La Cosecha", true);
        var userId = Guid.NewGuid();
        var now = new DateTime(2026, 10, 3, 12, 0, 0, DateTimeKind.Utc);
        Assert.True(tenant.RecordRegistration(userId, TenantRegistrationSource.SelfService, now).IsSuccess);
        Assert.Equal(userId, tenant.RegisteredByUserId);
        Assert.Equal(now, tenant.CreatedAtUtc);
        Assert.Equal(TenantRegistrationSource.SelfService, tenant.RegistrationSource);
        Assert.IsAssignableFrom<IVersioned>(tenant);
        Assert.True(tenant.RecordRegistration(userId, TenantRegistrationSource.SelfService, now).IsSuccess);
        Assert.True(tenant.RecordRegistration(Guid.NewGuid(), TenantRegistrationSource.Platform, now).IsFailure);
        Assert.Equal(userId, tenant.RegisteredByUserId);
    }

    [Fact]
    public void Registration_rejects_invalid_metadata_without_mutation()
    {
        var tenant = Tenant.CreateBusiness("La Cosecha", true);
        Assert.Throws<ArgumentException>(() => tenant.RecordRegistration(Guid.Empty, TenantRegistrationSource.Platform, new DateTime(2026, 10, 3, 0, 0, 0, DateTimeKind.Utc)));
        Assert.Throws<ArgumentException>(() => tenant.RecordRegistration(Guid.NewGuid(), TenantRegistrationSource.Platform, new DateTime(2026, 10, 3)));
        Assert.Null(tenant.CreatedAtUtc);
    }

    [Fact]
    public void Platform_tenant_event_records_operator_target_reason_and_time()
    {
        var actor = Guid.NewGuid();
        var target = Guid.NewGuid();
        var now = new DateTime(2026, 10, 3, 12, 0, 0, DateTimeKind.Utc);
        var ev = SecurityEvent.ForTenantAdministration(SecurityEventType.OrganizationSuspended, actor, target, " Revisar actividad ", now);
        Assert.Equal(AuditActorKind.PlatformOperator, ev.ActorKind);
        Assert.Equal(actor, ev.ActorId);
        Assert.Equal(target, ev.TargetTenantId);
        Assert.Equal("Revisar actividad", ev.Reason);
        Assert.Throws<ArgumentException>(() => SecurityEvent.ForTenantAdministration(SecurityEventType.OrganizationSuspended, actor, Guid.Empty, "Motivo", now));
        Assert.Throws<ArgumentOutOfRangeException>(() => SecurityEvent.ForTenantAdministration(SecurityEventType.LoginMethodChanged, actor, target, "Motivo", now));
    }
}
