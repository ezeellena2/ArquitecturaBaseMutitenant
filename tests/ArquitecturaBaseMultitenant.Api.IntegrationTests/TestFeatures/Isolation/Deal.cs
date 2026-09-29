using ArquitecturaBaseMultitenant.Domain.Common;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.Isolation;

internal sealed class Deal : Entity, IConsumerBusinessShared
{
    private Deal()
    {
    }

    public Deal(Guid consumerTenantId, Guid businessTenantId, string sharedSummary = "")
    {
        ArgumentNullException.ThrowIfNull(sharedSummary);
        ConsumerTenantId = consumerTenantId;
        BusinessTenantId = businessTenantId;
        SharedSummary = sharedSummary;
    }

    public Guid ConsumerTenantId { get; private set; }
    public Guid BusinessTenantId { get; private set; }
    public string SharedSummary { get; private set; } = string.Empty;
}
