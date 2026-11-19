# E24 · Varios asistentes, uno por equipo (spec)

**Estado:** pendiente · spec de la etapa 24 del bloque (decisiones 68, 69, 77 y 78), con el [formato](plantilla.md) de las etapas. No tiene equivalente programado en ArquitecturaBase, que lo dejó para una tercera etapa del asistente (anexo F, F2). `/plan-etapa` la contrasta con el código real después de la E23.

## Qué leer

Antes de empezar, en este orden:

1. **Lo anterior:** la [spec de la E23](23-asistente.md): el agente, un turno y sus tablas; y la ficha [WhatsApp](../../features/whatsapp.md): equipos y bot.
2. **Decisiones** de la hoja de ruta: 28, 35, 43, 46, 52, 68, 69, 77, 78 y 206. ADR [0051](../../decisions/0051-precision-tecnica-de-costos-ia.md).
3. **Fichas:** [modulos](../../rules/modulos.md), [concurrencia](../../rules/concurrencia.md), [multitenancy](../../rules/multitenancy.md), [guardado](../../rules/guardado.md), [paginado-y-busqueda](../../rules/paginado-y-busqueda.md) y [tests](../../rules/tests.md).
4. **Código para copiar:** la columna «Copia de» de los [pasos](#pasos).
5. **Referencia de producto,** solo para leer: en `../ArquitecturaBase/docs/plans/2026-10-23-traspaso-multitenant/`, el anexo F, F2.

## Qué se construye

**Varios asistentes por organización.** Cada uno tiene su nombre, su perfil, sus casos y sus límites por bandeja, como el de la E23. El conocimiento es de la organización y lo comparten todos (46). El principal no se da de baja. **Uno nuevo** arranca con una copia del perfil, los casos y los límites del principal, con el nombre que se le dio, para cambiarlos.

**Quién contesta cada chat: por equipo** (68):
- cada equipo de la bandeja puede tener un asistente, y un equipo tiene como mucho uno;
- **un chat nuevo de Clientes** (77): con varios asistentes, arranca con el menú del bot, aunque la bandeja esté en modo Asistente. La persona elige el equipo, y desde ahí le contesta el asistente de ese equipo. Con uno solo, el menú sigue apareciendo solo en atención manual. En Internos no hay menú: el bot pasa al empleado al equipo elegido, como en la E11, y le contesta el asistente de ese equipo;
- un chat en modo Asistente lo contesta el asistente de su equipo. Un chat sin equipo, o de un equipo sin asistente, lo contesta el principal;
- **si el chat cambia de equipo,** por el menú o porque un operador lo pasa, desde el mensaje siguiente contesta el asistente del equipo nuevo, con el mismo historial;
- el chat muestra qué asistente contesta, y «Por qué respondió esto» lo nombra.

**La asignación** la guarda WhatsApp, con el id del asistente como dato opaco. El asistente no conoce a los equipos.

**El modo de atención** y los topes siguen siendo por bandeja y por chat. El tope mensual de IA es uno para toda la organización. **Probar** se elige por asistente.

**Dar de baja un asistente:** deja de contestar, y sus equipos pasan al principal. Se reactiva, y sus asignaciones se conservan.

## Pantallas

**Aprobados y publicados** en el lienzo v158. En ArquitecturaBase no existe: todo sale de piezas de este lienzo. Muestran solo lo que existe hasta acá (78).

**Valen las de toda la app** (160 a 164, 173 a 175 y 180): lo que carga no salta, decidir a propósito, ninguna elección se descarta en silencio, mucho texto con jerarquía, sin parecer plantilla y con la escala de ArquitecturaBase, el texto largo entero y el estado dicho una vez.

Datos de ejemplo: el principal, Delta; Delta Soporte y Delta Ventas, de esos equipos, y Delta Obras, de baja.

| Tablero | Qué muestra | Estados |
| --- | --- | --- |
| `WA-Asistentes` y `M-WA-Asistentes` | la lista, como las de WhatsApp: el asistente, si es el principal, en su columna (206), sus equipos y su estado. Tiene «Nuevo asistente» y el ⋮ con Editar y Dar de baja o Reactivar. Cada uno abre `WA-Asistente` | el menú, el del principal («Dar de baja» grisado, con el motivo), nuevo asistente, falta el nombre, creando, dar de baja, uno de baja, un solo asistente, sin equipos, cargando y error |
| `WA-Equipo-Asistente` y `M-WA-Equipo-Asistente` | «Asistente de este equipo», un desplegable en «Editar equipo» de la 11. Sin elegir, contesta el principal | elegir el asistente (uno de baja no se elige), sin elegir, un asistente de baja y un solo asistente |
| `WA-Chats-Casos-24` y `M-WA-Chats-Casos-24` | sobre `WA-Chats-Casos`: el nombre del asistente que contesta, en la cabecera, la lista, cada respuesta y «Por qué respondió esto». Un chat nuevo arranca con el menú del bot | eligió Soporte, por qué respondió, pasó a Ventas (con el mismo historial), un equipo sin asistente (contesta el principal) y un solo asistente, sin menú |

El recorrido guiado «Dos equipos con su asistente» muestra a Lucía asignando el asistente de Ventas, a Martina eligiendo «Consultar un pedido» y el pase a Ventas, hasta el principal en un equipo sin asistente.

## Contrato

| Ruta | Permiso | Pide | Devuelve |
| --- | --- | --- | --- |
| `GET /api/organization/whatsapp/assistant/agents` | `whatsapp.manage` | `page`, `pageSize`, `search`, `status` | `PagedResult<{id, name, isPrimary, isActive, teamCount, teams[]}>`, con hasta `MaxMemberPreview` equipos. La arma WhatsApp, con `IConversationAgentSettings.ListAsync` y sus equipos: el asistente no los conoce |
| `POST /api/organization/whatsapp/assistant/agents` | `whatsapp.manage` | `{name}`, con `[Idempotent]` | 201 + Location; arranca con la copia del perfil, los casos y los límites del principal |
| `GET` y `PUT /api/organization/whatsapp/assistant/agents/{id}` | `whatsapp.manage` | lo de la E23 | el agente y 200 `{version, knowledgeVersion}` |
| `POST …/agents/{id}/deactivation` y `reactivation` | `whatsapp.manage` | `{version}` | 200 `{version}` |
| `POST /api/organization/whatsapp/assistant/agents/{id}/test` | `whatsapp.manage` y atender esa bandeja | `{messages[], inbox}`, de 1 a 5, como la E23 | lo de la E23: sin `formrecords.read`, sin el porqué |
| `PUT /api/organization/whatsapp/teams/{id}/assistant` | `whatsapp.manage` | `{assistantId?, version}` | 200 `{version}` |

Las rutas `/whatsapp/assistant/agent` de la E23 pasan a estas, y nombran al principal. El equipo suma `assistantId?` y `assistantName?`. Los mensajes del chat suman `assistantName?`.

**Puertos:** `ConversationTurn` suma `AgentId?`, que elige WhatsApp. `IConversationAgentSettings` suma `ListAsync`, con id, nombre, `IsPrimary` e `IsActive`, y sus otros métodos reciben el id del asistente.

**Errores:**

| Código | Tipo | Cuándo |
| --- | --- | --- |
| `Assistant.Agent.NotFound` | NotFound | ajeno o inexistente |
| `Assistant.Agent.NameDuplicate` | Validation (`name`) | dos con el mismo nombre |
| `Assistant.Agent.PrimaryRequired` | Conflict | dar de baja el principal |
| `WhatsApp.Team.AssistantUnavailable` | Validation (`assistantId`) | dado de baja o ajeno |

## Datos

Una migración por módulo. Sin tablas nuevas de asistentes: `AssistantAgents` suma filas y `IsActive`.

| Tabla | Columna | Tipo | Regla |
| --- | --- | --- | --- |
| `AssistantAgents` | `IsActive` | bool | dar de baja = `false` |
| `WhatsAppTeams` | `AssistantId` | uuid? | opaco, sin FK: es de otro módulo |
| `WhatsAppAssistantActions` | `AgentId` | uuid? | el que contestó |

Un equipo tiene como mucho un asistente por definición: la columna es del equipo.

## Pasos

Abreviaturas: `D/`, `A/`, `I/` y `P/` son las capas del back; `F/`, el front, y `[E23]`, un archivo que deja esa etapa.

**Back:**

| # | Qué hace | Archivos | Copia de | Test |
| --- | --- | --- | --- | --- |
| B1 | Varios agentes | suma a `D/Assistant/AssistantAgent.cs` y `AssistantAgentService.cs` [E23]; la migración | esos mismos | el principal no se da de baja; un asistente de baja; nombre repetido; el nuevo arranca con la copia del principal, con su nombre |
| B2 | La asignación | suma a `D/WhatsApp/WhatsAppTeam.cs` y `WhatsAppTeamService.cs`; la migración | esos mismos | otro módulo opaco; un asistente de baja |
| B3 | Elegir quién contesta | `A/Modules/WhatsApp/Services/WhatsAppAgentResolver.cs`; suma a `WhatsAppAssistantTurnService.cs` [E23] | `WhatsAppBotResolver.cs` | sin equipo; equipo sin asistente; cambiar de equipo con el mismo historial |
| B4 | El menú al empezar | suma a `WhatsAppAttentionBotWriter.cs` y `WhatsAppInboundGuard.cs` | esos mismos | con uno y con varios; la persona elige |
| B5 | Rutas y contratos | suma a `OrganizationWhatsAppAssistantController.cs` y a los equipos | esos mismos | cada ruta con 401, 403 y feature apagada = 404; segunda página; ejemplos exportados; aislamiento |

**Front:**

| # | Qué hace | Archivos | Copia de | Test |
| --- | --- | --- | --- | --- |
| F1 | Tipos y llamadas | suma a `F/src/modules/whatsapp/assistant/` | esos mismos | zod estricto |
| F2 | La lista | `AssistantsPage.tsx`, y la pantalla de la E23 por id | la lista de equipos | «Nuevo asistente»; dar de baja |
| F3 | El equipo y el chat | suma al equipo y a `MessageThread.tsx` | esos mismos | el nombre en el chat y en el porqué |

**Al cerrar:** `docs/features/asistente.md` y `whatsapp.md` suman su sección.

## Qué verificar al arrancar

| Marca | Qué hay que confirmar | Qué asume esta spec |
| --- | --- | --- |
| [E23] | `AssistantAgents` y las rutas del agente. | Sumar filas no rehace nada. |
| [E23] | El historial por conversación. | No depende de qué asistente contestó. |
| [E11] | Equipos y bot, en `WhatsAppTeam` y `WhatsAppAttentionBotWriter`. | El menú pasa a un equipo. |

## Decisiones tomadas (no se vuelven a preguntar)

- **Por equipo** (68): usa los equipos que ya existen, es predecible y en cada chat se ve quién contesta.
- **El menú elige el equipo** (77): decide la persona, no la IA.
- **Etapa propia,** después de la E23 (69).
- **Sin permiso nuevo:** se configura con «Administrar WhatsApp» (28).

## Fuera de esta etapa

- Un asistente principal que lee el mensaje y elige a quién pasarlo.
- Asistentes que se pasan el chat entre ellos.
- Un conocimiento propio de cada asistente.

## Cómo se cierra

La puerta de la etapa, con la lista de control de `/puerta`, más una **prueba real con la IA y Meta**:
1. dos equipos con su asistente: una persona elige «Soporte» en el menú y contesta el asistente de Soporte;
2. un operador pasa el chat a Ventas, y el mensaje siguiente lo contesta el asistente de Ventas, con el historial;
3. un chat sin equipo lo contesta el principal.

## Requisitos previos

La E23 cerrada.

## En el multitenant

- **Datos de la organización:** los asistentes y la asignación son `ITenantOwned`, con RLS.
- **Workers:** los de la E23.
- **Features:** `forms` y `assistant`.
- **WhatsApp:** los equipos y el número de la organización.
- **Tope de IA:** uno para toda la organización.
- **Cuotas:** las rutas cuentan en las cuotas generales.
