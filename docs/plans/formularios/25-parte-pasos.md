# E25 · Documentos por WhatsApp: pasos (spec, parte)

**Estado:** pendiente · parte de la [spec de la E25](25-documentos.md), que dice qué leer y cómo se cierra.

## Pasos

Abreviaturas: `D/`, `A/`, `I/` y `P/` son las capas del back; `F/`, el front, y `[E14]` a `[E23]`, un archivo que deja esa etapa.

**Back:**

| # | Qué hace | Archivos | Copia de | Test |
| --- | --- | --- | --- | --- |
| B1 | Validaciones fijas y BCRA | `D/Forms/FormFixedValidation.cs`, un enum cerrado, y `FormFixedValidationPolicy.cs`; el dato de referencia y su seed | el validador de `TaxIdTypes` | CBU y CVU; prefijo; lista cerrada; lo que no tiene la forma queda sin leer y lo que la tiene y no pasa, marcado (277) |
| B2 | La lectura del formulario y de la plantilla | suma a `FormDraft.cs`, `FormVersionCreator.cs` [E14] y a la plantilla [E22]; `FormReadingPolicy.cs` | `FormLogicPolicy.cs` [E19] | identidad; dato sin pregunta; «elegir varias», el bloque, «Mostrar si» y la marca no reciben datos; candado; se sella |
| B3 | Recibir y preparar | `IStructuredDocumentSink.cs`, `A/Modules/Forms/Services/FormStructuredDocumentSink.cs`, `IFormDocumentTextExtractor.cs`, `I/Modules/Forms/FormDocumentTextExtractor.cs` | `FileContentInspector.cs` [E18] | cada formato; macros, contraseña y viejos; topes; un PDF protegido y uno que solo restringe permisos; un PDF que se infla y uno que vence su plazo dan «ilegible»; uno de más páginas que las de la configuración no se lee (270) |
| B4 | Leer con la IA | `AssistantDocumentReader.cs`, su perfil y su propósito | `ConversationAgentService.cs` [E23] | lo que se manda, nunca una corrección ni otra respuesta; el modelo calificado; tope; instrucciones escondidas |
| B5 | El circuito, por fases | `FormDocumentService.cs`, `FormDocumentWorker.cs`; suma a `WhatsAppAssistantTurnService.cs` y a `AssistantAgentPolicy.cs` [E23], el tipo de entrada «documento» | `FormActionWorker.cs` [E21] | los tres casos; duplicados; ráfaga; un caso «documento» va antes que la lectura; sin `assistant` no se lee; sin un formulario que lea documentos, no se lee; los dos topes del día y su aviso fijo, que cortan a la medianoche de la organización (271); una planilla de más de 50 filas, y una con más filas que «Formularios por planilla» (270); una ubicación que contesta el pedido de la E20 es de ese envío; un trabado no frena a los demás; la lista de `TenantScopeUsageTests` |
| B6 | Proponer y confirmar | suma a `WhatsAppFlowBuilder.cs`, `FormRecordService.cs` [E14] y al prellenado [E23] | esos mismos | el documento adjunto; revalidar; la respuesta vuelve a su documento; dos veces el mismo documento no carga dos; la respuesta llega sin el archivo y la copia cuenta antes del mínimo de la pregunta (O38); sin Meta real en el test |
| B7 | La bandeja | `FormDocumentQueryService.cs`, `FormDocumentReader.cs`; la excepción en `paginado-y-busqueda.md` y `SearchIndexTests`; las fuentes `documentsToReview` y `documentsSuggestion` de la campana | `FormRecordReader.cs` [E15] | búsqueda con y sin puntos; lo privado fuera de la URL; aislamiento; el orden por columnas (264); «Se borra» con el plazo vigente (272); la campana con y sin `formrecords.read`, y «Llegaron N documentos parecidos» con el valor de la configuración (270) |
| B8 | Revisar | suma a `FormDocumentService.cs`; `IStructuredInboxAccess.cs`, su apagado y `WhatsAppStructuredInboxAccess.cs` | `FormRecordCorrectionService.cs` [E21] | permisos; sin WhatsApp no se revisa; cambiar de formulario vuelve a extraer sin releer; descartar; pasar al formulario; pasar un repetido pide `confirmRepeated`; quien la pasó no la aprueba ni la ve en «Por aprobar» (238), salvo con «Puede decidir quien cargó» (276), y un rol cuyo único miembro es esa persona da `RoleWithoutMembers` (239); con un bloque con mínimo, pasar desde la carga de la web, a nombre de quien lo mandó |
| B9 | Armar con la IA y desde el asistente | `FormProposalService.cs`, `IStructuredAuthoring.cs` y su apagado, `IAssistantProposalLookup.cs`, las dos lecturas, las dos propuestas, `decline_authoring` y las rutas de crear | `FormDefinitionCopyCreator.cs` [E17], `OrganizationUsersTool` | nada a medias; nada se publica; un catálogo a mano, uno de los miembros con filtros y uno desde una planilla; lo que se repite, como un bloque; uno por aprobar frena publicar; crear una sola vez; una reemplazada no se crea; un nombre o un prefijo en uso; un motivo equivocado de `decline_authoring` vuelve al modelo; un pedido vago trae una propuesta y pregunta una sola vez, sin gastar cupos; un prefijo mal escrito se corrige; importar en un catálogo que existe lo abre y no importa; con un documento de ejemplo, la lectura prendida; arma el modelo calificado, también con la clave propia y desde la bandeja; nunca propone una Ubicación (230); `ProposalTooLarge`; al publicar, los documentos que lo originaron quedan propuestos, dentro del cupo de relecturas, y los demás esperan a la medianoche de la organización (270 y 271); los estados de la propuesta, por clase (268) |
| B10 | Armar por WhatsApp | `IStructuredAuthoringCreator.cs` y su implementación; el resumen; la continuación de «Cambiar algo» | la conversación de la E23 | antes de los casos; solo en modo Asistente; solo vale el último botón; el resumen no gasta el cupo; sin permiso y «ya existe»; una planilla con un pedido va a armar, y sin pedido, a la lectura; crear revisa el chat y dice el motivo (`Forms.Proposal.ChatUnavailable`); sin poder saber qué armar después de preguntar, deriva |
| B11 | El catálogo propuesto | suma a `FormCatalog.cs` y a su servicio [E12]: la propuesta, aprobar y descartar | esos mismos | guardar no lo activa; aprobar y activar, con 409; aprobar uno de los miembros prueba sus filtros; descartar; uno que no espera aprobación |
| B12 | El chat del Inicio: la propuesta, el clip y el audio | suma a los turnos, los adjuntos y el historial del módulo Assistant; `AssistantAudioService.cs` | los adjuntos de la E8b | `proposal` en `done` y en el historial, con las clases de la E25 y sus estados (`new`, `replaced`, `applied` y `unavailable`, 268); los tipos de armar, de a uno; el formato por sus bytes; la transcripción es la del servidor; el audio vive mientras un turno con contenido lo nombra y se borra con la conversación; sin cupo no se guarda; el mismo audio dos veces; un lugar de subida por persona; sin ruta personal ni de Plataforma |
| B13 | Cargar solo si es seguro | `FormAutoLoadPolicy.cs` | la captura de `FormRecordService.cs` [E14] | cada señal que falta; la seguridad del modelo no cuenta; no se aplica con un bloque con mínimo |
| B14 | Precisión, retención, configuración y baja | suma a `FormsAccountDeletionParticipant.cs`; `FormDocumentRetentionWorker.cs`; suma a la Configuración de Formularios [E18] | `InvitationExpiryWorker.cs`; `FormSettingsService.cs` [E18] | a los días de la configuración, también después de bajarlos (272); lo propuesto sin revisar vence con su motivo; en lotes y al arrancar; lo usado sigue a la respuesta; cada valor con su mínimo, su máximo, el de la plataforma y el 409, leído en cada uso, y una fila vieja con los de por defecto (270) |
| B15 | Rutas y contratos | `P/Modules/Forms/Controllers/BusinessFormDocumentsController.cs`, `FormsContractContributor.cs`, un archivo por pedido en `P/Modules/Forms/Contracts/` y `UploadAssistantAudioHttpRequest` en `P/Modules/Assistant/Contracts/` | los de la E15 | cada ruta con 401, 403, feature apagada = 404, 409; sin `assistant`, `form-proposal` y `form-drafts` dan 404; ejemplos exportados |
| B16 | Acciones del asistente, las de la spec | `A/Modules/Forms/Services/FormDocumentActionSource.cs`; suma a `FormCatalogActionSource.cs` [E12] y `FormSettingsActionSource.cs` [E18]; sus pantallas en `AssistantActionScreen` con su traducción a `AssistantRouteKey`, sus textos `Action.*` en `FormsTexts` y sus escenarios | `RoleActionSource.cs` | `AssistantActionDeclarationTests` y la batería genérica; «Pasar al formulario» declara su efecto |

