# Asistente: contrato E8b

Freeze T01: 56 operaciones nuevas (45 chat, 11 gestión), 40 rutas. Los controllers llaman interfaces y servicios de Application; todos los servicios devuelven `Result` con `General.Contract.NotImplemented`, 501. La implementación de Partes 2 no forma parte de este freeze. No hay tablas, migraciones, llamadas al proveedor, adaptadores de archivos, workers ni herramientas ejecutables.

## Accesos y permisos

Consumer usa el asistente de manera implícita sin roles; Business requiere `assistant.use` y la gestión `assistant.configure`. Use está en todos los roles de sistema, incluido Administrador de empresa, y Configure sólo en Dueño. Use es de organización, no se amplía el catálogo de permisos de empresa. El reader actual de roles con `companyId=null` no incluye asignaciones por empresa: T02 debe verificar la efectividad de Administrador de empresa sin elevar otros permisos.

Platform usa permisos individuales `platform.assistant.use`, `.configure`, `.read`; los operadores existentes conservan sus grants. Un operador legado sin fila recupera únicamente `PlatformPermissions.LegacyBootstrap`; el primer operador nuevo recibe el catálogo completo. La gestión de Plataforma lleva gate de instalación y permisos y permite configurar sin clave. Chat Consumer/Platform está apagado en T01; la policy de Business devuelve null y conserva TenantFeatures. La feature aportada es Both/defaultfalse.

## Rutas y formas congeladas

`lado` corresponde a `me`, `organization` o `platform`. El OpenAPI generado es la fuente ejecutable de cuerpos, respuestas, ejemplos, errores por campo, permisos y modos. Toda entrada HTTP reside en `Api.Modules.Assistant.Contracts`; las salidas tipadas están en `Application.Modules.Assistant.Models` o son protocolos `*HttpResponse`.


