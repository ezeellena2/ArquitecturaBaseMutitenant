# E12 · Catálogos base (spec)

**Estado:** pendiente · spec de la primera etapa del bloque (decisiones 34, 78, 117, 119, 120 y 129). Es el ABM de catálogos completo, sin formularios: lo que ArquitecturaBase llama E1 (tipo de valor, carga de una vez y hasta 10.000 opciones), sobre los catálogos a mano, de los miembros y de los contactos. Acá nace el módulo `Forms`; la E14 le suma los formularios y la pregunta «Elegir de un catálogo». `/plan-etapa` la convierte en plan cuando le toque, después de verificar los puntos marcados **[E11]** y **[E9]** contra el código real.

**Fuentes:**
- alcance: [hoja de ruta](../2026-10-12-formularios-dinamicos-hoja-de-ruta.md), §E12;
- reglas: [AGENTS.md](../../../AGENTS.md) y las [fichas](../../rules/README.md);
- referencia de producto: el traspaso 2 de ArquitecturaBase, anexo C §10 y anexo D (CA1-A a CA3). Es solo referencia, no código para copiar.

**Requisitos:** la E11 (los contactos de la bandeja Clientes y sus etiquetas) y la E9 (cuotas y fuzz). La E10 no frena (123). Antes de esta etapa va [la base](base-de-la-12.md), con las piezas del front que esta usa: los botones y las esperas, la (i) y lo apagado con su motivo, los filtros sin cantidades, la primera carga, la confirmación que espera, la forma y la escala. Después van [su segunda parte](base-de-la-12-segunda-parte.md) (214), con `Picker`, el orden por columnas y los formatos; la etapa [«Acciones del asistente»](acciones-del-asistente.md), con el puerto `IAssistantActionSource`, el sufijo `ActionSource`, la batería genérica y, en el front, `src/shared/assistant-actions/`; y las [Costuras de módulos](costuras-de-modulos.md) (0069): el permiso de uso con una fila por origen, que leen `[FeatureGate("forms")]` y la feature de WhatsApp, y apagar un módulo sin borrar sus datos, con un test por módulo.

**Partes.** Esta spec no entra en un solo archivo, así que sigue en otros, con los mismos números de sección. Lo que no está en la tabla está acá.

| Secciones | Archivo |
| --- | --- |
| §2, §3, §4, §4b, §5, §6, §7 y §8 | [datos y contrato](12-parte-datos-y-contrato.md) |
| §9, §11, §14 y los errores con sus textos | [archivos, tests y referencia](12-parte-archivos-tests-y-referencia.md) |

## Qué leer

Antes de empezar:
1. **Decisiones** de la hoja de ruta: las de la §13 de esta spec.
2. **Fichas:** [persistencia-ef](../../rules/persistencia-ef.md), [multitenancy](../../rules/multitenancy.md), [auditoria](../../rules/auditoria.md), [guardado](../../rules/guardado.md), [concurrencia](../../rules/concurrencia.md), [idempotencia](../../rules/idempotencia.md), [api-http](../../rules/api-http.md), [permisos](../../rules/permisos.md), [paginado-y-busqueda](../../rules/paginado-y-busqueda.md), [result-y-errores](../../rules/result-y-errores.md), [validacion](../../rules/validacion.md), [textos-libres](../../rules/textos-libres.md), [numeros-y-moneda](../../rules/numeros-y-moneda.md), [fechas-y-zonas](../../rules/fechas-y-zonas.md), [textos-y-traducciones](../../rules/textos-y-traducciones.md), [logs](../../rules/logs.md), [cuotas](../../rules/cuotas.md), [modulos](../../rules/modulos.md), [modulos-habilitados](../../rules/modulos-habilitados.md), [datos-personales](../../rules/datos-personales.md) y [tests](../../rules/tests.md).
3. **Código para copiar:** el módulo Asistente, pieza por pieza, en la §9.
4. **Pantallas:** los tableros de la §10, con el recorrido `armar-un-catalogo` del front.
5. **Referencia de producto:** la §14.
6. **Acciones del asistente:** la [ADR 0068](../../decisions/0068-acciones-del-asistente.md) y la [receta](../../guides/agregar-una-accion-al-asistente.md), para la §17.

## 1. Alcance

