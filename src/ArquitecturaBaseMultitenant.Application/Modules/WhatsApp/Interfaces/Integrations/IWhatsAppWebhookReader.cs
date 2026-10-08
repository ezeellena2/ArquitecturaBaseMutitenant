using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Domain.Results;
namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Integrations;
public interface IWhatsAppWebhookReader { Result<WhatsAppWebhookBatch> Read(ReadOnlyMemory<byte> body); }
