using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Services;

/// <summary>Autoriza y confirma el evento previo a entrar al alcance privado de una organización.</summary>
public interface IPlatformScopeAccessService
{
    Task<Result<Guid>> AuthorizeAsync(Guid tenantId, string reason, CancellationToken cancellationToken);
}
