# E13 · Catálogos base (spec)

**Estado:** spec de la tercera etapa del bloque (decisión 34). Es lo que ArquitecturaBase llama E1: tipo de valor, carga de una vez y hasta 10.000 opciones, sobre los catálogos a mano y con filtros. Se apoya en la [spec de la E12](e12-formularios.md): usa su modelo, sus puertos y sus convenciones, y acá se escribe solo lo que la E13 agrega o cambia. `/plan-etapa` la convierte en plan cuando le toque, después de verificar los puntos marcados **[E12]** contra el código que haya dejado la E12, y los **[Meta]** contra la documentación vigente.

**Fuentes:**
- alcance: [hoja de ruta](../2026-10-12-formularios-dinamicos-hoja-de-ruta.md), §E13, y la [evaluación del traspaso 2](../2026-10-24-formularios-evaluacion-traspaso-2.md), §3;
- reglas: [AGENTS.md](../../../AGENTS.md) y las [fichas](../../rules/README.md);
- referencia de producto: el traspaso 2 de ArquitecturaBase, anexo C §10 y anexo D (CA1-A a CA6). Es solo referencia, no código para copiar.

## 1. Alcance

**Entra:**
- **Catálogos de la organización:** listado, alta con el origen fijo (a mano o con filtros), datos y estado «Se puede usar», «Dado de baja» o «No se puede usar».
- **Dar de baja y reactivar un catálogo** desde su pantalla, con su versión, sin borrarlo. Dado de baja, no se elige en preguntas nuevas, y los formularios publicados que lo usan no se pueden mandar: la confirmación de la baja lo advierte. Dar de baja uno con filtros no necesita vista previa, y reactivarlo sí.
- **A mano:**
  - **tipo de valor:** texto, entero, decimal o fecha, que se elige al crear y no cambia;
  - opciones con texto, detalle, código y, en un catálogo tipado, valor. Cada texto es uno solo, tal como lo escribe la persona (35);
  - las opciones se dan de baja sin borrarse; se reordenan con «subir» y «bajar», o de la A a la Z al guardar;
  - **la carga de una vez:** pegar desde Excel (agrega) o importar un XLSX o un CSV (reemplaza), con vista previa;
  - **exportar** y bajar la plantilla;
  - **hasta 10.000 opciones** por catálogo, contando las dadas de baja.
- **De los contactos** (decisión 55): las opciones son los contactos de WhatsApp de la organización, los de la bandeja Clientes, con su nombre. Sirve para elegir a un cliente en un formulario y mandarle el PDF a su chat (47). Ver la §4b.
- **Con filtros:**
  - sobre la fuente «Miembros de la organización», con condiciones armadas por el sistema y «Sin incluir a quien lo recibe»;
  - vista previa con una persona de ejemplo, elegida a propósito;
  - el valor es siempre de texto.
- **Las preguntas «elegir una» y «elegir varias» de un catálogo** en el editor de formularios.
- **Al mandar:**
  - las opciones se arman para cada destinatario y viajan dentro del mensaje;
  - no se puede mandar con más de 200 opciones en «elegir una», con más de 20 en «elegir varias», ni sin ninguna si la pregunta es obligatoria.
- **Al responder:**
  - la opción tiene que estar en la oferta, y se guardan el texto, el detalle, el código y el valor que vio la persona;
  - si era una opción a mano dada de baja, no se guarda nada, el operador ve el motivo y puede mandar de nuevo.
- **Permisos:** `formcatalogs.read` y `formcatalogs.manage`.

**No entra:**
- campos extra y qué viaja a WhatsApp (E13b);
- datos de las personas y filtros por destinatario (E13c);
- catálogos con una consulta SQL;
- productos u otras fuentes externas, que no existen en la plantilla (los clientes son la fuente «Contactos», 55);
- la búsqueda en vivo o las listas dependientes, porque necesitan el intercambio cifrado con Meta;
- límites por plan: los topes son iguales para todas (42).

## 2. Decisiones técnicas

Valen las de la E12. Las de producto están en la §5 de la hoja de ruta.

