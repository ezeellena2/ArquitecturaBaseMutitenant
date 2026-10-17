using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Services;

/// <summary>
/// El correo «Chats esperando» (D6), que corre un trabajo periódico: por organización, dentro de su alcance, y para los
/// equipos de operadores de Plataforma.
/// </summary>
public interface IWhatsAppWaitingNoticeService
{
    /// <summary>La organización del alcance abierto por el trabajo; con el módulo apagado no avisa.</summary>
    Task<Result> NotifyOrganizationAsync(CancellationToken cancellationToken);
    Task<Result> NotifyPlatformAsync(CancellationToken cancellationToken);
}
