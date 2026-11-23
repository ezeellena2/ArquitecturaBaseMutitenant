# E16 · Datos de las personas y filtros por destinatario · plan ejecutable

**Estado:** pendiente · Spec: `docs/plans/formularios/16-datos-de-las-personas.md` y su parte `16-parte-datos-y-contrato.md`. Tableros aprobados: lienzo v175. Decisiones pendientes: ninguna.

| Parte | Tareas | Estado |
| --- | --- | --- |
| Contrato | K0–K1 | Pendiente |
| Back | B1–B12 | Pendiente |
| Front | F1–F6 | Pendiente |
| Integración y puerta | I1–I3 | Pendiente |

## Forma de trabajo

Modo rápido: primero el contrato (K0 y K1); después Back y Front en paralelo, y una sola puerta al final. La prueba con Meta queda para la I1 de la 10 (123).

Cada tarea lleva sus pruebas, con un test con dos actores para cada bloqueo, y un arreglo corre solo lo que toca. Las reglas de trabajo van enteras en el texto de cada chat. La tabla de estado la actualiza la Guía.

**Revisión adversarial** ([AGENTS.md](../../AGENTS.md)): la llevan las tareas **(R)**, completa y antes de su commit; un subagente nuevo recibe el código y los escenarios, sin la conclusión de quien programó.

## Qué se verificó al arrancar

| Fila de la spec | Qué se encontró | Qué cambia |
| --- | --- | --- |
| [E14] Las preguntas del borrador, `FormOfferBuilder`, `offerProblems` y `publication-checks`. [E13] `FieldsJson`, el índice GIN y el modal. [E12] `IMemberDirectoryReader` | no existen | K0 |
| [E15] «Columnas» en la URL | guarda lo que difiere del valor por defecto | nada |
| Núcleo: la ficha del usuario | `src/areas/business/users/pages/UserDetailPage.tsx`, sin pestañas; `Page` ya las trae (`tabs`), y ningún módulo aporta a la ficha | F3 suma el registro neutral |
| Núcleo: `OrganizationUserRemover` | corre en la UoW de `RemoveAsync`, sin punto de extensión | B5 |
| Núcleo: otras formas de terminar | `Member.Remove` tiene solo los tres llamadores de B5; cerrar la organización o deshabilitar a alguien no terminan membresías | nada |
| Revocar a quien ya tenía cuenta | no llama a `Remove`: el miembro queda `Invited`, sin invitaciones pendientes | precisión 9 |
| Miembros con un permiso | los roles guardan sus permisos en `text[]` | B10 cuenta en SQL |

## Contrato

Las rutas son de `/api/organization/forms`, con `[Access(Access.Business)]` y `[FeatureGate("forms")]`, y cuentan en las cuotas generales. Toda ruta responde además 401, 403, 404 con la feature apagada y 400 `Validation.Failed`; los listados paginan con `PagedRequest`. Cuerpos y tipos: los de la spec, con estas precisiones.

| Método y ruta | Permiso | Idempotencia y versión | Errores propios |
| --- | --- | --- | --- |
| `GET /person-data` | `formpersondata.manage` | — | — |
| `POST /person-data` → 201 | `formpersondata.manage` | `[Idempotent]` | `TooMany`, `NameDuplicate`, `KeyInvalid`, `KeyDuplicate`, `SourceInvalid` |
| `PUT /person-data/{id}` → `{version}` | `formpersondata.manage` | `version` | `NotFound`, `TooMany`, `NameDuplicate`, `SourceInvalid`, `InUse`, `ValueRemovalNotConfirmed` |
| `GET /person-data/catalogs` | `formpersondata.manage` | — | — |
| `GET /person-data/values` | `formpersondata.manage` y `users.read` | — | — |
| `GET /person-data/options` | `forms.manage` | — | — |
| `GET /person-data/{id}/options` | `users.manage` | — | `NotFound` |
| `GET /person-data/members/{memberId}` | `users.manage` | — | `MemberNotFound` |
| `PUT /person-data/members/{memberId}` → `{version}` | `users.manage` | `version`, nula sin fila | `MemberNotFound`, `ValueInvalid` |
| `POST /catalogs/{id}/offer-previews` | `formcatalogs.read` | no escribe | `Forms.Catalog.NotFound` y `SampleMemberNotFound` [E12], `Forms.Draft.RecipientFilterInvalid` |
| `POST /catalogs/{id}/offer-maximums` | `formcatalogs.read` | no escribe | `Forms.Catalog.NotFound`, `Forms.Draft.RecipientFilterInvalid` |

