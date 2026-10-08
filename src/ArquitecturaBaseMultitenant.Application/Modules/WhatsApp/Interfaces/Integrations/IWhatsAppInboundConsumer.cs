using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Integrations;
/// <summary>T07 agrega efectos y respuesta dentro de la transacción y los locks del Process. No abre UoW ni vuelve a ordenar locks.</summary>
public interface IWhatsAppInboundConsumer
{
    Task ConsumeAsync(WhatsAppInboundContext context, CancellationToken ct);
}
