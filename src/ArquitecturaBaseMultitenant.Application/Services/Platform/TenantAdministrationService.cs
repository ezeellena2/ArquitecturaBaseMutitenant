using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Caching;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Platform;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Services.Platform;

internal sealed class TenantAdministrationService(IPlatformReader reader, IRequestValidator validator,
    IUnitOfWork unitOfWork, PlatformTenantStateWriter writer, ITenantStatusCache cache,
    TimeProvider clock, ILogger<TenantAdministrationService> logger) : ITenantAdministrationService
{
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