**Front:**

| # | Qué hace | Archivos | Copia de | Test |
| --- | --- | --- | --- | --- |
| F1 | Tipos y llamadas | `F/src/modules/forms/documents/api.ts`, `types.ts` | la API de la E21 | zod estricto |
| F2 | La bandeja y su panel | `documents/pages/DocumentsPage.tsx`, `components/DocumentPanel.tsx`, `DocumentPreview.tsx`; pasar desde la carga de la web, sobre `LoadResponsePage.tsx` [E20]; los adaptadores de descartar y de «Pasar al formulario» | su tablero | filtros en la pieza (262), panel, ↑ ↓ y permisos; revisar, descartar y volver a abrir en el ⋮ (261); el orden por columnas (264); «Venció», «Protegido» y pasar un repetido con su confirmación; con un bloque con mínimo, «Pasar al formulario» abre la carga con lo leído y los ítems vacíos; axe |
| F3 | La lectura | `documents/components/DocumentReadingSection.tsx`, `ReadingFields.tsx` | su tablero | editor, plantilla y su vista; el dato y las validaciones en la pieza (262); candado; «Lo escribió la IA»; los ejemplos solo con `formrecords.read` y la precisión por dato; «Cargar solo si es seguro» apagado con un bloque con mínimo, con su motivo |
| F4 | Armar con la IA | `documents/pages/BuildFormPage.tsx` | su tablero | crear borrador; el bloque; falla |
| F5 | La tarjeta, el clip, el micrófono y las entradas | la tarjeta de la propuesta en el chat del Inicio, por un punto de extensión; el clip; el micrófono; «Armar con la IA» en tres pantallas | su tablero | la de un formulario, la de un catálogo y la de importar en uno que existe; «Nueva», «Reemplazada», «Borrador creado», «Catálogo creado» y «Ya no disponible» con su motivo (268); «Armar con la IA» en el ⋮ de cada tema (261); «Lee documentos»; un archivo que no se lee; grabar, escuchar y descartar; borrar con sus audios; el pedido en el estado de la navegación |
| F6 | El catálogo propuesto | suma a la pantalla del catálogo de la E12 | su tablero | la etiqueta y quién lo pidió; «Descartar propuesta» y «Aprobar y activar»; guardar no lo activa; con una planilla, «Importar» abierto, y después de importar, la cabecera con su estado sin recargar (O24) |
| F7 | El chat y el detalle | suma a `MessageThread.tsx`, `RecordValues.tsx` y `RecordActivity.tsx` [E15] | esos mismos | «Por qué lo propuso»; la respuesta como el formulario, con el documento en su pregunta (266), y el renglón «La pasó desde un documento» (267) |
| F8 | Los Casos y la configuración | suma a la pantalla «Asistente» [E23] y a las dos páginas de la Configuración de Formularios [E18] | su tablero | el tipo de entrada «Documento», en la pieza (262); el bloque fijo «Armar formularios y catálogos», sin editar; los seis valores con su máximo, la (i) de los plazos y el 409 (270 y 272) |

