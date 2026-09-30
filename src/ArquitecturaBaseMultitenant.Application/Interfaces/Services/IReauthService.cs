using ArquitecturaBaseMultitenant.Application.Models.Identity;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Services;

/// <summary>Solicita y verifica un código de reautenticación que habilita operaciones sensibles sobre métodos de ingreso.</summary>
public interface IReauthService
{
    Task<Result<ReauthCodeResponse>> RequestAsync(RequestReauthRequest request, CancellationToken cancellationToken);
    Task<Result<ReauthResponse>> VerifyAsync(VerifyReauthRequest request, CancellationToken cancellationToken);
}
