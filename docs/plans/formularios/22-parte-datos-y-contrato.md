# E22 · Biblioteca de plantillas: datos y contrato (spec, parte)

**Estado:** pendiente · parte de la [spec de la E22](22-biblioteca.md), que dice qué leer y cómo se cierra.

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
| B5 | **El conteo de uso:** una fila técnica `platform.FormTemplateUsages(TemplateId, UsedByCount)` suma 1 la primera vez que una organización usa la plantilla. Para saber si es la primera, se mira si esa organización ya tiene un formulario con ese `SourceTemplateId`, en sus propios datos, también los borrados (`IgnoreQueryFilters(["SoftDelete"])`): usar, borrar y volver a usar suma una sola vez. | Cuenta organizaciones sin guardar cuáles fueron. La plataforma nunca lee datos de una organización. |
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
- en una columna de catálogo de un grupo de líneas (E19), lo mismo: `lines.columns[].catalogRef`;
- `catalogs[]`: `{catalogRef, name, source, valueType, fields[], items[{code, text, detail?, value?, fields{}}]}`, donde `source` es `Manual`, `Members` o `Contacts`. Solo los `Manual` llevan opciones, **hasta 200 por catálogo,** porque el borrador viaja entero en cada `GET` y cada `PUT`: una lista más larga es de cada organización. Los otros dos llegan sin filtros (64). Lo demás sigue los límites de la E12 y la E13. Un campo «elegir de otro catálogo» apunta a otro `catalogRef` de la misma plantilla;
- `rules[]`: las reglas de ejemplo, en el formato de la E21 (un evento, hasta 10 condiciones y hasta 10 acciones), con dos diferencias:
  - las condiciones apuntan a las preguntas y opciones de la plantilla (ids locales); en una pregunta de catálogo, a `catalogRef` más el código de la opción;
  - las acciones que piden un rol (pedir aprobación y avisar a un rol) **no llevan rol**. Llevan un `roleHint` opcional: un texto que solo se muestra, como «Supervisor»;
  - **lo que es de cada organización tampoco viaja,** y se trata como el rol: «Lo elige cada organización». Son la persona de «Avisar a una persona», la dirección fija de «Mandar por correo» (62) y la plantilla de WhatsApp para fuera de las 24 horas (61). Al usar la plantilla, el editor las pide como pide el rol;
- `pdfDesign?`: el diseño del PDF de la E21 (nombre, estilo, preguntas, quién aprobó, pie, «Incluir las fotos», secciones con título y orden, mostrar u ocultar y firmas; 59), con las preguntas por id local. El logo, el color y los datos del encabezado son de cada organización (82): la plantilla no los trae;

### Reglas de ejemplo y diseño del PDF

Se completó el 2026-10-18 con el [resumen](21-reglas-y-aprobacion.md) y los tableros de la E21 (v132). La regla: las reglas viajan en la plantilla sin destinatarios propios de una organización, y la copia las deja para completar.
- **Al publicar la plantilla:** las reglas se validan con las reglas de la E21 (pedir aprobación es la última acción y solo al recibir, el PDF al aprobar, o al recibir si ninguna regla pide aprobación, y adjuntarlo pide una acción anterior que lo genere), salvo el rol. Lo mismo vale para el diseño del PDF.
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
- el borrador del formulario, con los `FieldId` y `ChoiceId` de la versión (B11) y los `catalogRef` traducidos a los ids de los catálogos nuevos, también los de las columnas de líneas;
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
| POST `/api/platform/forms/templates/{id}/publication-checks` | — | 200 `{canPublish, issues[], nextVersionNumber}`; no escribe ni lleva `version`, como en la E14 (T8): la comprobación que vale es la de publicar |
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
| GET `/api/organization/forms/templates/{id}/use-proposal` | — | 200 `{name, prefix}`, con el prefijo libre, como `copy-proposal` de la E17 |
| POST `/api/organization/forms/templates/{id}/use` | `{name, prefix}` y `[Idempotent]` | 201 `FormDetail` de la E14 + Location al formulario nuevo; valen `PrefixTaken` y `PrefixInvalid` |

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
