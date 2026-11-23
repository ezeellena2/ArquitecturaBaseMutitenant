# E23 · El asistente, con Casos · plan ejecutable

**Estado:** pendiente · Spec: `docs/plans/formularios/23-asistente.md` y `23-parte-datos-y-contrato.md`. Tableros aprobados: lienzo v175, salvo los que la spec da como «Se redibujan» (261 a 264, 270, 271 y 278), que se aprueban antes de F2. ADR de la etapa: 0054 y 0055.

| Parte | Tareas | Estado |
| --- | --- | --- |
| Contrato | K0–K1 | Pendiente |
| Back | B1–B12 | Pendiente |
| Front | F1–F9 | Pendiente |
| Integración y puerta | I1–I3 | Pendiente |

## Forma de trabajo

Modo rápido: primero el contrato (K0 y K1); después Back y Front en paralelo, y una sola puerta al final. Cada tarea lleva sus pruebas focales; las suites y las reglas de `AGENTS.md` van enteras en los textos de los chats. Un arreglo corre solo lo suyo.

## Qué se verificó al arrancar

| Fila de la spec | Qué se encontró | Qué cambia |
| --- | --- | --- |
| [Meta] valores iniciales sin endpoint | allá anda en piloto (`WhatsAppFlowPrefillPolicy`, `init-values` y `flow_action_payload`) | se toma así; Meta, en la I1 de la E10 (123) |
| ADR de respuestas redactadas | no existía; la base es la 0021 de allá | [ADR 0054](../decisions/0054-respuestas-redactadas-por-el-asistente.md) |
| ADR 0048 (D8) y 0038 | D8: «solo responde en la v1»; la 0038 deja «IA» para después | B1 |
| El motor y la sesión de una persona | `AssistantTurnService` lee `ICurrentUser`; `AssistantTurnWorks` y `AssistantFinancialOperations` exigen una persona | el canal va sin cuenta (B4) |
| Proveedor de transcripción | Anthropic no transcribe; allá, OpenAI (`OpenAiAudioTranscriptionClient`) | OpenAI: [ADR 0055](../decisions/0055-transcripcion-de-audios.md) |
| Rutas del chat | las bandejas salen de `WhatsAppConversationsControllerBase`, que comparte Plataforma | usa la base de la organización de la E14 |
| Features y cuotas | `FeatureGateMiddleware` lee todos los `FeatureGateAttribute`; existe el perfil `AssistantConversation` | `[FeatureGate("assistant")]` y esa cuota |
| Workers | `SetFromAccess`, `ITenantFeatureCache` y `TenantScopeUsageTests.WhatsApp.cs` existen | se copian (B5) |
| [E14] a [E22] | todavía no existen | K0 |

## Contrato

### K0 · Lo que dejaron las etapas anteriores

El chat del contrato confirma esta lista antes de publicar.
- **Base:** `INotificationSource`, `NotificationItem`, el sufijo `NotificationSource` y `notifications` (`{kind, describe}`) en el `registration.ts` de un módulo; `ReasonDialog` y `PageLoading`.
- **Base, segunda parte** (214): el conversor del contenido del chat (0065); `OutboxStatus.Uncertain` con su revisión (0066 y 240); el clasificador de fallas del proveedor con `providerIssue` y el cupo que vuelve (0064 §1 y §7), y el diálogo en pasos de `PlatformAssistantSettingsPage` (254); la pieza `Picker` (262) y el orden con `ApplySort` (264), y de la B18, el cupo diario de la IA por persona y organización (271) y la purga de los chats a su plazo (272).
- **E14:** `IStructuredFormSource` (`ListSendableAsync` filtra por tipo de destinatario, 50), `IStructuredRecordSink`, `StructuredRecipient`, `StructuredRecordCapture`, `StructuredRecordReceipt`, `FormStructuredSource.cs`, `FormOfferBuilder.cs` con `offerProblems`, `WhatsAppFlowBuilder.cs`, `WhatsAppFlowBinding.Profile`, `WhatsAppFormSendService.cs`, `WhatsAppFlowSessions` (`SentByUserId`, `ProtectedOffer`, `FormName`), la captura (T5), `GET /{id}` y `PUT /{id}/draft`, `OrganizationWhatsAppConversationsControllerBase`, `FormEditorPage.tsx` y «Enviar formulario».
- **E15:** `IStructuredRecordOriginSource`, `StructuredRecordOrigin`, `WhatsAppStructuredRecordOriginSource.cs`, `FormRecordReader.cs`, `GET /{id}/records/{recordId}` con `sentBy?`, `QuickView.tsx` y `RecordFullPage.tsx`.
- **E16:** las opciones filtradas por destinatario en `FormOfferBuilder`, con `RecipientDataMissing`, `FilterUnavailable` y `NotForCustomers`.
- **E17:** `FormAvailabilityPolicy`, `FormDefinitionCopyCreator`, `DeactivatedAtUtc`, el editor dado de baja y `LifecycleDialogs.tsx`, con la fila «App · No se carga más» de la E20.
- **E18:** `WhatsAppChatFileService.cs` y los medios del chat, que siguen el plazo de los chats (272).
- **E19:** el bloque en la versión sellada y en el Flow (`rows`, `rowsOf`).
- **E20:** `FormVersions.AppOnly`, el tipo `location` y el pedido de ubicación antes del Flow (`WhatsAppFormSendService.cs` y `WhatsAppFlowSessions.LocationState`).
- **E21:** `FormRuleService`, `FormRulesPolicy`, `FormRuleSets`, `components/rules/`, `lib/rules.ts`, `Forms.Rules.Invalid`, el origen «acción de formulario» de la cola, `WhatsAppConversationLink.State` (`Unlinked` o `Linked`) y el perfil `flow-static-7.3-r12`.
- **E22:** el modo plantilla del editor y de `components/rules/`.

