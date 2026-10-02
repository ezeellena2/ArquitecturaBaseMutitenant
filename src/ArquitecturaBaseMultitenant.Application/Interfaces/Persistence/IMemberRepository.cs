using ArquitecturaBaseMultitenant.Domain.Tenancy;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

/// <summary>Obtiene y agrega membresías privadas bajo el alcance y la transacción del tenant activo.</summary>
public interface IMemberRepository
{
    Task LockUserAsync(Guid userId, CancellationToken cancellationToken);
    Task<Member?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken);
    Task<Member?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<Member?> GetByIdAsync(Guid id, uint expectedVersion, CancellationToken cancellationToken);

    void Add(Member member);
    void MarkUpdated(Member member);
}
