using ArquitecturaBaseMultitenant.Application.Models.Legal;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Services;

/// <summary>Permite a la cuenta solicitar o cancelar su baja según el estado y las reglas vigentes.</summary>
public interface IAccountDeletionService
{
    Task<Result<CancelAccountDeletionResponse>> CancelAsync(CancelAccountDeletionRequest request, CancellationToken ct);
    Task<Result<AccountDeletionResponse>> RequestAsync(RequestAccountDeletionRequest request, CancellationToken ct);
}
