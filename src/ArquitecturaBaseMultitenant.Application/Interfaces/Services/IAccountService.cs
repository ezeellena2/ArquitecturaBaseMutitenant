using ArquitecturaBaseMultitenant.Application.Models.Auth;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Services;

/// <summary>Orquesta el registro personal por código: solicita la prueba de correo y confirma el alta al verificarla.</summary>
public interface IAccountService
{
    Task<Result<RequestLoginCodeResponse>> RequestSignupCodeAsync(SignupRequest request,
        CancellationToken cancellationToken);

    Task<Result> VerifySignupAsync(VerifySignupRequest request, CancellationToken cancellationToken);
}
