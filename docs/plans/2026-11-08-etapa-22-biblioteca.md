# E22 · Biblioteca de plantillas y formularios estándar · plan ejecutable

**Estado:** pendiente · Spec: `docs/plans/formularios/22-biblioteca.md` y `22-parte-datos-y-contrato.md`. Tableros aprobados: lienzo v175, salvo los que la spec da como «Se redibujan» por el traspaso 18 (261 a 264), que se aprueban antes de F2. No hay decisión de producto pendiente.

| Parte | Tareas | Estado |
| --- | --- | --- |
| Contrato | K0–K1 | Pendiente |
| Back | B1–B8 | Pendiente |
| Front | F1–F7 | Pendiente |
| Integración y puerta | I1–I3 | Pendiente |

## Forma de trabajo

Modo rápido: primero el contrato (K0 y K1); después Back y Front en paralelo, y una sola puerta al final. Cada tarea lleva sus pruebas focales, y al terminarla, el build y los tests rápidos enteros de su repo, sin Docker y sin filtrar por módulo. Las tareas que tocan rutas, persistencia o aislamiento corren al terminar los tests de integración y de seguridad de Forms y de Plataforma. Un arreglo corre solo lo que toca.

Todo en `main`, sin ramas, worktrees, stash, reset ni push. Cada chat commitea solo lo suyo con `git commit -- <rutas>`, en español y con conventional commits. Con la app levantada, nadie edita el `src` del front. El contrato no cambia sin anotarlo en «Cambios al contrato». Ninguna clave real pasa por un agente, y al usuario se le pregunta solo lo de producto. La tabla de estado la actualiza la Guía.

## Qué se verificó al arrancar

| Fila de la spec | Qué se encontró | Qué cambia |
| --- | --- | --- |
| ADR 0050 | `IGlobalSecurityAudited` (`Domain/Common`) exige un `SecurityEvent` del mismo tipo y destino, con motivo, en la misma UoW; `AssistantPlatformSettings` lo usa con `PlatformSettingsChanged` y destino nulo, y `AssistantPersistenceTests` lo prueba | se copia tal cual; `FormTemplateUsages` queda técnica, sin la marca |
| Permiso de plataforma | `PlatformPermissions` tiene 14 claves. Las escrituras de Plataforma con un permiso de `RequiringRecentProof` piden la prueba reciente del segundo factor (`RecentPlatformProof`) | `FormTemplates.Manage` entra en `All` y en `RequiringRecentProof`, como `Assistant.Configure` y `WhatsApp.Manage` |
| Textos del permiso | van en `Application/Resources/Permissions.resx` y `.en.resx`; `PermissionTextsTests` recorre `Permissions.All` y `PlatformPermissions.All` | B2 los suma |
| `Cuenta-Plat` (78) | la lista sale de `usePlatformPermissionCatalog`: junta `permissions.platform` del `registration.ts` de cada módulo y sus locales (`AccountPermissionsTab.tsx`) | la segunda parte de la base la pasa a áreas plegables con `includes` (260); F7 lo declara en el módulo Forms, y `src/areas/platform/accounts` no se toca |
| Migración de operadores | `20261010214712_PlatformOperatorAssistantPermissions.cs` suma las claves a quien tiene `platform.operators.manage`, con un `SecurityEvent` `PlatformOperatorPermissionsChanged` por cuenta | B2 la copia |
| Rutas de Plataforma de un módulo | llevan `[InstalledModuleRoute(<Módulo>ApiModule.ManagementRoute, InstalledModuleRouteCategory.PlatformManagement)]`, y el módulo registra su disponibilidad (`AssistantApiModule`) | `FormsApiModule` suma `ManagementRoute = "Forms.Management"` |
| Historial con cursor | `CursorRequest` (`After`, `Limit` de 10 a 100) y `CursorResult<T>(Items, NextCursor, HasMore)`; modelo: `PlatformTenantHistoryQuery.cs` | `versions` los usa |
| Ver lo borrado | `QueryFilterBypassTests` permite `IgnoreQueryFilters(["SoftDelete"])` | el conteo (B5 de la spec) lo usa |
| Ayuda | `Infrastructure/Modules/Assistant/Help/Articles/<área>/` con `index.json` (D25 de la 0048) | B8 suma los artículos |
| [E12] [E14] [E19] [E21] | todavía no existen | van a K0 |

## Contrato

### K0 · Lo que dejó la etapa anterior

El chat del contrato confirma esta lista antes de publicar. Lo que difiera se anota en «Cambios al contrato» antes de programar.

