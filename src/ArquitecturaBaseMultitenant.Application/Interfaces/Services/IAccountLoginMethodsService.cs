using ArquitecturaBaseMultitenant.Application.Models.Identity;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Services;

public interface IAccountLoginMethodsService
{
    Task<Result<AccountLoginMethodsResponse>> ListAsync(CancellationToken cancellationToken);
}