`/person-data` va en `BusinessFormPersonDataController`, y las dos de catálogos, en `BusinessFormCatalogsController` [E12]. Los errores sin prefijo son `Forms.PersonData.*`. Un 409 de versión es `General.ConcurrencyConflict`. Las rutas que cambian son las de la spec: `fields[].role`, `questions[].recipientFilters`, `warnings[]` en `publication-checks`, los motivos nuevos de `offerProblems` y `role` en las columnas y el detalle.

**Precisiones del contrato** (técnicas, decididas acá):
1. **Valores:** `values` es `{<id del dato>: <texto o id de la opción>}`; al leer, cada uno es `{value, label, inactive?}`, con `inactive` para una opción dada de baja. `status`: `all`, `active` o `inactive` en `/person-data`, y el de la persona, como en Usuarios, en `/values`.
2. **`GET /person-data/catalogs`** suma `optionsCount`, como el combo de la E13 (111).
3. **Sin `forms.read`,** `usedBy` llega nulo y `InUse.uses` no nombra formularios ni preguntas (lista de control, punto 6).
4. **El papel «Filtro»** vale para cualquier tipo de campo, como en `FRM-Catalogo-16`. Un valor fijo se valida con `FormCatalogFieldValuePolicy` [E13]: en un campo «De un catálogo», una opción activa de su fuente. «Según quien lo recibe» pide un campo «De un catálogo» y un dato con la misma fuente.
5. **Publicar:** cada aviso `PeopleWithoutData` suma `dataName` («2 personas sin Zona»). Con filtros fijos y sin filtros según quien lo recibe, `CatalogTooLarge` cuenta las opciones que deja el filtro y trae `count` y `limit`.
6. **Bloqueos, siempre en este orden:** al definir, las definiciones de la organización (con `AdvisoryLockExtensions`), el dato y su fuente; al cargar valores, los datos por id, la fila de la persona y las fuentes.
7. **El máximo por persona** se cuenta en SQL: las opciones que dejan los filtros fijos, agrupadas por los valores de los campos según quien lo recibe, entre las combinaciones que tienen las personas activas con `formrecords.create`. `maxLabel` une los textos de esa combinación con «, ».
8. **El resumen** «6 personas · 2 sin Zona» sale de `totalCount` de `values` sin búsqueda y de `valueCount` de cada dato activo de un catálogo.
9. **Fin de una membresía:** `IMembershipEndParticipant` corre en la UoW y el alcance del llamador. Al revocar, avisa cuando el miembro queda `Invited` sin invitaciones pendientes, tenga cuenta o no.
10. **Columnas:** la planilla oculta por defecto las subcolumnas de un campo con `role: Filter`.

**Errores** (en `Domain/Forms/`, textos en `FormsTexts.resx` y `.en.resx`):

