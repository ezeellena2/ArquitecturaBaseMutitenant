# E13 · Campos de los catálogos (spec)

**Estado:** pendiente · spec de la etapa 13 del bloque (decisiones 34, 78, 105, 111, 125 y 130), con el [formato](plantilla.md) de las etapas. Es la parte de catálogo de lo que ArquitecturaBase llama E2, programado allá con un review de 39 puntos: los campos, sus valores, las columnas al importar y qué se ve en WhatsApp, con la vista previa del catálogo. `/plan-etapa` la contrasta con el código real cuando le toque, después de la E12.

## Qué leer

Antes de empezar, en este orden:

1. **Lo anterior:** la [spec de la E12](12-catalogos.md): C3 (guardar), C6 (valores), C7 a C11 (carga de una vez) y sus secciones de datos, contrato, flujos y archivos. Esta etapa suma a esas piezas, no crea otras.
2. **Decisiones** de la hoja de ruta: 35, 36, 42, 43, 78, 105, 109 a 111, 115, 116, 119, 125, 127, 129, 130, 261 a 264 y 269, que aplican «Decisiones tomadas» y el texto, y 268, las acciones del asistente, con la [ADR 0068](../../decisions/0068-acciones-del-asistente.md).
3. **Fichas:**

   | Ficha | Por qué acá |
   | --- | --- |
   | [concurrencia](../../rules/concurrencia.md) | versión del catálogo, 409 y guardar sin cambios |
   | [guardado](../../rules/guardado.md) | una sola UoW; locks → lecturas → reglas → escrituras |
   | [persistencia-ef](../../rules/persistencia-ef.md) y [multitenancy](../../rules/multitenancy.md) | las columnas nuevas, la migración y las fuentes dentro de la organización |
   | [validacion](../../rules/validacion.md) y [result-y-errores](../../rules/result-y-errores.md) | los errores por celda y los 409 que no son de versión |
   | [numeros-y-moneda](../../rules/numeros-y-moneda.md) y [fechas-y-zonas](../../rules/fechas-y-zonas.md) | los valores canónicos, y leer con la cultura del pedido |
   | [textos-libres](../../rules/textos-libres.md) y [textos-y-traducciones](../../rules/textos-y-traducciones.md) | el nombre de un campo es un solo texto; los errores van en es y en |
   | [api-http](../../rules/api-http.md) y [paginado-y-busqueda](../../rules/paginado-y-busqueda.md) | las rutas que cambian |
   | [auditoria](../../rules/auditoria.md) y [logs](../../rules/logs.md) | se audita solo lo que cambió; los valores no van a los logs |
   | [tests](../../rules/tests.md) | qué lleva test y dónde va |