**Al cerrar:** `docs/features/formularios.md` suma la sección, `datos-de-referencia.md`, la tabla, y `docs/features/asistente.md`, la propuesta, el clip y el audio del Inicio.

## Lo que ya se pagó allá (no repetir)

Lo que allá se encontró al programar «la IA arma formularios y catálogos», según su traspaso 8. Cada punto lleva su test:
1. **Una propuesta inválida vuelve al modelo,** con la ruta de cada argumento; no se arregla en silencio ni se recorta.
2. **Crear busca primero lo ya creado por esa propuesta:** un segundo toque devuelve lo mismo, aunque la propuesta haya vencido.
3. **El reemplazo es por capacidad:** un catálogo pedido después de un formulario no invalida el formulario.
4. **Los topes del pedido al modelo cambian con cada capacidad nueva.** Allá el asistente de WhatsApp llegaba a 14 definiciones con un tope de 12, y con 1500 tokens de salida un formulario mediano se cortaba.
5. **En WhatsApp, cada botón lleva su propuesta,** y solo vale el del último resumen: uno viejo contesta y no crea nada.
6. **El audio que trae un cambio no es una oferta:** no consulta el cupo diario.
7. **El navegador graba WebM o MP4,** y la normalización solo admitía Ogg y WAV: el formato se reconoce por sus bytes, no por lo que dice el cliente.
8. **Un turno sin propuesta conserva la forma que lee el front,** y aprobar un catálogo propuesto devuelve mapas vacíos, no nulos.
9. **Un CHECK de SQL va en una sola línea:** escrito en varias, un checkout con CRLF le mete un `\r`.

Y lo que trajo el traspaso 15 (allá, del 13 al 16/11):
10. **Cambiar de formulario releía el documento** y gastaba otra lectura: se vuelve a extraer con lo leído.
11. **Un PDF que se infla o tarda frenaba la lectura** de todos: tope acumulado, plazo y los intentos contados.
12. **Un documento propuesto quedaba para siempre** en la bandeja, con datos de clientes: vence a los 90 días (215).

Y lo que vio la [prueba integral de allá](../../backlog.md#lo-que-vio-la-prueba-integral-de-arquitecturabase), del 19 al 23/11:
13. **Meta no devuelve el archivo precargado** en la respuesta del Flow: si la copia no cuenta antes del mínimo de la pregunta, la respuesta queda rechazada y a la persona no le llega nada (O38).
14. **El resumen decía dos veces** el código y la opción que salen de la misma columna (O34).
15. **La cabecera de un catálogo propuesto** no decía su estado después de importar, hasta recargar (O24).

Allá también la IA leía el nombre en otro idioma que el que comparaba crear. Acá no pasa: el contenido tiene un solo nombre (35).
