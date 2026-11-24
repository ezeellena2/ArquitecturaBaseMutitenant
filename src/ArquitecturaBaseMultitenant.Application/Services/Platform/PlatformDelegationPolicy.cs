using ArquitecturaBaseMultitenant.Domain.Authorization;

namespace ArquitecturaBaseMultitenant.Application.Services.Platform;

/// <summary>
/// Nadie da lo que no tiene, en Plataforma (ADR 0061 §4). Decide sin leer ni escribir, con lo efectivo de quien pide (sus
/// permisos más lo que incluyen, decisión 260): invitar a un operador o cambiarle los permisos solo con permisos que
/// tiene, y suspender, reactivar, dar de baja o quitar a un operador solo si tiene todos sus permisos. La usan
/// <see cref="PlatformDelegationGuard"/> y el catálogo de Plataforma (<c>grantable</c>, decisión 269).
/// </summary>
internal static class PlatformDelegationPolicy
{
    /// <summary>Lo que permiten unos permisos de Plataforma: los del catálogo más lo que incluyen.</summary>
    public static IReadOnlySet<string> Effective(IEnumerable<string> permissions)
    {
        ArgumentNullException.ThrowIfNull(permissions);
        return new HashSet<string>(PlatformPermissions.Inclusions.Effective(permissions), StringComparer.Ordinal);
    }

    /// <summary>Si puede dar este permiso (<c>grantable</c>): si lo tiene.</summary>
    public static bool CanGrant(IReadOnlySet<string> held, string permission)
    {
        ArgumentNullException.ThrowIfNull(held);
        return held.Contains(permission);
    }

    /// <summary>Invitar o cambiar los permisos: cada permiso que entra o sale, con lo que incluye, lo tiene.</summary>
    public static bool CanChangePermissions(IReadOnlySet<string> held, IEnumerable<string> before,
        IEnumerable<string> after)
    {
        ArgumentNullException.ThrowIfNull(held);
        var changed = new HashSet<string>(Effective(before), StringComparer.Ordinal);
        changed.SymmetricExceptWith(Effective(after));
        return changed.IsSubsetOf(held);
    }

    /// <summary>Actuar sobre un operador: tiene todos sus permisos. Sobre uno mismo siempre.</summary>
    public static bool CanActOn(bool self, IReadOnlySet<string> held, IEnumerable<string> target)
    {
        ArgumentNullException.ThrowIfNull(held);
        return self || Effective(target).IsSubsetOf(held);
    }
}
