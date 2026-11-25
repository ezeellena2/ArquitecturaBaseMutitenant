# E14 · Formularios: premisas, contactos, criterios y acciones (spec, parte)

**Estado:** pendiente · parte de la [spec de la E14](14-formularios.md), que dice qué leer y cómo se cierra.

## 11. Premisas aplicadas

Rige la tabla de la [hoja de ruta](../2026-10-12-formularios-dinamicos-hoja-de-ruta.md#premisas-del-repo-aplicadas-a-formularios). Lo propio de la E14:

- **Servicios:** cada servicio tiene hasta 8 dependencias de negocio, y solo los servicios usan `IUnitOfWork`. Los helpers no abren transacciones.
- **Lo externo, fuera de la transacción:** nada de Meta adentro de una UoW. El estado `InFlight` se commitea antes de cada llamada.
- **Logs:**
  - `[LoggerMessage]`, con códigos y ids;
  - nunca el `flow_token`, los valores de una respuesta, el texto del mensaje ni teléfonos completos.
- **Textos:**
  - lo que ve una persona en WhatsApp por el sistema (el mensaje por defecto, el botón «Abrir formulario», la ayuda de los números y «Elegí hasta N») sale de resources es/en del módulo WhatsApp, en el idioma de su cuenta;
  - los textos del formulario son de la organización, un solo texto sin idioma (35).
- **Fechas:** `SubmittedAtUtc` y `ExpiresAtUtc` con `TimeProvider`, comparados con la hora firmada de Meta.
- **Sin datos de ejemplo:** no hay formularios sembrados.

## 12. Puntos de contacto con otras etapas

Al planificar la E14 se verifican contra el código real.

| Marca | Qué hay que confirmar | Qué asume esta spec |
| --- | --- | --- |
| [E11] | El modelo de números: `WhatsAppChannels`, la WABA de cada número y el token por organización. | Un Flow por versión y WABA. Se publica en todas las WABAs con números conectados. |
| [E11] | Conversaciones y mensajes: ids, tipos de mensaje y el contrato de la lista de mensajes. | Se suman un mensaje saliente «Formulario» y la tarjeta `form`. |
| [E11] | Reconocer al empleado y la bandeja Internos; «tomar» un chat, con `WhatsApp.Conversation.TakenByOther`, y el número sin medio de pago, con `PaymentRequired`. | Mandar cuenta como responder: toma el chat y frena con los mismos errores. |
| [E11] | La entrada de mensajes por organización: el contexto con `SetFromAccess`, la transacción del procesador, el paso por el bot y `WhatsAppMessage.Fail`. | La captura es una fase aparte, fuera de esa transacción y sin bot (T5), con sus reintentos en la misma cola (§16). |
| [E11] | El outbox con el número de la organización. | `WhatsAppOutboundMessage` suma la variante Flow con el `PhoneNumberId` de la organización. |
| [E11] | Las rutas de WhatsApp por bandeja (`customers` e `internal`) y el módulo de WhatsApp del front. | Los nombres de esta spec son provisorios. |
| [E11] | La copia compartida de la conversación para «Mensajes». | Guarda la tarjeta armada al capturar (T16). |
| [E12] | El esqueleto del módulo, los catálogos, los lectores de las fuentes «Miembros» y «Contactos», y la presentación y los campos de la E13. | `FormOfferBuilder` reusa los lectores, con una fila más que el tope. |
| [Meta] | Los límites del `Dropdown` y del `CheckboxGroup` dinámicos: cuántas opciones, el largo del título y del detalle, y el tamaño de `data` en el mensaje. | 200 opciones en `Dropdown` y 20 en `CheckboxGroup`, título ≤30 y detalle ≤300. El piloto de ArquitecturaBase confirmó 200 opciones con 136 caracteres de detalle cada una, y allá anduvo un mensaje de 34,7 KB con 331 opciones. |
| [Meta] | Que el mensaje de un Flow acepte `flow_name` en lugar de `flow_id`. Allá no se usó. | Lo acepta: lo usa un binding que una persona dio por publicado sin id (§6, punto 4), y se prueba en la §15. |
| Base de la 12 | La campana (`INotificationSource`), los botones que trabajan (43 y 115), `PageLoading` y `FormSkeleton` (160) y la escala (180). | Están en el núcleo y en el front. Forms suma `formPublicationFailed` a la campana. |
| [E9] | Las cuotas compartidas ([cuotas](../../rules/cuotas.md)). | Las rutas de Forms cuentan en las cuotas generales; no se definen perfiles propios. La vista oficial y «Consultar publicación», que llaman a Meta, llevan además un tope de negocio por organización (F17 de ArquitecturaBase), como los topes de correo, sin sumar un perfil a los doce: `Forms:MetaCalls:PerOrganizationPerMinute`, 20 por defecto hasta que se mida otra cosa, mayor que cero y validado al arrancar (es de costo, así que queda en `appsettings`, 270), que da `Forms.Meta.TooManyCalls` (429 con `Retry-After`). Se cuenta en la base (`platform.FormMetaCallCounters`, §3), compartido entre copias, nunca en un proceso. Cada organización tiene su WABA y su cupo con Meta; las consultas del worker no cuentan. |
| [E9] | Redis. | La E14 no cachea nada nuevo. |
| #51 | `Enter` borra `TenantKind`. | El worker de publicación usa `ExecutionActor`, y la captura, `SetFromAccess` (T5). |
| Costuras | `ModuleLimitResolver` e `IModuleLimitContributor` ([0069](../../decisions/0069-modulos-y-suscripciones.md) §4): cada módulo declara sus topes y el resolvedor da el de cada organización. | El tiempo para contestar (`whatsapp.flowSessionHours`, con `MaxFlowSessionHours`) se declara en `WhatsAppModuleLimitContributor` y se lee por el resolvedor, nunca directo de la configuración de Plataforma; el valor de la organización se valida contra el tope que da; en cada envío vale `ModuleLimitCeiling.Effective`, y el getter de `MaxFlowSessionHours` entra en `ModuleLimitArchitectureTests`. |

## 16. Criterios

Salen de los reviews y los pilotos de ArquitecturaBase. El plan los toma como lista de control, junto con la de `/puerta`.

- **Captura:**
  - una falla técnica se reintenta hasta **5 veces y al menos 15 minutos** desde la primera, con intervalos 30, 90, 210, 450 y 930 s; recién ahí queda `Rejected` y visible. El intento vive en el mensaje entrante, en su `Attempts` y `NextAttemptAtUtc`, y lo corre la misma cola: para una respuesta de Flow, `IWhatsAppInboundService.FailAsync` usa estos intervalos en lugar de los 6·n s de hoy, que no llegan a 3 minutos;
  - una respuesta que esta versión no entiende se deja para otra copia hasta cinco fallas y un día, sin `Rejected` antes (§6.7, ADR 0060);
  - un mensaje trabado no frena a los demás.
- **Catálogos al mandar:**
  - al operador le llegan solo la cantidad y los problemas, nunca los nombres de las personas ofrecidas;
  - las opciones vacías no viajan, y el texto de una opción sale de un campo que siempre tiene valor;
  - una pregunta de catálogo sin selección manda `null` y recibe la validación en su campo.
- **Estados inciertos:**
  - un error definitivo de Meta termina en «No se pudo», visible;
  - una publicación «Sin confirmar» se **consulta**; si la consulta no puede decidir (`RequiresReview`), pasa a «Necesita revisión» y una persona dice «Salió» o «No salió» (175). Nunca se ofrece «Reintentar» sobre lo incierto, y un pedido de publicar sin respuesta se repite solo con la misma `Idempotency-Key`;
  - un envío del chat sale por la cola de WhatsApp: lo que no se sabe si salió queda «Sin confirmar» y lo revisa una persona, sin reenviar (240).
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
  - **permisos:** un 403 en una acción no oculta la pantalla de lectura;
  - **qué se publica:** el diálogo habla del borrador guardado, nunca de lo que está en pantalla sin guardar;
  - **la unidad en la etiqueta** (86) gasta el límite: «Hectáreas aplicadas (ha)» tiene 24 caracteres, y una pregunta de número admite 20. Se prueba con ese caso (121);
  - **un solo selector de catálogo:** la pregunta usa el combo de la E13.
- **Privacidad:**
  - `ToString` redactado en todo modelo con valores o nombres;
  - sin `formrecords.read`, ningún valor en avisos, vistas previas, `aria-label` ni caché;
  - lo sellado (payloads cifrados) entra en la retención.
- **Meta:**
  - el perfil se versiona, y lo sellado no se recompila;
  - ningún texto fijo del Flow lleva una coma antes de `]` o `}`, y lo prueba un test (O29);
  - los IDs de pantalla son solo letras y guiones bajos;
  - 141006 cuando la WABA no tiene medio de pago, que se suma al cliente de Meta;
  - 131030 con el 9 argentino en un número de prueba;
  - en la vista oficial, cada `%` de `flow_action_payload` va escapado dos veces, el enlace no pasa de 6000 caracteres y nunca lleva `debug` (176).

## 17. Acciones del asistente

La E14 no declara ninguna (268, [ADR 0068](../../decisions/0068-acciones-del-asistente.md) y su [receta](../../guides/agregar-una-accion-al-asistente.md)):
- **crear y editar un formulario** es armar una definición, un árbol, que sigue con la [0057](../../decisions/0057-la-ia-propone-y-una-persona-crea.md) (`propose_form`);
- **publicar,** «Salió», «No salió» y descartar una publicación no se proponen: la IA nunca publica (0057);
- **mandar un formulario desde un chat** es un mensaje del chat, como responder: no crea, edita ni elimina un registro de una pantalla;
- **en los catálogos,** las opciones siguen con `catalogoptions.edit@1` de la E12: una opción que eligió una respuesta la frena la ruta con `ItemInUse`, y «Lo usan» solo lee.

## Textos de los errores

Los códigos de la §4, con los textos es y *en* que publica la K1 del plan:
- **`Forms.Definition`:** `NotFound`, «No encontramos el formulario.» (*We couldn't find the form.*); `PrefixTaken`, «Otro formulario ya usa ese prefijo.» (*Another form already uses this prefix.*); `PrefixLocked`, «El prefijo queda fijo al publicar.» (*The prefix is fixed once published.*); `PrefixInvalid`, «De 1 a 12 mayúsculas o números, primero una letra.» (*1 to 12 capital letters or digits, a letter first.*).
- **`Forms.Draft`:** `QuestionInvalid`, «Revisá esta pregunta.» (*Check this question.*); `TooManyQuestions`, «Hasta 40 preguntas.» (*Up to 40 questions.*); `TooManyOptions`, «Hasta 50 opciones; 20 si se eligen varias.» (*Up to 50 options; 20 for several.*); `CatalogUnavailable`, «Este catálogo no se puede usar.» (*This catalog can't be used.*); `CatalogTooLarge`, «Pasa el tope de WhatsApp.» (*It exceeds WhatsApp's limit.*); `CatalogPresentationInvalid`, «Una opción queda vacía o no entra.» (*An option is empty or too long.*).
- **`Forms.Publication`:** `Incomplete`, «Falta completar esto.» (*This is missing.*); `ChannelIssues`, «WhatsApp no puede mostrar parte.» (*WhatsApp can't show part of it.*); `InProgress`, «Ya hay una publicación en curso.» (*A publication is in progress.*); `NothingToPublish`, «No hay nada nuevo.» (*There's nothing new.*); `DraftChanged`, «El borrador cambió; revisalo.» (*The draft changed; check it.*); `ChannelUnavailable`, «No hay un número conectado.» (*No number is connected.*); `NotFound`, «No encontramos la publicación.» (*We couldn't find the publication.*); `NotResolvable`, «Ya no necesita revisión.» (*It no longer needs review.*); `NotDiscardable`, «Esta publicación ya no se puede descartar.» (*This publication can no longer be discarded.*). `Forms.Meta.TooManyCalls`, «Consultamos a WhatsApp muchas veces seguidas. Probá en un momento.» (*We asked WhatsApp too many times in a row. Try again in a moment.*). `Forms.Preview.NotAvailable`, «Esta versión no tiene vista oficial.» (*This version has no official preview.*).
- **`Forms.Record`:** `InvalidResponse`, «La respuesta no coincide.» (*The response doesn't match.*); `ReplayConflict`, «La respuesta llegó otra vez, distinta.» (*The response arrived again, different.*); `NotAllowed`, «Ya no puede responder formularios.» (*They can no longer answer forms.*); `OptionWithdrawn`, «Eligió una opción que se dio de baja.» (*They chose a deactivated option.*).
- **`Forms.Offer`:** `TooManyOptions`, «Pasa el tope de WhatsApp.» (*It exceeds WhatsApp's limit.*); `TooFewOptions`, «No llega al mínimo.» (*It's below the minimum.*); `NoOptions`, «No hay opciones.» (*There are no options.*); `CatalogUnavailable`, «El catálogo ya no se puede usar.» (*The catalog can no longer be used.*); `PresentationInvalid`, «Una opción queda vacía o no entra.» (*An option is empty or too long.*). `Forms.Catalog.FieldInUse`, «Un formulario usa este campo.» (*A form uses this field.*).
- **`WhatsApp.Flow`:** `OptionNotOffered`, «La opción no estaba en la oferta.» (*The option wasn't offered.*); `TextTooLong`, «Hasta {0} caracteres; tiene {1}.» (*Up to {0} characters; it has {1}.*); `TooManyComponents`, «Hasta 50 elementos.» (*Up to 50 items.*); `ResponseTooLarge`, «La respuesta puede pasar el tope.» (*The response may exceed the limit.*); `UnsupportedConfiguration`, «No se puede mostrar así.» (*It can't be shown this way.*); `ProviderRejected`, «WhatsApp rechazó la publicación.» (*WhatsApp rejected the publication.*); `ValidationFailed`, «WhatsApp no aceptó el formulario.» (*WhatsApp didn't accept the form.*); `AccountUnavailable`, «La cuenta de WhatsApp no está disponible.» (*The WhatsApp account isn't available.*); `PreviewUnavailable`, «WhatsApp no respondió. Probá de nuevo.» (*WhatsApp didn't respond. Try again.*); `NotPublishedForNumber`, «No está publicado para este número.» (*It isn't published for this number.*); `NumberAmbiguous`, «El número se lee de dos formas.» (*The number can be read two ways.*); `SessionExpired`, «La respuesta llegó vencida.» (*The response arrived too late.*).
- **`WhatsApp.Chat`:** `NotEmployee`, «Ya no es miembro.» (*They're no longer a member.*); `FormUnavailable`, «Ya no se puede mandar.» (*It can no longer be sent.*); `SendPending`, «Hay un envío en curso.» (*A message is being sent.*).
