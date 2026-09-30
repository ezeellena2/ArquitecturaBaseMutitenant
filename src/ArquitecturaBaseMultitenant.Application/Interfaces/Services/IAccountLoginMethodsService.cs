using ArquitecturaBaseMultitenant.Application.Models.Identity;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Services;

/// <summary>Entrega los métodos de ingreso visibles de la cuenta actual para su pantalla de gestión.</summary>
public interface IAccountLoginMethodsService
{
    Task<Result<AccountLoginMethodsResponse>> ListAsync(CancellationToken cancellationToken);
}
