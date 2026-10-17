using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Services;

/// <summary>Las plantillas de Meta de la organización del claim.</summary>
public interface IWhatsAppTemplateService
{
    Task<Result<WhatsAppTemplateListResponse>> ListAsync(WhatsAppTemplateListRequest request, CancellationToken cancellationToken);
    Task<Result<WhatsAppTemplateResponse>> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<Result<WhatsAppTemplateResponse>> CreateAsync(CreateWhatsAppTemplateRequest request, CancellationToken cancellationToken);
    Task<Result> DeleteAsync(Guid id, DeleteWhatsAppTemplateRequest request, CancellationToken cancellationToken);
}