| Código | Tipo y campo | es | en |
| --- | --- | --- | --- |
| `Forms.PersonData.NotFound` | NotFound | No encontramos ese dato. | We couldn't find that data item. |
| `Forms.PersonData.MemberNotFound` | NotFound | Esa persona no es de la organización. | That person isn't in the organization. |
| `Forms.PersonData.TooMany` | Conflict | Llegó a 20 datos, el máximo de una organización. | You reached 20 data items, the limit for an organization. |
| `Forms.PersonData.NameDuplicate` | Validation (`name`) | Ya hay un dato con ese nombre. | There's already a data item with that name. |
| `Forms.PersonData.KeyInvalid` | Validation (`key`) | Usá minúsculas, números o «_», empezando con una letra, hasta 40. | Use lowercase letters, numbers or "_", starting with a letter, up to 40. |
| `Forms.PersonData.KeyDuplicate` | Validation (`key`) | Otro dato ya usa esa clave. | Another data item already uses that key. |
| `Forms.PersonData.SourceInvalid` | Validation (`catalogId`) | Elegí un catálogo activo escrito a mano. | Choose an active manual catalog. |
| `Forms.PersonData.InUse` | Conflict (`uses`) | Se usa en un formulario: no se puede cambiar su tipo o catálogo ni darlo de baja. | A form uses it: you can't change its type or catalog or deactivate it. |
| `Forms.PersonData.ValueRemovalNotConfirmed` | Validation (`type`, con `count`) | Confirmá que se borran los valores. | Confirm that the values will be deleted. |
| `Forms.PersonData.ValueInvalid` | Validation (`values.<id>`) | Este valor no es válido. | This value isn't valid. |
| `Forms.Draft.RecipientFilterInvalid` | Validation (`…recipientFilters[j]` o `filters[j]`) | Revisá este filtro: el campo, el valor o el dato ya no sirven. | Check this filter: its field, value or data item no longer works. |
| `Forms.Draft.RecipientFilterForCustomers` | Validation (`questions[i]`) | Un formulario solo para clientes no puede filtrar según quien lo recibe. | A customers-only form can't filter by recipient. |
| `Forms.Offer.RecipientDataMissing` | Conflict (`dataName`) | A quien lo recibe le falta un dato. | The recipient is missing a data item. |
| `Forms.Offer.FilterUnavailable` | Conflict | El filtro dejó de funcionar. Avisale a quien administra los formularios. | The filter stopped working. Tell whoever manages the forms. |
| `Forms.Offer.NotForCustomers` | Conflict | No se le puede mandar a un cliente: una pregunta depende de un dato de la persona. | It can't be sent to a customer: a question depends on a person's data. |

`SourceInUse` suma los datos activos; `FieldInUse` suma `filters` al final de su lista `uses`, e `ItemInUse`, `forms` y `people`, en ese orden, al final de la suya. Debajo de la opción, `forms` dice «No se puede borrar: la usa un formulario. Podés darla de baja.» ("It can't be deleted: a form uses it. You can deactivate it.") y `people`, «No se puede borrar: la tiene una persona en sus datos. Podés darla de baja.» ("It can't be deleted: a person has it in their data. You can deactivate it.").

**Permiso nuevo:** `formpersondata.manage`, «Administrar los datos de las personas» / "Manage people data": «Definir los datos de las personas, como «Zona».» / "Define people data, such as Zone.". Va en el seed de `TenantAdmin` y en la migración de los roles de sistema.

### K0 · Lo que dejó la etapa anterior

El chat del contrato la confirma al arrancar y anota cada diferencia en «Cambios al contrato».
- **Base de la 12:** `UserDetailPage.tsx` con «Agregar empresa» y sin «Estado» (190); los botones con aro y gerundio; `ConfirmDialog`.
- **Base de la 12, segunda parte (214):** `disabledReason` con un toque (211), para «Agregar dato» con 20 y «Agregar filtro ▾»; su área en los permisos de un rol (260); el texto secundario de `DataTable` (206); `PersonalDataPurgeTests` (0062).
- **E12:** la exportación de ejemplos a `docs/contracts/examples/forms.json` y su guarda; `FormCatalog.cs`, `FormCatalogConfiguration.cs`, `FormCatalogService.cs`, `IFormCatalogRepository.LockAsync(ids)`, `FormCatalogReader.cs`, `FormCatalogFiltersPolicy.cs`, `UniqueKeys.cs` del módulo, `BusinessFormCatalogsController.cs`, `POST /catalogs/previews` en `FormCatalogSourceService` y `FormCatalogPreviewBuilder`, y `Forms.Catalog.SampleMemberNotFound`; `IMemberDirectoryReader` y `MemberDirectoryReader`; `test-e2e-real-forms.mjs` y `recorrido-usuario-forms.mjs`.
- **E13:** `FormCatalogField.cs`, `FormCatalogFieldEditor.cs` y `FormCatalogFieldValuePolicy.cs`; `FieldsJson` y `FieldValuesJson`, un mapa de clave a valor canónico con GIN `jsonb_path_ops`; `Forms.Catalog.SourceInUse`; en el front, `components/catalog/CatalogFieldsDialog.tsx`, `CatalogCombo.tsx` y `F/src/shared/ui/ColumnsMenu.tsx`.
- **E14:** las preguntas del borrador (`fieldId`, `type`, `catalogId?`, `presentation?`) en `FormDraft.cs`, `FormDraftEditor.cs` y `FormVersionCreator.cs`; `FormRecordWriter.cs`, que sella los campos; `FormOfferBuilder.cs`, que lee una fila más que el tope; `offerProblems` y `StructuredFormChoice.OfferProblems`; `publication-checks` con `issues[]` y `changes[]`, sin `warnings`; `FormPublicationService.cs`; `IFormReader.cs`; `FormOfferErrors.cs`; `Forms.Catalog.FieldInUse` con su lista `uses` (`presentation`) y `forms[]`, nulo sin `forms.read`, e `ItemInUse` con `records` en su lista `uses`; `CatalogCombo.tsx` con el origen, el estado y la consulta como parámetros; `docs/guides/quitar-formularios.md`; el Meta simulado de B12; en el front, `PublishDialog.tsx`, `CatalogQuestionFields.tsx`, con la zona plegada (145), y `SendFormDialog.tsx`, en `src/modules/whatsapp/chats/components/`.
- **E15:** `FormRecordColumnsBuilder` con las claves `field:<fieldId>:<clave>`, `FormRecordSheetBuilder` con todas o las visibles, la ruta de columnas con `catalogField?{key, name}`, `FormRecordsPage.tsx` y `RecordValues.tsx`, y «Columnas» en la URL con su valor por defecto por columna.

