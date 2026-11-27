# Arreglos de la revisión externa · plan ejecutable

**Estado:** pendiente · Va después de la E12 con el código listo y antes de la E13. Spec: este plan. Cada tarea trae el hallazgo de la revisión externa del 2026-11-27 (Antigravity), comprobado contra el código de `67cba1c1` por la verificación del mismo día (`.local/antigravity/verificacion/00-verificacion.md`, fuera de git; el id `T0N-M` de cada tarea es el de esa verificación). Tableros: ninguno nuevo; ninguna tarea suma una pantalla.

| Parte | Tareas | Estado |
| --- | --- | --- |
| Contrato | — | Sin cambios de rutas ni de cuerpos |
| Back | B1–B8 | Pendiente |
| Front | F1 | Pendiente |

## Forma de trabajo

Modo rápido con el conductor ([conducir una etapa](../guides/conducir-una-etapa.md), [ADR 0071](../decisions/0071-conductor-en-carriles.md)): back y front a la vez; la revisión en paralelo, con los tests enteros y los detectores; los arreglos; la última revisión de código, y el cierre, que deja el código listo. Después, la verificación, mientras se programa la E13.

**Reglas de cada chat:** todo en main, sin ramas, worktrees, stash, reset ni push; commits propios con `git commit -- <rutas>`, en español y conventional, con la línea `Tarea: arreglos-revision/<id>`; con la app levantada, nadie edita el `src` del front; TDD: cada arreglo empieza por su test dirigido en rojo. Cada tarea termina con su test dirigido y el build de la solución (en el front, `tsc -b` y el lint de lo que tocó).

**Sin producción:** no hay datos reales que corregir. Ninguna tarea migra datos salvo la que lo dice.

**Lo que no está acá no se agrega:** cada tarea arregla su hallazgo y nada más. Lo que encuentre de paso va a «Para después».

**Revisión adversarial:** las tareas marcadas **(R)**, una vez por tema de riesgo, en la revisión de la etapa.

## Contrato

No cambia ninguna ruta, ningún cuerpo ni ningún permiso. Los errores son los que ya existen:

| Tarea | Caso | Respuesta |
| --- | --- | --- |
| B1 | Google o un alta con un correo administrado cuya membresía terminó | La misma que da hoy el ingreso por código en ese caso (`LoginVerifier.cs:64`: `LoginCodeErrors.Invalid`; en Google, `ExternalLoginErrors.Failed`), sin sesión ni método nuevo |
| B2 | Una planilla de contactos pasada de los topes | 400 `WhatsApp.Import.InvalidFile` (o `WhatsApp.Import.TooManyRows` si son filas), como hoy |
| B2 | Un cuerpo multipart más grande que el tope | 413 de Kestrel, antes de leer el archivo |

## Back

Rutas abreviadas bajo `src/ArquitecturaBaseMultitenant.<Capa>/`; tests bajo `tests/ArquitecturaBaseMultitenant.<Proyecto>/`.

### Back A · Identidad y archivos

**B1 · El correo administrado revocado no abre la cuenta (R permisos)** (T07-01, T07-02)

[ADR 0033](../decisions/0033-metodos-de-ingreso.md): un correo de un dominio verificado por una organización deja de servir para entrar cuando termina la membresía. El ingreso por código lo cumple (`Application/Services/Auth/LoginVerifier.cs:60-65`). Google y las dos altas, no: quien recibe el buzón de un exempleado entra a su espacio Personal y a sus otras organizaciones.
- **Google:** en `Application/Services/Auth/GoogleAccountResolver.cs`, la rama sin método Google (después de `FindVerifiedUserIdAsync(LoginMethodType.Email, …)`) lee ese método Email y exige `LoginMethodAvailabilityPolicy.IsAvailableAsync`. Si no está disponible, falla antes de vincular Google o de registrar. `GoogleAccountGuard.CheckAsync` recibe `methodId` y no lo usa: se saca el parámetro.
- **Altas:** `SignupCodeRequester` no manda el código del alta de empresa ni del alta personal a un método existente que no pasa `CanReceiveCodeAsync` (como `LoginCodeDeliveryIssuer.cs:30-36`), y responde igual que si lo hubiera mandado. `SignupCodeVerifier.VerifyAsync` y `VerifyBusinessAsync`, en la rama de cuenta existente, exigen `IsAvailableAsync` antes de `SignupExistingMethodVerifier.ConfirmAsync`.
- **Copiá de:** `LoginVerifier.cs:60-65` y `LoginCodeDeliveryIssuer.cs:30-36`.
- **Tests:** en `Api.IntegrationTests/Auth/GoogleLoginTests.cs`, `BusinessSignupTests.cs` y `SignupTests.cs`, con una organización con su dominio verificado y la membresía de la persona dada de baja:
  - el ingreso con Google sin método Google previo, el alta de empresa y el alta personal con ese correo fallan, sin sesión, sin método nuevo y sin código enviado;
  - con la membresía activa, los tres siguen andando;
  - un Google ya vinculado (propio, sin organización administradora) sigue entrando.
