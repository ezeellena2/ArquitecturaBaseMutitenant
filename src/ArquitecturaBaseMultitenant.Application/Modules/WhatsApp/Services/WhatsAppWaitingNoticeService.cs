using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Services;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Services;

/// <summary>
/// Un correo por equipo cuando la conversación sin tomar más vieja pasa el tiempo elegido; no se repite hasta que la
/// bandeja del equipo se vacía y vuelve a llenarse (D6). «No avisar» deja el equipo afuera.
/// </summary>
internal sealed class WhatsAppWaitingNoticeService(IWhatsAppTeamNoticeRepository notices, IWhatsAppTeamReader teams,
    IWhatsAppConversationReader conversations, WhatsAppWaitingNotifier notifier, WhatsAppModulePolicy module, ITenantContext tenant,
    IUnitOfWork unitOfWork, TimeProvider clock, ILogger<WhatsAppWaitingNoticeService> logger) : IWhatsAppWaitingNoticeService
{
    public Task<Result> NotifyOrganizationAsync(CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, clock, "WhatsAppWaitingNoticeOrganization", async () =>
            await module.IsEnabledAsync(tenant.RequiredTenantId, cancellationToken)
                ? await NotifyAsync(platform: false, cancellationToken) : Result.Success());

    public Task<Result> NotifyPlatformAsync(CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, clock, "WhatsAppWaitingNoticePlatform", () => NotifyAsync(platform: true, cancellationToken));

    private Task<Result> NotifyAsync(bool platform, CancellationToken cancellationToken) =>
        unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await notices.LockAsync(platform, ct);
            var nowUtc = clock.GetUtcNow().UtcDateTime;
            var waiting = (await conversations.ListWaitingAsync(platform, ct)).ToDictionary(row => row.TeamId);
            var sent = (await notices.ListAsync(platform, ct)).ToDictionary(notice => notice.TeamId);
            // Una bandeja que se vació (o un equipo que ya no avisa) vuelve a poder avisar.
            foreach (var notice in sent.Values.Where(notice => !waiting.ContainsKey(notice.TeamId))) notices.Remove(notice);
            foreach (var team in await teams.ListNotifyingAsync(platform, ct))
            {
                if (!waiting.TryGetValue(team.Id, out var row) || sent.ContainsKey(team.Id)
                    || row.OldestWaitingSinceUtc.AddMinutes(team.NotifyAfterMinutes!.Value) > nowUtc) continue;
                await notifier.EnqueueAsync(team, row.Count, platform, ct);
                notices.Add(platform ? PlatformWhatsAppTeamNotice.Create(team.Id, nowUtc) : WhatsAppTeamNotice.Create(team.Id, nowUtc));
            }
            return Result.Success();
        }, CommitPolicy.OnSuccess, cancellationToken);
}