### K1 · Publicar

El Back publica el contrato y regenera `docs/contracts/openapi.json`; el Front corre `npm run contracts`, sin tipos a mano. Cada tarea del Back graba con la exportación de la E12 los ejemplos reales de sus respuestas nuevas o cambiadas, con errores y vacíos, en `docs/contracts/examples/forms.json`.

**Texto para el chat de Contrato**

```text
Leé AGENTS.md, docs/plans/2026-11-08-etapa-16-datos-de-las-personas.md y la spec 16 con su parte. Hacé K0: confirmá la lista contra el código y anotá cada diferencia en «Cambios al contrato». Después K1: publicá rutas, permiso, errores con sus textos es/en y precisiones, generá openapi.json y avisá a la Guía para el npm run contracts del front. No programes la lógica. Reglas: todo en main, sin ramas, worktrees, stash, reset ni push; commiteá solo lo tuyo, con git commit -- <rutas>, en español y conventional commits; con la app levantada nadie edita el src del front; TDD donde hay lógica; al terminar cada tarea, build y tests rápidos enteros, sin Docker ni filtrar por módulo, y los de integración y seguridad del área si tocó rutas, persistencia, autenticación o aislamiento; el contrato no cambia sin anotarlo en «Cambios al contrato»; ninguna clave real pasa por vos; al usuario, solo lo de producto, por la Guía.
```

## Back

Cada servicio público va en `OperationLog.RunAsync`, sin valores ni opciones en los logs; cada método que escribe abre una sola UoW (bloqueos, lecturas, reglas y escrituras). El aviso de fin de membresía es un puerto neutral, y cada ruta nueva lleva su test de aislamiento.

