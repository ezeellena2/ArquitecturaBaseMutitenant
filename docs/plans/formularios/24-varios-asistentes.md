# E24 · Varios asistentes, uno por equipo (spec)

**Estado:** pendiente · spec de la etapa 24 del bloque (decisiones 68, 69, 77, 78, 261 a 264, 270 y 278), con el [formato](plantilla.md) de las etapas. No tiene equivalente programado en ArquitecturaBase, que lo dejó para una tercera etapa del asistente (anexo F, F2). `/plan-etapa` la contrasta con el código real después de la E23.

## Qué leer

Antes de empezar, en este orden:

1. **Lo anterior:** la [spec de la E23](23-asistente.md): el agente, un turno y sus tablas; y la ficha [WhatsApp](../../features/whatsapp.md): equipos y bot.
2. **Decisiones** de la hoja de ruta: 28, 35, 43, 46, 52, 68, 69, 77, 78, 206, 261 a 264, 268 a 271 y 278. ADR [0051](../../decisions/0051-precision-tecnica-de-costos-ia.md) y [0068](../../decisions/0068-acciones-del-asistente.md).
3. **Fichas y guías:** [modulos](../../rules/modulos.md), [concurrencia](../../rules/concurrencia.md), [multitenancy](../../rules/multitenancy.md), [guardado](../../rules/guardado.md), [paginado-y-busqueda](../../rules/paginado-y-busqueda.md), [tests](../../rules/tests.md) y la [receta de acciones](../../guides/agregar-una-accion-al-asistente.md).
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

**El tope de ofrecimientos** (278) es el de los límites del asistente que contesta, en su bandeja: de 1 a 50 y 2 de entrada, dentro del tope de la organización que da el resolvedor ([0069](../../decisions/0069-modulos-y-suscripciones.md)), como en la E23, igual que «Volver a ofrecer un formulario rechazado» (270). El contador y los rechazos siguen siendo del chat: cambiar de equipo no los vuelve a cero. Con el tope lleno, el chat deriva con «Llegó al tope de ofrecimientos del día», y el porqué nombra al asistente.

**Dar de baja un asistente:** deja de contestar, y sus equipos pasan al principal. Se reactiva, y sus asignaciones se conservan.

## Pantallas

**Aprobados y publicados** en el lienzo v158. En ArquitecturaBase no existe: todo sale de piezas de este lienzo. Muestran solo lo que existe hasta acá (78).

**Valen las de toda la app** (160 a 164, 173 a 175, 180, 261 a 264 y 269): lo que carga no salta, decidir a propósito, ninguna elección se descarta en silencio, mucho texto con jerarquía, sin parecer plantilla y con la escala de ArquitecturaBase, el texto largo entero y el estado dicho una vez.

Datos de ejemplo: el principal, Delta; Delta Soporte y Delta Ventas, de esos equipos, y Delta Obras, de baja.

| Tablero | Qué muestra | Estados |
| --- | --- | --- |
| `WA-Asistentes` y `M-WA-Asistentes` | la lista, como las de WhatsApp: el asistente, si es el principal, en su columna (206), sus equipos y su estado. Tiene «Nuevo asistente» y el ⋮ con Editar y Dar de baja o Reactivar. Cada uno abre `WA-Asistente` | el menú, el del principal («Dar de baja» grisado, con el motivo), nuevo asistente, falta el nombre, creando, dar de baja, uno de baja, un solo asistente, sin equipos, cargando y error |
| `WA-Equipo-Asistente` y `M-WA-Equipo-Asistente` | «Asistente de este equipo», un desplegable en «Editar equipo» de la 11. Sin elegir, contesta el principal | elegir el asistente (uno de baja no se elige), sin elegir, un asistente de baja y un solo asistente |
| `WA-Chats-Casos-24` y `M-WA-Chats-Casos-24` | sobre `WA-Chats-Casos`: el nombre del asistente que contesta, en la cabecera, la lista, cada respuesta y «Por qué respondió esto». Un chat nuevo arranca con el menú del bot | eligió Soporte, por qué respondió, pasó a Ventas (con el mismo historial), un equipo sin asistente (contesta el principal) y un solo asistente, sin menú |