| Método/ruta | Permiso extra al uso del lado | Petición | Éxito |
|---|---|---|---|
| GET `/api/{lado}/assistant` | uso del lado | — | 200 `AssistantState`: configured, canConfigure, models, quickQueries, dailyLimit, dailyResetAtUtc, remainingMessages (null si >10), monthlyBudget/usedCost (null sin presupuesto org), noticeVersion/noticeAccepted, retentionDays, maxAttachmentBytes=10485760, maxAttachments=5. Apagado→404, habilitado sin clave→configured=false. |
| POST `/api/{lado}/assistant/notice` | uso | `{noticeVersion}` + key | 204; Entendido por usuario/versión, tras aceptar documentos legales vigentes; no registra ese consentimiento por inferencia. |
| GET `/api/{lado}/assistant/conversations` | uso | `cursor?,limit?=20,saved?`, máximo 100 | 200 CursorResult<ConversationSummary>; orden LastActivityAtUtc/Id descendente, cursor ligado al lado/tenant/owner/filtro. |
| POST `/api/{lado}/assistant/conversations` | uso | `{}` + key | 201 `{id,title:null,isSaved:false,version,createdAtUtc,lastActivityAtUtc}` + Location. Sin llamada IA. |
| GET `/api/{lado}/assistant/conversations/{id}` | uso | `cursor?,limit?=50` | 200 `{conversation:summary,messages:CursorResult<Message>}` en orden cronológico; se revalida propiedad. |
| PUT `/api/{lado}/assistant/conversations/{id}` | uso | `{title:string|null,isSaved:bool,version}` | 200 VersionResponse; renombrar/Guardar/Dejar de guardar. Guardar no modifica owner/tenant. |
| DELETE `/api/{lado}/assistant/conversations/{id}` | uso | `?version=` | 204; elimina textos/adjuntos y cancela turno activo; limpieza física diferida fiable. |
| POST `/api/{lado}/assistant/conversations/{id}/messages` | uso | `{text:string,attachmentIds:uuid[],conversationVersion:uint}` + key | 200 text/event-stream primera vez; replay terminal 200 application/json `TurnResult`, sin invocar modelo ni streaming. En curso409; preflight falla antes de abrir stream. |
| POST `/api/{lado}/assistant/conversations/{id}/turns/{turnId}/stop` | uso | `{}` + key | 204; cancela idempotentemente; resultado durable Stopped. También RequestAborted pide stop. |
| POST `/api/{lado}/assistant/quick/{key}` | uso + permisos de herramienta | `{conversationId?:uuid,version?:uint}` + key | 200 `{conversation:summary,messages:[user,assistant],result:QuickResult}`; crea conversación si falta; sin proveedor/título IA/cuota/costo. |
| PUT `/api/{lado}/assistant/conversations/{id}/messages/{messageId}/feedback` | uso | `{rating:"up"|"down"|null,version}` | 200 VersionResponse; solo respuesta assistant propia, no prompt ni detalle libre. |
| POST `/api/{lado}/assistant/conversations/{id}/attachments` | uso | multipart **un archivo** `file`, `conversationVersion`; key | 201 `{id,fileName,mediaType,byteLength,version}`; privado, todavía no consumido por mensaje. No devuelve URL pública/key de storage. |
| GET `/api/{lado}/assistant/conversations/{id}/attachments/{attachmentId}` | uso | — | 200 binario, Content-Type verificado, Content-Disposition attachment, Cache-Control no-store. |
| DELETE `/api/{lado}/assistant/conversations/{id}/attachments/{attachmentId}` | uso | `?version=` | 204 si pendiente; si ya es de un mensaje, se elimina con la conversación. |
| GET `/api/{lado}/assistant/conversations/{id}/export` | uso | `format=markdown|pdf` | 200 descarga privada .md/.pdf, no-store; solo conversación propia, sin tool payloads, claves ni bytes originales de adjuntos. |
| GET `/api/organization/assistant/settings` | assistant.configure | — | 200 `OrganizationAssistantSettings` |
| PUT `/api/organization/assistant/settings` | assistant.configure | `{mode:"PlatformKey"|"OwnKey",modelId,effort:"low"|"medium"|"high",dailyMessageLimit?,monthlyBudget?:Money,keyAction:"keep"|"replace"|"clear",apiKey?:string,version}` | 200 VersionResponse. Topes editables solo own-key; platform-key rechaza intentos de cambiarlos. Validar allowOwnKey/model/key y al menos un Dueño activo con método Email verificado utilizable antes de guardar OwnKey; sin él, Assistant.Settings.AlertRecipientRequired. |
| GET `/api/organization/assistant/usage` | assistant.configure | `month:YYYY-MM,groupBy=day|person,page=1,pageSize=20` | 200 UsagePage, resumen mensual + página de filas/costo, sin texto ni conversationId. |
| GET `/api/platform/assistant/settings` | platform.assistant.configure; gestión | — | 200 PlatformAssistantSettings |
| PUT `/api/platform/assistant/settings` | platform.assistant.configure; gestión | `{defaultModelId,allowedModelIds:uuid[],effort,dailyMessageLimit,monthlyOrganizationBudget:Money,personalEnabled,retentionDays,keyAction,apiKey?,version}` | 200 VersionResponse; personal default false, día 50, retención de 30 días; clave write-only. |
| GET `/api/platform/assistant/usage` | platform.assistant.read; gestión | `month:YYYY-MM,page=1,pageSize=20` | 200 UsagePage por organización + Personas/Operadores; costo platform-key separado de own-key. Sin abrir tenant privado ni historial. |
| GET `/api/platform/assistant/models` | platform.assistant.configure; gestión | `page,pageSize,search?,active?` | 200 PagedResult<ModelPrice> |
| POST `/api/platform/assistant/models` | mismo; gestión | `{name,provider:"Anthropic",providerModelId,inputPricePerMillion:Money,outputPricePerMillion:Money,isActive}` + key | 201 `{id,version}` |
| PUT `/api/platform/assistant/models/{id}` | mismo; gestión | campos de alta + `version` | 200 VersionResponse; unicidad provider/ID y revalidación de opciones elegidas. |
| GET `/api/platform/tenants/{tenantId}/assistant/policy` | platform.tenants.read; gestión | — | 200 `{allowOwnKey:bool,version}`; false/version0 si no existe. |
| PUT `/api/platform/tenants/{tenantId}/assistant/policy` | platform.tenants.manage; gestión | `{allowOwnKey,version,reason}` | 200 VersionResponse + SecurityEvent, sin leer/devolver clave. Cambio de política revalida configuración/turnos. |
| GET `/api/platform/tenants/{tenantId}/features` y PUT `/api/platform/tenants/{tenantId}/features/{key}` | permisos vigentes | contrato **existente** de Módulos, source/expiry/version/reason; key=assistant | Existente; Asistente es contribución al catálogo. No reemplazar rutas, cuerpo ni trial de WhatsApp/futuros módulos. |

