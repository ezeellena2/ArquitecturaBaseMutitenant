using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Services;

/// <summary>«Cargar teléfonos de empleados»: revisa sin revelar cuentas y vincula o invita por WhatsApp.</summary>
public interface IWhatsAppPhoneImportService
{
    Task<Result<WhatsAppPhoneImportPreviewResponse>> PreviewAsync(WhatsAppPhoneImportRequest request, CancellationToken cancellationToken);
    Task<Result<WhatsAppPhoneImportResponse>> ImportAsync(WhatsAppPhoneImportRequest request, CancellationToken cancellationToken);
}
