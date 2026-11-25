# E21 · Reglas, aprobación, PDF y avisos: pasos y qué verificar (spec, parte)

**Estado:** pendiente · parte de la [spec de la E21](21-reglas-y-aprobacion.md), que dice qué leer y cómo se cierra.

## La pregunta «Correo» (141 y 196)

Un tipo nuevo, validado con la ficha de correos. Suma a todo lo que existe: el editor, el Flow (un `TextInput` de correo), el verificador, la planilla, sus filtros (`contains` y `equals`), el detalle, el Excel, las condiciones, la carga desde la web, donde ocupa una columna (141), y el PDF. No entra en el bloque: en una pregunta del bloque aparece apagada en «Tipo de respuesta», con su motivo en el tooltip, como Foto y Documento, y «Mandar por correo» ofrece solo las preguntas de afuera del bloque (196).

## Pasos

Abreviaturas: `D/`, `A/`, `I/` y `P/` son las capas del back; `F/`, el front, y `[E14]` a `[E20]`, un archivo que deja esa etapa.

**Back:**

| # | Qué hace | Archivos | Copia de | Test |
| --- | --- | --- | --- | --- |
| B1 | Reglas: dominio, validación y revisiones | `D/Forms/FormRuleSet.cs` y `FormRuleErrors.cs`; `A/Modules/Forms/Interfaces/Services/IFormRuleService.cs`, `Interfaces/Persistence/IFormRuleRepository.cs` y su implementación; `A/Modules/Forms/Services/FormRuleService.cs`, `FormRulesPolicy.cs` | `FormDefinitionService.cs` [E14] | cada motivo; el PDF al recibir, con y sin aprobación; 409; lista vacía; no toca el borrador; el bloque como dato, y `PlaceholderInvalid` con una de sus preguntas; `OptionUnavailable`, con el bloqueo de las definiciones antes que el del catálogo; `RecordsReadRequired`; la ubicación solo con condiciones de presencia; las dos casillas de «Pedir aprobación», omitidas con su valor por defecto, y `OptionUnavailable` que nombra la opción (276) |
| B2 | Permisos, tablas y migraciones | `Permissions.cs`, `Permissions{,.en}.resx`, con sus áreas (257) y lo incluido (260): `[FormRecords.Correct] = [FormRecords.Read]` en `Permissions.Includes`, con el par en `PermissionInclusionsTests` y en la lista «Hoy:» de `docs/rules/permisos.md`, y `formrules.manage` y `formapprovals.decide` sin nada; `D/Forms/FormPdfDesign.cs`, `FormActionExecution.cs`, `FormActionRecipient.cs`, `FormRecordDocument.cs`, `FormActionWork.cs` y `FormActionKinds.cs`; la configuración de cada tabla en `I/Modules/Forms/Persistence/Configurations/`, con `FormActionWorkConfiguration.cs` en `Schemas.Platform`; las migraciones | `20261010203618_SystemRoleAssistantPermissions.cs` | aislamiento; hacia atrás; `PermissionTextsTests` y `PermissionInclusionsTests`; `FormsPersonalDataPurgeTests` [E12] inscribe lo que detecte, con su purga o por qué se conserva: `FormActionRecipients` (la baja borra la dirección y el texto armado de esa cuenta, y la ejecución queda), `FormActionExecutions`, `FormApprovalTasks`, `FormRuleSets` y `FormPdfDesigns` (sus `*ByUserId` siguen a la cuenta, como la auditoría), y la línea de `FormRecords` suma `CorrectedByUserId` y `VoidedByUserId` |
| B3 | El motor: crear ejecuciones en la captura, reservar, correr y confirmar | `IFormActionService.cs`, `FormActionService.cs`, `FormActionPlanBuilder.cs`, `FormActionStallNotifier.cs` (el aviso a quien cargó), `FormActionExecutionErrors.cs`, `IFormActionRepository.cs` y su implementación; suma a `FormRecordService.cs` [E14] | `AssistantTurnService.cs` | clave única; «No aplica»; orden dentro de la regla; un turno viejo no escribe; condición en memoria igual que en SQL; las condiciones con valor del bloque en un mismo ítem, y la presencia sobre la respuesta entera, también sin ningún ítem que las cumpla todas; `NoLongerInRole` sin frenar a los demás; `RoleWithoutMembers`, sin contar a quien cargó ni a quien corrigió salvo con «Puede decidir quien cargó» (276), y su reintento; las ocho consultas sin reenviar; `Unresolved`; el aviso a quien cargó, una vez por acción, y «No aplica» con la ventana cerrada o sin chat; `lastRequest` |
| B4 | El worker | `I/Modules/Forms/FormActionWorker.cs` | `AssistantTurnWorker.cs`, `WhatsAppInboundProcessor.cs` y, para el anillo, `WhatsAppTemplateSyncWorker.cs` | una vuelta de su bucle; con otro anillo de claves (`IKeyRingReadiness`, 0064 §11), no toma nada; con `forms` apagada (`TenantFeatureRow.IsModuleEnabled`, 0069 §5), no reserva y sigue al prenderla, en `FormsModuleOffKeepsDataTests` [E12]; `FormActionWorker` en la lista de `SetFromAccess` de `docs/rules/multitenancy.md`; una falla no frena a las demás; un tipo de acción desconocido no se reserva; `TraceParent` continuado, con etiquetas sin valores; un aviso en un chat vinculado escribe la copia compartida; la lista de `TenantScopeUsageTests` |
| B5 | Aprobación | `D/Forms/FormApprovalTask.cs` y `FormApprovalErrors.cs`, `IFormApprovalService.cs`, `FormApprovalService.cs`, `IFormApprovalRepository.cs` y su implementación | `AssistantConversationService.cs` | la propia, con y sin «Puede decidir quien cargó»; rechazar sin motivo, con y sin «Exigir motivo al rechazar», con lo sellado al llegar (276); sin el rol; dos a la vez; un 403 de leer conserva la tarea; decidir sin `formrecords.read` |
| B6 | «Por aprobar»: lista, cola, «Para resolver» y campana | suma a `IFormReader.cs` y `FormReader.cs` [E14]; `FormApprovalPendingItems.cs` | `OrganizationUserPendingItems.cs` | una sola fuente; `/pending`; la continuación en SQL; lo propio no cuenta, salvo con «Puede decidir quien cargó» (276); `status=pendingApproval` y `sort=pending` (264 y 275), también un formulario dado de baja con algo por aprobar; «Por resolver»: el filtro y la marca en un `EXISTS`, la cuenta por su índice, solo para quien administra reglas y ve todas, sin las reemplazadas ni las anuladas |
| B7 | Estado, anuladas y vigentes en lo que ya existe | suma a `FormRecordQueryFilters.cs`, `FormRecordSheetBuilder.cs` y `FormDashboardBuilder.cs` [E15] | esos mismos | el filtro en los cinco lugares; conteos; Excel con «Estado» siempre, con anuladas, y sin esa casilla con un estado elegido; el orden por `state`, en el circuito, `corrected` y `unresolvedActions` (264) |
| B8 | PDF: diseño, vista previa y copia fija | `D/Forms/FormPdfDesignErrors.cs`, `IFormPdfDesignService.cs`, `FormPdfDesignService.cs`, `A/Modules/Forms/Interfaces/Integrations/IFormPdfRenderer.cs`, `I/Modules/Forms/FormPdfRenderer.cs` | `AssistantExportRenderer.cs` | cada contenido; lo oculto no va (266); el bloque entero, con la tabla que sigue con su encabezado; sin página pública; la copia no cambia; una copia por idioma, y a cada persona la de su cuenta; las horas en la zona de quien cargó, con su ciudad, y la de la organización para un cliente; la ubicación con su enlace; cada dato de la opción de un catálogo en su renglón y, en el bloque, en su columna (263); fuentes embebidas |
| B9 | Avisos: el puerto y su lado WhatsApp | `A/Interfaces/Integrations/StructuredData/IStructuredRecordActionChannel.cs`, los modelos `StructuredAction*` en `A/Models/StructuredData/`, su apagado `I/StructuredData/DisabledStructuredRecordActionChannel.cs`, `WhatsAppStructuredRecordActionChannel.cs`; suma a `WhatsAppOutboundMessage.cs` y `WhatsAppCloudClient.cs`; el origen «acción de formulario» del mensaje de chat, con `<fecha>_WhatsAppChatMessageOrigin.cs` | `WhatsAppReplyIssuer.cs`, pero cada aviso, también el de quien cargó, con `QueueOptions(OutboxPriority.Notice)`, nunca `Reply` (H31) | ventana al aceptar y al despachar; plantilla; documento; un destinatario roto; sin duplicados; «Sin confirmar» sin repetir y su revisión; lo que contesta sale antes que un aviso; el bloque, un ítem por línea, sin las respuestas vacías, y un ítem vacío con solo su número; la ubicación con el enlace en el renglón siguiente |
| B10 | Plantillas con documento y variables | suma a `WhatsAppTemplateBase.cs` (`HeaderFormat`, en `<fecha>_WhatsAppTemplateHeader.cs`, nulo en las que ya existen) y `WhatsAppTemplateClient.cs` | esos mismos | el encabezado que va a Meta; rechazo; ida y vuelta de la migración |
| B11 | Correo con adjunto | suma a `EmailMessage.cs` y al canal de correos; `FormEmailActionNotifier.cs` | `IChannelSender.cs` | adjunto por clave, leído al despachar; dirección enmascarada en logs; el asunto «{formulario} · {número}»; el nombre y el logo de la organización, y sin página pública, solo el nombre (273) |
| B12 | Pregunta «Correo» | suma a `FormQuestionType.cs`, `WhatsAppFlowBuilder.cs`, el verificador y las lecturas | los mismos [E14 y E15] | válido e inválido; filtros; web |
| B13 | Corregir y anular | `IFormRecordCorrectionService.cs`, `FormRecordCorrectionService.cs`, `IFormRecordVoidService.cs`, `FormRecordVoidService.cs`; suma a `FormRecordErrors.cs` y `FormEntrySubmitService.cs` [E20]; la ruta, con `FormEntryUploadSlotAttribute` [E20] | esos mismos | quién, también con una rechazada; «Cargó» no cambia, también con un cliente; el número se conserva; archivos conservados; los ítems vuelven si el bloque sigue; reglas de nuevo; vuelve a «Por aprobar»; 409 entre dos; tarea cerrada; la anterior se abre por id; la marca de la tarjeta del chat |
| B14 | Lo propio y copiar | suma a las lecturas `own-records` [E20] y `FormDefinitionCopyCreator.cs` [E17] | esos mismos | el motivo de rechazo en los momentos; lo ajeno = 404; primera revisión |
| B15 | Rutas y contratos | `P/Modules/Forms/Controllers/BusinessFormRulesController.cs`, `BusinessFormApprovalsController.cs` y `BusinessFormCorrectionsController.cs`; `Contracts/FormRulesHttpRequest.cs`, `FormPdfDesignHttpRequest.cs`, `FormPdfPreviewHttpRequest.cs`, `FormRecordDecisionHttpRequest.cs`, `FormRecordCorrectionHttpRequest.cs`, `FormRecordVoidHttpRequest.cs` y `FormExecutionRetryHttpRequest.cs`; `FormsContractContributor.cs` | los de la E15 | cada ruta con 401, 403, feature apagada = 404, 409; el 403 de `status=pendingApproval` y `sort=pending`; ejemplos exportados |
| B16 | La baja de la cuenta | suma a `FormsAccountDeletionParticipant.cs` y a su cleanup [E14] | `AssistantAccountDeletionParticipant.cs` | la dirección y el texto armado de esa cuenta se borran; la ejecución queda; la purga es la que la B2 declaró en `FormsPersonalDataPurgeTests` |
| B17 | El chat en «Mi cuenta» | suma a `PersonalWhatsAppService.cs`, a `WhatsAppErrors.cs` y una ruta de la cuenta en `P/Modules/WhatsApp/Controllers/` | `PersonalWhatsAppController.cs`; el código, por `LoginCodeIssuer.IssueAccountCodeAsync` con `VerifyDestination`, como `LoginMethodIssuer` | cada organización de la persona, paginada; vinculado, sin vincular, sin chat y suspendida; hasta cuándo llegan, con la ventana de 24 horas; el código de vincular cuenta en el tope diario por cuenta de `identity.MessageQuotaBuckets` (`WhatsAppCodeAccount`, H10): el que pasa el tope, sumando «Cómo entrás» y «Vincular», da el 429 con su espera; sin el módulo, no hay ruta |
| B18 | Acciones del asistente: `formrecords.void@1` | `A/Modules/Forms/Services/FormRecordActionSource.cs`, registrada en `FormsModule.cs`, con su pantalla en `AssistantActionScreen` y su `AssistantRouteKey`; sus textos `Action.*` en `FormsTexts{,.en}.resx`; su escenario | `RoleActionSource.cs` | los de arquitectura de las acciones y la batería genérica |

