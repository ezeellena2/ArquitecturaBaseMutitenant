using ArquitecturaBaseMultitenant.Application.Common.Formatting;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Models.Invitations;
using ArquitecturaBaseMultitenant.Application.Models.Notifications;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Resources;
using Microsoft.Extensions.Options;
namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp;
internal sealed class WhatsAppTemplateRenderer(IOptions<WhatsAppOptions> options, WhatsAppTemplateCatalog catalog,
    DisplayFormatter formatter, IPublicOrigin origin) : IWhatsAppTemplateRenderer
{
    public WhatsAppOutboundMessage RenderCode(string destination, string code, int lifetimeMinutes, CultureProfile culture)
    {
        if (lifetimeMinutes != 10) throw new InvalidOperationException("Authentication template requires a ten-minute code lifetime.");
        return Message(destination, "LoginCode", culture, [code], code);
    }
    public async Task<WhatsAppOutboundMessage> RenderInvitationAsync(string destination, InvitationNotice notice, CultureProfile culture, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var context = await formatter.CreateAsync(culture.Entry.Code, notice.TimeZoneId, ct);
        return Message(destination, "Invitation", culture,
            [notice.InviterName, notice.OrganizationName, formatter.FormatDate(notice.ExpiresAtUtc, context)], UrlSuffix(notice.ActionUrl));
    }
    public async Task<WhatsAppOutboundMessage?> RenderNoticeAsync(string destination, AccountNotice notice, CultureProfile culture, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        WhatsAppOutboundMessage? message = notice switch
        {
            AccountNotice.LoginMethodChanged changed => Message(destination, "LoginMethodChanged", culture,
                await MethodChangeParametersAsync(changed, culture, ct)),
            AccountNotice.ReviewLoginMethods review => Message(destination, "ReviewLoginMethods", culture, [review.OrganizationName]),
            AccountNotice.OrganizationApproved approved => Message(destination, "OrganizationApproved", culture, [approved.OrganizationName]),
            AccountNotice.OrganizationRejected rejected => Message(destination, "OrganizationRejected", culture, [rejected.OrganizationName]),
            AccountNotice.PublicPageModerated moderated => Message(destination, "PublicPageModerated", culture,
                [moderated.OrganizationName, WhatsAppTexts.Get(moderated.IsBlocked ? "Page.Blocked" : "Page.Allowed", culture)]),
            AccountNotice.DeletionRequested deletion => Message(destination,
                !deletion.RequestedByPlatform ? "DeletionRequested" : deletion.CanCancel ? "PlatformDeletionRequested" : "SuspendedDeletionRequested", culture,
                deletion.RequestedByPlatform && !deletion.CanCancel
                    ? [formatter.FormatDate(deletion.ScheduledForUtc, await formatter.CreateAsync(culture.Entry.Code, deletion.TimeZoneId, ct)), Support(deletion.SupportEmail?.DisplayValue, culture)]
                    : [formatter.FormatDate(deletion.ScheduledForUtc, await formatter.CreateAsync(culture.Entry.Code, deletion.TimeZoneId, ct))]),
            AccountNotice.DeletionCancelled => Message(destination, "DeletionCancelled", culture, []),
            AccountNotice.AccountDeleted => Message(destination, "AccountDeleted", culture, []),
            AccountNotice.RecoveryReceived => Message(destination, "RecoveryReceived", culture, []),
            AccountNotice.RecoveryApproved approved => Message(destination, "RecoveryApproved", culture, [], UrlSuffix(approved.ActionUrl)),
            AccountNotice.RecoveryRejected => Message(destination, "RecoveryRejected", culture, []),
            AccountNotice.RecoveryRequested requested => Message(destination, "RecoveryRequested", culture, [Support(requested.SupportEmail?.DisplayValue, culture)]),
            AccountNotice.OperatorInvited => null,
            _ => throw new ArgumentOutOfRangeException(nameof(notice)),
        };
        return message;
    }
    private static string Support(string? email, CultureProfile culture) => email ?? WhatsAppTexts.Get("Support.Contact", culture);
    private async Task<IReadOnlyList<string>> MethodChangeParametersAsync(AccountNotice.LoginMethodChanged changed, CultureProfile culture, CancellationToken ct)
    {
        var context = await formatter.CreateAsync(culture.Entry.Code, changed.TimeZoneId, ct);
        return [WhatsAppTexts.Get("MethodChange." + changed.Change, culture), changed.MaskedMethod,
            WhatsAppTexts.Format("Notice.OccurredAt", culture, formatter.FormatDate(changed.OccurredAtUtc, context), formatter.FormatTime(changed.OccurredAtUtc, context))];
    }
    private WhatsAppOutboundMessage Message(string destination, string key, CultureProfile culture, IReadOnlyList<string> parameters, string? button = null)
    {
        var definition = catalog.Get(key);
        if (parameters.Count != definition.BodyParameters || (definition.HasUrlButton || definition.IsAuthentication) != (button is not null))
            throw new InvalidOperationException("Template parameter contract mismatch.");
        var language = culture.ResourceLanguages.FirstOrDefault(value => value is "es" or "en") ?? "es";
        var message = new WhatsAppOutboundMessage(options.Value.PhoneNumberId ?? throw new InvalidOperationException("WhatsApp unavailable."),
            destination, definition.Name, language, parameters, button, definition.IsAuthentication);
        message.Validate();
        return message;
    }
    private string UrlSuffix(string actionUrl)
    {
        var configured = origin.Value ?? throw new InvalidOperationException("Public origin required.");
        if (!Uri.TryCreate(actionUrl, UriKind.Absolute, out var action) || action.Scheme != configured.Scheme
            || action.Host != configured.Host || action.Port != configured.Port || action.UserInfo.Length != 0)
            throw new ArgumentException("Action URL must use the configured public origin.", nameof(actionUrl));
        return action.PathAndQuery.TrimStart('/') + action.Fragment;
    }
}
