using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Legal;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Legal;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Services.Legal;

/// <summary>
/// Cierra los pedidos de «Recuperar mi cuenta» de una cuenta eliminada (ADR 0035): los borra, con sus datos
/// personales, en el tramo global de la transacción del worker. Los SecurityEvent de cada decisión se conservan.
/// </summary>
internal sealed class RecoveryDeletionParticipant(IAccountDeletionCleanupRepository cleanup) : IAccountDeletionParticipant
{
    public Task<IReadOnlyList<Error>> CheckAsync(AccountDeletionContext context, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Error>>([]);
    public Task OnRequestedAsync(AccountDeletionContext context, CancellationToken ct) => Task.CompletedTask;
    public Task OnCancelledAsync(AccountDeletionContext context, CancellationToken ct) => Task.CompletedTask;

    public async Task ExecuteAsync(AccountDeletionContext context, CancellationToken ct)
    {
        if (context.TenantId is not null) return;
        await cleanup.RemoveRecoveryRequestsAsync(context.UserId, ct);
    }
}
