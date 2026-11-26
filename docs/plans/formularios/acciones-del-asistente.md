# Acciones del asistente (spec)

**Estado:** pendiente · spec de la etapa «Acciones del asistente», que va después de la segunda parte de la base y antes de la E12 ([ADR 0068](../../decisions/0068-acciones-del-asistente.md); decisión 268). El usuario eligió el lugar y el alcance el 2026-11-23: el motor y Usuarios y Roles enteros. Se contrasta con el código real con `/plan-etapa` cuando cierre la segunda parte de la base.

## Qué leer ✓

1. **Lo anterior:**
   - de la [segunda parte de la base](base-de-la-12-segunda-parte.md), «Nadie da lo que no tiene» (B1, con sus `Policy` y `grantable`), «Lo incluido» (B16), «Roles en áreas plegables» (F5) y «Una sola pieza para elegir» (F11);
   - de la [base](base-de-la-12.md), el `Inicio` de la organización y los diálogos de Usuarios.
2. **Decisiones:**
   - de la hoja de ruta: la 268, la 260 (lo incluido) y la 269;
   - las ADR [0068](../../decisions/0068-acciones-del-asistente.md) (manda), [0048](../../decisions/0048-asistente.md) (D5 y D8), [0057](../../decisions/0057-la-ia-propone-y-una-persona-crea.md) (la propuesta en el mensaje), [0060](../../decisions/0060-dos-versiones-conviven.md), [0061](../../decisions/0061-nadie-da-lo-que-no-tiene.md), [0064](../../decisions/0064-reglas-de-toda-la-ia.md) (§6, §9 y §10) y [0066](../../decisions/0066-lo-que-no-se-sabe-si-salio.md) («Sin confirmar»).
3. **Fichas:**

| Ficha | Por qué |
| --- | --- |
| [capas-y-flujo](../../rules/capas-y-flujo.md) | el sufijo `ActionSource` y la lista de dependencias permitidas |
| [permisos](../../rules/permisos.md) | el requisito es el de la ruta; la IA no tiene permisos propios |
| [multitenancy](../../rules/multitenancy.md) | RLS, lo público por el claim, la empresa y el 404 |
| [idempotencia](../../rules/idempotencia.md) | la clave de cada intento y el 422 |
| [concurrencia](../../rules/concurrencia.md) | editar con la versión y el 409 |
| [datos-personales](../../rules/datos-personales.md) | la propuesta guarda un correo y se borra con su conversación |
| [textos-y-traducciones](../../rules/textos-y-traducciones.md) | `Action.*` en es y en |
| [tests](../../rules/tests.md) | la batería genérica |

4. **Código para copiar:**
   - la fuente, de `Application/Services/OrganizationSettings/OrganizationDomainPendingItems.cs` (`IPendingItemSource`);
   - las herramientas, de `Application/Modules/Assistant/Tools/AssistantTool.cs` y `Services/AssistantTurnToolBuilder.cs`;
   - las rutas de la propuesta, de `Api/Modules/Assistant/Controllers/BusinessAssistantController.cs`;
   - la entidad privada, de `Domain/Assistant/AssistantMessage.cs` y su configuración;
   - en el front, el registro de `src/shared/modules/ModuleRegistry.tsx`, el diálogo `src/areas/business/users/components/InviteUserDialog.tsx`, `src/areas/business/users/pages/UserDetailPage.tsx`, `src/areas/business/roles/pages/RoleEditorPage.tsx` y `src/shared/api/useIdempotentMutation.ts`.
5. **Referencia de producto,** solo para leer (de ArquitecturaBase, `git show origin/main:<ruta>`):
   - su ADR 0034;
   - la sección «Acciones del asistente» de `docs/plans/2026-11-19-procesos-del-campo.md`;
   - sus tableros AC0 a AC7 y AU0 a AU8.

## Qué se construye

**La regla** es la de la 0068: la IA prepara, la persona confirma en la pantalla de siempre y guarda la ruta de siempre.

**Seis acciones,** todas en el Inicio de la organización:

| Acción | Qué prepara | Dónde se confirma | Ruta que guarda |
| --- | --- | --- | --- |
| `users.invite@1` · Invitar usuario | el correo, los roles de la organización, una empresa y sus roles | el diálogo «Invitar usuario», sobre el Inicio | `POST api/users/invitations` |
| `users.edit@1` · Cambiar roles | sumar o quitar roles de la organización y, en una empresa, sus roles | la ficha de la persona (`UserDetailPage`), sin guardar | `PUT api/users/{memberId}` |
| `users.remove@1` · Quitar de la organización | a quién | la confirmación de quitar, de esa ficha | `DELETE api/users/{memberId}` |
| `roles.create@1` · Nuevo rol | nombre, descripción, alcance, empresa (si es de empresa) y permisos | el editor de roles (`RoleEditorPage`), sin guardar | `POST api/roles` |
| `roles.edit@1` · Cambiar un rol | nombre, descripción y sumar o quitar permisos | el editor de ese rol | `PUT api/roles/{id}` |
| `roles.remove@1` · Dar de baja un rol | cuál | la confirmación de dar de baja de Roles | `DELETE api/roles/{id}` |

**Qué se puede proponer:**
- **Roles y permisos:** solo los roles y permisos que quien pide puede dar, con lo incluido (0061, 260). Lo demás no se resuelve y vuelve al modelo.
- **Quitar o dar de baja:** a una persona o un rol que la ruta dejaría quitar o dar de baja. Lo que esa ruta frena (el último Dueño, un rol con personas) lo dice su confirmación, como siempre.

**Cuándo se ofrecen:** las seis quedan registradas y **no se ofrecen**. Se ofrecen recién cuando pasan la calificación con un modelo real, antes de la prueba integral del bloque, con el visto bueno del usuario. Hasta entonces, la lista de calificadas está vacía en todos los ambientes. Los tests y el recorrido usan un modelo falso con su propia calificación de prueba.

**La tarjeta** va en el hilo, después del texto de la IA:
- se nombra con un sustantivo («Invitación de usuario», «Cambio de roles», «Quitar de la organización», «Rol nuevo», «Cambio de rol» y «Baja de rol», con el verbo de la pantalla de Roles, como eligió el usuario el 2026-11-23);
- muestra cada dato propuesto con su nombre, leído del servidor, y «Revisá» con el motivo en lo dudoso;
- trae un botón: «Revisar e invitar», «Revisar cambios», «Revisar y crear» o, en rojo contorneado, «Revisar y quitar» / «Revisar y dar de baja».

**Los estados de la tarjeta:**
- **nueva;**
- **«Reemplazada»:** otra propuesta posterior de su clase, en esa conversación, la reemplazó;
- **«Ya no disponible»,** con el motivo: sin el permiso, sin el módulo, de otra versión, ya no está a quién se refiere, o la cuenta se dio de baja;
- **aplicada:** se pliega en «Invitación enviada», «Roles cambiados», «Quitado», «Rol creado», «Rol cambiado» o «Rol dado de baja», con el enlace a lo hecho si se puede ver;
- **«Sin confirmar»:** pasó la validez de la clave del último intento (24 h de entrada) sin saber si se guardó, con el enlace al listado para revisarlo.

**Abrir:** el botón abre la pantalla de la acción con `?proposal=<id>`, que lee la propuesta del servidor.
- **El diálogo** abre sobre el Inicio, y las páginas, en su lugar.
- **Lo que se ve:** la pantalla lleva «Propuesta del asistente» junto al título, todo lo propuesto cargado y sin guardar, y «Revisá» en lo dudoso.
- **«Invitar usuario»** avisa antes de «Invitar» que manda la invitación.
- **Editar** muestra «Lo que cambia», con lo que se suma y lo que se quita. Si la persona o el rol cambió desde la propuesta, lo avisa arriba.
- **Si un valor no se puede mostrar,** la pantalla no abre la propuesta.

**Guardar** es el botón de siempre, con sus errores de siempre. Después de un 2xx, la tarjeta pasa a aplicada. Si otra pestaña ya la guardó, el diálogo dice «Ya se guardó» y lleva a lo creado.

