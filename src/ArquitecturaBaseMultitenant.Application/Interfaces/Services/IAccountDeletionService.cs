using ArquitecturaBaseMultitenant.Application.Models.Legal;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Services;

public interface IAccountDeletionService
{
    Task<Result<CancelAccountDeletionResponse>> CancelAsync(CancelAccountDeletionRequest request, CancellationToken ct);
    Task<Result<AccountDeletionResponse>> RequestAsync(RequestAccountDeletionRequest request, CancellationToken ct);
}