### K1 · Publicar antes de programar las pantallas

Lo publica el chat del contrato, como dice su texto, con los ejemplos exportados de cada respuesta, incluidos 400, 404, 409, 503 y los vacíos.

**Las ADR:** la 0054 fija cómo responde la IA (39, 67, 165 y 192), y la 0055, que transcribe OpenAI (27), con `Speech:OpenAI:Model` y la clave de la plataforma en user-secrets.

**Rutas.** Las del agente van en `OrganizationWhatsAppAssistantController` (`/api/organization/whatsapp`), con `[Access(Business)]`, `[FeatureGate(WhatsAppFeature.Key)]` y `[FeatureGate("assistant")]`. Las del chat van bajo el prefijo de su bandeja (`…` = `/api/organization/whatsapp/{customers|internal}`) y piden el permiso de atenderla. Los cuerpos son los de la spec; acá van los cambios.

| Ruta | Permiso | Cuota, idempotencia y versión | Errores |
| --- | --- | --- | --- |
| GET y PUT `assistant/agent` | `whatsapp.manage` | `version`, y `knowledgeVersion` solo si viaja `knowledge`; `limits` suma `formOffersPerDay` y `reofferDeclinedAfterDays` (270 y 278) | 400 `Assistant.Agent.Invalid` y `Assistant.Knowledge.Invalid`; 409 |
| POST `assistant/agent/test` | `whatsapp.manage` y atender esa bandeja | `assistantConversation`; `{messages[], inbox}`, de 1 a 5 | 400; 503 `Assistant.Agent.Unavailable` |
| GET y PUT `attention` | `whatsapp.manage` | `{customers, internal}`, cada una `{mode: Assistant\|Person, returnRule: Idle\|Resolved\|Never, idleMinutes?, version}` | 400; 409 |
| GET `unanswered-topics` y `…/filter-counts`; POST `…/{id}/dismiss` y `…/{id}/review` | `whatsapp.manage` | `status`: `pending` (de entrada), `unreviewed`, `open` o `dismissed`; `sort` (264); `{version}` | 404 `WhatsApp.UnansweredTopic.NotFound`; 409 |
| POST `…/conversations/{id}/assistant-mode` | atender y `whatsapp.manage` | `{pinned, version}`; feature `assistant` | 404; 409; 503 `Assistant.Agent.Unavailable` |
| POST `…/conversations/{id}/pin` | atender | `{pinned, version}` | 404; 409 |
| POST `…/conversations/{id}/resolve` | atender | `{version}` | 404; 409 |
| GET `…/conversations/{id}/actions/{actionId}` | atender | `{caseName, kind, why, used[], text?, createdAtUtc}` | 404 `WhatsApp.AssistantAction.NotFound` |
| POST `…/suggestions/{suggestionId}/send` y `dismiss` | atender | `send` con `[Idempotent]`, 202; `dismiss`, 204 | 404; 409 `WhatsApp.Suggestion.AlreadyHandled` y los de la ventana de la E11 |

En Forms (`/api/organization/forms`, `[Access(Business)]`, `[FeatureGate("forms")]`):

