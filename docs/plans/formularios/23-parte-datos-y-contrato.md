# E23 · El asistente, con Casos: datos y contrato (spec, parte)

**Estado:** pendiente · parte de la [spec de la E23](23-asistente.md), que dice qué leer y cómo se cierra. Tiene también cómo responde el asistente, los pasos y qué verificar al arrancar.

## Cómo responde

**Respuestas redactadas** (39), con su ADR:
- **fuentes** (67): lo de la organización, solo los hechos confirmados: nunca una pregunta sin respuesta ni un texto sin revisar, con la misma regla en el Inicio (0064 §9). Lo general, lo que sabe el modelo. Además, el historial de esa conversación y la hora. Lo leído es un dato, nunca una instrucción;
- corta, en el idioma de la conversación, sin enlaces, teléfonos, correos, precios ni compromisos que no estén cargados;
- **si es de la organización y no está cargado:** pregunta para entender, dice que lo consulta y deriva. Nunca contesta algo genérico;
- llega como propuesta tipada, y WhatsApp la valida antes de encolarla: el largo, el idioma, los enlaces y los datos de contacto. Si no pasa, no sale y deriva, con su motivo en «Por qué respondió esto»: no permitida en esa bandeja, más larga que el máximo, el tope de respuestas del día, la IA que no pudo responder o un botón;
- «Por qué respondió esto» guarda el texto, el caso y lo que usó. Los logs no llevan contenido.

**Un turno:**
1. llega un texto o un audio transcripto, en un chat en modo Asistente y dentro de las 24 horas;
2. espera 4 s sin mensajes nuevos, con un tope de 15 s, y procesa todo junto. Un sticker no reemplaza el pedido en curso, y el mismo toque de un botón recibe la misma respuesta;
3. arma el pedido en orden estable: perfil, casos, formularios candidatos, **hasta 10 mensajes de las últimas 24 horas de esa conversación** y lo que ya hizo;
4. el modelo elige el caso y propone hasta 3 acciones, una por cada cosa que pidió la persona, y el sistema valida cada una: que el caso exista, esté activo y su acción esté permitida en esa bandeja. Manda el texto actual del caso, no el que vio el modelo, las acciones salen en su orden y un formulario va solo;
5. encola en la salida y guarda el porqué;
6. **derivar** manda el mensaje fijo, pasa a atención manual y marca «Necesita una persona», en la cabecera, la lista y su filtro. La marca se quita cuando responde una persona;
7. en atención manual, la respuesta redactada se le ofrece al operador como borrador.

**Cuando la IA no contesta** un mensaje (falló, venció o quedó incierta), o lo que mandó queda «Sin confirmar» (240), el chat pasa a atención manual con «Necesita una persona» y el motivo «El asistente no pudo responder», y sale el mensaje fijo al derivar si la ventana de 24 horas sigue abierta. No deriva lo que todavía puede reintentarse ni un chat que ya tomó una persona. Lo que venció sin llegar al proveedor devuelve el cupo, y cada falla del proveedor lleva su código ([0064](../../decisions/0064-reglas-de-toda-la-ia.md)).