| # | Decisión | Por qué |
| --- | --- | --- |
| C1 | Los catálogos son de la organización: `ITenantOwned`, en el esquema `tenant` y en el módulo `Forms`. | Como los formularios (decisión 1). |
| C2 | **La fuente «Miembros» se define en el código, no como vista SQL.** Es un lector de Forms sobre las tablas del núcleo, con una lista cerrada de columnas y valores como parámetros de EF. Lleva un número de versión: si cambia, los catálogos viejos pasan a «No se puede usar» con su motivo. | El repo no tiene vistas SQL, y una vista con RLS necesita `security_invoker`, porque si no se saltea el aislamiento. |
| C3 | **Los ids que viajan a Meta son locales a la oferta** (`o1` … `o200`). La sesión guarda a qué ítem corresponde cada uno. También en «elegir varias». | No se mandan a Meta ids internos ni de personas, y «estar en la oferta» se comprueba solo. |
| C4 | **La oferta la arma Forms y la guarda WhatsApp,** cifrada en la sesión (`ProtectedOffer`). Se borra con la retención, una vez procesada. | Forms conoce los catálogos y WhatsApp conoce la sesión. Como contiene nombres de personas, va cifrada. |
| C5 | Para un catálogo a mano, la captura comprueba que la opción siga activa; si no, `Forms.Record.OptionWithdrawn`. Para uno con filtros alcanza con que esté en la oferta. | Con filtros, lo que vio la persona es lo que vale. |
| C6 | **Todo se guarda junto y viaja solo lo que cambió.** Un solo `PUT` del catálogo, con su versión (`IVersioned`, 409), lleva: <ul><li>nombre, descripción e `isActive`;</li><li>solo las opciones nuevas o cambiadas: las existentes con su `itemId` y las nuevas con un id local;</li><li>los movimientos `{itemId, direction}`, hasta 500;</li><li>`sortAlphabetically`, que reordena por texto al guardar.</li></ul>El pedido pesa hasta 512 KiB. El servidor escribe y audita solo las filas que cambiaron; si nada cambió, no escribe. Responde la versión y el mapa de ids locales a ids del servidor. | Configuración interna: una UoW, versión y 409, sin recibos. En ArquitecturaBase cada guardado reescribía las 10.000 opciones y su auditoría. |
| C7 | La posición la maneja el servidor y es única por catálogo, con una restricción `DEFERRABLE INITIALLY DEFERRED` hecha con SQL en la migración. Subir y bajar intercambian la posición con la vecina, bajo un bloqueo del catálogo. El código también es único con una restricción diferida, para permitir intercambios en un guardado. | Las posiciones que manda el cliente se rompían. |
| C8 | Una pregunta de elegir toma sus opciones de la pregunta o de un catálogo (`catalogId`). Cambiar el catálogo, o pasar de opciones propias a un catálogo, en una pregunta publicada crea una pregunta nueva, como cambiar el tipo. | Cambiar opciones no obliga a republicar, y las respuestas viejas no cambian de sentido. |
| C9 | La gramática de condiciones es general: `{column, operator, values[]}`, unidas con «y». La E15 la reusa para los filtros de la planilla. | Una sola gramática. |
| C10 | El Flow pasa a un perfil con datos iniciales. Cada pregunta de catálogo es un `Dropdown` o un `CheckboxGroup` con `data-source: ${data.field_<N>}`, y la pantalla declara su `data` con un `__example__` **[Meta]**. | Permite mandar opciones distintas a cada persona sin un endpoint de intercambio. |
| C11 | **Tipo de valor.** `Text` (por defecto), `Integer`, `Decimal` o `Date`, fijo desde el alta; un catálogo con filtros es siempre `Text`. En uno tipado, toda opción, activa o no, lleva su valor: enteros de hasta 18 dígitos, decimales sin redondeo y fechas ISO. Se guarda canónico. Lo que se ve en WhatsApp sigue siendo el texto. | Es la regla de ArquitecturaBase (C1-02). |
| C12 | **Carga de una vez.** Sobre un catálogo guardado y sin cambios en la pantalla: <ul><li>pegar **agrega**, e importar un archivo **reemplaza**: lo que no viene se da de baja, no se borra;</li><li>la vista previa no guarda ni abre transacción;</li><li>aplicar vuelve a leer los mismos bytes, compara el SHA-256 y revalida permisos y versión bajo el bloqueo del catálogo, en una sola UoW. Un archivo distinto o una versión vieja dan 409;</li><li>la identidad de una fila es su código, sin distinguir mayúsculas. Una columna opcional que falta conserva; presente y vacía, limpia;</li><li>más del 25 % de bajas pide confirmar la cantidad exacta; aplicar sin las filas con errores tiene su botón, y con más del 10 % de filas con error pide confirmarlo.</li></ul> | Es la regla de ArquitecturaBase (C1-03). |
| C13 | **Topes del archivo:** 10 MiB, 64 columnas, 10.000 filas de datos y 640.128 celdas de trabajo, contando las vacías con formato y las fórmulas matriciales; un XLSX hasta 100 MiB descomprimido. Las fórmulas cuentan por su valor guardado. El CSV va en UTF-8 estricto. Se lee con ClosedXML, que ya está en el repo. | Las celdas vacías con formato pasaron los topes en ArquitecturaBase. |
| C14 | **Números y fechas de un archivo o de lo pegado** se leen con la cultura del pedido. Lo que se puede leer de dos formas («1.500», «12.345», «3/4/2026») da `Forms.CatalogImport.ValueAmbiguous`, en su fila y columna. | Lista de control de `/puerta`, punto 1. |
| C15 | **Exportar** escribe el valor canónico como texto, con las columnas de la plantilla, y se vuelve a importar sin diferencias. La plantilla trae una columna por dato, si es obligatorio, y una fila de ejemplo. | Exportar e importar son el camino para los cambios grandes. |
| C17 | **La fuente «Contactos» la da WhatsApp por un puerto neutral del núcleo** (`IStructuredContactSource`, en `Application/Interfaces/Integrations/StructuredData/`). Forms nunca referencia a WhatsApp; sin WhatsApp, la implementación apagada no devuelve contactos y el origen no aparece. | `ModuleIsolationTests`, como los puertos de la E12. |
| C16 | **La vista previa, aplicar y exportar van de a uno por persona,** con el candado de PostgreSQL del núcleo, como los `I<X>Lock` de `Application/Interfaces/Persistence` (`AdvisoryLockExtensions`). La segunda da `Forms.CatalogImport.Busy`. Cuentan en las cuotas generales ([cuotas](../../rules/cuotas.md)), sin perfil propio. | Nunca un limitador por proceso. |

## 3. Modelo de datos

Todas las tablas son `ITenantOwned` en `tenant`, con RLS, FK compuestas y `TextLimits`, como en la E12.

**FormCatalogs** (`IAuditable`, `IVersioned`)

| Columna | Tipo | Regla |
| --- | --- | --- |
| `Id`, `TenantId` | uuid | — |
| `Name` | texto ≤80 | obligatorio |
| `Description` | texto ≤300? | opcional |
| `Source` | texto | `Manual`, `Members` o `Contacts`; no cambia después de crear |
| `ValueType` | texto | `Text`, `Integer`, `Decimal` o `Date`; no cambia; `Members` es siempre `Text` (C11) |
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

**FormCatalogItems** (`IAuditable`; solo los catálogos a mano; la versión es la del catálogo, C6)