**Entra:**
- **El módulo `Forms`:** quitable, con su feature `forms`, sus permisos de catálogos y la entrada «Gestión de formularios» del menú, que en esta etapa tiene solo «Catálogos» (34 y 78).
- **Catálogos de la organización:** listado, que ordena por cada columna (264), alta con el origen fijo (a mano, de los miembros o de los contactos), datos y estado «Activo», «De baja» o «No se puede usar».
- **Dar de baja y reactivar un catálogo** desde su pantalla, con su versión, sin borrarlo. Dar de baja uno con filtros no necesita vista previa, y reactivarlo sí.
- **A mano:**
  - **tipo de valor:** texto, entero, decimal o fecha, que se elige al crear y no cambia;
  - opciones con texto, detalle, código y, en un catálogo tipado, valor. Cada texto es uno solo, tal como lo escribe la persona (35). Se agregan y se editan en un modal y, en la compu, también en la tabla (117);
  - una opción se da de baja con su Estado. Una guardada que nada usa también se puede borrar: se marca, se borra al guardar y hasta entonces se restaura (129). Se reordenan con ↑ y ↓, o con «Ordenar por…» Texto o Código, que se aplica al guardar (224);
  - **la carga de una vez:** un solo «Importar», con dos orígenes: pegar desde Excel (agrega) o subir un XLSX o un CSV (reemplaza), con vista previa (120);
  - **exportar** y descargar la plantilla. El Excel se lee como en pantalla (119);
  - **hasta 10.000 opciones** por catálogo, contando las dadas de baja.
- **De los contactos** (55): las opciones son los contactos de WhatsApp de la organización, los de la bandeja Clientes, con su nombre. Su vista previa elige un cliente de ejemplo, igual que la de los miembros. Ver la §4b.
- **El nombre de un contacto** (81): un contacto sin nombre toma el de su perfil de WhatsApp cuando escribe, y se puede cambiar. El teléfono nunca se muestra.
- **Con filtros:**
  - sobre la fuente «Miembros de la organización», con condiciones armadas por el sistema y «Sin incluir a quien lo recibe»;
  - vista previa con una persona de ejemplo, elegida a propósito: el teléfono, con solo la pieza «Persona» (262) arriba, que se actualiza sola;
  - el valor es siempre de texto.
- **Permisos:** `formcatalogs.read` y `formcatalogs.manage`.
- **Acciones del asistente (268):** las opciones de un catálogo a mano, declaradas una vez por tipo (§17).

**No entra:**
- los formularios y la pregunta «Elegir de un catálogo»: elegir un catálogo en una pregunta, mandar, responder y «Lo usan» (E14);
- campos extra y qué viaja a WhatsApp (E13);
- datos de las personas y filtros por destinatario (E16);
- catálogos con una consulta SQL;
- productos u otras fuentes externas, que no existen en la plantilla (los clientes son la fuente «Contactos», 55);
- la búsqueda en vivo o las listas dependientes, porque necesitan el intercambio cifrado con Meta;
- límites por plan: los topes son iguales para todas (42).

## 10. Pantallas (tableros)

**Navegación (34 y 78):** «Gestión de formularios» llega con esta etapa, y su panel tiene solo «Catálogos». Los tableros de la etapa muestran solo eso.

**Aprobados y publicados en el lienzo v150** (2026-11-02, front `b859fe70`), sin «Textos en» ni «Cambios sin guardar». Tienen teléfono todos menos `FRM-Catalogo-Tipo` y `FRM-Catalogo-Nuevo`:
- `FRM-Catalogos`: el listado, sin la columna «Lo usan», que llega con los formularios;
- `FRM-Catalogo-Nuevo`: el origen, con la tarjeta «De los contactos de WhatsApp», y el tipo de valor;
- `FRM-Catalogo-Tipo`: el tipo de valor se elige al crear, con la columna «Valor» y el error en el campo;
- `FRM-Catalogo-Filtros`: un catálogo de los miembros, con sus filtros y la vista previa;
- `FRM-Catalogo-Contactos`: un catálogo «De los contactos», con sus filtros por etiqueta y la vista previa (55).