1. **B1 · Dominio, errores y permiso (R: permisos).** `D/Forms/FormPersonDataDefinition.cs`, `FormPersonDataValues.cs`, `FormRecipientFilter.cs` y `FormPersonDataErrors.cs`; suma los errores del borrador y de la oferta a los de la E14 (`FormOfferErrors.cs`); el permiso en `Permissions.cs`, `Permissions{,.en}.resx`, con `Area.formpersondata`, «Datos de las personas» (*People data*), para su área en los permisos de un rol (260 y 257), sin nada incluido porque depende de otra área, y el seed. Copia `D/Forms/FormCatalog.cs` [E12]. Tests: 20 contando solo los activos (105); la clave, su formato, repetida y fija al guardar (130); `PermissionTextsTests`.
2. **B2 · Tablas y migraciones (R: datos y aislamiento).** `I/Modules/Forms/Persistence/Configurations/FormPersonDataDefinitionConfiguration.cs` y `FormPersonDataValuesConfiguration.cs`, con `UniqueKeys.FormPersonDataName` y `FormPersonDataKey`; migraciones `<fecha>_FormPersonData.cs` y `<fecha>_SystemRoleFormPersonDataPermissions.cs`, con el SQL en una línea y un `Down` que se niega si hay datos. Copia `FormCatalogConfiguration.cs` [E12] y `20261010203618_SystemRoleAssistantPermissions.cs`. Tests: RLS de las dos tablas; la migración, también hacia atrás; los roles existentes reciben el permiso.
3. **B3 · Definir (R: datos, por los bloqueos).** `A/Modules/Forms/Interfaces/Services/IFormPersonDataService.cs`, `A/Modules/Forms/Services/FormPersonDataService.cs` y `FormPersonDataEditor.cs`, con su repositorio y los bloqueos de la precisión 6. El uso de un dato y «Lo usan» los lee `FormPersonDataReader`, en el borrador y la versión activa. Copia `FormCatalogService.cs` [E12]. Tests: dos personas crean el dato 20 a la vez; en uso por el borrador y por la versión activa; reactivar el 21; `count` exacto al cambiar el tipo; sin `forms.read`, `InUse` sin nombres; la fuente de un dato activo no se da de baja (`SourceInUse`).
4. **B4 · Valores por delta (R: datos).** `FormPersonDataValueEditor.cs`, `IFormPersonDataReader.cs` e `I/Modules/Forms/Persistence/Readers/FormPersonDataReader.cs`: ausente conserva, `null` limpia, un texto hasta 300 y una opción activa de la fuente. Copia `FormCatalogFieldValuePolicy.cs` [E13]. Tests: delta y `null`; guardar sin cambios compara la versión y no toca ni audita valores ([concurrencia](../rules/concurrencia.md)); una opción dada de baja se conserva; dos guardados a la vez = 409; un miembro de dos organizaciones; la tabla paginada, sin correos ni miembros quitados.
5. **B5 · Fin de una membresía (R: datos).** `A/Interfaces/Integrations/Legal/IMembershipEndParticipant.cs` y su contexto; suma el aviso a `OrganizationUserRemover.cs`, `OrganizationUserAccessRemover.cs` y `MembershipDeletionParticipant.cs` (precisión 9); `A/Modules/Forms/Services/FormsMembershipEndParticipant.cs`, que borra la fila de valores; se registra en `FormsModule`. `FormPersonDataValues` se inscribe en `PersonalDataPurgeTests`: la purga el fin de la membresía. Copia `IAccountDeletionParticipant.cs`. Tests: sacar a alguien, revocar la invitación de quien no entró (con cuenta y sin ella) y la baja de la cuenta borran los valores en la misma UoW; sin el módulo, nada; `ModuleIsolationTests`.
6. **B6 · El papel de un campo.** Suma `role` a `FormCatalogField.cs` y `FormCatalogFieldEditor.cs` [E13], a lo que sella `FormRecordWriter.cs` [E14] y a `FormRecordColumnsBuilder.cs` [E15]. Copia esos. Tests: un campo que usa un filtro no se quita ni cambia de papel, tipo o fuente (lección 1); una respuesta vieja se lee como `Data`; «Todas las preguntas» trae los campos de filtro.
7. **B7 · Filtros en el borrador.** `A/Modules/Forms/Services/FormRecipientFilterPolicy.cs`; suma `recipientFilters` a `FormDraft.cs`, `FormDraftEditor.cs` y `FormVersionCreator.cs` [E14], con los nombres en la lectura. Copia `FormCatalogFiltersPolicy.cs` [E12]. Tests: cada motivo de `RecipientFilterInvalid`; el valor fijo contra su fuente (lección 3); editar conserva el `FieldId`; cambiar un filtro es una pregunta cambiada en `changes[]` (lección 6).
8. **B8 · La oferta filtrada, en SQL (R: lo que sale a WhatsApp).** Suma a `FormOfferBuilder.cs` [E14] y `FormCatalogReader.cs` [E12]: resuelve al miembro real, filtra con `FieldValuesJson @>` antes del tope y lee una fila más; un cliente con un filtro según quien lo recibe da `NotForCustomers`. Copia esos. Tests: dos zonas; sin el dato; un valor que no es de la fuente actual; un filtro roto; un cliente con filtros fijos y con uno según quien lo recibe; el operador ve el motivo y la cantidad, nunca las opciones; nunca la lista entera; 201 opciones; 10.000 opciones con EXPLAIN; la vista oficial con los fijos y sin los según quien lo recibe (176).
9. **B9 · La vista previa y el máximo.** Las dos rutas de catálogos, en `FormCatalogSourceService` y `FormCatalogPreviewBuilder` [E12] y en `FormPersonDataReader.cs`, con la cuenta de la precisión 7. Copia `POST …/previews` [E12]. Tests: `Ready`, `RecipientDataMissing` y `FilterUnavailable`; no escribe ni toma bloqueos; sin `formcatalogs.read`, 403 (lección 4); una persona de otra organización; el máximo y las personas sin el dato, con los filtros sin guardar, iguales a los avisos de publicar.
10. **B10 · Publicar: problemas y avisos.** Suma a `FormPublicationService.cs` [E14] los `warnings` y los problemas nuevos, y a `IMemberDirectoryReader.cs` y `MemberDirectoryReader.cs` [E12] los miembros activos con un permiso, con la cuenta de B9. Copia esos. Tests: un filtro fijo que deja más de 20 en «elegir varias» frena (lección 2); con un filtro según quien lo recibe, un catálogo de más de 200 se publica; el máximo, solo si pasa el límite o da cero (157); personas sin el dato; para clientes frena y para los dos avisa.
11. **B11 · Una opción en uso no se borra.** Suma `forms` y `people` a la lista `uses` de `ItemInUse`, en el guardado de `FormCatalogService.cs` [E12], leídos con `IFormReader` [E14] y `FormPersonDataReader`. Copia `uses: records` de la E14. Tests: un filtro fijo del borrador y uno de la versión activa; una persona que la tiene; las dos se dan de baja.
12. **B12 · Rutas y contratos (R: permisos).** `P/Modules/Forms/Controllers/BusinessFormPersonDataController.cs`, `Contracts/*`, las dos rutas en `BusinessFormCatalogsController.cs` [E12] y los ejemplos en `FormsContractContributor.cs`; `AuditSummary` de las dos tablas en `Audit{,.en}.resx`, con su línea en `quitar-formularios.md`; las relaciones en `GrowingCollectionContractTests`. Copia `BusinessFormCatalogsController.cs` [E12]. Tests: cada ruta con 401, 403, feature apagada, validación, 409 y paginado; `values` sin uno de los dos permisos da 403; ejemplos exportados.

