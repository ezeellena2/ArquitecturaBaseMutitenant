using ArquitecturaBaseMultitenant.Application.Models.Auth;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Services;

/// <summary>
/// «Recuperar mi cuenta», sin sesión: código para el método nuevo, verificación y registro del pedido. Ninguna
/// respuesta revela si un correo o un teléfono tiene cuenta (ADR 0041) y nunca se entrega una sesión.
/// </summary>
public interface IAccountRecoveryService
{
    Task<Result<RequestLoginCodeResponse>> RequestCodeAsync(RequestRecoveryCodeRequest request,
        CancellationToken cancellationToken);

    Task<Result> VerifyCodeAsync(VerifyRecoveryCodeRequest request, CancellationToken cancellationToken);

    Task<Result> SubmitAsync(SubmitAccountRecoveryRequest request, CancellationToken cancellationToken);
}