## Pantallas ✓

**Aprobados:** `Asistente-Org-Acciones`, `M-Asistente-Org-Acciones`, `Usuarios-Invitar-Propuesta`, `M-Usuarios-Invitar-Propuesta`, `Usuario-Propuesta`, `M-Usuario-Propuesta`, `Rol-Propuesta`, `M-Rol-Propuesta`, `Confirmar-Propuesta` y `M-Confirmar-Propuesta`, en el lienzo v179 (página «Organización · Acciones del asistente»), aprobados por el usuario el 2026-11-23. `Confirmar-Propuesta` se redibujó en la v184 (↑ en la lista de atrás y «Ordenar por» al final de la barra, 264), aprobado el 2026-11-26.

| Tablero | Qué muestra |
| --- | --- |
| `Asistente-Org-Acciones` | el Inicio con la tarjeta de cada acción: nueva, con «Revisá», reemplazada, ya no disponible (con el motivo), aplicada (plegada) y sin confirmar; la de quitar con el botón rojo |
| `Usuarios-Invitar-Propuesta` | el diálogo «Invitar usuario» sobre el Inicio, con «Propuesta del asistente», un rol con «Revisá», lo que no se puede dar apagado con su motivo, el aviso de que manda la invitación y «Ya se guardó» |
| `Usuario-Propuesta` | la ficha de una persona con «Lo que cambia» (un rol que se suma y otro que se quita) y el aviso de que cambió desde la propuesta |
| `Rol-Propuesta` | el editor de roles con un rol nuevo propuesto (áreas plegables, 260) y con un cambio de permisos, en «Lo que cambia» |
| `Confirmar-Propuesta` | la confirmación de quitar a una persona y la de dar de baja un rol, abiertas desde la tarjeta |

Cada uno lleva su teléfono (`M-`). Manda el tablero aprobado, salvo lo que contradiga una decisión o un `Criterio-*`; el tablero muestra solo lo que existe hasta esta etapa (78).

## Contrato ✓

**Rutas nuevas** (módulo Assistant, `[Access(Business)]`, `[FeatureGate("assistant")]` y `assistant.use`; la propuesta tiene que ser de quien pide):

| Ruta | Qué pide | Qué devuelve |
| --- | --- | --- |
| `GET /api/organization/assistant/proposals/{proposalId}` | — | 200 `AssistantProposalResponse` |
| `POST /api/organization/assistant/proposals/{proposalId}/attempts` | `{}` | 200 `{idempotencyKey}`: la clave del intento de guardar |
| `POST /api/organization/assistant/proposals/{proposalId}/applied` | `{version?}` | 200 `AssistantProposalResponse`, ya aplicada |

**`AssistantProposalResponse`:**
- `{id, conversationId, kind: "action", action, version, state, unavailableReason?, values, target?, appliedLocation?}`;
- `state` es `new`, `replaced`, `applied`, `unconfirmed` o `unavailable`;
- `unavailableReason` es `permission`, `module`, `version`, `target` o `subject`;
- `values` es `[{field, value, label?, doubt?}]`, con `label` resuelto en el servidor y `doubt` como clave de texto;
- `target` es `{id, label, version}`, el de ahora.

**Cómo se calcula el estado:** se calcula en cada lectura, con los permisos de ese momento y en la empresa de la acción. El `GET` vuelve a resolver con la fuente el destino y cada `Record`.

**Las otras dos rutas:**
- **`attempts`:** devuelve la misma clave mientras ese intento no termine en 4xx. Si una clave de la propuesta ya terminó en 2xx, la marca aplicada y responde `AlreadyApplied`. Es idempotente por naturaleza y responde 200, así que `IdempotentActionsTests` no le exige `[Idempotent]`; el motivo va en un comentario de la acción.
- **`applied`:** lo comprueba el servidor:
  - al crear, que una clave de la propuesta terminó en 2xx para esa persona y esa `SaveRoute`;
  - al quitar o dar de baja, que el destino ya no se resuelve;
  - al editar, que la versión de ahora es la del cuerpo y distinta de la de la propuesta.

