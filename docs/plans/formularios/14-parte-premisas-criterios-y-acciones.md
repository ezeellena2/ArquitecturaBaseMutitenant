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
| [E9] | Las cuotas compartidas ([cuotas](../../rules/cuotas.md)). | Las rutas de Forms cuentan en las cuotas generales; no se definen perfiles propios. La vista oficial y «Consultar publicación», que llaman a Meta, llevan además un tope de negocio por organización (F17 de ArquitecturaBase), como los topes de correo, sin sumar un perfil a los doce: `Forms:MetaCalls:PerOrganizationPerMinute`, 20 por defecto hasta que se mida otra cosa, mayor que cero y validado al arrancar (es de costo, así que queda en `appsettings`, 270), que da `Forms.Meta.TooManyCalls` (429 con `Retry-After`). Se cuenta en la base, compartido entre copias, nunca en un proceso. Cada organización tiene su WABA y su cupo con Meta; las consultas del worker no cuentan. |
| [E9] | Redis. | La E14 no cachea nada nuevo. |
| #51 | `Enter` borra `TenantKind`. | El worker de publicación usa `ExecutionActor`, y la captura, `SetFromAccess` (T5). |
| Costuras | El resolvedor de topes ([0069](../../decisions/0069-modulos-y-suscripciones.md) §4): cada módulo declara sus topes y el resolvedor da el de cada organización. | El tiempo para contestar (`FlowSessionHours`, con `MaxFlowSessionHours`) se declaran en su módulo y se leen por el resolvedor, nunca directo de la configuración de Plataforma; el valor de la organización se valida contra el tope que da. |

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
