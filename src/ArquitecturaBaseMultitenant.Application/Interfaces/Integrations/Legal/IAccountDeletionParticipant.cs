using ArquitecturaBaseMultitenant.Application.Models.Legal;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Legal;

/// <summary>Hooks de cuenta en la UoW llamadora; Execute recibe el alcance de cada paso.</summary>
public interface IAccountDeletionParticipant
{
    Task<IReadOnlyList<Error>> CheckAsync(AccountDeletionContext context, CancellationToken ct);
    Task OnRequestedAsync(AccountDeletionContext context, CancellationToken ct);
    Task OnCancelledAsync(AccountDeletionContext context, CancellationToken ct);
    Task ExecuteAsync(AccountDeletionContext context, CancellationToken ct);
}
