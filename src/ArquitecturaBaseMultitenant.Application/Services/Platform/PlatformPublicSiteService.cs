using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Common;
using ArquitecturaBaseMultitenant.Application.Models.Platform;
using ArquitecturaBaseMultitenant.Application.Resources;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Services.Platform;

internal sealed class PlatformPublicSiteService(ITenantReader tenants, IPublicPageReader pages,
    PlatformActionGuard guard, PlatformPublicSiteWriter writer, IUnitOfWork unitOfWork,
    IRequestValidator validator, TimeProvider clock, ILogger<PlatformPublicSiteService> logger)
    : IPlatformPublicSiteService
{
    public Task<Result<PlatformPublicSiteResponse>> GetAsync(Guid tenantId, CancellationToken cancellationToken) =>
        OperationLog.RunAsync<PlatformPublicSiteResponse>(logger, clock, "GetPlatformPublicSite", async () =>
        {
            var tenant = await tenants.FindByIdAsync(tenantId, cancellationToken);
            if (tenant is not { Kind: TenantKind.Business }) return TenantErrors.NotFound;
            var entered = await guard.EnterAsync(tenantId, AuditTexts.PublicPageMetadataReadReason(), cancellationToken);
            if (entered.IsFailure) return entered.Error;
            using var lease = entered.Value;
            return await pages.FindModerationAsync(tenant.Slug, cancellationToken)
                ?? new PlatformPublicSiteResponse(tenant.Slug, tenant.Name, false, null, null, 0);
        });

    public Task<Result<VersionResponse>> UnpublishAsync(Guid tenantId, PlatformTenantStateRequest request, CancellationToken cancellationToken) =>
        ChangeAsync(tenantId, request, true, cancellationToken);

    public Task<Result<VersionResponse>> AllowPublishAsync(Guid tenantId, PlatformTenantStateRequest request, CancellationToken cancellationToken) =>
        ChangeAsync(tenantId, request, false, cancellationToken);

    private Task<Result<VersionResponse>> ChangeAsync(Guid tenantId, PlatformTenantStateRequest request, bool blocked, CancellationToken cancellationToken) =>
        OperationLog.RunAsync<VersionResponse>(logger, clock, blocked ? "UnpublishPlatformPublicSite" : "AllowPlatformPublicSitePublishing", async () =>
        {
            ArgumentNullException.ThrowIfNull(request);
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            var tenant = await tenants.FindByIdAsync(tenantId, cancellationToken);
            if (tenant is not { Kind: TenantKind.Business }) return TenantErrors.NotFound;
            var entered = await guard.EnterAsync(tenantId, request.Reason!, cancellationToken);
            if (entered.IsFailure) return entered.Error;
            using var lease = entered.Value;
            var saved = await unitOfWork.ExecuteInTransactionAsync(ct => writer.ChangeAsync(tenantId,
                lease.OperatorId, request, blocked, ct), CommitPolicy.OnSuccess, cancellationToken);
            return VersionResponse.Of(saved);
        });
}
