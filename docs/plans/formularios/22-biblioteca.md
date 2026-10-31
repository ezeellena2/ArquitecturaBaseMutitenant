# E22 · Biblioteca de plantillas de la plataforma (spec)

**Estado:** pendiente · spec de la etapa 22 del bloque (decisiones 34, 40, 78 y 79). **Es también la biblioteca de formularios estándar** (40). Se arma con las piezas de este repo y las de las etapas anteriores. `/plan-etapa` la convierte en plan cuando le toque, después de verificar los puntos marcados **[E14]**, **[E12]** y **[E21]** contra el código.

**Fuentes:**
- alcance y decisiones: [hoja de ruta](../2026-10-12-formularios-dinamicos-hoja-de-ruta.md), §E22 y decisiones 2, 13 a 16, 30 a 33, 35, 40, 45, 50, 59, 61, 62 y 64;
- reglas: [AGENTS.md](../../../AGENTS.md), las [fichas](../../rules/README.md) y la [ADR 0050](../../decisions/0050-auditoria-global-de-modulos.md) (auditoría de lo global);
- las specs de la [E14](14-formularios.md) y la [E12](12-catalogos.md).

## Qué leer

Antes de empezar:
1. **Lo anterior:** las specs de la [E12](12-catalogos.md), la [E14](14-formularios.md) y la [E21](21-reglas-y-aprobacion.md), sobre todo el borrador, los catálogos, las reglas y el diseño del PDF que se copian.
2. **Decisiones** de la hoja de ruta: 2, 13 a 16, 30 a 33, 35, 40, 45, 50, 59, 61, 62 y 64.
3. **Fichas:** [persistencia-ef](../../rules/persistencia-ef.md), [multitenancy](../../rules/multitenancy.md), [auditoria](../../rules/auditoria.md), [guardado](../../rules/guardado.md), [concurrencia](../../rules/concurrencia.md), [idempotencia](../../rules/idempotencia.md), [api-http](../../rules/api-http.md), [permisos](../../rules/permisos.md), [paginado-y-busqueda](../../rules/paginado-y-busqueda.md), [result-y-errores](../../rules/result-y-errores.md), [validacion](../../rules/validacion.md), [textos-libres](../../rules/textos-libres.md), [textos-y-traducciones](../../rules/textos-y-traducciones.md), [logs](../../rules/logs.md), [modulos](../../rules/modulos.md) y [tests](../../rules/tests.md). Además, la [ADR 0050](../../decisions/0050-auditoria-global-de-modulos.md).
4. **Código para copiar:** pieza por pieza, en la §7.
5. **Pantallas:** los tableros de la §8.

## 1. Alcance

