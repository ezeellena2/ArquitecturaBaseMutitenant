# E18 · Fotos y documentos (spec)

**Estado:** pendiente · spec de la etapa 18 del bloque (decisiones 34, 48 y 78), con el [formato](plantilla.md) de las etapas. Corresponde a la etapa 2B de ArquitecturaBase, cerrada con Meta y Azure reales, más los archivos del chat, que allá no existen. `/plan-etapa` la contrasta con el código real cuando le toque, después de la E17.

**Partes.** Esta spec no entra en un solo archivo, así que sigue en otro, con las mismas secciones. Lo que no está en la tabla está acá.

| Secciones | Archivo |
| --- | --- |
| Contrato, Datos y Pasos | [contrato, datos y pasos](18-parte-contrato-datos-y-pasos.md) |

## Qué leer

Antes de empezar, en este orden:

1. **Lo anterior:** la [spec de la E14](14-formularios.md): T5, T21, la §6, puntos 6 a 8, la §14 y la §16; la [E15](15-respuestas.md): columnas, filtros, Excel y detalle, y [cómo se lee una respuesta](15-parte-datos-y-contrato.md#cómo-se-lee-una-respuesta-263-266-y-269); y la [E17](17-ciclo-de-vida.md): copiar.
2. **Decisiones** de la hoja de ruta: 23, 35, 42, 43, 48, 78, 133, 262 (una sola pieza para elegir), 263 (nada debajo de otro dato), 264 (el orden de las tablas), 266 (la respuesta se lee como el formulario), 269 (los principios del traspaso 17) y 270 (ninguna regla de negocio fija en el código). De [WhatsApp](../../features/whatsapp.md#decisiones-del-usuario-e11): D4 y D7.
3. **Fichas:**

   | Ficha | Por qué acá |
   | --- | --- |
   | [guardado](../../rules/guardado.md) | subir y bajar van fuera de la UoW; confirmar, después del commit |
   | [concurrencia](../../rules/concurrencia.md) | la Configuración de Formularios, con su revisión y el 409 (270) |
   | [modulos](../../rules/modulos.md) | Forms y WhatsApp, por puertos neutrales |
   | [idempotencia](../../rules/idempotencia.md) | adjuntar es un archivo: modo durable |
   | [persistencia-ef](../../rules/persistencia-ef.md) y [multitenancy](../../rules/multitenancy.md) | las tablas nuevas, y las claves de los archivos sin organización |
   | [api-http](../../rules/api-http.md) y [cuotas](../../rules/cuotas.md) | descargas por la Api; cuentan en las cuotas generales |
   | [logs](../../rules/logs.md) y [datos-personales](../../rules/datos-personales.md) | nombres y contenidos nunca van a logs, a la lista de chats ni a los avisos |
   | [textos-libres](../../rules/textos-libres.md) y [textos-y-traducciones](../../rules/textos-y-traducciones.md) | los nombres se guardan como llegan |
   | [tests](../../rules/tests.md) | qué lleva test y dónde va |

4. **Código para copiar:** la columna «Copia de» de los [pasos](18-parte-contrato-datos-y-pasos.md#pasos).
5. **Referencia de producto,** solo para leer: en el traspaso del 17/10, retirado el 2026-11-08 (`git show e9f1a599:docs/plans/2026-10-17-formularios-traspaso/`), el anexo C, «Etapa 2B» y §3.6, el anexo B, «Etapa 2B», y el anexo D (F1 a F5).

## Qué se construye

**Las preguntas:**
- **Foto:** «Cámara o galería» o «Solo cámara», en la pieza (262). **Documento:** PDF, Excel, Word, JPEG y PNG, sin elegir (133);
- mínimo y máximo por pregunta. Obligatoria quiere decir mínimo 1;
- el borrador se guarda igual, y no se publica si la suma de los máximos pasa de 10 o no hay almacenamiento. Sin almacenamiento, los tipos se ven grisados, con el motivo en su tooltip;
- **tope por archivo** (270): «Tamaño máximo de un archivo», de la Configuración de Formularios (abajo). Publicar lo sella en la versión, y la versión publicada conserva el suyo aunque la configuración cambie. La (i) de «Cuántas fotos» y «Cuántos documentos» dice el vigente, con el número como parámetro. Hasta 10 archivos y 100 MiB por respuesta, por el tope de Meta, que queda en el código;
- copiar un formulario copia estas preguntas como cualquier otra.

**La Configuración de Formularios** (270) nace acá, como la del Asistente: una página del módulo para la organización, con `forms.manage`, y otra para la plataforma, con `platform.settings.manage`. Por ahora tiene un solo valor, «Tamaño máximo de un archivo», en MB, con su (i): en la organización, de 1 a 25 y 25 por defecto, nunca más que el máximo que fija la plataforma en la suya (de 1 a 25, 25 por defecto), que la página dice. Cada una con su revisión y el 409, y se lee de la base en cada uso. Si la plataforma baja su máximo, vale el menor de los dos, sin tocar lo guardado.

**El Flow:** `PhotoPicker` y `DocumentPicker`, una pregunta de archivo por pantalla, con un perfil nuevo. Su etiqueta admite 80 caracteres, y su ayuda, 300. Los IDs de pantalla son solo letras y guiones bajos. Cada selector lleva el tope por archivo sellado en la versión (`max-file-size-kb`), así el celular lo dice al elegir (270). Las opciones de catálogo que viajan en `data` llegan a la pantalla que las usa, aunque el Flow tenga varias **[Meta]**.

**El tope de la respuesta** sube de 16 a 64 KiB en un formulario con una pregunta de archivo, como `MaxMediaResponseJsonBytes` de allá. Al publicar, cada pregunta de archivo suma al peor caso 2 bytes más 2048 por archivo de su máximo; al recibir, una respuesta que trae archivos se acepta hasta 64 KiB, y el mensaje entrante la guarda cifrada hasta 96 KiB. Sin archivos, todo sigue como en la E14.

**Responder con archivos:**
- WhatsApp baja cada archivo de la API de medios con el token de la organización, solo por HTTPS, desde hosts de Meta y sin redirecciones;
- verifica hash, tamaño y tipo real, con las dimensiones antes de decodificar, y arma la miniatura de una foto;
- guarda en `IFileStore`, y le pasa a Forms las claves en la captura. Forms confirma los archivos junto con la respuesta. **Nada queda a medias:** si algo falla, no hay respuesta ni número;
- tiene 90 s por captura y aguanta un día de caída de Meta (H11 de ArquitecturaBase): reintenta con esperas que crecen, desde 30 s y el doble cada vez, hasta una hora, y abandona solo con cinco fallas y el día cumplido. Igual lo que llega al chat con un archivo. Si sigue fallando, o si no coinciden hash, tamaño o tipo, queda rechazada y visible, nombrando la pregunta, y se puede mandar de nuevo;
- cada intento sube de nuevo: lo que no quedó confirmado se borra enseguida o lo limpia el worker.

**Verificar un archivo** es una pieza del núcleo, que usan los dos módulos:
- el tipo sale del contenido, nunca de la extensión ni de lo que declara Meta;
- se reconocen JPEG, PNG y WebP; PDF; Word, Excel y PowerPoint, viejos y OOXML; texto plano, en UTF-8 válido y sin NUL, y los audios y videos de Meta. Cada uso admite los suyos: una pregunta de documento, los de la 133, y el chat, los de abajo;
- **una foto se rechaza por su encabezado, antes de decodificar:** hasta 16.384 píxeles de lado y 32 Mi píxeles de área;
- un documento dañado o cifrado se rechaza. No se ejecuta ni se renderiza nada;
- **el nombre de descarga** lleva la extensión del tipo verificado y sin caracteres de formato, como U+202E;
- del proveedor no se confía en los tipos: el id de medio puede llegar como número, y se conserva como texto. Una opcional vacía llega omitida.

**Ver los archivos de una respuesta:**
- miniatura y original solo por la Api, con `formrecords.read` y pertenencia, con `no-store` y `nosniff`. El permiso se revisa antes y después de abrir el archivo. Perder el permiso cierra el visor;
- las fotos se ven en línea y el original se baja solo al abrir el visor. Los documentos siempre se descargan;
- si falta el archivo guardado, 404. Las miniaturas se piden solo si están a la vista.

**Lo que suma a las pantallas que ya existen:**
- **la planilla:** una columna por pregunta de archivo, con la primera miniatura y la cantidad, que abre el visor. Un documento dice su cantidad, «2 documentos», sin los tipos: una celda no resume (206). No ordena, porque tiene varios valores (264). No tienen filtro en la planilla (143); para las condiciones de las reglas, `answered` también cuenta un archivo;
- **la respuesta, como el formulario** (266): la vista rápida y la pantalla completa suman al final la zona «Fotos y documentos», sin una pestaña aparte. Cada pregunta de archivo es su control de solo lectura, a todo el renglón y con su etiqueta arriba: las miniaturas, que abren el visor, y cada documento con su nombre, que lo descarga, y su tamaño al lado, nunca debajo (263). Plegada, su banda dice cuántas se respondieron, como las demás. La actividad no suma renglones: los archivos llegan con la respuesta (267);
- **la pantalla completa** suma «Descargar ▾», con los documentos;
- **el Excel:** la cantidad y los nombres en la celda, sin los archivos;
- **la tarjeta del chat:** «Bajando los archivos» o «Reintentando», guardada, con miniaturas y «+N», o rechazada, con «Mandar de nuevo»;
- **el visor:** anterior, siguiente, tira de miniaturas y Descargar.

**Los archivos del chat** (48), en todos los chats de atención, de una organización y de Plataforma:
- **recibir:** las fotos, los audios, los documentos y los videos que llegan se bajan, se verifican igual y se ven y se descargan desde el chat. Es lo que la D7 de la E11 dejó para esta etapa. Un sticker o una ubicación siguen diciendo solo su tipo;
- **un documento** se guarda si es de los que admite Meta: PDF, Word, Excel, PowerPoint o texto. Otro tipo queda como «Mandó un documento», sin descarga;
- **mandar:** el operador adjunta una foto, un documento, un audio o un video desde el compositor, con los topes de Meta para cada tipo y un texto opcional. Se guarda, se sube a Meta y sale por la cola. Solo dentro de las 24 horas, como el texto libre; fuera, siguen las plantillas;
- pide el mismo permiso que responder en esa bandeja. El archivo sigue la retención del chat (D4);
- **no se confunde con un formulario:** los archivos de una respuesta siguen a la respuesta (23), no al chat.

## Pantallas

Aprobados y publicados en el lienzo v152, con el armado de ArquitecturaBase (133) y sus dos recorridos guiados, `fotos-y-documentos` y `archivos-en-el-chat`. Los archivos del chat no existen allá. Muestran solo lo que existe hasta acá (78). Con el traspaso 5, redibujados, aprobados y publicados en el lienzo v163: `FRM-Respuestas-18`, con su teléfono, con un solo «Filtros» en el que las fotos y los documentos no entran (143), y `FRM-Respuesta-18`, con su teléfono, y `FRM-Respuesta-Completa-18`, con el detalle alineado y la actividad en columnas fijas (139). Con el traspaso 10, redibujados, aprobados y publicados en el lienzo v168: `FRM-Respuesta-Completa-18` y `M-FRM-Respuesta-18`, con «Respuesta» y «Actividad» abiertas, sin plegar (163 y 164). Con el traspaso 18 se redibujan los de la lista de abajo de la tabla, con su decisión.

| Tablero | Qué muestra |
| --- | --- |
| `FRM-Editor-18` y `M-FRM-Editor-18` | las preguntas de foto y documento, «Cuántas fotos» o «Cuántos documentos» con su (i), y «Sin almacenamiento» |
| `FRM-Config-Org`, `M-FRM-Config-Org` y `FRM-Config-Plat`, nuevos | la Configuración de Formularios de la organización, con «Tamaño máximo de un archivo», su (i) y el máximo de la plataforma, y la de la plataforma, con ese máximo (270) |
| `FRM-Publicar-18` | «Más de 10 archivos» |
| `WA-Chats-Respuesta-18` y `M-WA-Conversacion-Respuesta-18` | la tarjeta: bajando, reintentando, con fotos y un documento, muchas fotos y los dos rechazos |
| `WA-Chats-Visor` y `M-WA-Chats-Visor` | el visor: el título, en qué foto va, la tira de miniaturas y, al pie, el nombre, el tamaño y «Descargar», igual que en la planilla (194) |
| `FRM-Respuestas-18` y `M-FRM-Respuestas-18` | la planilla con la columna de archivos, que no ordena (264), el visor y la (i) de exportar, sin filtro de archivos (143) |
| `FRM-Respuesta-18` y `M-FRM-Respuesta-18` | la respuesta, con la zona «Fotos y documentos» al final y el visor (266) |
| `FRM-Respuesta-Completa-18` | la pantalla completa, con la zona «Fotos y documentos» y «Descargar ▾» |
| `FRM-Dar-De-Baja-18` | «Se conserva» suma las fotos y los documentos |
| `WA-Chats-Adjuntos` y `M-WA-Chats-Adjuntos` | los archivos del chat: lo que llega, antes de mandar, demasiado grande, enviando, enviado, no se pudo y fuera de las 24 horas |

**Se redibujan con el traspaso 18, tanda 3** (262 a 264, 266 y 270):
- `FRM-Respuesta-18`, `M-FRM-Respuesta-18` y `FRM-Respuesta-Completa-18`: la respuesta como el formulario, con la zona «Fotos y documentos» al final y sin la pestaña «Fotos»; cada foto y cada documento en su control de solo lectura, el documento con su nombre y su tamaño al lado (263 y 266);
- `FRM-Respuestas-18` y `M-FRM-Respuestas-18`: la columna de archivos no ordena (264), y en el teléfono las fotos no van debajo de otro dato (263);
- `FRM-Editor-18` y `M-FRM-Editor-18`: el origen de la foto en la pieza (262), y la (i) de «Cuántas fotos» con el tope de la configuración (270);
- `FRM-Config-Org`, su teléfono y `FRM-Config-Plat`, nuevos (270).

**La planilla:** la foto dice su miniatura y «2 fotos», y el documento, «1 documento» (206). «Sin respuesta» si no se respondió y «—» si la pregunta no existía en esa versión. En el teléfono, la fila no la junta con otros datos: las fotos y los documentos se ven al abrir la respuesta (263). Las preguntas de archivo no aparecen en «Filtros» (143).

**El chat:** el clip va al lado de la nota interna. Abre «Mandar un archivo a …», con la vista del archivo, su nombre, su tamaño y «Texto», opcional. Si pasa el tope, lo dice debajo del archivo y «Enviar» queda grisado.

**Mandan sobre esta spec en:**
- **«Sin almacenamiento»:** Foto y Documento aparecen grisados, con el motivo en su tooltip (43), no ocultos, porque se resuelve conectando el almacenamiento.
- **«Más de 10 archivos»** es un problema de la comprobación al publicar, con el botón grisado.
- **«Un archivo no es lo que dice»** es el rechazo por tipo real, y «No se pudo bajar un archivo», el rechazo después de los reintentos. Los dos nombran la pregunta y ofrecen «Mandar de nuevo».
- **«Reintentando»** se ve mientras corren los reintentos, sin acción del operador.
- **Fuera de las 24 horas,** adjuntar queda grisado, con el motivo en su tooltip.
- **Un archivo que manda el operador** sigue la [0066](../../decisions/0066-lo-que-no-se-sabe-si-salio.md), como el texto: lo que no salió se reintenta solo, lo dudoso queda «Sin confirmar» hasta que lo revisa alguien que puede enviar en esa bandeja, y lo definitivo queda «No se pudo mandar», con su motivo debajo. Una falla al subirlo a Meta no llega al cliente: se reintenta como lo que no salió.
- **Valen las de toda la app** (160 a 164, 173 a 175, 180, 261 a 264 y 269): lo que carga no salta, decidir a propósito, ninguna elección se descarta en silencio, mucho texto con jerarquía, sin parecer plantilla y con la escala de ArquitecturaBase, el texto largo entero, el estado dicho una vez, lo que se elige en la pieza, toda columna con un dato ordena y ninguna fila lleva un botón con texto.

## Qué verificar al arrancar

| Marca | Qué hay que confirmar | Qué asume esta spec |
| --- | --- | --- |
| [E10] | El adaptador de nube de `IFileStore` y de `IFileStoreMaintenance`. | En desarrollo alcanza `LocalFileStore`; las claves son del almacenamiento y opacas. |
| [E10] | Cómo se sabe que no hay almacenamiento. | La capacidad `form-files` se apaga sin adaptador configurado. |
| [E14] | La fase de captura, la sesión, el presupuesto de 16 KiB y lo que guarda el mensaje entrante. | Se pueden sumar `Receiving` y el tope de 64 KiB con archivos, al publicar y al recibir, y la reserva de la cola, de 120 s, alcanza para los 90 s de una captura. |
| [E15] | Los nombres de las piezas de lectura y de la forma de solo lectura de una respuesta. | Los de la columna «Archivos», y `lib/recordForm.ts` admite una zona al final, después de las del autor. |
| [E11] | El mensaje `Media` del chat y el payload de `WhatsAppOutboundMessage`. | La entrada guarda el id de medio de Meta, que hoy descarta; el outbox lleva la clave del archivo. |
| [Meta] | `PhotoPicker`, `DocumentPicker` y varias pantallas con `data`, sin endpoint. | Lo de la §14 de la E14, `${data.x}` llega a cada pantalla y `max-file-size-kb` admite de 1 a 25 MB. |
| [Meta] | Los topes de cada tipo y `POST {número}/media`, contra la documentación vigente. | Foto 5 MB, audio y video 16 MB, documento 100 MB. |

## Lo que ya se pagó allá (no repetir)

Cada punto lleva su test, escrito durante la etapa:
1. **Meta exige IDs de pantalla de solo letras y guiones bajos.**
2. **El id de medio puede llegar como número JSON,** y se conserva su texto, sin convertirlo.
3. **Meta omite la propiedad de una opcional vacía:** no manda `null`, `""` ni una lista vacía.
4. **Limpieza cuadrática:** consultar por página, nunca una consulta por archivo.
5. **Las dimensiones se miran antes de decodificar.**

## Decisiones tomadas (no se vuelven a preguntar)

- **Formatos de una pregunta:** sin CSV (Meta no lo admite), video ni comprimidos.
- **Nada a medias:** si un archivo no baja, o no coincide, la respuesta queda rechazada y visible.
- **Reintentos simples:** bajar de Meta y subir son operaciones que se pueden repetir, sin protocolo durable. Meta guarda los archivos hasta 20 días.
- **Los archivos siguen a la respuesta,** sin plazo mientras exista la organización (23).
- **Sin clave determinística:** el almacenamiento elige la clave, y el diario de `IFileStoreMaintenance` cierra lo que quedó sin confirmar.
- **Un solo archivo por mensaje** en el chat, como en Meta.
- **Del traspaso 18:** la respuesta se lee como el formulario, con «Fotos y documentos» al final (266), y el tope por archivo sale de la Configuración de Formularios (270).

## Fuera de esta etapa

- Exportar los archivos y descargar todas las fotos juntas.
- El antivirus, en la lista de lanzamiento: es requisito antes de producción.
- La copia de «Mensajes» sigue diciendo el tipo, sin el archivo.
- Video, comprimidos y CSV en una pregunta.

## Cómo se cierra

La puerta de la etapa, más una **prueba real con Meta y el almacenamiento de la E10**, que queda pendiente hasta la E10 (123):
1. una respuesta con cámara, un PDF y una pregunta de archivo opcional vacía;
2. las descargas íntegras, con el tamaño y el SHA-256 iguales a los guardados;
3. un rechazo y el reenvío manual, sin saltos de numeración;
4. en un chat de Clientes, ver y bajar una foto que mandó el contacto, y mandarle un PDF y una foto desde el compositor, abriéndolos en el celular;
5. un formulario con una pregunta de catálogo y dos de archivo, cada una en su pantalla: las opciones llegan a su pantalla y la respuesta vuelve completa.

## Requisitos previos

- La E17 cerrada.
- **De la E10:** el adaptador de nube de `IFileStore`. En desarrollo alcanza con `LocalFileStore`.

## En el multitenant

- **Datos de la organización:** `FormRecordFiles`, `WhatsAppChatFiles` y `FormOrganizationSettings` son `ITenantOwned`, con RLS. Plataforma, en su esquema global, con `FormPlatformSettings`.
- **Almacenamiento:** un solo contenedor privado, sin prefijo por organización: de quién es cada archivo lo dice su metadata.
- **Workers:** la captura con archivos es la fase de la E14, sin `ExecutionActor`; la limpieza y los archivos del chat recorren las organizaciones con `TenantJobRunner`.
- **Feature `forms`:** las rutas de los archivos de las respuestas la llevan. Las del chat son de WhatsApp: van sin ella y valen también en Plataforma.
- **Permisos:** ninguno nuevo.
- **WhatsApp (E11):** la API de medios se llama con el token de la WABA de la organización.
- **Cuotas:** las lecturas cuentan en las cuotas generales.
