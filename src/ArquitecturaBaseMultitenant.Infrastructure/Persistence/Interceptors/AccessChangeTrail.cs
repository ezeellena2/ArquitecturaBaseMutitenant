using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Invitations;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Quien entra a un lugar deja una sola entrada, como el tablero Auditoria-Org. Invitarlo deja «Invitó a …» con las
/// empresas y los roles adentro: el miembro, las membresías y los roles de esa persona en el mismo guardado no dejan
/// entrada propia. Agregarlo a una empresa deja «Agregó a …» con «Roles en la empresa» adentro. Los demás cambios de
/// roles siguen siendo una entrada por persona y lugar (<see cref="RoleAssignmentTrail"/>).
/// </summary>
internal sealed class AccessChangeTrail
{
    private readonly HashSet<Guid> invitedMembers;
    private readonly ILookup<Guid, Guid> invitedCompanies;
    private readonly HashSet<(Guid MemberId, Guid CompanyId)> addedMemberships;
    private readonly IReadOnlyCollection<RoleAssignmentTrail.RoleSetChange> roleChanges;

    private AccessChangeTrail(IReadOnlyCollection<EntityEntry<IAuditable>> changed,
        IReadOnlyCollection<RoleAssignmentTrail.RoleSetChange> roleChanges)
    {
        this.roleChanges = roleChanges;
        invitedMembers = [.. Added<Invitation>(changed).Select(invitation => invitation.MemberId)];
        var memberships = Added<CompanyMembership>(changed).ToArray();
        invitedCompanies = memberships.Where(membership => invitedMembers.Contains(membership.MemberId))
            .ToLookup(membership => membership.MemberId, membership => membership.CompanyId);
        addedMemberships = [.. memberships.Where(membership => !invitedMembers.Contains(membership.MemberId))
            .Select(membership => (membership.MemberId, membership.CompanyId))];
    }

    internal static AccessChangeTrail Plan(IReadOnlyCollection<EntityEntry<IAuditable>> changed,
        IReadOnlyCollection<RoleAssignmentTrail.RoleSetChange> roleChanges) => new(changed, roleChanges);

    /// <summary>Los cambios de roles que no van adentro del alta de una persona: una entrada cada uno.</summary>
    internal IEnumerable<RoleAssignmentTrail.RoleSetChange> StandaloneRoleChanges =>
        roleChanges.Where(change => !invitedMembers.Contains(change.Place.MemberId) && !AddsMembership(change.Place));

    /// <summary>El miembro y las membresías de una persona invitada en este guardado son parte de la invitación.</summary>
    internal bool IsPartOfInvitation(EntityEntry<IAuditable> entry) => entry.Entity switch
    {
        Member member => invitedMembers.Contains(member.Id),
        CompanyMembership membership => invitedMembers.Contains(membership.MemberId),
        _ => false,
    };

    /// <summary>Suma al alta de una persona sus empresas y sus roles, por nombre, como los guarda el rastro de roles.</summary>
    internal void Complete(EntityEntry<IAuditable> entry, Dictionary<string, object?> diff,
        IReadOnlyDictionary<Guid, string> roleNames)
    {
        if (entry.State != EntityState.Added) return;
        switch (entry.Entity)
        {
            case Invitation invitation:
                CompleteInvitation(invitation.MemberId, diff, roleNames);
                break;
            case CompanyMembership membership when addedMemberships.Contains((membership.MemberId, membership.CompanyId)):
                AddRoles(diff, AuditChangeKeys.CompanyRoles, roleChanges.Where(change =>
                    change.Place.MemberId == membership.MemberId && change.Place.CompanyId == membership.CompanyId), roleNames);
                break;
        }
    }

    private bool AddsMembership(RoleAssignmentTrail.RolePlace place) =>
        place.CompanyId is { } companyId && addedMemberships.Contains((place.MemberId, companyId));

    // Orden del diálogo de invitar: los roles de la organización, las empresas y los roles en ellas.
    private void CompleteInvitation(Guid memberId, Dictionary<string, object?> diff,
        IReadOnlyDictionary<Guid, string> roleNames)
    {
        var mine = roleChanges.Where(change => change.Place.MemberId == memberId).ToArray();
        AddRoles(diff, AuditChangeKeys.OrganizationRoles, mine.Where(change => change.Place.CompanyId is null), roleNames);
        Guid[] companyIds =
        [
            .. invitedCompanies[memberId]
                .Concat(mine.Where(change => change.After.Count > 0).Select(change => change.Place.CompanyId).OfType<Guid>())
                .Distinct(),
        ];
        if (companyIds.Length > 0)
            diff[AuditChangeKeys.CompanyIds] = RoleAssignmentTrail.RoleSetChange.Value(null, companyIds);
        AddRoles(diff, AuditChangeKeys.CompanyRoles, mine.Where(change => change.Place.CompanyId is not null), roleNames);
    }

    private static IEnumerable<T> Added<T>(IEnumerable<EntityEntry<IAuditable>> changed) =>
        changed.Where(entry => entry.State == EntityState.Added).Select(entry => entry.Entity).OfType<T>();

    // Quien recién entra no tenía roles en ese lugar: el «antes» queda vacío, no «Sin roles».
    private static void AddRoles(Dictionary<string, object?> diff, string key,
        IEnumerable<RoleAssignmentTrail.RoleSetChange> changes, IReadOnlyDictionary<Guid, string> roleNames)
    {
        var here = changes.ToArray();
        if (here.Length == 0) return;
        var before = RoleAssignmentTrail.RoleSetChange.Names(here.SelectMany(change => change.Before), roleNames);
        var after = RoleAssignmentTrail.RoleSetChange.Names(here.SelectMany(change => change.After), roleNames);
        diff[key] = RoleAssignmentTrail.RoleSetChange.Value(before.Length == 0 ? null : before, after);
    }
}
