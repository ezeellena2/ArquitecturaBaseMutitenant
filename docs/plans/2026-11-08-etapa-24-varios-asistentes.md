# E24 · Varios asistentes, uno por equipo · plan ejecutable

**Estado:** pendiente · Spec: `docs/plans/formularios/24-varios-asistentes.md`. Tableros aprobados: lienzo v169, salvo los que la spec da como «Se redibujan» por el traspaso 18 (262 a 264), que se aprueban antes de F2. No hay decisión de producto pendiente.

| Parte | Tareas | Estado |
| --- | --- | --- |
| Contrato | K0–K1 | Pendiente |
| Back | B1–B6 | Pendiente |
| Front | F1–F3 | Pendiente |
| Integración y puerta | I1–I3 | Pendiente |

## Forma de trabajo

Modo rápido: primero el contrato (K0 y K1); después Back y Front en paralelo, y una sola puerta al final. Cada tarea lleva sus pruebas focales, y al terminarla, el build y los tests rápidos enteros de su repo, sin Docker y sin filtrar por módulo. Las tareas que tocan rutas, persistencia o aislamiento corren al terminar los tests de integración y de seguridad de Assistant y WhatsApp. Un arreglo corre solo lo que toca.

Todo en `main`, sin ramas, worktrees, stash, reset ni push. Cada chat commitea solo lo suyo con `git commit -- <rutas>`, en español y con conventional commits. Con la app levantada, nadie edita el `src` del front. El contrato no cambia sin anotarlo en «Cambios al contrato». Ninguna clave real pasa por un agente, y al usuario se le pregunta solo lo de producto. La tabla de estado la actualiza la Guía.

## Qué se verificó al arrancar

| Fila de la spec | Qué se encontró | Qué cambia |
| --- | --- | --- |
| [E11] equipos y bot | `WhatsAppTeam` (`TenantId`, `Inbox`, `Name`, `NormalizedName`, `MemberIds`, `NotifyAfterMinutes`, `IVersioned`, `IAuditable`); el menú del bot pasa a un equipo con `WhatsAppBotAction.Team`, y en Internos el empleado va directo a `employeesTeamId`; `WhatsAppAttentionBotWriter` escribe el bot y «Además», y `WhatsAppBotResolver` resuelve el bot del número | el menú pasa a un equipo, como asume la spec; B4 lo adelanta con varios asistentes |
| Rutas de equipos | `OrganizationWhatsAppTeamsController` (`/api/organization/whatsapp/teams`, `whatsapp.manage`) hereda `WhatsAppTeamsControllerBase`, que comparte Plataforma | `PUT teams/{id}/assistant` va solo en el de la organización, con `[FeatureGate("assistant")]` |
| `MaxMemberPreview` | `WhatsAppFeature.MaxMemberPreview = 3`, acotado en `WhatsAppAttentionContractContributor` | `teams[]` de la lista usa ese tope, en el mismo contribuidor |
| Front de equipos | `whatsapp/teams/components/TeamDialog.tsx` y `teams/pages/TeamsPage.tsx` | F2 copia la lista; F3 suma al diálogo |
| Lista de asistentes | el tablero `WA-Asistentes` dice «4 asistentes · 1 de baja» en la banda | suma `filter-counts` (K1) |
| [E23] agentes, rutas e historial | todavía no existen | K0 |

## Contrato

### K0 · Lo que dejaron las etapas anteriores

