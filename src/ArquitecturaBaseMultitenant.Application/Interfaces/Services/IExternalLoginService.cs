using ArquitecturaBaseMultitenant.Application.Models.Auth;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Services;

/// <summary>Completa el callback de Google según la puerta que inició el flujo.</summary>
public interface IExternalLoginService
{
    Task<Result<ExternalSignInResponse>> SignInAsync(
        ExternalSignInRequest request, CancellationToken cancellationToken);
}
