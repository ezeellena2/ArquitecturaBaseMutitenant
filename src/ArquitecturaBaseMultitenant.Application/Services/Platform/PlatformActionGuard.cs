using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Services.Platform;

/// <summary>Entra únicamente después del evento confirmado y restaura el alcance al salir.</summary>
internal sealed class PlatformActionGuard(IPlatformScopeAccessService access, ITenantScope tenantScope)
{
    internal async Task<Result<Lease>> EnterAsync(Guid tenantId, string reason, CancellationToken cancellationToken)
    {
        var authorized = await access.AuthorizeAsync(tenantId, reason, cancellationToken);
        if (authorized.IsFailure) return authorized.Error;
        return new Lease(authorized.Value, tenantScope.Enter(tenantId));
    }

    internal sealed class Lease(Guid operatorId, IDisposable scope) : IDisposable
    {
        internal Guid OperatorId { get; } = operatorId;
        public void Dispose() => scope.Dispose();
    }
}
