namespace ArquitecturaBaseMultitenant.ArchitectureTests;

public sealed partial class TenantScopeUsageTests
{
    static partial void ContributeTenantScopeCallers(HashSet<string> declared)
    {
        declared.Add("ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant.AssistantTurnWorker");
        declared.Add("ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant.Retention.AssistantSnapshotRetentionWorker");
        declared.Add("ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant.Retention.AssistantRetentionWorker");
    }
}