**Entra:**
- **Plataforma:**
  - **Plantillas:** cada una trae un formulario (preguntas, botón y prefijo sugerido), sus catálogos (a mano, con su tipo de valor y sus campos; y de los miembros o de los contactos, sin filtros, 64), sus reglas de ejemplo y el diseño del PDF (ver [«Reglas de ejemplo»](#reglas-de-ejemplo-y-diseño-del-pdf)). Tiene categoría y descripción. Cada texto es uno solo, sin idioma (35).
  - **Edición:** borrador y publicar, con versiones. Publicar y retirar piden motivo.
  - **Categorías:** las arma la plataforma.
  - **Uso:** solo la cantidad de organizaciones que usaron cada plantilla.
- **Organización:**
  - con la feature `forms`, ve la biblioteca y una vista de cada plantilla;
  - con «Usar esta plantilla», obtiene una **copia independiente**: un formulario en borrador con sus catálogos nuevos, el diseño del PDF y su prefijo, y las reglas de ejemplo precargadas hasta que guarde las suyas. Nunca se publica sola.
- **Permiso de plataforma:** `platform.formtemplates.manage`.

**No entra:**
- avisar «hay una versión nueva» o actualizar una copia: queda para después, si se pide;
- filtros en los catálogos de una plantilla: los de los miembros y de los contactos llegan sin filtros, y la organización los ajusta (64). Tampoco los filtros por destinatario de la E16, que dependen de los datos de las personas de cada organización;
- plantillas de una organización para otra;
- la «Lectura de documentos» de una plantilla, que llega con los documentos (E25);
- saber qué organizaciones usaron una plantilla (decisión 16);
- borrar plantillas: se retiran.

## 2. Decisiones

**De producto, tomadas por el usuario el 2026-10-13** (decisiones 13 a 16 de la hoja de ruta):
1. **Edición:** borrador y publicar. Los cambios quedan en un borrador, y «Publicar» crea la versión nueva que ven las organizaciones. Publicar y retirar piden motivo, como el resto de Plataforma.
2. **Catálogos:** a mano, y de los miembros o de los contactos sin filtros (64, que cambia la 14). Los filtros dependen de los roles, las empresas y las etiquetas de cada organización: los pone la organización después de usar la plantilla.
3. **Categorías:** las arma la plataforma, en una lista corta con nombre y orden que se edita en la misma biblioteca. Ninguna lista fija en el código. El nombre es un solo texto, como el resto de la biblioteca (decisión 45, que reemplaza el «es/en» de la 15).
4. **Uso:** la plataforma ve solo la cantidad («La usaron 12 organizaciones»), nunca cuáles.

**Técnicas, o de producto menor**, con su porqué:

| # | Decisión | Por qué |
| --- | --- | --- |
| B1 | La biblioteca la ve quien tiene `forms.manage`. Usar una plantilla que trae catálogos pide además `formcatalogs.manage`. | Usarla crea un formulario y catálogos: son los mismos permisos que crearlos a mano. |
| B2 | **Sin borrar plantillas:** se retiran. Una retirada se puede volver a publicar, y si el borrador no cambió, no se crea una versión nueva. | Las copias guardan de qué plantilla y versión salieron. |
| B3 | Cada uso crea **catálogos nuevos**, con los mismos nombres. No se reusa un catálogo de un uso anterior. | Las copias son independientes; reusar un catálogo exige rastrear de dónde salió. |
| B4 | Si el prefijo sugerido está tomado en la organización, se propone otro con la regla de la E14. | Una sola regla de prefijos. |
| B5 | **El conteo de uso:** una fila técnica `platform.FormTemplateUsages(TemplateId, UsedByCount)` suma 1 la primera vez que una organización usa la plantilla. Para saber si es la primera, se mira si esa organización ya tiene un formulario con ese `SourceTemplateId`, en sus propios datos. | Cuenta organizaciones sin guardar cuáles fueron. La plataforma nunca lee datos de una organización. |
| B6 | Las plantillas, sus versiones y las categorías son globales, en `platform`, con `IGlobalSecurityAudited` y `PlatformSettingsChanged` con destino global (ADR 0050). Guardar el borrador registra un motivo fijo; publicar, retirar y cambiar categorías usan el motivo que escribe el operador. | La regla vigente de auditoría de lo global. |
| B7 | **El editor de la plataforma es el mismo de la E14,** montado en la ruta de Plataforma, y suma la pestaña «Catálogos». Sin Meta: la vista previa es solo la orientativa del teléfono. | Un solo editor. Una plantilla no se publica en WhatsApp. |
| B8 | **Publicar comprueba:** las reglas de Forms, los textos obligatorios, los catálogos de la plantilla, las reglas de ejemplo y `IStructuredFormChannel.Check` (es puro, no llama a Meta). Si WhatsApp no está instalado, la comprobación del canal no corre. | La copia nace lista para publicar. |
| B9 | La copia guarda `SourceTemplateId` y `SourceTemplateVersion` en la definición. | Sirve para el conteo (B5), para leer las reglas de ejemplo (decisión 30) y para el futuro aviso de versión nueva. |
| B10 | La plataforma no tiene formularios propios ni manda formularios. | Decisión 4 de la hoja de ruta: el número de la plataforma no manda formularios. |
| B11 | **La copia conserva los `FieldId` y `ChoiceId` de la versión de la plantilla.** Los catálogos y sus ítems sí son nuevos (B3). | Son únicos dentro de cada formulario, y nada los cruza entre formularios: las respuestas, la planilla y la publicación se unen siempre por formulario. Así las reglas de ejemplo y el diseño del PDF apuntan a las mismas preguntas sin guardar una tabla de equivalencias. |
| B12 | **Copiar el diseño del PDF no pide `formrules.manage`.** Verlo y cambiarlo sí lo piden. | Es parte del formulario nuevo y no hace nada hasta que alguien con ese permiso guarda reglas que lo usan. Si se pidiera el permiso, «Usar esta plantilla» se apagaría para quien diseña formularios sin armar reglas. |

## 3. Modelo de datos

**En `platform`** (globales). Cada entidad se declara en `FormsModelContributor` y lleva `IGlobalSecurityAudited`, salvo la fila técnica de uso. El modelo es `Domain/Assistant/AssistantPlatformSettings.cs`.

**FormTemplateCategories** (`IVersioned`)

| Columna | Tipo | Regla |
| --- | --- | --- |
| `Id` | uuid | — |
| `Name` | texto ≤40 | obligatorio, un solo texto (45) |
| `Position` | int | única, `DEFERRABLE INITIALLY DEFERRED` (como E12 C4) |

Una categoría se borra solo si ninguna plantilla la usa (`Forms.TemplateCategory.InUse`).

**FormTemplates** (`IVersioned`)

| Columna | Tipo | Regla |
| --- | --- | --- |
| `Id` | uuid | — |
| `CategoryId` | uuid | FK |
| `State` | texto | `Draft` (nunca publicada), `Published` o `Retired` |
| `Draft` | jsonb | `FormTemplateDraft` (abajo) |
| `PublishedVersionId` | uuid? | la versión que ven las organizaciones |
| `HasUnpublishedChanges` | bool | el borrador guardado difiere de la versión publicada; habilita «Publicar» y no se muestra |
| auditoría | — | `IGlobalSecurityAudited` |

**`FormTemplateDraft`** (jsonb, `format: 1`):
- `name`, `description` (≤300), `suggestedPrefix` y `submitLabel`, cada uno un solo texto (35), y `audience`: para quién es el formulario (50), que la copia conserva;
- `questions[]`, en el formato del borrador de la E14;
- en una pregunta de elegir con catálogo, `catalogRef` (id local de la plantilla) en lugar de `catalogId`;
- `catalogs[]`: `{catalogRef, name, source, valueType, fields[], items[{code, text, detail?, value?, fields{}}]}`, donde `source` es `Manual`, `Members` o `Contacts`. Solo los `Manual` llevan opciones; los otros dos llegan sin filtros (64), con los límites de la E12 y la E13. Un campo «elegir de otro catálogo» apunta a otro `catalogRef` de la misma plantilla;
- `rules[]`: las reglas de ejemplo, en el formato de la E21 (un evento, hasta 10 condiciones y hasta 10 acciones), con dos diferencias:
  - las condiciones apuntan a las preguntas y opciones de la plantilla (ids locales); en una pregunta de catálogo, a `catalogRef` más el código de la opción;
  - las acciones que piden un rol (pedir aprobación y avisar a un rol) **no llevan rol**. Llevan un `roleHint` opcional: un texto que solo se muestra, como «Supervisor»;
  - **lo que es de cada organización tampoco viaja,** y se trata como el rol: «Lo elige cada organización». Son la persona de «Avisar a una persona», la dirección fija de «Mandar por correo» (62) y la plantilla de WhatsApp para fuera de las 24 horas (61). Al usar la plantilla, el editor las pide como pide el rol;
- `pdfDesign?`: el diseño del PDF de la E21 (nombre, preguntas, quién aprobó, pie, «Incluir las fotos», secciones con título y orden, mostrar u ocultar y firmas; 59), con las preguntas por id local. El logo, el color y los datos del encabezado son de cada organización (82): la plantilla no los trae;

### Reglas de ejemplo y diseño del PDF

Se completó el 2026-10-18 con el [resumen](21-reglas-y-aprobacion.md) y los tableros de la E21 (v132). La regla: las reglas viajan en la plantilla sin destinatarios propios de una organización, y la copia las deja para completar.
- **Al publicar la plantilla:** las reglas se validan con las reglas de la E21 (pedir aprobación es la última acción y solo al recibir, el PDF solo al aprobar, y adjuntarlo pide una acción anterior que lo genere), salvo el rol. Lo mismo vale para el diseño del PDF.
- **Al usar la plantilla** (decisiones 30 a 33 de la [hoja de ruta](../2026-10-12-formularios-dinamicos-hoja-de-ruta.md#5-decisiones-de-producto), del 2026-10-18):
  - **las reglas no se activan solas ni se copian.** No hay columna ni tabla de reglas sugeridas:
    - mientras el formulario no tenga ninguna revisión de reglas, la lectura de reglas de la E21 devuelve además las reglas de ejemplo de la versión de la que salió (B9). Se leen de `FormTemplateVersions.Snapshot`, que es inmutable y no se borra (B2), así que retirar la plantilla no las cambia;
    - el editor las muestra precargadas en «Cuando llega una respuesta», sin guardar, con «Elegí el rol» en cada acción que lo pide;
    - las preguntas y opciones coinciden porque la copia conserva sus ids (B11). Un ítem de catálogo se busca por el catálogo de la pregunta y su código;
    - lo que apunta a una pregunta u opción que ya no está en el borrador se precarga marcado, y la persona lo corrige o lo borra antes de guardar;
  - cuando la persona elige los roles y toca «Guardar reglas», se crea la revisión 1, con la regla de guardado de la E21. Desde la primera revisión, aunque esté vacía, las reglas de ejemplo dejan de mostrarse. Hasta entonces, el formulario se puede publicar sin reglas;
  - el diseño del PDF se copia como primera revisión del diseño, porque no tiene datos de la organización (decisión 31);
  - el `roleHint` se muestra como «Rol sugerido», sin elegir ningún rol solo: las referencias van por id, nunca por nombre (decisión 32 y lección del 06 del traspaso).
- **En Plataforma,** las reglas y el diseño del PDF se editan en la pestaña «Plantilla», debajo de las preguntas, con la misma sección de la E21 (decisión 33).

**FormTemplateVersions** (inmutable)

| Columna | Tipo | Regla |
| --- | --- | --- |
| `Id`, `TemplateId` | uuid | — |
| `Number` | int | único por plantilla |
| `Snapshot` | jsonb | copia sellada del borrador |
| `ContentHash` | texto 64 | — |
| `PublishedAtUtc`, `PublishedBy`, `Reason` | — | — |

**FormTemplateUsages** (técnica, sin auditoría): `TemplateId` (PK) y `UsedByCount`.

**En `tenant`** (cambios a la E14): `FormDefinitions` suma `SourceTemplateId uuid?` y `SourceTemplateVersion int?`, con índice `(TenantId, SourceTemplateId)`.

## 4. Dominio y errores

**Entidades** (`Domain/Forms`):
- **`FormTemplate`:** `Create`, `SaveDraft`, `Publish(reason)`, `Retire(reason)` y `Republish(reason)`;
- **`FormTemplateVersion.Seal`**;
- **`FormTemplateCategory`:** `Rename` y `SwapPosition`.

**Copiar** (`FormTemplateCopyCreator`, en Application) arma, desde la versión publicada:
- los catálogos nuevos: los a mano con posiciones de 1 a n, su tipo de valor, sus campos y sus valores; los de los miembros y de los contactos, sin filtros. Sin WhatsApp, uno de los contactos queda «No se puede usar» hasta que se instale;
- el borrador del formulario, con los `FieldId` y `ChoiceId` de la versión (B11) y los `catalogRef` traducidos a los ids de los catálogos nuevos;
- el nombre de la plantilla, el prefijo libre y la fuente;
- el diseño del PDF, como primera revisión del diseño (decisión 31). Las reglas de ejemplo no se copian: se leen de la versión (decisión 30).

| Código | Tipo | Cuándo |
| --- | --- | --- |
| `Forms.Template.NotFound` | NotFound | inexistente; en la organización, también si no está publicada |
| `Forms.Template.Incomplete` | Validation (campo) | falta un texto obligatorio al publicar |
| `Forms.Template.Invalid` | Validation (campo) | pregunta, catálogo, `catalogRef`, regla (`rules[i].actions[j]…`) o diseño del PDF fuera de regla |
| `Forms.Template.ChannelIssues` | Validation | el perfil del canal marcó problemas (como en la E14) |
| `Forms.Template.NothingToPublish` | Conflict | publicada y sin cambios |
| `Forms.Template.NotPublished` | Conflict | retirar una que no está publicada |
| `Forms.Template.ReasonRequired` | Validation (campo `reason`) | publicar o retirar sin motivo |
| `Forms.Template.CatalogPermissionRequired` | Forbidden | usar una plantilla con catálogos sin `formcatalogs.manage` |
| `Forms.TemplateCategory.NotFound` / `InUse` | NotFound / Conflict | — |
| `Forms.TemplateCategory.MoveOutOfRange` | Conflict | — |

## 5. Contrato borrador

**Plataforma:** todas las rutas llevan `[Access(Access.Platform)]` y `[HasPlatformPermission(PlatformPermissions.FormTemplates.Manage)]`. Las altas llevan `[Idempotent]`. El modelo es `Api/Modules/Assistant/Controllers/PlatformAssistantController.cs`.

| Método y ruta | Pide | Devuelve |
| --- | --- | --- |
| GET `/api/platform/forms/templates` | `page`, `pageSize`, `sort`, `search`, `state` (`all`, `draft`, `published`, `retired`), `categoryId?` | 200 `PagedResult<{id, name, category, state, usedByCount}>` |
| GET `/api/platform/forms/templates/filter-counts` | `search`, `categoryId?` | 200 `{all, draft, published, retired}` |
| POST `/api/platform/forms/templates` | `{name, categoryId}` | 201 detalle |
| GET `/api/platform/forms/templates/{id}` | — | 200 `{id, state, category, draft, hasUnpublishedChanges, publishedVersion?{number, publishedAtUtc}, usedByCount, version}` |
| GET `/api/platform/forms/templates/{id}/versions` | `after?`, `limit?` (`CursorRequest`) | 200 `CursorResult<{number, publishedAtUtc, publishedBy, reason}>`, por fecha e Id descendentes, sin total |
| PUT `/api/platform/forms/templates/{id}/draft` | `{draft, categoryId, version}` | 200 `VersionResponse` |
| POST `/api/platform/forms/templates/{id}/publication-checks` | `{version}` | 200 `{canPublish, issues[], nextVersionNumber}`; no escribe |
| POST `/api/platform/forms/templates/{id}/publish` | `{version, reason}` | 200 `{versionNumber, version}`. Sirve también para volver a publicar. |
| POST `/api/platform/forms/templates/{id}/retire` | `{version, reason}` | 200 `VersionResponse` |
| GET `/api/platform/forms/template-categories` | `page`, `pageSize`, `search` | 200 `PagedResult<{id, name, position, templateCount, version}>`, por posición e Id |
| POST `/api/platform/forms/template-categories` | `{name, reason}` | 201 |
| PUT `/api/platform/forms/template-categories/{id}` | `{name, version, reason}` | 200 `VersionResponse` |
| POST `/api/platform/forms/template-categories/{id}/move` | `{direction, version, reason}` | 200 `VersionResponse` |
| DELETE `/api/platform/forms/template-categories/{id}` | `?version=` y `reason` | 204; o 409 `InUse` |

**Organización:** todas las rutas llevan `[Access(Access.Business)]`, `[FeatureGate("forms")]` y `[HasPermission(Permissions.Forms.Manage)]`.

| Método y ruta | Pide | Devuelve |
| --- | --- | --- |
| GET `/api/organization/forms/templates` | `page`, `pageSize`, `search`, `categoryId?` | 200 `PagedResult<{id, name, description, category, questionCount, catalogCount}>`, solo las publicadas |
| GET `/api/organization/forms/template-categories` | `page`, `pageSize`, `search` | 200 `PagedResult<{id, name, position}>`, solo categorías con al menos una plantilla publicada, por posición e Id |
| GET `/api/organization/forms/templates/{id}` | — | 200 la versión publicada, para ver: preguntas, catálogos con sus opciones, reglas de ejemplo en palabras (con «Rol a elegir») y si trae diseño del PDF |
| POST `/api/organization/forms/templates/{id}/use` | `{name?}` y `[Idempotent]` | 201 `FormDetail` de la E14 + Location al formulario nuevo |

**En la lectura de reglas de un formulario (E21),** sin ruta nueva: la respuesta suma `suggestedRules?`, las reglas de ejemplo con `roleHint` y sin rol. Viene solo si el formulario salió de una plantilla y todavía no tiene ninguna revisión de reglas (decisión 30). La lee el mismo servicio de reglas, con un reader de Forms sobre la versión global de la plantilla.

Las categorías son editables y pueden crecer: tanto su administración como los selectores de categoría consumen páginas, con las lecturas comunes de paginado y búsqueda (10 por defecto, 20, 50 o 100). El historial de versiones usa el cursor común, sin anidarlo completo en el detalle ni contar sus filas. Se verifica en integración que una segunda página/tanda no omita ni repita elementos, y que el contrato no exponga arrays crecientes sin límite (`GrowingCollectionContractTests`).

**Permiso de plataforma:** `platform.formtemplates.manage` va en `PlatformPermissions`, con sus textos y la migración de operadores. El modelo es `20261010214712_PlatformOperatorAssistantPermissions.cs`: agrega la clave a las asignaciones que tienen `platform.operators.manage`.

## 6. Flujos

1. **Crear plantilla:** una UoW con el evento (motivo fijo «Plantilla creada»). Queda en `Draft`.
2. **Guardar el borrador:** la versión esperada, la validación de forma y una UoW con el evento (motivo fijo). Marca `HasUnpublishedChanges`.
3. **Publicar** (o volver a publicar):
   - afuera se valida el motivo;
   - en la UoW:
     - la versión esperada;
     - se comprueba todo (B8);
     - si el borrador cambió respecto de la última versión, se sella `FormTemplateVersion` con número = última + 1; si no cambió y estaba retirada, se vuelve a publicar la misma versión;
     - `State = Published` y el evento con el motivo.
4. **Retirar:** el motivo, la versión y `State = Retired`. Las copias ya hechas no cambian.
5. **Categorías:** el alta, el cambio de nombre, el movimiento y la baja, cada uno en una UoW con su motivo.
6. **Ver en la organización:** se lee la versión publicada. Es un dato global, sin datos de ninguna organización.
7. **Usar esta plantilla:**
   - afuera se comprueban los permisos (B1);
   - en una UoW de Forms, dentro de la organización:
     - se lee la versión publicada (si se retiró en el medio, `NotFound`);
     - se crean los catálogos y el formulario (B3, B4, B9);
     - si es la primera vez de esta organización, se suma 1 en `FormTemplateUsages` (B5).
   - Responde 201 y el front abre la ficha del formulario nuevo, en borrador.

## 7. Archivos

| Archivo nuevo o cambiado | Modelo en este repo |
| --- | --- |
| `D/Forms/FormTemplate.cs`, `FormTemplateVersion.cs`, `FormTemplateCategory.cs`, `FormTemplateUsage.cs`, `FormTemplateErrors.cs` | `D/Assistant/AssistantPlatformSettings.cs` (global y auditada) |
| `D/Authorization/PlatformPermissions.cs`: clase `FormTemplates` con `Manage` | la clase `Assistant` del mismo archivo |
| `A/Modules/Forms/Services/FormTemplateService.cs`, `FormTemplateCategoryService.cs`, `FormTemplateLibraryService.cs` (organización) y los helpers `FormTemplatePublicationPolicy` y `FormTemplateCopyCreator` | los servicios de la E14 |
| `I/Modules/Forms/Persistence/Configurations/FormTemplate*Configuration.cs` y su declaración global en `FormsModelContributor` | `I/Modules/Assistant/Persistence/AssistantModelContributor.cs` |
| `I/Persistence/Migrations/<fecha>_FormTemplates.cs` y `<fecha>_PlatformOperatorFormTemplatePermissions.cs` | `20261010214712_PlatformOperatorAssistantPermissions.cs` |
| `P/Modules/Forms/Controllers/PlatformFormTemplatesController.cs` y `BusinessFormTemplatesController.cs` | `PlatformAssistantController.cs` y `BusinessFormsController` |
| `F/src/modules/forms/`: rutas `/plataforma/formularios`, `/plataforma/formularios/:id` y `/org/formularios/biblioteca`; el editor de la E14 reusado con la pestaña «Catálogos»; las páginas de biblioteca y vista de una plantilla | `F/src/modules/assistant/registration.ts` (rutas por acceso) y el editor de la E14 |

## 8. Pantallas (tableros)

Valen los criterios de la E14 y la decisión 43. El texto para Diseño está en [22-texto-diseno.md](22-texto-diseno.md). Datos de ejemplo:
- **Categorías:** Ventas, Logística y Mantenimiento.
- **Plantillas:**
  - «Visita a cliente» (Ventas): publicada, con cambios guardados sin publicar, que la usaron 12 organizaciones;
  - «Control de vehículo» (Logística): borrador;
  - «Orden de trabajo» (Mantenimiento): retirada.

**Aprobados** en el lienzo v126, ajustados al editor de una sola pantalla en la v130, y con las reglas de ejemplo y el PDF en la v134:

| Tablero | Qué muestra | Estados |
| --- | --- | --- |
| `Plat-FRM-Biblioteca` y `M-Plat-FRM-Biblioteca` | listado: nombre, categoría, estado (Borrador, Publicada o Retirada) y «La usaron N»; filtro por estado y categoría; «Nueva plantilla»; «Categorías» como otra sección del área, no como pestaña | vacío, sin resultados |
| `Plat-FRM-Plantilla-Nueva` | diálogo con el nombre y la categoría | — |
| `Plat-FRM-Plantilla` | ficha con pestañas **Plantilla**, **Catálogos** y **Publicación**. **Plantilla** es el editor de la E14: categoría, prefijo sugerido, descripción, para quién es (50), preguntas en el lugar y, debajo, las reglas de ejemplo y el diseño del PDF (33), con el teléfono al costado. Descartar, Guardar borrador y Publicar. **Catálogos:** una fila por catálogo, que se abre aparte. **Publicación:** estado, versiones con número, fecha, operador y motivo, «Retirar» en el ⋮ de la banda y «Volver a publicar» | cambios sin guardar, otra persona cambió, catálogos, un catálogo abierto, publicación (con su menú, borrador y retirada) |
| `Plat-FRM-Plantilla-Reglas` | «Cuando llega una respuesta» de la E21 en la pestaña «Plantilla», sin botón propio ni historial. En una acción que pide un rol: «Rol: Lo elige cada organización», con candado, y «Rol sugerido» | reglas, editar una regla, una regla con errores, sin reglas y borrador guardado |
| `Plat-FRM-Pdf` | el diseño del PDF de la E21, desde «Ver el diseño», con «Descartar» y «Guardar borrador». La vista previa muestra «Logo» y «Nombre de la organización» como marcadores | — |
| `Plat-FRM-Publicar` y `Plat-FRM-Retirar` | diálogos con el motivo. Publicar muestra además la comprobación: problemas por pregunta, catálogo o regla | sin problemas, con problemas |
| `Plat-FRM-Categorias` | lista con orden (⋮ con Editar, Subir, Bajar y Borrar), diálogo de alta y edición con motivo | una en uso: «Borrar» grisado, con el motivo en su tooltip |
| `FRM-Biblioteca` y `M-FRM-Biblioteca` | para la organización: tarjetas por categoría, con un buscador | sin plantillas |
| `FRM-Plantilla` y `M-FRM-Plantilla` | la vista de una plantilla: descripción, preguntas, catálogos que trae, «Reglas de ejemplo» en palabras (con «un rol que elegís al usarla» y el sugerido) y la vista del teléfono; «Usar esta plantilla» | sin permiso de catálogos |
| `FRM-Plantilla-Usar` | diálogo con el nombre y el prefijo propuesto; al confirmar, abre el formulario nuevo | prefijo tomado: se propone otro |
| `FRM-Editor-Reglas-Plantilla` y `M-FRM-Reglas-Plantilla` | el editor de la organización después de usar la plantilla: las reglas precargadas, con «De la plantilla» y «Sin guardar», «Elegí el rol» con «Rol sugerido» debajo, y una pregunta borrada marcada en la condición y en el texto que la nombra | — |
| `Cuenta-Plat` (ajuste) | los permisos del operador suman «Administrar la biblioteca de formularios» | — |

**Diseño redibuja** (evaluación, §5.1, y ajuste de la Guía del 2026-10-24), sin «Textos en», sin el inglés obligatorio, sin «Cambios sin guardar» ni «Cambios sin publicar», sin el número de versión en la lista ni en la banda, y sin textos junto a los botones (35 y 43):
- `Plat-FRM-Plantilla`, `Plat-FRM-Publicar`, `Plat-FRM-Retirar`, `Plat-FRM-Plantilla-Reglas` (con las acciones de la 61 y la 62, que quedan «Lo elige cada organización»), `Plat-FRM-Pdf` (con la base de la 59), `Plat-FRM-Categorias` y `M-FRM-Plantilla`;
- `Plat-FRM-Biblioteca`, `M-Plat-FRM-Biblioteca` y `Plat-FRM-Plantilla-Nueva`, por el estado de la plantilla;
- `FRM-Editor-Reglas-Plantilla` y `M-FRM-Reglas-Plantilla`: las reglas que llegan de una plantilla, en la pestaña de reglas de la E21.

**Diseño dibuja nuevo** `DOC1` · La biblioteca (evaluación, §5.2): `FRM-Biblioteca` como la biblioteca de formularios estándar (40), con en qué formularios propios se usó cada plantilla. La sección «Lectura de documentos» de `DOC1` no va en esta etapa.

**Mandan sobre esta spec en:**
- **Biblioteca de Plataforma:** filtros Estado y Categoría, y los estados vacía, sin resultados, cargando y error. En el teléfono es solo para ver.
- **El estado de una plantilla** (ajuste de la Guía del 2026-10-24, con la decisión 43): la lista y la banda dicen solo «Borrador», «Publicada» o «Retirada». «Publicar» habilitado ya dice que hay cambios para publicar, y el número de versión va en el historial de versiones de «Publicación».
- **Publicación de una plantilla:**
  - el estado va en grilla, con las versiones (número, fecha, operador y motivo);
  - en borrador dice «Todavía no se publicó.»;
  - una retirada muestra quién la retiró, el motivo y «Volver a publicar».
- **Publicar:** el motivo y la comprobación. Muestra qué cambia, o los problemas por pregunta, catálogo o regla, con «Publicar» grisado. La primera publicación es la versión 1, y volver a publicar sin cambios repite la misma.
- **Las reglas y el diseño del PDF en Plataforma** se guardan con el borrador: van en `FormTemplateDraft` (`rules[]` y `pdfDesign`), con el mismo `PUT …/draft`, su versión y el 409. «Guardar borrador» de `Plat-FRM-Pdf` es ese mismo guardado. Los problemas de las reglas al publicar son `Forms.Template.Invalid` con el campo `rules[i]…` (B8).
- **Las reglas precargadas en la organización:** «Guardar reglas» queda habilitado; al tocarlo sin elegir los roles, se marcan las acciones que lo piden (43). El servidor valida con la regla de la E21, que pide el rol en esas acciones. Se ven y se guardan con `formrules.manage`; sin ese permiso, la sección muestra su estado «sin permiso».
- **«Logo» y «Nombre de la organización»** van como marcadores, porque la plataforma no ve datos de ninguna organización.
- **Usar una plantilla:**
  - se pide el nombre y se propone el prefijo; si VIS está tomado, se propone VIS2, con ⓘ;
  - si la plantilla se retiró en el medio, avisa y vuelve a la biblioteca;
  - sin permiso de catálogos, «Usar esta plantilla» queda grisado, con el motivo en su tooltip (43).

**Decisiones del usuario** sobre los tableros:
- **Plataforma:** «Formularios» es un grupo de Administración, con Biblioteca y Categorías.
- **Pestaña Catálogos de la plantilla:** una fila por catálogo, con sus opciones y la pregunta que lo usa. Cada catálogo se abre aparte.
- **Retirar:** va en el ⋮ de la banda, en rojo. El diálogo pide motivo y dice que las copias ya hechas no cambian.
- **Categorías:** la lista con orden y ⋮ (Editar, Subir, Bajar y Borrar); crear, editar, mover y borrar piden motivo.
- **Organización:** «Nuevo formulario» de la lista «Formularios» pasa a «Nuevo formulario ▾», con «Desde cero» y «Desde una plantilla», que abre la biblioteca (79); en el teléfono, el «+» pregunta lo mismo.

## 9. Tests

| Dónde | Qué |
| --- | --- |
| `Domain.UnitTests/Forms/` | estados de la plantilla; publicar sin cambios; volver a publicar sin versión nueva; copia que conserva los ids de preguntas y opciones, con catálogos nuevos (tipo, campos y valores) y `catalogRef` traducidos |
| `Api.IntegrationTests/Modules/Forms/` | rutas de Plataforma: 403 sin el permiso, motivo obligatorio, evento de auditoría en la misma transacción, 409; rutas de organización: solo publicadas, retirada = 404, usar sin `formcatalogs.manage` = 403, el prefijo tomado se reemplaza, idempotencia; conteo: dos usos de la misma organización suman 1, y dos organizaciones suman 2; reglas de ejemplo: se leen de la versión aunque la plantilla se retire, y dejan de venir al guardar la primera revisión, también vacía; el diseño del PDF llega como primera revisión |
| Aislamiento | la plataforma no tiene ninguna ruta que lea formularios o respuestas de una organización; usar una plantilla escribe solo en la organización activa y en la fila de uso |
| Guardas | `ExplicitRouteInventoryTests`, `PermissionTextsTests` (de plataforma), `AuditTrail` de lo global (ADR 0050) |
| Front | biblioteca, vista, uso y editor de plataforma con MSW y axe |
| E2E y recorrido | el operador crea, completa y publica una plantilla; una organización la usa, completa el borrador y lo publica (con el canal de prueba) |

## 10. Puntos de contacto

| Marca | Qué hay que confirmar | Qué asume esta spec |
| --- | --- | --- |
| [E14] | El formato del borrador y el editor del front, para reusarlo en Plataforma. | El mismo formato, con `catalogRef`. |
| [E12] | El modelo de catálogos a mano, sus campos y sus límites. | Una plantilla trae catálogos con los mismos límites (10.000 opciones, 20 campos, textos de 30), su tipo de valor y sus campos. |
| [E21] | El formato de las reglas y del diseño del PDF que deje la E21. | **Resuelto el 2026-10-18** con su resumen, sus tableros (v132) y las decisiones 30 a 33: ver «Reglas de ejemplo y diseño del PDF» en la §3. |
| ADR 0050 | El tipo de evento para lo global del módulo. | `PlatformSettingsChanged` con destino global. |

## 11. Cómo se cierra

La puerta de la etapa, con la lista de control de `/puerta`, más una **prueba real con Meta**:
1. **En Plataforma:** un operador crea una plantilla con un catálogo a mano con un campo extra y reglas de ejemplo, y la publica con motivo.
2. **En una organización de prueba:** alguien la usa. El formulario queda en borrador, con su catálogo propio y el prefijo propuesto; se publica en Meta con el número de la organización, se manda desde un chat de Internos y se responde en el teléfono.
3. **El retiro:** la plataforma retira la plantilla, y la copia de la organización sigue igual.
4. **El conteo:** un segundo uso en la misma organización no suma; un uso en otra organización, sí.
