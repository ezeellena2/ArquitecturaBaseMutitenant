using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;

/// <summary>Resolves the signed owner and tenant binding used by turn writes.</summary>
internal sealed class AssistantExecutionIdentityResolver(ICurrentUser user, ITenantContext tenant)
{
    internal Guid Owner => user.UserId ?? throw new InvalidOperationException("An execution actor is required.");
    internal Access Access => user.Access ?? throw new InvalidOperationException("An execution access is required.");
    internal Guid? TenantId => tenant.TenantId;
    internal AssistantFinancialBinding Binding => new(Owner, Access, TenantId);
}
