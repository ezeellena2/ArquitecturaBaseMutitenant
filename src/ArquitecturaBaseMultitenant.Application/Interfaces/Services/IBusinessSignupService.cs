using ArquitecturaBaseMultitenant.Application.Models.Auth;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Services;

/// <summary>Verifica el correo de un alta empresarial sin crear un espacio Personal.</summary>
public interface IBusinessSignupService
{
    Task<Result<RequestLoginCodeResponse>> RequestEmailCodeAsync(SignupRequest request,
        CancellationToken cancellationToken);

    Task<Result> VerifyEmailCodeAsync(VerifySignupRequest request, CancellationToken cancellationToken);

    Task<Result<BusinessSignupResponse>> CreateOrganizationAsync(Guid userId, Guid operationId,
        BusinessSignupRequest request, CancellationToken cancellationToken);
}
