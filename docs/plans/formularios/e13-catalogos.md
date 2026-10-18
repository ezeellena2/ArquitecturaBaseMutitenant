# E13 · Catálogos a mano y con filtros (spec)

**Estado:** spec escrita el 2026-10-13. Se apoya en la [spec de la E12](e12-formularios.md): usa su modelo, sus puertos y sus convenciones, y acá se escribe solo lo que la E13 agrega o cambia. `/plan-etapa` la convierte en plan cuando le toque, después de verificar los puntos marcados **[E12]** contra el código que haya dejado la E12, y los **[Meta]** contra la documentación vigente.

**Fuentes:**
- alcance: [hoja de ruta](../2026-10-12-formularios-dinamicos-hoja-de-ruta.md), §E13;
- reglas: [AGENTS.md](../../../AGENTS.md) y las [fichas](../../rules/README.md);
- referencia: la etapa 2 de ArquitecturaBase, `origin/main` `202b65d` ([§14](#14-referencia-tomada-de-arquitecturabase)). Allá están programados los catálogos a mano; los filtros están a medio hacer, y la pregunta de catálogo y el envío, solo diseñados.

## 1. Alcance

**Entra:**
- **Catálogos de la organización:** listado, alta con el origen fijo (a mano o con filtros), datos y estado «Se puede usar», «Dado de baja» o «No se puede usar».
- **Dar de baja y reactivar un catálogo** desde su pantalla, con su versión, sin borrarlo. Dado de baja, no se elige en preguntas nuevas, y los formularios publicados que lo usan no se pueden mandar: la confirmación de la baja lo advierte. Dar de baja uno con filtros no necesita vista previa, y reactivarlo sí. Es una regla de producto de ArquitecturaBase (cerrada el 2026-10-15), que se copia por la decisión 18.
- **A mano:**
  - opciones con texto, detalle y código;
  - el idioma original del catálogo es obligatorio y la traducción, opcional. No hay respaldo opción por opción: si alguna opción ofrecida no tiene el idioma de quien recibe, todo el formulario sale en el original (decisión 17 de la hoja de ruta);
  - las opciones se dan de baja sin borrarse y se reordenan con «subir» y «bajar».
- **Con filtros:**
  - sobre la fuente «Miembros de la organización», con condiciones armadas por el sistema y «Sin incluir a quien lo recibe»;
  - vista previa con una persona de ejemplo, elegida a propósito.
- **La pregunta «Elegir de un catálogo»** en el editor de formularios.
- **Al mandar:**
  - las opciones se arman para cada destinatario y viajan dentro del mensaje;
  - no se puede mandar con más de 200 opciones, ni sin ninguna si la pregunta es obligatoria.
- **Al responder:**
  - la opción tiene que estar en la oferta, y se guarda el texto que vio la persona;
  - si era una opción a mano dada de baja, no se guarda nada, el operador ve el motivo y puede mandar de nuevo.
- **Permisos:** `formcatalogs.read` y `formcatalogs.manage`.

**No entra:**
- catálogos con una consulta SQL, decidido en la hoja de ruta;
- otras fuentes, como clientes o productos, que no existen en la plantilla;
- importar opciones desde Excel;
- la búsqueda en vivo o las listas dependientes, porque necesitan el intercambio cifrado con Meta;
- límites por plan.

## 2. Decisiones de la etapa

Valen las de la E12. Estas son las nuevas, técnicas. Las de producto están en la [§13](#13-decisiones-de-producto-de-la-etapa).

| # | Decisión | Por qué |
| --- | --- | --- |
| C1 | Los catálogos son de la organización: `ITenantOwned`, en el esquema `tenant` y en el módulo `Forms`. | Como los formularios (decisión 1 de la hoja de ruta). |
| C2 | **La fuente «Miembros» se define en el código, no como vista SQL.** Es un lector de Forms sobre las tablas del núcleo, con una lista cerrada de columnas y valores como parámetros de EF. Lleva un número de versión: si cambia, los catálogos viejos pasan a «No se puede usar» con su motivo. | El repo no tiene vistas SQL, y una vista con RLS necesita `security_invoker`, porque si no se saltea el aislamiento. El lector es el patrón de siempre. La hoja de ruta decía «vistas versionadas por migración»; se corrige. |
| C3 | **Los ids que viajan a Meta son locales a la oferta** (`o1` … `o200`). La sesión guarda a qué ítem corresponde cada uno. | No se mandan a Meta ids internos ni de personas, y «estar en la oferta» se comprueba solo. |
| C4 | **La oferta la arma Forms y la guarda WhatsApp,** cifrada en la sesión (`ProtectedOffer`). Se borra con la retención, una vez procesada. | Forms conoce los catálogos y WhatsApp conoce la sesión. Como contiene nombres de personas, va cifrada. |
| C5 | Para un catálogo a mano, la captura comprueba que la opción siga activa; si no, `Forms.Record.OptionWithdrawn`. Para uno con filtros alcanza con que esté en la oferta. | Es la regla de la hoja de ruta. Con filtros, lo que vio la persona es lo que vale. |
| C6 | **Todo se guarda junto** (tableros de la v129). Un solo `PUT` del catálogo, con su versión (`IVersioned`, 409), lleva lo siguiente: <ul><li>nombre, descripción e `isActive`;</li><li>las opciones cambiadas: las existentes con su `itemId` y las nuevas con un id local;</li><li>los movimientos `{itemId, direction}`.</li></ul>Responde la versión y el mapa de ids locales a ids del servidor. Crear (`POST`) lleva lo mismo. Las opciones no tienen versión propia. | Es la disposición elegida: una sola pantalla con «Guardar cambios». Sigue siendo configuración interna: una UoW, versión y 409, sin recibos (ADR 0018 de ArquitecturaBase y nuestra ficha de concurrencia). |
| C7 | La posición la maneja el servidor y es única por catálogo, con una restricción `DEFERRABLE INITIALLY DEFERRED` hecha con SQL en la migración. Subir y bajar intercambian la posición con la vecina, bajo un bloqueo del catálogo. | La lección de ArquitecturaBase: las posiciones que manda el cliente se rompían. |
| C8 | Una pregunta de catálogo guarda solo `catalogId`. Cambiar el catálogo de una pregunta publicada crea una pregunta nueva, como cambiar el tipo. | Cambiar opciones no obliga a republicar, y las respuestas viejas no cambian de sentido. |
| C9 | La gramática de condiciones es general: `{column, operator, values[]}`, unidas con «y». La E15 la reusa para los filtros de la planilla. | Una sola gramática. |
| C10 | El Flow pasa a un perfil con datos iniciales. Cada pregunta de catálogo es un `Dropdown` con `data-source: ${data.field_<N>}`, y la pantalla declara su `data` con un `__example__` **[Meta]**. | Es lo que permite mandar opciones distintas a cada persona sin un endpoint de intercambio. |
| C11 | **Idioma de un catálogo a mano:** tiene su idioma original, que se elige al crearlo y no cambia. Una pregunta solo puede usar un catálogo a mano con el **mismo idioma original que el formulario**. Los demás **no aparecen** en el selector, porque el idioma no se puede cambiar y no hay nada que resolver (`pantallas-y-ui.md` del front: lo que no aplica, no va). El servidor igual rechaza una referencia así con `Forms.Draft.CatalogLanguageMismatch`. Un catálogo de `Members` sirve en cualquier formulario, porque los nombres no se traducen. | Así el original de cada opción siempre existe y nunca se mezclan idiomas (decisión 17). ArquitecturaBase no resolvió este caso. Lo eligió el usuario el 2026-10-17: decisión 20 de la hoja de ruta. |

## 3. Modelo de datos

Todas las tablas son `ITenantOwned` en `tenant`, con RLS, FK compuestas y `TextLimits`, como en la E12.

**FormCatalogs** (`IAuditable`, `IVersioned`)

| Columna | Tipo | Regla |
| --- | --- | --- |
| `Id`, `TenantId` | uuid | — |
| `OriginalCulture` | texto | es o en, de los datos de referencia; no cambia (C11). Un catálogo de `Members` no tiene: los nombres no se traducen |
| `NameEs`, `NameEn` | texto ≤80 | el del idioma original es obligatorio; el otro, opcional. En pantalla se muestra el del idioma de quien mira, y si falta, el original |
| `DescriptionEs`, `DescriptionEn` | texto ≤300? | opcionales |
| `Source` | texto | `Manual` o `Members`; no cambia después de crear |
| `Filters` | jsonb? | solo con `Members`; CHECK que lo ata a `Source` |
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
| `Code` | texto ≤40 | obligatorio; único por catálogo sin distinguir mayúsculas (`CodeNormalized` + `UniqueKeys.FormCatalogItemCode`). No se ve en WhatsApp. |
| `TextEs`, `TextEn` | texto ≤30 por idioma, validado al guardar | el del idioma original del catálogo es obligatorio; es el título de la opción en WhatsApp |
| `DetailEs`, `DetailEn` | texto ≤300 por idioma, validado al guardar | opcional; la descripción de la opción. Si no hay detalle, el Flow omite `description` |
| `Position` | int | única por catálogo, `DEFERRABLE INITIALLY DEFERRED` (C7) |
| `IsActive` | bool | dar de baja = `false`; no se borra |

Un catálogo a mano tiene hasta **500 opciones**, entre activas y dadas de baja. La E12 queda así:
- **`FormDraft` y `FormVersions`:** el tipo `CatalogChoice` suma `catalogId` a la pregunta.
- **`FormRecordValues`:** suma `CatalogId uuid?`, `ChoiceCode` (texto, el código histórico de una opción a mano, que se ve en la tarjeta de la respuesta) y `ChoiceTexts jsonb`. `ChoiceId` es el id del ítem (opción a mano o miembro), y `ChoiceTexts` guarda **todas** las variantes de idioma del texto y el detalle tal como estaban al responder (`{es?: {text, detail?}, en?: {…}}`). La copia fija de un PDF en otro idioma no mezcla idiomas: es una lección de ArquitecturaBase (R1017 #3).
- **`WhatsAppFlowSessions`:** suma `ProtectedOffer` (bytea, cifrado con `IPayloadProtector`) y `OfferHash`. La oferta es `[{fieldId, options: [{localId, catalogId, itemId, texts{…}, detail?}]}]`, con todas las variantes de idioma.

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
- **«Sin incluir a quien lo recibe»:** `MemberId <> el miembro de quien recibe`. Es el único parámetro de contexto, y lo pone el servidor.
- **Ejecución:**
  - lector de Forms en `Infrastructure/Modules/Forms/Persistence/Readers/FormMembersCatalogReader.cs`, sobre `Member`, `CompanyMembership`, las asignaciones de roles y el nombre de `ApplicationUser`;
  - EF con parámetros y RLS de la organización, con la conexión normal;
  - un timeout corto (3 s);
  - lee 201 filas como mucho: si hay más, devuelve `exceedsLimit`.
- **Guarda de acceso global:** leer la cuenta global (`identity`) suma el lector a `IdentityAccessTests` con el partial del módulo, como el Asistente.
- **Versión:**
  - `sourceVersion` es 1;
  - si una versión nueva de la fuente cambia las columnas o los operadores, un catálogo con `sourceVersion` vieja pasa a «No se puede usar»;
  - sin migración de datos.

## 5. Dominio y errores

**Entidades** (`Domain/Forms`):
- **`FormCatalog`:** `CreateManual`, `CreateMembers`, `Rename` y `ChangeFilters` (solo `Members`).
- **`FormCatalogItem`:** `Create`, `Edit`, `Deactivate`, `Reactivate` y `SwapPosition`.

**Estado de un catálogo** (se calcula, no se guarda): «Se puede usar» o «No se puede usar», con estos motivos:
- `NoActiveOptions`: un catálogo a mano sin opciones activas;
- `SourceOutdated`;
- `RoleMissing` o `CompanyMissing`: falta un rol o una empresa de una condición.

| Código | Tipo | Cuándo |
| --- | --- | --- |
| `Forms.Catalog.NotFound` | NotFound | ajeno o inexistente |
| `Forms.Catalog.ItemNotFound` | NotFound | la opción no es de ese catálogo |
| `Forms.Catalog.DuplicateCode` | Conflict (campo `code`) | el código ya existe en el catálogo |
| `Forms.Catalog.TooManyItems` | Conflict | más de 500 opciones |
| `Forms.Catalog.MoveOutOfRange` | Conflict | subir la primera o bajar la última |
| `Forms.Catalog.Inactive` | Conflict | cambiar opciones de un catálogo dado de baja, o elegirlo en una pregunta nueva |
| `Forms.Catalog.WrongSource` | Conflict | opciones en un catálogo con filtros, o filtros en uno a mano |
| `Forms.Catalog.SampleMemberRequired` | Validation (campo `sampleMemberId`) | crear o guardar un catálogo con filtros sin persona de ejemplo |
| `Forms.Catalog.FiltersInvalid` | Validation (campo `conditions[i]`) | columna, operador o valor fuera de la fuente |
| `Forms.Catalog.SampleMemberNotFound` | Validation | la persona de ejemplo no es de la organización |
| `Forms.Catalog.ItemText…` | Validation | textos fuera de límite (`text.es`, `detail.en`…) |
| `Forms.Draft.CatalogUnavailable` | Validation (campo `questions[i]`) | la pregunta apunta a un catálogo que no se puede usar o ajeno |
| `Forms.Draft.CatalogLanguageMismatch` | Validation (campo `questions[i]`) | el catálogo a mano está en otro idioma original que el formulario (C11) |
| `Forms.Offer.TooManyOptions` | Conflict | más de 200 opciones para ese destinatario |
| `Forms.Offer.NoOptions` | Conflict | ninguna opción en una pregunta obligatoria |
| `Forms.Offer.CatalogUnavailable` | Conflict | el catálogo dejó de poder usarse |
| `Forms.Record.OptionWithdrawn` | Conflict | eligió una opción a mano que se dio de baja después de mandar |
| `WhatsApp.Flow.OptionNotOffered` | Validation | el id elegido no está en la oferta de la sesión |

## 6. Contrato borrador

Las rutas de Business llevan `[Access(Access.Business)]` y `[FeatureGate("forms")]`. Los listados, relaciones y opciones de selectores llevan `PagedRequest` y `PagedResult`, con los tamaños comunes (10 por defecto, 20, 50 o 100), búsqueda y orden estable; usan las lecturas comunes de paginado y búsqueda. Las altas llevan `[Idempotent]`.

| Método y ruta | Permiso | Pide | Devuelve |
| --- | --- | --- | --- |
| GET `/api/organization/forms/catalogs` | `formcatalogs.read` | `page`, `pageSize`, `sort` (`name`, `updatedAtUtc`), `search`, `source` (`all`, `manual`, `members`), `usable?` | 200 `PagedResult<{id, name, source, optionsCount?, usedByCount, usable, unusableReason?}>` |
| GET `/api/organization/forms/catalogs/filter-counts` | `formcatalogs.read` | `search` | 200 `{all, manual, members}` |
| POST `/api/organization/forms/catalogs` | `formcatalogs.manage` | `{name{…}, description?, source, originalCulture?, items?[], filters?, sampleMemberId?}`. Sin cultura, la de quien crea; `Members` no lleva cultura y exige `sampleMemberId` | 201 detalle + Location, con el mapa de ids |
| GET `/api/organization/forms/catalogs/{id}` | `formcatalogs.read` | — | 200 `{id, name, description, source, filters?, usable, unusableReason?, usedByCount, canManage, version}` |
| GET `/api/organization/forms/catalogs/{id}/used-by` | `formcatalogs.read` | `page`, `pageSize`, `search`, `sort` (`name`) | 200 `PagedResult<{formId, name}>` |
| PUT `/api/organization/forms/catalogs/{id}` | `formcatalogs.manage` | `{name, description, isActive, version, items?: [{itemId? \| localId, code, text{…}, detail{…}, isActive}], moves?: [{itemId, direction}], filters?, sampleMemberId?}`. `isActive` es obligatorio: omitirlo no apaga nada | 200 `{version, itemIdentityMap}` |
| GET `/api/organization/forms/catalogs/{id}/items` | `formcatalogs.read` | `page`, `pageSize`, `search`, `status` (`active`, `inactive`, `all`) | 200 `PagedResult<{id, code, text{…}, detail{…}, position, isActive}>`, ordenado por posición |
| GET `/api/organization/forms/catalogs/sources/members` | `formcatalogs.manage` | — | 200 `{version, columns[{key, type, operators, canDetail, canSort}]}`; catálogo cerrado de columnas y operadores de la §4 |
| GET `/api/organization/forms/catalogs/sources/members/roles` | `formcatalogs.manage` | `page`, `pageSize`, `search`, `sort` (`name`) | 200 `PagedResult<{id, name}>` de roles de la organización |
| GET `/api/organization/forms/catalogs/sources/members/companies` | `formcatalogs.manage` | `page`, `pageSize`, `search`, `sort` (`name`) | 200 `PagedResult<{id, name}>` de empresas de la organización |
| GET `/api/organization/forms/catalogs/sample-members` | `formcatalogs.manage` | `page`, `search` | 200 `PagedResult<{memberId, name}>`, sin correo |
| POST `/api/organization/forms/catalogs/previews` | `formcatalogs.manage` | `{filters, sampleMemberId}` | 200 `{options[{text, detail?}], total, exceedsLimit}`; no escribe |

Las pruebas de integración recorren una segunda página de `used-by`, roles y empresas, con búsqueda y desempate; `GrowingCollectionContractTests` verifica sus contratos sin agregar excepciones por módulo. La vista previa y la oferta conservan su límite explícito de 200 opciones (§4 y §7).

**Lo que cambia en las rutas de la E12:**
- **`FormDetail` y `PUT …/draft`:** una pregunta `CatalogChoice` lleva `catalogId`.
- **`publication-checks`:** suma `Forms.Draft.CatalogUnavailable`.
- **`GET …/whatsapp/conversations/{id}/forms`:** cada formulario trae `offerProblems[{fieldId, code}]` (`TooManyOptions`, `NoOptions`, `CatalogUnavailable`). Si hay alguno, el formulario aparece pero no se puede mandar.
- **La tarjeta `form`:** el valor de catálogo muestra el texto que vio la persona. Si la respuesta se rechazó con `OptionWithdrawn`, nombra la pregunta, no la opción.

## 7. Flujos

1. **Crear un catálogo:** se valida afuera y se crea en una UoW. Si es `Members`, sus filtros se validan contra la fuente.
2. **Guardar un catálogo a mano** (C6): una UoW con la versión del catálogo.
   - Bloqueo del catálogo.
   - Las opciones cambiadas: las nuevas van al final, se respeta el tope de 500 y el código tiene que estar libre.
   - Los movimientos, en orden, sobre el orden global por posición e id; después se renumera. La restricción diferida admite los intercambios.
   - Un movimiento fuera de rango revierte todo el guardado.
   - Responde la versión y el mapa de ids.
3. **Filtros:**
   - `POST` o `PUT` del catálogo con su versión y la persona de ejemplo: sin ella no se crea;
   - los filtros se validan contra la fuente y se vuelve a correr la vista previa con esa persona;
   - con más de 200 resultados para esa persona **se guarda**: la vista previa avisa, y lo que frena es el envío, donde el tope se comprueba para cada destinatario (decisión 22, [§13](#13-decisiones-de-producto-de-la-etapa)).
4. **Vista previa:** corre el lector con los filtros sin guardar, con la persona de ejemplo como destinatario. No toma su identidad ni sus permisos, y no escribe nada.
5. **Pregunta de catálogo:**
   - `PUT …/draft` valida que el catálogo exista y se pueda usar;
   - cambiar el catálogo de una pregunta ya publicada emite un `FieldId` nuevo;
   - `IStructuredFormChannel.Check(StructuredFormVersion)`, síncrono y puro, valida el perfil con datos.
6. **Mandar** (cambia la §6.6 de la E12):
   - `FindSendableAsync` de Forms resuelve, para quien recibe, la oferta de cada pregunta de catálogo. Lee los catálogos a mano activos o la fuente con los filtros; es solo lectura.
   - **El idioma del envío** (cambia la T10 de la E12): el de quien recibe, solo si la versión está completa en ese idioma **y** todas las opciones ofrecidas lo tienen. Si no, todo va en el original del formulario: preguntas, opciones y mensaje.
   - **Al operador** le llega solo la cantidad de opciones por pregunta y los problemas, nunca los nombres. Las opciones completas quedan en la sesión (lección de privacidad de ArquitecturaBase).
   - Si una pregunta tiene un problema, no se puede mandar.
   - **Nombres largos:** un nombre de la fuente «Miembros» de más de 30 caracteres se corta en 29 más «…», y el completo va primero en el detalle ([§13](#13-decisiones-de-producto-de-la-etapa)).
   - **En la UoW de WhatsApp:**
     - se numeran las opciones `o1` a `oN`;
     - se arma `flow_action_payload.data` con `{id: localId, title: text, description?: detail}`;
     - se guarda `ProtectedOffer` en la sesión y se encola el mensaje.
7. **Recibir** (cambia la §6.7 de la E12):
   - el verificador mira que el `localId` elegido esté en la oferta (si no, `OptionNotOffered`), y lo traduce a `{catalogId, itemId, text}`;
   - la captura de Forms, en su UoW, comprueba con un catálogo a mano que la opción siga activa: si no, `OptionWithdrawn`, sin respuesta ni número;
   - después WhatsApp marca la sesión `Rejected` con ese código, y la tarjeta ofrece «Mandar de nuevo», que abre el mismo diálogo de la E12.
8. **Retención:** `ProtectedOffer` se borra igual que `ProtectedFlowResponse` de la E12.

## 8. Puertos

No hay puertos nuevos; cambian los modelos de la E12:
- **`StructuredFormVersion.Questions[]`:** suma el tipo `CatalogChoice`.
- **`FindSendableAsync`:** devuelve `StructuredSendableForm(Version, Offers[FieldId → Options[ItemRef(CatalogId, ItemId), Text, Detail?]], Problems[FieldId, Code])`.
- **`ListSendableAsync`:** suma `HasProblems` por formulario.
- **`StructuredRecordCapture.Values[]`:** suma `CatalogId?`, `ChoiceCode?` y `ChoiceTexts` (todas las variantes de idioma), con `ChoiceId`.

## 9. Archivos

Los modelos están en este repo; no se copia código de ArquitecturaBase. Las abreviaturas son las de la E12 (`D/`, `A/`, `I/`, `P/`, `F/`).

| Archivo nuevo o cambiado | Modelo |
| --- | --- |
| `D/Forms/FormCatalog.cs`, `FormCatalogItem.cs`, `FormCatalogSource.cs`, `FormCatalogErrors.cs`, `FormOfferErrors.cs` | las entidades de la E12 |
| `D/Forms/FormQuestionType.cs`: suma `CatalogChoice` | — |
| `D/Authorization/Permissions.cs`: suma `formcatalogs.read` y `formcatalogs.manage` | la clase `Forms` de la E12 |
| `A/Modules/Forms/Interfaces/Services/IFormCatalogService.cs`, `IFormCatalogItemService.cs` y sus servicios, con los helpers `FormCatalogFiltersPolicy`, `FormCatalogItemEditor`, `FormCatalogPositionGuard` y `FormOfferBuilder` | los servicios de la E12 |
| `A/Modules/Forms/Catalogs/MembersCatalogSource.cs` (columnas, operadores y versión) | — (nuevo; es una definición, sin servicios) |
| `A/Modules/Forms/Interfaces/Persistence/IFormCatalogRepository.cs`, `IFormCatalogReader.cs`, `IFormMembersCatalogReader.cs` | los de la E12 |
| `I/Modules/Forms/Persistence/Configurations/FormCatalogConfiguration.cs`, `FormCatalogItemConfiguration.cs`; `Readers/FormCatalogReader.cs`, `FormMembersCatalogReader.cs` | las configuraciones de la E12; `I/Persistence/Readers/` del núcleo para miembros |
| `I/Persistence/Migrations/<fecha>_FormCatalogs.cs` (con la restricción diferida en `Sql`) y `<fecha>_SystemRoleFormCatalogPermissions.cs` | las migraciones de la E12 |
| `P/Modules/Forms/Controllers/BusinessFormCatalogsController.cs`, `Contracts/*` | `BusinessFormsController` de la E12 |
| WhatsApp: `WhatsAppFlowBuilder` (perfil con datos), `WhatsAppFlowReplyVerifier` (oferta), `WhatsAppFormSendService` (`ProtectedOffer`) | los de la E12 |
| `tests/ArchitectureTests/Modules/Forms/IdentityAccessTests.Forms.cs` | `Modules/Assistant/IdentityAccessTests.Assistant.cs` |
| `F/src/modules/forms/`: páginas de catálogos, el editor de filtros, la vista previa, el selector de catálogo en la pregunta y las rutas `/org/formularios/catalogos` y `/org/formularios/catalogos/:id` | las páginas de la E12 |
| `F/src/modules/forms/locales/{es,en}/forms.json` y `enums.json`: suman las claves, con paridad; los aporta `forms/registration.ts` | los recursos del módulo de la E12 |
| Front de WhatsApp: los avisos de oferta en el diálogo y el rechazo con «Mandar de nuevo» en la tarjeta | lo de la E12 |

**Punteros:** las carpetas nuevas de la E13 (`Catalogs/`) llevan su `AGENTS.md` y su `CLAUDE.md` al crearse. La ficha `docs/features/formularios.md` suma la sección de catálogos al cerrar.

## 10. Pantallas (tableros)

**Aprobados y publicados en el lienzo v129** (2026-10-17, front `e1a7b50a`), página «Organización · Formularios». Siguen la disposición de ArquitecturaBase, K1 a K7 (decisión 18); la consulta con SQL (K5) no entra. Las decisiones están en «Criterio · Decisiones por pantalla», grupo «E13 · Catálogos con la disposición de ArquitecturaBase». Los tableros del 13 y el 14/10, incluido `FRM-Catalogo-Opcion`, pasan a la página «Archivo».

| Tablero | Qué muestra |
| --- | --- |
| `FRM-Catalogos` y `M-FRM-Catalogos` | tabla con Catálogo (y su descripción), De dónde salen («Escritas a mano» o «De los miembros, con filtros»), Opciones, Lo usan y Estado (Activo; No se puede usar, con el motivo debajo; Dado de baja). El nombre abre el editor, sin columna de acciones. Filtros «Origen ▾» y «Estado ▾». En el teléfono, «Filtros» y «+» |
| `FRM-Catalogo-Nuevo` | el diálogo pregunta solo de dónde salen las opciones, en dos tarjetas. «Continuar» abre el editor, donde se escribe el nombre |
| `FRM-Catalogo` y `M-FRM-Catalogo` (a mano) | una sola pantalla, como el editor de formularios: <ul><li>«Textos en», con el inglés opcional;</li><li>el catálogo: nombre, de dónde salen (fijo) y descripción;</li><li>las opciones con buscador y «Estado ▾». Cada una se abre en el lugar (texto hasta 30, código, detalle y «Activa») y tiene ⋮ con Editar, Subir, Bajar y Dar de baja o Reactivar;</li><li>al costado, «Lo usan» y «Así lo ve el empleado».</li></ul>«Guardar cambios» guarda todo junto, y al crear, «Crear catálogo». «Dar de baja» va en el ⋮ de la banda. En el teléfono, las opciones van en hojas |
| `FRM-Catalogo-Filtros` y `M-FRM-Catalogo-Filtros` | una sola pantalla: el catálogo y «De dónde salen: los miembros, con filtros», con la vista Miembros (Nombre, Rol, Empresa y Activo), el texto, el detalle, las condiciones, «sin incluir a quien lo recibe» y el orden. Al costado, «Vista previa» con «Como si lo recibiera» y «Probar». En el teléfono, las condiciones van en hojas y la vista previa abajo |
| `FRM-Editor` y `M-FRM-Editor` (K6) | la pregunta «Elegir de un catálogo» se abre en el lugar, con el desplegable de catálogos, «Ver el catálogo» y el estado sin catálogos. Los que no se pueden usar aparecen apagados, con su motivo |
| `WA-Chats-Formulario`, `M-WA-Chats-Formulario` y `WA-Chats-Respuesta` (K7) | al mandar, las opciones de cada pregunta y «Preparando las opciones para esta persona…». La respuesta muestra el catálogo y el código debajo del valor |

**Estados que dibujan:**
- **Listado:** filtro abierto, sin coincidencias, cargando, vacío y error.
- **Catálogo a mano:**
  - opción sin completar, código repetido y texto de más de 30;
  - guardado, no se guardó y la revisión si otra persona guardó;
  - sin opciones, 500 opciones, nuevo, falta el nombre, cargando y no existe.
- **Catálogo con filtros:**
  - con más de 200 opciones, se guarda y avisa «Más de 200: no se va a poder mandar.» con ⓘ (decisión 22, corregido en la v130);
  - sin persona de ejemplo, no se crea;
  - un rol que ya no existe deja el catálogo en «No se puede usar».
- **Al mandar:**
  - el mensaje «Motivo: 3 opciones» por pregunta;
  - en rojo, con el envío apagado y sin enlace: más de 200 opciones, ninguna en una pregunta obligatoria, o un catálogo que no se puede usar («Avisale a quien lo administra»).
- **Respuesta:** una opción dada de baja no se guarda («Motivo: una opción se dio de baja. No se guardó la respuesta.») y ofrece «Mandar de nuevo».

**Mandan sobre esta spec en:**
- **Guardado (C6):** todo se guarda junto en un `PUT` del catálogo, con su versión; ya no hay rutas por opción.
- **Nuevo catálogo:** primero se elige el origen, y el nombre se escribe en el editor; el `POST` sale de «Crear catálogo».
- **Más de 200 con filtros:** se guarda con el aviso «Más de 200: no se va a poder mandar.» y su ⓘ, y lo que frena es el envío (decisión 22). Lo corrigió la v130 (2026-10-18, front `e074a7d7`), aprobada por el usuario.
- **Nombres de más de 30 caracteres:** se cortan en la lista y se ven completos en el detalle (decisión 21), también corregido en la v130.
- **Persona de ejemplo:** sin ella no se crea un catálogo con filtros (`Forms.Catalog.SampleMemberRequired`).
- **El código de la opción se ve en la tarjeta de la respuesta,** debajo del valor (nunca en WhatsApp). Por eso la captura guarda también el código histórico (`ChoiceCode`), junto con `ChoiceTexts`.
- **Catálogos en otro idioma:** siguen sin aparecer en el selector (C11, decisión 20). Los que no se pueden usar por algo que se resuelve, sí aparecen apagados.
- **Textos del listado:** «De dónde salen» dice «Escritas a mano» o «De los miembros, con filtros».

## 11. Tests

| Dónde | Qué |
| --- | --- |
| `tests/…Domain.UnitTests/Forms/` | origen fijo; código único sin distinguir mayúsculas; intercambio de posiciones en los extremos; estado y motivos de «se puede usar» |
| `tests/…Application.UnitTests/Modules/Forms/` | validación de filtros contra la fuente (columna, operador, ids); el armado de la oferta (más de 200, ninguna en una obligatoria, una opción sin el idioma de quien recibe = todo en el original, catálogo en otro idioma que el formulario); la captura guarda todas las variantes de idioma; el perfil con datos y el verificador (`localId` fuera de la oferta) |
| `tests/…Api.IntegrationTests/Modules/Forms/` | cada ruta con 401, 403, feature apagada = 404, validación, 409 e idempotencia; subir y bajar con dos personas a la vez; vista previa sin escribir; captura con opción dada de baja = sin número; la tarjeta con «Mandar de nuevo» |
| Aislamiento | la fuente «Miembros» nunca devuelve miembros de otra organización ni correos; la persona de ejemplo de otra organización da error; cada ruta nueva le responde 404 a otra organización |
| Guardas | `IdentityAccessTests` (partial de Forms), `ExplicitRouteInventoryTests`, `ResourceParityTests` y `PermissionTextsTests` |
| Front | páginas de catálogos, el editor de filtros y la vista previa, con MSW y axe; los avisos del diálogo de envío |
| E2E y recorrido | crear un catálogo a mano y uno con filtros, usarlos en un formulario, mandar y responder; dar de baja una opción antes de responder y mandar de nuevo |
| Prueba con Meta | **[Meta]** una oferta de 200 opciones viaja y vuelve con los ids intactos; textos de 30 caracteres y detalle |

## 12. Puntos de contacto

| Marca | Qué hay que confirmar | Qué asume esta spec |
| --- | --- | --- |
| [E12] | El formato del borrador y de la versión, y el mecanismo de identidad nueva al cambiar el tipo. | `CatalogChoice` + `catalogId` en el mismo formato, y el mismo mecanismo de identidad. |
| [E12] | La sesión, el verificador y el armado del Flow que haya dejado la E12. | Se suman `ProtectedOffer`, `OfferHash` y el perfil con datos. |
| [E12] | El participante de la baja de la cuenta. | `ChoiceLabel` puede ser el nombre de otra persona (catálogo de miembros): el participante lo tiene que contemplar. |
| [Meta] | Los límites del `Dropdown` dinámico: cuántas opciones, el largo del título y del detalle, y el tamaño de `data` en el mensaje. | 200 opciones, título ≤30 y detalle ≤300 por idioma. **El piloto de ArquitecturaBase del 2026-10-15 lo confirmó:** Meta aceptó 200 opciones con 136 caracteres de detalle cada una (`PIL200-000001`). Se vuelve a mirar en la documentación vigente al planificar. |
| [E9] | Los límites por organización. | Como en la E12. |

## 13. Decisiones de producto de la etapa

Las tomó el usuario el 2026-10-13. No se vuelven a preguntar.

1. **Un nombre de miembro de más de 30 caracteres** (el título de una opción en WhatsApp) se corta en 29 y se le agrega «…», y el nombre completo va primero en el detalle de la opción. Siempre se puede mandar. Las opciones a mano no lo necesitan, porque su texto ya tiene un tope de 30 al guardarse.
2. **Un catálogo con filtros que da más de 200 opciones se puede guardar.** La vista previa avisa «Más de 200: no se va a poder mandar», y el bloqueo es al mandar, porque la cantidad cambia igual cuando la organización crece. El usuario la confirmó el 2026-10-17 (decisión 22).

## 14. Referencia tomada de ArquitecturaBase

Se toma solo como referencia, en `origin/main` (`202b65d`). Las rutas son de `ArquitecturaBase`.

| Tema | Lo que sirve |
| --- | --- |
| Opciones a mano | Texto obligatorio en español y opcional en inglés, que usaba el español si faltaba; acá lo reemplaza la decisión 17 (todo en el original, sin mezclar). La vacía cuenta como faltante: fue un error en la búsqueda y el orden. El código no se ve en WhatsApp. Las opciones no se borran. Tope de 500. La posición es del servidor, con restricción diferida (`Migrations/20261012015620_*`). El cliente nunca manda posiciones. |
| Errores pagados | `a2038f8`: posiciones del cliente quitadas y la vacía tomada como inglés. `6da3263`: «Lo usan» en 0 sin permiso, ahora null. Front `73a26c7`: una página tardía de otro catálogo se cargaba en el borrador; las respuestas se descartan si cambió el catálogo o quien mira. |
| Filtros | Valores siempre como parámetros, columnas de una lista cerrada, timeout de 3 s y 201 filas para detectar «más de 200». La vista previa no toma la identidad de la persona de ejemplo (`Infrastructure/Modules/BusinessForms/Persistence/Readers/FormCatalogFilterReader.cs`, a medio hacer). |
| Pregunta y envío (solo diseño allá) | La versión guarda solo la referencia; cambiar el catálogo de una pregunta publicada crea una nueva; `Dropdown` con `data-source: ${data.<pregunta>_options}`; la oferta en la sesión; con filtros alcanza con estar en la oferta; una opción a mano dada de baja no guarda nada y el aviso nombra la pregunta, no la opción. |
| Pantallas (`ArquitecturaBaseFront/docs/design/formularios-whatsapp.md`, K1–K7) | K1 listado, K2 origen en dos tarjetas, K3 opciones, K4 filtros con «Como si lo recibiera», K6 pregunta y K7 envío y tarjeta. Sirven para el contenido; el dibujo es de nuestro lienzo. |
| No se toma | La vista SQL sobre `AspNetUsers` y roles globales; los permisos y el bloqueo globales; el guardado en lote con ids locales y mapa de identidades; el nombre en jsonb con columnas calculadas; los catálogos sin dueño. |

## 15. Cómo se cierra

La puerta de la etapa, más una **prueba real con Meta** con el número de una organización de prueba:
1. **A mano:** un catálogo con y sin detalle, en un formulario publicado. Se manda, se responde y la respuesta guarda el texto, el detalle y el catálogo; se ven igual después de recargar.
2. **Miembros:** un catálogo con filtros, vacío y con opciones, con «Sin incluir a quien lo recibe».
3. **Opción dada de baja después de mandar:** la respuesta se rechaza sin número. «Mandar de nuevo» da el número siguiente, sin saltos.
4. **Idioma:** una cuenta en inglés con un catálogo sin inglés recibe todo el formulario en español, sin opciones mezcladas.
5. **El tope:** un catálogo de 200 opciones con detalle viaja y vuelve con los ids intactos.

## 16. Lo que ArquitecturaBase aprendió después de esta spec

Vienen del anexo C §2 (etapa 2, cerrada el 2026-10-15) y del [06 de lecciones](../2026-10-17-formularios-traspaso/06-lecciones.md) del traspaso:
- **Los límites del canal se validan al guardar** (30 y 300 por idioma), no al mandar; la vista previa marca lo que no entra.
  - En ArquitecturaBase, nunca se trunca. Acá, la decisión 11 corta los nombres largos con «…», y la decisión 21 (2026-10-17) la mantiene.
- **Más de 200 resultados:** en ArquitecturaBase, un catálogo con filtros que da más de 200 no se guarda. Acá se guarda con aviso, y lo que frena es el envío (decisiones 12 y 22).
- **Referencias por id,** nunca por nombre: un rol renombrado no vacía el catálogo, y dar de baja un rol tampoco lo invalida.
- **Opciones vacías:** no viajan, y el texto de una opción sale de un campo que siempre tiene valor (en Miembros, el nombre).
- **Al operador** le llegan solo la cantidad y los problemas, nunca los nombres de las personas ofrecidas.
- **La captura** guarda todas las variantes de idioma del texto y del detalle (R1017 #3), y comprueba la baja de una opción a mano bajo el mismo bloqueo con que se edita.
- **Reintentos:** 5 fallas y 15 minutos, como en la E12.
- **En el front:**
  - la búsqueda de personas espera 300 ms y conserva el editor y el foco;
  - una página tardía de otro catálogo se descarta;
  - una pregunta de catálogo sin selección manda `null` y recibe la validación en su campo.
