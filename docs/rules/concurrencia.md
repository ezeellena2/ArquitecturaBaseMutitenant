# Ediciones simultáneas (concurrencia optimista)

**Regla:** toda entidad que se edita desde una pantalla lleva **versión**. Si dos personas la editan a la vez, la segunda que guarda recibe 409 `General.ConcurrencyConflict` y **no pisa** los cambios de la primera.

## Cómo se hace
- **Entidad:** implementa `IVersioned` (`uint Version { get; }`). La convención de EF la mapea a la columna de sistema `xmin` de Postgres como `IsRowVersion()`. No hace falta una columna nueva: Postgres la cambia en cada `UPDATE`.
- **Lectura:** el `*Response` de la ficha trae `version`.
- **Escritura:** el `*HttpRequest` de edición o de borrado trae `version`, **obligatorio**. El repositorio carga la entidad y fija la versión original: `repository.Get…(id, expectedVersion: request.Version)`.
- **Conflicto:**
  - `SaveChanges` lanza `DbUpdateConcurrencyException`;
  - `UnitOfWork` hace rollback y la traduce a `ConcurrencyConflictException`;
  - `ProblemDetailsMapper` responde 409 `General.ConcurrencyConflict`.
  
  Es la **única** excepción que llega al mapper como respuesta de negocio, porque siempre se responde igual.
- Las acciones de un solo clic que no dependen de lo que el usuario vio (activar, desactivar) también mandan `version`, así una fila vieja en pantalla no revierte un cambio.

## Prohibido
- Una columna `Version` o `RowVersion` manual incrementada a mano.
- Un `PUT` o `DELETE` de una entidad `IVersioned` sin `version` en el contrato.
- Atrapar `DbUpdateConcurrencyException` en un servicio para "reintentar": se pierde el cambio del otro.
- "El último que guarda gana" en silencio.

## Copiá de
- `Domain/Common/IVersioned.cs` y `Infrastructure/Persistence/Configurations/Tenant/RoleConfiguration.cs` (E2 y E4).

## Lo verifica
- `ConcurrencyTests`: dos ediciones con la misma versión, y la segunda da 409 sin cambiar nada.
- `VersionedContractTests` (arquitectura): todo contrato de edición o borrado de una entidad `IVersioned` tiene `Version`.

## Detalle
[backend.md §20](../architecture/backend.md#20-reglas-de-datos-que-se-aplican-solas) · front: `docs/rules/formularios.md`, "Ediciones simultáneas".
