using ArquitecturaBaseMultitenant.Domain.Authorization;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

/// <summary>Lee y prepara asignaciones de una membresía dentro del tenant activo.</summary>
public interface IRoleAssignmentRepository
{
    Task<IReadOnlyList<RoleAssignment>> ListForMemberAsync(Guid memberId, CancellationToken cancellationToken);

    void Add(RoleAssignment assignment);

    void Remove(RoleAssignment assignment);
}