4. **Código para copiar:** la columna «Copia de» de los [pasos](#pasos).
5. **Referencia de producto,** solo para leer: en `../ArquitecturaBase/docs/plans/2026-10-23-traspaso-multitenant/`, el anexo C §11 (C2-01 a C2-04, C2-06 y C2-08), el anexo D (CE1, CE2 y CE3-B, con el «Ajuste del review de E2») y el anexo E §2.7 (review R1023).

## Qué se construye

- **Campos por opción,** solo en los catálogos a mano:
  - hasta 20 activos por catálogo (42 y 105): quitar uno libera su lugar. Con su versión y el 409;
  - cada campo tiene identidad estable, nombre (un solo texto, 35), tipo, reglas, posición y estado. Su clave nace del nombre, en minúsculas, se puede escribir hasta guardar, y después queda fija, con su candado (130);
  - **tipos:** Texto, Número entero, Número con decimales, Fecha, Sí/No y «De un catálogo», que toma los valores de otro catálogo a mano activo de la misma organización, elegido en un combo (111);
  - **reglas:** obligatorio, único y un límite: el largo en un texto (de 1 a 300), y el mínimo y el máximo en un número o una fecha. Obligatorio y único se comprueban sobre las opciones activas.
- **El ciclo de un campo:**
  - nace opcional, y se cargan valores después de guardarlo;
  - quitar un campo lo saca de la tabla y conserva sus valores guardados. No se reactiva desde la pantalla. Con valores, pide confirmación;
  - cambiar el tipo o las reglas valida todas las opciones bajo el bloqueo del catálogo, y un error revierte todo;
  - al convertir a número o a fecha se lee con la cultura del pedido, y lo ambiguo («1.500», «12.345») no convierte. A texto, queda el texto que se ve: el de la opción fuente, aunque esté dada de baja, o «Sí» o «No».
- **Valores:**
  - el valor se guarda canónico, con la precisión de los números de la E12 (C6): enteros y decimales exactos, fecha ISO, sí/no y la identidad de la opción fuente. En la app, un número se lee como el valor de la E12: con la región del idioma, «1.500» en español es `numberAmbiguous` (207) y llega con todos sus dígitos, leído con `parseDecimalText` de la E12;
  - en el `PUT` del catálogo, `items[i].fields` es un delta: ausente conserva y `null` limpia. La importación manda todos los campos activos de la fila;
  - un valor de «De un catálogo» guarda la identidad de la opción fuente. Un valor nuevo exige una opción activa, y **el guardado que apunta a una opción dada de baja se conserva y no impide guardar**. Uno que no es opción de la fuente se rechaza;
  - un catálogo usado como fuente por campos activos no se da de baja.
- **Ordenar por un campo** (224): «Ordenar por…» de la E12 suma cada campo activo ya guardado. Cada tipo compara en el suyo, con el sentido dicho en palabras: un número, «De menor a mayor»; una fecha, «De la más antigua a la más reciente»; sí o no, «Primero «No»»; uno de otro catálogo, por el texto de su opción. Si se quita el campo elegido, el orden pendiente se cancela con él.
- **La tabla, el modal de opciones, pegar, importar y exportar:** una columna por campo después del código y antes del detalle, con «Columnas», la pieza múltiple (262), para ocultarlas, y una fila por campo en el modal. Los encabezados se reconocen por el nombre o por la clave. La plantilla y la exportación suman las columnas, y en una fuente se acepta la identidad, el código o el texto único de la opción. Una celda que nombra la opción guardada dada de baja la conserva; si nombra una activa, pasa a esa.
- **El Excel se lee como en pantalla** (119): cada columna lleva el nombre del campo; número y fecha, como la columna del valor de la E12 (C10); Sí/No, con su palabra, y una fuente, con el texto de la opción o, si ese texto llevaría a otra, con su código. Subirlo de vuelta no da diferencias.
- **Qué se ve en WhatsApp (la presentación del catálogo):**
  - el catálogo elige qué arma el título (hasta 30, con contenido) y la descripción (hasta 300) de cada opción. Cada uno sale de uno o más lugares, unidos con « · »: el título arranca en «Texto» y no se queda sin ninguno;
  - cada lugar es el texto, el código, el valor (si el catálogo es tipado), el detalle o un campo. Todos pueden ir a WhatsApp;
  - el editor guarda y avisa, opción por opción, lo que no entra;
  - el texto de la opción sigue siendo su etiqueta.
- **Volumen:** 10.000 opciones por 20 campos, medido con EXPLAIN al guardar, importar y exportar. Guardar manda y escribe solo lo que cambió (C3).

## Pantallas

Aprobadas y publicadas en el lienzo v150, con el armado de ArquitecturaBase (125 a 130). El recorrido guiado es `campos-de-un-catalogo`. Muestran solo el catálogo (78).

| Tablero | Qué muestra |
| --- | --- |
| `FRM-Catalogo-13` y `M-FRM-Catalogo-13` | el editor de un catálogo a mano: «El catálogo» y «En WhatsApp» con el teléfono al costado de las dos, y «Opciones» con «Agregar campos · N» y una columna por campo |
| `FRM-Catalogo-Campos` y `M-FRM-Catalogo-Campos` | el modal «Campos de …» |
| `FRM-Catalogo-Opciones-13` y `M-FRM-Catalogo-Opciones-13` | el modal de opciones de la E12, con una fila por campo |

Del traspaso 15 (v175): «Ordenar por…» en el ⋮ de estos tableros y ordenar por un campo, como «Rubro» (224), y, nuevos, `FRM-Catalogo-Ordenar-13` y `M-FRM-Catalogo-Ordenar-13`: «Ordenar las opciones» por un número, una fecha, sí o no o un campo de otro catálogo.

**Se redibujan con el traspaso 18, tanda 2** (261 a 263):
- `FRM-Catalogo-13` y su teléfono: «Reactivar» una opción, en el ⋮ de su fila (261), y «Columnas», en la pieza múltiple (262);
- `FRM-Catalogo-Campos` y su teléfono: en la lista, el número, el nombre y el tipo, cada uno en su columna (263); el tipo y el catálogo de un campo, en la pieza (262), como dice «El catálogo de un campo»;
- `FRM-Catalogo-Opciones-13` y su teléfono: el Estado, en la pieza, y el código en su columna, como en la E12;
- `FRM-Catalogo-Ordenar-13` y su teléfono: la columna y el orden, en la pieza (262).

**Redibujados y aprobados el 2026-11-26** (lienzo v186, front `f8192b07`; decisión 285).

**«Agregar campos · N»:** va en el encabezado de «Opciones», al lado de «Agregar opción», y cuenta los activos; se pone en rojo si el servidor rechazó uno. Quien solo lee ve «Campos», si hay. Sin campos, abre en uno nuevo.

**El modal «Campos de …» (125):** igual al de opciones, cada lado en su caja.
- **La lista:** «Campos · N de 20», con el número, el nombre y el tipo, cada uno en su columna, nunca el tipo debajo del nombre (263); ↑ ↓ en el elegido; «+ Agregar campo» al pie, grisado con el motivo en su tooltip al llegar a 20.
- **El campo:** «Campo 1 de 2» o «Nuevo campo» con «Quitar campo» en su banda, y las filas Nombre, Clave con su (i) y su candado, Tipo con su largo, rango o catálogo debajo, y Reglas (Obligatorio y Único). Uno nuevo nace opcional: «Obligatorio» grisado, con el motivo en su tooltip.
- **Guardar:** escribe en una copia. «Listo» la deja en el catálogo y, si hay un campo nuevo, avisa «Guardá el catálogo para cargar los valores de los campos nuevos.». «Cancelar» o Esc la descartan. Se guarda con «Guardar cambios» de la página.
- **Quitar:** pregunta; con valores, pide marcar «Sí, quitar el campo y sus N valores». Si está en «En WhatsApp», lo dice.
- **Celular:** ocupa la pantalla. Primero la lista y, al tocar un campo, el campo con «Volver a la lista».

**Borrar una opción (129):** una opción que es valor de un campo de otro catálogo no se borra: `Forms.Catalog.ItemInUse` suma `uses: options`, y debajo de la fila se lee «No se puede borrar: la usa otra opción. Podés darla de baja.».

**El catálogo de un campo (111):** con el tipo «De un catálogo», un combo debajo del tipo, que es la pieza (262). Cerrado dice el catálogo elegido («Zonas»). Abierto muestra los primeros catálogos a mano activos, cada uno con su cantidad de opciones en gris debajo del nombre («8 opciones»), y como vienen del servidor, el buscador va adentro de la lista, con el foco puesto: no se escribe en el campo. Es el listado de la E12, con `source=manual`, `status=active` y `search`.

**«En WhatsApp»:** va debajo de «El catálogo». «Título · hasta 30 caracteres» y «Descripción · hasta 300», cada uno con un chip por lugar, con ✕, y «Agregar campo», la pieza (262), con lo que falta. Con un solo lugar en el título, su ✕ va apagado. «Por ejemplo» muestra la primera opción activa. Lo que no entra se avisa debajo, con cuántas son y hasta tres ejemplos, y en el teléfono.

**Estados dibujados:**
- **La página:** los campos, un campo recién agregado (la columna grisada, con el motivo en su tooltip, y el aviso), cambiar el tipo con errores (arriba de la tabla, con «Revisar», que abre el modal en ese campo), una opción que usa otra (no se borra), «En WhatsApp», elegir qué muestra, «Columnas», un catálogo nuevo, guardado, un valor de una opción dada de baja, solo `formcatalogs.read` y un título que no entra.
- **El modal:** los campos, uno de un catálogo, el combo abierto, filtrando y sin coincidencias, agregar un campo, elegir el tipo, un número, clave repetida, quitar con y sin valores, 20 campos, sin campos todavía y solo lectura.

**Valen las de toda la app** (160 a 164, 173 a 175 y 180), que llegan con las piezas de la base de la 12. Los chips de «En WhatsApp» llevan el radio de un control (173).

## Contrato

Son las rutas de la E12, que suman lo siguiente. Permisos: `formcatalogs.read` para leer y `formcatalogs.manage` para cambiar. Sin rutas nuevas. El contrato va en `FormsContractContributor.cs`, con ejemplos exportados de una corrida real.

| Ruta | Qué suma |
| --- | --- |
| `GET /api/organization/forms/catalogs/{id}` | `fields[]`: `{id, key, name, type, sourceCatalogId?, sourceCatalogName?, required, unique, maxLength?, min?, max?, isActive}`, en orden; y `presentation`: `{title[], description[]}`, donde cada uno es una lista de lugares y un lugar es `text`, `code`, `value`, `detail` o `field:<id>` |
| `GET …/catalogs/{id}/items` | cada opción suma `fields`, un mapa de `id` del campo a valor canónico, y `presentationIssue?` (`TitleEmpty`, `TitleTooLong` o `DescriptionTooLong`). Suma el filtro `ids`, repetido, hasta 100 y sin vacíos, para nombrar las opciones de una fuente; si no, 400 |
| `POST …/catalogs` | `fields?[]` y `presentation?` en uno a mano |
| `PUT …/catalogs/{id}` | `fields?[]`: la lista entera, si cambió, con `id` o `localId`; el orden es el de la lista. `presentation?`. `items[i].fields`: delta por `id` de campo. `confirmedFieldRemovals?[]`: los campos con valores que se quitan. `sort.column` admite también el `id` de un campo activo guardado (224). Responde también el mapa de ids de los campos |
| `POST …/imports/previews` y `POST …/imports` | las columnas de los campos; los errores por fila y columna siguen la misma forma |
| `GET …/export` | una columna por campo activo, como en pantalla (119) |
| `GET …/template` | suma `catalogId?`: con él, trae las columnas de sus campos |

**Valores en el JSON:** un número va como texto, para no perder precisión; la fecha, `yyyy-MM-dd`; sí/no, booleano; una fuente, la identidad de la opción. `""` no es `null`: se valida como texto vacío.

**Errores** (en `Domain/Forms/FormCatalogErrors.cs`, con sus textos en `FormsTexts.resx` y `.en.resx`):

| Código | Tipo | Cuándo |
| --- | --- | --- |
| `Forms.Catalog.TooManyFields` | Validation (`fields`) | más de 20 activos |
| `Forms.Catalog.FieldKeyDuplicate` | Validation (`fields[i].key`) | dos campos activos con la misma clave, como «Cuit» y «CUIT»; el modal lo dice debajo de la clave: «Ya la usa «CUIT».» |
| `Forms.Catalog.FieldInvalid` | Validation (`fields[i]`) | tipo, regla o fuente fuera de lo permitido, o un campo en un catálogo que no es a mano |
| `Forms.Catalog.FieldRemovalNotConfirmed` | Validation (`fields[i]`) | quitar un campo con valores sin confirmarlo |
| `Forms.Catalog.FieldTypeNotConvertible` | Validation (`fields[i].type`) | valores que no convierten al tipo nuevo; trae `invalidRows`, las primeras 50 en orden de fila, con su motivo |
| `Forms.Catalog.FieldValuesIncompatible` | Validation (`fields[i]`) | valores que no cumplen una regla nueva, con `invalidRows` |
| `Forms.Catalog.FieldValuesMissing` | Validation (`fields[i].required`) | opciones activas sin valor al volverlo obligatorio |
| `Forms.Catalog.FieldValueInvalid` | Validation (`items[i].fields.<id>`) | el valor no es del tipo o no cumple el límite |
| `Forms.Catalog.FieldValueRequired` | Validation (`items[i].fields.<id>`) | falta el valor de un campo obligatorio en una opción activa |
| `Forms.Catalog.FieldValueDuplicate` | Validation (`items[i].fields.<id>`) | un valor único ya está en otra opción activa |
| `Forms.Catalog.SourceInUse` | Conflict | dar de baja un catálogo que es fuente de campos activos. No pone el editor en conflicto |
| `Forms.Catalog.PresentationInvalid` | Validation (`presentation`) | un lugar que no existe, o el título sin lugar |

Motivos de `invalidRows`: `required`, `range`, `length` e `invalid`. El 409 de edición sigue siendo el común, `General.ConcurrencyConflict`.

## Datos

Una migración aditiva, `<fecha>_FormCatalogFields.cs`, sobre las tablas de la E12 (`ITenantOwned`, esquema `tenant`, con RLS). Sin tablas nuevas.

| Tabla | Columna | Tipo | Regla |
| --- | --- | --- | --- |
| `FormCatalogs` | `FieldsJson` | jsonb, `[]` por defecto | la lista de campos, también los quitados; `{id, key, name, type, sourceCatalogId?, required, unique, maxLength?, min?, max?, position, isActive}`. Vacía si no es a mano |
| `FormCatalogs` | `Presentation` | jsonb, `{"title":["text"],"description":["detail"]}` por defecto | `{title[], description[]}`; así la tiene también un alta de la versión anterior (0060) |
| `FormCatalogItems` | `FieldValuesJson` | jsonb, `{}` por defecto | mapa de `key` a valor canónico, con índice GIN `jsonb_path_ops` |

**La clave:** se arma con el nombre, en minúsculas y sin acentos, con `^[a-z][a-z0-9_]{0,39}$`, y queda fija. Si choca con `text`, `code`, `value`, `detail` o con un campo quitado, suma un número. En el JSON del contrato se usa la identidad del campo, y la clave queda adentro.

## Pasos

Abreviaturas: `D/`, `A/`, `I/` y `P/` son las capas del back; `F/`, el front, y `[E12]`, un archivo que deja esa etapa.

**Back:**

| # | Qué hace | Archivos | Copia de | Test |
| --- | --- | --- | --- | --- |
| B1 | Campo, tipos, clave y reglas en el dominio; `ChangeFields`, `ChangePresentation` y `SetFieldValues` | `D/Forms/FormCatalogField.cs`, `FormCatalogFieldType.cs`, `FormCatalogPresentation.cs`; suma a `FormCatalog.cs`, `FormCatalogItem.cs` y `FormCatalogErrors.cs` | `D/Forms/FormCatalog.cs` [E12] | `Domain.UnitTests/Forms/`: clave fija, reservadas y repetidas; 20 contando solo los activos (105); reglas por tipo |
| B2 | Columnas, índice y migración | `I/Modules/Forms/Persistence/Configurations/FormCatalogConfiguration.cs`, `FormCatalogItemConfiguration.cs`, `I/Persistence/Migrations/<fecha>_FormCatalogFields.cs` | `AssistantMessageConfiguration.cs` (jsonb) y `AssistantDurableOperationConfiguration.cs` (GIN) | aislamiento y migración, también hacia atrás; un alta de la versión anterior toma los valores por defecto |
| B3 | Leer y validar el valor de un campo, con la cultura del pedido | `A/Modules/Forms/Services/FormCatalogFieldValuePolicy.cs` | `FormCatalogFiltersPolicy.cs` [E12] | «1.500», «12.345», `""`, `null` y una fuente dada de baja, en es y en |
| B4 | Guardar campos, presentación y delta de valores, bajo el bloqueo del catálogo y de sus fuentes, siempre en el mismo orden | suma a `FormCatalogService.cs` [E12]; `FormCatalogFieldEditor.cs` | `FormCatalogItemEditor.cs` [E12] | delta; guardar sin cambios; una opción entre 10.000 escribe y audita solo esa; 409; dos personas a la vez sobre el catálogo y su fuente; ordenar por un campo de cada tipo (número, fecha, sí o no, de otro catálogo), con lo vacío al final |
| B5 | Cambiar tipo o reglas: valida todas las opciones por lotes, y un error revierte todo | `A/Modules/Forms/Services/FormCatalogFieldChangePolicy.cs` | `FormCatalogImportPolicy.cs` [E12] | conversiones, `invalidRows` con las primeras 50, obligatorio y único sobre activas |
| B6 | Quitar un campo y proteger una fuente | suma a `FormCatalogFieldEditor.cs` y `FormCatalogService.cs` | `FormCatalogPositionGuard.cs` [E12] | confirmación con valores; `SourceInUse`; la lección 1 |
| B7 | Pegar, importar, exportar y la plantilla, con las columnas de los campos | suma a `FormCatalogFileResolver.cs`, `FormCatalogImportPolicy.cs` y `FormCatalogExportBuilder.cs` [E12] | esos mismos | exportar y reimportar sin diferencias, con número, fecha, Sí/No y fuente; dos opciones con el mismo texto; encabezado por nombre o clave; columna ausente conserva y vacía limpia; una fuente por identidad, código o texto único |
| B8 | Armar título y descripción de cada opción, y `presentationIssue` | `A/Modules/Forms/Services/FormCatalogPresentationBuilder.cs` | `FormCatalogExportBuilder.cs` [E12] | 30 y 300 caracteres; contenido vacío; un campo quitado sale de la presentación |
| B9 | Rutas, contratos y ejemplos | suma a `P/Modules/Forms/Controllers/BusinessFormCatalogsController.cs`, a los contratos, `Models` y `Validation` de la E12, sin contratos nuevos, y a `FormsContractContributor.cs` [E12]; textos en `FormsTexts{,.en}.resx` | esos mismos | cada ruta con 401, 403, feature apagada = 404, validación, 409 y paginado; un código por cada 409; ejemplos exportados |
| B10 | Medir el volumen | prueba de volumen del módulo | la de la E12 | 10.000 opciones por 20 campos: guardar, importar y exportar con EXPLAIN |

**Front:**

| # | Qué hace | Archivos | Copia de | Test |
| --- | --- | --- | --- | --- |
| F1 | Tipos y llamadas, validados con los ejemplos exportados | suma a `F/src/modules/forms/api/` | la API de catálogos [E12] | zod estricto con los ejemplos |
| F2 | El modal «Campos de …», con el combo del catálogo | `F/src/modules/forms/components/catalog/CatalogFieldsDialog.tsx`, `CatalogCombo.tsx` | su tablero, el modal de opciones [E12] y `Picker` (262) | agregar, subir, bajar y quitar; una copia hasta «Listo»; tope de 20; la clave y su candado; el combo abre con los primeros y filtra; celular; axe |
| F3 | Una columna por campo en la tabla de opciones, y una fila en el modal, con el control de su tipo | suma a `OptionsSection.tsx` y al modal de opciones [E12] | su tablero | columna y fila grisadas hasta guardar; valor dado de baja; la lección 4 |
| F4 | «En WhatsApp», con sus chips y «Por ejemplo», y el teléfono al costado | `F/src/modules/forms/components/catalog/PresentationEditor.tsx`, `CatalogPreviewPhone.tsx` | su tablero, `FilterChip.tsx` (base de la 12) y `Picker` (262) | cada pieza lista lo que falta; se arma como el servidor; un título que no entra |
| F5 | El editor: conflicto, guardado y lectura | suma a `CatalogEditorPage.tsx` [E12] | `CatalogEditorPage.tsx` [E12] | la lección 2; solo `formcatalogs.read`; textos en es y en |

**Al cerrar:** `docs/features/formularios.md` suma la sección de los campos.

## Acciones del asistente

No declara acciones nuevas: `catalogoptions.edit@1` de la E12 (§17 de su spec) toma los campos activos del catálogo destino como dato, sin versión nueva (0068, «Consecuencias»). Cada tipo cae en la lista cerrada (0068 §4): Texto → `Text`, Número entero → `Integer`, Número con decimales → `Decimal`, Fecha → `Date`, Sí/No → `Boolean` y «De un catálogo» → `Record`, que resuelve el servidor. Sus reglas son los límites. El adaptador dibuja la columna de cada campo, y los casos de la acción suman uno con un campo. Los campos de un catálogo se arman en «Campos de …», que no es una acción.

## Qué verificar al arrancar

| Marca | Qué hay que confirmar | Qué asume esta spec |
| --- | --- | --- |
| [E12] | Cómo quedó el `PUT` del catálogo y su delta de opciones. | `items` lleva solo lo cambiado, y el servidor escribe y audita solo eso. |
| [E12] | Los nombres reales de los helpers y de las páginas del front. | Los de la columna «Archivos», tomados de la spec de la E12. |
| [E12] | Los lectores de archivos y el puerto `IFormCatalogSpreadsheet`. | Leen columnas por nombre y con la cultura del pedido. |
| [E12] | La barrera de C11 y sus tiempos. | Cubre también los archivos con columnas de campos. |
| [E12] | El bloqueo del catálogo. | Se pueden bloquear dos catálogos en un orden fijo, sin esperar un deadlock. |
| Base de la 12 | Los botones del front. | Ponen el aro y su gerundio, y lo apagado dice por qué en su tooltip. |
| [E12] | La tabla de opciones con columnas fijas. | La dejó la E12: la casilla, el número y «Texto» a la izquierda y las acciones a la derecha; las columnas de los campos se desplazan entre ellas. |

## Lo que ya se pagó allá (no repetir)

Cada punto lleva su test, escrito durante la etapa:
1. **Dar de baja una opción de la fuente no bloquea los catálogos que la usan.** Un catálogo con un campo que apunta a una opción dada de baja se guarda, se importa y se exporta sin error, y conserva ese valor.
2. **Un 409 de una regla no es un conflicto de versión.** Solo `General.ConcurrencyConflict` pone el editor en conflicto; dar de baja un catálogo que es fuente de campos activos es un error que se corrige. Hay test en el back (el código de cada 409) y en el front (el editor queda editable).
3. **Un texto vacío no rompe la lectura.** Se prueba con un nombre o un valor de texto `""`, con `null` y con una referencia a una opción dada de baja, guardando y releyendo.
4. **El front no tapa al servidor al cambiar el tipo.** Cambiar el tipo de un campo con valores que no convierten muestra los errores del servidor por fila, y el front no bloquea antes de mandar.
5. **Un solo selector de catálogo.** El campo usa un combo que es una pieza aparte del módulo, sin nada propio de los campos.

## Decisiones tomadas (no se vuelven a preguntar)

- **Hasta 20 campos activos por catálogo,** igual para todas las organizaciones (42 y 105).
- **Títulos de más de 30 caracteres** (36): en un catálogo a mano, el título que arma la presentación se avisa al guardar y se marca en la vista previa.
- **El contenido no tiene idioma** (35): un solo nombre por campo.
- **Ningún nombre de campo va en el código:** cada organización arma los suyos.
- **Una columna de un campo nuevo** queda grisada hasta guardar el catálogo, con su motivo, y «Listo» lo avisa.
- **Los campos van en un modal, como en ArquitecturaBase** (125, que reemplaza la 118). La clave se muestra, con su candado (130).
- **Un campo que usa la presentación** del catálogo se puede quitar: sale del título y de la descripción.
- **Lo que se hace con una opción** va en el ⋮ de su fila, con ↑ ↓ ✎ 🗑 a la vista (261); todo lo que se elige, también «Columnas», es la pieza `Picker` (262); nada va debajo de otro dato (263), y la tabla de opciones y la lista de campos no ordenan por columnas, porque su orden es un dato (264).
- **El tope de un texto** (el nombre, la clave y el largo de un valor de texto) es el mismo en la pantalla y en el servidor, y al llegar se ve (269).

## Fuera de esta etapa

- Usar los campos en un formulario: la presentación propia de una pregunta, el sellado al responder y no poder quitar un campo que usa un formulario (E14). Hasta entonces, nada usa un campo.
- El papel «Filtro» de un campo, los filtros por destinatario y los datos de las personas (E16).
- Operadores distintos de «es igual a», y varios valores de un mismo campo.
- Completar otras preguntas con los datos de la opción.
- La búsqueda en vivo y los orígenes externos: después del bloque.

## Cómo se cierra

La puerta de la etapa, con la lista de control de `/puerta`. No hay prueba con Meta, porque la etapa no manda nada por WhatsApp. El recorrido prueba:
1. un catálogo «Proveedores» con CUIT, Zona (elegir del catálogo «Zonas») y Plazo de pago, cargados importando una planilla, y exportado de nuevo sin diferencias;
2. la presentación con el CUIT en la descripción, vista en el teléfono al costado;
3. dar de baja una zona: «Proveedores» se sigue guardando, importando y exportando, con el valor que tenía.

## Requisitos previos

La E12 cerrada.

## En el multitenant

- **Datos de la organización:** campos y valores van en las tablas de catálogos, `ITenantOwned` con RLS, y una fuente tiene que ser de la misma organización: una ajena se rechaza igual que una que no existe.
- **Workers:** no hay.
- **Feature `forms`:** las rutas llevan `[FeatureGate("forms")]`.
- **Permisos de organización:** los de catálogos de la E12 (`formcatalogs.read` y `formcatalogs.manage`), sin permisos nuevos.
- **Botones:** según la decisión 43.
- **Cuotas:** las rutas cuentan en las cuotas generales.
