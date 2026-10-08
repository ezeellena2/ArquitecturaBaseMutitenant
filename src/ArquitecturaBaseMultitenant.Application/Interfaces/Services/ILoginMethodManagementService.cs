using ArquitecturaBaseMultitenant.Application.Models.Identity;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Services;

/// <summary>Gestiona correos de la sesión iniciada: alta, verificación propia, principal y baja.</summary>
public interface ILoginMethodManagementService
{
    Task<Result<LoginMethodCodeResponse>> AddAsync(AddLoginMethodRequest request, CancellationToken cancellationToken);
    Task<Result<LoginMethodCodeResponse>> AddEmailAsync(AddLoginEmailRequest request, CancellationToken cancellationToken);
    Task<Result<LoginMethodCodeResponse>> SendCodeAsync(Guid methodId, CancellationToken cancellationToken);
    Task<Result> VerifyAsync(VerifyLoginMethodRequest request, CancellationToken cancellationToken);
    Task<Result> MakePrimaryAsync(ChangeLoginMethodRequest request, CancellationToken cancellationToken);
    Task<Result> RemoveAsync(ChangeLoginMethodRequest request, CancellationToken cancellationToken);
}
