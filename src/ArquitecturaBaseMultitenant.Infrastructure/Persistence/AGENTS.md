Contexto EF y límites de persistencia. `TenantContext` guarda el acceso scoped y `ITenantScope.Enter` fija un alcance técnico antes de la transacción; nunca toma el tenant del host ni de datos HTTP.

Antes de escribir, leé: [persistencia-ef](../../../docs/rules/persistencia-ef.md) · [multitenancy](../../../docs/rules/multitenancy.md) · [guardado](../../../docs/rules/guardado.md).

Copiá de: `TenantContext.cs` para el alcance; `../ArquitecturaBase/src/ArquitecturaBase.Infrastructure/Persistence/UnitOfWork.cs` para el límite real. Lo prueba `TenantContextTests` (E2).
