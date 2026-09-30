using ArquitecturaBaseMultitenant.Application.Models.Legal;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Services;

public interface IAccountDeletionProcessingService
{
    Task<Result<AccountDeletionWork?>> ClaimNextAsync(CancellationToken ct);
    Task<Result> ProcessTenantAsync(AccountDeletionWork work, AccountDeletionTenant tenant, CancellationToken ct);
    Task<Result> CompleteAsync(AccountDeletionWork work, CancellationToken ct);
}
