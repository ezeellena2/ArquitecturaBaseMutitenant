# E21 · Reglas, aprobación, PDF y avisos · plan ejecutable

**Estado:** pendiente · Spec: `docs/plans/formularios/21-reglas-y-aprobacion.md`, con sus partes de datos y contrato, corregir y anular, y pasos y qué verificar. Tableros aprobados: lienzo v175, salvo lo que la tabla de la spec da como «Se redibujan» («Sin confirmar» con su revisión, una opción que ya no está, la ubicación en un aviso y «Dónde se completa», y lo de los traspasos 17 y 18: 261 a 264, 266, 267, 275 y 276), que se aprueba antes de F2.

| Parte | Tareas | Estado |
| --- | --- | --- |
| Contrato | K0–K1 | Pendiente |
| Back | B1–B17 | Pendiente |
| Front | F1–F9 | Pendiente |
| Integración y puerta | I1–I4 | Pendiente |

## Forma de trabajo

Modo rápido: el contrato primero; Back y Front en paralelo; una sola puerta al final. Un arreglo corre solo lo que toca. Las reglas de `AGENTS.md` van enteras en el texto de cada chat.

## Qué se verificó al arrancar

| Fila de la spec | Qué se encontró | Qué cambia |
| --- | --- | --- |
| [Meta] | la documentación confirma el encabezado con documento y su alta, no los saltos de línea | «Meta»; la prueba, en K0 |
| [E14], [E15], [E19], [E20] y la base | están sus planes, no el código; `sameOperation` ya está en `useIdempotentMutation.ts` | K0 |
| Allá | `origin/main` en `be88b2c9`: renglones probados con Meta el 9/11 (T12) y el motor del 15 al 17/11 (T14) | B3, B4, B6 y B8 |
| Núcleo: la cola | lo de la ADR 0066 llega con la base 2 (214) | K0 y B9 |
| Núcleo: correos | `EmailMessage(To, Subject, HtmlBody, TextBody)` y `MimeMessageFactory`, sin adjuntos | B11 |
| Núcleo: roles | existe `IRoleReader.ListOrganizationRoleIdsAsync`; idioma y zona de la cuenta viven en `ApplicationUser`, que un módulo no lee | B3, B5 y B8 |
| Núcleo: «Mi cuenta» | `ModuleRegistration` no tiene lugar; `WhatsAppConversationLinkWriter` vincula solo chats de «Clientes», y nada dice si hay uno sin vincular | F8 y B17 |
| Página pública | `IPublicPageReader.FindCurrentAsync()` da nombre, logo (PNG, JPEG o WebP) y contacto; `ITenantSettingsReader`, idioma y zona | B8 |
| Plantillas | `WhatsAppTemplateClient` manda `BODY` y `BUTTONS`, posicional; `WA-Plantilla-Nueva` no tiene encabezado | B10 y F9 (201) |

## Meta

