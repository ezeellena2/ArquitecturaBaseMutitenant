# E13 · Catálogos a mano y con filtros (spec)

**Estado:** spec escrita el 2026-10-13. Se apoya en la [spec de la E12](e12-formularios.md): usa su modelo, sus puertos y sus convenciones, y acá se escribe solo lo que la E13 agrega o cambia. `/plan-etapa` la convierte en plan cuando le toque, después de verificar los puntos marcados **[E12]** contra el código que haya dejado la E12, y los **[Meta]** contra la documentación vigente.

**Fuentes:**
- alcance: [hoja de ruta](../2026-10-12-formularios-dinamicos-hoja-de-ruta.md), §E13;
- reglas: [AGENTS.md](../../../AGENTS.md) y las [fichas](../../rules/README.md);
- referencia: la etapa 2 de ArquitecturaBase, `origin/main` `202b65d` ([§14](#14-referencia-tomada-de-arquitecturabase)). Allá están programados los catálogos a mano; los filtros están a medio hacer, y la pregunta de catálogo y el envío, solo diseñados.

## 1. Alcance

**Entra:**
- **Catálogos de la organización:** listado, alta con el origen fijo (a mano o con filtros), datos y estado «Se puede usar» o «No se puede usar».
- **A mano:**
  - opciones con texto, detalle y código;
  - el inglés es opcional y, si falta, se usa el español;
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
- dar de baja un catálogo entero;
- otras fuentes, como clientes o productos, que no existen en la plantilla;
- importar opciones desde Excel;
- la búsqueda en vivo o las listas dependientes, porque necesitan el intercambio cifrado con Meta;
- límites por plan.

## 2. Decisiones de la etapa

Valen las de la E12. Estas son las nuevas, técnicas. Las de producto están en la [§13](#13-para-confirmar-con-el-usuario).

| # | Decisión | Por qué |
| --- | --- | --- |
| C1 | Los catálogos son de la organización: `ITenantOwned`, en el esquema `tenant` y en el módulo `Forms`. | Como los formularios (decisión 1 de la hoja de ruta). |
| C2 | **La fuente «Miembros» se define en el código, no como vista SQL.** Es un lector de Forms sobre las tablas del núcleo, con una lista cerrada de columnas y valores como parámetros de EF. Lleva un número de versión: si cambia, los catálogos viejos pasan a «No se puede usar» con su motivo. | El repo no tiene vistas SQL, y una vista con RLS necesita `security_invoker`, porque si no se saltea el aislamiento. El lector es el patrón de siempre. La hoja de ruta decía «vistas versionadas por migración»; se corrige. |
| C3 | **Los ids que viajan a Meta son locales a la oferta** (`o1` … `o200`). La sesión guarda a qué ítem corresponde cada uno. | No se mandan a Meta ids internos ni de personas, y «estar en la oferta» se comprueba solo. |
| C4 | **La oferta la arma Forms y la guarda WhatsApp,** cifrada en la sesión (`ProtectedOffer`). Se borra con la retención, una vez procesada. | Forms conoce los catálogos y WhatsApp conoce la sesión. Como contiene nombres de personas, va cifrada. |
| C5 | Para un catálogo a mano, la captura comprueba que la opción siga activa; si no, `Forms.Record.OptionWithdrawn`. Para uno con filtros alcanza con que esté en la oferta. | Es la regla de la hoja de ruta. Con filtros, lo que vio la persona es lo que vale. |
| C6 | **Las opciones se editan de a una,** con su propia versión: crear, editar, subir o bajar, y dar de baja o reactivar. | Sigue «lo corto en diálogo» y el `IVersioned` de siempre. Es más simple que el guardado en lote con ids locales de ArquitecturaBase. |
| C7 | La posición la maneja el servidor y es única por catálogo, con una restricción `DEFERRABLE INITIALLY DEFERRED` hecha con SQL en la migración. Subir y bajar intercambian la posición con la vecina, bajo un bloqueo del catálogo. | La lección de ArquitecturaBase: las posiciones que manda el cliente se rompían. |
| C8 | Una pregunta de catálogo guarda solo `catalogId`. Cambiar el catálogo de una pregunta publicada crea una pregunta nueva, como cambiar el tipo. | Cambiar opciones no obliga a republicar, y las respuestas viejas no cambian de sentido. |
| C9 | La gramática de condiciones es general: `{column, operator, values[]}`, unidas con «y». La E15 la reusa para los filtros de la planilla. | Una sola gramática. |
| C10 | El Flow pasa a un perfil con datos iniciales. Cada pregunta de catálogo es un `Dropdown` con `data-source: ${data.field_<N>}`, y la pantalla declara su `data` con un `__example__` **[Meta]**. | Es lo que permite mandar opciones distintas a cada persona sin un endpoint de intercambio. |

## 3. Modelo de datos

Todas las tablas son `ITenantOwned` en `tenant`, con RLS, FK compuestas y `TextLimits`, como en la E12.

**FormCatalogs** (`IAuditable`, `IVersioned`)

| Columna | Tipo | Regla |
| --- | --- | --- |
| `Id`, `TenantId` | uuid | — |
| `NameEs`, `NameEn` | texto ≤80 | el español es obligatorio y el inglés no; si falta, se usa el español |
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

**FormCatalogItems** (`IAuditable`, `IVersioned`; solo los catálogos a mano)

| Columna | Tipo | Regla |
| --- | --- | --- |
| `Id`, `TenantId`, `CatalogId` | uuid | FK compuesta |
| `Code` | texto ≤40 | obligatorio; único por catálogo sin distinguir mayúsculas (`CodeNormalized` + `UniqueKeys.FormCatalogItemCode`). No se ve en WhatsApp. |
| `TextEs`, `TextEn` | texto ≤30 **[Meta]** | el español es obligatorio; es el título de la opción en WhatsApp |
| `DetailEs`, `DetailEn` | texto ≤300? **[Meta]** | opcional; la descripción de la opción |
| `Position` | int | única por catálogo, `DEFERRABLE INITIALLY DEFERRED` (C7) |
| `IsActive` | bool | dar de baja = `false`; no se borra |

Un catálogo a mano tiene hasta **500 opciones**, entre activas y dadas de baja. La E12 queda así:
- **`FormDraft` y `FormVersions`:** el tipo `CatalogChoice` suma `catalogId` a la pregunta.
- **`FormRecordValues`:** suma `CatalogId uuid?`. `ChoiceId` es el id del ítem (opción a mano o miembro) y `ChoiceLabel` es el texto que vio la persona.
- **`WhatsAppFlowSessions`:** suma `ProtectedOffer` (bytea, cifrado con `IPayloadProtector`) y `OfferHash`. La oferta es `[{fieldId, options: [{localId, catalogId, itemId, text, detail?}]}]`.

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
| `Forms.Catalog.WrongSource` | Conflict | opciones en un catálogo con filtros, o filtros en uno a mano |
| `Forms.Catalog.FiltersInvalid` | Validation (campo `conditions[i]`) | columna, operador o valor fuera de la fuente |
| `Forms.Catalog.SampleMemberNotFound` | Validation | la persona de ejemplo no es de la organización |
| `Forms.Catalog.ItemText…` | Validation | textos fuera de límite (`text.es`, `detail.en`…) |
| `Forms.Draft.CatalogUnavailable` | Validation (campo `questions[i]`) | la pregunta apunta a un catálogo que no se puede usar o ajeno |
| `Forms.Offer.TooManyOptions` | Conflict | más de 200 opciones para ese destinatario |
| `Forms.Offer.NoOptions` | Conflict | ninguna opción en una pregunta obligatoria |
| `Forms.Offer.CatalogUnavailable` | Conflict | el catálogo dejó de poder usarse |
| `Forms.Record.OptionWithdrawn` | Conflict | eligió una opción a mano que se dio de baja después de mandar |
| `WhatsApp.Flow.OptionNotOffered` | Validation | el id elegido no está en la oferta de la sesión |

## 6. Contrato borrador

Las rutas de Business llevan `[Access(Access.Business)]` y `[FeatureGate("forms")]`. Los listados llevan `PagedRequest`, y las altas, `[Idempotent]`.

| Método y ruta | Permiso | Pide | Devuelve |
| --- | --- | --- | --- |
| GET `/api/organization/forms/catalogs` | `formcatalogs.read` | `page`, `pageSize`, `sort` (`name`, `updatedAtUtc`), `search`, `source` (`all`, `manual`, `members`), `usable?` | 200 `PagedResult<{id, name, source, optionsCount?, usedByCount, usable, unusableReason?}>` |
| GET `/api/organization/forms/catalogs/filter-counts` | `formcatalogs.read` | `search` | 200 `{all, manual, members}` |
| POST `/api/organization/forms/catalogs` | `formcatalogs.manage` | `{name{es,en?}, description?, source, filters?}` | 201 detalle + Location |
| GET `/api/organization/forms/catalogs/{id}` | `formcatalogs.read` | — | 200 `{id, name, description, source, filters?, usable, unusableReason?, usedBy[{formId, name}], canManage, version}` |
| PUT `/api/organization/forms/catalogs/{id}` | `formcatalogs.manage` | `{name, description, filters?, version}` | 200 `VersionResponse` |
| GET `/api/organization/forms/catalogs/{id}/items` | `formcatalogs.read` | `page`, `pageSize`, `search`, `status` (`active`, `inactive`, `all`) | 200 `PagedResult<{id, code, text{es,en}, detail{es,en}, position, isActive, version}>`, ordenado por posición |
| POST `/api/organization/forms/catalogs/{id}/items` | `formcatalogs.manage` | `{code, text{es,en?}, detail?}` | 201; queda última |
| PUT `/api/organization/forms/catalogs/{id}/items/{itemId}` | `formcatalogs.manage` | `{code, text, detail, isActive, version}` | 200 `VersionResponse` |
| POST `/api/organization/forms/catalogs/{id}/items/{itemId}/move` | `formcatalogs.manage` | `{direction: up\|down, version}` | 200 `VersionResponse` |
| GET `/api/organization/forms/catalogs/sources/members` | `formcatalogs.manage` | — | 200 `{version, columns[{key, type, operators, canDetail, canSort}], roles[{id, name}], companies[{id, name}]}` |
| GET `/api/organization/forms/catalogs/sample-members` | `formcatalogs.manage` | `page`, `search` | 200 `PagedResult<{memberId, name}>`, sin correo |
| POST `/api/organization/forms/catalogs/previews` | `formcatalogs.manage` | `{filters, sampleMemberId}` | 200 `{options[{text, detail?}], total, exceedsLimit}`; no escribe |

**Lo que cambia en las rutas de la E12:**
- **`FormDetail` y `PUT …/draft`:** una pregunta `CatalogChoice` lleva `catalogId`.
- **`publication-checks`:** suma `Forms.Draft.CatalogUnavailable`.
- **`GET …/whatsapp/conversations/{id}/forms`:** cada formulario trae `offerProblems[{fieldId, code}]` (`TooManyOptions`, `NoOptions`, `CatalogUnavailable`). Si hay alguno, el formulario aparece pero no se puede mandar.
- **La tarjeta `form`:** el valor de catálogo muestra el texto que vio la persona. Si la respuesta se rechazó con `OptionWithdrawn`, nombra la pregunta, no la opción.

## 7. Flujos

1. **Crear un catálogo:** se valida afuera y se crea en una UoW. Si es `Members`, sus filtros se validan contra la fuente.
2. **Opciones** (C6), cada operación en una UoW con la versión del ítem:
   - **crear:** bloqueo del catálogo, tope de 500, código libre y posición = última + 1;
   - **editar:** cambia texto, detalle, código o el estado activo;
   - **subir o bajar:** bloqueo del catálogo, se busca la vecina activa o dada de baja en el orden de posición, y se intercambian las dos posiciones. La restricción diferida admite el intercambio.
3. **Filtros:**
   - `PUT` del catálogo con su versión; los filtros se validan contra la fuente;
   - guardar con más de 200 resultados para la persona de ejemplo está permitido: la vista previa avisa, y el bloqueo es al mandar (para confirmar en la [§13](#13-para-confirmar-con-el-usuario)).
4. **Vista previa:** corre el lector con los filtros sin guardar, con la persona de ejemplo como destinatario. No toma su identidad ni sus permisos, y no escribe nada.
5. **Pregunta de catálogo:**
   - `PUT …/draft` valida que el catálogo exista y se pueda usar;
   - cambiar el catálogo de una pregunta ya publicada emite un `FieldId` nuevo;
   - `CheckAsync` del canal valida el perfil con datos.
6. **Mandar** (cambia la §6.6 de la E12):
   - `FindSendableAsync` de Forms resuelve, para quien recibe y en su idioma, la oferta de cada pregunta de catálogo. Lee los catálogos a mano activos o la fuente con los filtros; es solo lectura.
   - Si una pregunta tiene un problema, no se puede mandar.
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
- **`StructuredRecordCapture.Values[]`:** suma `CatalogId?`, con `ChoiceId` y `ChoiceLabel`.

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
| `F/src/locales/{es,en}/forms.json`: suma las claves | — |
| Front de WhatsApp: los avisos de oferta en el diálogo y el rechazo con «Mandar de nuevo» en la tarjeta | lo de la E12 |

**Punteros:** las carpetas nuevas de la E13 (`Catalogs/`) llevan su `AGENTS.md` y su `CLAUDE.md` al crearse. La ficha `docs/features/formularios.md` suma la sección de catálogos al cerrar.

## 10. Pantallas (tableros para Diseño)

Valen los criterios y los datos de ejemplo de la E12 ([texto para Diseño](e12-texto-diseno.md)). Ejemplos para esta etapa:
- **«Motivos de visita»:** a mano, con Seguimiento, Venta nueva, Reclamo y Cobranza (dada de baja).
- **«Supervisores»:** con filtros, «rol tiene Supervisor» y «Sin incluir a quien lo recibe».
- **«Clientes mayoristas»:** a mano, que no se puede usar porque no tiene opciones activas.

| Tablero | Qué muestra | Estados |
| --- | --- | --- |
| `FRM-Catalogos` y `M-FRM-Catalogos` | listado: nombre, origen (A mano o Con filtros), opciones, «Lo usan» y estado; «Nuevo catálogo»; filtro Todos, A mano y Con filtros | vacío, sin resultados, «No se puede usar» con ⓘ del motivo |
| `FRM-Catalogo-Nuevo` | diálogo: nombre y origen en dos tarjetas («Escribirlas a mano», «Tomarlas de los miembros, con filtros»); el origen no cambia después | — |
| `FRM-Catalogo` y `M-FRM-Catalogo` (a mano) | ficha con pestañas: **Opciones** (tabla con posición, texto, detalle y código, ⋮ con Editar, Subir, Bajar y Dar de baja o Reactivar; filtro Activas, Dadas de baja y Todas; «Agregar opción») y **Datos** (nombre y descripción es/en; «Lo usan») | sin opciones, 500 alcanzadas, conflicto |
| `FRM-Catalogo-Opcion` y `M-FRM-Catalogo-Opcion` | diálogo u hoja: texto es/en (≤30, con contador), detalle es/en y código | código repetido, texto largo |
| `FRM-Catalogo-Filtros` | ficha con filtros: filas de condición («Si», «y») con columna, operador y valor; «Sin incluir a quien lo recibe»; detalle (ninguno, roles, empresas) y orden. Al costado, «Como si lo recibiera» con una persona elegida, «Probar» y el resultado | N opciones, ninguna, más de 200 (aviso), rol o empresa que ya no existe |
| `FRM-Pregunta` (variante) | tipo «Elegir de un catálogo», con un selector de catálogo; los que no se pueden usar aparecen apagados con su motivo | catálogo elegido, sin catálogos |
| `WA-Chats-Formulario` (variante) | avisos por pregunta: más de 200 opciones, ninguna en una obligatoria, catálogo que no se puede usar; «Mandar» apagado | — |
| `WA-Chats-Respuesta` (variante) | la tarjeta con un valor de catálogo; rechazada porque «una opción se dio de baja», nombrando la pregunta, con «Mandar de nuevo» | — |
| `Rol` (ajuste) | el área Formularios suma «Ver catálogos» y «Administrar catálogos» | — |

**Elecciones para mostrar con opciones:**
- **Dónde viven los catálogos:** una pestaña «Catálogos» dentro de la página de Formularios, o un ítem propio en el menú. Recomiendo la pestaña.
- **El editor de filtros en el teléfono:** completo, o solo para ver.

## 11. Tests

| Dónde | Qué |
| --- | --- |
| `tests/…Domain.UnitTests/Forms/` | origen fijo; código único sin distinguir mayúsculas; intercambio de posiciones en los extremos; estado y motivos de «se puede usar» |
| `tests/…Application.UnitTests/Modules/Forms/` | validación de filtros contra la fuente (columna, operador, ids); el armado de la oferta (más de 200, ninguna en una obligatoria, el inglés que usa el español si falta); el perfil con datos y el verificador (`localId` fuera de la oferta) |
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
| [Meta] | Los límites del `Dropdown` dinámico: cuántas opciones, el largo del título y del detalle, y el tamaño de `data` en el mensaje. | 200 opciones, título ≤30 y detalle ≤300. Se verifica en la documentación vigente y en la prueba con Meta. |
| [E9] | Los límites por organización. | Como en la E12. |

## 13. Para confirmar con el usuario

1. **Un nombre de miembro de más de 30 caracteres** (el título de una opción en WhatsApp):
   - Recomendado: cortarlo en 29 y agregar «…», y poner el nombre completo en el detalle de la opción.
   - Alternativa: no dejar mandar mientras haya un nombre largo.
2. **Guardar un catálogo con filtros que da más de 200 opciones:**
   - Recomendado: se guarda, la vista previa avisa y el bloqueo es al mandar (la organización crece y el número cambia igual).
   - Alternativa: no se guarda, como en ArquitecturaBase.

## 14. Referencia tomada de ArquitecturaBase

Se toma solo como referencia, en `origin/main` (`202b65d`). Las rutas son de `ArquitecturaBase`.

| Tema | Lo que sirve |
| --- | --- |
| Opciones a mano | Texto obligatorio en español y opcional en inglés, que usa el español si falta (también si está vacío: fue un error en la búsqueda y el orden). El código no se ve en WhatsApp. Las opciones no se borran. Tope de 500. La posición es del servidor, con restricción diferida (`Migrations/20261012015620_*`). El cliente nunca manda posiciones. |
| Errores pagados | `a2038f8`: posiciones del cliente quitadas y la vacía tomada como inglés. `6da3263`: «Lo usan» en 0 sin permiso, ahora null. Front `73a26c7`: una página tardía de otro catálogo se cargaba en el borrador; las respuestas se descartan si cambió el catálogo o quien mira. |
| Filtros | Valores siempre como parámetros, columnas de una lista cerrada, timeout de 3 s y 201 filas para detectar «más de 200». La vista previa no toma la identidad de la persona de ejemplo (`Infrastructure/Modules/BusinessForms/Persistence/Readers/FormCatalogFilterReader.cs`, a medio hacer). |
| Pregunta y envío (solo diseño allá) | La versión guarda solo la referencia; cambiar el catálogo de una pregunta publicada crea una nueva; `Dropdown` con `data-source: ${data.<pregunta>_options}`; la oferta en la sesión; con filtros alcanza con estar en la oferta; una opción a mano dada de baja no guarda nada y el aviso nombra la pregunta, no la opción. |
| Pantallas (`ArquitecturaBaseFront/docs/design/formularios-whatsapp.md`, K1–K7) | K1 listado, K2 origen en dos tarjetas, K3 opciones, K4 filtros con «Como si lo recibiera», K6 pregunta y K7 envío y tarjeta. Sirven para el contenido; el dibujo es de nuestro lienzo. |
| No se toma | La vista SQL sobre `AspNetUsers` y roles globales; los permisos y el bloqueo globales; el guardado en lote con ids locales y mapa de identidades; el nombre en jsonb con columnas calculadas; los catálogos sin dueño. |
