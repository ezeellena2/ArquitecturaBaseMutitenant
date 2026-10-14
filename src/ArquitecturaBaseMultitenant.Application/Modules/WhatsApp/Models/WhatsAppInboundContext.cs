using ArquitecturaBaseMultitenant.Application.Models.Identity;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
/// <summary>Propiedad original revalidada bajo Phone, cuenta, contacto y mensaje. Nunca representa al dueño posterior.</summary>
public sealed record WhatsAppInboundContext(WhatsAppMessage Message, WhatsAppContact? Contact,
    UserAccountRow? OriginalAccount, LoginMethod? OriginalMethod, bool OriginalMethodAvailable, WhatsAppInboundChannel? Channel = null)
{
    public override string ToString() => nameof(WhatsAppInboundContext);
}
/// <summary>Una entrada reclamada; con organización, se procesa dentro de su alcance (decisión 4 de la E11).</summary>
public sealed record WhatsAppInboundClaim(Guid MessageId, Guid LeaseId, Guid? OrganizationId = null);
/// <summary>El número que recibió la entrada; la organización es null en el de Plataforma.</summary>
public sealed record WhatsAppInboundChannel(Guid Id, Guid? OrganizationId, string PhoneNumberId, WhatsAppNumberStatus Status, bool ConsultationsEnabled);
public sealed record WhatsAppInboundHint(Guid MessageId, string? WaId, Guid? OriginalUserId, Guid? OriginalMethodId)
{
    public override string ToString() => nameof(WhatsAppInboundHint);
}