El chat del contrato confirma esta lista antes de publicar; lo que difiera va a «Cambios al contrato».
- **Base, segunda parte** (214): la pieza `Picker` (262) y el orden con `ApplySort` (264).
- **Acciones del asistente:** `IAssistantActionSource`, la batería con `TestFeatures/AssistantActions/` y `useAssistantProposal`.
- **Costuras de módulos** ([0069](../decisions/0069-modulos-y-suscripciones.md) §4): el resolvedor de topes y cómo declara un módulo los suyos; lo usan los límites de cada asistente.
- **E15:** `GET /forms/{id}/records/{recordId}` con `sentBy?`, `QuickView.tsx` y `RecordFullPage.tsx`.
- **E23:** `AssistantAgents` (`Name`, `IsPrimary`, `Profile`, `Cases`, `Limits` con `formOffersPerDay` y `reofferDeclinedAfterDays`, y su índice único filtrado), `WhatsAppAssistantCounters` y `DeclinedAtUtc` (270 y 278), `D/Assistant/AssistantAgent.cs`, `AssistantKnowledge` (una por organización), `AssistantAgentService.cs`, `AssistantAgentPolicy.cs`, `IConversationAgent`, `IConversationAgentSettings` (`SaveAsync`, `TestAsync`), `IConversationAgentReader` (`GetAsync`), `ConversationTurn`, `ConversationAgentService.cs`, `WhatsAppAssistantTurnService.cs`, `WhatsAppAssistantActions`, `WhatsAppInboxAttention`, `WhatsAppAttentionService`, `OrganizationWhatsAppAssistantController.cs` con `assistant/agent` y `…/test`, `GET …/conversations/{id}/actions/{actionId}`, `WhatsAppFlowSessions.SentByAssistant`, `StructuredRecordOrigin.ByAssistant` y `sentBy.byAssistant`, el historial de 10 mensajes de 24 horas que arma WhatsApp por conversación, y en el front `F/src/modules/whatsapp/assistant/`, `/org/whatsapp/asistente` y «Por qué respondió esto» en `MessageThread.tsx`.

### K1 · Publicar antes de programar las pantallas

El Back publica las rutas con `501`, los errores con sus textos y los ejemplos exportados de cada respuesta nueva o cambiada, incluidos 400, 404, 409 y los vacíos. El Front corre `npm run contracts`. No hay permiso nuevo: todo pide `whatsapp.manage` (28), y probar, además, atender esa bandeja, como la E23.

**Rutas** (`/api/organization/whatsapp`, `[Access(Business)]`, `[FeatureGate(WhatsAppFeature.Key)]`, `[FeatureGate("assistant")]` y `whatsapp.manage`). Los cuerpos son los de la spec; acá van los cambios. Las rutas `assistant/agent` y `assistant/agent/test` de la E23 se retiran en este mismo despliegue: las reemplazan estas, y el principal es un asistente más de la lista. Es la excepción de la 0060, porque todavía no hay despliegue (la E10 va después del bloque, 123).

| Ruta | Cuota, idempotencia y versión | Errores |
| --- | --- | --- |
| GET `assistant/agents` | `page`, `pageSize`, `search`, `status` (`all`, `active`, `inactive`) y `sort` por `name` o `status` (264) | 400 |
| GET `assistant/agents/filter-counts` | `search` → `{all, active, inactive}` | 400 |
| POST `assistant/agents` | `{name}`, `[Idempotent]`; 201 + Location | 400 `Assistant.Agent.NameDuplicate` |
| GET y PUT `assistant/agents/{id}` | `version` y `knowledgeVersion`, como la E23 | 400; 404 `Assistant.Agent.NotFound`; 409 |
| POST `assistant/agents/{id}/deactivation` y `reactivation` | `{version}` | 404; 409 `Assistant.Agent.PrimaryRequired` |
| POST `assistant/agents/{id}/test` | `assistantConversation`; `{messages[], inbox}`, de 1 a 5, como la E23 | 400; 404; 503 `Assistant.Agent.Unavailable` |
| PUT `teams/{id}/assistant` | `{assistantId?, version}` | 400 `WhatsApp.Team.AssistantUnavailable`; 404 `WhatsApp.Team.NotFound`; 409 |

`GET assistant/agents/filter-counts` no está en la spec: lo pide la banda de `WA-Asistentes`. Con `status=all` y sin `sort`, la lista trae primero al principal y después por nombre e id.

**Lo que suman las rutas que ya existen:**
- el equipo (`GET teams` y `teams/{id}`) suma `assistantId?` y `assistantName?`;
- la lista de chats y la conversación suman `assistantName?`, el que contesta ese chat; cada mensaje del asistente y `GET …/actions/{actionId}`, `assistantName`;
- `GET /forms/{id}/records/{recordId}` suma `sentBy.assistantName?`, para «La mandó» (78).