| Columna | Tipo | Regla |
| --- | --- | --- |
| `Id`, `TenantId`, `CatalogId` | uuid | FK compuesta |
| `Code` | texto ≤40 | obligatorio; único por catálogo sin distinguir mayúsculas (`CodeNormalized` + `UniqueKeys.FormCatalogItemCode`, diferida). No se ve en WhatsApp. |
| `Text` | texto ≤30, validado al guardar | obligatorio; es el título de la opción en WhatsApp. Un texto repetido avisa, sin frenar |
| `Detail` | texto ≤300, validado al guardar | opcional; la descripción de la opción. Si no hay detalle, el Flow omite `description` |
| `NumberValue` | numeric? | en `Integer` (escala 0, hasta 18 dígitos) y `Decimal` (sin redondeo) |
| `DateValue` | date? | en `Date` |
| `Position` | int | única por catálogo, `DEFERRABLE INITIALLY DEFERRED` (C7) |
| `IsActive` | bool | dar de baja = `false`; no se borra |
| — | CHECK | el valor que corresponde al tipo del catálogo, y ningún otro |

Un catálogo a mano tiene hasta **10.000 opciones**, entre activas y dadas de baja, comprobado bajo el bloqueo del catálogo. Lo que cambia en la E12:
- **`FormDraft` y `FormVersions`:** las preguntas `Choice` y `MultiChoice` suman `catalogId` (C8).
- **`FormRecordValues`:** suma `CatalogId uuid?`, `ChoiceCode`, `ChoiceDetail` y `ChoiceValue` (texto canónico del valor, si el catálogo es tipado), sellados tal como estaban al responder. `ChoiceId` es el id del ítem (opción a mano o miembro), y `ChoiceLabel` es el texto que vio la persona.
- **`WhatsAppFlowSessions`:** suma `ProtectedOffer` (bytea, cifrado con `IPayloadProtector`) y `OfferHash`. La oferta es `[{fieldId, options: [{localId, catalogId, itemId, text, detail?, code?, value?}]}]`.

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
- **«Sin incluir a quien lo recibe»:** `MemberId <> el miembro de quien recibe`. Es el único parámetro de contexto en esta etapa, y lo pone el servidor.
- **Ejecución:**
  - lector de Forms en `Infrastructure/Modules/Forms/Persistence/Readers/FormMembersCatalogReader.cs`, sobre `Member`, `CompanyMembership`, las asignaciones de roles y el nombre de `ApplicationUser`;
  - EF con parámetros y RLS de la organización, con la conexión normal;
  - un timeout corto (3 s);
  - lee una fila más que el tope (201 en «elegir una», 21 en «elegir varias»): si la hay, devuelve `exceedsLimit`.
- **Guarda de acceso global:** leer la cuenta global (`identity`) suma el lector a `IdentityAccessTests` con el partial del módulo, como el Asistente.
- **Versión:**
  - `sourceVersion` es 1;
  - si una versión nueva de la fuente cambia las columnas o los operadores, un catálogo con `sourceVersion` vieja pasa a «No se puede usar»;
  - sin migración de datos.

## 4b. La fuente «Contactos» (55)

- **Qué devuelve:** cada opción es un contacto de la bandeja Clientes de la organización. Su id es el del contacto y su texto, el nombre del contacto.
- **Lo que nunca sale:** el teléfono, ni a quien completa el formulario ni en la planilla. El sistema lo usa solo para mandar al chat del contacto (47).
- **Filtros:** por etiqueta del contacto (`hasAny`, `hasNone`), con la misma gramática que «Miembros». Hasta 10 condiciones.
- **Ejecución:** como «Miembros»: lee una fila más que el tope, con un timeout corto, sin traer todos los contactos a memoria.
- **Un contacto borrado** deja de ofrecerse; las respuestas guardan el nombre que tenía, sellado.
- **El valor es siempre de texto.**

## 5. Dominio y errores

**Entidades** (`Domain/Forms`):
- **`FormCatalog`:** `CreateManual(valueType)`, `CreateMembers`, `CreateContacts`, `Rename`, `ChangeFilters` (solo `Members` y `Contacts`), `Deactivate` y `Reactivate`.
- **`FormCatalogItem`:** `Create`, `Edit`, `Deactivate`, `Reactivate` y `SwapPosition`.

**Estado de un catálogo** (se calcula, no se guarda): «Se puede usar» o «No se puede usar», con estos motivos:
- `NoActiveOptions`: un catálogo a mano sin opciones activas;
- `SourceOutdated`;
- `ChannelUnavailable`: un catálogo de `Contacts` sin WhatsApp instalado;
- `RoleMissing` o `CompanyMissing`: falta un rol o una empresa de una condición.

