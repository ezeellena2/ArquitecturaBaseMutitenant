# E14 · Formularios: archivos, tests y Meta (spec, parte)

**Estado:** pendiente · parte de la [spec de la E14](14-formularios.md), que dice qué leer y cómo se cierra.

## 8. Archivos

Cada pieza nueva copia la **estructura** del archivo modelo de este repo; no se copia código de ArquitecturaBase. Hay dos sufijos de ArquitecturaBase que no están en la lista cerrada y cambian:
- `Compiler` pasa a `WhatsAppFlowBuilder` («arma un modelo»);
- `Codec` pasa a `WhatsAppFlowReplyVerifier` («verifica algo que entra»).

**Abreviaturas:** `D/`, `A/`, `I/`, `P/` son `src/ArquitecturaBaseMultitenant.{Domain,Application,Infrastructure,Api}/`.

El esqueleto del módulo lo deja la E12: `AddFormsModule` en las tres capas, la feature `forms`, `FormsModelContributor`, `FormsContractContributor`, los recursos, la clase `Forms` de permisos y el registro del front. La E14 le suma sus piezas.

| Archivo nuevo o cambiado | Modelo en este repo |
| --- | --- |
| `D/Forms/FormDefinition.cs`, `FormDefinitionState.cs`, `FormVersion.cs`, `FormPublication.cs`, `FormRecord.cs`, `FormQuestionType.cs`, `FormDraft.cs`, `*Errors.cs` (con `FormOfferErrors.cs`) | `D/Assistant/AssistantOrganizationSettings.cs`, `D/Assistant/AssistantErrors.cs` |
| `D/Authorization/Permissions.cs`: la clase `Forms` de la E12 suma `forms.read`, `forms.manage`, `forms.publish`, `formrecords.create` y `formrecords.read`; `forms.manage` y `forms.publish` incluyen `forms.read` (`Includes`, 260). Los dos últimos todavía no los pide una ruta: los usa la lógica, en la captura y en la tarjeta, y por eso no son permisos sin uso ([permisos](../../rules/permisos.md)) | la clase `Assistant` del mismo archivo |
| `A/Modules/Forms/Interfaces/Services/IFormDefinitionService.cs`, `IFormPublicationService.cs`, `IFormRecordService.cs` | `IAssistantConversationService.cs`; `IAssistantTurnService.cs` para las fases |
| `A/Modules/Forms/Services/FormDefinitionService.cs`, `FormPublicationService.cs`, `FormRecordService.cs` y los helpers `FormPrefixResolver`, `FormDraftEditor`, `FormVersionCreator`, `FormRecordNumberIssuer`, `FormRecordWriter` y `FormOfferBuilder` (arma la oferta de cada pregunta de catálogo) | `AssistantConversationService.cs`, `AssistantTurnService.cs` y sus helpers |
| `A/Modules/Forms/Services/FormStructuredSource.cs`, `FormRecordSink.cs`, `FormRecordSource.cs` (implementan los puertos) | `A/Modules/WhatsApp/Services/WhatsAppLoginCodeChannel.cs` (un módulo implementa un puerto del núcleo) |
| `A/Modules/Forms/Interfaces/Persistence/IFormDefinitionRepository.cs`, `IFormReader.cs`; suman sus claves a `UniqueKeys.cs` | `A/Modules/Assistant/Interfaces/Persistence/` |
| `A/Modules/Forms/Models/`, `Validation/`; suman sus claves a los recursos de la E12 | las mismas carpetas del Asistente |
| `A/Interfaces/Integrations/StructuredData/*.cs`, `A/Models/StructuredData/*.cs` | `IStructuredContactSource` de la E12 |
| `I/StructuredData/Disabled*.cs` | `DisabledStructuredContactSource` de la E12 |
| `I/Modules/Forms/Persistence/Configurations/*.cs`, `Repositories/*.cs`, `Readers/*.cs` | `I/Modules/Assistant/Persistence/` |
| `A/Modules/Forms/Services/FormsAccountDeletionParticipant.cs`, `A/Modules/Forms/Interfaces/Persistence/IFormsAccountDeletionCleanupRepository.cs`; `I/Modules/Forms/Persistence/Repositories/FormsAccountDeletionCleanupRepository.cs` | `A/Modules/Assistant/Services/AssistantAccountDeletionParticipant.cs`, su puerto de cleanup y `I/Modules/Assistant/Persistence/Repositories/AssistantAccountDeletionCleanupRepository.cs`; `AssistantMaintenanceLayerTests` |
| `I/Modules/Forms/FormPublicationWorker.cs` | `I/Modules/Assistant/AssistantTurnWorker.cs` (descubre, entra con actor y llama fases del servicio) |
| `I/Modules/WhatsApp/WhatsAppFlowSessionCleanupWorker.cs` | `I/Modules/WhatsApp/WhatsAppBroadcastWorker.cs` y `TenantJobRunner.cs` |
| `I/Persistence/Migrations/<fecha>_FormDefinitions.cs` y `<fecha>_SystemRoleFormsPermissions.cs` | `20261010011328_AssistantModule.cs`, `20261010203618_SystemRoleAssistantPermissions.cs` |
| `P/Modules/Forms/Controllers/BusinessFormsController.cs`, `Contracts/*HttpRequest.cs`; `BusinessFormCatalogsController` suma `used-by` | `P/Modules/Assistant/...` |
| El guardado de un catálogo de la E12 (`FormCatalogService`): `Forms.Catalog.ItemInUse` suma `uses: records` si una respuesta eligió la opción, leído con `IFormReader` sobre el índice de `FormRecordValues` (129) | `uses: options`, que suma la E13 |
| WhatsApp: `A/Modules/WhatsApp/Services/WhatsAppFlowBuilder.cs` (con el perfil con datos, el `CheckboxGroup` de catálogo y el nombre del Flow, 165), `WhatsAppFlowPreviewLinkBuilder.cs` (el enlace de la vista oficial, 176), `WhatsAppFlowReplyVerifier.cs` (con la oferta), `WhatsAppFormSendService.cs` (con `ProtectedOffer`), `WhatsAppStructuredFormChannel.cs`; `I/Modules/WhatsApp/WhatsAppFlowClient.cs`, la fase de captura del procesador de entrada (T5), la copia de la tarjeta para «Mensajes» (T16), la vista previa de la lista de chats y las configuraciones de `WhatsAppFlowBindings` y `WhatsAppFlowSessions` | `I/Modules/WhatsApp/WhatsAppCloudClient.cs`, `I/Modules/WhatsApp/Inbound/WhatsAppInboundProcessor.cs` y lo que deje la E11 **[E11]** |
| WhatsApp: `A/Modules/WhatsApp/Interfaces/Services/IWhatsAppFlowPublicationService.cs`, `IWhatsAppFlowReplyService.cs` y sus implementaciones en `Services/` | `IAssistantTurnService.cs` y `AssistantTurnService.cs` (fases con una UoW cada una) |
| La política de seguridad no cambia: `P/Modules/WhatsApp/Hosting/MetaContentSecurityPolicySource.cs` ya permite `https://*.facebook.com` en `frame-src`, que cubre el marco de la vista oficial | `WhatsAppContentSecurityPolicyTests` |

