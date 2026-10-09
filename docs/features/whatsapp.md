# WhatsApp

Módulo quitable del número de la plataforma, Etapa 8. El alcance y las tareas están en el [plan ejecutable](../plans/2026-10-07-etapa-8-whatsapp.md); T02–T08 aportan composición, persistencia, adaptadores/outbox cifrado, Phone en identidad/recuperación, webhook durable, bot/enlaces y privacidad. El asistente corresponde a E8b y la atención de organizaciones, a E11.

## Contrato publicado en T01

`WhatsAppWebhookController` es el controller del módulo y delega en `IWhatsAppWebhookService`; `WhatsAppApiModule` y `WhatsAppModule` registran cada capa. Esta ficha documenta el área HTTP `whatsapp`, incluso mientras sus rutas permanecen apagadas.

`GET/POST /webhooks/whatsapp` son rutas técnicas globales, anónimas y condicionadas por `InstalledModuleRoute` y un contribuidor disponible, sin FeatureGate de organización. El contrato [OpenAPI](../contracts/openapi.json) las exporta sin secretos. Sin número de plataforma o sin el par de secretos de webhook devuelve 404 común. GET comprueba token y challenge sin sesión; POST autentica y persiste. Cada capa registra sus propios servicios y Program tiene una sentencia de composición removible. La metadata 501 de T01 permanece congelada: su limpieza está propuesta en «Cambios al contrato», sin alterar unilateralmente el exportado.

El POST declara máximo 5 MiB, firma `X-Hub-Signature-256` sobre bytes originales y deduplicación por IDs Meta/tipo de evento, sin Idempotency-Key. Un mensaje entrante nunca crea sesión. Los enlaces de cuenta común entran como Persona después de Continuar; el núcleo rechaza operadores bajo lock y el bot contesta solo «Para entrar, usá la web.», sin enlace ni revelar su tipo.

El núcleo conoce capacidades de canales, Phone y puertos neutrales; no compara una clave WhatsApp ni lee sus opciones. La invitación conserva identidad/proof Email y puede entregarse a un Phone verificado de esa misma cuenta. La [ficha de identidad](identidad.md) y el [informe de Contrato](../reviews/2026-10-08-etapa-8-contrato.md) distinguen la conducta existente de los pendientes 501.

## Entrada durable y entrega

`WhatsAppWebhookFilter` limita bytes reales a 5 MiB y verifica HMAC antes del binder, incluso si Content-Length miente. Firma inválida produce 401; JSON firmado inválido, 400; cuerpo excesivo, 413. No persiste ni registra el cuerpo original. El reader procesa lotes del PhoneNumberId configurado y omite otros destinos; no resuelve organizaciones del remitente, host o payload.

El ACK 200 sale después del commit. El índice único `(PhoneNumberId, Wamid, EventType)` y los locks de stream evitan duplicados; una colisión de índice revierte y repite en un scope fresco antes de responder. Los callbacks no abren contactos, ventanas de conversación ni tareas de bot. Delivered/read no retroceden por eventos desordenados; una aceptación tardía se reconcilia con callbacks tempranos.

La entrada conserva `OriginalUserId`/`OriginalMethodId` al insertarse. Un mensaje anónimo conserva esos campos nulos aunque después se vincule el número. Solo el texto humano permitido se cifra con DataProtection: OTP contiguo/agrupado, enlaces y tokens quedan como metadatos y no alimentan el bot. Nunca se transfiere historia por el dueño actual del contacto.

`WhatsAppInboundService` reclama con SKIP LOCKED y lease durable; procesa efectos y reply dentro de una UoW. Revalida el lease con reloj fresco tras esperar locks y después del consumer: su vencimiento revierte los efectos y aplica backoff. El worker usa scopes nuevos, la señal solo despierta y la base conserva la cola. Sin consumer registrado no reclama ni descarta mensajes; `WhatsAppBotWriter` aporta el consumidor de bot.

## Bot y enlaces

El bot decide con la cuenta y el método originales de la entrada preparados bajo lock; no busca al dueño posterior para apropiarse de una fila anónima o de historia A→B. Cuenta común disponible recibe un enlace de un solo uso del núcleo; suspendida o sin método disponible no lo recibe. El operador recibe exclusivamente la frase neutra es/en, sin nombre, marca, botón ni enlace. Las demás respuestas usan resources y el nombre configurado de plataforma `Email:AppName`, mediante el adaptador del módulo.

Desconocido con registro abierto recibe un CTA a `/registro`, sin teléfono en query y sin crear identidad. El formulario conserva términos y OTP. La selección de WhatsApp al abrir ese CTA externo necesita el encaje Front registrado en «Cambios al contrato». Con registro cerrado recibe dos botones de respuesta con IDs públicos fijos `bot.sign-in`/`bot.recover`; al elegir uno, el siguiente reply aporta el CTA URL único a ingreso o recuperación. El reader admite esos IDs, no tokens arbitrarios. Es el formato de Cloud API copiado de ArquitecturaBase, sin nuevas plantillas ni pantallas.

