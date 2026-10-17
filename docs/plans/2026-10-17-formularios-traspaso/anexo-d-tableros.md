# Tableros del lienzo «Formularios por WhatsApp» (texto de sus notas)

## Formularios por WhatsApp, en una mirada — `FWA-Idea.dc.html`
**Formularios por WhatsApp, en una mirada.** El administrador arma un formulario una vez. Los empleados lo completan desde WhatsApp, sin entrar a la web. Cada envío queda guardado como una fila, con su aprobación y su PDF.

## Formularios · diseñar: listado — `FWA-Listado.dc.html`

## Formularios · editor — `FWA-Editor.dc.html`

## Formularios · publicar en WhatsApp — `FWA-Publicar.dc.html`

## Formularios · en WhatsApp — `FWA-WhatsApp.dc.html`
**En el celular del empleado.** Juan Gómez trabaja en la calle. No abre la web: le escribe al WhatsApp de la empresa, como siempre. El formulario de la visita aparece adentro del chat.

## Formularios · respuestas — `FWA-Respuestas.dc.html`

## Formularios · una respuesta — `FWA-Respuesta.dc.html`

## Formularios · versiones — `FWA-Versiones.dc.html`
**Cambiar un formulario sin perder nada.** Cada vez que publicás, el formulario guarda una versión nueva. Cada respuesta queda atada a la versión con la que llegó, así que cambiar las preguntas nunca rompe lo que ya está cargado.
- **Agregar una pregunta:** Las respuestas anteriores la muestran vacía («—»). No se inventa un cero ni un «no».
- **Cambiarle el nombre:** Si «Importe cobrado» pasa a llamarse «Monto», la planilla muestra el nombre nuevo. El PDF ya emitido conserva el de su momento.
- **Quitar una pregunta:** Deja de pedirse. Lo que ya se respondió sigue guardado y se ve al abrir cada respuesta.
- **Cambiar el tipo:** De texto a número, por ejemplo: es una pregunta nueva. Lo viejo queda como estaba; convertirlo es una tarea aparte.

## Formularios · sugerir otro formulario — `FWA-Sug-Idea.dc.html`
**Un formulario puede sugerir otro.** Los formularios siguen siendo independientes. Lo que se configura es qué otro formulario conviene ofrecer después, en qué situación y con qué datos ya cargados. El asistente decide si corresponde; la persona elige.
- **Registrar entregaFormularioClienteFecha de entrega¿Se entregó completo?Bultos entregadosObservacionesCuando el cliente pida la facturaLa IA decidePasa: cliente, número de entrega y bultosFacturar entregaFormularioClienteEntrega asociadaBultos a facturarForma de pagoImporte totalSolo si «¿Se entregó completo?» es NoPreguntar siemprePasa: cliente, número de entrega y observacionesReclamo de entregaFormularioClienteEntrega asociadaQué faltóSi el cliente comenta cómo le fueLa IA decidePasa: clienteEncuesta de satisfacciónFormularioCliente¿Cómo fue la entrega? 1Se guarda la respuesta:** Juan manda «Registrar entrega». Queda guardada como siempre.
- **El asistente mira:** La respuesta, lo que Juan escribió y las sugerencias configuradas para ese formulario.
- **Ofrece con un botón:** Solo si encaja con el «cuándo», o siempre si es «Preguntar siempre». Juan elige.
- **La nueva recuerda de dónde vino:** La factura queda unida a la entrega. Así se arma la cadena.
- **Siguen siendo formularios separados:** Cada uno tiene sus preguntas, versiones, reglas y planilla. La sugerencia solo los conecta.
- **El asistente solo recomienda:** Ofrece formularios que la persona puede completar. Nunca envía ni guarda solo.
- **Sin IA también funciona:** «Preguntar siempre» ofrece el botón cada vez que se cumple la condición.

## Formularios · configurar sugerencias — `FWA-Sug-Editor.dc.html`

## Formularios · sugerencia en el celular — `FWA-Sug-Celular.dc.html`
**Una entrega que termina en factura.** Dos formularios separados. El asistente conecta uno con otro porque así está configurado y porque encaja con lo que dijo Juan.

## Formularios · la cadena en la web — `FWA-Sug-Respuesta.dc.html`

## Formularios · etapa 1: el editor — `FWA-E1-Editor.dc.html`

## Formularios · etapa 1: mandar desde el chat — `FWA-E1-Chat-Enviar.dc.html`

## Formularios · etapa 1: la respuesta en el chat — `FWA-E1-Chat-Respuesta.dc.html`

