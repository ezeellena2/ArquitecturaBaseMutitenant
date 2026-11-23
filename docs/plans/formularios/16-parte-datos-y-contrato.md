# E16 · Datos de las personas y filtros por destinatario: datos y contrato (spec, parte)

**Estado:** pendiente · parte de la [spec de la E16](16-datos-de-las-personas.md), que dice qué leer, qué se construye y cómo se cierra.

## Contrato

Son rutas de `/api/organization/forms`, con `[Access(Access.Business)]` y `[FeatureGate("forms")]`. Van en `FormsContractContributor.cs`, con ejemplos exportados de una corrida real.

| Ruta | Permiso | Pide | Devuelve |
| --- | --- | --- | --- |
| `GET /person-data` | `formpersondata.manage` | `page`, `pageSize`, `search`, `status`, `sort` (`name`, `key`, `type`, `catalogName` o `status`, y con `-` delante el otro sentido; 264) | `PagedResult<{id, key, name, type, catalogId?, catalogName?, isActive, usedBy[], usedByCount, valueCount, version}>`, en su orden si no se pide otro. `usedBy` trae hasta 5 nombres |
| `POST /person-data` | `formpersondata.manage` | `{name, key, type, catalogId?}`, con `[Idempotent]` | 201 + Location |
| `PUT /person-data/{id}` | `formpersondata.manage` | `{name, type, catalogId?, isActive, version, confirmValueRemoval?}`: la clave no viaja, porque no cambia | 200 `{version}` |
| `GET /person-data/catalogs` | `formpersondata.manage` | `page`, `pageSize`, `search` | `PagedResult<{id, name}>`, los catálogos a mano activos |
| `GET /person-data/values` | `formpersondata.manage` y `users.read` | `page`, `pageSize`, `search`, `status`, `sort` (`name` o `attribute`, con `-` para el otro sentido) y `sortAttributeId`, el dato que ordena con `attribute`: un texto por su valor y uno de catálogo por el texto de su opción, con lo vacío al final (264) | `PagedResult<{memberId, name, roles[], status, values, version?}>`, sin correos ni miembros quitados |
| `GET /person-data/options` | `forms.manage` | `page`, `pageSize`, `search` | `PagedResult<{id, name, catalogId?}>`, los datos activos de tipo catálogo |
| `GET /person-data/{id}/options` | `users.manage` | `page`, `pageSize`, `search` | `PagedResult<{id, text}>`, las opciones activas de su fuente. No concede `formcatalogs.read` |
| `GET /person-data/members/{memberId}` | `users.manage` | — | `{definitions[{id, name, type}], values, version?}` |
| `PUT /person-data/members/{memberId}` | `users.manage` | `{values, version?}`: `version` nulo si todavía no tiene fila | 200 `{version}` |
| `POST /catalogs/{id}/offer-previews` | `formcatalogs.read` | `{sampleMemberId, multiple, filters[], presentation?}` | `{status, total, options[{text, detail?}], missingData?}`, hasta 20 opciones. No escribe |
| `POST /catalogs/{id}/offer-maximums` | `formcatalogs.read` | `{multiple, filters[]}`, los filtros sin guardar | `{max, maxLabel?, limit, peopleWithoutData[{personDataId, name, count}]}`: el máximo por persona, con los valores de la combinación más grande, y cuántas personas activas con `formrecords.create` no tienen cada dato. Sin nombres de personas. No escribe |

`status` de una vista previa es `Ready`, `RecipientDataMissing` o `FilterUnavailable`. `maxLabel` es nulo si la pregunta tiene solo filtros fijos. En `values`, cada clave es la identidad de un dato; un valor es el texto o la identidad de la opción, y al leer lleva `{value, label}`.

**Rutas que cambian:**

| Ruta | Qué suma |
| --- | --- |
| `GET` y `PUT /catalogs/{id}` | `fields[].role` (`Data` o `Filter`); ausente es `Data` |
| `GET /{id}` y `PUT /{id}/draft` | `questions[].recipientFilters?[]`: `{fieldId, fieldName, mode, personDataId?, personDataName?, value?, valueLabel?}`, con `mode` `fixed` o `recipient`. Los nombres van en la lectura, porque con solo `forms.read` no se llega a `/person-data/options` |
| `POST /{id}/publication-checks` | `warnings[]`: `{fieldId, code, max?, maxLabel?, limit?, people?}`, con `code` `MaxOptionsPerRecipient` (solo si pasa el límite), `NoOptionsForRecipients` (si da cero, 157), `PeopleWithoutData` o `NotForCustomers` |
| `GET …/conversations/{id}/forms` | `offerProblems` suma `RecipientDataMissing`, con `dataName`, `FilterUnavailable` y `NotForCustomers`. `StructuredFormChoice.OfferProblems` suma `DataName?` |
| `GET /{id}/records/columns` y el detalle | `catalogField.role` y `fields[].role`; ausente es `Data` |

Un `sortAttributeId` que no es un dato activo de la organización, o `attribute` sin él, da 400 en `sortAttributeId`.

**Errores** (en `Domain/Forms/`, con sus textos en `FormsTexts.resx` y `.en.resx`):

