# E13b · Campos extra y qué viaja a WhatsApp (resumen)

**Estado:** resumen de la cuarta etapa del bloque (decisión 34). Es la primera mitad de lo que ArquitecturaBase llama E2, programado allá con un review de 39 puntos. Se planifica con `/plan-etapa` cuando le toque, después de la E13.

**Dónde está el detalle:** en el traspaso 2 de ArquitecturaBase, el anexo C §11 (C2-01 a C2-04, C2-07 y C2-08), el anexo D (CE1, CE2 y CE3-B, con el «Ajuste del review de E2») y el anexo E §2.7 (review R1023).

## Qué se construye

- **Back:**
  - **Campos por opción,** solo en los catálogos a mano:
    - hasta 20 por catálogo, contando los inactivos (42), en `FieldsJson` del catálogo, con su versión y el 409;
    - cada campo tiene identidad estable, clave en minúsculas (fija después de guardar), nombre (un solo texto, 35), tipo, reglas, papel, posición y estado;
    - **tipos:** texto, entero, decimal, fecha, sí/no, teléfono y «elegir de otro catálogo», que toma los valores de otro catálogo a mano activo de la misma organización. El teléfono se guarda y se valida según la ficha de [teléfonos](../../rules/telefonos.md), con el país de la organización; lo usa la acción «Mandar el PDF a un cliente» de la E16 (decisión 47);
    - **reglas:** obligatorio, único, mínimo, máximo y, en un texto, el largo. Obligatorio y único se comprueban sobre las opciones activas;
    - **papel:** «Dato» (viaja con la respuesta y se ve en la planilla, el Excel y el PDF) o «Filtro» (lo usan los filtros de la E13c).
  - **El ciclo de un campo:**
    - nace opcional, y se cargan valores después de guardarlo;
    - quitar es inactivar, conservando el nombre. Con valores, pide confirmación. Si lo usa el borrador o la versión activa de un formulario, no se quita, y el error dice qué formulario y qué pregunta;
    - cambiar el tipo o las reglas valida todas las opciones bajo el bloqueo del catálogo, y un error revierte todo;
    - al convertir a número o a fecha se lee con la cultura del pedido, y lo ambiguo («1.500», «12.345») no convierte.
  - **Valores:**
    - en `FieldValuesJson` de cada opción, jsonb con `{}` por defecto e índice GIN `jsonb_path_ops`, en el esquema `tenant` con RLS. El valor se guarda canónico;
    - en el `PUT` del catálogo, `items[i].fields` es un delta: ausente conserva y `null` limpia. La importación manda todos los campos activos de la fila;
    - un valor de «elegir de otro catálogo» guarda la identidad de la opción fuente. Un valor nuevo exige una opción activa, y **el guardado que apunta a una opción dada de baja se conserva y no impide guardar**;
    - un catálogo usado como fuente por campos activos no se da de baja.
  - **La tabla, pegar e importar:** una columna por campo después del código. Los encabezados se reconocen por la clave o por el nombre. La plantilla suma las columnas, y en una fuente se acepta la identidad, el código o el texto único de la opción.
  - **Qué viaja a WhatsApp (la presentación):**
    - el catálogo elige qué campos arman el título (hasta 30, con contenido) y la descripción (hasta 300) de cada opción, unidos con « · »;
    - una pregunta puede heredarla o tener la suya, que se publica con su versión;
    - el editor guarda y avisa lo que no entra; la comprobación de publicar y el envío lo frenan;
    - solo viaja lo elegido. El texto de la opción sigue siendo la etiqueta en la planilla y en el chat.
  - **Sellado:** la captura lee los campos de las opciones elegidas, por lote y bajo sus bloqueos, y sella identidad, clave, nombre, tipo, papel y valor. Editar el catálogo después no cambia lo respondido. La tarjeta del chat no cambia, y el asistente no recibe estos campos.
  - **Volumen:** 10.000 opciones por 20 campos, medido con EXPLAIN al guardar, ofrecer y capturar. Guardar manda y escribe solo lo que cambió (C6 de la E13).
