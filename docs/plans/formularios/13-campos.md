# E13 · Campos de los catálogos (resumen)

**Estado:** resumen de la etapa 13 del bloque (decisiones 34 y 78). Es la parte de catálogo de lo que ArquitecturaBase llama E2, programado allá con un review de 39 puntos: los campos, sus valores, las columnas al importar y qué se ve en WhatsApp, con la vista previa del catálogo. Se planifica con `/plan-etapa` cuando le toque, después de la E12.

**Dónde está el detalle:** en el traspaso 2 de ArquitecturaBase, el anexo C §11 (C2-01 a C2-04 y C2-08), el anexo D (CE1, CE2 y CE3-B, con el «Ajuste del review de E2») y el anexo E §2.7 (review R1023).

## Qué se construye

- **Back:**
  - **Campos por opción,** solo en los catálogos a mano:
    - hasta 20 por catálogo, contando los quitados (42), en `FieldsJson` del catálogo, con su versión y el 409;
    - cada campo tiene identidad estable, nombre (un solo texto, 35), tipo, reglas, posición y estado. Su clave se arma sola con el nombre, en minúsculas, queda fija después de guardar y no se muestra;
    - **tipos:** Texto, Número entero, Número con decimales, Fecha, Sí/No y «De un catálogo: …», que toma los valores de otro catálogo a mano activo de la misma organización;
    - **reglas:** obligatorio, único y un límite: el largo en un texto, y el mínimo y el máximo en un número o una fecha. Obligatorio y único se comprueban sobre las opciones activas.
  - **El ciclo de un campo:**
    - nace opcional, y se cargan valores después de guardarlo;
    - quitar un campo lo saca de la tabla y conserva sus valores en las respuestas que ya lo tienen. No se reactiva desde la pantalla. Con valores, pide confirmación. Un campo en uso no se quita;
    - cambiar el tipo o las reglas valida todas las opciones bajo el bloqueo del catálogo, y un error revierte todo;
    - al convertir a número o a fecha se lee con la cultura del pedido, y lo ambiguo («1.500», «12.345») no convierte.
  - **Valores:**
    - en `FieldValuesJson` de cada opción, jsonb con `{}` por defecto e índice GIN `jsonb_path_ops`, en el esquema `tenant` con RLS. El valor se guarda canónico, con la precisión de los números de la E12 (C6);
    - en el `PUT` del catálogo, `items[i].fields` es un delta: ausente conserva y `null` limpia. La importación manda todos los campos activos de la fila;
    - un valor de «elegir de otro catálogo» guarda la identidad de la opción fuente. Un valor nuevo exige una opción activa, y **el guardado que apunta a una opción dada de baja se conserva y no impide guardar**;
    - un catálogo usado como fuente por campos activos no se da de baja.
  - **La tabla, pegar, importar y exportar:** una columna por campo después del código. Los encabezados se reconocen por el nombre o por la clave. La plantilla y la exportación suman las columnas, y en una fuente se acepta la identidad, el código o el texto único de la opción.
  - **Qué se ve en WhatsApp (la presentación del catálogo):**
    - el catálogo elige qué arma el título (hasta 30, con contenido) y la descripción (hasta 300) de cada opción. El título sale de un solo lugar, que arranca en «Texto»; la descripción, de uno o más lugares, unidos con « · »;
    - cada lugar es el texto, el código, el valor (si el catálogo es tipado), el detalle o un campo. Todos pueden ir a WhatsApp;
    - se guarda en el catálogo, con su versión: `presentation: {title: <lugar>, description: [<lugar>…]}`, donde un lugar es `text`, `code`, `value`, `detail` o `field:<id>`;
    - el editor guarda y avisa, opción por opción, lo que no entra;
    - el texto de la opción sigue siendo su etiqueta.
  - **Volumen:** 10.000 opciones por 20 campos, medido con EXPLAIN al guardar, importar y exportar. Guardar manda y escribe solo lo que cambió (C3 de la E12).
