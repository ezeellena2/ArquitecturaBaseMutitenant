using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Services;

/// <summary>Plantillas de Meta de la organización o de Plataforma («Para consultas» y «Del sistema»), según el acceso.</summary>
public interface IWhatsAppTemplateService
{
    Task<Result<PagedResult<WhatsAppTemplateResponse>>> ListAsync(WhatsAppTemplateListRequest request, CancellationToken cancellationToken);
    Task<Result<WhatsAppTemplateResponse>> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<Result<WhatsAppTemplateResponse>> CreateAsync(CreateWhatsAppTemplateRequest request, CancellationToken cancellationToken);
    Task<Result<WhatsAppTemplateResponse>> DuplicateAsync(Guid id, CancellationToken cancellationToken);
    Task<Result> DeleteAsync(Guid id, DeleteWhatsAppTemplateRequest request, CancellationToken cancellationToken);
}