Lo común: 401, 403 sin el permiso, 404 con una feature apagada, 409 `General.ConcurrencyConflict` y los de idempotencia.

**Puertos.** `ConversationTurn` suma `AgentId?`, que elige WhatsApp. `IConversationAgentReader` suma `ListAsync(search, status, sort, page)`, con id, nombre, `IsPrimary` e `IsActive` y el orden de entrada de K1. `IConversationAgentSettings` suma `CreateAsync(name)`, que copia el perfil, los casos y los límites del principal, y `SetActiveAsync(id, active, version)`. Sus otros métodos reciben el id del asistente.

**Datos.** Los de la spec (`AssistantAgents.IsActive`, `WhatsAppTeams.AssistantId` y `WhatsAppAssistantActions.AgentId`), más:
- un índice único por organización y nombre normalizado en `AssistantAgents`, para `NameDuplicate`;
- `WhatsAppFlowSessions.SentByAgentId?`, para decir qué asistente mandó un formulario;
- cada migración anota la excepción de la 0060 que dice la spec.

**Textos de error:**

| Código | es | en |
| --- | --- | --- |
| `Assistant.Agent.NotFound` | No encontramos el asistente. | We couldn't find the assistant. |
| `Assistant.Agent.NameDuplicate` | Ya hay un asistente con ese nombre. | There's already an assistant with that name. |
| `Assistant.Agent.PrimaryRequired` | El principal no se da de baja. | The main assistant can't be deactivated. |
| `WhatsApp.Team.AssistantUnavailable` | Ese asistente está dado de baja o no existe. | That assistant is deactivated or doesn't exist. |

**Texto para el chat del Contrato**

```text
Leé AGENTS.md, este plan (docs/plans/2026-11-08-etapa-24-varios-asistentes.md) y la spec de la E24. Primero K0: confirmá en el código cada nombre que dejó la E23 y anotá en «Cambios al contrato» lo que difiera. Después K1: retirá las rutas assistant/agent de la E23 y publicá con 501 las nuevas y lo que suman las existentes, los errores con sus textos es/en y los ejemplos exportados; generá openapi.json y avisale a la Guía para que el front corra npm run contracts. Guardas de rutas, módulos y contrato en verde. Reglas: todo en main, sin ramas, worktrees, stash, reset ni push; commiteá solo lo tuyo con git commit -- <rutas>, en español y con conventional commits; con la app levantada, nadie edita el src del front; TDD donde hay lógica; al terminar, build y tests rápidos enteros, sin Docker y sin filtrar por módulo, y si tocaste rutas, persistencia, autenticación o aislamiento, los de integración y seguridad del área; el contrato no cambia sin anotarlo en «Cambios al contrato»; ninguna clave real pasa por vos; al usuario se le pregunta solo lo de producto.
```

## Back

Rigen las reglas de la E23: Result y ProblemDetails, `OperationLog.RunAsync`, `[LoggerMessage]` sin contenido, una UoW por método que escribe y RLS. El asistente no conoce a los equipos: la asignación es de WhatsApp, con el id del asistente como dato opaco, sin clave foránea. Ningún módulo referencia al otro.