**Con el armado de ArquitecturaBase** (117, 120 y 125 a 129):
- `FRM-Catalogo` y `M-FRM-Catalogo`: el editor de un catálogo a mano, con el armado de ArquitecturaBase. «El catálogo» dice de dónde salen y el tipo, y debajo van Nombre y Descripción; al costado, «Así lo ve quien lo recibe», con «Una opción | Varias» y el teléfono. La banda trae «Descartar» y «Guardar cambios», y un ⋮ con «Dar de baja» o «Reactivar», como el editor de formularios (209). «Opciones» va a todo el ancho: «+ Agregar opción», «Importar», «Exportar» y un ⋮ con Descargar la plantilla, «Ordenar por…» y Generar los códigos que faltan; abajo, solo «Buscar opción»; la tabla editable en la compu, la selección de filas para dar de baja o reactivar, el Estado como casilla y ↑ ↓ ✎ 🗑 a la vista. «Restaurar» una por borrar y «Reactivar» una dada de baja son opciones del ⋮ de su fila, con su ícono, nunca un botón con texto (261);
- `FRM-Catalogo-Importar`: «Origen» y «Revisión» a la vista, sin pasos numerados. Redibujado con el traspaso 5, aprobado y publicado en el lienzo v162: «Cambios» se lee lado a lado (138), y las tablas llevan la línea entre columnas y la fila a la derecha (137).

**Nuevos** (v148): `FRM-Catalogo-Opciones` y `M-FRM-Catalogo-Opciones`, el modal de opciones. Importar no va en el teléfono, como hasta ahora.

**Del traspaso 15** (v175): `FRM-Catalogo-Ordenar` y su teléfono, nuevos, y `FRM-Catalogo` y su teléfono, redibujados.

**Se redibujan con el traspaso 18, tanda 2** (261 a 264):
- `FRM-Catalogos` y `M-FRM-Catalogos`: «Origen» y «Estado», filtros con su botón y sin cantidades (262); el encabezado y «Ordenar por» ordenan cada columna (264); en el teléfono, «Nuevo catálogo» abre el editor, como en la compu, sin la hoja de elegir (262);
- `FRM-Catalogo-Nuevo` y `FRM-Catalogo-Tipo`: se van como diálogos, porque solo servían para elegir (262); el origen y el tipo de valor se eligen en la pieza, en «El catálogo» del editor;
- `FRM-Catalogo` y `M-FRM-Catalogo`: «Restaurar» y «Reactivar», en el ⋮ de la fila (261);
- `FRM-Catalogo-Opciones` y su teléfono: el Estado, en la pieza (262), y en la lista, el código en su columna, no debajo del texto (263);
- `FRM-Catalogo-Ordenar` y su teléfono: la columna y el orden, en la pieza (262);
- `FRM-Catalogo-Importar`: «Desde», en la pieza (262);
- `FRM-Catalogo-Filtros`, `FRM-Catalogo-Contactos` y sus teléfonos: los valores de una condición, varios por su nombre, separados por coma y sin chips, y la persona o el cliente de ejemplo, en la pieza, que dice solo el elegido (262).

**Se dibujan antes del front** (268): `FRM-Catalogo-Opciones-Propuesta` y su teléfono, el editor abierto desde una propuesta del asistente (§17), con «Propuesta del asistente» junto al título, lo propuesto cargado sin guardar, «Lo que cambia» con lo que se suma, cambia y quita, «Revisá» en lo dudoso y el aviso si el catálogo cambió desde la propuesta.

**Redibujados y aprobados el 2026-11-26** (lienzo v185, front `c29ade7b`; decisión 284): los de la tanda 2 y los dos de la propuesta. `FRM-Catalogo-Nuevo` y `FRM-Catalogo-Tipo` pasaron al Archivo del lienzo.

**«Ordenar por…» (224):** el diálogo «Ordenar las opciones» elige con la pieza (262) la columna (Texto o Código) y el sentido dicho con palabras, «De la A a la Z» o «De la Z a la A». El orden queda pendiente hasta guardar: arriba de las opciones, «Se ordenará por «Texto», de la A a la Z, al guardar.» con «Cancelar orden». Al guardar ordena todas, también las de otras páginas y las dadas de baja: los empates conservan su orden, lo vacío va al final y los números dentro de un texto se comparan por su valor («117» antes que «10030»). En el ⋮ queda apagado con su motivo en un catálogo sin guardar («Guardá el catálogo antes.») o con movimientos pendientes. No se ordena tocando el encabezado: el orden de las opciones es un dato (264).

