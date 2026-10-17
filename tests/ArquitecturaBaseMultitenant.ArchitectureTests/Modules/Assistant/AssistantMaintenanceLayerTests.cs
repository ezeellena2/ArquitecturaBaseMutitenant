using System.Reflection;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Legal;
using ArquitecturaBaseMultitenant.Domain.Assistant;

namespace ArquitecturaBaseMultitenant.ArchitectureTests;

public sealed class AssistantMaintenanceLayerTests
{
    [Fact]
    public void Account_deletion_policy_lives_in_application_and_infrastructure_only_cleans_ef()
    {
        var application = Assembly.Load("ArquitecturaBaseMultitenant.Application");
        var participant = application.GetType("ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services.AssistantAccountDeletionParticipant");
        Assert.NotNull(participant);
        Assert.False(participant.IsPublic);
        Assert.Contains(typeof(IAccountDeletionParticipant), participant.GetInterfaces());
        Assert.Null(Assembly.Load("ArquitecturaBaseMultitenant.Infrastructure").GetType(
            "ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant.AssistantAccountDeletionParticipant"));
    }

    [Fact]
    public void Retention_and_deletion_cleanup_adapters_do_not_decide_domain_state_changes()
    {
        var calls = ArchitectureIl.Calls(Assembly.Load("ArquitecturaBaseMultitenant.Infrastructure"));
        var adapters = new[]
        {
            "ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant.Persistence.Repositories.AssistantSnapshotRetentionRepository",
            "ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant.Persistence.Repositories.AssistantAccountDeletionCleanupRepository",
        };
        var mutationMethods = new[]
        {
            nameof(AssistantDurableOperationBase.RemoveContent), nameof(AssistantFinancialOperation.MarkContentDeleted),
            nameof(AssistantFinancialOperation.Anonymize), nameof(AssistantTurnWork.RequestStop),
        };
        Assert.DoesNotContain(calls, call => adapters.Contains(call.Owner, StringComparer.Ordinal)
            && mutationMethods.Contains(call.Method, StringComparer.Ordinal)
            && call.DeclaringType.StartsWith("ArquitecturaBaseMultitenant.Domain.Assistant.", StringComparison.Ordinal));
    }
}