Los replies libres conservan en su payload cifrado el cierre de ventana `LastInboundAtUtc + 24 h`. Cloud client vuelve a verificarlo con el reloj actual antes de POST: un reply vencido termina sin llamar a Meta y se limpia el payload. Las plantillas no usan esa ventana. El detalle de preview/canje/replay está en [Identidad](identidad.md#e8--enlaces-de-ingreso).

## Operación y puerta pendientes

El outbox cifra destino/payload, reclama con lease en una transacción, envía a Cloud API fuera de ella y confirma por token en otra; `Sent` significa aceptación, no delivered. Las plantillas y adaptadores de códigos, invitaciones y avisos usan perfil explícito, resources es/en y DisplayFormatter. El catálogo tiene 16 nombres (1 Authentication, 15 Utility), 32 variantes es/en; el lienzo no implica aprobación en Meta. Usar [plantillas](../operations/whatsapp-plantillas.md) y [configuración/túnel](../operations/configuracion.md) antes de la puerta real. La prueba de retirada completa pertenece a Integración; la receta y los tests de modelo no la sustituyen.

## Vínculos y privacidad

`WhatsAppPhoneLinkObserver` recibe la posesión ya verificada por el núcleo dentro de su UoW, también durante un alta antes de agregar el método a EF. Relee el contacto bajo su lock: sólo vincula uno libre o el vínculo propio compatible; no toma el de otra cuenta ni cambia snapshots históricos. Quitar Phone revoca sus enlaces, limpia texto/`ContactId` por propietario y método originales y elimina físicamente el contacto si todavía pertenece a ese vínculo. Un contacto reasignado a B se conserva.

`WhatsAppAccountDeletionParticipant` limpia sólo el paso global de baja, por `OriginalUserId`; no afecta historia de B ni filas anónimas. Retira contactos aún propios y enlaces pendientes. Conserva `OriginalUserId`/`OriginalMethodId` como IDs técnicos pseudónimos e inmutables y los metadatos de dedupe/entrega; la cuenta se anonimiza y los métodos se purgan. El outbox del núcleo bloquea y relee antes de cancelar pendientes, preserva `Sent` y termina esa limpieza antes de encolar el aviso final de eliminación, que sigue cifrado.

El servicio Application de retención borra texto a los **90 días fijos desde `ReceivedAtUtc`** del servidor, con TimeProvider, UoW única y lotes de 100 filas `FOR UPDATE SKIP LOCKED`. El worker de Infrastructure abre un scope por lote al arrancar y una vez por día. Sigue aunque el canal esté apagado; no depende de secretos ni transporte y no corta el outbox para aparentar retención. El inventario para exportación E10 está en [datos personales](../rules/datos-personales.md); E8 no adelanta su endpoint ni participante.

## Errores del módulo

`WhatsApp.Phone.CountryNotAllowed` (400, campo `phone`) corresponde a altas/pending de un país no habilitado, según el E.164 real. `WhatsApp.LoginCode.DailyLimit` (429) devuelve `retryAfter` hasta el próximo día UTC; la reserva de cuota y el código pertenecen a la misma UoW. El webhook devuelve `WhatsApp.Webhook.InvalidSignature` (401), `WhatsApp.Webhook.InvalidPayload` (400) y `WhatsApp.Webhook.TooManyRequests` (429). `WhatsApp.Inbound.LeaseExpired` es un resultado interno de la cola, sin ruta HTTP nueva. Los textos es/en los aporta el módulo mediante `IErrorTextSource`, sin referencias del mapper del núcleo al módulo. Las respuestas de error conservan ProblemDetails con `code` y `traceId`.

## Verificación

`Etapa8ContractTests`, `OpenApiContractTests`, `AccessDeclarationTests`, `ModuleControllersTests`, `LoginLinkIssuerTests`, `LoginMethodsCapabilitiesTests` e `InvitationDeliveryDestinationTests` verifican la parte publicada. `WhatsAppWebhookTests` prueba firma, tamaño, commit, dedupe, callbacks y propietario original; `WhatsAppInboundProcessorTests`, cola, leases, rollback y backoff en PostgreSQL real. `WhatsAppBotTests` de integración recorre webhook firmado→cola→bot, incluidos IDs públicos, operador, propietario original y bloqueo vencido por el reloj inyectado. `LoginLinkTests` verifica canje, cookies/gates y replay; `WhatsAppCloudClientTests`/`WhatsAppOutboxTests` cubren la ventana vencida al despachar. Las suites completas y el recorrido Meta del plan siguen pendientes; E8 no está cerrada.
