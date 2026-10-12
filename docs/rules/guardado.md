# Guardado: una sola forma

**Regla:** se guarda **solo** con `IUnitOfWork.ExecuteInTransactionAsync(trabajo, CommitPolicy, ct)`, **una vez** por cada método público de un servicio que escribe.

## Cómo se hace
1. **Afuera y antes:** `await validator.ValidateAsync(request, ct)`. Si falla, se devuelve el `ValidationError` sin abrir el límite.
2. **Un límite:** `unitOfWork.ExecuteInTransactionAsync(ct => UpdateCoreAsync(request, ct), CommitPolicy.OnSuccess, ct)`.
3. **Adentro**, en este orden: locks → lecturas → reglas (que devuelven `Error`) → escrituras.
4. **Afuera y después del commit:** invalidar el caché. Todo el método va dentro de `OperationLog.RunAsync`.
- `CommitPolicy.OnAnyResult` solo cuando también hay que guardar si falla: intentos, códigos consumidos, auditoría de seguridad.
- Los locks usan `AdvisoryLockKeys.For(tenantId, recurso, id)` o `FOR NO KEY UPDATE`, y exigen la transacción del caso de uso.
- Si hace falta una lectura para localizar el lock, se obtiene una proyección o un hint sin tracking y se relee la entidad después de adquirirlo. Repetir una consulta tracked no refresca la instancia que EF ya conserva: las reglas y escrituras no pueden usar ese estado previo al lock.
- El método público del servicio de entrada abre la transacción; los helpers trabajan dentro de ella y no reciben `IUnitOfWork`. El tope de 8 cuenta solo dependencias de negocio: excluye `ILogger<T>`, `TimeProvider`, `IRequestValidator` e `IUnitOfWork`, y cuenta todo lo demás, incluido `IOptions<T>` ([ADR 0039](../decisions/0039-tope-de-dependencias-de-negocio.md)). No resolver el tope con clases que solo reenvían ni ampliar la lista blanca de helpers de `TransactionBoundaryTests`. Los desvíos existentes se consolidan antes de E7; el trabajo nuevo de E5 ya cumple esta regla.
- Hay dos excepciones nominales fuera de los servicios de Application: `ReferenceDataSeeder` y `DatabaseSeeder`. El primero toma `AdvisoryLockKeys.ReferenceDataSeed` para el bootstrap de catálogos. El segundo toma `AdvisoryLockKeys.Seed` y coordina en una UoW el mismo seed global para Development y Production, llamando a `ReferenceDataSeeder.StageAsync` sin anidar una UoW. Son seeds técnicos, no servicios de negocio. Ninguno crea personas ni organizaciones de muestra. Solo `UnitOfWork` llama `SaveChanges`.

```csharp
public Task<Result> UpdateAsync(UpdateRoleRequest request, CancellationToken ct) =>
    OperationLog.RunAsync(logger, "UpdateRole", async () =>
    {
        if (await validator.ValidateAsync(request, ct) is { } invalid) return invalid;
        var result = await unitOfWork.ExecuteInTransactionAsync(c => UpdateCoreAsync(request, c), CommitPolicy.OnSuccess, ct);
        if (result.IsSuccess) await permissions.InvalidateRoleAsync(request.Id, ct);
        return result;
    });
```

El outbox reclama un lease durable con `SKIP LOCKED` y lo confirma antes de descifrar y enviar, fuera de transacción. `OutboxDispatchService` abre una UoW para Claim y otra para Complete, con scopes separados; Sent, Cancelled y falla terminal purgan el payload y un lease abandonado consume intentos.
- UnitOfWork usa READ COMMITTED, un solo SaveChanges y, ante excepción, rollback y ChangeTracker.Clear().

## Prohibido
- `SaveChanges` en un repositorio, un helper o un controller.
- Anidar límites: lanza, porque es un bug.
- Que un helper reciba `IUnitOfWork`.
- `ExecuteUpdate` o `ExecuteDelete` sobre entidades `IAuditable` o `ISoftDeletable`, porque se saltean la auditoría y el soft delete. La única llamada bulk declarada es `LoginMethodRepository.ClearPrimaryAsync` sobre `LoginMethod` técnico global: retira el principal anterior antes de activar el siguiente por el índice parcial inmediato, dentro de la misma UoW. No guarda ni tiene tenant, auditoría o soft-delete; el test real verifica cambio/rollback.
- Abrir un límite para una consulta.

## Copiá de
- `Application/Services/Roles/RoleService.cs`, métodos `UpdateAsync` y `UpdateCoreAsync` (E4).

## Lo verifica
- `TransactionBoundaryTests` (E0)

## Detalle
[backend.md §5, "Repositorio, reader y UnitOfWork"](../architecture/backend.md#repositorio-reader-y-unitofwork) · ADR 0001
