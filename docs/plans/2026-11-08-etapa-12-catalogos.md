# E12 · Catálogos · plan ejecutable

**Estado:** pendiente · Spec: `docs/plans/formularios/12-catalogos.md`, con `12-parte-datos-y-contrato.md` y `12-parte-archivos-tests-y-referencia.md`. Tableros del lienzo v175. No queda nada de producto para decidir.

| Parte | Tareas | Estado |
| --- | --- | --- |
| Contrato | K0–K1 | Pendiente |
| Back | B1–B9 | Pendiente |
| Front | F1–F8 | Pendiente |
| Integración y puerta | I1–I3 | Pendiente |

## Forma de trabajo

Modo rápido: contrato primero; después Back y Front a la vez, un chat por repo; una sola puerta al final ([etapa en paralelo](../guides/etapa-en-paralelo.md)). Las reglas de `AGENTS.md` van enteras en el texto de cada chat. Un arreglo corre solo lo que toca. La tabla de estado la actualiza la Guía. No hay prueba con Meta: la etapa no manda nada por WhatsApp.

## Qué se verificó al arrancar

| Fila | Qué se encontró | Qué cambia |
| --- | --- | --- |
| Base de la 12 | `PageLoading`, `FormSkeleton`, `InfoTip`, `pending` de `ConfirmDialog` y el «Filtros» único de `FilterBar` no existen: los deja la base | K0 |
| [E11] Contactos | `WhatsAppCustomer.Name` es opcional y `Tags`, `text[]` con GIN | nada |
| [E11] Nombre del perfil | Nadie lee `contacts[].profile.name`, y el contacto nace sin nombre | B5, con `WhatsAppWebhookReader` y `WhatsAppWebhookWriter`, que la §9 no nombra |
| [E11] Planilla | `IWhatsAppContactSpreadsheet` (ClosedXML) existe | se copia |
| [E9] Cuotas y Redis | `RateLimitMiddleware` aplica `ip`, `identity` y `tenant` | sin perfil propio ni caché |
| #51 | `ITenantScope.Enter` deja el `TenantKind` en null | sin workers: la barrera vence sola |
| Modelos | Existen; `AssistantErrorTextSource` vive en `AssistantTexts.cs` | se copian |
| `IdentityAccessTests` | Los lectores de la cuenta se listan en `IdentityStorageAccessTests.AllowedReaders` | `MemberDirectoryReader` va en las dos guardas |
| Permisos | El Dueño recibe `Permissions.All`, y `CompanyAdmin` no | solo el Dueño |
| Ejemplos de una corrida real | Solo `docs/contracts/examples/identity.json`, a mano; [api-http](../rules/api-http.md) da la exportación y su guarda a la E12 | B8 y F2 |
| `PersistenceNamingTests` | No existe; [persistencia-ef](../rules/persistencia-ef.md) lo da a la E12 | B9 |
| Tableros | Piden datos que la §6 de la spec no trae | los cuerpos de abajo |

## Contrato

### Rutas

Prefijo `/api/organization/forms/catalogs`, con `{id:guid}`. Todas llevan `[Access(Access.Business)]`, `[FeatureGate("forms")]` y `[HasPermission]`, y cuentan en las cuotas generales, sin perfil propio. Todas responden 401, 403 y 404 (feature apagada o catálogo ajeno, igual que inexistente). Los listados usan `PagedRequest` y `PagedResult` (10 por defecto; 20, 50 o 100), con `search` y orden estable.

