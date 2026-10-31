# E16 · Datos de las personas y filtros por destinatario (spec)

**Estado:** pendiente · spec de la etapa 16 del bloque (decisiones 34 y 78), con el [formato](plantilla.md) de las etapas. Es la segunda mitad de lo que ArquitecturaBase llama E2, entera: los datos de las personas y los filtros que los usan, juntos en una etapa, así nada queda sin quién lo use. `/plan-etapa` la contrasta con el código real cuando le toque, después de la E15.

## Qué leer

Antes de empezar, en este orden:

1. **Lo anterior:** la [spec de la E13](13-campos.md): datos, contrato y las lecciones; la [E14](14-formularios.md): T22, T23, la §6b y `publication-checks`; y la [E15](15-respuestas.md): «Columnas» y los campos sellados.
2. **Decisiones** de la hoja de ruta: 35, 37, 42, 43, 50 y 78.
3. **Fichas:**

   | Ficha | Por qué acá |
   | --- | --- |
   | [concurrencia](../../rules/concurrencia.md) y [guardado](../../rules/guardado.md) | versión del dato y de los valores; los bloqueos, siempre en el mismo orden |
   | [persistencia-ef](../../rules/persistencia-ef.md) y [multitenancy](../../rules/multitenancy.md) | las dos tablas nuevas, y una fuente dentro de la organización |
   | [permisos](../../rules/permisos.md) | el permiso nuevo; una ruta con dos `[HasPermission]` exige los dos |
   | [datos-personales](../../rules/datos-personales.md) y [auditoria](../../rules/auditoria.md) | los valores se van con la membresía |
   | [validacion](../../rules/validacion.md) y [result-y-errores](../../rules/result-y-errores.md) | errores por campo y los que dicen qué formulario usa algo |
   | [api-http](../../rules/api-http.md) y [paginado-y-busqueda](../../rules/paginado-y-busqueda.md) | las rutas nuevas |
   | [textos-libres](../../rules/textos-libres.md) y [textos-y-traducciones](../../rules/textos-y-traducciones.md) | un solo nombre por dato (35) |
   | [modulos](../../rules/modulos.md) | el núcleo no conoce a Forms |
   | [tests](../../rules/tests.md) | qué lleva test y dónde va |

