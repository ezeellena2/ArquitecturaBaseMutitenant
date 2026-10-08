using ArquitecturaBaseMultitenant.Application.Models.Identity;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
/// <summary>Propiedad original revalidada bajo Phone, cuenta, contacto y mensaje. Nunca representa al dueño posterior.</summary>
public sealed record WhatsAppInboundContext(WhatsAppMessage Message, WhatsAppContact? Contact,
    UserAccountRow? OriginalAccount, LoginMethod? OriginalMethod, bool OriginalMethodAvailable)
{
    public override string ToString() => nameof(WhatsAppInboundContext);
}
public sealed record WhatsAppInboundClaim(Guid MessageId, Guid LeaseId);
public sealed record WhatsAppInboundHint(Guid MessageId, string? WaId, Guid? OriginalUserId, Guid? OriginalMethodId)
{
    public override string ToString() => nameof(WhatsAppInboundHint);
}