| Código | Tipo | Cuándo |
| --- | --- | --- |
| `Forms.Catalog.NotFound` | NotFound | ajeno o inexistente |
| `Forms.Catalog.ItemNotFound` | NotFound | la opción no es de ese catálogo |
| `Forms.Catalog.DuplicateCode` | Validation (campo `items[i].code`) | el código ya existe en el catálogo |
| `Forms.Catalog.TooManyItems` | Conflict | más de 10.000 opciones |
| `Forms.Catalog.ValueRequired` / `ValueInvalid` | Validation (campo `items[i].value`) | falta el valor en un catálogo tipado, o no es del tipo |
| `Forms.Catalog.MoveOutOfRange` | Conflict | subir la primera o bajar la última |
| `Forms.Catalog.Inactive` | Conflict | cambiar opciones de un catálogo dado de baja, o elegirlo en una pregunta nueva |
| `Forms.Catalog.WrongSource` | Conflict | opciones en un catálogo con filtros, o filtros en uno a mano |
| `Forms.Catalog.SampleMemberRequired` | Validation (campo `sampleMemberId`) | crear o guardar un catálogo de `Members` con condiciones sin persona de ejemplo. Sin condiciones no hace falta, como en la copia de una plantilla (E17) |
| `Forms.Catalog.FiltersInvalid` | Validation (campo `conditions[i]`) | columna, operador o valor fuera de la fuente |
| `Forms.Catalog.SampleMemberNotFound` | Validation | la persona de ejemplo no es de la organización |
| `Forms.Catalog.ItemTextTooLong` / `ItemDetailTooLong` | Validation (campo `items[i].text` o `.detail`) | textos fuera de límite |
| `Forms.CatalogImport.FileChanged` | Conflict | el archivo que se aplica no es el de la vista previa (SHA-256) |
| `Forms.CatalogImport.FileInvalid` | Validation | formato, codificación o topes del archivo (C13) |
| `Forms.CatalogImport.ValueAmbiguous` | Validation (fila y columna) | un número o una fecha que se lee de dos formas (C14) |
| `Forms.CatalogImport.ConfirmationRequired` | Validation | más del 25 % de bajas o del 10 % de filas con error, sin confirmar |
| `Forms.CatalogImport.Busy` | Conflict | ya hay una vista previa, una aplicación o una exportación de esa persona (C16) |
| `Forms.Draft.CatalogUnavailable` | Validation (campo `questions[i]`) | la pregunta apunta a un catálogo que no se puede usar o ajeno |
| `Forms.Draft.CatalogTooLargeForMultiple` | Validation (campo `questions[i]`) | «elegir varias» con un catálogo a mano de más de 20 opciones activas |
| `Forms.Offer.TooManyOptions` | Conflict | más de 200 opciones en «elegir una», o más de 20 en «elegir varias», para ese destinatario |
| `Forms.Offer.TooFewOptions` | Conflict | menos opciones que el mínimo de una pregunta de «elegir varias» |
| `Forms.Offer.NoOptions` | Conflict | ninguna opción en una pregunta obligatoria |
| `Forms.Offer.CatalogUnavailable` | Conflict | el catálogo dejó de poder usarse |
| `Forms.Record.OptionWithdrawn` | Conflict | eligió una opción a mano que se dio de baja después de mandar |
| `WhatsApp.Flow.OptionNotOffered` | Validation | el id elegido no está en la oferta de la sesión |

## 6. Contrato borrador

Las rutas de Business llevan `[Access(Access.Business)]` y `[FeatureGate("forms")]`. Los listados, relaciones y opciones de selectores llevan `PagedRequest` y `PagedResult`, con los tamaños comunes (10 por defecto, 20, 50 o 100), búsqueda y orden estable. Las altas llevan `[Idempotent]`. Toda respuesta que consume el front tiene ejemplos exportados de una corrida real.

| Método y ruta | Permiso | Pide | Devuelve |
| --- | --- | --- | --- |
| GET `/api/organization/forms/catalogs` | `formcatalogs.read` | `page`, `pageSize`, `sort` (`name`, `updatedAtUtc`), `search`, `source` (`all`, `manual`, `members`, `contacts`), `status` (`all`, `active`, `inactive`) | 200 `PagedResult<{id, name, description?, source, valueType, optionsCount?, usedByCount, isActive, usable, unusableReason?}>` |
| GET `/api/organization/forms/catalogs/filter-counts` | `formcatalogs.read` | `search` | 200 `{all, manual, members, contacts, active, inactive}` |
| POST `/api/organization/forms/catalogs` | `formcatalogs.manage` | `{name, description?, source, valueType?, items?[], filters?, sampleMemberId?}`. Sin tipo, `Text`; `Members` exige `sampleMemberId` | 201 detalle + Location, con el mapa de ids |
| GET `/api/organization/forms/catalogs/{id}` | `formcatalogs.read` | — | 200 `{id, name, description, source, valueType, filters?, isActive, usable, unusableReason?, usedByCount, canManage, version}` |
| GET `/api/organization/forms/catalogs/{id}/used-by` | `formcatalogs.read` | `page`, `pageSize`, `search`, `sort` (`name`) | 200 `PagedResult<{formId, name}>` |
| PUT `/api/organization/forms/catalogs/{id}` | `formcatalogs.manage` | `{name, description, isActive, version, items?: [{itemId? \| localId, code, text, detail?, value?, isActive}], moves?: [{itemId, direction}], sortAlphabetically?, filters?, sampleMemberId?}`. `items` lleva solo lo nuevo o cambiado. `isActive` es obligatorio: omitirlo no apaga nada | 200 `{version, itemIdentityMap}` |
| GET `/api/organization/forms/catalogs/{id}/items` | `formcatalogs.read` | `page`, `pageSize`, `search`, `status` (`active`, `inactive`, `all`) | 200 `PagedResult<{id, code, text, detail?, value?, position, isActive}>`, ordenado por posición |
| POST `/api/organization/forms/catalogs/{id}/imports/previews` | `formcatalogs.manage` | multipart: el archivo o las filas pegadas, `mode` (`append` al pegar, `replace` con un archivo) y `version` | 200 `{sha256, rowsRead, added, changed, deactivated, errors: PagedResult<{row, column, code}>}`; no escribe |
| POST `/api/organization/forms/catalogs/{id}/imports` | `formcatalogs.manage` | multipart: el mismo archivo o filas, `mode`, `sha256`, `version`, `skipErrorRows`, `confirmedDeactivations?` | 200 `{version}`; 409 `FileChanged` o de versión |
| GET `/api/organization/forms/catalogs/{id}/export` | `formcatalogs.read` | — | 200 `.xlsx` con todas las opciones, activas y dadas de baja |
| GET `/api/organization/forms/catalogs/template` | `formcatalogs.read` | `valueType` | 200 `.xlsx` |
| GET `/api/organization/forms/catalogs/sources/members` | `formcatalogs.manage` | — | 200 `{version, columns[{key, type, operators, canDetail, canSort}]}`; catálogo cerrado de columnas y operadores de la §4 |
| GET `/api/organization/forms/catalogs/sources/members/roles` | `formcatalogs.manage` | `page`, `pageSize`, `search`, `sort` (`name`) | 200 `PagedResult<{id, name}>` de roles de la organización |
| GET `/api/organization/forms/catalogs/sources/members/companies` | `formcatalogs.manage` | `page`, `pageSize`, `search`, `sort` (`name`) | 200 `PagedResult<{id, name}>` de empresas de la organización |
| GET `/api/organization/forms/catalogs/sample-members` | `formcatalogs.manage` | `page`, `search` | 200 `PagedResult<{memberId, name}>`, sin correo |
| GET `/api/organization/forms/catalogs/sources/contacts/tags` | `formcatalogs.manage` | `page`, `pageSize`, `search` | 200 `PagedResult<{id, name}>` de etiquetas de contactos de la organización, por el puerto `IStructuredContactSource` (C17) |
| POST `/api/organization/forms/catalogs/previews` | `formcatalogs.manage` | `{source, filters, sampleMemberId?, multiple}`; con `Contacts` no hay persona de ejemplo | 200 `{options[{text, detail?}], total, exceedsLimit}`; no escribe |

