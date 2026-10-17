using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Services;

/// <summary>
/// Arranque del número de Plataforma desde la configuración de la instalación (decisión 3 de la E11): si no hay número
/// de Plataforma, lo crea; uno que salió de la configuración la sigue; uno conectado por el alta manda.
/// </summary>
public interface IWhatsAppChannelBootstrapService
{
    Task<Result> EnsurePlatformAsync(WhatsAppPlatformConfiguration configuration, CancellationToken cancellationToken);
}

public sealed record WhatsAppPlatformConfiguration(string? PhoneNumberId, string? WabaId, string? DisplayPhoneNumber, string? AccessToken)
{
    public override string ToString() => nameof(WhatsAppPlatformConfiguration);
}
