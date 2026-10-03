using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Resources;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Users;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Services.Platform;

internal sealed class PlatformScopeAccessService(ICurrentUser currentUser, IPlatformPermissionService permissions,
    ISecurityEventRepository events, IUnitOfWork unitOfWork, TimeProvider clock,
    IRequestInfo requestInfo, ILogger<PlatformScopeAccessService> logger) : IPlatformScopeAccessService
{
    public Task<Result<Guid>> AuthorizeAsync(Guid tenantId, string reason, CancellationToken cancellationToken) =>
        OperationLog.RunAsync<Guid>(logger, clock, "AuthorizePlatformTenantScope", async () =>
        {
            if (currentUser.Access != Access.Platform || currentUser.UserId is not { } actorId
                || !await permissions.HasPermissionAsync(actorId, PlatformPermissions.Tenants.Manage, cancellationToken))
                return AccessErrors.Wrong;
            if (tenantId == Guid.Empty) return TenantErrors.NotFound;
            if (string.IsNullOrWhiteSpace(reason)) return FieldErrors.Validation("reason", ValidationTexts.Required);
            if (reason.Length > TextLimits.Description) return FieldErrors.Validation("reason", ValidationTexts.MaxLength);
            return await unitOfWork.ExecuteInTransactionAsync(_ =>
            {
                events.Add(SecurityEvent.ForPlatformTenantAccess(actorId, tenantId, reason,
                    clock.GetUtcNow().UtcDateTime).WithEvidence(new SecurityEventChanges(), requestInfo.IpAddress));
                return Task.FromResult<Result<Guid>>(actorId);
            }, CommitPolicy.OnSuccess, cancellationToken);
        });
}
