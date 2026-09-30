using ArquitecturaBaseMultitenant.Domain.Tenancy;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

/// <summary>Obtiene y agrega membresías privadas bajo el alcance y la transacción del tenant activo.</summary>
public interface IMemberRepository
{
    Task<Member?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken);

    void Add(Member member);
}