Lo que dice la documentación oficial está en la [parte de pasos](formularios/21-parte-pasos-y-verificar.md#lo-que-dice-meta-2026-11-08).

**Así sale cerrado:** el PDF se sube a `/media` al despachar y viaja por `id`, sin `filename` en la plantilla; cada variable va en un renglón, sin más de tres espacios seguidos, y el texto armado no pasa de 1024; 132018 es una falla definitiva.

## Contrato

### K0 · Lo que dejó la etapa anterior y la prueba con Meta

**El chat del contrato confirma esta lista cerrada en el código.**
- **Base:** `INotificationSource`, `NotificationItem`, `notifications` (`{kind, describe}`) en `registration.ts`, `ConfirmDialog` con `pending` y el foco en «Cancelar», `PageLoading`.
- **Base 2:** `OutboxStatus.Uncertain`, la prioridad (H31) y la revisión por el id del envío, con los códigos de falla de la ADR 0066 §3; el conversor del chat cifrado (0065); `DisplayFormatter` con las fechas de la 205, y el tope diario de códigos por cuenta (H10); la pieza `Picker` (262) y el orden por columnas (264).
- **E12:** `AddFormsModule`, `FormsContractContributor`, `FormsTexts`, la feature `forms`, `IMemberDirectoryReader` y el catálogo «De los contactos».
- **E14:** `FormDefinitionState` y su bloqueo (T7); `FormRecord` (`SourceKey`, `Channel`, `Respondent*`, `SubmittedAtUtc`); `IFormRecordService.CaptureAsync` con una UoW; `FormRecordWriter`; `IFormReader`; `FormDefinitionService`; `IStructuredRecordSource`, `StructuredRecordCard`, `StructuredRecipient`; `WhatsAppFlowBuilder`; `FormsAccountDeletionParticipant`; `platform.FormPublicationWorks`.
- **E15:** `FormRecordCondition`, `FormRecordQueryFilters`, `FormRecordColumnsBuilder`, `FormRecordReader`, `FormReader` (`recordCount`, `lastRecord`), `FormRecordSheetBuilder`, `FormDashboardBuilder`, `BusinessFormRecordsController`, `IStructuredRecordOriginSource`; `FormRecordsPage`, `QuickView`, `RecordFullPage`, `ExportDialog`, `FormDashboard`, la forma de solo lectura (`RecordForm.tsx` y `lib/recordForm.ts`, 266) y `RecordActivity.tsx` (267).
- **E16 a E18:** `FormRecipientFilterPolicy`; `FormDefinitionCopyCreator` y `LifecycleDialogs.tsx`; `FormRecordFile`, `files[{id, fieldId, name, kind, mediaType, size, hasThumbnail}]`, `ListReferencedStorageKeysAsync`, SkiaSharp, `WhatsAppReplyIssuer.IssueFileAsync` e `IWhatsAppMediaClient.UploadAsync`.
- **E19 y E20:** `FormConditionEvaluator`, `FormLogicPolicy`, `FormRecordRowValues` con `rowsOf`, `HiddenFieldIds`, `ConditionsEditor.tsx`, `lib/conditions.ts`, `RowsBlockEditor.tsx` y el perfil `flow-static-7.3-r11`; `FormEntrySubmitService`, `FormEntryDurableAdapter`, `BusinessFormEntryController`, `own-records`, `GET /entry`, `Forms.Entry.*`, el modo para cargar de `RecordForm.tsx`, `EntryPanel`, `EntryRowsCards`, el tipo `location` y «App · No se carga más» en `LifecycleDialogs.tsx`.

**La prueba con Meta, del usuario antes de programar la 21,** con la cuenta de desarrollo de la 11, que necesita su medio de pago (sin él, Meta responde 131042). La hace él; ninguna clave pasa por un agente:
1. En business.facebook.com, **WhatsApp Manager › Administrar plantillas › Crear plantilla**: «Utilidad», nombre `prueba_documento_e21`, idioma «Español (ARG)», encabezado de tipo «Documento» con un PDF cualquiera y el cuerpo `Hola {{1}}, te mandamos el comprobante {{2}}.`, con los ejemplos «Ana» y «VIS-000001». «Enviar», y esperá «Activa».
2. En developers.facebook.com/tools/explorer, elegí la app «Plantilla Multitenant», **Get User Access Token** con `whatsapp_business_messaging`, y **Generate Access Token**.
3. `POST <id del número>/messages` (el id está en **App Dashboard › WhatsApp › Configuración de la API**), con el cuerpo `{"messaging_product":"whatsapp","to":"<tu número>","type":"template","template":{"name":"prueba_documento_e21","language":{"code":"es_AR"},"components":[{"type":"header","parameters":[{"type":"document","document":{"link":"https://www.w3.org/WAI/ER/tests/xhtml/testfiles/resources/pdf/dummy.pdf"}}]},{"type":"body","parameters":[{"type":"text","text":"Ana"},{"type":"text","text":"VIS-000001"}]}]}}`. **Submit:** el PDF llega a tu WhatsApp.
4. Repetí con `"text":"Ana\nGómez"` y anotá el código de error.

Le avisás a la Guía si se aprobó, si llegó el PDF y el código del paso 4.

### K1 · Publicar el contrato

El chat del contrato publica las rutas con 501, los permisos, los errores con sus textos y los ejemplos exportados de cada respuesta, también los 400, los 409 y los vacíos.

**Rutas.** Son de `/api/organization/forms`, con `[Access(Access.Business)]` y `[FeatureGate("forms")]`, salvo las de `/api/me`, y cuentan en las cuotas generales. Lo que la spec ya trae se cita.

| Ruta | Permiso | Pide → devuelve | Errores |
| --- | --- | --- | --- |
| `GET /{id}/rules` | `formrules.manage` | como la spec | `Forms.Definition.NotFound` |
| `PUT /{id}/rules` | `formrules.manage`, y `formrecords.read` como dice la parte | `{revision, rules[]}` → `{revision}` | `Forms.Rules.Invalid`, `TooMany`, `PlaceholderInvalid`, `OptionUnavailable`; 403 `RecordsReadRequired`; 409 |
| `GET /{id}/rules/revisions` y `…/{revision}` | `formrules.manage` | `page`, `pageSize` → `PagedResult<{revision, savedAtUtc, savedBy}>`; una, con `rules[]` y `questions[]` | `Forms.Rules.RevisionNotFound` |
| `GET /{id}/rules/options/{roles\|members\|templates}` | `formrules.manage` | `page`, `pageSize`, `search` → `{id, name}` de los roles de organización; `{userId, name}` de los miembros activos; `{id, name, language, documentHeader, variables[{label}]}` de las aprobadas | — |
| `GET` y `PUT /{id}/pdf-design` | `formrules.manage` | → `{design, revision, organization{name, logoDataUri?, contact?}}`; `{design, revision}` → `{revision}` | `Forms.PdfDesign.Invalid`; 409 |
| `POST /{id}/pdf-design/preview` | `formrules.manage` | `{design}` → PDF de ejemplo en el idioma del pedido, `no-store`; no guarda | `Forms.PdfDesign.Invalid` |
| `GET /pending` | `formapprovals.decide` | como la spec | — |
| `GET /{id}/approvals`, `…/{taskId}` y `…/continuation` | `formapprovals.decide` | como la spec; `direction` es `next` o `previous`, y `limit`, hasta 10 | `Forms.Approval.NotFound` |
| `POST /{id}/records/{recordId}/decision` | `formapprovals.decide` | `{decision: Approve\|Reject, reason?, version}`, `[Idempotent]` → `{version, state}`, `[ResponseVersion("Version")]` | `Forms.Approval.NotFound`, `AlreadyDecided`, `OwnRecord`, `RoleRequired`, `ReasonRequired`, `Forms.Record.ReasonTooLong`; 409 |
| `GET /{id}/records/{recordId}/workflow` | `formrecords.read` | abajo | `Forms.Record.NotFound` |
| `POST …/executions/{executionId}/recipients/{recipientId}/retry` y `…/executions/{executionId}/retry` | `formrules.manage` | `{version}`, `[Idempotent]` → `{version}` | `Forms.Execution.NotRetryable`; 409 |
| `GET /{id}/records/{recordId}/pdf` y `…/history` | `formrecords.read` | como la spec; el PDF en línea, con `nosniff`, y `documentId?` | `Forms.Record.PdfNotFound`, `NotFound` |
| `GET /{id}/records/{recordId}/correction` | `formrecords.correct` | la carga de la E20 con lo cargado, los ítems, `files[]` con la forma de la E18 y `version` | `Forms.Record.NotCorrectable`, `Superseded`, `AlreadyVoided` |
| `POST /{id}/records/{recordId}/corrections` | `formrecords.correct` | como la spec, con 101 MiB; `keepFileIds[]` son esos `id` | los de la carga, menos `Forms.Entry.Deactivated` y `NotForEmployees` (93); esos tres, `ReasonRequired`, `ReasonTooLong`; 409 |
| `POST /{id}/records/{recordId}/void` | `formrecords.correct` | `{reason, version}` → `{version}` | los mismos, sin los de la carga |
| `…/own-records/{recordId}/pdf`, `history`, `correction`, `corrections` y `void` | `formrecords.create` | lo mismo, con lo propio | lo ajeno, `Forms.Record.NotFound` |
| `GET /api/me/whatsapp/chats` | `[Access(Consumer)]`, `[FeatureGate("whatsapp")]` | `page`, `pageSize` → la fila de la spec, más `linkId?` y `displayNumber?` | — |
| `POST /api/me/whatsapp/chats/{linkId}/code` y `…/link` | lo mismo | `[Idempotent]`; como `…/login-methods/{id}/code` (202 `{resendAfterSeconds}`) y `/verify` (`{code}`), y el segundo devuelve la fila; el código cuenta en el tope diario por cuenta (H10) | `WhatsApp.ChatLink.NotFound` y los de esas rutas, con el 429 y `retryAfter` del tope |

**Concurrencia:** el 409 es `General.ConcurrencyConflict`. Las reglas y el diseño no son `IVersioned`: cada guardado inserta una revisión, la que vio la pantalla se compara bajo el bloqueo de la fila de estado (T7), y guardar sin cambios devuelve la misma.

**`workflow`:** `approval?{taskId, state: Open|Approved|Rejected|Closed, roleName, requestedAtUtc, canDecide, reasonRequired, decidedBy?{name, atUtc}, reason?}`, con `reason` `null` en un rechazo sin motivo (276); cada «Armar el PDF» confirmado suma `documentId`, y un aviso a un rol, `roleName`, el de hoy (267); `activity[{moment: Received|Approval|Approved|Rejected|Corrected|Voided, atUtc, kind, actor?, state: NotApplicable|Pending|InProgress|Done|Failed|Uncertain, failureCode?, canRetry, version, lastRequest?{kind: Retry|RetryRecipient, by, atUtc}, recipients[{id, executionId, kind: Member|Contact|Address, name?, address?, state, failureCode?, review?{by, atUtc}, canRetry, version}]}]`; `pdf?{createdAtUtc}`. Un destinatario suma el estado `Unconfirmed` («Sin confirmar», 240). `canRetry` va con `formrules.manage`: en un destinatario, con `WindowClosed`, `TemplateUnavailable`, `NoConversation`, `SendFailed` o «Sin confirmar» ya revisado; en una acción, con `RoleWithoutMembers` y alguien en el rol, o `Uncertain`.

**Rutas que cambian:** las de la spec, también `unresolvedActions`, `hasUnresolvedActions` y `unresolvedActionCount` (228); en la lista, `status=pendingApproval` y `sort=pending` (264 y 275), y en la planilla, `sort` por `state`, `corrected` y `unresolvedActions` (264). Además, `recordCount`, `lastRecord`, `ownCount` y `lastOwn` toman solo las vigentes sin anular, y de un número corregido, la última corrección; la tarjeta `form` de los mensajes del chat suma `after?{kind: Corrected|Voided, displayNumber}`.

**Puertos:** `StructuredActionMessage(Key, Recipient, Text, Template?(TemplateId, Variables[]), Document?(StorageKey, FileName))`, `StructuredActionReach(HasChat, WindowOpenUntilUtc?)` y `StructuredActionState(Key, State: Pending|Sent|Failed|Unconfirmed, FailureCode?, ReviewedBy?, ReviewedAtUtc?)`.

**Campana**, como `formPublicationFailed` (E14): `formApprovals`, a quien puede decidir, con `{formName, count}`, `link` `/org/formularios/{id}/respuestas?vista=por-aprobar&respuesta={la más vieja}` e `id` `formApprovals:{definitionId}:{count}:{tarea más nueva}`; `ruleNoticeFailed`, a quien tiene `formrules.manage`, con `{displayNumber}`, `link` `…/respuestas/{recordId}/completa` e `id` `ruleNoticeFailed:{recipientId}:{intento}`. Sus textos los arma el front (F4).

**Permisos:** los tres de la spec, con los textos de la §2 de la hoja de ruta y sus áreas en los permisos de un rol (260): `Area.formrules`, «Reglas y PDF» (*Rules and PDF*), y `Area.formapprovals`, «Aprobaciones» (*Approvals*) (257); `formrecords.correct` incluye `formrecords.read`; `<fecha>_SystemRoleFormRulesPermissions.cs` se los da al Dueño (`TenantAdmin`).

**Textos** (`FormsTexts` y los del módulo WhatsApp, es / en):
- `Forms.Rules.Invalid`, Validation (`rules[i]…`): «Revisá esta parte de la regla.» / "Check this part of the rule."
- `Forms.Rules.TooMany`, Validation: «Hasta 20 reglas, con 10 condiciones y 10 acciones cada una.» / "Up to 20 rules, each with 10 conditions and 10 actions."
- `Forms.Rules.PlaceholderInvalid`, Validation: «Pregunta no disponible: quitala del mensaje.» / "Question not available: remove it from the message."
- `Forms.Rules.OptionUnavailable`, Validation: «La opción «{0}» ya no está: elegí otra.» / "The option “{0}” is gone: choose another."
- `Forms.Rules.RecordsReadRequired`, Forbidden: «No se guardaron las reglas: un aviso que cita preguntas o adjunta el PDF pide también «Ver respuestas».» / "The rules weren't saved: a notice that quotes questions or attaches the PDF also needs “View responses”."
- `Forms.Rules.RevisionNotFound`, NotFound: «No encontramos esa revisión.» / "We couldn't find that revision."
- `Forms.PdfDesign.Invalid`, Validation: «Revisá esta parte del diseño.» / "Check this part of the design."
- `Forms.Approval.NotFound`, NotFound: «No encontramos esa aprobación.» / "We couldn't find that approval."
- `Forms.Approval.AlreadyDecided`, Conflict: «Otra persona ya la decidió.» / "Someone else already decided it."
- `Forms.Approval.OwnRecord`, Forbidden: «No podés decidir una respuesta que cargaste o corregiste.» / "You can't decide a response you submitted or corrected."
- `Forms.Approval.RoleRequired`, Forbidden: «No tenés el rol que la decide.» / "You don't have the role that decides it."
- `Forms.Approval.ReasonRequired`, Validation (`reason`): «Escribí el motivo del rechazo.» / "Write the reason for the rejection."
- `Forms.Record.NotCorrectable`, Conflict: «Esta respuesta ya no se puede corregir.» / "This response can no longer be corrected."
- `Forms.Record.Superseded`, Conflict: «Esta versión ya se reemplazó.» / "This version was already replaced."
- `Forms.Record.AlreadyVoided`, Conflict: «Esta respuesta ya está anulada.» / "This response is already void."
- `Forms.Record.ReasonRequired`, Validation (`reason`): «Escribí el motivo.» / "Write the reason."
- `Forms.Record.ReasonTooLong`, Validation (`reason`): «Hasta 500 caracteres.» / "Up to 500 characters."
- `Forms.Record.PdfNotFound`, NotFound: «Esta respuesta no tiene PDF.» / "This response has no PDF."
- `Forms.Execution.NotRetryable`, Conflict: «Este aviso no se puede reintentar.» / "This notice can't be retried."
- `WhatsApp.ChatLink.NotFound`, NotFound: «No encontramos ese chat.» / "We couldn't find that chat."
- **Fallas:** sus textos están en la [parte de datos y contrato](formularios/21-parte-datos-y-contrato.md#contrato), más `SendFailed` «WhatsApp no lo pudo mandar.» / "WhatsApp couldn't send it.".

**Texto para el chat del Contrato**

```text
Leé AGENTS.md de los dos repos, docs/plans/2026-11-08-etapa-21-reglas-y-aprobacion.md y la spec de la E21 con sus partes. Sin la prueba con Meta de K0, no sigas. Hacé K0 (confirmá la lista en el código) y K1: publicá exactamente la sección «Contrato» con 501, generá openapi.json y corré npm run contracts en el front. Todo en main, sin ramas, worktrees, stash, reset ni push; commiteá solo lo tuyo con git commit -- <rutas>, en español y con conventional commits. Con la app levantada, nadie edita el src del front. TDD donde hay lógica. Al terminar: build y tests rápidos enteros, sin Docker ni filtros, e integración y seguridad de las rutas. El contrato no cambia sin anotarlo en «Cambios al contrato». Ninguna clave real pasa por vos. Al usuario preguntale solo lo de producto.
```

## Back

Rigen Result, `OperationLog.RunAsync`, `[LoggerMessage]` sin direcciones, teléfonos ni textos, `TimeProvider`, una UoW por método que escribe, RLS, auditoría y `DisplayFormatter` con cultura y zona. Forms y WhatsApp se hablan solo por los puertos. Los tests son los de cada paso de la spec, más lo que se nombra; cada «no repetir» lleva el suyo.

1. **B1 · Reglas.** `D/Forms/FormRuleSet.cs`, `A/Modules/Forms/Services/FormRuleService.cs`, `FormRulesPolicy.cs`; copiá `FormDefinitionService.cs` [E14]. Pedir aprobación va última y solo al recibir; con aprobación, «Armar el PDF» va solo al aprobar; adjuntar pide un «Armar el PDF» antes, y una plantilla con documento. Guardar toma el bloqueo de las definiciones antes que el de un catálogo y comprueba las opciones (F16 de allá). «Pedir aprobación» suma `submitterMayDecide` y `rejectReasonRequired`, omitidas con su valor por defecto (276). Más: guardar sin cambios.
2. **B2 · Permisos, tablas y migraciones.** `Permissions.cs` y sus resx; `I/Modules/Forms/Persistence/Configurations/`; `<fecha>_FormRules.cs`, `<fecha>_FormRecordCorrections.cs` y la de roles; copiá `20261010203618_SystemRoleAssistantPermissions.cs`.
3. **B3 · El motor.** `IFormActionService.cs`, `FormActionService.cs`, `FormActionPlanBuilder.cs`; suma a `FormRecordService.cs` [E14] y a `IMemberDirectoryReader` (E12) `ListRecipientsAsync`, por ids o por rol, con nombre, correo verificado, idioma y zona; copiá `AssistantTurnService.cs`. La captura crea las ejecuciones de «Recibida» en su commit, y la decisión, las de su evento; se reserva con lease y turno, se valida ahí, se corre fuera de la UoW y se confirma, y el correo se encola con `IOutbox` al confirmar. **A quién llega:** a quien la cargó, al chat del contacto o al vinculado del miembro, y sin chat, «No aplica» (89); a una persona o a un rol, al vinculado, y sin él, `NoConversation`. **De allá (T14):** el motor de la parte de la spec, con `FormActionExecution.Unresolved` como regla única de la 228 y el aviso a quien cargó copiado de `FormActionStallService`, con `StallNoticedAtUtc`; `RoleWithoutMembers` cuenta a quien cargó o corrigió solo con «Puede decidir quien cargó» (276).
4. **B4 · El worker.** `I/Modules/Forms/FormActionWorker.cs` sobre `platform.FormActionWorks`; copiá `AssistantTurnWorker.cs` y el scope de `WhatsAppInboundProcessor.cs` (`SetFromAccess(tenantId, TenantKind.Business)`). Reserva solo los tipos que conoce (`FormActionKinds`) y continúa el `TraceParent` en el `ActivitySource` del módulo, con etiquetas sin valores.
5. **B5 · Aprobación.** `D/Forms/FormApprovalTask.cs`, `FormApprovalService.cs`; copiá `AssistantConversationService.cs`. El rol es de organización; los de quien decide salen de `IRoleReader.ListOrganizationRoleIdsAsync`. La tarea copia al abrirse las dos casillas de la revisión con que llegó la respuesta (276). Más: decidir con el formulario dado de baja.
6. **B6 · «Por aprobar».** Suma a `IFormReader.cs` y `FormReader.cs` [E14]; `FormApprovalPendingItems.cs`, `FormApprovalsNotificationSource.cs` y `FormRuleNoticeNotificationSource.cs`; copiá `OrganizationUserPendingItems.cs` y `FormPublicationNotificationSource` [E14]. Más: una sola fuente para la lista, «Para resolver» y la campana. **«Por resolver»** (228): el filtro `unresolvedActions` va en la función única de filtros, como un `EXISTS`, y `unresolvedActionCount` parte de las ejecuciones por resolver del formulario, por su índice, unidas una vez a las de «Todas». `status=pendingApproval` y `sort=pending` en `FormReader` (264 y 275).
7. **B7 · Estado y vigentes.** Suma a `FormRecordQueryFilters.cs`, `FormRecordColumnsBuilder.cs` (la columna «Estado»), `FormRecordReader.cs` (`state`, `corrected` y `hasPdf`), `FormRecordSheetBuilder.cs`, `FormDashboardBuilder.cs` y `FormReader.cs` [E15], y a `GET /entry` [E20]. «Estado» es columna fija del Excel. Más: un número corregido da una sola `lastRecord`.
8. **B8 · PDF.** `FormPdfDesignService.cs`, `IFormPdfRenderer.cs`, `I/Modules/Forms/FormPdfRenderer.cs`; copiá `AssistantExportRenderer.cs`. Una copia A4 por idioma, escrita una vez en `IFileStore` (`forms`) y confirmada después del commit; `FormRecordDocuments` se suma a `ListReferencedStorageKeysAsync` [E18]. Lo que no es PNG ni JPEG pasa a PNG con SkiaSharp. Las horas, con `DisplayFormatter`. Más: un logo y una foto WebP; la copia sigue después de las 24 horas.
9. **B9 · Avisos y WhatsApp.** `IStructuredRecordActionChannel.cs`, su apagado, `WhatsAppStructuredRecordActionChannel.cs`; suma a `WhatsAppOutboundMessage.cs` y `WhatsAppCloudClient.cs`; copiá `WhatsAppReplyIssuer.IssueFileAsync` [E18]. El PDF viaja por clave y se sube con `UploadAsync` al despachar. Dentro de las 24 horas va como documento, con `filename` y el texto en `caption` si entra; si no, el texto va antes. Lo de la base 2 (K0) no se vuelve a programar; `SendFailed` muestra en «Detalle» los códigos de la ADR 0066 §3. Más: una variable con un salto; 132018.
10. **B10 · Plantillas con documento.** Suma a `WhatsAppTemplateBase.cs` (`HeaderFormat`) y `WhatsAppTemplateClient.cs` (la subida del ejemplo con `WhatsApp:AppId` y el token de la organización). Más: lo que va a Meta; la sincronización trae `documentHeader`.
11. **B11 · Correo con adjunto.** Suma a `EmailMessage.cs` (`Attachments[{StorageKey, FileName, MediaType}]`), `EmailChannelSender.cs` y `MimeMessageFactory.cs`; `FormEmailActionNotifier.cs`; copiá `IChannelSender.cs`. Más: un archivo que falta.
12. **B12 · La pregunta «Correo».** Suma a `FormQuestionType.cs`, `WhatsAppFlowBuilder.cs` (el perfil `flow-static-7.3-r12`, sobre `r11`; lo sellado no cambia), el verificador [E14], `FormRecordCondition.cs` y `FormRecordColumnsBuilder.cs` [E15], `FormConditionEvaluator.cs` y `FormLogicPolicy.cs` [E19] y `FormPdfRenderer.cs`. Más: no entra en el bloque (196).
13. **B13 · Corregir y anular.** `FormRecordCorrectionService.cs`, `FormRecordVoidService.cs`; suma a `FormEntrySubmitService.cs` y `FormEntryDurableAdapter.cs` [E20], que suma `correction:` y su dueño: quien corrige con `formrecords.correct`, o quien la cargó en lo propio. Pasa por la captura con quien cargó la original y sus filtros por persona. Más: el reintento después de un corte; el permiso perdido; un formulario con una anulada no se borra.
14. **B14 · Lo propio y copiar.** Suma a las lecturas `own-records` [E20] y a `FormDefinitionCopyCreator.cs` [E17].
15. **B15 · Rutas y contratos.** `P/Modules/Forms/Controllers/BusinessFormRulesController.cs` (reglas, diseño y `retry`), `BusinessFormApprovalsController.cs` (`pending`, `approvals` y `decision`) y `BusinessFormCorrectionsController.cs` (`correction`, `corrections` y `void`); suma `workflow`, `pdf` y `history` a `BusinessFormRecordsController.cs` [E15], y lo propio, a `BusinessFormEntryController.cs` [E20]; `Contracts/*` y `FormsContractContributor.cs`. Más: `RouteIsolationMatrixTests`.
16. **B16 · La baja de la cuenta.** Suma a `FormsAccountDeletionParticipant.cs` y su cleanup [E14]; copiá `AssistantAccountDeletionParticipant.cs`.
17. **B17 · El chat en «Mi cuenta».** Suma a `PersonalWhatsAppService.cs`, `PersonalWhatsAppController.cs` y `WhatsAppConversationLinkWriter.cs`; copiá `LoginMethodManagementService` para el código. El vínculo, su migración y lo que se prueba están en la [parte de pasos](formularios/21-parte-pasos-y-verificar.md#el-vínculo-del-chat-b17).

**Texto para el chat del Back**

```text
Leé AGENTS.md, docs/plans/2026-11-08-etapa-21-reglas-y-aprobacion.md, la spec de la E21 con sus partes, las decisiones 261 a 264, 266, 267, 270, 275 y 276 de la hoja de ruta y las fichas que cita su «Qué leer». Después de K1, hacé B1 a B17 en orden contra ese contrato, copiando los moldes nombrados. No toques el front. Todo en main, sin ramas, worktrees, stash, reset ni push; commiteá solo lo tuyo con git commit -- <rutas>, en español y con conventional commits. Con la app levantada, nadie edita el src del front. TDD donde hay lógica. Por tarea: build y tests rápidos enteros, sin Docker ni filtros; si tocó rutas, persistencia, autenticación o aislamiento, también integración y seguridad del área. El contrato no cambia sin anotarlo en «Cambios al contrato» y avisarle a la Guía. Ninguna clave real pasa por vos. Al usuario preguntale solo lo de producto. No edites la tabla de estado.
```

## Front

Siguen los tableros de la v175 y las fichas del front, con i18n, los tipos generados, `shared/format`, las esperas (115) y axe donde cambia una pantalla. Los tests son los de la spec, más lo que se nombra.

1. **F1 · Tipos y llamadas.** `F/src/modules/forms/api/workflow.ts`; copiá la API de la E20.
2. **F2 · «Reglas».** `components/rules/`, `lib/rules.ts`; copiá `FRM-Editor-Reglas`, `FRM-Editor-Reglas-Bloque` y sus teléfonos. Textos de «Pedir aprobación» (276): «Puede decidir quien cargó» / "The submitter can decide", con la (i) «Quien cargó o corrigió la respuesta también puede aprobarla o rechazarla.» / "Whoever submitted or corrected the response can also approve or reject it.", y «Exigir motivo al rechazar» / "Require a reason to reject", con «Sin marcar, se puede rechazar sin escribir un motivo.» / "Unchecked, it can be rejected without a reason."
3. **F3 · El diseño del PDF.** `pages/PdfDesignPage.tsx`, `components/pdf/PdfPage.tsx`; copiá `FRM-Pdf`.
4. **F4 · «Por aprobar» y la campana.** Suma a la lista y a `FormRecordsPage.tsx`, y a `notifications` de `registration.ts`: «{count} respuestas por aprobar en {formName}» («1 respuesta…») / "{count} responses to approve in {formName}", y «No se pudo mandar el aviso de {displayNumber}» / "The notice for {displayNumber} couldn't be sent". Con «Por resolver (N)», la columna «Reglas», el chip del Excel y «Es cliente» en la cola (228, 226 y 206); «Con algo por aprobar» / "With something to approve" (275), y «1 de 2 por aprobar» / "1 of 2 to approve". Copiá `FRM-Formularios-21`, `FRM-Respuestas-21`, `FRM-Respuestas-Propias-21` y `Campana-21`.
5. **F5 · La decisión y Actividad.** `components/records/Workflow.tsx`; suma a `QuickView.tsx` y `RecordFullPage.tsx`; copiá `FRM-Respuesta-Aprobacion`. Textos: «Sin confirmar» / "Unconfirmed", «En duda» / "Uncertain", «Lo revisó {name} el {at}.» / "{name} reviewed it on {at}.", «{name} pidió reintentar el {at}.» / "{name} asked to retry on {at}.", «Sin motivo» / "No reason" (276), «Ver PDF» / "View PDF" y «Bajar PDF» / "Download PDF" (267). Suma a `RecordActivity.tsx` [E15] sus renglones.
6. **F6 · Corregir y anular.** Suma el modo «Corregir» a `RecordForm.tsx` y `lib/recordForm.ts` [E15 y E20], `QuickView.tsx` y `RecordFullPage.tsx`, sin página ni ruta propias (266), y `components/records/VoidDialog.tsx`; «Ya cargado · 86 KB» sale de `size`. Copiá `FRM-Respuesta-Corregir` y `FRM-Respuesta-Anular`. Más: el espacio de traducciones del motivo (18).
7. **F7 · Lo propio, tablero, tarjeta, baja y «Correo».** Suma a `FormRecordsPage.tsx`, `FormDashboard.tsx`, la tarjeta del chat, `LifecycleDialogs.tsx` (conserva la línea de la E20), el editor, `ConditionsEditor.tsx`, `lib/conditions.ts`, `RowsBlockEditor.tsx` [E19] y la carga; copiá `FRM-Respuestas-Propias-21`, `FRM-Tablero-21`, `WA-Chats-Respuesta-21`, `WA-Chats-Avisos`, `FRM-Dar-De-Baja-21`, `FRM-Editor-Baja-21`, `FRM-Editor-21` y `FRM-Cargar-Respuesta-21`.
8. **F8 · «Mi cuenta».** `F/src/modules/whatsapp/personal/components/AccountChatsSection.tsx` y su llamada; `account` en `ModuleRegistration` y su lugar en `AccountPage.tsx`, debajo de «Cómo entrás», con el acceso personal; copiá `Cuenta-21`, `MessagesPage.tsx` y `personal/api/messages.ts`. «Vincular», en el ⋮ de la organización (261), abre el diálogo del código de «Cómo entrás». Más: vincular, y la espera del 429.
9. **F9 · La plantilla con el PDF** (201). «Nueva plantilla» de WhatsApp suma «Encabezado», con «Sin encabezado» o «Documento (el PDF)», debajo de «Tipo»; copiá `WA-Plantilla-Nueva-21` y `M-WA-Plantilla-Nueva-21`.

**Texto para el chat del Front**

```text
Leé AGENTS.md del front, ../ArquitecturaBaseMutitenant/docs/plans/2026-11-08-etapa-21-reglas-y-aprobacion.md y la spec de la E21 con sus partes, con las decisiones 261 a 264, 266, 267, 269, 275 y 276 de la hoja de ruta. Esperá K1, corré npm run contracts y hacé F1 a F9 contra los tipos generados, copiando los tableros de la v175; lo que el encabezado del plan da por redibujar se espera aprobado. No toques el lienzo, las specs ni el back. Todo en main, sin ramas, worktrees, stash, reset ni push; commiteá solo lo tuyo con git commit -- <rutas>, en español y con conventional commits. Con la app levantada, no edites el src. TDD donde hay lógica. Por tarea: npx tsc -b y npm test enteros, sin filtrar, y si tocó rutas, sesión o permisos, sus pruebas de integración y seguridad; al cerrar, build, lint y contracts:check. El contrato no cambia sin anotarlo en «Cambios al contrato». Ninguna clave real pasa por vos. Al usuario preguntale solo lo de producto. No edites la tabla de estado.
```

## Integración y puerta

1. **I1 · Integrar** contra la API real: lo que no encaja vuelve a su dueño o va a «Cambios al contrato».
2. **I2 · Puerta.** `/puerta`: las suites y `contracts:check`; el E2E real (armar las reglas, recibir, aprobar desde la lista y desde «Para resolver», rechazar, corregir en el lugar una aprobada, anular una por aprobar, el PDF, un correo con adjunto en la carpeta de correos y los pasos 10 y 11 de «Cómo se cierra»); `realinear-docs` y `estado-docs` en 0; la galería de los tableros de la spec; la lista de control, que suma los riesgos de la [prueba integral de ArquitecturaBase](../backlog.md#lo-que-vio-la-prueba-integral-de-arquitecturabase): la vista rápida y la pantalla completa cuentan y recorren lo mismo, «1 de 2 por aprobar» (O22); si «Enviada» dice el estado, es el que quedó después de las reglas, también «Por resolver» (O23), y una regla con una opción borrada dice cuál y marca su condición (O41).
3. **I3 · Recorrido.** `/recorrido` con `armar-las-reglas` y `aprobar-o-rechazar`.
4. **I4 · Al cerrar.** `docs/features/formularios.md` y `whatsapp.md` suman su sección, la guía de correos los adjuntos y cada carpeta nueva su `AGENTS.md`. **Pendiente para la I1 de la E10** (123): «Cómo se cierra» con un número conectado y el almacenamiento, y crear por la API una plantilla con documento.

| Puerta | Resultado |
| --- | --- |
| Back / Front / Contrato / E2E / Documentación / Galería / Recorrido | Pendiente |

**Texto para el chat de Integración**

```text
Leé AGENTS.md de los dos repos, docs/plans/2026-11-08-etapa-21-reglas-y-aprobacion.md y la skill /puerta. Integrá contra K1, devolvé cada diferencia a su dueño y corré la puerta una sola vez sobre copias del corte. Todo en main, sin ramas, worktrees, stash, reset ni push; commiteá solo lo tuyo con git commit -- <rutas>, en español y con conventional commits. Con la app levantada, nadie edita el src del front, y el lienzo no se toca. TDD en cada arreglo, con el build y los tests rápidos enteros de su repo, sin filtrar, más integración y seguridad del área. El contrato no cambia sin anotarlo en «Cambios al contrato». Nunca manejes la PC del usuario: ninguna clave real pasa por vos. Al usuario preguntale solo lo de producto. Dejá solo la tabla final en este plan.
```

## Relación con otras etapas

**Usa de las anteriores:** la lista de K0; de la E11, las plantillas, la ventana de 24 horas, el outbox, `WhatsAppConversationLink` y su escritor, `WhatsAppLinkedMessage`, `PersonalWhatsAppService` y `WA-Plantilla-Nueva`.

**Deja para las siguientes:**
- **tablas:** `FormRuleSets`, `FormPdfDesigns`, `FormApprovalTasks`, `FormActionExecutions`, `FormActionRecipients`, `FormRecordDocuments`, `platform.FormActionWorks`; `ActiveRuleSetId` y `ActivePdfDesignId`; `FormRecords.State`, `Version` y lo de corregir y anular;
- **lo demás:** las piezas de B1 a B17 y F1 a F9, el perfil `r12`, la forma de `rules[]` y del diseño, los permisos y la campana del contrato, y el vínculo con `State` (quien lo lea, solo los `Linked`).

## Cambios al contrato


## Para decidir

Nada: la P2 (dónde se marca que una plantilla lleva el PDF) la resolvió la decisión 201, en B10 y F9.
