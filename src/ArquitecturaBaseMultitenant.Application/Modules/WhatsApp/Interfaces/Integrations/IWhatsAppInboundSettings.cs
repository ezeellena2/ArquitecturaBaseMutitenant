namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Integrations;
public interface IWhatsAppInboundSettings
{
    bool IsEnabled { get; }
    TimeSpan LeaseLifetime { get; }
    TimeSpan RetryDelay { get; }
    int MaxAttempts { get; }
}
