using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ArquitecturaBaseMultitenant.Application.Interfaces.ReferenceData;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Resources;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Services;

/// <summary>
/// Arma la respuesta de una plantilla. Las del sistema salen del catálogo de la E8 y sus textos, con lo que Meta dice de cada
/// idioma en la cuenta de Plataforma: el idioma de quien mira va primero, con su texto, y la categoría de arriba es la suya.
/// El motivo de un rechazo se muestra con el texto del código de Meta. A Meta le van sus idiomas (es, en); la respuesta trae la
/// cultura del catálogo de ese idioma (CC24), la predeterminada de la instalación si es de ese idioma, y uno sin cultura no se lista.
/// </summary>
internal sealed class WhatsAppTemplateResponseBuilder(IWhatsAppSystemTemplates system, IWhatsAppTemplateReader reader,
    ICultureCatalog cultures, TimeProvider clock)
{
    internal static WhatsAppTemplateResponse Response(WhatsAppTemplateRow row) => new(row.Id, row.Name, row.Category,
        [new WhatsAppTemplateLanguageResponse(row.Language, row.Category)], row.Body,
        [.. row.Variables.Select(variable => new WhatsAppTemplateVariableResponse(variable.Label, variable.Example))], row.Status,
        Reason(row.RejectionReason), IsSystem: false, row.UpdatedAtUtc, row.Version, Title: null);

    internal async Task<IReadOnlyList<WhatsAppTemplateResponse>> SystemAsync(CancellationToken ct)
    {
        var templates = system.List();
        var platform = await reader.ListPlatformStatesAsync([.. templates.Select(template => template.MetaName)], ct);
        var catalog = (await cultures.ListAsync(ct)).Where(entry => entry.IsEnabled)
            .OrderByDescending(entry => entry.IsDefault).ThenBy(entry => entry.SortOrder ?? int.MaxValue).ThenBy(entry => entry.Code, StringComparer.Ordinal)
            .GroupBy(entry => entry.LanguageCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().Code, StringComparer.OrdinalIgnoreCase);
        var language = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        return [.. templates.Select(template =>
        {
            var languages = template.Languages.OrderBy(value => Neutral(value.Culture) == language ? 0 : 1).ToList();
            var states = languages.Select(value => platform.States.FirstOrDefault(state => state.MetaName == template.MetaName
                && state.MetaLanguage == WhatsAppTemplateBase.ToMetaLanguage(value.Culture))).ToList();
            var shown = languages.Select((value, index) => catalog.TryGetValue(Neutral(value.Culture), out var code)
                    ? new WhatsAppTemplateLanguageResponse(code, states[index]?.Category ?? value.Category) : null)
                .OfType<WhatsAppTemplateLanguageResponse>().ToList();
            if (shown.Count == 0) return (WhatsAppTemplateResponse?)null;
            var known = states.OfType<WhatsAppTemplateStateRow>().ToList();
            var status = known.Any(state => state.Status == WhatsAppTemplateStatus.Rejected) ? WhatsAppTemplateStatus.Rejected
                : known.Any(state => state.Status == WhatsAppTemplateStatus.Paused) ? WhatsAppTemplateStatus.Paused
                : known.Count == languages.Count && known.All(state => state.Status == WhatsAppTemplateStatus.Approved) ? WhatsAppTemplateStatus.Approved
                : WhatsAppTemplateStatus.Pending;
            var culture = languages[0].Culture;
            return new WhatsAppTemplateResponse(SystemId(template.Key), template.MetaName, shown[0].Category, shown,
                WhatsAppTexts.Get("Template." + template.Key + ".Body", culture),
                [.. template.Variables.Select(name => new WhatsAppTemplateVariableResponse(WhatsAppTexts.Get("Template.Variable." + name + ".Label", culture),
                    WhatsAppTexts.Get("Template.Variable." + name + ".Example", culture)))],
                status, Reason(known.FirstOrDefault(state => state.Status == WhatsAppTemplateStatus.Rejected)?.RejectionReason), IsSystem: true,
                known.Count > 0 ? known.Max(state => state.UpdatedAtUtc) : platform.ConnectedAtUtc ?? clock.GetUtcNow().UtcDateTime, 0,
                WhatsAppTexts.Get("Template." + template.Key + ".Title"));
        }).OfType<WhatsAppTemplateResponse>()];
    }

    /// <summary>Una plantilla para elegir al mandar: sus datos por nombre y lo que cuesta un mensaje.</summary>
    internal static WhatsAppTemplateOptionResponse Option(WhatsAppTemplateRow template, ArquitecturaBaseMultitenant.Domain.ValueObjects.Money cost) =>
        new(template.Id, template.Name, template.Category, template.Body, [.. template.Variables.Select(variable => variable.Label)], cost);

    /// <summary>El identificador fijo de una plantilla del sistema: sale de su clave, no de una fila.</summary>
    internal static Guid SystemId(string key) => new(SHA256.HashData(Encoding.UTF8.GetBytes("whatsapp-system-template:" + key)).AsSpan(0, 16));

    private static string? Reason(string? code) =>
        code is null ? null : WhatsAppTexts.Find("Template.Rejection." + code) ?? WhatsAppTexts.Get("Template.Rejection.Other");

    private static string Neutral(string culture) => culture.Split('-')[0];
}
