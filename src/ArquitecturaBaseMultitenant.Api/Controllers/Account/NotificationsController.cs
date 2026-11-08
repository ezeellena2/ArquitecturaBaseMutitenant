using ArquitecturaBaseMultitenant.Api.ErrorHandling;
using ArquitecturaBaseMultitenant.Api.OpenApi;
using ArquitecturaBaseMultitenant.Api.Tenancy;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Notifications;
using ArquitecturaBaseMultitenant.Domain.Users;
using Microsoft.AspNetCore.Mvc;

namespace ArquitecturaBaseMultitenant.Api.Controllers.Account;

/// <summary>
/// La campana (base de la 12), en los tres accesos y sin permiso propio: cada fuente aplica los suyos. Cuenta en las cuotas
/// generales; marcar leído no lleva idempotencia.
/// </summary>
[ApiController]
[Route("api/notifications")]
[Tags("Account")]
[Access(Access.Consumer, Access.Business, Access.Platform)]
public sealed class NotificationsController(INotificationService service) : ControllerBase
{
    /// <summary>Los avisos de quien mira, hasta 50 y del más nuevo al más viejo, con cuántos están sin leer.</summary>
    [HttpGet]
    [ProducesResponseType<NotificationListResponse>(StatusCodes.Status200OK)]
    [ProducesProblem(StatusCodes.Status401Unauthorized)]
    [ProducesProblem(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> List(CancellationToken cancellationToken) =>
        (await service.ListAsync(cancellationToken)).ToActionResult(this);

    /// <summary>
    /// Marca leído un aviso visible de quien mira, sin cuerpo. Un id que no corresponde a uno se ignora y responde 204 igual.
    /// </summary>
    [HttpPost("{id}/read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesProblem(StatusCodes.Status401Unauthorized)]
    [ProducesProblem(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> MarkRead(string id, CancellationToken cancellationToken) =>
        (await service.MarkReadAsync(id, cancellationToken)).ToActionResult(this);
}