Las pruebas de integración recorren una segunda página de `used-by`, opciones, roles, empresas y errores de importación, con búsqueda y desempate; `GrowingCollectionContractTests` verifica sus contratos sin agregar excepciones por módulo. La vista previa y la oferta conservan su límite explícito (200 o 20).

**Lo que cambia en las rutas de la E12:**
- **`FormDetail` y `PUT …/draft`:** las preguntas `Choice` y `MultiChoice` llevan `catalogId?`.
- **`publication-checks`:** suma `Forms.Draft.CatalogUnavailable` y `CatalogTooLargeForMultiple`.
- **`GET …/whatsapp/conversations/{id}/forms`:** cada formulario trae `offerProblems[{fieldId, code}]` (`TooManyOptions`, `TooFewOptions`, `NoOptions`, `CatalogUnavailable`). Si hay alguno, el formulario aparece pero no se puede mandar.
- **La tarjeta `form`:** el valor de catálogo muestra el texto que vio la persona y su código. Si la respuesta se rechazó con `OptionWithdrawn`, nombra la pregunta, no la opción.

## 7. Flujos

1. **Crear un catálogo:** se valida afuera y se crea en una UoW. Si es `Members`, sus filtros se validan contra la fuente.
2. **Guardar un catálogo a mano** (C6): una UoW con la versión del catálogo.
   - Bloqueo del catálogo.
   - Si nada cambió, no escribe y responde la misma versión.
   - Las opciones nuevas o cambiadas: las nuevas van al final, se respeta el tope de 10.000, el código tiene que estar libre y el valor tiene que ser del tipo.
   - Los movimientos, en orden, sobre el orden global por posición e id; después se renumera. `sortAlphabetically` reordena por texto. La restricción diferida admite los intercambios.
   - Un movimiento fuera de rango revierte todo el guardado.
   - Responde la versión y el mapa de ids.
3. **Pegar o importar** (C12 a C14):
   - **vista previa:** toma el candado de la persona (C16), lee el archivo o las filas con sus topes y la cultura del pedido, y compara contra lo guardado. Devuelve el SHA-256, las altas, los cambios, las bajas y los errores por fila y columna. No escribe;
   - **aplicar:** toma el candado de la persona y, en una UoW, bloquea el catálogo, comprueba la versión y el SHA-256, revalida permisos y aplica todo junto: altas al final, cambios por código y, al reemplazar, bajas de lo que no vino. Pide la confirmación de bajas y de filas con error cuando corresponde.
4. **Exportar y la plantilla:** toman el candado de la persona y leen en lotes, sin traer las 10.000 opciones de una vez a memoria.
5. **Filtros:**
   - `POST` o `PUT` del catálogo con su versión y la persona de ejemplo: sin ella no se crea;
   - los filtros se validan contra la fuente y se vuelve a correr la vista previa con esa persona;
   - con más de 200 resultados para esa persona **se guarda**: la vista previa avisa, y lo que frena es el envío, donde el tope se comprueba para cada destinatario (decisiones 12, 22 y 37).
6. **Vista previa:** corre el lector con los filtros sin guardar, con la persona de ejemplo como destinatario. No toma su identidad ni sus permisos, y no escribe nada.
7. **Pregunta de catálogo:**
   - `PUT …/draft` valida que el catálogo exista y se pueda usar;
   - cambiar el catálogo de una pregunta ya publicada emite un `FieldId` nuevo;
   - `IStructuredFormChannel.Check(StructuredFormVersion)`, síncrono y puro, valida el perfil con datos; «elegir varias» con un catálogo a mano de más de 20 opciones activas no se publica.
8. **Mandar** (cambia la §6.6 de la E12):
   - `FindSendableAsync` de Forms resuelve, para quien recibe, la oferta de cada pregunta de catálogo. Lee los catálogos a mano activos o la fuente con los filtros, con una fila más que el tope; es solo lectura.
   - **Al operador** le llega solo la cantidad de opciones por pregunta y los problemas, nunca los nombres. Las opciones completas quedan en la sesión.
   - Si una pregunta tiene un problema, no se puede mandar.
   - **Nombres largos:** un nombre de la fuente «Miembros» de más de 30 caracteres se corta en 29 más «…», y el completo va primero en el detalle (decisiones 11, 21 y 36).
   - **En la UoW de WhatsApp:**
     - se numeran las opciones `o1` a `oN`;
     - se arma `flow_action_payload.data` con `{id: localId, title: text, description?: detail}`;
     - se guarda `ProtectedOffer` en la sesión y se encola el mensaje.
