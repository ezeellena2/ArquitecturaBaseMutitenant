using ArquitecturaBaseMultitenant.Domain.Authentication;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

public interface ILoginMethodRepository
{
    Task LockUserAsync(Guid userId, CancellationToken cancellationToken);
    Task<LoginMethod?> GetByIdForUserAsync(Guid userId, Guid methodId, CancellationToken cancellationToken);
    Task<IReadOnlyList<LoginMethod>> ListByUserIdAsync(Guid userId, CancellationToken cancellationToken);
    Task ClearPrimaryAsync(Guid userId, CancellationToken cancellationToken);
    Task<LoginMethod?> GetByIdAsync(Guid methodId, CancellationToken cancellationToken);

    void Add(LoginMethod method);
    void Remove(LoginMethod method);
}
