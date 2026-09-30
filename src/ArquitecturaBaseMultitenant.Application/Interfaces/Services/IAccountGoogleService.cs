using ArquitecturaBaseMultitenant.Application.Models.Identity;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Services;

public interface IAccountGoogleService
{
    Task<Result> LinkAsync(LinkGoogleRequest request, CancellationToken cancellationToken);
}
