namespace ArquitecturaBaseMultitenant.Application.Services.OrganizationUsers;

/// <summary>
/// Qué roles da, quita o vuelve a dar un cambio de los roles de una persona: lo que la guarda de la 0061 controla. La usan
/// «Guardar cambios» (<see cref="OrganizationUserAssignmentWriter"/>) y la fuente del asistente
/// (<see cref="OrganizationUserActionSource"/>), así una propuesta que la fuente acepta es una que la ruta guarda.
/// <list type="bullet">
/// <item>Se quita lo guardado que no se pide, salvo en una empresa inactiva: una edición general no la reactiva ni borra
/// sus roles.</item>
/// <item>Vuelve lo guardado en una empresa inactiva que el cambio reactiva.</item>
/// <item>Se da lo pedido que no estaba; a un invitado, todo lo pedido: lo que tiene guardado no vale hasta que acepte (pudo
/// quedar de una invitación revocada).</item>
/// </list>
/// </summary>
internal static class OrganizationUserRoleChangePolicy
{
    /// <summary>
    /// Lo que entra, sale o vuelve entre <paramref name="saved"/> y <paramref name="desired"/>, comparados por rol y empresa
    /// (<paramref name="key"/>), con las empresas donde su membresía está inactiva y las que el cambio pide.
    /// </summary>
    internal static OrganizationUserRoleChanges<T> Decide<T>(IReadOnlyCollection<T> saved, IReadOnlyCollection<T> desired,
        Func<T, (Guid RoleId, Guid? CompanyId)> key, IReadOnlySet<Guid> inactiveCompanyIds, IReadOnlyCollection<Guid> desiredCompanyIds,
        bool invited)
    {
        ArgumentNullException.ThrowIfNull(key);
        var desiredKeys = desired.Select(key).ToHashSet();
        var savedKeys = saved.Select(key).ToHashSet();
        var dropped = saved.Where(row => !desiredKeys.Contains(key(row))).ToArray();
        T[] removed = [.. dropped.Where(row => key(row).CompanyId is not { } companyId || !inactiveCompanyIds.Contains(companyId))];
        T[] revived = [.. dropped.Where(row => key(row).CompanyId is { } companyId && inactiveCompanyIds.Contains(companyId)
            && desiredCompanyIds.Contains(companyId))];
        T[] added = [.. desired.Where(row => !savedKeys.Contains(key(row)))];
        return new(removed, added, revived, invited ? [.. desired] : added);
    }
}

/// <summary>Lo que quita, suma y vuelve a dar un cambio, y lo que da: lo sumado o, a un invitado, todo lo pedido.</summary>
internal sealed record OrganizationUserRoleChanges<T>(IReadOnlyList<T> Removed, IReadOnlyList<T> Added, IReadOnlyList<T> Revived,
    IReadOnlyList<T> Granted);