**Front:**

| # | Qué hace | Archivos | Copia de | Test |
| --- | --- | --- | --- | --- |
| F1 | Tipos y llamadas | `api/workflow.ts` | la API de la E20 | zod estricto con los ejemplos |
| F2 | La pestaña de reglas | `components/rules/RulesSection.tsx`, la sección, y sus piezas en `components/rules/`, `lib/rules.ts`; suma «Reglas» a `formTabs.ts` [E14], con `formrules.manage`, y `/org/formularios/:id/reglas` a `registration.ts` | su tablero | guardar, el historial detrás de su botón, 409 y salir; el 403 de «Ver respuestas» con el texto del servidor; las reglas guardadas en pasos; «Agregar dato», «Así llega» y «Pregunta no disponible»; «Algún ítem con…», solo con condiciones con valor, y el bloque como dato; la casilla no se desmarca sola; subir y agregar «Armar el PDF»; cambiar el tipo conserva lo escrito y guarda solo lo que usa; lo que se elige, en la pieza (262); «Pedir aprobación» con sus dos casillas y su (i) (276); la opción que ya no está, nombrada y con su condición marcada |
| F3 | El diseño del PDF | `pages/PdfDesignPage.tsx`, en `/org/formularios/:id/pdf` de `registration.ts`, con `formrules.manage`; `components/pdf/PdfPage.tsx` | su tablero | estilos, en la pieza (262); el bloque entero; vista previa sin guardar; `<object>` |
| F4 | «Por aprobar» | suma a la lista y a `FormRecordsPage.tsx` [E15], y a los permisos de `registration.ts`, `formrules.manage`, `formapprovals.decide` y `formrecords.correct`, con sus áreas (257) y lo incluido (260) | su tablero | las tres combinaciones; la columna «Por aprobar» solo para quien aprueba, con su número a la planilla en «Por aprobar», y «Con algo por aprobar» (275); «Estado» por el circuito, «Reglas» y «Corregida», que ordenan (264); la vista «Por aprobar» y adónde abre; la cola con ↑ ↓, «Es cliente» y «Revisar» en el ⋮ (261), que la vista rápida y la pantalla completa cuentan igual; quien solo aprueba, solo la cola; «Por resolver (N)», que aparece con la cuenta o elegida y nunca abre; la columna «Reglas»; el Excel desde la vista, con su chip |
| F5 | La decisión y Actividad | `components/records/Workflow.tsx`; suma a `QuickView.tsx`, `RecordFullPage.tsx` y `RecordActivity.tsx` [E15] | su tablero | solo decide; «¿Aprobar VIS-000003?», con la principal y el foco en «Cancelar», y Enter no aprueba; el diálogo abierto hasta la respuesta; Reintentar, por persona y por acción; «Sin confirmar» y quién lo revisó, y en un correo, «Reintentar» con su confirmación; «No aplica» de quien dejó el rol; quién pidió el último reintento; se relee mientras sale; la actividad renglón por renglón, en sus bandas y con lo de cada renglón en su ⋮ (261 y 267): la decisión con su motivo o «Sin motivo» (276), cada PDF con ver y bajar, cada persona con su estado y el rol de hoy, y cada reintento y cada revisión; «Aprobar» y «Rechazar» arriba de las pestañas; sin el rol, la decisión de solo lectura y ningún error |
| F6 | Corregir y anular | suma el modo «Corregir» a `RecordForm.tsx` y `lib/recordForm.ts` [E15 y E20], y a `QuickView.tsx` y `RecordFullPage.tsx`, sin página ni ruta propias (266); `components/records/VoidDialog.tsx`, con el adaptador `useAssistantProposal('formrecords.void@1', …)` | el tablero | «Corregir» a la vista, con texto en la banda y el lápiz primero en la vista rápida, y «Anular» en el ⋮, también en lo propio (266); corregir en el lugar, con «Corregir VIS-000126», «Cancelar» y «Guardar corrección», sin ⋮, flechas ni actividad, y el aviso de otra versión; lo de antes, sin «Antes:» en el bloque; «Ya cargado · 86 KB»; el motivo al final; «¿Descartar la corrección?»; 409; «Ver la actual»; «¿Anular VIS-000127?»; el filtro «Anulada» e «Incluir las anuladas» |
| F7 | Lo propio, tablero y tarjeta | suma a `FormRecordsPage.tsx`, `FormDashboard.tsx` y la tarjeta | su tablero | estado y PDF; su actividad, con la decisión y el motivo del rechazo o «Sin motivo», sin columna, la corrección y la anulación (267); la tarjeta con «Después se corrigió…» o «Después se anuló…» |
| F8 | «Mi cuenta» | `F/src/modules/whatsapp/personal/components/AccountChatsSection.tsx` y su llamada, en `personal/api/chats.ts`; `account` en `F/src/shared/modules/types.ts` y su lectura en `ModuleRegistry.tsx`; suma su lugar a `F/src/areas/personal/account/pages/AccountPage.tsx` | `personal/pages/MessagesPage.tsx` y `personal/api/messages.ts` | vinculado, sin vincular, con «Vincular» en el ⋮ (261), sin chat y suspendida; «Llegan hasta el …» y «Solo con plantilla», cada uno con su (i); sin el módulo, la zona no está |
| F9 | La plantilla con el PDF (201) | suma «Encabezado» a `F/src/modules/whatsapp/templates/components/TemplateDialog.tsx` y `headerFormat` a `templates/api/templates.ts` | su tablero | «Sin encabezado» o «Documento (el PDF)», en la pieza (262); «Así llega» con el documento arriba |

