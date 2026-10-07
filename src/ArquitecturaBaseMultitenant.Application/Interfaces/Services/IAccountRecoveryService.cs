using ArquitecturaBaseMultitenant.Application.Models.Auth;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Services;

/// <summary>
/// «Recuperar mi cuenta», sin sesión: consulta la cuenta anterior, verifica el correo nuevo libre y registra el pedido.
/// Los errores corresponden al campo (ADR 0043); no entrega una sesión.
/// </summary>
public interface IAccountRecoveryService
{
    Task<Result> PreviewAsync(PreviewAccountRecoveryRequest request, CancellationToken cancellationToken);

    Task<Result<RequestLoginCodeResponse>> RequestCodeAsync(RequestRecoveryCodeRequest request,
        CancellationToken cancellationToken);

    Task<Result> VerifyCodeAsync(VerifyRecoveryCodeRequest request, CancellationToken cancellationToken);

    Task<Result> SubmitAsync(SubmitAccountRecoveryRequest request, CancellationToken cancellationToken);
}