**El modal de opciones (117):**
- **Cómo se abre:** «Agregar opción» lo abre con las opciones agregadas en él, y ✎, con las de la página. Cada lado va en su caja: la lista a la izquierda (número o «—», texto, «Nueva» y código o «Sin código», cada dato en su columna, 263) y la opción elegida a la derecha, con su posición («Opción 2 de 5» o «Nueva opción») y «Quitar opción» en su banda. «Agregar opción», al pie de la lista, suma otra.
- **Las filas:** Texto con su contador, Código con «Generar desde el texto», Valor si el catálogo es tipado, Detalle y, solo en una guardada, Estado, en la pieza (262), con «Activa» y «De baja». La ayuda va en la (i) de cada una.
- **Guardar:** escribe en el borrador, y se guarda con «Guardar cambios» de la página. «Cancelar» o Esc devuelven todo como estaba al abrirlo. «Listo» conserva todo, menos una opción nueva sin nada escrito.
- **Errores:** si «Guardar cambios» encuentra uno, el modal se abre en la primera opción con problemas, y la lista las marca con un triángulo.
- **Quitar:** en una nueva, «Quitar opción» no pregunta. En una guardada pregunta y la marca: se borra al guardar, y hasta entonces se puede restaurar (129). Si algo la usa, al guardar se dice qué y se puede dar de baja (`Forms.Catalog.ItemInUse`).
- **Celular:** ocupa la pantalla. Primero la lista y, al tocar una opción, la opción con «Volver a la lista».

**Importar (120):** un solo diálogo, de arriba abajo.
- **«Origen»:** «Desde», en la pieza (262), con «Subir un archivo» y «Pegar desde Excel», y, para un archivo, su nombre con «Elegir archivo» o «Cambiar». «Descargar la plantilla» va en la banda. Qué acepta el archivo y qué hace cada origen van en sus (i).
- **La grilla:** lo pegado se ve con los encabezados del catálogo, en el orden de la plantilla. Sin encabezados, las filas van en ese orden. Si lo copiado los trae, por nombre, cada columna va a su lugar y esa fila no se importa. Una columna que no es del catálogo queda al final, en rojo, con su motivo, y no se manda. Pegar sobre la grilla llena la reemplaza.
- **«Revisión»:** aparece debajo al tocar «Revisar», con el archivo, la hoja, las filas leídas y las que no cambian, y Errores, Altas, Cambios y Bajas con su cantidad, por fila y columna. «Cambios» se lee lado a lado (138): Fila, Código, Columna, Antes y Después, solo lo que cambió. «Antes de aplicar» aparece solo si hay algo para confirmar, con la confirmación de bajas y «Aplicar sin las N filas con errores».
- **Las esperas:** «Revisando…» muestra la revisión como un esqueleto, y Cancelar sigue andando. «Aplicando…» deja la revisión atenuada, y Cancelar y la ✕ apagados, con «Esperá a que termine de aplicar.» en su tooltip.

**Estados que se dibujan** (cada uno tiene que poder producirse; si no, es un bug):
- **Listado:** filtro abierto, sin coincidencias, cargando, vacío y error.
- **Catálogo a mano:**
  - código repetido, texto repetido (avisa sin frenar), texto de más de 30 y valor de otro tipo, en la tabla y en el modal;
  - guardado, no se guardó y la revisión si otra persona guardó;
  - sin opciones, 1.200 opciones con búsqueda y páginas, 10.000 opciones, nuevo y cargando;
  - importar grisado en un catálogo nuevo o con cambios sin guardar, con el motivo en su tooltip;
  - una opción nueva, borrar una guardada (pregunta), «Se borra al guardar» con «Restaurar», una dada de baja con «Reactivar», movimientos pendientes y el orden pendiente, con cómo descartarlos, y las dos formas de la vista previa. Con movimientos o con el orden pendientes, el 🗑 de una guardada queda apagado, con «Guardá o descartá el orden pendiente para borrar opciones.» en su tooltip; con una opción por borrar, ↑ ↓ quedan apagados, con «Guardá o descartá el borrado pendiente para mover opciones.»;
  - con solo `formcatalogs.read`: la tabla de lectura, sin casillas, con Exportar y la plantilla.