**Al cerrar:** `docs/features/formularios.md` y `whatsapp.md` suman su sección, y la guía de correos, los adjuntos.

## El vínculo del chat (B17)

`WhatsAppConversationLink` suma `State` (`Unlinked` o `Linked`, y `Linked` para los vínculos que ya existen) y `LastInboundAtUtc`, en `<fecha>_WhatsAppFormActions.cs`. La entrada crea un `Unlinked`, sin el nombre de la cuenta, cuando escribe el teléfono verificado de una cuenta, también en «Internos», y anota cada mensaje. `CanLinkAsync` y `LinkAsync` toman un `Unlinked`; el bot y el código lo pasan a `Linked`; `CopyAsync` copia solo los `Linked`, y desvincular vuelve a `Unlinked`, sin copias. La organización y «Mensajes» ven solo los `Linked`. Las organizaciones salen de `IUserTenantAccessReader` (suma a `IdentityAccessTests`), sin las que no tienen WhatsApp (109); `noticesUntilUtc` es `LastInboundAtUtc` más 24 horas, y el número, el primero conectado. Más: vincular con un código bueno y uno malo; el tope diario por cuenta; un `Unlinked` no copia.

## Acciones del asistente

Siguen la [receta](../../guides/agregar-una-accion-al-asistente.md) (268 y [ADR 0068](../../decisions/0068-acciones-del-asistente.md)), con la tarea B18:
- **`formrecords.void@1`:** anular, `RemoveConfirmation` en «¿Anular VIS-000127?», en la pantalla del detalle de una respuesta (la vista rápida y la completa, con «Anular» en el ⋮), con `formrecords.correct` y `POST api/organization/forms/{id}/records/{recordId}/void`. Sus campos: el formulario (`Choice`), la respuesta por su número (`Record`, que resuelve `FormRecordReader` con RLS) y el motivo (`LongText`, 500). Valida con el validador del pedido y la `Policy` que decide `canVoid` (93), y no sale del sistema.
- **No se declaran:**
  - cargar ni corregir: una respuesta no entra en los tipos cerrados, porque sus preguntas salen de cada versión y trae fotos, documentos, la ubicación y el bloque;
  - las reglas ni el diseño del PDF: son una definición entera, como en la [0057](../../decisions/0057-la-ia-propone-y-una-persona-crea.md);
  - aprobar ni rechazar: deciden una tarea, no crean, editan ni eliminan un registro;
  - lo propio: es la misma pantalla con otra ruta, y la acción es una por ruta.

