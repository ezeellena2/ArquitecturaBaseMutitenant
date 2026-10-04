using ArquitecturaBaseMultitenant.Application.Models.Platform;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Services;

/// <summary>Invita operadores de Plataforma con una identidad exclusiva (ADR 0042).</summary>
public interface IPlatformOperatorInvitationService
{
    Task<Result<PlatformOperatorInvitationResponse>> InviteAsync(InvitePlatformOperatorRequest request,
        CancellationToken ct);
}

/// <summary>Cambia o quita los permisos de un operador, sin dejar a la plataforma sin quien administre operadores.</summary>
public interface IPlatformOperatorService
{
    Task<Result> ChangePermissionsAsync(Guid accountId, ChangePlatformOperatorRequest request, CancellationToken ct);

    Task<Result> RemoveAsync(Guid accountId, PlatformAccountActionRequest request, CancellationToken ct);
}