4. **Código para copiar:** la columna «Copia de» de los [pasos](#pasos).
5. **Referencia de producto,** solo para leer: en `../ArquitecturaBase/docs/plans/2026-10-23-traspaso-multitenant/`, el anexo C §11 (C2-05, C2-06 y C2-09), el anexo D (`CE4-A`) y el anexo E §2.7 (review R1023, puntos 8 y 18 a 20).
6. **La maqueta,** en `../MaquetaMultitenant/src/`: en `areas/business/forms/`, `pages/PersonDataPage.tsx`, `components/MemberDataTab.tsx`, `components/CatalogQuestionFields.tsx` y `components/PublishDialog.tsx`; y `shared/modules/types.ts`, `useModules.ts` y `areas/business/users/pages/UserDetailPage.tsx`. Se copia lo que se ve. Su contrato no vale: manda esta spec. Por ejemplo, allá un filtro nombra el campo por su clave, y acá, por su identidad.

## Qué se construye

**Los datos de las personas,** como «Zona»:
- los define cada organización: nombre (un solo texto, 35), tipo (Texto o «De un catálogo», con una fuente a mano y activa), posición, estado y versión. Hasta 20, contando los dados de baja (42). Se dan de baja y se reactivan, sin borrarse;
- son de la organización y del miembro, nunca de la cuenta (42): una persona puede ser «Centro» en una organización y no tener el dato en otra;
- un dato que usa un filtro del borrador o de la versión activa de un formulario no cambia de tipo ni de fuente, ni se da de baja. El error dice qué formulario y qué pregunta;
- cambiar el tipo o la fuente de un dato con valores pide confirmar cuántos, y borra esos valores;
- un catálogo fuente de un dato activo no se da de baja, igual que el de un campo.

**Los valores:**
- una fila por miembro, con su propia versión y un `PUT` delta: ausente conserva y `null` limpia;
- un texto tiene hasta 300 caracteres. Uno de catálogo guarda la identidad de la opción y exige una opción activa de la fuente. **Un valor guardado que apunta a una opción dada de baja se conserva y sigue filtrando;**
- no van en «Exportar mis datos», porque son de la organización. **Al terminar una membresía, sus valores se borran con ella:** al sacar a alguien de la organización y en la baja de la cuenta. El núcleo suma para eso un punto de aviso neutral, que hoy no existe.

**El papel de un campo:** los campos de la E13 suman su papel, «Dato» (el de siempre) o «Filtro». Los dos se sellan al responder. En la planilla, las subcolumnas de un campo «Filtro» salen apagadas y se prenden en «Columnas». Un campo que usa un filtro del borrador o de la versión activa no se quita ni cambia de papel, tipo o fuente.

**Los filtros por destinatario,** en una pregunta de un catálogo a mano («Qué opciones ve cada persona»):
- filtros de igualdad, uno por campo de papel «Filtro», unidos con «y»;
- **fijo:** un valor elegido, que se valida contra su fuente. Por ejemplo, «Rubro es Frío»;
- **según quien lo recibe:** un dato de la persona, de tipo catálogo, con la misma fuente que el campo. Por ejemplo, «Zona es la zona de quien lo recibe»;
- se guardan en el borrador y se sellan con la versión. Editarlos conserva el `FieldId` si no cambia el catálogo.

**Al mandar,** la preparación de la oferta de la E14 resuelve al miembro real y filtra en SQL, antes del tope de 200 o 20:
- **a quien no tiene el dato no se le manda, y se dice cuál le falta.** También si el valor guardado no es opción de la fuente actual del dato;
- un filtro que dejó de funcionar frena el envío. **Nunca se retira el filtro ni se manda la lista entera;**
- **un cliente no tiene datos** (42): una pregunta con un filtro según quien lo recibe no se le manda, y un filtro fijo sí;
- al operador le llega el motivo y la cantidad, nunca los nombres de las opciones.

**La vista previa «Como si lo recibiera»:** con una persona de ejemplo, pide `formcatalogs.read`, no guarda ni toma bloqueos, y distingue «Lista», «Le falta el dato» y «El filtro no funciona». No nombra opciones de un catálogo que quien mira no puede leer.

**Al publicar** se separan los problemas, que frenan, de los avisos, que no (37):
- **frenan:** un filtro inválido; un filtro fijo que deja más opciones que el tope, si la pregunta no tiene filtros según quien lo recibe; y uno según quien lo recibe en un formulario solo para clientes;
- **avisan:** el máximo de opciones por combinación de datos, contra 200 o 20; cuántas personas activas con `formrecords.create` no tienen el dato; y uno según quien lo recibe en un formulario para los dos, que no se le va a poder mandar a un cliente;
- con un filtro según quien lo recibe, un catálogo de más de 200 opciones se publica: cambia lo que la E14 dice de `CatalogTooLarge`.

## Pantallas

Ninguno está dibujado. Diseño los dibuja con el [texto de la etapa](16-texto-diseno.md), con el recorrido guiado. Muestran solo lo que existe hasta acá (78).

| Tablero | Qué muestra |
| --- | --- |
| `FRM-Datos-Personas` y `M-FRM-Datos-Personas` | «Datos de las personas», en Administración: los datos, la tabla de personas y el diálogo para definir un dato, que allá no se dibujó |
| `FRM-Usuario-Datos` y `M-FRM-Usuario-Datos` | la ficha del usuario, con las pestañas «Acceso» y «Datos» |
| `FRM-Catalogo-16` y `M-FRM-Catalogo-16` | la columna «Filtro» en «Campos» |
| `FRM-Editor-16` y `M-FRM-Editor-16` | «Qué opciones ve cada persona» en la pregunta (`CE4-A`) |
| `FRM-Publicar-16` | los avisos, aparte de lo que frena |
| `WA-Chats-Formulario-16` y `M-WA-Chats-Formulario-16` | el formulario que no se puede mandar, con el motivo |
| `FRM-Respuestas-16` | «Columnas», con las subcolumnas de los filtros apagadas |

**«Datos de las personas»:** dos secciones. Los datos: nombre, tipo, quién lo usa, cuántos tienen valor, estado y ⋮ (Editar, Dar de baja o Reactivar). Las personas: una columna por dato activo, «—» si no tiene y ✎ con `users.manage`. Con el tope de 20, «Nuevo dato» queda grisado, con el motivo en su tooltip. Sin `users.read`, la segunda sección no aparece.

**El diálogo para definir un dato:** un solo campo de nombre, el tipo y, si es de un catálogo, cuál. Si hay valores, confirmar dice cuántos se borran.

**La ficha del usuario:** las pestañas aparecen solo si un módulo aporta una; si no, queda como hoy. «Datos» tiene sus botones y se guarda aparte del acceso, solo si cambió. Salir con cambios pregunta antes. Un error queda en la pestaña.

**«Qué opciones ve cada persona»:** un renglón por filtro, «Campo es valor», con ✕; «Agregar un filtro según quien lo recibe» y «Agregar un filtro fijo»; «Como si lo recibiera», con la persona y el resultado; y debajo el máximo por persona y quién no tiene el dato, con «Cargar los datos» para quien tiene `formpersondata.manage`. Sin campos de papel «Filtro», la sección no aparece. Con solo `forms.read`, se lee sin editar.

**Estados:** para cada tablero, cargando, vacío y error, más:
- **Datos:** 20 datos, un dato en uso (dar de baja grisado, con quién lo usa en su tooltip), cambiar el tipo con valores, y solo `formpersondata.manage`;
- **Filtros:** «Le falta el dato», «El filtro no funciona», más opciones que el tope y un catálogo sin campos de filtro;
- **Publicar:** problemas y avisos juntos, y solo avisos;
- **Enviar:** le falta el dato, el filtro no funciona y es un cliente.

**Mandan sobre esta spec en:** las fichas del front (un dato por celda, sin rótulos que expliquen, botones según la 43). «Dar de baja» y «Reactivar» son las palabras de siempre, y lo dado de baja es una opción del filtro «Estado».

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
| `GET /{id}` y `PUT /{id}/draft` | `questions[].recipientFilters?[]`: `{fieldId, mode, personDataId?, value?}`, con `mode` `fixed` o `recipient` |
| `POST /{id}/publication-checks` | `warnings[]`: `{fieldId, code, max?, maxLabel?, limit?, people?}`, con `code` `MaxOptionsPerRecipient`, `PeopleWithoutData` o `NotForCustomers` |
| `GET …/conversations/{id}/forms` | `offerProblems` suma `RecipientDataMissing`, con `dataName`, `FilterUnavailable` y `NotForCustomers` |
| `GET /{id}/records/columns` y el detalle | `catalogField.role` y `fields[].role`; ausente es `Data` |

**Errores** (en `Domain/Forms/`, con sus textos en `FormsTexts.resx` y `.en.resx`):

| Código | Tipo | Cuándo |
| --- | --- | --- |
| `Forms.PersonData.NotFound` | NotFound | el dato es ajeno o no existe |
| `Forms.PersonData.MemberNotFound` | NotFound | la persona no es miembro de la organización |
| `Forms.PersonData.TooMany` | Conflict | más de 20, contando los dados de baja |
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
| `TenantId`, `MemberId` | uuid | clave; FK compuesta a `Members` |
| `Values` | jsonb, `{}` por defecto | mapa de identidad del dato a texto o a identidad de la opción, con índice GIN `jsonb_path_ops` |

## Pasos

Abreviaturas: `D/`, `A/`, `I/` y `P/` son las capas del back; `F/`, el front; `M/`, la maqueta, y `[E12]` a `[E15]`, un archivo que deja esa etapa.

**Back:**

| # | Qué hace | Archivos | Copia de | Test |
| --- | --- | --- | --- | --- |
| B1 | Dominio, errores y permiso | `D/Forms/FormPersonDataDefinition.cs`, `FormPersonDataValues.cs`, `FormRecipientFilter.cs`, `FormPersonDataErrors.cs`; suma a `Permissions.cs` y `Permissions{,.en}.resx` | `D/Forms/FormCatalog.cs` [E12] | 20 contando las bajas; `PermissionTextsTests` |
| B2 | Tablas y migraciones | `I/Modules/Forms/Persistence/Configurations/FormPersonData*Configuration.cs`; las dos migraciones | `FormCatalogConfiguration.cs` [E12], `20261010203618_SystemRoleAssistantPermissions.cs` | aislamiento y migración, también hacia atrás |
| B3 | Definir: tope, en uso y confirmar el borrado. Bloquea definiciones, dato y fuente, en ese orden | `A/Modules/Forms/Interfaces/Services/IFormPersonDataService.cs`, `A/Modules/Forms/Services/FormPersonDataService.cs`, `FormPersonDataEditor.cs` | `FormCatalogService.cs` [E12] | dos personas crean el dato 20 a la vez; en uso por el borrador y por la versión activa; `count` exacto |
| B4 | Valores por delta. Bloquea dato, persona y fuente, en ese orden | `FormPersonDataValueEditor.cs`, `I/Modules/Forms/Persistence/Readers/FormPersonDataReader.cs` | `FormCatalogFieldValuePolicy.cs` [E13] | delta y `null`; opción dada de baja conservada; dos guardados = 409; un miembro de dos organizaciones; la tabla paginada, sin correos |
| B5 | El aviso de fin de membresía | `A/Interfaces/Integrations/Legal/IMembershipEndParticipant.cs`; suma a `OrganizationUserRemover.cs` y `MembershipDeletionParticipant.cs`; `A/Modules/Forms/Services/FormsMembershipEndParticipant.cs` | `IAccountDeletionParticipant.cs` | sacar a alguien y la baja de la cuenta borran los valores, en la misma UoW; sin el módulo, nada |
| B6 | El papel de un campo, sellado y columnas | suma a `FormCatalogField.cs`, `FormCatalogFieldEditor.cs` [E13], `FormRecordWriter.cs` [E14] y `FormRecordColumnsBuilder.cs` [E15] | esos mismos | no cambia en uso (lección 1); una respuesta vieja se lee como `Data` |
| B7 | Filtros en el borrador | `A/Modules/Forms/Services/FormRecipientFilterPolicy.cs`; suma a `FormDraft.cs` y `FormDraftEditor.cs` [E14] | `FormCatalogFiltersPolicy.cs` [E12] | cada motivo de `RecipientFilterInvalid`; lección 3; conserva el `FieldId` |
| B8 | La oferta filtrada, en SQL | suma a `FormOfferBuilder.cs` [E14] y `FormCatalogReader.cs` [E12] | esos mismos | las dos zonas; sin el dato; valor de otra fuente; filtro roto; cliente; nunca la lista entera; con 201 opciones; 10.000 opciones con EXPLAIN |
| B9 | La vista previa | suma a `FormCatalogService.cs` | `POST …/previews` [E12] | los tres estados; sin escribir; lección 4; persona de otra organización |
| B10 | Publicar: problemas y avisos | suma a `FormPublicationService.cs` [E14] y a `IMemberDirectoryReader.cs` | esos mismos | lección 2; el máximo por combinación; personas sin el dato; para clientes y para los dos |
| B11 | Rutas y contratos | `P/Modules/Forms/Controllers/BusinessFormPersonDataController.cs`, `Contracts/*`, `FormsContractContributor.cs`; `AuditSummary` en `Audit{,.en}.resx`; `GrowingCollectionContractTests` | `BusinessFormCatalogsController.cs` [E12] | cada ruta con 401, 403, feature apagada = 404, validación, 409 y paginado; los dos permisos de `values`; ejemplos exportados |

**Front:**

| # | Qué hace | Archivos | Copia de | Test |
| --- | --- | --- | --- | --- |
| F1 | Tipos y llamadas | suma a `F/src/modules/forms/api/` | `M/…/api/catalogs.ts` | zod estricto con los ejemplos |
| F2 | «Datos de las personas» | `F/src/modules/forms/pages/PersonDataPage.tsx`; suma a `registration.ts` y a sus recursos | `M/…/pages/PersonDataPage.tsx` | permisos, tope, en uso y confirmar; axe |
| F3 | La pestaña «Datos», con el registro neutral | `F/src/modules/forms/components/MemberDataTab.tsx`; suma a `F/src/shared/modules/types.ts`, `useModules.ts` y `UserDetailPage.tsx` | `M/…` | sin el módulo no hay pestañas; guarda aparte; salir con cambios; composición |
| F4 | La columna «Filtro» | suma a `FieldsSection.tsx` [E13] | `M/…/catalog/FieldsSection.tsx` | apagada en uso, con el motivo |
| F5 | «Qué opciones ve cada persona» | suma a `CatalogQuestionFields.tsx` [E14] | `M/…/CatalogQuestionFields.tsx` | fijo y según quien lo recibe; la vista previa se actualiza sola; sin `formcatalogs.read` |
| F6 | Publicar, enviar y «Columnas» | suma a `PublishDialog.tsx`, al diálogo de enviar [E14] y a `FormRecordsPage.tsx` [E15] | `M/…/PublishDialog.tsx` | avisos que no frenan; motivo al mandar; subcolumnas apagadas |

**Al cerrar:** `docs/features/formularios.md` suma la sección, y `docs/features/organizaciones.md`, el aviso de fin de membresía.

## Qué verificar al arrancar

| Marca | Qué hay que confirmar | Qué asume esta spec |
| --- | --- | --- |
| [E14] | La forma de las preguntas en el borrador, `FormOfferBuilder` y el armado de `offerProblems`. | Una pregunta suma una lista, y la oferta filtra antes del tope, con una fila más. |
| [E14] | Cómo quedó `publication-checks`. | Hoy no tiene `warnings`: nacen acá. |
| [E13] | `FieldsJson`, el índice GIN y la columna de la tabla de campos. | `FieldValuesJson @> …` filtra por igualdad con ese índice. |
| [E12] | `IMemberDirectoryReader`. | Suma las ids de los miembros activos con un permiso. |
| [E15] | Cómo «Columnas» guarda lo oculto en la URL. | Admite columnas apagadas por defecto. |
| Núcleo | La ficha del usuario del front, que hoy no tiene pestañas, y `OrganizationUserRemover`. | Si la base de la 12 no trae el registro de la maqueta, entra acá. |
| Núcleo | Otras formas de terminar una membresía. | Solo esos dos caminos llaman a `Member.Remove`. |

## Lo que ya se pagó allá (no repetir)

Cada punto lleva su test, escrito durante la etapa:
1. **Cambiarle el papel a un campo en uso** dejaba sin envío a todo un formulario publicado, sin aviso. `FieldInUse` frena cualquier cambio de papel, tipo o fuente (R1023 #8).
2. **Un filtro fijo que deja más de 20 opciones en «elegir varias»** frena al publicar (#18).
3. **El valor fijo de un filtro** se valida contra su fuente (#19).
4. **La vista previa** exige `formcatalogs.read`, y no nombra opciones de un catálogo que la persona no puede leer (#20).

## Decisiones tomadas (no se vuelven a preguntar)

- **Por organización y por miembro** (42). Un dato no depende de la empresa.
- **Hasta 20 datos de las personas,** iguales para todas las organizaciones (42).
- **Los avisos de publicar** no frenan (37).
- **Un formulario para los dos** con un filtro según quien lo recibe se publica con un aviso, y se manda solo a empleados.
- **La pestaña «Datos»** se ve con `users.manage`: quien solo lee las ve en la tabla, con los permisos de esa pantalla.

## Fuera de esta etapa

- Datos para clientes, un dato que dependa de la empresa, varios valores de un mismo dato y operadores distintos de «es igual a».
- Una columna de datos en la lista de usuarios.
- Completar otras preguntas con los datos de la opción.
- El filtro «según otra respuesta»: no funciona en un Flow de WhatsApp, así que en esta etapa no hay dónde usarlo. Va en la E20 (94).

## Cómo se cierra

La puerta de la etapa, con la lista de control de `/puerta`, más una **prueba real con Meta** con el número de una organización de prueba:
1. dos personas con distinta «Zona» reciben el mismo formulario, cada una con los proveedores de su zona;
2. una persona sin «Zona» no lo recibe, y el operador ve el motivo;
3. una persona que es miembro de dos organizaciones, con «Zona» en una sola, recibe según cada organización.

El recorrido suma: definir «Zona», cargarla en la ficha de dos personas, armar el filtro, verlo con «Como si lo recibiera» y publicar con el aviso de quién no tiene el dato; y sacar a una persona de la organización, con sus valores borrados.

## Requisitos previos

La E15 cerrada.

## En el multitenant

- **Datos de la organización:** definiciones y valores son `ITenantOwned` con RLS, y los valores van por membresía. Cada ruta nueva lleva su test de aislamiento.
- **Workers:** no hay.
- **Feature `forms`:** las rutas llevan `[FeatureGate("forms")]`.
- **Permisos de organización:** suma `formpersondata.manage`.
- **El núcleo no conoce a Forms:** la pestaña «Datos» y el aviso de fin de membresía entran por registros neutrales.
- **Cuotas:** las rutas cuentan en las cuotas generales.
