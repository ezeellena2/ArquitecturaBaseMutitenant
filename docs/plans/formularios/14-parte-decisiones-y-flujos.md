# E14 · Formularios: decisiones técnicas y flujos (spec, parte)

**Estado:** pendiente · parte de la [spec de la E14](14-formularios.md), que dice qué leer y cómo se cierra.

## 2. Decisiones técnicas

Las de producto están en la hoja de ruta (§5). Estas siguen las reglas del repo y, cuando hay más de una forma, la más simple.

| # | Decisión | Por qué |
| --- | --- | --- |
| T1 | `Forms` y `WhatsApp` se hablan solo por puertos neutrales del núcleo: el de contactos de la E12 y cuatro más (§7). | `ModuleIsolationTests` prohíbe que un módulo use tipos de otro. |
| T2 | **«Enviar formulario» es de WhatsApp.** La pantalla del chat, la ruta, la sesión y el mensaje van en el módulo WhatsApp, que lista los formularios con `IStructuredFormSource`. | El que manda es el operador desde el chat. |
| T3 | El borrador usa `IVersioned` y 409 `General.ConcurrencyConflict`, sin recibos de cambios. | La edición es interna (robustez proporcional, §4 de la hoja de ruta). |
| T4 | **Un solo worker de publicación, el de Forms.** Descubre trabajos en `platform.FormPublicationWorks`, entra con `ExecutionActor` y llama a las fases de `IFormPublicationService`. Fuera de toda UoW llama al canal, que coordina Meta con las fases de `IWhatsAppFlowPublicationService`; cada fase que escribe abre una sola UoW. | Copia la separación entre `AssistantTurnWorker` e `IAssistantTurnService`; ni el worker ni el adaptador neutral reciben `IUnitOfWork`. |
| T5 | **El worker de publicación** entra con `IExecutionActorFactory` e `IExecutionActorScope` (quien publicó), no con `ITenantScope.Enter` solo. **La captura de una respuesta no usa `ExecutionActor`:** un cliente no tiene cuenta ni actor. Va en una fase aparte del procesador de entrada, fuera de su transacción, con el contexto de la organización armado con `SetFromAccess`, como la entrada de la E11; la feature se consulta con ese contexto. A un empleado se le revalidan la membresía y `formrecords.create` dentro de la captura. Un `nfm_reply` no pasa por el bot. | Con `Enter` solo, `IFeatureService` da `false`, porque `Enter` deja `TenantKind` en null (backlog #51); y `ExecutionActorFactory` no puede armar un actor sin cuenta. |
| T6 | **Un Flow por versión y por WABA.** Los dos textos del sistema que van adentro del Flow (la ayuda de los números y «Elegí hasta N») viajan en el `data` del mensaje, en el idioma de la cuenta de quien recibe; un cliente sin cuenta, en el de la organización ([textos](../../rules/textos-y-traducciones.md); 35 y 42). | El contenido no tiene idioma; los textos del sistema sí, y cambian por destinatario sin otro Flow. |
| T7 | **Lo que cambia sin que nadie edite va en otra fila.** `FormDefinitionStates` (una por formulario) guarda la versión activa, el bloqueo del prefijo y `LastRecordNumber`, sumado bajo bloqueo de esa fila al capturar, con índice único (definición, número). La fila del borrador cambia solo al editar. El prefijo es único **dentro de la organización**. | Si el contador o la versión activa vivieran en la fila del borrador, cada respuesta o publicación cambiaría su `xmin` y quien edita recibiría un 409 falso. Sin bloqueos globales: cada organización numera aparte. |
| T8 | **Sin ticket de comprobación:** `publication-checks` solo informa y devuelve el `contentHash` del borrador que comprobó; `publications` lo recibe, vuelve a comprobar todo adentro de su transacción y, si el borrador cambió, responde `Forms.Publication.DraftChanged`. Ninguna de las dos lleva `version`, y publicar no escribe la fila del borrador. | La comprobación final es la que vale. `VersionedContractTests` exige `VersionResponse` a todo pedido con `version`. |
| T9 | **«Número con decimales»:** cada pregunta fija sus decimales (de 0 a 6) y admite hasta 15 dígitos enteros. Al responder se acepta punto o coma; más decimales que los permitidos se rechaza. | Evita que «125.500» se lea como 125,5: con 0 decimales, se rechaza. |
| T10 | **Lo que recibe la persona:** el formulario sale como está escrito. El mensaje por defecto, el botón «Abrir formulario» y los textos del sistema del Flow van en el idioma de su cuenta, y si es un cliente sin cuenta, en el de la organización (35 y 42). La sesión guarda ese idioma. | Un formulario se ve y se manda igual con el menú en cualquier idioma. |
| T11 | Los valores viven en filas tipadas (`FormRecordValues`), sin un JSON paralelo. «Elegir varias» guarda una fila por opción, con su posición. | Una sola fuente, que se puede filtrar por columna. |
| T12 | **Las capturas rechazadas no crean nada en Forms.** El rechazo queda en la sesión de WhatsApp, con su código. | La idempotencia la da el índice único `SourceKey`. |
| T13 | **«Elegir varias» es un tipo propio** (`MultiChoice`), no una marca de la lista: de 1 a 20 opciones, con `0 ≤ mínimo ≤ máximo ≤ 20` y máximo mayor que cero. Es obligatoria si y solo si el mínimo es mayor que cero. Pasar de «elegir una» a «elegir varias» crea otra identidad; cambiar el rango la conserva. Sin ninguna marcada es «Sin respuesta». | Es la regla de ArquitecturaBase (C1-04), con el tope de WhatsApp. |
| T14 | **Un mensaje sin datos no lleva `data`.** Un mapa vacío (`prefill_FORM:{}`) hace que el Flow no abra en Android. | Error ya pagado en el piloto de ArquitecturaBase. |
| T15 | **Quien recibe y quien responde es un empleado o un cliente** (49). La sesión y la respuesta guardan uno de los dos: el usuario del miembro, o el contacto de la bandeja Clientes. A un empleado se le revisan la membresía y `formrecords.create`; a un cliente, que el formulario sea para clientes. | Un cliente no tiene cuenta ni permisos: se lo reconoce por el contacto, que es su número. |
| T16 | **La respuesta de un cliente es dato de la organización** (`ITenantOwned`). Si el contacto vinculó su cuenta personal, ve la copia por la copia de la conversación de la E11, que ya es compartida: Forms no suma tablas compartidas. **En «Mis mensajes» ve la tarjeta con sus propias respuestas:** al capturar, WhatsApp guarda en esa copia la tarjeta que le devuelve la captura (el nombre del formulario, el número y los valores que mandó), porque el acceso personal no lee `tenant` ni tiene `formrecords.read`. Si el cliente da de baja su cuenta vinculada, la respuesta queda con el contacto, y solo desaparece la copia de «Mis mensajes». | La regla de datos personales y la D4 de la E11. |
| T17 | Una pregunta de elegir toma sus opciones de la pregunta o de un catálogo (`catalogId`). Cambiar el catálogo, o pasar de opciones propias a un catálogo, en una pregunta publicada crea una pregunta nueva, como cambiar el tipo. | Cambiar opciones no obliga a republicar, y las respuestas viejas no cambian de sentido. |
| T18 | **Los ids que viajan a Meta son locales a la oferta** (`o1` … `o200`). La sesión guarda a qué ítem corresponde cada uno. También en «elegir varias». | No se mandan a Meta ids internos ni de personas, y «estar en la oferta» se comprueba solo. |
| T19 | **La oferta la arma Forms y la guarda WhatsApp,** cifrada en la sesión (`ProtectedOffer`, texto que devuelve `IPayloadProtector`). Se borra una vez procesada o vencida (§6, punto 9). | Forms conoce los catálogos y WhatsApp conoce la sesión. Como contiene nombres de personas, va cifrada. |
| T20 | Para un catálogo a mano, la captura comprueba que la opción siga activa; si no, `Forms.Record.OptionWithdrawn`. Para uno con filtros alcanza con que esté en la oferta. | Con filtros, lo que vio la persona es lo que vale. |
| T21 | El Flow usa un perfil con datos iniciales. Cada pregunta de catálogo es un `Dropdown` o un `CheckboxGroup` con `data-source: ${data.field_<N>}`, y la pantalla declara su `data` con un `__example__` **[Meta]**. | Permite mandar opciones distintas a cada persona sin un endpoint de intercambio. |
| T22 | **La presentación** arma el título y la descripción de cada opción al mandar: la del catálogo (E13) o la propia de la pregunta, que se guarda en el borrador y se sella con la versión. Lo que queda vacío o no entra frena la comprobación de publicar y el envío (36). Un campo que usa la presentación de una pregunta del borrador o de la versión activa no se quita (`Forms.Catalog.FieldInUse`): en la tabla de campos de la E13, su «Quitar» queda apagado con ese motivo. La captura lee los campos activos de las opciones elegidas, por lote y bajo sus bloqueos, y sella identidad, clave, nombre, tipo y valor. La tarjeta del chat no cambia. | Editar el catálogo después no cambia lo respondido. |
| T23 | **Un catálogo que nunca se podría mandar no se publica:** uno a mano con más de 200 opciones activas en «elegir una», o más de 20 en «elegir varias» (`Forms.Draft.CatalogTooLarge`). Los de miembros y contactos se comprueban al mandar, porque dependen de quien recibe y cambian solos. | Sin esto, un catálogo de 10.000 opciones se publicaba y nunca se podía mandar. |
| T24 | **Un título de sección es un elemento del borrador, no una pregunta** (88): tiene un solo texto de hasta 80 caracteres y su lugar entre las preguntas, se sella con la versión y no cuenta en el tope de 40 preguntas. No tiene valor, así que no guarda nada al responder. En WhatsApp sale como subtítulo de la pantalla (`TextSubheading`) y cuenta en el tope de 50 componentes **[Meta]**. Para publicar, tiene texto y al menos una pregunta debajo. | Es la regla de ArquitecturaBase, decidida allá y sin programar. Un elemento aparte evita filas vacías en las respuestas. |

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
     - se bloquea el prefijo en la fila de estado; la fila del borrador no se escribe.
   - Responde 202.
   - **El worker** (`Infrastructure/Modules/Forms/FormPublicationWorker.cs`, modelo `AssistantTurnWorker`) hace tres pasos:
     1. busca trabajos listos y entra con `ExecutionActor` (el operador que publicó, revalidado: si perdió `forms.publish` antes de tocar Meta, la publicación termina `Failed`). Llama a `IFormPublicationService.ClaimAsync`, que reserva en su única UoW y pasa a `Preparing`;
     2. fuera de toda UoW llama a `IStructuredFormChannel.PublishAsync`:
        - el canal arma el Flow de la versión para cada WABA con números conectados **[E11]**;
        - por cada uno sigue los pasos Create → Upload → Validate → Publish → Verify;
        - el adaptador `WhatsAppStructuredFormChannel` llama a `IWhatsAppFlowPublicationService.BeginStepAsync` para reservar el binding/paso y guardar `InFlight` en su única UoW; hace la llamada a Meta después del commit y entrega el resultado a `CompleteStepAsync`, que abre otra UoW. El adaptador no abre transacciones;
     3. entrega lo que devolvió el canal a `IFormPublicationService.CompleteAsync`, que en su única UoW aplica:
        - `Published` → `ActivationPending`, y se activa la versión en la fila de estado, en la misma UoW → `Published`;
        - `Pending` → `Publishing`, con `NextCheckAtUtc`: Meta la está revisando;
        - `Uncertain` → `Unconfirmed`, con `NextCheckAtUtc`;
        - rechazo definitivo → `Failed`, con sus `issues`. Una versión se publica en todas las WABAs o en ninguna: si una rechaza, queda `Failed`, y el Flow que haya quedado en otra no se usa.
   - **Cuando quedó `Publishing` o `Unconfirmed`,** el worker vuelve a consultar con `GetPublicationAsync`, que solo verifica y nunca repite un POST.
   - **«Consultar ahora»** hace esa misma consulta, en `IFormPublicationService.CheckAsync`, y aplica el resultado con `CompleteAsync`.
   - **«Salió» y «No salió»,** para una publicación `Unconfirmed` o `NeedsReview`: `IFormPublicationService.ResolveAsync` la lleva a `Published`, activando la versión, o a `Failed`, en su única UoW. Sin `version`: lo protege el estado, bajo el bloqueo de la fila de estado, y dos a la vez dan `NotResolvable`. Sin esto, una publicación trabada frenaría todas las que siguen.
   - **Si se cae a mitad:** la reserva vuelve a tomar el trabajo, y el canal, al ver un `InFlight`, verifica en vez de repetir.
5. **Vista previa:** `IStructuredFormChannel.GetPreviewAsync(versionId)` devuelve la URL de Meta con su vencimiento, sin guardar nada.
6. **Mandar desde el chat** (módulo WhatsApp).
   - **Ubicación:** en escritorio, el botón «Enviar formulario» al lado de «Enviar»; en el teléfono, la opción «Enviar formulario» en el ⋮ del chat. Ambos abren el mismo diálogo. Si la acción no aplica al acceso, bandeja o permiso, no se muestra.
   - **Cuándo se puede usar:**
     - en Internos, quien escribe es miembro activo y el operador tiene «Atender internos» (decisión 10) **[E11]**;
     - en Clientes, el operador tiene «Atender clientes» **[E11]**;
     - la ventana de 24 horas está abierta;
     - el número está disponible;
     - no hay otro envío pendiente;
     - `IStructuredFormSource.ListSendableAsync(recipient)` devuelve al menos un formulario. Forms filtra los publicados para ese tipo de destinatario (50) y, a un empleado, los que puede responder (`formrecords.create`).
   - **Si no se puede usar:** el botón y la opción del ⋮ quedan grisados, con el motivo en su tooltip (43).
   - **Mandar:**
     - afuera de la UoW se valida el texto (≤1024) y se lee el formulario elegido con `FindSendableAsync`. El idioma de los textos del sistema es el de la cuenta de quien recibe (T10);
     - en la UoW: bloqueo de la conversación, se revalidan ventana, empleado y envío pendiente, se toma el chat si no estaba tomado (regla de la E11), se crea la sesión (token aleatorio de 32 bytes; se guarda solo el hash) y se encola el mensaje interactivo de tipo Flow por el outbox (`WhatsAppOutboundMessage` con su variante nueva);
     - el outbox lo manda y, si no se sabe si salió, lo reintenta solo, como a los demás mensajes; no hay un estado «sin confirmar» (80).
   - **Si la WABA del número no tiene Flow de esa versión:** `WhatsApp.Flow.NotPublishedForNumber`.
7. **Recibir y capturar** (worker de WhatsApp, siguiendo la entrada de la E11 **[E11]**).
   - **Webhook:** guarda el mensaje con `ProtectedFlowResponse`, sin duplicados por `wamid`.
   - **El procesador de entrada,** por cada mensaje, aislando los errores mensaje por mensaje y sin pasar por el bot:
     1. saca el hash del `flow_token` y busca la sesión;
     2. compara la hora firmada de Meta con `ExpiresAtUtc`: si se pasó, `Expired`;
     3. decodifica con el `FieldMap` del binding (§14); los arrays de «elegir varias» van a la captura, nunca a la descarga de medios;
     4. en una fase aparte, fuera de la transacción del procesador y con el contexto de la organización de `SetFromAccess` (T5), llama a `IStructuredRecordSink.CaptureAsync`. Las escrituras previas de reserva o vencimiento son fases de `IWhatsAppFlowReplyService.ClaimAsync`, con una única UoW; el procesador no las abre.
   - **La captura en Forms:** `FormRecordSink` delega en `IFormRecordService.CaptureAsync`, que abre su única UoW:
     - busca el `SourceKey`: si ya existe con el mismo contenido, devuelve el mismo número; si el contenido es otro, `ReplayConflict`;
     - a un empleado le revalida la membresía y `formrecords.create`; a un cliente, que el formulario siga siendo para clientes;
     - valida estricto contra la versión sellada;
     - bloquea la fila de estado de la definición y suma `LastRecordNumber` (T7);
     - guarda la respuesta y sus valores.
   - **Después:** `IWhatsAppFlowReplyService.CompleteAsync` abre su única UoW en WhatsApp y marca la sesión `Completed` (con número) o `Rejected` (con código). Si el contacto vinculó su cuenta, guarda en la copia compartida de la conversación la tarjeta armada para «Mis mensajes» (T16).
   - **Si se cae entre las dos UoW:** se repite y devuelve el mismo número.
8. **Tarjeta en el chat.**
   - Al listar los mensajes, WhatsApp llama a `IStructuredRecordSource.ReadAsync(recordIds, culture)`, con el idioma de quien mira para los textos del sistema («Sí», «No»).
   - Forms decide con los permisos de quien mira: con `formrecords.read` devuelve los valores, y sin él solo el número.
   - En la lista de chats, la vista previa es el texto fijo «Respuesta del formulario».
9. **Retención y baja.**
   - La retención de WhatsApp borra `ProtectedFlowResponse` cuando el mensaje ya se procesó; la respuesta y su número quedan (decisión 23).
   - **`ProtectedOffer`** vive en una tabla de la organización, que esa retención no ve. Lo limpia `WhatsAppFlowSessionCleanupWorker`, que recorre las organizaciones con `TenantJobRunner` y borra la oferta de las sesiones procesadas y de las vencidas, así no quedan nombres de personas guardados.
   - `Application/Modules/Forms/Services/FormsAccountDeletionParticipant` decide las reglas dentro del alcance y la UoW de la baja, sin abrir otra ([datos-personales](../../rules/datos-personales.md)). Copia `Application/Modules/Assistant/Services/AssistantAccountDeletionParticipant.cs`: usa un puerto `IFormsAccountDeletionCleanupRepository`, cuya implementación en Infrastructure hace solo el cleanup EF y exige la transacción del llamador. Lo protege `AssistantMaintenanceLayerTests`, que se extiende para Forms. `ChoiceLabel` puede ser el nombre de otra persona (un catálogo de miembros), y la regla lo contempla.

## 6b. La pregunta de catálogo

Cambia los flujos 2, 3, 6 y 7 para las preguntas con `catalogId`.

1. **Guardar y comprobar:**
   - `PUT …/draft` rechaza solo un catálogo que no existe o es ajeno. Uno dado de baja, o que no se puede usar, se guarda y frena en `publication-checks`: si no, una baja impediría guardar cualquier otro cambio;
   - cambiar el catálogo de una pregunta ya publicada emite un `FieldId` nuevo (T17);
   - la presentación propia de la pregunta se guarda en el borrador; si usa un campo quitado o inexistente, se guarda y frena al publicar, con `QuestionInvalid` en su pregunta;
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
