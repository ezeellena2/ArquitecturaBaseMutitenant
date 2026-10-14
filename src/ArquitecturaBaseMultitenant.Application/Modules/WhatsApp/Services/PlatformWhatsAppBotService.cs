using ArquitecturaBaseMultitenant.Application.Common.Exceptions;
using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Resources;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Services;

/// <summary>
/// El bot del número de Plataforma (ADR 0038, precisión del 03/10): global, con su evento de seguridad en la misma
/// transacción. «Entrar a mi cuenta» va primero, no se edita y se conserva aunque el pedido no la mande.
/// </summary>
internal sealed class PlatformWhatsAppBotService(IWhatsAppBotReader reader, IPlatformWhatsAppBotRepository bots,
    IPlatformWhatsAppTeamRepository teams, WhatsAppBotResolver resolver, ICurrentUser user, ISecurityEventRepository events,
    IRequestInfo requestInfo, IUnitOfWork unitOfWork, IRequestValidator validator, TimeProvider clock,
    ILogger<PlatformWhatsAppBotService> logger) : IPlatformWhatsAppBotService
{
    private const string Reason = "WhatsApp platform bot changed";

    public Task<Result<PlatformWhatsAppBotResponse>> GetAsync(CancellationToken cancellationToken) =>
        OperationLog.RunAsync<PlatformWhatsAppBotResponse>(logger, clock, "PlatformWhatsAppBotGet", async () =>
            Response(await reader.FindPlatformAsync(cancellationToken) ?? await resolver.PlatformAsync(cancellationToken)));

    public Task<Result<PlatformWhatsAppBotResponse>> UpdateAsync(UpdatePlatformWhatsAppBotRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync<PlatformWhatsAppBotResponse>(logger, clock, "PlatformWhatsAppBotUpdate", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            var saved = await unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                var bot = await bots.GetAsync(request.Version, ct);
                if (bot is null && request.Version != 0) throw new ConcurrencyConflictException();
                var menu = WhatsAppBotService.Options([.. request.Menu!.Where(option => option.Id != PlatformWhatsAppBot.SignInOptionId)], bot);
                var teamIds = menu.Select(option => option.TeamId).OfType<Guid>().Distinct().ToArray();
                if (teamIds.Length > 0 && (await teams.ListAsync(teamIds, ct)).Count != teamIds.Length) return Result.Failure(WhatsAppErrors.TeamNotFound);
                var before = bot?.Options.Count;
                if (bot is null) { bot = PlatformWhatsAppBot.Create(); bots.Add(bot); }
                var updated = bot.Update(request.Welcome!, request.OutOfHours!, request.Schedule!.TimeZoneId!,
                    WhatsAppBotService.Hours(request.Schedule), menu);
                if (updated.IsFailure) return updated;
                var changes = new SecurityEventChanges();
                changes.AddCount("whatsappBotOptions", before, bot.Options.Count);
                events.Add(SecurityEvent.ForPlatformSettings(AuditActorKind.PlatformOperator, user.UserId!.Value, Reason,
                    clock.GetUtcNow().UtcDateTime).WithEvidence(changes, requestInfo.IpAddress));
                return Result.Success();
            }, CommitPolicy.OnSuccess, cancellationToken);
            if (saved.IsFailure) return saved.Error;
            return Response((await reader.FindPlatformAsync(cancellationToken))!);
        });

    private static PlatformWhatsAppBotResponse Response(PlatformWhatsAppBot bot) =>
        new(bot.Welcome, bot.OutOfHours, WhatsAppBotService.Schedule(bot),
            [new WhatsAppBotMenuOption(PlatformWhatsAppBot.SignInOptionId, WhatsAppTexts.Get("Bot.SignInOption"), WhatsAppBotAction.Reply, null, null, true),
                .. WhatsAppBotService.Menu(bot)], bot.Version);
}
