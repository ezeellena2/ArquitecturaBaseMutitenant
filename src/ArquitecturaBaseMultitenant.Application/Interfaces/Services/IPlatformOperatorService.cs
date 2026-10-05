using ArquitecturaBaseMultitenant.Application.Models.Common;
using ArquitecturaBaseMultitenant.Application.Models.Platform;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Services;

/// <summary>
/// Invita operadores de Plataforma con una identidad exclusiva (ADR 0042). La invitación queda pendiente hasta el
/// primer ingreso con código a Plataforma y vence a los 7 días; mientras tanto se reenvía o se revoca.
/// </summary>
public interface IPlatformOperatorInvitationService
{
    Task<Result<PlatformOperatorInvitationResponse>> InviteAsync(InvitePlatformOperatorRequest request,
        CancellationToken ct);

    /// <summary>Renueva los 7 días de una invitación sin aceptar, vigente o vencida, y manda el correo de nuevo.</summary>
    Task<Result<PlatformOperatorInvitationResponse>> ResendAsync(Guid accountId, CancellationToken ct);

    /// <summary>Borra una invitación sin aceptar y libera el correo; la cuenta deja de figurar en Cuentas.</summary>
    Task<Result> RevokeAsync(Guid accountId, CancellationToken ct);
}

/// <summary>Cambia o quita los permisos de un operador, sin dejar a la plataforma sin quien administre operadores.</summary>
public interface IPlatformOperatorService
{
    Task<Result<VersionResponse>> ChangePermissionsAsync(Guid accountId, ChangePlatformOperatorRequest request, CancellationToken ct);

    Task<Result> RemoveAsync(Guid accountId, PlatformAccountActionRequest request, CancellationToken ct);
}
