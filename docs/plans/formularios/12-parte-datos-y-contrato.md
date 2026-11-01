# E12 · Catálogos base: datos y contrato (spec, parte)

**Estado:** pendiente · parte de la [spec de la E12](12-catalogos.md), que dice qué leer y cómo se cierra.

## 2. Decisiones técnicas

Siguen las reglas del repo y, cuando hay más de una forma, la más simple. Las de producto están en la §5 de la hoja de ruta.

| # | Decisión | Por qué |
| --- | --- | --- |
| C1 | Los catálogos son de la organización: `ITenantOwned`, en el esquema `tenant` y en el módulo `Forms`. | Como los formularios (decisión 1). |
| C2 | **La fuente «Miembros» se define en el código, no como vista SQL.** La definición (columnas, operadores y versión) es de Forms; la lectura es un reader del núcleo detrás de un puerto neutral (`IMemberDirectoryReader`), sobre las tablas de miembros, roles, empresas y la cuenta, con una lista cerrada de columnas y valores como parámetros de EF. La versión de la fuente cambia si cambian sus columnas: los catálogos viejos pasan a «No se puede usar» con su motivo. | El repo no tiene vistas SQL, y una vista con RLS necesita `security_invoker`. Las guardas de identidad (`IdentityAccessTests`) son listas cerradas del núcleo: un módulo no lee `ApplicationUser`. |
| C3 | **Todo se guarda junto y viaja solo lo que cambió.** Un solo `PUT` del catálogo, con su versión (`IVersioned`, 409), lleva: <ul><li>nombre, descripción e `isActive`;</li><li>solo las opciones nuevas o cambiadas: las existentes con su `itemId` y las nuevas con un id local;</li><li>los movimientos `{itemId, direction}`, hasta 500;</li><li>`sortAlphabetically`, que reordena por texto al guardar.</li></ul>El pedido pesa hasta 512 KiB. El servidor escribe y audita solo las opciones que cambiaron, y toca siempre la fila del catálogo, así su versión sube con cualquier cambio de opciones y un guardado sin cambios compara la versión, como manda [concurrencia](../../rules/concurrencia.md). Responde `VersionResponse` y el mapa de ids locales a ids del servidor. | Configuración interna: una UoW, versión y 409, sin recibos. En ArquitecturaBase cada guardado reescribía las 10.000 opciones y su auditoría. |
| C4 | La posición la maneja el servidor y es única por catálogo, con una restricción `DEFERRABLE INITIALLY DEFERRED` hecha con SQL en la migración. Subir y bajar intercambian la posición con la vecina, bajo un bloqueo del catálogo. El código también es único con una restricción diferida, para permitir intercambios en un guardado. Las dos se validan antes, bajo el bloqueo del catálogo, y la restricción queda de respaldo: un choque en el commit sería un 500. | Las posiciones que manda el cliente se rompían. |
| C5 | La gramática de condiciones es general: `{column, operator, values[]}`, unidas con «y». | Una sola gramática, que pueden reusar otras partes del módulo. |
| C6 | **Tipo de valor.** `Text` (por defecto), `Integer`, `Decimal` o `Date`, fijo desde el alta; un catálogo con filtros es siempre `Text`. En uno tipado, toda opción, activa o no, lleva su valor: enteros de hasta 18 dígitos, decimales de hasta 18 enteros y 10 decimales sin redondeo (más decimales se rechaza) y fechas ISO. Se guarda canónico. Lo que se ve en WhatsApp sigue siendo el texto. | Es la regla de ArquitecturaBase (C1-02). |
| C7 | **Carga de una vez.** Sobre un catálogo guardado y sin cambios en la pantalla: <ul><li>pegar **agrega**, e importar un archivo **reemplaza**: lo que no viene se da de baja, no se borra;</li><li>la vista previa no guarda ni abre transacción;</li><li>aplicar vuelve a leer los mismos bytes, compara el SHA-256 y revalida permisos y versión bajo el bloqueo del catálogo, en una sola UoW. Un archivo distinto o una versión vieja dan 409;</li><li>la identidad de una fila es su código, sin distinguir mayúsculas. Una columna opcional que falta conserva; presente y vacía, limpia;</li><li>más del 25 % de bajas pide confirmar la cantidad exacta; aplicar sin las filas con errores tiene su botón, y con más del 10 % de filas con error pide confirmarlo.</li></ul> | Es la regla de ArquitecturaBase (C1-03). |
| C8 | **Topes del archivo:** 10 MiB, 64 columnas, 10.000 filas de datos y 640.128 celdas de trabajo, contando las vacías con formato y las fórmulas matriciales; un XLSX hasta 100 MiB descomprimido. Las fórmulas cuentan por su valor guardado. El CSV va en UTF-8 estricto. Se lee y se escribe con ClosedXML (Excel, P12, que se adopta acá) detrás de un puerto del módulo, `IFormCatalogSpreadsheet`, como `IWhatsAppContactSpreadsheet` de la E11: Application no referencia ClosedXML. | Las celdas vacías con formato pasaron los topes en ArquitecturaBase. |
| C9 | **Números y fechas de un archivo o de lo pegado** se leen con la cultura del pedido. Lo que se puede leer de dos formas («1.500», «12.345», «3/4/2026») da `Forms.CatalogImport.ValueAmbiguous`, en su fila y columna. | Lista de control de `/puerta`, punto 1. |
| C10 | **Exportar** escribe el valor canónico como texto, con las columnas de la plantilla, y se vuelve a importar sin diferencias. La plantilla trae una columna por dato, si es obligatorio, y una fila de ejemplo. | Exportar e importar son el camino para los cambios grandes. |
| C11 | **La vista previa, aplicar y exportar van de a uno por persona.** Es una barrera con vencimiento (lease) por persona, guardada en PostgreSQL, que se toma sin esperar: si está tomada, `Forms.CatalogImport.Busy`. No es `AdvisoryLockExtensions`, que espera, ni necesita transacción, porque la vista previa no la abre. La barrera vence sola si el proceso se cae. Cuentan en las cuotas generales ([cuotas](../../rules/cuotas.md)), sin perfil propio. | Nunca un limitador por proceso. El repo no tiene un «probar sin esperar»: la forma se suma a la ficha de [concurrencia](../../rules/concurrencia.md) al planificar. |
| C12 | **La fuente «Contactos» la da WhatsApp por un puerto neutral del núcleo** (`IStructuredContactSource`, en `Application/Interfaces/Integrations/StructuredData/`). Forms nunca referencia a WhatsApp; sin WhatsApp, la implementación apagada no devuelve contactos y el origen no aparece. Las etiquetas de un contacto son textos (`text[]`), y los filtros comparan el texto. | `ModuleIsolationTests`. |
| C13 | **El módulo nace acá,** como el Asistente: `AddFormsModule` en las tres capas, la feature `forms` (`FeatureScope.Business`, apagada por defecto), `FormsModelContributor`, `FormsContractContributor`, sus recursos y su registro en el front. El retiro probado y su guía van en la E14, cuando el módulo ya se conecta con WhatsApp por todos sus puertos. | El módulo nace con su primera etapa, y el retiro se prueba una vez ([AGENTS.md](../../../AGENTS.md)). |
| C14 | **El nombre de un contacto** (81): el webhook lee el nombre del perfil, `contacts[].profile.name`, y lo guarda en `WhatsAppMessage.ProfileName`, que se limpia con los datos personales del mensaje. La entrada completa el nombre de un contacto sin nombre con ese dato, una sola vez: `WhatsAppCustomer.ProfileNameTakenAtUtc` lo marca, así un nombre que una persona cambió o vació no se vuelve a llenar. En las opciones, un nombre de más de 30 caracteres se corta como el de un miembro (36). Un contacto sin nombre ni perfil no se ofrece. | El nombre del contacto es opcional en la E11, y la entrada no lo completaba. |

