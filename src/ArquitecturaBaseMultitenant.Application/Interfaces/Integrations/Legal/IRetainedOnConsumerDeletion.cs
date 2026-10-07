namespace ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Legal;

/// <summary>Política del módulo por tipo compartido, motivo técnico y plazo desde la eliminación de la cuenta.</summary>
public interface IRetainedOnConsumerDeletion
{
    Type EntityType { get; }
    string Reason { get; }
    TimeSpan RetentionPeriod { get; }
}
