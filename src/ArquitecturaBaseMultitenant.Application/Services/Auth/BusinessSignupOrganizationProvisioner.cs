using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Auth;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Settings;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.Services.Auth;

/// <summary>Guarda o reproduce la organización y su primera empresa mediante TenantProvisioner.</summary>
internal sealed class BusinessSignupOrganizationProvisioner(
    ITenantRepository tenants,
    ICompanyRepository companies,
    ITenantSettingsRepository tenantSettings,
    IUserRepository users,
    TenantProvisioner provisioner)
{
    internal Task<Tenant?> FindTenantAsync(Guid tenantId, CancellationToken cancellationToken) =>
        tenants.GetByIdAsync(tenantId, cancellationToken);

    internal async Task<Result<BusinessSignupResponse>> ReplayAsync(Tenant existing, Guid userId,
        BusinessSignupRequest request, TenantSlug slug, Guid firstCompanyId, TaxId? requestedTaxId,
        Email? notificationEmail, CancellationToken cancellationToken)
    {
        if (existing.Kind != TenantKind.Business || existing.Name != request.OrganizationName!.Trim()
            || existing.Slug != slug.Value)
            return TenantErrors.ProvisioningConflict;

        var company = await companies.GetByIdAsync(firstCompanyId, 0, cancellationToken);
        if (company is null || company.Name != request.FirstCompanyName!.Trim()
            || !Equals(company.TaxId, requestedTaxId))
            return TenantErrors.ProvisioningConflict;
        var storedSettings = await tenantSettings.GetCurrentAsync(cancellationToken);
        if (storedSettings is null) return TenantErrors.ProvisioningConflict;

        var requiresApproval = existing.Status == TenantStatus.PendingApproval;
        var provisioned = await provisioner.StageAsync(existing, storedSettings, [userId], userId,
            cancellationToken, firstCompanyId);
        if (provisioned.IsFailure) return provisioned.Error;
        await users.RememberBusinessTenantAsync(userId, existing.Id, cancellationToken);
        return Response(existing, company.Id, requiresApproval, notificationEmail);
    }

    internal async Task<Result> CreateAsync(Tenant tenant, Company company, TenantSettings settings,
        Guid userId, CancellationToken cancellationToken)
    {
        companies.Add(company);
        var provisioned = await provisioner.StageAsync(tenant, settings, [userId], userId,
            cancellationToken, company.Id);
        if (provisioned.IsFailure) return provisioned;
        await users.RememberBusinessTenantAsync(userId, tenant.Id, cancellationToken);
        return Result.Success();
    }

    private static BusinessSignupResponse Response(Tenant tenant, Guid firstCompanyId, bool requiresApproval,
        Email? notificationEmail) =>
        new(tenant.Id, tenant.Name, tenant.Slug!, requiresApproval, firstCompanyId, tenant.Status,
            notificationEmail?.Value);
}