## 3. Modelo de datos

**Convención para todas las tablas:**
- todas son `ITenantOwned` en el esquema `tenant`, con clave `(TenantId, Id)`, `EnableTenantRls` y el `GRANT` a `mt_app` en la migración;
- las relaciones usan FK compuestas;
- los textos usan `TextLimits`.

**Modelo de entidad y configuración:** `Domain/Assistant/AssistantOrganizationSettings.cs` e `Infrastructure/Modules/Assistant/Persistence/Configurations/AssistantOrganizationSettingsConfiguration.cs`. **Migración:** `Infrastructure/Persistence/Migrations/20261010011328_AssistantModule.cs`.

**FormCatalogs** (`IAuditable`, `IVersioned`)

| Columna | Tipo | Regla |
| --- | --- | --- |
| `Id`, `TenantId` | uuid | — |
| `Name` | texto ≤60, `ShortName()` | obligatorio |
| `Description` | texto ≤300? | opcional |
| `Source` | texto | `Manual`, `Members` o `Contacts`; no cambia después de crear |
| `ValueType` | texto | `Text`, `Integer`, `Decimal` o `Date`; no cambia; `Members` y `Contacts` son siempre `Text` (C6) |
| `Filters` | jsonb? | solo con `Members` o `Contacts`; CHECK que lo ata a `Source` |
| `IsActive` | bool | dar de baja = `false` |
| `Version` y auditoría | — | — |

