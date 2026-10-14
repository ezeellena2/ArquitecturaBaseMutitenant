using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Resources;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Services;

/// <summary>
/// Manda una plantilla a Meta, una variante por cada cuenta de WhatsApp de los números vigentes (de la organización o de
/// Plataforma), y la borra de ellas. Fuera de la transacción. Sin un número vigente no hay a qué cuenta mandarla (NumberRequired). Una
/// Marketing lleva el botón «No recibir más», en su idioma, para que el contacto se dé de baja.
/// </summary>
internal sealed class WhatsAppTemplateIssuer(IWhatsAppChannelReader channels, IWhatsAppTemplateClient client, IWhatsAppTokenProtector protector)
{
    internal async Task<Result<IReadOnlyList<WhatsAppIssuedTemplate>>> SubmitAsync(bool platform, WhatsAppTemplateBase template, CancellationToken ct)
    {
        var accounts = await channels.ListTemplateAccountsAsync(platform, ct);
        if (accounts.Count == 0) return WhatsAppErrors.TemplateNumberRequired;
        var submission = new WhatsAppTemplateSubmission(template.MetaName, template.MetaLanguage, template.Category, template.Body,
            [.. template.Variables.Select(variable => variable.Example)],
            template.Category == WhatsAppTemplateCategory.Marketing ? WhatsAppTexts.Get("Template.Unsubscribe.Button", template.Language) : null);
        var issued = new List<WhatsAppIssuedTemplate>();
        Error? first = null;
        foreach (var account in accounts)
        {
            var created = await client.CreateAsync(account.WabaId, protector.Unprotect(account.ChannelId, account.EncryptedToken), submission, ct);
            if (created.IsSuccess) issued.Add(new(account.WabaId, created.Value));
            else first ??= created.Error;
        }
        // Basta con que la reciba una cuenta: las demás la tendrán pendiente hasta la próxima sincronización.
        return issued.Count > 0 ? issued : first!;
    }

    /// <summary>La borra de cada cuenta. Si Meta no responde, igual se borra acá: el nombre queda ocupado en Meta.</summary>
    internal async Task<IReadOnlyList<string>> RemoveAsync(bool platform, string metaName, CancellationToken ct)
    {
        var accounts = await channels.ListTemplateAccountsAsync(platform, ct);
        foreach (var account in accounts)
            _ = await client.DeleteAsync(account.WabaId, protector.Unprotect(account.ChannelId, account.EncryptedToken), metaName, ct);
        return [.. accounts.Select(account => account.WabaId)];
    }
}

internal sealed record WhatsAppIssuedTemplate(string WabaId, WhatsAppMetaTemplate Meta);