**Texto para el chat del Back**

```text
Leé AGENTS.md, docs/plans/2026-11-08-etapa-16-datos-de-las-personas.md, la spec 16 con su parte y las fichas de su «Qué leer». Después de K1, hacé B1 a B12 en orden con sus moldes, un test con dos actores por bloqueo y los ejemplos de tus respuestas; las tareas (R) llevan la revisión adversarial antes del commit; no toques el front ni la tabla de estado del plan. Reglas: todo en main, sin ramas, worktrees, stash, reset ni push; commiteá solo lo tuyo, con git commit -- <rutas>, en español y conventional commits; con la app levantada nadie edita el src del front; TDD donde hay lógica; al terminar cada tarea, build y tests rápidos enteros, sin Docker ni filtrar por módulo, y los de integración y seguridad del área si tocó rutas, persistencia, autenticación o aislamiento; el contrato no cambia sin anotarlo en «Cambios al contrato»; ninguna clave real pasa por vos; al usuario, solo lo de producto, por la Guía.
```

## Front

Todo va en `src/modules/forms`, con i18n es/en, los tipos generados, TanStack Query y axe; el núcleo suma solo las pestañas de la ficha. Lo que se elige de un catálogo usa `CatalogCombo.tsx` [E13], sin otro selector (lección 5).