1. **B1 · Varios agentes.** Suma a `D/Assistant/AssistantAgent.cs` (`IsActive`, `Deactivate`, `Reactivate`, el nombre normalizado) y a `AssistantAgentService.cs` [E23] lo nuevo del puerto, con `<fecha>_AssistantAgentsActive.cs`. Uno nuevo copia el perfil, los casos y los límites del principal, también los de la 270 y la 278, con su nombre. El conocimiento sigue siendo uno por organización y lo comparten todos (46). Copiá esos mismos [E23]. Tests: el principal no se da de baja; uno de baja no contesta ni se prueba; nombre repetido, también con otras mayúsculas; el nuevo arranca con la copia; reactivar.
2. **B2 · La asignación.** Suma a `D/WhatsApp/WhatsAppTeam.cs` (`AssistantId`, `AssignAssistant`) y a `WhatsAppTeamService.cs` la ruta del equipo, que valida el asistente con `IConversationAgentReader`; `WhatsAppTeamResponseBuilder.cs` suma su nombre; `<fecha>_WhatsAppTeamAssistants.cs` lleva `WhatsAppTeams.AssistantId`, `WhatsAppAssistantActions.AgentId` y `WhatsAppFlowSessions.SentByAgentId`. Dar de baja un asistente conserva la asignación. Copiá esos mismos. Tests: un id de otro módulo queda opaco; uno de baja o de otra organización da `AssistantUnavailable`; un equipo tiene como mucho uno, por definición.
3. **B3 · Elegir quién contesta.** `A/Modules/WhatsApp/Services/WhatsAppAgentResolver.cs` y lo que suma a `WhatsAppAssistantTurnService.cs` [E23]: el chat con equipo y asistente activo, ese; sin equipo, con un equipo sin asistente o con uno de baja, el principal. `ConversationTurn.AgentId`, `WhatsAppAssistantActions.AgentId` y `SentByAgentId` guardan el que contestó. El historial es el de la conversación, no el del asistente. Los modos y los topes siguen por bandeja y por chat, y el tope mensual es uno; el tope de ofrecimientos y «Volver a ofrecer» son los del asistente que contesta, con el contador y los rechazos del chat, y con el tope lleno deriva con `DailyOfferLimit` (278). Copiá `WhatsAppBotResolver.cs`. Tests: sin equipo; equipo sin asistente; uno de baja; cambiar de equipo cambia el asistente desde el mensaje siguiente, con el mismo historial y sin volver a cero el contador de ofrecimientos; el tope del que contesta; el porqué nombra al asistente.
4. **B4 · El menú al empezar.** Suma a `WhatsAppAttentionBotWriter.cs` y `WhatsAppInboundGuard.cs`: con dos o más asistentes activos, un chat nuevo de Clientes arranca con el menú del bot aunque la bandeja esté en modo Asistente, y después de elegir el equipo contesta su asistente (77). Con uno solo, el menú sigue solo en atención manual (52). En Internos no hay menú: el empleado va al equipo de `employeesTeamId`, como en la E11, y contesta su asistente. Copiá esos mismos. Tests: con uno y con varios; la persona elige; Internos; una opción del menú que no pasa a un equipo deja el chat sin equipo, y contesta el principal.
5. **B5 · Rutas y contratos.** Suma a `OrganizationWhatsAppAssistantController.cs` [E23] las rutas de K1 y retira las de la E23; la ruta del equipo, en `OrganizationWhatsAppTeamsController`; `assistantName` en las respuestas del chat, y en el origen de una respuesta: `StructuredRecordOrigin` suma `AssistantName?`, que es lo que recibe Forms, y lo arma `WhatsAppStructuredRecordOriginSource.cs` [E15]. Copiá esos mismos. Tests: cada ruta con 401, 403 y feature apagada = 404; la segunda página no repite ni omite; ejemplos exportados; aislamiento en `Api.IntegrationTests/Tenancy/` (el asistente y el equipo de otra organización dan 404); `ExplicitRouteInventoryTests` y `GrowingCollectionContractTests`.
6. **B6 · Acciones del asistente.** `assistants.create@1`, `assistant.agent.edit@2`, `assistants.remove@1` y `whatsappteams.assistant.edit@1`, como la spec, en `WhatsAppAssistantActionSource.cs`, que lee por `IConversationAgentReader`, registrada como `IAssistantActionSource`, con sus textos `Action.*` en `WhatsAppTexts` y su escenario en `TestFeatures/AssistantActions/`. Copiá `RoleActionSource.cs`. Tests: `AssistantActionDeclarationTests` y la batería genérica.

**Texto para el chat del Back**

```text
Leé AGENTS.md, este plan (docs/plans/2026-11-08-etapa-24-varios-asistentes.md), la spec de la E24, el plan de la E23 y las fichas modulos, concurrencia, multitenancy, guardado, paginado-y-busqueda y tests. Después de K1, hacé B1 a B6 en orden y contra ese contrato, copiando los moldes de cada tarea, con FakeChatClient. No toques el front ni la tabla de estado del plan. Reglas: todo en main, sin ramas, worktrees, stash, reset ni push; commiteá solo lo tuyo con git commit -- <rutas>, en español y con conventional commits; con la app levantada, nadie edita el src del front; TDD donde hay lógica; al terminar cada tarea, build y tests rápidos enteros del back, sin Docker y sin filtrar por módulo, y con rutas, persistencia, autenticación o aislamiento, los de integración y seguridad de Assistant y WhatsApp; el contrato no cambia sin anotarlo en «Cambios al contrato» y avisarle a la Guía; ninguna clave real pasa por vos; al usuario se le pregunta solo lo de producto.
```

