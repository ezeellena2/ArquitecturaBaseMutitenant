using ArquitecturaBaseMultitenant.Application.Models.Identity;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Services;

/// <summary>Vincula una identidad Google verificada con la cuenta que ya inició sesión; el servicio implementa las comprobaciones y el guardado.</summary>
public interface IAccountGoogleService
{
    Task<Result<Guid>> GetLinkUserIdAsync(CancellationToken cancellationToken);
    Task<Result> LinkAsync(LinkGoogleRequest request, CancellationToken cancellationToken);
}