No ruta nueva para editar ayuda (D25), no endpoint público de adjuntos, no herramientas de escritura, no borrado de modelos referidos: desactivar es el mecanismo dibujado. Los endpoints existentes legales y de `/api/me` mantienen sus cuerpos y solo capacidades aditivas neutrales.

### Esquemas de respuesta y SSE

`ConversationSummary={id,title:null|string,isSaved,version,createdAtUtc,lastActivityAtUtc}`. `Message={id,role:"user"|"assistant",text,status:"Complete"|"Stopped"|"Failed"|"Generating",createdAtUtc,version,attachments:[AttachmentSummary],links:[AssistantLink],rating:null|"up"|"down"}`; el historial propio puede incluir tokens/modelId, nunca mensajes de sistema ni resultados crudos de herramientas. `AssistantLink={routeKey,parameters,query,labelKey}` solo de la lista permitida de pantallas; front arma href desde manifiesto y exige permiso. No aceptar URL o HTML que invente el modelo.

`ModelPrice={id,name,provider,providerModelId,inputPricePerMillion:Money,outputPricePerMillion:Money,isActive,version}`. Settings devuelven mode/modelId/effort/topes/allowOwnKey/hasAlertRecipient (solo settings org)/retention/version y **solo** `hasKey`/`maskedKeySuffix` (últimos 4), jamás apiKey/encryptedKey. State.models tiene solo opciones activas elegibles. `UsagePage={month,cost:Money,platformKeyCost?:Money,budget?:Money,totalQueries,peopleCount,rows:PagedResult<UsageRow>}`; UsageRow organización/persona/día contiene claves/nombre mostrado, consultas/tokens/costo/topes, ningún título ni archivo.

`QuickResult={text,links:[AssistantLink],data:proyección tipada de la herramienta}`; no estructuras arbitrarias de Domain ni ValueObjects sin mapeo. `TurnResult={turnId,message:Message,conversationVersion,usage:{inputTokens,outputTokens,cacheWriteTokens,cacheReadTokens,cost:Money,remainingMessages,monthlyUsedCost?:Money}}`. Usar esquemas para cada QuickResult en OpenAPI, no object libre que obligue any en F.

SSE usa UTF-8, eventos nombrados, datos JSON tipados y `id=<turnId>:<sequence>`; `Cache-Control:no-store`, flush por fragmento, sin buffering de proxy/Vite. No EventSource anónimo: POST con httpClient/stream transport y bearer en memoria. Heartbeat comentario cada 15 s; termina con done o error, y cierra.

| Evento | Datos | Conducta |
|---|---|---|
| `start` | `{turnId,userMessageId,assistantMessageId,conversationVersion}` | IDs persistidos antes del primer delta. |
| `delta` | `{turnId,sequence,text}` | Texto acumulativo por fragmento, solo respuesta, nunca argumentos de herramientas. |
| `tool` | `{turnId,toolKey,state:"running"|"complete"|"failed",labelKey}` | Estado localizado tipo Consultando usuarios; sin resultados/PII/argumentos. |
| `title` | `{conversationId,title,version}` | Título auto si no renombró el usuario; si llega después del stream, query de historial lo recupera. |
| `done` | `TurnResult` | Resultado durable Complete o Stopped, cuota/costo definitivo. |
| `error` | `{turnId?,code,traceId,retryAfter?:int,status:"Failed"}` | Tras abrir HTTP 200, nunca cambiar status HTTP ni incrustar ProblemDetails textual; guardar Failed con parcial/uso conocido. |

RequestAborted no deshace mensaje ya admitido: solicita stop y finaliza con token interno acotado. Reabrir conversación lee Stopped/parcial desde BD. Crash de worker recupera lease y marca Failed si una llamada pudo facturarse; no vuelve a facturar automáticamente. Reintentar en UI es **un turno nuevo** con key nueva; redelivery/red es misma key y mismo cuerpo. No prometer exactamente una llamada externa si el proveedor aceptó y cayó el proceso; reservar costo y registrar esa incertidumbre.

### Errores y textos

ProblemDetails pre-stream con code/traceId y errors de campos; códigos post-stream en `error`. Publicarlos en OpenAPI/resources es/en, sin textos del proveedor ni prompt/key/archivo en detail. 401/403 de permisos, acceso y legales, validación común, Idempotency-Key requerido/reutilizado (422)/en curso 409 y `General.ConcurrencyConflict`409 conservan catálogo vigente.

