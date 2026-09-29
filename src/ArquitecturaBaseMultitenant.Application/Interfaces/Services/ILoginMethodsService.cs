using ArquitecturaBaseMultitenant.Application.Models.Auth;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Services;

/// <summary>Informa qué canales de ingreso están disponibles en esta instalación.</summary>
public interface ILoginMethodsService
{
    Task<Result<LoginMethodsResponse>> GetLoginMethodsAsync(CancellationToken cancellationToken);
}