- **Modal de opciones:** una nueva, una guardada, una dada de baja, una por borrar, volver a la lista en el celular, y errores con el triángulo.
- **Importar:** la grilla vacía y con una columna ajena; «Revisar» sin archivo o sin filas, que marca el campo; revisando, aplicando, errores por fila y columna, muchas bajas que piden confirmar, muchas filas con error, un valor ambiguo, el 409 y otra importación en curso.
- **Catálogo con filtros:**
  - con más de 200 opciones para la persona de ejemplo, de los miembros o de los contactos, se guarda, y la vista previa termina en «y N opciones más» con su ⓘ: «Con estos filtros hay 215 opciones, y WhatsApp muestra hasta 200.» (12 y 22);
  - sin persona de ejemplo, no se crea;
  - un rol que ya no existe deja el catálogo en «No se puede usar».
- **Catálogo de los contactos:** sin WhatsApp, «No se puede usar».

**Mandan sobre esta spec en:**
- **Las fichas del front** (pedidos del usuario del 2026-10-25 y del 2026-10-26):
  - ninguna sección lleva cantidades en su encabezado: nada de «N opciones · N activas»;
  - un dato por columna (206); el texto largo se ve entero, bajando de renglón (174);
  - las fechas, completas, con fecha y hora en la zona de quien mira; nunca «hoy», «ayer» ni «hace…»;
  - sin leyendas (109): la vista previa es el teléfono al costado, la ayuda que sirve va en la (i) (116), y lo que no ayuda a decidir no va;
  - toda tabla lleva una línea fina entre columnas y los números a la derecha, y cierra abajo (137);
  - lo dado de baja es una opción del filtro «Estado», no una vista aparte, en plural y de acuerdo con lo que se lista: en la lista de catálogos, «Activos» y «Dados de baja», en la pieza con su botón y sin cantidades (262). En la tabla de opciones no hay filtro: cada una muestra su casilla (126).
- **Una sola pieza para elegir (262):** todo lo que se elige es la pieza `Picker` de la base: los filtros del listado, el origen y el tipo de un catálogo nuevo, el Estado del modal, «Desde», la columna y el orden de «Ordenar por…», las condiciones de un catálogo con filtros (varias por su nombre, separadas por coma, sin chips) y la persona o el cliente de ejemplo, que vienen del servidor y se buscan adentro de la lista. Queda «Una opción | Varias» de la vista previa, que imita WhatsApp. Una casilla queda solo para un sí o no suelto, como el Estado de una fila o «Sin incluir a quien lo recibe».
- **Nada debajo de otro dato (263):** cada dato va en su columna, también en la lista del modal; debajo de una fila va solo su error.
- **Ordenar el listado (264):** Catálogo, De dónde salen, Tipo de valor, Opciones y Estado ordenan desde el encabezado y desde «Ordenar por», último en la barra. Lo vacío («—» en Opciones) va al final en los dos sentidos, el origen, el tipo y el estado van por su orden, y el primer toque ordena el texto de la A a la Z y los números de mayor a menor. Sin orden elegido, abre por nombre.
- **Valen las de toda la app** (160 a 164, 173 a 175 y 180): lo que carga no salta, decidir a propósito, ninguna elección se descarta en silencio, mucho texto con jerarquía, sin parecer plantilla y con la escala de ArquitecturaBase, el texto largo entero y el estado dicho una vez. Llegan con las piezas de la base de la 12.
- **La tabla de opciones no se aplasta:** cada columna tiene su ancho mínimo (Texto y Detalle 12rem, Código 6rem y el valor 8rem). Si no entra, la tabla se desplaza a lo ancho dentro de su caja, con la casilla, el número y «Texto» fijos a la izquierda y las acciones fijas a la derecha, con la línea entre columnas también en las fijas (137) y sin ningún «hay más columnas a la derecha». Es la primera tabla con columnas fijas, y las trae esta etapa.
- **Guardado (C3):** todo se guarda junto en un `PUT` del catálogo, con su versión, y viaja solo lo que cambió. Suma `deleteItems[]`, las opciones por borrar. Una que algo usa responde 409 `Forms.Catalog.ItemInUse`, con qué la usa por opción, y no se borra ninguna; en esta etapa nada usa una opción, y cada etapa suma su motivo.
- **Nuevo catálogo:** abre el editor, y en «El catálogo» se elige primero el origen y, para los escritos a mano, el tipo de valor, los dos en la pieza, sin un diálogo que solo sirve para elegir (262); después el nombre, y el `POST` sale de «Crear catálogo».
- **Persona de ejemplo:** sin ella no se crea un catálogo de miembros con condiciones (`Forms.Catalog.SampleRequired`, «Elegí una persona.»).
- **La vista previa de un catálogo de miembros o de contactos** es el teléfono, con solo la pieza «Persona» (262) arriba, y se actualiza sola; no hay «Probar». Se ve con `formcatalogs.read`, como el catálogo.