- **Front:**
  - la sección «Campos» del editor de un catálogo a mano;
  - una columna por campo en la tabla, con el control de su tipo;
  - la sección «En WhatsApp» del catálogo y la propia de una pregunta, con la vista previa.
- **Tableros que dibuja Diseño** (evaluación, §5.2), corregidos con la decisión 43 y con el «Ajuste del review de E2» de allá:
  - `CE1` · Definir los campos de un catálogo;
  - `CE2` · Cargar los valores;
  - `CE3-B` · Qué viaja a WhatsApp: el catálogo, y la pregunta puede cambiarlo.
- **Mandan sobre este resumen en:**
  - **una columna de un campo nuevo** queda grisada hasta guardar el catálogo, con el motivo en su tooltip (43);
  - **un campo que usa la presentación** del catálogo se puede quitar: sale del título y de la descripción del borrador.

## Las cuatro lecciones de gravedad alta del review de allá

Cada una lleva su test, escrito durante la etapa:
1. **Dar de baja una opción de la fuente no bloquea los catálogos que la usan.** Test: un catálogo con un campo que apunta a una opción dada de baja se guarda, se importa y se exporta sin error, y conserva ese valor.
2. **Un 409 de una regla no es un conflicto de versión.** Solo `General.ConcurrencyConflict` pone el editor en conflicto; quitar un campo en uso es un error que se corrige. Test en el back (el código de cada 409) y en el front (el editor queda editable).
3. **Un texto vacío no rompe la lectura.** Allá era una traducción `""`; acá se prueba con un nombre o un valor de texto `""`, con `null` y con una referencia a una opción inactiva, guardando y releyendo.
4. **El front no tapa al servidor al cambiar el tipo.** Test: cambiar el tipo de un campo con valores que no convierten muestra los errores del servidor por fila, y el front no bloquea antes de mandar.

## Decisiones tomadas (no se vuelven a preguntar)

- **Hasta 20 campos por catálogo,** igual para todas las organizaciones (42).
- **Nombres de más de 30 caracteres** (36): un nombre de miembro se corta con «…»; en un catálogo a mano, el título que arma la presentación se valida al guardar y frena al publicar y al mandar.
- **El contenido no tiene idioma** (35): un solo nombre por campo.
- **Ningún nombre de campo va en el código:** cada organización arma los suyos.

## Fuera de esta etapa

- Los filtros por destinatario y los datos de las personas (E13c).
- Operadores distintos de «es igual a», y varios valores de un mismo campo.
- Completar otras preguntas con los datos de la opción.
- La búsqueda en vivo, el asistente que arma catálogos y los orígenes externos: después del bloque.

## Cómo se cierra

La puerta de la etapa, con la lista de control de `/puerta`, más una **prueba real con Meta** con el número de una organización de prueba:
1. un catálogo «Proveedores» con CUIT, Zona (elegir del catálogo «Zonas») y Plazo de pago, cargados importando una planilla;
2. la presentación con el CUIT en la descripción, y una pregunta con la suya propia. En el celular se ven el título y la descripción elegidos;
3. responder, y ver los campos «Dato» sellados en la respuesta. Cambiar el CUIT en el catálogo no cambia lo respondido.

## Requisitos previos

La E13 cerrada.

## Ajustes ya decididos

- **Botones:** según la decisión 43.
- **Datos:** el esquema `tenant`, con RLS, y las fuentes dentro de la misma organización.

## En el multitenant

- **Datos de la organización:** campos y valores van en las tablas de catálogos, `ITenantOwned` con RLS.
- **Workers:** no hay.
- **Feature `forms`:** las rutas llevan `[FeatureGate("forms")]`.
- **Permisos de organización:** los de catálogos de la E13 (`formcatalogs.read` y `formcatalogs.manage`), sin permisos nuevos.
