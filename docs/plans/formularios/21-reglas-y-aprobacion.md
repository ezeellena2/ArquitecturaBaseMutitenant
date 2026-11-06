# E21 · Reglas, aprobación, PDF y avisos (spec)

**Estado:** pendiente · spec de la etapa 21 del bloque (decisiones 34, 78, 80, 82, 91 a 93, 100, 106, 113 y 114), con el [formato](plantilla.md) de las etapas. Corresponde a la etapa 4 de ArquitecturaBase, con piloto real, y a la etapa 3 de su carga desde la app, decidida y sin programar. `/plan-etapa` la contrasta con el código real cuando le toque, después de la E20.

**Partes.** Esta spec no entra en un solo archivo, así que sigue en otro, con las mismas secciones. Lo que no está en la tabla está acá.

| Secciones | Archivo |
| --- | --- |
| Contrato, Datos y «Lo que ya se pagó allá» | [datos y contrato](21-parte-datos-y-contrato.md) |

## Qué leer

Antes de empezar, en este orden:

1. **Lo anterior:** la [spec de la E14](14-formularios.md): T4, T5, T7 y la §6, punto 7; la [E15](15-respuestas.md): lista, planilla y detalle; la [E17](17-ciclo-de-vida.md): copiar y borrar; la [E18](18-fotos-y-documentos.md): archivos; la [E19](19-condiciones-calculos-y-lineas.md): condiciones, y la [E20](20-cargar-desde-la-web.md): la captura y lo propio.
2. **Decisiones** de la hoja de ruta: 25, 35, 42, 43, 47, 49, 55, 59 a 62, 80, 82, 85, 89, 91 a 93, 100, 106, 109, 113, 114, 115 y 116. Y la [ADR 0040](../../decisions/0040-para-resolver.md).
3. **Fichas:** [guardado](../../rules/guardado.md), [concurrencia](../../rules/concurrencia.md), [idempotencia](../../rules/idempotencia.md), [modulos](../../rules/modulos.md), [multitenancy](../../rules/multitenancy.md), [permisos](../../rules/permisos.md), [persistencia-ef](../../rules/persistencia-ef.md), [auditoria](../../rules/auditoria.md), [emails](../../rules/emails.md), [textos-libres](../../rules/textos-libres.md), [textos-y-traducciones](../../rules/textos-y-traducciones.md), [fechas-y-zonas](../../rules/fechas-y-zonas.md), [logs](../../rules/logs.md), [api-http](../../rules/api-http.md) y [tests](../../rules/tests.md).
4. **Código para copiar:** la columna «Copia de» de los [pasos](#pasos).
5. **Referencia de producto,** solo para leer: en [`../2026-10-17-formularios-traspaso/`](../2026-10-17-formularios-traspaso/README.md), el anexo C, «Etapa 4», el anexo B, «Etapa 4», y el anexo D (G1 a G6); en `../ArquitecturaBase/docs/plans/2026-10-23-traspaso-multitenant/`, el anexo C §5 y §6 (L-05 a L-08), y en ArquitecturaBase, `docs/plans/2026-10-29-cargar-formularios-desde-la-app.md`, «Corregir y anular».

## Qué se construye

**Reglas** «Cuando llega una respuesta»:
- cada guardado crea una revisión inmutable, con 409. No crea versión ni republica, y guardar una lista vacía las apaga;
- **cada regla:** un evento (Recibida, Aprobada o Rechazada), hasta 10 condiciones unidas con «y», con la gramática de la E15, y hasta 10 acciones en orden. Hasta 20 reglas;
- **las acciones:** pedir aprobación a un rol, armar el PDF, mandarle un mensaje a quien la cargó, avisar a un rol, avisar a una persona (62), mandar por correo (62) y mandar el PDF a un cliente (47);
- **el mensaje con datos** (149): «Agregar dato» abre una lista con buscador, con el número, el motivo del rechazo y las preguntas, sin fotos ni documentos, y el dato entra como una etiqueta que se borra entera. «Así llega» muestra el mensaje con datos de ejemplo. Se guarda `{FieldId}`, nunca el nombre, y una pregunta que ya no está se ve «Pregunta no disponible» y no deja guardar (`Forms.Rules.Invalid`). Es un solo texto (35). Una condición sobre una pregunta que no existía o no se mostró cuenta como «sin respuesta»;
- **los renglones** (146): una condición sobre una pregunta del bloque se cumple si algún renglón la cumple, y varias del mismo bloque buscan un renglón que las cumpla todas («Algún renglón de Artículos con ¿Llegó en buen estado? es No»). En el mensaje, el bloque es un dato con todos los renglones, uno por línea;
- **el orden:** pedir aprobación es la última acción de su regla, y solo al recibir. **El PDF** (106): si alguna regla pide aprobación, se arma solo al aprobar, y lo que espera aprobación no tiene PDF; si no, se arma al recibir. Adjuntarlo exige una acción anterior que lo arme en la misma regla;
- cada acción se ordena con ↑ ↓ y se quita con 🗑, a la vista, como en ArquitecturaBase (135);
- **cómo se lee una regla guardada** (138): en pasos, cada uno con su ícono: el cuándo (una flecha al recibir, una tilde al aprobar, un círculo tachado al rechazar); «Si», con una condición por renglón, la pregunta y el valor en negrita, y cada acción, con su dato aparte en una etiqueta (el rol, «con el PDF») y el mensaje entero en su recuadro;
- **el historial** (140): «Historial» abre un panel al costado con las revisiones, de la más nueva a la más vieja, y cada una se lee en pasos. Sin revisiones queda apagado, con «Todavía no hay revisiones.» en su tooltip.

**«Adjuntar el PDF»** (113). Las acciones que mandan un mensaje o un correo llevan esa casilla, y «Mandar el PDF a un cliente» lo manda siempre:
- **nada se desmarca solo:** ni al mover, quitar o cambiar una acción, ni al cambiar el evento;
- **si «Armar el PDF» falta o está después,** debajo de la casilla aparece en el momento «Para adjuntarlo, «Armar el PDF» tiene que ir antes de este mensaje.». Debajo, un botón lo arregla: «Subir "Armar el PDF"» pone el primero que está después justo antes del mensaje; si no hay ninguno, «Agregar "Armar el PDF" antes» lo agrega ahí, y el foco vuelve a la casilla. Con 10 acciones, «Agregar» queda apagado, con el motivo en su tooltip;
- **cuándo se puede** (106): si el formulario pide aprobación, el PDF existe solo al aprobar. En otro evento, la casilla sin marcar se apaga, con el motivo en su tooltip. Marcada no se apaga: dice lo mismo debajo, y la regla no se guarda hasta sacarla o cambiar el evento. Sin aprobación, vale al recibir;
- **«Mandar el PDF a un cliente»** no tiene casilla: el aviso y el arreglo van debajo de la acción;
- **cambiar el tipo de una acción conserva lo escrito:** el mensaje, el rol, la persona y el adjunto vuelven si se elige otra vez el tipo anterior. Al guardar, cada dato va solo con el tipo que lo usa.

**Un solo motor de acciones,** con ejecuciones idempotentes y el efecto fuera de la transacción: está en la [parte de datos y contrato](21-parte-datos-y-contrato.md#el-motor-de-acciones).

**Aprobación de un nivel** (60):
- decide una cuenta activa con `formapprovals.decide` y el rol de la regla, **nunca quien cargó la respuesta** (25), ni quien la corrigió;
- una sola tarea abierta por respuesta. Decidir es un solo commit; si otro decidió antes, 409. Rechazar exige un motivo de hasta 500 caracteres;
- **leer y decidir son independientes:** con `formapprovals.decide` y sin `formrecords.read`, se ve la tarea en la vista rápida, sin valores ni pantalla completa (100). Su cola es la vista «Por aprobar» (148): el número, la fecha y el rol;
- **«Aprobar» pide confirmar** (114): un diálogo como el de rechazar, «¿Aprobar VIS-000003?», con quién la cargó y cuándo; quien solo aprueba ve solo cuándo (100). El foco empieza en «Cancelar», así Enter no aprueba, y mientras manda espera abierto con «Aprobando…» (115);
- **sin el rol de la tarea:** quien tiene el permiso y no el rol, como una cuenta administradora, ve el detalle sin error. `approval.canDecide` dice si puede decidir, y el detalle pide la tarea solo entonces; si no, la vista rápida muestra la decisión de solo lectura (el rol, quién decidió y cuándo), y la pantalla completa no reserva la barra de decisión.

**«Por aprobar» en «Formularios»,** con la misma fuente que «Para resolver» (ADR 0040):
- la lista suma la columna «Por aprobar» y el filtro «Aprobación», y lo que espera decisión va primero. Cuenta solo lo que esa cuenta puede decidir, sin lo propio;
- con solo `formapprovals.decide`, ve los formularios con tareas, sin respuestas ni última respuesta;
- **la vista «Por aprobar»** (148): arriba de la planilla, quien puede decidir ve «Por aprobar (N) | Todas». «Por aprobar» es una tabla corta (N°, Cargó, Recibida, Rol y «Revisar»), de la más vieja a la más nueva, sin filtros ni valores; «Revisar» abre la vista rápida y ↑ ↓ recorren esa cola. Se abre ahí si hay algo para decidir, y la vista va en la dirección. Quien solo aprueba la ve sin Cargó (100). La campana y «Para resolver» abren la más vieja.

**La planilla** suma la columna «Estado», en los formularios que piden aprobación, y la marca de la copia PDF confirmada. El filtro «Estado» va en todos: con aprobación, Recibida, Por aprobar, Aprobada y Rechazada, y siempre, «Anulada». Lo comparten la lista, los conteos y el Excel. El tablero suma «Por estado».

**La actividad de una respuesta** (139): cuatro columnas fijas, Fecha, Qué pasó, Estado y Acciones, en bandas por momento («Al recibirla», «Aprobación», «Al aprobarla» o «Al rechazarla»). Quién y el detalle van debajo de qué pasó, en gris, y el estado y las acciones («Ver PDF», «Reintentar»), siempre en su columna. El PDF va en su renglón, debajo de «Armar el PDF», y cada persona de un aviso a un rol, en el suyo, con sus acuses. En la vista rápida las acciones van debajo del texto; en el teléfono, también el estado.

**Corregir y anular** (91 a 93). Una respuesta confirmada no se modifica: queda reemplazada por su corrección, o anulada. Vale para lo de WhatsApp y lo de la web:
- **quién:** quien la cargó, mientras no esté aprobada, y siempre si el formulario no pide aprobación. Con `formrecords.correct`, cualquiera y en cualquier momento; corregir lo ajeno pide también `formrecords.read`, porque no se corrige lo que no se ve. Quien solo ve respuestas, no. Un cliente no corrige ni anula: lo hace la organización;
- **corregir:** se abre lo cargado en la versión activa, con el formulario de la E20, lo de antes a la vista y un motivo de hasta 500 caracteres. Pasa por la misma captura, con sus cálculos y condiciones. Los archivos que no se tocan se conservan. Las listas usan los filtros por persona de quien la cargó, no los de quien corrige. Vale también en un formulario dado de baja o solo para clientes: `Forms.Entry.Unavailable` no aplica (93);
- **quién queda:** la respuesta sigue siendo de quien la cargó. Conserva `Respondent*`, sus empresas, el canal y `SubmittedAtUtc`, así «Cargó» no cambia. `CorrectedByUserId` y `CorrectedAtUtc` dicen quién la corrigió, y se ven en el detalle y en Actividad. El `SourceKey` es `correction:<clave de idempotencia>`;
- **el número** no cambia: es único entre las vigentes. La anterior queda en el historial tal como estaba, enlazada a su corrección y con su PDF. La nueva queda «Corregida». **Leer por id incluye las reemplazadas:** el historial abre la anterior, y la tarjeta del chat la encuentra;
- **las reglas de «Recibida» vuelven a correr,** como una respuesta nueva. Si pide aprobación, vuelve a «Por aprobar», aunque ya estuviera decidida. Los avisos salen otra vez, lo ya avisado, aprobado o impreso queda en la historia, y la tarea abierta de la anterior se cierra sin decisión;
- **anular:** al final del ⋮, con motivo. Queda «Anulada», con quién, cuándo y por qué, y cierra la tarea abierta. No se borra ni se deshace;
- **lo anulado sale de las lecturas:** la planilla, los conteos, la navegación, la lista y el Excel. Se ve con el filtro «Anulada», en todos los formularios, y con «Incluir las anuladas» en el Excel. «Respuestas» pasa a contar las vigentes, nunca desde el último número. Una anulada sigue siendo una respuesta: el formulario no se puede borrar;
- **en el chat,** la tarjeta muestra la marca. La copia de «Mis mensajes» no cambia: es lo que el cliente mandó.

**PDF:**
- un diseño con revisiones propias, con «Guardar diseño»: nombre, tres estilos, color, encabezado, secciones con título y orden, mostrar u ocultar preguntas, «Incluir las fotos», quién aprobó, hasta 3 firmas y pie. Sin editor libre (59);
- el logo y los datos salen de la página pública, y el color arranca con el del sistema (82). Sin página pública, va sin logo y con el nombre;
- muestra los campos «Dato», el valor de un catálogo tipado, las fotos, los renglones en una tabla, los cálculos y «No se mostró». «Incluir las fotos» sin marcar dice cuántas hay;
- al armarlo se fija una **copia fija** A4, una por ejecución, que se escribe una sola vez. Cambiar reglas, catálogos o el diseño no la altera. Los textos del sistema van en el idioma de la cuenta de quien cargó; si es un cliente sin cuenta, en el de la organización;
- solo se ve por la Api, con `formrecords.read`, con `<object>` y descarga como alternativa.

**Los avisos por WhatsApp y los correos** (61 y 62) están en la [parte de datos y contrato](21-parte-datos-y-contrato.md#los-avisos-y-los-correos).

**La pregunta «Correo»:** un tipo nuevo, validado con la ficha de correos. Suma a todo lo que existe: el editor, el Flow (un `TextInput` de correo), el verificador, la planilla, sus filtros (`contains` y `equals`), el detalle, el Excel, las condiciones, la carga desde la web, donde ocupa una columna (141), y el PDF.

**Lo propio** (85): la planilla de quien solo carga suma el estado, el PDF y el motivo de rechazo, sin el rol ni quién decidió; su detalle no tiene Actividad.

**Copiar un formulario** suma el diseño del PDF y las reglas vigentes, como su primera revisión.

## Pantallas

Aprobados y publicados en el lienzo v155, con el armado del front real de ArquitecturaBase (126 y 135) y los recorridos guiados `armar-las-reglas` y `aprobar-o-rechazar`. Con el traspaso 5 se redibujan las reglas, la planilla, el detalle y la carga con «Correo» (138 a 149). El ejemplo es «Visita a cliente», versión 4, que pide aprobación al rol Supervisor, con tres por aprobar. Muestran solo lo que existe hasta acá (78).

| Tablero | Qué muestra | Estado |
| --- | --- | --- |
| `FRM-Formularios-21` y su teléfono | la lista de la 20, con «Cargar», «Por aprobar» y «Quien solo aprueba» | Aprobado |
| `FRM-Editor-Reglas`, `M-FRM-Reglas` | las reglas, como una pestaña más, con su historial, las siete acciones, «Adjuntar el PDF» (113) y la plantilla para fuera de las 24 horas | Se redibujan: en pasos, con el historial detrás de su botón y «Agregar dato» (138, 140 y 149) |
| `FRM-Pdf` | el diseño, con tres estilos: Clásico, Moderno y Compacto (59), el color, el encabezado de la página pública (82), secciones y firmas | Aprobado |
| `FRM-Respuesta-Aprobacion` y su teléfono | la decisión, con «Aprobar» que pide confirmar (114), la actividad y el PDF | Se redibujan: la actividad en columnas fijas y la decisión de solo lectura (139) |
| `FRM-Respuestas-21` y su teléfono | sobre `FRM-Respuestas-20`: «Estado», su filtro con «Anulada», la marca del PDF, la cola desde la campana y «Quien solo aprueba» (100) | Se redibujan: la vista «Por aprobar» y «Filtros» (144 y 148) |
| `FRM-Tablero-21` y su teléfono | la tarjeta «Por estado» | Aprobados |
| `FRM-Respuesta-Corregir` y su teléfono | lo de antes a la vista, el motivo, «Corregida» y la versión anterior | Aprobados |
| `FRM-Respuesta-Anular` y su teléfono | «Anular» al final del ⋮, el motivo, el filtro «Anulada» y el Excel con las anuladas | Aprobados |
| `FRM-Respuestas-Propias-21` y su teléfono | lo propio, con estado, PDF y motivo del rechazo, y corregir lo suyo mientras no esté aprobado | Aprobados |
| `FRM-Editor-21` y `FRM-Cargar-Respuesta-21`, con sus teléfonos | la pregunta «Correo» | Aprobados; `FRM-Cargar-Respuesta-21` se redibuja en dos anchos (141) |
| `Campana-21` y su teléfono | «3 respuestas por aprobar» y «No se pudo mandar el aviso de …» | Aprobados |
| `WA-Chats-Avisos` y su teléfono | los avisos, con «Sin plantilla aprobada» (61) | Aprobados |
| `FRM-Dar-De-Baja-21` y `FRM-Editor-Baja-21` | «Lo que se conserva» suma el PDF y las aprobaciones, y lo que espera se sigue decidiendo | Aprobados |

`FRM-Por-Aprobar` y `M-FRM-Por-Aprobar` están en el Archivo.

**Mandan sobre esta spec en:** «Ver todos» de «Para resolver» abre la lista con «Con algo por aprobar». «Reglas de la revisión N» se queda en Actividad, porque es un historial. El diseño guarda aparte de las reglas, y su vista previa no guarda nada. Las reglas y el diseño valen sin publicar: lo dice la (i) de su título, sin leyendas (109 y 116).

## Pasos

Abreviaturas: `D/`, `A/`, `I/` y `P/` son las capas del back; `F/`, el front, y `[E14]` a `[E20]`, un archivo que deja esa etapa.

**Back:**

| # | Qué hace | Archivos | Copia de | Test |
| --- | --- | --- | --- | --- |
| B1 | Reglas: dominio, validación y revisiones | `D/Forms/FormRuleSet.cs`, `A/Modules/Forms/Services/FormRuleService.cs`, `FormRulesPolicy.cs` | `FormDefinitionService.cs` [E14] | cada motivo; el PDF al recibir, con y sin aprobación; 409; lista vacía; no toca el borrador |
| B2 | Permisos, tablas y migraciones | `Permissions.cs`, `Permissions{,.en}.resx`; `I/…/Configurations/`; las migraciones | `20261010203618_SystemRoleAssistantPermissions.cs` | aislamiento; hacia atrás; `PermissionTextsTests` |
| B3 | El motor: crear ejecuciones en la captura, reservar, correr y confirmar | `IFormActionService.cs`, `FormActionService.cs`, `FormActionPlanBuilder.cs`; suma a `FormRecordService.cs` [E14] | `AssistantTurnService.cs` | clave única; «No aplica»; orden dentro de la regla; un turno viejo no escribe; condición en memoria igual que en SQL |
| B4 | El worker | `I/Modules/Forms/FormActionWorker.cs` | `AssistantTurnWorker.cs`, `WhatsAppInboundProcessor.cs` | una vuelta de su bucle; una falla no frena a las demás; un aviso en un chat vinculado escribe la copia compartida; la lista de `TenantScopeUsageTests` |
| B5 | Aprobación | `D/Forms/FormApprovalTask.cs`, `FormApprovalService.cs` | `AssistantConversationService.cs` | la propia; sin el rol; dos a la vez; un 403 de leer conserva la tarea; decidir sin `formrecords.read` |
| B6 | «Por aprobar»: lista, cola, «Para resolver» y campana | suma a `IFormReader.cs` y `FormReader.cs` [E14]; `FormApprovalPendingItems.cs` | `OrganizationUserPendingItems.cs` | una sola fuente; `/pending`; la continuación en SQL; lo propio no cuenta |
| B7 | Estado, anuladas y vigentes en lo que ya existe | suma a `FormRecordQueryFilters.cs`, `FormRecordSheetBuilder.cs` y `FormDashboardBuilder.cs` [E15] | esos mismos | el filtro en los cinco lugares; conteos; Excel con anuladas |
| B8 | PDF: diseño, vista previa y copia fija | `FormPdfDesignService.cs`, `IFormPdfRenderer.cs`, `I/Modules/Forms/FormPdfRenderer.cs` | `AssistantExportRenderer.cs` | cada contenido; sin página pública; la copia no cambia; fuentes embebidas |
| B9 | Avisos: el puerto y su lado WhatsApp | `A/Interfaces/Integrations/StructuredData/IStructuredRecordActionChannel.cs`, su apagado, `WhatsAppStructuredRecordActionChannel.cs`; suma a `WhatsAppOutboundMessage.cs` y `WhatsAppCloudClient.cs` | `WhatsAppReplyIssuer.cs` | ventana al aceptar y al despachar; plantilla; documento; un destinatario roto; sin duplicados |
| B10 | Plantillas con documento y variables | suma a `WhatsAppTemplateBase.cs` y `WhatsAppTemplateClient.cs` | esos mismos | el encabezado que va a Meta; rechazo |
| B11 | Correo con adjunto | suma a `EmailMessage.cs` y al canal de correos; `FormEmailActionNotifier.cs` | `IChannelSender.cs` | adjunto por clave, leído al despachar; dirección enmascarada en logs |
| B12 | Pregunta «Correo» | suma a `FormQuestionType.cs`, `WhatsAppFlowBuilder.cs`, el verificador y las lecturas | los mismos [E14 y E15] | válido e inválido; filtros; web |
| B13 | Corregir y anular | `FormRecordCorrectionService.cs`, `FormRecordVoidService.cs`; suma a `FormEntrySubmitService.cs` [E20] | esos mismos | quién; «Cargó» no cambia, también con un cliente; el número se conserva; archivos conservados; reglas de nuevo; vuelve a «Por aprobar»; 409 entre dos; tarea cerrada; la anterior se abre por id |
| B14 | Lo propio y copiar | suma a las lecturas `own-records` [E20] y `FormDefinitionCopyCreator.cs` [E17] | esos mismos | motivo de rechazo; lo ajeno = 404; primera revisión |
| B15 | Rutas y contratos | `P/Modules/Forms/Controllers/BusinessFormRulesController.cs`, `BusinessFormApprovalsController.cs`, `Contracts/*`, `FormsContractContributor.cs` | los de la E15 | cada ruta con 401, 403, feature apagada = 404, 409; ejemplos exportados |
| B16 | La baja de la cuenta | suma a `FormsAccountDeletionParticipant.cs` y a su cleanup [E14] | `AssistantAccountDeletionParticipant.cs` | la dirección y el texto armado de esa cuenta se borran; la ejecución queda |

**Front:**

| # | Qué hace | Archivos | Copia de | Test |
| --- | --- | --- | --- | --- |
| F1 | Tipos y llamadas | `api/workflow.ts` | la API de la E20 | zod estricto con los ejemplos |
| F2 | La pestaña de reglas | `components/rules/`, `lib/rules.ts` | su tablero | guardar, el historial detrás de su botón, 409 y salir; las reglas guardadas en pasos; «Agregar dato», «Así llega» y «Pregunta no disponible»; «Algún renglón con…»; la casilla no se desmarca sola; subir y agregar «Armar el PDF»; cambiar el tipo conserva lo escrito y guarda solo lo que usa |
| F3 | El diseño del PDF | `pages/PdfDesignPage.tsx`, `components/pdf/PdfPage.tsx` | su tablero | estilos; vista previa sin guardar; `<object>` |
| F4 | «Por aprobar» | suma a la lista y a `FormRecordsPage.tsx` [E15] | su tablero | las tres combinaciones; la vista «Por aprobar» y adónde abre; la cola con ↑ ↓ |
| F5 | La decisión y Actividad | `components/records/Workflow.tsx`; suma a `QuickView.tsx` y `RecordFullPage.tsx` | su tablero | solo decide; «Aprobar» con el foco en «Cancelar», y Enter no aprueba; el diálogo abierto hasta la respuesta; Reintentar; se relee mientras sale; la actividad en columnas fijas, por momento; sin el rol, la decisión de solo lectura y ningún error |
| F6 | Corregir y anular | `components/records/CorrectionForm.tsx`, con los componentes de la E20 | el tablero | lo de antes; motivo; 409; filtro «Anulada» |
| F7 | Lo propio, tablero y tarjeta | suma a `FormRecordsPage.tsx`, `FormDashboard.tsx` y la tarjeta | su tablero | estado, PDF y motivo |

**Al cerrar:** `docs/features/formularios.md` y `whatsapp.md` suman su sección, y la guía de correos, los adjuntos.

## Qué verificar al arrancar

| Marca | Qué hay que confirmar | Qué asume esta spec |
| --- | --- | --- |
| [Meta] | Antes de programar: que una plantilla acepte un documento en el encabezado, y cómo se da de alta. | Sale con una carga previa de ejemplo. |
| [E14] | La captura. | Las ejecuciones entran en su commit. |
| [E15] | La función de filtros y `FormRecords`. | Filtrar las vigentes en un solo lugar alcanza. |
| [E20] | La captura web y `sameOperation`. | Una corrección usa las dos, con quien cargó la original en lugar de la sesión. |
| Núcleo | El canal de correos y `EmailMessage`. | Suma adjuntos por clave, sin bytes en el outbox. |
| Núcleo | Cómo leer las identidades de rol de una cuenta. | Un lector del núcleo las da, sin que Forms lea `identity`. |
| Base de la 12 | La campana. | Un módulo le aporta avisos. |
| Página pública | El logo y los datos. | Se leen por un puerto del núcleo. |

## Decisiones tomadas (no se vuelven a preguntar)

- **Un solo motor,** sin una segunda cola de salida.
- **El rol** delimita quién decide, y nunca reemplaza al permiso.
- **Aprobación de un nivel** (60), y **quien aprueba** es la 25.
- **PDF con PDFsharp-MigraDoc,** como el Asistente: no se suma otra biblioteca de PDF.
- **Sin `ExecutionActor` en el worker:** quien respondió puede ser un cliente sin cuenta.
- **Lo propio tiene sus rutas,** como en la E20.
- **«Adjuntar el PDF»** (113) y **confirmar antes de aprobar** (114), con las esperas y la ayuda de las reglas de todo el sistema (109, 115 y 116).

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

- La E20 cerrada. El almacenamiento de la E10 no frena: alcanza con `LocalFileStore`, y su prueba queda pendiente (123).
- La prueba con Meta de una plantilla con documento en el encabezado (47 y 61).

## En el multitenant

- **Datos de la organización:** todo con RLS, salvo `platform.FormActionWorks`.
- **Workers:** el de acciones abre un scope por organización con `SetFromAccess(tenantId, TenantKind.Business)`, como `WhatsAppInboundProcessor`, y suma a `TenantScopeUsageTests`: un aviso en un chat vinculado escribe la copia compartida, y con `Enter`, que usa `TenantJobRunner`, ese guardado falla ([multitenancy](../../rules/multitenancy.md)).
- **Feature `forms`:** las rutas la llevan.
- **WhatsApp:** los avisos salen por el número de la organización.
- **Almacenamiento:** los PDF van en `IFileStore`, con la categoría `forms`.
- **«Para resolver»:** un `IPendingGroupSource` del módulo.
- **Cuotas:** las rutas cuentan en las cuotas generales.
