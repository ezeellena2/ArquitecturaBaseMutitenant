# E13 · Campos de los catálogos (spec)

**Estado:** pendiente · spec de la etapa 13 del bloque (decisiones 34, 78 y 105), con el [formato](plantilla.md) de las etapas. Es la parte de catálogo de lo que ArquitecturaBase llama E2, programado allá con un review de 39 puntos: los campos, sus valores, las columnas al importar y qué se ve en WhatsApp, con la vista previa del catálogo. `/plan-etapa` la contrasta con el código real cuando le toque, después de la E12.

## Qué leer

Antes de empezar, en este orden:

1. **Lo anterior:** la [spec de la E12](12-catalogos.md): C3 (guardar), C6 (valores), C7 a C11 (carga de una vez) y sus secciones de datos, contrato, flujos y archivos. Esta etapa suma a esas piezas, no crea otras.
2. **Decisiones** de la hoja de ruta: 35 (el contenido no tiene idioma), 36 (títulos de más de 30 caracteres), 42 (hasta 20 campos), 43 (botones y avisos), 78 (el orden) y 105 (los topes cuentan los activos).
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
6. **La maqueta,** en `../MaquetaMultitenant/src/areas/business/forms/`: `components/catalog/FieldsSection.tsx`, `OptionsSection.tsx`, `fixedColumns.ts` y `CatalogPreviewPhone.tsx`, y `components/PresentationEditor.tsx`. Se copia lo que se ve. Su back de mentira no es el contrato: manda esta spec. Por ejemplo, allá el título es una lista y el campo tiene un papel, que acá llega después.

## Qué se construye

- **Campos por opción,** solo en los catálogos a mano:
  - hasta 20 activos por catálogo (42 y 105): quitar uno libera su lugar. Con su versión y el 409;
  - cada campo tiene identidad estable, nombre (un solo texto, 35), tipo, reglas, posición y estado. Su clave se arma sola con el nombre, en minúsculas, queda fija después de guardar y no se muestra;
  - **tipos:** Texto, Número entero, Número con decimales, Fecha, Sí/No y «De un catálogo: …», que toma los valores de otro catálogo a mano activo de la misma organización;
  - **reglas:** obligatorio, único y un límite: el largo en un texto (de 1 a 300), y el mínimo y el máximo en un número o una fecha. Obligatorio y único se comprueban sobre las opciones activas.
- **El ciclo de un campo:**
  - nace opcional, y se cargan valores después de guardarlo;
  - quitar un campo lo saca de la tabla y conserva sus valores guardados. No se reactiva desde la pantalla. Con valores, pide confirmación;
  - cambiar el tipo o las reglas valida todas las opciones bajo el bloqueo del catálogo, y un error revierte todo;
  - al convertir a número o a fecha se lee con la cultura del pedido, y lo ambiguo («1.500», «12.345») no convierte. A texto, queda el texto que se ve: el de la opción fuente, aunque esté dada de baja, o «Sí» o «No».
- **Valores:**
  - el valor se guarda canónico, con la precisión de los números de la E12 (C6): enteros y decimales exactos, fecha ISO, sí/no y la identidad de la opción fuente;
  - en el `PUT` del catálogo, `items[i].fields` es un delta: ausente conserva y `null` limpia. La importación manda todos los campos activos de la fila;
  - un valor de «De un catálogo» guarda la identidad de la opción fuente. Un valor nuevo exige una opción activa, y **el guardado que apunta a una opción dada de baja se conserva y no impide guardar**. Uno que no es opción de la fuente se rechaza;
  - un catálogo usado como fuente por campos activos no se da de baja.
- **La tabla, pegar, importar y exportar:** una columna por campo después del código. Los encabezados se reconocen por el nombre o por la clave. La plantilla y la exportación suman las columnas, y en una fuente se acepta la identidad, el código o el texto único de la opción. Una celda que nombra la opción guardada dada de baja la conserva; si nombra una activa, pasa a esa.
- **Qué se ve en WhatsApp (la presentación del catálogo):**
  - el catálogo elige qué arma el título (hasta 30, con contenido) y la descripción (hasta 300) de cada opción. El título sale de un solo lugar, que arranca en «Texto»; la descripción, de uno o más lugares, unidos con « · »;
  - cada lugar es el texto, el código, el valor (si el catálogo es tipado), el detalle o un campo. Todos pueden ir a WhatsApp;
  - el editor guarda y avisa, opción por opción, lo que no entra;
  - el texto de la opción sigue siendo su etiqueta.
