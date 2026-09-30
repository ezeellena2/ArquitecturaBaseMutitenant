using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Legal;
using ArquitecturaBaseMultitenant.Application.Models.Legal;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Infrastructure.Identity.Deletion;

/// <summary>El núcleo actual no bloquea ni cambia datos de tenant durante la gracia.</summary>
internal abstract class AccountDeletionParticipant : IAccountDeletionParticipant
{
    public virtual Task<IReadOnlyList<Error>> CheckAsync(AccountDeletionContext context, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Error>>([]);
    public virtual Task OnRequestedAsync(AccountDeletionContext context, CancellationToken ct) => Task.CompletedTask;
    public virtual Task OnCancelledAsync(AccountDeletionContext context, CancellationToken ct) => Task.CompletedTask;
    public abstract Task ExecuteAsync(AccountDeletionContext context, CancellationToken ct);
}
