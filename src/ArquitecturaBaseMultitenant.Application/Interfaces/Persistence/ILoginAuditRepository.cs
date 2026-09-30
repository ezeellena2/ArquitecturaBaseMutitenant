using ArquitecturaBaseMultitenant.Domain.Authentication;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

/// <summary>Registra intentos de ingreso y recupera la última entrada exitosa para el perfil de la cuenta.</summary>
public interface ILoginAuditRepository
{
    void Add(LoginAudit audit);

    Task<DateTime?> FindLastSuccessAtUtcAsync(Guid userId, CancellationToken cancellationToken);
}
