using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant.Persistence;

/// <summary>Canonical lock names; existing keys are preserved for running jobs.</summary>
internal static class AssistantLockKeys
{
    internal const string PlatformSettings = "global:assistant-settings";
    internal static string OrganizationSettings(Guid tenantId) => AdvisoryLockKeys.For(tenantId, "assistant-settings", "singleton");
    internal static string Policy(Guid tenantId) => AdvisoryLockKeys.For(tenantId, "assistant-policy", "singleton");
    internal static string AccountFinance(Guid owner) => AdvisoryLockKeys.Account(owner) + ":assistant:finance";
    internal static string Budget(Guid tenantId) => AdvisoryLockKeys.For(tenantId, "assistant:finance:budget", tenantId);
    internal static string FinancialOperation(Guid operationId) => "global:assistant:finance:operation:" + operationId.ToString("N");
    internal static string BudgetAlert(Guid monthId) => "global:assistant:budget-alert:" + monthId.ToString("N");
    internal static string Resource(Guid owner, Access access, Guid? tenantId, string resource, Guid id) => access == Access.Platform
        ? AdvisoryLockKeys.Account(owner) + ":assistant:" + resource + ":" + id.ToString("N")
        : AdvisoryLockKeys.For(tenantId ?? throw new InvalidOperationException("An organization scope is required."), "assistant:" + resource + ":" + owner.ToString("N") + ":" + access, id);
}