9. **Recibir** (cambia la §6.7 de la E12):
   - el verificador mira que cada `localId` elegido esté en la oferta (si no, `OptionNotOffered`), y lo traduce a `{catalogId, itemId, text, detail, code, value}`;
   - la captura de Forms, en su UoW, comprueba con un catálogo a mano que la opción siga activa, bajo el mismo bloqueo con que se edita: si no, `OptionWithdrawn`, sin respuesta ni número;
   - después WhatsApp marca la sesión `Rejected` con ese código, y la tarjeta ofrece «Mandar de nuevo», que abre el mismo diálogo de la E12.
10. **Retención:** `ProtectedOffer` se borra igual que `ProtectedFlowResponse` de la E12.

## 8. Puertos

**Un puerto nuevo, `IStructuredContactSource`** (C17), en `Application/Interfaces/Integrations/StructuredData/`: lista los contactos de la organización por página, con sus filtros por etiqueta y una fila más que el tope, sin teléfonos. Lo implementa WhatsApp y su versión apagada está en `Infrastructure/StructuredData/`.

Cambian además los modelos de la E12:
- **`StructuredFormVersion.Questions[]`:** suma `CatalogId?` en las preguntas de elegir.
- **`FindSendableAsync`:** devuelve `StructuredSendableForm(Version, Offers[FieldId → Options[ItemRef(CatalogId, ItemId), Text, Detail?, Code?, Value?]], Problems[FieldId, Code])`.
- **`ListSendableAsync`:** suma `HasProblems` por formulario.
- **`StructuredRecordCapture.Values[]`:** suma `CatalogId?`, `ChoiceCode?`, `ChoiceDetail?` y `ChoiceValue?`, con `ChoiceId`.

## 9. Archivos

Los modelos están en este repo; no se copia código de ArquitecturaBase. Las abreviaturas son las de la E12 (`D/`, `A/`, `I/`, `P/`, `F/`).

| Archivo nuevo o cambiado | Modelo |
| --- | --- |
| `D/Forms/FormCatalog.cs`, `FormCatalogItem.cs`, `FormCatalogSource.cs`, `FormCatalogValueType.cs`, `FormCatalogErrors.cs`, `FormCatalogImportErrors.cs`, `FormOfferErrors.cs` | las entidades de la E12 |
| `D/Authorization/Permissions.cs`: suma `formcatalogs.read` y `formcatalogs.manage` | la clase `Forms` de la E12 |
| `A/Modules/Forms/Interfaces/Services/IFormCatalogService.cs`, `IFormCatalogImportService.cs` y sus servicios, con los helpers `FormCatalogFiltersPolicy`, `FormCatalogItemEditor`, `FormCatalogPositionGuard`, `FormCatalogFileResolver` (lee el archivo o lo pegado), `FormCatalogImportPolicy` (decide altas, cambios, bajas y errores, sin escribir), `FormCatalogExportBuilder` (arma el `.xlsx`) y `FormOfferBuilder` | los servicios de la E12; los sufijos son los de la lista cerrada de [capas-y-flujo](../../rules/capas-y-flujo.md) |
| `A/Interfaces/Integrations/StructuredData/IStructuredContactSource.cs`, su modelo y `I/StructuredData/DisabledStructuredContactSource.cs`; en WhatsApp, `WhatsAppStructuredContactSource.cs` | los puertos de la E12 y sus implementaciones apagadas |
| `A/Modules/Forms/Catalogs/MembersCatalogSource.cs` (columnas, operadores y versión) | — (nuevo; es una definición, sin servicios) |
| `A/Modules/Forms/Interfaces/Persistence/IFormCatalogRepository.cs`, `IFormCatalogReader.cs`, `IFormMembersCatalogReader.cs`, `IFormCatalogLock.cs` | los de la E12; el candado, como los `I<X>Lock` de `Application/Interfaces/Persistence` |
| `I/Modules/Forms/Persistence/Configurations/FormCatalogConfiguration.cs`, `FormCatalogItemConfiguration.cs`; `Readers/FormCatalogReader.cs`, `FormMembersCatalogReader.cs` | las configuraciones de la E12; `I/Persistence/Readers/` del núcleo para miembros |
| `I/Persistence/Migrations/<fecha>_FormCatalogs.cs` (con las restricciones diferidas en `Sql`, en una línea y sin `\r`) y `<fecha>_SystemRoleFormCatalogPermissions.cs` | las migraciones de la E12 |
| `P/Modules/Forms/Controllers/BusinessFormCatalogsController.cs`, `Contracts/*` | `BusinessFormsController` de la E12 |
| WhatsApp: `WhatsAppFlowBuilder` (perfil con datos y `CheckboxGroup` con catálogo), `WhatsAppFlowReplyVerifier` (oferta), `WhatsAppFormSendService` (`ProtectedOffer`) | los de la E12 |
| `tests/ArchitectureTests/Modules/Forms/IdentityAccessTests.Forms.cs` | `Modules/Assistant/IdentityAccessTests.Assistant.cs` |
| `F/src/modules/forms/`: páginas de catálogos, la tabla editable, el diálogo de pegar e importar, el editor de filtros, la vista previa, el selector de catálogo en la pregunta y las rutas `/org/formularios/catalogos` y `/org/formularios/catalogos/:id` | las páginas de la E12 |
| `F/src/modules/forms/locales/{es,en}/forms.json` y `enums.json`: suman las claves, con paridad; los aporta `forms/registration.ts` | los recursos del módulo de la E12 |
| Front de WhatsApp: los avisos de oferta en el diálogo y el rechazo con «Mandar de nuevo» en la tarjeta | lo de la E12 |

**Punteros:** las carpetas nuevas de la E13 (`Catalogs/`) llevan su `AGENTS.md` y su `CLAUDE.md` al crearse. La ficha `docs/features/formularios.md` suma la sección de catálogos al cerrar.

## 10. Pantallas (tableros)

**Navegación (34):** «Catálogos» es una entrada del panel de «Gestión de formularios».

