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
    /// <summary>
    /// ADR 0068 §8: la propuesta del mensaje sale de una sola consulta (<c>AssistantMessageProposalQuery</c>), así el historial
    /// y el mensaje del SSE traen lo mismo con el mismo filtro de la organización.
    /// </summary>
    [Fact]
    public void The_proposal_of_a_message_is_projected_in_one_place()
    {
        var infrastructure = Path.Combine(SolutionRoot.FullPath, "src", "ArquitecturaBaseMultitenant.Infrastructure");
        var compiled = new[] { $"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", $"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}" };
        var projecting = Directory.EnumerateFiles(infrastructure, "*.cs", SearchOption.AllDirectories)
            .Where(path => !compiled.Any(folder => path.Contains(folder, StringComparison.Ordinal)))
            .Where(path => File.ReadAllText(path).Contains("AssistantMessageProposalResponse.From(", StringComparison.Ordinal))
            .Select(path => Path.GetFileName(path))
            .ToArray();

        Assert.Equal(["AssistantMessageProposalQuery.cs"], projecting);
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
