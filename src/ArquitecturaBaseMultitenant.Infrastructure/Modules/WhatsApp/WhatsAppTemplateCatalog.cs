using ArquitecturaBaseMultitenant.Application.Common.Formatting;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Resources;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using Microsoft.Extensions.Options;
namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp;
/// <summary>Variables son los nombres de los datos ({{1}}, {{2}}…) con sus textos en «Template.Variable.{nombre}».</summary>
internal sealed record WhatsAppTemplateDefinition(string Key, string Name, IReadOnlyList<string> Variables, bool HasUrlButton = false, bool IsAuthentication = false)
{
    public int BodyParameters => Variables.Count;
}
/// <summary>
/// Las 16 plantillas de la E8 (<c>docs/operations/whatsapp-plantillas.md</c>), con el nombre de Meta de la configuración y
/// los idiomas de sus textos. La invitación española es Marketing (decisión del usuario del 12/10, ADR 0038); el código,
/// Authentication; las demás, Utility.
/// </summary>
internal sealed class WhatsAppTemplateCatalog(IOptions<WhatsAppOptions> options) : IWhatsAppSystemTemplates
{
    /// <summary>Los idiomas de los textos de la E8, los mismos que elige el renderizador.</summary>
    internal static readonly IReadOnlyList<string> Languages = ["es", "en"];
    public IReadOnlyList<WhatsAppTemplateDefinition> Definitions => Build(options.Value.Templates);
    public WhatsAppTemplateDefinition Get(string key) => Definitions.Single(value => value.Key == key);
    public static string Body(string key, CultureProfile culture) => WhatsAppTexts.Get("Template." + key + ".Body", culture);
    public IReadOnlyList<WhatsAppSystemTemplate> List() => [.. Definitions.Select(definition => new WhatsAppSystemTemplate(definition.Key, definition.Name,
        definition.Variables, [.. Languages.Select(language => new WhatsAppSystemTemplateLanguage(language, Category(definition, language)))]))];
    private static WhatsAppTemplateCategory Category(WhatsAppTemplateDefinition definition, string language) =>
        definition.IsAuthentication ? WhatsAppTemplateCategory.Authentication
        : definition.Key == "Invitation" && language == Languages[0] ? WhatsAppTemplateCategory.Marketing
        : WhatsAppTemplateCategory.Utility;
    internal static IReadOnlyList<WhatsAppTemplateDefinition> Build(WhatsAppTemplateOptions templates) =>
    [
        new("LoginCode", templates.LoginCode, ["Code"], IsAuthentication: true),
        new("Invitation", templates.Invitation, ["Inviter", "Organization", "Expiration"], HasUrlButton: true),
        new("LoginMethodChanged", templates.LoginMethodChanged, ["Change", "Method", "Date"]),
        new("ReviewLoginMethods", templates.ReviewLoginMethods, ["Organization"]),
        new("DeletionRequested", templates.DeletionRequested, ["DeletionDate"]),
        new("DeletionCancelled", templates.DeletionCancelled, []),
        new("AccountDeleted", templates.AccountDeleted, []),
        new("DeletionRequestedByPlatform", templates.DeletionRequestedByPlatform, ["DeletionDate"]),
        new("DeletionRequestedSuspended", templates.DeletionRequestedSuspended, ["DeletionDate", "Support"]),
        new("RecoveryReceived", templates.RecoveryReceived, []),
        new("RecoveryApproved", templates.RecoveryApproved, [], HasUrlButton: true),
        new("RecoveryRejected", templates.RecoveryRejected, []),
        new("RecoveryRequested", templates.RecoveryRequested, ["Support"]),
        new("OrganizationApproved", templates.OrganizationApproved, ["Organization"]),
        new("OrganizationRejected", templates.OrganizationRejected, ["Organization"]),
        new("PublicPageModerated", templates.PublicPageModerated, ["Organization", "PageStatus"]),
    ];
}