- **Base, segunda parte** (214): la pieza `Picker` (262), el orden con `ApplySort` y el `SortMap` de cada reader (264), y los permisos de un operador en áreas plegables (260).
- **E12:** `FormCatalog`, `FormCatalogItem`, `FormCatalogSource` (`Manual`, `Members`, `Contacts`), `FormCatalogValueType`, el estado calculado «No se puede usar» con `unusableReason`, `IFormCatalogRepository`, `formcatalogs.manage`, `FormsModelContributor`, `FormsApiModule`, `FormsContractContributor`, `FormsTexts.resx` y `.en.resx`, `FormsErrorTextSource`, `UniqueKeys.cs` de Forms, `F/src/modules/forms/registration.ts` y sus locales `forms.json` y `enums.json`.
- **E13:** los campos de un catálogo con su `id` y su `key`, el campo «elegir de otro catálogo», la presentación de una opción y `CatalogCombo.tsx`.
- **E14:** `FormDefinition` (`Prefix`, `Audience`, `Draft`), `FormDraft` (`audience`, `submitLabel`, `questions[]` con `fieldId`, `catalogId?`, `presentation?` y `options[{choiceId, label}]`, y `titles[]`), `FormDefinitionStates`, `FormPrefixResolver`, `FormDraftEditor`, `FormDefinitionService`, `FormDetail`, `BusinessFormsController`, `IStructuredFormChannel.Check` con `StructuredFormVersion`, la forma de `issues[]` de `publication-checks` (T26), `Forms.Definition.PrefixTaken` y `PrefixInvalid`, y en el front `FormEditorPage.tsx`, `QuestionEditor.tsx` y `PublishDialog.tsx`.
- **E16:** `questions[].recipientFilters?[]` con `mode` `fixed` o `recipient`, y `fields[].role` (`Data` o `Filter`) en los campos de un catálogo.
- **E17:** `FormDefinitionCopyCreator`, `GET /{id}/copy-proposal`, `ISoftDeletable` en `FormDefinitions` y `FormAvailabilityPolicy`.
- **E18:** las preguntas `Photo` y `Document` (`photoSource?`, `minFiles`, `maxFiles`), la capacidad `form-files` y `Forms.Publication.FilesUnavailable`.
- **E19:** los tipos `Calculation` y `Rows`, `visibleIf?[]`, `calculation.tokens` con `{kind: "catalogField", fieldId, catalogFieldId}`, `rows?{minRows, maxRows}` y `rowsOf`, y «Agregar bloque» en el editor.
- **E20:** la lista dependiente (`recipientFilters[j].mode` `answer`, con `sourceFieldId`), el tipo `location`, `appOnly` y, en `issues[]` de `publication-checks`, `whatsApp: true` en lo que WhatsApp no puede mostrar: los frenos de `Check`, `RowsMinimumOverLimit`, `CatalogTooLarge`, `DependentListNotSupported` y `TooManyLocations` (229 y 230).
- **E21:** `FormRuleSet`, `FormPdfDesign`, `FormDefinitionStates.ActiveRuleSetId` y `ActivePdfDesignId`, `GET /{id}/rules` con `{revision, rules[], questions[]}` y `PUT /{id}/rules` con `{revision, rules[]}` → `{revision}`, `GET` y `PUT /{id}/pdf-design`, `FormRuleService`, `FormRulesPolicy`, `FormPdfDesignService`, `Forms.Rules.Invalid`, `Forms.Rules.PlaceholderInvalid`, `Forms.PdfDesign.Invalid`, y en el front `components/rules/`, `lib/rules.ts`, `pages/PdfDesignPage.tsx` y `components/pdf/PdfPage.tsx`.

### K1 · Publicar antes de programar las pantallas

El Back publica las rutas con `501`, los permisos, los códigos con sus textos y los ejemplos exportados de cada respuesta nueva, incluidos 400, 404, 409 y los vacíos, y genera `docs/contracts/openapi.json`. El Front corre `npm run contracts`; no escribe tipos a mano.