- **Front:**
  - la sección «Campos» del editor de un catálogo a mano, en la forma B: una tabla que se edita ahí mismo, sin diálogo, con las columnas Campo, Tipo, Obligatorio, Único, Límite y ⋮ (Subir, Bajar y Quitar). Quitar queda apagado si el campo está en uso, con el motivo en su tooltip. El encabezado de la sección no lleva cantidades (ni «N de 20» ni «N campos»), y «Agregar campo» queda apagado al llegar al tope de 20, con el motivo en su tooltip. Los errores van debajo de su celda, al guardar. «Campo» va fijo a la izquierda y el ⋮ fijo a la derecha, como en la tabla de opciones de la E12; se copia de la maqueta: `FieldsSection.tsx` y `fixedColumns.ts` (a1303a5 y af5eceb);
  - una columna por campo en la tabla, con el control de su tipo;
  - **la sección «En WhatsApp» del catálogo, en la forma C** (la eligió el usuario el 2026-10-26):
    - dos renglones, «Título» y «Descripción»;
    - el título lleva un solo desplegable, que arranca en «Texto»;
    - la descripción lleva un desplegable por lugar, con un ✕ para quitarlo y un «+» que suma otro;
    - cada desplegable lista Texto, Código, el valor si el catálogo lo tiene, Detalle y los campos: lo que todavía no está en ese mismo renglón, más lo que ya tiene elegido. Ninguno aparece apagado;
    - sin «Por ejemplo», sin la nota «No salen del sistema» y sin los largos en pantalla: el teléfono al costado muestra cómo queda.
- **Tableros que dibuja Diseño** (evaluación, §5.2), corregidos con la decisión 43 y con el «Ajuste del review de E2» de allá, mostrando solo el catálogo:
  - `CE1` · Definir los campos de un catálogo, en la forma B que aprobó el usuario el 2026-10-25 en la maqueta;
  - `CE2` · Cargar los valores;
  - `CE3-B` · Qué se ve en WhatsApp, en el catálogo: `FRM-Catalogo-13`, en la forma C, en el lienzo v145.
- **Mandan sobre este resumen en:**
  - **una columna de un campo nuevo** queda grisada hasta guardar el catálogo, con el motivo en su tooltip (43);
  - **un campo que usa la presentación** del catálogo se puede quitar: sale del título y de la descripción.

## Las cuatro lecciones de gravedad alta del review de allá

Cada una lleva su test, escrito durante la etapa:
1. **Dar de baja una opción de la fuente no bloquea los catálogos que la usan.** Test: un catálogo con un campo que apunta a una opción dada de baja se guarda, se importa y se exporta sin error, y conserva ese valor.
2. **Un 409 de una regla no es un conflicto de versión.** Solo `General.ConcurrencyConflict` pone el editor en conflicto; dar de baja un catálogo que es fuente de campos activos es un error que se corrige. Test en el back (el código de cada 409) y en el front (el editor queda editable).
3. **Un texto vacío no rompe la lectura.** Allá era una traducción `""`; acá se prueba con un nombre o un valor de texto `""`, con `null` y con una referencia a una opción dada de baja, guardando y releyendo.
4. **El front no tapa al servidor al cambiar el tipo.** Test: cambiar el tipo de un campo con valores que no convierten muestra los errores del servidor por fila, y el front no bloquea antes de mandar.

## Decisiones tomadas (no se vuelven a preguntar)

- **Hasta 20 campos por catálogo,** igual para todas las organizaciones (42).
- **Títulos de más de 30 caracteres** (36): en un catálogo a mano, el título que arma la presentación se valida al guardar y se marca en la vista previa.
- **El contenido no tiene idioma** (35): un solo nombre por campo.
- **Ningún nombre de campo va en el código:** cada organización arma los suyos.

## Fuera de esta etapa

- Usar los campos en un formulario: la presentación propia de una pregunta, el sellado al responder y no poder quitar un campo que usa un formulario (E14).
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

## Ajustes ya decididos

- **Botones:** según la decisión 43.
- **Datos:** el esquema `tenant`, con RLS, y las fuentes dentro de la misma organización.

## En el multitenant

- **Datos de la organización:** campos y valores van en las tablas de catálogos, `ITenantOwned` con RLS.
- **Workers:** no hay.
- **Feature `forms`:** las rutas llevan `[FeatureGate("forms")]`.
- **Permisos de organización:** los de catálogos de la E12 (`formcatalogs.read` y `formcatalogs.manage`), sin permisos nuevos.
