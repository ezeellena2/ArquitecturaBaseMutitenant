using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Platform;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Services.Platform;

/// <summary>
/// Protege al último operador activo con <c>platform.operators.manage</c>. Dentro de la transacción del
/// llamador toma el lock global, cuenta y recién después decide, como <c>LastTenantAdminGuard</c>. Lo usan
/// suspender, iniciar la baja y quitar o cambiar los permisos de un operador.
/// </summary>
internal sealed class PlatformOperatorGuard(IPlatformPermissionAssignmentRepository assignments)
{
    internal async Task<Result> CheckAnotherManagerRemainsAsync(Guid accountId, CancellationToken ct)
    {
        await assignments.LockOperatorsAsync(ct);
        var managers = await assignments.ListActiveManagerIdsAsync(ct);
        return managers.Count == 1 && managers.Contains(accountId) ? PlatformOperatorErrors.LastManager : Result.Success();
    }
}