**Los topes** (270, 271 y 278): un tope que corta contesta o deriva, y dice por qué; nunca deja a la persona sin respuesta ni al operador sin pista. El de respuestas del día, el de ofrecimientos y el del mes de IA derivan como el paso 6, con su motivo en la franja del chat y en el porqué: «Llegó al tope de N respuestas de hoy», «Llegó al tope de ofrecimientos del día» o «El asistente no está disponible: se agotó el tope de IA del mes.», que no es «El asistente no pudo responder». Los contadores por chat y día cortan a la medianoche de la zona de la organización, y el cupo diario de quien usa Probar o «Armar desde un texto» es suyo en esta organización y corta igual (271). Un audio se transcribe también con el tope de ofrecimientos lleno; con el del mes agotado, el chat dice «No pudimos pasar este audio a texto» y deriva.

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
| `GET /api/organization/whatsapp/unanswered-topics`, `…/filter-counts`, `POST …/{id}/dismiss` y `POST …/{id}/review` | `whatsapp.manage` | `page`, `pageSize`, `sort`, `search` y `status` (`pending`, la de entrada, que junta sin revisar y abiertos; `unreviewed`, `open` o `dismissed`); `review` y `dismiss`, con `{version}` | `PagedResult<{id, topic, description, questions, people, lastAtUtc, state, reviewedBy?, reviewedAtUtc?, dismissedBy?, dismissedAtUtc?, version}>`; `sort` por cada columna, con `state` por el circuito y `-lastAtUtc` de entrada (264); los conteos dan temas y preguntas por estado, para la banda, porque el filtro no lleva cantidades (262); `review` da 200 `{version}` o el 409 (219) |
| `GET /api/organization/forms/missing`, `…/filter-counts`, `POST …/{id}/dismiss` y `POST …/{id}/review` | `forms.manage` | lo mismo | lo mismo |
| `GET` y `PUT /api/organization/forms/{id}/suggestions` | `formsuggestions.manage` | `{revision, suggestions[]}` | `{revision}` |
| `POST /api/organization/forms/{id}/rules/proposals` | `formrules.manage` | `{text}` | `{rule}`; no guarda |

**Las del agente** llevan `[FeatureGate("assistant")]` y van en el módulo WhatsApp, en `OrganizationWhatsAppAssistantController.cs`: piden un permiso de WhatsApp (28), que el módulo Assistant no puede nombrar, porque retirar WhatsApp lo saca del catálogo. **Las del chat** van en una clase nueva, `OrganizationWhatsAppConversationsControllerBase`, entre `WhatsAppConversationsControllerBase` y los dos controllers de la organización, porque Plataforma no tiene modo de atención. **Rutas que cambian:** `POST …/conversations/{id}/take` de la E11, y responder sin tomar, que ya toma, pasan el chat a atención manual y dejan sin efecto una propuesta que llegue tarde, y siguen pidiendo solo atender esa bandeja; `GET` y `PUT /forms/{id}/draft` suman `offerWhen?`; `GET` y `PUT /api/platform/assistant/settings` suman `{maxReplyLength, maxRepliesPerDay, maxFormOffersPerDay}`, los máximos de la plataforma, el último de 1 a 50 (278); los mensajes del chat, `assistantActionId?`, `transcript?` y el estado de la conversación: `attentionMode`, `pinned`, `needsPerson?`; la lista de chats, el filtro `needsPerson`.

**Los límites** (270 y 278): cada bandeja de `limits` lleva `maxReplyLength` (de 100 a 1000 caracteres, 400 de entrada), `repliesPerDay` (de 1 a 50, 20 de entrada), `formOffersPerDay` (de 1 a 50, 2 de entrada) y `reofferDeclinedAfterDays` (`null` es «Nunca», el de entrada, o de 1 a 365). Los tres topes van hasta el tope de la organización, o `Assistant.Agent.Invalid`: el Asistente los declara en el resolvedor de topes ([0069](../../decisions/0069-modulos-y-suscripciones.md)) con su máximo de la plataforma y los lee por él. Esos máximos van en los mismos rangos, con 600, 30 y 50 de entrada. `idleMinutes` de la atención va de 5 a 1440, con 30 de entrada. Los valores de entrada salen de `WA-Asistente-Limites`, `WA-Modo-Atencion` y la 278; los rangos del largo y de las respuestas, de ArquitecturaBase, y el de `idleMinutes`, de esta spec. Lo guardado antes de un valor lo lee con el de entrada.

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

Van en `A/Interfaces/Integrations/Conversations/`, `StructuredData/` y `Speech/`, con sus modelos en `A/Models/Conversations/` y `A/Models/StructuredData/`, y sus apagados en `I/Conversations/`, `I/StructuredData/` e `I/Speech/`. `IStructuredRuleDrafter` lo implementa `A/Modules/Assistant/Services/AssistantStructuredRuleDrafter.cs`.

