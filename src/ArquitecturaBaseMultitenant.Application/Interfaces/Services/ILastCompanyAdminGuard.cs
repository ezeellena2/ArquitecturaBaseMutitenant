using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Services;

public interface ILastCompanyAdminGuard
{
    Task LockAsync(Guid companyId, CancellationToken cancellationToken);
    Task<Result> CheckAnotherActiveAdminAsync(Guid companyId, CancellationToken cancellationToken);
}