**`Filters`** (jsonb, `format: 1`):

```json
{ "sourceVersion": 1,
  "conditions": [ { "column": "role", "operator": "hasAny", "values": ["<roleId>"] } ],
  "excludeRecipient": true,
  "detail": "companies",
  "sort": { "column": "name", "descending": false } }
```

**FormCatalogItems** (`IAuditable`; solo los catálogos a mano; la versión es la del catálogo, C3)

| Columna | Tipo | Regla |
| --- | --- | --- |
| `Id`, `TenantId`, `CatalogId` | uuid | FK compuesta |
| `Code` | texto ≤40 | obligatorio; único por catálogo sin distinguir mayúsculas (`CodeNormalized` + `UniqueKeys.FormCatalogItemCode`, diferida). No se ve en WhatsApp. |
| `Text` | texto ≤30, validado al guardar | obligatorio; es el título de la opción en WhatsApp. Un texto repetido avisa, sin frenar |
| `Detail` | texto ≤300, validado al guardar | opcional; la descripción de la opción |
| `NumberValue` | numeric(28, 10)? | en `Integer` (escala 0, hasta 18 dígitos) y `Decimal` (hasta 10 decimales, sin redondeo) |
| `DateValue` | date? | en `Date` |
| `Position` | int | única por catálogo, `DEFERRABLE INITIALLY DEFERRED` (C4) |
| `IsActive` | bool | dar de baja = `false`; no se borra |
| — | CHECK | el valor que corresponde al tipo del catálogo, y ningún otro |

Un catálogo a mano tiene hasta **10.000 opciones**, entre activas y dadas de baja, comprobado bajo el bloqueo del catálogo.

**FormWorkLeases** (técnica, sin auditoría): `TenantId`, `UserId` (clave), `Kind` (`Preview`, `Apply` o `Export`) y `ExpiresAtUtc`. Es la barrera de C11: se toma con un `INSERT … ON CONFLICT` que pisa solo una vencida, y se suelta al terminar.

## 4. La fuente «Miembros»

Se define en el código, en `Application/Modules/Forms/Catalogs/MembersCatalogSource.cs`. La versión 1 tiene:

| Columna | Tipo | Operadores | Mostrar como detalle | Ordenar |
| --- | --- | --- | --- | --- |
| `name` (nombre visible de la cuenta) | texto | `contains`, `equals` | — (siempre es el texto) | sí |
| `role` (roles de la organización) | lista | `hasAny`, `hasNone` | sí, los nombres separados por coma | no |
| `company` (empresas con membresía activa) | lista | `hasAny`, `hasNone` | sí | no |
| `active` (miembro activo) | sí/no | `is` | no | no |