## Front

Siguen los tableros v169 y las fichas de UI y de toda la app (160 a 164, 173 a 175, 180 y 261 a 264); i18n, tipos generados y axe en cada pantalla.

1. **F1 · Tipos y llamadas.** Suma a `F/src/modules/whatsapp/assistant/api/` y a la API de equipos, con zod estricto contra los ejemplos.
2. **F2 · La lista.** `whatsapp/assistant/pages/AssistantsPage.tsx` en `/org/whatsapp/asistentes` (`WA-Asistentes` y su teléfono), con `components/NewAssistantDialog.tsx` y su adaptador de `assistants.create@1`: si es el principal, en su columna (206), sus equipos y su estado; la banda con «3 asistentes» o «4 asistentes · 1 de baja» y «Nuevo asistente»; el ⋮ con Editar y Dar de baja o Reactivar, con «Dar de baja» grisado en el principal y su motivo. Cada uno abre `AssistantPage.tsx` de la E23 en `/org/whatsapp/asistentes/:assistantId`, con su «Probar». La entrada «Asistente» del panel pasa a «Asistentes», y `/org/whatsapp/asistente` lleva a la lista. Copiá `TeamsPage.tsx`. El encabezado y «Ordenar por», como la spec (264). Tests: nuevo asistente, falta el nombre, nombre repetido, dar de baja, reactivar, ordenar, un solo asistente, sin equipos, cargando y error.
3. **F3 · El equipo y el chat.** «Asistente de este equipo» en `TeamDialog.tsx` (`WA-Equipo-Asistente` y su teléfono), en la pieza (262): sin elegir contesta el principal, y uno de baja va apagado con su motivo. El nombre del que contesta en la cabecera (`ConversationView.tsx`), la lista (`ChatsWorkspace.tsx`), cada respuesta y «Por qué respondió esto» (`MessageThread.tsx`), según `WA-Chats-Casos-24` y su teléfono. El renglón «La mandó» de `RecordActivity.tsx` [E15] dice el nombre del asistente en «Quién» (267). Tests: el nombre en el chat y en el porqué; pasó a Ventas con el mismo historial; un equipo sin asistente; un solo asistente, sin menú.

**Texto para el chat del Front**

```text
Leé AGENTS.md del front, ../ArquitecturaBaseMutitenant/docs/plans/2026-11-08-etapa-24-varios-asistentes.md y la spec de la E24. Esperá K1, corré npm run contracts y hacé F1 a F3 contra los tipos generados, copiando los tableros aprobados y, los que la spec da como «Se redibujan», su redibujo aprobado, con MSW y axe. No toques el lienzo, las specs, el back ni la tabla de estado del plan. Reglas: todo en main, sin ramas, worktrees, stash, reset ni push; commiteá solo lo tuyo con git commit -- <rutas>, en español y con conventional commits; con la app levantada, no edites el src; TDD donde hay lógica; al terminar cada tarea, npx tsc -b y npm test enteros, sin filtrar por módulo, y con rutas o permisos, sus tests del área; al cerrar la parte, build, lint y contracts:check; el contrato no cambia sin anotarlo en «Cambios al contrato» y avisarle a la Guía; ninguna clave real pasa por vos; al usuario se le pregunta solo lo de producto.
```

## Integración y puerta

