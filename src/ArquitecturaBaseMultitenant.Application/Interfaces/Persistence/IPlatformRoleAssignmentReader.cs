using ArquitecturaBaseMultitenant.Domain.Platform;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

/// <summary>Consulta la autorización global del operador sin datos de una organización.</summary>
public interface IPlatformRoleAssignmentReader
{
    Task<PlatformRole?> FindRoleAsync(Guid userId, CancellationToken cancellationToken);
}