Los cuerpos y tipos son los de la §5 de la spec. Cambia esto, porque los tableros o las etapas anteriores lo piden:
- **`FormTemplateDraft` suma `titles[]`,** como el borrador de la E14: `Plat-FRM-Plantilla` tiene «Agregar título».
- **`publication-checks` suma `draftSavedAtUtc` y `changes[]`,** para «Qué se publica» de `Plat-FRM-Publicar` (agregadas, cambiadas y reglas). Es propio de la plantilla, `TemplateChange {kind: added|changed|removed, target: question|catalog|rule, ref, label}`, y no el de la E14 (`{kind, fieldId, label}`), que no nombra catálogos ni reglas: por eso el diálogo de publicar de la plantilla es aparte (F3). En la primera publicación viene vacío. `issues[]` tiene la forma de la E14, y su `path` nombra también `catalogs[k]…`, `rules[i]…` y `pdfDesign…`.
- **Filtros:** una pregunta lleva solo `recipientFilters` con `mode` `answer` (la lista dependiente de la E20), que con WhatsApp instalado frena al publicar (B4); `fixed` y `recipient` dan `Forms.Template.Invalid`, porque un filtro fijo o según quien lo recibe es de cada organización (64).
- **`catalogs[].fields[]` lleva `role`** (`Data` o `Filter`, E16), y la copia lo conserva.
- **El listado de Plataforma ordena** (264): `sort` por `name`, el de entrada, `category`, `state` por el circuito, `usedByCount` o `lastPublishedAtUtc`, con lo vacío al final y el desempate por Id. `filter-counts` queda solo para el resumen, porque los filtros no llevan cantidades (262).
- **Foto y Documento** entran en una plantilla, que no guarda archivos: publicarla no pide `form-files`; lo comprueba la organización al publicar su copia (E18).

**Plataforma** (`/api/platform/forms`): `[Access(Access.Platform)]`, `[HasPlatformPermission(PlatformPermissions.FormTemplates.Manage)]` e `[InstalledModuleRoute(FormsApiModule.ManagementRoute, InstalledModuleRouteCategory.PlatformManagement)]`. Las escrituras piden la prueba reciente del segundo factor. Todas cuentan solo en las cuotas generales.

| Ruta | Idempotencia y concurrencia | Errores |
| --- | --- | --- |
| GET `templates` y `templates/filter-counts` | — | 400 |
| POST `templates` | `[Idempotent]` | 400, 404 `TemplateCategory.NotFound` |
| GET `templates/{id}` | — | 404 `Template.NotFound` |
| GET `templates/{id}/versions` | cursor | 404 |
| PUT `templates/{id}/draft` | `version`, 409 | 400 (`Template.Invalid` de forma), 404, 409 |
| POST `templates/{id}/publication-checks` | no escribe ni lleva `version` | 404 |
| POST `templates/{id}/publish` | `version`, 409 | 400 `ReasonRequired`, `Incomplete`, `Invalid` y `ChannelIssues`; 404; 409 `NothingToPublish` |
| POST `templates/{id}/retire` | `version`, 409 | 400 `ReasonRequired`; 404; 409 `NotPublished` |
| GET `template-categories` | — | 400 |
| POST `template-categories` | `[Idempotent]` | 400 |
| PUT `template-categories/{id}` | `version`, 409 | 400, 404 |
| POST `template-categories/{id}/move` | `version`, 409 | 404, 409 `MoveOutOfRange` |
| DELETE `template-categories/{id}` | `?version=`, 409 | 404, 409 `InUse` |

**Organización** (`/api/organization/forms`): `[Access(Access.Business)]`, `[FeatureGate("forms")]` y `[HasPermission(Permissions.Forms.Manage)]`. Cuotas generales.

| Ruta | Idempotencia | Errores |
| --- | --- | --- |
| GET `templates` y `template-categories` | — | 400 |
| GET `templates/{id}` | — | 404 `Template.NotFound` (también retirada) |
| GET `templates/{id}/use-proposal` | — | 404 |
| POST `templates/{id}/use` | `[Idempotent]` | 400 `Forms.Definition.PrefixInvalid`; 403 `CatalogPermissionRequired`; 404; 409 `Forms.Definition.PrefixTaken` |
| GET `{id}/rules` (E21, con `formrules.manage`) | — | suma `suggestedRules?`, sin ruta nueva |

Lo común: 401, 403 sin el permiso, 404 con la feature apagada, 409 `General.ConcurrencyConflict` y los de idempotencia. Las listas usan el paginado común (10 por defecto) y `GrowingCollectionContractTests` acota en `FormsContractContributor` las preguntas, los catálogos, las opciones (200), las reglas y `changes[]`.

**Textos de error** (en `FormsTexts.resx` y `.en.resx`):