`IStructuredFormSource` suma los candidatos con su `OfferWhen`, sin valores. **Las herramientas** viven en `A/Modules/Assistant/Tools`, como dice la guía, y solo leen o proponen: ejecuta siempre el módulo dueño. **Corren en el alcance de solo lectura** de «Acciones del asistente» (B2; 0068 §5), que en un turno de canal entra con la organización del turno, sin persona y nunca con `ITenantScope.Enter`; ese turno no ofrece las tres piezas de acciones (0068 §11). Ningún módulo implementa `IAssistantTool`, porque `ModuleIsolationTests` lo prohíbe: hablan por los puertos de arriba. `propose_form_offer` y `report_unanswered` son acciones de la propuesta de un turno, con los candidatos que WhatsApp pasa en `ConversationTurn`, y `propose_rule`, que llama `FormRuleProposalService`, usa `IStructuredRuleDrafter`. **El texto de la oferta** (165) no es parte de la propuesta: lo pone WhatsApp desde sus recursos, con el nombre del formulario, también al mandar una sugerencia.

**La campana:** suma `assistantUnanswered`, a quien tiene `whatsapp.manage`, por `INotificationSource` (base de la 12).

**Errores:**

| Código | Tipo | Cuándo |
| --- | --- | --- |
| `Assistant.Agent.Invalid` | Validation (`cases[i]…`) | un caso, una acción o un límite fuera de regla, o sobre el máximo de la plataforma |
| `Assistant.Knowledge.Invalid` | Validation (`knowledge.hours.days[i]…`) | una franja vacía, superpuesta, de más de 24 horas o la quinta de un día, una fecha especial repetida, o una pregunta frecuente sin pregunta o sin respuesta |
| `Assistant.Agent.Unavailable` | ServiceUnavailable | tope de IA agotado o sin modelo calificado |
| `WhatsApp.Chat.NotAssistantMode` | Conflict | una acción que pide ese modo |
| `Forms.Suggestions.Invalid` | Validation (`suggestions[i]`) | más de 20, otro destinatario, bucle o mapeo de otro tipo |

Un texto que no pasa el validador no es un error: se deriva, y queda en «Por qué respondió esto» con su motivo: `replyNotAllowed`, `replyTooLong`, `dailyReplyLimit`, `assistantFailed` o `button` (0054 §6). Un tope lleno tampoco: `dailyOfferLimit` (278) y `monthlyLimit` (270) derivan igual.

**Textos** (es / en):
- `Assistant.Agent.Invalid`: «Revisá este dato del asistente.» / "Check this assistant setting."
- `Assistant.Knowledge.Invalid`: «Revisá este dato de la información del negocio.» / "Check this business information field."
- `Assistant.Agent.Unavailable`: «El asistente no está disponible: se agotó el tope de IA del mes o no hay un modelo habilitado.» / "The assistant isn't available: the monthly AI limit was reached or no model is enabled."
- `WhatsApp.AssistantAction.NotFound`: «No encontramos esa acción del asistente.» / "We couldn't find that assistant action."
- `WhatsApp.Suggestion.AlreadyHandled`: «La sugerencia ya se mandó o se descartó.» / "The suggestion was already sent or dismissed."
- `WhatsApp.UnansweredTopic.NotFound` / `Forms.MissingTopic.NotFound`: «No encontramos ese tema.» / "We couldn't find that topic."
- `Forms.Suggestions.Invalid`: «Revisá esta sugerencia.» / "Check this suggestion."
- `Forms.Suggestions.RevisionNotFound`: «No encontramos esa revisión.» / "We couldn't find that revision."
- **Los motivos de los topes** (270 y 278), en los recursos de WhatsApp, con el número como parámetro: «Llegó al tope de {0} respuestas de hoy.» / "Reached today's limit of {0} replies."; «Llegó al tope de ofrecimientos del día.» / "Reached today's form offer limit."; «El asistente no está disponible: se agotó el tope de IA del mes.» / "The assistant isn't available: the monthly AI limit was reached."

## Datos

Tres migraciones, una por módulo: Assistant, WhatsApp y Forms. Todo es `ITenantOwned`, con RLS. Permiso nuevo: `formsuggestions.manage`, con su migración de roles.

