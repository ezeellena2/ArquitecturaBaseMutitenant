using ArquitecturaBaseMultitenant.Application.Common.Exceptions;
using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Services;

/// <summary>
/// El bot de cada número de la organización del claim (ADR 0038). Sin configurar responde con los valores iniciales
/// (versión 0); el primer cambio lo guarda. Cada opción «pasa a» un equipo de Clientes y los empleados, a uno de Internos.
/// </summary>
internal sealed class WhatsAppBotService(IWhatsAppChannelReader numbers, IWhatsAppChannelRepository channels, IWhatsAppBotReader reader,
    IWhatsAppBotRepository bots, IWhatsAppTeamRepository teams, WhatsAppBotResolver resolver, IUnitOfWork unitOfWork,
    IRequestValidator validator, TimeProvider clock, ILogger<WhatsAppBotService> logger) : IWhatsAppBotService
{
    public Task<Result<WhatsAppBotResponse>> GetAsync(Guid numberId, CancellationToken cancellationToken) =>
        OperationLog.RunAsync<WhatsAppBotResponse>(logger, clock, "WhatsAppBotGet", async () =>
        {
            if (await numbers.FindOrganizationAsync(numberId, clock.GetUtcNow().UtcDateTime, cancellationToken) is null)
                return WhatsAppErrors.NumberNotFound;
            return Response(await reader.FindAsync(numberId, cancellationToken) ?? await resolver.OrganizationAsync(numberId, cancellationToken));
        });

    public Task<Result<WhatsAppBotResponse>> UpdateAsync(Guid numberId, UpdateWhatsAppBotRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync<WhatsAppBotResponse>(logger, clock, "WhatsAppBotUpdate", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            var saved = await unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                if (await channels.GetOrganizationAsync(numberId, null, ct) is null) return Result.Failure(WhatsAppErrors.NumberNotFound);
                await bots.LockAsync(numberId, ct);
                var bot = await bots.GetAsync(numberId, request.Version, ct);
                // Sin fila, el número responde con los valores iniciales, que se leyeron con versión 0.
                if (bot is null && request.Version != 0) throw new ConcurrencyConflictException();
                var menu = Options(request.Menu!, bot);
                if (!await TeamsExistAsync(menu, request.EmployeesTeamId, ct)) return Result.Failure(WhatsAppErrors.TeamNotFound);
                if (bot is null) { bot = WhatsAppBot.Create(numberId); bots.Add(bot); }
                return bot.Update(request.Welcome!, request.OutOfHours!, request.Schedule!.TimeZoneId!, Hours(request.Schedule), menu,
                    request.AskPromotions ?? false, request.OfferAccountLink ?? false, request.EmployeesTeamId);
            }, CommitPolicy.OnSuccess, cancellationToken);
            if (saved.IsFailure) return saved.Error;
            return Response((await reader.FindAsync(numberId, cancellationToken))!);
        });

    private async Task<bool> TeamsExistAsync(IReadOnlyList<WhatsAppBotOption> menu, Guid? employeesTeamId, CancellationToken ct)
    {
        var ids = menu.Select(option => option.TeamId).Append(employeesTeamId).OfType<Guid>().Distinct().ToArray();
        if (ids.Length == 0) return true;
        var found = (await teams.ListAsync(ids, ct)).ToDictionary(team => team.Id, team => team.Inbox);
        bool Serves(Guid? team, WhatsAppInbox inbox) => team is not { } id || found.TryGetValue(id, out var current) && current == inbox;
        return menu.All(option => Serves(option.TeamId, WhatsAppInbox.Customers)) && Serves(employeesTeamId, WhatsAppInbox.Internal);
    }

    /// <summary>Las opciones pedidas; una que ya existía conserva su identificador, que es el de su fila en WhatsApp.</summary>
    internal static IReadOnlyList<WhatsAppBotOption> Options(IReadOnlyList<WhatsAppBotMenuOptionInput> menu, WhatsAppBotBase? current) =>
        [.. menu.Select(option => new WhatsAppBotOption(
            option.Id is { } id && current?.FindOption(id) is not null ? id : Guid.CreateVersion7(), option.Label!.Trim(), option.Action!.Value,
            option.Action == WhatsAppBotAction.Team ? option.TeamId : null, option.Action == WhatsAppBotAction.Reply ? option.Reply!.Trim() : null))];

    internal static IEnumerable<WhatsAppBotHours> Hours(WhatsAppBotScheduleInput schedule) =>
        schedule.Days!.Select(day => new WhatsAppBotHours(day.Day!.Value, day.From!.Value, day.To!.Value));

    internal static WhatsAppBotSchedule Schedule(WhatsAppBotBase bot) =>
        new(bot.TimeZoneId, [.. bot.Hours.Select(day => new WhatsAppBotScheduleDay(day.Day, day.From, day.To))]);

    internal static IEnumerable<WhatsAppBotMenuOption> Menu(WhatsAppBotBase bot) =>
        bot.Options.Select(option => new WhatsAppBotMenuOption(option.Id, option.Label, option.Action, option.TeamId, option.Reply, false));

    private static WhatsAppBotResponse Response(WhatsAppBot bot) =>
        new(bot.ChannelId, bot.Welcome, bot.OutOfHours, Schedule(bot), [.. Menu(bot)], bot.AskPromotions, bot.OfferAccountLink,
            bot.EmployeesTeamId, bot.Version);
}
