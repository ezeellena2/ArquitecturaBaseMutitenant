# Ediciones simultáneas (concurrencia optimista)

**Regla:** toda entidad que se edita desde una pantalla lleva **versión**. Si dos personas la editan a la vez, la segunda que guarda recibe 409 `General.ConcurrencyConflict` y **no pisa** los cambios de la primera.

## Cómo se hace
- **Entidad:** implementa `IVersioned` (`uint Version { get; }`). La convención de EF la mapea a la columna de sistema `xmin` de Postgres como `IsRowVersion()`. No hace falta una columna nueva: Postgres la cambia en cada `UPDATE`.
- **Lectura:** el `*Response` de la ficha trae `version`.
- **Escritura:** el `*HttpRequest` de edición o de borrado trae `version`, **obligatorio**. El repositorio carga la entidad y fija la versión original: `repository.Get…(id, expectedVersion: request.Version)`.
- **Respuesta del guardado:** un `PUT` o `POST` con `version` responde 200 con `VersionResponse` (`{ version }`): la versión que quedó después del commit, la que EF trae del mismo `UPDATE` o `INSERT`. El trabajo de la transacción devuelve la entidad guardada y el servicio arma la respuesta después de `ExecuteInTransactionAsync`, con `VersionResponse.Of(result)`; nunca la relee. Un `DELETE` responde 204.
- **Guardado sin cambios:** también compara la versión. Una entidad `IAuditable` se escribe sola (`ModifiedAtUtc`), `MarkUpdated` escribe todo y el perfil de la cuenta sube su versión. Sin una columna escrita, EF no manda el `UPDATE`, no compara la versión y la respuesta llevaría una que la pantalla no vio.
- **Excepción, «Tus datos» de Mi cuenta:** el perfil (nombre, idioma y zona) vive en la fila de la cuenta, que también cambia con el contacto principal, la última organización, las sesiones y los intentos fallidos; con el `xmin` de la fila, esas escrituras darían un 409 falso al guardar el perfil. Por eso el perfil lleva su propia versión, `ProfileVersion`: una columna que sube `UpdateProfileAsync` en cada guardado del perfil y la importación Google cuando completa alguna parte vacía del nombre (ADR 0046). Repetir Google sin completar nada no la cambia. El repositorio la compara con la que mandó la pantalla (si no coincide, 409) y dos guardados a la vez los separa el `xmin`, que EF compara en el mismo `UPDATE`. `/api/me` la devuelve como `version` (0 si el perfil nunca se guardó) y el `PUT` responde la nueva. Es la única columna de versión a mano.
- **Conflicto:**
  - `SaveChanges` lanza `DbUpdateConcurrencyException`;
  - `UnitOfWork` hace rollback y la traduce a `ConcurrencyConflictException`;
  - `ProblemDetailsMapper` responde 409 `General.ConcurrencyConflict`.
  
  Es la **única** excepción que llega al mapper como respuesta de negocio, porque siempre se responde igual.
- Las acciones de un solo clic que no dependen de lo que el usuario vio (activar, desactivar) también mandan `version`, así una fila vieja en pantalla no revierte un cambio.
- **Excepción, las transiciones de estado sin formulario sobre la cuenta del usuario (T18b):** suspender, reactivar, cerrar sesiones y dar de baja una cuenta desde Plataforma no llevan `version`. Es la misma fila de la excepción anterior: su `xmin` cambia con un ingreso, el reinicio de intentos fallidos, el sello de seguridad y el contacto principal, y comparar la versión daría un 409 falso si la persona ingresa mientras el operador mira la ficha. La acción no edita lo que el operador vio, así que la protege el **estado de la transición**, dentro de la transacción y con el lock de la cuenta (después del lock se relee la cuenta y el dominio decide): suspender solo una cuenta activa, reactivar solo una suspendida y la baja solo una activa o suspendida sin baja programada. Una acción repetida o contradictoria a la vez recibe el error de estado, 409 `Users.User.InvalidTransition` o `Legal.AccountDeletion.AlreadyPending`: una gana y la otra lo recibe, y nunca `General.ConcurrencyConflict`. Cerrar sesiones no cambia el estado: repetirla no choca. La regla vale para una transición de una fila que otras escrituras también cambian; sobre una fila que solo cambia por acciones del operador (un pedido de recuperación, la asignación de permisos de un operador) la acción sigue llevando su versión.
- **Front:** el guardado de una pantalla manda la versión de lo que la persona está editando: la de la lectura con la que arrancó o se reinició el formulario (al cargar, con «Ver lo nuevo» y al descartar) y, después de guardar, la que respondió el guardado; nunca la de una relectura. Si otra persona guardó mientras la ficha estaba quieta, el back responde 409 y sale el cartel con «Ver lo nuevo». Lo lleva `useEditForm` (front: `docs/rules/formularios.md`, "Ediciones simultáneas").