| Código | es | en |
| --- | --- | --- |
| `Forms.Template.NotFound` | No encontramos la plantilla. | We couldn't find the template. |
| `Forms.Template.Incomplete` | Completá este dato para publicar. | Fill in this field to publish. |
| `Forms.Template.Invalid` | Este dato no cumple las reglas de la plantilla. | This field doesn't meet the template rules. |
| `Forms.Template.ChannelIssues` | WhatsApp no admite algunas preguntas como están. | WhatsApp doesn't support some questions as they are. |
| `Forms.Template.NothingToPublish` | No hay cambios para publicar. | There are no changes to publish. |
| `Forms.Template.NotPublished` | La plantilla no está publicada. | The template isn't published. |
| `Forms.Template.ReasonRequired` | Escribí el motivo. | Enter the reason. |
| `Forms.Template.CatalogPermissionRequired` | Para usar esta plantilla necesitás administrar catálogos. | To use this template you need to manage catalogs. |
| `Forms.TemplateCategory.NotFound` | No encontramos la categoría. | We couldn't find the category. |
| `Forms.TemplateCategory.InUse` | La categoría tiene plantillas y no se puede borrar. | The category has templates and can't be deleted. |
| `Forms.TemplateCategory.MoveOutOfRange` | La categoría ya está en ese extremo. | The category is already at that end. |

**Permiso:** `platform.formtemplates.manage`, «Administrar la biblioteca de formularios» / «Manage the forms library», con su descripción «Crear, editar, publicar y retirar las plantillas de la biblioteca y sus categorías.» / «Create, edit, publish and retire the library templates and their categories.»

**Texto para el chat del Contrato**

```text
Leé AGENTS.md y este plan (docs/plans/2026-11-08-etapa-22-biblioteca.md), con la spec de la E22 y su parte de datos y contrato. Primero K0: confirmá en el código cada nombre de la lista y anotá en «Cambios al contrato» lo que difiera. Después K1: publicá con 501 las rutas, el permiso con sus textos, los errores es/en y los ejemplos exportados; generá openapi.json y avisale a la Guía para que el front corra npm run contracts. Guardas de rutas y de contrato en verde. Reglas: todo en main, sin ramas, worktrees, stash, reset ni push; commiteá solo lo tuyo con git commit -- <rutas>, en español y con conventional commits; con la app levantada, nadie edita el src del front; TDD donde hay lógica; al terminar, build y tests rápidos enteros, sin Docker y sin filtrar por módulo, y si tocaste rutas, persistencia, autenticación o aislamiento, los de integración y seguridad del área; el contrato no cambia sin anotarlo en «Cambios al contrato»; ninguna clave real pasa por vos; al usuario se le pregunta solo lo de producto.
```

## Back

Rigen Result y ProblemDetails, `OperationLog.RunAsync`, `[LoggerMessage]`, `TimeProvider`, textos es/en, una UoW por método que escribe, la auditoría global de la ADR 0050 y el aislamiento. Forms no referencia a WhatsApp: el canal se consulta por `IStructuredFormChannel`.

