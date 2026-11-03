# E23 · El asistente, con Casos (spec)

**Estado:** pendiente · spec de la etapa 23 del bloque (decisiones 34, 39, 51, 52, 63, 65 a 67 y 78), con el [formato](plantilla.md) de las etapas. Junta la etapa 5 de ArquitecturaBase, cerrada y en piloto, con sus Casos del asistente, decididos y sin programar. `/plan-etapa` la contrasta con el código real después de la E22, cuando esté la ADR de respuestas redactadas.

## Qué leer

Antes de empezar, en este orden:

1. **Lo anterior:** la [spec de la E14](14-formularios.md): T5, T6 y los puertos; la [E16](16-datos-de-las-personas.md): los filtros; la [E17](17-ciclo-de-vida.md): la disponibilidad; la [E18](18-fotos-y-documentos.md): bajar medios; la [E21](21-reglas-y-aprobacion.md): reglas y cola, y la ficha [Asistente](../../features/asistente.md).
2. **Decisiones** de la hoja de ruta: 19, 27, 28, 35, 39, 46, 50 a 52, 63, 65 a 67 y 78. ADR [0048](../../decisions/0048-asistente.md), [0051](../../decisions/0051-precision-tecnica-de-costos-ia.md) y [0038](../../decisions/0038-whatsapp-de-atencion.md).
3. **Fichas y guías:** [modulos](../../rules/modulos.md), [guardado](../../rules/guardado.md), [concurrencia](../../rules/concurrencia.md), [multitenancy](../../rules/multitenancy.md), [cuotas](../../rules/cuotas.md), [logs](../../rules/logs.md), [textos-y-traducciones](../../rules/textos-y-traducciones.md), [tests](../../rules/tests.md) y la [guía de herramientas](../../guides/agregar-una-herramienta-al-asistente.md).
4. **Código para copiar:** la columna «Copia de» de los [pasos](#pasos).
5. **Referencia de producto,** solo para leer: en [`../2026-10-17-formularios-traspaso/`](../2026-10-17-formularios-traspaso/README.md), el anexo C, «Etapa 5», y el anexo D (H1 a H7 y s1 a s4); en `../ArquitecturaBase/docs/plans/2026-10-23-traspaso-multitenant/`, el anexo C §7 a §9 y el anexo F, F1, con su ADR 0021.
6. **Los dibujos** son de Diseño.

## Qué se construye

**Quién atiende** (51 y 52), con «Administrar WhatsApp» (28):
- el asistente atiende empleados en Internos y clientes en Clientes;
- **por bandeja,** quién atiende los chats nuevos, y cuándo vuelve un chat al modo configurado después de que lo tomó una persona: pasado un tiempo sin mensajes, al marcarlo «Resuelto» o nunca. Se mira al llegar un mensaje, sin worker;
- **cada chat** se cambia a mano y se puede fijar. Pasarlo a Asistente pide `whatsapp.manage`. **Tomar la atención manual** pide solo el permiso de atender, siempre se puede, y deja sin efecto una propuesta que llegue tarde;
- el bot del menú aparece solo en los chats de Clientes en atención manual.

**Los Casos** (39), en una pantalla con un solo Guardar, con versión y 409. Se guardan como **el agente principal**, una fila, así la etapa siguiente suma filas:
- **Perfil:** nombre, presentación, tono, qué hace y qué no, qué hacer cuando no sabe y el mensaje fijo al derivar;
- **Conocimiento** (46): la información del negocio, por secciones, una por organización. Una sección vacía no se usa;
- **Casos:** cuándo (un texto), para quién (50), tipo de entrada, qué hacer, orden y si está activo. Los formularios van en un bloque fijo, y «Cualquier otra cosa» va al final y no se borra. Sin perfil, empieza en «No responder»;
- **Límites, uno por bandeja** (65): temas que no responde, lo que no puede prometer, largo máximo, tope de respuestas por conversación y día, tope de ofrecimientos (66) y acciones permitidas. La plataforma fija un máximo para cada uno.

**Seis acciones,** un menú cerrado en el código: mandar un formulario, responder con un texto exacto, responder con el conocimiento, preguntar con hasta 3 botones, derivar y no responder. Un caso nuevo es configuración; una acción nueva es código.

**Probar** muestra qué caso elige, por qué y qué respondería, con lo guardado y sin mandar nada. Cuenta en el tope de IA.

**Respuestas redactadas,** con su ADR, antes de planificar:
- **fuentes** (67): lo de la organización, solo lo cargado. Lo general, lo que sabe el modelo. Además, el historial de esa conversación y la hora. Lo leído es un dato, nunca una instrucción;
- corta, en el idioma de la conversación, sin enlaces, teléfonos, correos, precios ni compromisos que no estén cargados;
- **si es de la organización y no está cargado:** pregunta para entender, dice que lo consulta y deriva. Nunca contesta algo genérico;
- llega como propuesta tipada, y WhatsApp la valida antes de encolarla. Si no pasa, no sale y deriva;
- «Por qué respondió esto» guarda el texto, el caso y lo que usó. Los logs no llevan contenido.

**Un turno:**
1. llega un texto o un audio transcripto, en un chat en modo Asistente y dentro de las 24 horas;
2. espera 4 s sin mensajes nuevos, con un tope de 15 s, y procesa todo junto;
3. arma el pedido en orden estable: perfil, casos, formularios candidatos, **hasta 10 mensajes de las últimas 24 horas de esa conversación** y lo que ya hizo;
4. el modelo elige el caso y propone hasta 3 acciones, y el sistema valida cada una;
5. encola en la salida y guarda el porqué;
6. **derivar** manda el mensaje fijo, pasa a atención manual y marca «Necesita una persona», en la cabecera, la lista y su filtro. La marca se quita cuando responde una persona;
7. en atención manual, la respuesta redactada se le ofrece al operador como borrador.

**Ofrecer formularios:** hasta el tope por conversación y día, en la zona de la organización; solo los publicados, activos y con «Cuándo lo ofrece», para ese destinatario (50), con el filtro de disponibilidad y sin las versiones solo para la app. Lo manda el asistente: la sesión lo guarda así, y no toma el chat. **El prellenado** propone texto, números, fechas, sí/no y una opción ofrecida, como valores iniciales de un perfil nuevo del Flow **[Meta]**. Uno publicado antes se manda sin prellenar. El asistente no ve los valores de las respuestas.

**Sugerencias entre formularios:** `formsuggestions.manage`, hasta 20 por formulario, hacia formularios para el mismo destinatario. «La IA decide» o «Preguntar siempre», por condición y sin IA, con mapeos entre preguntas del mismo tipo. Una hacia un dado de baja deja de ofrecerse y vuelve si se reactiva. La respuesta conserva su origen y la cadena.

**Sin la feature `assistant`,** «Cuándo lo ofrece» y «Armar desde un texto» no aparecen, y una sugerencia queda en «Preguntar siempre», que no usa IA.

**Copiar un formulario** suma «Cuándo lo ofrece» y las sugerencias vigentes, como su primera revisión.

**Audios:** se bajan como los archivos del chat y se transcriben por un puerto del núcleo (27). La transcripción la ve también el operador.

**Armar una regla desde un texto** (63): la IA propone una regla normal, y la persona la revisa y la guarda. Después corre sin IA. Con el tope agotado, queda grisado, con el motivo en su tooltip.

**Preguntas sin respuesta** (67) y **Formularios que faltan:** agrupadas por tema, sin nombres ni mensajes, con cantidades. Se completa el conocimiento, se crea el formulario o se descarta.

**Catálogo propuesto:** se pide desde el asistente de la web, solo con filtros. El catálogo suma el estado «Por aprobar», hasta que una persona con `formcatalogs.manage` lo aprueba o lo descarta. Aprobado, queda activo.

**Desde el primer día:**
- **la espera:** el motor avisa con una señal local al confirmar, y la entrada sondea cada 2 s solo si hay algo pendiente. Una sola cola durable. Allá bajó de 35 s a 6,6 s;
- **el aislamiento por conversación:** `ChannelConversationId` en el contexto, la recuperación y la cancelación. Se prueba con tres personas a la vez;
- **la medición** de texto a envío y de audio a envío, desde la primera corrida.

**Costos:** el modelo, la transcripción y Probar entran en el tope mensual de IA (ADR 0051). Con el tope agotado, el asistente no está disponible y el chat sigue en atención manual.

## Pantallas

Diseño los dibujó con el [texto de la etapa](23-texto-diseno.md). **Dibujados** en el lienzo v157 (sin publicar: espera el OK del usuario). La pantalla «Asistente» no tiene modelo, ni en el lienzo ni en ArquitecturaBase: el usuario eligió armarla como el editor de un formulario. Lo demás sigue al front real de ArquitecturaBase, con nuestras piezas: el chat con su modo y «Por qué lo mandó», la sugerencia, los audios, «Qué sugerir después», «Cómo empezó» e «Información del negocio».

Datos de ejemplo: Grupo Delta vende y repara heladeras. Martina López escribe por Clientes y Tomás Acosta, por Internos.

| Tablero | Qué muestra | Estados |
| --- | --- | --- |
| `WA-Asistente` y `M-WA-Asistente` | «Asistente» en el panel de WhatsApp: Perfil, Conocimiento, Casos, Límites y Atención, un solo Guardar en la banda y «Probar» siempre al costado. Se entra por Casos | perfil, sin perfil (abre en Perfil, con «Cualquier otra cosa» en «No responder»), faltan los datos, con cambios, guardando, guardado, otra persona guardó, descartar y cargando |
| `WA-Asistente-Casos` | los casos propios con ↑ ↓ ✎ 🗑 (127), que se abren en el lugar: cuándo, para quién, el tipo de entrada, qué hacer y si está activo. Debajo, el bloque fijo de formularios y «Cualquier otra cosa» | editar, el menú de las seis acciones, preguntar con botones, texto exacto, mandar un formulario, derivar, caso nuevo, con errores, «Cualquier otra cosa» y sin perfil |
| `WA-Asistente-Probar` | lo que haría, en orden y hasta tres acciones: cada una con su caso, su porqué, lo que usó y cómo la vería la persona. Al final, «No se mandó nada» | cuatro pruebas (envíos a Córdoba, la heladera, hablar con alguien, hola y la hora), probando, con cambios sin guardar y tope de IA agotado (grisado, con el motivo en su tooltip) |
| `WA-Asistente-Limites` | Límites, por bandeja (65), con el tope de ofrecimientos (66) y el máximo de la plataforma debajo de cada número; Conocimiento, con las secciones de «Información del negocio» (46) | Internos, sobre el máximo, una sección vacía y editar una pregunta frecuente |
| `WA-Modo-Atencion` y `M-WA-Modo-Atencion` | la pestaña Atención, por bandeja: quién atiende los chats nuevos y cuándo vuelve un chat (52). En el teléfono, el ⋮ del chat: pasar de modo, fijarlo y «Marcar como resuelta» | Internos, una persona atiende, al marcarlo «Resuelto», guardando y guardado; en el teléfono, atención manual, fijada y resuelta |
| `WA-Chats-Casos` y `M-WA-Chats-Casos` | el chat de Clientes con los casos: «Por qué respondió esto» debajo de cada acción y derivar con «Necesita una persona» en la cabecera, la lista y su filtro. Muestra también el texto exacto, los botones, el tope del día y el borrador en atención manual | por qué respondió, sin la información (deriva), un formulario, botones, texto exacto, hablar con alguien, una ráfaga con tres acciones, el tope, el borrador, cambiar el modo, fijada, resuelta y el filtro |
| `WA-Preguntas-Sin-Respuesta` y `M-WA-Preguntas-Sin-Respuesta` | en el panel de WhatsApp, por tema, con preguntas, personas y la última. El ⋮ tiene «Completar el conocimiento» y «Descartar». Sin nombres ni mensajes | acciones, descartar, el filtro de estado, descartados, vacío, cargando y error |
| `WA-Chats-Asistente`, `WA-Chats-Sugerencia` y `WA-Chats-Audio`, con `M-WA-Conversacion-Asistente` | revisados: la barra de la bandeja y las fechas de WA-Chats, y el tope agotado. Sale «Sin respuesta del asistente», que pasa a `WA-Chats-Casos`. El audio, como allá: «Escuchar audio» y la transcripción debajo | los de antes, más escuchar el audio y «Ya no está» (D4) |
| `FRM-Editor-Asistente` | redibujado: «Cuándo lo ofrece el asistente», con su (i), en «El formulario» de la pestaña Diseño | sin «Cuándo lo ofrece» y sin la función del asistente |
| `FRM-Editor-Sugerencias` | redibujado: la pestaña «Sugerencias», como las reglas (79), con «Qué sugerir después», su historial al costado y ✎ 🗑 en cada una | editar, preguntar siempre, un dato de otro tipo, hacia un dado de baja, guardando, guardadas, sin sugerencias y sin el asistente |
| `FRM-Editor-Reglas-23` y `M-FRM-Reglas-23` | «Armar desde un texto» junto a «Agregar regla» (63): la IA propone una regla normal, que queda abierta y sin guardar | armar, armando, la regla propuesta, no se pudo, tope agotado (grisado, con el motivo) y sin el asistente |
| `FRM-Catalogos-23` y `FRM-Catalogo-Propuesta` | el estado «Por aprobar» en la lista y, redibujado, el catálogo propuesto, para leer, con quién lo pidió, «Descartar» y «Aprobar» | un filtro que no se pudo armar, descartar, aprobando, aprobado (queda activo) y sin permiso |
| `FRM-Dar-De-Baja-23` y `FRM-Editor-Baja-23` | «Qué deja de pasar» suma que el asistente no lo ofrece y las sugerencias hacia el formulario, que vuelven si se reactiva | — |
| `FRM-Faltan` y `M-FRM-Faltan` | revisados: la banda cuenta temas y pedidos, y las fechas van completas | los de antes |
| `FRM-Respuesta-Cadena` | revisado, sobre la vista rápida de la 21: «Cómo empezó», con «Ver los datos propuestos», y la cadena de respuestas | los datos que propuso y la cadena |
| `Campana-23` y `M-Campana-23` | la campana suma las preguntas sin respuesta, para quien administra WhatsApp | — |
| `Plat-Asistente-Config-23` y `M-Plat-Asistente-Config-23` | «Máximos del asistente de WhatsApp»: el largo de la respuesta, y las respuestas y los formularios que ofrece por conversación y día | — |

El recorrido guiado «Los casos del asistente» muestra a Lucía armando y probando los casos, y a Martina escribiendo, hasta la ráfaga con tres acciones.

**Mandan sobre esta spec en:**
- **Una consulta ya no queda sin respuesta:** `WA-Chats-Casos` reemplaza «Sin respuesta del asistente» de `WA-Chats-Asistente`.
- **Los nombres de la API:** «La IA decide» es `Assistant`, y «Preguntar siempre», `Always`.
- **Los audios:** «No pudimos pasar este audio a texto» cubre también el tope agotado, y un audio sigue la retención del chat (D4).
- **Probar** prueba lo guardado: con cambios sin guardar queda grisado, con el motivo en su tooltip.
- **Atención** es la quinta pestaña de la pantalla. Tiene su ruta y su versión (`/whatsapp/attention`), pero se guarda con el mismo botón.
- **El ⋮ del chat:** ahí van cambiar el modo, fijarlo y «Marcar como resuelta».
- **El catálogo propuesto** no se edita: se aprueba o se descarta.

**Decisión del usuario** (03/11): la pantalla «Asistente» se arma como el editor de un formulario. El contenido va a la izquierda y «Probar», siempre a la derecha, y los casos se editan en el lugar, con ↑ ↓ ✎ 🗑.

## Contrato

| Ruta | Permiso | Pide | Devuelve |
| --- | --- | --- | --- |
| `GET` y `PUT /api/organization/whatsapp/assistant/agent` | `whatsapp.manage` | `{profile, cases[], limits{customers, internal}, version, knowledge?, knowledgeVersion?}`; el conocimiento viaja solo si cambió, con su propia versión | el agente y 200 `{version, knowledgeVersion}` |
| `POST /api/organization/whatsapp/assistant/agent/test` | `whatsapp.manage` | `{text, inbox}` | `{actions[{caseId, why, used[], preview}]}`; no manda nada |
| `GET` y `PUT /api/organization/whatsapp/attention` | `whatsapp.manage` | por bandeja, `{mode, returnRule, idleMinutes?, version}` | la configuración |
| `POST …/conversations/{id}/assistant-mode` | atender esa bandeja y `whatsapp.manage` | `{pinned, version}` | 200 `{version}` |
| `POST …/conversations/{id}/pin` y `resolve` | atender esa bandeja | `{version}` | 200 `{version}` |
| `GET …/conversations/{id}/actions/{actionId}` | atender esa bandeja | — | el porqué |
| `POST …/conversations/{id}/suggestions/{suggestionId}/send` y `dismiss` | atender esa bandeja | `send`, con `[Idempotent]` | 202 y 204 |
| `GET /api/organization/whatsapp/unanswered-topics`, `POST …/{id}/dismiss` | `whatsapp.manage` | `page`, `pageSize`, `status` | `PagedResult<{id, topic, questions, people, lastAtUtc}>` |
| `GET /api/organization/forms/missing`, `POST …/{id}/dismiss` | `forms.manage` | lo mismo | lo mismo |
| `GET` y `PUT /api/organization/forms/{id}/suggestions` | `formsuggestions.manage` | `{revision, suggestions[]}` | `{revision}` |
| `POST /api/organization/forms/{id}/rules/proposals` | `formrules.manage` | `{text}` | `{rule}`; no guarda |
| `POST /api/organization/forms/catalogs/{id}/approval` | `formcatalogs.manage` | `{version}` | 200 `{version}` |
| `DELETE /api/organization/forms/catalogs/{id}` | `formcatalogs.manage` | `version`, solo si está `proposed` | 204 |

**Las del agente** llevan `[FeatureGate("assistant")]` y van en el módulo WhatsApp, en `OrganizationWhatsAppAssistantController.cs`: piden un permiso de WhatsApp (28), que el módulo Assistant no puede nombrar, porque retirar WhatsApp lo saca del catálogo. **Las del chat** van en una clase nueva, `OrganizationWhatsAppConversationsControllerBase`, entre `WhatsAppConversationsControllerBase` y los dos controllers de la organización, porque Plataforma no tiene modo de atención. **Rutas que cambian:** `GET` y `PUT /forms/{id}/draft` suman `offerWhen?`; los catálogos, `status` `proposed`; `GET` y `PUT /api/platform/assistant/settings` suman `{maxReplyLength, maxRepliesPerDay, maxFormOffersPerDay}`, los máximos de la plataforma; los mensajes del chat, `assistantActionId?`, `transcript?` y el estado de la conversación: `attentionMode`, `pinned`, `needsPerson?`; la lista de chats, el filtro `needsPerson`.

**Puertos neutrales:**

```csharp
// WhatsApp → Asistente (implementa el módulo Assistant)
public interface IConversationAgent
{
    Task<Result<ConversationProposal>> ProposeAsync(ConversationTurn turn, CancellationToken ct);
    Task<Result<string>> SuggestReplyAsync(ConversationTurn turn, CancellationToken ct);
}
// WhatsApp → Asistente (implementa el módulo Assistant): la configuración, para las rutas de WhatsApp
public interface IConversationAgentSettings
{
    Task<Result<ConversationAgentConfig>> GetAsync(CancellationToken ct);
    Task<Result<ConversationAgentVersions>> SaveAsync(ConversationAgentConfig config, CancellationToken ct);
    Task<Result<ConversationAgentTestResult>> TestAsync(ConversationAgentTest test, CancellationToken ct);
}
// Asistente → Forms (implementa Forms)
public interface IStructuredCatalogProposals
{
    Task<Result<StructuredCatalogProposal>> ProposeAsync(StructuredCatalogRequest request, CancellationToken ct);
}
// Forms → Asistente (implementa el módulo Assistant)
public interface IStructuredRuleDrafter
{
    Task<Result<StructuredRuleDraft>> DraftAsync(StructuredRuleRequest request, CancellationToken ct);
}
// En el núcleo, con su proveedor
public interface ISpeechTranscriber
{
    Task<Result<string>> TranscribeAsync(Stream audio, string mediaType, CancellationToken ct);
}
```

`IStructuredFormSource` suma los candidatos con su `OfferWhen`, sin valores. **Las herramientas** viven en `A/Modules/Assistant/Tools`, como dice la guía, y solo leen o proponen: ejecuta siempre el módulo dueño. Ningún módulo implementa `IAssistantTool`, porque `ModuleIsolationTests` lo prohíbe: hablan por los puertos de arriba. `propose_form_offer` y `report_unanswered` son acciones de la propuesta de un turno, con los candidatos que WhatsApp pasa en `ConversationTurn`; `propose_catalog` usa `IStructuredCatalogProposals`, y `propose_rule`, que llama `FormRuleProposalService`, `IStructuredRuleDrafter`.

**La campana:** suma `assistantUnanswered`, a quien tiene `whatsapp.manage`, por `INotificationSource` (base de la 12).

**Errores:**

| Código | Tipo | Cuándo |
| --- | --- | --- |
| `Assistant.Agent.Invalid` | Validation (`cases[i]…`) | un caso, una acción o un límite fuera de regla, o sobre el máximo de la plataforma |
| `Assistant.Agent.Unavailable` | ServiceUnavailable | tope de IA agotado o sin modelo calificado |
| `WhatsApp.Chat.NotAssistantMode` | Conflict | una acción que pide ese modo |
| `Forms.Suggestions.Invalid` | Validation (`suggestions[i]`) | más de 20, otro destinatario, bucle o mapeo de otro tipo |
| `Forms.Catalog.NotProposed` | Conflict | aprobar o descartar uno que no espera aprobación |

Un texto que no pasa el validador no es un error: se deriva, y queda en «Por qué respondió esto».

## Datos

Dos migraciones, una por módulo. Todo es `ITenantOwned`, con RLS. Permiso nuevo: `formsuggestions.manage`, con su migración de roles.

| Tabla | Qué guarda | Regla |
| --- | --- | --- |
| **AssistantAgents** (`IAuditable`, `IVersioned`) | `Name`, `IsPrimary`, `Profile`, `Cases` y `Limits` jsonb | una principal por organización, con índice único filtrado |
| **AssistantKnowledge** (`IAuditable`, `IVersioned`) | `Sections` jsonb | una por organización |
| **WhatsAppInboxAttention** (`IVersioned`) | `Inbox`, `Mode`, `ReturnRule`, `IdleMinutes?` | una por bandeja |
| **WhatsAppAssistantActions** | `ChatId`, `TurnId`, `MessageId?`, `CaseId`, `Kind`, `State`, `Why` jsonb | sigue la retención del chat |
| **WhatsAppAssistantCounters** | `ChatId`, `Day`, `Offers`, `Replies` | clave por chat y día, en la zona de la organización |
| **WhatsAppUnansweredTopics** | `Topic`, `Questions`, `People`, `State` | sin nombres ni mensajes |
| **FormMissingTopics** | lo mismo | sin nombres ni mensajes |
| **FormSuggestionSets** | `DefinitionId`, `Revision`, `Suggestions` jsonb | inmutable, como las reglas |

**Columnas nuevas:** `WhatsAppChats` suma `AttentionMode`, `Pinned`, `NeedsPersonAtUtc?`, `NeedsPersonReason?`, `ResolvedAtUtc?` y `TurnDueAtUtc?`. `WhatsAppChatMessages` suma `Transcript?`. `WhatsAppFlowSessions` suma `SentByAssistant`, y `SentByUserId` pasa a admitir `null`. `FormDefinitions` suma `OfferWhen`, que no se sella: cambiarlo no pide republicar. `FormDefinitionStates` suma `ActiveSuggestionSetId`. `FormRecords` suma `SuggestedByRecordId?`. `FormCatalogs` suma el estado `Proposed`. `AssistantPlatformSettings` suma los máximos. Asistente suma `ChannelId` y `ChannelConversationId`, con el índice `(TenantId, OwnerUserId, ChannelId, ChannelConversationId)`.

## Pasos

Abreviaturas: `D/`, `A/`, `I/` y `P/` son las capas del back; `F/`, el front, y `[E14]` a `[E21]`, un archivo que deja esa etapa.

**Back:**

| # | Qué hace | Archivos | Copia de | Test |
| --- | --- | --- | --- | --- |
| B1 | Las ADR: respuestas redactadas, acciones con confirmación y transcripción | `docs/decisions/` | ADR 0048 | — |
| B2 | Modo de atención | suma a `D/WhatsApp/WhatsAppChat.cs`, `WhatsAppInboundGuard.cs` y `WhatsAppAttentionBotWriter.cs`; `WhatsAppAttentionService.cs` | `WhatsAppTeamService.cs` | chat nuevo; vuelve solo; fijado; tomar a mitad de un envío; el bot solo en manual |
| B3 | El agente y el conocimiento | `D/Assistant/AssistantAgent.cs`, `AssistantKnowledge.cs`, `AssistantAgentService.cs`, `AssistantAgentPolicy.cs`; suma a `PlatformAssistantSettingsController.cs` | `AssistantOrganizationSettings.cs` | 409 en cada versión; máximos de la plataforma; «Cualquier otra cosa» |
| B4 | El motor para un chat | `A/Modules/Assistant/Services/ConversationAgentService.cs`, `AssistantChannelContext`; suma a `AssistantTurnBuilder.cs` | `AssistantTurnService.cs` | orden estable; 10 mensajes; dos conversaciones no se mezclan; sin `ExecutionActor` |
| B5 | El turno en WhatsApp: ráfaga, validar, encolar y derivar | `WhatsAppAssistantTurnService.cs`, `WhatsAppAssistantWorker.cs`, `WhatsAppReplyPolicy.cs` | `WhatsAppInboundProcessor.cs`, `WhatsAppModulePolicy.cs`, `WhatsAppReplyIssuer.cs` | ráfaga de tres; 3 acciones; propuesta tardía; validador; topes; IA caída; en un chat vinculado se escribe la copia compartida; la lista de `TenantScopeUsageTests` |
| B6 | Ofrecer y prellenar | suma a `FormStructuredSource.cs` y `WhatsAppFlowBuilder.cs`; la sesión | esos mismos [E14] | candidatos; destinatario; sin las versiones solo para la app; sin prellenar con un perfil anterior, que dice `WhatsAppFlowBindings.Profile` |
| B7 | Probar | `AssistantAgentTestService.cs` | `ConversationAgentService.cs` | no manda; cuenta en el tope |
| B8 | Audios | `A/Interfaces/Integrations/Speech/ISpeechTranscriber.cs`, su adaptador y su apagado | `WhatsAppChatFileService.cs` [E18] | con y sin tope; retención |
| B9 | Sugerencias | `FormSuggestionService.cs`; `WhatsAppSuggestionService.cs`; suma a `FormDefinitionCopyCreator.cs` [E17] | `FormRuleService.cs` [E21] | bucle; hacia un dado de baja; mapeos; la cadena; copiar lleva la primera revisión |
| B10 | Regla desde un texto, catálogo propuesto y temas | `FormRuleProposalService.cs`, las herramientas, los dos puertos y sus apagados, `FormMissingTopicService.cs`, `WhatsAppUnansweredTopicService.cs` | `OrganizationUsersTool` | nunca guarda; tope agotado; sin nombres; descartar un catálogo propuesto; `ModuleIsolationTests` |
| B11 | Rutas, contratos y ejemplos | `P/Modules/WhatsApp/Controllers/OrganizationWhatsAppAssistantController.cs`, `OrganizationWhatsAppConversationsControllerBase.cs`, `P/Modules/Forms/Controllers/*` | esos mismos | cada ruta con 401, 403, feature apagada = 404; aislamiento |

**Front:**

| # | Qué hace | Archivos | Copia de | Test |
| --- | --- | --- | --- | --- |
| F1 | Tipos y llamadas | suma a `F/src/modules/assistant/`, `whatsapp/` y `forms/` | esos mismos | zod estricto |
| F2 | La pantalla «Asistente» | `F/src/modules/whatsapp/assistant/` | el tablero | pestañas; un solo Guardar; 409; Probar |
| F3 | Modo y atención | suma a `ConversationView.tsx` y `ChatsWorkspace.tsx` | el tablero | cambiar, fijar y «Resuelto»; el filtro |
| F4 | El chat | suma a `MessageThread.tsx` | el tablero | porqué, botones, borrador y sugerencia |
| F5 | Temas | `UnansweredTopicsPage.tsx`, `MissingFormsPage.tsx` | su tablero | descartar; sin nombres |
| F6 | El editor | suma a `FormEditorPage.tsx` y `RulesSection.tsx` | esos mismos | «Cuándo lo ofrece»; «Qué sugerir después»; «Armar desde un texto» |

**Al cerrar:** las fichas del asistente, de WhatsApp y de formularios suman su sección, y la regla de WhatsApp pasa a «solo texto redactado con estos límites, o fijo».

## Qué verificar al arrancar

| Marca | Qué hay que confirmar | Qué asume esta spec |
| --- | --- | --- |
| [Meta] | Valores iniciales en un Flow con datos, sin endpoint. | Un perfil nuevo los admite por `data`. |
| ADR | La ADR 0048, D8, y la 0038, que dejaban esto para después. | Se actualizan con esta etapa. |
| Asistente | Cómo se separa el motor de la sesión de una persona. | Un turno de canal no necesita cuenta. |
| [E18] | El audio del chat. | Está guardado cuando se transcribe. |
| [E21] | La cola con su origen. | El asistente sale por la misma cola. |
| Proveedor | Quién transcribe. | Se elige con su ADR. |

## Lo que ya se pagó allá (no repetir)

Cada punto lleva su test, escrito durante la etapa:
1. **Con un índice por dueño y canal,** un operador tenía una sola operación de IA para todos sus chats: tres chats, y los dos últimos terminan.
2. **La espera de 35 s,** por un cierre que no avisaba.
3. **Un audio «Transcribiendo…» para siempre,** porque una falla del GET abortaba el POST.
4. **Una respuesta tardía** después de tomar el chat no sale.
5. **`business.information`** incluía textos sin revisar como si fueran hechos: lo vacío no se usa.

## Decisiones tomadas (no se vuelven a preguntar)

- **Casos** (39): contesta lo que configure cada organización, redacta dentro de sus límites y deriva lo demás.
- **La IA nunca guarda ni confirma una respuesta,** y siempre propone: ejecuta el módulo dueño.
- **La configuración vive en nuestra base,** por organización. **Genérico:** ninguna respuesta queda en el código.
- **Un solo botón, «Abrir formulario».**
- **Los catálogos del asistente** son solo con filtros, y siempre los aprueba una persona.
- **Un modelo se habilita solo con un perfil calificado.**
- **El conocimiento** es una pestaña, sin pantalla ni permiso aparte (46).
- **El agente vive en el módulo Assistant,** y el modo de cada chat, en WhatsApp.

## Fuera de esta etapa

- Varios asistentes y quién contesta cada chat (E24).
- Herramientas que escriben, que estrena la E25 (75), y leer fotos y documentos (E25).
- El acceso Personal: un cliente con cuenta ve la copia de la conversación.

## Cómo se cierra

La puerta de la etapa, con la lista de control de `/puerta`, más:
- **pruebas de arquitectura:** nadie llama al modelo fuera de las piezas del asistente, y las herramientas no reciben `IUnitOfWork`;
- **casos de evaluación** antes de habilitar el modelo: con y sin la información, fuera de tema, saludo y hora, derivación e instrucciones escondidas;
- **la medición** de texto a envío y de audio a envío;
- **prueba real con la IA y Meta:** «hola» y «¿qué hora es?» tienen sentido; una consulta con la información se responde y una sin ella deriva; «se rompió la heladera» manda el formulario; «quiero hablar con alguien» deriva; un audio transcripto, y un cliente recibe una respuesta y un formulario para clientes.

## Requisitos previos

- La E22 cerrada.
- La ADR de respuestas redactadas, y las dos piezas de la ADR 0048 (D8) y la 0038.

## En el multitenant

- **Tope de IA por organización (ADR 0051):** el modelo, la transcripción y Probar.
- **Datos de la organización:** todo con RLS. El historial nunca es de otra conversación ni de otra organización, con su test.
- **Workers:** los turnos abren un scope por organización con `SetFromAccess(tenantId, TenantKind.Business)`, como `WhatsAppInboundProcessor`, sin `ExecutionActor`, porque un cliente no tiene cuenta, y sin `TenantJobRunner`: lo que sale en un chat vinculado escribe la copia compartida. Se suman a la lista de `TenantScopeUsageTests`.
- **Features:** `forms` y `assistant`, prendidos en la organización. En un worker, `IFeatureService` da `false`, porque no hay sesión: se lee `ITenantFeatureCache`, como `WhatsAppModulePolicy`.
- **Permisos:** suma `formsuggestions.manage`, y `formcatalogs.manage` completa su texto. Lo demás pide `whatsapp.manage` o el permiso de atender.
- **WhatsApp:** el asistente actúa en el número de la organización.
- **Cuotas:** las rutas cuentan en las generales, y Probar, además, en la del Asistente.
