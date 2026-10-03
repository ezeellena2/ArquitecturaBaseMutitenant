using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Platform;
using ArquitecturaBaseMultitenant.Application.Services.Auth;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Settings;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.Services.Platform;

internal sealed class PlatformTenantActivator(IPlatformTenantRepository tenants, ITenantProvisioningReader metadata,
    IMemberRepository members, ITenantSettingsRepository settings, TenantProvisioner provisioner,
    PlatformTenantAudit audit, IUserRepository users)
{
    internal async Task<Result> ActivateAsync(Guid id, bool retry, PlatformTenantStateRequest request,
        TenantSettings fallbackSettings, DateTime nowUtc, CancellationToken ct)
    {
        var tenant = await tenants.GetForUpdateAsync(id, request.Version!.Value, ct);
        if (tenant is null) return TenantErrors.NotFound;
        if (!retry)
        {
            var approved = tenant.Approve();
            if (approved.IsFailure) return approved;
        }
        else if (tenant.Status is not (TenantStatus.Provisioning or TenantStatus.Active)) return TenantErrors.InvalidTransition;
        if (tenant.Status == TenantStatus.Provisioning)
        {
            var activated = tenant.Activate();
            if (activated.IsFailure) return activated;
        }
        var snapshot = await metadata.FindAsync(tenant.RegistrationSource == TenantRegistrationSource.SelfService
            ? tenant.RegisteredByUserId : null, ct);
        Member? owner = snapshot.OwnerMemberId is { } memberId ? await members.GetByIdAsync(memberId, ct) : null;
        if (owner is null && tenant.RegistrationSource == TenantRegistrationSource.SelfService
            && tenant.RegisteredByUserId is { } registeringUserId)
        {
            await members.LockUserAsync(registeringUserId, ct);
            var account = await users.GetByIdAsync(registeringUserId, ct);
            if (account?.Status != UserStatus.Active) return TenantErrors.ProvisioningConflict;
            owner = Member.Invite(registeringUserId);
            var active = owner.Activate(nowUtc);
            if (active.IsFailure) return active;
            members.Add(owner);
        }
        if (owner is null) return TenantErrors.ProvisioningConflict;
        var currentSettings = await settings.GetCurrentAsync(ct) ?? fallbackSettings;
        var prepared = await provisioner.StageBusinessOwnerAsync(tenant, currentSettings, owner, ct, snapshot.PrimaryCompanyId);
        if (prepared.IsFailure) return prepared;
        tenants.MarkUpdated(tenant);
        audit.Record(id, retry ? SecurityEventType.OrganizationProvisioningRetried : SecurityEventType.OrganizationApproved, request.Reason!);
        return Result.Success();
    }
}