| Método y ruta | Permiso | Además | Errores propios |
| --- | --- | --- | --- |
| GET `/` | `formcatalogs.read` | `sort` (`name`, `updatedAtUtc`), `source`, `status` | 400 |
| GET `filter-counts` | `formcatalogs.read` | `search` | 400 |
| POST `/` | `formcatalogs.manage` | `[Idempotent]`; 201 con Location | 400: validación, `SampleRequired`, `FiltersInvalid`, `SampleMemberNotFound`, `SampleContactNotFound`, `DuplicateCode`, `ValueRequired`, `ValueInvalid`, `ItemTextTooLong`, `ItemDetailTooLong`; 409: `WrongSource`, `TooManyItems`, `Request.InProgress`; 422 |
| GET `{id}` | `formcatalogs.read` | — | 404 `NotFound` |
| PUT `{id}` | `formcatalogs.manage` | `version` | los 400 del alta, `deleteItems[i]` y `sort` (224); 404 `ItemNotFound`; 409: `General.ConcurrencyConflict`, `ItemInUse`, `TooManyItems`, `MoveOutOfRange`, `Inactive`, `WrongSource` |
| GET `{id}/items` | `formcatalogs.read` | `status` (`active`, `inactive`, `all`) | 400 |
| POST `{id}/imports/previews` | `formcatalogs.manage` | multipart; barrera; no escribe | 400 `FileInvalid`; 409: `Busy`, `General.ConcurrencyConflict`, `Inactive`, `WrongSource`; 413; 429 `TooManyAtOnce` |
| POST `{id}/imports` | `formcatalogs.manage` | multipart; `[Idempotent]`; barrera; `version` | 400: `FileInvalid`, `ConfirmationRequired`; 409: `FileChanged`, `General.ConcurrencyConflict`, `Busy`, `Inactive`, `WrongSource`, `TooManyItems`, `Request.InProgress`; 413; 422; 429 `TooManyAtOnce` |
| GET `{id}/export` | `formcatalogs.read` | barrera; `.xlsx` | 409: `Busy`, `WrongSource`; 429 `TooManyAtOnce` |
| GET `template` | `formcatalogs.read` | `valueType`; barrera; `.xlsx` | 400; 409 `Busy` |
| GET `sources/members` | `formcatalogs.manage` | — | — |
| GET `sources/members/roles` | `formcatalogs.manage` | `sort` (`name`) | 400 |
| GET `sources/members/companies` | `formcatalogs.manage` | `sort` (`name`) | 400 |
| GET `sources/contacts/tags` | `formcatalogs.manage` | — | 400 |
| GET `sample-members` | `formcatalogs.read` | — | 400 |
| GET `sample-contacts` | `formcatalogs.read` | — | 400 |
| POST `previews` | `formcatalogs.read` | no escribe | 400: `FiltersInvalid`, `SampleRequired`, `SampleMemberNotFound`, `SampleContactNotFound` |

### Cuerpos

Valen los de la §6 de la spec, con estos cambios que piden los tableros:
- **Detalle:** suma `modifiedAtUtc`, `modifiedBy?` y `itemCounts: {total, active}`, que pide el conflicto («Laura Ríos lo guardó…»), y `sample?: {id, name, detail?}`.
- **La persona o el cliente de ejemplo se guarda** en `Filters` (`sampleId`), como lo muestra el tablero al volver a abrir; sin guardarlo, `SampleRequired` lo pediría en cada guardado. `POST` y `PUT` lo reciben como `sampleMemberId` o `sampleContactId`.
- **Fila del listado:** `optionsCount` es el total de opciones en uno a mano, activas y dadas de baja; en uno de miembros, los que cumplen los filtros, sin «Sin incluir a quien lo recibe»; en uno de contactos o que no se puede usar, `null` («—» en `FRM-Catalogos`).
- **Fila de opción:** suma `textRepeated` (otra opción del catálogo tiene el mismo texto, sin distinguir mayúsculas).
- **`sample-members`:** `{memberId, name, companies: string[], roles: string[]}`. **`sources/contacts/tags`:** `{name, contacts}`.
- **Vista previa de importar:** `{version, sha256, sheetName?, rowsRead, unchanged, deactivationShare, errorShare, confirmDeactivations, confirmErrors, errors, added, changed, deactivated}`. Las cuatro listas son `PagedResult` con el `page` y el `pageSize` del pedido: `errors {row, column?, code, value?, otherRow?}`, `added {row, code, text}`, `changed {row, code, column, before, after}` y `deactivated {code, text}`. Las proporciones son fracciones.
- **Aplicar:** `confirmedDeactivations` es la cantidad exacta que se confirmó.
- **`POST previews`:** `total` es la cantidad exacta; con más de 200, la cuenta un `COUNT` con el mismo timeout.

### Errores

Van en `Domain/Forms/FormCatalogErrors.cs` y `FormCatalogImportErrors.cs`, con sus textos en `FormsTexts.resx` y `.en.resx`. Solo `General.ConcurrencyConflict` pone el editor en conflicto.