## Formularios · etapa 1: en el celular — `FWA-E1-Celular.dc.html`
**Etapa 1 · En el celular de Juan.** Sin asistente y sin automatismos: el operador le manda el formulario desde el chat y Juan lo completa dentro de WhatsApp.

## Formularios · E1 · A · listado — `FWA-E1-Listado.dc.html`

## Formularios · E1 · B · nuevo formulario — `FWA-E1-Alta.dc.html`

## Formularios · E1 · C · prefijo — `FWA-E1-Prefijo.dc.html`
**C · El prefijo de numeración y sus estados.** Es el mismo campo del editor (e1-1). Se puede cambiar hasta que se acepta la primera publicación; desde ahí queda fijo y explica por qué.
- **Sugerido:** Se propone a partir del nombre mientras nadie lo toque.
- **Editado por la persona:** Si lo cambiás, deja de seguir al nombre: renombrar el formulario ya no lo pisa.
- **Ocupado:** Lo decide el servidor al guardar. El error va junto al campo y el resto de los cambios se conserva.
- **Fijo, publicado:** Las respuestas ya llevan ese número: no se cambia nunca más.
- **Fijo aunque la publicación falló:** Se fijó al aceptar el pedido de publicar. Si Meta falla y se reintenta, conserva el mismo prefijo.
- **Se fijó mientras editabas:** Otra persona inició la publicación. Al guardar, el resto de tus cambios se conserva y el prefijo muestra el valor actual.
- **El ejemplo no reserva nada:** ENT-000001 ilustra cómo se va a ver. El número real lo asigna el servidor al guardar cada respuesta.
- **El bloqueo lo informa el servidor:** No se deduce de que el formulario figure como publicado: llega como dato, también si la publicación falló.
- **No cambia solo:** Recuperar un guardado o renombrar el formulario nunca reemplaza un prefijo que la persona eligió.

## Formularios · E1 · D · conflicto — `FWA-E1-Conflicto.dc.html`

## Formularios · E1 · E · dos idiomas — `FWA-E1-Idiomas.dc.html`

## Formularios · E1 · P1 · publicar — `FWA-E2-Publicar.dc.html`

## Formularios · E1 · P1 · antes de publicar — `FWA-E2-Antes.dc.html`
**P1 · Antes de publicar: lo que puede mostrar la confirmación.** Variantes del diálogo «Publicar en WhatsApp». Los textos de los motivos los traduce el servidor; acá van de ejemplo.
- **Comprobando:** Al abrir, el servidor revisa el borrador guardado. No llama a WhatsApp ni guarda nada.
- **Hay cambios sin guardar:** Se publica solo lo guardado: primero se guarda, después se comprueba. Decidido el 2026-10-10 (D4).
- **Una pregunta no se puede mostrar:** WhatsApp no podría mostrarla. Se dice cuál y por qué, y no se publica nada.
- **Versión siguiente:** El resumen compara con lo publicado. Lo quitado no borra las respuestas que ya llegaron.
- **La comprobación venció:** Vale 10 minutos. Pasado ese tiempo hay que comprobar de nuevo; no se publica con datos viejos.
- **Cambió después de comprobar:** Otra persona guardó mientras la confirmación estaba abierta. Se revisa como en el tablero D.
- **Ya se está publicando:** Una publicación por formulario a la vez. Se puede seguir editando mientras tanto.
- **WhatsApp no está listo:** Falta configurar o verificar la cuenta de la empresa. No es un problema del formulario.
- **Comprobar no publica:** Revisa el borrador guardado, arma el resumen y avisa lo que WhatsApp no podría mostrar. No toca WhatsApp ni asigna números.
- **Los motivos van donde se arreglan:** Si el problema es de una pregunta, se nombra la pregunta y se lleva hasta ella. Si es de la cuenta, se dice que no es del formulario.
- **Nada se publica con dudas:** Comprobación vencida, cambios de otra persona o una publicación en curso: se vuelve a comprobar antes de aceptar.

## Formularios · E1 · P2 · publicando — `FWA-E2-Publicando.dc.html`

