using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Invitations;
using ArquitecturaBaseMultitenant.Application.Models.Platform;
using ArquitecturaBaseMultitenant.Application.Services.Auth;
using ArquitecturaBaseMultitenant.Application.Services.Invitations;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Invitations;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Settings;
using ArquitecturaBaseMultitenant.Domain.Tenancy;

namespace ArquitecturaBaseMultitenant.Application.Services.Platform;

internal sealed class PlatformTenantCreator(ITenantRepository tenants, ICompanyRepository companies,
    InvitationIssuer invitations, TenantProvisioner provisioner, PlatformTenantAudit audit,
    PlatformTenantSlugAllocator slugs, TimeProvider clock)
{
    internal async Task<Result<CreatedPlatformTenantResponse>> CreateAsync(Guid id, Guid operatorId,
        CreatePlatformTenantRequest request, TenantSettings settings, string reason, CancellationToken ct)
    {
        if (await tenants.GetByIdAsync(id, ct) is not null) return TenantErrors.ProvisioningConflict;
        var allocated = await slugs.AllocateAsync(request.Name!.Trim(), id, ct);
        if (allocated.IsFailure) return allocated.Error;
        var tenant = Tenant.CreateBusiness(id, request.Name!.Trim(), false);
        var registration = tenant.RecordRegistration(operatorId, TenantRegistrationSource.Platform, clock.GetUtcNow().UtcDateTime);
        if (registration.IsFailure) return registration.Error;
        var slug = tenant.SetSlug(TenantSlug.Create(allocated.Value).Value);
        if (slug.IsFailure) return slug.Error;
        var activated = tenant.Activate();
        if (activated.IsFailure) return activated.Error;
        var company = Company.Create(request.FirstCompanyName, null, settings.DefaultTimeZoneId);
        if (company.IsFailure) return company.Error;
        tenants.Add(tenant);
        companies.Add(company.Value);
        var issued = await invitations.IssueWithMemberAsync(new IssueInvitationRequest(request.OwnerEmail,
            operatorId, InvitationChannel.Email), ct, settings);
        if (issued.IsFailure) return issued.Error;
        var prepared = await provisioner.StageBusinessOwnerAsync(tenant, settings, issued.Value.Member, ct, company.Value.Id);
        if (prepared.IsFailure) return prepared.Error;
        audit.Record(id, SecurityEventType.OrganizationCreated, reason);
        return new CreatedPlatformTenantResponse(id, tenant.Name, tenant.Slug!, company.Value.Id, issued.Value.InvitationId);
    }
}
