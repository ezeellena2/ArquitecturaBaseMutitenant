namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Integrations;
public interface IWhatsAppInboundSettings
{
    bool IsEnabled { get; }
    TimeSpan LeaseLifetime { get; }
    TimeSpan RetryDelay { get; }
    int MaxAttempts { get; }
    /// <summary>Cuánto espera una entrada de una organización con el módulo apagado: no se procesa ni se descarta.</summary>
    TimeSpan PostponeDelay => TimeSpan.FromMinutes(15);
}