**Se redibujan con el traspaso 18, tanda 4** (262 a 264):
- `WA-Asistentes` y `M-WA-Asistentes`: el encabezado y «Ordenar por» ordenan «Asistente» y «Estado», y «Equipos» no, porque son varios; sin orden elegido va el principal primero y después por nombre, sin flecha (264). En el teléfono, sin datos juntos con «·», como «Delta · principal» (263);
- `WA-Equipo-Asistente` y `M-WA-Equipo-Asistente`: «Asistente de este equipo», en la pieza: sin elegir, el campo dice en gris que contesta el principal, y uno de baja va apagado con su motivo (262).

El recorrido guiado «Dos equipos con su asistente» muestra a Lucía asignando el asistente de Ventas, a Martina eligiendo «Consultar un pedido» y el pase a Ventas, hasta el principal en un equipo sin asistente.

## Contrato

| Ruta | Permiso | Pide | Devuelve |
| --- | --- | --- | --- |
| `GET /api/organization/whatsapp/assistant/agents` | `whatsapp.manage` | `page`, `pageSize`, `search`, `status` y `sort` por `name` o `status` (264) | `PagedResult<{id, name, isPrimary, isActive, teamCount, teams[]}>`, con hasta `MaxMemberPreview` equipos. La arma WhatsApp, con `IConversationAgentSettings.ListAsync` y sus equipos: el asistente no los conoce |
| `POST /api/organization/whatsapp/assistant/agents` | `whatsapp.manage` | `{name}`, con `[Idempotent]` | 201 + Location; arranca con la copia del perfil, los casos y los límites del principal, también los de la 270 y la 278 |
| `GET` y `PUT /api/organization/whatsapp/assistant/agents/{id}` | `whatsapp.manage` | lo de la E23 | el agente y 200 `{version, knowledgeVersion}` |
| `POST …/agents/{id}/deactivation` y `reactivation` | `whatsapp.manage` | `{version}` | 200 `{version}` |
| `POST /api/organization/whatsapp/assistant/agents/{id}/test` | `whatsapp.manage` y atender esa bandeja | `{messages[], inbox}`, de 1 a 5, como la E23 | lo de la E23: sin `formrecords.read`, sin el porqué |
| `PUT /api/organization/whatsapp/teams/{id}/assistant` | `whatsapp.manage` | `{assistantId?, version}` | 200 `{version}` |

Las rutas `/whatsapp/assistant/agent` de la E23 pasan a estas, y nombran al principal. Se retiran en el mismo despliegue: es la excepción de la [0060](../../decisions/0060-dos-versiones-conviven.md), porque todavía no hay despliegue (la E10 va después del bloque, 123). El equipo suma `assistantId?` y `assistantName?`. Los mensajes del chat suman `assistantName?`.

**Puertos:** `ConversationTurn` suma `AgentId?`, que elige WhatsApp. `IConversationAgentReader` suma `ListAsync(search, status, sort, page)`, con id, nombre, `IsPrimary` e `IsActive`. Los métodos de los dos puertos reciben el id del asistente.

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

Un equipo tiene como mucho un asistente por definición: la columna es del equipo. Cada migración agrega y escribe en el mismo despliegue, con el índice por nombre: es la excepción de la 0060, anotada en cada una, porque todavía no hay despliegue (123).

## Pasos

Abreviaturas: `D/`, `A/`, `I/` y `P/` son las capas del back; `F/`, el front, y `[E23]`, un archivo que deja esa etapa.

**Back:**

| # | Qué hace | Archivos | Copia de | Test |
| --- | --- | --- | --- | --- |
| B1 | Varios agentes | suma a `D/Assistant/AssistantAgent.cs` y `AssistantAgentService.cs` [E23]; la migración | esos mismos | el principal no se da de baja; un asistente de baja; nombre repetido; el nuevo arranca con la copia del principal, con su nombre |
| B2 | La asignación | suma a `D/WhatsApp/WhatsAppTeam.cs` y `WhatsAppTeamService.cs`; la migración | esos mismos | otro módulo opaco; un asistente de baja |
| B3 | Elegir quién contesta | `A/Modules/WhatsApp/Services/WhatsAppAgentResolver.cs`; suma a `WhatsAppAssistantTurnService.cs` [E23] | `WhatsAppBotResolver.cs` | sin equipo; equipo sin asistente; cambiar de equipo con el mismo historial; el tope de ofrecimientos del que contesta, con el contador del chat, y con el tope lleno deriva (278) |
| B4 | El menú al empezar | suma a `WhatsAppAttentionBotWriter.cs` y `WhatsAppInboundGuard.cs` | esos mismos | con uno y con varios; la persona elige |
| B5 | Rutas y contratos | suma a `OrganizationWhatsAppAssistantController.cs` y a los equipos, con sus contratos en `P/Modules/WhatsApp/Contracts/WhatsAppAttentionHttpContracts.cs` | esos mismos | cada ruta con 401, 403 y feature apagada = 404; segunda página; ejemplos exportados; aislamiento |

