using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Services;

/// <summary>Equipos de la organización o de Plataforma, según el acceso de la sesión.</summary>
public interface IWhatsAppTeamService
{
    Task<Result<PagedResult<WhatsAppTeamResponse>>> ListAsync(WhatsAppTeamListRequest request, CancellationToken cancellationToken);
    Task<Result<WhatsAppTeamDetailResponse>> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<Result<WhatsAppTeamResponse>> CreateAsync(CreateWhatsAppTeamRequest request, CancellationToken cancellationToken);
    Task<Result<WhatsAppTeamResponse>> UpdateAsync(Guid id, UpdateWhatsAppTeamRequest request, CancellationToken cancellationToken);
    Task<Result> DeleteAsync(Guid id, DeleteWhatsAppTeamRequest request, CancellationToken cancellationToken);
    /// <summary>Organización: miembros activos (memberId). Plataforma: operadores (accountId).</summary>
    Task<Result<PagedResult<WhatsAppReferenceResponse>>> ListMemberOptionsAsync(WhatsAppTeamMemberOptionListRequest request, CancellationToken cancellationToken);
}
