using ArquitecturaBaseMultitenant.Api.ErrorHandling;
using ArquitecturaBaseMultitenant.Api.OpenApi;
using ArquitecturaBaseMultitenant.Api.Tenancy;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using Microsoft.AspNetCore.Mvc;
using Microsoft.FeatureManagement.Mvc;

namespace ArquitecturaBaseMultitenant.Api.Modules.WhatsApp.Controllers;

/// <summary>
/// El número junto a «WhatsApp» en el menú. No declara un permiso único: el servicio cuenta solo las bandejas que la
/// persona puede ver (Atender clientes o Atender internos) y devuelve null en las otras.
/// </summary>
[ApiController]
[Route("api/organization/whatsapp/untaken")]
[Tags("WhatsApp Business inboxes")]
[Access(Access.Business)]
[FeatureGate(WhatsAppFeature.Key)]
public sealed class OrganizationWhatsAppUntakenController(IWhatsAppConversationService service) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<WhatsAppUntakenResponse>(200)]
    [ProducesProblem(401)]
    [ProducesProblem(403)]
    [ProducesProblem(404)]
    [ProducesProblem(500)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken) =>
        (await service.GetUntakenAsync(cancellationToken)).ToActionResult(this);
}