**Front:**

| # | Qué hace | Archivos | Copia de | Test |
| --- | --- | --- | --- | --- |
| F1 | Tipos y llamadas | suma a `F/src/modules/whatsapp/assistant/` | esos mismos | zod estricto |
| F2 | La lista | `F/src/modules/whatsapp/assistant/pages/AssistantsPage.tsx` (`/org/whatsapp/asistentes`) y `components/NewAssistantDialog.tsx`, y `AssistantPage.tsx` de la E23 en `/org/whatsapp/asistentes/:assistantId` | `whatsapp/teams/pages/TeamsPage.tsx` | «Nuevo asistente»; dar de baja; ordenar (264) |
| F3 | El equipo y el chat | suma a `TeamDialog.tsx`, a `MessageThread.tsx` y a `RecordActivity.tsx` [E15], con el asistente en «Quién» de «La mandó» | esos mismos | el nombre en el chat, en el porqué y en «La mandó»; el asistente del equipo, en la pieza (262) |

**Al cerrar:** `docs/features/asistente.md` y `whatsapp.md` suman su sección.

## Acciones del asistente

Con la [receta](../../guides/agregar-una-accion-al-asistente.md) (268 y [0068](../../decisions/0068-acciones-del-asistente.md)), registrada y sin ofrecer hasta la calificación: `assistants.create@1` · Nuevo asistente, en su diálogo sobre el Inicio (`Dialog`), con el nombre (`Text`), `whatsapp.manage` y `POST api/organization/whatsapp/assistant/agents`, sin efecto fuera del sistema. La declara `A/Modules/WhatsApp/Services/WhatsAppAssistantActionSource.cs`, con su pantalla en `AssistantActionScreen` y su traducción a `AssistantRouteKey`, y valida con el validador del pedido; el nombre repetido lo decide la ruta.

Las declara la misma fuente, que lee los asistentes por `IConversationAgentReader`, el lector del núcleo que implementa el Assistant (0068 §5):

| Acción | Dónde se confirma | Ruta que guarda |
| --- | --- | --- |
| `assistant.agent.edit@2` · El asistente | lo de la `@1` de la E23, con el asistente elegido (`Record`, el `Target`) | `PUT …/assistant/agents/{id}` |
| `assistants.remove@1` · Dar de baja | la confirmación de dar de baja de la lista (`RemoveConfirmation`); el principal no se ofrece | `POST …/agents/{id}/deactivation` |
| `whatsappteams.assistant.edit@1` · El asistente de un equipo | «Editar equipo» (`Dialog`), con el equipo (`Record`) y el asistente (`Choice` de los activos) | `PUT …/teams/{id}/assistant` |

Todas piden `whatsapp.manage`. La `@1` de la E23 se sigue leyendo un despliegue más (0060). **Reactivar no se declara:** es de un clic, sin confirmación (§9).

## Qué verificar al arrancar

| Marca | Qué hay que confirmar | Qué asume esta spec |
| --- | --- | --- |
| [E23] | `AssistantAgents` y las rutas del agente. | Sumar filas no rehace nada. |
| [E23] | El historial por conversación. | No depende de qué asistente contestó. |
| [E11] | Equipos y bot, en `WhatsAppTeam` y `WhatsAppAttentionBotWriter`. | El menú pasa a un equipo. |
| Costuras | El resolvedor de topes ([0069](../../decisions/0069-modulos-y-suscripciones.md) §4): cada módulo declara sus topes y el resolvedor da el de cada organización. | Los límites de cada asistente con máximo de la plataforma (`maxReplyLength`, `repliesPerDay` y `formOffersPerDay`, E23) se declaran en su módulo y se leen por el resolvedor, nunca directo de la configuración de Plataforma; el valor de la organización se valida contra el tope que da. |

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
