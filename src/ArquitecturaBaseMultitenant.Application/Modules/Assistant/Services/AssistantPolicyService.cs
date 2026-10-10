using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Models.Common;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Services;
using ArquitecturaBaseMultitenant.Domain.Results;
using Microsoft.Extensions.Logging;
using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Services.Platform;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Tenancy;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;

internal sealed class AssistantPolicyService(IAssistantSettingsReader reader, IAssistantSettingsRepository repository,
    ITenantReader tenants, PlatformActionGuard guard, AssistantSettingsAuditWriter audit, IUnitOfWork unitOfWork,
    IRequestValidator validator, TimeProvider clock, ILogger<AssistantPolicyService> logger) : IAssistantPolicyService
{
    public Task<Result<AssistantPolicyResponse>> GetAsync(Guid tenantId, CancellationToken cancellationToken) =>
        OperationLog.RunAsync<AssistantPolicyResponse>(logger, clock, "AssistantPolicyGet", async () =>
        {
            var tenant = await tenants.FindByIdAsync(tenantId, cancellationToken);
            if (tenant is not { Kind: TenantKind.Business }) return TenantErrors.NotFound;
            return await reader.FindPolicyAsync(tenantId, cancellationToken);
        });

    public Task<Result<VersionResponse>> UpdateAsync(Guid tenantId, UpdateAssistantPolicyRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync<VersionResponse>(logger, clock, "AssistantPolicyUpdate", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            var entered = await guard.EnterAsync(tenantId, request.Reason!, cancellationToken);
            if (entered.IsFailure) return entered.Error;
            using var lease = entered.Value;
            var result = await unitOfWork.ExecuteInTransactionAsync<Result<AssistantOrganizationPolicy>>(async ct =>
            {
                var current = await tenants.FindByIdAsync(tenantId, ct);
                if (current is not { Kind: TenantKind.Business }) return TenantErrors.NotFound;
                var row = await repository.GetPolicyAsync(tenantId, request.Version!.Value, ct);
                var created = row is null;
                var platform = await reader.FindPlatformAsync(ct);
                row ??= AssistantOrganizationPolicy.Create(tenantId, platform.MonthlyOrganizationBudget);
                var changes = new SecurityEventChanges();
                changes.AddBoolean("assistant.allowOwnKey", row.AllowOwnKey, request.AllowOwnKey);
                row.SetAllowOwnKey(request.AllowOwnKey!.Value);
                if (created) repository.Add(row); else repository.MarkUpdated(row);
                audit.RecordPolicy(tenantId, request.Reason!, changes);
                return row;
            }, CommitPolicy.OnSuccess, cancellationToken);
            return VersionResponse.Of(result);
        });
}
