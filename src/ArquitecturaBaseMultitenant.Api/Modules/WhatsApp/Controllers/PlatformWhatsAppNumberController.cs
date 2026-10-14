using ArquitecturaBaseMultitenant.Api.Authorization;
using ArquitecturaBaseMultitenant.Api.ErrorHandling;
using ArquitecturaBaseMultitenant.Api.Features;
using ArquitecturaBaseMultitenant.Api.Idempotency;
using ArquitecturaBaseMultitenant.Api.Modules.WhatsApp.Contracts;
using ArquitecturaBaseMultitenant.Api.OpenApi;
using ArquitecturaBaseMultitenant.Api.Tenancy;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Users;
using Microsoft.AspNetCore.Mvc;

namespace ArquitecturaBaseMultitenant.Api.Modules.WhatsApp.Controllers;

/// <summary>El número de Plataforma: lo conecta el alta de Meta y atiende consultas además de códigos y avisos.</summary>
[ApiController]
[Route("api/platform/whatsapp")]
[Tags("WhatsApp Platform management")]
[Access(Access.Platform)]
[InstalledModuleRoute(WhatsAppApiModule.ManagementRoute, InstalledModuleRouteCategory.PlatformManagement)]
[HasPlatformPermission(PlatformPermissions.WhatsApp.Manage)]
public sealed class PlatformWhatsAppNumberController(IPlatformWhatsAppNumberService service, IPlatformWhatsAppBotService bots) : ControllerBase
{
    [HttpGet("number")]
    [ProducesResponseType<PlatformWhatsAppNumberResponse>(200)]
    [ProducesProblem(401)]
    [ProducesProblem(403)]
    [ProducesProblem(404)]
    [ProducesProblem(500)]
    public async Task<IActionResult> GetNumber(CancellationToken cancellationToken) =>
        (await service.GetAsync(cancellationToken)).ToActionResult(this);

    [HttpPost("number")]
    [Idempotent]
    [ProducesResponseType<PlatformWhatsAppNumberResponse>(201)]
    [ProducesProblem(400)]
    [ProducesProblem(401)]
    [ProducesProblem(403)]
    [ProducesProblem(404)]
    [ProducesProblem(409)]
    [ProducesProblem(422)]
    [ProducesProblem(500)]
    [ProducesProblem(503)]
    public async Task<IActionResult> ConnectNumber([FromBody] ConnectPlatformWhatsAppNumberHttpRequest request, CancellationToken cancellationToken) =>
        (await service.ConnectAsync(new ConnectPlatformWhatsAppNumberRequest(request.Code, request.PhoneNumberId, request.WabaId, request.Coexistence), cancellationToken))
        .ToCreatedResult(this, nameof(GetNumber), _ => new { });

    [HttpPost("number/check")]
    [ResponseVersion("Number.Version")]
    [ProducesResponseType<PlatformWhatsAppNumberResponse>(200)]
    [ProducesProblem(400)]
    [ProducesProblem(401)]
    [ProducesProblem(403)]
    [ProducesProblem(404)]
    [ProducesProblem(409)]
    [ProducesProblem(500)]
    [ProducesProblem(503)]
    public async Task<IActionResult> CheckNumber([FromBody] CheckWhatsAppNumberHttpRequest request, CancellationToken cancellationToken) =>
        (await service.CheckAsync(new CheckWhatsAppNumberRequest(request.Version), cancellationToken)).ToActionResult(this);

    [HttpPut("number")]
    [ResponseVersion("Number.Version")]
    [ProducesResponseType<PlatformWhatsAppNumberResponse>(200)]
    [ProducesProblem(400)]
    [ProducesProblem(401)]
    [ProducesProblem(403)]
    [ProducesProblem(404)]
    [ProducesProblem(409)]
    [ProducesProblem(500)]
    public async Task<IActionResult> UpdateNumber([FromBody] UpdatePlatformWhatsAppNumberHttpRequest request, CancellationToken cancellationToken) =>
        (await service.UpdateAsync(new UpdatePlatformWhatsAppNumberRequest(request.ConsultationsEnabled, request.Version), cancellationToken)).ToActionResult(this);

    [HttpGet("bot")]
    [ProducesResponseType<PlatformWhatsAppBotResponse>(200)]
    [ProducesProblem(401)]
    [ProducesProblem(403)]
    [ProducesProblem(404)]
    [ProducesProblem(500)]
    public async Task<IActionResult> GetBot(CancellationToken cancellationToken) =>
        (await bots.GetAsync(cancellationToken)).ToActionResult(this);

    [HttpPut("bot")]
    [ResponseVersion("Version")]
    [ProducesResponseType<PlatformWhatsAppBotResponse>(200)]
    [ProducesProblem(400)]
    [ProducesProblem(401)]
    [ProducesProblem(403)]
    [ProducesProblem(404)]
    [ProducesProblem(409)]
    [ProducesProblem(500)]
    public async Task<IActionResult> UpdateBot([FromBody] UpdatePlatformWhatsAppBotHttpRequest request, CancellationToken cancellationToken) =>
        (await bots.UpdateAsync(new UpdatePlatformWhatsAppBotRequest(request.Welcome, request.OutOfHours, request.Schedule, request.Menu, request.Version), cancellationToken))
        .ToActionResult(this);
}
