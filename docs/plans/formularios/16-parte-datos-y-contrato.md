# E16 · Datos de las personas y filtros por destinatario: datos y contrato (spec, parte)

**Estado:** pendiente · parte de la [spec de la E16](16-datos-de-las-personas.md), que dice qué leer, qué se construye y cómo se cierra.

## Contrato

Son rutas de `/api/organization/forms`, con `[Access(Access.Business)]` y `[FeatureGate("forms")]`. Van en `FormsContractContributor.cs`, con ejemplos exportados de una corrida real.

| Ruta | Permiso | Pide | Devuelve |
| --- | --- | --- | --- |
| `GET /person-data` | `formpersondata.manage` | `page`, `pageSize`, `search`, `status` | `PagedResult<{id, name, type, catalogId?, catalogName?, isActive, usedBy[], usedByCount, valueCount, version}>`, en orden. `usedBy` trae hasta 5 nombres |
| `POST /person-data` | `formpersondata.manage` | `{name, type, catalogId?}`, con `[Idempotent]` | 201 + Location |
| `PUT /person-data/{id}` | `formpersondata.manage` | `{name, type, catalogId?, isActive, version, confirmValueRemoval?}` | 200 `{version}` |
| `GET /person-data/catalogs` | `formpersondata.manage` | `page`, `pageSize`, `search` | `PagedResult<{id, name}>`, los catálogos a mano activos |
| `GET /person-data/values` | `formpersondata.manage` y `users.read` | `page`, `pageSize`, `search`, `status` | `PagedResult<{memberId, name, roles[], status, values, version?}>`, sin correos ni miembros quitados |
| `GET /person-data/options` | `forms.manage` | `page`, `pageSize`, `search` | `PagedResult<{id, name, catalogId?}>`, los datos activos de tipo catálogo |
| `GET /person-data/{id}/options` | `users.manage` | `page`, `pageSize`, `search` | `PagedResult<{id, text}>`, las opciones activas de su fuente. No concede `formcatalogs.read` |
| `GET /person-data/members/{memberId}` | `users.manage` | — | `{definitions[{id, name, type}], values, version?}` |
| `PUT /person-data/members/{memberId}` | `users.manage` | `{values, version?}`: `version` nulo si todavía no tiene fila | 200 `{version}` |
| `POST /catalogs/{id}/offer-previews` | `formcatalogs.read` | `{sampleMemberId, multiple, filters[], presentation?}` | `{status, total, options[{text, detail?}], missingData?}`, hasta 20 opciones. No escribe |

`status` de una vista previa es `Ready`, `RecipientDataMissing` o `FilterUnavailable`. En `values`, cada clave es la identidad de un dato; un valor es el texto o la identidad de la opción, y al leer lleva `{value, label}`.

**Rutas que cambian:**

| Ruta | Qué suma |
| --- | --- |
| `GET` y `PUT /catalogs/{id}` | `fields[].role` (`Data` o `Filter`); ausente es `Data` |
| `GET /{id}` y `PUT /{id}/draft` | `questions[].recipientFilters?[]`: `{fieldId, fieldName, mode, personDataId?, personDataName?, value?, valueLabel?}`, con `mode` `fixed` o `recipient`. Los nombres van en la lectura, porque con solo `forms.read` no se llega a `/person-data/options` |
| `POST /{id}/publication-checks` | `warnings[]`: `{fieldId, code, max?, maxLabel?, limit?, people?}`, con `code` `MaxOptionsPerRecipient` (solo si pasa el límite), `NoOptionsForRecipients` (si da cero, 157), `PeopleWithoutData` o `NotForCustomers` |
| `GET …/conversations/{id}/forms` | `offerProblems` suma `RecipientDataMissing`, con `dataName`, `FilterUnavailable` y `NotForCustomers`. `StructuredFormChoice.OfferProblems` suma `DataName?` |
| `GET /{id}/records/columns` y el detalle | `catalogField.role` y `fields[].role`; ausente es `Data` |

**Errores** (en `Domain/Forms/`, con sus textos en `FormsTexts.resx` y `.en.resx`):

| Código | Tipo | Cuándo |
| --- | --- | --- |
| `Forms.PersonData.NotFound` | NotFound | el dato es ajeno o no existe |
| `Forms.PersonData.MemberNotFound` | NotFound | la persona no es miembro de la organización |
| `Forms.PersonData.TooMany` | Conflict | más de 20 activos, también al reactivar |
| `Forms.PersonData.NameDuplicate` | Validation (`name`) | dos datos con el mismo nombre |
| `Forms.PersonData.SourceInvalid` | Validation (`catalogId`) | la fuente no es un catálogo a mano activo de la organización |
| `Forms.PersonData.InUse` | Conflict | un dato en uso: trae `uses[{formId, formName, questionId, questionLabel, state}]` |
| `Forms.PersonData.ValueRemovalNotConfirmed` | Validation (`type`) | hay valores y falta `confirmValueRemoval`: trae `count` |
| `Forms.PersonData.ValueInvalid` | Validation (`values.<id>`) | un dato dado de baja, un texto largo o una opción que no es de la fuente |
| `Forms.Draft.RecipientFilterInvalid` | Validation (`questions[i].recipientFilters[j]`) | un campo que no es de papel «Filtro», repetido o quitado; un valor fijo fuera de su fuente; un dato con otra fuente, o una pregunta que no es de un catálogo a mano |
| `Forms.Draft.RecipientFilterForCustomers` | Validation (`questions[i]`) | según quien lo recibe, en un formulario solo para clientes |
| `Forms.Offer.RecipientDataMissing` | Conflict | a quien recibe le falta el dato |
| `Forms.Offer.FilterUnavailable` | Conflict | el filtro dejó de funcionar |
| `Forms.Offer.NotForCustomers` | Conflict | según quien lo recibe, para un cliente |

`Forms.Catalog.FieldInUse` suma la columna «Filtro»: cambiar el papel, el tipo o la fuente, y `uses`. `Forms.Catalog.SourceInUse` suma los datos activos.

**Permiso nuevo:** `formpersondata.manage`, con sus textos y la migración de los roles de sistema. La entrada «Datos de las personas» va en Administración, con ese permiso y la feature.

## Datos

Dos migraciones: `<fecha>_FormPersonData.cs` y `<fecha>_SystemRoleFormPersonDataPermissions.cs`. Las dos tablas son `ITenantOwned`, esquema `tenant`, con RLS. `role` y `recipientFilters` viven en el jsonb de la E13 y de la E14: lo que no los tiene sigue igual, sin migrar.

**FormPersonDataDefinitions** (`IAuditable`, `IVersioned`)

| Columna | Tipo | Regla |
| --- | --- | --- |
| `Id`, `TenantId` | uuid | — |
| `Name`, `NameNormalized` | texto ≤80 | obligatorio; único por organización (`UniqueKeys.FormPersonDataName`) |
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
