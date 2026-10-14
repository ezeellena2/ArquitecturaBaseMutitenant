using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Services;

/// <summary>Las plantillas de Plataforma: «Para consultas», que se crean y se borran, y las 16 «Del sistema», solo de lectura.</summary>
public interface IPlatformWhatsAppTemplateService
{
    Task<Result<WhatsAppTemplateListResponse>> ListAsync(WhatsAppTemplateListRequest request, CancellationToken cancellationToken);
    Task<Result<WhatsAppTemplateResponse>> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<Result<WhatsAppTemplateResponse>> CreateAsync(CreateWhatsAppTemplateRequest request, CancellationToken cancellationToken);
    Task<Result> DeleteAsync(Guid id, DeleteWhatsAppTemplateRequest request, CancellationToken cancellationToken);
}