## 12. Puntos de contacto

Al planificar la E12 se verifican contra el código real.

| Marca | Qué hay que confirmar | Qué asume esta spec |
| --- | --- | --- |
| Base de la 12 | Las piezas del front que usa esta etapa. | Están en el front: los botones y las esperas, la (i) y lo apagado con su motivo, `FilterBar` sin cantidades, la primera carga de `DataTable`, `PageLoading` y `FormSkeleton`, `ConfirmDialog` con `pending`, y la forma y la escala. |
| [E11] | Los contactos de la bandeja Clientes (`WhatsAppCustomer`): las etiquetas son `text[]` y el nombre es opcional. | `IStructuredContactSource` los lista por página, con filtros por el texto de la etiqueta, sin teléfonos; la entrada completa el nombre (C14). |
| [E11] | La planilla de contactos (`IWhatsAppContactSpreadsheet`, ClosedXML). | `IFormCatalogSpreadsheet` copia su forma. |
| [E9] | Las cuotas compartidas ([cuotas](../../rules/cuotas.md)). | Las rutas de Forms cuentan en las cuotas generales; no se definen perfiles propios. |
| [E9] | Redis. | La E12 no cachea nada nuevo. |
| #51 | `Enter` borra `TenantKind`. | La E12 no tiene workers. |

## 13. Decisiones de producto de la etapa

Están en la §5 de la hoja de ruta y no se vuelven a preguntar:
- **12, 22 y 37:** un catálogo con filtros que da más de 200 opciones se puede guardar. La vista previa lo avisa con su ⓘ, y el bloqueo es al mandar (E14).
- **35:** el contenido no tiene idioma.
- **36:** un nombre de más de 30 caracteres se corta en 29 y se le agrega «…».
- **42:** hasta 10.000 opciones, igual para todas las organizaciones.
- **43:** botones y avisos.
- **55:** el origen «De los contactos».
- **78:** los catálogos van primero, sin formularios.
- **81:** un contacto sin nombre toma el de su perfil de WhatsApp, y se puede cambiar; el teléfono nunca se muestra.
- **109, 110, 115 y 116:** sin leyendas, lo apagado dice por qué, toda espera a la vista y la ayuda en la (i).
- **117 y 129:** las opciones en un modal; una guardada se da de baja, y una que nada usa también se borra.
- **119:** el Excel se lee como en pantalla, y se reimporta sin diferencias.
- **120:** un solo «Importar», con la grilla.
- **224:** «Ordenar por…» reemplaza «Ordenar de la A a la Z».
- **261:** lo que se hace con una fila va en su ⋮; ↑ ↓ ✎ 🗑 siguen a la vista donde importa el orden (127).
- **262:** una sola pieza para elegir, y los filtros sin cantidades.
- **263:** nada debajo de otro dato.
- **264:** toda columna con un dato ordena, salvo las tablas cuyo orden es un dato, como las opciones.
- **268:** la IA prepara y la pantalla de siempre confirma; las opciones de un catálogo se declaran una vez por tipo (§17).
- **269:** los principios del traspaso 17: el tope de un texto es el mismo en la pantalla y en el servidor, y al llegar se ve.
- **270:** ninguna regla de negocio fija en el código. Los topes de las operaciones de archivo (C11) son técnicos y quedan en `appsettings`.

## 15. Cómo se cierra