- **Volumen:** 10.000 opciones por 20 campos, medido con EXPLAIN al guardar, importar y exportar. Guardar manda y escribe solo lo que cambió (C3).

## Pantallas

Aprobadas en el lienzo v145. Los dibujos están en `docs/design/capturas/plan/etapa-13/` del front, y el recorrido guiado es `armar-un-catalogo`. Muestran solo el catálogo (78).

| Tablero | Qué muestra |
| --- | --- |
| `FRM-Catalogo-13` | el editor de un catálogo a mano: «Campos» en la forma B (`CE1`), la tabla de opciones con una columna por campo (`CE2`) y «En WhatsApp» en la forma C (`CE3-B`), con el teléfono al costado |
| `M-FRM-Catalogo-13` | lo mismo en el teléfono |

**«Campos»:** una tabla que se edita ahí mismo, sin diálogo, con Campo, Tipo, Obligatorio, Único, Límite y ⋮ (Subir, Bajar y Quitar). El encabezado no lleva cantidades. «Agregar campo» queda apagado al llegar al tope de 20, con el motivo en su tooltip. Los errores van debajo de su celda, al guardar. «Campo» va fijo a la izquierda y el ⋮ fijo a la derecha, como en la tabla de opciones.

**«En WhatsApp»:** dos renglones, «Título» y «Descripción». El título lleva un solo desplegable; la descripción, uno por lugar, con un ✕ para quitarlo y un «+» que suma otro. Cada desplegable lista lo que todavía no está en ese renglón, más lo que ya tiene elegido, y ninguno aparece apagado. Sin «Por ejemplo», sin la nota «No salen del sistema» y sin los largos en pantalla: el teléfono al costado muestra cómo queda.

**Estados dibujados:** los campos, agregar un campo, elegir el tipo, las acciones de un campo, quitar un campo, cambiar el tipo con errores, 20 campos, «En WhatsApp», elegir qué muestra, un catálogo nuevo y guardado. La columna de un campo nuevo queda grisada hasta guardar, con el motivo en su tooltip (43).

**Estados que faltan dibujar.** Se le piden a Diseño antes de programar, sin frenar lo demás:
- un valor de una opción dada de baja: se ve con su texto y se puede guardar;
- solo `formcatalogs.read`: la tabla de lectura, sin edición;
- un título que no entra: se marca en esa opción y en el teléfono, y se puede guardar.

## Contrato

Son las rutas de la E12, que suman lo siguiente. Permisos: `formcatalogs.read` para leer y `formcatalogs.manage` para cambiar. Sin rutas nuevas. El contrato va en `FormsContractContributor.cs`, con ejemplos exportados de una corrida real.

| Ruta | Qué suma |
| --- | --- |
| `GET /api/organization/forms/catalogs/{id}` | `fields[]`: `{id, name, type, sourceCatalogId?, sourceCatalogName?, required, unique, maxLength?, min?, max?, isActive}`, en orden, sin la clave; y `presentation`: `{title, description[]}`, donde un lugar es `text`, `code`, `value`, `detail` o `field:<id>` |
| `GET …/catalogs/{id}/items` | cada opción suma `fields`, un mapa de `id` del campo a valor canónico, y `presentationIssue?` (`TitleEmpty`, `TitleTooLong` o `DescriptionTooLong`). Suma el filtro `ids`, repetido, hasta 100 y sin vacíos, para nombrar las opciones de una fuente; si no, 400 |
| `POST …/catalogs` | `fields?[]` y `presentation?` en uno a mano |
| `PUT …/catalogs/{id}` | `fields?[]`: la lista entera, si cambió, con `id` o `localId`; el orden es el de la lista. `presentation?`. `items[i].fields`: delta por `id` de campo. `confirmedFieldRemovals?[]`: los campos con valores que se quitan. Responde también el mapa de ids de los campos |
| `POST …/imports/previews` y `POST …/imports` | las columnas de los campos; los errores por fila y columna siguen la misma forma |
| `GET …/export` | una columna por campo activo, con el valor canónico |
| `GET …/template` | suma `catalogId?`: con él, trae las columnas de sus campos |

**Valores en el JSON:** un número va como texto, para no perder precisión; la fecha, `yyyy-MM-dd`; sí/no, booleano; una fuente, la identidad de la opción. `""` no es `null`: se valida como texto vacío.

**Errores** (en `Domain/Forms/FormCatalogErrors.cs`, con sus textos en `FormsTexts.resx` y `.en.resx`):