**Reglas:**
- **Qué devuelve:** cada opción es un miembro. Su id es el `MemberId` y su texto, el nombre de la cuenta.
- **Lo que nunca sale:** correo, teléfono, métodos de ingreso, ni nada de otra organización.
- **Condiciones:**
  - hasta 10, unidas con «y»;
  - los valores de `role` y `company` son ids que tienen que existir en la organización;
  - si un rol o una empresa ya no existe, el catálogo pasa a «No se puede usar» con su motivo.
- **«Sin incluir a quien lo recibe»:** `MemberId <> el miembro de quien recibe`. Es el único parámetro de contexto, y lo pone el servidor: en la vista previa, la persona de ejemplo; al mandar (E14), quien recibe.
- **Ejecución:**
  - el puerto neutral `IMemberDirectoryReader`, en `Application/Interfaces/Persistence/`, y su reader del núcleo en `Infrastructure/Persistence/Readers/MemberDirectoryReader.cs`, sobre `Member`, `CompanyMembership`, las asignaciones de roles y el nombre de la cuenta. No sabe de formularios: recibe condiciones de la lista cerrada y devuelve id, nombre y, si se pide, roles o empresas;
  - EF con parámetros y RLS de la organización, con la conexión normal;
  - un timeout corto (3 s);
  - lee una fila más que el tope que se le pide (201 en la vista previa): si la hay, devuelve `exceedsLimit`.
- **Guarda de acceso global:** el reader del núcleo entra en la lista cerrada de `IdentityAccessTests`, porque lee la cuenta global (`identity`).
- **Versión:**
  - `sourceVersion` es 1;
  - si una versión nueva de la fuente cambia las columnas o los operadores, un catálogo con `sourceVersion` vieja pasa a «No se puede usar»;
  - sin migración de datos.

## 4b. La fuente «Contactos» (55)

- **Qué devuelve:** cada opción es un contacto de la bandeja Clientes de la organización. Su id es el del contacto (`WhatsAppCustomer`) y su texto, el nombre del contacto (C14).
- **Lo que nunca sale:** el teléfono, ni en la vista previa ni en ninguna pantalla.
- **Filtros:** por etiqueta del contacto (`hasAny`, `hasNone`), con la misma gramática que «Miembros» y los valores como textos de etiqueta, sin distinguir mayúsculas. Hasta 10 condiciones.
- **Ejecución:** como «Miembros»: lee una fila más que el tope, con un timeout corto, sin traer todos los contactos a memoria.
- **El valor es siempre de texto.**

## 5. Dominio y errores

**Entidades** (`Domain/Forms`):
- **`FormCatalog`:** `CreateManual(valueType)`, `CreateMembers`, `CreateContacts`, `Rename`, `ChangeFilters` (solo `Members` y `Contacts`), `Deactivate` y `Reactivate`.
- **`FormCatalogItem`:** `Create`, `Edit`, `Deactivate`, `Reactivate` y `SwapPosition`.

**Estado de un catálogo:** «De baja» si se dio de baja; si no, se calcula, sin guardarse: «Activo» o «No se puede usar», con estos motivos:
- `NoActiveOptions`: un catálogo a mano sin opciones activas;
- `SourceOutdated`;
- `ChannelUnavailable`: un catálogo de `Contacts` sin WhatsApp instalado;
- `RoleMissing` o `CompanyMissing`: falta un rol o una empresa de una condición.

**Errores** (en `Domain/Forms/<Entidad>Errors.cs`, con textos en `Application/Modules/Forms/Resources/FormsTexts.resx` y `.en.resx`). El modelo es `Domain/Assistant/AssistantErrors.cs` y su `AssistantErrorTextSource`:

