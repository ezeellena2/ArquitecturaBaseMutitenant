using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Common.Exceptions;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Common.Text;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Caching;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Common;
using ArquitecturaBaseMultitenant.Application.Models.Platform;
using ArquitecturaBaseMultitenant.Application.Resources;
using ArquitecturaBaseMultitenant.Application.Services.Auth;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Users;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Services.Platform;

internal sealed class TenantAdministrationService(IPlatformReader reader, PlatformTenantStateWriter writer,
    ITenantStatusCache cache, PlatformActionGuard guard, PlatformTenantCreator creator,
    PlatformTenantActivator activator, IPersonalSpaceProvisioner defaults, IRequestValidator validator,
    ISecurityEventReader history, IUnitOfWork unitOfWork, TimeProvider clock, ILogger<TenantAdministrationService> logger)
    : ITenantAdministrationService
{
    public Task<Result<CreatedPlatformTenantResponse>> CreateAsync(Guid operationId, CreatePlatformTenantRequest request, CancellationToken ct) =>
        OperationLog.RunAsync<CreatedPlatformTenantResponse>(logger, clock, "CreatePlatformTenant", async () =>
        {
            ArgumentNullException.ThrowIfNull(request);
            if (await validator.ValidateAsync(request, ct) is { } invalid) return invalid;
            if (operationId == Guid.Empty) return TenantErrors.ProvisioningConflict;
            var settings = (await defaults.PrepareAsync(request.Culture, request.TimeZoneId, ct)).Settings;
            var reason = AuditTexts.OrganizationCreationReason();
            var authorized = await guard.EnterAsync(operationId, reason, ct);
            if (authorized.IsFailure) return authorized.Error;
            using var lease = authorized.Value;
            try
            {
                var result = await unitOfWork.ExecuteInTransactionAsync(tx =>
                    creator.CreateAsync(operationId, lease.OperatorId, request, settings, reason, tx), CommitPolicy.OnSuccess, ct);
                if (result.IsSuccess) await cache.InvalidateAsync(operationId, ct);
                return result;
            }
            catch (UniqueConstraintViolationException exception) when (exception.ConstraintName == UniqueKeys.TenantSlug)
            {
                return TenantSlugErrors.Taken;
            }
        });

    public Task<Result<VersionResponse>> ProvisionAsync(Guid tenantId, bool retry, PlatformTenantStateRequest request, CancellationToken ct) =>
        OperationLog.RunAsync<VersionResponse>(logger, clock, "ProvisionPlatformTenant", async () =>
        {
            ArgumentNullException.ThrowIfNull(request);
            request = retry ? request with { Reason = AuditTexts.OrganizationRetryReason() } : request;
            if (await validator.ValidateAsync(request, ct) is { } invalid) return invalid;
            var settings = (await defaults.PrepareAsync(null, null, ct)).Settings;
            var authorized = await guard.EnterAsync(tenantId, request.Reason!, ct);
            if (authorized.IsFailure) return authorized.Error;
            using var lease = authorized.Value;
            var result = await unitOfWork.ExecuteInTransactionAsync(tx =>
                activator.ActivateAsync(tenantId, retry, request, settings, clock.GetUtcNow().UtcDateTime, tx), CommitPolicy.OnSuccess, ct);
            if (result.IsSuccess) await cache.InvalidateAsync(tenantId, ct);
            return VersionResponse.Of(result);
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

    public Task<Result<PlatformTenantDetailResponse>> FindAsync(Guid id, CancellationToken ct) =>
        OperationLog.RunAsync<PlatformTenantDetailResponse>(logger, clock, "FindPlatformTenant", async () =>
        {
            var row = await reader.FindTenantDetailAsync(id, ct);
            if (row is null) return TenantErrors.NotFound;
            var tenant = row.Tenant;
            var state = row.LastStateChange;
            var owners = row.Owners.Select(owner => owner.IdentityStatus == UserStatus.Deleted
                ? owner with { DisplayName = ErrorTexts.Get("Legal.AccountDeletion.DeletedDisplayName"), Email = null }
                : owner).ToArray();
            var contact = owners.FirstOrDefault();
            return new PlatformTenantDetailResponse(tenant.Id, tenant.Name, tenant.Slug, tenant.Status,
                tenant.Source, tenant.CreatedAtUtc, tenant.CompanyCount, tenant.MemberCount, tenant.Version,
                contact?.DisplayName, contact is null ? row.PendingOwnerEmail : contact.Email, owners,
                state is null ? null : new PlatformTenantStateChangeResponse(state.AtUtc,
                    AuditTexts.SecurityEvent(state.Type), state.Reason));
        });

    public Task<Result<CursorResult<PlatformTenantHistoryResponse>>> HistoryAsync(Guid id,
        ListPlatformTenantHistoryRequest request, CancellationToken ct) =>
        OperationLog.RunAsync<CursorResult<PlatformTenantHistoryResponse>>(logger, clock, "ListPlatformTenantHistory", async () =>
        {
            ArgumentNullException.ThrowIfNull(request);
            if (await validator.ValidateAsync(request, ct) is { } invalid) return invalid;
            if (await reader.FindTenantAsync(id, ct) is null) return TenantErrors.NotFound;
            var page = await history.ListAsync(new ListPlatformAuditRequest
                { TenantId = id, Period = "all", After = request.After, Limit = request.Limit }, null, null, false, ct);
            return new CursorResult<PlatformTenantHistoryResponse>([.. page.Items.Select(row =>
                new PlatformTenantHistoryResponse(row.Id, row.OccurredAtUtc, AuditTexts.SecurityEvent(row.EventType!.Value),
                    row.ActorKind == AuditActorKind.System ? AuditTexts.Actor(AuditActorKind.System) : ActorName(row.Actor),
                    AuditTexts.ActorRole(row.ActorKind), row.Reason, row.Changes))], page.NextCursor, page.HasMore);
        });

    private static string ActorName(PlatformAuditAccount? account) => account switch
    {
        null => AuditTexts.UnknownAccount(),
        { IsDeleted: true } => ErrorTexts.Get("Legal.AccountDeletion.DeletedDisplayName"),
        { DisplayName: { Length: > 0 } name } => name,
        { PrimaryEmail: { } email } => PersonalDataMask.Email(email.Value),
        _ => AuditTexts.UnknownAccount(),
    };

    public Task<Result<VersionResponse>> ChangeStateAsync(Guid id, PlatformTenantAction action, PlatformTenantStateRequest request, CancellationToken ct) =>
        OperationLog.RunAsync<VersionResponse>(logger, clock, "ChangePlatformTenantState", async () =>
        {
            ArgumentNullException.ThrowIfNull(request);
            if (await validator.ValidateAsync(request, ct) is { } invalid) return invalid;
            var result = await unitOfWork.ExecuteInTransactionAsync(tx => writer.ChangeAsync(id, action, request, tx), CommitPolicy.OnSuccess, ct);
            if (result.IsSuccess) await cache.InvalidateAsync(id, ct);
            return VersionResponse.Of(result);
        });
}