| Tabla | Qué guarda | Regla |
| --- | --- | --- |
| **AssistantAgents** (`IAuditable`, `IVersioned`) | `Name`, `IsPrimary`, `Profile`, `Cases` y `Limits` jsonb | una principal por organización, con índice único filtrado |
| **AssistantKnowledge** (`IAuditable`, `IVersioned`) | `Sections` jsonb, con la forma de `knowledge` | una por organización; los topes de allá: nombre 120, qué hace 1000, dirección, referencia y zona 500 cada una, productos y servicios 2000, políticas y condiciones 2000, hasta 4 franjas por día, 32 fechas especiales y 20 preguntas frecuentes de 200, con su respuesta de 1000 |
| **WhatsAppInboxAttention** (`IVersioned`) | `Inbox`, `Mode`, `ReturnRule`, `IdleMinutes?` | una por bandeja |
| **WhatsAppChatModeChanges** | `ChatId`, `Mode`, `ChangedByUserId?`, `ChangedAtUtc` y `Reason` (a mano, derivó, volvió solo) | el historial del modo (H19 de ArquitecturaBase); al derivar el asistente, sin persona; sigue la retención del chat |
| **WhatsAppAssistantActions** | `ChatId`, `TurnId`, `MessageId?`, `CaseId`, `Kind`, `State`, `Why` jsonb, `DeclinedAtUtc?` | sigue la retención del chat; una oferta rechazada no vuelve a los candidatos de ese chat hasta el plazo de su límite (270) |
| **WhatsAppAssistantCounters** | `ChatId`, `Day`, `Offers`, `Replies` | clave por chat y día, en la zona de la organización; una pregunta con botones y el formulario elegido suman una sola oferta, y elegir de una ya ofrecida no suma (278) |
| **WhatsAppUnansweredTopics** (`IVersioned`) | `Topic`, `Description`, `Questions`, `People`, `LastAtUtc`, `State` (`Unreviewed`, `Open` o `Dismissed`), `ReviewedByUserId?`, `ReviewedAtUtc?`, `DismissedByUserId?` y `DismissedAtUtc?` | sin nombres ni mensajes de quien preguntó; `Topic`, con su índice de búsqueda; al asistente va solo el `Topic` de los revisados, y los que ya existen quedan `Unreviewed` (219) |
| **FormMissingTopics** | lo mismo | lo mismo |
| **FormSuggestionSets** | `DefinitionId`, `Revision`, `Suggestions` jsonb | inmutable, como las reglas |

**Columnas nuevas:** `WhatsAppChats` suma `AttentionMode`, `Pinned`, `NeedsPersonAtUtc?`, `NeedsPersonReason?` (el caso que derivó, `AssistantFailed`, `DailyReplyLimit`, `DailyOfferLimit` o `MonthlyLimit`), `ResolvedAtUtc?` y `TurnDueAtUtc?`. `WhatsAppChatMessages` suma `Transcript?`, que nace cifrado, como el texto redactado y el `Why` de `WhatsAppAssistantActions` ([0065](../../decisions/0065-contenido-del-chat-cifrado.md)). El dueño de un toque guarda su id con la decisión: el mismo toque recibe la misma respuesta, y otro sobre el mismo mensaje no decide otra vez (H21). `WhatsAppFlowSessions` suma `SentByAssistant`, y `SentByUserId` pasa a admitir `null`. `FormDefinitions` suma `OfferWhen`, que no se sella: cambiarlo no pide republicar. `FormDefinitionStates` suma `ActiveSuggestionSetId`. `FormRecords` suma `SuggestedByRecordId?`. `AssistantPlatformSettings` suma los máximos. Asistente suma `ChannelId` y `ChannelConversationId`, con el índice `(TenantId, OwnerUserId, ChannelId, ChannelConversationId)`.