| Código | Tipo | Cuándo |
| --- | --- | --- |
| `Forms.Catalog.NotFound` | NotFound | ajeno o inexistente |
| `Forms.Catalog.ItemNotFound` | NotFound | la opción no es de ese catálogo |
| `Forms.Catalog.DuplicateCode` | Validation (campo `items[i].code`) | el código ya existe en el catálogo |
| `Forms.Catalog.TooManyItems` | Conflict | más de 10.000 opciones |
| `Forms.Catalog.ValueRequired` / `ValueInvalid` | Validation (campo `items[i].value`) | falta el valor en un catálogo tipado, o no es del tipo |
| `Forms.Catalog.MoveOutOfRange` | Conflict | subir la primera o bajar la última |
| `Forms.Catalog.Inactive` | Conflict | cambiar opciones de un catálogo dado de baja |
| `Forms.Catalog.WrongSource` | Conflict | opciones en un catálogo con filtros, o filtros en uno a mano |
| `Forms.Catalog.SampleRequired` | Validation (campo `sampleMemberId`; el texto es «Elegí una persona.») | crear o guardar un catálogo de `Members` con condiciones sin persona de ejemplo. Sin condiciones no hace falta |
| `Forms.Catalog.FiltersInvalid` | Validation (campo `conditions[i]`) | columna, operador o valor fuera de la fuente |
| `Forms.Catalog.SampleMemberNotFound` / `SampleContactNotFound` | Validation | la persona o el cliente de ejemplo no es de la organización |
| `Forms.Catalog.ItemTextTooLong` / `ItemDetailTooLong` | Validation (campo `items[i].text` o `.detail`) | textos fuera de límite |
| `Forms.CatalogImport.FileChanged` | Conflict | el archivo que se aplica no es el de la vista previa (SHA-256) |
| `Forms.CatalogImport.FileInvalid` | Validation | formato, codificación o topes del archivo (C8) |
| `Forms.CatalogImport.ValueAmbiguous` | Validation (fila y columna) | un número o una fecha que se lee de dos formas (C9) |
| `Forms.CatalogImport.ConfirmationRequired` | Validation | más del 25 % de bajas o del 10 % de filas con error, sin confirmar |
| `Forms.CatalogImport.Busy` | Conflict | ya hay una vista previa, una aplicación o una exportación de esa persona (C11) |

Los códigos siguen `^[A-Z][A-Za-z]+(\.[A-Z][A-Za-z0-9]+){2,}$` (`ErrorCodeTests`). El 409 de edición es el común, `General.ConcurrencyConflict`, y es el único que pone el editor en conflicto: los otros 409 son errores que se corrigen.

## 6. Contrato borrador

Las rutas de Business llevan `[Access(Access.Business)]` y `[FeatureGate("forms")]`. Los listados, relaciones y opciones de selectores llevan `PagedRequest` y `PagedResult`, con los tamaños comunes (10 por defecto, 20, 50 o 100), búsqueda y orden estable. Las altas llevan `[Idempotent]`. Toda respuesta que consume el front tiene ejemplos exportados de una corrida real. El contrato y los errores van en OpenAPI con `Api/Modules/Forms/FormsContractContributor.cs`, como `Api/Modules/Assistant/AssistantContractContributor.cs`.

