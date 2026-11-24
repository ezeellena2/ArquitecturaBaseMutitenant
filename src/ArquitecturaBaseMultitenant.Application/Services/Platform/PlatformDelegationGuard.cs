using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Platform;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.Services.Platform;

/// <summary>
/// Nadie da lo que no tiene, en Plataforma (ADR 0061 §4): lo usan invitar a un operador, cambiarle o quitarle los
/// permisos, y suspender, reactivar o dar de baja una cuenta que es operador, adentro de la transacción y después de sus
/// locks, y decide con <see cref="PlatformDelegationPolicy"/>. Lo que tiene quien pide se lee de la base en cada llamada,
/// con la misma lectura que <c>PlatformPermissionService</c> (la que mira <c>[HasPlatformPermission]</c>): un operador
/// activo, con su invitación aceptada, y lo efectivo de su asignación. Lo del operador sobre el que se actúa se lee de su
/// asignación, en cualquier estado.
/// </summary>
internal sealed class PlatformDelegationGuard(ICurrentUser currentUser, IUserRepository users,
    IPlatformPermissionAssignmentReader assignments)
{
    /// <summary>Lo efectivo de quien pide, leído ahora; vacío si no es un operador activo.</summary>
    internal async Task<IReadOnlySet<string>> ReadHeldAsync(CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } actorId || actorId == Guid.Empty) return new HashSet<string>();
        var actor = await users.GetByIdAsync(actorId, cancellationToken);
        // ADR 0042: una invitación sin aceptar todavía no da permisos.
        if (actor is not { Status: UserStatus.Active, IsPlatformOperator: true, HasPendingOperatorInvitation: false })
            return new HashSet<string>();
        return PlatformDelegationPolicy.Effective(await assignments.FindPermissionsAsync(actorId, cancellationToken) ?? []);
    }

    /// <summary>
    /// Invitar a un operador o cambiarle los permisos de <paramref name="before"/> a <paramref name="after"/>: quien pide
    /// tiene cada permiso que entra o sale; si no, <see cref="PlatformOperatorErrors.PermissionsNotHeld"/>.
    /// </summary>
    internal async Task<Result> CheckPermissionsChangeAsync(IEnumerable<string> before, IEnumerable<string> after,
        CancellationToken cancellationToken) =>
        PlatformDelegationPolicy.CanChangePermissions(await ReadHeldAsync(cancellationToken), before, after)
            ? Result.Success()
            : PlatformOperatorErrors.PermissionsNotHeld;

    /// <summary>
    /// Actuar sobre la cuenta <paramref name="accountId"/>: si tiene permisos de operador, quien pide los tiene todos; si
    /// no, <see cref="PlatformOperatorErrors.HasMorePermissions"/>. Sobre uno mismo siempre.
    /// </summary>
    internal async Task<Result> CheckActOnAsync(Guid accountId, CancellationToken cancellationToken)
    {
        if (accountId == currentUser.UserId) return Result.Success();
        var target = await assignments.FindPermissionsAsync(accountId, cancellationToken);
        if (target is not { Count: > 0 }) return Result.Success();
        return PlatformDelegationPolicy.CanActOn(self: false, await ReadHeldAsync(cancellationToken), target)
            ? Result.Success()
            : PlatformOperatorErrors.HasMorePermissions;
    }
}