| Código | Tipo | Cuándo |
| --- | --- | --- |
| `Forms.Catalog.TooManyFields` | Validation (`fields`) | más de 20 activos |
| `Forms.Catalog.FieldKeyDuplicate` | Validation (`fields[i].name`) | dos campos activos con el mismo nombre |
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
| `FormCatalogs` | `Presentation` | jsonb | `{title, description[]}`; arranca en `text` y `detail` |
| `FormCatalogItems` | `FieldValuesJson` | jsonb, `{}` por defecto | mapa de `key` a valor canónico, con índice GIN `jsonb_path_ops` |

**La clave:** se arma con el nombre, en minúsculas y sin acentos, con `^[a-z][a-z0-9_]{0,39}$`, y queda fija. Si choca con `text`, `code`, `value`, `detail` o con un campo quitado, suma un número. En el JSON del contrato se usa la identidad del campo, y la clave queda adentro.

## Pasos

Abreviaturas: `D/`, `A/`, `I/` y `P/` son las capas del back; `F/`, el front; `M/`, la maqueta, y `[E12]`, un archivo que deja esa etapa.

**Back:**

| # | Qué hace | Archivos | Copia de | Test |
| --- | --- | --- | --- | --- |
| B1 | Campo, tipos, clave y reglas en el dominio; `ChangeFields`, `ChangePresentation` y `SetFieldValues` | `D/Forms/FormCatalogField.cs`, `FormCatalogFieldType.cs`, `FormCatalogPresentation.cs`; suma a `FormCatalog.cs`, `FormCatalogItem.cs` y `FormCatalogErrors.cs` | `D/Forms/FormCatalog.cs` [E12] | `Domain.UnitTests/Forms/`: clave fija, reservadas y repetidas; 20 contando los quitados; reglas por tipo |
| B2 | Columnas, índice y migración | `I/Modules/Forms/Persistence/Configurations/FormCatalogConfiguration.cs`, `FormCatalogItemConfiguration.cs`, `I/Persistence/Migrations/<fecha>_FormCatalogFields.cs` | `AssistantMessageConfiguration.cs` (jsonb) y `AssistantDurableOperationConfiguration.cs` (GIN) | aislamiento y migración, también hacia atrás |
| B3 | Leer y validar el valor de un campo, con la cultura del pedido | `A/Modules/Forms/Services/FormCatalogFieldValuePolicy.cs` | `FormCatalogFiltersPolicy.cs` [E12] | «1.500», «12.345», `""`, `null` y una fuente dada de baja, en es y en |
| B4 | Guardar campos, presentación y delta de valores, bajo el bloqueo del catálogo y de sus fuentes, siempre en el mismo orden | suma a `FormCatalogService.cs` [E12]; `FormCatalogFieldEditor.cs` | `FormCatalogItemEditor.cs` [E12] | delta; guardar sin cambios; una opción entre 10.000 escribe y audita solo esa; 409; dos personas a la vez sobre el catálogo y su fuente |
| B5 | Cambiar tipo o reglas: valida todas las opciones por lotes, y un error revierte todo | `A/Modules/Forms/Services/FormCatalogFieldChangePolicy.cs` | `FormCatalogImportPolicy.cs` [E12] | conversiones, `invalidRows` con las primeras 50, obligatorio y único sobre activas |
| B6 | Quitar un campo y proteger una fuente | suma a `FormCatalogFieldEditor.cs` y `FormCatalogService.cs` | `FormCatalogPositionGuard.cs` [E12] | confirmación con valores; `SourceInUse`; la lección 1 |
| B7 | Pegar, importar, exportar y la plantilla, con las columnas de los campos | suma a `FormCatalogFileResolver.cs`, `FormCatalogImportPolicy.cs` y `FormCatalogExportBuilder.cs` [E12] | esos mismos | exportar y reimportar sin diferencias; encabezado por nombre o clave; columna ausente conserva y vacía limpia; una fuente por identidad, código o texto único |
| B8 | Armar título y descripción de cada opción, y `presentationIssue` | `A/Modules/Forms/Services/FormCatalogPresentationBuilder.cs` | `FormCatalogExportBuilder.cs` [E12] | 30 y 300 caracteres; contenido vacío; un campo quitado sale de la presentación |
| B9 | Rutas, contratos y ejemplos | suma a `P/Modules/Forms/Controllers/BusinessFormCatalogsController.cs`, `Contracts/*HttpRequest.cs` y `FormsContractContributor.cs` [E12]; textos en `FormsTexts{,.en}.resx` | esos mismos | cada ruta con 401, 403, feature apagada = 404, validación, 409 y paginado; un código por cada 409; ejemplos exportados |
| B10 | Medir el volumen | prueba de volumen del módulo | la de la E12 | 10.000 opciones por 20 campos: guardar, importar y exportar con EXPLAIN |