## Formularios · E1 · P2 · cómo termina — `FWA-E2-Resultado.dc.html`
**P2 · Cómo termina una publicación.** Los resultados posibles en el mismo editor y en el listado. Los motivos de WhatsApp los traduce el servidor; acá van de ejemplo.
- **Publicada:** WhatsApp la aprobó y quedó activa. Recién ahí cambia la etiqueta y aparece el aviso.
- **WhatsApp rechazó una pregunta:** El motivo va junto a la pregunta. Se corrige, se guarda y se publica de nuevo.
- **Falló sin culpa de una pregunta:** WhatsApp no respondió o devolvió un error general. Reintentar retoma el mismo pedido.
- **Sin confirmar:** No se sabe todavía si WhatsApp la recibió. Se sigue consultando el mismo pedido, sin mandarlo otra vez.
- **Necesita revisión:** Tras varios intentos no se pudo saber qué pasó. Las acciones de revisión se definen antes de programar esta parte.
- **En el listado:** Una sola etiqueta por formulario, más una línea cuando la última publicación no terminó bien.
- **La versión anterior sigue andando:** Mientras se publica y si falla. Nadie se queda sin formulario por un rechazo de WhatsApp.
- **Nunca se publica dos veces:** Reintentar y consultar retoman el mismo pedido. Recargar la pantalla no manda nada.
- **Los números no se reciclan:** Si la versión 2 no llegó a publicarse, queda en la historia y la siguiente es la 3, como pasa con las respuestas. Según el contrato propuesto.

## Formularios · E1 · P3 · vista previa — `FWA-E2-Vista.dc.html`
**P3 · Vista previa orientativa y vista oficial de WhatsApp.** La columna derecha del editor. La vista propia ayuda mientras se edita; la oficial la arma WhatsApp con la versión publicada.
- **Sin publicar:** Solo la vista propia, que sigue al borrador mientras se edita.
- **Publicado: el borrador:** Con una versión publicada aparece el selector. Arranca en el borrador.
- **Cargando la oficial:** El servidor pide a WhatsApp un enlace temporal de la versión publicada.
- **La vista oficial:** La muestra WhatsApp, dentro de la página. Es la versión publicada, no el borrador.
- **Venció:** WhatsApp da el enlace por un rato. Al vencer se pide otro; no se guarda.
- **No se puede mostrar acá:** Si la página no puede incrustarla o WhatsApp no responde. Sin permiso o sin versión publicada, no hay selector.
- **Nunca se llama oficial a la vista propia:** La propia dice siempre «orientativa». «Oficial» es solo la que muestra WhatsApp.
- **El enlace es temporal:** No se guarda en el navegador, no se registra ni se comparte. Se pide de nuevo cuando hace falta.
- **La oficial muestra lo publicado:** Para ver el borrador como lo vería WhatsApp, hay que publicarlo: la vista oficial no previsualiza borradores.

## Formularios · E1 · C1 · cuenta vinculada — `FWA-E3-Cuenta.dc.html`
**C1 · ¿Juan puede recibir un formulario?.** El encabezado de la conversación y el botón «Enviar formulario» según la cuenta vinculada al número. Las reglas del chat de hoy (atención manual, 24 horas, envío pendiente) siguen igual.
- **Vinculada, con correo:** El encabezado dice de qué cuenta es el número. Es quien completa el formulario.
- **Vinculada, sin correo:** Hay cuentas que entran solo con WhatsApp. Se muestra el nombre de la cuenta.
- **Sin cuenta vinculada:** Se puede seguir escribiendo. El chat no crea cuentas ni vínculos.
- **Cuenta inactiva:** La cuenta existe pero no puede usar el sistema.
- **Ningún formulario para Juan:** Depende de los permisos del rol de Juan, no de los del operador.
- **Sin permiso de responder:** Como hoy: quien solo puede ver el chat no tiene ni Enviar ni Enviar formulario.
- **El motivo, al lado del botón:** Si el chat permite escribir pero no mandar un formulario, el botón queda deshabilitado con la razón debajo. No desaparece sin explicación.
- **El chat no crea cuentas:** Vincular un número es parte del ingreso con WhatsApp. Desde acá solo se informa.
- **Decide el servidor:** Quién puede recibir y qué formularios llegan del servidor, y se vuelven a revisar al enviar.

## Formularios · E1 · C2 · elegir el formulario — `FWA-E3-Elegir.dc.html`

## Formularios · E1 · C3 · el mensaje — `FWA-E3-Mensaje.dc.html`
**C3 · El mensaje que acompaña y los dos botones.** El botón que abre el formulario y el botón final son distintos. El primero lo pone el sistema; el segundo lo define cada formulario, en los dos idiomas.
- **El mensaje en el diálogo:** Viene escrito y se puede cambiar. Se valida antes de enviar y lo vuelve a validar el servidor.
- **Lo que le llega a Juan:** Un mensaje con un solo botón: «Abrir formulario». Siempre dice eso.
- **Adentro, el botón final:** El que se definió en el formulario. Es el que guarda la respuesta.
- **Una cuenta en inglés:** El mensaje viene escrito en inglés, el botón dice «Open form» y el formulario se abre en inglés.