1. **B1 · Dominio.** `D/Forms/FormTemplate.cs` (`Create`, `SaveDraft`, `Publish`, `Retire`, `Republish`), `FormTemplateVersion.cs` (`Seal`), `FormTemplateCategory.cs` (`Rename`, `SwapPosition`), `FormTemplateUsage.cs`, `FormTemplateDraft.cs` y `FormTemplateErrors.cs`. Copiá `D/Assistant/AssistantPlatformSettings.cs`. Tests en `Domain.UnitTests/Forms/`: los tres estados; publicar sin cambios; volver a publicar una retirada sin versión nueva; `HasUnpublishedChanges`.
2. **B2 · Permiso y persistencia.** `PlatformPermissions.FormTemplates.Manage`, en `All` y `RequiringRecentProof`; sus textos en `Permissions.resx` y `.en.resx`; `I/Modules/Forms/Persistence/Configurations/FormTemplate*Configuration.cs`, en `platform`, declaradas por tipo exacto en `FormsModelContributor`; las migraciones `<fecha>_FormTemplates.cs` (con `FormDefinitions.SourceTemplateId` y `SourceTemplateVersion`, y el índice `(TenantId, SourceTemplateId)`) y `<fecha>_PlatformOperatorFormTemplatePermissions.cs`. Copiá `AssistantModelContributor.cs` y `20261010214712_PlatformOperatorAssistantPermissions.cs`. La posición única de una categoría es `DEFERRABLE INITIALLY DEFERRED`, en `Sql`, como la E12. Tests: `PermissionTextsTests`, `DataClassificationTests`, una escritura global sin evento se rechaza, y la migración da el permiso solo a quien tiene `platform.operators.manage`, con su evento.
3. **B3 · Plantillas y categorías en Plataforma.** `A/Modules/Forms/Services/FormTemplateService.cs` y `FormTemplateCategoryService.cs`, sus interfaces, `IFormTemplateRepository`, `IFormTemplateReader` y los validadores. Copiá `AssistantSettingsService.cs` y `AssistantSettingsAuditWriter.cs` (escritura global con evento) y `FormDefinitionService.cs` [E14]. Guardar el borrador registra el motivo fijo «Borrador guardado»; crear, «Plantilla creada»; publicar, retirar y las categorías, el que escribe el operador. Tests: motivo obligatorio, evento en la misma transacción, 409, una categoría en uso, mover en los extremos, el listado por cada columna con lo vacío al final (264) y la segunda página de las versiones sin repetir ni omitir.
4. **B4 · Comprobar al publicar.** Helper `FormTemplatePublicationPolicy`: las reglas del borrador de la E14 a la E21 con `catalogRef`, los textos obligatorios, los catálogos (a mano hasta 200 opciones; de los miembros y de los contactos, sin filtros), las reglas de ejemplo con la validación de la E21 salvo el rol, el diseño del PDF y, si `IStructuredFormChannel.IsAvailable`, `Check` y todo issue con `whatsApp: true` de la E20, también una segunda Ubicación (229 y 230): una plantilla no se publica solo para la app ni lleva «Dónde se completa». No corre `FilesUnavailable`. Arma también `changes[]` contra la última versión. Copiá `FormRulesPolicy.cs` [E21]. Tests: cada problema con su `path`; una condición que compara números y una segunda Ubicación frenan; sin WhatsApp no corre `Check`; Foto y Documento sin almacenamiento; un filtro `fixed` o `recipient`; la primera versión es la 1 y volver a publicar sin cambios repite la misma.
5. **B5 · La biblioteca de la organización.** `FormTemplateLibraryService.cs` y su reader: solo publicadas, la versión publicada para ver, solo categorías con alguna publicada y `usedIn{id, name, count}`, que lee los formularios propios con `SourceTemplateId`, sin los borrados: la tarjeta no nombra lo que ya no existe, aunque el conteo de la plataforma sí los cuente. Tests: una retirada da 404; `usedIn` de esta organización y nunca de otra; la vista no trae nada de una organización.
6. **B6 · Usar una plantilla.** Helper `FormTemplateCopyCreator` y `use-proposal`. Copiá `FormDefinitionCopyCreator.cs` [E17] y `copy-proposal`, con `FormPrefixResolver` [E14]. En una UoW de la organización: lee la versión (retirada en el medio = 404), crea los catálogos nuevos (a mano con posiciones de 1 a n, tipo, campos con su id y su `role`, y valores; los otros, sin filtros), el formulario en borrador («En la app y por WhatsApp», 229) con los `FieldId`, `ChoiceId` y los `catalogRef` traducidos, también en el bloque, el diseño del PDF como revisión 1 (`ActivePdfDesignId`), y suma 1 en `FormTemplateUsages` si es el primer formulario de la organización con ese `SourceTemplateId`, contando los borrados con `IgnoreQueryFilters(["SoftDelete"])`. La UoW toma primero `AdvisoryLockKeys.For(tenantId, "form-template-use", templateId)`, y el incremento es atómico (`UsedByCount + 1` en SQL). Tests: ids conservados; una fórmula con `catalogFieldId` y una presentación propia siguen andando; prefijo tomado; idempotencia; dos usos de una organización suman 1, también a la vez, y dos organizaciones a la vez suman 2; sin `formcatalogs.manage`, 403; escribe solo en la organización activa y en la fila de uso.
7. **B7 · Las reglas de ejemplo.** En la lectura de reglas de la E21, `suggestedRules?` con `roleHint` y sin rol, desde `FormTemplateVersions.Snapshot`, mientras el formulario no tenga ninguna revisión. Un reader de Forms sobre la versión global. Guardar sigue siendo el `PUT` de la E21, que pide el rol. Tests: se leen aunque la plantilla se retire; dejan de venir con la primera revisión, también vacía; una pregunta borrada llega marcada.
8. **B8 · Rutas, guardas y ayuda.** `P/Modules/Forms/Controllers/PlatformFormTemplatesController.cs` y `BusinessFormTemplatesController.cs`, sus `Contracts/*HttpRequest.cs`, `FormsApiModule.ManagementRoute` y `FormsContractContributor`. Copiá `PlatformAssistantController.cs` y `BusinessFormsController` [E14]. Los artículos de ayuda de la biblioteca, en `Help/Articles/forms/` y `platform/`, con `index.json`. Tests: cada ruta con 401, 403 y feature apagada = 404; la prueba reciente en las escrituras; ejemplos exportados; `ExplicitRouteInventoryTests`, `AccessDeclarationTests`, `ModuleControllersTests`, `ApplicationServicesTests`, `ModuleIsolationTests` y `GrowingCollectionContractTests`; aislamiento en `Api.IntegrationTests/Tenancy/`: Plataforma no tiene ninguna ruta que lea formularios o respuestas de una organización.