**Diseño redibuja** (evaluación, §5.1): `FRM-Catalogo`, `M-FRM-Catalogo`, `FRM-Catalogo-Filtros`, `M-FRM-Catalogo-Filtros` y `FRM-Catalogo-Propuesta`. Sale «Textos en», sale «Cambios sin guardar», y el origen y el tipo de valor van como la A11: en su campo, apagados y en gris, sin «No se cambia» (43).

**Diseño dibuja nuevos** (evaluación, §5.2), con nuestras piezas y la decisión 43:
- `CA1-A` · Todo a la vista: el editor de un catálogo a mano con la barra de Opciones (Agregar opción, Pegar desde Excel, Importar, Exportar y «⋯» con la plantilla, ordenar de la A a la Z y generar los códigos que faltan), la búsqueda, la tabla editable en la compu, la selección de filas para dar de baja o reactivar, y «Así lo ve quien lo recibe» con «Una opción / Varias». En el celular, el editor por opción;
- `CA2` · Importar (y pegar): archivo o filas pegadas, revisar (Errores, Altas, Cambios y Bajas, por fila y columna) y aplicar, con la confirmación de bajas, «Aplicar sin las N filas con errores» y el 409;
- `CA3` · Tipo de valor: se elige al crear, la columna «Valor», el error en el campo, y el tipo en el editor de formularios, el Excel y el PDF;
- `CA4` · Elegir varias: «Permitir elegir varias» en la pregunta de catálogo, con mínimo y máximo, «varias, hasta N» en la lista de preguntas y la vista previa con casillas;
- `CA5` · Las respuestas con varias opciones: la planilla, «alguna de», el detalle, el chat, el Excel y el PDF (los usa la E15);
- `CA6` · El circuito de punta a punta.

**Diseño dibuja también** `FRM-Catalogo-Contactos`: un catálogo «De los contactos», con sus filtros por etiqueta y la vista previa (55). `FRM-Catalogo-Nuevo` suma la tercera tarjeta, «De los contactos de WhatsApp».

**Se quedan como están:** `FRM-Catalogos`, `M-FRM-Catalogos`, `FRM-Catalogo-Nuevo` (suma el tipo de valor) y la pregunta de catálogo en `FRM-Editor` y `WA-Chats-Formulario`.

**Estados que se dibujan:**
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
- **Al mandar:** la cantidad de opciones por pregunta; en rojo y con el envío grisado: más de 200 (o 20 en varias), menos que el mínimo, ninguna en una pregunta obligatoria, o un catálogo que no se puede usar.
- **Respuesta:** una opción dada de baja no se guarda y ofrece «Mandar de nuevo».

**Mandan sobre esta spec en:**
- **Guardado (C6):** todo se guarda junto en un `PUT` del catálogo, con su versión, y viaja solo lo que cambió.
- **Nuevo catálogo:** primero se elige el origen, y para los escritos a mano, el tipo de valor; el nombre se escribe en el editor y el `POST` sale de «Crear catálogo».
- **Persona de ejemplo:** sin ella no se crea un catálogo con filtros (`Forms.Catalog.SampleMemberRequired`).
- **El código de la opción se ve en la tarjeta de la respuesta,** debajo del valor (nunca en WhatsApp).
- **Cualquier catálogo de la organización** sirve en cualquier formulario: el contenido no tiene idioma (35).

## 11. Tests

| Dónde | Qué |
| --- | --- |
| `tests/…Domain.UnitTests/Forms/` | origen y tipo fijos; código único sin distinguir mayúsculas; intercambio de posiciones en los extremos; valor obligatorio y del tipo; estado y motivos de «se puede usar» |
| `tests/…Application.UnitTests/Modules/Forms/` | validación de filtros contra la fuente; el armado de la oferta (más de 200, más de 20 en varias, menos que el mínimo, ninguna en una obligatoria); la lectura de archivos y de lo pegado con «1.500», «1,500», «12.345» y «3/4/2026» en es y en; topes del archivo con celdas vacías con formato; el plan de importación (altas, cambios, bajas, columna ausente y columna vacía); exportar y volver a importar sin diferencias; el perfil con datos y el verificador (`localId` fuera de la oferta) |
| `tests/…Api.IntegrationTests/Modules/Forms/` | cada ruta con 401, 403, feature apagada = 404, validación, 409 e idempotencia; guardar sin cambios no escribe ni audita; guardar una opción entre 10.000 escribe y audita solo esa; subir y bajar con dos personas a la vez; aplicar con otro archivo = 409; dos importaciones de la misma persona a la vez = `Busy`; vista previa sin escribir; captura con opción dada de baja = sin número; la tarjeta con «Mandar de nuevo»; ejemplos exportados de cada respuesta |
| Volumen | un catálogo de 10.000 opciones: listar, buscar, guardar una opción, exportar y ofrecer, sin traer todo para quedarse con poco, con EXPLAIN |
| Aislamiento | la fuente «Miembros» nunca devuelve miembros de otra organización ni correos; la persona de ejemplo de otra organización da error; cada ruta nueva le responde 404 a otra organización |
| Guardas | `IdentityAccessTests` (partial de Forms), `ExplicitRouteInventoryTests`, `ResourceParityTests` y `PermissionTextsTests` |
| Front | páginas de catálogos, la tabla editable, el diálogo de importar, el editor de filtros y la vista previa, con MSW, axe y los ejemplos exportados validados con zod estricto; los avisos del diálogo de envío; los botones según la decisión 43 |
| E2E y recorrido | pegar opciones desde una planilla, importar un archivo que da de baja algunas, usar el catálogo en una pregunta de «elegir varias», mandar y responder; dar de baja una opción antes de responder y mandar de nuevo |
| Prueba con Meta | §15 |

## 12. Puntos de contacto

