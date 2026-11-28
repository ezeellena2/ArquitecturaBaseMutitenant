using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;

public sealed record WhatsAppWebhookBatch(IReadOnlyList<WhatsAppWebhookMessage> Messages, IReadOnlyList<WhatsAppWebhookStatus> Statuses,
    IReadOnlyList<WhatsAppWebhookTemplate>? Templates = null, IReadOnlyList<WhatsAppWebhookEcho>? Echoes = null)
{
    public IReadOnlyList<WhatsAppWebhookTemplate> TemplateUpdates => Templates ?? [];
    public IReadOnlyList<WhatsAppWebhookEcho> MessageEchoes => Echoes ?? [];
    public override string ToString() => nameof(WhatsAppWebhookBatch);
}
/// <summary>Un cambio de estado o de categoría de una plantilla en una cuenta de WhatsApp (el id de la entrada).</summary>
public sealed record WhatsAppWebhookTemplate(string WabaId, WhatsAppMetaTemplate Update);
/// <summary>Un mensaje recibido, con el nombre del perfil de WhatsApp del remitente si Meta lo mandó (C14 de la E12).</summary>
public sealed record WhatsAppWebhookMessage(string PhoneNumberId, string Wamid, string Sender, WhatsAppMessageKind Kind,
    string? Text, string? ReplyId, DateTime OccurredAtUtc, string? ContentType = null, string? ProfileName = null)
{
    public override string ToString() => nameof(WhatsAppWebhookMessage);
}
/// <summary>Un mensaje enviado desde la app WhatsApp Business del celular en coexistencia (<c>smb_message_echoes</c>, B10).</summary>
public sealed record WhatsAppWebhookEcho(string PhoneNumberId, string Wamid, string Recipient, WhatsAppMessageKind Kind,
    string? Text, DateTime OccurredAtUtc, string? ContentType = null)
{
    public override string ToString() => nameof(WhatsAppWebhookEcho);
}
/// <summary>Un estado de entrega con el precio que informó Meta (decisión 8) y, si falló, su código.</summary>
public sealed record WhatsAppWebhookStatus(string PhoneNumberId, string Wamid, WhatsAppMessageStatus Status, DateTime OccurredAtUtc,
    string? PricingCategory = null, string? PricingType = null, int? ErrorCode = null)
{
    public override string ToString() => nameof(WhatsAppWebhookStatus);
}