**`Message`** (historial y SSE) suma `proposal?` `{id, kind, action, version, state}`. Lo demás lo trae el `GET`.

**El contrato de las acciones:**
- el build publica `docs/contracts/assistant-actions.json`, con cada acción: id, versión, clase, requisito, `SaveRoute`, confirmación, pantalla, efecto, campos (tipo, obligatorio, límites, `DependsOn`) y destino;
- el front genera sus tipos con `npm run contracts`;
- **lo armado como dato** (0068, «Consecuencias»): una acción puede declarar que sus campos salen de la definición de su destino, como las opciones de un catálogo. `read_action` con el destino da esos campos, con su tipo y sus límites, y `assistant-actions.json` publica la acción con esa marca y sin campos fijos.

**Las herramientas** (no son HTTP; su forma va en `docs/features/asistente.md`):
- `assistant_actions`, precargada: `[{action, name}]`, o las áreas si son más de 40;
- `read_action` `{action | area, field?, search?}`;
- `propose_action` `{action, target?, values:[{field, value}]}`, que devuelve `AssistantActionToolData`:
  - `accepted` con el id de la propuesta;
  - o `rejected`, con `[{path, code}]`, o con `OnePerTurn`, `NotAvailable` o `Invalid`.

**Errores** (textos en `AssistantTexts.resx` y `.en.resx`):

| Código | Tipo | Cuándo |
| --- | --- | --- |
| `Assistant.Proposal.NotFound` | NotFound | no existe, o es de otra persona u organización |
| `Assistant.Proposal.Unavailable` | Conflict | `attempts` o `applied` sobre una reemplazada, no disponible o sin confirmar; con el motivo |
| `Assistant.Proposal.AlreadyApplied` | Conflict | `attempts` sobre una aplicada o con una clave que terminó en 2xx, con `location` |
| `Assistant.Proposal.NotApplied` | Conflict | `applied` sin poder comprobarlo |
| `Assistant.Action.NotAvailable`, `.Invalid` y `.OnePerTurn` | — | solo dentro de `AssistantActionToolData`, nunca como HTTP |

**Las rutas de Usuarios y Roles no cambian.** Un 422 `Request.IdempotencyKeyReused` no prueba que se guardó: la clave guarda también un 4xx, y el 422 dice «misma clave con otro cuerpo». Ante un 422 con una clave de la propuesta, el front vuelve a llamar a `attempts`: un 409 `Assistant.Proposal.AlreadyApplied` con `location` es «Ya se guardó», y una clave nueva es otro intento.

**Concurrencia:** editar manda la versión leída al abrir, y vale el 409 de siempre. Sin permiso nuevo.

## Datos ✓

**`tenant.AssistantProposals`** (`ITenantOwned`, RLS forzado, sin auditoría de entidad porque su contenido es de un chat, como los mensajes):

| Columna | Tipo | Regla |
| --- | --- | --- |
| `Id`, `TenantId`, `ConversationId`, `MessageId`, `OwnerUserId` | uuid | FK compuesta al mensaje, con borrado en cascada |
| `Kind` | texto | `Action` |
| `ActionId`, `ActionVersion` | texto ≤80, int | sellados |
| `Values` | jsonb | por campo: lo que escribió el modelo, el id resuelto y la duda; nunca nombres resueltos |
| `TargetId?`, `BaseVersion?` | uuid, int | Edit y Remove |
| `State` | texto | `New`, `Replaced` o `Applied`; «no disponible» y «Sin confirmar» se calculan al leer |
| `AttemptKeys` | jsonb | hasta 10 claves con su fecha |
| `AppliedAtUtc?`, `AppliedLocation?` | timestamptz, texto ≤2048 | — |
| `CreatedAtUtc` y versión (`xmin`) | — | — |

