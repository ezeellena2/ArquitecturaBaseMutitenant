# Formularios dinámicos por WhatsApp: hoja de ruta

Estado: **propuesta del 2026-10-12, con las decisiones de producto tomadas** ([sección 5](#5-decisiones-de-producto)), para sumar al [plan de desarrollo](2026-09-27-plan-de-desarrollo.md). No es un plan de etapa: cada etapa se escribe en el formato de [etapa en paralelo](../guides/etapa-en-paralelo.md) cuando le toque, a partir de su sección de acá.

**Origen.** Es la misma funcionalidad que se está construyendo en `ArquitecturaBase`, adaptada a organizaciones, empresas, plataforma y personas. Allá la Etapa 1 está cerrada y probada con Meta real (envío, respuesta y número guardado), y las demás tienen diseño y decisiones elegidas. Las referencias para copiar código y tableros están en la [sección 6](#6-qué-copiar-de-arquitecturabase).

## 1. Qué es el producto

- **Formularios propios.** Una organización arma sus formularios: preguntas tipadas, textos en español e inglés, versiones y un prefijo de numeración propio. No hay circuitos rígidos: cada formulario es independiente y puede sugerir otro.
- **Por WhatsApp.**
  - Se publican como Flows de Meta en el número de la organización.
  - Se mandan desde el chat, y el empleado los completa dentro de WhatsApp.
  - La respuesta queda numerada (`VIS-000123`) y se ve en el chat.
- **Catálogos.** Listas compartidas entre formularios (clientes, supervisores, equipos), escritas a mano o tomadas de los datos del sistema con filtros.
- **Fotos y documentos dentro del formulario.** Con cámara o galería: PDF, Excel, Word e imágenes.
- **Planilla.** Cada formulario es una planilla con filtros, detalle y exportación a Excel.
- **Reglas.** Al llegar una respuesta se puede pedir aprobación, armar un PDF y avisar por WhatsApp.
- **Asistente.** Reconoce lo que la persona cuenta (texto o audio) y le ofrece el formulario prellenado. Nunca guarda solo: confirma la persona.
- **Plataforma (nuevo respecto de ArquitecturaBase).** Una biblioteca de plantillas (formularios y catálogos de ejemplo) que la organización puede tomar como copia propia, o crear desde cero.

## 2. Cómo encaja en el modelo multitenant

### Quién es quién

| Acceso | Rol en formularios |
| --- | --- |
| **Plataforma** | Mantiene la biblioteca de plantillas y habilita el módulo por organización. **Nunca ve formularios, respuestas ni archivos de una organización.** |
| **Organización (B2B)** | Dueña de sus formularios, catálogos, reglas, respuestas y archivos. Los diseña quien tiene permiso; los responden sus empleados. |
| **Empresa** | En la v1 los formularios son de la organización; la respuesta guarda la empresa de quien respondió, para filtrar ([decisión 1](#5-decisiones-de-producto)). |
| **Empleado** | Miembro activo con teléfono verificado y permiso `formrecords.create`. Lo reconoce la bandeja «Internos» de E11. |
| **Persona (B2C)** | Fuera de la v1. Responder como cliente de una organización es una etapa posterior ([decisión 3](#5-decisiones-de-producto)). |

### Clasificación de los datos

- **Privado de la organización (`ITenantOwned`, esquema `tenant`, RLS):** definiciones, versiones, catálogos e ítems, respuestas y valores, recibos, reglas, ejecuciones, tareas, documentos, metadatos de archivos, sugerencias y pedidos de formularios.
- **Global de plataforma (esquema `platform`, ADR 0050):** la biblioteca de plantillas y sus versiones.
- **Compartido persona↔organización (`IConsumerBusinessShared`):** solo cuando llegue B2C. Requiere resolver antes el backlog #51: `Enter` borra `TenantKind`, así que hoy un worker no puede crear datos compartidos.
- **Entrada de WhatsApp:** el webhook sigue global (`identity`). El worker resuelve la organización por el número de E11 (`WhatsAppChannels`) y captura dentro de `ITenantScope.Enter(tenantId)`, con `IFeatureService` antes de actuar.
- Un recurso de otra organización responde 404. Cada ruta nueva lleva su test de aislamiento.

### Dependencias

| Dependencia | Para qué | Etapa que la necesita |
| --- | --- | --- |
| **E11: WhatsApp por organización** (números propios, alta embebida, token por organización, bandeja Internos) | Sin número → organización no hay dónde publicar ni a quién mandar. Los Flows se crean en el WABA de la organización y su costo lo paga la organización. | F1 en adelante (requisito duro) |
| **E10: almacenamiento en la nube** | `IFileStore` hoy guarda en disco. En desarrollo alcanza con `LocalFileStore`; en producción hace falta el adaptador de nube. | F4 en producción |
| **P12 Excel** (propuesto, sin adoptar) | Exportar la planilla. | F5 |
| **PDF** | Ya existe PDFsharp-MigraDoc en el asistente: se reusa y no se suma otra biblioteca. | F6 |
| **Asistente v2** (acciones con confirmación) y **canal WhatsApp del asistente** | Ofrecer el formulario prellenado desde WhatsApp. | F7 |

### Módulo, feature y permisos

- **Módulo quitable `Forms`.** Va en `Domain\Forms\` y `Application|Infrastructure|Api\Modules\Forms\`, con `AddFormsModule()` por capa, EF por `IPersistenceModelContributor`, retiro probado una vez en F1 y `ModuleIsolationTests`.
- **Feature `forms`.** `IFeatureDefinitionContributor`, `FeatureScope.Business`; la plataforma lo habilita por organización (Plan, Manual o Trial). Con el feature apagado, las rutas responden 404.
- **Permisos de organización** (en `Permissions.cs` del núcleo, como manda acá; cada uno entra con la etapa que tiene una ruta que lo usa):
  - `forms.read`, `forms.manage` y `forms.publish` (F1);
  - `formrecords.create` y `formrecords.read` (F1);
  - `formcatalogs.read` y `formcatalogs.manage` (F2);
  - `formrecords.export` (F5);
  - `formrules.manage` y `formapprovals.decide` (F6);
  - `formsuggestions.manage` (F7).
- Cada permiso nuevo trae su migración que actualiza los roles de sistema de las organizaciones existentes (modelo: `20261010203618_SystemRoleAssistantPermissions.cs`).
- **Permiso de plataforma:** uno para mantener la biblioteca (F3), con `[HasPlatformPermission]`.
- **«Para resolver»:** las respuestas por aprobar (F6) aportan un `IPendingItemSource` (ADR 0040), en lugar de un contador propio.

### Meta, costos y límites

- **Cada organización publica en su propio WABA**, con el token de E11. Antes de publicar se verifica que el número pertenezca a ese WABA, como en ArquitecturaBase. Sin medio de pago en el WABA, Meta rechaza el envío (en ArquitecturaBase fue el error 141006).
- **Los costos de Meta los paga la organización** (E11). Los de IA (F7, incluida la transcripción de audios) entran en el tope mensual en USD por organización del asistente (ADR 0051).
- **Límites técnicos iguales para todos en la v1:** hasta 10 archivos y 100 MiB por respuesta (tope de Meta), un tope por archivo de la instalación, hasta 200 opciones por lista y hasta 10.000 respuestas por exportación. Límites por plan, después.

## 3. Etapas

Las etapas llevan número F*. Al sumarlas al plan de desarrollo reciben su número E. Cada una tiene tableros a dibujar en el lienzo del front (v119) antes de programar. Los de ArquitecturaBase sirven de referencia de contenido, no de estilo.

### F1 · Formularios: crear, publicar, mandar y recibir

- **Editor.**
  - Seis tipos: texto corto, texto largo, número con decimales (precisión y escala), fecha sin hora, sí/no y lista de opciones escritas en la pregunta.
  - Nombre, preguntas, ayudas, opciones y botón final en español e inglés. Se puede guardar incompleto, pero se publica con los dos idiomas completos.
  - **Prefijo de numeración:** se propone desde el nombre, se edita hasta la primera publicación, es único y no se recicla.
  - Vista previa en el teléfono.
  - El borrador se guarda con revisión y 409, como la configuración interna (`IVersioned`).
- **Publicar.**
  - Comprobación local antes de mandar: tipos, límites del perfil de Meta y un presupuesto de respuesta de 16 KiB.
  - Publicación en Meta como **operación durable** (efecto externo): crear, subir el `FLOW_JSON`, verificar y publicar, con estados aceptada, preparando, publicando, sin confirmar, activación pendiente, publicada y fallida.
  - Un Flow por versión y por idioma. Vista previa oficial de Meta. Solo «Sin confirmar» cuando pudo haber efecto en Meta; un error definitivo termina en fallida y visible.
- **Versiones.** Publicar fija las preguntas. Cambiar crea una versión nueva, y cada respuesta conserva la versión con la que llegó.
- **Mandar desde el chat** (bandeja Internos de E11).
  - El botón aparece si el contacto es un empleado activo, con idioma y `formrecords.create`, y dentro de las 24 horas.
  - El mensaje lleva un texto de hasta 1024 caracteres y un único botón, «Abrir formulario».
  - Se crea una sesión con un token aleatorio, válida 24 horas (configurable), y el mensaje sale por el outbox.
- **Recibir.**
  - El `nfm_reply` firmado se procesa en un worker que aísla los errores por mensaje y vence según la hora firmada por Meta.
  - El codec de la versión sellada da los valores exactos, sin redondeos. La ayuda pide punto decimal y se rechazan más decimales que la escala.
  - La respuesta numerada se guarda en un commit, de forma idempotente.
  - En el chat, una tarjeta con valores solo para quien tiene `formrecords.read`; sin ese permiso, solo el número.
  - La respuesta cruda se cifra y se purga con la retención de WhatsApp.
- **Cómo se cierra.** Puerta de etapa, más una prueba real con el número de una organización: publicar, mandar, completar en el teléfono y ver el número tras recargar.

### F2 · Catálogos a mano y con filtros

- **Listado y alta.** Se elige el origen al crear y no cambia después.
- **Escritos a mano.**
  - Cada opción tiene texto, detalle y código. El inglés es opcional y, si falta, se usa el español.
  - Una opción se da de baja sin borrarse. Las posiciones son únicas y se reordena con «mover» por id.
- **Con filtros.**
  - Condiciones armadas por el sistema sobre vistas versionadas por migración, con columnas permitidas y valores como parámetros, usando la conexión normal.
  - Primera vista: **miembros de la organización** (nombre, rol, empresa y activo), con RLS. Nunca correo ni teléfono.
  - Único parámetro de contexto: el destinatario. «Sin incluir a quien lo recibe». Vista previa con una persona de ejemplo elegida explícitamente.
- **La pregunta «Elegir de un catálogo».** La versión guarda solo la referencia. Cambiar el catálogo de una pregunta publicada crea una pregunta nueva.
- **Al mandar.**
  - Las opciones se arman para cada destinatario y viajan dentro del mensaje, sin endpoint de intercambio.
  - Más de 200 opciones, o ninguna en una pregunta obligatoria, no deja mandar.
- **Al responder.**
  - La opción tiene que estar en la oferta. Se guarda el texto que vio la persona.
  - Si una opción escrita a mano se dio de baja antes de responder, no se guarda nada, se avisa al operador sin valores y puede reenviar.
- **Fuera.** SQL configurable (catálogo con consulta): no se ofrece.

### F3 · Biblioteca de plantillas de la plataforma

- **Plataforma.**
  - Arma plantillas: formulario (preguntas es/en, botón y prefijo sugerido), sus catálogos escritos a mano y, más adelante, reglas de ejemplo.
  - Categoría, descripción es/en, versiones y publicada o retirada.
  - Datos globales en `platform`, con auditoría (ADR 0050).
- **Organización.**
  - Con el feature `forms` ve la biblioteca y elige «Usar esta plantilla».
  - **Se copia:** se crea un formulario en borrador y sus catálogos como datos propios (`ITenantOwned`), con identidades nuevas y prefijo propio. Desde ahí son independientes.
  - Nunca se publica sola: la organización revisa y publica.
- **Privacidad.** La plataforma solo ve cuántas organizaciones usaron cada plantilla, nunca qué respondieron.
- **Fuera.** Avisar «hay una versión nueva de la plantilla» y actualizar una copia: después, si se pide.

### F4 · Fotos y documentos

- **Preguntas nuevas.**
  - «Foto»: cámara o galería, o solo cámara (evidencia en el momento).
  - «Documento»: PDF, Excel, Word e imágenes. CSV no, porque Meta no lo admite.
  - Mínimo y máximo por pregunta. Una pregunta de archivo por pantalla del Flow.
- **Al responder.**
  - El worker baja cada archivo con el token de la organización y verifica hash, tamaño y tipo real.
  - Lo guarda con `IFileStore` y metadatos en `tenant`, y arma una miniatura liviana.
  - Completa o nada: si un archivo falla, la respuesta no se guarda y el operador puede reenviar.
  - Bajar y subir con clave determinística: alcanza con un reintento simple.
- **Ver.** Visor y descarga solo por la Api, con `formrecords.read`, `no-store` y `nosniff`. Los documentos siempre se descargan.
- **Antes de producción.** El adaptador de nube (E10) y un antivirus.

### F5 · Planilla, detalle y Excel

- **Navegación y planilla.**
  - «Respuestas» en el panel, con un enlace por formulario visible.
  - Columnas fijas: número, recibida, quién cargó y canal. Una columna por pregunta de todas las versiones.
  - «—» si la pregunta no existía en esa versión; «Sin respuesta» si quedó vacía. Ausencia, cero y false se distinguen.
- **Filtros por tipo.** Usan la gramática de condiciones de F2 y conteos con la misma definición. Los textos libres van en el cuerpo, nunca en la URL.
- **Detalle.** Valores con las preguntas de su versión, origen e historial, y galería de archivos.
- **Excel.**
  - `.xlsx` con los filtros aplicados y permiso `formrecords.export`, hasta 10.000 respuestas por vez y sin trabajos en segundo plano.
  - De los archivos, cantidad y nombres. Encabezados en el idioma de quien exporta.
  - La biblioteca se adopta según P12.
- **Robustez.** Todo es lectura: sin recibos ni pruebas de varios hosts.

### F6 · Reglas, aprobación, PDF y avisos

- **Reglas «Cuando llega una respuesta».**
  - Guardado e historial propios, sin versión nueva ni republicar.
  - Cada regla: cuándo (se recibe, se aprueba o se rechaza), si (condición opcional) y hacer (acciones en orden).
- **Un solo motor de acciones.**
  - Ejecuciones creadas en el commit de la captura o de la decisión, con clave idempotente.
  - El efecto se hace fuera de la transacción y un timeout queda «Sin confirmar».
  - Se ve en «Qué pasó después».
- **Aprobación de un nivel.**
  - «Pedir aprobación» es la última acción de su regla: nunca hay PDF de algo sin aprobar.
  - Nadie aprueba lo que cargó. La respuesta conserva la revisión de reglas con la que llegó.
  - Pendientes en «Para resolver».
- **PDF.**
  - Diseño simple con revisiones propias, en el idioma de quien lo recibe, como copia fija.
  - Opción de incluir las fotos. Se guarda con `IFileStore`.
- **Avisos por WhatsApp.**
  - Salen por el outbox con el número de la organización. Dentro de 24 horas, como texto o documento.
  - **Fuera de las 24 horas, con plantillas de Meta aprobadas**, creadas al empezar la etapa, como manda este repo (en ArquitecturaBase quedaba fuera).

### F7 · Asistente en WhatsApp, sugerencias y audios

- **Requisitos.** El asistente v2 (acciones con confirmación) y el canal WhatsApp del asistente. Las herramientas siguen la receta de este repo ([agregar una herramienta](../guides/agregar-una-herramienta-al-asistente.md)): el modelo lee o propone, y ejecuta el servicio del módulo dueño.
- **Modo Asistente por conversación.**
  - Reconoce el pedido y manda el formulario prellenado. En atención manual solo le sugiere al operador.
  - Solo formularios con «Cuándo lo ofrece el asistente» completado.
  - Hasta 2 ofrecimientos por día y por conversación.
  - Lo que no es un formulario no se contesta: queda sin leer para un operador.
- **Sugerencias entre formularios.** Una entrega puede sugerir una factura, por ejemplo. Modos Assistant (decide la IA) o Always (por condición, sin IA), con mapeo de preguntas y la cadena de origen en el detalle.
- **Audios.** Se bajan como los archivos de F4, se transcriben con un proveedor elegido y la transcripción la ve también el operador. El costo entra en el tope de IA de la organización.
- **Formularios que faltan.** Si la persona quiere registrar algo y no hay formulario, el asistente lo avisa y registra el pedido. Quien diseña formularios los ve agrupados.
- **Catálogo propuesto por el asistente.** Solo con filtros; lo aprueba una persona.
- **Casos de evaluación.** Mensajes de ejemplo con el resultado esperado, que se corren antes de habilitar un modelo o instrucciones nuevas.

### Después de F7

- **B2C, formularios para clientes:** la persona responde como cliente de una organización desde la bandeja Clientes de E11. Respuesta compartida con copia para «Mis mensajes» (`IConsumerInteractionSource`).
- Intercambio cifrado con Meta: búsqueda en vivo, listas dependientes y listas largas.
- Grupos y remitos con líneas.
- Corregir una respuesta con una respuesta sucesora.
- Cargar respuestas desde la web e importar desde Excel.
- Borrador de formulario armado por la IA.
- Copiar y eliminar formularios, y deprecar Flows viejos.
- Límites por plan.

## 4. Lo que aprendimos en ArquitecturaBase

Aplicarlo desde el primer día evita lo que nos frenó la Etapa 1 durante días:

1. **Robustez proporcional al efecto.** Recuperación durable, «Sin confirmar» y pruebas de varios hosts solo para Meta, archivos, IA y mensajes a personas. Lo interno se guarda con revisión y 409.
2. **El contrato es el código:** DTO, OpenAPI y ejemplos exportados, no un documento de contrato de 60 KB.
3. **Planes cortos.** Un resumen por etapa con las decisiones ya tomadas, que no se vuelven a preguntar. Lo que el plan no fija se resuelve con lo más simple que cumpla las reglas. Un contrato todavía no publicado se cambia sin capas de compatibilidad.
4. **Tests rápidos por corte;** integración y visuales una vez, al terminar.
5. **Pantalla nueva: primero el tablero**, elegido por el usuario.
6. **Errores que ya pagamos:**
   - una respuesta con error frenaba la cola entera, y ahora se aísla por mensaje;
   - las respuestas pasados 30 minutos se perdían, y ahora la sesión dura 24 horas y vence según la hora de Meta;
   - las respuestas grandes se descartaban sin rastro, y ahora hay un presupuesto de 16 KiB al publicar y un rechazo visible;
   - errores definitivos de Meta quedaban «Sin confirmar» para siempre;
   - en la Argentina, «125.500» puede leerse como 125,5, por eso la ayuda pide punto decimal y se valida la escala.

## 5. Decisiones de producto

Decididas por el usuario el 2026-10-12. No se vuelven a preguntar.

1. **Formularios por organización.** En la v1 son de la organización; la respuesta guarda la empresa de quien respondió y la planilla filtra por empresa. Formularios y permisos por empresa (`ICompanyOwned`), después.
2. **Las plantillas se toman como copia independiente.** Una referencia obliga a resolver qué pasa con las respuestas cuando la plataforma cambia la plantilla.
3. **B2C, después de F7,** con la bandeja Clientes de E11 andando y el backlog #51 resuelto.
4. **El número de la plataforma (E8) no manda formularios:** es para ingreso y avisos de cuenta.
5. **Sin límites por plan en la v1** (formularios, respuestas, almacenamiento): solo se habilita el feature. Medir el uso real antes de poner topes.

## 6. Qué copiar de ArquitecturaBase

Repo `C:\Users\ezequ\source\repos\ArquitecturaBase` y su front `ArquitecturaBaseFront`:

- **Resúmenes por etapa** con alcance y decisiones: `docs/plans/formularios/` (README, etapa 2, 2B, 3, 4 y 5).
- **Reglas implementadas de la Etapa 1:** `docs/features/formularios.md` (borrador, publicación, numeración, captura) y `docs/features/whatsapp.md` (perfil de Flows, mensaje interactivo, recepción, presupuesto de 16 KiB y fuentes de Meta).
- **ADR:** 0018 (robustez proporcional), 0019 (sin SQL configurable en catálogos) y 0020 (motor de IA con capacidades: acá se traduce a herramientas del asistente).
- **Código:**
  - backend: `src/*/Modules/BusinessForms/` (borrador, versiones, publicación durable, captura y numeración) y `src/*/Modules/WhatsApp/`, incluidos `WhatsAppFlowCompiler`, el codec de valores, la publicación y el despacho de Flows, el envío y la recepción de formularios;
  - front: `src/features/business-forms/` y las partes de formularios de `src/features/whatsapp/`.
- **Tableros:**
  - página «Formularios por WhatsApp» del lienzo de ArquitecturaBase (<https://claude.ai/artifact/HPbmDPLnr8JZ9TxevtTqJJ>), con un recorrido guiado con Play;
  - las capturas están en `ArquitecturaBaseFront/docs/design/evidence/formularios-whatsapp/`;
  - se redibujan con el tema de este proyecto en su lienzo.

Al copiar, se adapta a este repo: permisos en el catálogo del núcleo, `Domain\Forms\` en vez de `Domain/Modules/`, tres clases de datos con RLS, `[Access]`, `[FeatureGate]`, `[Idempotent]`, `OperationLog.RunAsync`, `IVersioned` y la lista cerrada de sufijos de helpers.
