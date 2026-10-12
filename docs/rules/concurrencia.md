# Ediciones simultáneas (concurrencia optimista)

**Regla:** toda entidad que se edita desde una pantalla lleva **versión**. Si dos personas la editan a la vez, la segunda que guarda recibe 409 `General.ConcurrencyConflict` y **no pisa** los cambios de la primera.

## Cómo se hace
- **Entidad:** implementa `IVersioned` (`uint Version { get; }`). La convención de EF la mapea a la columna de sistema `xmin` de Postgres como `IsRowVersion()`. No hace falta una columna nueva: Postgres la cambia en cada `UPDATE`.
- **Lectura:** el `*Response` de la ficha trae `version`.
- **Escritura:** el `*HttpRequest` de edición o de borrado trae `version`, **obligatorio**. El repositorio carga la entidad y fija la versión original: `repository.Get…(id, expectedVersion: request.Version)`.
- **Respuesta del guardado:** un `PUT` o `POST` con `version` responde 200 con `VersionResponse` (`{ version }`): la versión que quedó después del commit, la que EF trae del mismo `UPDATE` o `INSERT`. El trabajo de la transacción devuelve la entidad guardada y el servicio arma la respuesta después de `ExecuteInTransactionAsync`, con `VersionResponse.Of(result)`; nunca la relee. Un `DELETE` responde 204.
- **Respuesta compuesta:** cuando el contrato aprobado incluye el recurso y su versión dentro de una respuesta mayor, la acción declara `[ResponseVersion("Conversation.Version")]` (ruta de propiedades CLR). La guarda exige 200, que esa ruta exista en el tipo de éxito y termine en `uint`, y rechaza 204, rutas inexistentes o tipos distintos. Sigue siendo la versión devuelta después del commit; no se oculta ni se renombra `Version`. Los namespaces de contratos incluyen `Api.Modules.<Nombre>.Contracts` con forma exacta.
- **Guardado sin cambios:** también compara la versión. Una entidad `IAuditable` se escribe sola (`ModifiedAtUtc`), `MarkUpdated` escribe todo y el perfil de la cuenta sube su versión. Sin una columna escrita, EF no manda el `UPDATE`, no compara la versión y la respuesta llevaría una que la pantalla no vio.
- **Conflicto:**
  - `SaveChanges` lanza `DbUpdateConcurrencyException`;
  - `UnitOfWork` hace rollback y la traduce a `ConcurrencyConflictException`;
  - `ProblemDetailsMapper` responde 409 `General.ConcurrencyConflict`.
  
  Es la **única** excepción que llega al mapper como respuesta de negocio, porque siempre se responde igual.
- Las acciones de un solo clic que no dependen de lo que el usuario vio (activar, desactivar) también mandan `version`, así una fila vieja en pantalla no revierte un cambio.
- **Front:** el guardado de una pantalla manda la versión de lo que la persona está editando: la de la lectura con la que arrancó o se reinició el formulario (al cargar, con «Ver lo nuevo» y al descartar) y, después de guardar, la que respondió el guardado; nunca la de una relectura. Si otra persona guardó mientras la ficha estaba quieta, el back responde 409 y sale el cartel con «Ver lo nuevo». Lo lleva `useEditForm` (front: `docs/rules/formularios.md`, "Ediciones simultáneas").

## Prohibido
- Una columna `Version` o `RowVersion` manual incrementada a mano, salvo `ProfileVersion` de «Tus datos» (la excepción de arriba).
- Pedir `version` en las transiciones de estado sin formulario de la cuenta cubiertas por T18b (suspender, reactivar, cerrar sesiones, la baja): un ingreso daría un 409 falso. La protege el estado de la transición, bajo el lock.
- Un `PUT` o `DELETE` de una entidad `IVersioned` sin `version` en el contrato.
- Atrapar `DbUpdateConcurrencyException` en un servicio para "reintentar": se pierde el cambio del otro.
- "El último que guarda gana" en silencio.
- Responder 204 a un guardado con `version`, o responder una versión releída después del commit: si otra persona guardó en el medio, la pantalla tomaría como base una versión que no vio.
- En el front, guardar lo que la persona empezó a editar con la versión de una relectura posterior: pisa el cambio de la otra persona sin 409.

## Copiá de
- `Domain/Common/IVersioned.cs` (E2) y `Infrastructure/Persistence/Configurations/Tenant/RoleConfiguration.cs` (E4).
- La respuesta del guardado: `Application/Models/Common/VersionResponse.cs` y `UpdateAsync` de `Application/Services/Roles/RoleService.cs` (E7).

## Lo verifica
- `VersionedContractTests` (E2)

## Detalle
[backend.md §20](../architecture/backend.md#20-reglas-de-datos-que-se-aplican-solas) · front: `docs/rules/formularios.md`, "Ediciones simultáneas".
