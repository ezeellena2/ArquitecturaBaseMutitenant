using ArquitecturaBaseMultitenant.Application.Models.OrganizationUsers;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

/// <summary>
/// Puerto neutral del núcleo: si un teléfono es un método Phone verificado de un miembro activo de la organización actual,
/// devuelve su cuenta. No dice nada de cuentas que no son miembros activos.
/// </summary>
public interface IActiveMemberPhoneReader
{
    Task<Guid?> FindMemberUserIdAsync(PhoneNumber phone, CancellationToken cancellationToken);

    /// <summary>
    /// De varios teléfonos, los que son el método Phone verificado de un miembro activo o inactivo de la organización actual,
    /// con su nombre («Cargar teléfonos»). Una cuenta que no es miembro no aparece.
    /// </summary>
    Task<IReadOnlyList<MemberPhoneRow>> ListMembersAsync(IReadOnlyCollection<PhoneNumber> phones, CancellationToken cancellationToken);
}
