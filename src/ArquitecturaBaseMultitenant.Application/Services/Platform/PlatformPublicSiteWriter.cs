using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Platform;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.PublicSite;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;

namespace ArquitecturaBaseMultitenant.Application.Services.Platform;

internal sealed class PlatformPublicSiteWriter(IPublicPageRepository pages, ITenantRepository tenants,
    PlatformAuditWriter audit, IAuditLog auditLog, PlatformTenantNotifier notices, TimeProvider clock)
{
    internal async Task<Result<PublicPage>> ChangeAsync(Guid tenantId, Guid operatorId, PlatformTenantStateRequest request,
        bool blocked, CancellationToken ct)
    {
        var tenant = await tenants.GetByIdAsync(tenantId, ct);
        if (tenant is not { Kind: TenantKind.Business }) return TenantErrors.NotFound;
        var page = await pages.GetCurrentAsync(request.Version!.Value, ct);
        if (page is null) return PublicPageErrors.NotFound;
        var wasPublished = page.IsPublished;
        var wasBlocked = page.PublishBlockedAtUtc is not null;
        var changed = blocked
            ? page.ApplyModerationBlock(request.Reason, operatorId, clock.GetUtcNow().UtcDateTime)
            : page.AllowPublish();
        if (changed.IsFailure) return changed.Error;
        pages.MarkUpdated(page);
        var changes = new SecurityEventChanges();
        changes.AddBoolean("publicPage.isPublished", wasPublished, page.IsPublished);
        changes.AddBoolean("publicPage.publishBlocked", wasBlocked, page.PublishBlockedAtUtc is not null);
        audit.Record(tenantId, blocked ? SecurityEventType.PublicPageUnpublished : SecurityEventType.PublicPagePublicationAllowed,
            request.Reason!, changes);
        auditLog.Record(AuditAction.Updated, "PublicPage", page.Id,
            new Dictionary<string, object?> { ["isPublished"] = page.IsPublished, ["publishBlocked"] = blocked });
        await notices.EnqueuePageModerationAsync(tenant, blocked, ct);
        return page;
    }
}