## Formularios · E1 · C4 · cambios — `FWA-E3-Cambios.dc.html`
**C4 · Si algo cambia con el diálogo abierto.** El operador está eligiendo el formulario y, mientras tanto, cambia algo. Los textos son los del chat de hoy cuando ya existen.
- **Se cerró la ventana de 24 horas:** Pasó el plazo con el diálogo abierto. Se avisa al enviar o antes, si el chat ya lo sabe.
- **Te sacaron el permiso:** El servidor rechaza el envío. No se muestra nada más del formulario.
- **La conversación dejó la atención manual:** Otra persona cambió el modo. Mandar formularios, como escribir, es de atención manual.
- **Empezó otro envío:** Alguien mandó un mensaje desde otra pantalla. Se espera a que se confirme.
- **El número cambió de cuenta:** El vínculo se revocó o pasó a otra cuenta. No se manda a quien no corresponde.
- **El formulario dejó de estar disponible:** Se despublicó o Juan perdió el permiso para completarlo. Se elige otro; nunca se reemplaza solo.
- **Lo escrito no se pierde:** El mensaje del diálogo y el borrador del chat quedan como estaban, aunque no se pueda enviar.
- **El servidor revisa al enviar:** Permiso, modo, ventana, vínculo y formulario se comprueban de nuevo en el momento de mandar.
- **Nunca se manda dos veces:** Con un envío pendiente o sin confirmar en la conversación, no se puede mandar otro mensaje ni otro formulario.

## Formularios · E1 · C5 · estado del envío — `FWA-E3-Estado.dc.html`
**C5 · Después de tocar «Enviar formulario».** El formulario enviado en la conversación, con los estados y textos del chat de hoy.
- **Guardando el pedido:** El botón queda ocupado. El pedido lleva su identificador desde el primer toque.
- **En cola:** El pedido quedó guardado y se cierra el diálogo. Todavía no salió hacia WhatsApp.
- **Aceptado, entregado, leído:** Los mismos estados que un texto. Aceptado por WhatsApp no quiere decir que Juan lo vio.
- **No se pudo enviar:** WhatsApp lo rechazó. No se reintenta solo: se puede mandar uno nuevo.
- **Envío sin confirmar:** No se sabe si salió. El chat queda bloqueado hasta revisar el teléfono, como con un texto.
- **No se sabe si se guardó:** Se cortó antes de la respuesta del servidor. Se consulta ese mismo pedido antes de repetirlo.
- **La misma cola que un texto:** Un formulario es un mensaje más de la conversación: mismo orden, mismos estados y el mismo bloqueo mientras hay uno pendiente.
- **Nada se repite solo:** Ni el pedido ni el mensaje. Repetir usa el mismo identificador y el mismo contenido.
- **Revisado no es entregado:** «Ya revisé el WhatsApp» habilita seguir escribiendo; no marca el formulario como entregado ni lo manda de nuevo.

## Formularios · E1 · R1 · la respuesta llega — `FWA-E4-Llega.dc.html`
**R1 · La respuesta llega al chat.** Lo que ve el operador desde que Juan toca el botón final hasta que la respuesta queda guardada, y los casos en que no se guarda.
- **Guardando:** Juan tocó «Confirmar visita». La respuesta llegó y se está guardando: todavía no se muestran valores.
- **Guardada:** Recién cuando quedó guardada aparecen los valores y el número. El número confirma que existe.
- **No se pudo guardar:** Las respuestas no coinciden con lo publicado o falta algo obligatorio. No se guarda nada a medias.
- **No reconocida:** Llegó una respuesta que no corresponde a ningún envío vigente (vencido o de otro número). No se guarda ni se muestran valores.
- **Llegó dos veces:** WhatsApp puede repetir un aviso. Se guarda y se muestra una sola vez, con el mismo número.
- **En la lista de chats:** La vista previa dice qué pasó, sin valores, en el mismo lugar donde hoy se ve el último mensaje.
- **Guardada solo si se guardó:** «Guardada como» aparece después de confirmar el guardado. Antes, la tarjeta dice que se está guardando.
- **Nada automático para Juan:** En la Etapa 1 nadie le contesta solo. El operador ve qué pasó y le escribe si hace falta.
- **Una respuesta, una vez:** Cada envío admite una sola respuesta. Una repetición no crea otra ni cambia el número.