1. **F1 · Tipos y llamadas.** Suma a `F/src/modules/forms/api/`. Copia la API de la E15. Tests: zod estricto con los ejemplos exportados.
2. **F2 · «Datos de las personas».** `pages/PersonDataPage.tsx`; suma a `registration.ts` `formpersondata.manage` en sus permisos, la ruta `/org/datos-de-las-personas`, en Administración, con ese permiso y la feature, y sus recursos; pasa a `CatalogCombo.tsx` [E13 y E14] la fuente, `GET /person-data/catalogs`. Tableros `FRM-Datos-Personas` y `M-FRM-Datos-Personas`. Tests: el combo sin `formcatalogs.read`; sin `users.read`, la línea que lo dice; sin `users.manage`, sin ✎; «De baja» en la columna «Estado», nunca debajo del nombre (206); el tope de 20 con su motivo, que en el teléfono se abre con un toque (211); en uso, grisado; confirmar el borrado de valores; buscar y ninguna coincide; axe.
3. **F3 · La pestaña «Datos».** `components/MemberDataTab.tsx`; suma `userDetailTabs` a `src/shared/modules/types.ts`, su lectura a `useModules.ts` y las pestañas a `UserDetailPage.tsx`, con `Page`. Tableros `FRM-Usuario-Datos` y `M-FRM-Usuario-Datos`. Tests: sin un módulo que aporte, no hay pestañas; se ve con `users.manage`; guarda aparte, solo si cambió; salir con cambios pregunta; un error queda en la pestaña; «De baja» en una opción dada de baja; `composition`.
4. **F4 · «Papel» y los motivos de borrar.** Suma a `CatalogFieldsDialog.tsx` [E13], y a `OptionsSection.tsx` y `OptionsDialog.tsx` [E12] los motivos `forms` y `people` debajo de la opción, como el de la E14. Tableros `FRM-Catalogo-16` y `M-FRM-Catalogo-16`. Tests: en uso, el motivo debajo; «De un catálogo · Filtro» en la lista; los dos motivos nuevos.
5. **F5 · «Qué opciones ve cada persona».** Suma a `CatalogQuestionFields.tsx` [E14]. Tableros `FRM-Editor-16` y `M-FRM-Editor-16`. Tests: un solo «Agregar filtro ▾», con sus dos opciones y sus motivos; fijo y según quien lo recibe; la zona plegada, con su línea, que se abre sola con un error o un aviso (145); la vista previa en dos columnas (138), el máximo y las personas sin el dato se actualizan solos; sin `formcatalogs.read`, sin vista previa; «Cargar los datos» solo con `formpersondata.manage`.
6. **F6 · Publicar, enviar y «Columnas».** Suma a `PublishDialog.tsx` y `SendFormDialog.tsx` [E14] y a `FormRecordsPage.tsx` [E15]. Tableros `FRM-Publicar-16`, `WA-Chats-Formulario-16`, `M-WA-Chats-Formulario-16`, `FRM-Respuestas-16` y `M-FRM-Respuestas-16`. Tests: la zona «Avisos» no frena; el máximo, solo si no entra o da cero (157); el motivo al mandar, con el dato; las subcolumnas de un filtro, apagadas; el teléfono sin «Columnas».

**Texto para el chat del Front**

```text
Leé AGENTS.md del front, ../ArquitecturaBaseMutitenant/docs/plans/2026-11-08-etapa-16-datos-de-las-personas.md y la spec 16. Esperá K1, corré npm run contracts y hacé F1 a F6 con los tipos generados y los tableros v175. No toques el lienzo, las specs, el back ni la tabla de estado del plan; fuera de src/modules/forms, solo las pestañas de F3 y SendFormDialog.tsx. Por tarea, npx tsc -b y npm test; al cerrar, build, lint y contracts:check. Reglas: todo en main, sin ramas, worktrees, stash, reset ni push; commiteá solo lo tuyo, con git commit -- <rutas>, en español y conventional commits; con la app levantada nadie edita el src del front; TDD donde hay lógica; al terminar cada tarea, build y tests rápidos enteros, sin Docker ni filtrar por módulo, y los de integración y seguridad del área si tocó rutas, persistencia, autenticación o aislamiento; el contrato no cambia sin anotarlo en «Cambios al contrato»; ninguna clave real pasa por vos; al usuario, solo lo de producto, por la Guía.
```

## Integración y puerta