| HTTP / código | Campo/uso | Español / inglés |
|---|---|---|
| 404 `Http.NotFound` | módulo apagado/retirado | Texto común vigente; `Assistant.Module.Disabled` solo diagnóstico interno, sin revelar módulo en HTTP. |
| 404 `Assistant.Conversation.NotFound` | id/ajena | «No encontramos esa conversación.» / “Conversation not found.” |
| 404 `Assistant.Attachment.NotFound` | archivo ajeno/inexistente | «No encontramos ese adjunto.» / “Attachment not found.” |
| 400 `Assistant.Settings.NotConfigured` | chat sin clave válida | «El asistente todavía no está configurado.» / “The assistant is not configured yet.” |
| 400 `Assistant.Settings.KeyRejected` | apiKey | «La clave no es válida.» / “The key is invalid.” |
| 403 `Assistant.Settings.OwnKeyNotAllowed` | mode | «La plataforma no habilitó tu propia clave.» / “The platform has not enabled your own key.” |
| 400 `Assistant.Settings.AlertRecipientRequired` | mode | «Para usar la clave de tu organización, algún Dueño tiene que tener un correo verificado.» / “To use your organization's key, an owner must have a verified email.” UI usa nombre de organización como v115 y acción «Agregar un correo» / “Add an email” hacia Mi cuenta; no se guarda OwnKey. |
| 400 `Assistant.Model.NotAllowed` | modelId | «Elegí un modelo habilitado.» / “Choose an enabled model.” |
| 409 `Assistant.Model.AlreadyExists` | providerModelId | «Ese modelo ya está en el catálogo.» / “That model is already in the catalog.” |
| 404 `Assistant.Model.NotFound` | id | «No encontramos ese modelo.» / “Model not found.” |
| 400 `Assistant.Settings.PlatformLimitsReadOnly` | topes platform-key | «Estos topes los fija la plataforma.» / “These limits are set by the platform.” |
| 400 `Assistant.Notice.AcceptanceRequired` | noticeVersion | «Leé el aviso antes de tu primera consulta.» / “Read the notice before your first query.” |
| 429 `Assistant.Limit.DailyReached` | Retry-After hasta 00:00Z siguiente; dailyLimit y dailyResetAtUtc en ProblemDetails | «Llegaste a las {limit} consultas de hoy. Se renuevan a las {hora}.» / “You reached today's limit of {limit} queries. They reset at {time}.” Hora local con DisplayFormatter; v115 muestra 21:00 en Buenos Aires, no literal fijo. |
| 429 `Assistant.Limit.MonthlyReached` | Retry-After hasta próximo mes | «Llegaste al tope del mes.» / “You reached the monthly limit.” |
| 409 `Assistant.Turn.InProgress` | conversación | «Esperá o detené la respuesta actual.» / “Wait for or stop the current response.” |
| 404 `Assistant.Turn.NotFound` | turnId/ajeno | «No encontramos esa respuesta.» / “Response not found.” |
| 400 `Assistant.Attachment.UnsupportedType` | file | «Adjuntá una imagen o un PDF.» / “Attach an image or a PDF.” |
| 413 `Assistant.Attachment.TooLarge` | file | «El archivo pesa más de 10 MB.» / “The file is larger than 10 MB.” |
| 400 `Assistant.Attachment.TooMany` | attachmentIds | «Podés adjuntar hasta 5 archivos por mensaje.» / “You can attach up to 5 files per message.” |
| 409 `Assistant.Attachment.AlreadyUsed` | attachmentIds/borrar | «Ese adjunto ya es de un mensaje.» / “That attachment already belongs to a message.” |
| 400 `Assistant.QuickQuery.NotAvailable` | key o permisos cambiados | «Esa consulta no está disponible.» / “That query is unavailable.” |
| 503 `Assistant.Provider.Unavailable` | reintentable, Retry-After | «El asistente no responde ahora.» / “The assistant is unavailable now.” |
| 400 `Assistant.Provider.ContextTooLarge` | message | «La conversación o los adjuntos son demasiado grandes. Empezá una nueva.» / “The conversation or attachments are too large. Start a new one.” |
| 500 `Assistant.Export.Failed` | descarga | «No pudimos exportar la conversación.» / “We could not export the conversation.” |
## Transporte SSE