## Formularios · E1 · R2 · permisos — `FWA-E4-Permisos.dc.html`
**R2 · Quién ve lo que respondió Juan.** La misma respuesta para un operador que puede ver respuestas y para uno que solo puede ver el chat.
- **Con permiso de ver respuestas:** Ver el chat y ver respuestas son dos permisos distintos. Con los dos, la tarjeta completa.
- **Solo con permiso de ver el chat:** Se sabe que Juan respondió y con qué número, pero no qué respondió.
- **Se lo sacaron con el chat abierto:** Al actualizar, los valores se borran de la pantalla y de lo guardado en el navegador. Sin avisos que repitan valores.
- **Lo que nunca lleva valores:** La lista de chats, los avisos y los nombres para lectores de pantalla dicen solo el formulario y el número.
- **Dos permisos:** Ver el chat (whatsapp) y ver respuestas (formrecords.read). El segundo se pide aparte y se revisa en cada carga.
- **Sin valores, sin rastro:** Si el permiso falta o se pierde, los valores no se piden, no se guardan en el navegador y no quedan en la pantalla.
- **El número sí:** Que exista la respuesta y su número no son datos privados: ayudan a coordinar sin ver el contenido.

## Formularios · E1 · R3 · valores — `FWA-E4-Valores.dc.html`
**R3 · Cómo se muestran los valores.** La tarjeta muestra cada tipo de respuesta sin perder información, con las preguntas de la versión que Juan contestó.
- **Todos los tipos:** Texto corto y largo, número, fecha, sí o no, lista y una opcional sin responder.
- **Una respuesta larga:** Se ven las primeras y el resto se abre en el lugar. La conversación no salta.
- **El mensaje ya no está:** WhatsApp o la retención borran el texto del chat; la respuesta guardada no se toca.
- **Respondió en inglés:** Preguntas y opciones en el idioma de quien mira. Lo escrito, tal como lo escribió Juan.
- **Con las preguntas de su versión:** Etiquetas y opciones como estaban al responder, aunque después se renombren.
- **Sin perder nada:** «No» y «0» son respuestas; «Sin respuesta» es otra cosa. Decimales exactos, sin redondear.
- **Fechas sin hora:** Una fecha es un día: no cambia con la zona horaria de quien mira.

## Formularios · Etapa 2 · K1 · catálogos — `FWA-K1-Catalogos.dc.html`

## Formularios · Etapa 2 · K2 · nuevo catálogo — `FWA-K2-Nuevo.dc.html`

## Formularios · Etapa 2 · K3 · escritas a mano — `FWA-K3-Manual.dc.html`

## Formularios · Etapa 2 · K4 · con filtros — `FWA-K4-Filtros.dc.html`

## Formularios · Etapa 2 · K5 · con una consulta — `FWA-K5-Consulta.dc.html`

## Formularios · Etapa 2 · K6 · elegir de un catálogo — `FWA-K6-Pregunta.dc.html`

## Formularios · Etapa 2 · K7 · enviar y responder — `FWA-K7-Envio.dc.html`
**K7 · Al mandar el formulario y al recibir la respuesta.** Las preguntas con catálogo en el chat de la Etapa 1: cómo se preparan las opciones, cuándo no se puede mandar y cómo se guarda lo que elige Juan.
- **Al mandar, las opciones se preparan:** Para Juan, en ese momento. Viajan dentro del mensaje: lo que ve es lo que había al enviarlo.
- **Un catálogo no se puede usar:** No se manda un formulario que el empleado no podría completar.
- **Demasiadas o ninguna:** Más de 200 opciones no entran en WhatsApp; ninguna deja una pregunta obligatoria sin respuesta posible.
- **Lo que ve Juan:** El texto y el detalle de cada opción, en una lista de WhatsApp.
- **Guardada con su referencia:** La respuesta guarda qué opción, de qué catálogo, y el texto de ese momento. Si después se renombra, la respuesta no cambia.
- **La opción se dio de baja antes:** Si Juan eligió algo que ya no está activo, no se guarda. Se avisa al operador y se ofrece mandarlo de nuevo con la lista actual (decidido el 2026-10-10).
- **La lista no queda en la versión:** Publicar fija la pregunta y su catálogo. Las opciones se arman en cada envío.
- **Lo que se ofreció queda registrado:** El envío recuerda qué opciones recibió Juan, para validar lo que responde.
- **Listas grandes, después:** Buscar en vivo en una lista larga necesita el intercambio de datos con WhatsApp, que llega en una etapa posterior.

## Formularios · Etapa 2B · F1 · fotos y documentos en el editor — `FWA-F1-Editor.dc.html`