**Entidades:** `D/Assistant/AssistantAgent.cs` y `AssistantKnowledge.cs`, con su `<Entidad>Configuration.cs` en `I/Modules/Assistant/Persistence/Configurations/`; `D/WhatsApp/WhatsAppInboxAttention.cs`, `WhatsAppChatModeChange.cs`, `WhatsAppAssistantAction.cs`, `WhatsAppAssistantCounter.cs` y `WhatsAppUnansweredTopic.cs`, con la suya en `I/Modules/WhatsApp/Persistence/`, y `D/Forms/FormMissingTopic.cs` y `FormSuggestionSet.cs`, con la suya en `I/Modules/Forms/Persistence/Configurations/`.

**Dos versiones:** cada migración agrega y escribe en el mismo despliegue (las columnas que pasan a admitir nulos, sus CHECK y los valores nuevos): es la excepción de la [0060](../../decisions/0060-dos-versiones-conviven.md), anotada en cada una, porque todavía no hay despliegue (la E10 va después del bloque, 123) y volver a la imagen anterior no aplica.

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
8. **Con el tope de ofrecimientos lleno,** el formulario quedaba bloqueado sin respuesta ni derivación, aunque la persona hubiera tocado un botón del propio asistente (O36): deriva con su motivo (278).
9. **Con el tope lleno, un audio no se transcribía** y quedaba sin respuesta, aunque pidiera algo que contesta un caso (O39): se transcribe siempre (278).
10. **Un cupo diario agotado derivaba con «El asistente no pudo responder»,** sin que la persona ni el operador supieran que fue el cupo (O35): cada tope dice el suyo (271).