**Front** (`F/` = `ArquitecturaBaseMutitenantFront/`):

| Archivo nuevo o cambiado | Modelo |
| --- | --- |
| `F/src/modules/forms/registration.ts` de la E12: suma las rutas `/org/formularios` (la lista «Formularios») y `/org/formularios/:id` (el formulario, con su pestaña «Diseño»), con `forms.read`, y «Formularios» en el panel de «Gestión de formularios» (79). Las pestañas del formulario son una pieza nueva del front, a la que cada etapa suma la suya | la misma registration |
| `F/src/modules/forms/{api,components,hooks,pages}/`: la lista, el formulario con sus pestañas, el editor con el combo del catálogo (`CatalogCombo.tsx`, de la E13) y la presentación en la pregunta, publicar, la etiqueta y las acciones de la publicación en la banda, sin zona ni pasos (175), el diálogo de la vista oficial (`OfficialPreviewDialog.tsx`, 176), que al cerrarse cancela el pedido, y en los catálogos «Lo usan», «Quitar «…»» con «Ir a la pregunta» y la opción que eligió una respuesta. Las preguntas que se abren en el lugar son una excepción a las filas que no se expanden: se escribe en la ficha de tablas del front | `F/src/modules/assistant/` |
| `F/src/modules/forms/locales/{es,en}/forms.json` y `enums.json`: suman las claves, con paridad | los recursos de la E12 |
| WhatsApp **[E11]**: el diálogo «Enviar formulario», con los avisos de la oferta y el mensaje que propone al elegir (165), la tarjeta de respuesta, con el rechazo y «Mandar de nuevo», y la vista previa de la lista de chats, en el módulo de WhatsApp del front que deje la E11; en «Mensajes», la tarjeta copiada (T16) | — |

