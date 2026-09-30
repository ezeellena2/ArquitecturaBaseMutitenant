using ArquitecturaBaseMultitenant.Application.Models.Legal;
using ArquitecturaBaseMultitenant.Domain.Legal;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Services;

/// <summary>Entrega el contenido público de la versión legal vigente en la cultura solicitada.</summary>
public interface ILegalService
{
    Task<Result<LegalDocumentRow>> GetCurrentAsync(LegalDocumentKind kind,
        string? culture, CancellationToken cancellationToken);
}