- Si cambia la firma, también `Application.UnitTests/Services/Auth/GoogleAccountGuardTests.cs`.

**B2 · Las planillas pasan por un solo escáner con topes (R efectos)** (T07-03, T07-04)

La importación de contactos de WhatsApp no tiene tope de bytes, escaneo del ZIP ni `[RequestSizeLimit]`, y lee `cell.Value`: cualquier dueño de organización puede tirar una copia de la API con una planilla armada. Forms sí escanea, pero cuenta celdas solo en `xl/worksheets/` y no limita la cantidad de partes.
- El escáner de `Infrastructure/Modules/Forms/FormCatalogSpreadsheet.cs` (`Scan` y `CountCells`) pasa al núcleo, a `Infrastructure/Spreadsheets/` (nuevo), porque un módulo no usa el código de otro. Lo usan Forms y WhatsApp, cada uno con sus topes.
- El escáner suma un tope a la cantidad de partes del ZIP y cuenta las celdas de cada hoja que declara `xl/workbook.xml` con sus relaciones, esté donde esté.
- `Infrastructure/Modules/WhatsApp/WhatsAppContactSpreadsheet.cs`:
  - corta en 10 MiB antes de copiar el archivo (el tope de Forms, `FormCatalogFileResolver.cs:70`);
  - escanea antes de `new XLWorkbook`;
  - lee `HasFormula ? CachedValue : Value`, como Forms.
- `[RequestSizeLimit]` de 11 MiB en las rutas multipart de la importación de contactos (`Api/Modules/WhatsApp/Controllers/OrganizationWhatsAppContactsController.cs`) y de catálogos, así Kestrel corta antes de bufferear.
- **Copiá de:** `FormCatalogSpreadsheet.cs:124-191`.
- **Tests:** `Api.IntegrationTests/Modules/Forms/FormCatalogSpreadsheetTests.cs`, más uno nuevo de contactos. Se rechazan sin abrir el libro, en los dos lectores:
  - un `.xlsx` con `<f t="array" ref="A1:ZZ100000">`;
  - una hoja guardada fuera de `xl/worksheets/`;
  - un ZIP con 20.000 partes;
  - un archivo de 12 MiB.

  El ejemplo de cada importación sigue entrando.

### Back B · WhatsApp

**B3 · Quitar un teléfono corta la copia a «Mensajes» (R aislamiento)** (T04-01)

`WhatsAppConversationLinkWriter.CopyAsync` filtra solo lo entrante, y al quitar el teléfono los vínculos quedan. Si el número pasa a otra persona, las respuestas de la organización le llegan al dueño anterior.
- `Application/Modules/WhatsApp/Services/WhatsAppContactRemover.cs`: `RemoveMethodAsync` borra los vínculos de ese número y esa cuenta en `engagement.WhatsAppConversationLinks`, como `WhatsAppAccountDataRemover.RemoveLinksAsync` lo hace al borrar la cuenta.
- `WhatsAppConversationLinkWriter.CopyAsync` copia lo saliente solo si el contacto del chat sigue siendo de `link.AccountId`, por si quedó un vínculo viejo.
- **Tests:** en `Api.IntegrationTests/Modules/WhatsApp/WhatsAppConversationLinkTests.cs` y `WhatsAppPhoneLinkObserverTests.cs`, A vincula su chat, quita el teléfono, B verifica el mismo número y la organización le contesta a B: nada aparece en «Mensajes» de A, y el vínculo de A ya no está en la base.

