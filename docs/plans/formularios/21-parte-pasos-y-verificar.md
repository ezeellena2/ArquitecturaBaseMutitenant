# E21 · Reglas, aprobación, PDF y avisos: pasos y qué verificar (spec, parte)

**Estado:** pendiente · parte de la [spec de la E21](21-reglas-y-aprobacion.md), que dice qué leer y cómo se cierra.

## Pasos

Abreviaturas: `D/`, `A/`, `I/` y `P/` son las capas del back; `F/`, el front, y `[E14]` a `[E20]`, un archivo que deja esa etapa.

**Back:**

| # | Qué hace | Archivos | Copia de | Test |
| --- | --- | --- | --- | --- |
| B1 | Reglas: dominio, validación y revisiones | `D/Forms/FormRuleSet.cs`, `A/Modules/Forms/Services/FormRuleService.cs`, `FormRulesPolicy.cs` | `FormDefinitionService.cs` [E14] | cada motivo; el PDF al recibir, con y sin aprobación; 409; lista vacía; no toca el borrador; el bloque como dato, sin sus preguntas sueltas |
| B2 | Permisos, tablas y migraciones | `Permissions.cs`, `Permissions{,.en}.resx`; `I/…/Configurations/`; las migraciones | `20261010203618_SystemRoleAssistantPermissions.cs` | aislamiento; hacia atrás; `PermissionTextsTests` |
| B3 | El motor: crear ejecuciones en la captura, reservar, correr y confirmar | `IFormActionService.cs`, `FormActionService.cs`, `FormActionPlanBuilder.cs`; suma a `FormRecordService.cs` [E14] | `AssistantTurnService.cs` | clave única; «No aplica»; orden dentro de la regla; un turno viejo no escribe; condición en memoria igual que en SQL; «algún ítem», con varias del bloque en el mismo ítem |
| B4 | El worker | `I/Modules/Forms/FormActionWorker.cs` | `AssistantTurnWorker.cs`, `WhatsAppInboundProcessor.cs` | una vuelta de su bucle; una falla no frena a las demás; un aviso en un chat vinculado escribe la copia compartida; la lista de `TenantScopeUsageTests` |
| B5 | Aprobación | `D/Forms/FormApprovalTask.cs`, `FormApprovalService.cs` | `AssistantConversationService.cs` | la propia; sin el rol; dos a la vez; un 403 de leer conserva la tarea; decidir sin `formrecords.read` |
| B6 | «Por aprobar»: lista, cola, «Para resolver» y campana | suma a `IFormReader.cs` y `FormReader.cs` [E14]; `FormApprovalPendingItems.cs` | `OrganizationUserPendingItems.cs` | una sola fuente; `/pending`; la continuación en SQL; lo propio no cuenta |
| B7 | Estado, anuladas y vigentes en lo que ya existe | suma a `FormRecordQueryFilters.cs`, `FormRecordSheetBuilder.cs` y `FormDashboardBuilder.cs` [E15] | esos mismos | el filtro en los cinco lugares; conteos; Excel con anuladas, y sin esa casilla con un estado elegido |
| B8 | PDF: diseño, vista previa y copia fija | `FormPdfDesignService.cs`, `IFormPdfRenderer.cs`, `I/Modules/Forms/FormPdfRenderer.cs` | `AssistantExportRenderer.cs` | cada contenido; el bloque entero, con la tabla que sigue con su encabezado; sin página pública; la copia no cambia; fuentes embebidas |
| B9 | Avisos: el puerto y su lado WhatsApp | `A/Interfaces/Integrations/StructuredData/IStructuredRecordActionChannel.cs`, su apagado, `WhatsAppStructuredRecordActionChannel.cs`; suma a `WhatsAppOutboundMessage.cs` y `WhatsAppCloudClient.cs` | `WhatsAppReplyIssuer.cs` | ventana al aceptar y al despachar; plantilla; documento; un destinatario roto; sin duplicados; el bloque, un ítem por línea |
| B10 | Plantillas con documento y variables | suma a `WhatsAppTemplateBase.cs` y `WhatsAppTemplateClient.cs` | esos mismos | el encabezado que va a Meta; rechazo |
| B11 | Correo con adjunto | suma a `EmailMessage.cs` y al canal de correos; `FormEmailActionNotifier.cs` | `IChannelSender.cs` | adjunto por clave, leído al despachar; dirección enmascarada en logs |
| B12 | Pregunta «Correo» | suma a `FormQuestionType.cs`, `WhatsAppFlowBuilder.cs`, el verificador y las lecturas | los mismos [E14 y E15] | válido e inválido; filtros; web |
| B13 | Corregir y anular | `FormRecordCorrectionService.cs`, `FormRecordVoidService.cs`; suma a `FormEntrySubmitService.cs` [E20] | esos mismos | quién, también con una rechazada; «Cargó» no cambia, también con un cliente; el número se conserva; archivos conservados; los ítems vuelven si el bloque sigue; reglas de nuevo; vuelve a «Por aprobar»; 409 entre dos; tarea cerrada; la anterior se abre por id; la marca de la tarjeta del chat |
| B14 | Lo propio y copiar | suma a las lecturas `own-records` [E20] y `FormDefinitionCopyCreator.cs` [E17] | esos mismos | el motivo de rechazo en los momentos; lo ajeno = 404; primera revisión |
| B15 | Rutas y contratos | `P/Modules/Forms/Controllers/BusinessFormRulesController.cs`, `BusinessFormApprovalsController.cs`, `Contracts/*`, `FormsContractContributor.cs` | los de la E15 | cada ruta con 401, 403, feature apagada = 404, 409; ejemplos exportados |
| B16 | La baja de la cuenta | suma a `FormsAccountDeletionParticipant.cs` y a su cleanup [E14] | `AssistantAccountDeletionParticipant.cs` | la dirección y el texto armado de esa cuenta se borran; la ejecución queda |
| B17 | El chat en «Mi cuenta» | suma a `PersonalWhatsAppService.cs` y una ruta de la cuenta en `P/Modules/WhatsApp/Controllers/` | `PersonalWhatsAppController.cs` | cada organización de la persona; vinculado, sin vincular y sin chat; hasta cuándo llegan, con la ventana de 24 horas; sin el módulo, no hay ruta |

