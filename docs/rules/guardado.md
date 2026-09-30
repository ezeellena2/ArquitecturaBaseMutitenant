# Guardado: una sola forma

**Regla:** se guarda **solo** con `IUnitOfWork.ExecuteInTransactionAsync(trabajo, CommitPolicy, ct)`, **una vez** por cada método público de un servicio que escribe.

## Cómo se hace
1. **Afuera y antes:** `await validator.ValidateAsync(request, ct)`. Si falla, se devuelve el `ValidationError` sin abrir el límite.
2. **Un límite:** `unitOfWork.ExecuteInTransactionAsync(ct => UpdateCoreAsync(request, ct), CommitPolicy.OnSuccess, ct)`.
3. **Adentro**, en este orden: locks → lecturas → reglas (que devuelven `Error`) → escrituras.
4. **Afuera y después del commit:** invalidar el caché. Todo el método va dentro de `OperationLog.RunAsync`.
- `CommitPolicy.OnAnyResult` solo cuando también hay que guardar si falla: intentos, códigos consumidos, auditoría de seguridad.
- Los locks usan `AdvisoryLockKeys.For(tenantId, recurso, id)` o `FOR NO KEY UPDATE`, y exigen la transacción del caso de uso.
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

## Prohibido
- `SaveChanges` en un repositorio, un helper o un controller.
- Anidar límites: lanza, porque es un bug.
- Que un helper reciba `IUnitOfWork`.
- `ExecuteUpdate` o `ExecuteDelete` sobre entidades `IAuditable` o `ISoftDeletable`, porque se saltean la auditoría y el soft delete. La única llamada bulk declarada es `LoginMethodRepository.ClearPrimaryAsync` sobre `LoginMethod` técnico global: retira el principal anterior antes de activar el siguiente por el índice parcial inmediato, dentro de la misma UoW. No guarda ni tiene tenant, auditoría o soft-delete; el test real verifica cambio/rollback.
- Abrir un límite para una consulta.

## Copiá de
- `Application/Services/Roles/RoleService.cs`, métodos `UpdateAsync` y `UpdateCoreAsync` (E4).

## Lo verifica
- `TransactionBoundaryTests` (E2): lee el IL; solo los puntos de entrada de `Interfaces/Services`, `ReferenceDataSeeder` y `DatabaseSeeder` reciben `IUnitOfWork`, y nadie salvo `UnitOfWork` llama a `SaveChanges`.
- `UnitOfWorkTests` (E2): rollback, no anidar, 23505 → `UniqueConstraintViolationException`.
- `FakeUnitOfWork` (E2) en los tests unitarios aplica la misma `CommitPolicy`.

## Detalle
[backend.md §5, "Repositorio, reader y UnitOfWork"](../architecture/backend.md#repositorio-reader-y-unitofwork) · ADR 0001
