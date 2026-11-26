# Formularios dinámicos por WhatsApp: hoja de ruta

**Estado:** en curso · aprobada el 2026-10-12, con las decisiones de producto tomadas ([sección 5](#5-decisiones-de-producto)). Está sumada al [plan de desarrollo](2026-09-27-plan-de-desarrollo.md) como **las catorce etapas de la E12 a la E25, en el orden de la decisión 78**, sin esperar a la E10 (123). No es un plan de etapa: cada etapa se escribe en el formato de [etapa en paralelo](../guides/etapa-en-paralelo.md) cuando le toque, a partir de su sección de acá.

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
| **Cliente de la organización** | Cualquier contacto de la bandeja «Clientes» de E11, tenga o no cuenta. Completa los formularios que son para clientes (decisiones 49 y 50). Si vinculó su cuenta personal, ve la copia en «Mensajes». |

### Clasificación de los datos

- **Privado de la organización (`ITenantOwned`, esquema `tenant`, RLS):** definiciones, versiones, catálogos e ítems, respuestas y valores, recibos, reglas, ejecuciones, tareas, documentos y metadatos de archivos.
- **Global de plataforma (esquema `platform`, ADR 0050):** la biblioteca de plantillas y sus versiones.
- **La respuesta de un cliente** es dato de la organización (`ITenantOwned`), como la de un empleado. La copia de quien vinculó su cuenta va por la copia de la conversación de la E11 (que ya es `IConsumerBusinessShared`): Forms no suma tablas compartidas. En «Mensajes», la persona ve la tarjeta del formulario con sus propias respuestas.
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
  | `forms.publish` | E14 | Publicar formularios | Publish forms | Publicar un formulario, darlo de baja y reactivarlo. | Publish a form, deactivate it and reactivate it. |
  | `formrecords.create` | E14 | Responder formularios | Answer forms | Completar formularios por WhatsApp o desde la web. | Fill in forms on WhatsApp or on the web. |
  | `formrecords.read` | E14 | Ver respuestas | View responses | Ver las respuestas y sus valores. | View responses and their values. |
  | `formrecords.export` | E15 | Exportar respuestas | Export responses | Bajar en Excel las respuestas, con los filtros aplicados. | Download the responses to Excel, with the filters applied. |
  | `formpersondata.manage` | E16 | Administrar los datos de las personas | Manage people data | Definir los datos de las personas, como «Zona». | Define people data, such as Zone. |
  | `formrules.manage` | E21 | Administrar reglas y PDF | Manage rules and PDF | Armar las reglas de un formulario y el diseño de su PDF. | Build a form's rules and its PDF design. |
  | `formapprovals.decide` | E21 | Aprobar o rechazar respuestas | Approve or reject responses | Decidir las respuestas que piden aprobación, según el rol de la regla; las que cargó o corrigió la misma persona, solo si la regla lo deja (25 y 276). | Decide the responses that need approval, according to the rule's role; the ones the same person submitted or corrected, only if the rule allows it. |
  | `formrecords.correct` | E21 | Corregir y anular respuestas | Correct and void responses | Corregir o anular cualquier respuesta, en cualquier momento (93). | Correct or void any response at any time. |
  | `formsuggestions.manage` | E23 | Administrar sugerencias entre formularios | Manage suggestions between forms | Elegir qué formulario se sugiere después de otro. | Choose which form is suggested after another. |
- **El texto de un permiso dice solo lo que ya existe** (78). La tabla tiene el texto final, y cada etapa lo completa cuando llega: `forms.manage` y `forms.publish` suman lo suyo en la E17; `formrecords.create` suma «o desde la web» y `forms.publish` deja «en WhatsApp» en la E20 (98), y `formcatalogs.manage` suma el asistente en la E25, que trae el catálogo que propone (167).
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

Son catorce, de la E12 a la E25, en el orden de la decisión 78: **el número es el orden.** La E26, de carpetas, va después, antes de las integraciones (231). Entre paréntesis va la etapa de ArquitecturaBase que sirve de referencia. Cada etapa tiene su spec o su resumen en [`formularios/`](formularios/README.md), con la equivalencia con los números de antes, y sus tableros se dibujan en el lienzo antes de programar (decisiones 18 y 44).

**La regla del orden** (78, que controla `/plan-etapa`):
- cada etapa usa solo lo que dejaron las anteriores;
- lo que suma se ve en todas las pantallas que ya existen: un tipo de pregunta nuevo, en la planilla, el detalle, el Excel y el PDF, si ya están;
- nada nombra, prueba ni dibuja algo de una etapa posterior: eso pasa a esa etapa.

**La base, antes de la E12:** lo que lista el [plan de desarrollo](2026-09-27-plan-de-desarrollo.md#etapas-12-a-25-formularios-por-whatsapp): los botones (la F5 que sale de la 10) y la campana en el núcleo, donde también se avisa la versión nueva. La prueba con Meta de la E14 en adelante pide una organización con su número conectado, y queda para cuando esté la I1 de la 10 (123). Después van la [segunda parte de la base](formularios/base-de-la-12-segunda-parte.md) (214) y [Acciones del asistente](formularios/acciones-del-asistente.md) (268).

**Criterios.** En todas las etapas rige la lista de control de la skill `/puerta`: doce puntos que salen de los 142 problemas de los reviews de ArquitecturaBase ([§4](#4-lo-que-aprendimos-en-arquitecturabase)). Cada etapa suma los suyos. El plan de la etapa los toma como lista de control, y cada punto que aplica lleva su test.

### E12 · Catálogos (AB E1, sin formularios)

- **Nace el módulo `Forms`,** con su feature, sus permisos de catálogos y «Gestión de formularios» con solo «Catálogos».
- **A mano, de los miembros o de los contactos de WhatsApp** (55), con tipo de valor, carga de una vez (XLSX, CSV o pegar), exportar y la plantilla. Hasta 10.000 opciones (42). Un contacto sin nombre toma el de su perfil (81).
- **Guardar todo junto,** mandando y escribiendo solo lo que cambió. «Ordenar por…» Texto o Código se aplica al guardar (224).
- **Spec:** [formularios/12-catalogos.md](formularios/12-catalogos.md).
- **Criterios:**
  - los números y las fechas de un archivo se leen con la cultura del pedido, y lo ambiguo se rechaza;
  - ninguna lectura trae las 10.000 opciones para quedarse con pocas;
  - guardar sin cambios no toca las opciones, y guardar una opción no reescribe las demás;
  - los límites del canal se validan al guardar; las referencias van por id;
  - importar y exportar van de a uno por persona, con un tope por organización y otro de la instalación, compartidos entre las copias.

### E13 · Campos de los catálogos (AB E2, la parte del catálogo)

- **Campos por opción** (hasta 20, 42), con tipo y reglas, y valores en jsonb con índice GIN; columnas al pegar, importar y exportar.
- **Qué se ve en WhatsApp:** qué campos van al título y a la descripción de cada opción, con la vista previa del catálogo (36).
- **Ordenar por un campo,** como «Rubro», con el sentido de su tipo (224).
- **Spec:** [formularios/13-campos.md](formularios/13-campos.md).
- **Criterios:** las cuatro lecciones de gravedad alta del review E2 de allá, cada una con su test (están en el resumen).

### E14 · Formularios: crear, publicar, mandar y recibir (AB 1)

- **«Formularios»** (79): una sola lista, con «Nuevo formulario»; cada formulario se abre con su pestaña «Diseño».
- **Editor:** siete tipos de pregunta, entre ellos «elegir varias» (38), un `CheckboxGroup` de 1 a 20 opciones con mínimo y máximo, y la pregunta «Elegir de un catálogo», con la presentación del catálogo o la suya (78). Títulos de sección entre las preguntas, que en WhatsApp salen como subtítulo (88). Cada texto es uno solo, sin idioma (35). Prefijo propio, versiones y vista previa en el teléfono.
- **Publicar:** operación durable en Meta, con un Flow por versión y por WABA de la organización. Una publicación sin confirmar se puede descartar (225).
- **Mandar** desde el chat, a un empleado (Internos) o a un cliente (Clientes), según para quién es el formulario (49 y 50), con una sesión que dura el tiempo para contestar que elige la organización (24 horas por defecto, de 1 a 168; 270). Las opciones de catálogo viajan en el mensaje, con ids locales a la oferta. Lo que seguro no salió por la cola de WhatsApp se reintenta solo, y lo que no se sabe si salió queda «Sin confirmar» para una persona (240).
- **Recibir:** la respuesta numerada, idempotente, con los campos de catálogo sellados, y su tarjeta en el chat y en «Mensajes». La captura va sin `ExecutionActor`, porque un cliente no tiene cuenta.
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
- **La planilla** de cada formulario, con las subcolumnas de los campos sellados, la empresa (24) y un solo «Filtros», con filtros de una opción y sin cantidades (143 y 144).
- **El detalle:** la vista rápida al costado y la pantalla completa, con ↑ ↓ en el orden de la planilla.
- **Excel** con los filtros aplicados; «elegir varias» unido con «; ». El diálogo dice qué exporta con los chips de la planilla (226).
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
- **Deprecar los Flows** por WABA de la organización, como operación durable, al vencer el último envío, también los de una publicación descartada (225).
- **Spec:** [formularios/17-ciclo-de-vida.md](formularios/17-ciclo-de-vida.md).
- **Criterios:**
  - un rechazo de Meta al deprecar se clasifica y tiene una salida: no se repite para siempre;
  - un solo filtro de disponibilidad para todo lo que ofrece formularios.

### E18 · Fotos y documentos (AB 2B)

- **Preguntas «Foto» y «Documento»,** con mínimo y máximo, una por pantalla del Flow.
- **Al responder:** se baja cada archivo con el token de la organización, se verifican hash, tamaño y tipo real, y se guarda con `IFileStore`. Completa o nada, y aguanta un día de caída de Meta (H11 de ArquitecturaBase).
- **Ver:** visor y descarga solo por la Api, con `formrecords.read`, también en la planilla y el detalle. El antivirus está en la lista de lanzamiento.
- **Archivos en el chat** (48): el operador ve y baja lo que le mandan, y manda fotos y archivos desde el compositor, dentro de las 24 horas.
- **La Configuración de Formularios** (270), de la organización y de Plataforma, con «Tamaño máximo de un archivo»; las etapas siguientes suman ahí sus valores.
- **Spec:** [formularios/18-fotos-y-documentos.md](formularios/18-fotos-y-documentos.md).
- **Criterios:**
  - las dimensiones de una foto se rechazan por el encabezado, antes de decodificar; el tipo sale del contenido, nunca de la extensión;
  - el nombre de descarga lleva la extensión del tipo verificado y sin caracteres de formato (U+202E);
  - no se confía en los tipos del proveedor: el id de medio puede llegar como número y una opcional vacía llega omitida;
  - si falta el archivo guardado, 404; las miniaturas privadas se cargan solo las visibles.

### E19 · Condiciones, cálculos y líneas (nueva)

- **Preguntas condicionales:** «Mostrar si» otra respuesta cumple una condición. El servidor las vuelve a evaluar al capturar, y la planilla distingue «No se mostró». Lo que WhatsApp no puede mostrar, el editor lo avisa debajo de la condición (235).
- **Cálculos:** una pregunta «Cálculo» con las cuatro operaciones sobre números de la respuesta, campos del catálogo y la suma de una pregunta del bloque, que también va dentro del bloque, por ítem (233). Se calcula al llegar la respuesta (54) y redondea al par (232).
- **El bloque** (146, 177 y 178): preguntas que se cargan varias veces, con un mínimo y un máximo de ítems; se agrega con «Agregar bloque», y en WhatsApp va una pantalla por ítem, hasta 10.
- **En las respuestas:** la planilla, el detalle y el Excel los muestran; el Excel, con una fila por ítem y hasta 100.000 filas (227).
- **Spec:** [formularios/19-condiciones-calculos-y-lineas.md](formularios/19-condiciones-calculos-y-lineas.md).
- **Criterios:**
  - antes de programar, se prueban con Meta el componente `If`, una pantalla por ítem con «¿Agregás otro ítem?» y un archivo dentro de un `If`, en un celular, con el `routing_model` declarado: allá, sin él, responder «No» cortaba el formulario;
  - el servidor nunca confía en lo que llega: vuelve a evaluar condiciones y cálculos;
  - un cálculo que no se puede hacer queda «Sin respuesta», nunca rompe la captura;
  - los montos van en `decimal`, con moneda, y los cálculos redondean al par, como lo recalcula ARCA (232).

### E20 · Cargar una respuesta desde la web (AB, carga desde la app)

- **Un empleado completa un formulario desde la web,** a su nombre, con la misma captura que por WhatsApp: catálogos con sus filtros, archivos, condiciones, cálculos y el bloque (73).
- **Las entradas:** «Cargar» en cada fila de «Formularios» y «Agregar respuesta» en la planilla (83). Se carga en una página propia o en un panel al costado, y los dos mantienen lo cargado para la siguiente (84 y 147).
- **Lo propio:** quien solo carga abre «Formularios» y ve la planilla con lo suyo (85).
- **El armado:** hasta tres preguntas por renglón y zonas plegables; el ancho sale del tipo de pregunta, en dos anchos: una columna o el renglón entero (87 y 141).
- **Listas dependientes,** solo en la web (94).
- **«Dónde se completa»** (229): cada formulario elige «En la app y por WhatsApp» o «Solo en la app». Sin número conectado, todo se carga en la app (98). Lo que WhatsApp no puede mostrar, como una lista dependiente, un catálogo a mano demasiado grande o un mínimo de más de 10 ítems, frena al publicar, con «Pasar a «Solo en la app»».
- **La pregunta «Ubicación»** (230): «Usar mi ubicación» en la web y, por WhatsApp, el pedido de Meta antes del Flow.
- **El cálculo se ve en gris** mientras se carga, y vale el del servidor (234).
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
- **Aprobación de un nivel:** quien cargó no aprueba lo suyo, salvo que la regla lo deje (276). «Por aprobar» va en la lista «Formularios», en «Para resolver», con la misma fuente (ADR 0040), y como una vista arriba de la planilla (148). Al lado, «Por resolver (N)» junta lo que una regla dejó esperando a una persona, y la columna «Reglas» lo marca en cada fila (228).
- **PDF** como copia fija, con los campos «Dato», las fotos, el bloque y los cálculos, y las horas en la zona de quien cargó (236).
- **Corregir y anular una respuesta** (91 a 93), con el permiso nuevo «Corregir y anular respuestas». La corrección reemplaza a la anterior con el mismo número y vuelve a pasar por las reglas; anular deja la respuesta «Anulada», sin borrarla.
- **Lo propio de quien solo carga** (85) suma el estado, el PDF y el motivo de rechazo.
- **Avisos por WhatsApp** con el número de la organización; a quien cargó le llega uno si su regla se frena (237), y lo que no se sabe si salió queda «Sin confirmar» (240). Fuera de las 24 horas, con una plantilla aprobada de la organización (61), que suma el encabezado con documento y el llenado de sus variables.
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
- **Los estándares de un módulo** conviven con estas plantillas, con preguntas atadas y sus reglas activas (220): llegan con la segunda parte de ARCA, y esta etapa no cambia.
- **Spec:** [formularios/22-biblioteca.md](formularios/22-biblioteca.md).
- **Criterios:**
  - la plataforma no tiene ninguna ruta que lea datos de una organización;
  - las referencias de una plantilla van por id local, nunca por nombre.

### E23 · El asistente, con Casos (AB 5 y Casos)

- **El asistente atiende empleados y clientes** (51). Cada organización configura, por bandeja, quién atiende los chats nuevos y cuándo vuelve un chat al modo configurado; cada chat se cambia o se fija a mano (52). Se configura con «Administrar WhatsApp» (28).
- **Armar una regla desde un texto** (63): la IA propone la regla y la persona la revisa y la guarda.
- **Sin información cargada** (67): ayuda con lo general, nunca inventa datos de la organización, pregunta y deriva, y anota la pregunta en «Preguntas sin respuesta».
- **Límites por bandeja** (65), y el tope de ofrecimientos lo elige cada organización (66).
- **Casos** (39): cada organización configura perfil, conocimiento, casos y límites. Seis acciones, respuestas redactadas dentro de los límites y «Cualquier otra cosa» al final. Las respuestas redactadas siguen la [ADR 0054](../decisions/0054-respuestas-redactadas-por-el-asistente.md).
- **La información del negocio** (169), como en ArquitecturaBase: varias franjas por día, «24 horas», productos y servicios, políticas y condiciones, y «Aplicar de lunes a viernes»; la dirección, escrita.
- **Cómo trabaja:** historial de 24 horas de esa conversación, ráfagas de 4 s con tope de 15 s y hasta 3 acciones por turno. El asistente manda el formulario con la oferta fija que lo nombra (165) y lo prellena con un perfil nuevo del Flow, sin «elegir varias» ni las preguntas del bloque.
- **Lo de la etapa 5 de allá:** sugerencias al operador y entre formularios, audios transcriptos (27) y «Formularios que faltan». El catálogo que propone el asistente pasa a la E25 (167).
- **Desde el primer día:** la espera corta, el aislamiento por conversación y la medición.
- **Costos:** dentro del tope mensual de IA de la organización (ADR 0051).
- **Del traspaso 15:** los temas de «Preguntas sin respuesta» y «Formularios que faltan» se revisan antes de usarse con otros (219); Probar con hasta cinco mensajes; el servidor revisa cada acción antes de encolarla, y si la IA no contesta, deriva con «El asistente no pudo responder». El cupo, las fallas y el apagado, como toda la IA ([ADR 0064](../decisions/0064-reglas-de-toda-la-ia.md), 223).
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
- **Armar formularios y catálogos con la IA** (75, 102 y de la 154 a la 156): desde el chat del Inicio de la organización, con un texto, un archivo o un audio, o por WhatsApp. La IA propone y una persona crea el borrador o el catálogo, que queda por aprobar, también el catálogo que propone el asistente (167). El clip suma .xlsx, .csv y .docx, de a uno por mensaje; lo que se repite se arma como un bloque (177), y armar por WhatsApp se atiende antes de los casos (171). Al publicar, los documentos que originaron el formulario quedan propuestos.
- **Validaciones fijas:** un conjunto cerrado en el código, y la tabla de entidades del BCRA como dato de referencia; lo descartado se borra a los días de la Configuración, 90 de entrada (272); la lectura entra en el tope de la organización, con un tope por persona y por día (41).
- **«Pasar al formulario»** desde la bandeja (99): la respuesta queda a nombre de quien mandó el documento, y dice quién la revisó.
- **Del traspaso 15:** lo propuesto sin revisar vence a los días de la Configuración, 90 de entrada (215 y 272); las correcciones las ve una persona y no van a la IA (216); un tope diario de lecturas por organización (217); arma siempre un modelo calificado (218); por WhatsApp, una planilla con un pedido va a armar (221), y la IA puede proponer importar en un catálogo que existe (222).
- **Spec:** [formularios/25-documentos.md](formularios/25-documentos.md).
- **Criterios:**
  - precargar un archivo en un Flow se prueba con Meta antes de planificar;
  - rigen las ADR [0056](../decisions/0056-lectura-de-documentos-con-ia.md) y [0057](../decisions/0057-la-ia-propone-y-una-persona-crea.md), de la lectura de documentos y de la IA que arma;
  - la IA solo carga sola con las tres señales de la 71, nunca con la seguridad que declara el modelo; lo leído es un dato, nunca una instrucción;
  - las validaciones fijas corren en el servidor, sin IA, también al confirmar.

### E26 · Carpetas de formularios y catálogos (AB, CM1 a CM6)

- **Las carpetas son menús del menú lateral** (265): cada una abre su panel con sus formularios y un grupo por subcarpeta, en dos niveles, entre lo de los módulos y Administración, y se administra ahí mismo, con «Nueva carpeta» y su ⋮. Un formulario entra con «Agregar formularios» o con «Carpetas…», y un catálogo, solo con «Carpetas…». A cada persona le abre la planilla, la carga o el diseño, según sus permisos.
- **Los listados:** «Formularios» (79) y «Catálogos» suman el filtro «Carpeta», sin panel. Solo ordenan y no dan ni quitan acceso: las administra `forms.manage`, y `formcatalogs.manage` pone catálogos en carpetas, sin permisos nuevos (231).
- **Datos:** `ITenantOwned`, con RLS, el bloqueo, dos niveles, hasta 500 carpetas y doce íconos, e índices por organización; carpetas del sistema por clave, para lo que sumen los módulos. Nace con dos niveles: no hay datos que migrar.
- **Con la biblioteca** (E22): «Usar esta plantilla» pide la carpeta, y la copia de un estándar va a una carpeta del sistema; un doble toque devuelve el mismo borrador, y con otra carpeta es un conflicto (F21 y F47 de ArquitecturaBase).
- **Spec:** se escribe al planificarla, con el diseño de allá: la sección CM1 a CM6 de `docs/plans/2026-11-03-carpetas-de-formularios-y-catalogos.md` y `ArquitecturaBaseFront/docs/design/carpetas.md` (`git show origin/main:`).

### Después del bloque

- **Integraciones, lo primero después del bloque** (76): ARCA es la primera, como módulo propio y con la conexión de cada empresa (150 a 152), y su [hoja de ruta](2026-11-06-arca-hoja-de-ruta.md) tiene las etapas, la adaptación y lo que no hay que repetir. Las que siguen (Mercado Libre, Mercado Pago y webhooks) se evalúan con su traspaso. La API pública y el MCP necesitan una identidad por organización.
- **Catálogos, fases 3 a 6:** la búsqueda en vivo (56), las listas dependientes en WhatsApp (94), elegir primero por un campo (191) y los orígenes externos.
- **La conexión a la base de un cliente,** con una solución simple: un agente del lado del cliente o una réplica de solo lectura.
- **Los tableros que cruzan formularios.**
- **Importar respuestas desde Excel, y cargar a nombre de otra persona.**
- **Varios niveles de aprobación** (60).
- **Límites por plan.**
- **Agentes que hacen procesos** (idea del usuario del 2026-11-25, sin decidir): el escalón que sigue al asistente de la E23 y la E24, sobre las mismas piezas. La empresa lo arma con configuración, y cada disparador y cada acción nueva es código nuestro. Son cuatro cosas, cada una con su decisión de producto: otros disparadores además de un mensaje (una hora, o un evento como una respuesta nueva); escribir sin que una persona confirme (hoy la IA prepara y la persona confirma, [0068](../decisions/0068-acciones-del-asistente.md)), con lo que puede hacer sola según el permiso; procesos de varios pasos con esperas («si no contesta en dos días, volvé a escribirle»); y actuar sobre los módulos de negocio que existan.

## 4. Lo que aprendimos en ArquitecturaBase

Aplicarlo desde el primer día evita lo que nos frenó la Etapa 1 durante días:

1. **Robustez proporcional al efecto** (ADR 0018 de ArquitecturaBase). Mandan nuestras fichas de [guardado](../rules/guardado.md), [concurrencia](../rules/concurrencia.md) e [idempotencia](../rules/idempotencia.md):

   | Efecto | Cómo se hace acá |
   | --- | --- |
   | **Fuera del sistema:** Meta, IA y mensajes a personas. Bajar un archivo de Meta y subirlo al almacenamiento se pueden repetir sin efecto, y van con reintentos simples (E18) | Operación durable: un trabajo con lease y fence en un registro técnico (como `AssistantTurnWorks`). Antes de cada llamada se commitea `InFlight`, y la llamada va fuera de la UoW. Lo incierto queda «Sin confirmar» y se consulta antes de repetir, salvo lo que sale por la cola de WhatsApp, que se reintenta sola (80). Idempotencia por el id del trabajo o de la sesión, y `[Idempotent]` en el `POST` que lo pide. Pruebas de varios hosts. |
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
   - en la Argentina, «125.500» puede leerse como 125,5, por eso se valida la escala y lo ambiguo se rechaza, con la ayuda en la región del idioma (207);
   - en la prueba de punta a punta de allá, del 2026-11-02 y el 2026-11-03 (traspaso 5, §12): un rótulo chico al lado de una frase no ordena nada (138), y un historial vacío no ocupa lugar (140). Lo de cada etapa va en su «no repetir»: la E15, la E19, la E20 y la E21;
   - en su prueba integral, del 19 al 22/11 (traspaso 16, 05): lo que pide una ayuda se prueba en el cliente real, porque que el patrón propio lo admita no dice que Meta lo deje escribir (O29, 207); la cuenta en vivo lee los números igual que la validación (O19); y si no llega un webhook, se mira primero la suscripción de la app de Meta, que es una sola (O27).
7. **La lista de control de la puerta.** Los 142 problemas de los reviews de ArquitecturaBase quedaron en doce puntos, en la skill `/puerta` y en [tests](../rules/tests.md): números y fechas con la cultura del pedido, el front que no tapa al servidor, cambiar algo en uso, guardar sin cambios, lecturas con el volumen máximo, nada que nombre lo que no se puede leer, nada que exija un idioma, un test por cada lock, migraciones portables, botones y avisos (43), arreglos en los caminos hermanos y respuestas de proveedores clasificadas. Cada etapa del bloque la aplica, con un test por punto.

## 5. Decisiones de producto

Decididas por el usuario entre el 2026-10-12 y el 2026-10-24. No se vuelven a preguntar.

1. **Formularios por organización.** En la v1 son de la organización; la respuesta guarda la empresa de quien respondió y la planilla filtra por empresa. Formularios y permisos por empresa (`ICompanyOwned`), después.
2. **Las plantillas se toman como copia independiente.** Una referencia obliga a resolver qué pasa con las respuestas cuando la plataforma cambia la plantilla.
3. **B2C, después del bloque,** con la bandeja Clientes de E11 andando y el backlog #51 resuelto. Reemplazada por la 49 para los formularios: los clientes los completan desde el principio.
4. **El número de la plataforma (E8) no manda formularios:** es para ingreso y avisos de cuenta.
5. **Sin límites por plan en la v1** (formularios, respuestas, almacenamiento): solo se habilita el feature. Medir el uso real antes de poner topes.
6. **El bloque va después de la E10:** cambiada por la 123, que la deja para después. Decía 8b → 11 → 9 → 10 → 12 a 19 (12 a 17 cuando se decidió). Nace con Redis, límites y el fuzz de la E9, y con la nube de la E10.
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

Decididas el 2026-10-17, con el traspaso de ArquitecturaBase de ese día, retirado el 2026-11-08:

17. **Idioma.** Reemplazada por la 35 (2026-10-24), que saca el idioma y las traducciones del contenido. Decía, con la regla que ArquitecturaBase adoptó el 2026-10-16:
    - solo el idioma original es obligatorio para guardar y publicar, y la traducción es opcional;
    - si la traducción no está completa, el formulario, el PDF, los mensajes y las opciones de catálogo salen enteros en el idioma original, sin mezclar;
    - el idioma de las pantallas del sistema no cambia.
18. **Pantallas.** Acotada por la 108 (2026-11-01): de acá en adelante no se copian pantallas. Decía que se copian de ArquitecturaBase:
    - Diseño rehace los tableros de la 12 y la 13 con la disposición de su lienzo y del capítulo 05 del traspaso, con las piezas y los criterios de nuestro lienzo y sin mostrar alternativas;
    - la 17 no existe allá: sus tableros se quedan y solo se ajustan al editor nuevo;
    - si la disposición choca con una regla escrita de este repo, manda la de acá.
19. **Asistente** (antes F7, la etapa 5 de ArquitecturaBase). Se diseña ahora, después de los tableros de la 16. Se sigue programando después del bloque. Desde la 34, es la E23, después de la E22, con Casos (39).

Elegidas por el usuario el 2026-10-17, entre las que quedaron al alinear el bloque con el traspaso (todas las opciones recomendadas):

20. **Catálogo a mano en otro idioma que el formulario:** solo sirven los catálogos con el mismo idioma original que el formulario; los demás no aparecen en el selector (C11 de la E12). Deja de aplicar con la 35 (2026-10-24): el contenido no tiene idioma, así que sirve cualquier catálogo de la organización.
21. **Nombres de miembros de más de 30 caracteres:** se mantiene la decisión 11, que los corta con «…» y pone el nombre completo en el detalle. ArquitecturaBase no trunca, pero acá el nombre es el de la cuenta de cada persona y bloquear dejaría sin uso el catálogo.
22. **Catálogo con filtros de más de 200 opciones:** se mantiene la decisión 12. Se guarda con aviso, y el bloqueo es al mandar. El usuario la confirmó el 2026-10-17, después de los tableros de la v129, que se corrigen.
23. **Cuánto se guardan las respuestas, sus archivos y sus PDF:** sin plazo mientras exista la organización. Se borran con la baja de la organización, y lo personal de quien da de baja su cuenta, con el participante de la baja. Se revisa con el primer cliente.
24. **La empresa en la planilla:** un filtro «Empresa ▾» y una columna «Empresas» que se puede ocultar desde «Columnas».
25. **Quién aprueba:** cualquiera de la organización con el rol de la regla y `formapprovals.decide`, salvo quien cargó la respuesta. **Con la 276, una casilla de la acción deja decidir a quien cargó.**
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

Elegidas por el usuario el 2026-10-24, con la evaluación del traspaso 2, retirada el 2026-11-08, todas las recomendadas:

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
37. **Catálogos de más de 200 opciones** (12 y 22): se mantienen. Al publicar se suman dos avisos que no frenan, el máximo por persona y las personas sin el dato, y el tope de 20 en «elegir varias». La 157 cambia el del máximo por persona: solo cuando no entra o da cero.
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
    - los textos del sistema que recibe una persona, en el idioma de su cuenta, salvo los nombres fiscales de ARCA, siempre en español (242);
    - los topes de catálogos: 10.000 opciones, 20 campos y 20 datos de las personas, contando los activos (105).

    Los datos de las personas son por organización y por miembro. En la v1 no dependen de la empresa ni hay para B2C. El usuario lo reconfirmó el 2026-10-24 con la 49: los clientes no tienen datos de las personas.
43. **Botones y avisos, en todo el sistema** (`pantallas-y-ui.md` y `formularios.md` del front):
    - un botón está habilitado o grisado, sin textos al lado ni debajo. Mientras trabaja, pone el aro y su gerundio (la 115 cambia «grisado con su mismo texto»);
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

49. **Los clientes completan formularios desde el principio.** Cualquier contacto de la bandeja Clientes, tenga o no cuenta: se lo reconoce por su número. Si vinculó su cuenta personal, ve la copia en «Mensajes». Reemplaza la 3 para los formularios.
50. **Cada formulario dice para quién es:** solo empleados, solo clientes, o los dos. El envío, el asistente y las sugerencias ofrecen solo los que corresponden a quien recibe.
51. **El asistente atiende también a los clientes,** en la bandeja Clientes, con los mismos límites configurables. Reemplaza la 29.
52. **El modo de atención es configurable** (la 77 lo precisa con varios asistentes):
    - cada organización elige, por bandeja, quién atiende los chats nuevos: el asistente o una persona;
    - y cuándo vuelve un chat al modo configurado después de que lo tomó una persona: cuando pasa un tiempo sin mensajes (que elige la organización), cuando la persona lo marca «Resuelto», o nunca;
    - cada chat se cambia a mano en cualquier momento, en los dos sentidos, y se puede fijar para que no vuelva solo;
    - el bot del menú de la E11 aparece solo en los chats de Clientes que están en atención manual.
53. **Las preguntas condicionales y los cálculos van dentro del bloque de etapas** (de la E12 a la E25), no después.

Elegidas por el usuario el 2026-10-24, al revalidar el bloque (paso 2, armar y mandar un formulario):

54. **Condiciones, cálculos y líneas son una etapa propia, la E19,** después de la E16, porque un cálculo usa los campos de los catálogos. Los cálculos se hacen al llegar la respuesta: quien completa no ve el total en WhatsApp antes de enviar. Las líneas (un pedido con varios productos) llevan un tope de renglones, que se prueba antes con Meta. La 78 la pone después de la E18, y la 146 cambia las líneas por renglones.

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
66. **El tope de ofrecimientos de formularios** (2 por conversación y día) lo elige cada organización, dentro de un máximo de la plataforma. **La 270 lo generaliza a toda regla de negocio.**
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
75. **El asistente de la web arma formularios y catálogos:** se le pasa un PDF o se le escribe cómo se quiere, y crea el formulario en borrador y los catálogos por aprobar, y lleva al editor. Nada se publica solo. Va en la E25, con «Armar formulario con la IA». La 154 cambia cómo se crea: con un botón de la persona, sobre una propuesta.
76. **Después del bloque, lo primero son las integraciones:** ARCA, Mercado Libre, Mercado Pago y webhooks, con plantillas conectadas en la biblioteca.

Elegida por el usuario el 2026-10-24, en la revisión completa del bloque:

77. **Con varios asistentes (E24), el chat nuevo arranca con el menú del bot,** aunque la bandeja esté en modo Asistente. La persona elige el equipo, y desde ahí le contesta el asistente de ese equipo. Con un solo asistente, el menú sigue apareciendo solo en atención manual (52).

Elegidas por el usuario el 2026-10-25, al revisar el orden del bloque con la Guía y con dos revisiones independientes:

78. **Los catálogos van primero, y el número de cada etapa es su orden:** E12 catálogos, E13 campos de los catálogos, E14 formularios (con las preguntas de catálogo, la oferta, la presentación propia de una pregunta y el sellado), E15 respuestas, E16 datos de las personas y filtros por destinatario, E17 ciclo de vida y copiar, E18 fotos y documentos, E19 condiciones, cálculos y líneas, E20 cargar desde la web, E21 reglas, aprobación, PDF y avisos, E22 biblioteca, E23 asistente, E24 varios asistentes y E25 documentos. Cada etapa usa solo lo de las anteriores, lo que suma se ve en las pantallas que ya existen, y nada nombra, prueba ni dibuja algo de una etapa posterior. Cambia la 34; la equivalencia con los números de antes está en [`formularios/`](formularios/README.md). Antes de la E12 va la base: los botones (la F5 de la 10), la campana en el núcleo y el tamaño general.
79. **Una sola lista «Formularios»,** con «Nuevo formulario»; «Desde una plantilla» se suma en la E22. Cada formulario se abre con pestañas: «Diseño» desde la E14, «Respuestas» y «Tablero» desde la E15, y las de las etapas siguientes. Sale «Diseñar formularios». Lo que ve cada persona depende de sus permisos.
80. **Lo que sale por la cola de WhatsApp y no se sabe si salió se reintenta solo,** como los demás mensajes. Salen «Envío sin confirmar», «Ya lo revisé» y «Resolver el envío» del envío de formularios (E14) y de los avisos de las reglas (E21). La cola del núcleo no suma un estado incierto. **Cambiada por la 240** (2026-11-18): lo que no se sabe si salió queda «Sin confirmar».
81. **Un contacto sin nombre toma el nombre de su perfil de WhatsApp** cuando escribe, y se puede cambiar. Va en la E12, con la fuente «Contactos». El teléfono nunca se muestra.
82. **El PDF toma el logo y los datos de la página pública** de la organización. El color se elige en el diseño del PDF de cada formulario, y arranca con el del sistema de entrada (E21).

Elegidas por el usuario el 2026-10-29 y el 2026-10-30 en ArquitecturaBase, viendo cada tablero, y adoptadas con el [traspaso 3](../../../ArquitecturaBase/docs/plans/2026-10-30-traspaso-multitenant-carga-desde-la-app.md), porque no chocan con este modelo:

83. **Las entradas para cargar desde la web** (E20): «Cargar» en cada fila de «Formularios» y «Agregar respuesta» en la planilla, sin una entrada nueva en el menú. Quien solo tiene `formrecords.create` abre «Formularios» con sus formularios activos y «Cargar», sin conteos. La 153 le suma «Mis cargas» y «Mi última carga». **«Cargar» pasa a ser la primera opción del ⋮ de la fila (261).**
84. **Página y panel** (E20): «Nueva respuesta» es una página propia, para la compu y el teléfono. Desde la planilla, en la compu, se abre un panel al costado, con «Mantener las respuestas para la siguiente». La 147 la suma también a la página.
85. **Lo propio** (E20 y E21): quien ve respuestas usa la planilla de siempre. Quien solo carga ve esa misma planilla con lo suyo; el estado, el PDF y el motivo de rechazo llegan con la E21. No hay «Mis cargas» aparte.
86. **La unidad va en la etiqueta,** como «Densidad (kg/ha)». No hay un dato «Unidad».
87. **Hasta tres preguntas por renglón** (E20): tres columnas en la compu, dos en la tableta y una en el teléfono y en el panel. El ancho sale del tipo de pregunta, el autor no lo elige, y las preguntas no se reordenan.
88. **Títulos de sección** (E14 y E20): el autor agrega títulos entre las preguntas. En WhatsApp, cada título es un subtítulo; en la web, arma una zona. Todas las zonas se pliegan y arrancan abiertas, y al enviar se abre sola la que tiene errores.

Elegidas por el usuario el 2026-10-31, al evaluar el traspaso 3 contra este modelo (las recomendadas, salvo la 98):

89. **Cargar desde la web no exige el teléfono verificado** (E20): alcanza con ser miembro activo y tener `formrecords.create`. El teléfono sigue reconociendo al empleado por WhatsApp.
90. **El canal se llama «App»** (E20), en la planilla, sus filtros, el detalle y el Excel, también si se cargó desde el teléfono.
91. **Corregir una respuesta** (E21), como en ArquitecturaBase. Reemplaza la 58. Se corrige con un motivo, también lo aprobado. La corrección reemplaza a la anterior con el mismo número, la anterior queda en el historial con su PDF, y vuelve a pasar por las reglas: si el formulario pide aprobación, vuelve a «Por aprobar».
92. **Anular una respuesta** (E21): con motivo, queda «Anulada» con quién y cuándo, y cierra la aprobación pendiente. No se borra ni se deshace. Sale de la planilla, los conteos y el Excel, salvo que se pida.
93. **Quién corrige y anula** (E21): quien la cargó, mientras no esté aprobada (siempre, si el formulario no pide aprobación), y con el permiso `formrecords.correct`, «Corregir y anular respuestas», cualquiera y en cualquier momento. Quien solo ve respuestas, no. Excepción: una respuesta con una factura emitida no se corrige (241).
94. **Listas dependientes** (E20): un filtro de catálogo «según otra respuesta», solo en la web. Va adentro de la E20, para formularios de empleados o para los dos. En WhatsApp, después del bloque, con el intercambio cifrado con Meta.
95. **El avance al subir** (E20): el botón dice «Enviando…» con su aro (115), y una barra sin palabras muestra cuánto falta.
96. **Un catálogo a mano de más de 200 opciones** (E20): si el formulario es para empleados o para los dos, se publica con un aviso y se carga solo desde la web. Si es solo para clientes, sigue sin publicarse.
97. **Un cálculo no se ve mientras se carga** (E20): igual que por WhatsApp (54), y aparece en «Enviada».
98. **Publicar solo para la app** (E20): es nueva, no viene de ArquitecturaBase. Una organización sin WhatsApp conectado publica igual, sin Meta, y sus empleados cargan desde la web. Si después conecta un número, vuelve a publicar para mandarlo por WhatsApp. Un formulario solo para clientes no se publica.

Elegida por el usuario el 2026-10-31, al escribir la spec de la E25, la recomendada:

99. **«Pasar al formulario» desde la bandeja** (E25): un operador con `formrecords.read` y el permiso de atender esa bandeja carga, con lo que leyó la IA, la respuesta de quien mandó el documento. Queda a nombre de esa persona, por el canal `whatsapp`, igual que «Cargar solo si es seguro», y la respuesta dice quién la revisó. Es la única forma de cargar a nombre de otro: en la web, sigue siendo cada uno a su nombre (73).

Elegidas por el usuario el 2026-10-31, con la lectura de punta a punta de las specs, todas las recomendadas:

100. **Quien solo aprueba** (E21): con permiso para decidir y sin permiso para ver respuestas, ve la cola de lo que espera su aprobación, con el número, el formulario y la fecha, sin los valores, y decide ahí.
101. **Quien solo carga, con un formulario dado de baja** (E20): su lista suma el filtro «Estado», con «Dados de baja»: ve lo suyo y no puede cargar más. Amplía la 83.
102. **Los catálogos que arma el asistente desde un documento** (E25): además de los de filtros, puede armar catálogos a mano, con lo que leyó, siempre por aprobar. Mientras no se aprueben, el formulario no se publica.
103. **Un número en otra cuenta de WhatsApp** (E14): si una cuenta conectada no tiene el formulario, publicar se habilita aunque no haya cambios, y en el chat el formulario aparece con el motivo.
104. **«Elegir varias» con un catálogo a mano de más de 20 opciones** (E20): va como la 96: si es para empleados o para los dos, se publica solo para la app; si es solo para clientes, no se publica.
105. **Los topes de 20 campos y de 20 datos de las personas cuentan solo los activos** (E13 y E16): quitar o dar de baja uno libera su lugar. Lo ya respondido queda igual.
106. **El PDF de un formulario sin aprobación** (E21): si el formulario no pide aprobación, el PDF se arma al recibir la respuesta. Si la pide, solo al aprobar: lo que espera aprobación no tiene PDF.
107. **Un contacto de WhatsApp no se borra:** hoy una organización no tiene cómo, y no entra al plan. Las specs no contemplan un contacto borrado. Si un cliente sin cuenta pide que borren sus datos, no hay cómo hacerlo desde la app.

Decidida por el usuario el 2026-11-01:

108. **Reglas y diseños propios:** en ArquitecturaBase no se toca ni se pide nada, y ninguna etapa espera algo de allá. Las pantallas se dibujan con las specs, estas decisiones y las reglas y piezas de este proyecto. El lienzo de la carga desde la app de ArquitecturaBase (<https://claude.ai/artifact/Kvbr5KFJrGT951JLiu8i4M>) es solo un ejemplo: lo que sirva se adapta a nuestro sistema y no se copia tal cual. Lo ya decidido no se vuelve a preguntar. Una pantalla nueva que no tiene modelo en nuestro lienzo se diseña acá, con opciones para el usuario. Acota la 18.

Elegidas por el usuario el 2026-10-31 y el 2026-11-01 en ArquitecturaBase, probando la app de punta a punta, y adoptadas con el traspaso 4 de allá, del 2026-11-02, porque no chocan con este modelo:

109. **Sin leyendas explicativas, en todo el sistema:** la información es etiqueta y valor o control. El renglón de abajo es solo para el error o para un dato. Una explicación que sirve va en la (i) (116), y lo que no ayuda a decidir se saca, sin pasarlo a la (i).
110. **Lo apagado dice por qué, en todo el sistema:** el motivo de un botón, una casilla o la ✕ de un diálogo apagados va en su tooltip, si no es obvio. Amplía la 43, que lo decía de las opciones de un menú.
111. **Un solo combo para elegir un catálogo** (E13, E14 y E16): al tocarlo muestra los primeros, con su cantidad de opciones, y al escribir los filtra. Es el mismo en un campo, en un dato de las personas y en una pregunta, sin buscador aparte, lista paginada ni descripción del catálogo elegido.
112. **Publicar por zonas** (E14): «Problemas» y «Qué se publica», sin descripción ni párrafos. Un texto que pasa su límite lo dice en concreto: «Hasta 20 caracteres; tiene 26.». Salen «Qué queda fijo» y «Qué va a pasar», y queda el prefijo mientras está libre. La E16 suma la zona «Avisos».
113. **«Adjuntar el PDF» no se desmarca solo** (E21): ni al mover, quitar o cambiar una acción, ni al cambiar el evento. Si «Armar el PDF» falta o está después, lo dice al lado, con un botón que lo arregla. Cambiar el tipo de una acción conserva lo escrito.
114. **«Aprobar» pide confirmar** (E21), con el foco en «Cancelar», porque ↑ ↓ dejan la respuesta siguiente con «Aprobar» en el mismo lugar.

Elegidas por el usuario el 2026-11-02, al evaluar ese traspaso contra este modelo, todas las recomendadas:

115. **Toda acción que tarda muestra que trabaja, en todo el sistema.** Cambia la 43 y la 95:
    - el botón pone el aro y su gerundio («Guardando…»), con su color, en su lugar y sin aceptar otro clic;
    - una confirmación queda abierta hasta la respuesta;
    - una tabla que vuelve a leer atenúa sus filas y corre una línea arriba.
116. **La (i) y la ⓘ, cada una para lo suyo:** la ayuda de un campo o una sección va en una (i) con tooltip. Lo que no se puede hacer, y qué hacer para resolverlo, sigue en el aviso importante, que se abre desde su ⓘ.
117. **Las opciones de un catálogo se agregan y se editan en un modal** (E12): la lista a la izquierda y la opción a la derecha. En la compu, la tabla sigue editable. Una opción guardada no se quita: se da de baja, porque acá no se borran.
118. ~~Reemplazada por la 125.~~ **Los campos de un catálogo siguen en su tabla** (E13), sin modal ni «Listo». La columna de un campo nuevo queda apagada hasta guardar, con el motivo en su tooltip.
119. **El Excel de un catálogo se lee como en pantalla** (E12): números y fechas de Excel, con su formato, y se reimporta sin diferencias. Lo que Excel no guarda exacto va como texto canónico.
120. **Un solo «Importar»** (E12): «Origen» y «Revisión» a la vista en un diálogo, sin pasos numerados, y lo pegado en una grilla con las columnas del catálogo. Pegar sigue agregando y el archivo, reemplazando.
121. **El límite de un texto se avisa al publicar** (E14): el editor no tiene contador, tampoco para una etiqueta con su unidad (86). Se puede sumar después, sin rehacer nada.
122. **La ayuda y los motivos en el teléfono:** sin mouse no hay tooltip. Tocar la (i) abre una hoja chica con el texto, que queda hasta cerrarla, y tocar algo apagado muestra el motivo en la franja oscura de abajo, que se va sola.
123. **La E10 queda para después, y el bloque no la espera.** Cambia la 6. La base y las etapas se programan y se cierran sin ella, con `LocalFileStore`. Lo que pide Meta con un número conectado, o la nube, queda pendiente en el plan de cada etapa, y se prueba junto cuando esté la I1.
124. **La maqueta no se usa.** Lo que se ve sale del lienzo y cómo funciona, de las specs. Ninguna etapa copia de ella.

Elegidas por el usuario el 2026-11-02, al comparar el lienzo con el front real de ArquitecturaBase:

125. **Los campos de un catálogo van en un modal, como en ArquitecturaBase.** Reemplaza la 118. No hay sección «Campos»: «Agregar campos · N», en el encabezado de «Opciones», abre «Campos de …», con la lista a la izquierda y el campo a la derecha, igual al modal de opciones (117).
126. **Formularios sigue el armado real de ArquitecturaBase.** Cambia la 108. Donde allá hay una pantalla, acá se copia cómo está armada: sus zonas, su orden y sus controles. Antes de dibujar una etapa se mira su front, no solo el traspaso. Los colores y las piezas siguen siendo los nuestros, y mandan las decisiones que dicen otra cosa, como la 79 y la 35.
127. **Las acciones van a la vista donde importa el orden:** opciones, preguntas y campos muestran ↑ ↓ ✎ 🗑. Las demás tablas siguen con el ⋮.
128. **Se mantiene una sola lista «Formularios», con sus pestañas (79),** aunque ArquitecturaBase tenga dos pantallas.
129. **🗑 borra una opción guardada que nada usa, como en ArquitecturaBase.** Cambia la 117: se pide confirmar, se marca «Se borra al guardar» y se puede restaurar hasta guardar. Si un formulario, una respuesta, otra opción o el dato de una persona la usa, no se borra: se dice qué la usa y se puede dar de baja. Una opción nueva se quita sin preguntar. Allá dice «Eliminar»; acá, «Borrar», como en todo el sistema.
130. **Lo que vuelve con el armado (126):** las filas con acciones a la vista llevan dos líneas, el nombre y su detalle, como excepción a «un dato por celda, en un renglón», que sigue valiendo en las tablas; la vista previa de un catálogo trae «Una opción | Varias»; y la clave de un campo o de un dato se muestra, con su (i), y queda con candado al guardar. **Las dos líneas, reemplazadas por la 263: cada dato en su columna.**
131. **Reactivar se abre en el formulario, como en ArquitecturaBase (E17).** Es publicar: «Reactivar» de la lista lleva al formulario y abre ahí el diálogo, con las zonas de «Publicar» (112), «Próxima respuesta» en «Qué se publica» y el botón «Reactivar con la versión N».
132. **Con respuestas, «Borrar» queda grisado, con el motivo (43).** Cambia lo que decía la E17: antes no aparecía. Allá se lee una línea en el menú; acá va en su tooltip y, en el teléfono, debajo de la acción.
133. **Una pregunta de documento acepta siempre PDF, Excel, Word, JPEG y PNG, como en ArquitecturaBase (E18).** No se eligen por pregunta: lo dice la (i) de «Cuántos documentos». Cambia lo que decía la E18.
134. **La carga desde la web sigue el armado de ArquitecturaBase, y lo que allá no existe usa lo que ya está aprobado acá.** Publicar solo para la app va en la zona «Avisos» de la 16 y no necesita un diálogo propio; los filtros de una pregunta de catálogo se agregan con «Agregar filtro ▾», que suma «Según otra respuesta»; «Canal» entra en los filtros rápidos de la planilla.
135. **Las reglas, el PDF y la decisión siguen el armado de ArquitecturaBase (E21), y lo que allá no existe usa lo que ya está aprobado acá.** Las reglas llevan «Agregar regla» y «Guardar reglas» en la cabecera de su caja y el historial al costado; una regla se edita en el lugar y se cierra con «Listo», y cada acción muestra ↑ ↓ 🗑, en lugar del ⋮. El diseño del PDF lleva «Guardar diseño» en «El comprobante» y la vista previa al costado, con tres estilos: Clásico, Moderno y Compacto. La decisión va en la vista rápida, con los momentos arriba de las pestañas. Corregir abre la carga de la 20, con lo de antes a la vista. La lista «Formularios» de la 21 parte de la de la 20, con «Cargar».

Elegidas por el usuario el 2026-11-02 y el 2026-11-03 en ArquitecturaBase, probando la app de punta a punta, y adoptadas con el [traspaso 5](../../../ArquitecturaBase/docs/plans/2026-11-03-traspaso-multitenant-lecturas-y-filtros.md) de allá, del 2026-11-03, porque no chocan con este modelo:

136. **Un cambio de diseño vale para toda la app.** Se busca en todas las pantallas que usan la misma pieza o el mismo patrón, se dibuja con casos de varias áreas y se escribe como regla en las fichas del front y en su tablero «Criterio», para que lo cumplan las pantallas nuevas.
137. **Las tablas, en todo el sistema:** una línea fina entre columnas, del tono de la que separa las filas, en el encabezado y en el cuerpo; los números a la derecha, encabezado incluido, y la tabla cierra abajo con la línea de su última fila, salvo que el borde de su caja ya la cierre. En una celda que baja de renglón, como en una comparación, un código con guiones («30-71458236-9») no se parte, y una zona horaria se corta solo en sus barras.
138. **Las lecturas, en todo el sistema:** lo guardado que se muestra sin editarlo no deja rótulos sueltos con texto al lado ni «Rótulo: valor» corrido en un renglón:
    - lo que pasa en orden, como una regla o una sugerencia, va en pasos, uno debajo del otro, con su ícono en una columna fija. Las palabras que unen («Si», «y», «es») van en gris y los datos, en negrita; el dato aparte de una acción (el rol, «con el PDF»), en una etiqueta, y el mensaje, entero en su recuadro;
    - los datos de un registro van en dos columnas, como en `DetailGrid`, y los que dependen de un valor, como los campos de la opción elegida, alineados debajo de él;
    - dos versiones van lado a lado: qué cambió, el dato una sola vez y cada versión en su columna, solo lo distinto. En el teléfono, el dato va arriba y las dos versiones debajo;
    - en una lista paginada para elegir, lo elegido que no está en la página va primero, marcado.
139. **El detalle de una respuesta** (E15 y E21): el valor primero, y los campos de la opción elegida alineados debajo, en una tabla chica con una línea a la izquierda. Los momentos (la mandó, la respondió, la aprobó) van un renglón cada uno, con la fecha a la derecha y quién debajo. La actividad va en cuatro columnas fijas (Fecha, Qué pasó, Estado y Acciones), agrupada por momento, con cada persona de un aviso en su renglón, y nada se repite. En la vista rápida, «Abrir en pantalla completa» y «Abrir conversación» son íconos con su tooltip en el renglón del título, antes de ↑ ↓ ✕. **El detalle y la actividad, cambiados por la 266 y la 267.**
140. **Los historiales, detrás de un botón** (E21, E22 y E23): «Historial», con su reloj, va primero en el encabezado de su sección y abre un panel al costado. Sin revisiones queda apagado, con el motivo en su tooltip. Tocar una revisión la muestra en pasos, sin editar, y la sección gana el ancho. Cambia la 135, que lo ponía al costado.
141. **La carga desde la web, en dos anchos** (E20): lo que tiene un solo valor ocupa una columna (número, fecha, sí o no, texto corto de hasta 80, elegir una y elegir de un catálogo); lo que necesita espacio, el renglón entero. «Elegir varias» reparte sus casillas en columnas, elegir una con hasta cinco opciones las muestra en fila aunque sean largas, y el panel junto a la planilla tiene las mismas zonas que la página. Sigue la 87: el ancho sale del tipo. **Elegir va en la pieza y en una columna (262).**

Elegidas por el usuario el 2026-11-03, al evaluar el traspaso 5 contra este modelo, todas las recomendadas:

142. **Lo elegido se distingue sin dudas, en todo el sistema** (OE1-A): nunca solo con un tinte, sino con un relleno, una tilde, un punto, un subrayado o una barra, con 3:1 contra lo no elegido y el texto a 4,5:1. En un segmentado, la elegida va rellena con el color de la marca y la letra blanca, y las otras, blancas; en el teléfono, cada opción mide 44 × 44. Una tarjeta elegida suma un borde de 2 px y el tinte de la marca; en un desplegable de un filtro, la elegida lleva una tilde a la izquierda, y la fila abierta de una tabla y la conversación abierta del chat, el tinte y una barra de 3 px al inicio. No se arman botones sueltos que hacen de segmentado. Cambia la elegida blanca con sombra de `tema.md` del front.
143. **Un filtro de opciones es un desplegable simple, en todo el sistema** (PL2-A y FO1-A): cerrado dice «Todas» (o «Cualquier …») o la elegida; abierto, una opción por renglón, sin la cantidad ni texto debajo, con la elegida tildada y ninguna apagada. Si no hay coincidencias, el vacío lo dice y ofrece limpiar. Con muchas opciones o que vienen del servidor (un catálogo, las personas) es el combo con búsqueda, que muestra 20 y «Mostrar N más». Se elige una. Un segmentado de pocas opciones fijas conserva la cantidad de cada conjunto («Activos (8) · Dados de baja (2)»). En la planilla se va el filtro «Respuesta» (con respuesta, sin respuesta, no existía), y las fotos y los documentos no se filtran. El contrato no cambia: las facetas siguen trayendo sus cantidades, y `answered`, `unanswered` y `notAsked` quedan para las reglas y las sugerencias. No entran «Columnas» ni las condiciones de una regla, que eligen varias. Cambia las fichas del front, que pedían la cantidad por opción, y «alguna de» de la E15. **Cambiada por la 262: el desplegable es la pieza, se busca adentro de la lista y los segmentados de filtro pasan a desplegable sin cantidad.**
144. **Los filtros de una planilla o un listado, con un solo «Filtros»** (PL1-A): a la vista, el buscador y hasta tres filtros, sin botón para aplicar; lo demás va en un solo «Filtros», con la cantidad de aplicados, que se despliega como «Columnas»: a la izquierda lo que se filtra y a la derecha lo que se elige en lo marcado. Cada cambio se aplica enseguida, y en el teléfono ocupa la pantalla. Lo aplicado va en chips con su × y «Limpiar todo», y no va «N respuestas»: el total está al pie. Reemplaza «Más filtros», con su borrador y «Ver N respuestas».
145. **Las zonas de una pregunta de catálogo arrancan plegadas** (ZP1-A, E14 y E16): «En WhatsApp» y «Qué opciones ve cada persona» arrancan plegadas cada vez que se abre la pregunta, con la flecha en la banda y lo configurado en una línea («Rubro: Agroquímicos · Zona según quien lo recibe», «sin valor» si falta, «Sin filtros»; «Como en el catálogo · Texto · Detalle»). Abierta, «Agregar filtro ▾» vuelve a la banda y la (i) va junto al título. La que tiene un error o un aviso se abre sola, y solo un problema nuevo la vuelve a abrir. Es una excepción más a «sin paneles plegables» del front.
146. **Renglones en los formularios** (E19, E20 y E21; cambia la 54): un tipo de pregunta «Renglones», con nombre, ayuda y un mínimo y un máximo de renglones (el mínimo de 0 al máximo y el máximo hasta 100; uno nuevo arranca con 1 y 20). Sus preguntas se eligen con la casilla «Se carga varias veces» y se juntan en un bloque; una que ya existía pasa como pregunta nueva, y publicar lo dice en «Pasan a renglones». Un bloque por formulario, sin bloques adentro, con texto corto y largo, número, fecha, sí o no y elegir una, propia o de catálogo. En WhatsApp, una pantalla por renglón, hasta 10, con «¿Agregás otro renglón?» desde el mínimo y sin intercambio con Meta; con un máximo mayor que 10, publicar avisa sin frenar. En la web, una tabla con «Agregar renglón», y en el panel y el teléfono, un recuadro por renglón. La planilla sigue con una fila por respuesta, el detalle muestra cada renglón como los campos de un catálogo (139) y el Excel suma una hoja por bloque. Las reglas y los filtros usan «Algún renglón con…», y en el mensaje el bloque es un dato con todos los renglones. Reemplaza el «Grupo de líneas» de hasta 6 columnas. Desde el 2026-11-04 (RG7-A y RG8-A, sumadas allá al traspaso 5 después de incorporarlo): un formulario con bloque tiene el tope de respuesta de uno con archivos, 64 KiB, y con un mínimo de 0, WhatsApp abre el bloque con una pantalla con su nombre y «¿Agregás un renglón?».
147. **«Mantener las respuestas» también en la página** (AR2-A, E20; cambia la 84): la casilla va en la banda, antes de Cancelar y Enviar, y en el teléfono, en el pie fijo, arriba de Enviar. Marcada, al enviar el aviso dice el número y el formulario queda con lo cargado, sin los archivos y con el foco en la primera pregunta; sin marcar, se ve «Enviada». No se recuerda al volver a entrar.
148. **«Por aprobar», una vista arriba de la planilla** (PL1-A con nuestra pieza, E21): en un formulario que pide aprobación, quien puede decidir ve «Por aprobar (N) | Todas», con el segmentado de dos vistas de lo mismo, y no con pestañas (79). «Por aprobar» es una tabla corta (N°, Cargó, Recibida, Rol y «Revisar»), de la más vieja a la más nueva y sin filtros; «Todas» es la planilla. Se abre en «Por aprobar» si hay algo para decidir. Quien solo ve respuestas tiene la planilla, y quien solo aprueba, solo «Por aprobar». Siguen la campana y «Para resolver».
149. **El mensaje con datos** (MD1-A, E21 y E22): «Agregar dato», al final del renglón del mensaje, abre una lista con buscador: el número, el motivo del rechazo al rechazar y las preguntas en su orden, sin fotos ni documentos. El dato entra donde está el cursor, como una etiqueta con el nombre actual de su pregunta, que se borra entera; las llaves escritas o pegadas no entran. «Así llega», debajo, muestra el mensaje con datos de ejemplo. Se guarda la identidad de la pregunta, `{FieldId}`, y nunca su nombre: la etiqueta sigue a la pregunta renombrada, y una que ya no está se ve «Pregunta no disponible», en rojo, y no deja guardar. Las variables de una plantilla de WhatsApp (61) se llenan con la misma lista. Como el contrato no está publicado, no se acepta el nombre entre llaves.

150 a 152 son de ARCA y están en su [hoja de ruta](2026-11-06-arca-hoja-de-ruta.md).

Elegida por el usuario el 2026-11-06, al evaluar el [traspaso 7](../../../ArquitecturaBase/docs/plans/2026-11-06-traspaso-multitenant-carga-etapas-2-a-5.md) contra este modelo, la recomendada:

153. **«Mis cargas» y «Mi última carga»** (E20; cambia la 83): quien solo carga ve en «Formularios», al lado de «Cargar», cuántas respuestas cargó de cada formulario y la última, con su número y su fecha; sin cargas, «Todavía no cargaste». Nunca ve los totales de la organización. Son columnas de la lista, no la página «Mis cargas» aparte que descarta la 85.

Elegidas por el usuario el 2026-11-06, al evaluar el [traspaso 8](../../../ArquitecturaBase/docs/plans/2026-11-06-traspaso-multitenant-ia-arma-formularios.md) contra este modelo, todas las recomendadas:

154. **La IA propone y una persona crea** (IA1-A, E25; cambia la 75): en el chat del Inicio, la propuesta es una tarjeta en el hilo, con el nombre, el prefijo y las preguntas, «Ver cómo queda» y «Crear borrador» o «Crear catálogo». Nada se crea hasta que la persona toca el botón, y cada ajuste trae otra tarjeta, que reemplaza a la anterior. Las herramientas del chat no escriben: proponen.
155. **También por WhatsApp** (IA3-A, E25): una cuenta vinculada le pide al número de la organización, con un texto o un audio, que arme un formulario o un catálogo. El asistente contesta con un resumen y los botones «Crear borrador» o «Crear catálogo» y «Cambiar algo»; crear contesta con el enlace para revisarlo y publicarlo en la web.
156. **El micrófono en el chat del Inicio** (E25): para cualquier consulta, con «Detener», «Descartar» y escuchar antes de mandar; el servidor transcribe y el costo entra en el tope mensual de IA de la organización.

Elegidas por el usuario el 2026-11-06, al evaluar el [traspaso 9](../../../ArquitecturaBase/docs/plans/2026-11-06-traspaso-multitenant-ajustes-de-toda-la-app.md) contra este modelo, todas las recomendadas:

157. **El aviso de opciones por persona, solo cuando no entra o da cero** (NI3-B, E16; cambia la 37): al publicar, el máximo por persona se avisa solo cuando pasa el límite de WhatsApp, 200 en una elección o 20 en varias, y, nuevo, cuando nadie ve ninguna opción: «Con estos filtros nadie ve ninguna opción.». Si entra, publicar no dice nada.
158. **Reintentar un aviso por persona** (RT1-A, E21): «Reintentar» va en el renglón de la persona, en la actividad, solo con la falla confirmada, y le manda el aviso solo a ella. No se repite a quienes lo recibieron.
159. **El chat de WhatsApp en «Mi cuenta»** (PF1-A, E21): debajo de los medios de ingreso, una zona con cada organización de la persona, que dice si su chat está vinculado, con «Vincular», y hasta cuándo le llegan los avisos. «Verificado» sigue siendo entrar con el número.

Programadas o elegidas por el usuario en ArquitecturaBase hasta el 2026-11-07, y adoptadas con el traspaso 10 (su evaluación, retirada el 2026-11-08) porque no chocan con este modelo (126):

160. **Lo que carga no salta, en todo el sistema** (ES1-A, ES2 y ES3): un listado que carga por primera vez dibuja su encabezado y el aro en el alto de ocho filas, sin filas esqueleto; una página que espera su ruta o sus permisos muestra su banda con el título como bloque de carga y tres barras, y un ajuste, sus zonas. Cambia «la primera carga conserva su aro» de las fichas del front y el «Cargando» de `Criterio-Tablas`.
161. **Decidir a propósito, en todo el sistema:** una acción que no se deshace y dispara avisos, como aprobar, pide confirmar con el número («¿Aprobar VIS-000003?»), con la principal y el foco en «Cancelar». Generaliza la 114; el rojo lleno queda para lo que destruye.
162. **Ninguna elección se descarta en silencio, en todo el sistema:** cambiar un tipo, una acción o una opción no pierde lo escrito sin avisar. Generaliza la 113.
163. **Mucho texto, con jerarquía, en todo el sistema:** todo a la vista y sin plegar; las fechas a la derecha, una vez por momento; lo que se consulta poco, detrás de un botón en el encabezado de su sección. Generaliza la 139 y la 140. Los campos de un ajuste siguen en columnas iguales, no del ancho de lo que llevan (PF1-A).
164. **Una regla nueva vale también para lo que ya existe.** Cambia la 136, que decía «para que lo cumplan las pantallas nuevas»: la 137, la 143, la 144 y la 160 pasan también a los tableros viejos y a la app ya programada, como los filtros de Usuarios y Roles, que entran con la base de la 12.
165. **El envío nombra el formulario** (E14 y E23): sin texto del operador, el mensaje dice «Completá el formulario «Visita a cliente».», propuesto al elegir el formulario y sin pisar lo escrito; la oferta del asistente dice «Te preparé el formulario «Visita a cliente» para que lo completes.». El nombre del Flow en Meta es «Visita a cliente · v4 · ab63a1f9», sin idioma (35) y de hasta 80 caracteres.
166. **Corregir, anular, lo propio y la carga, como en ArquitecturaBase** (E20 y E21): «Corregir», con su lápiz, va en la fila del título, y el ⋮ tiene solo «Anular», en rojo; «Motivo de la corrección» va al final y el botón dice «Guardar corrección»; el motivo del rechazo va en los momentos del detalle, no en una columna; una zona plegada dice «N de M respondidas»; «Enviada» tiene «Volver a Formularios»; una lista dependiente dice «Elegí primero «Zona»» y, en el chat, un formulario que la tiene aparece apagado con su motivo. Quién corrige una rechazada sigue siendo la 93. **Dónde se corrige, cambiado por la 266: en la misma pantalla.**
167. **El catálogo que propone el asistente pasa a la E25** (cambia la E23; cumple la 154): nace con «Crear catálogo», desde la tarjeta del Inicio o el resumen de WhatsApp, se edita como cualquier catálogo y lleva «Descartar propuesta» y «Aprobar y activar»; guardar no lo activa.
168. **Un catálogo dentro del bloque filtra por quien lo recibe** (E19), como fuera del bloque (E16).
169. **La información del negocio del asistente, como en ArquitecturaBase** (E23): varias franjas por día, «24 horas», productos y servicios, políticas y condiciones, y «Aplicar de lunes a viernes». La dirección sigue escrita, sin buscarla en Google.
170. **Los avisos en «Mi cuenta»** (E21; la 159 con la 61): la zona del chat dice «Llegan hasta el …» y, pasadas las 24 horas, «Solo con plantilla», con su (i), porque un aviso con una plantilla aprobada llega igual.
171. **El micrófono va solo en el Inicio de la organización** (156), porque su costo es de su tope; y **pedir por WhatsApp que arme algo se atiende antes de los casos** (155), sin configurar nada: con la cuenta vinculada y el permiso.
172. **La CUIT de una empresa no cambia con ARCA conectado:** primero se desconecta.

Elegidas por el usuario el 2026-11-07, al evaluar el traspaso 10 contra este modelo:

173. **Que no parezca plantilla, en todo el sistema** (cambia el tema del 04/10): salen el cuadrado del logo, el ícono de sección en su cuadradito, el avatar redondo con degradé, la pastilla del menú activo, las pastillas de filtros, etiquetas y contadores, y las sombras fuera de menús, desplegables y tooltips. Quedan la paleta Pradera y la densidad: el nombre sin cuadrado, la banda sin ícono, el avatar como un cuadrado chico con la inicial, el activo con una barra, los filtros y las etiquetas con el radio de un control, y el botón principal sólido. En la app entra con la base de la 12.
174. **El texto largo se ve entero, en todo el sistema:** una celda baja de renglón en lugar de cortar con «…»; un código con guiones no se parte y una zona horaria se corta solo en sus barras (137). «Un dato por celda» sigue valiendo.
175. **El estado, una vez, en todo el sistema** (EP1-A): lo que dice el encabezado no se repite debajo, la acción que destraba va en la banda y el porqué de un botón apagado, en su tooltip. En la publicación (E14 y E17) no hay pasos: la etiqueta dice el estado y, solo cuando hacen falta, la banda trae «Consultar publicación», «Repetir el mismo intento» (cuando WhatsApp no recibió el pedido) y, con «Necesita revisión», «Salió» y «No salió»; lo rechazado sigue marcado en su pregunta, sin una zona «Problemas». La baja se dice en una frase («Lo dio de baja Ana Gómez el …»), y al reactivar, la etiqueta dice el estado de la publicación. Cambia `Criterio-Decisiones`, que mandaba los pasos a la ⓘ.
176. **La vista oficial se recorre en un diálogo** (VO1-A, E14): «Ver vista oficial» abre un diálogo con el marco de 480 × 840, achicado para entrar en la ventana, que arranca en la primera pantalla y se recorre como en el celular. Cada pregunta de catálogo trae sus primeras 20 opciones activas, en orden, con los filtros fijos y sin los filtros por destinatario; de los miembros y los contactos, solo lo que quien mira puede leer. El enlace lo arma el backend, de hasta 6000 caracteres: si no entra, cada lista lleva menos. Al costado queda «Así lo ve quien lo recibe», del borrador. Cambia `Criterio-Decisiones`, que la ponía al costado con «Borrador | Versión N en WhatsApp».
177. **El bloque se agrega con «Agregar bloque», y lo que se repite se llama «ítem»** (RG10-D y RG12-A, de la E19 a la E21; cambia la 146): «Agregar bloque» va al lado de «Agregar pregunta», y el bloque arranca abierto y con «Bloque» de nombre, también cuando lo crea «Se carga varias veces»; con un bloque armado, el botón se apaga con su motivo, y «Tipo de respuesta» deja de ofrecerlo. Todo texto que ve el usuario dice «ítem» («item» en inglés) donde decía «renglón»: «Bloque · de 1 a 10 ítems · 3 preguntas», «Preguntas de cada ítem», «¿Agregás otro ítem?», «Productos · ítem 2», «Agregar ítem», «Algún ítem de Productos con…», «En algún ítem de Productos» en «Filtros» y la columna «Ítem» del Excel. El código y los documentos siguen diciendo `rows`, y «renglón» queda donde es una línea.
178. **Un bloque con un mínimo de más de 10 ítems** (RG13-A, E19 y E20; suma a la 146): con WhatsApp, en la E19, publicar frena con «En WhatsApp se cargan hasta 10 ítems; el mínimo es 15.» e «Ir a la pregunta». Desde la E20, si el formulario es para empleados o para los dos, se publica solo para la app, con su aviso en «Avisos», como un catálogo demasiado grande (96 y 104); si es solo para clientes, frena. La captura valida siempre el mínimo y el máximo, y un máximo de más de 10 sigue avisando sin frenar.
179. **El ingreso y el consentimiento por WhatsApp, como en ArquitecturaBase** (E8 y E11; entra con la base de la 12): ningún mensaje lleva un enlace secreto. El bot de Plataforma lleva al ingreso con «Entrar a mi cuenta», y el código se pide desde la web. Invitar por WhatsApp desde «Cargar teléfonos» pide marcar «La persona aceptó recibir mensajes por WhatsApp».
180. **La escala, como ArquitecturaBase, en todo el sistema** (cambia el tema del 04/10 junto con la 173): el usuario la eligió viendo la misma planilla en tres escalas; la de hoy le parecía grande y redondeada. La letra baja un 18 %, sin bajar de 10,5 px, y el cuerpo queda en unos 11 px; las filas, los controles y las bandas bajan un 26 % (una fila de tabla, unos 33 px; un botón o un filtro, de 24 a 27 px; la barra de arriba, unos 39 px); los rellenos y las separaciones, un 28 %; las esquinas quedan en 3 px como mucho, también en las pastillas y los avatares, y sin sombras (173). Los colores siguen siendo la paleta Pradera. En el lienzo entra con el pase de las piezas compartidas, y en la app, con la base de la 12.
181. **Un documento con ítems no completa el bloque** (E25): la lectura completa el número, la fecha y el proveedor, y deja el bloque vacío, para cargar los ítems a mano en el Flow, como el prellenado de la E23. Así no hace falta prellenar las pantallas del bloque ni otra prueba con Meta.
182. **«Casos» muestra el bloque fijo «Armar formularios y catálogos»** (E25; la 171): va arriba de los casos, sin editar, como el bloque fijo de formularios, para que quien configura vea que un pedido de armar se atiende antes.
183. **La banda de cada ítem dice «Ítem 2»** (E20, en el panel y en el teléfono): el nombre del bloque ya va arriba, como etiqueta, y la banda entra en 320 aunque el bloque se llame «Materiales de construcción». Los títulos de las pantallas de WhatsApp siguen diciendo «Productos · ítem 2» (177).
184. **En «Enviar formulario», todo formulario que se carga solo desde la app aparece apagado, con su motivo** (E20): el que tiene una lista dependiente dice ««Proveedor» depende de otra respuesta: se carga desde la app.», y el que pide más ítems de los que admite WhatsApp, el mismo motivo que dio al publicar (110 y 178).
185. **La columna del bloque se ve entera en la planilla** (E19), bajando de renglón como todo texto largo (174): «12 · Caño de 1/2 pulgada, Codo de 90°, …» con todos los nombres. El detalle muestra cada ítem.
186. **«Salió» y «No salió» piden confirmar** (E14; la 161 en la 175): «¿La versión 4 salió en WhatsApp?», con la principal y el foco en «Cancelar», porque ninguno de los dos se deshace. Un «Salió» por error deja mandar una versión que en Meta no existe, y un «No salió» por error obliga a publicar otra.
187. **En el teléfono no se achica** (la 180, en el lienzo y en la app): la escala de la 180 es para la compu; debajo de 768 px, todo lo que se toca sigue midiendo 44 × 44, porque el dedo lo necesita.
188. **En la lista de chats, la vista previa del último mensaje se corta con «…»** (excepción a la 174): va en un renglón, como en WhatsApp, para que las filas tengan la misma altura, y el mensaje entero se ve al abrir el chat.
189. **El panel verde del ingreso queda liso** (la 173): sin degradé ni cuadrícula, porque era lo que más parecía plantilla de lo que quedaba. La paleta Pradera no cambia.
190. **La ficha de un usuario no repite su estado** (la 175): sale el campo gris «Estado», que no se podía editar, porque la banda ya lo dice («● Invitación pendiente»).
191. **Elegir primero por un campo va después del bloque** (LD1-A a LD3-A de ArquitecturaBase, programado allá el 2026-11-07 y el 2026-11-08): una pregunta de catálogo pide primero el valor de un campo, como «Rubro», y lista solo las opciones con ese valor, también por WhatsApp. Va con las listas dependientes en WhatsApp, y de la E12 a la E25 no cambia nada: la E20 sigue sin listas dependientes dentro del bloque.
192. **Ante un pedido vago, la IA propone enseguida** (E25; precisa la 155): con «Armame un formulario nuevo para el taller», arma su mejor versión, como una orden de trabajo con el cliente, el vehículo, qué hay que hacer y la fecha, y la persona la ajusta con «Cambiar algo» o escribiéndole. Pregunta una sola vez, con sus palabras, solo si no puede saber ni qué armar, y si sigue sin poder, deriva a una persona. Allá, el 2026-11-07, pregunta y espera con un texto fijo. Acá no hay un texto fijo para cada caso: la IA contesta con sus palabras, dentro de los límites de las respuestas redactadas (39), y es fijo solo lo que tiene que ser exacto: los botones, el resumen de lo que va a crear, la oferta de un formulario (165) y el mensaje al derivar.
193. **Una sola pestaña no lleva barra** (E14): el formulario muestra la barra de pestañas cuando tiene dos. En la E14 solo está «Diseño», y la barra aparece en la E15, con «Respuestas» y «Tablero».
194. **El visor de una foto dice el nombre, el tamaño y «Descargar»** (E18): igual en la planilla y en el chat. Quién la mandó y cuándo ya están en la tarjeta del chat.
195. **Pasar una pregunta al bloque pide confirmar** (E19; la 162): en una pregunta con «Mostrar si» o con «Permitir elegir varias», «Se carga varias veces» sigue activa, y marcarla pregunta antes: «Se quitan sus condiciones.» o «Pasa a elegir una sola opción.».
196. **«Correo» no entra en el bloque** (E21): en una pregunta del bloque aparece apagada en «Tipo de respuesta», con su motivo, como Foto y Documento, y «Mandar por correo» ofrece solo las preguntas de afuera del bloque.
197. **El bloque no es una variable de una plantilla de WhatsApp** (E21): una variable no admite saltos de línea, y eso se confirma en la prueba con Meta. El bloque va solo en los avisos dentro de las 24 horas.
198. **La tarjeta de una plantilla dice dónde se usó** (E22): «La usaste en «Visita a cliente» y 1 más».
199. **Un documento que llega en atención manual se lee igual** (E25): queda en «Documentos recibidos», y al operador le aparece la sugerencia de mandar el formulario prellenado, con «Revisar y enviar», como en la E23.
200. **Lo que WhatsApp no puede mostrar se carga desde la app** (E20; generaliza la 96, la 104 y la 178): si un formulario tiene algo que WhatsApp no puede mostrar, como una condición que compara números o fechas, demasiadas preguntas en una pantalla o una respuesta demasiado grande, y es para empleados o para los dos, se publica solo para la app, con su motivo en «Avisos». Si es solo para clientes, publicar frena. Vale igual con número conectado o sin él.
201. **Una plantilla de WhatsApp puede llevar el PDF arriba** (E21): «Nueva plantilla» suma «Encabezado», con «Sin encabezado» o «Documento (el PDF)», debajo de «Tipo», y la app arma el PDF de ejemplo que pide Meta. Se dibuja en el lienzo, con su teléfono, antes de programar la E21.
202. **Un tema descartado vuelve si alguien lo pregunta de nuevo** (E23): pasa a «Abiertos», con la pregunta nueva sumada, en «Preguntas sin respuesta» y en «Formularios que faltan».
203. **Un documento de un formulario solo para la app no se ofrece por WhatsApp** (E25): queda en «Documentos recibidos», para pasarlo al formulario desde la web.
204. **Con el bloque al final, WhatsApp termina en una pantalla de cierre** (E19; como la r12 de ArquitecturaBase, 2026-11-18): si no hay preguntas después del bloque, el «No» de «¿Agregás otro ítem?» y el último ítem llevan a una pantalla con el nombre del formulario, «Tocá «Enviar» para mandar el formulario.» y el botón final. Meta no acepta mandar desde la pantalla de un ítem: allá lo rechazó el 9/11. Con preguntas después, el «No» lleva a ellas, como antes. Descartada: frenar al publicar y pedir una pregunta después del bloque.

Del traspaso 15, diseño para toda la app, fechas y números (2026-11-18). Como toda regla de diseño (136 y 164), cada una vale en todo el sistema donde tenga sentido, y el usuario pidió no tomar decisiones para un caso suelto. El detalle está en las fichas del front y en los «Criterio» del lienzo:

205. **Las fechas, solo con números, en todo el sistema** (pregunta 12, como ArquitecturaBase): con hora, la hora va después de una coma («27/09/2026, 14:35»; en inglés, «09/27/2026, 2:35 PM»), y el mes nunca va en letras. Sale la fecha larga. Un mes se escribe como su rango: «01/10/2026 – 31/10/2026» en «Uso del asistente» y en el eje por mes del tablero de un formulario, y «del 01/10/2026 al 31/10/2026» dentro de una frase, como el correo del tope del asistente. Vale para la app, el PDF, los correos, WhatsApp, el Excel y el asistente. Cambia la fecha «sin coma», la fecha larga y «septiembre de 2026» de `formatos.md` del front, y `FormatMonth` de `fechas-y-zonas.md`.
206. **Un dato por columna, en todo el sistema** (pregunta 13, como ArquitecturaBase; precisa «un dato por celda» del 2026-10-25 y conserva la 130): cada columna muestra un dato y su encabezado lo nombra. Cuentan como uno la fecha y la hora de un momento, un valor con su unidad y varios valores del mismo dato. No van juntos un número, su fecha y su estado, «fecha · persona», una etiqueta al lado del dato («Corregida», «Sin verificar» o «Dado de baja» van en su columna) ni el resumen de otra tabla: la columna del bloque dice «3 ítems», la de documentos «2 documentos», y cada uno se ve en el detalle (cambia la 185). Dos excepciones: un texto secundario en gris debajo del dato, que no se ordena ni se filtra y nunca es un estado, una fecha ni una persona (la descripción de un tema que falta, el detalle de una opción en la planilla); y las listas que no son tablas, como el chat o las opciones de un diálogo, siguen las lecturas (138). El punto delante del correo de ArquitecturaBase no aplica: en Usuarios el estado tiene su columna. **El texto secundario en gris, sacado por la 263.**
207. **Un número que escribe una persona se lee con la región de su idioma, en la app y en WhatsApp** (pregunta 14, como ArquitecturaBase): en español, coma decimal y punto de miles; en inglés, al revés. Lo que se lee distinto con y sin los miles («1.500» en español) es un error de su pregunta, que pide escribirlo sin separador de miles («1500» o «1,5»), y al servidor llega el número con punto, como al importar un catálogo (C9 de la E12). En WhatsApp, la ayuda pide coma en español y punto en inglés, según el idioma que guarda la sesión, y el decodificador rechaza lo ambiguo con `WhatsApp.Flow.NumberAmbiguous`, por la validación fija, sin cambiar el Flow publicado. Cambia la T9 de la E14 («se acepta punto o coma») y «la ayuda pide punto decimal» de la §4. Allá se probó con Meta el 21/11 (O29, traspasos 16 y 17): el cliente de los Flows borraba toda coma seguida de `]` o `}`, también dentro del patrón, y Meta rechazaba la coma; con el patrón que nombra primero la coma (`[,.]`, su perfil r16) pasa, y ningún texto fijo del Flow lleva una coma antes de `]` o `}`. La coma sigue, y acá va a la prueba con un número conectado de la E14.
208. **El porcentaje va pegado al número, en todo el sistema** (P-c, como ArquitecturaBase): «12,5%» en español y «12.5%» en inglés, en la app, los correos, WhatsApp y el PDF. Lo escribe la pieza de formato de cada lado; un texto nunca lo arma a mano. Cambia el patrón de es-AR del dato de referencia, «12,5 %».
209. **Qué va a la vista y qué en el ⋮, en todo el sistema** (AC1-A de ArquitecturaBase; P-a): a la vista, con texto, la tarea de la pantalla (guardar, publicar, crear, exportar, aprobar o rechazar lo que espera), con la principal al final, y lo que lleva a otro lado o baja algo, como íconos con su nombre en un panel o con texto en la banda. En el ⋮, lo demás que se hace con el objeto: corregir, anular, dar de baja, reactivar, eliminar, descargar la plantilla u ordenar, con lo destructivo al final, en rojo y después de una línea. El orden: la tarea, ir y bajar, el ⋮ y al final ↑ ↓ ×. «Corregir» pasa al ⋮, arriba de «Anular» (cambia la 166 en eso); «Aprobar» y «Rechazar» siguen a la vista, primeros, mientras la respuesta espera; «Dar de baja» y «Reactivar» de un catálogo pasan al ⋮, como en el editor de formularios. Las filas de una tabla (127) y el pie de un diálogo no cambian. **«Corregir» vuelve a la vista por la 266.**
210. **Los avisos, en todo el sistema** (AV1-A de ArquitecturaBase; P-d): el fondo nunca dice el tipo. Todo aviso es blanco, con la tinta de siempre (en el teléfono, la franja oscura de siempre), y el tipo se ve en un filo de 3 px a la izquierda y en el ícono, del mismo color: lo que salió bien, en verde con tilde; información, en gris con (i); atención, con triángulo; error, en rojo con octógono. La ✕ va adentro, a la derecha. Vale para el aviso de abajo, la franja de error de un formulario y el cartel que bloquea una pantalla. **Todo aviso pasajero se va solo, y el de error a los 10 s;** el que trae una acción, sea del tipo que sea, como «Copiar código» de un error del servidor, queda hasta cerrarlo. Cambia «rojo con ✕ si falló», del 2026-10-25, que quedaba hasta cerrarlo.
211. **Lo apagado dice por qué sobre el mismo control, igual en la compu y en el teléfono** (MA1-A de ArquitecturaBase; P-g; cambia la 122 y precisa la 110): el motivo va en un tooltip arriba del control, solo si no es obvio, sin ícono, candado ni texto al lado. Se abre al pasar el mouse, con el foco, un clic o un toque; el clic, el toque, Enter o Espacio muestran el motivo y no hacen la acción, y lo cierran Escape, salir, perder el foco o tocar afuera. Responde el control entero (la casilla con su texto, el renglón de una opción, la opción de un menú) y no cambia de lugar ni de forma al apagarse; una espera de segundos es solo su motivo. Se alcanza con Tab, y el lector dice su nombre, «no disponible» y el motivo. En el teléfono, el motivo y la (i) se abren con un toque, como tooltip sobre el control: ya no van en la franja oscura ni en la hoja chica. El candado de un campo que queda fijo (130) sigue.
212. **Los permisos de un rol, en una matriz, en cada acceso** (RO1-A de ArquitecturaBase; P-b): una fila por área, con Ver (`<área>.read`), Administrar (`<área>.manage`) y «Otros permisos», con su nombre; donde el área no tiene ese permiso va «—», y sin otros permisos esa columna no aparece. La casilla del área marca o quita la fila entera, y qué permite cada permiso va en la (i) del área. Sin buscador, «Todos · Elegidos», «Expandir todo» ni áreas plegables. Desde 768 px, la matriz; más angosto, una banda por área con sus permisos debajo. Un rol de sistema que no se cambia se lee con tildes, sin casillas. Igual en los roles de la organización, en los de una empresa y en los permisos de un operador de Plataforma (ADR 0042). Cambia las áreas plegables de un rol, aprobadas para la base de la 12. **Reemplazada por la 260 (2026-11-23).**
213. **La lista de chequeo de diseño y las reglas que no chocan, en todo el sistema** (ArquitecturaBase, del 8 al 11/11; no contradicen nada de acá, así que se adoptan sin pregunta y se avisaron el 2026-11-18):
   - todo tablero, pantalla o diálogo pasa las doce preguntas de la lista antes de mostrarse y otra vez antes de programarse, con cada diferencia aceptada en la nota del tablero. Sumar un control, un dato o un estado a algo que existe obliga a dibujarlo entero y pasar la lista otra vez. Lo que un diálogo dice que va a hacer (cuántas, con qué filtros, a quién, a qué archivo) va en `DetailGrid`. Un cambio pedido sobre una pantalla se busca también en las parecidas;
   - **una cosa, una zona:** lo que se configura y se guarda junto va en una zona, con los campos en el orden en que se completan y cada botón al lado de su dato. Se parte solo en cosas distintas o en pasos, como el alta de ARCA (152);
   - **una confirmación dice en una frase lo que se afecta,** sin instrucciones;
   - **las piezas:** el título de un grupo de opciones lleva su (i); un chip de solo lectura, sin ✕; el tooltip respeta los saltos de renglón; las teclas que mueven la pantalla mueven el contenido aunque el foco esté en el menú; un desplegable mide 44 en el teléfono;
   - los diálogos ya dibujados se repasan con la lista: dar de baja en dos columnas, «Deja de pasar» y «Se conserva»; eliminar en una frase; «¿Aprobar N?» con quién la cargó y cuándo.
214. **Las reglas nuevas para toda la app se programan en una segunda parte de la base, antes de la E12** (2026-11-18): la base de la 12 cierra su puerta como está. Lo del traspaso 15 que cae sobre lo ya programado (de la 205 a la 213 y las tareas del núcleo) va junto, con una sola puerta, antes de empezar la E12. Los tableros ya lo dibujan, y las tareas están en `docs/backlog.md`.

Del traspaso 15, biblioteca, asistente, documentos e IA (tanda 4, 2026-11-18). En todas, el usuario eligió la recomendada: la de ArquitecturaBase, adaptada a muchas organizaciones. Las reglas de toda la IA están en la [ADR 0064](../decisions/0064-reglas-de-toda-la-ia.md):

215. **Un documento «Propuesto» que nadie revisa vence a los 90 días** (pregunta 49; F9 de ArquitecturaBase; E25, suma a la 41): desde su último cambio o el fin del envío, pasa a «Descartado» con «Nadie lo revisó en 90 días: venció.» y sigue la purga de los descartados. Igual uno descartado que alguien reabre y deja. Son datos financieros de clientes que no los cargaron.
216. **Lo que corrige una persona no va a la IA** (R1; E25; precisa la ADR 0056): «Cómo viene leyendo» muestra los últimos 30 días por formulario y por dato, y «Ver los ejemplos» lista las últimas correcciones solo a quien tiene `formrecords.read`. Al proveedor no va nada de otras respuestas, tampoco como ejemplo; la lectura mejora con «Cómo reconocerlo» y las preguntas, que edita una persona. Vale para toda la IA: lo que cargó o corrigió alguien nunca viaja en el pedido de otro.
217. **Un tope diario de lecturas por organización** (R6; E25; suma a la 41): además del tope por persona o contacto, 50 lecturas por organización y por día UTC (`Forms:DocumentReadsPerOrganizationPerDay`, que calibra el piloto). Al llegar a cualquiera de los dos, el documento queda en la bandeja sin leer, con el aviso fijo «Por hoy llegué al tope de documentos que puedo leer. Lo dejé para que lo revise el equipo.».
218. **Arma siempre un modelo calificado para armar** (R2; E25; precisa la 0057 §8): en el Inicio, el de `Assistant:Authoring:ProviderModelId`, con la clave que paga ese turno (la de la plataforma o la propia); por WhatsApp, el del canal, que también pasa los casos de armar. El modelo que eligió la organización sigue para las consultas del chat. Como el canal y los documentos: lo que propone o actúa usa un modelo que calificó la plataforma.
219. **Lo que la IA anota con lo que escriben las personas se revisa antes de usarse con otras** (pregunta 41; F7 y F14 de ArquitecturaBase; E23, suma a la 202): en «Formularios que faltan» y en «Preguntas sin respuesta», un tema nuevo, o uno descartado que vuelve, nace «Sin revisar», y el asistente no lo usa en otros chats hasta que alguien lo marca «Revisado» (`forms.manage` en una lista y `whatsapp.manage` en la otra), con su versión y el 409. Al asistente va solo el nombre del tema, y los que ya existen quedan sin revisar. Allá es solo en «Formularios que faltan»; acá van las dos, porque las dos las arma la IA con lo que escriben los clientes (51).
220. **Los estándares de un módulo conviven con las plantillas de la plataforma** (pregunta 40; BI1-A de ArquitecturaBase; con la etapa 2 de ARCA): un módulo aporta sus estándares como datos globales, con las preguntas atadas con candado (cambiar el tipo de «Precio unitario» da `Forms.Draft.BoundQuestionChanged`), sus casillas y sus valores iniciales, y sus reglas llegan activas, como la revisión 1, porque sin ellas el estándar no sirve. Se ven solo con el módulo prendido, la plataforma no los edita y su «Origen» lo dice. Las plantillas de la plataforma siguen con la 30. La E22 no cambia.
221. **Por WhatsApp, una planilla con un pedido de armar va a armar** (E25; cambia la spec y la 0057 §6): con «Armame un catálogo de proveedores con esto», la IA propone el catálogo con sus columnas y unas filas, con «Crear catálogo» y «Cambiar algo». Sin pedido, la planilla va a la lectura de documentos, como antes.
222. **La IA puede proponer importar en un catálogo que existe** (E25; precisa la 154): con «Actualizá Proveedores con esto», la tarjeta trae el mapeo de las columnas y tres filas de muestra, y abre «Importar» de la E12 con el archivo ya revisado; por WhatsApp, el resumen trae el enlace. Importa la persona y reemplaza la lista, como subir un Excel. La IA nunca importa sola.
223. **La IA en varias copias, como ArquitecturaBase** (preguntas 50, 52, 53 y 54; en toda la IA, también en el Inicio ya programado, que va en la segunda parte de la base, 214): el cupo vuelve solo si el pedido venció sin llegar al proveedor; borrar una conversación del Inicio borra en la misma operación sus audios y lo que leyó, con el aviso «Se borran también sus archivos y audios.»; un solo juego de claves de cifrado, con su valor de control, y una copia que no coincide no entra al tráfico; un despliegue deja terminar el paso con la IA, y lo demás vuelve a la cola.

Del traspaso 15, catálogos, formularios, lo que se carga y lo que se decide (tandas 2 y 3, 2026-11-18). El usuario eligió la recomendada en todas; en la 233, después de que se le explicó para qué sirve un cálculo, la que junta lo de acá y lo de allá. Las de la 239 no contradicen nada de acá y se adoptan sin pregunta:

224. **Ordenar una lista que se guarda, con «Ordenar por…», en todo el sistema** (OR1-A de ArquitecturaBase; E12 y E13; precisa la 209): en el ⋮ de «Opciones», «Ordenar por…» reemplaza «Ordenar de la A a la Z» y abre «Ordenar las opciones»: la columna (Texto o Código en la E12; la E13 suma cada campo activo ya guardado, y uno de otro catálogo ordena por el texto de su opción) y el sentido dicho con palabras según el tipo («De la A a la Z», «De menor a mayor», «De la más antigua a la más reciente», «Primero «No»»). El orden queda pendiente hasta guardar, con «Se ordenará por «Texto», de la A a la Z, al guardar.» y «Cancelar orden» arriba de las opciones, y mientras tanto ↑ ↓ y borrar se apagan con su motivo. Al guardar ordena todas, también las de otras páginas y las dadas de baja: los empates conservan su orden, lo vacío va al final y los números dentro de un texto se comparan por su valor («117» antes que «10030»). Se apaga con su motivo en un catálogo sin guardar («Guardá el catálogo antes.») o con movimientos pendientes. No se ordena tocando el encabezado. Como el contenido no tiene idioma (35), el texto se compara con la intercalación del pedido.
225. **Una publicación trabada se puede descartar** (F3 de ArquitecturaBase; E14 y E17; suma a la 175): lo que está en curso o sin confirmar se vuelve a consultar solo, con una espera que crece. Con «Sin confirmar · versión 4», la banda suma «Descartar esta publicación», con el permiso de publicar, que pide confirmar («¿Descartar la publicación de la versión 4?», con el foco en «Cancelar», 161) y la cierra como fallida. Quien vuelve a publicar mientras sigue abierta recibe cuál es, así otra persona llega al descarte. En la E14 sus Flows quedan sin usar, como con una cuenta que rechaza; la E17 suma retirarlos. El «Descartar» de la banda sigue siendo el de los cambios del borrador.
226. **Exportar dice qué exporta con los chips de la pantalla, en todo el sistema** (EX1-A de ArquitecturaBase; E15; aplica la 213): arriba, «Respuestas» y «Filtros» en `DetailGrid`, con los mismos chips de la planilla sin la ✕ («Ninguno» sin filtros); «Columnas» es el título de un grupo con su (i), y sin columnas ocultas «Solo las visibles» queda apagada con «Se ven todas las columnas.»; al pie, «Archivo». Sale la frase con las cantidades y «(13 columnas)». La E19 suma «Filas» y la E21, la vista «Por resolver».
227. **El Excel de un formulario con bloque, una fila por ítem** (pregunta 27; XL1-A de ArquitecturaBase; E19; cambia la 146 en el Excel): una sola hoja «Respuestas»; los datos de la respuesta se repiten en cada ítem, y en el lugar del bloque van «Ítem» y sus preguntas. Una respuesta sin ítems ocupa una fila con «Ítem» vacío, y una de una versión sin el bloque lleva «—». El diálogo suma «Filas», con «Una por ítem de Productos» o «Una por respuesta», que no trae las columnas del ítem, y el chip de una pregunta del bloque dice «Estado: Dañado (en algún ítem de Productos)». Además de las 10.000 respuestas, el tope es de 100.000 filas: se corta antes de escribir, con «El Excel tendría más de 100.000 filas, una por ítem. Acotá los filtros o sacá el bloque de las columnas.». Sale la hoja por bloque.
228. **«Por resolver (N)», la tercera vista de la planilla** (pregunta 24; F2 de ArquitecturaBase; E21; suma a la 148): una respuesta está por resolver cuando una acción de sus reglas quedó «No se pudo» sin reintentarse sola, o en duda después de las consultas automáticas, esperando a una persona. No cuenta lo que la app todavía consulta sola, lo que espera, lo que terminó ni lo que no aplica; acá cuenta también el aviso fuera de las 24 horas sin plantilla (61). Para todo el que ve la planilla, también lo propio, la columna «Reglas», después de «Estado», dice «Por resolver» o «—», y aparece cuando alguna fila de la página la tiene. Quien tiene «Administrar reglas y PDF» y ve todas las respuestas tiene «Por aprobar (N) | Por resolver (N) | Todas», con la misma planilla y el filtro puesto: la vista aparece con la cuenta mayor que cero o ya elegida, y nunca es con la que abre la planilla. El Excel pedido desde ahí exporta lo que se ve, y su diálogo lo dice. Sigue hasta anular o corregir la respuesta, y la corrección arranca sin marca. No suma a «Para resolver» ni a la campana. Va con el segmentado y no con pestañas (79).
229. **«Dónde se completa», un ajuste de cada formulario** (pregunta 26; SA1-A a SA5 de ArquitecturaBase; E20; reemplaza la 96, la 104 y la 200, y la 178 desde la E20; precisa la 98 y la 184): en «El formulario», al lado del prefijo, «Dónde se completa» ofrece «En la app y por WhatsApp», el de un formulario nuevo, o «Solo en la app»; va con el borrador y cada versión lo sella. Aparece solo en una organización con WhatsApp conectado: sin número, todo se carga en la app (98). En un formulario solo para clientes, «Solo en la app» está apagada, con «Los clientes responden solo por WhatsApp.». Con «En la app y por WhatsApp», lo que WhatsApp no puede mostrar (una lista dependiente, un catálogo a mano demasiado grande, un mínimo de más de 10 ítems, una condición con números o fechas, demasiadas preguntas en una pantalla o una respuesta demasiado grande) frena al publicar, con «Ir a la pregunta» y, si no es solo para clientes, «Pasar a «Solo en la app»»: ya no pasa solo. Con «Solo en la app» se va todo lo de WhatsApp, valen los límites de la app y publicar es al instante. Pasar a WhatsApp espera a Meta, y mientras sigue la versión anterior. En «Formularios», la columna «En WhatsApp» pasa a «Estado» y se suma «Dónde se completa»; la baja de uno de la app dice «App · No se carga más». «Enviar formulario» lo sigue mostrando apagado (184), con «Se completa solo en la app.».
230. **La pregunta «Ubicación»** (pregunta 28; UB1-A a UB4-A y W1-A de ArquitecturaBase; E20, y cada etapa siguiente la suma a lo suyo): un punto al completar, con latitud, longitud y precisión, que no prueba presencia. Solo lleva «Obligatoria», nunca va en el bloque, y ninguna condición ni cálculo se apoya en ella. En la app, «Usar mi ubicación» la pide al tocar, con la precisión, «Ver en el mapa» y «Volver a pedirla», y avisa con más de 100 m. En la planilla, el punto es un enlace al mapa; en el detalle, va con la precisión; en el Excel, en tres columnas. Por WhatsApp, antes del Flow sale el pedido de ubicación de Meta, «Para completar «Visita a cliente», mandanos tu ubicación.», con «Seguir sin ubicación» si es opcional; al llegar sale el Flow, todo se guarda en una sola captura y, si no llega, vence con su sesión (con la 270, el tiempo para contestar de la Configuración de WhatsApp de la organización). Un formulario pide una sola ubicación por WhatsApp: una segunda es de lo que WhatsApp no puede mostrar (229). Es un dato personal: sigue la 23 y se borra con la baja de la cuenta de quien la mandó, aunque la respuesta queda. La E21 la suma al PDF y a los avisos, la E23 al asistente y la E25 dice que la IA no la propone y que una ubicación recibida la completa. Se prueba con Meta con el número de una organización (123).
231. **Las carpetas, en una etapa propia, la E26** (pregunta 30; CP1-A a CP5-A de ArquitecturaBase): después de la E25 y antes de las integraciones (76), con dos listados, «Formularios» y «Catálogos» (79), y sin permisos nuevos: solo ordenan y no dan ni quitan acceso. No renumera nada y llega antes de la etapa 2 de ARCA, que la necesita. Su spec se escribe después del traspaso 16, porque allá todavía no pasaron su prueba integral. **La forma, cambiada por la 265: las carpetas son menús del menú lateral.**
232. **Los cálculos redondean al par** (pregunta 32; half-even de ArquitecturaBase; E19; excepción escrita en `numeros-y-moneda.md`): cada cálculo se redondea a sus decimales, y un monto a los de su moneda, con `MidpointRounding.ToEven`, y un total suma lo ya redondeado: 2,205 da 2,20. Es como recalcula ARCA, así el total del formulario y el de la factura cierran. Los demás importes siguen con `AwayFromZero`.
233. **El cálculo, con su fórmula, también dentro del bloque** (pregunta 33; E19; suma a la 54): sigue la fórmula de la E19, con las cuatro operaciones y paréntesis, armada sin escribir código y con el campo numérico de la opción elegida en un catálogo. Como en ArquitecturaBase, un cálculo puede ir dentro del bloque: usa los datos de su ítem, también el campo del catálogo elegido en ese ítem, y se calcula en cada uno («Importe = Cantidad × Precio»); afuera, un cálculo suma una pregunta del bloque, que puede ser ese importe. La suma del bloque ya no multiplica ítem por ítem: lo hace el cálculo del ítem. No se toman las tres operaciones fijas de allá (Multiplicar, Sumar y Porcentaje), porque cada caso nuevo, como un descuento, pedía programar otra.
234. **El cálculo se ve en gris mientras se carga en la app** (S1; CA1-A de ArquitecturaBase; E20; cambia la 97): en la carga desde la web, cada cálculo se ve en gris, sin poder editarse, y cambia al escribir, en la tabla del bloque y en los recuadros. Es una vista previa con la misma fórmula y el mismo redondeo; al enviar, el servidor lo vuelve a calcular y vale lo suyo. Por WhatsApp sigue sin verse (54). Es una excepción a «el front solo muestra», escrita en `numeros-y-moneda.md`.
235. **«Mostrar si» queda como en la E19, con el aviso en el editor** (S2; E19 y E20): hasta 10 condiciones con «y», también con números, fechas y «respondida o no». No se achica a la de ArquitecturaBase, una sola pregunta de elegir una o de sí o no, porque después habría que programar lo que falte. En un formulario que va por WhatsApp, el editor avisa debajo de una condición que WhatsApp no puede mostrar («WhatsApp no muestra una condición con números.»), como hace ArquitecturaBase con la cuarta en cadena, y desde la E20 trae «Pasar a «Solo en la app»» como salida (229).
236. **Las horas del PDF, en la zona de quien cargó** (pregunta 37; ArquitecturaBase; E21): la copia fija lleva una sola zona, la de la cuenta de quien cargó, con su nombre: «17/11/2026, 18:30 (Madrid)». A un cliente que respondió por WhatsApp sin cuenta le va la de la organización. Una corrección conserva la zona de quien cargó.
237. **Quien cargó se entera si su regla se frena** (pregunta 38; F2 de ArquitecturaBase; E21): cuando una acción frena su regla (228), a quien cargó le llega un aviso una vez por acción, por WhatsApp con el número de la organización, en el idioma de su cuenta o en el de la organización si no tiene, con el número y el formulario y sin valores: «Tu respuesta PED-000012 de «Pedido de materiales» quedó frenada. Ya le avisamos a quien la administra.». Con la ventana de 24 horas cerrada, o si cargó desde la web sin chat (89), queda «No aplica» y la acción se marca sin avisar. No usa plantillas.
238. **Quien pasó una respuesta desde un documento no la aprueba** (pregunta 39; F4 de ArquitecturaBase; E25; suma a la 99): como quien la cargó y quien la corrigió, quien la revisó y la pasó desde «Documentos recibidos» queda afuera. No la ve en «Por aprobar» ni en su conteo, y decidir da 403 (`Forms.Approval.OwnRecord`). La tarea guarda también a quien la pasó.
239. **Las acciones de una regla, como ArquitecturaBase** (T14/06 §3 y B3; E21): un aviso a un rol sale a quien sigue en el rol al mandarlo, y quien se fue queda «No aplica» sin frenar a los demás. Pedir aprobación a un rol sin personas que puedan decidir, sin contar a quien cargó ni a quien corrigió, falla sin abrir tarea y se reintenta cuando el rol tiene a alguien. Lo que quedó en duda por un corte de la app se consulta solo, sin reenviar (15 s, el doble cada vez, hasta 15 minutos y ocho veces), y después espera a una persona. La actividad dice quién pidió el último reintento y cuándo. Guardar una regla cuyo aviso cita preguntas, adjunta el PDF o manda copia pide también «Ver respuestas».

Del traspaso 15, WhatsApp y ARCA (tanda 5, 2026-11-18). El usuario eligió la recomendada, la de ArquitecturaBase, en todas menos en el vencimiento de los chats, donde se queda la D4 de [WhatsApp](../features/whatsapp.md#decisiones-del-usuario-e11). Lo de WhatsApp está en las ADR [0065](../decisions/0065-contenido-del-chat-cifrado.md) y [0066](../decisions/0066-lo-que-no-se-sabe-si-salio.md). **Las decisiones 243 a 253 son de ARCA** y están en su [hoja de ruta](2026-11-06-arca-hoja-de-ruta.md#2-lo-que-se-decidió), como la 150 a la 152:

240. **Lo que no se sabe si salió por WhatsApp queda «Sin confirmar»** (N1; H7 y H2 de ArquitecturaBase; cambia la 80): lo que seguro no salió (no se conectó, Meta limitó o respondió un error con código) se reintenta solo, dos veces como mucho, y ese chat espera para no desordenarse. Lo que no se sabe (se cortó la espera o la respuesta, o un error del servidor sin código) no se repite: queda «Sin confirmar» y lo revisa cualquiera que puede enviar en esa bandeja, y queda quién y cuándo. Revisar no reenvía ni dice que llegó. Una invitación por WhatsApp dudosa dice «Sin confirmar» en Usuarios. Vuelven a la E14 y a la E21 el estado y su revisión, que la 80 había sacado.
241. **Una respuesta con una factura emitida no se corrige** (ARCA; excepción a la 93): con la emisión hecha o en camino, nadie la corrige, tampoco con `formrecords.correct`. Se anula, lo que emite la nota de crédito, y se carga otra.
242. **Los nombres fiscales van siempre en español** (ARCA; H28 de ArquitecturaBase; excepción a la 42): el tipo de comprobante, el de documento y la condición frente al IVA («Factura C», «Nota de crédito», «Responsable inscripto») se ven en español en todos lados, también en la vista previa y con la cuenta en inglés, porque son los nombres legales. Lo demás de la pantalla sigue el idioma de la cuenta.

254. **El diálogo de un modelo del asistente, en pasos** (Plataforma › Asistente; la 212 y la 213 en el núcleo; 2026-11-18): al agregar un modelo, los datos técnicos son el segundo paso del diálogo, después de «Siguiente»; al editarlo, se ven como lectura, en una grilla, y «Cambiar datos técnicos» abre ese paso. Sale «Avanzado: datos técnicos», que se plegaba y se desplazaba adentro del diálogo. ArquitecturaBase no tiene ese diálogo, porque allá no se cargan. Se programa en la segunda parte de la base (214).
255. **Una difusión con un destinatario dudoso** (WhatsApp › Difusiones; la 240 y la 0066; 2026-11-19): si no se sabe si le salió el mensaje, la fila del destinatario dice «Sin confirmar», sin revisión, y no cuenta como enviado ni como «No llegó». Es la misma palabra que en los chats y en las invitaciones. Se programa en la segunda parte de la base (214), con `WA-Difusion-Resultados`.
256. **La parte del multitenant en el Gmail compartido** (0063 §2; 2026-11-19): mientras la cuenta de correo de desarrollo se comparta con ArquitecturaBase, el tope global del multitenant es de 80 correos por día, con 20 reservados para invitaciones; los demás topes quedan con los valores de la 0063. La cuenta se separa antes de producción. Va en la segunda parte de la base.
257. **Los nombres de las áreas de Formularios en la matriz de un rol** (212; 2026-11-19): cortos, como «Catálogos», «Formularios» y «Respuestas», que dibuja `Rol`: «Datos de las personas» (*People data*, E16), «Reglas y PDF» (*Rules and PDF*) y «Aprobaciones» (*Approvals*, E21), y «Sugerencias» (*Suggestions*, E23).
258. **Un formulario «Solo en la app» y el asistente** (la 229; E23; 2026-11-19): en el editor, «Cuándo lo ofrece» y la pestaña «Sugerencias» quedan apagados con el motivo «Se completa solo en la app.» y conservan lo cargado. El asistente no lo ofrece, y una sugerencia hacia él deja de ofrecerse, como hacia uno dado de baja; en «Qué sugerir después» aparece marcada con ese motivo. Todo vuelve si se lo pasa otra vez a WhatsApp. Se redibujan `FRM-Editor-Asistente` y `FRM-Editor-Sugerencias`.
259. **Dos textos que se cortan con «…»** (la 174; 2026-11-20): el título de la barra de arriba del teléfono y el valor elegido en un campo cerrado de un renglón se cortan con «…», como los dibujan sus tableros. Por ejemplo, «Organización Taller del Sur…». Todo lo demás sigue entero y baja de renglón, como pide la 174, y la vista previa de la lista de chats sigue siendo la excepción de la 188.
260. **Los permisos de un rol vuelven a las áreas plegables, y un permiso incluye a otro** (RO3-D de ArquitecturaBase, traspaso 16, 01, y su pregunta 69; reemplaza a la 212; 2026-11-23): el usuario eligió la forma de ArquitecturaBase, que es la de nuestro editor de antes de la 212. Cada área es una banda con la flecha, su casilla (vacía, parcial o completa), su nombre y su (i); abierta, cada permiso es una fila con su casilla y su nombre entero. Al entrar se abren las áreas que tienen algo, y un rol nuevo arranca con todas cerradas. Sin buscador, filtros, cuentas ni descripciones a la vista: qué permite cada permiso va en la (i) del área. El Dueño, y todo rol de sistema que no se cambia, se lee con tildes, sin casillas y con sus áreas cerradas. En el teléfono va la misma lista, con filas de 44 px, y la flecha y la casilla del área tienen cada una su zona de 44 px. Es igual en los roles de la organización, en los de una empresa y en los permisos de un operador de Plataforma. **Un permiso incluye a otro:** el que no sirve sin el «Ver» de su misma área lo incluye, dentro de cada catálogo y nunca entre áreas ni entre catálogos. Marcarlo marca lo incluido y lo deja apagado con su motivo («Lo incluye «Administrar usuarios».»). Lo que tiene una cuenta son los permisos de sus roles más lo que esos incluyen, y guardar un rol guarda lo pedido más lo incluido. Hoy entran `users`, `roles`, `companies`, `settings` y `company.members` en la organización y la empresa, y `platform.accounts`, `platform.tenants` y `platform.assistant` en Plataforma. `users.invite` y `roles.assign` se confirman contra sus pantallas. Cada etapa que suma un permiso declara lo que incluye: `formcatalogs.manage` (E12), `forms.manage` y `forms.publish` (E14) y la corrección de respuestas, en su etapa. Los nombres cortos de la 257 son los de las bandas.

Del traspaso 16 (lo que cambió en ArquitecturaBase con su prueba integral, hasta el 21/11) y del 17 (su guía de diseño de los catálogos y formularios, del 22/11), el 2026-11-23. El usuario respondió de a una las preguntas 70 a 78 de allá: en la 70 se quedó con el ⋮ de acá, y en las demás eligió la de ArquitecturaBase. La 269 no contradice nada de acá y se adopta sin pregunta. Lo que cae sobre lo ya programado va en la segunda parte de la base, como pide la 214 ([backlog](../backlog.md#traspasos-16-y-17-para-la-segunda-parte-de-la-base)); lo demás, en la spec de su etapa:

261. **Las acciones de una fila van en su ⋮, y nunca un botón con texto, en todo el sistema** (pregunta 70 del traspaso 16; cambia la 83 en «Cargar»): el usuario se quedó con el ⋮ de acá y no con los íconos a la vista de ArquitecturaBase. Lo que se hace con una fila (cargar, revisar, restaurar, reactivar, descartar, reintentar) es una opción de su ⋮, con su ícono; donde importa el orden siguen ↑ ↓ ✎ 🗑 a la vista (127). Ir a otro lado sigue siendo un enlace, y lo que es un dato de la fila (el nombre que la abre, el número de la última respuesta) sigue siendo texto. «Cargar» de «Formularios» pasa a ser la primera opción del ⋮; en el teléfono la fila de la lista lo sigue ofreciendo con nuestra forma de fila.
262. **Una sola pieza para elegir, en todo el sistema** (pregunta 74 del traspaso 17, que reemplaza la 71 del 16; DS1-A, DS2-A y su extensión del 22/11 de ArquitecturaBase; cambia la 141 y la 143): todo lo que se elige (una o varias opciones, también de dos a cuatro fijas, el Sí o No, los filtros y la carga de un formulario) va en la pieza `Picker`. Una casilla queda solo para un sí o no suelto que se marca («Incluir las fotos»). Sin radios, segmentados ni listas de casillas para elegir, ni un diálogo que solo sirve para elegir: elegir varios adentro de un diálogo que guarda es la pieza múltiple. Quedan afuera la vista previa que imita WhatsApp, las dos vistas de lo mismo (como «Por aprobar | Todas», 148) y los menús de acciones.
   - **Cerrada,** un campo con su flecha fina que dice lo elegido, igual para una y para varias; sin nada, lo dice en gris («Sin zona»). En la barra de un listado, el filtro conserva su botón y abre la misma lista.
   - **Abierta,** la lista de los menús: la descripción de cada opción en gris debajo de su nombre; una opción apagada, con su motivo (211); «Todas» o «Sin …» primero, separada por una línea; la elegida con su tilde a la izquierda y, en varias, una casilla, y marcar no cierra. Con más de diez opciones, o si vienen del servidor, el buscador va adentro, arriba, con el foco puesto y la cantidad debajo; sin buscador, una letra lleva a la primera opción que empieza así. Del servidor: «Buscando…», «Ninguna opción coincide…», «Mostrar N más» y «Reintentar», y las elegidas que no están en la página van primero. Con un tope, la lista dice «Hasta 5» y, al llegar, «Elegiste 5 de 5», y las demás se apagan.
   - **Varias, por su nombre** (DS2-A): separadas por coma, en el orden de la lista; si no entran, las que entran y «y N más». El campo nunca crece, y el nombre accesible las lleva a todas.
   - **En el teléfono** la lista ocupa la pantalla, con el nombre del campo, la ✕, el buscador si son más de diez y renglones de 56 px; en varias se aplica con «Listo (N)». Con el teclado, las flechas recorren, Enter elige (en varias, marca sin cerrar), Escape cierra y devuelve el foco al campo, y Tab cierra y sigue.
   - **Los filtros** (cambia la 143): el desplegable de un filtro es la pieza, sin cantidades, y con muchas opciones se busca adentro de la lista, no escribiendo en el campo. Los segmentados de filtro con su cantidad («Activos (8) · Dados de baja (2)») pasan a un filtro con su botón, sin cantidades.
   - **La carga** (cambia la 141): el Sí o No, que sigue arrancando vacío, elegir una y elegir varias van en la pieza y ocupan una columna; se van las opciones en fila y las casillas en columnas.
   - **La zona horaria:** la ciudad y, en gris, la hora de su país y el desfase («Montevideo · hora de Uruguay · GMT−3»), en una sola lista para toda la app; viaja el identificador de IANA.
   - Lo que llega en mayúsculas de un catálogo se muestra como llega. Una regla de arquitectura del front impide elegir de una lista con otra pieza. Las de hoy (`SearchSelect`, `MultiSelect`, `CountrySelect`, `TimeZoneSelect`, `CultureSelect`, el segmentado de `RadioGroupField` y los desplegables de `FilterBar`) pasan a la pieza en la segunda parte de la base.
263. **Nada debajo de otro dato, en todo el sistema** (pregunta 75 del traspaso 17, que cambia lo que se eligió en la 13 del 15; cambia la 206 y reemplaza las dos líneas de la 130): cada dato va en su columna o en su campo, o no va, en las tablas, las lecturas, los detalles y el teléfono. Se va el texto secundario en gris de la 206: la descripción de un tema que falta pasa a su columna o no va. Se van las dos líneas de la 130: en las filas con ↑ ↓ ✎ 🗑, el tipo de una pregunta y si es obligatoria, o el tipo y el papel de un campo, van en sus columnas. En el teléfono, una fila no junta datos con «·» debajo del nombre: lleva menos datos, o es una tarjeta con cada dato nombrado. Debajo queda solo lo que no es un dato del registro: «Antes: …» al corregir, el error de un campo y la línea gris de quién y cuándo. Lo demás de la 206 sigue, también las listas que no son tablas, como el chat o las opciones de un diálogo, que siguen las lecturas (138).
264. **Ordenar las tablas por sus columnas, en todo el sistema** (pregunta 76 del traspaso 17; OT1-C de ArquitecturaBase): toda columna con un dato ordena. No ordenan Acciones, las de un ícono, las de varios valores (los roles de una persona, elegir varias, el bloque y la ubicación) ni «Cargó» de la planilla, porque Formularios no guarda el nombre (E15).
   - **Desde dos lugares que mueven el mismo orden.** El encabezado: cada columna que ordena es un botón con su texto; la que ordena muestra ↑ o ↓, en el color de la marca, después del título (antes, en una columna de números), y las demás muestran ↕ al pasar el mouse o con el foco, con 44 px para tocar. Y «Ordenar por», último en la barra y separado de los filtros porque no saca filas: dice el orden con palabras («Recibida, más reciente primero») y abre la pieza (262) con «Columna» y «Orden». En el teléfono, el botón ocupa el ancho.
   - **Dónde:** una tabla paginada ordena en la base, con su desempate; una que llega entera, en el navegador, con la misma pieza. El orden se suma a la búsqueda, los filtros y las vistas; cambiarlo vuelve a la página 1, y «Limpiar todo» lo conserva. Va en la dirección (`sort=campo` o `sort=-campo`; la columna de una pregunta suma su id).
   - **Lo vacío va al final,** en los dos sentidos; con el mismo valor, sigue el orden de siempre de esa lista y después su clave. **Un estado o un tipo ordena por el orden del circuito** (Recibida, Por aprobar, Aprobada, Rechazada, Anulada), igual en los dos idiomas. **El primer toque ordena en el sentido útil:** el texto de la A a la Z, los números de mayor a menor, las fechas de la más reciente a la más vieja, el «Sí» primero y un estado por el circuito; el segundo lo invierte.
   - **Sin orden elegido,** cada lista abre como siempre: si ese orden es una columna se ve su flecha, y si es compuesto no hay flecha y el botón dice «Ordenar por».
   - **No cambian las tablas cuyo orden es un dato:** las opciones de un catálogo (224), las preguntas y los campos con ↑ ↓, los ítems de una respuesta, las comparaciones y las vistas previas de una importación. Tampoco «Por aprobar», que es una cola, de la más vieja a la más nueva (148).
265. **Las carpetas son menús del menú lateral** (pregunta 72 del traspaso 16, que reemplaza la 30 del 15; CM1 a CM6 de ArquitecturaBase; cambia la 231 en la forma, y siguen en la E26): cada carpeta del primer nivel es un menú del menú lateral, que abre su panel con sus formularios y un grupo por cada subcarpeta. Son dos niveles: una subcarpeta no tiene otras. Van después de lo de los módulos y antes de Administración, en orden alfabético y con «Nueva carpeta» al final, solo con Formularios prendido para la organización.
   - **Se administran en el menú:** «Nueva carpeta» y un ⋮ en el panel de cada carpeta («Editar…», con el nombre y uno de doce íconos; «Nueva subcarpeta…»; «Mover a…» y, tras una línea, «Borrar» en rojo) y en cada grupo («Editar…», solo el nombre; «Agregar formularios…»; «Mover a…» y «Borrar»). Una carpeta con subcarpetas no entra en otra, y su «Mover a…» queda apagado con el motivo. Las crea, edita, mueve y borra quien tiene `forms.manage`; quien solo tiene `formcatalogs.manage` pone catálogos en las que existen. Una carpeta nueva arranca con el primero de los doce íconos que ninguna otra usa. Borrar cuenta lo que se pierde y nunca borra un formulario ni un catálogo.
   - **Un formulario entra** desde la carpeta, con «Agregar formularios» (la pieza múltiple con «Guardar», 262, que lista también los borradores, con su estado), o desde el formulario, con «Carpetas…», y puede estar en varias. Un catálogo va en carpetas con «Carpetas…» y no aparece en el menú.
   - **Qué abre:** a quien ve o aprueba respuestas, la planilla (con «Agregar respuesta» si además carga); a quien solo carga, la carga; a quien solo administra, el diseño. Desde el menú, las migas dicen «Carpeta / Formulario» y el panel queda abierto con el formulario marcado. En el menú van solo los formularios activos que la persona puede abrir: quien administra formularios ve todas las carpetas, también las vacías, y a los demás no les aparece nada vacío. Quien solo carga no ve «Nueva carpeta», los ⋮ ni «Agregar formularios». El menú se relee solo al publicar, dar de baja, reactivar o eliminar un formulario.
   - **Los listados pierden el panel:** «Formularios» (79) y «Catálogos» suman el filtro «Carpeta».
   - **En el teléfono,** cada carpeta es un grupo del cajón, con su ⋮; con el menú contraído, su ícono, con el nombre en el tooltip. Sigue lo demás de la 231: no dan ni quitan acceso, sin permisos nuevos, `ITenantOwned` con RLS, 500 por organización y las carpetas del sistema por clave. Allá se pasó de cinco niveles a dos con una migración en dos despliegues; acá no hay datos y nace con dos.
266. **Una respuesta se lee como el formulario y se corrige en el lugar** (pregunta 77 del traspaso 17; VF1-A de ArquitecturaBase; cambia la 139 en el detalle, la 166 en dónde se corrige y la 209 en «Corregir»): la vista rápida y la pantalla completa tienen la forma de la carga, con las mismas zonas, el mismo orden, la grilla de hasta tres por renglón y el ancho de cada tipo (141).
   - **Cada pregunta es su control,** con lo respondido adentro y sin poder tocarlo: el recuadro gris claro con el valor en negro; un cálculo, en gris y con su fórmula en la (i); la ubicación, en un renglón con su precisión y «Ver en el mapa»; el bloque, en su tabla.
   - **Los datos de la opción de un catálogo** (los campos sellados) son cada uno su campo, en el mismo renglón, con el nombre «Pregunta · Dato» («Cliente · Zona») y en gris, porque salen del catálogo; en el bloque, una columna más. Lo que no aporta no va, y no se repite lo que ya es otra respuesta.
   - **Lo que una condición ocultó no aparece,** como al cargar. Arriba queda la línea gris de quién y cuándo, y los momentos pasan a la actividad (267).
   - **«Corregir» va a la vista:** en la banda de la pantalla completa, con texto; en la vista rápida, el lápiz primero. «Anular» sigue en el ⋮, en rojo. Quién ve cada uno lo decide el servidor (93), y sin permiso no se dibujan.
   - **Corregir destraba los mismos controles en la misma pantalla:** el título pasa a «Corregir VIS-000126», la banda queda con «Cancelar» y «Guardar corrección», y se van el ⋮, las flechas y la actividad. «Antes: …» va debajo de lo que cambió, salvo adentro del bloque, y el motivo, obligatorio, al final. Ya no hay otro panel ni otra página para corregir. La respuesta se lee con su versión y se corrige con la activa; si algo cambia, un aviso arriba lo dice al empezar.
   - **El detalle trae la forma de la versión de la respuesta:** las preguntas en orden con su tipo, los títulos, las condiciones, los cálculos y, en cada pregunta de catálogo, sus datos visibles con su nombre por idioma.

   La E15 arma esa forma de solo lectura, que la E20 vuelve editable para cargar; la E18, la E19 y la E21 suman lo suyo (fotos y documentos, condiciones, cálculos y bloque, y corregir en el lugar).
267. **La actividad de una respuesta, renglón por renglón** (pregunta 78 del traspaso 17; VF2-A de ArquitecturaBase; cambia la 139 en la actividad): una tabla con un renglón por cada cosa que pasó y las columnas Fecha, Hora, Qué pasó, Quién, Canal, Detalle, Estado y Acciones; lo que no aplica dice «—». Las bandas por momento («Al recibirla», «Aprobación», «Al aprobarla») quedan como renglones que separan. Cada PDF es su renglón, y cada persona de un aviso también, con su estado («Entregado», «Leído», «No se pudo», «Sin confirmar»). El motivo de un rechazo, de una corrección o de una anulación va entero en «Detalle», la única celda que puede ocupar varias líneas. Lo que se hace con un renglón (ver o bajar el PDF, reintentar, revisar un envío) va en su ⋮ (261). En la vista rápida y en el teléfono, donde no entran ocho columnas, cada renglón es un recuadro, con «Qué pasó» arriba y los demás datos con su nombre, de a dos. La E15 la arma con la mandó y la respondió, y cada etapa suma sus renglones.
268. **Cuando el asistente cree, edite o elimine, la IA prepara y la pantalla de siempre confirma** (pregunta 73 del traspaso 16, que amplía la 43 del 15; ADR 0034 de ArquitecturaBase; es la forma de la v2 de la D8 de la [ADR 0048](../decisions/0048-asistente.md), con su diseño en la [ADR 0068](../decisions/0068-acciones-del-asistente.md) y su etapa, «Acciones del asistente», después de la segunda parte de la base y antes de la E12 (2026-11-23)): crear, editar o eliminar con el asistente es una acción declarada, con un id estable con versión (`users.create@1`), sus campos y de dónde salen sus opciones, su permiso, su validación, dónde se confirma, si tiene un efecto fuera del sistema, sus casos de ejemplo y su presupuesto. Un registro del núcleo junta las que aporta cada módulo, sin nombrarlo, y las filtra por la organización, los módulos prendidos para ella y los permisos y el alcance de quien pide. Tres piezas sirven a todas las pantallas: la lectura de los tipos de acción que esa persona puede hacer, la de los campos y las opciones acotadas de una acción y una sola herramienta de proponer. Se valida sin guardar, con el esquema de la acción y el validador de su pantalla, y lo rechazado vuelve al modelo.
   - **Confirma la pantalla de siempre:** lo que se guarda en un diálogo, en ese diálogo sobre el Inicio, con «Propuesta del asistente»; lo que se guarda en una página, en esa página con lo propuesto y sin guardar, con un aviso antes de «Guardar» si tiene efectos fuera del sistema; lo que nace en borrador o por aprobar lo crea la tarjeta (154); editar muestra solo lo que cambia, con el 409 de siempre, y eliminar abre la confirmación de esa pantalla. De a una cosa, y nunca claves ni conexiones, consentimientos tildados ni efectos fuera del sistema sin su confirmación. Sin pantalla donde confirmar, contesta «todavía no».
   - **Al proveedor, lo mínimo:** lo que dijo la persona, los tipos de acción y los nombres de las definiciones, nunca datos de otros registros ni de otra organización. A quién se refiere un editar o un eliminar lo busca la pantalla, con los permisos de quien pide.
   - **Por WhatsApp,** un resumen con «Revisar en la app», solo para quien trabaja en la organización y con sus permisos: a un cliente (50) no se le ofrece ninguna.
   - Una acción se ofrece cuando sus casos pasan la calificación contra el modelo real y su costo está medido. El primer ejemplo, cuando se programe, es «Nuevo usuario» desde el Inicio, con solo los roles que quien pide puede dar (0061). Desde la E12, cada etapa que suma una pantalla que crea, edita o elimina declara sus acciones con la [receta](../guides/agregar-una-accion-al-asistente.md), y se ofrecen calificadas antes de la prueba integral del bloque.
269. **Los principios del traspaso 17 y la lista de chequeo con las reglas del 22/11, en todo el sistema** (traspaso 17, 01; no contradicen nada de acá y se adoptan sin pregunta, como la 213):
   - **el contenido es del autor y la forma, de la plataforma:** quien arma un formulario o un catálogo decide qué preguntas o campos tiene y en qué orden; cómo se ven lo decide la app por el tipo de cada dato, igual al cargar, al leer, en la planilla, en el Excel y en el PDF, y el autor nunca elige anchos, estilos ni controles;
   - **lo que se busca, donde se busca:** cada cosa va donde la persona la espera, aunque otra forma parezca más ordenada, como las carpetas en el menú (265);
   - **lo que el servidor rechaza, la pantalla lo frena antes:** apagado con su motivo (211), o con el error solo en el campo que falta; el tope de un campo es el mismo en la pantalla y en el servidor, y al llegar se ve (O10, O13, O16 y O26 de la prueba de allá);
   - **se diseña con datos de verdad** y con las medidas de la app real: 331 opciones, textos largos, nombres en inglés, celdas vacías, varias versiones de un formulario y un bloque con muchos ítems;
   - **la lista de chequeo** de la 213 suma estas reglas: todo lo que se elige va en la pieza (262); nada debajo de otro dato, la respuesta con la forma de su formulario y la actividad renglón por renglón (263, 266 y 267); toda columna con un dato ordena (264), y ninguna fila lleva un botón con texto (261).

Del traspaso 18 (lo último de ArquitecturaBase, hasta el 23/11), tanda 1, el 2026-11-23. El usuario aprobó el plan de la tanda y respondió de a una las preguntas 79 a 82: en la 80 eligió que todo cupo sea de la organización, y en las demás, la de ArquitecturaBase. Lo que cae sobre lo ya programado va en la segunda parte de la base (214); lo demás, en la spec de su etapa:

270. **Ninguna regla de negocio fija en el código** (pregunta 79 del traspaso 18; el criterio de RC1-A a RC7-B de ArquitecturaBase; generaliza la 66): todo tope, plazo, cupo, espera o valor por defecto de una regla de negocio, que un administrador querría ajustar, se ajusta en pantalla. Cada organización ajusta lo suyo en su Configuración, dentro de un máximo que la plataforma fija en la suya; lo que no es de ninguna organización (los países que reciben códigos, porque cada código cuesta, o los plazos de los datos de la plataforma) lo fija solo la plataforma. Cada valor va en la configuración de su dueño (el núcleo no nombra los módulos), con su valor por defecto, su mínimo y su máximo, revisión y 409, y se lee de la base en cada uso, no al arrancar; lo guardado antes del cambio, y lo que escribe la versión anterior mientras conviven, lee el valor por defecto (0060). Un texto que nombra el número lo recibe como parámetro, y la política de privacidad promete los mismos plazos. Quedan en el código lo que fija un proveedor o una norma (Meta, ARCA, el proveedor de IA, el servidor de correo) y lo técnico (leases, lotes, esperas, reintentos, los topes de capacidad de un formulario o un catálogo); quedan en `appsettings` lo de seguridad, costo o abuso (el código de ingreso, los cupos de códigos y de invitaciones, las cuotas de pedidos). **Un tope que corta contesta o deriva, y dice por qué** (O35, O36 y O39 de allá): nunca deja a la persona sin respuesta ni al operador sin pista. La regla está en [configuración](../operations/configuracion.md#reglas-de-negocio-en-pantalla-270); lo programado va en la segunda parte de la base, y cada etapa nace con lo suyo.
271. **Los cupos diarios de la IA son de cada organización y se renuevan a su medianoche** (pregunta 80 del traspaso 18; RC5 de ArquitecturaBase, llevado a cada organización; cambia la [ADR 0049](../decisions/0049-precisiones-whatsapp-y-asistente.md) en el corte del día y en el cupo con la clave de la plataforma): con la clave propia o con la de la plataforma, el cupo diario de una persona es de esa organización y se renueva a la medianoche de la zona horaria de la organización. Una persona en dos organizaciones tiene dos cupos, también con la clave de la plataforma: el usuario lo eligió así, aunque esa clave la pague la plataforma. Fuera de una organización (el acceso personal y Plataforma), el cupo es de la persona y se renueva a la medianoche de la zona de su perfil. Las relecturas de un documento y los demás cupos diarios de la IA cortan igual. El presupuesto mensual ya usaba la zona de la organización y no cambia. Cuando un cupo se agota, la persona y el operador ven que fue el cupo (O35 de allá, con la 270).
272. **La retención, con sus plazos en pantalla, y los chats de WhatsApp a 90 días** (pregunta 81 del traspaso 18; RC1-A de ArquitecturaBase; cambia la D4 de [WhatsApp](../features/whatsapp.md#decisiones-del-usuario-e11), que se había reconfirmado el 2026-11-18, y lleva a pantalla los plazos de la [ADR 0062](../decisions/0062-retencion-de-los-datos-del-ingreso.md), de la 215 y del Inicio): la plataforma fija los plazos de sus datos en su configuración, en «Privacidad y retención»: los ingresos (12 meses, de 1 a 120), los códigos y enlaces (30 días, de 1 a 3650), las invitaciones cerradas (30 días, de 7 a 3650) y las conversaciones del Inicio (30 días, el `retentionDays` que ya tiene), además de los chats de su propio WhatsApp (90 días, de 1 a 3650). Cada organización elige en su Configuración los de sus datos, dentro de los límites de la plataforma: los mensajes de sus chats de WhatsApp (90 días por defecto, de 1 a 3650; ya no «mientras exista el contacto») y, desde la E25, vencer los documentos propuestos sin revisar y borrar los descartados (90 días cada uno). La purga de los chats corre por organización con `TenantJobRunner`, y la (i) de cada zona avisa que la política de privacidad tiene que prometer lo mismo. Los tokens de OpenIddict (14 días) son técnicos y quedan en el código.
273. **El nombre de la app, el remitente y el logo de los correos, en la configuración de Plataforma** (pregunta 82 del traspaso 18; RC6-B de ArquitecturaBase): Plataforma › Configuración suma «Nombre y correos», con el nombre de la aplicación y el del remitente (hasta 60 caracteres cada uno) y el logo de los correos (vacío, o una dirección `https://`), y «Así llega» con el correo de un código. Salen de `appsettings` (`Email:AppName` y `Email:Smtp:FromName`), con la regla de las dos versiones: se leen de la base un despliegue antes de borrar las claves. El nombre lo usan también las respuestas del bot de WhatsApp. Cada correo de una organización la nombra con su nombre y su logo, como la invitación. Los cupos de correo siguen en `appsettings` (0063).

Del traspaso 18, tanda 2 (catálogos y formularios, de la E12 a la E17), el 2026-11-23. El usuario aprobó el plan de la tanda, con la propuesta de la 275, y respondió la pregunta 83: se quedó con el calendario de WhatsApp, no con la de ArquitecturaBase. Las decisiones 261 a 270 que caen en estas etapas están en sus specs:

274. **La fecha de un formulario por WhatsApp sigue con el calendario de WhatsApp** (pregunta 83 del traspaso 18; O48 de ArquitecturaBase, que allá pasó a un texto con números; excepción escrita a la 205): la pregunta de fecha de un Flow sigue con `CalendarPicker`, más cómodo en el celular. Dentro de WhatsApp la fecha se ve como la dibuja Meta («22 Nov. 2026» en el formulario y «Nov 22 2026» en su resumen), porque Meta no deja elegir el formato; lo que arma la app (la planilla, el detalle, el PDF, el Excel, los correos y los mensajes que manda por WhatsApp) sigue solo con números. Es la única excepción a la 205, y está escrita en [fechas y zonas](../rules/fechas-y-zonas.md).
275. **El panel de «Gestión de formularios» en dos grupos, y la columna «Por aprobar» en «Formularios»** (FU1-B y FU2-A de ArquitecturaBase, del 23/11, que allá unieron «Formularios» y «Diseñar formularios» en lo que acá ya es la 79; aprobado por el usuario con el plan de la tanda): el panel lleva arriba «Formularios» y, desde la E25, «Documentos recibidos», con su número; después una línea sin título, y abajo «Catálogos» y, cuando llegan, «Biblioteca» (E22) y «Formularios que faltan» (E23). En la lista «Formularios», el nombre abre el editor y el número de respuestas, la planilla. Desde la E21, la lista suma la columna «Por aprobar», solo para quien aprueba, cuyo número abre la planilla en «Por aprobar», y el filtro «Estado» suma «Con algo por aprobar». «Cargar» sigue en el ⋮ (261).

Del traspaso 18, tanda 3 (lo que se carga y lo que se decide, de la E18 a la E21), el 2026-11-23. El usuario aprobó el plan de la tanda y eligió la recomendada en la pregunta 84. Las decisiones 261 a 270 y la 275 que caen en estas etapas están en sus specs:

276. **«Pedir aprobación» con dos casillas** (pregunta 84 del traspaso 18; RC4-A de ArquitecturaBase; cambia la 25 y la 238 en que son fijas, y la regla del motivo de la E21): cada acción «Pedir aprobación» suma, debajo del rol, «Puede decidir quien cargó» (sin marcar) y «Exigir motivo al rechazar» (marcada), cada una con su (i). Por defecto queda como hasta ahora. Se guardan con la revisión de las reglas, y cada respuesta las sella al llegar: lo que ya espera aprobación sigue con lo que tenía. Con la primera marcada, quien cargó la respuesta, quien la corrigió y quien la pasó desde un documento (238) también la deciden, la ven en «Por aprobar» y en sus conteos, y cuentan como personas del rol para pedir aprobación (239). Sin la segunda, rechazar sin motivo vale y la actividad lo dice. Un motivo que falta cuando se exige es 400 `Forms.Approval.ReasonRequired`. Sirve para un rol con una sola persona, que hasta ahora trababa todo lo que esa persona cargaba.

Del traspaso 18, tanda 4 (biblioteca, asistente y documentos, de la E22 a la E25), el 2026-11-23. El usuario aprobó el plan de la tanda, con la 277 y la 278; no tenía preguntas numeradas. Las decisiones 261 a 272 que caen en estas etapas están en sus specs:

277. **Un dato leído que no tiene la forma de su validación queda sin leer** (O44 de ArquitecturaBase, opción A que eligió allá el usuario; E25, precisa la 41 y las validaciones fijas): si lo que la IA leyó de un documento o de una fila de una planilla ni siquiera tiene la forma que pide su validación (22 dígitos o un alias para una cuenta, once dígitos para una CUIT), la pregunta queda vacía, como lo que no se leyó, y no se propone marcada como inválida. Lo que tiene la forma y no pasa la validación (un CBU con el dígito verificador mal) sigue marcado. Por ejemplo, el número interno de una cuenta de débito, que no es CBU, CVU ni alias, queda sin leer.
278. **El tope de ofrecimientos del asistente, como allá** (TO1-A, con O36 y O39 de ArquitecturaBase; E23; precisa la 66 con la 270): cada organización lo elige por bandeja, de 1 a 50 y 2 por defecto, dentro del máximo de la plataforma; sin un asistente guardado vale 2. Con el tope lleno, el pedido deriva a una persona con el motivo «Llegó al tope de ofrecimientos del día», a la vista en la franja del chat y en el porqué: la persona nunca queda sin respuesta. Elegir una opción de una pregunta ya ofrecida no gasta otra oferta, una carga desde una planilla tampoco, y los textos no nombran «dos». Un audio se transcribe siempre y sigue como un texto, también con el tope lleno: si lo contesta un caso, lo contesta.

Del traspaso 18, tanda 5 (WhatsApp y ARCA), el 2026-11-23. El usuario aprobó el plan de la tanda; no tenía preguntas numeradas. Los países que reciben códigos pasan a la configuración de Plataforma con la segunda parte de la base (270), y **la 279, el aviso de vencimiento del certificado, es de ARCA** y está en su [hoja de ruta](2026-11-06-arca-hoja-de-ruta.md#2-lo-que-se-decidió), como la 243 a la 253.

De la revisión de alineación del 2026-11-23. El usuario eligió la recomendada:

280. **Un documento repetido por WhatsApp dice si solo se propuso o si ya se cargó** (O37 y O46 de ArquitecturaBase, que allá siguen sin decidir; E25): si el primero solo se propuso, el asistente contesta «Ya me lo mandaste el {fecha}: completá el formulario que te mandé.»; si ya se cargó, el de `Forms.Document.Duplicate`, «Ya se cargó en {number} el {date}.». Nunca dice que se cargó lo que solo se propuso.

De la revisión de punta a punta del 2026-11-25. El usuario eligió la recomendada:

281. **Un correo de una regla que no se sabe si salió queda «Sin confirmar»** (revisión del 2026-11-25, la G3-16; [ADR 0066](../decisions/0066-lo-que-no-se-sabe-si-salio.md) §7, que da al correo el mismo estado de la cola, y §4; precisa la 240, que lo decía solo por WhatsApp, la 228 y la 267, que ya lista «Sin confirmar» entre los estados de un aviso; E21): si la cola no sabe si salió el correo de una regla, por ejemplo porque el servidor de correo cortó la conexión después de recibirlo, el renglón de esa persona en la actividad de la respuesta dice «Sin confirmar», como una invitación por correo. No se repite solo, y la respuesta queda «Por resolver» (228). Como un correo no tiene bandeja donde revisarlo, quien tiene «Administrar reglas y PDF» ve «Reintentar» en el ⋮ del renglón (261), que pide confirmar: «Puede que ya le haya llegado. ¿Mandarlo otra vez?». Una vez mandado, deja de estar por resolver. Allá el correo se da por hecho al dejarlo en la cola, porque su cola no tiene «Sin confirmar».

282. **El correo de una regla lleva arriba el nombre y el logo de la organización** (repaso de la B18 del 2026-11-25; precisa la 273, que decía «como la invitación»; E21): el correo que manda una regla de Formularios lleva en el encabezado el nombre y el logo de la página pública de la organización, en lugar de los de la aplicación, como el PDF (82); sin página pública, solo el nombre. El nombre de la aplicación y el remitente siguen saliendo de Plataforma › Configuración (273). La invitación queda como está: el encabezado de la aplicación y la organización nombrada en el texto.

283. **La hora en que vuelve el cupo del día se dice en la zona de la organización** (repaso de la F13 del 2026-11-25; precisa la 271; tablero `Asistente-Org-Base2`): dentro de una organización, el aviso del cupo agotado dice cuándo vuelve en la zona de la organización y la nombra («Vuelven el 24/11/2026, a las 00:00, hora de Grupo Delta.»), porque el cupo corta a su medianoche, aunque el perfil de quien mira esté en otra zona. Fuera de una organización, en la zona del perfil. Es una excepción a la regla del front de mostrar fecha y hora en la zona de quien mira (decisión del 2026-10-25), solo para la hora en que vuelve un cupo.

284. **Los tableros de la E12, redibujados con la tanda 2 del traspaso 18, quedan aprobados con lo que recomendó Diseño** (2026-11-26, lienzo v185; la tanda 2 de la revisión del 2026-11-25): el catálogo nuevo arranca con «Escritas a mano» y «Texto» elegidos, como el diálogo aprobado en la v150; mientras se revisa una importación, «Desde» y «Cambiar» quedan apagados con su motivo («Para cambiar el origen, cancelá y volvé a importar.», 115 y 211); el resumen de Catálogos dice solo la cantidad («7 catálogos»), porque lo activo lo dice el filtro «Estado»; en el teléfono, una condición va sin «Si» ni «Y» y la tarjeta de un catálogo lleva de dónde salen, las opciones y el estado; el cliente de ejemplo de un catálogo de contactos arranca en «Sin cliente»; en una propuesta del asistente, «Lo que cambia» va en tres columnas, como `Rol-Propuesta`, y el motivo de «Revisá», debajo de la celda.

285. **Los tableros de la E13 y la E14, redibujados con la tanda 2 del traspaso 18, quedan aprobados con lo que recomendó Diseño** (2026-11-26, lienzo v186): los campos de «En WhatsApp» van en la pieza múltiple en el orden del catálogo, sin chips; en el teléfono, la tarjeta de una opción lleva en el editor los campos que deja «Columnas», y en el modal, Código y Detalle; «Columnas» es un botón a todo el ancho arriba de las tarjetas; el mismo catálogo aparece apagado en el catálogo de un campo, con su motivo; la tarjeta de Catálogos de la E14 lleva de dónde salen, quién lo usa y el estado; «Lo usan» ordena solo desde su encabezado; en el editor, la fila de una pregunta no lleva el catálogo ni sus detalles («3 opciones», «2 decimales»), y el tipo puede ocupar dos renglones; enviar un formulario por el chat abre sin uno elegido, la pieza trae de a 20 y muestra solo el nombre, un problema de todo el formulario se rotula «En WhatsApp», y la fila de un envío revisado sigue diciendo «Sin confirmar»; el máximo de la plataforma para contestar va de 1 a 168 horas, y si baja por debajo de lo que guardó una organización, el error aparece al editar; los datos de un valor de catálogo en la respuesta se rotulan «Código», «Detalle» y «Catálogo».

**Condición del usuario para todas:** ninguna decisión copiada de ArquitecturaBase pasa por encima de [AGENTS.md](../../AGENTS.md) ni de [docs/rules](../rules/README.md); si una choca con una regla de acá, manda la regla de acá.

## 6. Qué tomar de ArquitecturaBase

Repo `C:\Users\ezequ\source\repos\ArquitecturaBase` y su front `ArquitecturaBaseFront`. **El código está en `origin/main`:** antes de leerlo, traé los cambios, porque la copia local puede estar atrasada. Se sigue desarrollando allá, así que cada etapa mira lo que haya al planificarse.

**Qué se toma:** allá solo se lee y no se toca nada (108). Las reglas y los diseños son los de este proyecto, y las reglas de producto que sirvieron ya están en estas decisiones. **El código tampoco se copia:** la estructura (módulos, entidades, servicios y rutas) sale de este repo, con su modelo de organizaciones, empresas y plataforma, sus fichas y sus módulos de ejemplo (Asistente y WhatsApp). De ArquitecturaBase se toma solo lo que no depende del modelo de negocio:
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

**Lo que se toma se adapta a este repo:**
- `BusinessForms` pasa a `Forms`, en `Domain\Forms\` en vez de `Domain/Modules/`;
- los puertos van en `Application/Interfaces/Integrations/StructuredData/`, no en `Interfaces/Channels/`;
- permisos en el catálogo del núcleo, tres clases de datos con RLS, `[Access]`, `[FeatureGate]`, `[Idempotent]`, `OperationLog.RunAsync` e `IVersioned`;
- la lista cerrada de sufijos de helpers: `Compiler` y `Codec` no están. Se renombran a uno de la lista (por ejemplo, `WhatsAppFlowBuilder`), o se suma el sufijo a la ficha con su test.

**En curso allá al 2026-11-08,** sin traspasar (lo que siguió al traspaso 10, hasta `ae60b950`). No se arma nada con esto hasta que llegue su traspaso, y lo que allá no se probó a mano no se toma como probado acá: EP1-A, los esqueletos, importar y exportar opciones, las listas dependientes, el menú sin saltos y las etapas 2 y 3 de renglones.
- **Renglones:** allá se probaron de punta a punta con Meta el 2026-11-09, y sus planes se retiraron (traspaso 15). Lo suyo ya está acá: la planilla, «Filtros» y el Excel en la spec 19, la carga en la 20, y el PDF y las reglas en la 21.
- **Elegir primero por un campo** (LD1-A a LD3-A): programado allá; acá va después del bloque (191).
- **La IA arma, etapas 2 y 3:** las planillas con «Importar opciones» y los documentos. Ante un pedido vago, allá pregunta y espera, y acá propone enseguida (192).
- **Carpetas:** allá pasaron a ser menús del menú lateral el 20/11 (CM1 a CM6); acá son la E26, con la 265.

**Allá al 2026-11-23** (traspaso 18, 05, que reemplaza los estados de los anteriores): en `main`, las reglas que pasaron a Configuración (acá, la 270 a la 273), el orden de las tablas en el backend, la pieza única en el front y casi toda la respuesta como formulario, y en la app los roles (RO3-D) y las carpetas como menús. Elegidos y sin programar: Información del negocio (el horario en una tabla de lectura, con un diálogo por día) y Formularios en una sola entrada, que llega a lo que acá ya es la 79. La prueba integral probó WhatsApp entero y le faltan los pasos a mano y el cierre; lo que siga llega con el traspaso 19. **Sin decidir allá,** anotado sin etapa:
- **los procesos del campo y la ADR 0033 de allá** (motores configurables, en propuesta): se cruzan con la 76 y con los orígenes externos de los catálogos. Si allá se aceptan, cada motor va por organización y los paquetes y los catálogos de referencia los comparte la plataforma;
- **MO1-B,** los módulos que prende y apaga el administrador de cada cliente, todavía sin documento: acá eso es de la plataforma (`platform.tenants.manage`), así que choca. Se espera el documento;
- **el CRM,** sin nada decidido, y los recorridos de punta a punta con Playwright, que allá siguen como propuesta;
- **lo que vio su prueba integral,** como riesgos para cada etapa, en el [backlog](../backlog.md#lo-que-vio-la-prueba-integral-de-arquitecturabase).
- **Lo que falta decidir allá:** el rendimiento para producción, la homologación de ARCA, Azure y el teclado de Roles. «Desvincular» sin el módulo de WhatsApp se decidió allá el 2026-11-07 (la ruta pasa al núcleo), y acá ya está en el núcleo (`api/me/login-methods`).

## 7. Decisiones pendientes

Ninguna.

Las de los traspasos 16 y 17, del 2026-11-23, pasaron a la §5 como decisiones 260 a 269, las de la tanda 1 del traspaso 18, del mismo día, como 270 a 273, las de su tanda 2 como 274 y 275, la de su tanda 3 como 276, las de su tanda 4 como 277 y 278, y la de su tanda 5, de ARCA, como 279, en su hoja de ruta.

Las del traspaso 10, del 2026-11-07, pasaron a la §5 como decisiones 160 a 179 (su evaluación, retirada el 2026-11-08: `git show 4626f5ba:docs/plans/2026-11-07-traspaso-10-evaluacion.md`), y lo que se decidió después, al escribir las specs y al dibujar, como 180 a 190; las dos de forma de trabajo, a `AGENTS.md`. Las de los commits de allá del 2026-11-07 y el 2026-11-08, revisados el 2026-11-08, como 191 y 192, y las de la revisión de las specs de ese mismo día, como 193 a 199. Las que salieron al escribir los planes ejecutables, el mismo día, son la 200 a la 203.

Las del 2026-10-17 pasaron a la §5 como decisiones 20 a 29, las del 2026-10-18 como decisiones 30 a 33, las del 2026-10-24 como decisiones 34 a 77, las del 2026-10-25 como decisiones 78 a 82, las del traspaso 3, del 2026-10-30 y el 2026-10-31, como decisiones 83 a 98, las del traspaso 4, del 2026-11-02, como decisiones 109 a 122, las del traspaso 5, del 2026-11-03, como decisiones 136 a 149, las del traspaso 6, del 2026-11-06, como decisiones 150 a 152, en la [hoja de ruta de ARCA](2026-11-06-arca-hoja-de-ruta.md), la del traspaso 7, del mismo día, como decisión 153, las del traspaso 8, también del 2026-11-06, como decisiones 154 a 156, y las del traspaso 9, del mismo día, como decisiones 157 a 159.
