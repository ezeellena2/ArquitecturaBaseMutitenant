using ArquitecturaBaseMultitenant.Application.Models.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;

namespace ArquitecturaBaseMultitenant.Application.Services.Auth;

/// <summary>Selecciona una organización solo desde las membresías de la cuenta.</summary>
internal static class AccessSwitchPolicy
{
    public static Result<Guid> SelectBusinessTenant(IReadOnlyList<UserTenantAccessRow> memberships,
        Guid? requestedTenantId, Guid? lastBusinessTenantId)
    {
        ArgumentNullException.ThrowIfNull(memberships);

        var businesses = memberships.Where(member => member.Kind == TenantKind.Business).ToArray();

        if (requestedTenantId is not null)
        {
            var requested = businesses.FirstOrDefault(member => member.TenantId == requestedTenantId);
            return requested?.MemberStatus switch
            {
                MemberStatus.Active => RequireActiveTenant(requested),
                MemberStatus.Inactive => MemberErrors.Inactive,
                _ => AccessErrors.NotMember,
            };
        }

        var active = businesses.Where(member => member.MemberStatus == MemberStatus.Active &&
            member.TenantStatus == TenantStatus.Active).ToArray();
        if (active.Length == 0)
        {
            if (businesses.Length == 1 && businesses[0].MemberStatus == MemberStatus.Active)
            {
                return RequireActiveTenant(businesses[0]);
            }

            return businesses.Length == 1 && businesses[0].MemberStatus == MemberStatus.Inactive
                ? MemberErrors.Inactive
                : AccessErrors.NotMember;
        }

        if (active.Length == 1)
        {
            return active[0].TenantId;
        }

        if (lastBusinessTenantId is { } lastId && active.FirstOrDefault(member => member.TenantId == lastId) is { } last)
        {
            return last.TenantId;
        }

        return active.OrderBy(member => member.JoinedAtUtc ?? DateTime.MaxValue)
            .ThenBy(member => member.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(member => member.TenantId)
            .First().TenantId;
    }

    private static Result<Guid> RequireActiveTenant(UserTenantAccessRow member) => member.TenantStatus switch
    {
        TenantStatus.Active => member.TenantId,
        TenantStatus.PendingApproval => TenantErrors.PendingApproval,
        TenantStatus.Provisioning => TenantErrors.Provisioning,
        TenantStatus.Suspended => TenantErrors.Suspended,
        TenantStatus.Closed => TenantErrors.Closed,
        _ => TenantErrors.InvalidTransition,
    };
}
