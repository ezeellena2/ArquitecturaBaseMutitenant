# E14 · Formularios: crear, publicar, mandar y recibir (spec)

**Estado:** pendiente · spec de la etapa 14 del bloque (decisiones 34 y 78), con las decisiones 35, 38, 43, 79, 80 y 103. Nace con la pregunta «Elegir de un catálogo», sobre los catálogos de la E12 y sus campos de la E13. `/plan-etapa` la convierte en plan cuando le toque, después de verificar los puntos marcados **[E11]**, **[E12]**, **[E9]** y **[Meta]** contra el código real y la documentación vigente.

**Fuentes:**
- alcance y decisiones de producto: [hoja de ruta](../2026-10-12-formularios-dinamicos-hoja-de-ruta.md), §E14 y §5;
- reglas: [AGENTS.md](../../../AGENTS.md) y las [fichas](../../rules/README.md);
- conocimiento de Meta: ArquitecturaBase ([§14](14-parte-archivos-tests-y-meta.md#14-referencia-de-meta-tomada-de-arquitecturabase)). Es solo referencia, no código para copiar.

**Requisitos:** la E13 cerrada (con ella, el módulo `Forms`, los catálogos y sus campos), la E11 (números por organización, bandejas, token por organización) y la E9 (Redis, límites y fuzz). Para la prueba con Meta, una organización con su número conectado (la I1 de la 10).

**Partes.** Esta spec no entra en un solo archivo, así que sigue en otros, con los mismos números de sección. Lo que no está en la tabla está acá.

| Secciones | Archivo |
| --- | --- |
| §2, §6 y §6b | [decisiones técnicas y flujos](14-parte-decisiones-y-flujos.md) |
| §3, §4, §5 y §7 | [datos y contrato](14-parte-datos-y-contrato.md) |
| §8, §10 y §14 | [archivos, tests y Meta](14-parte-archivos-tests-y-meta.md) |

## Qué leer

Antes de empezar:
1. **Lo anterior:** las specs de la [E12](12-catalogos.md) y la [E13](13-campos.md), sobre todo sus datos, su contrato y sus archivos.
2. **Decisiones** de la hoja de ruta: las de la §13 de esta spec.
3. **Fichas:** [guardado](../../rules/guardado.md), [concurrencia](../../rules/concurrencia.md), [idempotencia](../../rules/idempotencia.md), [multitenancy](../../rules/multitenancy.md), [persistencia-ef](../../rules/persistencia-ef.md), [auditoria](../../rules/auditoria.md), [api-http](../../rules/api-http.md), [permisos](../../rules/permisos.md), [paginado-y-busqueda](../../rules/paginado-y-busqueda.md), [result-y-errores](../../rules/result-y-errores.md), [validacion](../../rules/validacion.md), [textos-libres](../../rules/textos-libres.md), [textos-y-traducciones](../../rules/textos-y-traducciones.md), [numeros-y-moneda](../../rules/numeros-y-moneda.md), [fechas-y-zonas](../../rules/fechas-y-zonas.md), [datos-personales](../../rules/datos-personales.md), [logs](../../rules/logs.md), [cuotas](../../rules/cuotas.md), [modulos](../../rules/modulos.md) y [tests](../../rules/tests.md). Además, cómo caen en este módulo: §11.
4. **Código para copiar:** el módulo Asistente y lo que deje la E11, pieza por pieza, en la §8.
5. **Pantallas:** los tableros de la §9.
6. **Referencia de Meta:** la §14.

## 1. Alcance

**Entra:**
- **«Formularios»** (79): una sola lista, con «Nuevo formulario». Cada formulario se abre con sus pestañas, y esta etapa trae «Diseño»; lo que ve cada persona depende de sus permisos.
- **Editor:** formularios de la organización con siete tipos de pregunta, entre ellos «elegir varias» (38), títulos de sección entre las preguntas (88), prefijo de numeración y vista previa en el teléfono. El contenido es un solo texto por campo, tal como lo escribe la persona, en el idioma que quiera (35).
- **Publicación en WhatsApp:**
  - un Flow por versión y por cuenta de WhatsApp Business (WABA);
  - operación durable, con sus estados visibles y la vista previa oficial de Meta.
- **Envío desde el chat:** «Enviar formulario» en un chat de Internos (a un empleado) o de Clientes (a un cliente), con la sesión de 24 horas. Cada formulario dice para quién es: solo empleados, solo clientes, o los dos (49 y 50). El mensaje sale por la cola de WhatsApp y, si no se sabe si salió, se reintenta solo, como los demás (80).
- **Recepción:**
  - el `nfm_reply` se convierte en una respuesta numerada (`VIS-000123`), sin duplicados;
  - en el chat aparece una tarjeta con los valores, o solo con el número si quien mira no puede ver respuestas.
- **La pregunta «Elegir de un catálogo»** (78): «elegir una» y «elegir varias» toman sus opciones de la pregunta o de un catálogo de la E12 (a mano, de los miembros o de los contactos). Ver la §6b.
  - **la presentación:** la pregunta usa la del catálogo (E13) o tiene la suya, que se publica con su versión;
  - **al publicar:** un catálogo a mano con más de 200 opciones activas en «elegir una», o más de 20 en «elegir varias», no se publica, porque nunca se podría mandar;
  - **al mandar:** las opciones se arman para cada destinatario y viajan dentro del mensaje. No se puede mandar con más de 200 opciones en «elegir una», con más de 20 en «elegir varias», ni sin ninguna si la pregunta es obligatoria;
  - **al responder:** la opción tiene que estar en la oferta, y se sellan el texto, el detalle, el código, el valor y los campos que vio la persona. Si era una opción a mano dada de baja, no se guarda nada, el operador ve el motivo y puede mandar de nuevo;
  - **en los catálogos:** «Lo usan» en el listado y en el catálogo, sin la cantidad en el encabezado de la sección. Dado de baja, un catálogo no se elige en preguntas nuevas, y los formularios publicados que lo usan no se pueden mandar: la confirmación de la baja lo advierte. Un campo que usa la presentación propia de una pregunta no se quita, y el error dice qué formulario y qué pregunta.
- **El módulo** (nace en la E12):
  - los permisos de formularios;
  - los cuatro puertos neutrales de los formularios, con sus implementaciones apagadas;
  - el retiro probado una vez, con los catálogos de la E12 y la E13.

**No entra:**
- dar de baja, reactivar, borrar y deprecar los Flows (E17);
- los datos de las personas y los filtros por destinatario (E16);
- fotos y documentos (E18);
- las respuestas: pestañas «Respuestas» y «Tablero», planilla, detalle y Excel (E15);
- reglas, aprobación, PDF y avisos (E21);
- biblioteca de plantillas y «Desde una plantilla» (E22);
- el asistente (E23);
- copiar formularios (E17);
- condiciones, cálculos y líneas (E19);
- cargar desde la web (E20);
- límites por plan.

## 9. Pantallas (tableros)

**Navegación (34, 78 y 79):** el menú principal dice «Gestión de formularios», y su panel ya tiene «Catálogos» (E12). Esta etapa suma «Formularios»: una sola lista, con «Nuevo formulario», y cada formulario se abre con su pestaña «Diseño». No existe «Diseñar formularios». Los tableros de la etapa muestran solo lo que existe hasta ella.

**Aprobados y publicados en el lienzo v141** (2026-10-25, front `93cb7d01`), con sus teléfonos, sin idioma en el contenido y con los botones de la decisión 43. Reemplazan a los de la v139:
- **`FRM-Formularios`:** la lista «Formularios» (79), una tabla con Formulario, Preguntas, En WhatsApp y Último cambio (fecha y hora completas), el filtro «Estado ▾» y «Nuevo formulario». El nombre abre el formulario con su pestaña «Diseño». Reemplaza a `FRM-Listado`;
- **`FRM-Editor`:** el editor en una sola pantalla:
  - el formulario: nombre, «Para quién es» (empleados, clientes o los dos; 50), texto del botón final y prefijo, cada uno en su campo;
  - las preguntas, que se abren en el lugar, con ⋮ (Editar, Subir, Bajar y Quitar);
  - «elegir varias», con el mínimo y el máximo (en el ejemplo, «Fallas», de 1 a 3);
  - la pregunta «De un catálogo»: el selector, «Permitir elegir varias» con mínimo y máximo, «varias, hasta N» en la lista de preguntas, un catálogo de más de 20 y la presentación de la pregunta (la del catálogo o la suya);
  - el teléfono al costado.

  En el teléfono, la pregunta se edita en una hoja y los botones van fijos abajo.

  **Se redibuja con los títulos de sección (88):** «Agregar título» en el editor, el título entre las preguntas con su ⋮ (Editar, Subir, Bajar y Quitar) y el subtítulo en el teléfono al costado, en `FRM-Editor` y `M-FRM-Editor`.
- **`FRM-Catalogos-14`:** el listado de catálogos de la E12 con la columna «Lo usan».
- **`FRM-Catalogo-14` y `M-FRM-Catalogo-14`** (por dibujar): el catálogo, con la sección «Lo usan», «Quitar» apagado en un campo en uso, con el motivo en su tooltip, y la advertencia al darlo de baja.
- **`Campana-14`** (por dibujar): «WhatsApp rechazó la versión N de …», que lleva al formulario.
- **`WA-Mensajes-14`:** «Mis mensajes», con la tarjeta de una respuesta propia de un cliente que vinculó su cuenta.
- **`FRM-Publicar`:** «Publicar la versión N en WhatsApp», con qué cambia y qué queda fijo.
- **`FRM-Publicando`:** cómo termina, arriba del editor, con los pasos Aceptada, Enviada a WhatsApp, WhatsApp la revisa y Lista para mandar. Al costado, la vista oficial con «Borrador | Versión N en WhatsApp».
- **`WA-Chats-Formulario` y `M-WA-Chats-Formulario`:** «Enviar formulario» al lado de «Enviar» en Internos (en el teléfono, en el ⋮ del chat). El diálogo «Enviar un formulario a …» tiene una tarjeta por formulario, con la versión y la cantidad de preguntas cada una en su lugar (con buscador y paginado si son muchos), la cantidad de opciones por pregunta de catálogo y los problemas de la oferta, el mensaje prellenado de hasta 1024 caracteres y hasta cuándo se puede mandar, con fecha y hora. Sin «Ya lo revisé» ni «Resolver el envío» (80).
- **`WA-Chats-Formulario-Cliente` y `M-WA-Chats-Formulario-Cliente`** (nuevos): «Enviar formulario» en un chat de Clientes, con solo los formularios para clientes o para los dos (49 y 50). En el ejemplo, «Entrega de pedido» es para los dos.
- **`WA-Chats-Respuesta` y su teléfono:** la tarjeta con las primeras 3 respuestas en grilla, «Ver las N respuestas» (en el teléfono, en una hoja) y «Guardada como VIS-000123»; el valor de catálogo con su código, y el rechazo de una opción dada de baja con «Mandar de nuevo».

**Sin rastro de etapas posteriores:** los tableros de la v141 no tienen «4 reglas · Revisión 3» ni «Más de 10 archivos». `Rol` suma los cinco permisos de formularios.

**Estados que se dibujan** (cada uno tiene que poder producirse; si no, es un bug):
- **Editor:** con cambios, guardando (el botón grisado con su mismo texto), guardado, no se guardó (lo escrito se conserva), otra persona guardó (con «Qué cambió | Guardado actualmente | Mis cambios»), prefijo ocupado, para quién es, quitar una pregunta, cambiar el tipo, descartar, solo lectura, cargando y no existe.
- **Publicar:** versión nueva, primera publicación, comprobando, con cambios sin guardar (publicar grisado, con el motivo en su tooltip), una pregunta que no se puede mostrar, un catálogo demasiado grande, cambió después de comprobar, ya se está publicando, sin número conectado, sin cambios, con una cuenta conectada que no tiene el formulario (103), y un título sin preguntas debajo, con «Ir al título» (T24).
- **Cómo termina:** publicando, publicado, rechazada con «Ir a la pregunta», sin confirmar con «Consultar ahora» (nunca «Reintentar»), necesita revisión, con «Salió» y «No salió», y la vista oficial. En el teléfono, «Sin confirmar» y «Necesita revisión» abren su detalle en una hoja.
- **Enviar:**
  - En cola, Leído, y No se pudo mandar (con ⓘ, sin reintento);
  - con el diálogo abierto, ventana cerrada, formulario que ya no está y formulario que no está publicado para este número (103).
- **Al mandar una pregunta de catálogo:** la cantidad de opciones por pregunta; en rojo y con el envío grisado: más de 200 (o 20 en varias), menos que el mínimo, ninguna en una pregunta obligatoria, un catálogo que no se puede usar o una opción que no entra.
- **Respuesta:**
  - guardando, no se pudo guardar, no reconocida y sin permiso;
  - el mensaje ya no está;
  - con «elegir varias»;
  - una opción dada de baja no se guarda y ofrece «Mandar de nuevo»;
  - venció sin respuesta.

**Mandan sobre esta spec en:**
- **Las fichas del front** (pedido del usuario del 2026-10-25): un dato por celda, en un renglón; las fechas completas, con fecha y hora en la zona de quien mira, nunca «hoy», «ayer» ni «hace…»; sin rótulos que expliquen, así que la vista previa es el teléfono al costado; y «Dados de baja», cuando llegue, es una opción del filtro «Estado».
- **Textos:** la lista se llama «Formularios», la pestaña del editor «Diseño», y la acción del chat es «Enviar formulario» (79).
- **Botones (43):** «Guardar cambios» se habilita cuando hay cambios, sin «Cambios sin guardar». Mientras trabaja, queda grisado con su mismo texto. Si falta un dato obligatorio, el botón sigue habilitado y al tocarlo se marca el campo.
- **Si no se sabe si algo salió (43):** ante un corte de red al guardar o al mandar, la pantalla vuelve a leer sola y muestra lo que quedó, sin pisar lo escrito:
  - **al guardar:** relee el borrador y compara **solo el contenido**, sin las identidades que emite el servidor. Si es igual a lo mandado, quedó guardado y el editor toma la versión y las identidades de esa relectura; si no, Guardar sigue habilitado con lo escrito y la versión de antes. Nunca reintenta solo. Tomar la versión de una relectura es una excepción a [concurrencia](../../rules/concurrencia.md), que se escribe en la ficha de formularios del front;
  - **al mandar:** relee el chat. Si la sesión existe, se ve el envío; si no, se puede mandar de nuevo con la misma `Idempotency-Key`, que devuelve el resultado guardado si el primero sí llegó.
- **Envío pendiente:** un envío pendiente bloquea el chat (`WhatsApp.Chat.SendPending`). No bloquea que haya otro formulario sin responder.
- **Un envío que no se sabe si salió** lo reintenta la cola sola, como a los demás mensajes; no hay «sin confirmar» ni «Ya lo revisé» (80).
- **«Cambió después de comprobar»** es `Forms.Publication.DraftChanged` (T8).
- **«Necesita revisión»:** `FormPublication` tiene el estado `NeedsReview`. Es para cuando Meta no confirmó la creación de un Flow y la consulta no puede saber si existe (un Create sin id no se adopta por nombre). Una persona con `forms.publish` lo mira en Meta y lo resuelve.
- **«No reconocida»:** una respuesta de Flow que no corresponde a ninguna sesión se muestra en el chat como un mensaje sin valores y no crea nada en Forms.
- **La banda de conflicto** («Otra persona cambió esto mientras lo editabas.») lleva ⚠.
- **«Venció sin respuesta»:** se calcula al leer: la sesión sigue `Pending` y ya pasó `ExpiresAtUtc`. `Expired` queda para una respuesta que llegó tarde.
- **El código de la opción de un catálogo se ve en la tarjeta de la respuesta,** debajo del valor (nunca en WhatsApp).
- **Cualquier catálogo de la organización** sirve en cualquier formulario: el contenido no tiene idioma (35).

## 11. Premisas aplicadas

Rige la tabla de la [hoja de ruta](../2026-10-12-formularios-dinamicos-hoja-de-ruta.md#premisas-del-repo-aplicadas-a-formularios). Lo propio de la E14:

- **Servicios:** cada servicio tiene hasta 8 dependencias de negocio, y solo los servicios usan `IUnitOfWork`. Los helpers no abren transacciones.
- **Lo externo, fuera de la transacción:** nada de Meta adentro de una UoW. El estado `InFlight` se commitea antes de cada llamada.
- **Logs:**
  - `[LoggerMessage]`, con códigos y ids;
  - nunca el `flow_token`, los valores de una respuesta, el texto del mensaje ni teléfonos completos.
- **Textos:**
  - lo que ve una persona en WhatsApp por el sistema (el botón «Abrir formulario», la ayuda de los números y «Elegí hasta N») sale de resources es/en del módulo WhatsApp, en el idioma de su cuenta;
  - los textos del formulario son de la organización, un solo texto sin idioma (35).
- **Fechas:** `SubmittedAtUtc` y `ExpiresAtUtc` con `TimeProvider`, comparados con la hora firmada de Meta.
- **Sin datos de ejemplo:** no hay formularios sembrados.

## 12. Puntos de contacto con otras etapas

Al planificar la E14 se verifican contra el código real.

| Marca | Qué hay que confirmar | Qué asume esta spec |
| --- | --- | --- |
| [E11] | El modelo de números: `WhatsAppChannels`, la WABA de cada número y el token por organización. | Un Flow por versión y WABA. Se publica en todas las WABAs con números conectados. |
| [E11] | Conversaciones y mensajes: ids, tipos de mensaje y el contrato de la lista de mensajes. | Se suman un mensaje saliente «Formulario» y la tarjeta `form`. |
| [E11] | Reconocer al empleado y la bandeja Internos; «tomar» un chat. | Mandar cuenta como responder: toma el chat. |
| [E11] | La entrada de mensajes por organización: el contexto con `SetFromAccess`, la transacción del procesador, el paso por el bot y `WhatsAppMessage.Fail`. | La captura es una fase aparte, fuera de esa transacción y sin bot (T5), con sus reintentos en la misma cola (§16). |
| [E11] | El outbox con el número de la organización. | `WhatsAppOutboundMessage` suma la variante Flow con el `PhoneNumberId` de la organización. |
| [E11] | Las rutas de WhatsApp por bandeja (`customers` e `internal`) y el módulo de WhatsApp del front. | Los nombres de esta spec son provisorios. |
| [E11] | La copia compartida de la conversación para «Mis mensajes». | Guarda la tarjeta armada al capturar (T16). |
| [E12] | El esqueleto del módulo, los catálogos, los lectores de las fuentes «Miembros» y «Contactos», y la presentación y los campos de la E13. | `FormOfferBuilder` reusa los lectores, con una fila más que el tope. |
| [Meta] | Los límites del `Dropdown` y del `CheckboxGroup` dinámicos: cuántas opciones, el largo del título y del detalle, y el tamaño de `data` en el mensaje. | 200 opciones en `Dropdown` y 20 en `CheckboxGroup`, título ≤30 y detalle ≤300. El piloto de ArquitecturaBase confirmó 200 opciones con 136 caracteres de detalle cada una. |
| [E9] | Las cuotas compartidas ([cuotas](../../rules/cuotas.md)). | Las rutas de Forms cuentan en las cuotas generales; no se definen perfiles propios. |
| [E9] | Redis. | La E14 no cachea nada nuevo. |
| #51 | `Enter` borra `TenantKind`. | El worker de publicación usa `ExecutionActor`, y la captura, `SetFromAccess` (T5). |

## 13. Decisiones de producto de la etapa

Están en la §5 de la hoja de ruta y no se vuelven a preguntar: 86 (la unidad va en la etiqueta, sin un dato «Unidad»), 88 (títulos de sección), 9 (la respuesta guarda todas las empresas activas de quien respondió), 10 (manda cualquiera con «Atender internos», y a un cliente, con «Atender clientes»), 11, 21 y 36 (un nombre de más de 30 caracteres se corta en 29 y se le agrega «…», y el completo va primero en el detalle), 12, 22 y 37 (un catálogo con filtros que da más de 200 opciones se guarda, y el tope frena al mandar), 49 y 50 (los clientes completan los formularios que son para clientes), 35 (el contenido no tiene idioma), 38 («elegir varias» va acá), 43 (botones y avisos), 78 (la E14 nace con la pregunta «Elegir de un catálogo»), 79 (una sola lista «Formularios», con la pestaña «Diseño») 80 (lo que sale por la cola de WhatsApp se reintenta solo, sin «sin confirmar») y 103 (un número en otra cuenta habilita publicar).

## 15. Cómo se cierra

La puerta de la etapa ([etapa en paralelo](../../guides/etapa-en-paralelo.md)) tiene las suites, el E2E real, la documentación en 0, la galería, el recorrido y la lista de control de `/puerta`. Además, hay una **prueba real con Meta** con el número de una organización de prueba, abriendo cada variante en un celular:
1. **Formulario de prueba:** con los siete tipos de pregunta, entre ellos «elegir varias» con mínimo 1 y máximo 3, y con un título de sección, que se ve como subtítulo. Meta confirma `PUBLISHED`, con un solo Flow.
2. **Mandar a una cuenta en inglés:** el formulario llega como está escrito, y el botón, la ayuda de los números y «Elegí hasta N» llegan en inglés. Se abre en Android y en iPhone.
3. **Completar:** dejar vacía una opcional, poner `12.50` en el número y marcar dos opciones. En el chat aparece el número de la respuesta tras recargar, y las dos opciones quedan en su orden.
4. **Mirar lo que llega de Meta:** qué manda para una opcional vacía y para «elegir varias» sin marcar, y que `12.50` y `12,50` se guardan iguales.
5. **Una WABA sin medio de pago** (si se puede probar): queda `AccountUnavailable` visible, no «Sin confirmar» para siempre.
6. **Un catálogo a mano,** con y sin detalle, en un formulario publicado. Se manda, se responde, y la respuesta guarda el texto, el detalle, el código y el catálogo; se ven igual después de recargar.
7. **«Elegir varias» con un catálogo:** mínimo 1 y máximo 3; se marcan dos y quedan en su orden.
8. **Miembros y contactos:** un catálogo de miembros con filtros, vacío y con opciones, con «Sin incluir a quien lo recibe», y uno de los contactos para elegir a un cliente.
9. **Opción dada de baja después de mandar:** la respuesta se rechaza sin número. «Mandar de nuevo» da el número siguiente, sin saltos.
10. **El tope:** un catálogo de 200 opciones con detalle viaja y vuelve con los ids intactos.
11. **La presentación y el sellado:** «Proveedores» con el CUIT en la descripción (la del catálogo) y una pregunta con su presentación propia se ven así en el celular; al responder, los campos quedan sellados, y cambiar el CUIT en el catálogo no cambia lo respondido.
12. **Lo que no se verificó en Meta:** el tope de `data` en el mensaje, el `flow_token` de 128, el cuerpo de 1024 y un `Dropdown` con `${data.x}` sin endpoint.
13. **«Mis mensajes»:** un cliente que vinculó su cuenta ve la tarjeta de su respuesta.

## 16. Criterios

Salen de los reviews y los pilotos de ArquitecturaBase. El plan los toma como lista de control, junto con la de `/puerta`.

- **Captura:**
  - una falla técnica se reintenta hasta **5 veces y al menos 15 minutos** desde la primera, con intervalos 30, 90, 210, 450 y 930 s; recién ahí queda `Rejected` y visible. El intento vive en el mensaje entrante, en su `Attempts` y `NextAttemptAtUtc`, y lo corre la misma cola: para una respuesta de Flow, `IWhatsAppInboundService.FailAsync` usa estos intervalos en lugar de los 6·n s de hoy, que no llegan a 3 minutos;
  - un mensaje trabado no frena a los demás.
- **Catálogos al mandar:**
  - al operador le llegan solo la cantidad y los problemas, nunca los nombres de las personas ofrecidas;
  - las opciones vacías no viajan, y el texto de una opción sale de un campo que siempre tiene valor;
  - una pregunta de catálogo sin selección manda `null` y recibe la validación en su campo.
- **Estados inciertos:**
  - un error definitivo de Meta termina en «No se pudo», visible;
  - una publicación «Sin confirmar» se **consulta** y, si sigue incierta, una persona dice «salió» o «no salió»; nunca se ofrece «Reintentar» sobre lo incierto;
  - un envío del chat sale por la cola de WhatsApp, que lo reintenta sola (80).
- **Contratos:**
  - cada campo nuevo de un DTO se exporta, con su ejemplo de una corrida real, y el front lo consume con zod estricto en el mismo corte;
  - las rutas de error tienen la misma forma en los dos lados;
  - `""` no es `null`: lo que falta va como `null`, y una propiedad vacía se omite en lo que va a Meta;
  - una entrada inválida es 400, nunca 500;
  - un campo omitido no apaga nada.
- **Guardado:** ante un error de red o un 5xx, el editor relee y compara el contenido (§9); nunca reintenta solo. Solo el 409 de versión pide revisar.
- **Pantalla:**
  - **errores:** un solo dueño por error (aviso común o junto al campo, nunca los dos), y «Reintentar» solo ante errores de red;
  - **navegación:** no desmonta el editor; se conserva el contenido con `aria-busy` y el foco nunca queda en `body`;
  - **permisos:** un 403 en una acción no oculta la pantalla de lectura.
- **Privacidad:**
  - `ToString` redactado en todo modelo con valores o nombres;
  - sin `formrecords.read`, ningún valor en avisos, vistas previas, `aria-label` ni caché;
  - lo sellado (payloads cifrados) entra en la retención.
- **Meta:**
  - el perfil se versiona, y lo sellado no se recompila;
  - los IDs de pantalla son solo letras y guiones bajos;
  - 141006 cuando la WABA no tiene medio de pago, que se suma al cliente de Meta;
  - 131030 con el 9 argentino en un número de prueba.
