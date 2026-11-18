# E23 · El asistente, con Casos: datos y contrato (spec, parte)

**Estado:** pendiente · parte de la [spec de la E23](23-asistente.md), que dice qué leer y cómo se cierra.

## Contrato

| Ruta | Permiso | Pide | Devuelve |
| --- | --- | --- | --- |
| `GET` y `PUT /api/organization/whatsapp/assistant/agent` | `whatsapp.manage` | `{profile, cases[], limits{customers, internal}, version, knowledge?, knowledgeVersion?}`; el conocimiento viaja solo si cambió, con su propia versión | el agente y 200 `{version, knowledgeVersion}` |
| `POST /api/organization/whatsapp/assistant/agent/test` | `whatsapp.manage` y atender esa bandeja | `{messages[], inbox}`, de 1 a 5 mensajes, uno por renglón, como una ráfaga | `{actions[{caseId, why?, used[]?, preview}]}`; no manda nada ni gasta los topes de WhatsApp; sin `formrecords.read`, sin `why`, `used` ni lo prellenado de un formulario |
| `GET` y `PUT /api/organization/whatsapp/attention` | `whatsapp.manage` | por bandeja, `{mode, returnRule, idleMinutes?, version}` | la configuración |
| `POST …/conversations/{id}/assistant-mode` | atender esa bandeja y `whatsapp.manage` | `{pinned, version}` | 200 `{version}` |
| `POST …/conversations/{id}/pin` y `resolve` | atender esa bandeja | `{version}` | 200 `{version}` |
| `GET …/conversations/{id}/actions/{actionId}` | atender esa bandeja | — | el porqué |
| `POST …/conversations/{id}/suggestions/{suggestionId}/send` y `dismiss` | atender esa bandeja | `send`, con `[Idempotent]` | 202 y 204 |
| `GET /api/organization/whatsapp/unanswered-topics`, `…/filter-counts`, `POST …/{id}/dismiss` y `POST …/{id}/review` | `whatsapp.manage` | `page`, `pageSize`, `sort`, `search` y `status` (`unreviewed`, `open` o `dismissed`); `review` y `dismiss`, con `{version}` | `PagedResult<{id, topic, description, questions, people, lastAtUtc, state, reviewedBy?, reviewedAtUtc?, dismissedBy?, dismissedAtUtc?, version}>`; los conteos dan temas y preguntas por estado, para la banda y el filtro; `review` da 200 `{version}` o el 409 (219) |
| `GET /api/organization/forms/missing`, `…/filter-counts`, `POST …/{id}/dismiss` y `POST …/{id}/review` | `forms.manage` | lo mismo | lo mismo |
| `GET` y `PUT /api/organization/forms/{id}/suggestions` | `formsuggestions.manage` | `{revision, suggestions[]}` | `{revision}` |
| `POST /api/organization/forms/{id}/rules/proposals` | `formrules.manage` | `{text}` | `{rule}`; no guarda |

**Las del agente** llevan `[FeatureGate("assistant")]` y van en el módulo WhatsApp, en `OrganizationWhatsAppAssistantController.cs`: piden un permiso de WhatsApp (28), que el módulo Assistant no puede nombrar, porque retirar WhatsApp lo saca del catálogo. **Las del chat** van en una clase nueva, `OrganizationWhatsAppConversationsControllerBase`, entre `WhatsAppConversationsControllerBase` y los dos controllers de la organización, porque Plataforma no tiene modo de atención. **Rutas que cambian:** `POST …/conversations/{id}/take` de la E11, y responder sin tomar, que ya toma, pasan el chat a atención manual y dejan sin efecto una propuesta que llegue tarde, y siguen pidiendo solo atender esa bandeja; `GET` y `PUT /forms/{id}/draft` suman `offerWhen?`; `GET` y `PUT /api/platform/assistant/settings` suman `{maxReplyLength, maxRepliesPerDay, maxFormOffersPerDay}`, los máximos de la plataforma; los mensajes del chat, `assistantActionId?`, `transcript?` y el estado de la conversación: `attentionMode`, `pinned`, `needsPerson?`; la lista de chats, el filtro `needsPerson`.