**Texto para el chat del Back**

```text
Leé AGENTS.md, este plan (docs/plans/2026-11-08-etapa-22-biblioteca.md), la spec de la E22 con su parte de datos y contrato, la ADR 0050 y las fichas persistencia-ef, multitenancy, auditoria, guardado, concurrencia, idempotencia, api-http, permisos, paginado-y-busqueda, result-y-errores, validacion, textos-y-traducciones, logs, modulos y tests. Después de K1, hacé B1 a B8 en orden y contra ese contrato, copiando los moldes de cada tarea. No toques el front ni la tabla de estado del plan. Reglas: todo en main, sin ramas, worktrees, stash, reset ni push; commiteá solo lo tuyo con git commit -- <rutas>, en español y con conventional commits; con la app levantada, nadie edita el src del front; TDD donde hay lógica; al terminar cada tarea, build y tests rápidos enteros del back, sin Docker y sin filtrar por módulo, y con rutas, persistencia, autenticación o aislamiento, los de integración y seguridad de Forms y de Plataforma; el contrato no cambia sin anotarlo en «Cambios al contrato» y avisarle a la Guía; ninguna clave real pasa por vos; al usuario se le pregunta solo lo de producto.
```

## Front

Siguen los tableros v175 y las fichas de UI, formularios, listados, accesibilidad y tema; i18n, tipos generados y axe en cada pantalla. La lectura usa las piezas de la base de la 12 (`PageLoading`, `ReasonDialog`, el estado una vez).

