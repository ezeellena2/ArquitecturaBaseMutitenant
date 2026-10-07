namespace ArquitecturaBaseMultitenant.Application.Models.Legal;

/// <summary>Identifica la retención y el alcance que se abre antes de iniciar su transacción.</summary>
public sealed record ConsumerDataRetentionWork(Guid RetentionId, Guid ConsumerTenantId);
