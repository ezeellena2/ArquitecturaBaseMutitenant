using ArquitecturaBaseMultitenant.Application.Models.Auth;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Services;

public interface IAccountService
{
    Task<Result<RequestLoginCodeResponse>> RequestSignupCodeAsync(SignupRequest request,
        CancellationToken cancellationToken);

    Task<Result> VerifySignupAsync(VerifySignupRequest request, CancellationToken cancellationToken);
}
