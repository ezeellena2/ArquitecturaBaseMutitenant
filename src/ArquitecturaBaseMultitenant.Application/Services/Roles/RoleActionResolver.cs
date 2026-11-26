using System.Globalization;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.AssistantActions;
using ArquitecturaBaseMultitenant.Application.Models.Roles;
using ArquitecturaBaseMultitenant.Application.Resources;
using ArquitecturaBaseMultitenant.Domain.Authorization;

namespace ArquitecturaBaseMultitenant.Application.Services.Roles;

/// <summary>
/// Los roles que nombra una acción del asistente (ADR 0068 §5), para todas las fuentes, sin leer la lista entera: se buscan
/// con el <c>ApplySearch</c> del lector, de a una página, y se leen por id solo los de la propuesta o los de una persona.
/// Ver los roles es lo que pide su lista de la pantalla (<c>roles.read</c> en la organización); la fuente que nombra roles
/// sin pedir ese permiso en su requisito lo mira con <see cref="CanRead"/>.
/// </summary>
internal sealed class RoleActionResolver(IRoleReader roles)
{
    /// <summary>Si puede ver los roles, como lo mira <c>[HasPermission]</c> de <c>GET api/roles/paged</c>.</summary>
    internal static bool CanRead(PermissionsByPlace held) =>
        OrganizationDelegationPolicy.InOrganization(held).Contains(Permissions.Roles.Read);

    /// <summary>Un rol se nombra como lo muestra la pantalla, en el idioma de la persona o en el de los recursos, o como se guardó.</summary>
    internal static IEnumerable<string> Names(RoleRow role) => NamesOf(role.Name);

    /// <summary>Una página de los roles de un lugar, con la búsqueda y el orden del listado de siempre.</summary>
    internal Task<PagedResult<RoleRow>> PageAsync(ListRolesRequest request, CancellationToken cancellationToken) =>
        roles.ListAsync(request, cancellationToken);

    /// <summary>
    /// Los roles del lugar (<paramref name="place"/>: alcance o empresa) que pueden ser el texto: los que el lector encuentra con
    /// su búsqueda y los de sistema cuyo nombre en pantalla lo contiene, que se guardan con su clave.
    /// </summary>
    internal async Task<IReadOnlyList<RoleRow>> SearchAsync(string text, ListRolesRequest place, CancellationToken cancellationToken)
    {
        var found = (await roles.ListAsync(place with
        {
            Search = AssistantActionInputs.SearchOf(text), Page = PagedRequest.DefaultPage, PageSize = AssistantActionLimits.Candidates,
        }, cancellationToken)).Items.ToList();
        foreach (var key in SystemRoles.All.Where(key => NamesOf(key).Any(name => AssistantActionInputs.Contains(name, text))))
        {
            var system = await roles.ListAsync(place with { Search = null, SystemRoleKey = key, Page = PagedRequest.DefaultPage }, cancellationToken);
            found.AddRange(system.Items.Where(role => found.TrueForAll(other => other.Id != role.Id)));
        }
        return found;
    }

    /// <summary>Los roles pedidos, por id, en una consulta; los que no están en la organización no vuelven.</summary>
    internal async Task<IReadOnlyDictionary<Guid, RoleRow>> ListByIdsAsync(IEnumerable<Guid> roleIds, CancellationToken cancellationToken)
    {
        Guid[] ids = [.. roleIds.Distinct()];
        return ids.Length == 0 ? new Dictionary<Guid, RoleRow>()
            : (await roles.ListByIdsAsync(ids, cancellationToken)).ToDictionary(role => role.Id);
    }

    /// <summary>Los permisos de cada rol pedido, en una consulta.</summary>
    internal async Task<IReadOnlyDictionary<Guid, IReadOnlyList<string>>> ListPermissionsAsync(IEnumerable<Guid> roleIds,
        CancellationToken cancellationToken)
    {
        Guid[] ids = [.. roleIds.Distinct()];
        return ids.Length == 0 ? new Dictionary<Guid, IReadOnlyList<string>>() : await roles.ListPermissionsByRoleAsync(ids, cancellationToken);
    }

    private static IEnumerable<string> NamesOf(string name) =>
        new[] { RoleTexts.Name(name), RoleTexts.NameIn(name, CultureInfo.InvariantCulture), name }.Distinct(StringComparer.Ordinal);
}
