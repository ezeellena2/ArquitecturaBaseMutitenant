using ArquitecturaBaseMultitenant.Application.Common.Formatting;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Resources;
using Microsoft.Extensions.Options;
namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp;
internal sealed record WhatsAppTemplateDefinition(string Key, string Name, int BodyParameters, bool HasUrlButton = false, bool IsAuthentication = false);
internal sealed class WhatsAppTemplateCatalog(IOptions<WhatsAppOptions> options)
{
    public IReadOnlyList<WhatsAppTemplateDefinition> Definitions => Build(options.Value.Templates);
    public WhatsAppTemplateDefinition Get(string key) => Definitions.Single(value => value.Key == key);
    public static string Body(string key, CultureProfile culture) => WhatsAppTexts.Get("Template." + key + ".Body", culture);
    internal static IReadOnlyList<WhatsAppTemplateDefinition> Build(WhatsAppTemplateOptions templates) =>
    [
        new("LoginCode", templates.LoginCode, 1, IsAuthentication: true),
        new("Invitation", templates.Invitation, 3, HasUrlButton: true),
        new("LoginMethodChanged", templates.LoginMethodChanged, 3),
        new("ReviewLoginMethods", templates.ReviewLoginMethods, 1),
        new("DeletionRequested", templates.DeletionRequested, 1),
        new("DeletionCancelled", templates.DeletionCancelled, 0),
        new("AccountDeleted", templates.AccountDeleted, 0),
        new("PlatformDeletionRequested", templates.PlatformDeletionRequested, 1),
        new("SuspendedDeletionRequested", templates.SuspendedDeletionRequested, 2),
        new("RecoveryReceived", templates.RecoveryReceived, 0),
        new("RecoveryApproved", templates.RecoveryApproved, 0, HasUrlButton: true),
        new("RecoveryRejected", templates.RecoveryRejected, 0),
        new("RecoveryRequested", templates.RecoveryRequested, 1),
        new("OrganizationApproved", templates.OrganizationApproved, 1),
        new("OrganizationRejected", templates.OrganizationRejected, 1),
        new("PublicPageModerated", templates.PublicPageModerated, 2),
    ];
}
