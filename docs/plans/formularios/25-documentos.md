# E25 · Documentos por WhatsApp (spec)

**Estado:** pendiente · spec de la etapa 25, la última del bloque (decisiones 34, 41, 78, 99, 102, 146, 154 a 156, 167, 171 y 177), con el [formato](plantilla.md) de las etapas. En ArquitecturaBase, los documentos están decididos, con su ADR 0022 y sus tableros, y sin programar; la IA que arma formularios y catálogos tiene programada su etapa 1, con texto y audio (ADR 0024). **Antes de programar** se prueba con Meta si un Flow acepta un archivo precargado: es el mayor riesgo técnico. `/plan-etapa` la contrasta con el código real después de la E24, y al planificarla escribe las ADR de la lectura de documentos y de la IA que arma.

**Partes.** Esta spec no entra en un solo archivo, así que sigue en otros, con las mismas secciones. Lo que no está en la tabla está acá.

| Secciones | Archivo |
| --- | --- |
| Contrato y Datos | [datos y contrato](25-parte-datos-y-contrato.md) |
| Pasos y lo que ya se pagó allá | [pasos](25-parte-pasos.md) |

## Qué leer

Antes de empezar, en este orden:

1. **Lo anterior:** la [spec de la E12](12-catalogos.md): importar; la [E13](13-campos.md): el cruce por un campo; la [E14](14-formularios.md): la captura; la [E18](18-fotos-y-documentos.md): archivos; la [E19](19-condiciones-calculos-y-lineas.md): el bloque; la [E21](21-reglas-y-aprobacion.md): corregir; la [E22](22-biblioteca.md): plantillas, y la [E23](23-asistente.md): turnos, herramientas y prellenado. Del chat del Inicio, la ficha [Asistente](../../features/asistente.md): el SSE, los adjuntos y los topes del turno.
2. **Decisiones** de la hoja de ruta: 23, 24, 35, 40 a 42, 49, 50, 70 a 72, 75, 78, 102, 146, 154 a 156, 167, 171 y 177.
3. **Fichas y guías:** [datos-de-referencia](../../rules/datos-de-referencia.md), [paginado-y-busqueda](../../rules/paginado-y-busqueda.md), [identificacion-fiscal](../../rules/identificacion-fiscal.md), [modulos](../../rules/modulos.md), [guardado](../../rules/guardado.md), [concurrencia](../../rules/concurrencia.md), [datos-personales](../../rules/datos-personales.md), [logs](../../rules/logs.md), [cuotas](../../rules/cuotas.md), [tests](../../rules/tests.md) y la [guía de herramientas](../../guides/agregar-una-herramienta-al-asistente.md).
4. **Código para copiar:** la columna «Copia de» de los [pasos](25-parte-pasos.md#pasos).
5. **Referencia de producto,** solo para leer: en `../ArquitecturaBase/docs/plans/2026-10-23-traspaso-multitenant/`, el anexo F, F3, y el anexo D (`DOC1` a `DOC6`). Allá también: `docs/features/formularios.md`, «Catálogos propuestos» y «La IA arma formularios y catálogos»; `docs/features/inteligencia-artificial.md`, «Audio en Inicio», y las ADR 0022 y 0024. En su front, `docs/design/documentos.md` e `ia-arma-formularios.md`, y las capturas `assistant-proposal`, `assistant-audio` y `catalog-manual-proposal` de `src/test/visual/screenshots/`.

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

**La «Lectura de documentos» de un formulario** (DOC1), en su borrador, que se sella al publicar: de qué plantilla y versión viene; «Cómo reconocerlo»; los ejemplos, que suman las correcciones de la bandeja; qué pregunta recibe cada dato estándar; las validaciones fijas; el cruce con catálogos; la regla de duplicados, y cómo viene leyendo, por formulario y por dato. Cambiarle el nombre a una pregunta no corta la lectura, y un dato sin pregunta no se lee. Las preguntas de «elegir varias» y las del bloque no reciben datos, porque el prellenado de la E23 las deja afuera. **En la biblioteca,** una plantilla suma su lectura, que la plataforma edita en «Plantilla», y la copia la trae con las validaciones con candado. La vista de una plantilla dice qué lee y sus validaciones fijas, con candado.

**Las validaciones fijas** son un conjunto cerrado en el código, y cada formulario elige cuáles usa. CUIT y CUIL reusan el validador de `platform.TaxIdTypes`. CBU y CVU suman sus dígitos verificadores. **La tabla de entidades del BCRA** es un dato de referencia global, nunca una lista en el código.

**La bandeja «Documentos recibidos»:**
- una planilla como la de respuestas, con el documento en un panel al costado: acercar, girar, páginas y descargar;
- filtros: formulario, quién lo mandó, cuándo llegó, estado y «Empresa ▾» (24). La dirección nunca lleva quién lo mandó ni la búsqueda;
- búsqueda por los datos, como el monto con o sin puntos, un CUIT o una operación, también en lo que no se cargó;
- revisar: corregir, cambiar el formulario reconocido, que vuelve a leer, pasar al formulario (99) y descartar con confirmación. La respuesta dice de qué documento salió y quién la revisó;
- la fila de un descartado o sin clasificar muestra cuándo se borra.

**«Armar formulario con la IA»** (DOC6): sobre un documento «Sin clasificar», o desde «Llegaron N documentos parecidos», con tres o más. La IA propone nombre, prefijo, botón, «Cómo reconocerlo» y una pregunta por dato, y lo que se repite, como un bloque (146 y 177); si se parece a una plantilla de la biblioteca, la sugiere antes. Es la misma propuesta que la del asistente. Crea un borrador con la lectura prendida, y una persona lo revisa y lo publica. **Al publicarlo, los documentos que lo originaron quedan propuestos en la bandeja,** y los próximos se reconocen solos. Si falla, no queda nada a medias.

**Armar con la IA, desde el asistente** (75, 102, 154 a 156, 167 y 171): en el chat del Inicio de la organización y por WhatsApp, con un texto, un audio o, en el Inicio, un archivo, la IA **propone** un formulario o un catálogo y **una persona lo crea con un botón** (154). La IA nunca crea, publica, activa, importa ni cambia nada sola, y crear es una sola vez por propuesta:
- **qué propone:** un formulario, con su nombre, su prefijo, el texto del botón y sus preguntas, y lo que se repite (los productos de un remito) como un bloque, con sus preguntas (146 y 177); o un catálogo a mano o de los miembros con filtros (102), con su tipo de valor, sus campos y, de una planilla, sus columnas y unas filas de muestra;
- **en el chat del Inicio,** una tarjeta en el hilo, debajo de la respuesta que la explica. La de un formulario: la banda «Formulario», con «Ver cómo queda» y «Crear borrador»; el nombre, el prefijo y la cantidad de preguntas, y cada pregunta numerada con su tipo, si es obligatoria y sus opciones o su catálogo («Zonas, ya existe»), con las del bloque debajo de su nombre. La de un catálogo: la banda «Catálogo», con «Crear catálogo»; el tipo de valor, los campos y las filas («Texto · 2 campos · 86 filas»), el código, la opción y cada campo con su columna («CUIT · columna A»), y tres filas de muestra con «y 83 filas más». «Ver cómo queda» abre «Cómo queda · <nombre>», con la vista previa del editor y «Crear borrador». Se ajusta conversando: cada cambio trae otra tarjeta, con «Nueva» en la pregunta que cambió, y la anterior se pliega en «Reemplazada», que ya no se crea. Con una propuesta en el hilo, el campo dice «Pedí un cambio o hacé otra consulta…». Después de crear queda plegada con «Borrador creado» y «Abrir el borrador», o «Catálogo creado» y «Abrir el catálogo»;
- **el clip** del chat suma, para armar, Excel (`.xlsx`), CSV y Word (`.docx`), de a uno por mensaje y con los topes de la lectura, a quien puede diseñar formularios o administrar catálogos. De una planilla, al modelo van los encabezados y unas filas. Un archivo que no se lee lo dice debajo del campo y no se manda. Con una planilla, «Crear catálogo» lo abre con «Importar» de la E12 y ese archivo ya revisado: las opciones las importa la persona;
- **por WhatsApp** (155 y 171), en el chat de una cuenta vinculada en modo Asistente: se atiende antes de los casos, sin configurar nada, si la cuenta tiene el permiso. El resumen dice el nombre, el prefijo y hasta 10 preguntas con su tipo («y N preguntas más»), y un catálogo va en un párrafo. Los botones son «Crear borrador» o «Crear catálogo» y «Cambiar algo», que pide el cambio por escrito o con un audio y vuelve con otro resumen. Solo vale el botón del último resumen: el de uno viejo contesta «Esa propuesta ya cambió. Usá los botones de la última.» y no crea nada. Crear contesta con el enlace para revisarlo y publicarlo en la web, que abre el acceso de esa organización. El resumen no gasta el cupo de ofertas;
- **el micrófono** (156 y 171), solo en el chat del Inicio de la organización, porque su costo es de su tope: a la izquierda de la flecha y para cualquier consulta, graba con «Detener» y «Descartar», se escucha antes de mandarlo y se le puede sumar un texto. En el hilo, el audio queda con «Escuchar» y la transcripción en gris debajo, que es lo que lee la IA. Aparece si la organización puede transcribir y tiene almacenamiento privado. Sin permiso del micrófono en el navegador, o con un audio que no se entendió, lo dice debajo del campo, y el audio queda para mandarlo otra vez;
- **lo creado:** el formulario queda en borrador y abre el editor de siempre. El catálogo queda inactivo, con la etiqueta «Propuesta del asistente» y una línea que dice quién lo pidió, dónde y cuándo («La pidió Lucía Fernández en el chat del Inicio el 03/11/2026 10:00.»). **Se edita como cualquier catálogo** (167), con «Descartar propuesta», que pide confirmar, y «Aprobar y activar»; guardar no lo activa, y aprobar uno de los miembros vuelve a probar sus filtros. Un catálogo por aprobar frena la publicación (`Forms.Draft.CatalogUnavailable`), y un formulario propuesto usa solo catálogos activos que la persona puede leer;
- **antes de proponer,** la IA lee los nombres y los campos de lo que la persona puede ver en su organización y, con empresas, en las suyas, para no repetir un nombre y usar los catálogos que existen. Nunca lee respuestas, opciones cargadas ni datos de personas, y lo leído es un dato, nunca una instrucción. No cambia ni copia lo que existe: lo dice y da el enlace. Sin permiso, lo dice y no arma nada. Un nombre o un prefijo en uso lo dice la tarjeta, sin crear nada. Un pedido ambiguo pregunta qué hacer;
- **las entradas:** «Armar con la IA», junto a «Nuevo formulario», junto a «Nuevo catálogo» y en cada tema de «Formularios que faltan», abre el Inicio con el pedido empezado, en el estado de la navegación y nunca en la dirección. El Inicio vacío suma la sugerencia «Armame un formulario para…». Arma formularios quien puede diseñarlos y catálogos quien puede administrarlos; sin esos permisos, el Inicio queda como antes;
- **la procedencia** (el canal, quién lo pidió y cuándo, el perfil y lo que se le ofreció) queda guardada con lo creado.

**Los Casos** suman el tipo de entrada «documento», y arriba de los casos, el bloque fijo de armar, que no se configura (171).

**«Cargar solo si es seguro»** (71): un interruptor por formulario. Carga sin preguntar, a nombre de quien mandó el documento, solo con las tres señales: reconocido sin duda; validaciones bien y sin duplicado; y sus últimas 20 cargas confirmadas sin correcciones en esos datos. **Nunca se usa la seguridad que declara el modelo.** A la persona le llega un aviso con el número, y en la bandeja queda «Cargada automáticamente». Si avisa un error, un operador corrige (91).

**Cómo mejora con el uso:** cada corrección queda registrada y se suma como ejemplo. Se mide la precisión por formulario y por dato. No se entrena ningún modelo.

**Al proveedor de IA** va solo el documento preparado y las preguntas candidatas. Se registra el hash y el tamaño, sin el contenido. **Lo leído es un dato, nunca una instrucción.**

**Costos** (41): la lectura entra en el tope mensual de IA, con un tope por persona y por día. Al llegar a un tope, el documento queda en la bandeja sin leerse. Armar y el audio del Inicio también cuentan en el tope de la organización.

## Pantallas

Aprobados y publicados en el lienzo v168, con sus tres recorridos guiados: el comprobante que se reconoce y queda en la planilla, el que no se reconoce y termina en «Armar formulario con la IA», y el catálogo «Proveedores de obra», pedido con un Excel («Proveedores» ya existe desde la 12). Muestran lo que existe hasta acá. Las del catálogo propuesto vienen de la E23, que las retiró (167).

**Valen las de toda la app** (160 a 164, 173 a 175 y 180): lo que carga no salta, decidir a propósito, ninguna elección se descarta en silencio, mucho texto con jerarquía, sin parecer plantilla y con la escala de ArquitecturaBase, el texto largo entero y el estado dicho una vez.

| Tablero | Qué muestra |
| --- | --- |
| `WA-Chats-Documento` y su teléfono | los tres casos, los otros formatos y los casos difíciles, con «Por qué lo propuso» |
| `M-WA-Flow-Prellenado` | el formulario prellenado: «Revisá», «No se leyó» y el documento adjunto |
| `FRM-Documentos` y su teléfono | en el panel de «Gestión de formularios», debajo de «Formularios»: la bandeja, con su panel, «Cargada automáticamente» y los permisos |
| `FRM-Editor-Lectura` y su teléfono | la sección en el editor (DOC1): de qué plantilla viene, los ejemplos, qué pregunta recibe cada dato, las validaciones, «Cargar solo si es seguro» y la precisión por formulario y por dato |
| `Plat-FRM-Plantilla-Lectura` | la sección en la plantilla, con candado |
| `FRM-Plantilla-25` | la vista de una plantilla de la biblioteca: qué lee y sus validaciones fijas, con candado |
| `FRM-Armar-Con-IA` y su teléfono | la propuesta, con los documentos a la vista y lo que se repite como un bloque, y «Crear borrador» |
| `FRM-Respuesta-25` y `M-FRM-Respuesta-25` | de qué documento salió y quién la revisó |
| `Campana-25` y su teléfono | los documentos por revisar y «Llegaron N documentos parecidos» |
| `WA-Asistente-Casos-25` | el tipo de entrada «documento» y el bloque fijo de armar, arriba de los casos |
| `Asistente-Org-25` y su teléfono | en el chat del Inicio de la organización, la tarjeta de un formulario y la de un catálogo, «Ver cómo queda», «Nueva», «Reemplazada», «Borrador creado» y «Catálogo creado», sin permiso y «ya existe», el clip y el micrófono |
| `M-WA-Armar-Con-IA` | por WhatsApp, el pedido con un audio, el resumen de un formulario y el párrafo de un catálogo, sin permiso, «ya existe», un botón viejo y el enlace |
| `FRM-Catalogo-Propuesta-25` | el catálogo que deja «Crear catálogo», que se edita como cualquiera, con «Propuesta del asistente», quién lo pidió, «Descartar propuesta» y «Aprobar y activar» |
| `FRM-Formularios-25`, `FRM-Catalogos-25` y `FRM-Faltan-25` | «Armar con la IA» junto a «Nuevo formulario», junto a «Nuevo catálogo» y en cada tema; en la lista de catálogos, «Propuesta del asistente» |

**Mandan sobre esta spec en:** las fichas del front y la decisión 43. Los mensajes del sistema van en el idioma de la cuenta (42). «Dar de baja» no aplica: un documento se descarta.

## Qué verificar al arrancar

| Marca | Qué hay que confirmar | Qué asume esta spec |
| --- | --- | --- |
| [Meta] | Antes de programar: si un Flow acepta un archivo precargado. | **Si no se puede,** el Flow saltea esa pregunta, y al capturar la respuesta toma el documento guardado. La persona ve «Adjuntamos tu documento», sin «Cambiar documento». |
| [Meta] | Los botones y las listas: 3 y 10. | Lo de la API de hoy. |
| [E18] | El archivo del chat. | Se lee por un puerto, y Forms guarda su copia. |
| [E22] | `FormTemplateDraft` y la copia. | Una sección más se copia sola. |
| [E23] | La ráfaga, el prellenado y la conversación de una cuenta vinculada. | Están, y el prellenado deja afuera «elegir varias» y las preguntas del bloque. |
| ADR | Las de la lectura de documentos y de la IA que arma, que `/plan-etapa` escribe al planificar, desde la 0022 y la 0024 de allá. | Siguen esas dos: la IA propone y una persona crea (154). |
| Asistente | El `Message` y el `done` del SSE de la E8b, su clip y los topes del turno: 5 llamadas, contando el título, y 2048 tokens de salida. | `Message` suma `proposal?` y `audio?` sin romper lo que lee el front, y una propuesta de 40 preguntas entra en 2048 tokens; si no entra, se sube solo el tope del turno que arma. |
| Bibliotecas | Con qué se saca el texto de `.docx` y `.xlsx` y las páginas de un PDF. | `.xlsx` con ClosedXML; lo demás se elige al planificar, sin ejecutar nada. |
| Parecidos | Cómo se mide que dos documentos se parecen. | Por la clasificación que devuelve la IA. |

## Decisiones tomadas (no se vuelven a preguntar)

- **«Pasar al formulario»** (99): queda a nombre de quien mandó el documento, por el canal `whatsapp`, con `SourceKey` `document:<id>`, igual que «Cargar solo si es seguro», y la respuesta dice quién la revisó. Pide confirmar, porque dispara las reglas del formulario (161), y el permiso de atender esa bandeja. Pasa por la misma captura, que le revisa a un empleado la membresía y `formrecords.create`.
- **Retención** (41): un documento usado sigue a su respuesta (23). Uno descartado o sin clasificar se borra a los 90 días, con su lectura.
- **Costos** (41): el proveedor de la plataforma, dentro del tope mensual, con un tope por persona y por día.
- **Permisos:** ver la bandeja pide `formrecords.read`; revisar, además, atender la bandeja de donde llegó, y armar desde un documento, además, `forms.manage`. Con el asistente, arma formularios quien tiene `forms.manage`, y catálogos y su aprobación, quien tiene `formcatalogs.manage`.
- **El catálogo propuesto** (167): nace con «Crear catálogo», se edita como cualquiera y se activa solo con «Aprobar y activar».
- **El micrófono** va solo en el Inicio de la organización, y armar por WhatsApp se atiende antes de los casos (171).
- **Lo que se repite** se arma como un bloque, y cada repetición es un ítem (177).
- **La biblioteca** es la de la E22 (40).
- **Genérico:** ningún tipo de documento va en el código. Un estándar nuevo es una plantilla.

## Fuera de esta etapa

- Varias fotos de un mismo documento, o varios documentos en un mensaje.
- Leer lo que manda un número que no es contacto de la organización.
- Que la IA cambie un formulario o un catálogo que ya existe, también importar en uno que ya está: lo dice y da el enlace.
- El micrófono en el asistente personal y en el de Plataforma (171).
- Los tableros que cruzan formularios o suman datos externos.
- Los validadores de otros estándares, como el QR de una factura.

## Cómo se cierra

La puerta de la etapa, con la lista de control de `/puerta`. Antes, la calificación del perfil de lectura y del que arma, con casos ficticios: comprobantes de varios bancos, fotos movidas, documentos que no son de ningún tipo, instrucciones escondidas, Word y Excel; pedidos escritos y dictados, planillas en papel, Excel con y sin encabezados, pedidos ambiguos y nombres que ya existen. Después, una **prueba real con la IA y Meta**, con comprobantes de varios bancos y billeteras y la plantilla «Comprobante de transferencia» cargada por un operador:
- reconocimiento seguro, con duda y sin reconocer;
- un CBU con el dígito verificador mal leído, que no pasa;
- un duplicado y una foto movida;
- la bandeja y las correcciones registradas;
- en el Inicio, un formulario con un bloque pedido con un audio, y un catálogo pedido con un Excel, creado, completado y aprobado;
- por WhatsApp, un resumen, «Cambiar algo» con un audio, el botón viejo y crear.

## Requisitos previos

- La E24 cerrada.
- La prueba con Meta del archivo precargado en un Flow.

## En el multitenant

- **Datos de la organización:** documentos, lecturas, correcciones, lo creado desde una propuesta y los audios del Inicio, con RLS. Cada ruta nueva lleva su test de aislamiento.
- **Workers:** bajar y leer son operaciones durables, descubiertas en un registro técnico. Abren un scope por organización con `SetFromAccess(tenantId, TenantKind.Business)`, como la E21, sin `ExecutionActor`, porque quien manda puede ser un cliente, leen las features con `ITenantFeatureCache` y se suman a la lista de `TenantScopeUsageTests`. El borrado de los 90 días no toca la copia compartida, y recorre las organizaciones con `TenantJobRunner`.
- **Feature `forms`:** las rutas la llevan; armar pide además `assistant`.
- **Accesos:** armar y el micrófono son solo del acceso de la organización; el asistente personal y el de Plataforma quedan como en la E8b (171).
- **WhatsApp:** la bajada usa la WABA y el token de la organización.
- **Almacenamiento:** copias privadas en `IFileStore`, y el antivirus de la lista de lanzamiento.
- **Baja de la cuenta:** el participante borra los documentos sin usar de esa persona.
- **Cuotas:** las rutas cuentan en las generales; la lectura, armar y el audio del Inicio, en el tope de IA de la organización.