1. **I1 · Integrar.** El front contra la API real, con `FakeChatClient` y el canal de prueba; cada diferencia vuelve a su dueño.
2. **I2 · Puerta.** `/puerta` una sola vez sobre copias del corte:
   - build sin advertencias y `dotnet test` con Docker; front build, lint, test y `contracts:check`;
   - E2E real, con sus casos en `F/scripts/test-e2e-real-whatsapp.mjs`, con «Dos equipos con su asistente»: Lucía asigna el asistente de Ventas; Martina elige «Consultar un pedido» en el menú y contesta el asistente de ese equipo; un operador pasa el chat a Ventas y el mensaje siguiente lo contesta el de Ventas, con el historial; un chat sin equipo lo contesta el principal; dar de baja un asistente pasa sus equipos al principal;
   - `realinear-docs --detectar` en 0 y `estado-docs --detectar`;
   - galería de `F/scripts/capturas/etapa-24.json`, a 1440, 390 y 320, con `WA-Asistentes`, `WA-Equipo-Asistente` y `WA-Chats-Casos-24`, con sus teléfonos;
   - `/recorrido` con `dos-equipos-con-su-asistente`, en `F/scripts/recorrido-usuario-whatsapp.mjs`.
3. **I3 · Cierre.** `docs/features/asistente.md` y `whatsapp.md` suman su sección. La prueba real con la IA y Meta de la spec (dos equipos, el pase a Ventas y un chat sin equipo) queda pendiente para la I1 de la E10 (123).

| Puerta | Resultado |
| --- | --- |
| Back / Front / Contrato / E2E / Documentación / Galería / Recorrido | Pendiente |

**Texto para el chat de Integración**

```text
Leé AGENTS.md de los dos repos, este plan (docs/plans/2026-11-08-etapa-24-varios-asistentes.md) y la skill /puerta. Integrá el front con la API real, devolvé cada diferencia a su dueño y corré la puerta una sola vez sobre copias del corte; después de un arreglo, solo lo que toca. No toques el lienzo. Lo de Meta queda pendiente para la I1 de la E10. Dejá solo la tabla corta del resultado en este plan. Reglas: todo en main, sin ramas, worktrees, stash, reset ni push; commiteá solo lo tuyo con git commit -- <rutas>, en español y con conventional commits; con la app levantada, nadie edita el src del front; TDD en cada arreglo con lógica; build y tests rápidos enteros, sin filtrar por módulo, y los de integración y seguridad del área que toque el arreglo; el contrato no cambia sin anotarlo en «Cambios al contrato»; ninguna clave real pasa por vos; al usuario se le pregunta solo lo de producto.
```

## Relación con otras etapas

**Usa de las anteriores:**
- **E11:** `WhatsAppTeam`, `WhatsAppTeamService`, `WhatsAppTeamResponseBuilder`, `WhatsAppBot` con `employeesTeamId`, `WhatsAppBotResolver`, `WhatsAppAttentionBotWriter`, `WhatsAppInboundGuard`, `OrganizationWhatsAppTeamsController`, `TeamDialog.tsx` y `TeamsPage.tsx`.
- **E15 y E23:** lo que lista K0, con lo que la E23 suma del traspaso 15: los controles antes de encolar, «El asistente no pudo responder», Probar con cinco mensajes y los temas que se revisan (219), y del 18: los topes que derivan con su motivo (270 y 278). Cada asistente los hereda, sin nada propio.

**Deja para las siguientes:**
- **Datos:** `AssistantAgents.IsActive` y su índice por nombre, `WhatsAppTeams.AssistantId`, `WhatsAppAssistantActions.AgentId` y `WhatsAppFlowSessions.SentByAgentId`.
- **Puertos y servicios:** `ConversationTurn.AgentId`, `IConversationAgentReader.ListAsync`, `IConversationAgentSettings.CreateAsync` y `SetActiveAsync`, y `WhatsAppAgentResolver`.
- **Rutas:** `/api/organization/whatsapp/assistant/agents…` (en lugar de `assistant/agent`) y `PUT /api/organization/whatsapp/teams/{id}/assistant`; `assistantName` en el chat, el porqué y `sentBy`.
- **Front:** `/org/whatsapp/asistentes` y `/org/whatsapp/asistentes/:assistantId`, que reemplazan `/org/whatsapp/asistente`; `AssistantsPage.tsx`.

## Cambios al contrato


## Para decidir

Nada: los tableros v158 a v169 y las decisiones 28, 35, 43, 46, 52, 68, 69, 77, 78, 206, 261 a 264, 270 y 278 resuelven el producto.