## Qué verificar al arrancar

| Marca | Qué hay que confirmar | Qué asume esta spec |
| --- | --- | --- |
| [Meta] | Antes de planificar: que una plantilla acepte un documento en el encabezado, cómo se da de alta y que una variable no admite saltos de línea (197). | Sale con una carga previa de ejemplo. |
| [E14] | La captura. | Las ejecuciones entran en su commit. |
| [E15] | La función de filtros, `FormRecords` y `RecordActivity.tsx`. | Filtrar las vigentes en un solo lugar alcanza, y la actividad suma los renglones de cada etapa (267). |
| [E19] | El bloque en las condiciones y en `FormRecordRowValues`. | Las condiciones con valor buscan un mismo ítem y la presencia mira la respuesta entera, igual en memoria y en SQL, con la regla de «Filtros». |
| [E20] | La captura web, `sameOperation` y el modo para cargar de `RecordForm.tsx`. | Una corrección usa las tres, en el lugar (266), con quien cargó la original en lugar de la sesión. |
| Allá | Las etapas 3 y 4 de renglones, probadas con Meta el 9/11 (T12), y el motor del 15 al 17/11: `BusinessFormActionRun.Unresolved`, `FormActionStallService`, `LastRequestKind`, `FormActionKinds`, la consulta automática y `RoleWithoutMembers` (T14). | Deciden y escriben como dice esta spec. Lo que allá cambie después se adopta si no choca con las reglas de acá (126). |
| Núcleo | El canal de correos y `EmailMessage`. | Suma adjuntos por clave, sin bytes en el outbox. |
| Núcleo | Cómo leer las identidades de rol, el idioma y la zona de una cuenta. | Un lector del núcleo los da, sin que Forms lea `identity` (236). |
| Núcleo | «Sin confirmar» en la cola, de la ADR 0066, con su revisión, los códigos de falla y la prioridad de lo que contesta (H31). | Llegan con la segunda parte de la base (214), y B9 los usa sin volver a programarlos. |
| Núcleo | Cómo «Mi cuenta» suma la zona de un módulo. | El módulo de WhatsApp la pone, y apagado no está. |
| Base de la 12 | La campana. | Un módulo le aporta avisos. |
| Página pública | El logo y los datos. | Se leen por un puerto del núcleo. |

