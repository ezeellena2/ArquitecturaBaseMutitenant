using ArquitecturaBaseMultitenant.Domain.Common;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.Isolation;

internal sealed class Deal : Entity, IConsumerBusinessShared
{
    private Deal()
    {
    }

    public Deal(Guid consumerTenantId, Guid businessTenantId)
    {
        ConsumerTenantId = consumerTenantId;
        BusinessTenantId = businessTenantId;
    }

    public Guid ConsumerTenantId { get; private set; }
    public Guid BusinessTenantId { get; private set; }
}