## Formularios · Etapa 2B · F2 · en el celular — `FWA-F2-Celular.dc.html`
**F2 · En el celular de Juan.** Cómo saca las fotos y adjunta el remito dentro del formulario, sin salir de WhatsApp.
- **Solo cámara:** Un solo botón: la foto se saca en ese momento. Hasta tener la mínima, no puede seguir.
- **Con dos fotos:** Las ve, puede quitar una y sacar otra. Cuenta cuántas lleva.
- **El documento y el final:** Otra pantalla, porque es otro archivo. Puede llevar más preguntas y el botón final.
- **Cámara o galería:** Si la pregunta lo permite, también puede elegir una foto que ya tiene.
- **WhatsApp controla primero:** Mínimo, máximo, tamaño y tipos se validan en el teléfono antes de dejar seguir.
- **Nada se guarda antes del final:** Si Juan cierra sin tocar «Confirmar visita», no queda ninguna foto ni respuesta.
- **Los archivos viajan cifrados:** WhatsApp los guarda hasta 20 días. El sistema los baja enseguida y los guarda privados (F3).

## Formularios · Etapa 2B · F3 · la respuesta con archivos — `FWA-F3-Llega.dc.html`
**F3 · La respuesta llega con fotos y documentos.** La tarjeta del chat de la Etapa 1 (R1–R3) con archivos: mientras se bajan, guardada, sin permiso y cuando un archivo no se puede guardar.
- **Bajando los archivos:** La respuesta llegó con 3 archivos. Hasta tenerlos guardados, la tarjeta no muestra valores.
- **Guardada:** Con todos los archivos guardados aparecen los valores, las miniaturas y el número.
- **Solo con permiso de ver el chat:** Los archivos son respuestas: sin permiso de verlas, ni miniaturas ni nombres.
- **No se pudo bajar un archivo:** Después de varios intentos, WhatsApp no lo entregó. No se guarda la respuesta a medias.
- **Un archivo no es lo que dice:** El sistema revisa el contenido real de cada archivo, no solo su nombre.
- **Muchas fotos:** La tarjeta muestra las primeras y cuántas más hay. Tocar abre el visor (F4).
- **Completa o nada:** Si un archivo falla, la respuesta no se guarda. El operador ve el motivo y puede mandar el formulario de nuevo.
- **Miniaturas livianas:** La tarjeta muestra una vista reducida que se arma al guardar. La foto original se baja solo al abrirla.
- **Los archivos son respuestas:** Mismo permiso que los valores. Nunca en la lista de chats, en los avisos ni en los registros del sistema.

## Formularios · Etapa 2B · F4 · ver y descargar — `FWA-F4-Visor.dc.html`

## Formularios · Etapa 2B · F5 · límites y casos — `FWA-F5-Casos.dc.html`
**F5 · Límites y casos.** Lo que frena WhatsApp en el teléfono, lo que revisa el sistema al publicar y al guardar, y cuánto duran los archivos.
- **Más de 10 archivos:** Se revisa antes de publicar, como el resto de la comprobación (P1).
- **Cómo se arman las pantallas:** Una pregunta de archivo abre pantalla nueva; las siguientes pueden compartirla.
- **Un archivo muy grande:** WhatsApp no deja adjuntarlo. El tope lo fija la instalación, hasta 25 MB.
- **Un tipo que no se admite:** «Documento» solo deja elegir PDF, Excel, Word o imágenes. Un CSV no aparece para elegir.
- **WhatsApp tarda en entregarlo:** El sistema reintenta solo unas veces. Si no lo consigue, la respuesta queda sin guardar (F3).
- **Todavía no se pueden pedir:** Sin el almacenamiento de archivos configurado, los tipos se ven pero no se pueden elegir.
- **Se quitó el permiso con el visor abierto:** El visor se cierra y las miniaturas se quitan de la pantalla.
- **Cuánto duran:** Mientras exista la respuesta, no lo que dura el chat de WhatsApp.
- **WhatsApp controla primero:** Cantidad, tamaño y tipos, en el teléfono, antes de dejar enviar.
- **El sistema verifica después:** Huella, tamaño y tipo real de cada archivo antes de guardarlo. Si algo no coincide, no se guarda.
- **Antivirus antes de producción:** WhatsApp no garantiza que un archivo sea inofensivo. Se decide con el despliegue.

## Formularios · Etapa 3 · T1 · planilla — `FWA-T1-Planilla.dc.html`

## Formularios · Etapa 3 · T2 · filtros — `FWA-T2-Filtros.dc.html`

## Formularios · Etapa 3 · T3 · una respuesta — `FWA-T3-Respuesta.dc.html`

