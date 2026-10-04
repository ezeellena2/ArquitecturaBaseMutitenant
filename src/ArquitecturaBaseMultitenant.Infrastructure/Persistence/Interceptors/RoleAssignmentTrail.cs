using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Agrupa las asignaciones de rol de un guardado: los roles de una persona en un lugar (la organización o una
/// empresa) dejan un solo cambio, con los nombres antes y después, como el tablero Auditoria-Org.
/// Antes y después salen del ChangeTracker: quien cambia los roles de una persona en un lugar carga primero los que
/// ya tiene ahí. Lo hacen todos los servicios; en una empresa o una organización nueva no hay ninguno.
/// </summary>
internal static class RoleAssignmentTrail
{
    internal static IReadOnlyList<RoleSetChange> Collect(DbContext context)
    {
        var tracked = context.ChangeTracker.Entries<RoleAssignment>().ToArray();
        return
        [
            .. tracked
                .Where(entry => entry.State is EntityState.Added or EntityState.Deleted)
                .Select(entry => new RolePlace(entry.Entity.TenantId, entry.Entity.MemberId, entry.Entity.CompanyId))
                .Distinct()
                .Select(place => Change(place, tracked.Where(entry => place.Contains(entry.Entity)).ToArray()))
                .Where(change => !change.Before.SetEquals(change.After)),
        ];
    }

    internal static IReadOnlyDictionary<Guid, string> LoadNames(DbContext context,
        IReadOnlyCollection<RoleSetChange> changes)
    {
        var (names, missing) = TrackedNames(context, changes);
        if (missing.Length == 0) return names;
        foreach (var role in StoredNames(context, missing).ToArray()) names[role.Id] = role.Name;
        return names;
    }

    internal static async Task<IReadOnlyDictionary<Guid, string>> LoadNamesAsync(DbContext context,
        IReadOnlyCollection<RoleSetChange> changes, CancellationToken cancellationToken)
    {
        var (names, missing) = TrackedNames(context, changes);
        if (missing.Length == 0) return names;
        foreach (var role in await StoredNames(context, missing).ToArrayAsync(cancellationToken))
            names[role.Id] = role.Name;
        return names;
    }

    private static RoleSetChange Change(RolePlace place, IReadOnlyCollection<EntityEntry<RoleAssignment>> here) => new(
        place,
        [.. here.Where(entry => entry.State != EntityState.Added).Select(entry => entry.Entity.RoleId)],
        [.. here.Where(entry => entry.State != EntityState.Deleted).Select(entry => entry.Entity.RoleId)]);

    // Un rol creado en el mismo guardado (el alta de una organización) todavía no está en la base.
    private static (Dictionary<Guid, string> Names, Guid[] Missing) TrackedNames(DbContext context,
        IReadOnlyCollection<RoleSetChange> changes)
    {
        var roleIds = changes.SelectMany(change => change.Before.Concat(change.After)).ToHashSet();
        var names = new Dictionary<Guid, string>();
        if (roleIds.Count == 0) return (names, []);
        foreach (var entry in context.ChangeTracker.Entries<Role>().Where(entry => roleIds.Contains(entry.Entity.Id)))
            names[entry.Entity.Id] = entry.Entity.Name;
        return (names, [.. roleIds.Where(roleId => !names.ContainsKey(roleId))]);
    }

    private static IQueryable<RoleName> StoredNames(DbContext context, Guid[] roleIds) =>
        context.Set<Role>().AsNoTracking().Where(role => roleIds.Contains(role.Id))
            .Select(role => new RoleName(role.Id, role.Name));

    private sealed record RoleName(Guid Id, string Name);

    internal sealed record RolePlace(Guid TenantId, Guid MemberId, Guid? CompanyId)
    {
        public bool Contains(RoleAssignment assignment) => assignment.TenantId == TenantId
            && assignment.MemberId == MemberId && assignment.CompanyId == CompanyId;
    }

    internal sealed record RoleSetChange(RolePlace Place, HashSet<Guid> Before, HashSet<Guid> After)
    {
        /// <summary>
        /// La persona y la empresa van como claves técnicas, para que la lectura las muestre por nombre; los roles,
        /// por su nombre guardado, para que el cambio se lea igual aunque después se renombren o se borren.
        /// </summary>
        public Dictionary<string, object?> Diff(IReadOnlyDictionary<Guid, string> names)
        {
            var diff = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                [nameof(RoleAssignment.MemberId)] = Value(null, Place.MemberId),
            };
            if (Place.CompanyId is { } companyId) diff[nameof(RoleAssignment.CompanyId)] = Value(null, companyId);
            diff[Place.CompanyId is null ? AuditChangeKeys.OrganizationRoles : AuditChangeKeys.CompanyRoles] =
                Value(Names(Before, names), Names(After, names));
            return diff;
        }

        private static Dictionary<string, object?> Value(object? oldValue, object? newValue) => new()
        {
            ["old"] = oldValue,
            ["new"] = newValue,
        };

        private static string[] Names(IEnumerable<Guid> roleIds, IReadOnlyDictionary<Guid, string> names) =>
        [
            .. roleIds.Select(roleId => names.GetValueOrDefault(roleId)).OfType<string>()
                .Order(StringComparer.Ordinal),
        ];
    }
}
