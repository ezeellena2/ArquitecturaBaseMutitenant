# E17 · Biblioteca de plantillas de la plataforma (spec)

**Estado:** spec escrita el 2026-10-13. No tiene fuente en ArquitecturaBase: se arma con las piezas de este repo y las de las E12, E13 y E16. `/plan-etapa` la convierte en plan cuando le toque, después de verificar los puntos marcados **[E12]**, **[E13]** y **[E16]** contra el código.

**Fuentes:**
- alcance y decisiones: [hoja de ruta](../2026-10-12-formularios-dinamicos-hoja-de-ruta.md), §E17 y decisiones 2, 13, 14, 15 y 16;
- reglas: [AGENTS.md](../../../AGENTS.md), las [fichas](../../rules/README.md) y la [ADR 0050](../../decisions/0050-auditoria-global-de-modulos.md) (auditoría de lo global);
- las specs de la [E12](e12-formularios.md) y la [E13](e13-catalogos.md).

## 1. Alcance

**Entra:**
- **Plataforma:**
  - **Plantillas:** cada una trae un formulario (preguntas en su idioma original, con traducción opcional; botón y prefijo sugerido), sus catálogos a mano, sus reglas de ejemplo y el diseño del PDF (de la E16; ver [«Reglas de ejemplo»](#reglas-de-ejemplo-y-diseño-del-pdf)). Tiene categoría y descripción, con la misma regla de idioma (decisión 17 de la hoja de ruta).
  - **Edición:** borrador y publicar, con versiones. Publicar y retirar piden motivo.
  - **Categorías:** las arma la plataforma.
  - **Uso:** solo la cantidad de organizaciones que usaron cada plantilla.
- **Organización:**
  - con la feature `forms`, ve la biblioteca y una vista de cada plantilla;
  - con «Usar esta plantilla», obtiene una **copia independiente**: un formulario en borrador con sus catálogos y reglas propias, identidades nuevas y su prefijo. Nunca se publica sola.
- **Permiso de plataforma:** `platform.formtemplates.manage`.

**No entra:**
- avisar «hay una versión nueva» o actualizar una copia: queda para después, si se pide;
- catálogos con filtros en una plantilla (decisión 14);
- plantillas de una organización para otra;
- saber qué organizaciones usaron una plantilla (decisión 16);
- borrar plantillas: se retiran.

## 2. Decisiones

**De producto, tomadas por el usuario el 2026-10-13** (decisiones 13 a 16 de la hoja de ruta):
1. **Edición:** borrador y publicar. Los cambios quedan en un borrador, y «Publicar» crea la versión nueva que ven las organizaciones. Publicar y retirar piden motivo, como el resto de Plataforma.
2. **Catálogos:** solo a mano. Los catálogos con filtros dependen de roles y empresas de cada organización.
3. **Categorías:** las arma la plataforma, en una lista corta con nombre y orden que se edita en la misma biblioteca. Ninguna lista fija en el código. Desde la decisión 17, el nombre lleva un idioma original obligatorio y una traducción opcional; se muestra en el idioma de quien mira si existe, y si no, en el original.
4. **Uso:** la plataforma ve solo la cantidad («La usaron 12 organizaciones»), nunca cuáles.

**Técnicas, o de producto menor**, con su porqué:

| # | Decisión | Por qué |
| --- | --- | --- |
| B1 | La biblioteca la ve quien tiene `forms.manage`. Usar una plantilla que trae catálogos pide además `formcatalogs.manage`. | Usarla crea un formulario y catálogos: son los mismos permisos que crearlos a mano. |
| B2 | **Sin borrar plantillas:** se retiran. Una retirada se puede volver a publicar, y si el borrador no cambió, no se crea una versión nueva. | Las copias guardan de qué plantilla y versión salieron. |
| B3 | Cada uso crea **catálogos nuevos**, con los mismos nombres. No se reusa un catálogo de un uso anterior. | Las copias son independientes; reusar un catálogo exige rastrear de dónde salió. |
| B4 | Si el prefijo sugerido está tomado en la organización, se propone otro con la regla de la E12. | Una sola regla de prefijos. |
| B5 | **El conteo de uso:** una fila técnica `platform.FormTemplateUsages(TemplateId, UsedByCount)` suma 1 la primera vez que una organización usa la plantilla. Para saber si es la primera, se mira si esa organización ya tiene un formulario con ese `SourceTemplateId`, en sus propios datos. | Cuenta organizaciones sin guardar cuáles fueron. La plataforma nunca lee datos de una organización. |
| B6 | Las plantillas, sus versiones y las categorías son globales, en `platform`, con `IGlobalSecurityAudited` y `PlatformSettingsChanged` con destino global (ADR 0050). Guardar el borrador registra un motivo fijo; publicar, retirar y cambiar categorías usan el motivo que escribe el operador. | La regla vigente de auditoría de lo global. |
| B7 | **El editor de la plataforma es el mismo de la E12,** montado en la ruta de Plataforma, y suma la pestaña «Catálogos». Sin Meta: la vista previa es solo la orientativa del teléfono. | Un solo editor. Una plantilla no se publica en WhatsApp. |
| B8 | **Publicar comprueba:** las reglas de Forms, el idioma original completo (la traducción es opcional y no bloquea), los catálogos de la plantilla y `IStructuredFormChannel.Check` (es puro, no llama a Meta). Si WhatsApp no está instalado, la comprobación del canal no corre. | La copia nace lista para publicar. |
| B9 | La copia guarda `SourceTemplateId` y `SourceTemplateVersion` en la definición. | Sirve para el conteo (B5) y para el futuro aviso de versión nueva. |
| B10 | La plataforma no tiene formularios propios ni manda formularios. | Decisión 4 de la hoja de ruta: el número de la plataforma no manda formularios. |

## 3. Modelo de datos

**En `platform`** (globales). Cada entidad se declara en `FormsModelContributor` y lleva `IGlobalSecurityAudited`, salvo la fila técnica de uso. El modelo es `Domain/Assistant/AssistantPlatformSettings.cs`.

**FormTemplateCategories** (`IVersioned`)

| Columna | Tipo | Regla |
| --- | --- | --- |
| `Id` | uuid | — |
| `NameEs`, `NameEn` | texto ≤40 | los dos son obligatorios |
| `Position` | int | única, `DEFERRABLE INITIALLY DEFERRED` (como E13 C7) |

Una categoría se borra solo si ninguna plantilla la usa (`Forms.TemplateCategory.InUse`).

**FormTemplates** (`IVersioned`)

| Columna | Tipo | Regla |
| --- | --- | --- |
| `Id` | uuid | — |
| `CategoryId` | uuid | FK |
| `State` | texto | `Draft` (nunca publicada), `Published` o `Retired` |
| `Draft` | jsonb | `FormTemplateDraft` (abajo) |
| `PublishedVersionId` | uuid? | la versión que ven las organizaciones |
| `HasUnpublishedChanges` | bool | el borrador difiere de la versión publicada |
| auditoría | — | `IGlobalSecurityAudited` |

**`FormTemplateDraft`** (jsonb, `format: 1`):
- `originalCulture` (no cambia), `name{…}`, `description{…}` (≤300), `suggestedPrefix`, `submitLabel{…}`; los textos son mapas por cultura, con el original obligatorio;
- `questions[]`, en el formato del borrador de la E12;
- en una pregunta `CatalogChoice`, `catalogRef` (id local de la plantilla) en lugar de `catalogId`;
- `catalogs[]`: `{catalogRef, name{…}, items[{code, text{…}, detail{…}}]}`, en el mismo idioma original que la plantilla (C11 de la E13);
- `rules[]`: las reglas de ejemplo, en el formato de la E16 (un evento, hasta 10 condiciones y hasta 10 acciones), con dos diferencias:
  - las condiciones apuntan a las preguntas y opciones de la plantilla (ids locales); en una pregunta de catálogo, a `catalogRef` más el código de la opción;
  - las acciones que piden un rol (pedir aprobación y avisar a un rol) **no llevan rol**. Llevan un `roleHint` opcional: un texto que solo se muestra, como «Supervisor»;
- `pdfDesign?`: el diseño del PDF de la E16 (nombre, preguntas, quién aprobó, pie e «Incluir las fotos»), con las preguntas por id local.

### Reglas de ejemplo y diseño del PDF

Se completó el 2026-10-18 con el [resumen](e16-reglas-y-aprobacion.md) y los tableros de la E16 (v132). La regla: las reglas viajan en la plantilla sin destinatarios propios de una organización, y la copia las deja para completar.
- **Al publicar la plantilla:** las reglas se validan con las reglas de la E16 (pedir aprobación es la última acción y solo al recibir, el PDF solo al aprobar, adjuntarlo pide una acción anterior que lo genere, y los textos van en el idioma original), salvo el rol. Lo mismo vale para el diseño del PDF.
- **Al usar la plantilla** (pendiente de elección; esto es lo recomendado, ver la [§7 de la hoja](../2026-10-12-formularios-dinamicos-hoja-de-ruta.md#7-decisiones-pendientes)):
  - las reglas **no se activan solas**. Quedan como reglas sugeridas en la definición (`FormDefinitions.SuggestedRules`, jsonb) y el editor las muestra precargadas en «Cuando llega una respuesta», sin guardar, con «Elegí el rol» en cada acción que lo pide;
  - cuando la persona elige los roles y toca «Guardar reglas», se crea la revisión 1, con la regla de guardado de la E16. Hasta entonces, el formulario se puede publicar sin reglas;
  - el diseño del PDF se copia como primera revisión del diseño, porque no tiene datos de la organización;
  - el `roleHint` se muestra como ayuda, sin elegir ningún rol solo: las referencias van por id, nunca por nombre (lección del 06 del traspaso).

**FormTemplateVersions** (inmutable)

| Columna | Tipo | Regla |
| --- | --- | --- |
| `Id`, `TemplateId` | uuid | — |
| `Number` | int | único por plantilla |
| `Snapshot` | jsonb | copia sellada del borrador |
| `ContentHash` | texto 64 | — |
| `PublishedAtUtc`, `PublishedBy`, `Reason` | — | — |

**FormTemplateUsages** (técnica, sin auditoría): `TemplateId` (PK) y `UsedByCount`.

**En `tenant`** (cambios a la E12): `FormDefinitions` suma `SourceTemplateId uuid?` y `SourceTemplateVersion int?`, con índice `(TenantId, SourceTemplateId)`.

## 4. Dominio y errores

**Entidades** (`Domain/Forms`):
- **`FormTemplate`:** `Create`, `SaveDraft`, `Publish(reason)`, `Retire(reason)` y `Republish(reason)`;
- **`FormTemplateVersion.Seal`**;
- **`FormTemplateCategory`:** `Rename` y `SwapPosition`.

**Copiar** (`FormTemplateCopyCreator`, en Application) arma, desde la versión publicada:
- los catálogos nuevos, con posiciones de 1 a n;
- el borrador del formulario, con `FieldId` y `ChoiceId` nuevos y los `catalogRef` traducidos a los ids de los catálogos nuevos;
- el nombre de la plantilla, el prefijo libre y la fuente;
- el mismo idioma original que la plantilla, en el formulario y en sus catálogos, con la traducción que tenga;
- las reglas sugeridas y el diseño del PDF, con los ids de preguntas, opciones e ítems de catálogo traducidos a los nuevos (los ítems, por `catalogRef` y código).

| Código | Tipo | Cuándo |
| --- | --- | --- |
| `Forms.Template.NotFound` | NotFound | inexistente; en la organización, también si no está publicada |
| `Forms.Template.OriginalIncomplete` | Validation (campo) | falta un texto en el idioma original al publicar. Una traducción incompleta no bloquea: la comprobación solo la informa. |
| `Forms.Template.Invalid` | Validation (campo) | pregunta, catálogo, `catalogRef`, regla (`rules[i].actions[j]…`) o diseño del PDF fuera de regla |
| `Forms.Template.ChannelIssues` | Validation | el perfil del canal marcó problemas (como en la E12) |
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
| GET `/api/platform/forms/templates` | `page`, `pageSize`, `sort`, `search`, `state` (`all`, `draft`, `published`, `retired`), `categoryId?` | 200 `PagedResult<{id, name, category, state, versionNumber?, hasUnpublishedChanges, usedByCount}>` |
| GET `/api/platform/forms/templates/filter-counts` | `search`, `categoryId?` | 200 `{all, draft, published, retired}` |
| POST `/api/platform/forms/templates` | `{name{es}, categoryId}` | 201 detalle |
| GET `/api/platform/forms/templates/{id}` | — | 200 `{id, state, category, draft, publishedVersion?{number, publishedAtUtc}, usedByCount, version}` |
| GET `/api/platform/forms/templates/{id}/versions` | `after?`, `limit?` (`CursorRequest`) | 200 `CursorResult<{number, publishedAtUtc, publishedBy, reason}>`, por fecha e Id descendentes, sin total |
| PUT `/api/platform/forms/templates/{id}/draft` | `{draft, categoryId, version}` | 200 `VersionResponse` |
| POST `/api/platform/forms/templates/{id}/publication-checks` | `{version}` | 200 `{canPublish, issues[], nextVersionNumber}`; no escribe |
| POST `/api/platform/forms/templates/{id}/publish` | `{version, reason}` | 200 `{versionNumber, version}`. Sirve también para volver a publicar. |
| POST `/api/platform/forms/templates/{id}/retire` | `{version, reason}` | 200 `VersionResponse` |
| GET `/api/platform/forms/template-categories` | `page`, `pageSize`, `search` | 200 `PagedResult<{id, name{es,en}, position, templateCount, version}>`, por posición e Id |
| POST `/api/platform/forms/template-categories` | `{name{es,en}, reason}` | 201 |
| PUT `/api/platform/forms/template-categories/{id}` | `{name, version, reason}` | 200 `VersionResponse` |
| POST `/api/platform/forms/template-categories/{id}/move` | `{direction, version, reason}` | 200 `VersionResponse` |
| DELETE `/api/platform/forms/template-categories/{id}` | `?version=` y `reason` | 204; o 409 `InUse` |

**Organización:** todas las rutas llevan `[Access(Access.Business)]`, `[FeatureGate("forms")]` y `[HasPermission(Permissions.Forms.Manage)]`.

| Método y ruta | Pide | Devuelve |
| --- | --- | --- |
| GET `/api/organization/forms/templates` | `page`, `pageSize`, `search`, `categoryId?` | 200 `PagedResult<{id, name, description, category, questionCount, catalogCount}>`, solo las publicadas |
| GET `/api/organization/forms/template-categories` | `page`, `pageSize`, `search` | 200 `PagedResult<{id, name, position}>`, solo categorías con al menos una plantilla publicada, por posición e Id |
| GET `/api/organization/forms/templates/{id}` | — | 200 la versión publicada, para ver: preguntas, catálogos con sus opciones, reglas de ejemplo en palabras (con «Rol a elegir») y si trae diseño del PDF |
| POST `/api/organization/forms/templates/{id}/use` | `{name?}` y `[Idempotent]` | 201 `FormDetail` de la E12 + Location al formulario nuevo |

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
| `A/Modules/Forms/Services/FormTemplateService.cs`, `FormTemplateCategoryService.cs`, `FormTemplateLibraryService.cs` (organización) y los helpers `FormTemplatePublicationPolicy` y `FormTemplateCopyCreator` | los servicios de la E12 |
| `I/Modules/Forms/Persistence/Configurations/FormTemplate*Configuration.cs` y su declaración global en `FormsModelContributor` | `I/Modules/Assistant/Persistence/AssistantModelContributor.cs` |
| `I/Persistence/Migrations/<fecha>_FormTemplates.cs` y `<fecha>_PlatformOperatorFormTemplatePermissions.cs` | `20261010214712_PlatformOperatorAssistantPermissions.cs` |
| `P/Modules/Forms/Controllers/PlatformFormTemplatesController.cs` y `BusinessFormTemplatesController.cs` | `PlatformAssistantController.cs` y `BusinessFormsController` |
| `F/src/modules/forms/`: rutas `/plataforma/formularios`, `/plataforma/formularios/:id` y `/org/formularios/biblioteca`; el editor de la E12 reusado con la pestaña «Catálogos»; las páginas de biblioteca y vista de una plantilla | `F/src/modules/assistant/registration.ts` (rutas por acceso) y el editor de la E12 |

## 8. Pantallas (tableros para Diseño)

Valen los criterios de la E12. El texto para Diseño está en [e17-texto-diseno.md](e17-texto-diseno.md). Datos de ejemplo:
- **Categorías:** Ventas, Logística y Mantenimiento.
- **Plantillas:**
  - «Visita a cliente» (Ventas): publicada en la versión 2, con cambios sin publicar, que la usaron 12 organizaciones;
  - «Control de vehículo» (Logística): borrador;
  - «Orden de trabajo» (Mantenimiento): retirada.

| Tablero | Qué muestra | Estados |
| --- | --- | --- |
| `Plat-FRM-Biblioteca` y `M-Plat-FRM-Biblioteca` | listado: nombre, categoría, estado (Borrador, Publicada · v2, Retirada), «Cambios sin publicar» y «La usaron N»; filtro por estado y categoría; «Nueva plantilla»; «Categorías» como otra sección del área, no como pestaña (regla del 2026-10-13 en `docs/rules/pantallas-y-ui.md` del front) | vacío, sin resultados |
| `Plat-FRM-Plantilla-Nueva` | diálogo con el nombre y la categoría | — |
| `Plat-FRM-Plantilla` | ficha con pestañas **Plantilla**, **Catálogos** y **Publicación** (ajustada en la v130). **Plantilla** es el editor de la E12: «Textos en», categoría, prefijo sugerido, descripción, preguntas en el lugar y «Así lo ve el empleado». Con cambios: Descartar, Guardar borrador y Publicar. **Catálogos:** una fila por catálogo, que se abre aparte. **Publicación:** estado, versiones con fecha, operador y motivo, «Retirar» en el ⋮ de la banda y «Volver a publicar» | Plantilla, cambios sin guardar, otra persona cambió, textos en inglés, catálogos, un catálogo abierto, publicación (con su menú, borrador y retirada) |
| `Plat-FRM-Publicar` y `Plat-FRM-Retirar` | diálogos con el motivo. Publicar muestra además la comprobación (problemas por pregunta o catálogo) | sin problemas, con problemas |
| `Plat-FRM-Categorias` | lista con orden (⋮ con Editar, Subir, Bajar y Borrar), diálogo de alta y edición con motivo | una en uso: «Borrar» apagado con su motivo |
| `FRM-Biblioteca` y `M-FRM-Biblioteca` | para la organización: tarjetas por categoría, con un buscador | sin plantillas |
| `FRM-Plantilla` y `M-FRM-Plantilla` | la vista de una plantilla: descripción, preguntas, catálogos que trae y la vista del teléfono; «Usar esta plantilla» | sin permiso de catálogos = botón apagado con su motivo |
| `FRM-Plantilla-Usar` | diálogo con el nombre y el prefijo propuesto; al confirmar, abre la ficha del formulario nuevo | prefijo tomado: se propone otro |
| `Cuenta-Plat` (ajuste) | los permisos del operador suman «Administrar la biblioteca de formularios» | — |

**Dibujados y aprobados en el lienzo v126** (front `8863e87e`), con las decisiones del usuario del 14 al 17/10 en «Criterio · Decisiones por pantalla» (grupo E17). Los tableros son:
- `Plat-FRM-Biblioteca`, `Plat-FRM-Plantilla-Nueva`, `Plat-FRM-Plantilla`, `Plat-FRM-Publicar`, `Plat-FRM-Retirar` y `Plat-FRM-Categorias`;
- `FRM-Biblioteca`, `FRM-Plantilla` y `FRM-Plantilla-Usar`;
- los teléfonos `M-Plat-FRM-Biblioteca`, `M-FRM-Biblioteca` y `M-FRM-Plantilla`;
- el ajuste de `Cuenta-Plat`.

**Ajustados al editor nuevo en la v130** (2026-10-18, front `e074a7d7`), aprobada por el usuario. `Plat-FRM-Plantilla` deja las pestañas «Preguntas» e «Inglés», y su pestaña «Plantilla» es el editor de una sola pantalla de la E12.

**Falta dibujar, para las reglas de ejemplo** (después de que el usuario elija los puntos pendientes de la §7 de la hoja):
- en la pestaña «Plantilla» de `Plat-FRM-Plantilla`: la sección «Cuando llega una respuesta» de la E16 (`FRM-Editor-Reglas`), con «Rol a elegir» y el rol sugerido en lugar del selector de roles, y el diseño del PDF de la plantilla (`FRM-Pdf`);
- en la organización, después de usar una plantilla: `FRM-Editor-Reglas` con las reglas sugeridas precargadas, sin guardar, y «Elegí el rol» en cada acción que lo pide;
- en `FRM-Plantilla`: las reglas de ejemplo en palabras.

**Decisiones del usuario.** Eligió todas las recomendadas, y ya no hay elecciones abiertas:
- **Plataforma:** «Formularios» es un grupo de Administración, con Biblioteca y Categorías.
- **Pestaña Catálogos de la plantilla:** una fila por catálogo, con sus opciones y la pregunta que lo usa. Cada catálogo se abre aparte.
- **Retirar:** va en el ⋮ de la banda, en rojo. El diálogo pide motivo y dice que las copias ya hechas no cambian.
- **Categorías:**
  - la lista con orden y ⋮ (Editar, Subir, Bajar y Borrar);
  - crear, editar, mover y borrar piden motivo;
  - una categoría en uso deja «Borrar» apagado, con su motivo.
- **Organización:**
  - la biblioteca se abre desde «Nuevo formulario ▾», con «Desde cero» y «Desde una plantilla»;
  - en el teléfono, el «+» pregunta lo mismo.

**Mandan sobre esta spec en:**
- **Biblioteca de Plataforma:** filtros Estado y Categoría, y los estados vacía, sin resultados, cargando y error. En el teléfono es solo para ver.
- **Publicación de una plantilla:**
  - el estado va en grilla, con las versiones (número, fecha, operador y motivo);
  - en borrador dice «Todavía no se publicó.»;
  - una retirada muestra quién la retiró, el motivo y «Volver a publicar».
- **Publicar:** el motivo y la comprobación. Muestra qué cambia, o los problemas por pregunta o por catálogo con «Publicar» apagado. La primera publicación es la versión 1, y volver a publicar sin cambios repite la misma.
- **Usar una plantilla:**
  - se pide el nombre y se propone el prefijo; si VIS está tomado, se propone VIS2, con ⓘ;
  - si la plantilla se retiró en el medio, avisa y vuelve a la biblioteca;
  - sin permiso de catálogos, «Usar esta plantilla» aparece apagado con ⓘ (en el teléfono, con el motivo debajo).

## 9. Tests

| Dónde | Qué |
| --- | --- |
| `Domain.UnitTests/Forms/` | estados de la plantilla; publicar sin cambios; volver a publicar sin versión nueva; copia con identidades nuevas y `catalogRef` traducidos |
| `Api.IntegrationTests/Modules/Forms/` | rutas de Plataforma: 403 sin el permiso, motivo obligatorio, evento de auditoría en la misma transacción, 409; rutas de organización: solo publicadas, retirada = 404, usar sin `formcatalogs.manage` = 403, el prefijo tomado se reemplaza, idempotencia; conteo: dos usos de la misma organización suman 1, y dos organizaciones suman 2 |
| Aislamiento | la plataforma no tiene ninguna ruta que lea formularios o respuestas de una organización; usar una plantilla escribe solo en la organización activa y en la fila de uso |
| Guardas | `ExplicitRouteInventoryTests`, `PermissionTextsTests` (de plataforma), `AuditTrail` de lo global (ADR 0050) |
| Front | biblioteca, vista, uso y editor de plataforma con MSW y axe |
| E2E y recorrido | el operador crea, completa y publica una plantilla; una organización la usa, completa el borrador y lo publica (con el canal de prueba) |

## 10. Puntos de contacto

| Marca | Qué hay que confirmar | Qué asume esta spec |
| --- | --- | --- |
| [E12] | El formato del borrador y el editor del front, para reusarlo en Plataforma. | El mismo formato, con `catalogRef`. |
| [E13] | El modelo de catálogos a mano y sus límites. | Una plantilla trae catálogos con los mismos límites (500 opciones, textos de 30). |
| [E16] | El formato de las reglas y del diseño del PDF que deje la E16. | **Resuelto el 2026-10-18** con su resumen y sus tableros (v132): ver «Reglas de ejemplo y diseño del PDF» en la §3. |
| ADR 0050 | El tipo de evento para lo global del módulo. | `PlatformSettingsChanged` con destino global. |

## 11. Cómo se cierra

La puerta de la etapa, más una **prueba real con Meta**:
1. **En Plataforma:** un operador crea una plantilla en español con el inglés incompleto, con un catálogo a mano, y la publica con motivo.
2. **En una organización de prueba:** alguien la usa. El formulario queda en borrador, con su catálogo propio y el prefijo propuesto; se publica en Meta con el número de la organización, se manda desde un chat de Internos y se responde en el teléfono.
3. **El retiro:** la plataforma retira la plantilla, y la copia de la organización sigue igual.
4. **El conteo:** un segundo uso en la misma organización no suma; un uso en otra organización, sí.
