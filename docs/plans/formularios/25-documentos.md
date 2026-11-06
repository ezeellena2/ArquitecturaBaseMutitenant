# E25 · Documentos por WhatsApp (spec)

**Estado:** pendiente · spec de la etapa 25, la última del bloque (decisiones 34, 41, 78, 99, 102 y 154 a 156), con el [formato](plantilla.md) de las etapas. En ArquitecturaBase está decidido, con su ADR 0022 y sus tableros, y sin programar. **Antes de programar** se prueba con Meta si un Flow acepta un archivo precargado: es el mayor riesgo técnico. `/plan-etapa` la contrasta con el código real después de la E24.

**Partes.** Esta spec no entra en un solo archivo, así que sigue en otro, con las mismas secciones. Lo que no está en la tabla está acá.

| Secciones | Archivo |
| --- | --- |
| Contrato y Datos | [datos y contrato](25-parte-datos-y-contrato.md) |

## Qué leer

Antes de empezar, en este orden:

1. **Lo anterior:** la [spec de la E13](13-campos.md): el cruce por un campo; la [E14](14-formularios.md): la captura; la [E18](18-fotos-y-documentos.md): archivos; la [E21](21-reglas-y-aprobacion.md): corregir; la [E22](22-biblioteca.md): plantillas, y la [E23](23-asistente.md): turnos, herramientas y prellenado.
2. **Decisiones** de la hoja de ruta: 23, 24, 35, 40 a 42, 49, 50, 70 a 72, 75, 78 y 102.
3. **Fichas y guías:** [datos-de-referencia](../../rules/datos-de-referencia.md), [paginado-y-busqueda](../../rules/paginado-y-busqueda.md), [identificacion-fiscal](../../rules/identificacion-fiscal.md), [modulos](../../rules/modulos.md), [guardado](../../rules/guardado.md), [concurrencia](../../rules/concurrencia.md), [datos-personales](../../rules/datos-personales.md), [logs](../../rules/logs.md), [cuotas](../../rules/cuotas.md), [tests](../../rules/tests.md) y la [guía de herramientas](../../guides/agregar-una-herramienta-al-asistente.md).
4. **Código para copiar:** la columna «Copia de» de los [pasos](#pasos).
5. **Referencia de producto,** solo para leer: en `../ArquitecturaBase/docs/plans/2026-10-23-traspaso-multitenant/`, el anexo F, F3, y el anexo D (`DOC1` a `DOC6`); allá, la ADR 0022.

## Qué se construye

**Qué se lee** (70): lo que manda un empleado activo, con teléfono verificado y `formrecords.create`, en Internos, y lo que manda un cliente, cualquier contacto de Clientes (49). A cada uno se le proponen solo los formularios que son para él (50).

| Lo que llega | Cómo se lee |
| --- | --- |
| Foto o captura (JPEG, PNG) | la lee la IA |
| PDF | la IA lee todas sus páginas, con un tope |
| Word (`.docx`) | el servidor saca el texto y las tablas, sin ejecutar nada, y la IA los lee |
| Excel (`.xlsx`) | el servidor lee las hojas, con los topes de la E12. Varias filas proponen hasta 3 cargas por mensaje, y las demás quedan en la bandeja |
| Ubicación y contacto | sin IA: completan las preguntas elegidas en el editor |

No se leen `.doc`, `.xls`, los archivos con macros ni los que tienen contraseña: se pide un PDF o una versión sin eso. El tipo se verifica por el contenido.

**El circuito:**
1. **recibir:** se toma el archivo que ya bajó el chat y se guarda una copia privada;
2. **clasificar** entre los formularios con «Lectura de documentos» que esa persona puede completar. Seguro: propone ese. Con duda entre dos: «¿Qué es este documento?», con un botón para cada uno y «Otro». Sin reconocerlo: una lista de hasta 10 filas con «Otro», que lo deja «Sin clasificar» y lo anota en «Formularios que faltan». Un nombre de más de 20 caracteres va como lista;
3. **extraer** los datos según las preguntas elegidas, con su seguridad por dato;
4. **validar sin IA:** las validaciones fijas, el cruce con catálogos y los duplicados (el mismo archivo, o la misma operación, monto y fecha);
5. **proponer** el formulario prellenado, con el documento en su pregunta de archivo. Lo que no se leyó queda vacío, y lo dudoso, marcado;
6. **confirmar o corregir** lo hace la persona. Al confirmar se vuelven a revisar las validaciones y los duplicados con lo corregido. Nada se carga solo, salvo «Cargar solo si es seguro»;
7. **organizar:** queda como una respuesta más, con sus reglas, su aprobación y su PDF.

**Los casos difíciles:** una foto movida pide otra, diciendo qué falta; un PDF con contraseña pide uno sin ella; un repetido avisa cuándo se cargó; un dato que no pasa una validación no se propone como válido; un formulario que no se le puede mandar se dice, deriva y queda en la bandeja; un número que no es contacto no se lee.

**La «Lectura de documentos» de un formulario,** en su borrador, que se sella al publicar: «Cómo reconocerlo», qué pregunta recibe cada dato estándar, las validaciones fijas, el cruce con catálogos y la regla de duplicados. Cambiarle el nombre a una pregunta no corta la lectura, y un dato sin pregunta no se lee. **En la biblioteca,** una plantilla suma su lectura, que la plataforma edita en «Plantilla», y la copia la trae con las validaciones con candado.

**Las validaciones fijas** son un conjunto cerrado en el código, y cada formulario elige cuáles usa. CUIT y CUIL reusan el validador de `platform.TaxIdTypes`. CBU y CVU suman sus dígitos verificadores. **La tabla de entidades del BCRA** es un dato de referencia global, nunca una lista en el código.

**La bandeja «Documentos recibidos»:**
- una planilla como la de respuestas, con el documento en un panel al costado: acercar, girar, páginas y descargar;
- filtros: formulario, quién lo mandó, cuándo llegó, estado y «Empresa ▾» (24). La dirección nunca lleva quién lo mandó ni la búsqueda;
- búsqueda por los datos, como el monto con o sin puntos, un CUIT o una operación, también en lo que no se cargó;
- revisar: corregir, cambiar el formulario reconocido, que vuelve a leer, pasar al formulario (99) y descartar con confirmación. La respuesta dice de qué documento salió y quién la revisó;
- la fila de un descartado o sin clasificar muestra cuándo se borra.

**«Armar formulario con la IA»:** sobre un documento «Sin clasificar», o desde «Llegaron N documentos parecidos», con tres o más. La IA propone nombre, prefijo, botón, «Cómo reconocerlo» y una pregunta por dato; si se parece a un estándar, lo sugiere antes. Crea un borrador con la lectura prendida, y una persona lo revisa y lo publica. Si falla, no queda nada a medias.

**Armar con la IA, desde el asistente** (75, 102 y 154 a 156): en el chat del Inicio y por WhatsApp, con un texto, un audio o, en el Inicio, un PDF, una planilla o una foto, la IA **propone** un formulario o un catálogo y **una persona lo crea con un botón** (154). La IA nunca crea, publica, activa ni cambia nada sola, y crear es una sola vez por propuesta:
- **en el chat del Inicio,** una tarjeta en el hilo, debajo de la respuesta: la banda «Formulario» o «Catálogo», el nombre, el prefijo y cada pregunta con su tipo, si es obligatoria y sus opciones o su catálogo. «Ver cómo queda» abre la vista previa del editor en un diálogo, y «Crear borrador» o «Crear catálogo» va en la banda y en el diálogo. Se ajusta conversando: cada cambio trae otra tarjeta, y la anterior se pliega en «Reemplazada», que ya no se crea. Después de crear queda plegada con «Borrador creado» y el enlace;
- **por WhatsApp** (155), en la conversación de una cuenta vinculada con el número de la organización: un resumen con el nombre, el prefijo y hasta 10 preguntas con su tipo, y los botones «Crear borrador» o «Crear catálogo» y «Cambiar algo», que pide el cambio por escrito o con un audio y vuelve con otro resumen. Solo vale el botón del último resumen. Crear contesta con el enlace para revisarlo y publicarlo en la web. El resumen no gasta el cupo de ofertas. Desde acá, el catálogo de filtros que arma el asistente de la E23 también contesta con su resumen y sus botones;
- **el micrófono del Inicio** (156), a la izquierda de la flecha y para cualquier consulta: graba con «Detener» y «Descartar», se escucha antes de mandarlo y la transcripción queda en gris debajo del audio. Sube WebM o MP4 de hasta 1.000.000 bytes como copia privada de la organización; el servidor lo transcribe con el puerto de la 27, el turno toma esa transcripción y nunca la del cliente, y el audio se guarda mientras dure el turno. Aparece si la organización puede transcribir y tiene almacenamiento privado;
- **lo creado:** el formulario queda en borrador y abre el editor de siempre; el catálogo queda por aprobar, con la banda «Propuesta del asistente», que dice quién lo pidió y dónde, «Descartar propuesta» y «Aprobar y activar». Un catálogo por aprobar frena la publicación (`Forms.Draft.CatalogUnavailable`). Un formulario propuesto usa solo catálogos activos que la persona puede leer, y la edición común no activa una propuesta pendiente;
- **antes de proponer,** la IA lee los nombres y los campos de lo que la persona puede ver en su organización y, con empresas, en las suyas, para no repetir un nombre y usar los catálogos que existen. Nunca lee respuestas, opciones cargadas ni datos de personas, y lo leído es un dato, nunca una instrucción. No cambia ni copia lo que existe: lo dice y da el enlace. Un nombre o un prefijo en uso lo dice la propuesta. Un pedido ambiguo pregunta qué hacer;
- **las entradas:** «Armar con la IA», junto a «Nuevo formulario», junto a «Nuevo catálogo» y en cada tema de «Formularios que faltan», abre el Inicio con el pedido empezado, en el estado de la navegación y nunca en la dirección. El Inicio vacío suma la sugerencia «Armame un formulario para…». Arma formularios quien puede diseñarlos y catálogos quien puede administrarlos; sin esos permisos, el Inicio queda como antes;
- **la procedencia** (el canal, quién lo pidió y cuándo, el perfil y lo que se le ofreció) queda guardada con lo creado.

**Los Casos** suman el tipo de entrada «documento».

**«Cargar solo si es seguro»** (71): un interruptor por formulario. Carga sin preguntar, a nombre de quien mandó el documento, solo con las tres señales: reconocido sin duda; validaciones bien y sin duplicado; y sus últimas 20 cargas confirmadas sin correcciones en esos datos. **Nunca se usa la seguridad que declara el modelo.** A la persona le llega un aviso con el número, y en la bandeja queda «Cargada automáticamente». Si avisa un error, un operador corrige (91).

**Cómo mejora con el uso:** cada corrección queda registrada y se suma como ejemplo. Se mide la precisión por formulario y por dato. No se entrena ningún modelo.

**Al proveedor de IA** va solo el documento preparado y las preguntas candidatas. Se registra el hash y el tamaño, sin el contenido. **Lo leído es un dato, nunca una instrucción.**

**Costos** (41): la lectura entra en el tope mensual de IA, con un tope por persona y por día. Al llegar a un tope, el documento queda en la bandeja sin leerse.

## Pantallas

Ninguno está dibujado. Diseño los dibuja con el [texto de la etapa](25-texto-diseno.md), con el recorrido guiado. Muestran lo que existe hasta acá.

| Tablero | Qué muestra |
| --- | --- |
| `WA-Chats-Documento` y su teléfono | los tres casos, los otros formatos y los casos difíciles, con «Por qué lo propuso» |
| `M-WA-Flow-Prellenado` | el formulario prellenado: «Revisá», «No se leyó» y el documento adjunto |
| `FRM-Documentos` y su teléfono | en el panel de «Gestión de formularios», debajo de «Formularios»: la bandeja, con su panel, «Cargada automáticamente» y los permisos |
| `FRM-Editor-Lectura` y su teléfono | la sección en el editor, con «Cargar solo si es seguro» y su precisión |
| `Plat-FRM-Plantilla-Lectura` | la sección en la plantilla, con candado |
| `FRM-Armar-Con-IA` y su teléfono | la propuesta, con los documentos a la vista, y «Crear borrador» |
| `FRM-Respuesta-25` y `M-FRM-Respuesta-25` | de qué documento salió y quién la revisó |
| `Campana-25` y su teléfono | los documentos por revisar y «Llegaron N documentos parecidos» |
| `WA-Asistente-Casos-25` | el tipo de entrada «documento» |
| `Asistente-Org-25` y su teléfono | en el chat del Inicio, la tarjeta de la propuesta, con «Ver cómo queda», «Crear borrador», «Reemplazada» y «Borrador creado», y el micrófono |
| `M-WA-Armar-Con-IA` | por WhatsApp, el pedido con un audio, el resumen con «Crear borrador» y «Cambiar algo», y el enlace |
| `FRM-Formularios-25`, `FRM-Catalogos-25` y `FRM-Faltan-25` | «Armar con la IA» junto a «Nuevo formulario», junto a «Nuevo catálogo» y en cada tema |

**Mandan sobre esta spec en:** las fichas del front y la decisión 43. Los mensajes del sistema van en el idioma de la cuenta (42). «Dar de baja» no aplica: un documento se descarta.

## Pasos

Abreviaturas: `D/`, `A/`, `I/` y `P/` son las capas del back; `F/`, el front, y `[E14]` a `[E23]`, un archivo que deja esa etapa.

**Back:**

| # | Qué hace | Archivos | Copia de | Test |
| --- | --- | --- | --- | --- |
| B1 | Validaciones fijas y BCRA | `D/Forms/FormFixedValidations.cs`; el dato de referencia y su seed | el validador de `TaxIdTypes` | CBU y CVU; prefijo; lista cerrada |
| B2 | La lectura del formulario y de la plantilla | suma a `FormDraft.cs`, `FormVersionCreator.cs` [E14] y a la plantilla [E22]; `FormReadingPolicy.cs` | `FormLogicPolicy.cs` [E19] | identidad; dato sin pregunta; candado; se sella |
| B3 | Recibir y preparar | `IStructuredDocumentSink.cs`, `FormDocumentReceiver.cs`, `IFormDocumentTextExtractor.cs`, `I/Modules/Forms/FormDocumentTextExtractor.cs` | `FileContentInspector.cs` [E18] | cada formato; macros, contraseña y viejos; topes |
| B4 | Leer con la IA | `AssistantDocumentReader.cs`, su perfil y su propósito | `ConversationAgentService.cs` [E23] | lo que se manda; tope; instrucciones escondidas |
| B5 | El circuito, por fases | `FormDocumentService.cs`, `FormDocumentWorker.cs`; suma a `WhatsAppAssistantTurnService.cs` | `FormActionWorker.cs` [E21] | los tres casos; duplicados; ráfaga; un trabado no frena a los demás; la lista de `TenantScopeUsageTests` |
| B6 | Proponer y confirmar | suma a `WhatsAppFlowBuilder.cs`, `FormRecordService.cs` [E14] y al prellenado [E23] | esos mismos | el documento adjunto; revalidar; la respuesta vuelve a su documento; dos veces el mismo documento no carga dos; sin Meta real en el test |
| B7 | La bandeja | `FormDocumentQueryService.cs`, `FormDocumentReader.cs`; la excepción en `paginado-y-busqueda.md` y `SearchIndexTests` | `FormRecordReader.cs` [E15] | búsqueda con y sin puntos; lo privado fuera de la URL; aislamiento |
| B8 | Revisar | suma a `FormDocumentService.cs`; `IStructuredInboxAccess.cs`, su apagado y `WhatsAppStructuredInboxAccess.cs` | `FormRecordCorrectionService.cs` [E21] | permisos; sin WhatsApp no se revisa; volver a leer; descartar; pasar al formulario |
| B9 | Armar con la IA y desde el asistente | `FormProposalService.cs`, `IStructuredFormDrafts.cs`, su apagado, las dos lecturas, las dos propuestas y las rutas de crear | `FormDefinitionCopyCreator.cs` [E17], `OrganizationUsersTool` | nada a medias; nada se publica; catálogos por aprobar, a mano y con filtros; uno por aprobar frena publicar; crear una sola vez; una reemplazada no se crea |
| B13 | Armar por WhatsApp y el audio del Inicio | `IStructuredAuthoringProposalChannel.cs` y su implementación; la continuación de «Cambiar algo»; el audio en el asistente | la conversación de la E23 | solo vale el último botón; el resumen no gasta el cupo; la transcripción es la del servidor |
| B10 | Cargar solo si es seguro | `FormAutoLoadPolicy.cs` | `FormRecordCapture` [E14] | cada señal que falta; la seguridad del modelo no cuenta |
| B11 | Precisión, retención y baja | suma a `FormsAccountDeletionParticipant.cs`; `FormDocumentRetentionWorker.cs` | `InvitationExpiryWorker.cs` | a los 90 días; lo usado sigue a la respuesta |
| B12 | Rutas y contratos | `P/Modules/Forms/Controllers/BusinessFormDocumentsController.cs`, `Contracts/*`, `FormsContractContributor.cs` | los de la E15 | cada ruta con 401, 403, feature apagada = 404, 409; ejemplos exportados |

**Front:**

| # | Qué hace | Archivos | Copia de | Test |
| --- | --- | --- | --- | --- |
| F1 | Tipos y llamadas | `F/src/modules/forms/documents/api.ts`, `types.ts` | la API de la E24 | zod estricto |
| F2 | La bandeja y su panel | `documents/pages/DocumentsPage.tsx`, `components/DocumentPanel.tsx`, `DocumentPreview.tsx` | su tablero | filtros, panel, ↑ ↓ y permisos; axe |
| F3 | La lectura | `documents/components/DocumentReadingSection.tsx`, `ReadingFields.tsx` | su tablero | editor y plantilla; candado |
| F4 | Armar con la IA | `documents/pages/BuildFormPage.tsx` | su tablero | crear borrador; falla |
| F6 | La tarjeta, el micrófono y las entradas | la tarjeta de la propuesta en el chat del Inicio, por un punto de extensión; el micrófono; «Armar con la IA» en tres pantallas | su tablero | «Reemplazada» y «Borrador creado»; grabar, escuchar y descartar; el pedido en el estado de la navegación |
| F5 | El chat y el detalle | suma a `MessageThread.tsx` y `RecordValues.tsx` | esos mismos | «Por qué lo propuso»; de qué documento salió |

**Al cerrar:** `docs/features/formularios.md` suma la sección, y `datos-de-referencia.md`, la tabla.

## Qué verificar al arrancar

| Marca | Qué hay que confirmar | Qué asume esta spec |
| --- | --- | --- |
| [Meta] | Antes de programar: si un Flow acepta un archivo precargado. | **Si no se puede,** el Flow saltea esa pregunta, y al capturar la respuesta toma el documento guardado. La persona ve «Adjuntamos tu documento», sin «Cambiar documento». |
| [Meta] | Los botones y las listas: 3 y 10. | Lo de la API de hoy. |
| [E18] | El archivo del chat. | Se lee por un puerto, y Forms guarda su copia. |
| [E22] | `FormTemplateDraft` y la copia. | Una sección más se copia sola. |
| [E23] | La ráfaga, el prellenado y la conversación de una cuenta vinculada. | La ADR del asistente ya está; las propuestas no escriben (154). |
| Proveedor | Los topes del pedido al modelo con un formulario de 40 preguntas. | 16 definiciones y 4096 tokens de salida, como allá. |
| Bibliotecas | Con qué se saca el texto de `.docx` y `.xlsx` y las páginas de un PDF. | `.xlsx` con ClosedXML; lo demás se elige al planificar, sin ejecutar nada. |
| Parecidos | Cómo se mide que dos documentos se parecen. | Por la clasificación que devuelve la IA. |

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

Allá también la IA leía el nombre en otro idioma que el que comparaba crear. Acá no pasa: el contenido tiene un solo nombre (35).

## Decisiones tomadas (no se vuelven a preguntar)

- **«Pasar al formulario»** (99): queda a nombre de quien mandó el documento, por el canal `whatsapp`, con `SourceKey` `document:<id>`, igual que «Cargar solo si es seguro», y la respuesta dice quién la revisó. Pide el permiso de atender esa bandeja. Pasa por la misma captura, que le revisa a un empleado la membresía y `formrecords.create`.
- **Retención** (41): un documento usado sigue a su respuesta (23). Uno descartado o sin clasificar se borra a los 90 días, con su lectura.
- **Costos** (41): el proveedor de la plataforma, dentro del tope mensual, con un tope por persona y por día.
- **Permisos:** ver la bandeja pide `formrecords.read`; revisar, además, atender la bandeja de donde llegó, y armar, además, `forms.manage`.
- **La biblioteca** es la de la E22 (40).
- **Genérico:** ningún tipo de documento va en el código. Un estándar nuevo es una plantilla.

## Fuera de esta etapa

- Varias fotos de un mismo documento, o varios documentos en un mensaje.
- Leer lo que manda un número que no es contacto de la organización.
- Los tableros que cruzan formularios o suman datos externos.
- Los validadores de otros estándares, como el QR de una factura.

## Cómo se cierra

La puerta de la etapa, con la lista de control de `/puerta`. Antes, la calificación del perfil de lectura, con casos ficticios: comprobantes de varios bancos, fotos movidas, documentos que no son de ningún tipo, instrucciones escondidas, Word y Excel. Después, una **prueba real con Meta**, con comprobantes de varios bancos y billeteras y la plantilla «Comprobante de transferencia» cargada por un operador:
- reconocimiento seguro, con duda y sin reconocer;
- un CBU con el dígito verificador mal leído, que no pasa;
- un duplicado y una foto movida;
- la bandeja y las correcciones registradas.

## Requisitos previos

- La E24 cerrada, con la ADR del asistente, que suma las propuestas que crea una persona (154).
- La prueba con Meta del archivo precargado en un Flow.

## En el multitenant

- **Datos de la organización:** documentos, lecturas y correcciones, con RLS. Cada ruta nueva lleva su test de aislamiento.
- **Workers:** bajar y leer son operaciones durables, descubiertas en un registro técnico. Abren un scope por organización con `SetFromAccess(tenantId, TenantKind.Business)`, como la E21, sin `ExecutionActor`, porque quien manda puede ser un cliente, leen las features con `ITenantFeatureCache` y se suman a la lista de `TenantScopeUsageTests`. El borrado de los 90 días no toca la copia compartida, y recorre las organizaciones con `TenantJobRunner`.
- **Feature `forms`:** las rutas la llevan.
- **WhatsApp:** la bajada usa la WABA y el token de la organización.
- **Almacenamiento:** copias privadas en `IFileStore`, y el antivirus de la lista de lanzamiento.
- **Baja de la cuenta:** el participante borra los documentos sin usar de esa persona.
- **Cuotas:** las rutas cuentan en las generales; la lectura, en el tope de IA.
