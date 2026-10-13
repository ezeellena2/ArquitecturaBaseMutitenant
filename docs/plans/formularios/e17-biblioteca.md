# E17 · Biblioteca de plantillas de la plataforma (spec)

**Estado:** spec escrita el 2026-10-13. No tiene fuente en ArquitecturaBase: se arma con las piezas de este repo y las de las E12, E13 y E16. `/plan-etapa` la convierte en plan cuando le toque, después de verificar los puntos marcados **[E12]**, **[E13]** y **[E16]** contra el código.

**Fuentes:**
- alcance y decisiones: [hoja de ruta](../2026-10-12-formularios-dinamicos-hoja-de-ruta.md), §E17 y decisiones 2, 13, 14, 15 y 16;
- reglas: [AGENTS.md](../../../AGENTS.md), las [fichas](../../rules/README.md) y la [ADR 0050](../../decisions/0050-auditoria-global-de-modulos.md) (auditoría de lo global);
- las specs de la [E12](e12-formularios.md) y la [E13](e13-catalogos.md).

## 1. Alcance

**Entra:**
- **Plataforma:**
  - **Plantillas:** cada una trae un formulario (preguntas en es/en, botón y prefijo sugerido), sus catálogos a mano y sus reglas de ejemplo **[E16]**. Tiene categoría y descripción en es/en.
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
3. **Categorías:** las arma la plataforma, en una lista corta con nombre es/en y orden que se edita en la misma biblioteca. Ninguna lista fija en el código.
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
| B8 | **Publicar comprueba:** las reglas de Forms, los dos idiomas completos, los catálogos de la plantilla y `IStructuredFormChannel.Check` (es puro, no llama a Meta). Si WhatsApp no está instalado, la comprobación del canal no corre. | La copia nace lista para publicar. |
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
- `name{es,en}`, `description{es,en}` (≤300), `suggestedPrefix`, `submitLabel{es,en}`;
- `questions[]`, en el formato del borrador de la E12;
- en una pregunta `CatalogChoice`, `catalogRef` (id local de la plantilla) en lugar de `catalogId`;
- `catalogs[]`: `{catalogRef, name{es,en}, items[{code, text{es,en}, detail{es,en}}]}`;
- `rules[]` **[E16]**.

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
- el nombre de la plantilla, el prefijo libre y la fuente.

| Código | Tipo | Cuándo |
| --- | --- | --- |
| `Forms.Template.NotFound` | NotFound | inexistente; en la organización, también si no está publicada |
| `Forms.Template.MissingTranslation` | Validation (campo) | falta un idioma al publicar |
| `Forms.Template.Invalid` | Validation (campo) | pregunta, catálogo o `catalogRef` fuera de regla |
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
| GET `/api/platform/forms/templates/{id}` | — | 200 `{id, state, category, draft, publishedVersion?{number, publishedAtUtc}, versions[{number, publishedAtUtc, publishedBy, reason}], usedByCount, version}` |
| PUT `/api/platform/forms/templates/{id}/draft` | `{draft, categoryId, version}` | 200 `VersionResponse` |
| POST `/api/platform/forms/templates/{id}/publication-checks` | `{version}` | 200 `{canPublish, issues[], nextVersionNumber}`; no escribe |
| POST `/api/platform/forms/templates/{id}/publish` | `{version, reason}` | 200 `{versionNumber, version}`. Sirve también para volver a publicar. |
| POST `/api/platform/forms/templates/{id}/retire` | `{version, reason}` | 200 `VersionResponse` |
| GET `/api/platform/forms/template-categories` | — | 200 `[{id, name{es,en}, position, templateCount, version}]` |
| POST `/api/platform/forms/template-categories` | `{name{es,en}, reason}` | 201 |
| PUT `/api/platform/forms/template-categories/{id}` | `{name, version, reason}` | 200 `VersionResponse` |
| POST `/api/platform/forms/template-categories/{id}/move` | `{direction, version, reason}` | 200 `VersionResponse` |
| DELETE `/api/platform/forms/template-categories/{id}` | `?version=` y `reason` | 204; o 409 `InUse` |

**Organización:** todas las rutas llevan `[Access(Access.Business)]`, `[FeatureGate("forms")]` y `[HasPermission(Permissions.Forms.Manage)]`.

| Método y ruta | Pide | Devuelve |
| --- | --- | --- |
| GET `/api/organization/forms/templates` | `page`, `pageSize`, `search`, `categoryId?` | 200 `PagedResult<{id, name, description, category, questionCount, catalogCount}>`, solo las publicadas |
| GET `/api/organization/forms/template-categories` | — | 200 las categorías con al menos una plantilla publicada |
| GET `/api/organization/forms/templates/{id}` | — | 200 la versión publicada, para ver: preguntas, catálogos con sus opciones y reglas **[E16]** |
| POST `/api/organization/forms/templates/{id}/use` | `{name?}` y `[Idempotent]` | 201 `FormDetail` de la E12 + Location al formulario nuevo |

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
| `Plat-FRM-Plantilla` | ficha con pestañas: **Preguntas** e **Inglés** (las piezas de la E12), **Catálogos** (los catálogos a mano de la plantilla, con sus opciones) y **Publicación** (estado, versiones con fecha, operador y motivo, «Publicar», «Retirar» y «Volver a publicar») | cambios sin publicar, conflicto |
| `Plat-FRM-Publicar` y `Plat-FRM-Retirar` | diálogos con el motivo. Publicar muestra además la comprobación (problemas por pregunta o catálogo) | sin problemas, con problemas |
| `Plat-FRM-Categorias` | lista con orden (⋮ con Editar, Subir, Bajar y Borrar), diálogo de alta y edición con motivo | borrar una en uso = aviso |
| `FRM-Biblioteca` y `M-FRM-Biblioteca` | para la organización: tarjetas por categoría, con un buscador | sin plantillas |
| `FRM-Plantilla` y `M-FRM-Plantilla` | la vista de una plantilla: descripción, preguntas, catálogos que trae y la vista del teléfono; «Usar esta plantilla» | sin permiso de catálogos = botón apagado con su motivo |
| `FRM-Plantilla-Usar` | diálogo con el nombre y el prefijo propuesto; al confirmar, abre la ficha del formulario nuevo | prefijo tomado: se propone otro |
| `Cuenta-Plat` (ajuste) | los permisos del operador suman «Administrar la biblioteca de formularios» | — |

**Elecciones para mostrar con opciones:**
- **Dónde ve la organización la biblioteca:**
  - recomendado: en «Nuevo formulario», con dos caminos, «Desde cero» y «Desde una plantilla»;
  - alternativa: «Biblioteca» como otra sección del panel de Formularios (con la regla del 2026-10-13, ya no puede ser una pestaña).
- **El menú de Plataforma:** «Formularios» como ítem propio, o dentro de una sección de módulos.

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
| [E16] | Qué es una regla, cómo se guarda y a quién avisa. | Las reglas viajan en la plantilla sin destinatarios propios de una organización; la copia las deja para completar. Se especifica cuando exista la spec de la E16. |
| ADR 0050 | El tipo de evento para lo global del módulo. | `PlatformSettingsChanged` con destino global. |
