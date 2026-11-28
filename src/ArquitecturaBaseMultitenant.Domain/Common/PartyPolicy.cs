using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;

namespace ArquitecturaBaseMultitenant.Domain.Common;

/// <summary>Comprueba qué parte de un dato compartido ejecuta una transición.</summary>
public static class PartyPolicy
{
    public static Result Require(IConsumerBusinessShared entity, TenantKind? activeTenantKind, Guid? activeTenantId,
        Party requiredParty)
    {
        ArgumentNullException.ThrowIfNull(entity);

        if (activeTenantId is not { } tenantId || tenantId == Guid.Empty)
        {
            return PartyErrors.NotAllowed;
        }

        var activeParty = activeTenantKind switch
        {
            TenantKind.Personal => Party.Consumer,
            TenantKind.Business => Party.Business,
            _ => (Party?)null,
        };

        return activeParty is { } party
            ? Require(entity, party, tenantId, requiredParty)
            : PartyErrors.NotAllowed;
    }

    public static Result Require(IConsumerBusinessShared entity, Party activeParty, Guid activeTenantId, Party requiredParty)
    {
        ArgumentNullException.ThrowIfNull(entity);

        if (activeTenantId == Guid.Empty || activeParty != requiredParty)
        {
            return PartyErrors.NotAllowed;
        }

        var allowed = activeParty switch
        {
            Party.Consumer => entity.ConsumerTenantId == activeTenantId,
            Party.Business => entity.BusinessTenantId == activeTenantId,
            _ => false,
        };
        return allowed ? Result.Success() : PartyErrors.NotAllowed;
    }
}