| Método y ruta | Permiso | Pide | Devuelve |
| --- | --- | --- | --- |
| GET `/api/organization/forms/catalogs` | `formcatalogs.read` | `page`, `pageSize`, `sort` (`name`, `updatedAtUtc`), `search`, `source` (`all`, `manual`, `members`, `contacts`), `status` (`all`, `active`, `inactive`) | 200 `PagedResult<{id, name, description?, source, valueType, optionsCount?, isActive, usable, unusableReason?}>` |
| GET `/api/organization/forms/catalogs/filter-counts` | `formcatalogs.read` | `search` | 200 `{all, manual, members, contacts, active, inactive}` |
| POST `/api/organization/forms/catalogs` | `formcatalogs.manage` | `{name, description?, source, valueType?, items?[], filters?, sampleMemberId?}`. Sin tipo, `Text`; `Members` con condiciones exige `sampleMemberId` | 201 detalle + Location, con el mapa de ids |
| GET `/api/organization/forms/catalogs/{id}` | `formcatalogs.read` | — | 200 `{id, name, description, source, valueType, filters?, roles?: [{id, name}], companies?: [{id, name}], isActive, usable, unusableReason?, canManage, version}`. `roles` y `companies` traen el nombre de cada id que usa el filtro, así quien solo tiene `formcatalogs.read` lo lee sin pedir esas listas, que piden `formcatalogs.manage` y hacen falta solo para editar el filtro. Un id que ya no existe no viene, y el catálogo queda «No se puede usar» con su motivo |
| PUT `/api/organization/forms/catalogs/{id}` | `formcatalogs.manage` | `{name, description, isActive, version, items?: [{itemId? \| localId, code, text, detail?, value?, isActive}], moves?: [{itemId, direction}], sortAlphabetically?, filters?, sampleMemberId?}`. `items` lleva solo lo nuevo o cambiado. `isActive` es obligatorio: omitirlo no apaga nada | 200 `{version, itemIdentityMap}` |
| GET `/api/organization/forms/catalogs/{id}/items` | `formcatalogs.read` | `page`, `pageSize`, `search`, `status` (`active`, `inactive`, `all`) | 200 `PagedResult<{id, code, text, detail?, value?, position, isActive}>`, ordenado por posición |
| POST `/api/organization/forms/catalogs/{id}/imports/previews` | `formcatalogs.manage` | multipart: el archivo o las filas pegadas, `mode` (`append` al pegar, `replace` con un archivo) y `version` | 200 `{version, sha256, rowsRead, added, changed, deactivated, errors: PagedResult<{row, column, code}>}`, con la versión leída (`VersionedContractTests`); no escribe |
| POST `/api/organization/forms/catalogs/{id}/imports` | `formcatalogs.manage` | multipart: el mismo archivo o filas, `mode`, `sha256`, `version`, `skipErrorRows`, `confirmedDeactivations?` | 200 `{version}`; 409 `FileChanged` o de versión |
| GET `/api/organization/forms/catalogs/{id}/export` | `formcatalogs.read` | — | 200 `.xlsx` con todas las opciones, activas y dadas de baja |
| GET `/api/organization/forms/catalogs/template` | `formcatalogs.read` | `valueType` | 200 `.xlsx` |
| GET `/api/organization/forms/catalogs/sources/members` | `formcatalogs.manage` | — | 200 `{version, columns[{key, type, operators, canDetail, canSort}]}`; catálogo cerrado de columnas y operadores de la §4 |
| GET `/api/organization/forms/catalogs/sources/members/roles` | `formcatalogs.manage` | `page`, `pageSize`, `search`, `sort` (`name`) | 200 `PagedResult<{id, name}>` de roles de la organización |
| GET `/api/organization/forms/catalogs/sources/members/companies` | `formcatalogs.manage` | `page`, `pageSize`, `search`, `sort` (`name`) | 200 `PagedResult<{id, name}>` de empresas de la organización |
| GET `/api/organization/forms/catalogs/sample-members` | `formcatalogs.read` | `page`, `pageSize`, `search` | 200 `PagedResult<{memberId, name}>`, sin correo |
| GET `/api/organization/forms/catalogs/sample-contacts` | `formcatalogs.read` | `page`, `pageSize`, `search` | 200 `PagedResult<{contactId, name}>` de los contactos de Clientes, sin teléfono, por el puerto `IStructuredContactSource` (C12) |
| GET `/api/organization/forms/catalogs/sources/contacts/tags` | `formcatalogs.manage` | `page`, `pageSize`, `search` | 200 `PagedResult<{name}>` con los textos de etiqueta que usan los contactos de la organización, por el puerto `IStructuredContactSource` (C12) |
| POST `/api/organization/forms/catalogs/previews` | `formcatalogs.read` | `{source, filters, sampleMemberId?, sampleContactId?}`: la persona de ejemplo con `Members`, y el cliente de ejemplo con `Contacts`. El catálogo no elige entre una opción o varias: lo decide la pregunta | 200 `{options[{text, detail?}], total, exceedsLimit}`; no escribe |

