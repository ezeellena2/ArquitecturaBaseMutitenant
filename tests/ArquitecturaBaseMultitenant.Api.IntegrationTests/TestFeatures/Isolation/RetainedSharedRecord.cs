using ArquitecturaBaseMultitenant.Domain.Common;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.Isolation;

/// <summary>Copia sintética con dos datos personales y borrado lógico, para comprobar también su purga legal.</summary>
internal sealed class RetainedSharedRecord : Entity, IConsumerBusinessShared, IAuditable, ISoftDeletable
{
    private RetainedSharedRecord() { }

    internal RetainedSharedRecord(Guid businessTenantId, string consumerName, string consumerNote)
    {
        BusinessTenantId = businessTenantId;
        ConsumerName = consumerName;
        ConsumerNote = consumerNote;
    }

    public Guid ConsumerTenantId { get; private set; }
    public Guid BusinessTenantId { get; private set; }
    public string Summary { get; private set; } = "Resumen de negocio conservado";
    [NotAudited] public string? ConsumerName { get; private set; }
    [NotAudited] public string? ConsumerNote { get; private set; }
    [NotAudited] public bool ConsumerCopyDeleted { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public Guid? CreatedBy { get; private set; }
    public DateTime? ModifiedAtUtc { get; private set; }
    public Guid? ModifiedBy { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAtUtc { get; private set; }
    public Guid? DeletedBy { get; private set; }

    public void ReplaceConsumerCopy()
    {
        ConsumerName = null;
        ConsumerNote = null;
        ConsumerCopyDeleted = true;
    }
}
