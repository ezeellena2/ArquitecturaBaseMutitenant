using ArquitecturaBaseMultitenant.Domain.Authentication;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

public interface ILoginMethodRepository
{
    Task<LoginMethod?> GetByIdAsync(Guid methodId, CancellationToken cancellationToken);

    void Add(LoginMethod method);
}