| Marca | Qué hay que confirmar | Qué asume esta spec |
| --- | --- | --- |
| [E12] | El formato del borrador y de la versión, y el mecanismo de identidad nueva al cambiar el tipo. | `catalogId` en las preguntas de elegir, en el mismo formato y con el mismo mecanismo de identidad. |
| [E12] | La sesión, el verificador y el armado del Flow que haya dejado la E12. | Se suman `ProtectedOffer`, `OfferHash` y el perfil con datos. |
| [E12] | El participante de la baja de la cuenta. | `ChoiceLabel` puede ser el nombre de otra persona (catálogo de miembros): el participante lo tiene que contemplar. |
| [Meta] | Los límites del `Dropdown` y del `CheckboxGroup` dinámicos: cuántas opciones, el largo del título y del detalle, y el tamaño de `data` en el mensaje. | 200 opciones en `Dropdown` y 20 en `CheckboxGroup`, título ≤30 y detalle ≤300. El piloto de ArquitecturaBase confirmó 200 opciones con 136 caracteres de detalle cada una. Se vuelve a mirar en la documentación vigente al planificar. |
| [E9] | Las cuotas compartidas. | Como en la E12. |

## 13. Decisiones de producto de la etapa

Están en la §5 de la hoja de ruta y no se vuelven a preguntar:
- **11, 21 y 36:** un nombre de miembro de más de 30 caracteres se corta en 29 y se le agrega «…», y el nombre completo va primero en el detalle. Las opciones a mano no lo necesitan, porque su texto ya tiene un tope de 30 al guardarse.
- **12, 22 y 37:** un catálogo con filtros que da más de 200 opciones se puede guardar. La vista previa avisa, y el bloqueo es al mandar.
- **35:** el contenido no tiene idioma; cualquier catálogo sirve en cualquier formulario.
- **42:** hasta 10.000 opciones, igual para todas las organizaciones.
- **43:** botones y avisos.

## 14. Referencia tomada de ArquitecturaBase

Se toma solo como referencia, del traspaso 2 (anexo C §10 y anexo D). Las rutas son de `ArquitecturaBase`.

| Tema | Lo que sirve |
| --- | --- |
| Opciones a mano | El código es la identidad y no se ve en WhatsApp. Las opciones no se borran. La posición es del servidor, con restricción diferida. El cliente nunca manda posiciones. |
| Carga de una vez | Pegar agrega e importar reemplaza; vista previa sin transacción; SHA-256 bajo los locks; confirmación con más del 25 % de bajas y del 10 % de errores; los topes de archivo, incluidas las celdas vacías con formato; CSV UTF-8 estricto; exportar sin diferencias al reimportar. |
| Tipo de valor | Se elige al crear y no cambia; canónico; Excel y PDF muestran el valor junto al texto. |
| Filtros | Valores siempre como parámetros, columnas de una lista cerrada, timeout de 3 s y una fila más que el tope. La vista previa no toma la identidad de la persona de ejemplo. |
| Errores pagados | Posiciones del cliente; «Lo usan» en 0 sin permiso (ahora null); una página tardía de otro catálogo cargada en el borrador; cada guardado reescribía las 10.000 opciones; los números leídos en la cultura equivocada al importar. |
| Pantallas | CA1-A a CA6. Sirven para el contenido; el dibujo es de nuestro lienzo, con la decisión 43. |
| No se toma | La vista SQL sobre `AspNetUsers` y roles globales; los permisos y el bloqueo globales; el idioma original y las traducciones; la política `forms-export` por proceso. |

## 15. Cómo se cierra

La puerta de la etapa, con la lista de control de `/puerta`, más una **prueba real con Meta** con el número de una organización de prueba, abriendo cada variante en un celular:
1. **A mano:** un catálogo con y sin detalle, en un formulario publicado. Se manda, se responde y la respuesta guarda el texto, el detalle, el código y el catálogo; se ven igual después de recargar.
2. **Carga de una vez:** importar una planilla real de unas 1.000 filas, con algunas bajas, y usar el catálogo actualizado.
3. **Elegir varias:** una pregunta de catálogo con mínimo 1 y máximo 3; se marcan dos y quedan en su orden.
4. **Miembros:** un catálogo con filtros, vacío y con opciones, con «Sin incluir a quien lo recibe».
5. **Opción dada de baja después de mandar:** la respuesta se rechaza sin número. «Mandar de nuevo» da el número siguiente, sin saltos.
6. **El tope:** un catálogo de 200 opciones con detalle viaja y vuelve con los ids intactos.

## 16. Criterios

Salen de los reviews de ArquitecturaBase (E1) y del traspaso del 17/10. El plan los toma como lista de control, junto con la de `/puerta`:
- **Los límites del canal se validan al guardar** (30 y 300), no al mandar; la vista previa marca lo que no entra.
- **Referencias por id,** nunca por nombre: un rol renombrado no vacía el catálogo, y dar de baja un rol tampoco lo invalida.
- **Opciones vacías:** no viajan, y el texto de una opción sale de un campo que siempre tiene valor.
- **Al operador** le llegan solo la cantidad y los problemas, nunca los nombres de las personas ofrecidas.
- **Ninguna lectura trae todo para quedarse con poco:** listar, buscar, exportar y ofrecer leen en páginas o lotes, probado con 10.000 opciones.
- **Guardar sin cambios no escribe,** y guardar una opción no reescribe las demás.
- **Lo que viene de un archivo** se lee con la cultura del pedido y rechaza lo ambiguo.
- **Reintentos:** 5 fallas y 15 minutos, como en la E12.
- **En el front:**
  - la búsqueda espera 300 ms y conserva el editor y el foco;
  - una página tardía de otro catálogo se descarta;
  - el archivo vive solo en el diálogo y se descarta al salir;
  - una pregunta de catálogo sin selección manda `null` y recibe la validación en su campo;
  - el front no tapa la validación del servidor.
