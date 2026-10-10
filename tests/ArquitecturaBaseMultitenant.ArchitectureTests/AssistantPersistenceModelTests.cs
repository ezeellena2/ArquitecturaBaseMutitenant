using Microsoft.EntityFrameworkCore;
using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using System.Reflection;

namespace ArquitecturaBaseMultitenant.ArchitectureTests;

public sealed class AssistantPersistenceModelTests
{
    [Fact]
    public void A_global_audit_contract_cannot_remove_the_audit_trail_from_a_private_entity()
    {
        var builder = new ModelBuilder();
        builder.Entity<GlobalAuditedPrivateProbe>().ToTable("Probe", "tenant").HasKey(value => value.TenantId);
        builder.Entity<GlobalAuditedPrivateProbe>().HasQueryFilter("Tenant", value => true);
        var validate = typeof(ApplicationDbContext).Assembly
            .GetType("ArquitecturaBaseMultitenant.Infrastructure.Persistence.Rls.TenantIsolationModelValidator", throwOnError: true)!
            .GetMethod("Validate", BindingFlags.Public | BindingFlags.Static)!;
        var invocation = Assert.Throws<TargetInvocationException>(() => validate.Invoke(null, [builder.Model, null]));
        var error = Assert.IsType<InvalidOperationException>(invocation.InnerException);
        Assert.Contains(nameof(GlobalAuditedPrivateProbe), error.Message, StringComparison.Ordinal);
    }
    private sealed class GlobalAuditedPrivateProbe : ITenantOwned, IGlobalSecurityAudited
    {
        public Guid TenantId { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public Guid? CreatedBy { get; set; }
        public DateTime? ModifiedAtUtc { get; set; }
        public Guid? ModifiedBy { get; set; }
        SecurityEventType IGlobalSecurityAudited.SecurityEventType => SecurityEventType.PlatformSettingsChanged;
        Guid? IGlobalSecurityAudited.SecurityEventTargetTenantId => null;
    }
}
