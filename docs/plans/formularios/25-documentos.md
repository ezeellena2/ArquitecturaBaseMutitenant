# E25 · Documentos por WhatsApp (spec)

**Estado:** pendiente · spec de la etapa 25, la última del bloque (decisiones 34, 41, 78, 99, 102, 146, 154 a 156, 165, 167, 171, 177, 181, 182, 192, 215 a 218, 221 a 223, 230 y 238, y de los traspasos 16 a 18, 261 a 264, 266 a 272 y 275 a 278), con el [formato](plantilla.md) de las etapas. En ArquitecturaBase, los documentos están decididos, con su ADR 0022 y sus tableros, y sin programar; la IA que arma formularios y catálogos tiene programada su etapa 1, con texto y audio (ADR 0024). **Antes de programar,** en la K0 del plan, se prueba con Meta si un Flow acepta un archivo precargado: es el mayor riesgo técnico. Allá se precarga, pero Meta no devuelve ese archivo en la respuesta (O38, en [Qué verificar](25-parte-pantallas-y-verificar.md#qué-verificar-al-arrancar)). `/plan-etapa` la contrasta con el código real después de la E24. Rigen las ADR [0056](../../decisions/0056-lectura-de-documentos-con-ia.md), la lectura de documentos, y [0057](../../decisions/0057-la-ia-propone-y-una-persona-crea.md), la IA que arma.

**Partes.** Esta spec no entra en un solo archivo, así que sigue en otros, con las mismas secciones. Lo que no está en la tabla está acá.

| Secciones | Archivo |
| --- | --- |
| Contrato, Datos, la Configuración de Formularios y En el multitenant | [datos y contrato](25-parte-datos-y-contrato.md) |
| Pasos y lo que ya se pagó allá | [pasos](25-parte-pasos.md) |
| Armar con la IA desde el asistente, Pantallas y Qué verificar al arrancar | [pantallas y verificar](25-parte-pantallas-y-verificar.md) |

## Qué leer

Antes de empezar, en este orden:

1. **Lo anterior:** la etapa [«Acciones del asistente»](acciones-del-asistente.md): la propuesta en el `Message`, sus estados y su tarjeta; la [spec de la E12](12-catalogos.md): importar; la [E13](13-campos.md): el cruce por un campo; la [E14](14-formularios.md): la captura; la [E15](15-respuestas.md): la respuesta como el formulario y su actividad; la [E18](18-fotos-y-documentos.md): archivos y la Configuración de Formularios; la [E19](19-condiciones-calculos-y-lineas.md): el bloque; la [E20](20-cargar-desde-la-web.md): la carga de la web; la [E21](21-reglas-y-aprobacion.md): corregir y las dos casillas de aprobar; la [E22](22-biblioteca.md): plantillas, y la [E23](23-asistente.md): turnos, herramientas y prellenado. Del chat del Inicio, la ficha [Asistente](../../features/asistente.md): el SSE, los adjuntos y los topes del turno.
2. **Decisiones** de la hoja de ruta: 23, 24, 35, 39, 40 a 42, 49, 50, 70 a 72, 75, 78, 99, 102, 110, 146, 154 a 156, 161, 165, 167, 171, 177, 178, 181, 182, 192, 206, de la 215 a la 223, 229, 230, 238, 239, de la 261 a la 264, de la 266 a la 272 y de la 275 a la 278. ADR [0056](../../decisions/0056-lectura-de-documentos-con-ia.md), [0057](../../decisions/0057-la-ia-propone-y-una-persona-crea.md), [0064](../../decisions/0064-reglas-de-toda-la-ia.md), las reglas de toda la IA, [0068](../../decisions/0068-acciones-del-asistente.md), las acciones del asistente, y [0070](../../decisions/0070-motor-que-contesta-whatsapp.md), el motor que contesta WhatsApp, al que esta etapa suma pasos y acciones.
3. **Fichas y guías:** [datos-de-referencia](../../rules/datos-de-referencia.md), [fechas-y-zonas](../../rules/fechas-y-zonas.md), [paginado-y-busqueda](../../rules/paginado-y-busqueda.md), [identificacion-fiscal](../../rules/identificacion-fiscal.md), [modulos](../../rules/modulos.md), [guardado](../../rules/guardado.md), [concurrencia](../../rules/concurrencia.md), [datos-personales](../../rules/datos-personales.md), [logs](../../rules/logs.md), [cuotas](../../rules/cuotas.md), [tests](../../rules/tests.md), la [guía de herramientas](../../guides/agregar-una-herramienta-al-asistente.md) y la [receta de acciones](../../guides/agregar-una-accion-al-asistente.md).
4. **Código para copiar:** la columna «Copia de» de los [pasos](25-parte-pasos.md#pasos).
5. **Referencia de producto,** solo para leer: en `../ArquitecturaBase/docs/plans/2026-10-23-traspaso-multitenant/`, el anexo F, F3, y el anexo D (`DOC1` a `DOC6`). Allá también: `docs/features/formularios.md`, «Catálogos propuestos» y «La IA arma formularios y catálogos»; `docs/features/inteligencia-artificial.md`, «Audio en Inicio», y las ADR 0022 y 0024. En su front, `docs/design/documentos.md` e `ia-arma-formularios.md`, y las capturas `assistant-proposal`, `assistant-audio` y `catalog-manual-proposal` de `src/test/visual/screenshots/`.

## Qué se construye

**Qué se lee** (70): lo que manda un empleado activo, con teléfono verificado y `formrecords.create`, en Internos, y lo que manda un cliente, cualquier contacto de Clientes (49). A cada uno se le proponen solo los formularios que son para él (50). Un documento que se reconoce como un formulario solo para la app queda en «Documentos recibidos», para pasarlo al formulario desde la web, y no se ofrece por WhatsApp (203). En un chat en atención manual también se lee: queda en «Documentos recibidos», y al operador le aparece la sugerencia de mandar el formulario prellenado, con «Revisar y enviar», como en la E23 (199). **Se lee solo si se va a usar:** si ningún formulario publicado que esa persona puede completar tiene «Lectura de documentos», el archivo queda en el chat como antes, sin IA.

| Lo que llega | Cómo se lee |
| --- | --- |
| Foto o captura (JPEG, PNG) | la lee la IA |
| PDF | la IA lee todas sus páginas, hasta las «Páginas de un PDF» de la configuración (10); uno más largo no se lee, y se dice con el número (270) |
| Word (`.docx`) | el servidor saca el texto y las tablas, sin ejecutar nada, y la IA los lee |
| Excel (`.xlsx`) | el servidor lee las hojas, con los topes de la E12: la IA solo propone qué columna va a qué pregunta, y el servidor arma cada fila. Hasta 50 filas, sin contar encabezados, totales ni filas vacías. Varias filas proponen por mensaje tantas cargas como «Formularios por planilla» de la configuración (3, 270), en su orden, con el aviso fijo «Recibí una planilla con 7 filas de «Cobranzas».» y «1 de 3 · {resumen}» en cada una; las demás quedan en la bandeja, y una fila repetida no se carga |
| Ubicación y contacto | sin IA: completan las preguntas elegidas en el editor. Una ubicación que contesta el pedido de la E20 antes de un Flow es de ese envío (230) |

No se leen `.doc`, `.xls`, los archivos con macros ni los que tienen contraseña: se pide un PDF o una versión sin eso. Un PDF con contraseña queda «Sin clasificar» con el motivo «Protegido: tiene contraseña.», salvo el que solo restringe permisos, que se abre. El tipo se verifica por el contenido. **Un PDF hostil no frena la lectura:** se infla con un tope acumulado, cada lectura tiene un plazo que da «ilegible», y los intentos se cuentan.

**El circuito:**
1. **recibir:** se toma el archivo que ya bajó el chat y se guarda una copia privada;
2. **clasificar** entre los formularios con «Lectura de documentos» que esa persona puede completar. Seguro: propone ese. Con duda entre dos: «¿Qué es este documento?», con un botón para cada uno y «Otro». Sin reconocerlo: una lista de hasta 10 filas con «Otro», que lo deja «Sin clasificar» y lo anota en «Formularios que faltan». Un nombre de más de 20 caracteres va como lista;
3. **extraer** los datos según las preguntas elegidas, con su seguridad por dato;
4. **validar sin IA:** las validaciones fijas, el cruce con catálogos y los duplicados (el mismo archivo, o la misma operación, monto y fecha);
5. **proponer** el formulario prellenado, con el documento en su pregunta de archivo. Lo que no se leyó, o no tiene la forma de su validación, queda vacío; lo dudoso, y lo que tiene la forma y no pasa, marcado (277). Lo lleva la oferta fija de la E23 (165), y lo que explica la lectura lo redacta la IA, dentro de los límites de las respuestas redactadas (39 y 192);
6. **confirmar o corregir** lo hace la persona. Al confirmar se vuelven a revisar las validaciones y los duplicados con lo corregido. Nada se carga solo, salvo «Cargar solo si es seguro»;
7. **organizar:** queda como una respuesta más, con sus reglas, su aprobación y su PDF.

**Los casos difíciles:** una foto movida pide otra, diciendo qué falta; un PDF con contraseña pide uno sin ella («Este PDF tiene contraseña. ¿Me mandás uno sin ella?»); una planilla de más de 50 filas, «Por acá cargo planillas de hasta 50 filas. Dividila y mandámela de nuevo.»; el tope del día, «Por hoy llegué al tope de documentos que puedo leer. Lo dejé para que lo revise el equipo.», que es fijo (217) y corta a la medianoche de la organización (271); un repetido avisa cuándo se cargó, y si el primero solo se propuso, no dice que ya se cargó (280: si el primero solo se propuso, «Ya me lo mandaste el {fecha}: completá el formulario que te mandé.»; si ya se cargó, «Ya se cargó en {number} el {date}.»); un dato que no pasa una validación no se propone como válido; un monto con signo menos, como el de un comprobante de débito, es un riesgo sin decidir (O47 de allá: en una transferencia el monto es siempre positivo, y un negativo resta en el tablero); un formulario que no se le puede mandar se dice, deriva y queda en la bandeja; un número que no es contacto no se lee.

**La «Lectura de documentos» de un formulario** (DOC1), en su borrador, que se sella al publicar: de qué plantilla y versión viene; «Cómo reconocerlo», con la marca «Lo escribió la IA» cuando sale de «Armar formulario con la IA»; qué pregunta recibe cada dato estándar, en la pieza (262); las validaciones fijas, en la pieza múltiple; el cruce con catálogos; la regla de duplicados, y «Cómo viene leyendo», de los últimos 30 días, por formulario y por dato, en una tabla que ordena (264). **Los ejemplos son para personas** (216): «Ver los ejemplos» lista las últimas correcciones solo a quien tiene `formrecords.read`, y nunca van a la IA. Cambiarle el nombre a una pregunta no corta la lectura, y un dato sin pregunta no se lee. Las preguntas de «elegir varias», las del bloque, las que tienen «Mostrar si» y la que fija una marca no reciben datos, porque el prellenado de la E23 las deja afuera. **En la biblioteca,** una plantilla suma su lectura, que la plataforma edita en «Plantilla», y la copia la trae con las validaciones con candado. La vista de una plantilla dice qué lee y sus validaciones fijas, con candado.

**Las validaciones fijas** son un conjunto cerrado en el código, y cada formulario elige cuáles usa. CUIT y CUIL reusan el validador de `platform.TaxIdTypes`. CBU y CVU suman sus dígitos verificadores. **Cada una sabe su forma** (277): 22 dígitos o un alias para una cuenta, once dígitos para una CUIT. Lo leído de un documento o de una fila que ni siquiera la tiene queda sin leer, como el número interno de una cuenta de débito, que no es CBU, CVU ni alias. **La tabla de entidades del BCRA** es un dato de referencia global, nunca una lista en el código.

**La bandeja «Documentos recibidos»:**
- una planilla como la de respuestas, con el documento en un panel al costado: acercar, girar, páginas y descargar. Cada dato va en su columna (263), y toda columna con un dato ordena (264);
- filtros, en la pieza (262): formulario, quién lo mandó, cuándo llegó, estado y «Empresa ▾» (24). La dirección nunca lleva quién lo mandó ni la búsqueda;
- búsqueda por los datos, como el monto con o sin puntos, un CUIT o una operación, también en lo que no se cargó;
- lo que se hace con una fila (revisar, descartar, volver a abrir) va en su ⋮ (261). Revisar: corregir, cambiar el formulario reconocido, que vuelve a sacar los datos de lo ya leído, sin leer el documento otra vez, pasar al formulario (99), descartar con confirmación y volver a abrir uno descartado, mientras no se borró. Pasar uno que repite una respuesta guardada pide confirmarlo: «¿Pasarlo igual? Ya está cargado en REC-12 desde el 03/11/2026, 10:00.» (`Forms.Document.Duplicate` con `confirmRepeated`). La respuesta se lee como el formulario, con el documento en su pregunta de archivo (266), y su actividad suma el renglón «La pasó desde un documento», con quién y cuándo, y el documento en «Detalle» (267). Si falta algo obligatorio que la lectura no completa, como los ítems de un bloque (181), «Pasar al formulario» abre la carga de la E20 con lo leído, y guarda igual, con su confirmación, a nombre de quien lo mandó;
- la fila de un descartado o sin clasificar muestra cuándo se borra, en su columna «Se borra» (263). **Uno propuesto que nadie revisa vence** (215): pasados los días de «Vencer los propuestos sin revisar» de la configuración (90, 272) desde su último cambio o el fin del envío, pasa a «Descartado», con «Nadie lo revisó en {n} días: venció.», con el número como parámetro (270), y sigue la misma purga; igual uno descartado que alguien reabre y deja. El menú cuenta solo lo abierto.

**«Armar formulario con la IA»** (DOC6): sobre un documento «Sin clasificar», o desde «Llegaron N documentos parecidos», con los de «Parecidos para sugerir un formulario» de la configuración (3, 270). La IA propone nombre, prefijo, botón, «Cómo reconocerlo» y una pregunta por dato, y lo que se repite, como un bloque (146 y 177); si se parece a una plantilla de la biblioteca, la sugiere antes. Es la misma propuesta que la del asistente. Crea un borrador con la lectura prendida, y una persona lo revisa y lo publica. **Al publicarlo, los documentos que lo originaron quedan propuestos en la bandeja,** y los próximos se reconocen solos. Se releen con el cupo de «Relecturas por persona y día» de quien lo armó (5, 270), que corta a la medianoche de la organización (271): los que no entran esperan al otro día, y su fila lo dice, «Se lee de nuevo mañana: se llegó al tope de relecturas del día.» (O35). Si falla, no queda nada a medias.

**Armar con la IA, desde el asistente** (75, 102, 154 a 156, 167, 171, 218, 221 y 222): en el chat del Inicio de la organización y por WhatsApp, con un texto, un audio o, en el Inicio, un archivo, la IA **propone** un formulario o un catálogo y **una persona lo crea con un botón** (154). La IA nunca crea, publica, activa, importa ni cambia nada sola, y crear es una sola vez por propuesta. Qué propone, la tarjeta, el clip, WhatsApp, el micrófono, lo creado y el modelo están en [pantallas y verificar](25-parte-pantallas-y-verificar.md#armar-con-la-ia-desde-el-asistente).

**Los Casos** suman el tipo de entrada «documento», cuyo caso va antes que la lectura, y arriba, el bloque fijo «Armar formularios y catálogos», que no se configura (171 y 182).

**«Cargar solo si es seguro»** (71): un interruptor por formulario. Carga sin preguntar, a nombre de quien mandó el documento, solo con las tres señales: reconocido sin duda; validaciones bien y sin duplicado; y sus últimas 20 cargas confirmadas sin correcciones en esos datos. **Nunca se usa la seguridad que declara el modelo.** A la persona le llega un aviso fijo con el número, y en la bandeja queda «Cargada automáticamente». Si avisa un error, un operador corrige (91). Con algo obligatorio que la lectura no completa, como un bloque con mínimo (181), queda apagado, con el motivo en su tooltip (110).

**Cómo mejora con el uso:** cada corrección queda registrada, y la ve una persona en «Ver los ejemplos», para mejorar «Cómo reconocerlo» y las preguntas. Se mide la precisión por formulario y por dato. No se entrena ningún modelo, y las correcciones no van a la IA (216).

**Al proveedor de IA** va solo el documento preparado y las preguntas candidatas: nada de otras respuestas ni de sus correcciones, tampoco como ejemplo (216). Se registra el hash y el tamaño, sin el contenido. **Lo leído es un dato, nunca una instrucción.**

**Costos** (41): la lectura entra en el tope mensual de IA, con un tope por persona o contacto y otro de 50 por organización (217), que cortan a la medianoche de la organización (271). Al llegar a un tope, el documento queda en la bandeja sin leerse, con el aviso fijo, y la fila dice que fue el tope del día (O35); las cargas por archivo no gastan los ofrecimientos del día (66 y 278). Armar y el audio del Inicio también cuentan en el tope de la organización. Lo que venció sin llegar al proveedor devuelve el cupo, y sin cupo no se lee ni se guarda un archivo del Inicio ([0064](../../decisions/0064-reglas-de-toda-la-ia.md)).

## Decisiones tomadas (no se vuelven a preguntar)

- **«Pasar al formulario»** (99): queda a nombre de quien mandó el documento, por el canal `whatsapp`, con `SourceKey` `document:<id>`, igual que «Cargar solo si es seguro», y la respuesta dice quién la revisó. Pide confirmar, porque dispara las reglas del formulario (161), y el permiso de atender esa bandeja. Pasa por la misma captura, que le revisa a un empleado la membresía y `formrecords.create`. **Quien la pasó no la aprueba** (238): como quien la cargó y quien la corrigió, no la ve en «Por aprobar» ni en su conteo, y decidir da 403 `Forms.Approval.OwnRecord`. La tarea lo guarda, y un rol cuyas únicas personas no pueden decidir da `RoleWithoutMembers` sin abrir tarea (239). Con «Puede decidir quien cargó», sellada en la respuesta, también la decide quien la pasó (276).
- **Retención** (41, 215 y 272): un documento usado sigue a su respuesta (23). Uno descartado o sin clasificar se borra, con su lectura, y uno propuesto que nadie revisa vence, a los días que elige la organización en su configuración (90, de 1 a 3650, dentro del máximo de la plataforma); la (i) avisa que la política de privacidad tiene que prometer lo mismo.
- **Costos** (41, 217 y 271): el proveedor de la plataforma, dentro del tope mensual, con un tope por persona o contacto y otro por organización, en `appsettings`, por día de la organización.
- **Lo que no tiene la forma** de su validación queda sin leer; lo que la tiene y no pasa, marcado (277).
- **Los topes y plazos de esta etapa** van en la Configuración de Formularios, con su mínimo, su máximo y el tope de la organización que da el resolvedor ([0069](../../decisions/0069-modulos-y-suscripciones.md)), leídos en cada uso (270 y 272).
- **La propuesta de armar** es una clase más de la de «Acciones del asistente», con sus estados; la tarjeta sigue creando lo que nace en borrador o por aprobar (154 y 268).
- **Los ejemplos** (216) los ve una persona y no van a la IA.
- **El modelo que arma** (218) es siempre uno calificado para armar.
- **Planillas** (221 y 222): por WhatsApp, con un pedido de armar, van a armar; la IA puede proponer importar en un catálogo que existe, y lo importa la persona.
- **Permisos:** ver la bandeja pide `formrecords.read`; revisar, además, atender la bandeja de donde llegó, y armar desde un documento, además, `forms.manage`. Con el asistente, arma formularios quien tiene `forms.manage`, y catálogos y su aprobación, quien tiene `formcatalogs.manage`.
- **El catálogo propuesto** (167): nace con «Crear catálogo», se edita como cualquiera y se activa solo con «Aprobar y activar».
- **El micrófono** va solo en el Inicio de la organización, y armar por WhatsApp se atiende antes de los casos (171).
- **Lo que se repite** se arma como un bloque, y cada repetición es un ítem (177).
- **La biblioteca** es la de la E22 (40).
- **Genérico:** ningún tipo de documento va en el código. Un estándar nuevo es una plantilla.

## Acciones del asistente

Con la [receta](../../guides/agregar-una-accion-al-asistente.md) (268 y [0068](../../decisions/0068-acciones-del-asistente.md)), registradas y sin ofrecer hasta la calificación:

| Acción | Dónde se confirma | Permiso | Ruta que guarda |
| --- | --- | --- | --- |
| `formdocuments.discard@1` · Descartar un documento | la confirmación de descartar de la bandeja (`RemoveConfirmation`) | `formrecords.read` | `POST …/documents/{id}/discard` |
| `formdocuments.record@1` · Pasar al formulario | la bandeja con el documento en su panel y su confirmación, sin guardar (`Page`). Su efecto fuera del sistema son las reglas del formulario (correo y WhatsApp), y se avisa antes de guardar | `formrecords.read` | `POST …/documents/{id}/record` |
| `formcatalogs.discard@1` · Descartar propuesta | la confirmación de «Descartar propuesta» del catálogo (`RemoveConfirmation`) | `formcatalogs.manage` | `DELETE …/catalogs/{id}` |
| `forms.settings.edit@2` | la Configuración de Formularios de la organización (E18), que suma los seis valores de «Documentos recibidos» (`WholeNumber`): una versión nueva (0068 §12) | los de la E18 | los de la E18 |

Atender la bandeja de donde llegó un documento lo decide la ruta (403), porque la fuente no puede usar `IStructuredInboxAccess` (0068 §5).

**No se declaran:**
- **«Volver a abrir» un documento:** es de un clic, sin confirmación (§9);
- **corregir lo leído:** ya lo propone la IA al leer, y lo corrige la persona;
- **«Aprobar y activar» un catálogo propuesto:** la IA nunca activa lo que propuso (0057 §2 y 167);
- **armar, crear desde la tarjeta e importar:** siguen con la propuesta de la 0057;
- **la «Lectura de documentos», «Cargar solo si es seguro» y los casos «Documento»:** son del formulario, que sigue con la 0057, y de la configuración del asistente (E23).

## Fuera de esta etapa

- Varias fotos de un mismo documento, o varios documentos en un mensaje.
- Leer lo que manda un número que no es contacto de la organización.
- Que la IA cambie un formulario o un catálogo que ya existe: lo dice y da el enlace. Importar en uno que existe sí está: lo propone la IA y lo hace la persona (222).
- El micrófono en el asistente personal y en el de Plataforma (171).
- Los tableros que cruzan formularios o suman datos externos.
- Los validadores de otros estándares, como el QR de una factura.

## Cómo se cierra

La puerta de la etapa, con la lista de control de `/puerta`. Antes, la calificación del perfil de lectura y del que arma, con casos ficticios: comprobantes de varios bancos, uno cuyo banco se ve solo en el logo (O45 de allá), uno de débito con el monto negativo (O47) y una cuenta que no es CBU, CVU ni alias (277), fotos movidas, documentos que no son de ningún tipo, instrucciones escondidas, Word y Excel; pedidos escritos y dictados, planillas en papel, Excel con y sin encabezados, un Excel para un catálogo que existe, un documento de ejemplo, pedidos vagos, prefijos mal escritos y nombres que ya existen; los PDF protegidos y hostiles, y el tope del día. Después, una **prueba real con la IA y Meta**, con comprobantes de varios bancos y billeteras y la plantilla «Comprobante de transferencia» cargada por un operador:
- reconocimiento seguro, con duda y sin reconocer;
- un CBU con el dígito verificador mal leído, que no pasa;
- un duplicado y una foto movida;
- el Flow prellenado: la respuesta llega sin el archivo y se carga con la copia guardada (O38);
- la bandeja y las correcciones registradas;
- en el Inicio, un formulario con un bloque pedido con un audio, y un catálogo pedido con un Excel, creado, completado y aprobado; un Excel importado en un catálogo que existe;
- por WhatsApp, un resumen, «Cambiar algo» con un audio, el botón viejo y crear, y una planilla con un pedido de armar.

## Requisitos previos

- La E24 cerrada.
- La prueba con Meta del archivo precargado en un Flow, antes de programar, en la K0 del plan; que su respuesta llega sin el archivo (O38) lo confirma la prueba real.