using ArquitecturaBaseMultitenant.Application.Common.Formatting;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.ReferenceData;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Resources;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Services;

/// <summary>
/// El bot de un número que todavía no se configuró: como no se apaga (D9), responde con los textos iniciales en la cultura
/// de la organización, de lunes a viernes de 9 a 18 en su zona y sin menú. En Plataforma, la cultura es la predeterminada
/// del catálogo y la zona, la de su país. No se guarda hasta que alguien lo edita.
/// </summary>
internal sealed class WhatsAppBotResolver(ITenantSettingsReader settings, ICultureCatalog cultures, ICountryCatalog countries)
{
    private static readonly DayOfWeek[] Weekdays = [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday];
    private static readonly TimeOnly Opens = new(9, 0);
    private static readonly TimeOnly Closes = new(18, 0);

    internal async Task<WhatsAppBot> OrganizationAsync(Guid channelId, CancellationToken ct)
    {
        var current = await settings.FindCurrentAsync(ct) ?? throw new InvalidOperationException("The organization has no settings.");
        var profile = await new CultureProfiles(cultures).LoadAsync(current.DefaultCulture, ct);
        var bot = WhatsAppBot.Create(channelId);
        bot.Update(WhatsAppTexts.Get("Bot.DefaultWelcome", profile), WhatsAppTexts.Get("Bot.DefaultOutOfHours", profile),
            current.DefaultTimeZoneId, Hours(), [], askPromotions: false, offerAccountLink: false, employeesTeamId: null);
        return bot;
    }

    internal async Task<PlatformWhatsAppBot> PlatformAsync(CancellationToken ct)
    {
        var profile = await new CultureProfiles(cultures).LoadAsync(null, ct);
        var zone = (await countries.FindAsync(profile.Entry.CountryCode, ct))?.DefaultTimeZoneId
            ?? throw new InvalidOperationException("The default culture's country has no time zone.");
        var bot = PlatformWhatsAppBot.Create();
        bot.Update(WhatsAppTexts.Get("Bot.DefaultWelcome", profile), WhatsAppTexts.Get("Bot.DefaultOutOfHours", profile), zone, Hours(), []);
        return bot;
    }

    private static IEnumerable<WhatsAppBotHours> Hours() => Weekdays.Select(day => new WhatsAppBotHours(day, Opens, Closes));
}