- **«Sin confirmar»** sale al leer, como «no disponible»: hay claves en `AttemptKeys`, ninguna terminó en 2xx y pasó la validez de la clave (`IdempotencyOptions.ValidityHours`, 24 h de entrada) desde la última. Vencida, `IdempotencyStore` la borra y la vuelve a reservar, así que otro guardado ya no sería único. No lo escribe ningún worker.
- **Índice** parcial por `(TenantId, ConversationId, Kind)` con `State = 'New'`, para reemplazar dentro de su clase.
- **La migración** es aditiva, y la tabla se suma al inventario del módulo (`AssistantModelContributor`), que la quita si se quita el asistente.
- **Las personas que nombra una propuesta** (el destino y los `Record` de una persona) se suman a los `SubjectUserIds` de su mensaje.
- **En la historia del turno siguiente,** el mensaje con una propuesta suma la acción y lo que escribió el modelo, nunca lo resuelto ni los nombres (0068 §7; 0064 §9 y §10). El pedido lleva la conversación entera, así que la última propuesta nunca queda afuera.
- **Se borra** con su conversación, con la retención de las conversaciones del Inicio (`retentionDays`, 30 por defecto; 272), salvo las guardadas, y con la baja de la cuenta. Es una tabla del módulo: no entra en `PersonalDataPurgeTests`, que mira solo el modelo del núcleo (ADR 0062); su borrado lo prueba la B5.

**`tenant.AssistantTurns`** suma `OfferedActions` (jsonb, nulo): la lista de acciones que se ofreció en el turno, sellada (0068 §6). Su migración va con la B4. Un turno que ofrece acciones se sella con `EngineVersion` 2 (`AssistantTurnWork.CurrentEngineVersion` pasa a 2; uno sin acciones sigue en 1): una copia anterior no lo toma (0064 §6, B14 de la base 2), sin excepción de la 0060.

**`platform.IdempotencyKeys`** no cambia: `IIdempotencyStore` suma una lectura, `FindAsync(scope, userId, key)`, con el estado, el código y la ruta.

**La calificación** vive en `Application/Modules/Assistant/Actions/qualified-actions.json`: `[{action, version, providerModelId, qualifiedOn, measuredCost}]`. Está vacía en esta etapa y la carga un test de su forma.

## Pasos ✓

**Back,** con las esperas del plan:

| # | Qué hace | Archivos | Copia de | Test |
| --- | --- | --- | --- | --- |
| B1 | Los tests de arquitectura de las acciones y los endurecimientos de la 0068. Los modelos, el puerto, las declaraciones, la lista cerrada de pantallas del núcleo (`AssistantActionScreen`, que el módulo traduce a su `AssistantRouteKey`) y la fila `ActionSource` de capas-y-flujo entran antes, con el contrato. Los usos de `ExecuteSqlRaw` de los tests (ocho, en seis archivos) pasan a `ExecuteSqlAsync` interpolado | `Application/Models/AssistantActions/*`, `Application/Interfaces/Services/IAssistantActionSource.cs`, `BannedSymbols.txt`, `RuntimeRoleTests` y esos tests | `IPendingItemSource` | `AssistantActionDeclarationTests` (en `ArchitectureTests/`, y la traducción de cada pantalla, en `ArchitectureTests/Modules/Assistant/`): id, ruta y requisito por reflexión de atributos, campos (un `Items` con un solo nivel, su tope y su clave), textos, casos, el árbol de dependencias con su lista de lo permitido, también a través de cada puerto lector del núcleo, `Company` con su campo, `Remove` con `RemoveConfirmation`, ninguna ruta de la lista de lo que no se declara y cada pantalla con su traducción |
| B2 | El alcance de solo lectura: un alcance de DI propio. Entra con el actor revalidado (worker) o con el tenant del claim (petición), abre `READ ONLY` y se descarta. Corren ahí las fuentes y las 19 lecturas | el puerto `IAssistantReadOnlyScope` en `Application/Modules/Assistant/Interfaces/Integrations/`, implementado en `Infrastructure/Modules/Assistant/` | `ExecutionActorContext` (`IExecutionActorScope`), `TenantContext` | una fuente de prueba que escribe falla sin dejar filas; tenant correcto en los dos caminos; las lecturas de hoy siguen verdes |
| B3 | Lo que tiene quien pide, sin el `Guard`: las `Policy` de la 0061 ya son puras (B1 de la base 2), y `ReadHeldAsync` y `ReadAssignedAsync` pasan de `OrganizationDelegationGuard` a `DelegationPermissionsResolver` (solo con `ICurrentUser` e `IRoleReader`), que usan los dos `Guard`, `RoleService` y las fuentes, sin cambiar la conducta de la ruta | `OrganizationDelegationGuard.cs`, `CompanyDelegationGuard.cs`, `RoleService.cs` y `Application/Services/Roles/DelegationPermissionsResolver.cs` (nuevo) | `OrganizationDelegationGuard.ReadHeldAsync` | `DelegatedAdministrationTests` sin cambios; la `Policy` y su `Guard` deciden igual en la organización y en una empresa, y el `Guard` y el resolvedor leen lo mismo |
| B4 | El registro y las tres piezas: el filtro por llamada, el sello en `AssistantTurns.OfferedActions` con su migración y `EngineVersion` 2, la lista precargada en la preparación del turno, `read_action`, `propose_action`, `AssistantActionToolData` en el `switch` cerrado y la calificación | `Application/Modules/Assistant/Actions/*`, `AssistantTurnWork`, `AssistantTurnPreparationBuilder`, `AssistantTurnToolBuilder` | `AssistantToolCatalog`, `AssistantTool` | catálogo, sello y su versión, una por turno, rechazo sin cortar el turno, sin calificación no se ofrece, `ModelSnapshotTests` |
| B5 | La propuesta: entidad, migración, reemplazo por clase, sujetos, retención y baja, y su lugar en la historia del turno siguiente. Las tres rutas, `FindAsync` del almacén y «Sin confirmar» calculado al leer | `Domain/Assistant/AssistantProposal.cs`, configuración, migración, `AssistantModelContributor`, `AssistantTurnWriter`, `AssistantProposalService`, controller | `AssistantMessage`, `AssistantConversationService` | aislamiento entre personas y organizaciones, estados, re-resolución, aplicada comprobada, `attempts` después de un 2xx, la retención y la baja la borran, el pedido siguiente la trae sin lo resuelto, `ModelSnapshotTests` |
| B6 | `OrganizationUserActionSource`, con las tres de Usuarios, la empresa por su permiso y el aviso de la invitación | `Application/Services/OrganizationUsers/OrganizationUserActionSource.cs` | `OrganizationDomainPendingItems` | escenarios de la batería |
| B7 | `RoleActionSource`, con las tres de Roles y los permisos del alcance que quien pide tiene. Quitar un permiso que sigue incluido por otro que queda (260) se rechaza con su ruta (`Invalid`) | `Application/Services/Roles/RoleActionSource.cs` | B6 | escenarios de la batería |
| B8 | La batería genérica, los escenarios y el contrato publicado | `Api.IntegrationTests/Modules/Assistant/AssistantActionContractTests.cs`, `TestFeatures/AssistantActions/*`, el generador de `assistant-actions.json` | `RouteIsolationMatrixTests` | la batería de la 0068 para las seis |

**Front:**

| # | Qué hace | Archivos | Copia de | Test |
| --- | --- | --- | --- | --- |
| F1 | El registro de adaptadores y `useAssistantProposal`: leer por id, marcas, operaciones, la clave de cada intento, el 422 que vuelve a preguntar a `attempts`, «Ya se guardó» y `applied` | `src/shared/assistant-actions/*` | `ModuleRegistry.tsx`, `useIdempotentMutation.ts` | hook, el 422 que se guardó y el que no, y límites del módulo |
| F2 | La tarjeta en el hilo, con sus seis estados | `src/modules/assistant/components/AssistantActionCard.tsx` | `ToolStatus.tsx` | estados, teclado y axe |
| F3 | Los adaptadores de Usuarios: el diálogo sobre el Inicio, la ficha con «Lo que cambia» y quitar | `InviteUserDialog.tsx`, `UserDetailPage.tsx` | — | todo lo propuesto se ve, y lo enviado es lo que se ve |
| F4 | Los adaptadores de Roles: crear, cambiar y dar de baja | `RoleEditorPage.tsx`, `RolesPage.tsx` | F3 | ídem |
| F5 | Un adaptador por acción del contrato | `src/shared/assistant-actions/coverage.test.ts` | — | falla si falta uno |