## Formularios · Etapa 3 · T4 · estados — `FWA-T4-Estados.dc.html`
**T4 · La planilla: estados y casos.** Los estados de la planilla y del detalle con las recetas de listados del sistema, y qué pasa cuando las preguntas cambian entre versiones.
- **Cargando:** Cabecera y filtros quedan a la vista; la tabla muestra filas de espera.
- **Todavía no hay respuestas:** Publicado pero sin respuestas. Dice cómo conseguirlas.
- **Sin coincidencias:** Con filtros que no dejan nada. Los filtros siguen visibles para cambiarlos.
- **No se pudo cargar:** Si falla una actualización con datos ya cargados, la tabla queda como estaba y aparece un aviso.
- **Muchos formularios:** El panel muestra los primeros y un enlace a la lista completa, con búsqueda.
- **Sin permiso de ver respuestas:** No aparece la sección Respuestas en el panel. Una dirección guardada lleva a «Sin permiso».
- **Una respuesta que no está:** No existe o no se puede ver: la misma página, sin decir cuál de las dos.
- **Una pregunta cambió de tipo:** Renombrar mantiene la columna; cambiar el tipo crea una pregunta nueva y otra columna. Nada se mezcla ni se convierte.
- **Nada se inventa:** «—» si la pregunta no existía, «Sin respuesta» si no se contestó. Nunca un cero o un vacío en su lugar.
- **El servidor filtra y cuenta:** Filtros, conteos, orden y página se resuelven antes de traer las filas; el navegador no carga todo para filtrar.
- **Los permisos primero:** Quien no puede ver respuestas no ve la sección, ni cuántas hay, ni sus números.

## Formularios · Etapa 3 · T5 · exportar a Excel — `FWA-T5-Exportar.dc.html`

## Formularios · Etapa 4 · G1 · reglas — `FWA-G1-Reglas.dc.html`

## Formularios · Etapa 4 · G2 · diseño del PDF — `FWA-G2-Pdf.dc.html`

## Formularios · Etapa 4 · G3 · por aprobar — `FWA-G3-PorAprobar.dc.html`

## Formularios · Etapa 4 · G4 · decidir — `FWA-G4-Decidir.dc.html`

## Formularios · Etapa 4 · G5 · historial — `FWA-G5-Historial.dc.html`

## Formularios · Etapa 4 · G6 · celular — `FWA-G6-Celular.dc.html`
**G6 · Lo que recibe el empleado.** Los mensajes que mandan las reglas, sin asistente: confirmación al recibir, aviso con el PDF al aprobar y el motivo al rechazar.
- **Al recibirse:** Si una regla lo pide, Juan recibe el número enseguida.
- **Al aprobarse:** El aviso y el PDF, dentro de las 24 horas desde su último mensaje.
- **Al rechazarse:** Con el motivo que escribió quien la rechazó.
- **En inglés:** Para quien tiene la cuenta en inglés: los mismos mensajes, en su idioma, y el PDF también.
- **Mensajes que manda una regla:** Salen por la misma cola del chat, con sus estados. El operador los ve en la conversación como mensajes del sistema.
- **Las 24 horas siguen valiendo:** Fuera de ese plazo, WhatsApp no deja escribir sin una plantilla aprobada por Meta. Hasta que lleguen las plantillas, la acción queda «No se pudo» y el PDF sigue guardado.
- **Nada se repite:** Cada mensaje lleva el identificador de su acción: reintentar no lo manda dos veces.

## Formularios · Etapa 5 · H1 · modo Asistente — `FWA-H1-Asistente.dc.html`

## Formularios · Etapa 5 · H2 · sugerencia al operador — `FWA-H2-Sugerencia.dc.html`

## Formularios · Etapa 5 · H3 · modos — `FWA-H3-Modos.dc.html`
**H3 · Cambiar de modo.** Cómo se pasa una conversación a Asistente y cómo se vuelve a Atención manual, con los casos que hay que cuidar.
- **Pasar a modo Asistente:** Con el permiso nuevo «Configurar el asistente», que tiene Admin. Se confirma, porque el asistente va a escribir solo.
- **Tomar atención manual a mitad de un envío:** El envío que ya empezó termina; después el asistente no manda nada más.
- **El asistente no está disponible:** Sin una conexión de IA configurada, o si el módulo no está instalado, no se ofrece el modo.
- **Los chats que ya existen:** Lo que hoy está en «Automatización sin configurar» pasa a Atención manual. Nadie queda en modo Asistente sin que alguien lo elija.
- **En la lista de chats:** Una marca dice qué conversaciones atiende el asistente.
- **Sin permiso para cambiar el modo:** Se ve el modo, pero no el botón. Escribir sigue dependiendo del permiso de responder.
- **Siempre se puede frenar:** Tomar la atención manual corta al asistente en el momento; solo termina lo que ya estaba saliendo.
- **Nada se activa solo:** Ninguna conversación pasa a Asistente por una actualización o por defecto: alguien lo elige.
- **Una sola cola:** Operador, asistente y reglas escriben por la misma cola, con los mismos estados y el mismo bloqueo.