**Punteros:** cada carpeta nueva lleva su `AGENTS.md` de una a tres líneas («qué va acá · Leé ficha · Copiá de archivo») y un `CLAUDE.md` con `@AGENTS.md`, más su fila en `docs/architecture/arbol.md`. Se crean con la carpeta, no antes (`HarnessTests`). Al cerrar la etapa, `docs/features/formularios.md` (nacida en la E12) suma la sección de formularios.

## 10. Tests

| Dónde | Qué |
| --- | --- |
| `tests/…Domain.UnitTests/Forms/` | identidades; tipo cambiado = identidad nueva; de «elegir una» a «elegir varias» = identidad nueva, y cambiar el rango la conserva; prefijo (formato, bloqueo); transiciones de `FormPublication`; numeración en `FormDefinitionState` |
| `tests/…Application.UnitTests/Modules/Forms/` | validadores (rango de «elegir varias»); reglas de publicar (textos obligatorios, un título sin preguntas debajo, «nada para publicar», un catálogo demasiado grande, cada problema de largo con su límite y su largo en UTF-16); guardar sin cambios compara la versión sin emitir identidades; armado del Flow (el `CheckboxGroup`, el subtítulo de un título de sección, sin `data` cuando no hay nada que mandar, los textos del sistema en el idioma de quien recibe, y un test que recorre el JSON armado y falla si encuentra «,]» o «,}», O29) y verificador de respuestas (cada tipo, punto y coma, decimales de más, «125.500» con 0 decimales, obligatoria vacía, opción ajena, ids repetidos o fuera del rango en «elegir varias», presupuesto de 16 KiB); el armado de la oferta (más de 200, más de 20 en varias, menos que el mínimo, ninguna en una obligatoria, nombres largos cortados); el perfil con datos y el verificador (`localId` fuera de la oferta); la vista oficial (las primeras 20 activas con los filtros fijos, ninguna sin `formcatalogs.read`, `interactive`, `navigate` y la primera pantalla, sin `debug`, hasta 6000 caracteres escapados recortando cada lista, una opción con `%` y Meta que no contesta: `PreviewUnavailable` a los 30 s); el nombre del Flow (80 caracteres en NFC, cortado con «…», solo los caracteres permitidos y fuera de las huellas), y el mensaje por defecto en el idioma de quien recibe |
| `tests/…Api.IntegrationTests/Modules/Forms/` | cada ruta: 401, 403, feature apagada = 404, validación, 409, idempotencia y paginado; publicar con un canal de prueba (Published, Uncertain → verifica, Failed, y `RequiresReview` → `NeedsReview`); «Salió» activa la versión, y un binding sin id manda con `flow_name`; «No salió» la deja `Failed`; captura idempotente y número sin saltos con dos capturas a la vez; «elegir varias» con una fila por opción y su posición; tarjeta con y sin `formrecords.read`; captura con una opción dada de baja = sin número, y la tarjeta con «Mandar de nuevo»; los campos sellados no cambian al editar el catálogo; un campo que usa la presentación de una pregunta no se quita, y `usedIn` trae los ids de «Ir a la pregunta»; borrar una opción que eligió una respuesta = `ItemInUse` con `uses: records`, y una que nadie eligió se borra; «Lo usan» con su segunda página; una respuesta o una publicación que termina mientras alguien edita no le da un 409 al guardar; publicar con un borrador que cambió después de comprobar = `DraftChanged`; una cuenta conectada sin el Flow habilita publicar sin cambios, y con todas, `NothingToPublish`; publicar dos veces con la misma `Idempotency-Key` da la misma publicación; «Salió» o «No salió» fuera de `NeedsReview` = `NotResolvable`; baja de cuenta; ejemplos exportados de cada respuesta. `WhatsAppContentSecurityPolicyTests` suma que `https://business.facebook.com` entra en `frame-src` |
| `tests/…Api.IntegrationTests/Modules/Forms/*IsolationTests.cs` | otra organización recibe 404 en cada ruta; RLS de cada tabla; la captura no cruza organizaciones; la oferta nunca trae opciones de otra organización (modelo: `Modules/Assistant/AssistantConversationIsolationTests.cs`) |
| Volumen | ofrecer y capturar con un catálogo de 10.000 opciones y 20 campos, sin traer todo para quedarse con poco, con EXPLAIN |
| `tests/…Api.IntegrationTests/Modules/WhatsApp/` **[E11]** | botón y motivos, por bandeja: sin Forms no aparece, sin formularios queda grisado, y `TakenByOther` y `PaymentRequired` como al responder; la vista previa de la lista de chats, con el nombre y sin valores; sesión vencida por la hora de Meta; un mensaje con error no frena a los demás; respuesta de más de 16 KiB rechazada y visible; la captura de un cliente sin cuenta, sin `ExecutionActor` y fuera de la transacción del procesador; con `forms` apagada, rechazada; un empleado que perdió el permiso o la membresía, `NotAllowed`; un `nfm_reply` nunca llega al bot; la tarjeta copiada en «Mensajes» de quien vinculó su cuenta; una captura que falla se reintenta con sus intervalos y termina `Rejected`; la oferta de una sesión vencida se borra |
| Guardas (partials del módulo) | `ExplicitRouteInventoryTests`, `ModuleControllersTests`, `ApplicationServicesTests`, `TenantScopeUsageTests`, `ResourceParityTests` (sumar los resx de Forms), `PermissionTextsTests` y `ModuleIsolationTests` en verde; `TransactionBoundaryTests` sin ampliar excepciones, y la guarda de capas de baja como `AssistantMaintenanceLayerTests` |
| Front | Vitest con MSW y axe en las páginas y el diálogo; los ejemplos exportados validados con zod estricto; `registration`, `navigation` y `composition` del módulo; paridad de sus recursos `forms` y `enums` compuestos desde `registration.ts`, también al retirar Forms; los botones según la decisión 43; la etiqueta y las acciones de cada estado de la publicación y el motivo de «Publicar en WhatsApp» apagado (175); «Repetir el mismo intento» con la misma clave después de un corte; la vista oficial con la medida del marco al cargar, vencer y fallar, a 1440, 390 y 320, y cerrarla cancela el pedido (176); el mensaje que se propone al elegir, sin pisar lo escrito (165) |
| E2E real y recorrido | crear con los siete tipos, publicar contra el canal real o el de prueba, mandar a una cuenta en inglés (el formulario llega como está escrito y los textos del sistema, en inglés); responder y ver el número tras recargar; usar un catálogo en una pregunta de «elegir varias», mandar y responder; dar de baja una opción antes de responder y mandar de nuevo; cortar la red al publicar: la pantalla relee y «Repetir el mismo intento» publica una sola versión; abrir la vista oficial y recorrerla |
| Prueba con Meta | con el número de una organización de prueba: §15 |
| Retiro | una vez, con la base vacía: quitar `Forms` con sus catálogos (guía nueva, `docs/guides/quitar-formularios.md`, copiando `quitar-asistente.md`) y que el núcleo y WhatsApp sigan en verde. Allá la prueba encontró que la vuelta atrás de la migración de retiro necesita a mano las dos claves diferibles de las opciones, y que los tests de WhatsApp, el inventario de rutas y los de migraciones del núcleo daban por instalado el módulo: se omiten solos sin él (traspaso 9) |

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