1. **F1 · Tipos y llamadas.** `F/src/modules/forms/api/templates.ts` y `platformTemplates.ts`, contra K1, con los ejemplos exportados validados con zod estricto. Copiá la API de catálogos [E12].
2. **F2 · Plataforma: navegación, biblioteca y categorías.** En `registration.ts`, el grupo «Formularios» de Administración con Biblioteca (`/plataforma/formularios`) y Categorías (`/plataforma/formularios/categorias`), con el permiso. Páginas de `Plat-FRM-Biblioteca` (y su teléfono, solo para ver), `Plat-FRM-Plantilla-Nueva` y `Plat-FRM-Categorias`. Copiá `F/src/modules/assistant/registration.ts` y la lista de catálogos [E12]. Tests: filtros Estado y Categoría en la pieza y sin cantidades (143, 144 y 262); ordenar desde el encabezado y desde «Ordenar por», que vuelve a la página 1 (264); en el teléfono, sin datos juntos (263); los estados de cada tablero y axe.
3. **F3 · La plantilla.** `/plataforma/formularios/:id` con las pestañas de `Plat-FRM-Plantilla`: Plantilla (el editor de la E14 en modo plantilla, sin «Dónde se completa» (229), con `catalogRef`, «Agregar título», «Agregar bloque» y «Agregar pregunta», y el teléfono), Catálogos (una fila por catálogo con su origen y la pregunta que lo usa; cada uno se abre en `/plataforma/formularios/:id/catalogos/:catalogRef` con el editor de la E12 y la E13 sobre el borrador) y Publicación (grilla sin el renglón «Estado», versiones con cursor). La categoría y «Para quién es», en la pieza (262); las preguntas, con sus datos en columnas, como el editor de la E14 (263), y «Catálogos» ordena en el navegador (264). En modo plantilla, `QuestionEditor.tsx` ofrece Foto y Documento sin mirar `form-files`, y «Agregar filtro ▾» ofrece solo «Según otra respuesta». La banda con Descartar, Guardar borrador, Publicar y el ⋮ con «Retirar» en rojo. `TemplatePublishDialog.tsx` (`Plat-FRM-Publicar`), aparte, con «Problemas» y «Qué se publica» como `PublishDialog.tsx` [E14] y sus propios `changes[]`; `Plat-FRM-Retirar`. Copiá el editor y `PublishDialog.tsx` [E14]. Tests: 409 «otra persona guardó», descartar, la comprobación con «Publicar versión 3» grisado, «Qué se publica», primera publicación, volver a publicar, Foto y Documento, y el filtro.
4. **F4 · Reglas y PDF en Plataforma.** `Plat-FRM-Plantilla-Reglas`: la sección de reglas de la E21 en modo plantilla, debajo de las preguntas, sin historial ni «Guardar reglas»; el rol, la persona, la dirección y la plantilla de WhatsApp con candado y «Lo elige cada organización», y «Rol sugerido» como texto. `Plat-FRM-Pdf`: el diseño de la E21 desde «Ver el diseño», con el color con candado y «Logo» y «Nombre de la organización» como marcadores; «Guardar borrador» es el mismo `PUT …/draft`. Copiá `components/rules/` y `pages/PdfDesignPage.tsx` [E21], con la pieza (262) y cada dato de catálogo en su renglón (263). Tests: una regla con errores, guardar con el borrador, salir sin guardar.
5. **F5 · La organización: lista y biblioteca.** `FRM-Formularios-22` y su teléfono, con «Estado» y «Dónde se completa» de la E20 (229), «Cargar» en el ⋮ (261) y el orden de la E21 (264): «Nuevo formulario ▾» con «Desde cero» y «Desde una plantilla», y el «+» del teléfono pregunta lo mismo. `/org/formularios/biblioteca` con `FRM-Biblioteca` y su teléfono (tarjetas por categoría, cada una con sus datos nombrados, 263, buscador y «La usaste en «Visita a cliente» y 1 más»), `FRM-Plantilla` y su teléfono (las reglas de ejemplo en pasos, con los datos como etiquetas, 138 y 149) y `FRM-Plantilla-Usar` (el prefijo propuesto con su (i); se retiró: avisa y vuelve). Sin `formcatalogs.manage`, «Usar esta plantilla» grisado con su motivo. Tests: los estados de cada tablero, idempotencia al repetir y axe.
6. **F6 · Las reglas precargadas.** `FRM-Editor-Reglas-Plantilla` y su teléfono: con `suggestedRules`, la sección de la E21 las muestra sin guardar, con «Elegí…» y el rol sugerido en gris en el campo vacío y como «Sugerido» en su opción, no debajo (262 y 263); «Historial» apagado con «Todavía no se guardaron: vienen de la plantilla.» (140); «Guardar reglas» habilitado, y al tocarlo sin elegir se marca lo que falta (43); una pregunta borrada, marcada en la condición y como «Pregunta no disponible» en el mensaje (149). Sin `formrules.manage`, su estado «sin permiso»; una regla que cita respuestas, adjunta el PDF o manda una copia pide además `formrecords.read` al guardarla, con el texto del servidor (la regla de la E21). Tests: los cuatro casos, y guardar sin «Ver respuestas».
7. **F7 · El permiso en la cuenta del operador.** `registration.ts` de Forms declara `platform.formtemplates.manage` con su área, y `forms.json` es/en, su nombre y su descripción: en los permisos de un operador (260), en `Cuenta-Plat` y su teléfono, su área con «Administrar la biblioteca de formularios» y su (i), sin nada incluido. Test: el área en la compu y en el teléfono, y desaparece al retirar Forms.

**Texto para el chat del Front**

```text
Leé AGENTS.md del front, ../ArquitecturaBaseMutitenant/docs/plans/2026-11-08-etapa-22-biblioteca.md y la spec de la E22. Esperá K1, corré npm run contracts y hacé F1 a F7 contra los tipos generados, copiando los tableros del lienzo v175 y los componentes de cada tarea, con MSW y axe. No toques el lienzo, las specs, el back ni la tabla de estado del plan. Reglas: todo en main, sin ramas, worktrees, stash, reset ni push; commiteá solo lo tuyo con git commit -- <rutas>, en español y con conventional commits; con la app levantada, no edites el src; TDD donde hay lógica; al terminar cada tarea, npx tsc -b y npm test enteros, sin filtrar por módulo, y con rutas o permisos, sus tests del área; al cerrar la parte, build, lint y contracts:check; el contrato no cambia sin anotarlo en «Cambios al contrato» y avisarle a la Guía; ninguna clave real pasa por vos; al usuario se le pregunta solo lo de producto.
```

## Integración y puerta