## Prohibido
- Una columna `Version` o `RowVersion` manual incrementada a mano, salvo `ProfileVersion` de «Tus datos» (la excepción de arriba).
- Pedir `version` en una transición de estado sin formulario sobre una fila que otras escrituras también cambian (la cuenta del usuario: suspender, reactivar, cerrar sesiones, la baja): un ingreso daría un 409 falso. La protege el estado de la transición, bajo el lock.
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
- `VersionedContractTests` (E7): todo `PUT` o `POST` con `version` declara 200 con `VersionResponse` y no 204 (test de arquitectura).
- Por área, el guardado responde la versión nueva, es la que lee la ficha y sirve para el guardado siguiente sin 409 (con `SavedVersion`): `RolesTests`, `OrganizationUsersTests`, `CompaniesTests`, `OrganizationSettingsTests`, `PublicPagesTests`, `AccountProfileTests`, `PlatformSettingsTests`, `PlatformFeatureAdministrationTests` y `PlatformOperatorTests` (E7), y las acciones de Plataforma de `PlatformTenantAdministrationTests`, `PlatformTenantProvisioningHttpTests`, `PublicPageModerationTests`, `PlatformDomainAdministrationTests` y `PlatformRecoveryTests` (T18c). En una acción terminal, la versión respondida coincide con la lectura y reutilizarla recibe el error de estado previsto, nunca `General.ConcurrencyConflict`. Un guardado viejo que no cambia nada da 409: `AccountProfileTests` y `OrganizationUsersTests` (E7). `ProfileServiceTests` (E7): la respuesta lleva la versión que dejó el guardado del perfil.
- `GoogleNamesTests` (E3, ajuste A7): completar nombres desde Google invalida una edición anterior del perfil; repetir sin cambios no avanza la versión.
- `AccountProfileTests` (E7): «Tus datos» guarda sin 409 con la versión leída antes de entrar a otra organización, cambiar el contacto principal, contar y reiniciar intentos fallidos y cerrar las sesiones; dos guardados del perfil con la misma versión sí dan 409.
- `PlatformAccountStateTests` (E7): suspender, reactivar, cerrar sesiones y dar de baja una cuenta sin versión, con una escritura en la fila de la cuenta entre la lectura de la ficha y la acción (`A_write_to_the_account_between_the_sheet_and_the_action_does_not_conflict`: un ingreso con un código mal escrito, intentos fallidos y su reinicio, el contacto principal), que no da 409; y dos pedidos iguales a la vez, donde uno gana y el otro recibe el error de estado, y repetir cerrar sesiones no choca (`Two_contradictory_actions_at_once_one_wins_and_the_other_gets_the_state_error`).
- Front: `useEditForm.test.tsx` (E7) y las pruebas de Roles, Usuario, Configuración, Página pública y Mi cuenta (E7): una relectura de fondo con otra versión no cambia la que manda el guardado; sale el 409 y no se pisa lo de la otra persona.

## Detalle
[backend.md §20](../architecture/backend.md#20-reglas-de-datos-que-se-aplican-solas) · front: `docs/rules/formularios.md`, "Ediciones simultáneas".
