# E14 · Formularios: decisiones técnicas y flujos (spec, parte)

**Estado:** pendiente · parte de la [spec de la E14](14-formularios.md), que dice qué leer y cómo se cierra.

## 2. Decisiones técnicas

Las de producto están en la hoja de ruta (§5). Estas siguen las reglas del repo y, entre varias formas, la más simple.

| # | Decisión | Por qué |
| --- | --- | --- |
| T1 | `Forms` y `WhatsApp` se hablan solo por puertos neutrales del núcleo: el de contactos de la E12 y cuatro más (§7). | `ModuleIsolationTests` prohíbe que un módulo use tipos de otro. |
| T2 | **«Enviar formulario» es de WhatsApp.** La pantalla del chat, la ruta, la sesión y el mensaje van en el módulo WhatsApp, que lista los formularios con `IStructuredFormSource`. | El que manda es el operador desde el chat. |
| T3 | El borrador usa `IVersioned` y 409 `General.ConcurrencyConflict`, sin recibos de cambios. | La edición es interna (robustez proporcional, §4 de la hoja de ruta). |
| T4 | **Un solo worker de publicación, el de Forms.** Descubre trabajos en `platform.FormPublicationWorks`, entra con `ExecutionActor` y llama a las fases de `IFormPublicationService` y, fuera de toda UoW, al canal, que coordina Meta con las fases de `IWhatsAppFlowPublicationService`. | Copia la separación entre `AssistantTurnWorker` e `IAssistantTurnService`. |
| T5 | **El worker de publicación** entra con `IExecutionActorFactory` e `IExecutionActorScope` (quien publicó), no con `ITenantScope.Enter` solo. **La captura de una respuesta no usa `ExecutionActor`.** Va en una fase aparte del procesador de entrada, fuera de su transacción, con el contexto de la organización armado con `SetFromAccess`, como la entrada de la E11; la feature se consulta con ese contexto. Un `nfm_reply` no pasa por el bot. | Con `Enter` solo, `IFeatureService` da `false`, porque `Enter` deja `TenantKind` en null (backlog #51); y `ExecutionActorFactory` no puede armar un actor sin cuenta. |
| T6 | **Un Flow por versión y por WABA.** Los dos textos del sistema que van adentro del Flow (la ayuda de los números y «Elegí hasta N») viajan en el `data` del mensaje, en el idioma de la cuenta de quien recibe; un cliente sin cuenta, en el de la organización ([textos](../../rules/textos-y-traducciones.md); 35 y 42). | El contenido no tiene idioma; los textos del sistema sí, y cambian por destinatario sin otro Flow. |
| T7 | **Lo que cambia sin que nadie edite va en otra fila.** `FormDefinitionStates` (una por formulario) guarda la versión activa, el bloqueo del prefijo y `LastRecordNumber`, sumado bajo bloqueo de esa fila al capturar, con índice único (definición, número). La fila del borrador cambia solo al editar. El prefijo es único **dentro de la organización**. | Si vivieran en la fila del borrador, cada respuesta o publicación cambiaría su `xmin` y daría un 409 falso a quien edita. Cada organización numera aparte, sin bloqueos globales. |
| T8 | **Sin ticket de comprobación:** `publication-checks` solo informa y devuelve el `contentHash` del borrador que comprobó; `publications` lo recibe, vuelve a comprobar todo adentro de su transacción y, si el borrador cambió, responde `Forms.Publication.DraftChanged`. Ninguna de las dos lleva `version`, y publicar no escribe la fila del borrador. | La comprobación final es la que vale. `VersionedContractTests` exige `VersionResponse` a todo pedido con `version`. |
| T9 | **«Número con decimales»:** cada pregunta fija sus decimales (de 0 a 6) y admite hasta 15 dígitos enteros. Al responder se lee con el idioma que guarda la sesión (207): la ayuda pide coma en español y punto en inglés, sin separador de miles (O42), y el otro separador se acepta solo si no hay duda. Lo que se lee distinto con y sin los miles («1.500» en español) se rechaza con `WhatsApp.Flow.NumberAmbiguous`, por la validación fija, y pide «1500» o «1,5», con la pregunta nombrada en el rechazo (O30); más decimales que los permitidos, también. El Flow publicado no cambia: la ayuda viaja en el `data` (T6). | ArquitecturaBase no lo probó con Meta: va a la prueba con un número conectado. |
| T10 | **Lo que recibe la persona:** el formulario y su nombre salen como están escritos. El mensaje por defecto, «Completá el formulario «Visita a cliente».» (165), el botón «Abrir formulario» y los textos del sistema del Flow van en el idioma de T6, y la sesión lo guarda. El diálogo propone el mensaje al elegir un formulario, y elegir otro cambia el nombre, pero nunca pisa lo que escribió el operador. Si no lo cambió, manda `text: null` y el servidor arma el mismo texto. | Un formulario se ve y se manda igual con el menú en cualquier idioma. |
| T11 | Los valores viven en filas tipadas (`FormRecordValues`), sin un JSON paralelo. «Elegir varias» guarda una fila por opción, con su posición. | Una sola fuente, que se puede filtrar por columna. |
| T12 | **Las capturas rechazadas no crean nada en Forms.** El rechazo queda en la sesión de WhatsApp, con su código. | La idempotencia la da el índice único `SourceKey`. |
| T13 | **«Elegir varias» es un tipo propio** (`MultiChoice`), no una marca de la lista: de 1 a 20 opciones, con `0 ≤ mínimo ≤ máximo ≤ 20` y máximo mayor que cero. Es obligatoria si y solo si el mínimo es mayor que cero. Pasar de «elegir una» a «elegir varias» crea otra identidad; cambiar el rango la conserva. Sin ninguna marcada es «Sin respuesta». | Es la regla de ArquitecturaBase (C1-04), con el tope de WhatsApp. |
| T14 | **Un mensaje sin datos no lleva `data`.** Un mapa vacío (`prefill_FORM:{}`) hace que el Flow no abra en Android. | Error ya pagado en el piloto de ArquitecturaBase. |
| T15 | **Quien recibe y quien responde es un empleado o un cliente** (49). La sesión y la respuesta guardan uno de los dos: el usuario del miembro, o el contacto de la bandeja Clientes. A un empleado se le revisan la membresía y `formrecords.create`; a un cliente, que el formulario sea para clientes. | Un cliente no tiene cuenta ni permisos: se lo reconoce por el contacto, que es su número. |
| T16 | **La respuesta de un cliente es dato de la organización** (`ITenantOwned`). Si el contacto vinculó su cuenta personal, ve la copia por la copia de la conversación de la E11, que ya es compartida: Forms no suma tablas compartidas. **En «Mensajes», en su perfil personal, ve la tarjeta con sus propias respuestas:** al capturar, WhatsApp guarda en esa copia la tarjeta que le devuelve la captura (el nombre del formulario, el número y los valores que mandó), porque el acceso personal no lee `tenant` ni tiene `formrecords.read`. Si el cliente da de baja su cuenta vinculada, la respuesta queda con el contacto, y solo desaparece la copia de «Mensajes». | La regla de datos personales y la D4 de la E11. |
| T17 | Una pregunta de elegir toma sus opciones de la pregunta o de un catálogo (`catalogId`). Cambiar el catálogo, o pasar de opciones propias a un catálogo, en una pregunta publicada crea una pregunta nueva, como cambiar el tipo. | Cambiar opciones no obliga a republicar, y las respuestas viejas no cambian de sentido. |
| T18 | **Los ids que viajan a Meta son locales a la oferta** (`o1` … `o200`). La sesión guarda a qué ítem corresponde cada uno. También en «elegir varias». | No se mandan a Meta ids internos ni de personas, y «estar en la oferta» se comprueba solo. |
| T19 | **La oferta la arma Forms y la guarda WhatsApp,** cifrada en la sesión (`ProtectedOffer`, texto que devuelve `IPayloadProtector`). Se borra una vez procesada o vencida (§6, punto 9). | Forms conoce los catálogos, y WhatsApp, la sesión; va cifrada porque tiene nombres de personas. |
| T20 | Para un catálogo a mano, la captura comprueba que la opción siga activa; si no, `Forms.Record.OptionWithdrawn`. Para uno con filtros alcanza con que esté en la oferta. | Con filtros, lo que vio la persona es lo que vale. |
| T21 | El Flow usa un perfil con datos iniciales. Cada pregunta de catálogo es un `Dropdown` o un `CheckboxGroup` con `data-source: ${data.field_<N>}`, y la pantalla declara su `data` con un `__example__` **[Meta]**. | Permite mandar opciones distintas a cada persona sin un endpoint de intercambio. |
| T22 | **La presentación** arma el título y la descripción de cada opción al mandar: la del catálogo (E13) o la propia de la pregunta, que se guarda en el borrador y se sella con la versión. Lo que queda vacío o no entra frena la comprobación de publicar y el envío (36). Un campo que usa la presentación de una pregunta del borrador o de la versión activa no se quita (`Forms.Catalog.FieldInUse`): en el modal «Campos de …» de la E13, «Quitar campo» abre «Quitar «…»», que dice dónde se usa, con «Ir a la pregunta». La captura lee los campos activos de las opciones elegidas, por lote y bajo sus bloqueos, y sella identidad, clave, nombre, tipo y valor. La tarjeta del chat no cambia. | Editar el catálogo después no cambia lo respondido. |
| T23 | **Un catálogo que nunca se podría mandar no se publica:** uno a mano con más de 200 opciones activas en «elegir una», o más de 20 en «elegir varias» (`Forms.Draft.CatalogTooLarge`). Los de miembros y contactos se comprueban al mandar, porque dependen de quien recibe y cambian solos. | Si no, se publicaría y nunca se podría mandar. |
| T24 | **Un título de sección es un elemento del borrador, no una pregunta** (88): tiene un solo texto de hasta 80 caracteres y su lugar entre las preguntas, se sella con la versión y no cuenta en el tope de 40 preguntas. No tiene valor ni guarda nada al responder. En WhatsApp sale como subtítulo de la pantalla (`TextSubheading`) y cuenta en el tope de 50 componentes **[Meta]**. Para publicar, tiene texto y al menos una pregunta debajo. Hay hasta 40. Sin títulos, la lista se omite, así el borrador, la versión y el Flow salen con los mismos bytes. | Es la regla de ArquitecturaBase; Meta aceptó el subtítulo el 2026-11-06. Un elemento aparte evita filas vacías en las respuestas. |
| T25 | **Publicar por zonas** (112). El diálogo arma con `publication-checks` zonas con líneas entre filas, sin descripción ni párrafos. **«Problemas»** aparece solo si hay alguno: cada fila dice qué es, el detalle e «Ir a la pregunta». **«Qué se publica»** va siempre: el borrador guardado, las preguntas agregadas, cambiadas y quitadas respecto de la versión publicada y, mientras está libre, el prefijo que se fija. Salen «Qué queda fijo» y «Qué va a pasar». **«Publicar» apagado** dice su motivo en el tooltip: el problema, una publicación en curso o sin número conectado. | Lo eligió el usuario entre tres tableros (112). |
| T26 | **El límite, en concreto** (112 y 121). Cada problema de largo de `ChannelIssues` trae `limit` y `length`, medido por el canal en UTF-16 sobre el borrador guardado. La fila dice «Hasta 20 caracteres; tiene 26.» y, si no es el título, empieza con «La ayuda: …» o «La opción «…»: …». El editor no tiene contador. | El límite es del canal: Forms no lo conoce y el front no mide por su cuenta. |

## 6. Flujos, con sus transacciones

«UoW» es una `ExecuteInTransactionAsync(…, CommitPolicy.OnSuccess, ct)`. Cada método público de servicio va en `OperationLog.RunAsync`. Las escrituras de definición pertenecen a `IFormDefinitionService`; las de publicación, a `IFormPublicationService`. Los workers coordinan las fases, y los adaptadores neutrales delegan en los servicios del módulo; ninguno abre una UoW ni recibe `IUnitOfWork`.

1. **Crear.**
   - Afuera de la UoW se valida el nombre.
   - En la UoW: se propone el prefijo libre, se crea la definición con el borrador vacío y se responde 201.
2. **Guardar el borrador.**
   - Afuera: se validan el pedido y las reglas de las preguntas.
   - En la UoW:
     - se carga la definición con la versión esperada;
     - si el contenido no cambió, se escribe solo `ModifiedAtUtc`, para comparar la versión ([concurrencia](../../rules/concurrencia.md));
     - si el prefijo cambió, se comprueba que no esté bloqueado y que esté libre (el choque del índice se traduce a `PrefixTaken`);
     - se emiten las identidades nuevas y se guarda.
   - Responde 200 `{version, questions}`.
3. **Comprobar.** Es solo lectura:
   - reglas de Forms y textos obligatorios;
   - `IStructuredFormChannel.Check(StructuredFormVersion)`, síncrono y puro: sin escrituras, sin bloqueos y sin Meta;
   - `HasConnectedNumberAsync`, sin Meta: sin un número conectado, `ChannelUnavailable`;
   - `HasAccountWithoutFlowAsync`, sin Meta, que dice si hay algo para publicar aunque el borrador no haya cambiado (103);
   - la diferencia con la versión activa.
4. **Publicar.**
   - **En la UoW del pedido:**
     - bloqueo de la fila de estado de la definición (T7);
     - el borrador tiene el `contentHash` comprobado; si no, `DraftChanged` (T8);
     - se comprueba otra vez todo lo del punto 3;
     - que no haya otra publicación abierta;
     - se sella la `FormVersion` (número = última + 1);
     - se crean `FormPublication` (`Accepted`) y `FormPublicationWorks`;
     - se bloquea el prefijo en la fila de estado.
   - Responde 202. El pedido lleva `[Idempotent]`: repetirlo con la misma `Idempotency-Key` («Repetir el mismo intento», 175) devuelve la misma publicación.
   - **El worker** (`Infrastructure/Modules/Forms/FormPublicationWorker.cs`, modelo `AssistantTurnWorker`) hace tres pasos:
     1. busca trabajos listos y entra con `ExecutionActor` (el operador que publicó, revalidado: si perdió `forms.publish` antes de tocar Meta, la publicación termina `Failed`). Llama a `IFormPublicationService.ClaimAsync`, que reserva en su única UoW y pasa a `Preparing`;
     2. fuera de toda UoW llama a `IStructuredFormChannel.PublishAsync`:
        - el canal arma el Flow de la versión para cada WABA con números conectados **[E11]**;
        - por cada uno sigue los pasos Create → Upload → Validate → Publish → Verify;
        - el adaptador `WhatsAppStructuredFormChannel` llama a `IWhatsAppFlowPublicationService.BeginStepAsync` para reservar el binding/paso y guardar `InFlight` en su única UoW; hace la llamada a Meta después del commit y entrega el resultado a `CompleteStepAsync`, que abre otra UoW;
     3. entrega lo que devolvió el canal a `IFormPublicationService.CompleteAsync`, que en su única UoW guarda el nombre del Flow (`FlowName`) y aplica:
        - `Published` → `ActivationPending`, y se activa la versión en la fila de estado, en la misma UoW → `Published`;
        - `Pending` → `Publishing`, con `NextCheckAtUtc`: Meta la está revisando;
        - `Uncertain` → `Unconfirmed`, con `NextCheckAtUtc`;
        - `Uncertain` con `RequiresReview` → `NeedsReview`, sin `NextCheckAtUtc`. El canal lo marca cuando su verificación no puede decidir: un Create incierto que no dejó `MetaFlowId` (nunca se adopta por nombre) o un Flow que no coincide con lo publicado (dueño o asset);
        - rechazo definitivo → `Failed`, con sus `issues`. Una versión se publica en todas las WABAs o en ninguna: si una rechaza, queda `Failed`, y el Flow que haya quedado en otra no se usa.
   - **Cuando quedó `Publishing` o `Unconfirmed`,** el worker vuelve a consultar con `GetPublicationAsync`, que solo verifica y nunca repite un POST, con una espera que crece: 15 s, el doble cada vez, hasta 15 minutos (F3 de ArquitecturaBase). Una consulta que vuelve con `RequiresReview` la deja `NeedsReview`.
   - **«Consultar publicación»** hace esa misma consulta, en `IFormPublicationService.CheckAsync`, y aplica el resultado con `CompleteAsync`.
   - **«Salió» y «No salió»,** solo para una publicación `NeedsReview` (175), en `IFormPublicationService.ResolveAsync`:
     1. comprueba `forms.publish` y que siga `NeedsReview`;
     2. fuera de toda UoW llama a `IStructuredFormChannel.ResolvePublicationAsync`, y WhatsApp guarda lo que dijo la persona en los bindings que no quedaron publicados, en la única UoW de `IWhatsAppFlowPublicationService.ResolveAsync` y sin llamar a Meta. Con «Salió» quedan `Published`: uno sin `MetaFlowId` manda el formulario con `flow_name`, el nombre del Flow, único en la cuenta y fijado antes del Create (165), en lugar de `flow_id` **[Meta]**, y su vista oficial responde `Forms.Preview.NotAvailable`. Con «No salió» quedan `Failed`;
     3. en su única UoW, bajo el bloqueo de la fila de estado, la lleva a `Published`, activando la versión, o a `Failed`, y guarda quién lo dijo.

     Sin `version`: lo protege el estado. Lo que no está `NeedsReview` da `NotResolvable`, también la segunda de dos a la vez. Si se cae entre los pasos 2 y 3, repetir lo mismo termina el trabajo, y lo contrario da `NotResolvable`.
   - **«Descartar esta publicación»** (225), solo para una `Unconfirmed`, en `IFormPublicationService.DiscardAsync`: comprueba `forms.publish` y, en una UoW bajo el bloqueo de la fila de estado, la lleva a `Failed` con quién la descartó, sin llamar a Meta. Sin `version`, como «Salió» y «No salió»: lo protege el estado, y el worker, que escribe la fila en cada consulta, no da un 409 falso. Los Flows que hayan quedado no se usan, como con una WABA que rechaza. Lo que no está `Unconfirmed` da `NotDiscardable`. `InProgress` trae el `publicationId` de la abierta, así otra persona llega al descarte.
   - **Si se cae a mitad:** la reserva vuelve a tomar el trabajo, y el canal, al ver un `InFlight`, verifica en vez de repetir.
5. **Vista oficial** (176). Solo lee y no guarda nada:
   - Forms arma las opciones de ejemplo de la versión sellada con `FormOfferBuilder`: las primeras 20 activas de cada pregunta de catálogo, en orden y con los filtros fijos del catálogo; ninguna si quien mira no tiene `formcatalogs.read`, y de los miembros y los contactos, la lista que vería quien mira si la recibiera;
   - `IStructuredFormChannel.GetPreviewAsync` le pide a Meta la URL de la vista (§14), y `WhatsAppFlowPreviewLinkBuilder` le suma `interactive=true`, `flow_action=navigate` y `flow_action_payload` con la primera pantalla y su `data`: las opciones de ejemplo y los textos del sistema, en el idioma de quien mira. Sin `debug`, que muestra nombres técnicos y JSON;
   - el enlace escapado mide hasta 6000 caracteres: si no entra, cada lista lleva menos opciones. Meta decodifica `flow_action_payload` dos veces, así que en ese JSON cada `%` va como `\u0025`;
   - toda la cadena del canal (la cuenta, la verificación del Flow y la vista) tiene un tope de 30 s: si Meta no contesta, responde `WhatsApp.Flow.PreviewUnavailable`, y cerrar el diálogo cancela el pedido;
   - devuelve el enlace con su vencimiento, y el front lo usa tal cual en el marco de 480 × 840.
6. **Mandar desde el chat** (módulo WhatsApp).
   - **Ubicación:** en escritorio, el botón «Enviar formulario» al lado de «Enviar»; en el teléfono, la opción «Enviar formulario» en el ⋮ del chat. Ambos abren el mismo diálogo. No se muestra si la acción no aplica al acceso, bandeja o permiso, o si Forms no está disponible: `IStructuredFormSource.IsAvailableAsync` da `false` sin el módulo o con `forms` apagada.
   - **Cuándo se puede usar:**
     - en Internos, quien escribe es miembro activo y el operador tiene «Atender internos» (decisión 10) **[E11]**;
     - en Clientes, el operador tiene «Atender clientes» **[E11]**;
     - la ventana de 24 horas está abierta;
     - el número está disponible y tiene medio de pago (si no, `WhatsApp.Conversation.PaymentRequired`) **[E11]**;
     - el chat no lo tomó otra persona (si no, `WhatsApp.Conversation.TakenByOther`, como al responder) **[E11]**;
     - no hay otro envío pendiente: en cola, en curso o esperando su reintento; uno «Sin confirmar» no cuenta (240);
     - `IStructuredFormSource.ListSendableAsync(recipient)` devuelve al menos un formulario. Forms filtra los publicados para ese tipo de destinatario (50) y, a un empleado, los que puede responder (`formrecords.create`).
   - **Si no se puede usar:** el botón y la opción del ⋮ quedan grisados, con el motivo en su tooltip (43).
   - **Mandar:**
     - afuera de la UoW se valida el texto, que es opcional: `null` arma el mensaje por defecto (T10), y si viene, mide hasta 1024. Se lee el formulario elegido con `FindSendableAsync`;
     - en la UoW: bloqueo de la conversación, se revalidan ventana, empleado, envío pendiente y que el chat no sea de otra persona, se toma el chat si no estaba tomado (regla de la E11), se crea la sesión (token aleatorio de 32 bytes; se guarda solo el hash), que vence según el tiempo para contestar que lee en ese momento de la Configuración de WhatsApp de la organización (270), y se encola el mensaje interactivo de tipo Flow por el outbox (`WhatsAppOutboundMessage` con su variante nueva);
     - el outbox lo manda: lo que seguro no salió se reintenta solo, y lo que no se sabe si salió queda «Sin confirmar» para una persona, como los demás mensajes (240, ADR 0066). La sesión sigue abierta hasta que vence. La cuenta de Meta la verifica solo el worker, antes de mandar, nunca el pedido de «Enviar» (H33).
   - **Si la WABA del número no tiene Flow de esa versión:** `WhatsApp.Flow.NotPublishedForNumber`.
7. **Recibir y capturar** (worker de WhatsApp, siguiendo la entrada de la E11 **[E11]**).
   - **Webhook:** guarda el mensaje con `ProtectedFlowResponse`, sin duplicados por `wamid`.
   - **El procesador de entrada,** por cada mensaje, aislando los errores mensaje por mensaje:
     1. saca el hash del `flow_token` y busca la sesión;
     2. compara la hora firmada de Meta con `ExpiresAtUtc`: si se pasó, `Expired`;
     3. decodifica con el `FieldMap` del binding (§14); los arrays de «elegir varias» van a la captura, nunca a la descarga de medios;
     4. en una fase aparte (T5), llama a `IStructuredRecordSink.CaptureAsync`. Las escrituras previas de reserva o vencimiento son fases de `IWhatsAppFlowReplyService.ClaimAsync`, con una única UoW; el procesador no las abre.
   - **La captura en Forms:** `FormRecordSink` delega en `IFormRecordService.CaptureAsync`, que abre su única UoW:
     - busca el `SourceKey`: si ya existe con el mismo contenido, devuelve el mismo número; si el contenido es otro, `ReplayConflict`;
     - a un empleado le revalida la membresía y `formrecords.create`; a un cliente, que el formulario siga siendo para clientes;
     - valida estricto contra la versión sellada;
     - bloquea la fila de estado de la definición y suma `LastRecordNumber` (T7);
     - guarda la respuesta y sus valores.
   - **Después:** `IWhatsAppFlowReplyService.CompleteAsync` abre su única UoW en WhatsApp y marca la sesión `Completed` (con número) o `Rejected` (con código). Si el contacto vinculó su cuenta, guarda en la copia compartida de la conversación la tarjeta armada para «Mensajes» (T16).
   - **Si se cae entre las dos UoW:** se repite y devuelve el mismo número.
   - **Lo que esta versión no entiende** ([ADR 0060](../../decisions/0060-dos-versiones-conviven.md)): una respuesta con un perfil o un `FieldMap` que esta copia no conoce no es una falla técnica ni se rechaza: vuelve a la cola para otra copia, con una espera que crece, y queda `Rejected` solo con cinco fallas y un día cumplido desde que llegó. La falla técnica sigue con sus 15 minutos (§16).
8. **Tarjeta en el chat.**
   - Al listar los mensajes, WhatsApp llama a `IStructuredRecordSource.ReadAsync(recordIds, culture)`, con el idioma de quien mira para los textos del sistema («Sí», «No»).
   - Forms decide con los permisos de quien mira: con `formrecords.read` devuelve los valores, y sin él solo el número.
   - En la lista de chats, la vista previa del formulario la arma WhatsApp con el nombre que guarda la sesión y su estado: «Vos: Formulario · Visita a cliente» al mandar, «No se pudo mandar un formulario», «Envió un formulario · guardando», «Respondió Visita a cliente» y, si no se guardó, «No pudimos guardar un formulario» o «Llegó un formulario que no se guardó». Va en un renglón, cortada con «…» (188), y nunca lleva valores.
9. **Retención y baja.**
   - La retención de WhatsApp borra `ProtectedFlowResponse` cuando el mensaje ya se procesó; la respuesta y su número quedan (decisión 23).
   - El mensaje «Formulario» y la tarjeta de la copia vinculada siguen la retención de los chats que elige la organización (90 días por defecto, 272), cifrados ([ADR 0065](../../decisions/0065-contenido-del-chat-cifrado.md)). Lo que una etapa siguiente sume a la sesión, como la ubicación (230), se vacía al cerrar la captura o al vencer el envío (H13 de ArquitecturaBase).
   - **`ProtectedOffer`** vive en una tabla de la organización, que esa retención no ve. Lo limpia `WhatsAppFlowSessionCleanupWorker`, que recorre las organizaciones con `TenantJobRunner` y borra la oferta de las sesiones procesadas y de las vencidas.
   - `Application/Modules/Forms/Services/FormsAccountDeletionParticipant` decide las reglas dentro del alcance y la UoW de la baja, sin abrir otra ([datos-personales](../../rules/datos-personales.md)). Copia `Application/Modules/Assistant/Services/AssistantAccountDeletionParticipant.cs`: usa un puerto `IFormsAccountDeletionCleanupRepository`, cuya implementación en Infrastructure hace solo el cleanup EF y exige la transacción del llamador. Lo protege `AssistantMaintenanceLayerTests`, que se extiende para Forms. `ChoiceLabel` puede ser el nombre de otra persona (un catálogo de miembros), y la regla lo contempla.

## 6b. La pregunta de catálogo

Cambia los flujos 2, 3, 6 y 7 para las preguntas con `catalogId`.

1. **Guardar y comprobar:**
   - `PUT …/draft` rechaza solo un catálogo que no existe o es ajeno. Uno dado de baja, o que no se puede usar, se guarda y frena en `publication-checks`: si no, una baja impediría guardar cualquier otro cambio;
   - cambiar el catálogo de una pregunta ya publicada emite un `FieldId` nuevo (T17);
   - la presentación propia de la pregunta que usa un campo quitado o inexistente se guarda y frena al publicar, con `QuestionInvalid` en su pregunta;
   - `IStructuredFormChannel.Check(StructuredFormVersion)` valida el perfil con datos (T21); un catálogo a mano que nunca se podría mandar no se publica (T23), ni una presentación que no entra (T22).
2. **Mandar:**
   - `FindSendableAsync` de Forms resuelve, para quien recibe, la oferta de cada pregunta de catálogo, con la presentación de la pregunta o la del catálogo. Lee los catálogos a mano activos o la fuente con los filtros (los lectores de la E12), con una fila más que el tope; es solo lectura.
   - **Al operador** le llega solo la cantidad de opciones por pregunta y los problemas, nunca los nombres. Las opciones completas quedan en la sesión.
   - Si una pregunta tiene un problema, no se puede mandar.
   - **Nombres largos:** un nombre de las fuentes «Miembros» o «Contactos» de más de 30 caracteres se corta en 29 más «…», y el completo va primero en el detalle (decisiones 11, 21 y 36).
   - **En la UoW de WhatsApp:**
     - se numeran las opciones `o1` a `oN` (T18);
     - se arma `flow_action_payload.data` con `{id: localId, title, description?}`, sin `description` si no hay detalle;
     - se guarda `ProtectedOffer` en la sesión y se encola el mensaje.
3. **Recibir:**
   - el verificador mira que cada `localId` elegido esté en la oferta (si no, `OptionNotOffered`), y lo traduce a `{catalogId, itemId, text, detail, code, value}`;
   - la captura de Forms, en su UoW, comprueba con un catálogo a mano que la opción siga activa, bajo el mismo bloqueo con que se edita: si no, `OptionWithdrawn`, sin respuesta ni número (T20). Sella los campos de la opción (T22);
   - después WhatsApp marca la sesión `Rejected` con ese código, y la tarjeta ofrece «Mandar de nuevo», que abre el mismo diálogo de envío.