**Front:**

| # | Qué hace | Archivos | Copia de | Test |
| --- | --- | --- | --- | --- |
| F1 | Tipos y llamadas, validados con los ejemplos exportados | suma a `F/src/modules/forms/api/` | la API de catálogos [E12] | zod estricto con los ejemplos |
| F2 | La sección «Campos», en la forma B | `F/src/modules/forms/components/catalog/FieldsSection.tsx` | `M/…/catalog/FieldsSection.tsx` y `fixedColumns.ts` | agregar, subir, bajar y quitar; tope de 20; errores por celda; axe; botones según la 43 |
| F3 | Una columna por campo en la tabla de opciones, con el control de su tipo | suma a `OptionsSection.tsx` [E12] | `M/…/catalog/OptionsSection.tsx` | columna grisada hasta guardar; valor dado de baja; la lección 4 |
| F4 | «En WhatsApp», en la forma C, con el teléfono al costado | `F/src/modules/forms/components/PresentationEditor.tsx`, `CatalogPreviewPhone.tsx` | `M/…/components/PresentationEditor.tsx` | cada desplegable lista lo que corresponde; se arma como el servidor; un título que no entra |
| F5 | El editor: conflicto, guardado y lectura | suma a `CatalogEditorPage.tsx` [E12] | `M/…/pages/CatalogEditorPage.tsx` | la lección 2; solo `formcatalogs.read`; textos en es y en |

**Al cerrar:** `docs/features/formularios.md` suma la sección de los campos.

## Qué verificar al arrancar

| Marca | Qué hay que confirmar | Qué asume esta spec |
| --- | --- | --- |
| [E12] | Cómo quedó el `PUT` del catálogo y su delta de opciones. | `items` lleva solo lo cambiado, y el servidor escribe y audita solo eso. |
| [E12] | Los nombres reales de los helpers y de las páginas del front. | Los de la columna «Archivos», tomados de la spec de la E12. |
| [E12] | Los lectores de archivos y el puerto `IFormCatalogSpreadsheet`. | Leen columnas por nombre y con la cultura del pedido. |
| [E12] | La barrera de C11 y sus tiempos. | Cubre también los archivos con columnas de campos. |
| [E12] | El bloqueo del catálogo. | Se pueden bloquear dos catálogos en un orden fijo, sin esperar un deadlock. |
| Base de la 12 | Los botones y la tabla con columnas fijas del front. | `fixedColumns` ya está en el front, o entra acá copiado de la maqueta. |

## Lo que ya se pagó allá (no repetir)

Cada punto lleva su test, escrito durante la etapa:
1. **Dar de baja una opción de la fuente no bloquea los catálogos que la usan.** Un catálogo con un campo que apunta a una opción dada de baja se guarda, se importa y se exporta sin error, y conserva ese valor.
2. **Un 409 de una regla no es un conflicto de versión.** Solo `General.ConcurrencyConflict` pone el editor en conflicto; dar de baja un catálogo que es fuente de campos activos es un error que se corrige. Hay test en el back (el código de cada 409) y en el front (el editor queda editable).
3. **Un texto vacío no rompe la lectura.** Se prueba con un nombre o un valor de texto `""`, con `null` y con una referencia a una opción dada de baja, guardando y releyendo.
4. **El front no tapa al servidor al cambiar el tipo.** Cambiar el tipo de un campo con valores que no convierten muestra los errores del servidor por fila, y el front no bloquea antes de mandar.

## Decisiones tomadas (no se vuelven a preguntar)

- **Hasta 20 campos activos por catálogo,** igual para todas las organizaciones (42 y 105).
- **Títulos de más de 30 caracteres** (36): en un catálogo a mano, el título que arma la presentación se avisa al guardar y se marca en la vista previa.
- **El contenido no tiene idioma** (35): un solo nombre por campo.
- **Ningún nombre de campo va en el código:** cada organización arma los suyos.
- **Una columna de un campo nuevo** queda grisada hasta guardar el catálogo (43).
- **Un campo que usa la presentación** del catálogo se puede quitar: sale del título y de la descripción.

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
