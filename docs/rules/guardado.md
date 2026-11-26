# Guardado: una sola forma

**Regla:** se guarda **solo** con `IUnitOfWork.ExecuteInTransactionAsync(trabajo, CommitPolicy, ct)`, **una vez** por cada método público de un servicio que escribe.

## Cómo se hace
1. **Afuera y antes:** `await validator.ValidateAsync(request, ct)`. Si falla, se devuelve el `ValidationError` sin abrir el límite.
2. **Un límite:** `unitOfWork.ExecuteInTransactionAsync(ct => UpdateCoreAsync(request, ct), CommitPolicy.OnSuccess, ct)`.
3. **Adentro**, en este orden: locks → lecturas → reglas (que devuelven `Error`) → escrituras.
4. **El caché no se toca:** el mismo commit avanza las generaciones de lo que cambió y después las invalida en Redis ([cache](cache.md)). Todo el método va dentro de `OperationLog.RunAsync`.
- `CommitPolicy.OnAnyResult` solo cuando también hay que guardar si falla: intentos, códigos consumidos, auditoría de seguridad.
- Los locks usan `AdvisoryLockKeys.For(tenantId, recurso, id)` o `FOR NO KEY UPDATE`, y exigen la transacción del caso de uso.
- Si hace falta una lectura para localizar el lock, se obtiene una proyección o un hint sin tracking y se relee la entidad después de adquirirlo. Repetir una consulta tracked no refresca la instancia que EF ya conserva: las reglas y escrituras no pueden usar ese estado previo al lock.
- El método público del servicio de entrada abre la transacción; los helpers trabajan dentro de ella y no reciben `IUnitOfWork`. El tope de 8 cuenta solo dependencias de negocio: excluye `ILogger<T>`, `TimeProvider`, `IRequestValidator` e `IUnitOfWork`, y cuenta todo lo demás, incluido `IOptions<T>` ([ADR 0039](../decisions/0039-tope-de-dependencias-de-negocio.md)). No resolver el tope con clases que solo reenvían ni ampliar la lista blanca de helpers de `TransactionBoundaryTests`. Los desvíos existentes se consolidan antes de E7; el trabajo nuevo de E5 ya cumple esta regla.
- Hay dos excepciones nominales fuera de los servicios de Application: `ReferenceDataSeeder` y `DatabaseSeeder`. El primero toma `AdvisoryLockKeys.ReferenceDataSeed` para el bootstrap de catálogos. El segundo toma `AdvisoryLockKeys.Seed` y coordina en una UoW el mismo seed global para Development y Production, llamando a `ReferenceDataSeeder.StageAsync` sin anidar una UoW. Son seeds técnicos, no servicios de negocio. Ninguno crea personas ni organizaciones de muestra. Solo `UnitOfWork` llama `SaveChanges`.
- Fuera de la UoW abre también una transacción, que nunca guarda, el alcance de solo lectura del asistente (`AssistantReadOnlyScope`, [ADR 0068](../decisions/0068-acciones-del-asistente.md) §5): un scope de DI propio, `READ ONLY` y siempre deshecho, donde corren las herramientas y las fuentes. Una escritura adentro la rechaza la base, y su `IUnitOfWork` no abre otra; `TransactionBoundaryTests` la recibe del parcial del módulo.

```csharp
public Task<Result> UpdateAsync(UpdateRoleRequest request, CancellationToken ct) =>
    OperationLog.RunAsync(logger, "UpdateRole", async () =>
    {
        if (await validator.ValidateAsync(request, ct) is { } invalid) return invalid;
        return await unitOfWork.ExecuteInTransactionAsync(c => UpdateCoreAsync(request, c), CommitPolicy.OnSuccess, ct);
    });
```

El outbox reclama un lease durable con `SKIP LOCKED` y lo confirma antes de descifrar y enviar, fuera de transacción. `OutboxDispatchService` abre una UoW para Claim y otra para Complete, con scopes separados; Sent, Cancelled y falla terminal purgan el payload; cada reserva consume un intento, y una que vence sin respuesta deja el mensaje «Sin confirmar» y no se vuelve a reclamar ([ADR 0066](../decisions/0066-lo-que-no-se-sabe-si-salio.md)). Una copia reserva solo los canales que tiene registrados, y lo que su versión no puede leer vuelve a la cola con espera y tope, sin borrarse ([ADR 0060](../decisions/0060-dos-versiones-conviven.md)). Con varias copias, cada trabajo de fondo que borra o avisa corre una vez entre ellas ([concurrencia](concurrencia.md)).
- UnitOfWork usa READ COMMITTED, un solo SaveChanges y, ante excepción, rollback y ChangeTracker.Clear().
- **Un aviso en el log que depende de lo que se guardó,** desde un helper que corre adentro, va con `IAfterCommit.Add(() => Log…)`: corre recién con el commit y un rollback lo descarta (`AfterCommitInterceptor`). Así un caso que reserva y falla después no avisa lo que no pasó, como el tope de correo de `EmailQuotaGuard` ([ADR 0063](../decisions/0063-topes-de-correo-de-la-plataforma.md)). Solo para lo que no escribe ni falla.
- **La robustez es proporcional al efecto** (la 0018 de ArquitecturaBase). Comprobantes recuperables, estado incierto, leases, reconciliación y pruebas con varios hosts son solo para lo que tiene un efecto fuera del sistema o cuesta repetir: Meta, el correo, el almacenamiento, un LLM, un mensaje a una persona o lo que dispara una regla. Guardar algo interno usa la forma de esta ficha, con su versión y 409 si otro guardó antes ([concurrencia](concurrencia.md)). Más robustez para algo interno se justifica en el cambio con el riesgo concreto (qué se pierde o se duplica, y para quién); sin eso, la revisión la objeta.
- **Un solo `ExecuteInTransactionAsync` por método público,** contando todo su recorrido: los métodos propios que llama, sus lambdas y sus máquinas async. `TransactionBoundaryTests` lo contará leyendo el IL, con un caso de control, al programar (traspaso 9).

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
- `EmailQuotaAdversarialTests` (E12): el aviso del tope de correo va después del commit, con la base real, y una reserva que se deshace no avisa

## Detalle
[backend.md §5, "Repositorio, reader y UnitOfWork"](../architecture/backend.md#repositorio-reader-y-unitofwork) · ADR 0001
