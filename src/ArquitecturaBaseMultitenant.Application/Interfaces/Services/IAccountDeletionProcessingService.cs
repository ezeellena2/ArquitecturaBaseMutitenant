using ArquitecturaBaseMultitenant.Application.Models.Legal;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Services;

/// <summary>Coordina la baja diferida: reclama una cuenta, procesa cada organización por separado y confirma el cierre global.</summary>
public interface IAccountDeletionProcessingService
{
    Task<Result<AccountDeletionWork?>> ClaimNextAsync(CancellationToken ct);
    Task<Result> ProcessTenantAsync(AccountDeletionWork work, AccountDeletionTenant tenant, CancellationToken ct);
    Task<Result> CompleteAsync(AccountDeletionWork work, CancellationToken ct);
}
