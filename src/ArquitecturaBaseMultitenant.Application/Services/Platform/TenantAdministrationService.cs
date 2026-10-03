using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Common.Exceptions;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Caching;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Platform;
using ArquitecturaBaseMultitenant.Application.Resources;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Services.Platform;

internal sealed class TenantAdministrationService(IPlatformReader reader, IRequestValidator validator,
    IUnitOfWork unitOfWork, PlatformTenantStateWriter writer, ITenantStatusCache cache,
    TimeProvider clock, ILogger<TenantAdministrationService> logger, PlatformTenantProvisioning provisioning) : ITenantAdministrationService
{
    public Task<Result<CreatedPlatformTenantResponse>> CreateAsync(Guid operationId, CreatePlatformTenantRequest request, CancellationToken ct) =>
        OperationLog.RunAsync<CreatedPlatformTenantResponse>(logger, clock, "CreatePlatformTenant", async () =>
        {
            ArgumentNullException.ThrowIfNull(request);
            if (await validator.ValidateAsync(request, ct) is { } invalid) return invalid;
            if (operationId == Guid.Empty) return TenantErrors.ProvisioningConflict;
            var settings = await provisioning.PrepareAsync(request.Culture, request.TimeZoneId, ct);
            var reason = AuditTexts.OrganizationCreationReason();
            var authorized = await provisioning.EnterAsync(operationId, reason, ct);
            if (authorized.IsFailure) return authorized.Error;
            using var lease = authorized.Value;
            try
            {
                var result = await unitOfWork.ExecuteInTransactionAsync(tx =>
                    provisioning.CreateAsync(operationId, lease.OperatorId, request, settings, reason, tx), CommitPolicy.OnSuccess, ct);
                if (result.IsSuccess) await cache.InvalidateAsync(operationId, ct);
                return result;
            }
            catch (UniqueConstraintViolationException exception) when (exception.ConstraintName == "IX_Tenants_Slug")
            {
                return TenantSlugErrors.Taken;
            }
        });

    public Task<Result> ProvisionAsync(Guid tenantId, bool retry, PlatformTenantStateRequest request, CancellationToken ct) =>
        OperationLog.RunAsync(logger, clock, "ProvisionPlatformTenant", async () =>
        {
            ArgumentNullException.ThrowIfNull(request);
            if (await validator.ValidateAsync(request, ct) is { } invalid) return invalid;
            var settings = await provisioning.PrepareAsync(null, null, ct);
            var authorized = await provisioning.EnterAsync(tenantId, request.Reason!, ct);
            if (authorized.IsFailure) return authorized.Error;
            using var lease = authorized.Value;
            var result = await unitOfWork.ExecuteInTransactionAsync(tx =>
                provisioning.ActivateAsync(tenantId, retry, request, settings, clock.GetUtcNow().UtcDateTime, tx), CommitPolicy.OnSuccess, ct);
            if (result.IsSuccess) await cache.InvalidateAsync(tenantId, ct);
            return result;
        });

    public Task<Result<PagedResult<PlatformTenantResponse>>> ListAsync(ListPlatformTenantsRequest request, CancellationToken ct) =>
        OperationLog.RunAsync<PagedResult<PlatformTenantResponse>>(logger, clock, "ListPlatformTenants", async () =>
        {
            ArgumentNullException.ThrowIfNull(request);
            if (await validator.ValidateAsync(request, ct) is { } invalid) return invalid;
            return await reader.ListTenantsAsync(request, ct);
        });

    public Task<Result<PlatformTenantFilterCounts>> CountAsync(ListPlatformTenantsRequest request, CancellationToken ct) =>
        OperationLog.RunAsync<PlatformTenantFilterCounts>(logger, clock, "CountPlatformTenants", async () =>
        {
            ArgumentNullException.ThrowIfNull(request);
            if (await validator.ValidateAsync(request, ct) is { } invalid) return invalid;
            return await reader.CountTenantsAsync(request, ct);
        });

    public Task<Result<PlatformTenantResponse>> FindAsync(Guid id, CancellationToken ct) =>
        OperationLog.RunAsync<PlatformTenantResponse>(logger, clock, "FindPlatformTenant", async () =>
        {
            var row = await reader.FindTenantAsync(id, ct);
            return row is null ? TenantErrors.NotFound : row;
        });

    public Task<Result> ChangeStateAsync(Guid id, PlatformTenantAction action, PlatformTenantStateRequest request, CancellationToken ct) =>
        OperationLog.RunAsync(logger, clock, "ChangePlatformTenantState", async () =>
        {
            ArgumentNullException.ThrowIfNull(request);
            if (await validator.ValidateAsync(request, ct) is { } invalid) return invalid;
            var result = await unitOfWork.ExecuteInTransactionAsync(tx => writer.ChangeAsync(id, action, request, tx), CommitPolicy.OnSuccess, ct);
            if (result.IsSuccess) await cache.InvalidateAsync(id, ct);
            return result;
        });
}
