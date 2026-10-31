# E21 · Reglas, aprobación, PDF y avisos (spec)

**Estado:** pendiente · spec de la etapa 21 del bloque (decisiones 34, 78, 80, 82 y 91 a 93), con el [formato](plantilla.md) de las etapas. Corresponde a la etapa 4 de ArquitecturaBase, probada allá con un piloto real, y a la etapa 3 de su carga desde la app, corregir y anular, decidida y sin programar. `/plan-etapa` la contrasta con el código real cuando le toque, después de la E20.

## Qué leer

Antes de empezar, en este orden:

1. **Lo anterior:** la [spec de la E14](14-formularios.md): T4, T5, T7 y la §6, punto 7; la [E15](15-respuestas.md): lista, planilla y detalle; la [E17](17-ciclo-de-vida.md): copiar y borrar; la [E18](18-fotos-y-documentos.md): archivos; la [E19](19-condiciones-calculos-y-lineas.md): condiciones, y la [E20](20-cargar-desde-la-web.md): la captura y lo propio.
2. **Decisiones** de la hoja de ruta: 25, 35, 42, 43, 47, 49, 55, 59 a 62, 80, 82, 85, 89 y 91 a 93. Y la [ADR 0040](../../decisions/0040-para-resolver.md).
3. **Fichas:** [guardado](../../rules/guardado.md), [concurrencia](../../rules/concurrencia.md), [idempotencia](../../rules/idempotencia.md), [modulos](../../rules/modulos.md), [multitenancy](../../rules/multitenancy.md), [permisos](../../rules/permisos.md), [persistencia-ef](../../rules/persistencia-ef.md), [auditoria](../../rules/auditoria.md), [emails](../../rules/emails.md), [textos-libres](../../rules/textos-libres.md), [textos-y-traducciones](../../rules/textos-y-traducciones.md), [fechas-y-zonas](../../rules/fechas-y-zonas.md), [logs](../../rules/logs.md), [api-http](../../rules/api-http.md) y [tests](../../rules/tests.md).
4. **Código para copiar:** la columna «Copia de» de los [pasos](#pasos).
5. **Referencia de producto,** solo para leer: en [`../2026-10-17-formularios-traspaso/`](../2026-10-17-formularios-traspaso/README.md), el anexo C, «Etapa 4», el anexo B, «Etapa 4», y el anexo D (G1 a G6); en `../ArquitecturaBase/docs/plans/2026-10-23-traspaso-multitenant/`, el anexo C §5 y §6 (L-05 a L-08), y en ArquitecturaBase, `docs/plans/2026-10-29-cargar-formularios-desde-la-app.md`, «Corregir y anular», con CW10-A y CW11-A.
6. **La maqueta,** en `../MaquetaMultitenant/src/areas/business/forms/`: `api/workflow.ts`, `lib/rules.ts`, `components/rules/`, `components/records/Workflow.tsx` y `CorrectionForm.tsx`, `components/pdf/PdfPage.tsx` y `pages/PdfDesignPage.tsx`. Su corrección es la de la decisión 58, ya reemplazada: manda esta spec.

## Qué se construye

**Reglas** «Cuando llega una respuesta»:
- cada guardado crea una revisión inmutable, con 409. No crea versión ni republica, y guardar una lista vacía las apaga;
- **cada regla:** un evento (Recibida, Aprobada o Rechazada), hasta 10 condiciones unidas con «y», con la gramática de la E15, y hasta 10 acciones en orden. Hasta 20 reglas;
- **las acciones:** pedir aprobación a un rol, armar el PDF, mandarle un mensaje a quien la cargó, avisar a un rol, avisar a una persona (62), mandar por correo (62) y mandar el PDF a un cliente (47);
- los textos usan `{número}`, `{motivo}` y `{pregunta}`, y son un solo texto (35). Una condición sobre una pregunta que no existía o no se mostró cuenta como «sin respuesta»;
- **el orden:** pedir aprobación es la última acción de su regla, y solo al recibir. El PDF se arma solo al aprobar: nunca hay PDF de algo sin aprobar. Adjuntarlo exige una acción anterior que lo arme en la misma regla.

**Un solo motor de acciones:**
- cada respuesta conserva la revisión con la que llegó, y la captura crea sus ejecuciones en el mismo commit;
- cada acción es una ejecución, con clave respuesta + evento + regla + acción, reserva y número de turno. El efecto va fuera de la transacción;
- lo confirmado nunca se repite. Cada acción espera solo a la anterior de su regla. Si la condición no se cumple, queda «No aplica», que no es un error;
- la configuración se valida al reservar: un rol borrado o una persona que se fue dan «No se pudo», y no cuelgan nada;
- un destinatario inalcanzable no tumba a los demás. Lo sellado manda: no se copia la respuesta en cada ejecución;
- «Reintentar» retoma solo lo que no salió. Lo que la cola de WhatsApp no sabe si salió lo reintenta ella sola (80).

**Aprobación de un nivel** (60):
- decide una cuenta activa con `formapprovals.decide` y el rol de la regla, **nunca quien cargó la respuesta** (25), ni quien la corrigió;
- una sola tarea abierta por respuesta. Decidir es un solo commit; si otro decidió antes, 409. Rechazar exige un motivo de hasta 500 caracteres;
- **leer y decidir son independientes:** con `formapprovals.decide` y sin `formrecords.read`, se ve la tarea en la vista rápida, sin valores ni pantalla completa.

**«Por aprobar» en «Formularios»,** con la misma fuente que «Para resolver» (ADR 0040):
- la lista suma la columna «Por aprobar» y el filtro «Aprobación», y lo que espera decisión va primero. Cuenta solo lo que esa cuenta puede decidir, sin lo propio;
- con solo `formapprovals.decide`, ve los formularios con tareas, sin respuestas ni última respuesta;
- dentro de un formulario, «Por aprobar · N» va arriba de la planilla, de la más vieja a la más nueva. «Revisar» abre la vista rápida, y ↑ ↓ recorren esa cola, calculada en SQL, sin valores;
- la campana avisa lo que espera y lo que no se pudo mandar.

**La planilla** suma la columna «Estado» y su filtro, para los formularios que piden aprobación, compartidos por la lista, los conteos y el Excel, y la marca de la copia PDF confirmada. El tablero suma «Por estado».

**Corregir y anular** (91 a 93). Una respuesta confirmada no se modifica: queda reemplazada por su corrección, o anulada. Vale para lo de WhatsApp y lo de la web:
- **quién:** quien la cargó, mientras no esté aprobada, y siempre si el formulario no pide aprobación. Con `formrecords.correct`, cualquiera y en cualquier momento. Quien solo ve respuestas, no. Un cliente no corrige ni anula: lo hace la organización;
- **corregir:** se abre lo cargado en la versión activa, con el formulario de la E20, lo de antes a la vista y un motivo de hasta 500 caracteres. Pasa por la misma captura, con sus cálculos y condiciones. Los archivos que no se tocan se conservan;
- **el número** no cambia: es único entre las vigentes. La anterior queda en el historial tal como estaba, enlazada a su corrección y con su PDF. La nueva queda «Corregida»;
- **las reglas de «Recibida» vuelven a correr,** como una respuesta nueva. Si pide aprobación, vuelve a «Por aprobar», aunque ya estuviera decidida;
- **anular:** al final del ⋮, con motivo. Queda «Anulada», con quién, cuándo y por qué, y cierra la tarea abierta. No se borra ni se deshace;
- **lo anulado sale de las lecturas:** la planilla, los conteos, la navegación, la lista y el Excel. Se ve con el filtro «Anulada», en todos los formularios, y con «Incluir las anuladas» en el Excel. «Respuestas» pasa a contar las vigentes. Una anulada sigue siendo una respuesta: el formulario no se puede borrar;
- **en el chat,** la tarjeta muestra la marca. La copia de «Mis mensajes» no cambia: es lo que el cliente mandó.

**PDF:**
- un diseño con revisiones propias, con «Guardar diseño»: nombre, tres estilos, color, encabezado, secciones con título y orden, mostrar u ocultar preguntas, «Incluir las fotos», quién aprobó, hasta 3 firmas y pie. Sin editor libre (59);
- el logo y los datos salen de la página pública, y el color arranca con el del sistema (82). Sin página pública, va sin logo y con el nombre;
- muestra los campos «Dato», el valor de un catálogo tipado, las fotos, las líneas, los cálculos y «No se mostró». «Incluir las fotos» sin marcar dice cuántas hay;
- al aprobar se fija una **copia fija** A4, una por ejecución, que se escribe una sola vez. Cambiar reglas, catálogos o el diseño no la altera. Los textos del sistema van en el idioma de la cuenta de quien cargó; si es un cliente sin cuenta, en el de la organización;
- solo se ve por la Api, con `formrecords.read`. Se muestra con `<object>` y descarga como alternativa, nunca en un iframe con sandbox.

**Avisos por WhatsApp:**
- Forms los pide por un puerto neutral, y WhatsApp los encola en su misma cola, sin duplicados por ejecución y destinatario;
- la ventana de 24 horas se mira al aceptar y al despachar. **Fuera, todos van con una plantilla aprobada de la organización,** que elige la regla (61). Sin plantilla, queda «No se pudo», a la vista;
- las plantillas suman **un encabezado con documento,** para mandar el PDF, y la regla llena cada variable con `{número}`, el nombre del formulario o una pregunta;
- **el PDF a un cliente** (47 y 55) va al chat del contacto elegido en una pregunta de un catálogo «De los contactos». Sin contacto o borrado, «No se pudo»;
- **avisar a una persona:** la regla elige un miembro por id, y se valida al reservar;
- **quien cargó puede ser un cliente:** sigue la misma regla. **Quien cargó desde la web puede no tener dónde recibir** (89): queda «No aplica», y lo decide el canal.

**Mandar por correo** (62): a quien cargó, a un rol o a una persona, al correo de su cuenta; a una dirección fija, o al correo de una pregunta. Lleva el texto y, si se elige, el PDF. Sale por la cola de correos del núcleo, que suma los adjuntos. Un cliente sin correo queda «No se pudo». Las direcciones nunca van completas a los logs.

**La pregunta «Correo»:** un tipo nuevo, validado con la ficha de correos. Suma a todo lo que existe: el editor, el Flow (un `TextInput` de correo), el verificador, la planilla, sus filtros (`contains` y `equals`), el detalle, el Excel, las condiciones, la carga desde la web, donde ocupa dos columnas, y el PDF.

**Lo propio** (85): la planilla de quien solo carga suma el estado, el PDF y el motivo de rechazo.

**Copiar un formulario** suma el diseño del PDF y las reglas vigentes, como su primera revisión.

## Pantallas

Diseño los revisa o los dibuja con el [texto de la etapa](21-texto-diseno.md). Muestran solo lo que existe hasta acá (78).

| Tablero | Qué muestra | Estado |
| --- | --- | --- |
| `FRM-Formularios-21` y su teléfono | la lista, con «Por aprobar», y «Quien solo aprueba» | A revisar |
| `FRM-Respuesta-Aprobacion` y `WA-Chats-Avisos` | la decisión, Actividad, el PDF y los avisos | A revisar |
| `FRM-Editor-Reglas`, `M-FRM-Reglas` | las reglas, como una pestaña más | Se redibujan |
| `FRM-Pdf` | el diseño, con dos o tres estilos para elegir (59) | Se redibuja |
| `M-FRM-Respuesta-Aprobacion` | la decisión en el teléfono | Se redibuja |
| `FRM-Respuestas-21` y su teléfono | «Por aprobar · N» arriba, Estado, su filtro y «Anulada» | Por dibujar |
| `FRM-Tablero-21` | la tarjeta «Por estado» | Por dibujar |
| `FRM-Respuesta-Corregir` y su teléfono (CW10-A) | lo de antes a la vista, el motivo y «Corregida» | Por dibujar |
| `FRM-Respuesta-Anular` y su teléfono (CW11-A) | «Anular», el motivo y el Excel con las anuladas | Por dibujar |
| `FRM-Respuestas-Propias-21` y su teléfono (CW6-C) | lo propio, con estado, PDF y motivo | Por dibujar |

**Lo que se corrige:** salen «Cambios sin guardar», «Revisión N» junto a Guardar, «Vista previa en inglés» y «Falta el inglés». Las acciones nuevas se suman a `FRM-Editor-Reglas`: «Avisar a una persona», «Mandar por correo», «Mandar el PDF a un cliente» y la plantilla. `FRM-Por-Aprobar` y `M-FRM-Por-Aprobar` pasan al Archivo.

**Mandan sobre esta spec en:** «Ver todos» de «Para resolver» abre la lista con «Con algo por aprobar». «Reglas de la revisión N» se queda en Actividad, porque es un historial. El diseño guarda aparte de las reglas, y su vista previa no guarda nada.

## Contrato

Son rutas de `/api/organization/forms`, con `[Access(Access.Business)]` y `[FeatureGate("forms")]`.

| Ruta | Permiso | Pide | Devuelve |
| --- | --- | --- | --- |
| `GET /{id}/rules` | `formrules.manage` | — | `{revision, rules[], questions[]}` |
| `PUT /{id}/rules` | `formrules.manage` | `{revision, rules[]}` | 200 `{revision}`, 409 si otro guardó |
| `GET /{id}/rules/revisions` y `…/{revision}` | `formrules.manage` | `page`, `pageSize` | la lista paginada y cada revisión |
| `GET /{id}/rules/options/{roles\|members\|templates}` | `formrules.manage` | `page`, `pageSize`, `search` | `PagedResult` de cada una, sin correos |
| `GET` y `PUT /{id}/pdf-design`, `POST /{id}/pdf-design/preview` | `formrules.manage` | `{design, revision}` | el diseño, su revisión, `organization` y un PDF de ejemplo, que no guarda |
| `GET /pending` | `formapprovals.decide` | `page`, `pageSize`, `search` | `PagedResult<{id, name, pending}>` |
| `GET /{id}/approvals`, `…/{taskId}` y `…/{taskId}/continuation` | `formapprovals.decide` | `page`, `pageSize`; `direction` y `limit` | la cola, sin valores |
| `POST /{id}/records/{recordId}/decision` | `formapprovals.decide` | `{decision, reason?, version}`, con `[Idempotent]` | 200 `{version, state}` |
| `GET /{id}/records/{recordId}/workflow` | `formrecords.read` | — | `{ruleRevision?, approval?, activity[], pdf?, canCorrect, canVoid}` |
| `POST …/executions/{executionId}/retry` | `formrules.manage` | — | 200 |
| `GET /{id}/records/{recordId}/pdf` | `formrecords.read` | — | 200 PDF, `no-store` |
| `GET /{id}/records/{recordId}/correction` | `formrecords.correct` | — | la versión activa, con lo cargado |
| `POST /{id}/records/{recordId}/corrections` | `formrecords.correct` | multipart: el de la E20, más `reason`, `version` y `keepFileIds[]`, con `[Idempotent(Mode = IdempotencyMode.DurableReference)]` | 201, como la E20 |
| `POST /{id}/records/{recordId}/void` | `formrecords.correct` | `{reason, version}` | 200 `{version}` |
| `…/own-records/{recordId}/pdf`, `correction`, `corrections` y `void` | `formrecords.create` | lo mismo | lo mismo, solo con lo propio y mientras se pueda |

**Rutas que cambian:** `GET /` y `GET /overview` suman `pending?` (`null` sin `formapprovals.decide`), el filtro `approval` y el orden; `recordCount` cuenta las vigentes. `POST …/records/search`, `facets`, `navigation`, `export` y `dashboard` suman `state`, con `Voided`, y `includeVoided` en el Excel. El detalle suma `state`, `corrected`, `voided?` y `version`. `GET /{id}/records/columns` suma `approvals`.

**Puertos neutrales:**

```csharp
// Forms → WhatsApp (implementa WhatsApp)
public interface IStructuredRecordActionChannel
{
    Task<StructuredActionReach> GetReachAsync(StructuredRecipient recipient, CancellationToken ct);
    Task<Result<StructuredActionReceipt>> SendAsync(StructuredActionMessage message, CancellationToken ct);
    Task<IReadOnlyList<StructuredActionState>> ReadStatesAsync(IReadOnlyCollection<Guid> keys, CancellationToken ct);
}
```

`StructuredActionMessage(Key, Recipient, Text, Template?, Document?)`. `Key` es el id de ejecución y destinatario. Sin WhatsApp, `GetReachAsync` dice que no hay canal. El correo usa `IOutbox`, con el canal de correos.

**Errores** (con sus textos en `FormsTexts.resx` y `.en.resx`):

| Código | Tipo | Cuándo |
| --- | --- | --- |
| `Forms.Rules.Invalid` | Validation (`rules[i]…`) | un evento, condición, acción, rol, persona, plantilla o pregunta fuera de regla, o el orden de las acciones |
| `Forms.Rules.TooMany` | Validation (`rules`) | más de 20 reglas, 10 condiciones o 10 acciones |
| `Forms.PdfDesign.Invalid` | Validation | nombre, pie, firmas o preguntas fuera de regla |
| `Forms.Approval.NotFound` | NotFound | la tarea no existe o no se puede ver |
| `Forms.Approval.AlreadyDecided` | Conflict | otro decidió antes |
| `Forms.Approval.OwnRecord` | Forbidden | la cargó o la corrigió quien decide |
| `Forms.Approval.RoleRequired` | Forbidden | no tiene el rol de la tarea |
| `Forms.Approval.ReasonRequired` | Validation (`reason`) | rechazar sin motivo |
| `Forms.Record.NotCorrectable` | Conflict | ya aprobada, para quien solo la cargó, o anulada |
| `Forms.Record.Superseded` | Conflict | corregir o anular una que ya se reemplazó |
| `Forms.Record.AlreadyVoided` | Conflict | ya está anulada |
| `Forms.Record.ReasonRequired` | Validation (`reason`) | corregir o anular sin motivo |
| `Forms.Record.PdfNotFound` | NotFound | no hay copia, o falta el archivo |
| `Forms.Execution.NotRetryable` | Conflict | lo que no quedó en «No se pudo» |

**Fallas de una ejecución,** que se ven en Actividad: `WindowClosed`, `TemplateUnavailable`, `NoConversation`, `NoContact`, `ContactDeleted`, `NotMember`, `RoleMissing`, `NoEmail`, `NoAnswer` y `NoPdf`.

**Permisos nuevos:** `formrules.manage`, `formapprovals.decide` y `formrecords.correct`, con sus textos y la migración de los roles de sistema.

## Datos

Tres migraciones: `<fecha>_FormRules.cs`, `<fecha>_FormRecordCorrections.cs` y `<fecha>_SystemRoleFormRulesPermissions.cs`. Todo es `ITenantOwned`, esquema `tenant`, con RLS, salvo el registro de trabajo.

| Tabla | Qué guarda | Regla |
| --- | --- | --- |
| **FormRuleSets** | `DefinitionId`, `Revision`, `Rules` jsonb, quién y cuándo | inmutable; único por los dos |
| **FormPdfDesigns** | `DefinitionId`, `Revision`, `Design` jsonb, quién y cuándo | inmutable; único por los dos |
| **FormApprovalTasks** (`IVersioned`) | `RecordId`, `DefinitionId`, `RoleId`, `State`, `RequestedAtUtc`, decisión, motivo | una abierta por respuesta, con índice único filtrado; índice por definición, rol, fecha e id |
| **FormActionExecutions** | `RecordId`, `RuleSetId`, `Event`, `RuleId`, `ActionId`, `Position`, `Kind`, `State`, `FailureCode?`, `Fence`, `LeaseExpiresAtUtc`, `NextAttemptAtUtc` | único por respuesta, evento, regla y acción |
| **FormActionRecipients** | `ExecutionId`, `Kind`, `UserId?`, `ContactId?`, `Address?` cifrada, `State`, `FailureCode?`, `RenderedText?` | uno por destinatario; el error de uno no toca a los otros |
| **FormRecordDocuments** | `RecordId`, `ExecutionId`, `DesignId`, `StorageKey`, `FileName`, `ByteLength`, `Sha256`, `Culture` | único por ejecución; se escribe una vez |
| **platform.FormActionWorks** | `TenantId`, `NextAtUtc`, `LeaseExpiresAtUtc` | registro técnico global, sin datos de negocio |

**Columnas nuevas:**
- `FormDefinitionStates`: `ActiveRuleSetId` y `ActivePdfDesignId`, por T7;
- `FormRecords`: `RuleSetId?`, `State`, `Version` (`IVersioned`), `CorrectionOfRecordId?`, `CorrectionReason?`, `SupersededAtUtc?`, y `VoidedAtUtc?`, `VoidedByUserId?` y `VoidReason?`. **El índice único `(DefinitionId, Number)` pasa a filtrar `SupersededAtUtc IS NULL`,** y las lecturas de la E15 filtran las vigentes;
- en WhatsApp, las plantillas suman `HeaderFormat`, y el mensaje de chat, el origen «acción de formulario».

## Pasos

Abreviaturas: `D/`, `A/`, `I/` y `P/` son las capas del back; `F/`, el front; `M/`, la maqueta, y `[E14]` a `[E20]`, un archivo que deja esa etapa.

**Back:**

| # | Qué hace | Archivos | Copia de | Test |
| --- | --- | --- | --- | --- |
| B1 | Reglas: dominio, validación y revisiones | `D/Forms/FormRuleSet.cs`, `A/Modules/Forms/Services/FormRuleService.cs`, `FormRulesPolicy.cs` | `M/…/lib/rules.ts`; `FormDefinitionService.cs` [E14] | cada motivo; 409; lista vacía; no toca el borrador |
| B2 | Permisos, tablas y migraciones | `Permissions.cs`, `Permissions{,.en}.resx`; `I/…/Configurations/`; las migraciones | `20261010203618_SystemRoleAssistantPermissions.cs` | aislamiento; hacia atrás; `PermissionTextsTests` |
| B3 | El motor: crear ejecuciones en la captura, reservar, correr y confirmar | `IFormActionService.cs`, `FormActionService.cs`, `FormActionPlanner.cs`; suma a `FormRecordService.cs` [E14] | `AssistantTurnService.cs` | clave única; «No aplica»; orden dentro de la regla; un turno viejo no escribe; condición en memoria igual que en SQL |
| B4 | El worker | `I/Modules/Forms/FormActionWorker.cs` | `AssistantTurnWorker.cs`, `TenantJobRunner.cs` | una vuelta de su bucle; una falla no frena a las demás |
| B5 | Aprobación | `D/Forms/FormApprovalTask.cs`, `FormApprovalService.cs` | `AssistantConversationService.cs` | la propia; sin el rol; dos a la vez; un 403 de leer conserva la tarea |
| B6 | «Por aprobar»: lista, cola, «Para resolver» y campana | suma a `IFormReader.cs` y `FormReader.cs` [E14]; `FormApprovalPendingItems.cs` | `OrganizationUserPendingItems.cs` | una sola fuente; `/pending`; la continuación en SQL; lo propio no cuenta |
| B7 | Estado, anuladas y vigentes en lo que ya existe | suma a `FormRecordQueryFilters.cs`, `FormRecordSheetBuilder.cs` y `FormDashboardBuilder.cs` [E15] | esos mismos | el filtro en los cinco lugares; conteos; Excel con anuladas |
| B8 | PDF: diseño, vista previa y copia fija | `FormPdfDesignService.cs`, `IFormPdfRenderer.cs`, `I/Modules/Forms/FormPdfRenderer.cs` | `AssistantExportRenderer.cs` | cada contenido; sin página pública; la copia no cambia; fuentes embebidas |
| B9 | Avisos: el puerto y su lado WhatsApp | `A/Interfaces/Integrations/StructuredData/IStructuredRecordActionChannel.cs`, su apagado, `WhatsAppStructuredRecordActionChannel.cs`; suma a `WhatsAppOutboundMessage.cs` y `WhatsAppCloudClient.cs` | `WhatsAppReplyIssuer.cs` | ventana al aceptar y al despachar; plantilla; documento; un destinatario roto; sin duplicados |
| B10 | Plantillas con documento y variables | suma a `WhatsAppTemplateBase.cs` y `WhatsAppTemplateClient.cs` | esos mismos | el encabezado que va a Meta; rechazo |
| B11 | Correo con adjunto | suma a `EmailMessage.cs` y al canal de correos; `FormEmailActionHandler.cs` | `IChannelSender.cs` | adjunto por clave, leído al despachar; dirección enmascarada en logs |
| B12 | Pregunta «Correo» | suma a `FormQuestionType.cs`, `WhatsAppFlowBuilder.cs`, el verificador y las lecturas | los mismos [E14 y E15] | válido e inválido; filtros; web |
| B13 | Corregir y anular | `FormRecordCorrectionService.cs`, `FormRecordVoidService.cs`; suma a `FormEntrySubmitService.cs` [E20] | esos mismos | quién; el número se conserva; archivos conservados; reglas de nuevo; vuelve a «Por aprobar»; 409 entre dos; tarea cerrada |
| B14 | Lo propio y copiar | suma a las lecturas `own-records` [E20] y `FormDefinitionCopier.cs` [E17] | esos mismos | motivo de rechazo; lo ajeno = 404; primera revisión |
| B15 | Rutas y contratos | `P/Modules/Forms/Controllers/BusinessFormRulesController.cs`, `BusinessFormApprovalsController.cs`, `Contracts/*`, `FormsContractContributor.cs` | los de la E15 | cada ruta con 401, 403, feature apagada = 404, 409; ejemplos exportados |

**Front:**

| # | Qué hace | Archivos | Copia de | Test |
| --- | --- | --- | --- | --- |
| F1 | Tipos y llamadas | `api/workflow.ts` | `M/…` | zod estricto con los ejemplos |
| F2 | La pestaña de reglas | `components/rules/`, `lib/rules.ts` | `M/…` | guardar, historial, 409 y salir |
| F3 | El diseño del PDF | `pages/PdfDesignPage.tsx`, `components/pdf/PdfPage.tsx` | `M/…` | estilos; vista previa sin guardar; `<object>` |
| F4 | «Por aprobar» | suma a la lista y a `FormRecordsPage.tsx` [E15] | `M/…` | las tres combinaciones; la cola con ↑ ↓ |
| F5 | La decisión y Actividad | `components/records/Workflow.tsx`; suma a `QuickView.tsx` y `RecordFullPage.tsx` | `M/…` | solo decide; Reintentar; se relee mientras sale |
| F6 | Corregir y anular | `components/records/CorrectionForm.tsx`, con los componentes de la E20 | el tablero | lo de antes; motivo; 409; filtro «Anulada» |
| F7 | Lo propio, tablero y tarjeta | suma a `FormRecordsPage.tsx`, `FormDashboard.tsx` y la tarjeta | `M/…` | estado, PDF y motivo |

**Al cerrar:** `docs/features/formularios.md` y `whatsapp.md` suman su sección, y la guía de correos, los adjuntos.

## Qué verificar al arrancar

| Marca | Qué hay que confirmar | Qué asume esta spec |
| --- | --- | --- |
| [Meta] | Antes de programar: que una plantilla acepte un documento en el encabezado, y cómo se da de alta. | Sale con una carga previa de ejemplo. |
| [E14] | La captura. | Las ejecuciones entran en su commit. |
| [E15] | La función de filtros y `FormRecords`. | Filtrar las vigentes en un solo lugar alcanza. |
| [E20] | La captura web y `sameOperation`. | Una corrección usa las dos. |
| Núcleo | El canal de correos y `EmailMessage`. | Suma adjuntos por clave, sin bytes en el outbox. |
| Núcleo | Cómo leer las identidades de rol de una cuenta. | Un lector del núcleo las da, sin que Forms lea `identity`. |
| Base de la 12 | La campana. | Un módulo le aporta avisos. |
| Página pública | El logo y los datos. | Se leen por un puerto del núcleo. |

## Lo que ya se pagó allá (no repetir)

Cada punto lleva su test, escrito durante la etapa:
1. **Un rol borrado colgaba una aprobación:** se valida al reservar.
2. **Chrome bloqueó el visor del PDF** en un iframe con sandbox: va con `<object>`.
3. **El contrato de `origin` del recibo:** la web rechazó una respuesta correcta; cada campo nuevo se exporta con su ejemplo.
4. **Un aviso a un rol se repetía** por una reserva vencida: la clave va por ejecución y destinatario.
5. **Una copia entera de la respuesta por ejecución:** se lee por id.

## Decisiones tomadas (no se vuelven a preguntar)

- **Un solo motor,** sin una segunda cola de salida.
- **El rol** delimita quién decide, y nunca reemplaza al permiso.
- **Aprobación de un nivel** (60), y **quien aprueba** es la 25.
- **PDF con PDFsharp-MigraDoc,** como el Asistente: no se suma otra biblioteca de PDF.
- **Sin `ExecutionActor` en el worker:** quien respondió puede ser un cliente sin cuenta.
- **Lo propio tiene sus rutas,** como en la E20.

## Fuera de esta etapa

- Varios niveles o varios aprobadores.
- Acciones de otros módulos y compensaciones.
- El asistente, las sugerencias y armar una regla desde un texto (E23).

## Cómo se cierra

La puerta de la etapa, con la lista de control de `/puerta`, más una **prueba real con Meta y el almacenamiento**:
1. llega un aviso al recibir;
2. otra cuenta con el rol aprueba desde la lista y desde «Para resolver»;
3. el PDF con el logo, la foto, los campos «Dato», los renglones y el total se abre en el celular, y las acciones quedan en «Hecho»;
4. una cuenta en inglés recibe el aviso y el PDF, con los textos del sistema en inglés;
5. un aviso fuera de las 24 horas llega con la plantilla de la organización;
6. un correo con el PDF llega a una dirección fija y al correo escrito en el formulario;
7. corregir una aprobada: conserva el número, vuelve a «Por aprobar», y al aprobar sale un PDF nuevo;
8. anular una con aprobación abierta: la tarea se cierra, sale de la planilla, los conteos y el Excel, y aparece con «Anulada».

## Requisitos previos

- La E20 cerrada, y el almacenamiento de la E10.
- La prueba con Meta de una plantilla con documento en el encabezado (47 y 61).

## En el multitenant

- **Datos de la organización:** todo con RLS, salvo `platform.FormActionWorks`.
- **Workers:** el de acciones descubre las organizaciones con trabajo y entra con `TenantJobRunner`.
- **Feature `forms`:** las rutas la llevan.
- **WhatsApp:** los avisos salen por el número de la organización.
- **Almacenamiento:** los PDF van en `IFileStore`, con la categoría `forms`.
- **«Para resolver»:** un `IPendingGroupSource` del módulo.
- **Cuotas:** las rutas cuentan en las cuotas generales.