La primera admisión de messages responde 200 `text/event-stream`; un replay terminal responde 200 `application/json` con `AssistantTurnResultResponse`. El stream se abre sólo después del preflight. Los eventos son `start`, `delta`, `tool`, `title`, `done`, `error`. OpenAPI define seis sobres cerrados `{event,data}` con `oneOf` y discriminator `event`, y `x-sse-events` referencia los payloads y ejemplos.

En el wire SSE, `event:` identifica el evento y `data:` contiene sólo el JSON del payload, sin un campo event adicional. UTF-8, `id: turnId:sequence`, comentarios heartbeat cada 15 segundos, `Cache-Control: no-store` y flush en cada frame. Es un POST con bearer autenticado; no EventSource anónimo. `done` contiene el TurnResult completo; `error` conserva code/traceId/retryAfter y failed. RequestAborted solicita stop y no deshace una admisión durable.

## Proyecciones, links y secretos

Quick devuelve un resultado tipado: nueve variantes concretas de data, cada una con una propiedad requerida distinta y `additionalProperties:false`; `oneOf` no añade un discriminator `$type` al wire. No se permite `object`, any ni JsonElement libre. Las consultas revalidan permisos de herramientas. Los links tienen una whitelist enum de rutas internas y mapas tipados de parámetros/query, sin URL arbitraria. Account sólo expone métodos enmascarados y proyecciones seguras de ayuda; Interactions usa el DTO paginado neutral.

`apiKey` es write-only y se admite sólo con keyAction replace; keep/clear lo rechazan. Las respuestas sólo publican hasKey/maskedKeySuffix. Los requests con secretos, texto, títulos o archivos tienen ToString redactado. No se registran claves, cipher, texto, nombres o bytes de archivos. Los precios y presupuestos usan Money no negativo, con la moneda de facturación fija leída desde AssistantContractPolicy/AssistantContractData; T03 valida además el catálogo factual.

## Puertos y decisiones del freeze

- Actor: `Application/Models/Request/ExecutionActor` y `Interfaces/Integrations/Request/IExecutionActorContext`, `IExecutionActorScope`. OwnerUserId/Access/TenantId/Culture/TimeZoneId se capturan desde contexto firmado; scope es IDisposable.
- Archivos: `IFileStore.PutAsync(FileObject, Stream, CancellationToken) -> Task<StoredFile>`; `OpenReadAsync(storageKey, ct) -> Task<Stream?>`; `DeleteAsync(storageKey, ct)`. Ningún adaptador en T01.
- Interacciones: `IConsumerInteractionSource.ListAsync(ListConsumerInteractionsRequest, ct)` devuelve `Result<PagedResult<ConsumerInteractionResponse>>`, una proyección segura por fuente; ninguna fuente registrada en T01.
- Replay: protocolos genéricos en Application, no Api, para evitar dependencia HTTP en resolvers privados. `IIdempotentReplayResolver.Kind/ResolveAsync(IdempotentReference, ct)` devuelve Result de IdempotentReplayResponse, con variante genérica tipada. T05 debe usar estos punteros en su implementación.
- IA: `IChatClientFactory` vive sólo en el módulo Application y devuelve Result<IChatClient>; el núcleo no importa tipos AI.
- Concurrencia: las versiones son uint/xmin; quick declara ResponseVersion(Conversation.Version), validada por la guarda neutral. No versiones manuales ni relectura después del commit.
- Error 413: ErrorType.PayloadTooLarge y ValidationError con clasificación explícita conservan errors.file. Todos los códigos del módulo se traducen por IErrorTextSource; 429/503 con retryAfter también publican Retry-After.

## Pendiente para Partes 2

T02–T09 implementan persistencia, configuración factual y disponibilidad, permisos efectivos de empresa, reservas y replay privados, notice, cuotas y alertas, turnos/tools/provider, cancelación y recuperación, archivos/export/limpieza y sus pruebas de conducta. Los validadores de T01 publican reglas de entrada y se prueban dirigidamente; los stubs no simulan esa conducta ni datos de producto.

T01 publica `AssistantMoney` como schema local cerrado `{amount:decimal,currency:ISO}` para todas las entradas/salidas monetarias, incluidas nullable; conserva `Money` y su converter de Domain y el schema E8 sin modificaciones. Las queries wire se publican camelCase con mes civil ASCII `YYYY-MM` (0001 a 9999), month/version requeridos, defaults de paginado y enums de formato/agrupación. Los ejemplos terminales representan `message.role=assistant`, estado Complete y valores nullable/enums válidos.
