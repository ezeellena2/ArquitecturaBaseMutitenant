# E19 · Condiciones, cálculos y líneas (spec)

**Estado:** pendiente · spec de la etapa 19 del bloque (decisiones 53, 54, 78, 146, 168, 177, 178, 204, 206, 227, 232, 233 y 235), con el [formato](plantilla.md) de las etapas. De ArquitecturaBase se toman el cálculo en el bloque y su redondeo, no sus operaciones fijas (232 y 233). El bloque es lo que allá llaman «Renglones en los formularios»: está programado entero y se probó de punta a punta con Meta el 2026-11-09. **Antes de programar** se prueba con Meta, en un celular, una pregunta que aparece según otra, una pantalla por ítem con «¿Agregás otro ítem?», el bloque al final con su pantalla de cierre y una pregunta de archivo dentro de un `If` (K2 del plan); la respuesta grande, con el número de una organización (I3, 123).

**Partes.** Esta spec no entra en un solo archivo, así que sigue en otro, con las mismas secciones. Lo que no está en la tabla está acá.

| Secciones | Archivo |
| --- | --- |
| Contrato, Datos, Pasos, los textos del bloque y «Lo que puede salir mal» | [contrato, datos y pasos](19-parte-contrato-datos-y-pasos.md) |

## Qué leer

Antes de empezar, en este orden:

1. **Lo anterior:** la [spec de la E14](14-formularios.md): T9, T11, T13, T24, la §3, la §6, punto 7, y la §14; la [E15](15-respuestas.md): filtros, valores, Excel y tablero; la [E16](16-datos-de-las-personas.md): los filtros por destinatario y sus avisos; la [E12](12-catalogos.md): C5, y la [E13](13-campos.md): los tipos de campo.
2. **Decisiones** de la hoja de ruta: 35, 43, 53, 54, 78, 146, 157, 162, 168, 174, 177, 178, 204, 206, 226, 227, 232, 233 y 235.
3. **Fichas:**

   | Ficha | Por qué acá |
   | --- | --- |
   | [numeros-y-moneda](../../rules/numeros-y-moneda.md) | `decimal`, `Money`, los decimales de cada moneda y la excepción de los cálculos, que redondean al par (232) |
   | [datos-de-referencia](../../rules/datos-de-referencia.md) | la moneda de un cálculo, nunca una lista en el código |
   | [validacion](../../rules/validacion.md) y [result-y-errores](../../rules/result-y-errores.md) | los errores de cada pregunta |
   | [persistencia-ef](../../rules/persistencia-ef.md) y [multitenancy](../../rules/multitenancy.md) | la tabla nueva y las columnas |
   | [api-http](../../rules/api-http.md) | las rutas que cambian y sus ejemplos |
   | [logs](../../rules/logs.md) | ni fórmulas con datos ni valores en los logs |
   | [textos-y-traducciones](../../rules/textos-y-traducciones.md) | «No se mostró», «ítem» y las palabras del editor, en es y en |
   | [tests](../../rules/tests.md) | qué lleva test y dónde va |

