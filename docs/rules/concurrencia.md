# Ediciones simultáneas (concurrencia optimista)

**Regla:** toda entidad que se edita desde una pantalla lleva **versión**. Si dos personas la editan a la vez, la segunda que guarda recibe 409 `General.ConcurrencyConflict` y **no pisa** los cambios de la primera.

## Cómo se hace
- **Entidad:** implementa `IVersioned` (`uint Version { get; }`). La convención de EF la mapea a la columna de sistema `xmin` de Postgres como `IsRowVersion()`. No hace falta una columna nueva: Postgres la cambia en cada `UPDATE`.
- **Lectura:** el `*Response` de la ficha trae `version`.
- **Escritura:** el `*HttpRequest` de edición o de borrado trae `version`, **obligatorio**. El repositorio carga la entidad y fija la versión original: `repository.Get…(id, expectedVersion: request.Version)`.
- **Respuesta del guardado:** un `PUT` con `version`, y publicar o despublicar la Página pública, responde 200 con `VersionResponse` (`{ version }`): la versión que quedó después del commit, la que EF trae del mismo `UPDATE` o `INSERT`. El trabajo de la transacción devuelve la entidad guardada y el servicio arma la respuesta después de `ExecuteInTransactionAsync`, con `VersionResponse.Of(result)`; nunca la relee. Un `DELETE` responde 204.
- **Guardado sin cambios:** también compara la versión. Una entidad `IAuditable` se escribe sola (`ModifiedAtUtc`), `MarkUpdated` escribe todo y el perfil de la cuenta sube su versión. Sin una columna escrita, EF no manda el `UPDATE`, no compara la versión y la respuesta llevaría una que la pantalla no vio.
- **Excepción, «Tus datos» de Mi cuenta:** el perfil (nombre, idioma y zona) vive en la fila de la cuenta, que también cambia con el contacto principal, la última organización, las sesiones y los intentos fallidos; con el `xmin` de la fila, esas escrituras darían un 409 falso al guardar el perfil. Por eso el perfil lleva su propia versión, `ProfileVersion`: una columna que sube solo `UpdateProfileAsync`, en cada guardado del perfil. El repositorio la compara con la que mandó la pantalla (si no coincide, 409) y dos guardados a la vez los separa el `xmin`, que EF compara en el mismo `UPDATE`. `/api/me` la devuelve como `version` (0 si el perfil nunca se guardó) y el `PUT` responde la nueva. Es la única columna de versión a mano.
- **Conflicto:**
  - `SaveChanges` lanza `DbUpdateConcurrencyException`;
  - `UnitOfWork` hace rollback y la traduce a `ConcurrencyConflictException`;
  - `ProblemDetailsMapper` responde 409 `General.ConcurrencyConflict`.
  
  Es la **única** excepción que llega al mapper como respuesta de negocio, porque siempre se responde igual.
- Las acciones de un solo clic que no dependen de lo que el usuario vio (activar, desactivar) también mandan `version`, así una fila vieja en pantalla no revierte un cambio.
- **Front:** el guardado de una pantalla manda la versión de lo que la persona está editando: la de la lectura con la que arrancó o se reinició el formulario (al cargar, con «Ver lo nuevo» y al descartar) y, después de guardar, la que respondió el guardado; nunca la de una relectura. Si otra persona guardó mientras la ficha estaba quieta, el back responde 409 y sale el cartel con «Ver lo nuevo». Lo lleva `useEditForm` (front: `docs/rules/formularios.md`, "Ediciones simultáneas").

## Prohibido
- Una columna `Version` o `RowVersion` manual incrementada a mano, salvo `ProfileVersion` de «Tus datos» (la excepción de arriba).
- Un `PUT` o `DELETE` de una entidad `IVersioned` sin `version` en el contrato.
- Atrapar `DbUpdateConcurrencyException` en un servicio para "reintentar": se pierde el cambio del otro.
- "El último que guarda gana" en silencio.
- Responder 204 a un guardado con `version`, o responder una versión releída después del commit: si otra persona guardó en el medio, la pantalla tomaría como base una versión que no vio.
- En el front, guardar lo que la persona empezó a editar con la versión de una relectura posterior: pisa el cambio de la otra persona sin 409.

## Copiá de
- `Domain/Common/IVersioned.cs` (E2) y `Infrastructure/Persistence/Configurations/Tenant/RoleConfiguration.cs` (E4).
- La respuesta del guardado: `Application/Models/Common/VersionResponse.cs` y `UpdateAsync` de `Application/Services/Roles/RoleService.cs` (E7).

## Lo verifica
- `ConcurrencyTests` (E2): dos ediciones con la misma versión, y la segunda da 409 sin cambiar nada.
- `VersionedContractTests` (E2): todo contrato de edición o borrado de una entidad `IVersioned` tiene `Version` (test de arquitectura).
- `VersionedContractTests` (E7): todo `PUT` con `version` declara 200 con `VersionResponse` y no 204 (test de arquitectura).
- Por área, el guardado responde la versión nueva, es la que lee la ficha y sirve para el guardado siguiente sin 409 (con `SavedVersion`): `RolesTests`, `OrganizationUsersTests`, `CompaniesTests`, `OrganizationSettingsTests`, `PublicPagesTests`, `AccountProfileTests`, `PlatformSettingsTests`, `PlatformFeatureAdministrationTests` y `PlatformOperatorTests` (E7). Un guardado viejo que no cambia nada da 409: `AccountProfileTests` y `OrganizationUsersTests` (E7). `ProfileServiceTests` (E7): la respuesta lleva la versión que dejó el guardado del perfil.
- `AccountProfileTests` (E7): «Tus datos» guarda sin 409 con la versión leída antes de entrar a otra organización, cambiar el contacto principal, contar y reiniciar intentos fallidos y cerrar las sesiones; dos guardados del perfil con la misma versión sí dan 409.
- Front: `useEditForm.test.tsx` (E7) y las pruebas de Roles, Usuario, Configuración, Página pública y Mi cuenta (E7): una relectura de fondo con otra versión no cambia la que manda el guardado; sale el 409 y no se pisa lo de la otra persona.

## Detalle
[backend.md §20](../architecture/backend.md#20-reglas-de-datos-que-se-aplican-solas) · front: `docs/rules/formularios.md`, "Ediciones simultáneas".
