using ArquitecturaBaseMultitenant.Domain.Common;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.Isolation;

/// <summary>
/// Representa un dato compartido sintético entre una persona y una empresa. Permite probar las políticas
/// que limitan su lectura a ambas partes.
/// </summary>
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
