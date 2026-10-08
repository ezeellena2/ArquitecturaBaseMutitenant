using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Legal;
using ArquitecturaBaseMultitenant.Application.Models.Legal;
using ArquitecturaBaseMultitenant.Domain.Results;
namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Services;

internal sealed class WhatsAppAccountDeletionParticipant(WhatsAppContactRemover remover) : IAccountDeletionParticipant
{
    public Task<IReadOnlyList<Error>> CheckAsync(AccountDeletionContext context, CancellationToken ct) => Task.FromResult<IReadOnlyList<Error>>([]);
    public Task OnRequestedAsync(AccountDeletionContext context, CancellationToken ct) => Task.CompletedTask;
    public Task OnCancelledAsync(AccountDeletionContext context, CancellationToken ct) => Task.CompletedTask;
    public Task ExecuteAsync(AccountDeletionContext context, CancellationToken ct) =>
        context.TenantId is null ? remover.RemoveAccountAsync(context.UserId, ct) : Task.CompletedTask;
}
