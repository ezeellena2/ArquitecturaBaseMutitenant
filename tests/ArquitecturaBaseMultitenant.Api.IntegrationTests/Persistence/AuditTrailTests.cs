using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.Auditing;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Interceptors;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Time.Testing;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Persistence;

public sealed class AuditTrailTests
{
    private static readonly DateTimeOffset Instant = new(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Added_entity_creates_one_entry_with_actor_time_and_masked_diff()
    {
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        using var context = CreateContext();
        var record = new AuditedRecord(tenantId, "Before", "super-secret", "alice@example.com");
        context.Add(record);

        ApplyAudit(context, tenantId, new TestCurrentUser(actorId, AccessKind.Business));

        var audit = Assert.Single(context.ChangeTracker.Entries<AuditEntry>()).Entity;
        Assert.Equal(tenantId, audit.TenantId);
        Assert.Equal(AuditActorKind.User, audit.ActorKind);
        Assert.Equal(actorId, audit.ActorId);
        Assert.Equal(AuditAction.Created, audit.Action);
        Assert.Equal(record.Id, audit.EntityId);
        Assert.Equal(Instant.UtcDateTime, audit.OccurredAtUtc);
        Assert.DoesNotContain("Secret", audit.Changes, StringComparison.Ordinal);
        Assert.DoesNotContain("super-secret", audit.Changes, StringComparison.Ordinal);
        Assert.DoesNotContain("alice@example.com", audit.Changes, StringComparison.Ordinal);
        using var json = JsonDocument.Parse(audit.Changes);
        Assert.Equal("Before", json.RootElement.GetProperty("Name").GetProperty("new").GetString());
    }

    [Fact]
    public void Modified_entity_records_old_and_new_values_but_not_not_audited_properties()
    {
        var tenantId = Guid.NewGuid();
        using var context = CreateContext();
        var record = new AuditedRecord(tenantId, "Before", "old-secret", "alice@example.com");
        context.Attach(record);
        record.Rename("After");
        record.ChangeSecret("new-secret");

        ApplyAudit(context, tenantId, new TestCurrentUser());

        var audit = Assert.Single(context.ChangeTracker.Entries<AuditEntry>()).Entity;
        Assert.Equal(AuditAction.Updated, audit.Action);
        Assert.Equal(AuditActorKind.System, audit.ActorKind);
        using var json = JsonDocument.Parse(audit.Changes);
        var name = json.RootElement.GetProperty("Name");
        Assert.Equal("Before", name.GetProperty("old").GetString());
        Assert.Equal("After", name.GetProperty("new").GetString());
        Assert.False(json.RootElement.TryGetProperty("Secret", out _));
    }

    [Fact]
    public void Soft_delete_is_recorded_as_deleted_in_the_same_change_tracker()
    {
        var tenantId = Guid.NewGuid();
        using var context = CreateContext();
        var record = new AuditedRecord(tenantId, "Widget", "secret", "alice@example.com");
        context.Attach(record);
        context.Remove(record);
        var actor = new TestCurrentUser();
        var clock = new FakeTimeProvider(Instant);
        new SoftDeleteInterceptor(actor, clock).SavingChanges(Event(context), default);

        ApplyAudit(context, tenantId, actor);

        Assert.Equal(EntityState.Modified, context.Entry(record).State);
        var audit = Assert.Single(context.ChangeTracker.Entries<AuditEntry>()).Entity;
        Assert.Equal(AuditAction.Deleted, audit.Action);
        using var diff = JsonDocument.Parse(audit.Changes);
        Assert.False(diff.RootElement.TryGetProperty("Name", out _));
        Assert.True(diff.RootElement.GetProperty("IsDeleted").GetProperty("new").GetBoolean());
    }

    [Fact]
    public void Public_entity_audits_business_and_shared_entity_audits_both_parties()
    {
        var businessTenant = Guid.NewGuid();
        using var publicContext = CreateContext();
        publicContext.Add(new AuditedPublicRecord(businessTenant));
        ApplyAudit(publicContext, businessTenant, new TestCurrentUser());
        Assert.Equal(businessTenant, Assert.Single(publicContext.ChangeTracker.Entries<AuditEntry>()).Entity.TenantId);

        var consumerTenant = Guid.NewGuid();
        using var sharedContext = CreateContext();
        sharedContext.Add(new AuditedSharedRecord(consumerTenant, businessTenant));
        var trail = new AuditTrailInterceptor(new TestCurrentUser(), new TestTenantContext(consumerTenant),
            new FakeTimeProvider(Instant));
        trail.PrepareAuditEntries(sharedContext);
        var auditTenants = sharedContext.ChangeTracker.Entries<AuditEntry>()
            .Select(entry => entry.Entity.TenantId).ToArray();
        Assert.Equal(2, auditTenants.Length);
        Assert.Contains(consumerTenant, auditTenants);
        Assert.Contains(businessTenant, auditTenants);
    }

    [Fact]
    public void Shared_batch_collects_distinct_counterparts_for_one_transaction_setting()
    {
        var consumerTenant = Guid.NewGuid();
        var firstBusiness = Guid.NewGuid();
        var secondBusiness = Guid.NewGuid();
        using var context = CreateContext();
        context.Add(new AuditedSharedRecord(consumerTenant, firstBusiness));
        context.Add(new AuditedSharedRecord(consumerTenant, secondBusiness));
        var trail = new AuditTrailInterceptor(new TestCurrentUser(), new TestTenantContext(consumerTenant),
            new FakeTimeProvider(Instant));

        var counterpartIds = trail.PrepareAuditEntries(context);

        Assert.Equal(2, counterpartIds.Count);
        Assert.Contains(firstBusiness, counterpartIds);
        Assert.Contains(secondBusiness, counterpartIds);
        Assert.Equal(4, context.ChangeTracker.Entries<AuditEntry>().Count());
    }

    [Fact]
    public void Audit_history_cannot_be_updated_or_deleted_through_the_interceptor()
    {
        var tenantId = Guid.NewGuid();
        var audit = AuditEntry.Create(tenantId, AuditActorKind.System, null,
            AuditAction.Custom, "Export", Guid.NewGuid(), "{}", Instant.UtcDateTime);
        using var context = CreateContext();
        context.Attach(audit);
        context.Entry(audit).State = EntityState.Modified;
        var trail = new AuditTrailInterceptor(new TestCurrentUser(), new TestTenantContext(tenantId),
            new FakeTimeProvider(Instant));
        Action save = () => trail.SavingChanges(Event(context), default);
        Assert.Throws<InvalidOperationException>(save);

        context.Entry(audit).State = EntityState.Deleted;
        Assert.Throws<InvalidOperationException>(save);
    }

    [Fact]
    public void Explicit_audit_event_uses_the_active_tenant_and_platform_actor()
    {
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        var tenant = new TestTenantContext(tenantId);
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=explicit_audit_model_test")
            .Options;
        using var context = new ApplicationDbContext(options, tenant);
        var log = new AuditLog(context, tenant, new TestCurrentUser(actorId, AccessKind.Platform),
            new FakeTimeProvider(Instant));

        log.Record(AuditAction.Custom, "Export", subjectId,
            new Dictionary<string, object?> { ["destination"] = "masked" });

        var audit = Assert.Single(context.ChangeTracker.Entries<AuditEntry>()).Entity;
        Assert.Equal(EntityState.Added, context.Entry(audit).State);
        Assert.Equal(tenantId, audit.TenantId);
        Assert.Equal(AuditActorKind.PlatformOperator, audit.ActorKind);
        Assert.Equal(actorId, audit.ActorId);
        Assert.Equal(subjectId, audit.EntityId);
        Assert.Equal(Instant.UtcDateTime, audit.OccurredAtUtc);
        using var json = JsonDocument.Parse(audit.Changes);
        Assert.Equal("masked", json.RootElement.GetProperty("destination").GetString());
    }

    private static void ApplyAudit(AuditTestDbContext context, Guid tenantId, TestCurrentUser actor)
    {
        var clock = new FakeTimeProvider(Instant);
        new AuditableEntityInterceptor(actor, clock).SavingChanges(Event(context), default);
        new AuditTrailInterceptor(actor, new TestTenantContext(tenantId), clock)
            .SavingChanges(Event(context), default);
    }

    private static AuditTestDbContext CreateContext() => new(
        new DbContextOptionsBuilder<AuditTestDbContext>()
            .UseNpgsql("Host=localhost;Database=audit_model_test")
            .Options);

    private static DbContextEventData Event(DbContext context) =>
        new(null!, (_, _) => string.Empty, context);
}
