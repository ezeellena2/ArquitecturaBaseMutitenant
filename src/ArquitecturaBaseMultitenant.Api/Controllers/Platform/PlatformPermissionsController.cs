using ArquitecturaBaseMultitenant.Api.Authorization;
using ArquitecturaBaseMultitenant.Api.ErrorHandling;
using ArquitecturaBaseMultitenant.Api.OpenApi;
using ArquitecturaBaseMultitenant.Api.Tenancy;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Platform;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Users;
using Microsoft.AspNetCore.Mvc;

namespace ArquitecturaBaseMultitenant.Api.Controllers.Platform;

/// <summary>
/// El catálogo de permisos de un operador (decisiones 260 y 269), con el permiso de la pantalla desde donde se invita y se
/// edita a un operador. El front toma de acá solo <c>includes</c> y <c>grantable</c>.
/// </summary>
[ApiController]
[Route("api/platform/permissions")]
[Tags("Operadores de Plataforma")]
[Access(Access.Platform)]
public sealed class PlatformPermissionsController(IPlatformOperatorService service) : ControllerBase
{
    [HttpGet]
    [HasPlatformPermission(PlatformPermissions.Accounts.Read)]
    [ProducesResponseType<IReadOnlyList<PlatformPermissionItem>>(StatusCodes.Status200OK)]
    [ProducesProblem(StatusCodes.Status401Unauthorized)]
    [ProducesProblem(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> List(CancellationToken ct) =>
        (await service.GetPermissionCatalogAsync(ct)).ToActionResult(this);
}
