using ArquitecturaBaseMultitenant.Api.Authorization;
using ArquitecturaBaseMultitenant.Api.ErrorHandling;
using ArquitecturaBaseMultitenant.Api.Idempotency;
using ArquitecturaBaseMultitenant.Api.Modules.WhatsApp.Contracts;
using ArquitecturaBaseMultitenant.Api.OpenApi;
using ArquitecturaBaseMultitenant.Api.Tenancy;
using ArquitecturaBaseMultitenant.Application.Models.Common;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using Microsoft.AspNetCore.Mvc;
using Microsoft.FeatureManagement.Mvc;

namespace ArquitecturaBaseMultitenant.Api.Modules.WhatsApp.Controllers;

/// <summary>«Cargar teléfonos de empleados» en Usuarios: dice «Ya es miembro», «Se invita» o «Número inválido», nunca si hay cuenta.</summary>
[ApiController]
[Route("api/organization/whatsapp/phone-imports")]
[Tags("WhatsApp Business users")]
[Access(Access.Business)]
[FeatureGate(WhatsAppFeature.Key)]
[HasPermission(Permissions.Users.Invite)]
public sealed class OrganizationWhatsAppPhoneImportsController(IWhatsAppPhoneImportService service) : ControllerBase
{
    [HttpPost("preview")]
    [ProducesResponseType<WhatsAppPhoneImportPreviewResponse>(200)]
    [ProducesProblem(400)]
    [ProducesProblem(401)]
    [ProducesProblem(403)]
    [ProducesProblem(404)]
    [ProducesProblem(500)]
    public async Task<IActionResult> Preview([FromBody] WhatsAppPhoneImportHttpRequest request, CancellationToken cancellationToken) =>
        (await service.PreviewAsync(ToModel(request), cancellationToken)).ToActionResult(this);

    [HttpPost]
    [Idempotent]
    [ProducesResponseType<WhatsAppPhoneImportResponse>(200)]
    [ProducesProblem(400)]
    [ProducesProblem(401)]
    [ProducesProblem(403)]
    [ProducesProblem(404)]
    [ProducesProblem(409)]
    [ProducesProblem(422)]
    [ProducesProblem(500)]
    [ProducesProblem(503)]
    public async Task<IActionResult> Import([FromBody] WhatsAppPhoneImportHttpRequest request, CancellationToken cancellationToken) =>
        (await service.ImportAsync(ToModel(request), cancellationToken)).ToActionResult(this);

    private static WhatsAppPhoneImportRequest ToModel(WhatsAppPhoneImportHttpRequest request) =>
        new(request.Phones?.Select(phone => phone is null ? null! : new PhoneInputRequest(phone.Country, phone.Number)).ToArray(), request.CompanyId, request.RoleIds);
}