## Acciones del asistente ✓

Las seis de «Qué se construye»: esta etapa arma la mecánica de la 0068 y las declara.

## Qué verificar al arrancar ✓

| Marca | Qué hay que confirmar | Qué asume esta spec |
| --- | --- | --- |
| Base 2 · B1 | `OrganizationDelegationGuard`, `CompanyDelegationGuard`, sus `Policy` y los 403 de la 0061 en invitar, editar y roles | existen, con las `Policy` puras; B3 saca del `Guard` la lectura de lo que tiene quien pide |
| Base 2 · B16 | lo incluido en la proyección efectiva (`Permissions.Inclusions`) | los permisos de quien pide ya lo incluyen |
| Base 2 · F5 | cómo sabe el front qué roles y permisos puede dar quien pide | `grantable` en los roles y en el catálogo (269), que calcula la misma `Policy` que usan las fuentes; lo que difiera va a «Cambios al contrato» en K0 |
| Base 2 · F11 | `Picker` con `selectedOptions`, lo elegido que no está en la página cargada | existe; F3 y F4 le pasan lo propuesto con el `label` de la propuesta, sin buscar por id |
| Base 2 · B9 | `CoreTextsNeutralTests` y `ModelSnapshotTests` | existen; K1 y las migraciones de B4 y B5 los corren |
| Base 2 · B14 | `AssistantTurnWork.CurrentEngineVersion` (1) | B4 sella 2 en un turno con acciones |
| Tools | las 19 lecturas no usan `IUnitOfWork` en su camino | corren en `READ ONLY` sin cambios |
| `UpdateOrganizationUserRequest` | reemplaza las listas enteras | la ficha aplica las operaciones sobre lo leído y manda las listas completas |
| Rol | la pantalla no deja cambiar alcance ni empresa al editar (la ruta sí, sin personas) | `roles.edit@1` no los propone, y el adaptador manda los leídos |

## Decisiones tomadas

- **El alcance:** las seis acciones de Usuarios y Roles; elegido por el usuario el 2026-11-23.
- **«Dar de baja» un rol:** la tarjeta usa el verbo de la pantalla de Roles («Baja de rol», «Revisar y dar de baja», «Rol dado de baja»), y no «borrar»; elegido por el usuario el 2026-11-23.
- **Cuándo se ofrecen:** juntas, calificadas, antes de la prueba integral del bloque, como eligió el usuario.
- **Lo demás** lo fija la 0068: dónde se confirma, lo que nunca se propone, al proveedor lo mínimo y una por turno.

## Fuera de esta etapa

- **Plataforma y Personal:** todavía no tienen acciones.
- **WhatsApp:** cuando el canal tenga lugar.
- **Otras acciones de Usuarios:** los roles y miembros de una empresa desde su pantalla, y dar de baja o reactivar a una persona.
- **Las acciones de las etapas siguientes:** desde la E12, cada una declara las de sus pantallas.
- **La calificación con el modelo real:** antes de la prueba integral, con el visto bueno del usuario.

## Cómo se cierra ✓

La puerta de la etapa, con la lista de control de `/puerta`, suma lo propio:
- **la batería genérica** en verde para las seis acciones;
- **el E2E** «Invitar usuario» desde una propuesta del modelo falso, con «Ya se guardó» en una segunda pestaña;
- **el recorrido de cada acción** con el modelo falso;
- **la galería** de los tableros nuevos, lado a lado.

No hay prueba con Meta ni con un proveedor real.

## Requisitos previos

- La segunda parte de la base, cerrada.
- Los tableros de «Pantallas», aprobados en el lienzo v179 el 2026-11-23.
