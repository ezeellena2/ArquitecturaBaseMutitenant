using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Legal;
using ArquitecturaBaseMultitenant.Application.Models.Legal;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Services;

/// <summary>
/// La baja de una cuenta en WhatsApp (B7): en cada organización, lo que escribió como miembro queda sin su nombre y las
/// conversaciones que tenía tomadas quedan sin tomar; en su espacio personal se borran sus vínculos con sus copias; y en lo
/// global, sus contactos del ingreso y su nombre en las consultas que atendió como operadora. El texto de los chats de la
/// organización es de la organización y se queda (D4).
/// </summary>
internal sealed class WhatsAppAccountDeletionParticipant(WhatsAppContactRemover remover, WhatsAppAccountDataRemover data) : IAccountDeletionParticipant
{
    public Task<IReadOnlyList<Error>> CheckAsync(AccountDeletionContext context, CancellationToken ct) => Task.FromResult<IReadOnlyList<Error>>([]);
    public Task OnRequestedAsync(AccountDeletionContext context, CancellationToken ct) => Task.CompletedTask;
    public Task OnCancelledAsync(AccountDeletionContext context, CancellationToken ct) => Task.CompletedTask;
    public async Task ExecuteAsync(AccountDeletionContext context, CancellationToken ct)
    {
        switch (context)
        {
            case { TenantId: null }:
                await remover.RemoveAccountAsync(context.UserId, ct);
                await data.ForgetPlatformAuthorAsync(context.UserId, context.NowUtc, ct);
                break;
            case { TenantKind: TenantKind.Business }:
                await data.ForgetOrganizationAuthorAsync(context.UserId, context.NowUtc, ct);
                break;
            case { TenantKind: TenantKind.Personal, TenantId: { } personal }:
                await data.RemoveLinksAsync(personal, ct);
                break;
        }
    }
}