**El conocimiento** (169) viaja en `knowledge`: `{business{name, description}, location{kind, address?, reference?, area?}, hours{days[7]{state, slots[]{from, to, nextDay}}, specialDates[]{date, state, slots[]}}, products?, policies?, faq[]{id, question, answer}}`. `state` es `Unknown`, `Open`, `Closed` o `AllDay`; las horas van en `HH:mm`, de 15 en 15 y en la zona horaria de la organización, y una franja que termina después de la medianoche lleva `nextDay`. «Aplicar de lunes a viernes» lo hace la pantalla: el contrato recibe siempre los siete días. La dirección es un texto: no se busca ni se guarda un lugar de Google.

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

`IStructuredFormSource` suma los candidatos con su `OfferWhen`, sin valores. **Las herramientas** viven en `A/Modules/Assistant/Tools`, como dice la guía, y solo leen o proponen: ejecuta siempre el módulo dueño. Ningún módulo implementa `IAssistantTool`, porque `ModuleIsolationTests` lo prohíbe: hablan por los puertos de arriba. `propose_form_offer` y `report_unanswered` son acciones de la propuesta de un turno, con los candidatos que WhatsApp pasa en `ConversationTurn`, y `propose_rule`, que llama `FormRuleProposalService`, usa `IStructuredRuleDrafter`. **El texto de la oferta** (165) no es parte de la propuesta: lo pone WhatsApp desde sus recursos, con el nombre del formulario, también al mandar una sugerencia.

**La campana:** suma `assistantUnanswered`, a quien tiene `whatsapp.manage`, por `INotificationSource` (base de la 12).

**Errores:**

| Código | Tipo | Cuándo |
| --- | --- | --- |
| `Assistant.Agent.Invalid` | Validation (`cases[i]…`) | un caso, una acción o un límite fuera de regla, o sobre el máximo de la plataforma |
| `Assistant.Knowledge.Invalid` | Validation (`knowledge.hours.days[i]…`) | una franja vacía, superpuesta, de más de 24 horas o la quinta de un día, una fecha especial repetida, o una pregunta frecuente sin pregunta o sin respuesta |
| `Assistant.Agent.Unavailable` | ServiceUnavailable | tope de IA agotado o sin modelo calificado |
| `WhatsApp.Chat.NotAssistantMode` | Conflict | una acción que pide ese modo |
| `Forms.Suggestions.Invalid` | Validation (`suggestions[i]`) | más de 20, otro destinatario, bucle o mapeo de otro tipo |

Un texto que no pasa el validador no es un error: se deriva, y queda en «Por qué respondió esto» con su motivo: `replyNotAllowed`, `replyTooLong`, `dailyReplyLimit`, `assistantFailed` o `button` (0054 §6).

## Datos

Tres migraciones, una por módulo: Assistant, WhatsApp y Forms. Todo es `ITenantOwned`, con RLS. Permiso nuevo: `formsuggestions.manage`, con su migración de roles.

| Tabla | Qué guarda | Regla |
| --- | --- | --- |
| **AssistantAgents** (`IAuditable`, `IVersioned`) | `Name`, `IsPrimary`, `Profile`, `Cases` y `Limits` jsonb | una principal por organización, con índice único filtrado |
| **AssistantKnowledge** (`IAuditable`, `IVersioned`) | `Sections` jsonb, con la forma de `knowledge` | una por organización; los topes de allá: nombre 120, qué hace 1000, dirección, referencia y zona 500 cada una, productos y servicios 2000, políticas y condiciones 2000, hasta 4 franjas por día, 32 fechas especiales y 20 preguntas frecuentes de 200, con su respuesta de 1000 |
| **WhatsAppInboxAttention** (`IVersioned`) | `Inbox`, `Mode`, `ReturnRule`, `IdleMinutes?` | una por bandeja |
| **WhatsAppChatModeChanges** | `ChatId`, `Mode`, `ChangedByUserId?`, `ChangedAtUtc` y `Reason` (a mano, derivó, volvió solo) | el historial del modo (H19 de ArquitecturaBase); al derivar el asistente, sin persona; sigue la retención del chat |
| **WhatsAppAssistantActions** | `ChatId`, `TurnId`, `MessageId?`, `CaseId`, `Kind`, `State`, `Why` jsonb | sigue la retención del chat |
| **WhatsAppAssistantCounters** | `ChatId`, `Day`, `Offers`, `Replies` | clave por chat y día, en la zona de la organización |
| **WhatsAppUnansweredTopics** (`IVersioned`) | `Topic`, `Description`, `Questions`, `People`, `LastAtUtc`, `State` (`Unreviewed`, `Open` o `Dismissed`), `ReviewedByUserId?`, `ReviewedAtUtc?`, `DismissedByUserId?` y `DismissedAtUtc?` | sin nombres ni mensajes de quien preguntó; `Topic`, con su índice de búsqueda; al asistente va solo el `Topic` de los revisados, y los que ya existen quedan `Unreviewed` (219) |
| **FormMissingTopics** | lo mismo | lo mismo |
| **FormSuggestionSets** | `DefinitionId`, `Revision`, `Suggestions` jsonb | inmutable, como las reglas |