| Ruta | Permiso | Cuota, idempotencia y versión | Errores |
| --- | --- | --- | --- |
| GET `missing` y `…/filter-counts`; POST `…/{id}/dismiss` y `…/{id}/review` | `forms.manage` | feature `assistant`; como la anterior | 404 `Forms.MissingTopic.NotFound`; 409 |
| GET y PUT `{id}/suggestions` | `formsuggestions.manage` | `revision`, 409 | 400 `Forms.Suggestions.Invalid` |
| GET `{id}/suggestions/revisions` y `…/{revision}` | `formsuggestions.manage` | paginado | 404 `Forms.Suggestions.RevisionNotFound` |
| POST `{id}/rules/proposals` | `formrules.manage` | `assistantConversation`; feature `assistant`; no guarda | 400; 503 `Assistant.Agent.Unavailable` |

Las revisiones de sugerencias las pide el historial de `FRM-Editor-Sugerencias` (140). `pin` suma `pinned`, para desfijar.

**Rutas que cambian:**
- `…/conversations/{id}/take` y responder sin tomar pasan el chat a «Persona», quitan «Necesita una persona» y dejan sin efecto la propuesta que llegue tarde.
- `GET …/conversations` suma el filtro `needsPerson` y, por fila, `attentionMode`, `pinned` y `needsPerson?`; `GET …/conversations/{id}` suma ese estado, y cada mensaje, `assistantActionId?` y `transcript?`.
- `GET /forms/{id}` y `PUT /forms/{id}/draft` suman `offerWhen?` (no se sella).
- `GET` y `PUT /api/platform/assistant/settings` suman `{maxReplyLength, maxRepliesPerDay, maxFormOffersPerDay}`, con los valores iniciales de `Plat-Asistente-Config-23` y `WA-Asistente-Limites`; el de ofrecimientos, de 1 a 50 (278).
- `GET /forms/{id}/records/{recordId}` suma `sentBy.byAssistant`, `origin?{proposed[{fieldId, value, corrected}]}` y `chain[]{recordId, definitionId, displayNumber, formName, relation: Previous|Next}`. Lo del envío llega por el puerto de origen de la E15: `StructuredRecordOrigin` suma `ByAssistant` y `Proposed[]`, que arma `WhatsAppStructuredRecordOriginSource.cs`; la cadena la lee `FormRecordReader.cs` de `SuggestedByRecordId`.

Lo común: 401, 403, 404 con una feature apagada, 409 `General.ConcurrencyConflict`, idempotencia y paginado común.

**Campana**, como `documentsToReview`: un solo aviso, `assistantUnanswered`, a quien tiene `whatsapp.manage`, con `{count}`, las preguntas de los temas pendientes (sin revisar y abiertos, 219), `link` `/org/whatsapp/preguntas-sin-respuesta` e `id` `assistantUnanswered:{count}:{la pregunta más nueva}`. El texto, del front, es el de `Campana-23`: «{count} preguntas sin respuesta del asistente» / «{count} unanswered questions from the assistant».

**Puertos.** Los de la spec van en el núcleo (`A/Interfaces/Integrations/Conversations/`, `StructuredData/` y `Speech/`), con sus apagados en `I/Conversations/`, `I/StructuredData/` e `I/Speech/`, registrados con `TryAdd`. Suman:
- `IStructuredFormSource.ListOfferCandidatesAsync(StructuredRecipient, ct)`: los candidatos de la spec, sin `offerProblems`, con sus preguntas y sin valores;
- `IStructuredMissingFormSink.RecordAsync(StructuredMissingForm, ct)` (WhatsApp → Forms), para «Formularios que faltan»;
- `StructuredRecordCapture.SuggestedByRecordId?` y `StructuredRecordReceipt.Suggestions[]{definitionId, versionId, mode: Assistant|Always, prefill[]}`;
- `ConversationTurn` lleva `TurnId` (el id del último mensaje de la ráfaga), `ChannelId`, `ChannelConversationId`, el destinatario, hasta 10 mensajes, los candidatos y lo que ya hizo.

**Datos.** Los de la spec, más:
- `WhatsAppFlowSessions` suma `ProtectedPrefill?`, cifrado como `ProtectedOffer`, y `SuggestedByRecordId?`;
- `AssistantTurnWorks` y `AssistantFinancialOperations` suman `ChannelId?` y `ChannelConversationId?`; la persona admite nulo solo con canal (CHECK), y el índice único de trabajo activo es el de la spec en `platform`: `(ScopeTenantId, OwnerUserId, ChannelId, ChannelConversationId)`, con nulos no distintos;
- una sugerencia es una fila de `WhatsAppAssistantActions` con `State` `Suggested`;
- las migraciones: `<fecha>_AssistantAgents.cs`, `<fecha>_WhatsAppAssistant.cs` (aparte de `…_WhatsAppFormActions.cs` de la E21), `<fecha>_FormAssistant.cs` y `<fecha>_SystemRoleFormSuggestionPermissions.cs`.

