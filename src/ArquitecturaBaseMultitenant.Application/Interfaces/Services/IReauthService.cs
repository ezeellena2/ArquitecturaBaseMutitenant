using ArquitecturaBaseMultitenant.Application.Models.Identity;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Services;

public interface IReauthService
{
    Task<Result<ReauthCodeResponse>> RequestAsync(RequestReauthRequest request, CancellationToken cancellationToken);
    Task<Result<ReauthResponse>> VerifyAsync(VerifyReauthRequest request, CancellationToken cancellationToken);
}