**Front:**

| # | Qué hace | Archivos | Copia de | Test |
| --- | --- | --- | --- | --- |
| F1 | Tipos y llamadas | `api/workflow.ts` | la API de la E20 | zod estricto con los ejemplos |
| F2 | La pestaña de reglas | `components/rules/`, `lib/rules.ts` | su tablero | guardar, el historial detrás de su botón, 409 y salir; las reglas guardadas en pasos; «Agregar dato», «Así llega» y «Pregunta no disponible»; «Algún ítem con…» y el bloque como dato; la casilla no se desmarca sola; subir y agregar «Armar el PDF»; cambiar el tipo conserva lo escrito y guarda solo lo que usa |
| F3 | El diseño del PDF | `pages/PdfDesignPage.tsx`, `components/pdf/PdfPage.tsx` | su tablero | estilos; el bloque entero; vista previa sin guardar; `<object>` |
| F4 | «Por aprobar» | suma a la lista y a `FormRecordsPage.tsx` [E15] | su tablero | las tres combinaciones; la vista «Por aprobar» y adónde abre; la cola con ↑ ↓ |
| F5 | La decisión y Actividad | `components/records/Workflow.tsx`; suma a `QuickView.tsx` y `RecordFullPage.tsx` | su tablero | solo decide; «¿Aprobar VIS-000003?», con la principal y el foco en «Cancelar», y Enter no aprueba; el diálogo abierto hasta la respuesta; Reintentar; se relee mientras sale; la actividad en columnas fijas, por momento; sin el rol, la decisión de solo lectura y ningún error |
| F6 | Corregir y anular | `components/records/CorrectionForm.tsx` y `pages/CorrectionPage.tsx`, con los componentes de la E20; suma «Corregir» y el ⋮ a `QuickView.tsx` y `RecordFullPage.tsx` | el tablero | «Corregir» en la fila del título y el ⋮ solo con «Anular»; lo de antes, sin «Antes:» en el bloque; «Ya cargado · 86 KB»; el motivo al final; «¿Descartar la corrección?»; 409; «Ver la actual»; «¿Anular VIS-000127?»; el filtro «Anulada» e «Incluir las anuladas» |
| F7 | Lo propio, tablero y tarjeta | suma a `FormRecordsPage.tsx`, `FormDashboard.tsx` y la tarjeta | su tablero | estado y PDF; el motivo del rechazo en los momentos, sin columna; la tarjeta con «Después se corrigió…» o «Después se anuló…» |
| F8 | «Mi cuenta» | `F/src/modules/whatsapp/personal/components/AccountChatsSection.tsx` y su llamada; suma su lugar a `F/src/areas/personal/account/pages/AccountPage.tsx` | `personal/pages/MessagesPage.tsx` y `personal/api/messages.ts` | vinculado, sin vincular y sin chat; «Llegan hasta el …» y «Solo con plantilla», con su (i); sin el módulo, la zona no está |

**Al cerrar:** `docs/features/formularios.md` y `whatsapp.md` suman su sección, y la guía de correos, los adjuntos.

## Qué verificar al arrancar

| Marca | Qué hay que confirmar | Qué asume esta spec |
| --- | --- | --- |
| [Meta] | Antes de programar: que una plantilla acepte un documento en el encabezado, y cómo se da de alta. | Sale con una carga previa de ejemplo. |
| [E14] | La captura. | Las ejecuciones entran en su commit. |
| [E15] | La función de filtros y `FormRecords`. | Filtrar las vigentes en un solo lugar alcanza. |
| [E19] | El bloque en las condiciones y en `FormRecordRowValues`. | «Algún ítem» se evalúa igual en memoria y en SQL, con la regla de «Filtros». |
| [E20] | La captura web y `sameOperation`. | Una corrección usa las dos, con quien cargó la original en lugar de la sesión. |
| Allá | La etapa 4 de renglones, las reglas con el bloque, que el 2026-11-07 no tenía plan. | Sigue RG5-A, como dice esta spec. Lo que allá cambie se adopta si no choca con las reglas de acá (126). |
| Núcleo | El canal de correos y `EmailMessage`. | Suma adjuntos por clave, sin bytes en el outbox. |
| Núcleo | Cómo leer las identidades de rol de una cuenta. | Un lector del núcleo las da, sin que Forms lea `identity`. |
| Núcleo | Cómo «Mi cuenta» suma la zona de un módulo. | El módulo de WhatsApp la pone, y apagado no está. |
| Base de la 12 | La campana. | Un módulo le aporta avisos. |
| Página pública | El logo y los datos. | Se leen por un puerto del núcleo. |