**B4 · El código por correo pasa adelante y las difusiones respetan el módulo apagado (R efectos)** (T03-03, T04-07)
- `Infrastructure/Messaging/Email/EmailLoginCodeChannel.cs` encola los códigos de ingreso, de alta y de verificación con `OutboxPriority.Reply`. Así lo hace `WhatsAppLoginCodeChannel.cs:33` y así lo dice `whatsapp.md` («El turno»: los códigos van con prioridad 0). Hoy caen en `Notice`, detrás de las difusiones.
- `Application/Modules/WhatsApp/Services/WhatsAppBroadcastDispatchService.cs` saltea las organizaciones con el módulo `whatsapp` apagado (`WhatsAppModulePolicy`). La difusión no se cancela: sigue cuando se prende, como todo lo del módulo apagado, que no borra.
- **Tests:**
  - en `Api.IntegrationTests/Messaging/OutboxReservationTests.cs`, con 200 envíos de una difusión en la cola, un código de ingreso por correo sale primero;
  - en `Api.IntegrationTests/Modules/WhatsApp/WhatsAppBroadcastTests.cs`, con el módulo apagado, el worker no encola nada de esa organización y la de al lado sigue.

**B5 · El webhook y el cliente de Meta (R efectos)** (T05-02, T04-05, T04-04)
- **Cuota:** el perfil `WhatsAppWebhook` sube de 120 a 3.000 pedidos por minuto por IP (`PlatformRateLimits.cs:29` y `appsettings.json`). Una difusión genera varios avisos de estado por destinatario, y con pocas IPs de Meta el 429 demora los mensajes de todas las organizaciones. Sin producción, alcanza con el valor inicial; las bases que ya existen lo cambian en Plataforma › Configuración.
- **Cliente:** el `AddStandardResilienceHandler` del `WhatsAppCloudClient` (`Infrastructure/Modules/WhatsApp/WhatsAppInfrastructureModule.cs`):
  - lleva `AttemptTimeout` y `TotalRequestTimeout` iguales a la espera del canal `whatsapp` del Outbox, porque hoy corta a los 10 s y deja «Sin confirmar» lo que tarda entre 10 y 15;
  - deja de tener un circuit breaker común a todas las organizaciones: el Outbox ya reintenta y espera por chat.
- **Fallidos:** `WhatsAppWebhookWriter` guarda el código de un `status: failed` en el `FailureReason` que ya existe (`WhatsAppChatMessageBase`, y el del destinatario en `WhatsAppBroadcast`). Usa el mismo mapeo de `WhatsAppCloudClient.Classify` a `WhatsAppSendFailures`, con 131049 y 131026. «No se pudo enviar» muestra el motivo como con un rechazo síncrono.
- **Tests:**
  - `Api.IntegrationTests/Security/DistributedRateLimitTests.cs`: el perfil inicial;
  - `Api.IntegrationTests/Modules/WhatsApp/WhatsAppCloudClientTests.cs`: una respuesta de 12 s sale confirmada y no «Sin confirmar»; muchos 5xx de un número no cortan los envíos de otro;
  - `WhatsAppBroadcastTests.cs` y el test del webhook del chat: un `failed` con 131049 deja su motivo en el mensaje y en el destinatario.

**B6 · Plantillas recategorizadas y validación al arrancar (R efectos)** (T05-05, T05-08)
- **Recategorizadas:** si Meta pasa una plantilla de Utility a Marketing, hoy el envío deduce el botón «No recibir más» de la categoría nueva (`WhatsAppTemplateReader.cs:96-97`, `WhatsAppReplyIssuer.cs:44,51` y `WhatsAppBroadcastDispatchService.cs:65`) y manda un botón que la plantilla no tiene: fallan todos sus envíos.
  - La plantilla guarda si se creó con el botón, y el envío lo decide por eso, no por la categoría que Meta le pone después.
  - La columna nueva va con su migración, que se agrega antes de usarse ([migración](../guides/migracion.md)): las filas que ya están quedan con el botón si se crearon como Marketing.
