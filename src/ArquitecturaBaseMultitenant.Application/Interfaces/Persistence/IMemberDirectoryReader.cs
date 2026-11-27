using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Models.Tenancy;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

/// <summary>
/// El directorio de miembros de la organización del claim, para las listas que arma otra área sin saber de ella (C2 de la E12):
/// id, nombre visible de la cuenta y, si se piden, los nombres de sus roles o de sus empresas. Entra un miembro activo o
/// inactivo con nombre, y nunca uno de otra organización, un correo, un teléfono ni un método de ingreso. La lista lee con un
/// timeout corto (3 s).
/// </summary>
public interface IMemberDirectoryReader
{
    /// <summary>Los que cumplen las condiciones, por nombre, hasta el tope; cuenta el total solo si lo pasan.</summary>
    Task<MemberDirectoryResponse> ListAsync(MemberDirectoryQuery query, CancellationToken ct);

    /// <summary>Una persona que se puede elegir de ejemplo: un miembro activo con nombre; <c>null</c> si no es de la organización.</summary>
    Task<MemberDirectorySampleRow?> FindSampleAsync(Guid memberId, CancellationToken ct);

    /// <summary>Las personas que se pueden elegir de ejemplo, por nombre; la búsqueda mira el nombre.</summary>
    Task<PagedResult<MemberDirectorySampleRow>> ListSamplesAsync(PagedRequest request, CancellationToken ct);

    /// <summary>Los roles de la organización, por nombre.</summary>
    Task<PagedResult<MemberDirectoryNameRow>> ListRolesAsync(PagedRequest request, CancellationToken ct);

    /// <summary>Las empresas de la organización, por nombre.</summary>
    Task<PagedResult<MemberDirectoryNameRow>> ListCompaniesAsync(PagedRequest request, CancellationToken ct);

    /// <summary>Los de esos ids que existen en la organización; un id que ya no existe no viene.</summary>
    Task<IReadOnlyList<MemberDirectoryNameRow>> ListRolesByIdAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct);

    /// <summary>Las de esos ids que existen en la organización; un id que ya no existe no viene.</summary>
    Task<IReadOnlyList<MemberDirectoryNameRow>> ListCompaniesByIdAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct);
}