1. **I1 · Integrar.** El front contra la API real; cada diferencia vuelve a su dueño. Se regeneran los tipos y se validan los ejemplos.
2. **I2 · Puerta.** `/puerta` una sola vez sobre copias del corte:
   - build sin advertencias y `dotnet test` con Docker; front build, lint, test y `contracts:check`;
   - E2E real: el operador crea una plantilla con un catálogo a mano con un campo extra y reglas de ejemplo, y la publica con motivo; una organización la usa, elige los roles, guarda las reglas y publica el formulario con el canal de prueba; la plataforma la retira y la copia sigue igual; un segundo uso en la misma organización no suma, y uno en otra, sí;
   - `realinear-docs --detectar` en 0 y `estado-docs --detectar`;
   - galería a 1440, 390 y 320 de las pantallas nuevas: `Plat-FRM-Biblioteca`, `Plat-FRM-Plantilla` con sus pestañas, `Plat-FRM-Plantilla-Reglas`, `Plat-FRM-Pdf`, `Plat-FRM-Publicar`, `Plat-FRM-Retirar`, `Plat-FRM-Categorias`, `FRM-Formularios-22`, `FRM-Biblioteca`, `FRM-Plantilla`, `FRM-Plantilla-Usar`, `FRM-Editor-Reglas-Plantilla` y `Cuenta-Plat`;
   - `/recorrido` con «armar una plantilla» y «usar una plantilla»;
   - la lista de control (269): los catálogos que crea una plantilla numeran sus opciones desde 1, como todo catálogo (O43 de la prueba integral de ArquitecturaBase, que ya coincide).
3. **I3 · Cierre.** `docs/features/formularios.md` suma «Biblioteca»; la ficha de Plataforma, el grupo «Formularios»; los `AGENTS.md` de las carpetas nuevas. La prueba con Meta de la §11 de la spec (publicar la copia en el número de la organización, mandarla desde Internos y responderla en el teléfono) queda pendiente para la I1 de la E10 (123).

| Puerta | Resultado |
| --- | --- |
| Back / Front / Contrato / E2E / Documentación / Galería / Recorrido | Pendiente |

**Texto para el chat de Integración**

```text
Leé AGENTS.md de los dos repos, este plan (docs/plans/2026-11-08-etapa-22-biblioteca.md) y la skill /puerta. Integrá el front con la API real, devolvé cada diferencia a su dueño y corré la puerta una sola vez sobre copias del corte; después de un arreglo, solo lo que toca. No toques el lienzo. Lo de Meta queda pendiente para la I1 de la E10. Dejá solo la tabla corta del resultado en este plan. Reglas: todo en main, sin ramas, worktrees, stash, reset ni push; commiteá solo lo tuyo con git commit -- <rutas>, en español y con conventional commits; con la app levantada, nadie edita el src del front; TDD en cada arreglo con lógica; build y tests rápidos enteros, sin filtrar por módulo, y los de integración y seguridad del área que toque el arreglo; el contrato no cambia sin anotarlo en «Cambios al contrato»; ninguna clave real pasa por vos; al usuario se le pregunta solo lo de producto.
```

## Relación con otras etapas

**Usa de las anteriores:**
- **Base:** `ReasonDialog`, `PageLoading`, `FormSkeleton` y la confirmación de la 161; de su segunda parte (214), los permisos de un operador en áreas plegables (`AccountPermissionsTab`, 260).
- **E12 a E21:** lo que lista K0.

**Deja para las siguientes:**
- **Entidades y tablas:** `FormTemplate`, `FormTemplateVersion`, `FormTemplateCategory`, `FormTemplateUsage`; `platform.FormTemplates`, `FormTemplateVersions`, `FormTemplateCategories` y `FormTemplateUsages`; `FormDefinitions.SourceTemplateId` y `SourceTemplateVersion`.
- **Tipos y piezas:** `FormTemplateDraft` (`format: 1`, con `titles[]`), `FormTemplateCopyCreator`, `FormTemplatePublicationPolicy`, `FormTemplateService`, `FormTemplateLibraryService`, `FormsApiModule.ManagementRoute`.
- **Rutas:** las de `/api/platform/forms/templates…` y `template-categories…`, y las de `/api/organization/forms/templates…`; `suggestedRules?` en la lectura de reglas.
- **Permiso:** `platform.formtemplates.manage`.
- **Para el módulo ARCA:** los estándares que aporta un módulo conviven con estas plantillas, con preguntas atadas y sus reglas activas (220); llegan con su segunda parte, sin cambiar esta etapa.
- **Front:** `/plataforma/formularios`, `/plataforma/formularios/:id` con sus pestañas, `/plataforma/formularios/categorias` y `/org/formularios/biblioteca`; el editor de la E14 y la sección de reglas de la E21 en modo plantilla.

## Cambios al contrato


## Para decidir

Nada: los tableros v175 y las decisiones 2, 13 a 16, 30 a 33, 35, 40, 43, 45, 50, 59, 61, 62, 64, 79, 82, 138, 149, 175, 177, 198, 229, 230, 260 a 264 y 269 resuelven el producto.