- **Al arrancar:** `WhatsAppOptionsValidator` (`Infrastructure/Modules/WhatsApp/WhatsAppOptionsValidator.cs:11`) valida siempre lo que no depende del número. Solo el `AccessToken` queda atado a `PhoneNumberId`, porque Plataforma puede conectar su número desde la pantalla.
- **Tests:**
  - `Api.IntegrationTests/Modules/WhatsApp/WhatsAppTemplateManagementTests.cs`: una plantilla creada como Utility que el sync trae como Marketing sale sin el botón; una creada como Marketing sale con él;
  - `WhatsAppChannelSettingsTests.cs`: con `PhoneNumberId` vacío y un secreto del webhook inválido, el host no arranca;
  - el test de la migración con filas guardadas antes del cambio.

### Back C · Asistente

**B7 · El PDF tiene tope antes de descomprimirse (R efectos)** (T06-03)

`Infrastructure/Modules/Assistant/AssistantTurnContextGuard.cs:48` infla cada stream del PDF entero con PDFsharp (`UnfilteredValue`, que copia todo a memoria sin límite) y recién después compara. Un PDF de 10 MB puede inflarse a varios GB.
- Antes de inflar: un tope de páginas, con el valor de `AssistantTechnicalLimits` que corresponda al tope de tokens.
- Cada stream se infla con un `DeflateStream` propio que corta al pasar lo que queda del tope acumulado de bytes, sin usar `UnfilteredValue`.
- Un PDF pasado de tope se trata como hoy uno demasiado grande: `Assistant.Provider.ContextTooLarge`.
- **Tests:** en `Api.IntegrationTests/Modules/Assistant/AssistantTurnContextGuardTests.cs`, un PDF de 1 MB con un stream que se infla a 2 GB se rechaza sin pasar de un consumo de memoria acotado. Uno normal sigue entrando.

**B8 · Costo, `org_pending` y llamadas repetidas** (T06-01, T06-06, T04-18)
- **Costo:** `Infrastructure/Modules/Assistant/AssistantTurnWorker.cs:164-167` y `:315` pasan `CacheBreakdownComplete=false` con la escritura de caché en `null`, y `AssistantCostPolicy` cobra toda la entrada al precio de escritura de caché de 1 hora, que es el doble. El código no manda `cache_control`, así que no hay escrituras: va con ceros y `CacheBreakdownComplete=true`. Si el proveedor informa lecturas de caché, se usan.
- **`org_pending`:** `Application/Modules/Assistant/Tools/Business/OrganizationPendingTool.cs` acepta los ítems de organización (el pendiente de WhatsApp de un número sin medio de pago, con `Subject` no nulo) en vez de cortar el turno con `QuickQueryNotAvailable`.
- **Llamadas repetidas:** el modelo puede pedir la misma herramienta varias veces en una vuelta. `calls.Count > turn.Tools.Count` deja de fallar el turno; el tope es el de llamadas por vuelta que ya existe, no la cantidad de herramientas distintas.
- **Tests:**
  - `Application.UnitTests/Modules/Assistant/AssistantCostTests.cs`: 10.000 tokens de entrada sin caché cuestan la entrada base, no el doble;
  - `Api.IntegrationTests/Modules/Assistant/AssistantWorkerActorTests.cs`: `org_pending` con un número sin medio de pago contesta con el pendiente, y dos llamadas a la misma herramienta en una vuelta terminan bien.

**Texto para «Arreglos · Back» (cada carril):**

> Leé AGENTS.md, las fichas de docs/rules/ que nombra tu tarea y docs/plans/2026-11-27-etapa-arreglos-revision.md. Cada tarea arregla un hallazgo comprobado de la revisión externa: empezá por su test dirigido en rojo, arreglá lo mínimo que lo pone en verde, sin rediseñar el área, y terminá con el build de la solución. Respetá las reglas que no se negocian, sobre todo una sola forma de guardar, `Result` y los módulos que no se referencian entre sí. Lo que encuentres de paso va a «Para después». Commiteá con la línea «Tarea: arreglos-revision/<id>».

