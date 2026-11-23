# Acciones del asistente (spec)

**Estado:** pendiente · spec de la etapa «Acciones del asistente», que va después de la segunda parte de la base y antes de la E12 ([ADR 0068](../../decisions/0068-acciones-del-asistente.md); decisión 268). El usuario eligió el lugar y el alcance el 2026-11-23: el motor y Usuarios y Roles enteros. Se contrasta con el código real con `/plan-etapa` cuando cierre la segunda parte de la base.

## Qué leer ✓

1. **Lo anterior:**
   - de la [segunda parte de la base](base-de-la-12-segunda-parte.md), «Nadie da lo que no tiene» (B1), «Lo incluido» (B16), «Roles en áreas plegables» (F5) y «Una sola pieza para elegir» (F11);
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
- **«Reemplazada»:** otra propuesta posterior de esa conversación la reemplazó;
- **«Ya no disponible»,** con el motivo: sin el permiso, sin el módulo, de otra versión, ya no está a quién se refiere, o la cuenta se dio de baja;
- **aplicada:** se pliega en «Invitación enviada», «Roles cambiados», «Quitado», «Rol creado», «Rol cambiado» o «Rol dado de baja», con el enlace a lo hecho si se puede ver;
- **«Sin confirmar»:** pasaron 24 h sin saber si se guardó, con el enlace al listado para revisarlo.

**Abrir:** el botón abre la pantalla de la acción con `?proposal=<id>`, que lee la propuesta del servidor.
- **El diálogo** abre sobre el Inicio, y las páginas, en su lugar.
- **Lo que se ve:** la pantalla lleva «Propuesta del asistente» junto al título, todo lo propuesto cargado y sin guardar, y «Revisá» en lo dudoso.
- **«Invitar usuario»** avisa antes de «Invitar» que manda la invitación.
- **Editar** muestra «Lo que cambia», con lo que se suma y lo que se quita. Si la persona o el rol cambió desde la propuesta, lo avisa arriba.
- **Si un valor no se puede mostrar,** la pantalla no abre la propuesta.

**Guardar** es el botón de siempre, con sus errores de siempre. Después de un 2xx, la tarjeta pasa a aplicada. Si otra pestaña ya la guardó, el diálogo dice «Ya se guardó» y lleva a lo creado.

## Pantallas ✓

**A revisar:** `Asistente-Org-Acciones`, `M-Asistente-Org-Acciones`, `Usuarios-Invitar-Propuesta`, `M-Usuarios-Invitar-Propuesta`, `Usuario-Propuesta`, `M-Usuario-Propuesta`, `Rol-Propuesta`, `M-Rol-Propuesta`, `Confirmar-Propuesta` y `M-Confirmar-Propuesta`, en el lienzo v178 (página «Propuesta · Acciones del asistente»). El usuario los aprueba antes de las tareas del front que los usan.

| Tablero | Qué muestra |
| --- | --- |
| `Asistente-Org-Acciones` | el Inicio con la tarjeta de cada acción: nueva, con «Revisá», reemplazada, ya no disponible (con el motivo), aplicada (plegada) y sin confirmar; la de quitar con el botón rojo |
| `Usuarios-Invitar-Propuesta` | el diálogo «Invitar usuario» sobre el Inicio, con «Propuesta del asistente», un rol con «Revisá», lo que no se puede dar apagado con su motivo, el aviso de que manda la invitación y «Ya se guardó» |
| `Usuario-Propuesta` | la ficha de una persona con «Lo que cambia» (un rol que se suma y otro que se quita) y el aviso de que cambió desde la propuesta |
| `Rol-Propuesta` | el editor de roles con un rol nuevo propuesto (áreas plegables, 260) y con un cambio de permisos, en «Lo que cambia» |
| `Confirmar-Propuesta` | la confirmación de quitar a una persona y la de dar de baja un rol, abiertas desde la tarjeta |

Cada uno lleva su teléfono (`M-`). Manda sobre el dibujo esta spec: el tablero muestra solo lo que existe hasta esta etapa.

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
- **`attempts`:** devuelve la misma clave mientras ese intento no termine en 4xx. Es idempotente por naturaleza, sin `[Idempotent]`, y va en la lista de excepciones de `IdempotentActionsTests` con su motivo.
- **`applied`:** lo comprueba el servidor:
  - al crear, que una clave de la propuesta terminó en 2xx para esa persona y esa `SaveRoute`;
  - al quitar o dar de baja, que el destino ya no se resuelve;
  - al editar, que la versión de ahora es la del cuerpo y distinta de la de la propuesta.

**`Message`** (historial y SSE) suma `proposal?` `{id, kind, action, version, state}`. Lo demás lo trae el `GET`.

