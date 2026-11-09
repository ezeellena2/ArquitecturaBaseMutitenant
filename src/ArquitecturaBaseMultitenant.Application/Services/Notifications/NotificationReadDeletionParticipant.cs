using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Legal;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Legal;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Services.Notifications;

/// <summary>
/// La baja de una cuenta borra qué avisos de la campana abrió (base de la 12), en el tramo global de la transacción del
/// worker: son suyos, globales y no se auditan.
/// </summary>
internal sealed class NotificationReadDeletionParticipant(INotificationReadRepository reads) : IAccountDeletionParticipant
{
    public Task<IReadOnlyList<Error>> CheckAsync(AccountDeletionContext context, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Error>>([]);
    public Task OnRequestedAsync(AccountDeletionContext context, CancellationToken ct) => Task.CompletedTask;
    public Task OnCancelledAsync(AccountDeletionContext context, CancellationToken ct) => Task.CompletedTask;

    public async Task ExecuteAsync(AccountDeletionContext context, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.TenantId is not null) return;
        await reads.RemoveForAccountAsync(context.UserId, ct);
    }
}
