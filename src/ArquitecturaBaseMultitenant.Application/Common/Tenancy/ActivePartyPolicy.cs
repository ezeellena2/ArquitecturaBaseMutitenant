using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Common.Tenancy;

/// <summary>
/// Comprueba qué parte de un dato compartido ejecuta una operación leyendo el espacio y su clase directamente de
/// <see cref="ITenantContext"/>, sin que el llamador declare la parte activa.
/// </summary>
public static class ActivePartyPolicy
{
    public static Result Require(ITenantContext tenantContext, IConsumerBusinessShared entity, Party requiredParty)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        return PartyPolicy.Require(entity, tenantContext.TenantKind, tenantContext.TenantId, requiredParty);
    }
}
