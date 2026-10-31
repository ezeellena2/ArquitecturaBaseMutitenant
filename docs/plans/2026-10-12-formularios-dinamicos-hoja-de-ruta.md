# Formularios dinámicos por WhatsApp: hoja de ruta

**Estado:** en curso · aprobada el 2026-10-12, con las decisiones de producto tomadas ([sección 5](#5-decisiones-de-producto)). Está sumada al [plan de desarrollo](2026-09-27-plan-de-desarrollo.md) como **las catorce etapas de la E12 a la E25, en el orden de la decisión 78, después de la E10**. No es un plan de etapa: cada etapa se escribe en el formato de [etapa en paralelo](../guides/etapa-en-paralelo.md) cuando le toque, a partir de su sección de acá.

**Origen.** Es la misma funcionalidad que se está construyendo en `ArquitecturaBase`, adaptada a organizaciones, empresas, plataforma y personas. Allá la Etapa 1 está cerrada y probada con Meta real (envío, respuesta y número guardado), y las demás tienen diseño y decisiones elegidas.

**ArquitecturaBase es referencia, no fuente de código** (decisión del usuario, 2026-10-12): es otro modelo de negocio. Se toma lo que no depende de él (el protocolo de Meta, los límites, los errores ya pagados) y la estructura se arma con las piezas de este repo ([sección 6](#6-qué-tomar-de-arquitecturabase)).

**Specs por etapa:** cada etapa tiene su especificación detallada en [`formularios/`](formularios/README.md), que `/plan-etapa` convierte en el plan corto cuando le toca.

## 1. Qué es el producto

- **Formularios propios.** Una organización arma sus formularios: preguntas tipadas, textos tal como los escribe la persona, sin idioma ni traducciones (decisión 35), versiones y un prefijo de numeración propio. No hay circuitos rígidos: cada formulario es independiente.
- **Por WhatsApp.**
  - Se publican como Flows de Meta en el número de la organización.
  - Se mandan desde el chat, y el empleado o el cliente los completa dentro de WhatsApp (49).
  - La respuesta queda numerada (`VIS-000123`) y se ve en el chat.
- **Catálogos.** Listas compartidas entre formularios (clientes, supervisores, equipos), escritas a mano o tomadas de los datos del sistema con filtros.
- **Fotos y documentos dentro del formulario.** Con cámara o galería: PDF, Excel, Word e imágenes.
- **Planilla.** Cada formulario es una planilla con filtros, detalle y exportación a Excel.
- **Reglas.** Al llegar una respuesta se puede pedir aprobación, armar un PDF y avisar por WhatsApp.
- **Plataforma (nuevo respecto de ArquitecturaBase).** Una biblioteca de plantillas y formularios estándar (formularios, catálogos y reglas de ejemplo) que la organización puede tomar como copia propia, o crear desde cero.
- **El asistente con Casos (E23):** reconoce lo que la persona cuenta (texto o audio), ofrece el formulario prellenado, contesta lo que configure la organización y deriva lo demás.
- **Documentos (E25):** lee con IA lo que mandan los empleados y los clientes y propone el formulario prellenado, con la confirmación de una persona, salvo con «Cargar solo si es seguro» y sus tres señales (71).

## 2. Cómo encaja en el modelo multitenant

### Quién es quién

| Acceso | Rol en formularios |
| --- | --- |
| **Plataforma** | Mantiene la biblioteca de plantillas y habilita el módulo por organización. **Nunca ve formularios, respuestas ni archivos de una organización.** |
| **Organización (B2B)** | Dueña de sus formularios, catálogos, reglas, respuestas y archivos. Los diseña quien tiene permiso; los responden sus empleados y sus clientes (49). |
| **Empresa** | En la v1 los formularios son de la organización; la respuesta guarda las empresas de quien respondió, para filtrar ([decisiones 1 y 9](#5-decisiones-de-producto)). |
| **Empleado** | Miembro activo con permiso `formrecords.create`. Por WhatsApp, además, con teléfono verificado: lo reconoce la bandeja «Internos» de E11. Desde la web (E20) alcanza con su sesión (89). |
| **Cliente de la organización** | Cualquier contacto de la bandeja «Clientes» de E11, tenga o no cuenta. Completa los formularios que son para clientes (decisiones 49 y 50). Si vinculó su cuenta personal, ve la copia en «Mis mensajes». |

### Clasificación de los datos

- **Privado de la organización (`ITenantOwned`, esquema `tenant`, RLS):** definiciones, versiones, catálogos e ítems, respuestas y valores, recibos, reglas, ejecuciones, tareas, documentos y metadatos de archivos.
- **Global de plataforma (esquema `platform`, ADR 0050):** la biblioteca de plantillas y sus versiones.
- **La respuesta de un cliente** es dato de la organización (`ITenantOwned`), como la de un empleado. La copia de quien vinculó su cuenta va por la copia de la conversación de la E11 (que ya es `IConsumerBusinessShared`): Forms no suma tablas compartidas. En «Mis mensajes», la persona ve la tarjeta del formulario con sus propias respuestas.
- **Entrada de WhatsApp:** el webhook sigue global (`identity`). El procesador de entrada resuelve la organización por el número de E11 (`WhatsAppChannels`), arma su contexto con `SetFromAccess` y consulta `IFeatureService` antes de actuar. La captura de una respuesta es una fase aparte, fuera de su transacción y sin `ExecutionActor`, porque un cliente no tiene cuenta (T5 de la E14).
- Un recurso de otra organización responde 404. Cada ruta nueva lleva su test de aislamiento.

### Dependencias

| Dependencia | Para qué | Etapa que la necesita |
| --- | --- | --- |
| **E11: WhatsApp por organización** (números propios, alta embebida, token por organización, bandejas y contactos) | Sin número de la organización no hay a quién mandar, y hasta la E20 tampoco dónde publicar (98). Los Flows se crean en el WABA de la organización y su costo lo paga la organización. Los catálogos de contactos salen de la bandeja Clientes. | E12 en adelante (requisito duro) |
| **E9: endurecimiento** | Redis, límites por organización y el fuzz de aislamiento sobre todas las rutas. El módulo nace con esas guardas y el fuzz cubre sus rutas. | E12 en adelante |
| **E10: almacenamiento en la nube** | El adaptador de nube de `IFileStore` (ADR 0048, D15). En desarrollo alcanza con `LocalFileStore`. | E18 en producción |
| **Excel (P12)** | Importar y exportar catálogos, y exportar la planilla. Se adopta con ClosedXML, que ya usa la E11, detrás de un puerto del módulo. | E12 |
| **PDF** | Ya existe PDFsharp-MigraDoc en el asistente: se reusa y no se suma otra biblioteca. | E21 |

### Módulo `Forms` y canal WhatsApp

- **`Forms` es un módulo quitable aparte, no una parte de WhatsApp** ([decisión 7](#5-decisiones-de-producto)).
  - Es una herramienta general, como WhatsApp y el Asistente, y no un «módulo de negocio» en el sentido de [AGENTS.md](../../AGENTS.md) y la ADR 0032.
  - Nace en la E12, y al cerrarla se suma a la tabla «Dónde va cada cosa» de AGENTS.md.
- **Qué es de cada uno:**
  - **`Forms`:** definiciones, borrador, versiones, numeración, catálogos, respuestas, archivos, planilla, reglas, aprobaciones y PDF.
  - **`WhatsApp`:** armar el `FLOW_JSON`, publicarlo en Meta como operación durable, las sesiones, el mensaje interactivo y la recepción del `nfm_reply`.
- **Ninguno referencia al otro.** `ModuleIsolationTests` ya lo rompe. Hablan por puertos neutrales del núcleo, con la idea de ArquitecturaBase, en `Application/Interfaces/Integrations/StructuredData/`, con sus modelos en `Application/Models/StructuredData/`:
  - `IStructuredFormSource`: WhatsApp lee los formularios publicados que puede mandar;
  - `IStructuredFormChannel`: Forms pide publicar y consulta el estado;
  - `IStructuredRecordSink`: WhatsApp entrega la respuesta capturada;
  - `IStructuredRecordSource`: WhatsApp muestra la respuesta en el chat;
  - `IStructuredContactSource` (E12): Forms lee los contactos para un catálogo, y sus nombres;
  - `IStructuredRecordOriginSource` (E15): Forms lee la conversación de una respuesta.
- **Implementaciones apagadas.** El núcleo trae las implementaciones apagadas de esos puertos:
  - sin WhatsApp, los formularios se diseñan pero no se mandan; desde la E20 se publican solo para la app (98);
  - sin Forms, WhatsApp anda como siempre.
- **Ids y transacciones.**
  - Cada lado guarda los ids del otro como opacos, sin claves foráneas.
  - Cada módulo abre su propia transacción y nunca llama al otro adentro de ella.
- **Registro y EF.**
  - Va en `Domain\Forms\` y `Application|Infrastructure|Api\Modules\Forms\`, con `AddFormsModule()` por capa y EF por `IPersistenceModelContributor`.
  - El retiro se prueba una vez, en la E14, con la base vacía.
- **Feature `forms`.**
  - `IFeatureDefinitionContributor`, `FeatureScope.Business`. La plataforma lo habilita por organización (Plan, Manual o Trial).
  - Con el feature apagado, las rutas responden 404.
- **Permisos de organización:** van en `Permissions.cs` del núcleo, como manda acá, y cada uno entra con la etapa que tiene una ruta que lo usa. Sus nombres van en `Permissions.resx` y `.en.resx` (regla 6), y se ven en el editor de roles:

  | Código | Etapa | Nombre (es) | Nombre (en) | Qué permite (es) | Qué permite (en) |
  | --- | --- | --- | --- | --- | --- |
  | `formcatalogs.read` | E12 | Ver catálogos | View catalogs | Ver los catálogos y sus opciones. | View catalogs and their options. |
  | `formcatalogs.manage` | E12 | Administrar catálogos | Manage catalogs | Crear y editar catálogos, importarlos, darlos de baja y aprobar los que propone el asistente. | Create and edit catalogs, import them, deactivate them and approve the ones the assistant proposes. |
  | `forms.read` | E14 | Ver formularios | View forms | Ver la lista «Formularios» y cada formulario, sin cambiarlo. | View the Forms list and each form, without changing it. |
  | `forms.manage` | E14 | Diseñar formularios | Design forms | Crear y editar formularios en la pestaña «Diseño», copiarlos y borrarlos. | Create and edit forms in the Design tab, copy them and delete them. |
  | `forms.publish` | E14 | Publicar formularios | Publish forms | Publicar un formulario en WhatsApp, darlo de baja y reactivarlo. | Publish a form on WhatsApp, deactivate it and reactivate it. |
  | `formrecords.create` | E14 | Responder formularios | Answer forms | Completar formularios por WhatsApp o desde la web. | Fill in forms on WhatsApp or on the web. |
  | `formrecords.read` | E14 | Ver respuestas | View responses | Ver las respuestas y sus valores. | View responses and their values. |
  | `formrecords.export` | E15 | Exportar respuestas | Export responses | Bajar en Excel las respuestas, con los filtros aplicados. | Download the responses to Excel, with the filters applied. |
  | `formpersondata.manage` | E16 | Administrar los datos de las personas | Manage people data | Definir los datos de las personas, como «Zona». | Define people data, such as Zone. |
  | `formrules.manage` | E21 | Administrar reglas y PDF | Manage rules and PDF | Armar las reglas de un formulario y el diseño de su PDF. | Build a form's rules and its PDF design. |
  | `formapprovals.decide` | E21 | Aprobar o rechazar respuestas | Approve or reject responses | Decidir las respuestas que piden aprobación, según el rol de la regla, menos las que cargó la misma persona (25). | Decide the responses that need approval, according to the rule's role, except the ones the same person submitted. |
  | `formrecords.correct` | E21 | Corregir y anular respuestas | Correct and void responses | Corregir o anular cualquier respuesta, en cualquier momento (93). | Correct or void any response at any time. |
  | `formsuggestions.manage` | E23 | Administrar sugerencias entre formularios | Manage suggestions between forms | Elegir qué formulario se sugiere después de otro. | Choose which form is suggested after another. |
- Cada permiso nuevo trae su migración que actualiza los roles de sistema de las organizaciones existentes (modelo: `20261010203618_SystemRoleAssistantPermissions.cs`).
- **Permiso de plataforma:** uno para mantener la biblioteca (E22), con `[HasPlatformPermission]`.
- **«Para resolver»:** las respuestas por aprobar (E21) aportan un `IPendingItemSource` (ADR 0040), en lugar de un contador propio.

### Premisas del repo aplicadas a formularios

Valen todas las de [AGENTS.md](../../AGENTS.md) y las [fichas](../rules/README.md). Así caen en este módulo:

| Premisa | En formularios |
| --- | --- |
| [Capas y flujo](../rules/capas-y-flujo.md) | Controller → `IForm<X>Service` → servicio → repositorio, reader o puerto. Los helpers usan los sufijos de la lista cerrada ([sección 6](#6-qué-tomar-de-arquitecturabase)). |
| [Guardado](../rules/guardado.md) | Una `ExecuteInTransactionAsync` por método público que escribe. Publicar en Meta, bajar archivos, armar el PDF y avisar van fuera de la transacción, en workers u outbox. |
| [Errores](../rules/result-y-errores.md) | `Forms.<Entidad>.<Motivo>` en `Domain/Forms/<Entidad>Errors`. Toda falla es ProblemDetails con `code` y `traceId`. |
| [Aislamiento](../rules/multitenancy.md) y [permisos](../rules/permisos.md) | Todo es privado salvo la biblioteca. Las rutas llevan `[Access]`, `[HasPermission]` (o `[HasPlatformPermission]`) y `[FeatureGate]`; lo ajeno responde 404. Los workers entran con `Enter(tenantId)` sin transacción abierta. |
| [Concurrencia](../rules/concurrencia.md) | Borrador, catálogos y reglas con `IVersioned` y 409. |
| [Idempotencia](../rules/idempotencia.md) | `[Idempotent]` en los `POST` que crean, publican, mandan o deciden una aprobación. La captura usa el recibo de Meta como clave. |
| [Fechas](../rules/fechas-y-zonas.md) | «Fecha sin hora» es `DateOnly`; lo recibido, `…Utc`. Los vencimientos usan `TimeProvider` y la hora firmada por Meta. |
| [Números](../rules/numeros-y-moneda.md) | «Número con decimales» es `decimal` con precisión y escala, nunca `double`; `null` es «Sin respuesta». |
| [Textos](../rules/textos-y-traducciones.md) y [textos libres](../rules/textos-libres.md) | La interfaz sale de resources es/en. Las preguntas son contenido de la organización: un solo texto, como lo escribió la persona, sin idioma ni traducciones (decisión 35). Lo que escribe la persona pasa por `TextLimits`. |
| Producto genérico | Ningún cliente, tipo de documento ni respuesta va escrito en el código: lo particular es configuración de cada organización (2026-10-24). |
| [Formato](../architecture/backend.md#18-representación-y-formato-de-datos-unificado) | El PDF y los avisos por WhatsApp se formatean con `DisplayFormatter`, con cultura y zona explícitas. |
| [Paginado](../rules/paginado-y-busqueda.md) | Planilla, catálogos y listados paginados, 10 por defecto. En la planilla (E15) y en la bandeja de documentos (E25), los filtros con texto libre van en el cuerpo; los listados simples usan `search` común. |
| [Logs](../rules/logs.md) | `[LoggerMessage]` y `OperationLog.RunAsync`. Nunca el token de sesión, los valores de una respuesta ni teléfonos completos. |
| [Datos personales](../rules/datos-personales.md) | Las respuestas guardan quién respondió: el módulo registra su `IAccountDeletionParticipant`. Qué se conserva lo fija la decisión 23. |
| [WhatsApp](../rules/modulos.md) | Todo envío sale por el outbox. Fuera de las 24 horas, con una plantilla aprobada de la organización (E11 y 61); sin ella, «No se pudo». |
| Datos de ejemplo | No hay: la biblioteca nace vacía y la llenan los operadores. |

### Meta, costos y límites

- **Cada organización publica en su propio WABA**, con el token de E11. Antes de publicar se verifica que el número pertenezca a ese WABA, como en ArquitecturaBase. Sin medio de pago en el WABA, Meta rechaza el envío (en ArquitecturaBase fue el error 141006).
- **Los costos de Meta los paga la organización** (E11).
- **Límites técnicos iguales para todos en la v1:** hasta 10 archivos y 100 MiB por respuesta (tope de Meta), un tope por archivo de la instalación, hasta 50 opciones escritas en una pregunta, hasta 200 de un catálogo en «elegir una» y 20 en «elegir varias», hasta 10.000 opciones por catálogo, 20 campos y 20 datos de las personas (42), y hasta 10.000 respuestas por exportación. Límites por plan, después.

## 3. Etapas

Son catorce, de la E12 a la E25, en el orden de la decisión 78: **el número es el orden.** Entre paréntesis va la etapa de ArquitecturaBase que sirve de referencia. Cada etapa tiene su spec o su resumen en [`formularios/`](formularios/README.md), con la equivalencia con los números de antes, y sus tableros se dibujan en el lienzo antes de programar (decisiones 18 y 44).

**La regla del orden** (78, que controla `/plan-etapa`):
- cada etapa usa solo lo que dejaron las anteriores;
- lo que suma se ve en todas las pantallas que ya existen: un tipo de pregunta nuevo, en la planilla, el detalle, el Excel y el PDF, si ya están;
- nada nombra, prueba ni dibuja algo de una etapa posterior: eso pasa a esa etapa.

**La base, antes de la E12:** lo que se copia de la maqueta, que lista el [plan de desarrollo](2026-09-27-plan-de-desarrollo.md#etapas-12-a-25-formularios-por-whatsapp): los botones (la F5 que sale de la 10), la campana en el núcleo, donde también se avisa la versión nueva, y la letra al 85 %, que confirmó el usuario. Para la prueba con Meta de la E14 hace falta una organización con su número conectado (la I1 de la 10).

**Criterios.** En todas las etapas rige la lista de control de la skill `/puerta`: doce puntos que salen de los 142 problemas de los reviews de ArquitecturaBase ([§4](#4-lo-que-aprendimos-en-arquitecturabase)). Cada etapa suma los suyos. El plan de la etapa los toma como lista de control, y cada punto que aplica lleva su test.

### E12 · Catálogos (AB E1, sin formularios)

- **Nace el módulo `Forms`,** con su feature, sus permisos de catálogos y «Gestión de formularios» con solo «Catálogos».
- **A mano, de los miembros o de los contactos de WhatsApp** (55), con tipo de valor, carga de una vez (XLSX, CSV o pegar), exportar y la plantilla. Hasta 10.000 opciones (42). Un contacto sin nombre toma el de su perfil (81).
- **Guardar todo junto,** mandando y escribiendo solo lo que cambió.
- **Spec:** [formularios/12-catalogos.md](formularios/12-catalogos.md).
- **Criterios:**
  - los números y las fechas de un archivo se leen con la cultura del pedido, y lo ambiguo se rechaza;
  - ninguna lectura trae las 10.000 opciones para quedarse con pocas;
  - guardar sin cambios no toca las opciones, y guardar una opción no reescribe las demás;
  - los límites del canal se validan al guardar; las referencias van por id.

### E13 · Campos de los catálogos (AB E2, la parte del catálogo)

- **Campos por opción** (hasta 20, 42), con tipo y reglas, y valores en jsonb con índice GIN; columnas al pegar, importar y exportar.
- **Qué se ve en WhatsApp:** qué campos van al título y a la descripción de cada opción, con la vista previa del catálogo (36).
- **Spec:** [formularios/13-campos.md](formularios/13-campos.md).
- **Criterios:** las cuatro lecciones de gravedad alta del review E2 de allá, cada una con su test (están en el resumen).

### E14 · Formularios: crear, publicar, mandar y recibir (AB 1)

- **«Formularios»** (79): una sola lista, con «Nuevo formulario»; cada formulario se abre con su pestaña «Diseño».
- **Editor:** siete tipos de pregunta, entre ellos «elegir varias» (38), un `CheckboxGroup` de 1 a 20 opciones con mínimo y máximo, y la pregunta «Elegir de un catálogo», con la presentación del catálogo o la suya (78). Títulos de sección entre las preguntas, que en WhatsApp salen como subtítulo (88). Cada texto es uno solo, sin idioma (35). Prefijo propio, versiones y vista previa en el teléfono.
- **Publicar:** operación durable en Meta, con un Flow por versión y por WABA de la organización.
- **Mandar** desde el chat, a un empleado (Internos) o a un cliente (Clientes), según para quién es el formulario (49 y 50), con una sesión de 24 horas. Las opciones de catálogo viajan en el mensaje, con ids locales a la oferta. Lo que sale por la cola de WhatsApp se reintenta solo (80).
- **Recibir:** la respuesta numerada, idempotente, con los campos de catálogo sellados, y su tarjeta en el chat y en «Mis mensajes». La captura va sin `ExecutionActor`, porque un cliente no tiene cuenta.
- **Módulo:** los puertos neutrales de los formularios y el retiro probado.
- **Spec:** [formularios/14-formularios.md](formularios/14-formularios.md).
- **Criterios:**
  - una publicación incierta se consulta y, si sigue incierta, la resuelve una persona («salió» o «no salió»); lo definitivo termina en «No se pudo»;
  - los reintentos se acotan (5 fallas y 15 minutos), y un mensaje trabado no frena a los demás;
  - si no se sabe si algo salió, la pantalla vuelve a leer sola (43);
  - los campos nuevos de un DTO se exportan con su ejemplo real en el mismo corte; `""` no es `null`; una entrada inválida es 400;
  - sin `formrecords.read`, ningún valor en avisos, vistas previas ni caché; al operador le llegan solo cantidades de opciones;
  - cada variante del Flow se abre en un celular.

### E15 · Respuestas: planilla, detalle, Excel y tablero (AB 3, MF1 y DC0)

- **La lista «Formularios»** suma las respuestas, y cada formulario, sus pestañas «Respuestas» y «Tablero» (79).
- **La planilla** de cada formulario, con las subcolumnas de los campos sellados, el filtro «alguna de» y la empresa (24).
- **El detalle:** la vista rápida al costado y la pantalla completa, con ↑ ↓ en el orden de la planilla.
- **Excel** con los filtros aplicados; «elegir varias» unido con «; ».
- **El tablero de cada formulario** (57): gráficos armados solos desde los tipos de las preguntas, ajustables por cada persona.
- **Spec:** [formularios/15-respuestas.md](formularios/15-respuestas.md).
- **Criterios:**
  - para exportar, una consulta de ids ordenados e hidratación por lotes; una exportación a la vez por persona;
  - la misma regla en memoria y en SQL («sin respuesta» sobre una pregunta que no existía);
  - lo privado (textos buscados, personas y condiciones) nunca va en la URL, el storage ni las cachés persistentes;
  - el nombre del cliente y su conversación salen de WhatsApp por un puerto neutral;
  - EXPLAIN con 10.000 respuestas.

### E16 · Datos de las personas y filtros por destinatario (AB E2, la otra parte)

- **Datos de las personas,** como «Zona», por organización y por miembro (42), con el permiso nuevo `formpersondata.manage`.
- **El papel «Filtro»** de un campo, y los **filtros por destinatario** en una pregunta de catálogo: a quien no tiene el dato no se le manda, y se dice por qué. Al publicar, dos avisos que no frenan (37).
- **Spec:** [formularios/16-datos-de-las-personas.md](formularios/16-datos-de-las-personas.md).
- **Criterios:**
  - nunca se retira el filtro ni se manda la lista entera;
  - nada nombra opciones que la persona no puede leer, tampoco la vista previa;
  - cada exclusión o lock lleva un test con dos actores a la vez.

### E17 · Ciclo de vida y copiar (AB, dar de baja y borrar)

- **Copiar un formulario** (74), con su borrador y un prefijo nuevo; las etapas siguientes suman lo suyo a la copia.
- **Dar de baja** conserva la versión activa y los envíos abiertos; **reactivar** es publicar una versión nueva; **borrar** es lógico, sin respuestas ni envíos abiertos, y deja el prefijo reservado.
- **Deprecar los Flows** por WABA de la organización, como operación durable, al vencer el último envío.
- **Spec:** [formularios/17-ciclo-de-vida.md](formularios/17-ciclo-de-vida.md).
- **Criterios:**
  - un rechazo de Meta al deprecar se clasifica y tiene una salida: no se repite para siempre;
  - un solo filtro de disponibilidad para todo lo que ofrece formularios.

### E18 · Fotos y documentos (AB 2B)

- **Preguntas «Foto» y «Documento»,** con mínimo y máximo, una por pantalla del Flow.
- **Al responder:** se baja cada archivo con el token de la organización, se verifican hash, tamaño y tipo real, y se guarda con `IFileStore`. Completa o nada.
- **Ver:** visor y descarga solo por la Api, con `formrecords.read`, también en la planilla y el detalle. El antivirus está en la lista de lanzamiento.
- **Archivos en el chat** (48): el operador ve y baja lo que le mandan, y manda fotos y archivos desde el compositor, dentro de las 24 horas.
- **Spec:** [formularios/18-fotos-y-documentos.md](formularios/18-fotos-y-documentos.md).
- **Criterios:**
  - las dimensiones de una foto se rechazan por el encabezado, antes de decodificar; el tipo sale del contenido, nunca de la extensión;
  - el nombre de descarga lleva la extensión del tipo verificado y sin caracteres de formato (U+202E);
  - no se confía en los tipos del proveedor: el id de medio puede llegar como número y una opcional vacía llega omitida;
  - si falta el archivo guardado, 404; las miniaturas privadas se cargan solo las visibles.

### E19 · Condiciones, cálculos y líneas (nueva)

- **Preguntas condicionales:** «Mostrar si» otra respuesta cumple una condición. El servidor las vuelve a evaluar al capturar, y la planilla distingue «No se mostró».
- **Cálculos:** una pregunta «Cálculo» con las cuatro operaciones sobre números de la respuesta, campos del catálogo y la suma de una columna de líneas. Se calcula al llegar la respuesta (54).
- **Líneas:** un grupo de renglones con sus columnas, con un tope de 1 a 10 (54).
- **En las respuestas:** la planilla, el detalle y el Excel los muestran.
- **Spec:** [formularios/19-condiciones-calculos-y-lineas.md](formularios/19-condiciones-calculos-y-lineas.md).
- **Criterios:**
  - antes de planificar, se prueban con Meta el componente `If`, los renglones y un archivo dentro de un `If`, en un celular;
  - el servidor nunca confía en lo que llega: vuelve a evaluar condiciones y cálculos;
  - un cálculo que no se puede hacer queda «Sin respuesta», nunca rompe la captura;
  - los montos van en `decimal`, con moneda y redondeo `AwayFromZero`.

### E20 · Cargar una respuesta desde la web (AB, carga desde la app)

- **Un empleado completa un formulario desde la web,** a su nombre, con la misma captura que por WhatsApp: catálogos con sus filtros, archivos, condiciones, cálculos y líneas (73).
- **Las entradas:** «Cargar» en cada fila de «Formularios» y «Agregar respuesta» en la planilla (83). Se carga en una página propia o en un panel al costado (84).
- **Lo propio:** quien solo carga abre «Formularios» y ve la planilla con lo suyo (85).
- **El armado:** hasta tres preguntas por renglón y zonas plegables; el ancho sale del tipo de pregunta (87).
- **Listas dependientes,** solo en la web (94).
- **Formularios solo para la app:** sin número conectado, con una lista dependiente o con un catálogo a mano de más de 200 opciones, se publica sin Meta y se carga desde la web (94, 96 y 98).
- **Spec:** [formularios/20-cargar-desde-la-web.md](formularios/20-cargar-desde-la-web.md).
- **Criterios:**
  - un solo lugar valida y numera; los archivos que se suben se verifican igual que los de WhatsApp;
  - repetir el envío con las mismas respuestas devuelve el primer resultado, aunque después cambie la versión o se dé de baja el formulario;
  - la oferta sella solo lo elegido; la lista de un catálogo se busca en el servidor y nunca se trae entera;
  - lo propio lo filtra el servidor, también en una dirección directa;
  - las columnas y las migas miden su contenedor, no la pantalla.

### E21 · Reglas, aprobación, PDF y avisos (AB 4)

- **Reglas** «Cuando llega una respuesta», con guardado e historial propios, sin versión nueva ni republicar.
- **Un solo motor de acciones,** con ejecuciones idempotentes y el efecto fuera de la transacción.
- **Aprobación de un nivel:** nadie aprueba lo que cargó. «Por aprobar» va en la lista «Formularios» y en «Para resolver», con la misma fuente (ADR 0040).
- **PDF** como copia fija, con los campos «Dato», las fotos, las líneas y los cálculos.
- **Corregir y anular una respuesta** (91 a 93), con el permiso nuevo «Corregir y anular respuestas». La corrección reemplaza a la anterior con el mismo número y vuelve a pasar por las reglas; anular deja la respuesta «Anulada», sin borrarla.
- **Lo propio de quien solo carga** (85) suma el estado, el PDF y el motivo de rechazo.
- **Avisos por WhatsApp** con el número de la organización. Fuera de las 24 horas, con una plantilla aprobada de la organización (61), que suma el encabezado con documento y el llenado de sus variables.
- **Avisar a una persona y mandar por correo** (62), con el PDF adjunto si se elige, y la pregunta «Correo».
- **El diseño del PDF:** una base bien hecha con logo, color, encabezado, secciones, mostrar u ocultar preguntas y firmas (59). El logo y los datos salen de la página pública, y el color se elige en cada formulario (82).
- **Mandar el PDF a un cliente** de la organización (47), al chat del contacto elegido en el formulario (55).
- **Spec:** [formularios/21-reglas-y-aprobacion.md](formularios/21-reglas-y-aprobacion.md).
- **Criterios:**
  - la configuración que usa un efecto se valida al reservar (un rol borrado colgaba una aprobación);
  - un destinatario inalcanzable no tumba a los demás;
  - lo sellado manda, y no se copia la respuesta entera en cada ejecución;
  - el PDF se muestra con `<object>`, con descarga como alternativa, nunca en un iframe con sandbox;
  - cada worker tiene un test de su bucle.

### E22 · Biblioteca de plantillas de la plataforma (nueva)

- **Plataforma:** arma plantillas (formulario, catálogos a mano, de los miembros o de los contactos sin filtros, reglas de ejemplo y diseño del PDF), con categoría, descripción, versiones y publicada o retirada. Datos globales en `platform`, con auditoría (ADR 0050).
- **Es la biblioteca de formularios estándar** (40).
- **Organización:** «Desde una plantilla», en «Nuevo formulario ▾» (79), crea una copia independiente en borrador, con sus catálogos y el diseño del PDF. Las reglas de ejemplo se muestran precargadas hasta que guarda las suyas (30 a 33). Nunca se publica sola.
- **Privacidad:** la plataforma solo ve cuántas organizaciones usaron cada plantilla.
- **Spec:** [formularios/22-biblioteca.md](formularios/22-biblioteca.md).
- **Criterios:**
  - la plataforma no tiene ninguna ruta que lea datos de una organización;
  - las referencias de una plantilla van por id local, nunca por nombre.

### E23 · El asistente, con Casos (AB 5 y Casos)

- **El asistente atiende empleados y clientes** (51). Cada organización configura, por bandeja, quién atiende los chats nuevos y cuándo vuelve un chat al modo configurado; cada chat se cambia o se fija a mano (52). Se configura con «Administrar WhatsApp» (28).
- **Armar una regla desde un texto** (63): la IA propone la regla y la persona la revisa y la guarda.
- **Sin información cargada** (67): ayuda con lo general, nunca inventa datos de la organización, pregunta y deriva, y anota la pregunta en «Preguntas sin respuesta».
- **Límites por bandeja** (65), y el tope de ofrecimientos lo elige cada organización (66).
- **Casos** (39): cada organización configura perfil, conocimiento, casos y límites. Seis acciones, respuestas redactadas dentro de los límites y «Cualquier otra cosa» al final. Pide su ADR de respuestas redactadas.
- **Cómo trabaja:** historial de 24 horas de esa conversación, ráfagas de 4 s con tope de 15 s y hasta 3 acciones por turno. El asistente manda el formulario y lo prellena con un perfil nuevo del Flow.
- **Lo de la etapa 5 de allá:** sugerencias al operador y entre formularios, audios transcriptos (27), «Formularios que faltan» y el catálogo propuesto.
- **Desde el primer día:** la espera corta, el aislamiento por conversación y la medición.
- **Costos:** dentro del tope mensual de IA de la organización (ADR 0051).
- **Spec:** [formularios/23-asistente.md](formularios/23-asistente.md).
- **Criterios:**
  - el modelo lee o propone, y ejecuta el módulo dueño;
  - un texto redactado que no pasa el validador no sale y deriva;
  - el historial nunca es de otra conversación ni de otra organización, con un test de dos personas a la vez;
  - se mide de texto a envío y de audio a envío.

### E24 · Varios asistentes, uno por equipo (nueva)

- **Varios asistentes por organización,** cada uno con su perfil, sus casos y sus límites; el conocimiento lo comparten (46).
- **Quién contesta: por equipo** (68). Cada equipo de la bandeja tiene su asistente, y un chat sin equipo lo contesta el principal.
- **Spec:** [formularios/24-varios-asistentes.md](formularios/24-varios-asistentes.md).
- **Criterios:**
  - en cada chat se ve qué asistente contesta;
  - cambiar de equipo cambia el asistente desde el mensaje siguiente, con el mismo historial.

### E25 · Documentos por WhatsApp (nueva en el bloque)

- **Lee con IA** lo que mandan los empleados y los clientes (foto, PDF, Word, Excel, ubicación y contacto) y propone el formulario prellenado (70). Confirma una persona, salvo con «Cargar solo si es seguro» y sus tres señales (71).
- **La «Lectura de documentos»** de un formulario y de una plantilla de la biblioteca.
- **Pantallas:** la bandeja «Documentos recibidos» y «Armar formulario con la IA».
- **Armar formularios y catálogos desde el asistente de la web** (75): se le pasa un PDF o se le escribe cómo se quiere, y crea el borrador y los catálogos por aprobar.
- **Validaciones fijas:** un conjunto cerrado en el código, y la tabla de entidades del BCRA como dato de referencia; lo descartado se borra a los 90 días; la lectura entra en el tope de la organización, con un tope por persona y por día (41).
- **Para decidir al planificar:** «Pasar al formulario» desde la bandeja, que es cargar a nombre de otro.
- **Spec:** [formularios/25-documentos.md](formularios/25-documentos.md).
- **Criterios:**
  - precargar un archivo en un Flow se prueba con Meta antes de planificar;
  - la IA solo carga sola con las tres señales de la 71, nunca con la seguridad que declara el modelo; lo leído es un dato, nunca una instrucción;
  - las validaciones fijas corren en el servidor, sin IA, también al confirmar.

### Después del bloque

- **Integraciones, lo primero después del bloque** (76): un módulo `Integrations`, con conexiones y credenciales por organización. La API pública y el MCP necesitan una identidad por organización. Quedan abiertas las 13 decisiones de su estudio.
- **Catálogos, fases 3 a 6:** la búsqueda en vivo (56), las listas dependientes en WhatsApp (94) y los orígenes externos.
- **La conexión a la base de un cliente,** con una solución simple: un agente del lado del cliente o una réplica de solo lectura.
- **Los tableros que cruzan formularios.**
- **Importar respuestas desde Excel, y cargar a nombre de otra persona.**
- **Varios niveles de aprobación** (60).
- **Límites por plan.**

## 4. Lo que aprendimos en ArquitecturaBase

Aplicarlo desde el primer día evita lo que nos frenó la Etapa 1 durante días:

1. **Robustez proporcional al efecto** (ADR 0018 de ArquitecturaBase). Mandan nuestras fichas de [guardado](../rules/guardado.md), [concurrencia](../rules/concurrencia.md) e [idempotencia](../rules/idempotencia.md):

   | Efecto | Cómo se hace acá |
   | --- | --- |
   | **Fuera del sistema:** Meta, descarga de medios, almacenamiento, IA y mensajes a personas | Operación durable: un trabajo con lease y fence en un registro técnico (como `AssistantTurnWorks`). Antes de cada llamada se commitea `InFlight`, y la llamada va fuera de la UoW. Lo incierto queda «Sin confirmar» y se consulta antes de repetir, salvo lo que sale por la cola de WhatsApp, que se reintenta sola (80). Idempotencia por el id del trabajo o de la sesión, y `[Idempotent]` en el `POST` que lo pide. Pruebas de varios hosts. |
   | **Lectura:** planilla, filtros, Excel | Sin recibos ni leases: presupuesto de tiempo y topes. |
   | **Configuración interna:** borradores, catálogos, reglas, diseño del PDF, plantillas | Una sola `ExecuteInTransactionAsync`, `IVersioned` y 409 `General.ConcurrencyConflict`. `[Idempotent]` solo en las altas, y sin recibos de 30 días. |
2. **El contrato es el código:** DTO, OpenAPI y ejemplos exportados, no un documento de contrato de 60 KB. Los ejemplos salen de una corrida real, con sus estados y sus errores de negocio, y el front los valida con zod estricto (2026-10-24: en ArquitecturaBase, el back mandaba `pending` y el front esperaba `Pending`).
3. **Planes cortos.** Un resumen por etapa con las decisiones ya tomadas, que no se vuelven a preguntar. Lo que el plan no fija se resuelve con lo más simple que cumpla las reglas. Un contrato todavía no publicado se cambia sin capas de compatibilidad.
4. **Tests rápidos por corte;** integración y visuales una vez, al terminar.
5. **Pantalla nueva: primero el tablero**, elegido por el usuario, y el recorrido guiado en el lienzo (44).
6. **Errores que ya pagamos:**
   - una respuesta con error frenaba la cola entera, y ahora se aísla por mensaje;
   - las respuestas pasados 30 minutos se perdían, y ahora la sesión dura 24 horas y vence según la hora de Meta;
   - las respuestas grandes se descartaban sin rastro, y ahora hay un presupuesto de 16 KiB al publicar y un rechazo visible;
   - errores definitivos de Meta quedaban «Sin confirmar» para siempre;
   - en la Argentina, «125.500» puede leerse como 125,5, por eso la ayuda pide punto decimal y se valida la escala.
7. **La lista de control de la puerta.** Los 142 problemas de los reviews de ArquitecturaBase quedaron en doce puntos, en la skill `/puerta` y en [tests](../rules/tests.md): números y fechas con la cultura del pedido, el front que no tapa al servidor, cambiar algo en uso, guardar sin cambios, lecturas con el volumen máximo, nada que nombre lo que no se puede leer, nada que exija un idioma, un test por cada lock, migraciones portables, botones y avisos (43), arreglos en los caminos hermanos y respuestas de proveedores clasificadas. Cada etapa del bloque la aplica, con un test por punto.

## 5. Decisiones de producto

Decididas por el usuario entre el 2026-10-12 y el 2026-10-24. No se vuelven a preguntar.

1. **Formularios por organización.** En la v1 son de la organización; la respuesta guarda la empresa de quien respondió y la planilla filtra por empresa. Formularios y permisos por empresa (`ICompanyOwned`), después.
2. **Las plantillas se toman como copia independiente.** Una referencia obliga a resolver qué pasa con las respuestas cuando la plataforma cambia la plantilla.
3. **B2C, después del bloque,** con la bandeja Clientes de E11 andando y el backlog #51 resuelto. Reemplazada por la 49 para los formularios: los clientes los completan desde el principio.
4. **El número de la plataforma (E8) no manda formularios:** es para ingreso y avisos de cuenta.
5. **Sin límites por plan en la v1** (formularios, respuestas, almacenamiento): solo se habilita el feature. Medir el uso real antes de poner topes.
6. **El bloque va después de la E10:** 8b → 11 → 9 → 10 → 12 a 19 (12 a 17 cuando se decidió). Nace con Redis, límites y el fuzz de la E9, y con la nube de la E10.
7. **`Forms` es un módulo aparte de `WhatsApp`,** unidos por puertos neutrales del núcleo, como en ArquitecturaBase.
8. **Alcance:** seis etapas, con la biblioteca al final. El asistente en WhatsApp (antes F7) se programa después; su diseño se adelanta (decisión 19). Reemplazada por la 34.
9. **Varias empresas:** si quien responde es miembro de varias empresas, la respuesta guarda todas sus empresas activas, y el filtro por empresa la encuentra por cualquiera.
10. **Quién manda un formulario desde el chat:** cualquiera con «Atender internos», sin permiso aparte. Lo que limita es que quien recibe tenga `formrecords.create`. Desde la 49, a un cliente lo manda quien tiene «Atender clientes».
11. **Nombres largos en un catálogo de miembros:** si pasan los 30 caracteres del título de una opción en WhatsApp, se cortan con «…» y el nombre completo va en el detalle. Se le suma la presentación (36).
12. **Catálogo con filtros de más de 200 opciones:** se puede guardar con aviso; el bloqueo es al mandar. Se le suman los avisos de la 37.
13. **Biblioteca, edición:** borrador y publicar, con versiones; publicar y retirar piden motivo.
14. **Biblioteca, catálogos:** una plantilla trae solo catálogos a mano. Cambiada por la 64: también trae catálogos de los miembros y de los contactos, sin filtros.
15. **Biblioteca, categorías:** las arma la plataforma (nombre y orden), sin listas fijas en el código. El nombre es un solo texto desde la 45.
16. **Biblioteca, uso:** la plataforma ve solo cuántas organizaciones usaron cada plantilla, nunca cuáles.

Decididas el 2026-10-17, con el [traspaso de ArquitecturaBase](2026-10-17-formularios-traspaso/README.md):

17. **Idioma.** Reemplazada por la 35 (2026-10-24), que saca el idioma y las traducciones del contenido. Decía, con la regla que ArquitecturaBase adoptó el 2026-10-16:
    - solo el idioma original es obligatorio para guardar y publicar, y la traducción es opcional;
    - si la traducción no está completa, el formulario, el PDF, los mensajes y las opciones de catálogo salen enteros en el idioma original, sin mezclar;
    - el idioma de las pantallas del sistema no cambia.
18. **Pantallas.** Se copian de ArquitecturaBase:
    - Diseño rehace los tableros de la 12 y la 13 con la disposición de su lienzo y del [05 del traspaso](2026-10-17-formularios-traspaso/05-pantallas.md), con las piezas y los criterios de nuestro lienzo y sin mostrar alternativas;
    - la 17 no existe allá: sus tableros se quedan y solo se ajustan al editor nuevo;
    - si la disposición choca con una regla escrita de este repo, manda la de acá.
19. **Asistente** (antes F7, la etapa 5 de ArquitecturaBase). Se diseña ahora, después de los tableros de la 16. Se sigue programando después del bloque. Desde la 34, es la E23, después de la E22, con Casos (39).

Elegidas por el usuario el 2026-10-17, entre las que quedaron al alinear el bloque con el traspaso (todas las opciones recomendadas):

20. **Catálogo a mano en otro idioma que el formulario:** solo sirven los catálogos con el mismo idioma original que el formulario; los demás no aparecen en el selector (C11 de la E12). Deja de aplicar con la 35 (2026-10-24): el contenido no tiene idioma, así que sirve cualquier catálogo de la organización.
21. **Nombres de miembros de más de 30 caracteres:** se mantiene la decisión 11, que los corta con «…» y pone el nombre completo en el detalle. ArquitecturaBase no trunca, pero acá el nombre es el de la cuenta de cada persona y bloquear dejaría sin uso el catálogo.
22. **Catálogo con filtros de más de 200 opciones:** se mantiene la decisión 12. Se guarda con aviso, y el bloqueo es al mandar. El usuario la confirmó el 2026-10-17, después de los tableros de la v129, que se corrigen.
23. **Cuánto se guardan las respuestas, sus archivos y sus PDF:** sin plazo mientras exista la organización. Se borran con la baja de la organización, y lo personal de quien da de baja su cuenta, con el participante de la baja. Se revisa con el primer cliente.
24. **La empresa en la planilla:** un filtro «Empresa ▾» y una columna «Empresas» que se puede ocultar desde «Columnas».
25. **Quién aprueba:** cualquiera de la organización con el rol de la regla y `formapprovals.decide`, salvo quien cargó la respuesta.
26. **Avisos por WhatsApp fuera de las 24 horas:** quedan «No se pudo», a la vista, y el PDF queda guardado, como en ArquitecturaBase. Reemplazada por la 61: fuera de las 24 horas, todos los avisos van con una plantilla de la organización.
    - El usuario había elegido la A (plantillas de Meta), pero pedía un mecanismo que el proyecto no tiene: plantillas del sistema en la WABA de cada organización, con su alta y aprobación por WABA y las variables armadas solas.
    - `docs/operations/whatsapp-plantillas.md` cubre solo las del número de la plataforma, y la E11 manda solo una plantilla que elige una persona en el chat.
    - Por eso, la Guía la pasó a la B el 2026-10-17, por la condición de abajo.
27. **Transcribir audios en el asistente:** sí, por un puerto del núcleo en `Application/Interfaces/Integrations`, como el transporte del asistente. La clave va en user-secrets (ficha de configuración), y el costo entra en el tope mensual de IA de la organización (ADR 0048 y 0051). El proveedor se elige con su ADR al planificar el asistente.
28. **Quién pasa una conversación a modo Asistente:** quien tiene «Administrar WhatsApp», sin permiso nuevo.
29. **Dónde actúa el asistente:** solo en la bandeja Internos, con empleados. Reemplazada por la 51.

Elegidas por el usuario el 2026-10-18, sobre las reglas de ejemplo de la biblioteca (E22), todas las recomendadas:

30. **Reglas que piden un rol, al usar una plantilla:** llegan precargadas y sin guardar en «Cuando llega una respuesta», con «Elegí el rol» en cada acción que lo pide, y se activan al guardarlas. Hasta entonces, el formulario se puede publicar sin reglas.
    - **Sin estado nuevo:** la copia no guarda las reglas de ejemplo. El editor las lee de la versión de la plantilla de la que salió el formulario, que la copia ya conserva (B9 de la E22), mientras el formulario no tenga ninguna revisión de reglas.
    - Al guardar la primera revisión, aunque esté vacía, dejan de mostrarse.
31. **Diseño del PDF:** la plantilla lo trae, y se copia como primer diseño, porque no tiene datos de la organización.
32. **Rol sugerido:** la plantilla puede mostrar un «Rol sugerido» (por ejemplo, «Supervisor») como ayuda, sin elegir ningún rol sola: las referencias van por id, nunca por nombre.
33. **Dónde edita la plataforma las reglas y el PDF de una plantilla:** en la pestaña «Plantilla», debajo de las preguntas, con la misma sección de la E21 que en el editor de la organización.

Elegidas por el usuario el 2026-10-24, con la [evaluación del traspaso 2](2026-10-24-formularios-evaluacion-traspaso-2.md), todas las recomendadas:

34. **Once etapas, en este orden** (cambiada por la 54, la 69 y la 73, que suman la E19, la E24 y la E20, y por la 78, que fija el orden y los números):
    1. E14, formularios;
    2. E17, ciclo de vida;
    3. E12, catálogos base;
    4. E13, campos extra;
    5. E16, datos de las personas y filtros por destinatario;
    6. E18, fotos y documentos;
    7. E15, «Formularios», planilla, detalle y Excel;
    8. E21, reglas, aprobación, PDF y avisos;
    9. E22, biblioteca;
    10. E23, el asistente con Casos;
    11. E25, Documentos por WhatsApp.

    Reemplaza la 8. El alcance está en la §3 de la evaluación. Dos cambios de fondo:
    - el ciclo de vida (dar de baja, reactivar, borrar y deprecar los Flows por WABA) entra al bloque;
    - la navegación queda como en ArquitecturaBase. El menú principal dice «Gestión de formularios», y su panel tiene Formularios, Diseñar formularios, Catálogos y Formularios que faltan (la 79 saca «Diseñar formularios»);
    - «Formularios» (MF1, E15) es la lista de formularios, y desde ahí se entra a cada uno, con sus respuestas y sus acciones;
    - una respuesta se ve rápido en el panel de la derecha y completa en su propia pantalla, también con sus acciones.
35. **Idioma** (reemplaza la 17): el contenido no tiene idioma.
    - Lo que crea una organización (formularios, catálogos, opciones, campos y datos de las personas) se guarda en un solo texto, tal como lo escribió la persona, en el idioma que quiera.
    - No depende del idioma elegido en el menú. En palabras del usuario: «si el usuario quiere trabajar en inglés y crea un formulario en español, es problema del usuario».
    - El botón de idioma del encabezado cambia solo los textos del sistema.
    - No hay traducciones del contenido: no hay selector, pares «Nombre» / «Nombre en inglés», vista previa en dos idiomas ni avisos de traducción.
    - Un formulario se ve y se manda igual con el menú en cualquier idioma. Por WhatsApp sale como está escrito, y los textos del sistema que lo acompañan van en el idioma de la cuenta de quien recibe.
36. **Nombres de más de 30 caracteres** (11 y 21): se mantiene el recorte con «…». Se suma la presentación de la E13, que elige qué campos van al título (30) y a la descripción (300).
37. **Catálogos de más de 200 opciones** (12 y 22): se mantienen. Al publicar se suman dos avisos que no frenan, el máximo por persona y las personas sin el dato, y el tope de 20 en «elegir varias».
38. **«Elegir varias»:** va en la E14, con los demás tipos de pregunta.
39. **El asistente con Casos** (E23): contesta lo que configure cada organización (perfil, conocimiento, casos y límites), redacta dentro de esos límites y deriva lo demás. Reemplaza «lo que no es un formulario no se contesta».
    - En la primera versión, solo en Internos. Cambiada por la 51: también en Clientes.
    - Se configura con «Administrar WhatsApp»: se mantiene la 28.
    - Pide un ADR de respuestas redactadas, como la 0021 de ArquitecturaBase.
40. **Biblioteca** (E22): es la misma que la de formularios estándar. La «Lectura de documentos» de una plantilla se suma en la E25.
41. **Documentos** (E25):
    - los usados siguen a su respuesta (23), y los descartados o sin clasificar se borran a los 90 días;
    - la lectura usa el proveedor de la plataforma y entra en el tope mensual de IA de la organización, con un tope por persona y por día.
42. **Lo que es igual para todas las organizaciones:**
    - los idiomas: español e inglés, sin configurar;
    - los textos del sistema que recibe una persona, en el idioma de su cuenta;
    - los topes de catálogos: 10.000 opciones, 20 campos y 20 datos de las personas.

    Los datos de las personas son por organización y por miembro. En la v1 no dependen de la empresa ni hay para B2C. El usuario lo reconfirmó el 2026-10-24 con la 49: los clientes no tienen datos de las personas.
43. **Botones y avisos, en todo el sistema** (`pantallas-y-ui.md` y `formularios.md` del front):
    - un botón está habilitado o grisado, sin textos al lado ni debajo. Mientras trabaja, queda grisado con su mismo texto;
    - nunca aparece «Cambios sin guardar»;
    - si falta un dato obligatorio, el botón queda habilitado, y al tocarlo se marca el campo con el error de siempre;
    - una opción de menú que no se puede usar queda solo grisada, con el motivo en su tooltip. Reemplaza la decisión del 13/10;
    - la única cuenta en un botón es «Reenviar en 30 s», al pedir un código.
44. **Ver antes de programar:** el lienzo navegable, con un recorrido guiado por etapa, sin maqueta viva.

Elegidas por el usuario el 2026-10-24, al actualizar las specs:

45. **El nombre de una categoría de la biblioteca** (E22) es un solo texto, como el resto de la biblioteca. Reemplaza el «es/en» de la 15.
46. **El conocimiento del asistente** (E23) es una pestaña de la pantalla del asistente: la información del negocio de cada organización, que se edita ahí, con el mismo Guardar y con «Administrar WhatsApp». No suma pantalla ni permiso.
47. **Mandar el PDF a un cliente** (E21): una acción más de las reglas, que le manda el PDF a un cliente de la organización (un contacto que no es empleado), al chat del contacto elegido en una pregunta de un catálogo «De los contactos» (55). Dentro de las 24 horas va como documento; fuera, con una plantilla aprobada de la organización (E11). Para esta acción cambia la 26, porque la E11 ya trae las plantillas de cada organización.
48. **Archivos en el chat** (E18): en todos los chats de atención, el operador ve y baja lo que le mandan (fotos, audios, documentos y videos) y puede mandar archivos desde el compositor, dentro de las 24 horas.

Elegidas por el usuario el 2026-10-24, al revalidar el bloque (paso 1, quién participa):

49. **Los clientes completan formularios desde el principio.** Cualquier contacto de la bandeja Clientes, tenga o no cuenta: se lo reconoce por su número. Si vinculó su cuenta personal, ve la copia en «Mis mensajes». Reemplaza la 3 para los formularios.
50. **Cada formulario dice para quién es:** solo empleados, solo clientes, o los dos. El envío, el asistente y las sugerencias ofrecen solo los que corresponden a quien recibe.
51. **El asistente atiende también a los clientes,** en la bandeja Clientes, con los mismos límites configurables. Reemplaza la 29.
52. **El modo de atención es configurable** (la 77 lo precisa con varios asistentes):
    - cada organización elige, por bandeja, quién atiende los chats nuevos: el asistente o una persona;
    - y cuándo vuelve un chat al modo configurado después de que lo tomó una persona: cuando pasa un tiempo sin mensajes (que elige la organización), cuando la persona lo marca «Resuelto», o nunca;
    - cada chat se cambia a mano en cualquier momento, en los dos sentidos, y se puede fijar para que no vuelva solo;
    - el bot del menú de la E11 aparece solo en los chats de Clientes que están en atención manual.
53. **Las preguntas condicionales y los cálculos van dentro del bloque.**

Elegidas por el usuario el 2026-10-24, al revalidar el bloque (paso 2, armar y mandar un formulario):

54. **Condiciones, cálculos y líneas son una etapa propia, la E19,** después de la E16, porque un cálculo usa los campos de los catálogos. Los cálculos se hacen al llegar la respuesta: quien completa no ve el total en WhatsApp antes de enviar. Las líneas (un pedido con varios productos) llevan un tope de renglones, que se prueba antes con Meta. La 78 la pone después de la E18.

Elegidas por el usuario el 2026-10-24, al revalidar el bloque (paso 3, catálogos):

55. **Un catálogo puede salir de los contactos de WhatsApp** de la organización (E12), como hoy sale de los miembros. Así, para mandar el PDF a un cliente (47), se elige su contacto en una pregunta y el PDF va a su chat, sin cargar su teléfono dos veces. El teléfono no se muestra.
56. **Los catálogos grandes** (más de 200 opciones para una persona) se resuelven por ahora con los filtros por persona; la búsqueda mientras se escribe queda después del bloque.

Elegidas por el usuario el 2026-10-24, al revalidar el bloque (paso 4, ver las respuestas):

57. **El tablero automático de cada formulario** pasa de la E25 a la E15, junto con la planilla: no depende de los documentos.
58. **Corregir una respuesta** entra al bloque, en la E21: con un permiso nuevo, «Corregir respuestas»; solo mientras está recibida o por aprobar, porque lo aprobado queda fijo con su PDF; solo los valores, no los archivos. Conserva el número, queda marcada «Corregida» y la actividad guarda qué cambió. El Excel sigue con hasta 10.000 respuestas por vez. Reemplazada por la 91 a la 93.

Elegidas por el usuario el 2026-10-24, al revalidar el bloque (paso 5, reglas, aprobación y PDF):

59. **El diseño del PDF** (E21): una base bien hecha, con personalización acotada: el logo, el color de la organización, un encabezado con sus datos, las preguntas agrupadas en secciones con título y en el orden que se elija, mostrar u ocultar cada pregunta, y un lugar para firmas. Sin editor libre. Diseño muestra dos o tres estilos para elegir. La 82 dice de dónde salen el logo, los datos y el color.
60. **La aprobación es de un nivel.** Varios niveles, después del bloque.
61. **Fuera de las 24 horas, todos los avisos de WhatsApp** (a empleados y a clientes) van con una plantilla aprobada de la organización. Reemplaza la 26. Si la organización no tiene la plantilla aprobada, el aviso queda «No se pudo», a la vista, para que nadie espere un envío que no puede salir.
62. **Dos acciones más en las reglas** (E21):
    - **avisar a una persona** puntual, no solo a un rol;
    - **mandar por correo**: a quien cargó, a un rol o a una persona, al correo de su cuenta; a una dirección fija escrita en la regla; o al correo de una pregunta del formulario, con un tipo de pregunta «Correo» nuevo.
63. **Armar una regla desde un texto** (E23): la persona la escribe en palabras, la IA la convierte en una regla normal, y la persona la revisa y la guarda. Después corre siempre igual, sin IA. La IA no interpreta el texto en cada respuesta. La propuesta entra en el tope mensual de IA de la organización (ADR 0051): con el tope agotado, «Armar desde un texto» queda grisado, con el motivo en su tooltip, y la regla se arma a mano.

Elegidas por el usuario el 2026-10-24, al revalidar el bloque (paso 6, la biblioteca):

64. **Una plantilla puede traer catálogos de los miembros y de los contactos,** sin filtros, además de los escritos a mano. La organización les pone sus filtros después de usarla. Cambia la 14. El usuario reconfirmó que la copia es independiente (no se entera de las versiones nuevas) y que todas las organizaciones ven todas las plantillas.

Elegidas por el usuario el 2026-10-24, al revalidar el bloque (paso 7, el asistente):

65. **Los límites del asistente son uno por bandeja:** con los clientes, la organización puede ser más estricta que con los empleados.
66. **El tope de ofrecimientos de formularios** (2 por conversación y día) lo elige cada organización, dentro de un máximo de la plataforma.
67. **Sin la información cargada,** el asistente ayuda con lo general, que no compromete a la organización. Lo de la organización (precios, envíos, stock, horarios, garantías) lo contesta solo si está cargado. Si falta, pregunta para entender, dice que lo consulta y deriva a una persona, y anota la pregunta en «Preguntas sin respuesta», agrupada por tema, para que la organización complete su conocimiento.
68. **Varios asistentes, uno por equipo:** cada equipo de la bandeja tiene su asistente, con su perfil, sus casos y sus límites; el conocimiento lo comparten. Un chat sin equipo lo contesta el principal, y en cada chat se ve quién contesta.
69. **Los varios asistentes son una etapa propia, la E24,** después de la E23.

Elegidas por el usuario el 2026-10-24, al revalidar el bloque (paso 8, los documentos):

70. **También se leen los documentos que mandan los clientes,** con los formularios que son para clientes (50).
71. **La carga sin confirmar entra al bloque,** en la E25: un interruptor «Cargar solo si es seguro» por formulario, que carga sin preguntar solo si el formulario quedó reconocido sin duda, las validaciones dan bien sin duplicado, y ese formulario tiene sus últimas 20 cargas confirmadas sin correcciones. Nunca se usa la seguridad que declara el modelo, y lo cargado se puede corregir (91).
72. **El único formulario estándar de la primera versión** es «Comprobante de transferencia». Los demás los suma la plataforma después, como plantillas.

Elegidas por el usuario el 2026-10-24, al revalidar el bloque (paso 9, lo que queda afuera):

73. **Cargar una respuesta desde la web** entra al bloque: un empleado, a su nombre, con la misma captura que por WhatsApp. Es la etapa E20, después de la E18.
74. **Copiar un formulario** entra al bloque, en la E17.
75. **El asistente de la web arma formularios y catálogos:** se le pasa un PDF o se le escribe cómo se quiere, y crea el formulario en borrador y los catálogos por aprobar, y lleva al editor. Nada se publica solo. Va en la E25, con «Armar formulario con la IA».
76. **Después del bloque, lo primero son las integraciones:** ARCA, Mercado Libre, Mercado Pago y webhooks, con plantillas conectadas en la biblioteca.

Elegida por el usuario el 2026-10-24, en la revisión completa del bloque:

77. **Con varios asistentes (E24), el chat nuevo arranca con el menú del bot,** aunque la bandeja esté en modo Asistente. La persona elige el equipo, y desde ahí le contesta el asistente de ese equipo. Con un solo asistente, el menú sigue apareciendo solo en atención manual (52).

Elegidas por el usuario el 2026-10-25, al revisar el orden del bloque con la Guía y con dos revisiones independientes:

78. **Los catálogos van primero, y el número de cada etapa es su orden:** E12 catálogos, E13 campos de los catálogos, E14 formularios (con las preguntas de catálogo, la oferta, la presentación propia de una pregunta y el sellado), E15 respuestas, E16 datos de las personas y filtros por destinatario, E17 ciclo de vida y copiar, E18 fotos y documentos, E19 condiciones, cálculos y líneas, E20 cargar desde la web, E21 reglas, aprobación, PDF y avisos, E22 biblioteca, E23 asistente, E24 varios asistentes y E25 documentos. Cada etapa usa solo lo de las anteriores, lo que suma se ve en las pantallas que ya existen, y nada nombra, prueba ni dibuja algo de una etapa posterior. Cambia la 34; la equivalencia con los números de antes está en [`formularios/`](formularios/README.md). Antes de la E12 va la base: los botones (la F5 de la 10), la campana en el núcleo y el tamaño general.
79. **Una sola lista «Formularios»,** con «Nuevo formulario»; «Desde una plantilla» se suma en la E22. Cada formulario se abre con pestañas: «Diseño» desde la E14, «Respuestas» y «Tablero» desde la E15, y las de las etapas siguientes. Sale «Diseñar formularios». Lo que ve cada persona depende de sus permisos.
80. **Lo que sale por la cola de WhatsApp y no se sabe si salió se reintenta solo,** como los demás mensajes. Salen «Envío sin confirmar», «Ya lo revisé» y «Resolver el envío» del envío de formularios (E14) y de los avisos de las reglas (E21). La cola del núcleo no suma un estado incierto.
81. **Un contacto sin nombre toma el nombre de su perfil de WhatsApp** cuando escribe, y se puede cambiar. Va en la E12, con la fuente «Contactos». El teléfono nunca se muestra.
82. **El PDF toma el logo y los datos de la página pública** de la organización. El color se elige en el diseño del PDF de cada formulario, y arranca con el del sistema de entrada (E21).

Elegidas por el usuario el 2026-10-29 y el 2026-10-30 en ArquitecturaBase, viendo cada tablero, y adoptadas con el [traspaso 3](../../../ArquitecturaBase/docs/plans/2026-10-30-traspaso-multitenant-carga-desde-la-app.md), porque no chocan con este modelo:

83. **Las entradas para cargar desde la web** (E20): «Cargar» en cada fila de «Formularios» y «Agregar respuesta» en la planilla, sin una entrada nueva en el menú. Quien solo tiene `formrecords.create` abre «Formularios» con sus formularios activos y «Cargar», sin conteos.
84. **Página y panel** (E20): «Nueva respuesta» es una página propia, para la compu y el teléfono. Desde la planilla, en la compu, se abre un panel al costado, con «Mantener las respuestas para la siguiente».
85. **Lo propio** (E20 y E21): quien ve respuestas usa la planilla de siempre. Quien solo carga ve esa misma planilla con lo suyo; el estado, el PDF y el motivo de rechazo llegan con la E21. No hay «Mis cargas» aparte.
86. **La unidad va en la etiqueta,** como «Densidad (kg/ha)». No hay un dato «Unidad».
87. **Hasta tres preguntas por renglón** (E20): tres columnas en la compu, dos en la tableta y una en el teléfono y en el panel. El ancho sale del tipo de pregunta, el autor no lo elige, y las preguntas no se reordenan.
88. **Títulos de sección** (E14 y E20): el autor agrega títulos entre las preguntas. En WhatsApp, cada título es un subtítulo; en la web, arma una zona. Todas las zonas se pliegan y arrancan abiertas, y al enviar se abre sola la que tiene errores.

Elegidas por el usuario el 2026-10-31, al evaluar el traspaso 3 contra este modelo (las recomendadas, salvo la 98):

89. **Cargar desde la web no exige el teléfono verificado** (E20): alcanza con ser miembro activo y tener `formrecords.create`. El teléfono sigue reconociendo al empleado por WhatsApp.
90. **El canal se llama «App»** (E20), en la planilla, sus filtros, el detalle y el Excel, también si se cargó desde el teléfono.
91. **Corregir una respuesta** (E21), como en ArquitecturaBase. Reemplaza la 58. Se corrige con un motivo, también lo aprobado. La corrección reemplaza a la anterior con el mismo número, la anterior queda en el historial con su PDF, y vuelve a pasar por las reglas: si el formulario pide aprobación, vuelve a «Por aprobar».
92. **Anular una respuesta** (E21): con motivo, queda «Anulada» con quién y cuándo, y cierra la aprobación pendiente. No se borra ni se deshace. Sale de la planilla, los conteos y el Excel, salvo que se pida.
93. **Quién corrige y anula** (E21): quien la cargó, mientras no esté aprobada (siempre, si el formulario no pide aprobación), y con el permiso `formrecords.correct`, «Corregir y anular respuestas», cualquiera y en cualquier momento. Quien solo ve respuestas, no.
94. **Listas dependientes** (E20): un filtro de catálogo «según otra respuesta», solo en la web. Va adentro de la E20, para formularios de empleados o para los dos. En WhatsApp, después del bloque, con el intercambio cifrado con Meta.
95. **El avance al subir** (E20): el botón queda grisado con su texto, y una barra sin palabras muestra cuánto falta. Respeta la 43.
96. **Un catálogo a mano de más de 200 opciones** (E20): si el formulario es para empleados o para los dos, se publica con un aviso y se carga solo desde la web. Si es solo para clientes, sigue sin publicarse.
97. **Un cálculo no se ve mientras se carga** (E20): igual que por WhatsApp (54), y aparece en «Enviada».
98. **Publicar solo para la app** (E20): es nueva, no viene de ArquitecturaBase. Una organización sin WhatsApp conectado publica igual, sin Meta, y sus empleados cargan desde la web. Si después conecta un número, vuelve a publicar para mandarlo por WhatsApp. Un formulario solo para clientes no se publica.

**Condición del usuario para todas:** ninguna decisión copiada de ArquitecturaBase pasa por encima de [AGENTS.md](../../AGENTS.md) ni de [docs/rules](../rules/README.md); si una choca con una regla de acá, manda la regla de acá.

## 6. Qué tomar de ArquitecturaBase

Repo `C:\Users\ezequ\source\repos\ArquitecturaBase` y su front `ArquitecturaBaseFront`. **El código está en `origin/main`:** antes de leerlo, traé los cambios, porque la copia local puede estar atrasada. Se sigue desarrollando allá, así que cada etapa mira lo que haya al planificarse.

**Qué se copia:** desde la decisión 18 (2026-10-17), las pantallas y las reglas de producto se copian de ArquitecturaBase. **El código no:** la estructura (módulos, entidades, servicios y rutas) sale de este repo, con su modelo de organizaciones, empresas y plataforma, sus fichas y sus módulos de ejemplo (Asistente y WhatsApp). De ArquitecturaBase se toma solo lo que no depende del modelo de negocio:
- cómo se arma y se publica un Flow;
- los límites de Meta;
- el formato de los valores;
- los estados de una publicación;
- los errores ya pagados.

- **Resúmenes por etapa** con alcance y decisiones: `docs/plans/formularios/` (README, etapa 2, 2B, 3, 4 y 5).
- **Reglas implementadas de la Etapa 1:** `docs/features/formularios.md` (borrador, publicación, numeración, captura) y `docs/features/whatsapp.md` (perfil de Flows, mensaje interactivo, recepción, presupuesto de 16 KiB y fuentes de Meta).
- **ADR:**
  - 0018: robustez proporcional;
  - 0019: SQL de configuración acotado a catálogos, aceptada pero sin habilitar consultas por ahora;
  - 0020: motor de IA con capacidades; acá se traduce a herramientas del asistente.
- **Código:**
  - **Puertos entre módulos:**
    - `src/*.Application/Interfaces/Channels/IStructured*.cs`;
    - sus modelos, en `Models/StructuredData/`;
    - las implementaciones apagadas, en `Application/Channels/`: `DisabledStructuredFormSource` y `UnavailableStructuredRecord*`;
    - los adaptadores, en `Modules/BusinessForms/Channels/` y `Modules/WhatsApp/Channels/WhatsAppStructuredFormChannel.cs`.
  - **Backend:**
    - `src/*/Modules/BusinessForms/`: borrador, versiones, publicación durable, captura y numeración;
    - `src/*/Modules/WhatsApp/`: incluidos `WhatsAppFlowCompiler`, `WhatsAppFlowValueCodec`, la publicación y el despacho de Flows, el envío y la recepción de formularios.
  - **Front:** `src/features/business-forms/` y las partes de formularios de `src/features/whatsapp/`.
- **Tableros:**
  - página «Formularios por WhatsApp» del lienzo de ArquitecturaBase (<https://claude.ai/artifact/HPbmDPLnr8JZ9TxevtTqJJ>), con un recorrido guiado con Play;
  - las capturas están en `ArquitecturaBaseFront/docs/design/evidence/formularios-whatsapp/`;
  - se redibujan con el tema de este proyecto en su lienzo.

**Lo que se toma se adapta a este repo:**
- `BusinessForms` pasa a `Forms`, en `Domain\Forms\` en vez de `Domain/Modules/`;
- los puertos van en `Application/Interfaces/Integrations/StructuredData/`, no en `Interfaces/Channels/`;
- permisos en el catálogo del núcleo, tres clases de datos con RLS, `[Access]`, `[FeatureGate]`, `[Idempotent]`, `OperationLog.RunAsync` e `IVersioned`;
- la lista cerrada de sufijos de helpers: `Compiler` y `Codec` no están. Se renombran a uno de la lista (por ejemplo, `WhatsAppFlowBuilder`), o se suma el sufijo a la ficha con su test.

## 7. Decisiones pendientes

Ninguna.

Las del 2026-10-17 pasaron a la §5 como decisiones 20 a 29, las del 2026-10-18 como decisiones 30 a 33, las del 2026-10-24 como decisiones 34 a 77, las del 2026-10-25 como decisiones 78 a 82, y las del traspaso 3, del 2026-10-30 y el 2026-10-31, como decisiones 83 a 98.
