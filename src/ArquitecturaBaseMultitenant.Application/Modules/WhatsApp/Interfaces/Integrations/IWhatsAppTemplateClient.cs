using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Integrations;

/// <summary>
/// Las plantillas de una cuenta de WhatsApp en la Graph API. Se llama fuera de la transacción; un nombre que Meta ya tiene en
/// ese idioma sale como <c>WhatsApp.Template.NameTaken</c> y lo demás como <c>WhatsApp.Number.MetaRejected</c>, sin el texto de Meta.
/// </summary>
public interface IWhatsAppTemplateClient
{
    Task<Result<WhatsAppMetaTemplate>> CreateAsync(string wabaId, string token, WhatsAppTemplateSubmission submission, CancellationToken ct);
    /// <summary>Borra el nombre en la cuenta, en todos sus idiomas.</summary>
    Task<Result> DeleteAsync(string wabaId, string token, string metaName, CancellationToken ct);
    Task<Result<IReadOnlyList<WhatsAppMetaTemplate>>> ListAsync(string wabaId, string token, CancellationToken ct);
    /// <summary>
    /// A cuántas personas distintas se puede escribir por plantilla en 24 horas (<c>whatsapp_business_manager_messaging_limit</c>,
    /// de toda la cartera de Meta); null si no tiene tope.
    /// </summary>
    Task<Result<int?>> GetMessagingLimitAsync(string phoneNumberId, string token, CancellationToken ct);
}

/// <summary>Lo que se manda a Meta: datos posicionales con un ejemplo cada uno y, en Marketing, el botón «No recibir más».</summary>
public sealed record WhatsAppTemplateSubmission(string MetaName, string MetaLanguage, WhatsAppTemplateCategory Category, string Body,
    IReadOnlyList<string> Examples, string? UnsubscribeButton);

/// <summary>Lo que Meta dice de una plantilla; null en lo que no informó. El motivo es su código (INVALID_FORMAT).</summary>
public sealed record WhatsAppMetaTemplate(string? MetaTemplateId, string MetaName, string MetaLanguage, WhatsAppTemplateStatus? Status,
    WhatsAppTemplateCategory? Category, string? RejectionReason, bool Deleted = false);