Y lo que vio la [prueba integral de allá](../../backlog.md#lo-que-vio-la-prueba-integral-de-arquitecturabase), que va también en la lista de control de la puerta:
11. **Una sugerencia hacia un formulario que publicó otra versión** lo nombra y dice qué hacer (O20).
12. **Una sugerencia armada antes de tomar la conversación y mandada después:** el servidor la rechaza con un 409 claro, y la pantalla no la muestra como un resultado incierto (O32).
13. **Un audio que el proveedor devuelve vacío** contesta o deriva, y dice por qué (270). Allá el chat decía «No pudimos pasar este audio a texto», a la persona no le llegaba nada y no se derivaba.
14. **Ante «quiero cargar el registro de lluvia y además decime a qué hora abren»,** el motor propone solo el formulario (154).

## Pasos

Abreviaturas: `D/`, `A/`, `I/` y `P/` son las capas del back; `F/`, el front, y `[E14]` a `[E21]`, un archivo que deja esa etapa.

**Back:**

| # | Qué hace | Archivos | Copia de | Test |
| --- | --- | --- | --- | --- |
| B1 | Sumar a la 0048 una precisión con fecha: en WhatsApp, el asistente propone una de las seis acciones del menú (0054) y la ejecuta WhatsApp. La D8 queda como la dejó la 0068. Actualizar la 0038, que dejaba la IA para después | `docs/decisions/` | esas mismas | — |
| B2 | Modo de atención | suma a `D/WhatsApp/WhatsAppChat.cs`, `WhatsAppInboundGuard.cs` y `WhatsAppAttentionBotWriter.cs`; `WhatsAppAttentionService.cs` | `WhatsAppTeamService.cs` | chat nuevo; vuelve solo; fijado; tomar a mitad de un envío; el bot solo en manual |
| B3 | El agente y el conocimiento | `D/Assistant/AssistantAgent.cs`, `AssistantKnowledge.cs`, `AssistantAgentService.cs`, `AssistantAgentPolicy.cs`; suma a `PlatformAssistantSettingsController.cs` | `AssistantOrganizationSettings.cs` | 409 en cada versión; máximos de la plataforma; los límites con sus rangos, y sin asistente guardado, 2 y «Nunca» (270 y 278); «Cualquier otra cosa»; franjas que se pisan, la quinta de un día y la que pasa la medianoche |
| B4 | El motor para un chat | `A/Modules/Assistant/Services/ConversationAgentService.cs`, `AssistantChannelContext`; suma a `AssistantTurnBuilder.cs` | `AssistantTurnService.cs` | orden estable; 10 mensajes; dos conversaciones no se mezclan; sin `ExecutionActor` |
| B5 | El turno en WhatsApp: ráfaga, validar, encolar y derivar | `WhatsAppAssistantTurnService.cs`, `WhatsAppAssistantWorker.cs`, `WhatsAppReplyPolicy.cs` | `WhatsAppInboundProcessor.cs`, `WhatsAppModulePolicy.cs`, `WhatsAppReplyIssuer.cs` | ráfaga de tres; 3 acciones; propuesta tardía; validador; topes, que derivan con su motivo (270 y 278); IA caída; en un chat vinculado se escribe la copia compartida; la lista de `TenantScopeUsageTests` |
| B6 | Ofrecer y prellenar | suma a `FormStructuredSource.cs` y `WhatsAppFlowBuilder.cs`; la sesión; el texto de la oferta en los recursos de WhatsApp | esos mismos [E14] | candidatos; destinatario; sin las versiones solo para la app; sin prellenar con un perfil anterior, que dice `WhatsAppFlowBindings.Profile`; la oferta nombra el formulario; «elegir varias», el bloque, la Ubicación, «Mostrar si» y la marca quedan sin prellenar; con Ubicación, el pedido antes del Flow; el tope de ofrecimientos: una pregunta con botones y su formulario gastan uno, elegir de una ya ofrecida no gasta otra y un rechazado vuelve recién a su plazo (278) |
| B7 | Probar | `AssistantAgentTestService.cs` | `ConversationAgentService.cs` | no manda; cuenta en el tope y en el cupo del día de quien prueba (271) |
| B8 | Audios | `A/Interfaces/Integrations/Speech/ISpeechTranscriber.cs`, su adaptador y su apagado | `WhatsAppChatFileService.cs` [E18] | con el tope de ofrecimientos lleno se transcribe (278); con el del mes, deriva (270); el plazo de los chats (272) |
| B9 | Sugerencias | `FormSuggestionService.cs`; `WhatsAppSuggestionService.cs`; suma a `FormDefinitionCopyCreator.cs` [E17] | `FormRuleService.cs` [E21] | bucle; hacia un dado de baja; mapeos; la cadena; copiar lleva la primera revisión |
| B10 | Regla desde un texto, temas y campana | `FormRuleProposalService.cs`, las herramientas, el puerto y su apagado, `FormMissingTopicService.cs`, `WhatsAppUnansweredTopicService.cs` y la fuente `assistantUnanswered` de la campana | `OrganizationUsersTool` | nunca guarda; tope agotado; sin nombres; buscar por tema, ordenar por cada columna (264) y los conteos; la campana con y sin `whatsapp.manage`; `ModuleIsolationTests` |
| B11 | Rutas, contratos y ejemplos | `P/Modules/WhatsApp/Controllers/OrganizationWhatsAppAssistantController.cs`, `OrganizationWhatsAppConversationsControllerBase.cs`, `P/Modules/Forms/Controllers/*`; los contratos de WhatsApp, en `P/Modules/WhatsApp/Contracts/WhatsAppAttentionHttpContracts.cs`, y los de Forms, `SaveFormSuggestionsHttpRequest` y `ProposeFormRuleHttpRequest`, un archivo cada uno | esos mismos | cada ruta con 401, 403, feature apagada = 404; aislamiento |
| B12 | Acciones del asistente: las tres de la spec | `A/Modules/WhatsApp/Services/WhatsAppAttentionActionSource.cs` y `A/Modules/Forms/Services/FormMissingTopicActionSource.cs`, registradas como `IAssistantActionSource`, con sus pantallas en `AssistantActionScreen` y su traducción a `AssistantRouteKey`; sus textos `Action.*` en `WhatsAppTexts` y `FormsTexts`, es y en; sus escenarios en `TestFeatures/AssistantActions/` | `RoleActionSource.cs` | `AssistantActionDeclarationTests` y la batería genérica (`AssistantActionContractTests`) |

**Front:**

| # | Qué hace | Archivos | Copia de | Test |
| --- | --- | --- | --- | --- |
| F1 | Tipos y llamadas | suma a `F/src/modules/assistant/`, `whatsapp/` y `forms/` | esos mismos | zod estricto |
| F2 | La pantalla «Asistente» | `F/src/modules/whatsapp/assistant/pages/AssistantPage.tsx` (`/org/whatsapp/asistente`) | el tablero | pestañas; un solo Guardar; 409; Probar; el horario, con «Aplicar de lunes a viernes»; los límites de la 270 y la 278, y lo que se elige, en la pieza (262) |
| F3 | Modo y atención | suma a `ConversationView.tsx` y `ChatsWorkspace.tsx` | el tablero | cambiar, fijar y «Resuelto»; el filtro; el motivo de cada tope (270 y 278) |
| F4 | El chat | suma a `MessageThread.tsx` | el tablero | porqué, botones, borrador y sugerencia |
| F5 | Temas | `F/src/modules/whatsapp/assistant/pages/UnansweredTopicsPage.tsx` (`/org/whatsapp/preguntas`) y `F/src/modules/forms/pages/MissingFormsPage.tsx` (`/org/formularios/faltan`) | su tablero | descartar; sin nombres; la descripción en su columna (263); ordenar (264) |
| F6 | El editor | suma a `FormEditorPage.tsx` y `components/rules/` [E21] | esos mismos | «Cuándo lo ofrece»; «Qué sugerir después»; «Armar desde un texto», también con el cupo del día agotado (271) |
| F7 | Los máximos de la plataforma | suma a `PlatformAssistantSettingsPage.tsx` | su tablero | los tres máximos, el de ofrecimientos de 1 a 50 (278); 409 |
| F8 | «Cómo empezó» y la cadena | suma a `RecordActivity.tsx` [E15] el renglón «La mandó», con «El asistente» en «Quién» (267), y a `QuickView.tsx` y `RecordFullPage.tsx` | su tablero | «La mandó»; los datos propuestos; la cadena de respuestas |
| F9 | Dar de baja | suma a `LifecycleDialogs.tsx` y al editor dado de baja [E17] | su tablero | el asistente y las sugerencias en lo que deja de pasar; vuelven al reactivar |

**Al cerrar:** las fichas del asistente, de WhatsApp y de formularios suman su sección, y la regla de WhatsApp pasa a «solo texto redactado con estos límites, o fijo».

## Qué verificar al arrancar

| Marca | Qué hay que confirmar | Qué asume esta spec |
| --- | --- | --- |
| [Meta] | Valores iniciales en un Flow con datos, sin endpoint. | Un perfil nuevo los admite por `data`. |
| ADR | La de respuestas redactadas (39). | Rige la [0054](../../decisions/0054-respuestas-redactadas-por-el-asistente.md): las fuentes, la forma, el validador y derivar. |
| ADR | La 0048 y la 0038. | La 0048 suma una precisión (B1), sin tocar su D8, que remite a la 0068; la 0038 se actualiza. |
| Acciones | El alcance de solo lectura y sus entradas. | Suma la del turno de canal, con la organización y sin persona. |
| Costuras | El resolvedor de topes ([0069](../../decisions/0069-modulos-y-suscripciones.md) §4): cada módulo declara sus topes y el resolvedor da el de cada organización. | `maxReplyLength`, `repliesPerDay` y `formOffersPerDay` se declaran en su módulo y se leen por el resolvedor, nunca directo de la configuración de Plataforma; el valor de la organización se valida contra el tope que da. |
| Asistente | Cómo se separa el motor de la sesión de una persona. | Un turno de canal no necesita cuenta. |
| [E18] | El audio del chat. | Está guardado cuando se transcribe. |
| [E19] | Cómo queda el bloque en la versión sellada y en el Flow. | El prellenado lo saltea sin leerlo. |
| [E21] | La cola con su origen. | El asistente sale por la misma cola. |
| Proveedor | Quién transcribe. | OpenAI, con la [0055](../../decisions/0055-transcripcion-de-audios.md) (27). |
