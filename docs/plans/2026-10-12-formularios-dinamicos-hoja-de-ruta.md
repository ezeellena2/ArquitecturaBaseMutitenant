# Formularios dinámicos por WhatsApp: hoja de ruta

Estado: **aprobada el 2026-10-12, con las decisiones de producto tomadas** ([sección 5](#5-decisiones-de-producto)). Está sumada al [plan de desarrollo](2026-09-27-plan-de-desarrollo.md) como **etapas 12 a 17, después de la E10**. No es un plan de etapa: cada etapa se escribe en el formato de [etapa en paralelo](../guides/etapa-en-paralelo.md) cuando le toque, a partir de su sección de acá.

**Origen.** Es la misma funcionalidad que se está construyendo en `ArquitecturaBase`, adaptada a organizaciones, empresas, plataforma y personas. Allá la Etapa 1 está cerrada y probada con Meta real (envío, respuesta y número guardado), y las demás tienen diseño y decisiones elegidas. Las referencias para copiar código y tableros están en la [sección 6](#6-qué-copiar-de-arquitecturabase).

## 1. Qué es el producto

- **Formularios propios.** Una organización arma sus formularios: preguntas tipadas, textos en español e inglés, versiones y un prefijo de numeración propio. No hay circuitos rígidos: cada formulario es independiente.
- **Por WhatsApp.**
  - Se publican como Flows de Meta en el número de la organización.
  - Se mandan desde el chat, y el empleado los completa dentro de WhatsApp.
  - La respuesta queda numerada (`VIS-000123`) y se ve en el chat.
- **Catálogos.** Listas compartidas entre formularios (clientes, supervisores, equipos), escritas a mano o tomadas de los datos del sistema con filtros.
- **Fotos y documentos dentro del formulario.** Con cámara o galería: PDF, Excel, Word e imágenes.
- **Planilla.** Cada formulario es una planilla con filtros, detalle y exportación a Excel.
- **Reglas.** Al llegar una respuesta se puede pedir aprobación, armar un PDF y avisar por WhatsApp.
- **Plataforma (nuevo respecto de ArquitecturaBase).** Una biblioteca de plantillas (formularios, catálogos y reglas de ejemplo) que la organización puede tomar como copia propia, o crear desde cero.
- **Después del bloque:** el asistente que reconoce lo que la persona cuenta (texto o audio) y ofrece el formulario prellenado, y las sugerencias entre formularios ([Después](#después-del-bloque)).

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

- **Privado de la organización (`ITenantOwned`, esquema `tenant`, RLS):** definiciones, versiones, catálogos e ítems, respuestas y valores, recibos, reglas, ejecuciones, tareas, documentos y metadatos de archivos.
- **Global de plataforma (esquema `platform`, ADR 0050):** la biblioteca de plantillas y sus versiones.
- **Compartido persona↔organización (`IConsumerBusinessShared`):** solo cuando llegue B2C. Requiere resolver antes el backlog #51: `Enter` borra `TenantKind`, así que hoy un worker no puede crear datos compartidos.
- **Entrada de WhatsApp:** el webhook sigue global (`identity`). El worker resuelve la organización por el número de E11 (la tabla de números la define esa etapa) y captura dentro de `ITenantScope.Enter(tenantId)`, con `IFeatureService` antes de actuar.
- Un recurso de otra organización responde 404. Cada ruta nueva lleva su test de aislamiento.

### Dependencias

| Dependencia | Para qué | Etapa que la necesita |
| --- | --- | --- |
| **E11: WhatsApp por organización** (números propios, alta embebida, token por organización, bandeja Internos) | Sin número de la organización no hay dónde publicar ni a quién mandar. Los Flows se crean en el WABA de la organización y su costo lo paga la organización. | E12 en adelante (requisito duro) |
| **E9: endurecimiento** | Redis, límites por organización y el fuzz de aislamiento sobre todas las rutas. El módulo nace con esas guardas y el fuzz cubre sus rutas. | E12 en adelante |
| **E10: almacenamiento en la nube** | El adaptador de nube de `IFileStore` (ADR 0048, D15). En desarrollo alcanza con `LocalFileStore`. | E14 en producción |
| **P12 Excel** (propuesto, sin adoptar) | Exportar la planilla. Se adopta al planificar la E15. | E15 |
| **PDF** | Ya existe PDFsharp-MigraDoc en el asistente: se reusa y no se suma otra biblioteca. | E16 |

### Módulo `Forms` y canal WhatsApp

- **`Forms` es un módulo quitable aparte, no una parte de WhatsApp** ([decisión 7](#5-decisiones-de-producto)).
  - Es una herramienta general, como WhatsApp y el Asistente, y no un «módulo de negocio» en el sentido de [AGENTS.md](../../AGENTS.md) y la ADR 0032.
  - Al empezar la E12 se suma a la tabla «Dónde va cada cosa» de AGENTS.md.
- **Qué es de cada uno:**
  - **`Forms`:** definiciones, borrador, versiones, numeración, catálogos, respuestas, archivos, planilla, reglas, aprobaciones y PDF.
  - **`WhatsApp`:** armar el `FLOW_JSON`, publicarlo en Meta como operación durable, las sesiones, el mensaje interactivo y la recepción del `nfm_reply`.
- **Ninguno referencia al otro.** `ModuleIsolationTests` ya lo rompe. Hablan por puertos neutrales del núcleo, copiados de ArquitecturaBase, en `Application/Interfaces/Integrations/StructuredData/`, con sus modelos en `Application/Models/StructuredData/`:
  - `IStructuredFormSource`: WhatsApp lee los formularios publicados que puede mandar;
  - `IStructuredFormChannel`: Forms pide publicar y consulta el estado;
  - `IStructuredRecordSink`: WhatsApp entrega la respuesta capturada;
  - `IStructuredRecordSource`: WhatsApp muestra la respuesta en el chat.
- **Implementaciones apagadas.** El núcleo trae las implementaciones apagadas de esos puertos:
  - sin WhatsApp, los formularios se diseñan pero no se mandan;
  - sin Forms, WhatsApp anda como siempre.
- **Ids y transacciones.**
  - Cada lado guarda los ids del otro como opacos, sin claves foráneas.
  - Cada módulo abre su propia transacción y nunca llama al otro adentro de ella.
- **Registro y EF.**
  - Va en `Domain\Forms\` y `Application|Infrastructure|Api\Modules\Forms\`, con `AddFormsModule()` por capa y EF por `IPersistenceModelContributor`.
  - El retiro se prueba una vez, en la E12, con la base vacía.
- **Feature `forms`.**
  - `IFeatureDefinitionContributor`, `FeatureScope.Business`. La plataforma lo habilita por organización (Plan, Manual o Trial).
  - Con el feature apagado, las rutas responden 404.
- **Permisos de organización** (en `Permissions.cs` del núcleo, como manda acá; cada uno entra con la etapa que tiene una ruta que lo usa):
  - `forms.read`, `forms.manage` y `forms.publish` (E12);
  - `formrecords.create` y `formrecords.read` (E12);
  - `formcatalogs.read` y `formcatalogs.manage` (E13);
  - `formrecords.export` (E15);
  - `formrules.manage` y `formapprovals.decide` (E16).
- Cada permiso nuevo trae su migración que actualiza los roles de sistema de las organizaciones existentes (modelo: `20261010203618_SystemRoleAssistantPermissions.cs`).
- **Permiso de plataforma:** uno para mantener la biblioteca (E17), con `[HasPlatformPermission]`.
- **«Para resolver»:** las respuestas por aprobar (E16) aportan un `IPendingItemSource` (ADR 0040), en lugar de un contador propio.

### Premisas del repo aplicadas a formularios

Valen todas las de [AGENTS.md](../../AGENTS.md) y las [fichas](../rules/README.md). Así caen en este módulo:

| Premisa | En formularios |
| --- | --- |
| [Capas y flujo](../rules/capas-y-flujo.md) | Controller → `IForm<X>Service` → servicio → repositorio, reader o puerto. Los helpers usan los sufijos de la lista cerrada ([sección 6](#6-qué-copiar-de-arquitecturabase)). |
| [Guardado](../rules/guardado.md) | Una `ExecuteInTransactionAsync` por método público que escribe. Publicar en Meta, bajar archivos, armar el PDF y avisar van fuera de la transacción, en workers u outbox. |
| [Errores](../rules/result-y-errores.md) | `Forms.<Entidad>.<Motivo>` en `Domain/Forms/<Entidad>Errors`. Toda falla es ProblemDetails con `code` y `traceId`. |
| [Aislamiento](../rules/multitenancy.md) y [permisos](../rules/permisos.md) | Todo es privado salvo la biblioteca. Las rutas llevan `[Access]`, `[HasPermission]` (o `[HasPlatformPermission]`) y `[FeatureGate]`; lo ajeno responde 404. Los workers entran con `Enter(tenantId)` sin transacción abierta. |
| [Concurrencia](../rules/concurrencia.md) | Borrador, catálogos y reglas con `IVersioned` y 409. |
| [Idempotencia](../rules/idempotencia.md) | `[Idempotent]` en los `POST` que crean, publican, mandan o deciden una aprobación. La captura usa el recibo de Meta como clave. |
| [Fechas](../rules/fechas-y-zonas.md) | «Fecha sin hora» es `DateOnly`; lo recibido, `…Utc`. Los vencimientos usan `TimeProvider` y la hora firmada por Meta. |
| [Números](../rules/numeros-y-moneda.md) | «Número con decimales» es `decimal` con precisión y escala, nunca `double`; `null` es «Sin respuesta». |
| [Textos](../rules/textos-y-traducciones.md) y [textos libres](../rules/textos-libres.md) | La interfaz sale de resources es/en. Las preguntas son contenido de la organización, en es/en. Lo que escribe la persona pasa por `TextLimits`. |
| [Formato](../architecture/backend.md#18-representación-y-formato-de-datos-unificado) | El PDF y los avisos por WhatsApp se formatean con `DisplayFormatter`, con cultura y zona explícitas. |
| [Paginado](../rules/paginado-y-busqueda.md) | Planilla, catálogos y listados paginados, 10 por defecto. Los filtros con texto libre van en el cuerpo. |
| [Logs](../rules/logs.md) | `[LoggerMessage]` y `OperationLog.RunAsync`. Nunca el token de sesión, los valores de una respuesta ni teléfonos completos. |
| [Datos personales](../rules/datos-personales.md) | Las respuestas guardan quién respondió: el módulo registra su `IAccountDeletionParticipant`. Qué se conserva lo fija el plan de la E12. |
| [WhatsApp](../rules/modulos.md) | Todo envío sale por el outbox. Fuera de las 24 horas, con plantillas de Meta aprobadas, creadas al empezar la etapa que las usa. |
| Datos de ejemplo | No hay: la biblioteca nace vacía y la llenan los operadores. |

### Meta, costos y límites

- **Cada organización publica en su propio WABA**, con el token de E11. Antes de publicar se verifica que el número pertenezca a ese WABA, como en ArquitecturaBase. Sin medio de pago en el WABA, Meta rechaza el envío (en ArquitecturaBase fue el error 141006).
- **Los costos de Meta los paga la organización** (E11).
- **Límites técnicos iguales para todos en la v1:** hasta 10 archivos y 100 MiB por respuesta (tope de Meta), un tope por archivo de la instalación, hasta 200 opciones por lista y hasta 10.000 respuestas por exportación. Límites por plan, después.

## 3. Etapas

Cada etapa lleva su número del plan de desarrollo, de la E12 a la E17. Antes se llamaban F1, F2, F4, F5, F6 y F3; el asistente (F7) pasó a [Después](#después-del-bloque). Entre paréntesis va la etapa de ArquitecturaBase de donde se copia.

Cada una tiene tableros a dibujar en el lienzo vigente del front antes de programar. Los de ArquitecturaBase sirven de referencia de contenido, no de estilo.

### E12 · Formularios: crear, publicar, mandar y recibir (AB etapa 1)

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
- **Módulo.** Los puertos neutrales del núcleo y sus implementaciones apagadas, `AddFormsModule()` y el retiro probado una vez.
- **Cómo se cierra.** Puerta de etapa, más una prueba real con el número de una organización: publicar, mandar, completar en el teléfono y ver el número tras recargar.

### E13 · Catálogos a mano y con filtros (AB etapa 2)

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

### E14 · Fotos y documentos (AB etapa 2B)

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
- **Antes de producción.** Un antivirus. El adaptador de nube ya llega con la E10.

### E15 · Planilla, detalle y Excel (AB etapa 3)

- **Navegación y planilla.**
  - «Respuestas» en el panel, con un enlace por formulario visible.
  - Columnas fijas: número, recibida, quién cargó y canal. Una columna por pregunta de todas las versiones.
  - «—» si la pregunta no existía en esa versión; «Sin respuesta» si quedó vacía. Ausencia, cero y false se distinguen.
- **Filtros por tipo.** Usan la gramática de condiciones de la E13 y conteos con la misma definición. Los textos libres van en el cuerpo, nunca en la URL.
- **Detalle.** Valores con las preguntas de su versión, origen e historial, y galería de archivos.
- **Excel.**
  - `.xlsx` con los filtros aplicados y permiso `formrecords.export`, hasta 10.000 respuestas por vez y sin trabajos en segundo plano.
  - De los archivos, cantidad y nombres. Encabezados en el idioma de quien exporta.
  - La biblioteca se adopta según P12.
- **Robustez.** Todo es lectura: sin recibos ni pruebas de varios hosts.

### E16 · Reglas, aprobación, PDF y avisos (AB etapa 4)

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

### E17 · Biblioteca de plantillas de la plataforma (nueva)

Va al final del bloque para que las plantillas puedan traer reglas de ejemplo.

- **Plataforma.**
  - Arma plantillas: formulario (preguntas es/en, botón y prefijo sugerido), sus catálogos escritos a mano y sus reglas de ejemplo.
  - Categoría, descripción es/en, versiones y publicada o retirada.
  - Datos globales en `platform`, con auditoría (ADR 0050).
- **Organización.**
  - Con el feature `forms` ve la biblioteca y elige «Usar esta plantilla».
  - **Se copia:** se crea un formulario en borrador, con sus catálogos y reglas como datos propios (`ITenantOwned`), con identidades nuevas y prefijo propio. Desde ahí son independientes.
  - Nunca se publica sola: la organización revisa y publica.
- **Privacidad.** La plataforma solo ve cuántas organizaciones usaron cada plantilla, nunca qué respondieron.
- **Fuera.** Avisar «hay una versión nueva de la plantilla» y actualizar una copia: después, si se pide.

### Después del bloque

**Asistente en WhatsApp, sugerencias y audios** (antes F7; AB etapa 5).
- **Por qué quedó afuera:** requiere dos cosas que no están planificadas:
  - el asistente v2 con acciones con confirmación (ADR 0048, D8);
  - la IA en WhatsApp, que la ADR 0038 deja para después.
- **Cómo se haría:** las herramientas siguen la receta de este repo ([agregar una herramienta](../guides/agregar-una-herramienta-al-asistente.md)); el modelo lee o propone, y ejecuta el servicio del módulo dueño. Los costos de IA, incluida la transcripción de audios, entran en el tope mensual en USD por organización del asistente (ADR 0051).
- **Modo Asistente por conversación.**
  - Reconoce el pedido y manda el formulario prellenado. En atención manual solo le sugiere al operador.
  - Solo formularios con «Cuándo lo ofrece el asistente» completado.
  - Hasta 2 ofrecimientos por día y por conversación.
  - Lo que no es un formulario no se contesta: queda sin leer para un operador.
- **Sugerencias entre formularios.** Una entrega puede sugerir una factura, por ejemplo. Modos Assistant (decide la IA) o Always (por condición, sin IA), con mapeo de preguntas y la cadena de origen en el detalle.
- **Audios.** Se bajan como los archivos de la E14, se transcriben con un proveedor elegido y la transcripción la ve también el operador.
- **Formularios que faltan.** Si la persona quiere registrar algo y no hay formulario, el asistente lo avisa y registra el pedido. Quien diseña formularios los ve agrupados.
- **Catálogo propuesto por el asistente.** Solo con filtros; lo aprueba una persona.
- **Casos de evaluación.** Mensajes de ejemplo con el resultado esperado, que se corren antes de habilitar un modelo o instrucciones nuevas.

**Lo demás que queda para después:**
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
3. **B2C, después del bloque,** con la bandeja Clientes de E11 andando y el backlog #51 resuelto.
4. **El número de la plataforma (E8) no manda formularios:** es para ingreso y avisos de cuenta.
5. **Sin límites por plan en la v1** (formularios, respuestas, almacenamiento): solo se habilita el feature. Medir el uso real antes de poner topes.
6. **El bloque va después de la E10:** 8b → 11 → 9 → 10 → 12 a 17. Nace con Redis, límites y el fuzz de la E9, y con la nube de la E10.
7. **`Forms` es un módulo aparte de `WhatsApp`,** unidos por puertos neutrales del núcleo, como en ArquitecturaBase.
8. **Alcance:** seis etapas, con la biblioteca al final. El asistente en WhatsApp (antes F7) queda para después.

## 6. Qué copiar de ArquitecturaBase

Repo `C:\Users\ezequ\source\repos\ArquitecturaBase` y su front `ArquitecturaBaseFront`. **El código está en `origin/main`:** antes de copiar, traé los cambios, porque la copia local puede estar atrasada. Se sigue desarrollando allá, así que cada etapa copia lo que haya al planificarse.

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

**Al copiar, se adapta a este repo:**
- `BusinessForms` pasa a `Forms`, en `Domain\Forms\` en vez de `Domain/Modules/`;
- los puertos van en `Application/Interfaces/Integrations/StructuredData/`, no en `Interfaces/Channels/`;
- permisos en el catálogo del núcleo, tres clases de datos con RLS, `[Access]`, `[FeatureGate]`, `[Idempotent]`, `OperationLog.RunAsync` e `IVersioned`;
- la lista cerrada de sufijos de helpers: `Compiler` y `Codec` no están. Se renombran a uno de la lista (por ejemplo, `WhatsAppFlowBuilder`), o se suma el sufijo a la ficha con su test.