**El contrato de las acciones:**
- el build publica `docs/contracts/assistant-actions.json`, con cada acción: id, versión, clase, requisito, `SaveRoute`, confirmación, pantalla, efecto, campos (tipo, obligatorio, límites, `DependsOn`) y destino;
- el front genera sus tipos con `npm run contracts`.

**Las herramientas** (no son HTTP; su forma va en `docs/features/asistente.md`):
- `assistant_actions`, precargada: `[{action, name}]`, o las áreas si son más de 40;
- `read_action` `{action | area, field?, search?}`;
- `propose_action` `{action, target?, values:[{field, value}]}`, que devuelve `AssistantActionToolData`:
  - `accepted` con el id de la propuesta;
  - o `rejected`, con `[{path, code}]`, o con `OnePerTurn`, `NotAvailable` o `Invalid`.

**Errores** (textos en `Assistant.resx` y `.en.resx`):

| Código | Tipo | Cuándo |
| --- | --- | --- |
| `Assistant.Proposal.NotFound` | NotFound | no existe, o es de otra persona u organización |
| `Assistant.Proposal.Unavailable` | Conflict | `attempts` o `applied` sobre una reemplazada, no disponible o sin confirmar; con el motivo |
| `Assistant.Proposal.AlreadyApplied` | Conflict | `attempts` sobre una aplicada, con `location` |
| `Assistant.Proposal.NotApplied` | Conflict | `applied` sin poder comprobarlo |
| `Assistant.Action.NotAvailable`, `.Invalid` y `.OnePerTurn` | — | solo dentro de `AssistantActionToolData`, nunca como HTTP |

**Las rutas de Usuarios y Roles no cambian.** Si un 422 `Request.IdempotencyKeyReused` llega con una clave de la propuesta, el front lo muestra como «Ya se guardó».

**Concurrencia:** editar manda la versión leída al abrir, y vale el 409 de siempre. Sin permiso nuevo.

## Datos ✓

**`tenant.AssistantProposals`** (`ITenantOwned`, RLS forzado, sin auditoría de entidad porque su contenido es de un chat, como los mensajes):

| Columna | Tipo | Regla |
| --- | --- | --- |
| `Id`, `TenantId`, `ConversationId`, `MessageId`, `OwnerUserId` | uuid | FK compuesta al mensaje, con borrado en cascada |
| `Kind` | texto | `Action` (la E25 suma las suyas) |
| `ActionId`, `ActionVersion` | texto ≤80, int | sellados |
| `Values` | jsonb | por campo: lo que escribió el modelo, el id resuelto y la duda; nunca nombres resueltos |
| `TargetId?`, `BaseVersion?` | uuid, int | Edit y Remove |
| `State` | texto | `New`, `Replaced`, `Applied` o `Unconfirmed`; «no disponible» se calcula |
| `AttemptKeys` | jsonb | hasta 10 claves con su fecha |
| `AppliedAtUtc?`, `AppliedLocation?` | timestamptz, texto ≤2048 | — |
| `CreatedAtUtc` y versión (`xmin`) | — | — |

- **Índice** parcial por `ConversationId` con `State = 'New'`, para reemplazar.
- **La migración** es aditiva, y la tabla se suma al inventario del módulo, que la quita si se quita el asistente.
- **Las personas que nombra una propuesta** (el destino y los `Record` de una persona) se suman a los `SubjectUserIds` de su mensaje.
- **Se borra** con su conversación, en la retención de 30 días (salvo las guardadas), y con la baja de la cuenta. El inventario de datos personales del módulo la suma.

**`platform.IdempotencyKeys`** no cambia: `IIdempotencyStore` suma una lectura, `FindAsync(scope, userId, key)`, con el estado, el código y la ruta.

**La calificación** vive en `Application/Modules/Assistant/Actions/qualified-actions.json`: `[{action, version, providerModelId, qualifiedOn, measuredCost}]`. Está vacía en esta etapa y la carga un test de su forma.

## Pasos ✓

**Back,** en orden y con un solo carril:

| # | Qué hace | Archivos | Copia de | Test |
| --- | --- | --- | --- | --- |
| B1 | Los tests de arquitectura de las acciones y los endurecimientos de la 0068. Los modelos, el puerto, las declaraciones y la fila `ActionSource` de capas-y-flujo entran antes, con el contrato | `Application/Models/AssistantActions/*`, `Application/Interfaces/Services/IAssistantActionSource.cs`, `BannedSymbols.txt`, `RuntimeRoleTests` | `IPendingItemSource` | `AssistantActionDeclarationTests`: id, ruta y requisito por reflexión de atributos, campos, textos, casos, el árbol de dependencias con su lista de lo permitido y `Company` con su campo |
| B2 | El alcance de solo lectura: un alcance de DI propio. Entra con el actor revalidado (worker) o con el tenant del claim (petición), abre `READ ONLY` y se descarta. Corren ahí las fuentes y las 19 lecturas | `Application/Modules/Assistant/Services/AssistantReadOnlyScope*.cs` y su adaptador en Infrastructure | `ExecutionActorScope`, `TenantContext` | una fuente de prueba que escribe falla sin dejar filas; tenant correcto en los dos caminos; las lecturas de hoy siguen verdes |
| B3 | La `Policy` de la 0061, separada de su `Guard`, que la sigue usando | `Services/Roles/OrganizationDelegationPolicy.cs` | `OrganizationDelegationGuard` | `DelegatedAdministrationTests` sin cambios, más la `Policy` pura |
| B4 | El registro y las tres piezas: el filtro por llamada, el sello en el turno, la lista precargada en la preparación del turno, `read_action`, `propose_action`, `AssistantActionToolData` en el `switch` cerrado y la calificación | `Application/Modules/Assistant/Actions/*`, `AssistantTurnPreparationBuilder`, `AssistantTurnToolBuilder` | `AssistantToolCatalog`, `AssistantTool` | catálogo, sello, una por turno, rechazo sin cortar el turno, sin calificación no se ofrece |
| B5 | La propuesta: entidad, migración, reemplazo, sujetos, retención y baja. Las tres rutas, `FindAsync` del almacén y «Sin confirmar» a las 24 h | `Domain/Assistant/AssistantProposal.cs`, configuración, migración, `AssistantProposalService`, controller | `AssistantMessage`, `AssistantConversationService` | aislamiento entre personas y organizaciones, estados, re-resolución, aplicada comprobada |
| B6 | `OrganizationUserActionSource`, con las tres de Usuarios, la empresa por su permiso y el aviso de la invitación | `Application/Services/OrganizationUsers/OrganizationUserActionSource.cs` | `OrganizationDomainPendingItems` | escenarios de la batería |
| B7 | `RoleActionSource`, con las tres de Roles y los permisos del alcance que quien pide tiene | `Application/Services/Roles/RoleActionSource.cs` | B6 | escenarios de la batería |
| B8 | La batería genérica, los escenarios y el contrato publicado | `Api.IntegrationTests/Modules/Assistant/AssistantActionContractTests.cs`, `TestFeatures/AssistantActions/*`, el generador de `assistant-actions.json` | `RouteIsolationMatrixTests` | la batería de la 0068 para las seis |

**Front:**

| # | Qué hace | Archivos | Copia de | Test |
| --- | --- | --- | --- | --- |
| F1 | El registro de adaptadores y `useAssistantProposal`: leer por id, marcas, operaciones, la clave de cada intento, «Ya se guardó» y `applied` | `src/shared/assistant-actions/*` | `ModuleRegistry.tsx`, `useIdempotentMutation.ts` | hook y límites del módulo |
| F2 | La tarjeta en el hilo, con sus seis estados | `src/modules/assistant/components/AssistantActionCard.tsx` | `ToolStatus.tsx` | estados, teclado y axe |
| F3 | Los adaptadores de Usuarios: el diálogo sobre el Inicio, la ficha con «Lo que cambia» y quitar | `InviteUserDialog.tsx`, `UserDetailPage.tsx` | — | todo lo propuesto se ve, y lo enviado es lo que se ve |
| F4 | Los adaptadores de Roles: crear, cambiar y dar de baja | `RoleEditorPage.tsx`, `RolesPage.tsx` | F3 | ídem |
| F5 | Un adaptador por acción del contrato | `src/shared/assistant-actions/coverage.test.ts` | — | falla si falta uno |

## Qué verificar al arrancar ✓

| Marca | Qué hay que confirmar | Qué asume esta spec |
| --- | --- | --- |
| Base 2 · B1 | `OrganizationDelegationGuard` y los 403 de la 0061 en invitar, editar y roles | existen; B3 separa la `Policy` |
| Base 2 · B16 | lo incluido en la proyección efectiva | los permisos de quien pide ya lo incluyen |
| Base 2 · F5 | cómo sabe el front qué roles y permisos puede dar quien pide | lo dice el servidor al leer los roles y el catálogo (269), y las fuentes usan la misma `Policy`; lo que difiera va a «Cambios al contrato» en K0 |
| Base 2 · F11 | `Picker` y su búsqueda por id | existe; F3 y F4 lo usan |
| Tools | las 19 lecturas no usan `IUnitOfWork` en su camino | corren en `READ ONLY` sin cambios |
| `UpdateOrganizationUserRequest` | reemplaza las listas enteras | la ficha aplica las operaciones sobre lo leído y manda las listas completas |
| Rol | alcance y empresa no cambian al editar | `roles.edit@1` no los propone |

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
- Los tableros de «Pantallas», aprobados en el lienzo.