**Permiso:** `formsuggestions.manage`, «Administrar sugerencias entre formularios» / «Manage suggestions between forms», con el texto de la §2 de la hoja de ruta; su área en los permisos de un rol (260), `Area.formsuggestions`, «Sugerencias» (*Suggestions*, 257), sin nada incluido.

**Textos de error** (es / en): los de la [parte de datos y contrato](formularios/23-parte-datos-y-contrato.md#contrato), con los motivos de los topes (270 y 278).

`WhatsApp.Chat.NotAssistantMode`, de la spec, no se publica: ninguna ruta pide ese modo, y la propuesta tardía se descarta sin error (B5).

**La oferta** (165), en los recursos de WhatsApp: «Te preparé el formulario «{0}» para que lo completes.» / «I prepared the form "{0}" for you to fill in.».

**Texto para el chat del Contrato**

```text
Leé AGENTS.md, este plan (docs/plans/2026-11-08-etapa-23-asistente.md), la spec de la E23 con su parte de datos y contrato, y las ADR 0048, 0051, 0038, 0054 y 0055. Primero K0: confirmá en el código cada nombre de la lista y anotá en «Cambios al contrato» lo que difiera. Después K1: publicá con 501 las rutas nuevas y las que cambian, el permiso con sus textos y su migración, los puertos con sus apagados, los errores es/en y los ejemplos exportados; generá openapi.json y avisale a la Guía para que el front corra npm run contracts. Guardas de rutas, módulos y contrato en verde. Reglas: todo en main, sin ramas, worktrees, stash, reset ni push; commiteá solo lo tuyo con git commit -- <rutas>, en español y con conventional commits; con la app levantada, nadie edita el src del front; TDD donde hay lógica; al terminar, build y tests rápidos enteros, sin Docker y sin filtrar por módulo, y si tocaste rutas, persistencia, autenticación o aislamiento, los de integración y seguridad del área; el contrato no cambia sin anotarlo en «Cambios al contrato»; ninguna clave real pasa por vos; al usuario se le pregunta solo lo de producto.
```

## Back

Rigen Result, `OperationLog.RunAsync`, `[LoggerMessage]` sin contenido, `TimeProvider`, una UoW por método que escribe, RLS y la regla de los módulos: el modelo lee o propone, y ejecuta el módulo dueño. Cada punto de «Lo que ya se pagó allá» lleva su test.

1. **B1 · ADR.** La 0048 (D8: el asistente de WhatsApp propone acciones tipadas que ejecuta WhatsApp) y la 0038 (la IA atiende con Casos; el bot, solo en Clientes en manual), con fecha.
2. **B2 · Modo de atención.** Suma a `D/WhatsApp/WhatsAppChat.cs` las columnas de la spec, `D/WhatsApp/WhatsAppInboxAttention.cs`, `WhatsAppAttentionService.cs`, y a `WhatsAppInboundGuard.cs`, `WhatsAppAttentionBotWriter.cs` y `WhatsAppConversationService.cs` (tomar y responder), con `<fecha>_WhatsAppAssistant.cs`. Cada cambio de modo escribe `WhatsAppChatModeChanges`, con quién y cuándo (H19). Copiá `WhatsAppTeamService.cs`. Tests: chat nuevo según su bandeja; vuelve por tiempo, al resolver o nunca; fijado no vuelve; tomar a mitad de un envío deja sin efecto la propuesta (punto 4); el bot solo en manual; responder quita «Necesita una persona».
3. **B3 · El agente y el conocimiento.** `D/Assistant/AssistantAgent.cs` y `AssistantKnowledge.cs`, `AssistantAgentService.cs` (implementa `IConversationAgentSettings`) y `AssistantAgentPolicy.cs`; los máximos en `AssistantPlatformSettings` y `PlatformAssistantSettingsController.cs`, con `<fecha>_AssistantAgents.cs`. Sin fila, el `GET` da el agente por defecto con `version: 0`, y el primer `PUT` lo crea. Copiá `AssistantOrganizationSettings.cs` y `AssistantSettingsService.cs`. Tests: 409 en cada versión; los máximos; los límites con sus rangos, y sin fila o guardado antes, 2 y «Nunca» (270 y 278); «Cualquier otra cosa»; sin perfil; franjas que se pisan, la quinta y la que pasa la medianoche; fecha repetida; pregunta frecuente incompleta.
4. **B4 · El motor para un chat.** `ConversationAgentService.cs` (implementa `IConversationAgent`), `AssistantChannelContext` y lo que suma a `AssistantTurnBuilder.cs`: perfil, casos, candidatos, historial y lo hecho, en orden estable, con solo los hechos confirmados (punto 5) y los temas revisados (219). El trabajo es un `AssistantTurnWork` de canal, idempotente por `TurnId`, que no cobra dos veces y cuenta solo en el tope mensual, no en los 50 mensajes por persona. El modelo es el de `Assistant:Channel:ProviderModelId`, calificado; sin él, `Unavailable`. Copiá `AssistantTurnService.cs`. Tests: orden estable; 10 mensajes de 24 horas; tres conversaciones a la vez no se mezclan ni se cancelan (punto 1); sin `ICurrentUser` ni `ExecutionActor`; tope agotado.
5. **B5 · El turno en WhatsApp.** `WhatsAppAssistantTurnService.cs`, `WhatsAppAssistantWorker.cs` y `WhatsAppReplyPolicy.cs` (el validador), con «Cómo responde» de la spec. La ráfaga: `TurnDueAtUtc` a 4 s del último mensaje, con tope de 15 s desde el primero. La única cola durable son los chats con `TurnDueAtUtc`: el worker los toma corriendo esa fecha como lease; el escritor de entrada avisa con una señal local al confirmar, y el worker sondea cada 2 s solo con pendientes (punto 2). En «Persona», una propuesta que llega se descarta y el texto va como borrador (`SuggestReplyAsync`). **Los topes** por chat y día van en `WhatsAppAssistantCounters`, en la zona de la organización, y los límites se leen del agente en cada turno: con uno lleno, o el del mes agotado, deriva con `DailyReplyLimit`, `DailyOfferLimit` o `MonthlyLimit` (270 y 278). Solo un chat `Linked` escribe la copia compartida. La medición, un histograma de texto o audio a envío. El texto redactado, el `Why` y la transcripción nacen cifrados (0065). Copiá `WhatsAppInboundProcessor.cs`, `WhatsAppModulePolicy.cs` y `WhatsAppReplyIssuer.cs`. Tests: ráfaga de tres; tres acciones, en su orden, y un formulario solo; propuesta tardía; validador y cada motivo; cada tope deriva con el suyo, y el mensaje fijo sale solo con la ventana abierta; IA caída deriva con `AssistantFailed` y «El asistente no pudo responder», salvo lo reintentable o un chat tomado; lo vencido sin intentarse devuelve el cupo; un sticker y el mismo toque (H21); «Sin confirmar» deriva (240); la copia con `Linked` y sin ella con `Unlinked`; `TenantScopeUsageTests`.
6. **B6 · Ofrecer y prellenar.** Suma a `FormStructuredSource.cs` `ListOfferCandidatesAsync`, con `FormAvailabilityPolicy` (un candidato con un problema de la oferta de la E16 no se ofrece), y a `WhatsAppFlowBuilder.cs` el perfil `flow-static-7.3-r13`, con `init-values`, sobre el `r12`; la sesión, la oferta y `FormDefinitions.OfferWhen`, con `<fecha>_FormAssistant.cs`. Copiá esos mismos [E14]. Los candidatos y la Ubicación, como la spec (230). Tests: candidatos; el orden antes del corte; destinatario; sin las solo para la app; con cada problema de la oferta; un perfil anterior va sin prellenar; la oferta nombra el formulario; «elegir varias», fotos, documentos, el bloque, la Ubicación, «Mostrar si» y la marca, sin prellenar; con Ubicación, el pedido antes del Flow y sin turno; el tope de ofrecimientos (278): una pregunta con botones y su formulario gastan uno, elegir de una ya ofrecida no gasta otra, y un rechazado vuelve recién a su plazo (`DeclinedAtUtc`, 270).
7. **B7 · Probar.** `AssistantAgentTestService.cs`, como la spec, con el tope, la cuota y el cupo del día de quien prueba en esta organización (271); sin `formrecords.read`, sin el porqué ni lo prellenado. Copiá `ConversationAgentService.cs`. Tests: no manda; cuenta en el tope; el cupo del día agotado, con su motivo; cinco mensajes y el sexto; sin «Ver respuestas»; las cuatro pruebas del tablero.
8. **B8 · Audios.** `ISpeechTranscriber`, su apagado `I/Speech/DisabledSpeechTranscriber.cs` y su implementación en el módulo Assistant (`AssistantSpeechTranscriber.cs`, con el tope, e `I/Modules/Assistant/Speech/OpenAiSpeechClient.cs`); WhatsApp transcribe el audio que bajó la E18 y guarda `Transcript`. Copiá `WhatsAppChatFileService.cs` [E18]. Tests: con el tope de ofrecimientos lleno se transcribe y sigue como un texto (278); con el del mes agotado, «No pudimos pasar este audio a texto» y deriva (270); una falla de la lectura no deja «Transcribiendo…» (punto 3); sin vencimiento propio: el audio y su transcripción se van con el chat, a su plazo (272).
9. **B9 · Sugerencias entre formularios.** `FormSuggestionService.cs`, `D/Forms/FormSuggestionSet.cs`, `ActiveSuggestionSetId`, `SuggestedByRecordId`, `Suggestions[]` en el recibo, `WhatsAppSuggestionService.cs` (`Always`: una tarjeta para el operador; `Assistant`: un candidato del turno siguiente), la migración del permiso, y lo que suma a `FormDefinitionCopyCreator.cs` [E17]: `OfferWhen` y las sugerencias vigentes, como su primera revisión. Copiá `FormRuleService.cs` [E21]. Tests: bucle; hacia un dado de baja o de la app (258); mapeos; la cadena; la copia lleva `OfferWhen` y las sugerencias.
10. **B10 · Regla desde un texto, temas y campana.** `FormRuleProposalService.cs` con `IStructuredRuleDrafter` (lo implementa el Asistente con `propose_rule`, con el modelo calificado del canal, como Probar: 218), `FormMissingTopicService.cs` con `IStructuredMissingFormSink`, `WhatsAppUnansweredTopicService.cs` y `WhatsAppUnansweredNotificationSource.cs`. Los estados de un tema, como la spec (202 y 219): nace `Unreviewed`, y `review` lo pasa a `Open`, con su versión; el listado ordena por cada columna (264). Copiá `OrganizationUsersTool` y `FormPublicationNotificationSource` [E14]. Tests: un tema sin revisar no llega a otro chat; revisar con 409; nunca guarda; `propose_rule` con el modelo del canal y, sin él, 503; tope agotado y cupo del día (271); sin nombres; buscar, ordenar y contar; un descartado vuelve, sin revisar, y a la campana; la campana con y sin `whatsapp.manage`; `ModuleIsolationTests`.
11. **B11 · Rutas y contratos.** `OrganizationWhatsAppAssistantController.cs`; lo que suma a `OrganizationWhatsAppConversationsControllerBase.cs` [E14]; `P/Modules/Forms/Controllers/BusinessFormSuggestionsController.cs` y `BusinessFormMissingTopicsController.cs`, y las rutas que cambian. Copiá `WhatsAppTeamsControllerBase.cs` y los controllers de la E21. Tests: cada ruta con 401, 403 y feature apagada = 404; las de Plataforma no existen; aislamiento en `Api.IntegrationTests/Tenancy/`; ejemplos exportados; `ExplicitRouteInventoryTests` y `GrowingCollectionContractTests`.
12. **B12 · Arquitectura, evaluación y ayuda.** Prueba de arquitectura: solo Assistant usa `IChatClientFactory`, y ninguna herramienta recibe `IUnitOfWork`. Los casos de evaluación con el modelo real (con y sin la información, fuera de tema, saludo y hora, derivación e instrucciones escondidas), en `tests/…/Modules/Assistant/Evaluation/`, que sin clave se saltean. La ayuda `whatsapp/asistente.{es,en}.md`.

**Texto para el chat del Back**

```text
Leé AGENTS.md, este plan (docs/plans/2026-11-08-etapa-23-asistente.md), la spec de la E23 con su parte de datos y contrato, las ADR 0048, 0051, 0038, 0054 y 0055, la guía de herramientas del asistente y las fichas que nombra la spec. Después de K1, hacé B1 a B12 en orden y contra ese contrato, copiando los moldes de cada tarea, con FakeChatClient. No toques el front ni la tabla de estado del plan. Reglas: todo en main, sin ramas, worktrees, stash, reset ni push; commiteá solo lo tuyo con git commit -- <rutas>, en español y con conventional commits; con la app levantada, nadie edita el src del front; TDD donde hay lógica; al terminar cada tarea, build y tests rápidos enteros del back, sin Docker y sin filtrar por módulo, y con rutas, persistencia, autenticación o aislamiento, los de integración y seguridad de Assistant, WhatsApp y Forms; el contrato no cambia sin anotarlo en «Cambios al contrato» y avisarle a la Guía; ninguna clave real pasa por vos; al usuario se le pregunta solo lo de producto.
```

## Front

Siguen los tableros v175 y las reglas de toda la app (160 a 164, 173 a 175, 180 y 261 a 264), con i18n, tipos generados y axe.

1. **F1 · Tipos y llamadas.** Suma a `F/src/modules/whatsapp/assistant/api/`, `whatsapp/chats/api/`, `forms/api/` y `assistant/api/`, con zod estricto contra los ejemplos.
2. **F2 · La pantalla «Asistente».** `/org/whatsapp/asistente`, en `F/src/modules/whatsapp/assistant/` y en el panel de WhatsApp, con `whatsapp.manage`: `WA-Asistente` y su teléfono, `WA-Asistente-Casos`, `WA-Asistente-Probar`, `WA-Asistente-Limites` y `WA-Modo-Atencion`. Un solo Guardar manda el agente y la atención que cambiaron, y «Probar» va como la spec. Límites suma el tope de ofrecimientos y «Volver a ofrecer un formulario rechazado» (270 y 278); la bandeja y lo que se elige, en la pieza (262), y los casos, un dato por columna (263). Tests: pestañas; un Guardar; 409; Probar, con cinco mensajes, sin «Ver respuestas» y con el cupo del día agotado (271); los límites; el horario con «Aplicar de lunes a viernes».
3. **F3 · Modo y atención en el chat.** Suma a `ConversationView.tsx` y `ChatsWorkspace.tsx` el ⋮ con el modo, fijar y «Marcar como resuelta», y «Necesita una persona» en la cabecera, la lista y su filtro (`WA-Chats-Casos` y su teléfono, y `M-WA-Modo-Atencion`). El motivo «El asistente no pudo responder» en la franja, y el de cada tope (270 y 278). Tests: cambiar, fijar, resolver, el filtro y cada motivo.
4. **F4 · El chat.** Suma a `MessageThread.tsx` «Por qué respondió esto», como la spec, los botones, el texto exacto, el borrador, la sugerencia con «Revisar y enviar», el audio con su transcripción y el tope (`WA-Chats-Casos`, `WA-Chats-Asistente`, `WA-Chats-Sugerencia`, `WA-Chats-Audio` y `M-WA-Conversacion-Asistente`). Tests: cada uno.
5. **F5 · Temas y campana.** `UnansweredTopicsPage.tsx` en `/org/whatsapp/preguntas-sin-respuesta` (`WA-Preguntas-Sin-Respuesta` y su teléfono) y `MissingFormsPage.tsx` en `/org/formularios/faltan` (`FRM-Faltan` y su teléfono); `assistantUnanswered` en `notifications` del `registration.ts` de WhatsApp, con su texto es/en (`Campana-23` y su teléfono). «Sin revisar» en el filtro y la columna, y «Marcar como revisado» primero en el ⋮ de la fila. La descripción en su columna, el orden y el filtro, como la spec (262 a 264). Tests: descartar; revisar y el 409; sin nombres; el filtro de estado; ordenar; el aviso.
6. **F6 · El editor.** «Cuándo lo ofrece el asistente» en Diseño (`FRM-Editor-Asistente`), la pestaña «Sugerencias» con su historial (`FRM-Editor-Sugerencias`) y «Armar desde un texto» (`FRM-Editor-Reglas-23` y su teléfono), sobre `FormEditorPage.tsx` y `components/rules/`. Sin la feature `assistant`, como la spec. En el modo plantilla de la E22 no aparece ninguno de los tres. En uno «Solo en la app», como la spec (258). Lo que se elige en las sugerencias, en la pieza (262). Tests: los tres, con y sin la feature, en uno de la app, con el tope del mes y el cupo del día agotados (271) y en modo plantilla.
7. **F7 · Los máximos de la plataforma.** Suma a `PlatformAssistantSettingsPage.tsx` (`Plat-Asistente-Config-23` y su teléfono). Tests: los tres máximos, el de ofrecimientos de 1 a 50 (278); 409.
8. **F8 · «Cómo empezó» y la cadena.** Suma a `QuickView.tsx` y `RecordFullPage.tsx` [E15] «La mandó: El asistente», los datos propuestos con «Corregido al completar» y la cadena (`FRM-Respuesta-Cadena`). Tests: los datos propuestos; la cadena.
9. **F9 · Dar de baja.** `LifecycleDialogs.tsx` y el editor dado de baja [E17] suman el asistente y las sugerencias, y conservan la línea de la E20 (`FRM-Dar-De-Baja-23` y `FRM-Editor-Baja-23`). Test: vuelven al reactivar.

**Texto para el chat del Front**

```text
Leé AGENTS.md del front, ../ArquitecturaBaseMutitenant/docs/plans/2026-11-08-etapa-23-asistente.md y la spec de la E23. Esperá K1, corré npm run contracts y hacé F1 a F9 contra los tipos generados, copiando los tableros del lienzo v175, con MSW y axe. No toques el lienzo, las specs, el back ni la tabla de estado del plan. Reglas: todo en main, sin ramas, worktrees, stash, reset ni push; commiteá solo lo tuyo con git commit -- <rutas>, en español y con conventional commits; con la app levantada, no edites el src; TDD donde hay lógica; al terminar cada tarea, npx tsc -b y npm test enteros, sin filtrar por módulo, y con rutas o permisos, sus tests del área; al cerrar la parte, build, lint y contracts:check; el contrato no cambia sin anotarlo en «Cambios al contrato» y avisarle a la Guía; ninguna clave real pasa por vos; al usuario se le pregunta solo lo de producto.
```

## Integración y puerta

1. **I1 · Integrar** contra la API real, con `FakeChatClient` y el canal de prueba.
2. **I2 · Puerta.** `/puerta`:
   - build sin advertencias y `dotnet test` con Docker; front build, lint, test y `contracts:check`;
   - los casos de evaluación con el modelo real, con la clave que ya cargó el usuario;
   - E2E real con «Los casos del asistente»: Lucía arma y prueba; Martina escribe hasta la ráfaga con tres acciones; una consulta sin la información deriva; un formulario sale con su oferta; con el tope de ofrecimientos en 1, el segundo deriva con su motivo; tres personas a la vez; un audio; una regla desde un texto;
   - la medición de texto y de audio a envío, en el resultado;
   - `realinear-docs --detectar` en 0 y `estado-docs --detectar`;
   - la lista de control (269), con los riesgos de la [prueba integral de ArquitecturaBase](../backlog.md#lo-que-vio-la-prueba-integral-de-arquitecturabase): una sugerencia hacia un formulario que publicó otra versión lo nombra y dice qué hacer (O20); una sugerencia armada antes de tomar la conversación y mandada después: el servidor la rechaza con un 409 claro, y la pantalla no la muestra como un resultado incierto (O32); un audio que el proveedor devuelve vacío contesta o deriva, y dice por qué (270): allá el chat decía «No pudimos pasar este audio a texto», a la persona no le llegaba nada y no se derivaba; y ante «quiero cargar el registro de lluvia y además decime a qué hora abren», el motor propone solo el formulario (154);
   - galería a 1440, 390 y 320 de los tableros de F2 a F9;
   - `/recorrido` con «Los casos del asistente».
3. **I3 · Cierre.** `docs/features/asistente.md`, `whatsapp.md` y `formularios.md` suman su sección; la regla de WhatsApp pasa a «solo texto redactado con estos límites, o fijo»; los `AGENTS.md` de las carpetas nuevas. Quedan dos pendientes: la prueba real con la IA y Meta, para la I1 de la E10 (123), y uno del usuario: publicar desde «Legales» la versión de los Términos y la Privacidad que nombra a OpenAI (ADR 0055).

| Puerta | Resultado |
| --- | --- |
| Back / Front / Contrato / Evaluación / E2E / Medición / Documentación / Galería / Recorrido | Pendiente |

**Texto para el chat de Integración**

```text
Leé AGENTS.md de los dos repos, este plan (docs/plans/2026-11-08-etapa-23-asistente.md) y la skill /puerta. Integrá el front con la API real, devolvé cada diferencia a su dueño y corré la puerta una sola vez sobre copias del corte; después de un arreglo, solo lo que toca. La clave de los casos de evaluación ya la cargó el usuario. No toques el lienzo. Lo de Meta queda pendiente para la I1 de la E10. Dejá solo la tabla corta del resultado en este plan. Reglas: todo en main, sin ramas, worktrees, stash, reset ni push; commiteá solo lo tuyo con git commit -- <rutas>, en español y con conventional commits; con la app levantada, nadie edita el src del front; TDD en cada arreglo con lógica; build y tests rápidos enteros, sin filtrar por módulo, y los de integración y seguridad del área que toque el arreglo; el contrato no cambia sin anotarlo en «Cambios al contrato»; ninguna clave real pasa por vos; al usuario se le pregunta solo lo de producto.
```

## Relación con otras etapas

**Usa de las anteriores:** lo que lista K0; de la E11, `WhatsAppChat`, `WhatsAppTeam`, `WhatsAppBot`, `WhatsAppConversationService` y la cola de salida; de la E8b, el módulo Assistant, el tope mensual y `FakeChatClient`.

**Deja para las siguientes:**
- **Datos:** `AssistantAgents` (`IsPrimary`), `AssistantKnowledge`, `WhatsAppInboxAttention`, `WhatsAppAssistantActions`, `WhatsAppAssistantCounters`, `WhatsAppUnansweredTopics`, `FormMissingTopics`, `FormSuggestionSets` y las columnas nuevas de K1.
- **Lo demás:** lo de K1 (en especial `assistant/agent` y `…/test`), `ConversationProposal`, los servicios de B3 a B5, el perfil `flow-static-7.3-r13` y las pantallas de F2, F4 y F5.

## Cambios al contrato


## Para decidir

Nada.
