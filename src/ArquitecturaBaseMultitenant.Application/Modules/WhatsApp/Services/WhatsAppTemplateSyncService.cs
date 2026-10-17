using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Services;
using ArquitecturaBaseMultitenant.Domain.Results;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Services;

/// <summary>
/// Trae de Meta las plantillas de cada cuenta de WhatsApp de los números vigentes y deja su estado como Meta lo tiene: cubre
/// los webhooks perdidos y las plantillas del sistema creadas a mano en Meta. Las consultas a Meta van antes de la
/// transacción; una cuenta que no responde queda como estaba.
/// </summary>
internal sealed class WhatsAppTemplateSyncService(IWhatsAppChannelReader channels, IWhatsAppTemplateClient client, IWhatsAppTokenProtector protector,
    WhatsAppTemplateStateWriter states, IUnitOfWork unitOfWork, TimeProvider clock, ILogger<WhatsAppTemplateSyncService> logger) : IWhatsAppTemplateSyncService
{
    public Task<Result> SyncAsync(CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, clock, "WhatsAppTemplateSync", async () =>
        {
            var fetched = new List<(string WabaId, IReadOnlyList<WhatsAppMetaTemplate> Templates)>();
            foreach (var account in await channels.ListAllTemplateAccountsAsync(cancellationToken))
            {
                if (protector.TryUnprotect(account.ChannelId, account.EncryptedToken) is not { } token) continue;
                var listed = await client.ListAsync(account.WabaId, token, cancellationToken);
                if (listed.IsSuccess) fetched.Add((account.WabaId, listed.Value));
            }
            if (fetched.Count == 0) return Result.Success();
            return await unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                foreach (var (wabaId, templates) in fetched.OrderBy(value => value.WabaId, StringComparer.Ordinal))
                    await states.ApplyAsync(wabaId, [.. templates], replaceAll: true, ct);
                return Result.Success();
            }, CommitPolicy.OnSuccess, cancellationToken);
        });
}
