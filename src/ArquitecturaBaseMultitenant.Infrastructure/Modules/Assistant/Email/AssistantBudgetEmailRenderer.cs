using ArquitecturaBaseMultitenant.Application.Common.Formatting;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using ArquitecturaBaseMultitenant.Application.Interfaces.ReferenceData;
using ArquitecturaBaseMultitenant.Application.Models.Messaging;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Resources;
using ArquitecturaBaseMultitenant.Application.Resources;
using ArquitecturaBaseMultitenant.Domain.Assistant;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant.Email;

/// <summary>
/// Tablero Mensajes, «Asistente: 80% del tope» y «… tope alcanzado», con su clave o la de la plataforma: los textos en la
/// cultura de quien lo recibe, los montos, el mes (como su rango, 205) y el día en que vuelve a responder formateados por
/// DisplayFormatter, y el armado común de avisos del núcleo.
/// </summary>
internal sealed class AssistantBudgetEmailRenderer(DisplayFormatter formatter, ICultureCatalog cultures, IEmailTemplateRenderer emails)
    : IAssistantBudgetEmailRenderer
{
    public async Task<EmailMessage> RenderAsync(AssistantBudgetAlertNotice notice, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(notice);
        var context = await formatter.CreateAsync(notice.Culture, notice.TimeZoneId, ct);
        var profile = await new CultureProfiles(cultures).LoadAsync(context.Culture, ct);
        var used = await formatter.FormatMoneyAsync(notice.Used, context, ct);
        var budget = await formatter.FormatMoneyAsync(notice.Budget, context, ct);
        var month = formatter.FormatMonthRange(notice.Month, context, MonthRangeStyle.Sentence);
        // El mes es civil y el tope vuelve a cero el primer día del siguiente.
        var resumes = formatter.FormatDate(notice.Month.AddMonths(1), context);
        var percent = formatter.FormatPercent(notice.Threshold / 100m, context);
        var organization = notice.OrganizationName;
        var ownKey = notice.Funding == AssistantKeyFunding.OwnKey;
        var reached = notice.Threshold >= AssistantBudgetAlert.ReachedThreshold;
        var key = "Budget.Alert." + (ownKey ? "OwnKey" : "PlatformKey") + (reached ? ".Reached" : ".Near");
        var greeting = string.IsNullOrWhiteSpace(notice.RecipientName)
            ? NotificationTexts.Get("Greeting.Unnamed", profile)
            : NotificationTexts.Format("Greeting.Named", profile, notice.RecipientName);
        // El de la plataforma va al correo de soporte, sin saludo; el tope alcanzado de la plataforma cabe en un párrafo.
        string[] paragraphs = (ownKey, reached) switch
        {
            (true, false) => [AssistantTexts.Format(key + ".Body", profile, greeting, organization, used, budget, month),
                AssistantTexts.Format(key + ".After", profile, resumes)],
            (true, true) => [AssistantTexts.Format(key + ".Body", profile, greeting, organization, budget, month),
                AssistantTexts.Format(key + ".After", profile, resumes)],
            (false, false) => [AssistantTexts.Format(key + ".Body", profile, organization, used, budget, month),
                AssistantTexts.Format(key + ".After", profile, resumes)],
            (false, true) => [AssistantTexts.Format(key + ".Body", profile, organization, budget, month, resumes)],
        };
        var action = AssistantBudgetAlertNotice.LeadsToSettings(notice.Funding, notice.Threshold)
            ? "Budget.Alert.ReviewSettings" : "Budget.Alert.ViewUsage";
        return await emails.RenderNoticeAsync(notice.Recipient, new EmailNotice(AssistantTexts.Format(key + ".Subject", profile, organization, percent),
            AssistantTexts.Format(key + ".Title", profile, organization, percent), paragraphs)
        {
            Details =
            [
                new(AssistantTexts.Get("Budget.Alert.Organization", profile), organization),
                new(AssistantTexts.Get("Budget.Alert.Used", profile), AssistantTexts.Format("Budget.Alert.UsedOf", profile, used, budget)),
                new(AssistantTexts.Get("Budget.Alert.Month", profile), formatter.FormatMonthRange(notice.Month, context, MonthRangeStyle.Label)),
            ],
            Action = new(AssistantTexts.Get(action, profile), notice.ActionUrl),
        }, profile, ct);
    }
}