La puerta de la etapa ([etapa en paralelo](../../guides/etapa-en-paralelo.md)), con la lista de control de `/puerta`. No hay prueba con Meta, porque la E12 no manda nada por WhatsApp. El recorrido prueba:
1. **A mano:** «Proveedores» pegando desde una planilla, y «Recargo por zona», decimal, con el valor en su columna.
2. **Carga de una vez:** importar una planilla real de unas 1.000 filas, con algunas bajas, y exportarla de nuevo sin diferencias.
3. **Miembros:** un catálogo con filtros y su vista previa con una persona de ejemplo, vacío y con opciones, con «Sin incluir a quien lo recibe».
4. **Contactos:** un catálogo de los contactos de Clientes, filtrado por etiqueta, sin teléfonos a la vista. Un contacto nuevo que escribe sin nombre aparece con el de su perfil (81).
5. **Volumen:** 10.000 opciones: buscar, guardar una y exportar, con EXPLAIN.

## 16. Criterios

Salen de los reviews de ArquitecturaBase (E1) y del traspaso del 17/10. El plan los toma como lista de control, junto con la de `/puerta`:
- **Los límites del canal se validan al guardar** (30 y 300), no al mandar; la vista previa marca lo que no entra.
- **Referencias por id,** nunca por nombre: un rol renombrado no vacía el catálogo, y dar de baja un rol tampoco lo invalida.
- **Ninguna lectura trae todo para quedarse con poco:** listar, buscar, exportar y la vista previa leen en páginas o lotes, probado con 10.000 opciones.
- **Guardar sin cambios no toca las opciones,** y guardar una opción no reescribe las demás.
- **Lo que viene de un archivo** se lee con la cultura del pedido y rechaza lo ambiguo. Una columna opcional que no se pegó conserva sus valores.
- **El Excel no redondea nada:** lo que no entra en una celda numérica va como texto canónico.
- **Contratos:** `""` no es `null`; una entrada inválida es 400, nunca 500; un campo omitido no apaga nada.
- **En el front:**
  - la búsqueda espera 300 ms y conserva el editor y el foco;
  - guardar las opciones conserva el lugar: la pantalla no sube (160);
  - una página tardía de otro catálogo se descarta;
  - el archivo vive solo en el diálogo y se descarta al salir;
  - el front no tapa la validación del servidor;
  - un 403 en una acción no oculta la pantalla de lectura.

## 17. Acciones del asistente

Lo que la IA puede preparar en esta etapa (268, [ADR 0068](../../decisions/0068-acciones-del-asistente.md)), con la [receta](../../guides/agregar-una-accion-al-asistente.md):
- **`catalogoptions.edit@1` · Cambiar las opciones de un catálogo:** editar, con `formcatalogs.manage` (el de la ruta), el módulo `forms` y `SaveRoute` `PUT api/organization/forms/catalogs/{id}`, sin `[Idempotent]` porque no crea. Se confirma en el editor (`Page`), sin guardar, con «Propuesta del asistente» y «Lo que cambia», y el editor suma su valor a `AssistantActionScreen`, con su traducción a `AssistantRouteKey`. No tiene efecto fuera del sistema. El destino es un catálogo a mano activo, que encuentra `IFormCatalogReader` por su nombre.
- **Una vez por tipo** (0068, «Consecuencias»): sirve para todo catálogo a mano, y uno nuevo no pide nada. Sus campos salen del catálogo destino. Las opciones se suman, se cambian y se quitan como operaciones (0068 §4), cada una con Texto (`Text`, 30), Código (`Text`, 40), Detalle (`LongText`, 300), Estado (`Boolean`) y, en uno tipado, Valor (`Integer`, `Decimal` o `Date`). Una opción guardada es un `Record`: la resuelve el servidor por su texto o su código, y nunca va al proveedor. Quitar la marca como en la pantalla: se borra al guardar, y una que algo usa da `ItemInUse`. No propone el orden ni importar.
- **Lo que no se declara:** crear un catálogo o cambiar su nombre, su descripción o sus filtros es armar la definición, que sigue con la 0057 (`propose_catalog`). Dar de baja o reactivar el catálogo tampoco: el editor lo guarda al tocarlo, sin un estado pendiente donde confirmarlo, y sin pantalla donde confirmar no hay acción.

Queda registrada y sin ofrecer hasta que se califique, antes de la prueba integral del bloque.
