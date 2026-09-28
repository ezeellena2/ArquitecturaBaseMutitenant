namespace ArquitecturaBaseMultitenant.Domain.Common;

/// <summary>Copia de datos visible solo a una persona y una organización.</summary>
public interface IConsumerBusinessShared
{
    Guid ConsumerTenantId { get; }

    Guid BusinessTenantId { get; }
}
