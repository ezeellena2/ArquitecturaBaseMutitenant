using ArquitecturaBaseMultitenant.Application.Models.Identity;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

public interface ILoginMethodReader
{
    Task<IReadOnlyList<LoginMethodRow>> ListByUserIdAsync(Guid userId, CancellationToken cancellationToken);
}
