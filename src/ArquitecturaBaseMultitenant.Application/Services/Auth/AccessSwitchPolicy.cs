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
                MemberStatus.Inactive => WithOrganization(MemberErrors.Inactive, requested),
                _ => AccessErrors.NotMember,
            };
        }

        var active = businesses.Where(member => member.MemberStatus == MemberStatus.Active &&
            member.TenantStatus == TenantStatus.Active).ToArray();
        if (active.Length == 0)
        {
            var unavailable = businesses.Where(member => member.MemberStatus == MemberStatus.Active).ToArray();
            if (unavailable.Length > 0)
                return RequireActiveTenant(Preferred(unavailable, lastBusinessTenantId));

            var inactive = businesses.Where(member => member.MemberStatus == MemberStatus.Inactive).ToArray();
            return inactive.Length > 0
                ? WithOrganization(MemberErrors.Inactive, Preferred(inactive, lastBusinessTenantId))
                : AccessErrors.NotMember;
        }

        return Preferred(active, lastBusinessTenantId).TenantId;
    }

    private static UserTenantAccessRow Preferred(IReadOnlyList<UserTenantAccessRow> candidates,
        Guid? lastBusinessTenantId)
    {
        if (lastBusinessTenantId is { } lastId
            && candidates.FirstOrDefault(member => member.TenantId == lastId) is { } last)
            return last;

        return candidates.OrderBy(member => member.JoinedAtUtc ?? DateTime.MaxValue)
            .ThenBy(member => member.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(member => member.TenantId)
            .First();
    }

    private static Result<Guid> RequireActiveTenant(UserTenantAccessRow member) => member.TenantStatus switch
    {
        TenantStatus.Active => member.TenantId,
        TenantStatus.PendingApproval => WithOrganization(TenantErrors.PendingApproval, member),
        TenantStatus.Provisioning => WithOrganization(TenantErrors.Provisioning, member),
        TenantStatus.Suspended => WithOrganization(TenantErrors.Suspended, member),
        TenantStatus.Closed => WithOrganization(TenantErrors.Closed, member),
        _ => WithOrganization(TenantErrors.InvalidTransition, member),
    };

    private static Error WithOrganization(Error error, UserTenantAccessRow member) =>
        error with
        {
            Metadata = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["organizationName"] = member.Name,
                ["tenantId"] = member.TenantId.ToString("D"),
            },
        };
}
