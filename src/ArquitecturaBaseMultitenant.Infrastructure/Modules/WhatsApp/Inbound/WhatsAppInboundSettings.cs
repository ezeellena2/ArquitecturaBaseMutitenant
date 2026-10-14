using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Integrations;
using Microsoft.Extensions.Options;
namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp.Inbound;
internal sealed class WhatsAppInboundSettings(IOptions<WhatsAppOptions> options, IWhatsAppChannelSettings channel) : IWhatsAppInboundSettings
{
    public bool IsEnabled => channel.IsAttending;
    public TimeSpan LeaseLifetime => TimeSpan.FromSeconds(options.Value.InboundLeaseSeconds);
    public TimeSpan RetryDelay => TimeSpan.FromSeconds(options.Value.RetryDelaySeconds);
    public int MaxAttempts => options.Value.InboundMaxAttempts;
}