1. **I1 · Integrar.** El front contra la API real; cada diferencia vuelve a su dueño, anotada en «Cambios al contrato».
2. **I2 · Puerta.** `/puerta` una vez, sobre copias del corte: build y `dotnet test` del back; build, lint, test y `contracts:check` del front; el E2E real (`test-e2e-real-forms.mjs`) con el Meta simulado de la E14 (definir «Zona», cargarla, armar el filtro, publicar con avisos y mandar a quien tiene el dato y a quien no); `realinear-docs` y `estado-docs` en 0; la galería (`scripts/capturas/etapa-16.json`) a 1440, 390 y 320, solo de `FRM-Datos-Personas`, `FRM-Usuario-Datos`, `FRM-Catalogo-16`, `FRM-Editor-16`, `FRM-Publicar-16`, `WA-Chats-Formulario-16`, `FRM-Respuestas-16` y sus teléfonos; y la lista de control, con un test por punto que aplica (2 a 10). El retiro del módulo no corresponde.
3. **I3 · Recorrido y cierre.** `/recorrido` (`recorrido-usuario-forms.mjs`) con `una-zona-para-cada-persona`, más sacar a una persona y revocar la invitación de otra con «Zona» cargada, con sus valores borrados. **Pendiente para la I1 de la 10:** la prueba con Meta de la spec (dos zonas, una persona sin el dato y una persona de dos organizaciones). Al cerrar, `docs/features/formularios.md` suma la sección, y `docs/features/organizaciones.md`, el aviso de fin de membresía; cada carpeta nueva trae su `AGENTS.md` y `CLAUDE.md` desde que nace.

| Puerta | Resultado |
| --- | --- |
| Back / Front / Contrato / E2E / Documentación / Galería / Recorrido | Pendiente |
| Meta | Pendiente para la I1 de la 10 |

**Texto para el chat de Integración**

```text
Leé AGENTS.md de los dos repos, docs/plans/2026-11-08-etapa-16-datos-de-las-personas.md y la skill /puerta. Hacé I1 a I3: integrá, devolvé cada diferencia a su dueño y corré la puerta una vez sobre copias del corte; después, solo lo que toca cada arreglo. Meta queda para la I1 de la 10. No toques el lienzo ni manejes la PC del usuario; en el plan, solo la tabla final. Reglas: todo en main, sin ramas, worktrees, stash, reset ni push; commiteá solo lo tuyo, con git commit -- <rutas>, en español y conventional commits; con la app levantada nadie edita el src del front; TDD donde hay lógica; al terminar cada arreglo, build y tests rápidos enteros, sin Docker ni filtrar por módulo, y los de integración y seguridad del área si tocó rutas, persistencia, autenticación o aislamiento; el contrato no cambia sin anotarlo en «Cambios al contrato»; ninguna clave real pasa por vos; al usuario, solo lo de producto, por la Guía.
```

## Relación con otras etapas

**Usa de las anteriores:** la lista de K0, con `forms.read`, `forms.manage`, `formcatalogs.read`, `formrecords.create`, `users.read` y `users.manage`; y del núcleo, `OrganizationUserRemover`, `OrganizationUserAccessRemover`, `MembershipDeletionParticipant`, `IAccountDeletionParticipant`, `AdvisoryLockExtensions`, `Page` con `tabs`, `src/shared/modules/types.ts` y `useModules.ts`.

**Deja para las siguientes:**
- **Back:** las tablas `FormPersonDataDefinitions` y `FormPersonDataValues`; `FormRecipientFilter` y `recipientFilters` en el borrador y la versión; `FormRecipientFilterPolicy`; `FormOfferBuilder` con los filtros en SQL; `warnings[]` en `publication-checks`; `IFormPersonDataReader`, también con «Lo usan» de un dato; `IMembershipEndParticipant`; el papel `Filter` de un campo; `formpersondata.manage`; los errores `Forms.Offer.RecipientDataMissing`, `FilterUnavailable` y `NotForCustomers`; y `offer-previews` y `offer-maximums`.
- **Front:** `PersonDataPage.tsx`, `MemberDataTab.tsx`, `userDetailTabs`, la zona «Avisos» de `PublishDialog.tsx`, «Qué opciones ve cada persona» en `CatalogQuestionFields.tsx` con un solo «Agregar filtro ▾», y «Papel» en `CatalogFieldsDialog.tsx`.

## Cambios al contrato


## Para decidir

Nada.