| Código | Tipo | Cuándo |
| --- | --- | --- |
| `Forms.PersonData.NotFound` | NotFound | el dato es ajeno o no existe |
| `Forms.PersonData.MemberNotFound` | NotFound | la persona no es miembro de la organización |
| `Forms.PersonData.TooMany` | Conflict | más de 20 activos, también al reactivar |
| `Forms.PersonData.NameDuplicate` | Validation (`name`) | dos datos con el mismo nombre |
| `Forms.PersonData.KeyInvalid` | Validation (`key`) | la clave no cumple `^[a-z][a-z0-9_]{0,39}$` |
| `Forms.PersonData.KeyDuplicate` | Validation (`key`) | otro dato de la organización, también uno dado de baja, ya tiene esa clave |
| `Forms.PersonData.SourceInvalid` | Validation (`catalogId`) | la fuente no es un catálogo a mano activo de la organización |
| `Forms.PersonData.InUse` | Conflict | un dato en uso: trae `uses[{formId, formName, questionId, questionLabel, state}]` |
| `Forms.PersonData.ValueRemovalNotConfirmed` | Validation (`type`) | hay valores y falta `confirmValueRemoval`: trae `count` |
| `Forms.PersonData.ValueInvalid` | Validation (`values.<id>`) | un dato dado de baja, un texto largo o una opción que no es de la fuente |
| `Forms.Draft.RecipientFilterInvalid` | Validation (`questions[i].recipientFilters[j]`) | un campo que no es de papel «Filtro», repetido o quitado; un valor fijo fuera de su fuente; un dato con otra fuente, o una pregunta que no es de un catálogo a mano |
| `Forms.Draft.RecipientFilterForCustomers` | Validation (`questions[i]`) | según quien lo recibe, en un formulario solo para clientes |
| `Forms.Offer.RecipientDataMissing` | Conflict | a quien recibe le falta el dato |
| `Forms.Offer.FilterUnavailable` | Conflict | el filtro dejó de funcionar |
| `Forms.Offer.NotForCustomers` | Conflict | según quien lo recibe, para un cliente |

`Forms.Catalog.FieldInUse` suma los filtros: cambiar el papel, el tipo o la fuente de un campo que usa un filtro, con `uses`. `Forms.Catalog.SourceInUse` suma los datos activos. `Forms.Catalog.ItemInUse` suma `uses: forms`, si un filtro fijo del borrador o de la versión activa nombra la opción, y `uses: people`, si la tiene una persona en sus datos.

**Permiso nuevo:** `formpersondata.manage`, con sus textos y la migración de los roles de sistema. La entrada «Datos de las personas» va en Administración, con ese permiso y la feature.

## Datos

Dos migraciones: `<fecha>_FormPersonData.cs` y `<fecha>_SystemRoleFormPersonDataPermissions.cs`. Las dos tablas son `ITenantOwned`, esquema `tenant`, con RLS. `role` y `recipientFilters` viven en el jsonb de la E13 y de la E14: lo que no los tiene sigue igual, sin migrar.

**FormPersonDataDefinitions** (`IAuditable`, `IVersioned`)

| Columna | Tipo | Regla |
| --- | --- | --- |
| `Id`, `TenantId` | uuid | — |
| `Name`, `NameNormalized` | texto ≤80 | obligatorio; único por organización (`UniqueKeys.FormPersonDataName`) |
| `Key` | texto ≤40 | `^[a-z][a-z0-9_]{0,39}$`; nace del nombre, en minúsculas y sin acentos, y queda fija al guardar; única por organización, también entre los dados de baja (`UniqueKeys.FormPersonDataKey`) |
| `Type` | texto | `Text` o `Catalog` |
| `CatalogId` | uuid? | FK compuesta a `FormCatalogs`; CHECK que lo ata a `Catalog` |
| `Position` | int | la maneja el servidor |
| `IsActive` | bool | dar de baja = `false` |

**FormPersonDataValues** (`IAuditable`, `IVersioned`)

| Columna | Tipo | Regla |
| --- | --- | --- |
| `TenantId`, `Id` | uuid | clave. La auditoría pide un `Id` propio |
| `MemberId` | uuid | único; FK compuesta a `Members` |
| `Values` | jsonb, `{}` por defecto | mapa de identidad del dato a texto o a identidad de la opción, con índice GIN `jsonb_path_ops` |

## Lo que ya se pagó allá (no repetir)

Cada punto lleva su test, escrito durante la etapa, y los pasos los llaman «lección»:
1. **Cambiarle el papel a un campo en uso** dejaba sin envío a todo un formulario publicado, sin aviso. `FieldInUse` frena cualquier cambio de papel, tipo o fuente (R1023 #8).
2. **Un filtro fijo que deja más de 20 opciones en «elegir varias»** frena al publicar (#18).
3. **El valor fijo de un filtro** se valida contra su fuente (#19).
4. **La vista previa** exige `formcatalogs.read`, y no nombra opciones de un catálogo que la persona no puede leer (#20).
5. **Un solo selector de catálogo:** el dato usa el combo de la E13, sin buscador ni lista paginada aparte.
6. **Los filtros van en la versión.** En la prueba de allá, una versión salió sin los filtros de una pregunta. Cambiar un filtro cuenta como una pregunta cambiada en «Qué se publica», y la versión activa sigue con los suyos hasta publicar.
