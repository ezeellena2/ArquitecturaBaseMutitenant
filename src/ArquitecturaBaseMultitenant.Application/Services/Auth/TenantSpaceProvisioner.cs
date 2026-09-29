using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Settings;
using ArquitecturaBaseMultitenant.Domain.Tenancy;

namespace ArquitecturaBaseMultitenant.Application.Services.Auth;

/// <summary>Prepara las mismas escrituras tenant al registrar Personal o sembrar una empresa de muestra.</summary>
public sealed class TenantSpaceProvisioner(
    ITenantRepository tenants,
    IMemberRepository members,
    ITenantSettingsRepository settings,
    TimeProvider timeProvider)
{
    public void Stage(Tenant tenant, TenantSettings tenantSettings, IReadOnlyCollection<Guid> userIds)
    {
        ArgumentNullException.ThrowIfNull(tenant);
        ArgumentNullException.ThrowIfNull(tenantSettings);
        ArgumentNullException.ThrowIfNull(userIds);
        if (userIds.Count == 0)
            throw new ArgumentException("A new space needs an initial member.", nameof(userIds));

        tenants.Add(tenant);
        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        foreach (var userId in userIds)
        {
            var member = Member.Invite(userId);
            if (member.Activate(nowUtc).IsFailure)
                throw new InvalidOperationException("The initial member could not be activated.");
            members.Add(member);
        }

        settings.Add(tenantSettings);
    }
}
