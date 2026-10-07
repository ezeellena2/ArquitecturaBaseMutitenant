namespace ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Legal;

/// <summary>Reemplaza una copia compartida en el alcance Consumer y la UoW provistos por el llamador.</summary>
public interface IConsumerCopyDeletion
{
    Task ReplaceAsync(string entityTypeKey, Guid entityId, Guid consumerTenantId,
        Guid businessTenantId, CancellationToken cancellationToken);
}