| Código | HTTP y campo | es | en |
| --- | --- | --- | --- |
| `Forms.Catalog.NotFound` | 404 | No encontramos este catálogo. | We couldn't find this catalog. |
| `Forms.Catalog.ItemNotFound` | 404, `deleteItems[i]` o `moves[i]` | Esa opción no es de este catálogo. | That option isn't in this catalog. |
| `Forms.Catalog.ItemInUse` | 409, `deleteItems[i]`, con `uses` | No se puede borrar porque se usa. Podés darla de baja. | It can't be deleted because it's in use. You can deactivate it. |
| `Forms.Catalog.DuplicateCode` | 400, `items[i].code` | Ya está en la opción {n}. | Option {n} already has it. |
| `Forms.Catalog.TooManyItems` | 409 | Llegó a 10.000 opciones, el máximo de un catálogo. | It reached 10,000 options, the maximum for a catalog. |
| `Forms.Catalog.ValueRequired` | 400, `items[i].value` | Escribí el valor. | Enter the value. |
| `Forms.Catalog.ValueInvalid` | 400, `items[i].value` | Escribí un número entero. / Escribí un número. / Escribí una fecha. | Enter a whole number. / Enter a number. / Enter a date. |
| `Forms.Catalog.MoveOutOfRange` | 409 | No se puede mover más. | It can't move any further. |
| `Forms.Catalog.Inactive` | 409 | El catálogo está dado de baja. Reactivalo para cambiar sus opciones. | The catalog is deactivated. Reactivate it to change its options. |
| `Forms.Catalog.WrongSource` | 409 | Eso no corresponde al origen de este catálogo. | That doesn't match this catalog's source. |
| `Forms.Catalog.SampleRequired` | 400, `sampleMemberId` | Elegí una persona. | Choose a person. |
| `Forms.Catalog.FiltersInvalid` | 400, `conditions[i]` | Revisá esta condición. | Check this condition. |
| `Forms.Catalog.SampleMemberNotFound` | 400, `sampleMemberId` | Esa persona ya no es de la organización. | That person is no longer in the organization. |
| `Forms.Catalog.SampleContactNotFound` | 400, `sampleContactId` | Ese cliente ya no está en Clientes. | That customer is no longer in Customers. |
| `Forms.Catalog.ItemTextTooLong` | 400, `items[i].text` | Puede tener hasta 30 caracteres. | It can have up to 30 characters. |
| `Forms.Catalog.ItemDetailTooLong` | 400, `items[i].detail` | Puede tener hasta 300 caracteres. | It can have up to 300 characters. |
| `Forms.CatalogImport.FileChanged` | 409 | El archivo no es el que se revisó. Revisá de nuevo. | This isn't the file you reviewed. Review it again. |
| `Forms.CatalogImport.FileInvalid` | 400, `file` | El archivo no es un XLSX ni un CSV en UTF-8. / El archivo pasa los topes (C8). | The file isn't an XLSX or a UTF-8 CSV. / The file is over the limits. |
| `Forms.CatalogImport.ValueAmbiguous` | en la revisión, con `value` | «{value}» se lee de dos formas. Escribí {a} o {b}. | "{value}" can be read two ways. Write {a} or {b}. |
| `Forms.CatalogImport.CellRequired` | en la revisión | Falta el dato. | The value is missing. |
| `Forms.CatalogImport.ConfirmationRequired` | 400 | Confirmá las bajas o las filas con errores antes de aplicar. | Confirm the deactivations or the rows with errors before applying. |
| `Forms.CatalogImport.Busy` | 409 | Ya tenés una importación o una exportación en curso. Probá en un momento. | You already have an import or an export in progress. Try again in a moment. |
| `Forms.CatalogImport.TooManyAtOnce` | 429, con `Retry-After` | Hay muchas importaciones y exportaciones en curso. Probá en un momento. | There are too many imports and exports in progress. Try again in a moment. |

