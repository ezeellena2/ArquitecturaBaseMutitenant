using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Settings;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;

namespace ArquitecturaBaseMultitenant.Application.Services.Auth;

/// <summary>Prepara un espacio personal o una organización real con sus roles iniciales.</summary>
public sealed class TenantSpaceProvisioner(
    ITenantRepository tenants,
    IMemberRepository members,
    IRoleRepository roles,
    IRoleAssignmentRepository assignments,
    ICompanyMembershipRepository companyMemberships,
    ITenantSettingsRepository settings,
    TimeProvider timeProvider)
{
    internal async Task<Result> StageBusinessOwnerAsync(Tenant tenant, TenantSettings requestedSettings, Member owner,
        bool addTenant, Guid? companyId, CancellationToken ct)
    {
        var currentSettings = await settings.GetCurrentAsync(ct);
        if (currentSettings is not null && (currentSettings.DefaultCulture != requestedSettings.DefaultCulture
            || currentSettings.DefaultTimeZoneId != requestedSettings.DefaultTimeZoneId
            || currentSettings.DefaultCurrency != requestedSettings.DefaultCurrency))
            return TenantErrors.ProvisioningConflict;
        var defaults = new List<Role>();
        var missing = new List<Role>();
        foreach (var template in SystemRoles.CreateDefaults())
        {
            var existing = await roles.GetByNameAsync(template.Name, ct);
            if (existing is not null && (!existing.IsSystemRole || existing.Scope != template.Scope
                || !existing.Permissions.Order().SequenceEqual(template.Permissions.Order())))
                return TenantErrors.ProvisioningConflict;
            defaults.Add(existing ?? template);
            if (existing is null) missing.Add(template);
        }
        var ownerAssignments = await assignments.ListForMemberAsync(owner.Id, ct);
        var membership = companyId is { } id ? await companyMemberships.FindAsync(id, owner.Id, ct) : null;
        if (membership is not null && membership.Status != CompanyMembershipStatus.Active)
            return TenantErrors.ProvisioningConflict;

        if (addTenant) tenants.Add(tenant);
        foreach (var role in missing) roles.Add(role);
        var tenantAdmin = defaults.Single(r => r.Name == SystemRoles.TenantAdmin);
        if (!ownerAssignments.Any(a => a.RoleId == tenantAdmin.Id && a.CompanyId == null))
            assignments.Add(RoleAssignment.Assign(owner.Id, tenantAdmin.Id, tenantAdmin.Scope, tenantAdmin.CompanyId,
                null).Value);
        if (companyId is { } initialCompany)
        {
            if (membership is null) companyMemberships.Add(CompanyMembership.Create(initialCompany, owner.Id).Value);
            var companyAdmin = defaults.Single(r => r.Name == SystemRoles.CompanyAdmin);
            if (!ownerAssignments.Any(a => a.RoleId == companyAdmin.Id && a.CompanyId == initialCompany))
                assignments.Add(RoleAssignment.Assign(owner.Id, companyAdmin.Id, companyAdmin.Scope,
                    companyAdmin.CompanyId, initialCompany).Value);
        }
        if (currentSettings is null) settings.Add(requestedSettings);
        return Result.Success();
    }

    public void Stage(Tenant tenant, TenantSettings tenantSettings, IReadOnlyCollection<Guid> userIds,
        Guid? businessOwnerUserId = null, Guid? primaryCompanyId = null)
    {
        ArgumentNullException.ThrowIfNull(tenant);
        ArgumentNullException.ThrowIfNull(tenantSettings);
        ArgumentNullException.ThrowIfNull(userIds);
        if (userIds.Count == 0)
            throw new ArgumentException("A new space needs an initial member.", nameof(userIds));
        if (tenant.Kind != TenantKind.Business && businessOwnerUserId is not null)
            throw new ArgumentException("Only a business space can specify an owner.", nameof(businessOwnerUserId));
        if (tenant.Kind != TenantKind.Business && primaryCompanyId is not null)
            throw new ArgumentException("Only a business space can have an initial company.", nameof(primaryCompanyId));
        if (primaryCompanyId == Guid.Empty)
            throw new ArgumentException("The initial company ID is required.", nameof(primaryCompanyId));
        if (businessOwnerUserId is { } requestedOwner && !userIds.Contains(requestedOwner))
            throw new ArgumentException("The owner must be one of the initial members.", nameof(businessOwnerUserId));
        if (tenant.Kind == TenantKind.Business && userIds.Count > 1 && businessOwnerUserId is null)
            throw new ArgumentException("A business space with multiple initial members needs an explicit owner.",
                nameof(businessOwnerUserId));

        tenants.Add(tenant);
        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        var tenantMembers = new Dictionary<Guid, Member>();
        foreach (var userId in userIds)
        {
            var member = Member.Invite(userId);
            if (member.Activate(nowUtc).IsFailure)
                throw new InvalidOperationException("The initial member could not be activated.");
            members.Add(member);
            tenantMembers.Add(userId, member);
        }

        if (tenant.Kind == TenantKind.Business)
        {
            var systemRoles = SystemRoles.CreateDefaults();
            foreach (var role in systemRoles)
                roles.Add(role);

            var owner = tenantMembers[businessOwnerUserId ?? userIds.First()];
            var tenantAdmin = systemRoles.Single(role => role.Name == SystemRoles.TenantAdmin);
            var assignment = RoleAssignment.Assign(owner.Id, tenantAdmin.Id, tenantAdmin.Scope, tenantAdmin.CompanyId,
                companyId: null);
            if (assignment.IsFailure)
                throw new InvalidOperationException("The initial organization owner could not be assigned.");
            assignments.Add(assignment.Value);

            if (primaryCompanyId is { } initialCompanyId)
            {
                var companyAdmin = systemRoles.Single(role => role.Name == SystemRoles.CompanyAdmin);
                var companyMembership = CompanyMembership.Create(initialCompanyId, owner.Id);
                if (companyMembership.IsFailure)
                    throw new InvalidOperationException("The initial company membership could not be created.");
                var companyAssignment = RoleAssignment.Assign(owner.Id, companyAdmin.Id, companyAdmin.Scope,
                    companyAdmin.CompanyId, initialCompanyId);
                if (companyAssignment.IsFailure)
                    throw new InvalidOperationException("The initial company administrator could not be assigned.");
                companyMemberships.Add(companyMembership.Value);
                assignments.Add(companyAssignment.Value);
            }
        }

        settings.Add(tenantSettings);
    }
}