## Textos de los errores

En `FormsTexts` y los del módulo WhatsApp, es / en; el tipo de cada error está en la [parte de datos](21-parte-datos-y-contrato.md#contrato).

- `Forms.Rules.Invalid`: «Revisá esta parte de la regla.» / "Check this part of the rule."
- `Forms.Rules.TooMany`: «Hasta 20 reglas, con 10 condiciones y 10 acciones cada una.» / "Up to 20 rules, each with 10 conditions and 10 actions."
- `Forms.Rules.PlaceholderInvalid`: «Pregunta no disponible: quitala del mensaje.» / "Question not available: remove it from the message."
- `Forms.Rules.OptionUnavailable`: «La opción «{0}» ya no está: elegí otra.» / "The option “{0}” is gone: choose another."
- `Forms.Rules.RecordsReadRequired`: «No se guardaron las reglas: un aviso que cita preguntas o adjunta el PDF pide también «Ver respuestas».» / "The rules weren't saved: a notice that quotes questions or attaches the PDF also needs “View responses”."
- `Forms.Rules.RevisionNotFound`: «No encontramos esa revisión.» / "We couldn't find that revision."
- `Forms.PdfDesign.Invalid`: «Revisá esta parte del diseño.» / "Check this part of the design."
- `Forms.Approval.NotFound`: «No encontramos esa aprobación.» / "We couldn't find that approval."
- `Forms.Approval.AlreadyDecided`: «Otra persona ya la decidió.» / "Someone else already decided it."
- `Forms.Approval.OwnRecord`: «No podés decidir una respuesta que cargaste o corregiste.» / "You can't decide a response you submitted or corrected."
- `Forms.Approval.RoleRequired`: «No tenés el rol que la decide.» / "You don't have the role that decides it."
- `Forms.Approval.ReasonRequired`: «Escribí el motivo del rechazo.» / "Write the reason for the rejection."
- `Forms.Record.NotCorrectable`: «Esta respuesta ya no se puede corregir.» / "This response can no longer be corrected."
- `Forms.Record.Superseded`: «Esta versión ya se reemplazó.» / "This version was already replaced."
- `Forms.Record.AlreadyVoided`: «Esta respuesta ya está anulada.» / "This response is already void."
- `Forms.Record.ReasonRequired`: «Escribí el motivo.» / "Write the reason."
- `Forms.Record.ReasonTooLong`: «Hasta 500 caracteres.» / "Up to 500 characters."
- `Forms.Record.PdfNotFound`: «Esta respuesta no tiene PDF.» / "This response has no PDF."
- `Forms.Execution.NotRetryable`: «Este aviso no se puede reintentar.» / "This notice can't be retried."
- `WhatsApp.ChatLink.NotFound`: «No encontramos ese chat.» / "We couldn't find that chat."
- **Fallas:** sus textos están en la [parte de datos y contrato](21-parte-datos-y-contrato.md#contrato), más `SendFailed` «WhatsApp no lo pudo mandar.» / "WhatsApp couldn't send it.".

## La prueba con Meta, antes de programar

La hace el usuario con la cuenta de desarrollo de la 11, que necesita su medio de pago (sin él, Meta responde 131042); ninguna clave pasa por un agente:
1. En business.facebook.com, **WhatsApp Manager › Administrar plantillas › Crear plantilla**: «Utilidad», nombre `prueba_documento_e21`, idioma «Español (ARG)», encabezado de tipo «Documento» con un PDF cualquiera y el cuerpo `Hola {{1}}, te mandamos el comprobante {{2}}.`, con los ejemplos «Ana» y «VIS-000001». «Enviar», y esperá «Activa».
2. En developers.facebook.com/tools/explorer, elegí la app «Plantilla Multitenant», **Get User Access Token** con `whatsapp_business_messaging`, y **Generate Access Token**.
3. `POST <id del número>/messages` (el id está en **App Dashboard › WhatsApp › Configuración de la API**), con el cuerpo `{"messaging_product":"whatsapp","to":"<tu número>","type":"template","template":{"name":"prueba_documento_e21","language":{"code":"es_AR"},"components":[{"type":"header","parameters":[{"type":"document","document":{"link":"https://www.w3.org/WAI/ER/tests/xhtml/testfiles/resources/pdf/dummy.pdf"}}]},{"type":"body","parameters":[{"type":"text","text":"Ana"},{"type":"text","text":"VIS-000001"}]}]}}`. **Submit:** el PDF llega a tu WhatsApp.
4. Repetí con `"text":"Ana\nGómez"` y anotá el código de error.

Después le avisa a la Guía si se aprobó, si llegó el PDF y el código del paso 4.

## Lo que dice Meta (2026-11-08)

Documentación oficial, en `developers.facebook.com/documentation/business-messaging/whatsapp/`:
- **El encabezado con documento** existe en Utility y Marketing: `{"type":"HEADER","format":"DOCUMENT","example":{"header_handle":["<h>"]}}` (`templates/components`).
- **El alta:** `POST /<WABA_ID>/message_templates`. El `header_handle` sale de la Resumable Upload API (`POST /<APP_ID>/uploads` y `POST /upload:<sesión>`, que devuelve `h`), y la muestra solo puede ser PDF (`developers.facebook.com/docs/graph-api/guides/upload`). También se crea en WhatsApp Manager, y se revisa en hasta 24 horas (`templates/template-review`).
- **El envío:** `{"type":"document","document":{"id":"<media>"}}`, con el id de `POST /<PHONE_NUMBER_ID>/media`, hasta 100 MB y por 30 días; `link` pide un servidor público (`templates/template-media`, `business-phone-numbers/media`). Dentro de las 24 horas, un mensaje `document` lleva `caption` de hasta 1024 caracteres y `filename` (`messages/document-messages`).
- **Las variables:** la regla de los saltos de línea ya no figura. Desde la v23.0, un parámetro inválido da 132018 (`templates/template-management`), y el cuerpo admite 1024 caracteres.

## Lo que ya se pagó allá (no repetir)

Cada punto lleva su test, escrito durante la etapa:
1. **Un rol borrado colgaba una aprobación:** se valida al reservar.
2. **Chrome bloqueó el visor del PDF** en un iframe con sandbox: va con `<object>`.
3. **El contrato de `origin` del recibo:** la web rechazó una respuesta correcta; cada campo nuevo se exporta con su ejemplo.
4. **Un aviso a un rol se repetía** por una reserva vencida: la clave va por ejecución y destinatario.
5. **Una copia entera de la respuesta por ejecución:** se lee por id.
6. **Lo que va en la versión y lo que se guarda aparte.** En la prueba de allá, la versión salió sin filtros, y las reglas y el PDF no se habían guardado. Las reglas y el diseño tienen su propio Guardar y valen sin publicar, y salir con cambios pregunta antes.
7. **Un error dice qué hacer en cada caso.** «Agregá Armar el PDF…» se leía igual si faltaba la acción o si estaba más abajo.
8. **Nada se desmarca ni se borra sin avisar:** ni al cambiar el tipo, ni al mover o quitar una acción.
9. **Una decisión que no se deshace pide confirmar:** allá se aprobó la siguiente sin querer, porque ↑ ↓ cambian de respuesta con el foco en el panel.
10. **Un aviso que no puede llegar se descubría recién en la actividad.** Allá sale solo por WhatsApp. Acá va con plantilla fuera de las 24 horas (61) o por correo (62), y lo que no salió se ve por persona en Actividad y en la campana.
11. **El detalle no depende de lo que la persona no tiene.** Allá, con el permiso de aprobar y sin el rol de la tarea, el detalle fallaba al pedir la tarea.
12. **Nada vacío a la vista.** Un historial sin revisiones ocupaba una columna fija: va detrás de su botón, apagado (140).
13. **Un dato guardado por nombre se rompía al renombrar.** El mensaje guarda `{FieldId}`, como las condiciones (149).
14. **Una corrección usa los filtros por persona de quien la cargó** (traspaso 7): con los de quien corrige, otra persona vería otras opciones y podría dejar una que el autor no tenía.
15. **El conteo de respuestas no sale del último número:** con anuladas, se cuentan las vigentes.
16. **Una migración que pierde datos al volver se niega a volver:** el `Down` de las correcciones frena si ya hay correcciones, anuladas o tareas cerradas.
17. **Cancelar una corrección con cambios pregunta** («¿Descartar la corrección?», con «Seguir corrigiendo»); sin cambios, vuelve a la lectura.
18. **Un texto de otro espacio de traducciones se declara en `useTranslation`.** Allá, anular desde la pantalla completa mostraba la clave del motivo, y lo encontró el recorrido, no los tests.
19. **Corregir una respuesta con ítems los perdía:** allá la corrección abría solo las preguntas sueltas. Abre y guarda los ítems, que vuelven si el bloque sigue.
20. **Un rol sin personas abría una tarea que nadie podía decidir** (B3 de allá), y un aviso a un rol le llegaba a quien ya se había ido: los dos se miran al reservar y al mandar.
21. **Una acción frenada se descubría abriendo la respuesta** (F2 de allá): la marca «Por resolver», su vista y el aviso a quien cargó salen de la misma regla.
