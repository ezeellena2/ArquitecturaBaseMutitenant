using ArquitecturaBaseMultitenant.Api.Authorization;
using ArquitecturaBaseMultitenant.Api.ErrorHandling;
using ArquitecturaBaseMultitenant.Api.Features;
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

/// <summary>La configuración de WhatsApp de la organización (decisión 272): el plazo de sus chats, con su versión.</summary>
[ApiController]
[Route("api/organization/whatsapp/settings")]
[Tags("WhatsApp Business management")]
[Access(Access.Business)]
[FeatureGate(WhatsAppFeature.Key)]
public sealed class OrganizationWhatsAppSettingsController(IWhatsAppSettingsService service) : ControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.Settings.Read)]
    [ProducesResponseType<WhatsAppOrganizationSettingsResponse>(200)]
    [ProducesProblem(401)]
    [ProducesProblem(403)]
    [ProducesProblem(500)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken) =>
        (await service.GetAsync(cancellationToken)).ToActionResult(this);

    [HttpPut]
    [HasPermission(Permissions.Settings.Manage)]
    [ProducesResponseType<VersionResponse>(200)]
    [ProducesProblem(400)]
    [ProducesProblem(401)]
    [ProducesProblem(403)]
    [ProducesProblem(409)]
    [ProducesProblem(500)]
    public async Task<IActionResult> Update([FromBody] UpdateWhatsAppOrganizationSettingsHttpRequest request, CancellationToken cancellationToken) =>
        (await service.UpdateAsync(new UpdateWhatsAppOrganizationSettingsRequest(request.ChatRetentionDays, request.Version), cancellationToken))
        .ToActionResult(this);
}

/// <summary>La configuración de WhatsApp de la plataforma (decisiones 270 y 272): los países que reciben códigos y los plazos.</summary>
[ApiController]
[Route("api/platform/whatsapp/settings")]
[Tags("WhatsApp Platform management")]
[Access(Access.Platform)]
[InstalledModuleRoute(WhatsAppApiModule.ManagementRoute, InstalledModuleRouteCategory.PlatformManagement)]
[HasPlatformPermission(PlatformPermissions.Settings.Manage)]
public sealed class PlatformWhatsAppSettingsController(IPlatformWhatsAppSettingsService service) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PlatformWhatsAppSettingsResponse>(200)]
    [ProducesProblem(401)]
    [ProducesProblem(403)]
    [ProducesProblem(500)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken) =>
        (await service.GetAsync(cancellationToken)).ToActionResult(this);

    [HttpPut]
    [ProducesResponseType<VersionResponse>(200)]
    [ProducesProblem(400)]
    [ProducesProblem(401)]
    [ProducesProblem(403)]
    [ProducesProblem(409)]
    [ProducesProblem(500)]
    public async Task<IActionResult> Update([FromBody] UpdatePlatformWhatsAppSettingsHttpRequest request, CancellationToken cancellationToken) =>
        (await service.UpdateAsync(new UpdatePlatformWhatsAppSettingsRequest(request.AllowedCountries, request.ChatRetentionDays,
            request.MaxOrganizationChatRetentionDays, request.Version), cancellationToken)).ToActionResult(this);
}
