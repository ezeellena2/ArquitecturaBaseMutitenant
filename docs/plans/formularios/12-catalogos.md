# E12 · Catálogos base (spec)

**Estado:** pendiente · spec de la primera etapa del bloque (decisiones 34 y 78). Es el ABM de catálogos completo, sin formularios: lo que ArquitecturaBase llama E1 (tipo de valor, carga de una vez y hasta 10.000 opciones), sobre los catálogos a mano, de los miembros y de los contactos. Acá nace el módulo `Forms`; la E14 le suma los formularios y la pregunta «Elegir de un catálogo». `/plan-etapa` la convierte en plan cuando le toque, después de verificar los puntos marcados **[E11]** y **[E9]** contra el código real.

**Fuentes:**
- alcance: [hoja de ruta](../2026-10-12-formularios-dinamicos-hoja-de-ruta.md), §E12, y la [evaluación del traspaso 2](../2026-10-24-formularios-evaluacion-traspaso-2.md), §3;
- reglas: [AGENTS.md](../../../AGENTS.md) y las [fichas](../../rules/README.md);
- referencia de producto: el traspaso 2 de ArquitecturaBase, anexo C §10 y anexo D (CA1-A a CA3). Es solo referencia, no código para copiar.

**Requisitos:** la E11 (los contactos de la bandeja Clientes y sus etiquetas) y la E9 (cuotas y fuzz). El bloque arranca después de la E10, y antes de esta etapa va la base del [plan de desarrollo](../2026-09-27-plan-de-desarrollo.md#etapas-12-a-25-formularios-por-whatsapp): los botones (la F5 que sale de la 10), la campana en el núcleo y el tamaño general.

## Qué leer

Antes de empezar:
1. **Decisiones** de la hoja de ruta: las de la §13 de esta spec.
2. **Fichas:** [persistencia-ef](../../rules/persistencia-ef.md), [multitenancy](../../rules/multitenancy.md), [auditoria](../../rules/auditoria.md), [guardado](../../rules/guardado.md), [concurrencia](../../rules/concurrencia.md), [idempotencia](../../rules/idempotencia.md), [api-http](../../rules/api-http.md), [permisos](../../rules/permisos.md), [paginado-y-busqueda](../../rules/paginado-y-busqueda.md), [result-y-errores](../../rules/result-y-errores.md), [validacion](../../rules/validacion.md), [textos-libres](../../rules/textos-libres.md), [numeros-y-moneda](../../rules/numeros-y-moneda.md), [fechas-y-zonas](../../rules/fechas-y-zonas.md), [textos-y-traducciones](../../rules/textos-y-traducciones.md), [logs](../../rules/logs.md), [cuotas](../../rules/cuotas.md), [modulos](../../rules/modulos.md) y [tests](../../rules/tests.md).
3. **Código para copiar:** el módulo Asistente, pieza por pieza, en la §9.
4. **Pantallas:** los tableros de la §10, con el recorrido `armar-un-catalogo` del front, y la maqueta para lo que manda en la §10.
5. **Referencia de producto:** la §14.

## 1. Alcance

**Entra:**
- **El módulo `Forms`:** quitable, con su feature `forms`, sus permisos de catálogos y la entrada «Gestión de formularios» del menú, que en esta etapa tiene solo «Catálogos» (34 y 78).
- **Catálogos de la organización:** listado, alta con el origen fijo (a mano, de los miembros o de los contactos), datos y estado «Activo», «De baja» o «No se puede usar».
- **Dar de baja y reactivar un catálogo** desde su pantalla, con su versión, sin borrarlo. Dar de baja uno con filtros no necesita vista previa, y reactivarlo sí.
- **A mano:**
  - **tipo de valor:** texto, entero, decimal o fecha, que se elige al crear y no cambia;
  - opciones con texto, detalle, código y, en un catálogo tipado, valor. Cada texto es uno solo, tal como lo escribe la persona (35);
  - las opciones se dan de baja sin borrarse; se reordenan con «subir» y «bajar», o de la A a la Z al guardar;
  - **la carga de una vez:** pegar desde Excel (agrega) o importar un XLSX o un CSV (reemplaza), con vista previa;
  - **exportar** y bajar la plantilla;
  - **hasta 10.000 opciones** por catálogo, contando las dadas de baja.
- **De los contactos** (55): las opciones son los contactos de WhatsApp de la organización, los de la bandeja Clientes, con su nombre. Su vista previa elige un cliente de ejemplo, igual que la de los miembros. Ver la §4b.
- **El nombre de un contacto** (81): un contacto sin nombre toma el de su perfil de WhatsApp cuando escribe, y se puede cambiar. El teléfono nunca se muestra.
- **Con filtros:**
  - sobre la fuente «Miembros de la organización», con condiciones armadas por el sistema y «Sin incluir a quien lo recibe»;
  - vista previa con una persona de ejemplo, elegida a propósito: el teléfono, con solo el desplegable «Persona» arriba, que se actualiza sola;
  - el valor es siempre de texto.
- **Permisos:** `formcatalogs.read` y `formcatalogs.manage`.

**No entra:**
- los formularios y la pregunta «Elegir de un catálogo»: elegir un catálogo en una pregunta, mandar, responder y «Lo usan» (E14);
- campos extra y qué viaja a WhatsApp (E13);
- datos de las personas y filtros por destinatario (E16);
- catálogos con una consulta SQL;
- productos u otras fuentes externas, que no existen en la plantilla (los clientes son la fuente «Contactos», 55);
- la búsqueda en vivo o las listas dependientes, porque necesitan el intercambio cifrado con Meta;
- límites por plan: los topes son iguales para todas (42).

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
| C14 | **El nombre de un contacto** (81): la entrada de mensajes de WhatsApp completa el nombre de un contacto sin nombre con el de su perfil, una sola vez; si una persona lo cambia, la entrada no lo pisa. En las opciones, un nombre de más de 30 caracteres se corta como el de un miembro (36). Un contacto sin nombre ni perfil no se ofrece. | El nombre del contacto es opcional en la E11, y la entrada no lo completaba. |

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
| `Name` | texto ≤80 | obligatorio |
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
- **Un contacto borrado** deja de ofrecerse.
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

## 9. Archivos

Cada pieza nueva copia la **estructura** del archivo modelo de este repo; no se copia código de ArquitecturaBase. **Abreviaturas:** `D/`, `A/`, `I/`, `P/` son `src/ArquitecturaBaseMultitenant.{Domain,Application,Infrastructure,Api}/`, y `F/` es `ArquitecturaBaseMutitenantFront/`.

| Archivo nuevo | Modelo en este repo |
| --- | --- |
| `D/Forms/FormCatalog.cs`, `FormCatalogItem.cs`, `FormCatalogSource.cs`, `FormCatalogValueType.cs`, `FormCatalogErrors.cs`, `FormCatalogImportErrors.cs` | `D/Assistant/AssistantOrganizationSettings.cs`, `D/Assistant/AssistantErrors.cs` |
| `D/Authorization/Permissions.cs`: clase `Forms` en `OrganizationScoped`, con `formcatalogs.read` y `formcatalogs.manage` | la clase `Assistant` del mismo archivo |
| `A/Modules/Forms/FormsModule.cs`; `I/Modules/Forms/FormsModule.cs`; `P/Modules/Forms/FormsApiModule.cs`; `Program.cs`: `AddFormsModule` en las tres capas | `A/Modules/Assistant/AssistantModule.cs` y sus pares; la línea del Asistente en `Program.cs` |
| `A/Modules/Forms/Services/FormsFeatureDefinitionContributor.cs` (`new("forms", FeatureScope.Business, false)`) | `AssistantFeatureDefinitionContributor.cs` |
| `A/Modules/Forms/Resources/FormsTexts{,.en}.resx`, `FormsErrorTextSource.cs`; `P/Modules/Forms/FormsContractContributor.cs` | los del Asistente |
| `A/Modules/Forms/Interfaces/Services/IFormCatalogService.cs`, `IFormCatalogImportService.cs` y sus servicios, con los helpers `FormCatalogFiltersPolicy`, `FormCatalogItemEditor`, `FormCatalogPositionGuard`, `FormCatalogFileResolver` (lee el archivo o lo pegado), `FormCatalogImportPolicy` (decide altas, cambios, bajas y errores, sin escribir) y `FormCatalogExportBuilder` (arma las filas que se exportan) | `AssistantConversationService.cs` y sus helpers; los sufijos son los de la lista cerrada de [capas-y-flujo](../../rules/capas-y-flujo.md) |
| `A/Modules/Forms/Interfaces/Integrations/IFormCatalogSpreadsheet.cs` e `I/Modules/Forms/FormCatalogSpreadsheet.cs` (ClosedXML o CSV, solo celdas) | `IWhatsAppContactSpreadsheet.cs` y `I/Modules/WhatsApp/WhatsAppContactSpreadsheet.cs` |
| `A/Interfaces/Persistence/IMemberDirectoryReader.cs` e `I/Persistence/Readers/MemberDirectoryReader.cs` (C2) | los readers del núcleo de `I/Persistence/Readers/` |
| WhatsApp: la entrada de mensajes completa el nombre de un contacto sin nombre con el de su perfil (C14) | `I/Modules/WhatsApp/WhatsAppInboundProcessor.cs` |
| `A/Interfaces/Integrations/StructuredData/IStructuredContactSource.cs`, su modelo y `I/StructuredData/DisabledStructuredContactSource.cs`; en WhatsApp, `WhatsAppStructuredContactSource.cs` | `A/Interfaces/Integrations/Messaging/ILoginCodeChannel.cs`, `I/Messaging/Email/EmailLoginCodeChannel.cs` y `A/Modules/WhatsApp/Services/WhatsAppLoginCodeChannel.cs` |
| `A/Modules/Forms/Catalogs/MembersCatalogSource.cs` (columnas, operadores y versión) | — (es una definición, sin servicios) |
| `A/Modules/Forms/Interfaces/Persistence/IFormCatalogRepository.cs`, `IFormCatalogReader.cs`, `IFormWorkLease.cs`, `UniqueKeys.cs` | `A/Modules/Assistant/Interfaces/Persistence/` |
| `I/Modules/Forms/Persistence/FormsModelContributor.cs`; `Configurations/FormCatalogConfiguration.cs`, `FormCatalogItemConfiguration.cs`, `FormWorkLeaseConfiguration.cs`; `Readers/FormCatalogReader.cs` | `I/Modules/Assistant/Persistence/` (`AssistantModelContributor.cs`) |
| `I/Persistence/Migrations/<fecha>_FormCatalogs.cs` (con las restricciones diferidas en `Sql`, en una línea y sin `\r`) y `<fecha>_SystemRoleFormCatalogPermissions.cs` | `20261010011328_AssistantModule.cs`, `20261010203618_SystemRoleAssistantPermissions.cs` |
| `P/Modules/Forms/Controllers/BusinessFormCatalogsController.cs`, `Contracts/*HttpRequest.cs` | `P/Modules/Assistant/Controllers/BusinessAssistantController.cs` |
| `IdentityAccessTests`: el `MemberDirectoryReader` en la lista cerrada del núcleo | las entradas del núcleo de esa guarda |

**Front:**

| Archivo nuevo | Modelo |
| --- | --- |
| `F/src/modules/forms/registration.ts`: rutas `/org/formularios/catalogos` y `/org/formularios/catalogos/:id`; guarda con `features.includes("forms")` y `formcatalogs.read`; la entrada «Gestión de formularios» del menú (34), en el teléfono y en la compu; aporta `resources` es/en de `forms` y sus entradas de `enums` | `F/src/modules/assistant/registration.ts` |
| `F/src/modules/forms/{api,components,hooks,pages}/`, `errors.ts`: el listado, el editor con la tabla editable, el diálogo de pegar e importar, el editor de filtros y la vista previa | `F/src/modules/assistant/`. La tabla editable es una pieza nueva: su excepción a las filas de solo lectura se escribe en la ficha de tablas del front |
| `F/src/modules/forms/locales/{es,en}/forms.json` y `enums.json` | `F/src/modules/assistant/locales/{es,en}/assistant.json` y `enums.json` |
| `F/src/app/moduleComposition.ts`: suma `formsRegistration` | la línea del Asistente |

**Punteros:** cada carpeta nueva lleva su `AGENTS.md` de una a tres líneas («qué va acá · Leé ficha · Copiá de archivo») y un `CLAUDE.md` con `@AGENTS.md`, más su fila en `docs/architecture/arbol.md`. Se crean con la carpeta, no antes (`HarnessTests`). Al cerrar la etapa nacen `docs/features/formularios.md`, con la sección de catálogos, y la fila de Forms en «Dónde va cada cosa» de `AGENTS.md`; cada etapa siguiente suma su sección.

## 10. Pantallas (tableros)

**Navegación (34 y 78):** «Gestión de formularios» llega con esta etapa, y su panel tiene solo «Catálogos». Los tableros de la etapa muestran solo eso.

**Aprobados y publicados en el lienzo v141** (2026-10-25, front `93cb7d01`), con sus teléfonos, sin «Textos en» ni «Cambios sin guardar», y con el origen y el tipo de valor como la A11: en su campo, apagados y en gris, sin «No se cambia» (43):
- `FRM-Catalogos`: el listado, sin la columna «Lo usan», que llega con los formularios;
- `FRM-Catalogo-Nuevo`: el origen, con la tarjeta «De los contactos de WhatsApp», y el tipo de valor;
- `FRM-Catalogo` (`CA1-A`): el editor de un catálogo a mano con la sección «Opciones»: en su banda, «+ Agregar opción» y un ⋮ con Pegar desde Excel, Importar, Exportar, Descargar la plantilla, Ordenar de la A a la Z y Generar los códigos que faltan; abajo, solo «Buscar opción» y el filtro de estado; la tabla editable en la compu, la selección de filas para dar de baja o reactivar, solo el ⋮ en cada fila (con «Editar» adentro, sin un ✎ aparte; lienzo v143), y el teléfono al costado. En el celular, el editor por opción;
- `FRM-Catalogo-Importar` (`CA2`): archivo o filas pegadas, revisar (Errores, Altas, Cambios y Bajas, por fila y columna) y aplicar, con la confirmación de bajas, «Aplicar sin las N filas con errores» y el 409;
- `FRM-Catalogo-Tipo` (`CA3`): el tipo de valor se elige al crear, con la columna «Valor» y el error en el campo;
- `FRM-Catalogo-Filtros`: un catálogo de los miembros, con sus filtros y la vista previa;
- `FRM-Catalogo-Contactos`: un catálogo «De los contactos», con sus filtros por etiqueta y la vista previa (55).

**Estados que se dibujan** (cada uno tiene que poder producirse; si no, es un bug):
- **Listado:** filtro abierto, sin coincidencias, cargando, vacío y error.
- **Catálogo a mano:**
  - opción sin completar, código repetido, texto repetido (avisa sin frenar), texto de más de 30 y valor de otro tipo;
  - guardado, no se guardó y la revisión si otra persona guardó;
  - sin opciones, 1.200 opciones con búsqueda y páginas, 10.000 opciones, nuevo y cargando;
  - pegar e importar grisados en un catálogo nuevo o con cambios sin guardar, con el motivo en su tooltip;
  - con solo `formcatalogs.read`: la tabla de lectura, sin casillas, con Exportar y la plantilla.
- **Importar:** errores por fila y columna, muchas bajas que piden confirmar, muchas filas con error, un valor ambiguo, el 409 y otra importación en curso.
- **Catálogo con filtros:**
  - con más de 200 opciones, se guarda y avisa con ⓘ (decisiones 22 y 37);
  - sin persona de ejemplo, no se crea;
  - un rol que ya no existe deja el catálogo en «No se puede usar».
- **Catálogo de los contactos:** sin WhatsApp, «No se puede usar».

**Mandan sobre esta spec en:**
- **Las fichas del front** (pedidos del usuario del 2026-10-25 y del 2026-10-26):
  - ninguna sección lleva cantidades en su encabezado: nada de «N opciones · N activas»;
  - un dato por celda, en un renglón;
  - las fechas, completas, con fecha y hora en la zona de quien mira; nunca «hoy», «ayer» ni «hace…»;
  - sin rótulos que expliquen: la vista previa es el teléfono al costado;
  - lo dado de baja es una opción del filtro «Estado», no una vista aparte, en plural y de acuerdo con lo que se lista: en la lista de catálogos, «Activos · Dados de baja»; en la tabla de opciones, «Todas · Activas · Dadas de baja»;
  - el catálogo no tiene «Una opción | Varias»: lo decide la pregunta.
- **La tabla de opciones no se aplasta:** cada columna tiene su ancho mínimo (Texto y Detalle 12rem, Código 6rem, el valor y cada campo 8rem). Si no entra, la tabla se desplaza a lo ancho dentro de su caja, con la casilla, el número y «Texto» fijos a la izquierda y el ⋮ de la fila fijo a la derecha. Se copia de la maqueta: `OptionsSection.tsx` y `fixedColumns.ts` (a1303a5 y af5eceb).
- **Guardado (C3):** todo se guarda junto en un `PUT` del catálogo, con su versión, y viaja solo lo que cambió.
- **Nuevo catálogo:** primero se elige el origen, y para los escritos a mano, el tipo de valor; el nombre se escribe en el editor y el `POST` sale de «Crear catálogo».
- **Persona de ejemplo:** sin ella no se crea un catálogo de miembros con condiciones (`Forms.Catalog.SampleRequired`, «Elegí una persona.»).
- **La vista previa de un catálogo de miembros o de contactos** es el teléfono, con solo el desplegable «Persona» arriba, y se actualiza sola; no hay «Probar». Se ve con `formcatalogs.read`, como el catálogo.

## 11. Tests

| Dónde | Qué |
| --- | --- |
| `tests/…Domain.UnitTests/Forms/` | origen y tipo fijos; código único sin distinguir mayúsculas; intercambio de posiciones en los extremos; valor obligatorio y del tipo; el estado «Activo» o «No se puede usar» y sus motivos |
| `tests/…Application.UnitTests/Modules/Forms/` | validación de filtros contra la fuente; la lectura de archivos y de lo pegado con «1.500», «1,500», «12.345» y «3/4/2026» en es y en; topes del archivo con celdas vacías con formato; el plan de importación (altas, cambios, bajas, columna ausente y columna vacía); exportar y volver a importar sin diferencias; el corte de nombres largos en la vista previa |
| `tests/…Api.IntegrationTests/Modules/Forms/` | cada ruta con 401, 403, feature apagada = 404, validación, 409, idempotencia y paginado; guardar sin cambios compara la versión sin tocar ni auditar opciones; guardar una opción entre 10.000 escribe y audita solo esa, y sube la versión del catálogo; un código repetido en un intercambio da el error de validación, nunca un 500 en el commit; subir y bajar con dos personas a la vez; aplicar con otro archivo = 409; dos importaciones de la misma persona a la vez = `Busy`, y una barrera vencida se vuelve a tomar; vista previa sin escribir; ejemplos exportados de cada respuesta |
| `tests/…Api.IntegrationTests/Modules/WhatsApp/` | un contacto sin nombre toma el de su perfil al escribir; uno con nombre puesto por una persona no se pisa (C14) |
| Volumen | un catálogo de 10.000 opciones: listar, buscar, guardar una opción y exportar, sin traer todo para quedarse con poco, con EXPLAIN |
| Aislamiento | la fuente «Miembros» nunca devuelve miembros de otra organización ni correos; la de «Contactos», ni contactos de otra ni teléfonos; la persona o el cliente de ejemplo de otra organización da error; cada ruta nueva le responde 404 a otra organización |
| Guardas (partials del módulo) | `IdentityAccessTests`, `ExplicitRouteInventoryTests`, `ModuleControllersTests`, `ApplicationServicesTests`, `ModuleIsolationTests`, `ResourceParityTests` (suma los resx de Forms) y `PermissionTextsTests` |
| Front | páginas de catálogos, la tabla editable, el diálogo de importar, el editor de filtros y la vista previa, con MSW, axe y los ejemplos exportados validados con zod estricto; `registration`, `navigation` y `composition` del módulo; los botones según la decisión 43 |
| E2E y recorrido | pegar opciones desde una planilla, importar un archivo que da de baja algunas, exportar; un catálogo de miembros con filtros y su vista previa; uno de los contactos filtrado por etiqueta |

## 12. Puntos de contacto

Al planificar la E12 se verifican contra el código real.

| Marca | Qué hay que confirmar | Qué asume esta spec |
| --- | --- | --- |
| [E11] | Los contactos de la bandeja Clientes (`WhatsAppCustomer`): las etiquetas son `text[]` y el nombre es opcional. | `IStructuredContactSource` los lista por página, con filtros por el texto de la etiqueta, sin teléfonos; la entrada completa el nombre (C14). |
| [E11] | La planilla de contactos (`IWhatsAppContactSpreadsheet`, ClosedXML). | `IFormCatalogSpreadsheet` copia su forma. |
| [E9] | Las cuotas compartidas ([cuotas](../../rules/cuotas.md)). | Las rutas de Forms cuentan en las cuotas generales; no se definen perfiles propios. |
| [E9] | Redis. | La E12 no cachea nada nuevo. |
| #51 | `Enter` borra `TenantKind`. | La E12 no tiene workers. |

## 13. Decisiones de producto de la etapa

Están en la §5 de la hoja de ruta y no se vuelven a preguntar:
- **12, 22 y 37:** un catálogo con filtros que da más de 200 opciones se puede guardar. La vista previa avisa, y el bloqueo es al mandar (E14).
- **35:** el contenido no tiene idioma.
- **36:** un nombre de más de 30 caracteres se corta en 29 y se le agrega «…».
- **42:** hasta 10.000 opciones, igual para todas las organizaciones.
- **43:** botones y avisos.
- **55:** el origen «De los contactos».
- **78:** los catálogos van primero, sin formularios.
- **81:** un contacto sin nombre toma el de su perfil de WhatsApp, y se puede cambiar; el teléfono nunca se muestra.

## 14. Referencia tomada de ArquitecturaBase

Se toma solo como referencia, del traspaso 2 (anexo C §10 y anexo D). Las rutas son de `ArquitecturaBase`.

| Tema | Lo que sirve |
| --- | --- |
| Opciones a mano | El código es la identidad y no se ve en WhatsApp. Las opciones no se borran. La posición es del servidor, con restricción diferida. El cliente nunca manda posiciones. |
| Carga de una vez | Pegar agrega e importar reemplaza; vista previa sin transacción; SHA-256 bajo los locks; confirmación con más del 25 % de bajas y del 10 % de errores; los topes de archivo, incluidas las celdas vacías con formato; CSV UTF-8 estricto; exportar sin diferencias al reimportar. |
| Tipo de valor | Se elige al crear y no cambia; canónico; Excel y PDF muestran el valor junto al texto. |
| Filtros | Valores siempre como parámetros, columnas de una lista cerrada, timeout de 3 s y una fila más que el tope. La vista previa no toma la identidad de la persona de ejemplo. |
| Errores pagados | Posiciones del cliente; una página tardía de otro catálogo cargada en el editor; cada guardado reescribía las 10.000 opciones; los números leídos en la cultura equivocada al importar. |
| Pantallas | CA1-A a CA3. Sirven para el contenido; el dibujo es de nuestro lienzo, con la decisión 43. |
| No se toma | La vista SQL sobre `AspNetUsers` y roles globales; los permisos y el bloqueo globales; el idioma original y las traducciones; la política `forms-export` por proceso. |

## 15. Cómo se cierra

La puerta de la etapa ([etapa en paralelo](../../guides/etapa-en-paralelo.md)), con la lista de control de `/puerta`. No hay prueba con Meta, porque la E12 no manda nada por WhatsApp. El recorrido prueba:
1. **A mano:** «Proveedores» pegando desde una planilla, y «Recargo por zona», decimal, con el valor en su columna.
2. **Carga de una vez:** importar una planilla real de unas 1.000 filas, con algunas bajas, y exportarla de nuevo sin diferencias.
3. **Miembros:** un catálogo con filtros y su vista previa con una persona de ejemplo, vacío y con opciones, con «Sin incluir a quien lo recibe».
4. **Contactos:** un catálogo de los contactos de Clientes, filtrado por etiqueta, sin teléfonos a la vista. Un contacto nuevo que escribe sin nombre aparece con el de su perfil (81).
5. **Volumen:** 10.000 opciones: buscar, guardar una y exportar, con EXPLAIN.

## 16. Criterios

Salen de los reviews de ArquitecturaBase (E1) y del traspaso del 17/10. El plan los toma como lista de control, junto con la de `/puerta`:
- **Los límites del canal se validan al guardar** (30 y 300), no al mandar; la vista previa marca lo que no entra.
- **Referencias por id,** nunca por nombre: un rol renombrado no vacía el catálogo, y dar de baja un rol tampoco lo invalida.
- **Ninguna lectura trae todo para quedarse con poco:** listar, buscar, exportar y la vista previa leen en páginas o lotes, probado con 10.000 opciones.
- **Guardar sin cambios no toca las opciones,** y guardar una opción no reescribe las demás.
- **Lo que viene de un archivo** se lee con la cultura del pedido y rechaza lo ambiguo.
- **Contratos:** `""` no es `null`; una entrada inválida es 400, nunca 500; un campo omitido no apaga nada.
- **En el front:**
  - la búsqueda espera 300 ms y conserva el editor y el foco;
  - una página tardía de otro catálogo se descarta;
  - el archivo vive solo en el diálogo y se descarta al salir;
  - el front no tapa la validación del servidor;
  - un 403 en una acción no oculta la pantalla de lectura.