Las pruebas de integración recorren una segunda página de opciones, roles, empresas, etiquetas, personas y clientes de ejemplo, y errores de importación, con búsqueda y desempate; `GrowingCollectionContractTests` verifica sus contratos sin agregar excepciones por módulo. La vista previa conserva su límite explícito (200).

**Plataforma:** no tiene rutas nuevas. La feature `forms` aparece sola en la pestaña «Módulos» de la ficha de la organización.

## 7. Flujos

«UoW» es una `ExecuteInTransactionAsync(…, CommitPolicy.OnSuccess, ct)`. Cada método público de servicio va en `OperationLog.RunAsync`.

1. **Crear un catálogo:** se valida afuera y se crea en una UoW. Si es `Members` o `Contacts`, sus filtros se validan contra la fuente.
2. **Guardar un catálogo a mano** (C3): una UoW con la versión del catálogo.
   - Bloqueo del catálogo.
   - Si nada cambió, escribe solo la fila del catálogo para comparar la versión ([concurrencia](../../rules/concurrencia.md)), sin tocar ni auditar opciones.
   - Las opciones nuevas o cambiadas: las nuevas van al final, se respeta el tope de 10.000, el código tiene que estar libre y el valor tiene que ser del tipo.
   - Los movimientos, en orden, sobre el orden global por posición e id; después se renumera. `sortAlphabetically` reordena por texto. La restricción diferida admite los intercambios.
   - Un movimiento fuera de rango revierte todo el guardado.
   - Responde la versión y el mapa de ids.
3. **Pegar o importar** (C7 a C9):
   - **vista previa:** toma la barrera de la persona (C11), lee el archivo o las filas con sus topes y la cultura del pedido, y compara contra lo guardado. Devuelve la versión, el SHA-256, las altas, los cambios, las bajas y los errores por fila y columna. No escribe datos del catálogo;
   - **aplicar:** toma la barrera de la persona y, en una UoW, bloquea el catálogo, comprueba la versión y el SHA-256, revalida permisos y aplica todo junto: altas al final, cambios por código y, al reemplazar, bajas de lo que no vino. Pide la confirmación de bajas y de filas con error cuando corresponde.
4. **Exportar y la plantilla:** toman la barrera de la persona y leen en lotes, sin traer las 10.000 opciones de una vez a memoria.
5. **Filtros:**
   - `POST` o `PUT` del catálogo con su versión y, en `Members` con condiciones, la persona de ejemplo: sin ella no se crea;
   - los filtros se validan contra la fuente y se vuelve a correr la vista previa con esa persona;
   - con más de 200 resultados para esa persona **se guarda**: la vista previa avisa, y lo que frena es el envío de la E14, donde el tope se comprueba para cada destinatario (decisiones 12, 22 y 37).
6. **Vista previa:** corre el lector con los filtros sin guardar, con la persona o el cliente de ejemplo como destinatario. La pantalla la vuelve a pedir sola al cambiar los filtros o la persona, sin botón «Probar». No toma su identidad ni sus permisos, y no escribe nada. Un nombre de más de 30 caracteres se ve cortado con la regla de la decisión 36, la misma que la E14 aplica al mandar.

## 8. Puertos

**El primer puerto neutral del módulo, `IStructuredContactSource`** (C12), en `Application/Interfaces/Integrations/StructuredData/`, con su modelo en `Application/Models/StructuredData/`: lista los contactos de la organización por página, con sus filtros por etiqueta y una fila más que el tope, sin teléfonos. Lo implementa WhatsApp, y su versión apagada está en `Infrastructure/StructuredData/`, registrada con `TryAdd`. La E14 suma a la misma carpeta los cuatro puertos de los formularios.