**Columnas nuevas:** `WhatsAppChats` suma `AttentionMode`, `Pinned`, `NeedsPersonAtUtc?`, `NeedsPersonReason?` (el caso que derivó, `AssistantFailed` o el tope agotado), `ResolvedAtUtc?` y `TurnDueAtUtc?`. `WhatsAppChatMessages` suma `Transcript?`, que nace cifrado, como el texto redactado y el `Why` de `WhatsAppAssistantActions` ([0065](../../decisions/0065-contenido-del-chat-cifrado.md)). El dueño de un toque guarda su id con la decisión: el mismo toque recibe la misma respuesta, y otro sobre el mismo mensaje no decide otra vez (H21). `WhatsAppFlowSessions` suma `SentByAssistant`, y `SentByUserId` pasa a admitir `null`. `FormDefinitions` suma `OfferWhen`, que no se sella: cambiarlo no pide republicar. `FormDefinitionStates` suma `ActiveSuggestionSetId`. `FormRecords` suma `SuggestedByRecordId?`. `AssistantPlatformSettings` suma los máximos. Asistente suma `ChannelId` y `ChannelConversationId`, con el índice `(TenantId, OwnerUserId, ChannelId, ChannelConversationId)`.

## En el multitenant

- **Tope de IA por organización (ADR 0051):** el modelo, la transcripción y Probar.
- **Datos de la organización:** todo con RLS. El historial nunca es de otra conversación ni de otra organización, con su test.
- **Workers:** los turnos abren un scope por organización con `SetFromAccess(tenantId, TenantKind.Business)`, como `WhatsAppInboundProcessor`, sin `ExecutionActor`, porque un cliente no tiene cuenta, y sin `TenantJobRunner`: lo que sale en un chat vinculado escribe la copia compartida. Se suman a la lista de `TenantScopeUsageTests`.
- **Features:** `forms` y `assistant`, prendidos en la organización. En un worker, `IFeatureService` da `false`, porque no hay sesión: se lee `ITenantFeatureCache`, como `WhatsAppModulePolicy`.
- **Permisos:** suma `formsuggestions.manage`. Lo demás pide `whatsapp.manage` o el permiso de atender.
- **WhatsApp:** el asistente actúa en el número de la organización. Lo que manda y queda «Sin confirmar» no se repite solo y deriva (240, [0066](../../decisions/0066-lo-que-no-se-sabe-si-salio.md)).
- **La traza:** una sola, del webhook a Meta y al motor de IA, con `tenant.id` en el span y nunca en una métrica, ni números, cuerpos o ids de mensajes (H19; [logs](../../rules/logs.md)).
- **Cuotas:** las rutas cuentan en las generales, y Probar, además, en la del Asistente, con el cupo de quien prueba. El cupo vuelve solo si el turno no llegó al proveedor, y el tope de turnos pendientes es de la copia ([0064](../../decisions/0064-reglas-de-toda-la-ia.md) §1 y §12).
- **Las fallas del proveedor:** con la clave de la plataforma las arregla el operador, y la organización ve «no disponible» (0064 §7).

## Lo que ya se pagó allá (no repetir)

Cada punto lleva su test, escrito durante la etapa:
1. **Con un índice por dueño y canal,** un operador tenía una sola operación de IA para todos sus chats: tres chats, y los dos últimos terminan.
2. **La espera de 35 s,** por un cierre que no avisaba.
3. **Un audio «Transcribiendo…» para siempre,** porque una falla del GET abortaba el POST.
4. **Una respuesta tardía** después de tomar el chat no sale.
5. **`business.information`** incluía textos sin revisar como si fueran hechos: lo vacío no se usa, y solo van los hechos confirmados.
6. **La lista de formularios se cortaba en 100 sin orden,** y el corte cambiaba entre turnos: se ordena por nombre antes de cortar.
7. **Un tema que escribió un cliente llegaba al asistente de los demás chats:** nace «Sin revisar» (219), y la lectura de los candidatos no se marca parcial por los temas.