## Formularios · Etapa 5 · H4 · catálogo propuesto — `FWA-H4-Catalogo.dc.html`
**H4 · Un catálogo que propone el asistente.** Quien administra pide un catálogo en sus palabras; el asistente arma la configuración y la deja para revisar en la web.
- **Lo pide por WhatsApp:** Ana administra los catálogos y le escribe al asistente desde su número vinculado.
- **Lo revisa en la web:** La misma pantalla de K4, marcada como propuesta. Se puede ajustar antes de aprobar.
- **El asistente propone, una persona aprueba:** Una propuesta no se usa en ningún formulario hasta que la aprueba alguien con permiso de administrar catálogos (y el especial, si es una consulta).
- **Solo sobre lo permitido:** Arma filtros o una consulta sobre las vistas de catálogo. Nunca ejecuta nada fuera del usuario de solo lectura ni inventa datos que no existen.
- **Queda registrado:** El catálogo guarda que lo propuso el asistente, quién lo pidió y quién lo aprobó.

## Formularios · Etapa 5 · H5 · límites — `FWA-H5-Limites.dc.html`
**H5 · Los límites del asistente.** Qué hace el asistente cuando no está seguro, cuando no corresponde y cuando no puede.
- **No está seguro:** Pregunta antes de mandar. Con botones de respuesta rápida, sin formulario todavía.
- **Una consulta o una charla:** No contesta: solo actúa cuando alguien quiere registrar algo. La conversación queda sin leer para un operador.
- **Después de un «ahora no»:** No insiste con lo mismo. Ofrece formularios hasta 2 veces por día en cada conversación.
- **No hay formulario, o no puede usarlo:** Le avisa y anota el pedido (H7). Un formulario que esa cuenta no puede completar cuenta como que no existe: no lo nombra.
- **Fuera de las 24 horas:** Si Juan no escribió en las últimas 24 horas, el asistente no puede escribirle primero. Responde cuando Juan escribe.
- **Todo queda registrado:** En la respuesta guardada: que el formulario lo mandó el asistente, por qué y qué datos propuso.
- **Solo con «Cuándo lo ofrece»:** El asistente ofrece un formulario si tiene completado «Cuándo lo ofrece el asistente» (editor 03). Sin eso, no lo conoce.
- **Nunca guarda solo:** Manda, sugiere o pregunta. La respuesta la confirma siempre la persona en el formulario.
- **Las sugerencias entre formularios:** Después de una respuesta puede sugerir el siguiente (s1–s4), con las mismas reglas.

## Formularios · Etapa 5 · H6 · audios — `FWA-H6-Audios.dc.html`
**H6 · Audios en el chat.** Juan puede contar lo que hizo en un audio. Se baja, se pasa a texto y el asistente lo lee como si lo hubiera escrito; el operador ve el audio y su transcripción.
- **Llega un audio:** Se baja de WhatsApp como las fotos de la 2B y se pasa a texto. Mientras tanto ya se puede escuchar.
- **Transcripto:** El texto aparece debajo del audio, para el operador y para el asistente. También en atención manual.
- **En modo Asistente, lo lee como texto:** Lo trata como si Juan lo hubiera escrito: reconoce la visita y manda el formulario prellenado. Juan igual revisa y confirma.
- **En atención manual, sugiere:** Con la transcripción, el asistente le sugiere al operador qué mandar, como con un mensaje escrito.
- **No se pudo pasar a texto:** Sin transcripción (falla o no hay proveedor configurado), el asistente no lo interpreta: queda sin leer para un operador.
- **Cuánto dura:** El audio y su transcripción siguen la retención de los mensajes de WhatsApp, no la de las respuestas.
- **Un audio es un mensaje:** Lo escucha quien puede ver el chat. Ni el audio ni su texto van a los avisos, a la lista de chats ni a los registros del sistema.
- **La transcripción no decide:** Es texto para el asistente: lo que proponga, Juan lo revisa y lo confirma en el formulario.
- **Un proveedor calificado:** Se transcribe con el proveedor que se elige y califica al implementarlo. Sin él, los audios quedan para el operador.

## Formularios · Etapa 5 · H7 · formularios que faltan — `FWA-H7-Faltan.dc.html`

## Formularios · detalle · D1 · pestañas con barra de estado — `FWA-D1-Pestanas.dc.html`

## Formularios · detalle · D2 · panel lateral — `FWA-D2-Panel.dc.html`

## Formularios · detalle · D3 · el comprobante primero — `FWA-D3-Comprobante.dc.html`

## Formularios · detalle · D4 · la historia como columna — `FWA-D4-Historia.dc.html`
