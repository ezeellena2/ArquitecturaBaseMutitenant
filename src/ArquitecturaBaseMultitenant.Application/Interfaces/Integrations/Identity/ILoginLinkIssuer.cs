using ArquitecturaBaseMultitenant.Application.Models.Auth;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;

/// <summary>Emite dentro de la UoW del llamador, ligado al método Phone verificado; jamás a un operador.</summary>
public interface ILoginLinkIssuer
{
    Task<Result<IssuedLoginLink>> IssueAsync(Guid userId, Guid methodId, CancellationToken ct);
    Task<Result> InvalidateForMethodAsync(Guid userId, Guid methodId, CancellationToken ct);
}
