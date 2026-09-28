namespace ArquitecturaBaseMultitenant.Domain.Common;

/// <summary>Comprueba qué parte de un dato compartido ejecuta una transición.</summary>
public static class PartyPolicy
{
    public static bool Require(IConsumerBusinessShared entity, Party activeParty, Guid activeTenantId, Party requiredParty)
    {
        ArgumentNullException.ThrowIfNull(entity);

        if (activeTenantId == Guid.Empty || activeParty != requiredParty)
        {
            return false;
        }

        return activeParty switch
        {
            Party.Consumer => entity.ConsumerTenantId == activeTenantId,
            Party.Business => entity.BusinessTenantId == activeTenantId,
            _ => false,
        };
    }
}
