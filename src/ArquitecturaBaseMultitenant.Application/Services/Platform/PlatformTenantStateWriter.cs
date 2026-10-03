using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Platform;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;

namespace ArquitecturaBaseMultitenant.Application.Services.Platform;

/// <summary>Prepara una transición global dentro de la UoW del servicio.</summary>
internal sealed class PlatformTenantStateWriter(IPlatformTenantRepository tenants, PlatformTenantAudit audit,
    PlatformTenantNotifier notices, ITokenRevoker tokens)
{
    internal async Task<Result> ChangeAsync(Guid id, PlatformTenantAction action, PlatformTenantStateRequest request, CancellationToken ct)
    {
        var tenant = await tenants.GetForUpdateAsync(id, request.Version!.Value, ct);
        if (tenant is null) return TenantErrors.NotFound;
        var before = tenant.Status;
        var result = action switch
        {
            PlatformTenantAction.Reject when tenant.Status == TenantStatus.PendingApproval => tenant.Close(),
            PlatformTenantAction.Suspend => tenant.Suspend(),
            PlatformTenantAction.Reactivate when tenant.Status == TenantStatus.Suspended => tenant.Activate(),
            PlatformTenantAction.Close => tenant.Close(),
            _ => TenantErrors.InvalidTransition,
        };
        if (result.IsFailure) return result;
        tenants.MarkUpdated(tenant);
        var type = action switch
        {
            PlatformTenantAction.Reject => SecurityEventType.OrganizationRejected,
            PlatformTenantAction.Suspend => SecurityEventType.OrganizationSuspended,
            PlatformTenantAction.Reactivate => SecurityEventType.OrganizationReactivated,
            PlatformTenantAction.Close => SecurityEventType.OrganizationClosed,
            _ => throw new ArgumentOutOfRangeException(nameof(action)),
        };
        var changes = new SecurityEventChanges();
        changes.AddEnum("status", before, tenant.Status);
        audit.Record(id, type, request.Reason!, changes);
        if (action is not PlatformTenantAction.Reactivate) await tokens.RevokeTenantAsync(id, ct);
        if (action == PlatformTenantAction.Reject) await notices.EnqueueRejectedAsync(tenant, ct);
        return Result.Success();
    }
}
