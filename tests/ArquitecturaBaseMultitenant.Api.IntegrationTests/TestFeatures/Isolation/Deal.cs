using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.Isolation;

/// <summary>
/// Representa un dato compartido sintético entre una persona y una empresa. Permite probar las políticas
/// que limitan su lectura a ambas partes.
/// </summary>
internal sealed class Deal : Entity, IConsumerBusinessShared, IAuditable, IVersioned
{
    private Deal()
    {
    }

    public Deal(Guid consumerTenantId, Guid businessTenantId, string sharedSummary = "")
    {
        ArgumentNullException.ThrowIfNull(sharedSummary);
        ConsumerTenantId = consumerTenantId;
        BusinessTenantId = businessTenantId;
        Summary = sharedSummary;
    }

    public Guid ConsumerTenantId { get; private set; }
    public Guid BusinessTenantId { get; private set; }
    public string Summary { get; private set; } = string.Empty;
    public string SharedSummary => Summary;
    [NotAudited]
    public string? ConsumerName { get; private set; }
    public DealStatus Status { get; private set; } = DealStatus.Requested;
    public DateTime CreatedAtUtc { get; private set; }
    public Guid? CreatedBy { get; private set; }
    public DateTime? ModifiedAtUtc { get; private set; }
    public Guid? ModifiedBy { get; private set; }
    public uint Version { get; private set; }

    // El interceptor sella ConsumerTenantId. La contraparte ya fue resuelta por PublicSite.
    public static Deal Request(Guid businessTenantId, string summary, string? consumerName) =>
        new(Guid.Empty, businessTenantId, summary) { ConsumerName = consumerName };

    public Result Transition(DealStatus status, Party activeParty, Guid activeTenantId)
    {
        var required = status == DealStatus.Cancelled ? Party.Consumer : Party.Business;
        var party = PartyPolicy.Require(this, activeParty, activeTenantId, required);
        if (party.IsFailure) return party;
        if (Status != DealStatus.Requested || status == DealStatus.Requested || !Enum.IsDefined(status))
            return Error.Conflict("General.ConcurrencyConflict", "The request state already changed.");
        Status = status;
        return Result.Success();
    }
}

public enum DealStatus { Requested, Confirmed, Rejected, Cancelled }