## Front

**F1 · El editor de catálogos guarda lo editado en otras páginas** (T09-01)

En `src/modules/forms/hooks/useCatalogEditor.ts`, `buildRequest` manda solo las filas visibles (la página, lo propuesto por el asistente y las nuevas). `resetDraft` borra el resto con «Guardamos el catálogo», así que lo editado en otra página o antes de una búsqueda se pierde sin aviso.
- `edits` guarda la fila base completa además del cambio, y `buildRequest` manda todas las filas editadas, estén o no en la página.
- El contador de cambios cuenta todas.
- No hay pantalla ni texto nuevo.
- **Copiá de:** cómo `useCatalogEditor` ya conserva lo propuesto por el asistente en otra página (F9 de la E12, `5e2b8ef1`).
- **Tests:** en `CatalogEditorPage.test.tsx`, editar una opción en la página 1, pasar a la 2, editar otra, buscar y guardar: el `PUT` lleva las dos, y después de guardar las dos se ven con su valor nuevo.

**Texto para «Arreglos · Front»:**

> Leé AGENTS.md del front, sus fichas de docs/rules/ y docs/plans/2026-11-27-etapa-arreglos-revision.md del back. La F1 arregla un hallazgo comprobado: empezá por su test en rojo, arreglá lo mínimo, sin pantallas ni textos nuevos, y terminá con `tsc -b` y el lint de lo que tocaste. Con la app levantada, no edites `src`. Commiteá con la línea «Tarea: arreglos-revision/F1».

## Acciones del asistente

No hay: ninguna tarea suma una pantalla que cree, edite o elimine.

## Revisión y cierre

- **La revisión suma a su lista de control:**
  - que B1 no abra otra forma de saber si una cuenta existe;
  - que B2 rechace antes de abrir el libro;
  - que B3 no deje copiar nada de un vínculo viejo;
  - que B5 no aumente la superficie del webhook sin firma.
- **El cierre actualiza la documentación:**
  - `docs/features/identidad.md` (B1);
  - `docs/features/whatsapp.md` (B3, B4, B5 y B6);
  - `docs/rules/cuotas.md` y `docs/operations/configuracion.md`, si nombran el perfil de 120 (B5);
  - `docs/features/asistente.md` (B7 y B8);
  - `docs/backlog.md`: se quitan los ítems que esta etapa deja viejos. El 15 ya no vale, porque `BrokenMarkdownLinks` valida los punteros (`HarnessTests.cs:326,648`), y la parte de la regla 10 del ítem 4 tampoco, porque existe `OperationLogTests`.
- **Para la I1 de la 10:** nada nuevo; sus ajustes están en la B7 de su plan.

## Verificación de la etapa

- Sin tableros nuevos: la galería no suma pantallas.
- **E2E real y recorrido de las áreas tocadas:**
  - el ingreso y las dos altas;
  - la importación de contactos con su ejemplo;
  - un chat de WhatsApp con su vínculo en «Mensajes»;
  - una difusión;
  - un turno del asistente con un PDF;
  - el editor de catálogos con dos páginas editadas.
- **A mano:** nada. El ingreso con Google y el webhook de Meta los cubren los tests de integración.

## Relación con otras etapas

- **Usa de las anteriores:** de la E12, `useCatalogEditor` con el F9 (F1) y el escáner de `FormCatalogSpreadsheet` (B2).
- **Deja para las siguientes:**
  - el escáner común de planillas en `Infrastructure/Spreadsheets/`, para la importación de la E15 y las demás;
  - los ajustes de despliegue en la B7 del [plan de la 10](2026-10-16-etapa-10-despliegue.md).

## Para después

El cierre lo pasa a `docs/backlog.md`, a «Para después de la etapa arreglos-revision». Son los hallazgos reales de la verificación que no entran acá, con su id:

- **Medios:**
  - escala, para antes de pasar de unas 50 organizaciones:
    - los viajes a PostgreSQL por pedido (T03-01: Legal sin caché);
    - el checkpoint por fragmento del streaming (T06-02);
    - el Outbox secuencial sin señal de despertar (el resto de T03-03, sin la prioridad, que arregla la B4);
    - los sondeos del front (T09-04, T04-17);
    - la cola de entrada (T03-10);
    - los índices (T03-13);
  - Meta:
    - el estado «reconectar» ante el 190 y reconectar el mismo número sin crear un canal nuevo (T05-01), que pide tablero;
    - un primer país sin tarifa (T05-06);
    - `account_update` y la calidad del número (T05-07);
  - WhatsApp: el hilo de más de 100 mensajes (T04-10, T09-03), los avisos pegados (T04-15), el Bot que guarda con la versión viva (T09-02) y el front del asistente con `DailyReached` (T04-16);
  - Forms: la `Idempotency-Key` después de un 400 (T09-06);
  - módulos: las guías de retiro sin `CoreTextsNeutralTests` (T01-02), los pares Organización y Plataforma de WhatsApp (T01-03);
  - E10: no van acá; están en la B7 de su plan.
- **Bajos:** los de la tabla de la verificación, salvo los que arregla de paso una tarea de este plan.
- **Decisiones de producto pendientes:**
  - una WABA en dos organizaciones (T02-09);
  - un mensaje para una organización con WhatsApp apagado (T02-11, `backlog.md:167`);
  - el autenticador de un operador que se quita (T07-08);
  - la retención de `OutboxMessages` y `WhatsAppMessages` (T03-07);
  - una conversación guardada que nombra a alguien dado de baja (T06-10);
  - el tope de gasto del asistente en Personal, antes de prenderlo (T06-04);
  - la escala de escritorio al 75 % (T09-11);
  - el arnés de punteros (T01-08).

## Cambios al contrato

## Para decidir

Las cuatro son de producto (lo que ve o puede hacer una persona). Cada respuesta suma su tarea a este plan o pasa a «Para después».

1. **Invitaciones por WhatsApp desde el número de Plataforma** (T05-03).
   - *Ejemplo:* una organización sube 200 teléfonos en «Invitar por teléfono» y salen 200 invitaciones Marketing, pagadas por la plataforma, desde el mismo número que manda los códigos de ingreso. Si Meta lo marca por calidad, se cae el ingreso por WhatsApp de todos.
   - *Recomendada:* un tope diario por organización y otro global para las invitaciones por WhatsApp, con `MessageQuotaBuckets`, como el correo; pasado el tope, la persona se invita por correo.
   - *Alternativa:* dejarlo con los límites de Meta.
2. **Coexistencia con la app WhatsApp Business** (T05-04).
   - *Ejemplo:* una pyme conectó su número en coexistencia y contesta desde el celular; la bandeja web sigue mostrando el chat sin responder y le llega el correo de «Chats esperando».
   - *Recomendada:* leer `smb_message_echoes` y mostrar esas respuestas en el chat como enviadas desde el celular.
   - *Alternativa:* sacar la opción de coexistencia del alta hasta tenerlo.
3. **La respuesta a una plantilla fuera de las 24 h** (T04-03).
   - *Ejemplo:* un operador manda una plantilla a un cliente que escribió hace tres días. El cliente contesta, el chat vuelve al bot con la bienvenida y pierde la asignación (D11 de `whatsapp.md`).
   - *Recomendada:* si en las últimas 24 h salió una plantilla de un operador, el chat sigue con ese operador y su equipo, sin la bienvenida.
   - *Alternativa:* dejarlo como dice D11.
4. **Los chats tomados de alguien suspendido o sin permiso** (T04-11).
   - *Ejemplo:* una organización suspende a un empleado o le saca el permiso de WhatsApp, y sus chats siguen tomados a su nombre sin que nadie los conteste. Además, «Transferir» deja pasar un chat a quien no tiene permiso de WhatsApp.
   - *Recomendada:* al suspenderlo o sacarle el permiso, sus chats vuelven a «Sin tomar» de su bandeja, como al borrar la cuenta, y «Transferir» acepta solo a quien puede atender esa bandeja.
   - *Alternativa:* que queden tomados y los libere a mano un administrador.