- **`uses` de `ItemInUse`** es una lista de motivos sin repetir, en el orden fijo de su enum: cada etapa suma el suyo al final, y el front muestra el primero.
- En una fila de importar, `DuplicateCode` dice «Está repetido en la fila {otherRow}.» («It's repeated in row {otherRow}.»), e `ItemTextTooLong` e `ItemDetailTooLong`, «Tiene más de {n} caracteres.» («It has more than {n} characters.»). `CellRequired` es nuevo.
- Los motivos de «No se puede usar» (§5) van en `enums.json` del front.

### Decisiones técnicas del contrato

- Los `POST` que crean o aplican llevan `[Idempotent]`, como `whatsapp/contacts/import`; las vistas previas, no.
- **Bloqueo del catálogo:** advisory, con `AcquireAdvisoryLocksAsync`, que ordena las claves; `IFormCatalogRepository.LockAsync(ids)` acepta varios.
- **Guardar toca siempre la fila del catálogo** (C3): sin cambios escribe solo `ModifiedAtUtc`, así EF compara la versión (409 común si difiere), como manda [concurrencia](../rules/concurrencia.md), sin tocar ni auditar opciones.
- **«Dar de baja» y «Reactivar»** mandan el mismo `PUT`, con `isActive` y lo pendiente.
- **Rol de un miembro:** cualquier asignación activa, de organización o de empresa. Un miembro sin nombre no se ofrece, como un contacto (C14).
- **Sin WhatsApp** (módulo no instalado o feature apagada), un catálogo de contactos queda en `ChannelUnavailable` y el origen no se ofrece.
- **El corte de la 36** vive en `Domain/Forms/FormOptionTitle.cs`: 29 caracteres y «…», con el nombre entero en el detalle (11 y 21).
- **Tres servicios**, por el tope de dependencias (ADR 0039): `IFormCatalogService`, `IFormCatalogImportService` e `IFormCatalogSourceService` (fuentes, ejemplos, etiquetas y vista previa).

### K0 · Lo que dejó la base

El chat del contrato confirma esta lista al arrancar. Lo que difiera se anota en «Cambios al contrato» antes de programar:
1. Front: el aro y su gerundio, y `ConfirmDialog` con `pending` y el foco en «Cancelar» (F1 y F13); `DataTable` con su primera carga y `refreshing`, y `FilterBar` con un solo «Filtros», chips y «Limpiar todo» (F3); las fechas completas (F4); la forma y la escala (F8); `InfoTip` y el motivo de lo apagado (F10); lo elegido (F11); `PageLoading` y `FormSkeleton` (F12); las capturas a 320 (F15).
2. Back: `PermissionAuthorizationHandler` con una lectura por requisito (B10).
3. La segunda parte de la base (214): `parseDecimal` con `numberAmbiguous` (207), `PercentText` pegado (208), los avisos (210), el tooltip que se abre con un clic o un toque, también en el teléfono (211), la matriz de roles (212) y las piezas de la 213.

### K1 · Publicar el contrato

Un solo chat, en el back:
- el esqueleto del módulo: `AddFormsModule` en las tres capas (`FormsModule` y `FormsApiModule`) y `Program.cs`, `FormsFeatureDefinitionContributor` (`new("forms", FeatureScope.Business, false)`) y los recursos con `FormsErrorTextSource`;
- la clase `Forms` de `Permissions.cs`, con sus textos en `Permissions.resx` y `.en.resx` solo con lo que existe en esta etapa: «Ver catálogos», «Ver los catálogos y sus opciones.» («View catalogs», "View catalogs and their options."), y «Administrar catálogos», «Crear y editar catálogos, importarlos y darlos de baja.» («Manage catalogs», "Create and edit catalogs, import them and deactivate them."), y su fila en la matriz de roles (212), `Area.formcatalogs`, «Catálogos» («Catalogs»); `<fecha>_SystemRoleFormCatalogPermissions.cs`, que copia `20261010203618_SystemRoleAssistantPermissions.cs`, solo para el Dueño;
- las rutas con 501 en `BusinessFormCatalogsController`, sus `*HttpRequest`, los errores con sus textos y `FormsContractContributor.cs`;
- `openapi.json` regenerado y las guardas de rutas en verde.

En el front, `npm run contracts`.

**Texto para el chat de Contrato**

```text
Leé AGENTS.md, docs/plans/2026-11-08-etapa-12-catalogos.md y la spec de la E12 con sus dos partes. Primero K0: confirmá la lista contra el código. Después K1, copiando el Asistente. Generá openapi.json y avisale a la Guía para el npm run contracts del front, que no tocás. Reglas: todo en main, sin ramas, worktrees, stash, reset ni push; commits propios con git commit -- <rutas>, en español y conventional; con la app levantada, nadie edita el src del front; TDD donde hay lógica; al terminar cada tarea, build y tests rápidos enteros, sin Docker ni filtrar por módulo, más integración y seguridad del área si tocó rutas, persistencia, autenticación o aislamiento; el contrato no cambia sin anotarlo en «Cambios al contrato»; ninguna clave real pasa por vos; al usuario, solo lo de producto, por la Guía.
```

## Back

Cada tarea copia la estructura de su modelo, nunca código de ArquitecturaBase. Aplican `Result`, `OperationLog.RunAsync`, `[LoggerMessage]`, `TimeProvider` y una UoW por método que escribe; ningún log lleva textos de opciones, nombres ni teléfonos. Los tests son los de la §11 de la spec y los de cada tarea.

1. **B1 · Dominio.** `D/Forms/FormCatalog.cs`, `FormCatalogItem.cs`, `FormCatalogSource.cs`, `FormCatalogValueType.cs`, `FormCatalogUnusableReason.cs`, `FormsTextLimits.cs` (30, 40 y 300, que `TextLimits` no tiene), `FormOptionTitle.cs`, `FormWorkLease.cs` y los dos de errores. Copia: `AssistantOrganizationSettings.cs` y `AssistantErrors.cs`. Test suma: el corte de 30.
2. **B2 · Persistencia.** `FormsModelContributor`, las tres configuraciones, `UniqueKeys`, `IFormCatalogRepository`, `IFormCatalogReader` y `<fecha>_FormCatalogs.cs`: las restricciones diferidas en `Sql`, en una línea y sin `\r`, RLS, el `GRANT` y un `Down` que se niega antes de perder datos. Copia: `I/Modules/Assistant/Persistence/` y `20261010011328_AssistantModule.cs`. Tests: clasificación, configuración, `UniqueKeysTests`, RLS por tabla y la migración de ida y vuelta.
3. **B3 · Guardar a mano** (C3, C4 y C6). `FormCatalogService` (alta, detalle, listado, `filter-counts`, opciones y guardado), `FormCatalogItemEditor` y `FormCatalogPositionGuard`. Copia: `AssistantConversationService.cs` y `UpdateAsync` de `RoleService.cs`. Tests suman: sin cambios compara la versión y no toca ni audita opciones; «Ordenar por…» (224) ordena todas, también otras páginas y las dadas de baja, conserva los empates, deja lo vacío al final y compara los números por su valor, y `sort` con `moves` o `deleteItems` da 400.
4. **B4 · Fuentes** (C2, C5, C12, §4 y §4b), sin SQL configurable ([ADR 0053](../decisions/README.md)). `MembersCatalogSource`, `IMemberDirectoryReader` y `MemberDirectoryReader` (núcleo), `IStructuredContactSource` con su modelo, `DisabledStructuredContactSource` (`TryAdd`), `WhatsAppStructuredContactSource`, `FormCatalogFiltersPolicy`, `FormCatalogPreviewBuilder` y `FormCatalogSourceService`. Copia: `ILoginCodeChannel.cs`, `EmailLoginCodeChannel.cs` y `WhatsAppLoginCodeChannel.cs`. Tests suman: 201 filas dan `exceedsLimit` y el total exacto; las etiquetas como detalle de un contacto (§4b); el timeout de 3 s; el corte con el nombre entero en el detalle; sin WhatsApp, `ChannelUnavailable`; `ModuleIsolationTests`.
5. **B5 · El nombre del perfil** (C14). `WhatsAppWebhookReader` lee el nombre; `WhatsAppWebhookWriter` lo guarda cifrado, como el texto, en `WhatsAppMessage.EncryptedProfileName`; `ClearPersonalData` y la purga de 90 días de `WhatsAppMessageRepository` lo borran; `WhatsAppCustomer.TakeProfileName` lo toma una sola vez (`ProfileNameTakenAtUtc`). La migración `<fecha>_WhatsAppContactProfileName.cs` copia `20261014040520_WhatsAppConversationLinks.cs`, que suma `LinkOfferedAtUtc` a `WhatsAppCustomers`, y su `Down` se niega con datos. Test suma: el nombre no queda en la cola.
6. **B6 · La barrera** (C11). `IFormWorkLease` sobre `FormWorkLeases`: `INSERT … ON CONFLICT` que pisa solo una vencida, dos minutos de vida y se suelta al terminar. Copia: la reserva de `Infrastructure/Idempotency/IdempotencyStore.cs` y el vencimiento de `WhatsAppMessage.TryLease`. «Probar sin esperar» se suma a [concurrencia](../rules/concurrencia.md). Suma los topes por organización y de la instalación (C11, F11 de ArquitecturaBase). Tests: la misma persona dos veces da `Busy`; una vencida se retoma; otra persona no espera; la tercera de una organización da `TooManyAtOnce` con `Retry-After` y otra organización no espera.
7. **B7 · Importar, exportar y la plantilla** (C7 a C10). `FormCatalogImportService`, `FormCatalogFileResolver`, `FormCatalogImportPolicy`, `FormCatalogExportBuilder`, `IFormCatalogSpreadsheet` y `FormCatalogSpreadsheet`. Copia: `IWhatsAppContactSpreadsheet.cs` y `WhatsAppContactSpreadsheet.cs`. Tests suman: las listas de la revisión con su segunda página, las dos confirmaciones y un XLSX que se infla con estilos o textos compartidos (C8).
8. **B8 · Rutas, ejemplos y volumen.** Los controllers de K1 llaman a los servicios. Los tests de integración de Forms graban cada respuesta y cada error de negocio en `docs/contracts/examples/forms.json`, y la guarda de [api-http](../rules/api-http.md) pide un ejemplo por cada respuesta de Forms. Partes del módulo en `ExplicitRouteInventoryTests`, `ModuleControllersTests`, `ApplicationServicesTests`, `VersionedContractTests`, `RouteIsolationMatrixTests` y `ResourceParityTests`; `GrowingCollectionContractTests` sin excepciones. Volumen con `EXPLAIN`, copiando `BrowserSessionAuthorizationTests.cs`.
9. **B9 · `PersistenceNamingTests`** ([persistencia-ef](../rules/persistencia-ef.md)). Mira solo los `I*Repository` y los `I*Reader` de `Interfaces/Persistence`, del núcleo y de los módulos; no `Interfaces/Integrations`, donde está `IUserDisplayNameReader`. Pasan a `List…` y `Count…` los `Find…` y `Get…` de hoy que devuelven colecciones o conteos (`IPlatformPermissionAssignmentReader`, `IAssistantTurnRepository`, `IWhatsAppContactReader`, `ICompanyMembershipReader`, `IWhatsAppConversationReader`, los `GetFilterCountsAsync` de cuatro lectores y dos de `IAssistantSettingsReader`). Los verbos que la ficha no prohíbe quedan.

**Texto para el chat del Back**

```text
Leé AGENTS.md, docs/plans/2026-11-08-etapa-12-catalogos.md, la spec de la E12 con sus dos partes y las fichas de su «Qué leer». Con K1 publicado, hacé B1 a B9 en orden, contra ese contrato, copiando los modelos de cada tarea; nada de ArquitecturaBase. Forms nunca referencia a WhatsApp: hablan por IStructuredContactSource. No toques el front ni la tabla de estado del plan. Reglas: todo en main, sin ramas, worktrees, stash, reset ni push; commits propios con git commit -- <rutas>, en español y conventional; con la app levantada, nadie edita el src del front; TDD donde hay lógica; al terminar cada tarea, build y tests rápidos enteros, sin Docker ni filtrar por módulo, más integración y seguridad del área si tocó rutas, persistencia, autenticación o aislamiento; el contrato no cambia sin anotarlo en «Cambios al contrato»; ninguna clave real pasa por vos; al usuario, solo lo de producto, por la Guía.
```

## Front

Todo va en `src/modules/forms`, aportado por `registration.ts`, contra los tipos generados. Cada pantalla copia su tablero de la v175. Tests con MSW y axe; los estados de la §10 de la spec y los criterios de su §16 tienen cada uno su test.

1. **F1 · Módulo y navegación.** `registration.ts` (`/org/formularios/catalogos`, `/nuevo`, donde «Crear catálogo» manda el `POST`, y `/:id`; guarda con `forms` y `formcatalogs.read`; «Gestión de formularios» con su panel y solo «Catálogos», en la compu y en el teléfono; `permissions.organization` con los dos permisos, que `F/src/auth/usePermissions.test.ts` compara con `Permissions.cs`), `locales/{es,en}/forms.json`, `enums.json` y `F/src/app/moduleComposition.ts`. Copia: `src/modules/assistant/registration.ts` y el panel de `whatsapp/contacts/routes.ts`. Tests: `registration`, `navigation` y `composition`.
2. **F2 · API, errores y ejemplos.** `api/catalogs.ts` y `errors.ts`, que lleva cada código a su campo; copia `whatsapp/contacts/api/contacts.ts` y `whatsapp/contacts/errors.ts`. `F/scripts/check-contracts.mjs` suma la validación de cada ejemplo de `docs/contracts/examples/` contra su esquema de `openapi.json`, estricta y con zod, copiando `renderValidationContracts` de `generate-contracts.mjs`. Tests: solo el 409 común marca conflicto; en `validation-contracts.test.mjs`, un ejemplo con un campo de más o un valor fuera de su enum hace fallar `contracts:check`.
3. **F3 · Listado y nuevo catálogo.** `pages/CatalogsPage.tsx` y `components/catalog/NewCatalogDialog.tsx`, que en el teléfono es una hoja. Tableros: `FRM-Catalogos`, `M-FRM-Catalogos` y `FRM-Catalogo-Nuevo`. Tests: el resumen con `filter-counts`; los filtros sin cantidades; sin WhatsApp no se ofrece el origen de contactos.
4. **F4 · La tabla editable.** `F/src/shared/ui/EditableTable.tsx`, nueva, y las columnas fijas en `table.tsx` (`pinned`), con los anchos mínimos y la línea entre columnas también en las fijas (137). Copia: `DataTable.tsx` y su `Column`. Su excepción a las filas de solo lectura va en `paginado-y-listados.md` del front. Test: se desplaza a lo ancho sin aplastarse.
5. **F5 · El editor a mano.** `pages/CatalogEditorPage.tsx`, con «Dar de baja» o «Reactivar» en el ⋮ de la banda (209), `components/catalog/OptionsSection.tsx`, `CatalogPreviewPhone.tsx` (con «Una opción | Varias»), `CatalogConflictDialog.tsx` (lado a lado, 138) y `hooks/useCatalogEditor.ts`. Suma `components/catalog/SortDialog.tsx` (224). Tableros: `FRM-Catalogo`, `M-FRM-Catalogo`, `FRM-Catalogo-Ordenar` y su teléfono, y `FRM-Catalogo-Tipo`. Tests: borrar, restaurar, mover y «Ordenar por…», con el orden pendiente, «Cancelar orden» y el motivo de lo apagado; guardar conserva el lugar (F7 del traspaso 15); con dos motivos de `uses`, se ve el primero; «1.500» en el valor da `numberAmbiguous`, y uno de 18 dígitos llega entero (207 y C6), también en el modal; solo `formcatalogs.read`.
6. **F6 · El modal de opciones.** `components/catalog/OptionsDialog.tsx`. Tableros: `FRM-Catalogo-Opciones` y su teléfono. Tests: «Cancelar», Esc, «Listo», los errores con el triángulo y «Volver a la lista».
7. **F7 · Importar.** `components/catalog/ImportDialog.tsx` y `F/src/shared/ui/PasteGrid.tsx`. Tablero: `FRM-Catalogo-Importar`. Tests: las listas de la revisión con su segunda página, con «Tiene más de 30 caracteres.» en una fila; las dos confirmaciones; el 409 y `Busy`.
8. **F8 · Filtros y vista previa.** `components/catalog/FiltersEditor.tsx` y los combos de la persona y el cliente de ejemplo, con `SearchSelect`. Tableros: `FRM-Catalogo-Filtros`, `FRM-Catalogo-Contactos` y sus teléfonos. Tests: la vista previa se pide sola; «y N opciones más» con su ⓘ; `SampleRequired`; un rol que ya no existe.

**Texto para el chat del Front**

```text
Leé AGENTS.md del front, ../ArquitecturaBaseMutitenant/docs/plans/2026-11-08-etapa-12-catalogos.md y la spec de la E12 con sus dos partes. Con K1 publicado, corré npm run contracts y hacé F1 a F8 contra los tipos generados, copiando los tableros de la v175 de cada tarea; si a uno le falta un estado, seguí y avisale a la Guía. No toques el lienzo, el back ni la tabla de estado del plan, ni escribas tipos generados. Reglas: todo en main, sin ramas, worktrees, stash, reset ni push; commits propios con git commit -- <rutas>, en español y conventional; con la app levantada, nadie edita el src del front; TDD donde hay lógica; al terminar cada tarea, build y tests rápidos enteros, sin Docker ni filtrar por módulo, más integración y seguridad del área si tocó rutas, persistencia, autenticación o aislamiento; el contrato no cambia sin anotarlo en «Cambios al contrato»; ninguna clave real pasa por vos; al usuario, solo lo de producto, por la Guía.
```

## Integración y puerta

1. **I1 · Integrar.** El front contra la API real, con los ejemplos validados por `contracts:check`. Lo que no encaja vuelve a su dueño y se anota abajo.
2. **I2 · Puerta.** `/puerta` una sola vez, sobre copias del corte: las suites y `contracts:check`; el E2E real (`F/scripts/test-e2e-real-forms.mjs`, nuevo, con sus casos en `test-e2e-real.test.mjs`) con los cinco casos de la §15 de la spec, habilitando antes `forms` en la organización; `realinear-docs` y `estado-docs` en 0; la galería de los quince tableros de la etapa (`scripts/capturas/etapa-12.json`) a 1440, 390 y 320, y la lista de control.
3. **I3 · Recorrido y cierre.** `/recorrido` con `armar-un-catalogo` y `catalogo-con-filtros` (`F/scripts/recorrido-usuario-forms.mjs`, nuevo). Al cerrar nacen `docs/features/formularios.md` con su sección de catálogos, la fila de Forms en «Dónde va cada cosa» de `AGENTS.md`, los `AGENTS.md` de cada carpeta nueva y sus filas en `docs/architecture/arbol.md`. El retiro del módulo no se prueba en esta etapa (C13).

**Pendiente para la I1 de la 10:** el nombre del perfil (C14) con un número real conectado.

| Puerta | Resultado |
| --- | --- |
| Back / Front / Contrato / E2E / Documentación / Galería / Recorrido | Pendiente |

**Texto para el chat de Integración**

```text
Leé AGENTS.md de los dos repos, docs/plans/2026-11-08-etapa-12-catalogos.md y la skill /puerta. Integrá el front contra la API real; lo que no encaja vuelve a su dueño. Corré la puerta una sola vez sobre copias del corte y después solo lo que toque cada arreglo; en el plan va solo la tabla final. No toques el lienzo ni manejes la PC del usuario. Reglas: todo en main, sin ramas, worktrees, stash, reset ni push; commits propios con git commit -- <rutas>, en español y conventional; con la app levantada, nadie edita el src del front; TDD donde hay lógica; al terminar cada arreglo, build y tests rápidos enteros, sin Docker ni filtrar por módulo, más integración y seguridad del área si tocó rutas, persistencia, autenticación o aislamiento; el contrato no cambia sin anotarlo en «Cambios al contrato»; ninguna clave real pasa por vos; al usuario, solo lo de producto, por la Guía.
```

## Relación con otras etapas

### Usa de las anteriores

- **Base de la 12:** la lista de K0.
- **E11:** `WhatsAppCustomer` (`Name`, `Tags`), `WhatsAppMessage.ClearPersonalData` y `TryLease`, `WhatsAppWebhookReader`, `WhatsAppWebhookWriter`, `WhatsAppOrganizationInboundWriter`, la purga de `WhatsAppMessageRepository`, `WhatsAppFeature.Key` y, como modelo, `IWhatsAppContactSpreadsheet`.
- **E9:** las cuotas generales y `RouteIsolationMatrixTests`.
- **Núcleo:** los moldes de cada tarea, `IUserDisplayNameReader` y `Member`, `CompanyMembership`, `RoleAssignment`, `Role` y `Company`; en el front, `SearchSelect`, `DataTable`, `FilterBar` y `check-contracts.mjs`.

### Deja para las siguientes

- **Módulo:** `AddFormsModule` en las tres capas, `FormsApiModule`, `BusinessFormCatalogsController`, la feature `forms`, `FormsModelContributor`, `FormsContractContributor`, `FormsTexts` con `FormsErrorTextSource`, `Permissions.Forms` y `src/modules/forms/registration.ts` con el panel «Gestión de formularios» y sus permisos.
- **Datos:** `FormCatalog`, `FormCatalogItem`, `FormCatalogSource`, `FormCatalogValueType`, `FormCatalogUnusableReason`, `FormsTextLimits`, `FormOptionTitle` y los dos de errores; las tablas `tenant.FormCatalogs`, `FormCatalogItems` y `FormWorkLeases` (clave `TenantId, UserId`).
- **Servicios y helpers:** los tres servicios; `FormCatalogFiltersPolicy`, `FormCatalogItemEditor`, `FormCatalogPositionGuard`, `FormCatalogFileResolver`, `FormCatalogImportPolicy`, `FormCatalogExportBuilder` y `FormCatalogPreviewBuilder`; `IFormCatalogRepository` con `LockAsync(ids)`, `IFormCatalogReader`, `IFormWorkLease`, `UniqueKeys`, `IFormCatalogSpreadsheet` y `MembersCatalogSource`.
- **Puertos:** `Application/Interfaces/Integrations/StructuredData/` con `IStructuredContactSource`, su modelo en `Application/Models/StructuredData/`, `DisabledStructuredContactSource` y `WhatsAppStructuredContactSource`; `IMemberDirectoryReader` y `MemberDirectoryReader`, con una fila más que el tope.
- **Rutas:** las diecisiete; `ItemInUse` con `uses`, una lista en orden fijo a la que cada etapa suma su motivo; el `sort: {column, descending}` del `PUT`, con su orden estable (224).
- **Front:** `CatalogsPage`, `CatalogEditorPage`, `OptionsSection`, `OptionsDialog`, `SortDialog`, `ImportDialog`, `FiltersEditor`, `CatalogPreviewPhone`, `CatalogConflictDialog`, `useCatalogEditor` y `api/catalogs.ts`; `EditableTable`, `PasteGrid` y las columnas fijas de `table.tsx`.
- **Pruebas:** la de volumen del módulo, `PersistenceNamingTests` y los ejemplos: `docs/contracts/examples/forms.json`, su guarda y la validación de `contracts:check`.

## Cambios al contrato


## Para decidir

Nada.
