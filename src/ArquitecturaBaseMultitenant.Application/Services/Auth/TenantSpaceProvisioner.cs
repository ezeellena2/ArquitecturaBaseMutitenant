using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Settings;
using ArquitecturaBaseMultitenant.Domain.Tenancy;

namespace ArquitecturaBaseMultitenant.Application.Services.Auth;

/// <summary>Prepara un espacio personal o una organización real con sus roles iniciales.</summary>
public sealed class TenantSpaceProvisioner(
    ITenantRepository tenants,
    IMemberRepository members,
    IRoleRepository roles,
    IRoleAssignmentRepository assignments,
    ITenantSettingsRepository settings,
    TimeProvider timeProvider)
{
    public void Stage(Tenant tenant, TenantSettings tenantSettings, IReadOnlyCollection<Guid> userIds,
        Guid? businessOwnerUserId = null)
    {
        ArgumentNullException.ThrowIfNull(tenant);
        ArgumentNullException.ThrowIfNull(tenantSettings);
        ArgumentNullException.ThrowIfNull(userIds);
        if (userIds.Count == 0)
            throw new ArgumentException("A new space needs an initial member.", nameof(userIds));
        if (tenant.Kind != TenantKind.Business && businessOwnerUserId is not null)
            throw new ArgumentException("Only a business space can specify an owner.", nameof(businessOwnerUserId));
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
            var assignment = RoleAssignment.Assign(owner.Id, tenantAdmin, companyId: null);
            if (assignment.IsFailure)
                throw new InvalidOperationException("The initial organization owner could not be assigned.");
            assignments.Add(assignment.Value);
        }

        settings.Add(tenantSettings);
    }
}