4. **Código para copiar:** la columna «Copia de» de los [pasos](19-parte-contrato-datos-y-pasos.md#pasos).
5. **Referencia de producto,** solo para leer: en ArquitecturaBase, el traspaso 12 (`docs/plans/2026-11-11-traspaso-multitenant-renglones.md`) y, en `docs/features/formularios.md`, que manda sobre él, las tres secciones «El bloque de renglones…» y «Cálculos»; en `ArquitecturaBaseFront`, `docs/design/renglones-en-formularios.md`. Allá los planes de renglones se retiraron. Mandan esta spec y las reglas y los diseños de este proyecto (108). La referencia de los Flows es la §14 de la E14.

## Qué se construye

**Preguntas condicionales:**
- una pregunta puede tener «Mostrar si», con hasta 10 condiciones unidas con «y», sobre preguntas anteriores: sí/no y elegir una (`in`), elegir varias (`anyOf`), número y fecha (`equals`, `gte`, `lte`), y respondida o no (`answered`, `unanswered`). Es la gramática de la E12 (C5), con la misma regla de la E15;
- en WhatsApp, la pregunta aparece o se oculta mientras la persona completa el formulario, con el componente `If` **[Meta]**, armado como dice la [parte](19-parte-contrato-datos-y-pasos.md#contrato): Meta no acepta «y» ni «o» dentro de un `If` y anida hasta tres. Un título de sección se oculta con sus preguntas si todas tienen la misma condición; si no, se ve siempre;
- **lo que el canal no pueda mostrar no se publica:** la comprobación lo marca en su pregunta, y nunca se manda sin la condición. Entre eso están un número, elegir varias, varias respuestas de una pregunta de catálogo y más de tres condiciones en cadena. **«Mostrar si» no se achica por eso** (235): en un formulario que va por WhatsApp, el editor lo avisa debajo de la condición, con su motivo («WhatsApp no muestra una condición con números.»), y publicar frena igual;
- **el servidor vuelve a evaluar al capturar y nunca confía en lo que llega.** Evalúa en orden, y una pregunta oculta cuenta como sin respuesta para las que dependen de ella: una condición sobre ella se cumple solo con `unanswered`, y con los demás operadores las que dependen se ocultan en cadena. Una pregunta oculta no guarda valor, y «obligatoria» vale solo si se mostró;
- **«No se mostró»:** la respuesta guarda qué preguntas quedaron ocultas. En la planilla se ve como «No se mostró», distinto de «Sin respuesta» y de «—». Las condiciones suman `notShown`, y `unanswered` también lo incluye; la planilla no lo filtra (143);
- una pregunta condicional de catálogo con un problema de oferta frena el envío igual: no se sabe de antemano si se va a ver.

**Cálculos:**
- un tipo de pregunta «Cálculo», que no se ve en WhatsApp ni cuenta en sus topes;
- su fórmula usa un número fijo, un número o un cálculo anterior de la misma respuesta, un campo numérico de la opción elegida en un catálogo y la suma de una pregunta del bloque, que puede ser un cálculo del ítem;
- **un cálculo puede ir dentro del bloque** (233): usa solo los datos de su ítem, también el campo del catálogo elegido en ese ítem, y se calcula en cada uno («Importe = Cantidad × Precio»). Afuera, un cálculo suma una pregunta del bloque, que puede ser ese importe («Total = suma de Importe»);
- **solo las cuatro operaciones** y paréntesis, armadas en el editor sin escribir código;
- se calcula **al llegar la respuesta,** con los valores sellados, en `decimal`, y **se redondea al par** (`MidpointRounding.ToEven`, 232) a sus decimales: es la excepción escrita en la ficha. Si es un monto, el formulario fija su moneda, de los datos de referencia, con los decimales de esa moneda;
- **una división por cero, un dato que falta, también el de una pregunta que no se mostró, o un número demasiado grande dejan el cálculo «Sin respuesta»,** nunca un error de la captura. En una suma del bloque, un ítem sin ese dato no cuenta. Cada cálculo se redondea por su lado, y el que suma otros suma lo ya redondeado: un importe de 2,205 queda en 2,20, y el total suma 2,20;
- el resultado se guarda: editar el catálogo o el formulario después no lo cambia;
- se ve en la tarjeta del chat, y en la planilla, el detalle, el Excel y el tablero, como un número que se filtra y se ordena. Un monto se ve con su moneda, y el tablero lo totaliza en ella.

**El bloque** (146, 177 y 178, que cambian la 54). Los textos, con su inglés, están en la [parte](19-parte-contrato-datos-y-pasos.md#los-textos-del-bloque):
- **«Agregar bloque»** va en la banda «Preguntas», al lado de «Agregar pregunta». Suma el bloque al final de la lista, abierto y con «Bloque» de nombre, con su ayuda, el mínimo y el máximo de ítems y su propio «Agregar pregunta». El mínimo va de 0 al máximo y el máximo llega hasta 100; uno nuevo arranca con 1 y 20, y con mínimo 0 es opcional. Con un bloque armado, el botón se apaga con «Ya hay un bloque.» en su tooltip, y «Tipo de respuesta» no lo ofrece;
- **en la lista,** el bloque es una pregunta más, con ↑ ↓ ✎ 🗑, que dice «Bloque · de 1 a 10 ítems · 3 preguntas», y debajo va el recuadro «Preguntas de cada ítem», con las suyas numeradas 5.1, 5.2… Quitarlo pide confirmar, porque se lleva sus preguntas: «¿Quitar el bloque?» (162);
- **«Se carga varias veces»,** una casilla debajo de «Obligatoria», con su (i), junta las preguntas marcadas en el bloque; si no hay uno, lo crea en el lugar de la primera, con «Bloque» de nombre. Desmarcar una la saca, y queda justo después del bloque. Una pregunta que ya existía pasa como pregunta nueva, con otra identidad: lo cargado antes conserva su columna, y publicar lo dice en «Pasan al bloque». En Foto, Documento y Elegir varias, que no entran, la casilla no aparece. En una pregunta con «Mostrar si» o con «Permitir elegir varias», sigue activa, y marcarla pregunta antes «¿Pasar «Monto» al bloque?», con «Se quitan sus condiciones.» o «Pasa a elegir una sola opción.», «Cancelar» y «Pasar al bloque» (162 y 195);
- **qué entra:** un bloque por formulario, sin bloques adentro, con texto corto y largo, número, fecha, sí o no, elegir una, propia o de un catálogo, y cálculo, con los datos de su ítem (233). En una pregunta del bloque, «Elegir varias», «Foto» y «Documento» aparecen apagadas en «Tipo de respuesta», y «Permitir elegir varias», en su casilla, cada una con su motivo en su tooltip (43). Sus preguntas cuentan para el tope del formulario, y el bloque mismo no; un bloque sin preguntas se guarda pero no se publica;
- **un catálogo del bloque filtra por quien lo recibe** (168), como una pregunta suelta de la E16: lleva sus filtros fijos y por destinatario, los avisos al publicar (157) y su oferta, una sola para todos los ítems y con el mismo tope;
- **en WhatsApp, una pantalla por ítem,** hasta 10 o el máximo si es menor, con el número en el título («Productos · ítem 2»). Con mínimo 0, el bloque abre con una pantalla con su nombre y «¿Agregás un ítem?»; con 1 o más, esa pantalla no está. Desde el mínimo, cada ítem termina con «¿Agregás otro ítem?», sí o no; el último no pregunta, y la flecha vuelve al anterior. El «No» y el último ítem llevan a lo de después del bloque. **Si el bloque es lo último, llevan a una pantalla de cierre** (204), con el nombre del formulario, «Tocá «Enviar» para mandar el formulario.» (con el texto de su botón final) y ese botón: Meta no acepta mandar desde la pantalla de un ítem. Es el Flow estático con más pantallas, sin intercambio con Meta **[Meta]**;
- **el `routing_model`:** el Flow con bloque declara cada `navigate` de cada pantalla, con las dos ramas de cada `If`. Allá, sin él, Meta lo dedujo de los `navigate`, dejó afuera la rama «No», y responder «No» cortaba el formulario. Sin bloque, el Flow sale como en la E18;
- **los topes de WhatsApp** (178): un mínimo de más de 10 frena al publicar, con «En WhatsApp se cargan hasta 10 ítems; el mínimo es 15.» e «Ir a la pregunta». Un máximo de más de 10 avisa sin frenar, y la (i) del máximo dice el tope;
- **el tope de la respuesta:** al publicar se suma lo más largo que podría mandar cada pregunta, una vez por ítem de WhatsApp, con su clave `_r<n>`, de hasta 4 bytes más. Un formulario con bloque tiene el tope de uno con archivos (E18): 64 KiB al publicar y al recibir; si igual no entra, publicar frena como hoy. Allá, 10 ítems de 600 caracteres con eñes pesaron 34,6 KB y llegaron enteros el 9/11; acá se prueba con el número de una organización (I3, 123) **[Meta]**;
- **lo que llega:** cada pregunta de un ítem llega como `field_<N>_r<n>`, y «¿Agregás otro ítem?», como `field_<bloque>_r<n>`; con mínimo 0, `_r0` es «¿Agregás un ítem?». De los ítems no visitados no llega ninguna clave, ni vacía ni con `${…}`, y lo que llegue de un ítem después de un «No» se ignora;
- **la captura entiende el bloque desde el primer día.** Valida la cantidad contra el mínimo y el máximo, siempre, y por WhatsApp contra el tope, y cada ítem como preguntas sueltas: obligatorias, tipos, largos y opciones ofrecidas; después calcula los cálculos de cada ítem y los de afuera. Un ítem vacío cuenta como ítem y se guarda: la respuesta lleva la cantidad de ítems, y cada valor, la posición de su ítem. Los catálogos se sellan como en una pregunta suelta. En la tarjeta del chat, una respuesta con ítems que no se pueden leer queda «no se pudo guardar», con su motivo, y «no reconocida» queda solo para lo que no corresponde a un envío;
- **en la tarjeta del chat,** el nombre del bloque y un ítem por línea, numerado, con sus valores separados por « · » en el orden de las preguntas y sin los que quedaron sin respuesta: «1. Caño de 1/2 pulgada · 10»;
- **en la planilla,** una fila por respuesta. La columna del bloque dice cuántos ítems tiene, «3 ítems» o «1 ítem», sin sus valores: cada ítem se ve en el detalle (206, que cambia la 185). No se ordena, y una respuesta de antes del bloque dice «—»;
- **en «Filtros»,** el nombre del bloque encabeza sus preguntas, que van debajo, con sangría, y una pregunta del bloque lleva debajo de su título, en gris, «En algún ítem de Productos»: se cumple si algún ítem la cumple. Varias del mismo bloque buscan un mismo ítem que las cumpla todas, también en los conteos. La búsqueda mira también los textos largos de los ítems, y el total sigue contando respuestas;
- **en el detalle** (139), cada ítem con su número y el valor de su primera pregunta en negro, y los demás datos alineados debajo; sin ítems, «Sin respuesta». La pantalla completa muestra una tabla, con el número sin encabezado; en el teléfono, cada ítem con sus datos alineados, como en el detalle, sin desplazarse de costado;
- **en el Excel** (227), una sola hoja, «Respuestas», con una fila por ítem: los datos de la respuesta se repiten en cada una y, en el lugar del bloque, van «Ítem», con su número, y sus preguntas, con los tipos y la precisión de las demás. Una respuesta sin ítems ocupa una fila con «Ítem» vacío, y una de una versión sin el bloque lleva «—»;
- **el diálogo de exportar** (226) suma «Filas»: «Una por ítem de Productos» o «Una por respuesta», que no trae las columnas del ítem. El chip de un filtro sobre una pregunta del bloque dice «Estado: Dañado (en algún ítem de Productos)»;
- **el tope del Excel:** además de las 10.000 respuestas, 100.000 filas, que se cuentan antes de escribir: «El Excel tendría más de 100.000 filas, una por ítem. Acotá los filtros o sacá el bloque de las columnas.» (por qué, en la [parte](19-parte-contrato-datos-y-pasos.md#contrato)).

**Copiar un formulario** copia las condiciones, los cálculos y el bloque: el borrador se copia con sus identidades, así que no hay nada que traducir.

**Al editar:**
- el borrador se guarda incompleto, como siempre. **Una referencia rota,** a una pregunta quitada, que ahora va después o que cambió de tipo, se guarda y frena al publicar, con «Ir a la pregunta»;
- el selector de cada condición y de cada dato de la fórmula ofrece solo las preguntas anteriores que sirven;
- el front avisa lo que ve, pero no tapa al servidor: la comprobación que vale es la de publicar.

## Pantallas

Aprobados y publicados en el lienzo v153, con el recorrido guiado `un-pedido-con-renglones`. El ejemplo es «Pedido de materiales», con el bloque «Productos». Muestran solo lo que existe hasta acá (78).
- **Siguen aprobados** `FRM-Tablero-19` y su teléfono.
- **Redibujados con el traspaso 10, aprobados y publicados en el lienzo v168,** con sus teléfonos:
  - `FRM-Editor-Condiciones` y `FRM-Editor-Calculo`, sin el «Grupo de líneas» (146 y 177);
  - `FRM-Editor-Lineas`, con «Agregar bloque», los textos con «ítem» (177) y un catálogo del bloque con sus filtros (168);
  - `FRM-Respuestas-19`, `FRM-Respuesta-19` y `WA-Chats-Respuesta-19`, con los ítems, «Filtros» (144) y el detalle alineado (139);
  - `FRM-Publicar-19`, sin teléfono, con «Pasan al bloque» y el mínimo que frena (178).
- **Se redibujan en la v175** la pantalla de cierre (204), el cálculo en el bloque (233), el aviso de «Mostrar si» (235), «3 ítems» (206) y el diálogo de exportar con «Filas» (227), nuevo; la lista está al final del [plan de la etapa](../2026-11-08-etapa-19-condiciones-calculos-y-lineas.md).

| Tablero | Qué muestra |
| --- | --- |
| `FRM-Editor-Condiciones` y `M-FRM-Editor-Condiciones` | «Mostrar si» en una pregunta, con el aviso de lo que WhatsApp no muestra, y el teléfono al costado, con la pregunta que aparece |
| `FRM-Editor-Calculo` y `M-FRM-Editor-Calculo` | la pregunta «Cálculo»: las piezas de la fórmula, la moneda, los decimales y el cálculo de un ítem |
| `FRM-Editor-Lineas` y `M-FRM-Editor-Lineas` | el bloque: «Agregar bloque», «Se carga varias veces», el mínimo y el máximo, un catálogo con sus filtros, quitarlo y, en el teléfono, una pantalla por ítem y la de cierre |
| `FRM-Respuestas-19` y `M-FRM-Respuestas-19` | la planilla, con «No se mostró», el cálculo y «3 ítems», y en «Filtros» las preguntas del bloque |
| `FRM-Respuestas-Exportar-19` | el diálogo de exportar, con «Filas» y el chip de una pregunta del bloque |
| `FRM-Respuesta-19` y `M-FRM-Respuesta-19` | el detalle, con cada ítem y sus datos alineados y los cálculos, y la tabla en la pantalla completa |
| `WA-Chats-Respuesta-19` y `M-WA-Conversacion-Respuesta-19` | la tarjeta, con un ítem por línea y el total |
| `FRM-Tablero-19` y `M-FRM-Tablero-19` | el tablero, con un monto y su moneda |
| `FRM-Publicar-19` | una condición que WhatsApp no puede mostrar, más de 50 componentes, «Pasan al bloque», el bloque sin preguntas, un máximo de más de 10 que avisa y un mínimo de más de 10 que frena |

**Estados que se dibujan:**
- **Editor:** una condición con cada tipo de pregunta; una fórmula incompleta, con el motivo debajo de su pregunta; una referencia rota; el contador de componentes cerca de 50; sin preguntas anteriores que sirvan; el bloque recién agregado, uno con mínimo 0 («hasta 10 ítems») y «Agregar bloque» apagado;
- **Publicar:** problemas por pregunta, con «Ir a la pregunta»;
- **Planilla y detalle:** una respuesta con una pregunta oculta, un cálculo que quedó «Sin respuesta» y una respuesta de antes del bloque.

**«Mostrar si»:** va en la pregunta abierta, en su caja, como las condiciones de las reglas de ArquitecturaBase: Pregunta, Condición, Valor y el tacho, y «Agregar condición». Sin preguntas anteriores que sirvan, «Agregar condición» queda grisado, con el motivo en su tooltip. Una referencia rota queda en rojo, con el motivo debajo, y la pregunta dice «Corregir».

**La fórmula:** piezas en una caja, con «Agregar dato ▾» (un número, una pregunta, un campo del catálogo o la suma de una pregunta del bloque) y + − × ÷ ( ). La suma se elige en un diálogo. En un cálculo del bloque, «Agregar dato ▾» ofrece solo lo de su ítem, sin la suma. «Es un monto» pide la moneda, y los decimales salen de ella.

**El bloque en el editor:** un recuadro en la lista de preguntas, con las suyas adentro, ↑ ↓ ✎ 🗑 en cada una y su propio «Agregar pregunta» en la banda «Preguntas de cada ítem». El ✎ del bloque edita el nombre, la ayuda y, bajo «Ítems», el mínimo y el máximo, y con el bloque abierto el teléfono al costado muestra la pantalla de un ítem. Cerca de 50 componentes en una pantalla, se lee «46 de 50 componentes».

**Mandan sobre esta spec en:** las fichas del front. La fórmula se arma con piezas, sin un campo para escribirla. Un dato por celda, y un monto con su moneda. Todo texto que ve el usuario dice «ítem», también en inglés («item»), donde los tableros de antes decían «renglón».

**Valen las de toda la app** (160 a 164, 173 a 175 y 180): lo que carga no salta, decidir a propósito, ninguna elección se descarta en silencio, mucho texto con jerarquía, sin parecer plantilla y con la escala de ArquitecturaBase, el texto largo entero y el estado dicho una vez.

## Qué verificar al arrancar

| Marca | Qué hay que confirmar | Qué asume esta spec |
| --- | --- | --- |
| [Meta] | `If` y su expresión, antes de programar: comparar un `Dropdown`, una casilla de `CheckboxGroup`, un número escrito en `TextInput` y una fecha, entre pantallas, y dónde se admite. Allá, el 2026-11-11: una sola comparación por `If`, sin `&&` ni `\|\|`, aun con paréntesis; hasta tres `If` anidados, y un control nunca en dos ramas. | Lo que no se pueda, `Check` lo frena al publicar. Los números hoy son texto (§14 de la E14), y puede no haber comparación numérica. |
| [Meta] | Una pregunta de archivo dentro de un `If`, en su pantalla. | Si no se puede saltear, se la frena al publicar con `ConditionNotSupported`. |
| [Meta] | Una pantalla por ítem. Allá Meta aceptó con el `routing_model` el bloque con preguntas después (6 y 7/11). **El bloque al final no pasaba,** aunque allá se escribió que sí: lo rechazó (`INVALID_PROPERTY_VALUE`) y pasó el 9/11 con la pantalla de cierre (traspaso 12, §4). | Se prueba igual acá (K2 del plan), y la respuesta grande con el número de una organización (I3). Alcanzan 10 ítems sin intercambio, con un `Footer` en cada rama del `If` dentro del `Form` y el mismo `name` en las diez pantallas; **una pantalla que manda no navega en otra rama** (204). Si los 40 KB no llegan enteros, un formulario con bloque se queda con 16 KiB. |
| Allá | Renglones, programado entero y probado con Meta el 9/11: el Excel en una hoja, con su tope; «En algún ítem de…», con todas las condiciones en el mismo ítem; el cálculo del ítem, y el PDF con el bloque, que suma la E21. | Se contrasta con el código de allá, y lo que allá cambió se adopta si no choca con las reglas de acá (126). |
| [E14] | El orden de la captura y `FormRecordValues`. | Se pueden sumar los pasos, y el texto sellado existe. |
| [E15] | La función de filtros, la lectura en memoria de qué versiones tienen cada pregunta y `Number`. | `notShown` entra en la misma función. |
| [E16] | Los filtros por destinatario y sus avisos. | Sirven igual para una pregunta del bloque (168). |
| [E18] | El perfil del Flow. | Este suma el siguiente. |
| Datos de referencia | Cómo se valida una moneda y sus decimales. | `ICurrencyCatalog` da los decimales. |

## Decisiones tomadas (no se vuelven a preguntar)

- **Un monto calculado se guarda en `NumberValue`,** con su `Currency`, y se lee como `Money`: es la columna de todos los números, y así se filtra y se ordena con la misma regla. Se redondea a los decimales de la moneda, y un monto de más de 13 dígitos enteros, el tope de un importe, cuenta como demasiado grande.
- **Una etapa propia** (54), después de las respuestas, así lo que suma se ve en lo que ya existe (78).
- **Los cálculos se hacen al llegar la respuesta:** quien completa no ve el total en WhatsApp antes de enviar (54).
- **Del traspaso 15:** los cálculos redondean al par (232); la fórmula también va dentro del bloque (233); «Mostrar si» queda entero, con el aviso en el editor (235), y el Excel va en una hoja, con una fila por ítem (227).
- **El bloque** (146, 177 y 178): se agrega con «Agregar bloque», con un mínimo y un máximo de hasta 100, y en WhatsApp una pantalla por ítem, hasta 10; un mínimo de más de 10 frena al publicar. Cambia el tope de 1 a 10 de la 54.
- **Con el bloque al final, una pantalla de cierre** en WhatsApp (204).
- **«Ítem» en todo texto que ve el usuario** (177): el código y los documentos siguen diciendo `rows`.
- **Un catálogo del bloque filtra por quien lo recibe** (168).
- **La columna del bloque dice cuántos ítems tiene** («3 ítems»), y cada uno se ve en el detalle (206).
- **Genérico:** ninguna fórmula ni bloque va escrito en el código.

## Fuera de esta etapa

- Más de un bloque, bloques dentro de un bloque y condiciones dentro de un bloque.
- Fotos o documentos en un ítem, ordenar por el bloque, condiciones sobre todos los ítems o su cantidad, duplicar un ítem, importar ítems desde un Excel y la lista de lo cargado antes de enviar por WhatsApp.
- Mostrar el total en WhatsApp antes de enviar.
- Funciones más allá de las cuatro operaciones y la suma de una pregunta del bloque.
- Lo que suman las etapas siguientes: la carga del bloque desde la web, con «Agregar ítem»; la (i) del máximo y la de «Se carga varias veces» con lo que hace la app, y un mínimo de más de 10 que deja solo para la app un formulario para empleados o para los dos (E20 y 178); el PDF con el bloque entero y «Algún ítem de…» en las reglas (E21).
- Una lista dependiente dentro del bloque y elegir primero por un campo: quedan para después de la E25 (191).

## Cómo se cierra

La puerta de la etapa, con la lista de control de `/puerta`, más una **prueba real con Meta** con el número de una organización de prueba, abriendo cada variante en un celular:
1. «Monto» aparece solo si «¿Se concretó la venta?» es Sí; con No, la respuesta llega sin monto.
2. Un pedido con 3 ítems (producto del catálogo y cantidad), con el «Importe» de cada ítem, Cantidad × el precio del catálogo, y el «Total» que suma los importes, en la tarjeta del chat.
3. Un pedido con un ítem y «No» en «¿Agregás otro ítem?», y un bloque con mínimo 0 y «No» en «¿Agregás un ítem?»: los dos llegan enteros, sin cortar el formulario.
4. Un formulario con el bloque al final se publica sin `validation_errors`, y con «No» y con el último ítem pasa a la pantalla de cierre y llega entero.
5. Las respuestas en la planilla, el detalle y el Excel: «No se mostró», «3 ítems», el importe de cada ítem, una fila por ítem en el Excel y el total.

## Requisitos previos

- La E18 cerrada.
- La prueba con Meta del componente `If`, de una pantalla por ítem, del bloque al final con su pantalla de cierre y de una pregunta de archivo dentro de un `If` (K2); la de una respuesta de unos 40 KB queda para la I3 (123).

## En el multitenant

- **Datos de la organización:** `FormRecordRowValues` y las columnas nuevas son `ITenantOwned`, con RLS.
- **Workers:** no hay; el cálculo corre en la captura.
- **Feature `forms`:** las rutas llevan `[FeatureGate("forms")]`.
- **Permisos:** los de la E14, sin permisos nuevos.
- **Cuotas:** las rutas cuentan en las cuotas generales.