## 14. Referencia de Meta (tomada de ArquitecturaBase)

Es lo único que se toma de ArquitecturaBase: lo que dice Meta y lo que ya se pagó. Los archivos están en `src/ArquitecturaBase.*/Modules/WhatsApp/` de `origin/main`. **Antes de programar, se verifica contra la documentación vigente de Meta.**

| Tema | Lo que se sabe |
| --- | --- |
| Perfil | `flow-static-7.3-r9`: Flow JSON `7.3`, la versión de Graph de la configuración del módulo (hoy `v26.0`, no la `v25.0` de ArquitecturaBase), sin endpoint ni `data_api_version`. Una pantalla `FORM` terminal: `SingleColumnLayout` > `Form` > controles + `Footer` con `on-click-action: complete` y un payload `field_<GuidN>: ${form.field_<GuidN>}`. Los textos del sistema son propiedades dinámicas de `data` (T6). |
| Componentes | Texto corto: `TextInput` con `max-chars` (80 por defecto). Texto largo: `TextArea` con `max-length` (600). Número: `TextInput` de texto con el patrón `^-?(0|[1-9][0-9]*)([,.][0-9]+)?$`, de hasta 60 caracteres, con la coma primero: el cliente de los Flows borra toda coma seguida de `]` o `}`, también dentro del patrón, así que ningún texto fijo del Flow la lleva (O29 de allá, 207). Fecha: `CalendarPicker` single, que en WhatsApp se ve como lo dibuja Meta; lo que arma la app sigue solo con números (274). Sí/no: `RadioButtonsGroup` con ids `true` y `false`. Elegir una: `Dropdown` con el `ChoiceId` como id. Elegir varias: `CheckboxGroup` de 1 a 20 opciones, con `min-selected-items` y `max-selected-items` sellados; sin ayuda, dice «Elegí hasta N». Título de sección: `TextSubheading`, que Meta aceptó allá el 2026-11-06; acá se prueba igual (§15). `max-chars` y `max-length` van como **números** JSON (Meta rechazó la versión que los mandaba como texto). |
| Límites | Etiqueta: 20 caracteres (30 en sí/no y en elegir varias, 40 en fecha). Ayuda: 80 (300 en la descripción de sí/no y en elegir varias). Opción: 30. Título: 80. Botón: 35. Hasta 40 preguntas, 50 opciones y 50 componentes; JSON ≤1 MiB. Se cuentan en UTF-16. Sin recortar: si no entra, se informa con su `FieldId` y el texto. Una pregunta «elegir una» con ayuda no se puede representar: frena al comprobar, en su ayuda (`UnsupportedConfiguration`). |
| Presupuesto de respuesta | 16 KiB. El peor caso es 785 + la suma de (42 + los bytes del valor): texto 2 + 6 × largo, número 62, fecha 12, sí/no 7, opción 38, elegir varias 38 por opción marcable. Si se pasa, no se publica (`ResponseTooLarge` sobre los largos de texto). |
| Publicar | (1) Comprobar que el número sea de la WABA: `GET {waba}/phone_numbers`. (2) `POST {waba}/flows` con `{name, categories:["OTHER"]}`. (3) `POST {flow}/assets` multipart con `asset_type=FLOW_JSON`; los `validation_errors[].pointers[].path` se mapean a la pregunta y se descarta el texto de Meta. (4) `POST {flow}/publish`. (5) Verificar: `GET` del Flow y de la salud del número, bajar el asset (solo de `*.fbcdn.net` o `mmg.whatsapp.net`, sin redirecciones, ≤1 MiB) y comparar el SHA-256. **El nombre del Flow** (165): «Visita a cliente · v4 · ab63a1f9», con el nombre del formulario, la versión y los últimos 8 caracteres del id de la publicación, sin idioma (35), porque Meta lo exige único en la cuenta. Hasta 80 caracteres en NFC, solo letras, dígitos, espacios y puntuación común; el nombre del formulario se corta con «…». No entra en `FlowJsonHash` ni en el `ContentHash` de la versión, y es el que muestran el administrador de WhatsApp y la vista oficial. |
| Vista previa | `GET {flow}?fields=preview.invalidate(false)`. La URL tiene que ser de `business.facebook.com` y trae `expires_at`. **La vista oficial** (176) le suma `interactive=true`, `flow_action=navigate` y `flow_action_payload` con la primera pantalla y sus datos, sin `debug`; Meta decodifica ese payload dos veces. El marco mide 480 × 840: con menos, Meta muestra sus propias barras. Allá, sin `https://business.facebook.com` en `frame-src`, el marco se veía solo en desarrollo; acá lo cubre `https://*.facebook.com`. La cadena entera tiene un tope de 30 s (§6, punto 5). |
| Errores | **Incierto** (no se repite solo, se verifica): 5xx, 429, timeout, cuerpo ilegible, Create 2xx sin id. **Definitivo:** otros 4xx y 2xx con `validation_errors`. Lo que falla antes de llamar a Meta termina en fallida, no en «Sin confirmar». **Sin medio de pago** (141006, visto en ArquitecturaBase; el repo todavía no lo conoce): el Flow aparece disponible pero la salud total queda BLOCKED; acá se muestra como `WhatsApp.Flow.AccountUnavailable`. |
| Mensaje | `type: interactive`, `interactive.type: flow`, cuerpo ≤1024, `action.name: flow`, `parameters: {flow_message_version: "3", flow_id (o flow_name, §6, punto 4), flow_token (≤128), flow_cta (≤20, «Abrir formulario»), flow_action: navigate, flow_action_payload: {screen: "FORM", data?}}`. `data` lleva los textos del sistema que el Flow usa y las opciones de cada pregunta de catálogo (§6b); si no hay nada, no se manda (T14). Sin texto del operador, el cuerpo es «Completá el formulario «<nombre>».» (165). Allá anduvieron un `Dropdown` con datos y sin endpoint, y un mensaje de 34,7 KB con 331 opciones (2026-11-07). |
| Respuesta | `messages[].interactive.type == "nfm_reply"`, `nfm_reply.name == "flow"`, `response_json` (texto) con `flow_token` y `field_<GuidN>`. Profundidad ≤4, ≤50 campos; «elegir varias» llega como array de ids. Una respuesta de más de 16 KiB se guarda como rechazada, visible, y no se descarta en silencio. |
| Valores | Texto: hasta el largo y sin NUL. Número: con el separador decimal del idioma de la sesión, o el otro si no hay duda; lo ambiguo («1.500» en español) da `WhatsApp.Flow.NumberAmbiguous` (207), con su test en es y en para «1.500», «1,5», «1.5» y «1500»; sin exponente ni separador de miles, sin redondear: la ayuda lo dice, y «1.500,5» lo frena el patrón en el teléfono, con el mensaje de Meta (O42); se recortan los ceros a la derecha. Fecha: `yyyy-MM-dd`. Sí/no: solo `true` o `false`. Opción: un `ChoiceId` de la versión sellada. Elegir varias: ids distintos de la versión, en el orden recibido. Vacío: si es obligatoria, error; si no, ausente. |
| Lecciones | <ol><li>commitear `InFlight` antes de cada POST, y ante una duda verificar, no repetir;</li><li>un Create sin id confirmado nunca se adopta por nombre;</li><li>usar la hora firmada de Meta para vencer, y aislar los errores mensaje por mensaje;</li><li>el token no forma parte de la identidad de la cuenta (WABA + número): rotarlo no rompe nada;</li><li>no hashear arreglos sin orden fijo;</li><li>en el teléfono hay que probar: ausente contra `false`, el largo máximo de texto y la sesión de una versión anterior. Entran en la prueba con Meta;</li><li>un `%` suelto en una opción («2 4 D 100%») rompía la vista oficial: en `flow_action_payload` cada `%` va como `\u0025`.</li></ol> |
| No se toma | El número y la WABA globales; los permisos planos (`formtemplates.*`); el contacto atado 1:1 a un usuario con `LinkRevision`; el bloqueo global `forms:definitions` y el prefijo global; un Flow por idioma; los modos Manual, Bot y Asistente en esta etapa (la E23 los suma, 52); la pausa de 6 s por destinatario en Redis; los recibos de borrador de 30 días; los perfiles anteriores a r9. |

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
