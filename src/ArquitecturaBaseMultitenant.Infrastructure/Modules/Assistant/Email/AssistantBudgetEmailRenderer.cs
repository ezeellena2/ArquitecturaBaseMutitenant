using System.Net;
using ArquitecturaBaseMultitenant.Application.Common.Formatting;
using ArquitecturaBaseMultitenant.Application.Models.Messaging;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Resources;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant.Email;

internal sealed class AssistantBudgetEmailRenderer(DisplayFormatter formatter) : IAssistantBudgetEmailRenderer
{
    private static readonly string Template = ReadTemplate();
    public async Task<EmailMessage> RenderAsync(string recipient, int threshold, Money used, Money budget, string culture, string zone, CancellationToken ct)
    {
        var context = await formatter.CreateAsync(culture, zone, ct);
        var subject = AssistantTexts.GetForCulture("Budget.AlertSubject", System.Globalization.CultureInfo.GetCultureInfo(context.Culture));
        var template = AssistantTexts.GetForCulture("Budget.AlertBody", System.Globalization.CultureInfo.GetCultureInfo(context.Culture));
        var body = template.Replace("{0}", formatter.FormatDecimal(threshold, 0, context), StringComparison.Ordinal)
            .Replace("{1}", await formatter.FormatMoneyAsync(used, context, ct), StringComparison.Ordinal)
            .Replace("{2}", await formatter.FormatMoneyAsync(budget, context, ct), StringComparison.Ordinal);
        return new(recipient, subject, Template.Replace("{{Language}}", WebUtility.HtmlEncode(System.Globalization.CultureInfo.GetCultureInfo(context.Culture).TwoLetterISOLanguageName), StringComparison.Ordinal)
            .Replace("{{Subject}}", WebUtility.HtmlEncode(subject), StringComparison.Ordinal)
            .Replace("{{Body}}", WebUtility.HtmlEncode(body), StringComparison.Ordinal), body);
    }
    private static string ReadTemplate()
    {
        using var stream = typeof(AssistantBudgetEmailRenderer).Assembly.GetManifestResourceStream("AssistantEmail.BudgetAlert.html")
            ?? throw new InvalidOperationException("The installed assistant email template is missing.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
