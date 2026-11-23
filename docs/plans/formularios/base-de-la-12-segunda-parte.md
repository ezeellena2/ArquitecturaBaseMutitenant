# Base de la 12, segunda parte (spec)

**Estado:** pendiente · spec de lo que va después de la puerta de la base de la 12 y antes de la E12 (decisión 214). Junta lo del traspaso 15 que cae sobre lo ya programado: el núcleo (ADR 0061 a 0063 y las N del backlog), el diseño, las fechas y los números (205 a 213), los roles en áreas plegables con lo incluido (260, que reemplaza a la 212), la IA del Inicio (ADR 0064, 216, 223 y 254), lo de WhatsApp de la E11 (ADR 0065 y 0066, 240) y la CUIT de once ceros. El plan es [`2026-11-19-etapa-base-de-la-12-segunda-parte.md`](../2026-11-19-etapa-base-de-la-12-segunda-parte.md), con lo que se verificó contra el código.

## Qué leer ✓

1. **Decisiones** de la [hoja de ruta](../2026-10-12-formularios-dinamicos-hoja-de-ruta.md): 205 a 214, 216, 223, 240, 254 y 260. ADR [0060](../../decisions/0060-dos-versiones-conviven.md) (la excepción de abajo), [0061](../../decisions/0061-nadie-da-lo-que-no-tiene.md), [0062](../../decisions/0062-retencion-de-los-datos-del-ingreso.md), [0063](../../decisions/0063-topes-de-correo-de-la-plataforma.md), [0064](../../decisions/0064-reglas-de-toda-la-ia.md), [0065](../../decisions/0065-contenido-del-chat-cifrado.md) y [0066](../../decisions/0066-lo-que-no-se-sabe-si-salio.md).
2. **Fichas:** permisos (B16 y B1), multitenancy y concurrencia (purgas, cola y cuotas por alcance), cuotas y cache (topes y lugar de subida), guardado y persistencia-ef (migraciones), datos-personales y logs (retención, cifrado, métricas), fechas-y-zonas, numeros-y-moneda y datos-de-referencia (B10), identificacion-fiscal (B11), api-http y result-y-errores (códigos nuevos), textos-y-traducciones, tests y la guía de migración. Del front: `formatos.md`, `pantallas-y-ui.md`, `tema.md`, `accesibilidad.md`, `formularios.md` y `paginado-y-listados.md`.
3. **Features:** [identidad](../../features/identidad.md), [organizaciones](../../features/organizaciones.md), [roles](../../features/roles.md), [whatsapp](../../features/whatsapp.md), [asistente](../../features/asistente.md#reglas-de-toda-la-ia-traspaso-15-sin-programar) y [personal](../../features/personal.md).
4. **Código para copiar** (este repo): `OrganizationUserAdministratorGuard.cs` (los guardas), `InvitationExpiryWorker.cs` y `AccountDeletionWorker.cs` (las purgas), `WhatsAppCodeQuotaIssuer.cs` (los contadores diarios), `OutboxDispatchStore.cs` (la reserva), `WhatsAppConversationReader.cs` (el estado que sale de la cola), `AssistantKeyProtector.cs` (el propósito por organización), `TenantConnectionInterceptor.cs` (de dónde sale la organización), `AssistantChatClient.cs` (el estado HTTP), `DataClassificationTests.cs` y `TenantTelemetryTests.cs` (las guardas).
5. **Referencia, solo para leer** (`git show origin/main:<ruta>` en `../ArquitecturaBase`, de donde no se copia código): `PermissionDelegationGuard.cs`, `DelegatedAdministrationTests.cs`, `PersonalDataRetentionTests.cs`, `EmailQuotaGuard.cs`, `EmailInvitationDeliveryStatusSource.cs`, `WhatsAppInvitationDeliveryStatusSource.cs`, la migración `AddUncertainSendsAndNextAttempts`, `WhatsAppChatContent.cs`, `AiAssistantUploadSlotAttribute.cs`, `LlmProviderFailures.cs`, `HmacAiKeyRingControl.cs` y `AiKeyRingReadiness.cs`. Para la 260: el traspaso 16, 01 (`docs/plans/2026-11-21-traspaso-multitenant/01-roles-y-permisos.md`), los commits `cbd5ed08`, `39ed143b` y `9f74a236`, y en `../ArquitecturaBaseFront`, `docs/design/roles-y-permisos.md`.

## Pantallas ✓

Lienzo v176. Lo que ya está dibujado:

| Tablero | Qué muestra |
| --- | --- |
| `Criterio-Tablas`, `-Botones`, `-Menus`, `-Dialogos`, `-Formularios`, `-Avisos` | las reglas 205 a 213 con su ejemplo: un dato por columna, el ⋮, lo apagado, la grilla y la frase, el número ambiguo y los avisos según su tipo |
| `Rol`, `Cuenta-Plat`, `Cuentas` (invitar) y sus `M-` | se redibujan en áreas plegables (260): van en «Faltan» |
| `Asistente-Org`, `-Personal`, `-Plat` y sus `M-` | «Subir sin cupo», «Una subida a la vez», «Conversación no disponible» y «Borrar» con «Se borran también sus archivos.» |
| `Asistente-Config-Org`, `Plat-Asistente-Config` y sus `M-` | cada falla del proveedor según quién paga, la clave ilegible y el proveedor desconocido; el diálogo de un modelo en dos pasos y la lectura con «Cambiar datos técnicos» (254) |
| `Asistente-Uso-Org` | el mes como rango y `MonthField` |
| todos los vivos | fechas con coma, porcentaje pegado y avisos blancos con filo |

**Faltan** (se dibujan antes de la F5 y de la F10; la tarea queda escrita):
- **los permisos de un rol en áreas plegables (260),** con los detalles de la RO3-D y lo incluido apagado con su motivo: `Rol`, `M-Rol` y los de rol con alcance de empresa; `Cuenta-Plat`, la invitación de `Cuentas` y sus teléfonos; el ejemplo de permisos de `Criterio-Tablas`. Los de la v175 quedan viejos;
- el estado de entrega de una invitación (`Pending`, `Sent`, `Uncertain`, `Failed`) en `Usuarios`, `Usuario`, `Empresa` (miembros invitados), `Cuentas` y `Cuenta-Plat` (la invitación de un operador), con «Reenviar» que pide revisar con la persona si quedó «Sin confirmar» (0066 §4);
- «Sin confirmar» en un mensaje de `WA-Chats` y `Plat-WA-Chats`, con «Marcar como revisado» y «Lo revisó Ana Gómez · 19/11/2026, 14:35», y los ocho motivos de «No se pudo enviar»;
- «Sin confirmar» en `WA-Difusion-Resultados` (255);
- la (i) de `Auditoria-Plat`: «Los ingresos se guardan doce meses.» (0062 §6);
- sus teléfonos.

## Contrato ✓

**Rutas que cambian.** Todas conservan acceso, permiso y cuotas generales; lo nuevo es aditivo.

| Ruta | Cambio |
| --- | --- |
| `GET /api/permissions` | cada permiso suma `includes` (260): los códigos que incluye, de su misma área y su mismo catálogo; `[]` si no incluye nada |
| `POST /api/roles`, `PUT /api/roles/{id}` | 403 `Roles.Role.PermissionsNotHeld`; guardan lo pedido más lo incluido (260), y el `GET` devuelve eso |
| `POST /api/users/invitations`, `PUT /api/users/{memberId}` | 403 `Tenancy.Member.RolesNotHeld`; `PUT` también `Tenancy.Member.HasMorePermissions` |
| `PUT /api/users/{memberId}/status`, `DELETE /api/users/{memberId}` | 403 `Tenancy.Member.HasMorePermissions` |
| `POST /api/companies/{companyId}/members`, `PUT …/members/{memberId}/roles` | 403 `Companies.Membership.RolesNotHeld`; `PUT` también `…HasMorePermissions` |
| `PUT /api/companies/{companyId}/members/{memberId}/status` | 403 `Companies.Membership.HasMorePermissions` |
| `POST /api/platform/operators/invitations`, `PUT /api/platform/accounts/{id}/operator` | 403 `Platform.Operator.PermissionsNotHeld`; guardan lo pedido más lo incluido (260) |
| `POST /api/platform/accounts/{id}/suspend`, `/reactivate`, `/deletion`, `DELETE /api/platform/accounts/{id}/operator` (sobre un operador) | 403 `Platform.Operator.HasMorePermissions` |
| `POST /api/auth/login-code`, las de código del registro, del registro de empresa y de recuperación, `POST /api/me/login-methods` y `/{methodId}/code` (correo) | 429 `Messaging.Email.DailyLimit` con `retryAfter` |
| invitar y reenviar: `POST /api/users/invitations`, `…/invitations/{id}/resend`, `POST /api/companies/{companyId}/members`, `…/invitations/{id}/resend`, `POST /api/platform/operators/invitations`, `…/{id}/invitation/resend` (por correo) | 429 `Messaging.Email.DailyLimit` con `retryAfter` |
| `POST /api/me/login-methods` y `/{methodId}/code` (teléfono) | 429 `WhatsApp.LoginCode.AccountDailyLimit` con `retryAfter` |
| `GET /api/users/paged` y `/{memberId}`, `GET /api/companies/{companyId}/members/paged`, `GET /api/platform/accounts/paged` y `/{id}` | suman `invitationDelivery`: `Pending`, `Sent`, `Uncertain`, `Failed` o `null` sin invitación pendiente |
| `GET {base}/conversations/{id}` (las tres bases de WhatsApp) | `status` suma `Uncertain`; `failureReason` usa los códigos de abajo; suman `reviewedByName` y `reviewedAtUtc` |
| `GET` de los resultados de una difusión | el destinatario suma `Uncertain` |
| `POST {org\|me\|platform}/assistant/conversations/{id}/attachments` | antes de leer el cuerpo: 429 de siempre sin cupo (`Assistant.Limit.DailyReached`/`MonthlyReached`); 429 `Assistant.Attachment.UploadBusy` y `UploadQueueFull` con `retryAfter`; 400 `Assistant.Attachment.UploadTimedOut` a los 120 s; la misma clave con otro archivo, 409 `Request.IdempotencyKeyReused` |
| listado de conversaciones del asistente | la fila suma `available` (`false`: sin título ni mensajes) |
| `GET …/assistant/conversations/{id}` | 409 `Assistant.Conversation.Unavailable` si no se puede leer; `DELETE` la borra igual |
| `GET/PUT /api/organization/assistant/settings` y `/api/platform/assistant/settings` | suman `providerIssue` (código o `null`) y `unsupportedProvider` |
| `GET /api/reference-data` | cambian valores, no la forma: `DateTimePattern` con coma y `PercentPattern` de es-AR `{number}%`; `LongDatePattern` queda sin uso |

**Lo incluido (260).** Un permiso que no sirve sin el «Ver» de su misma área lo incluye, dentro de cada catálogo y nunca entre áreas ni entre catálogos. Lo que tiene una cuenta son los permisos de sus roles (o, en un operador, los suyos) más lo que esos incluyen, en cadena; las autorizaciones y los 403 de la 0061 comparan contra eso. El back entra antes que el front o junto con él: un front nuevo con un back viejo falla al leer `includes`.

**Rutas nuevas.** `GET /api/platform/permissions`: Plataforma no tenía catálogo en la API (el front lo arma en `permissionCatalog.ts` con sus módulos). Devuelve cada permiso de `PlatformPermissions.All`, con los de los módulos instalados, como `{ code, includes }`. Lleva `[Access(Platform)]` y `platform.accounts.read`, el permiso de la pantalla desde donde se invita y se edita a un operador. El front sigue tomando nombres y orden de sus módulos, y de acá solo `includes`. En K1 va con `includes` vacío.

`POST {base}/conversations/{id}/messages/{messageId}/review`, con `{base}` = `/api/organization/whatsapp/customers` (`whatsapp.customers`), `/internal` (`whatsapp.internal`) y `/api/platform/whatsapp` (`platform.whatsapp.attend`): los mismos atributos de acceso, permiso y módulo que su controller. Sin cuerpo. 200 con el `WhatsAppChatMessageResponse`. Otro chat, otra organización o un mensaje que no es de ese chat: 404. Si no está «Sin confirmar»: 409 `WhatsApp.Message.NotUncertain`. Revisar dos veces devuelve la primera revisión. No lleva `[Idempotent]`: no crea ni envía.

**Códigos de falla** (no son respuestas HTTP):
- WhatsApp, en `failureReason`: `WhatsApp.Send.NotAllowed`, `WindowClosed`, `Undeliverable`, `RateLimited`, `TokenInvalid`, `PermissionMissing`, `Unavailable` y `Rejected`, más `ReadOnly` y `PaymentRequired`. Reemplazan a `RecipientNotAllowed`, `WindowExpired`, `Credentials` y `Permissions`; la migración pasa los guardados.
- Asistente, en el evento `error` y el estado del turno: `Assistant.Provider.CredentialRejected` (401 y 403), `Limited` (402 y 429), `ModelRejected` (400 y 404), `Rejected` (otro 4xx), `Uncertain` (5xx), `Unavailable` (sin conexión, DNS, TLS o 10 s), `KeyUnreadable`, `UnsupportedProvider` y `PlatformUnavailable` (lo que ve una organización o una persona con la clave de la plataforma cuando la falla la arregla el operador). `providerIssue` toma `CredentialRejected`, `Limited`, `ModelRejected`, `Unavailable`, `KeyUnreadable` o, en la organización con la clave de la plataforma, `PlatformUnavailable`.

**Textos.** Los del núcleo, en `Errors.resx` y `.en.resx`; los de WhatsApp y del asistente, en los recursos de su módulo (`WhatsAppTexts`, `AssistantTexts`), porque el núcleo no nombra módulos (N22). `numberAmbiguous` es un texto de validación del front, en es y en.

**Concurrencia.** Nada nuevo lleva versión: revisar es idempotente. `docs/contracts/format-cases.json` cambia con B10 y lo prueban los dos lados.

## Datos ✓

Cada migración se agrega y se escribe en el mismo despliegue. Es la excepción de la 0060, anotada en cada migración: todavía no hay despliegue (la E10 va después del bloque, 123), y volver a la imagen anterior no aplica.

- **Lo incluido (260):** sin columnas nuevas. Los roles que creó cada organización y los permisos de cada operador se completan una vez con lo que incluyen y les falta; los de sistema ya tienen todo su catálogo.
- **`identity.MessageQuotaBuckets`** (global, sin RLS, como `identity.WhatsAppCodeQuotas`): `Scope` (`Global`, `CodeIp`, `CodeAccount`, `Inviter`, `Organization`, `WhatsAppCodeAccount`), `KeyHash` (HMAC de la IP, la cuenta o la organización; `all` en el global), `HourUtc`, `Count`. PK `(Scope, KeyHash, HourUtc)`. Las 24 horas móviles suman las 24 horas cerradas y la actual; la purga borra las de más de 25 horas.
- **Purgas (0062):** índices de `identity.LoginCodes` por fecha, de `identity.LoginAudits` por cuenta y fecha, y de `tenant.Invitations` parcial sobre las cerradas; OpenIddict con `PruneAsync`.
- **`platform.OutboxMessages`:** `Status` suma `Uncertain`; columnas `OrderingKey` (texto, nulo: el HMAC del destino que da el canal), `Priority` (`smallint`, 0 contesta, 1 avisa; 1 por defecto) y `DiscardAfterUtc` (nulo). Índices parciales `(Priority, NextAttemptAtUtc, Id)` y `(OrderingKey, CreatedAtUtc, Id)` sobre `Pending` y `Failed`.
- **`tenant.Invitations`:** `OutboxMessageId` (nulo). La invitación de un operador lo suma donde la guarda `PlatformOperatorInvitationService`. El estado de entrega no se guarda: lo lee el reader desde la cola, como `WhatsAppConversationReader`. Sin fila en la cola o sin id, `Sent`.
- **WhatsApp:** `Text` de `tenant.WhatsAppChatMessages` y `platform.PlatformWhatsAppChatMessages`, `LastMessagePreview` de `tenant.WhatsAppChats`, `platform.PlatformWhatsAppChats` y `engagement.WhatsAppConversationLinks`, y `Text` de `engagement.WhatsAppLinkedMessages` pasan a `bytea`. La migración vacía el contenido (son datos de prueba, 0065 §7) y su `Down` vuelve a `text` vacío. Los mensajes suman `ReviewedByUserId`, `ReviewedByName` y `ReviewedAtUtc`, que la baja de la cuenta olvida como `ForgetAuthor`. `failureReason` pasa a los códigos nuevos.
- **Asistente:** el turno suma `EngineVersion` (`int`, 1) y `TraceParent` (nulo); la configuración de la organización y la de la plataforma suman `ProviderIssueCode` y `ProviderIssueAtUtc`; `platform.AiKeyRingControls` (`KeyId` PK, `Control` `bytea`, `CreatedAtUtc`), global y sin borrado; índices parciales de los turnos pendientes y de los adjuntos sin confirmar.
- **Datos de referencia:** `cultures.source.json` con los patrones nuevos, regenerado con `generar.mjs`. `LongDatePattern` queda en la tabla sin uso y se borra en un despliegue posterior (0060).

## Pasos ✓

### Back

1. **B16 · Un permiso incluye a otro (260), antes de la B1,** que compara con lo efectivo. Toma de ArquitecturaBase `cbd5ed08` (la regla), `39ed143b` (completar al arrancar) y `9f74a236` (los 403):
   - `Permissions.Includes` y `PlatformPermissions.Includes` en Domain, con los pares de la 260; `users.invite` y `roles.assign` se confirman contra sus pantallas. El catálogo valida al componerse que lo incluido existe y es de su misma área y su mismo catálogo;
   - `PermissionItem.Includes` en `GET /api/permissions` y `GET /api/platform/permissions`;
   - una sola función de lo efectivo (lo asignado más lo que incluye, en cadena), que usan `PermissionService`, `PlatformPermissionService` y las guardas de la B1. `RolePermissionCache` guarda lo efectivo;
   - `RoleService`, `PlatformOperatorService` y `PlatformOperatorInvitationService` guardan lo pedido más lo incluido;
   - los roles que ya creó cada organización y los permisos de los operadores se completan una vez, recorriendo las organizaciones con su RLS: una migración de datos o el arranque, sin autor en su historia, que avanza `t:<organización>:roles`. Lo efectivo queda como red mientras conviven dos versiones (0060);
   - la regla va a la ficha `permisos`, la guía `permiso-nuevo` (un permiso nuevo declara lo que incluye) y la feature `roles`.

   **Tests:** lo efectivo, también en la caché; el catálogo que rechaza lo incluido de otra área o de otro catálogo; guardar un rol y un operador; completar un rol viejo en dos organizaciones, sin cruce; y los 403 que pedían «Administrar» sin «Ver», que dejan de probar lo que decían y se corrigen (en ArquitecturaBase fueron cinco).
2. **B1 · Nadie da lo que no tiene.** `OrganizationDelegationGuard` (`Services/Roles/`), `CompanyDelegationGuard` (`Services/Companies/`) y `PlatformDelegationGuard` (`Services/Platform/`), copiando `OrganizationUserAdministratorGuard`. Cada uno relee, dentro del límite y después de los locks, los permisos efectivos de quien pide en ese alcance, con la proyección que lee `PermissionAuthorizationHandler` y solo con el catálogo vigente. Lo usan `RoleService`, los casos de `OrganizationUsers` (invitar, editar, estado y quitar), `CompanyMembershipService` (sumar, roles y estado), `PlatformOperatorService`, `PlatformOperatorInvitationService` y la baja, suspensión y reactivación de una cuenta que es operador. **Tests:** `Organization/DelegatedAdministrationTests` con cuentas que tienen solo cada permiso en los tres alcances; sobre uno mismo pasa; el Dueño pide todo el catálogo.
3. **B2 · Retención del ingreso.** `IdentityRetentionWorker` (global: códigos a 30 días, ingresos a 12 meses salvo el último de cada cuenta, OpenIddict a 14 días de vencido o revocado, y los contadores de B3) e `InvitationRetentionWorker`, con `TenantJobRunner`: cerradas a 30 días salvo la última de cada destino, nunca una pendiente. Lotes de 500 en transacciones cortas, copiando `InvitationExpiryWorker`, y el log con la cantidad o el tipo de la excepción. Antes de borrar, revisá que la auditoría de la organización siga nombrando a quien no tiene nombre. `ArchitectureTests/PersonalDataPurgeTests`, copiando `DataClassificationTests`, exige que cada tabla con datos personales nombre su purga o por qué se conserva. **Tests:** cada plazo, la última que queda y dos organizaciones sin cruce.
4. **B3 · Topes de correo y de códigos.** `EmailQuotaOptions` (`Email:Quotas:*`, validadas al arrancar), `EmailQuotaGuard` (`Services/Messaging/`) y `MessageQuotaBuckets`, copiando `WhatsAppCodeQuotaIssuer`. Con la reserva de invitaciones, los avisos en el log al 80 % y al llegar, y los valores del plan. Lo llaman los emisores de códigos por correo y los de invitaciones por correo de los tres alcances. `WhatsAppCodeQuotaIssuer` suma 10 por cuenta y día desde Mi cuenta. `IpAddressKey.Normalize` (`Infrastructure/Security/`) lleva la IPv6 a su /64 antes del HMAC en `RateLimitIpHasher` y `WhatsAppQuotaIpHasher`. Las claves van en `docs/operations/configuracion.md`. **Tests:** la ventana móvil, la reserva, el 429 con `retryAfter` en cada ruta, dos organizaciones con su tope y el global compartido, y dos IPv6 del mismo /64.
5. **B4 · El bloqueo no se usa contra otro.** `GoogleAccountGuard` y `ExternalLoginService` dejan de mirar el bloqueo por códigos fallidos; la suspensión y la baja siguen. En `LoginCodeAttemptVerifier`, con la cuenta bloqueada, un código correcto responde `Identity.Account.LockedOut` sin consumirse. **Tests:** unitarios de los dos y uno de integración del ingreso.
6. **B5 · La cola no repite lo dudoso.** `OutboxStatus.Uncertain`; `OutboxMessage` con `OrderingKey`, `Priority` y `DiscardAfterUtc`; un lease vencido pasa a `Uncertain` (`Outbox.Delivery.Interrupted`) y no se vuelve a reclamar. `OutboxDeliveryReceipt.Uncertain(code)`. `OutboxDispatchStore.LockIdsAsync` ordena por prioridad, salta un destino con algo anterior pendiente o en curso y cancela lo vencido (`Outbox.Delivery.Expired`). `OutboxDeliveryProcessor`: lo que falla antes de llamar al transporte vuelve a pendiente, y una excepción durante la llamada queda `Uncertain`. `IOutbox.Enqueue` suma `OutboxEnqueueOptions`. `OutboxOptions` por canal (espera y tope de intentos), con un validador que deja la entrega y la confirmación dentro del apagado. `SmtpEmailTransport` y `EmailChannelSender` clasifican: antes de `DATA` vuelve a pendiente, un 5xx de destinatario falla, después de `DATA` o con la espera vencida queda «Sin confirmar». `EmailLoginCodeChannel` pone `DiscardAfterUtc`. El chequeo `email` queda en Degraded con un 535 hasta el próximo envío, y un `Meter` cuenta enviados y descartados por canal. **Tests:** las transiciones del dominio; en `Messaging/`, el orden por destino, la prioridad, el lease vencido, cada clase de SMTP, el código vencido y el chequeo.
7. **B6 · Estado de entrega de la invitación.** Al emitir y al reenviar, la invitación guarda `OutboxMessageId`: la de la organización, la de una empresa, la del teléfono y la del operador. Los readers de usuarios, miembros de empresa y cuentas de Plataforma derivan `invitationDelivery` de la cola, como `WhatsAppConversationReader`. **Tests:** cada estado por correo y por WhatsApp, y una organización que no ve el estado de otra.
8. **B7 · WhatsApp «Sin confirmar».** `WhatsAppCloudClient` clasifica sin leer más que el código de Meta:
   - sin conexión, 429 o un código pasajero: se reintenta a los 30 s, dos veces;
   - los códigos que no cambian: fallan con los ocho códigos;
   - la espera vencida, la respuesta cortada, un 5xx sin código o una respuesta ilegible: «Sin confirmar».

   La cola de WhatsApp lleva `OrderingKey` (HMAC del número y del destino) y `Priority` 0 para el operador, el bot, los códigos y las confirmaciones, y 1 para las difusiones. `WhatsAppConversationReader` y `WhatsAppBroadcastReader` derivan `Uncertain` cuando el mensaje sigue pendiente y la cola dice `Uncertain`: un aviso de Meta posterior manda. `ReviewAsync` en `WhatsAppConversationService` y `PlatformWhatsAppConversationService` (UoW, quien y cuando), la ruta en `WhatsAppConversationsControllerBase` y los textos en `WhatsAppTexts`. **Tests:** la tabla de respuestas de Meta en `WhatsAppCloudClientTests`; la ruta en las tres bases (403 sin permiso, 404 ajeno, 409 y la segunda revisión); el orden de un chat y el bot antes que una difusión.
9. **B8 · El chat cifrado.** `WhatsAppChatContentConverter` (`ValueConverter<string?, byte[]?>`) en `Infrastructure/Modules/WhatsApp/Persistence/`. El protector sale de `ITenantContext`, el mismo que usa `TenantConnectionInterceptor`, y se resuelve en cada conversión con propósito `ArquitecturaBaseMultitenant.WhatsApp.ChatContent.v1`: en `tenant.*`, con la organización; en `platform.*`, con `platform`. En `engagement.*` va `…WhatsApp.LinkedContent.v1`. Sin organización en `tenant.*`, lanza; lo que no abre se lee `null`. Si el chat no puede probar que sigue al alcance en la petición, en `ITenantScope.Enter` y en `TenantJobRunner`, con dos contextos en paralelo, pasa a un tipo `ChatContent` en repositorios y lectores y lo anota en la 0065 §4. Se suman las líneas de `runbook.md`, `whatsapp.md` y `datos-personales.md`. **Tests:** `Tenancy/WhatsAppChatContentIsolationTests`: el SQL crudo no muestra texto, un cifrado copiado a otra organización se lee vacío, la bandeja busca por nombre y número, y una guarda de que ningún lector filtra por el contenido.
10. **B9 · Lo transversal.** `ServiceDefaults/Extensions.cs` con `DisableForUnsafeHttpMethods`. `RequestLogScopeMiddleware` (`Api/RequestContext/`) pone en el scope los ids de la persona y de la organización, y la consola va en JSON fuera de Development. `ArchitectureTests/CoreTextsNeutralTests` rechaza «WhatsApp» y «asistente» en los recursos del núcleo, salvo los nombres de permisos del catálogo, en una lista escrita con su motivo; lo demás pasa al módulo. `MetricTagsTests`, junto a `TenantTelemetryTests`, rechaza etiquetas de organización, empresa, teléfono o contenido. Salen de `appsettings.Development.json` el `ClientId` de Google, `Email:Smtp:UserName`, `FromAddress` y los ids de Meta; `StartupConfigurationTests` se invierte, y van las §2, §3 y §7 de `configuracion.md`.
11. **B10 · Fechas y porcentaje.** `cultures.source.json` y `generar.mjs`; `format-cases.json` con la coma, el mes como rango y el porcentaje pegado. `DisplayFormatter.FormatMonthRange(month, context, MonthRangeStyle.Sentence|Label)` reemplaza a `FormatMonth` y `FormatMonthLabel`, y `FormatPercent` usa el patrón. `AssistantBudgetEmailRenderer` dice «del 01/10/2026 al 31/10/2026». **Tests:** los casos de los dos lados y el correo del tope.
12. **B11 · La CUIT nunca once ceros.** `ArgentineCuitValidator` rechaza `00000000000`, con su test de dominio y la línea de `identificacion-fiscal.md`.
13. **B12 · El cupo y las subidas.** Tests de que recuperar o vencer un turno sin `BeginCall` devuelve el cupo diario y la reserva del mes, y con `BeginCall` no. `ListReadyAsync` toma 4. `AssistantUploadSlotAttribute`, un filtro de recurso en `Api/Modules/Assistant/`, corre antes de enlazar `[FromForm]`, que es lo que lee el cuerpo. En orden:
    - mira el cupo diario y el mensual con `IAssistantUploadAdmission`;
    - toma el lugar de la persona en Redis con `SET NX` y vencimiento; sin Redis, el de la copia (0058);
    - toma el de la copia: 4 a la vez, 8 en espera y 2 por organización;
    - le da al cuerpo 120 s.

    Las opciones van en `Assistant:Uploads:*`. **Tests:** sin cupo no guarda nada, la segunda subida, la cola por organización y la misma clave con el mismo archivo o con otro.
14. **B13 · Las fallas del proveedor.** `AssistantProviderFailureClassifier` (Infrastructure) traduce lo que ya entrega `AssistantChatClient`, y el worker deja de mandar todo a `Unavailable`. `AssistantAvailabilityPolicy` prueba descifrar la clave antes de reservar (`KeyUnreadable`, sin gastar cupo). La configuración guarda y limpia `ProviderIssue*`. Un proveedor guardado que esta versión no conoce pausa: con la clave propia, a esa organización, y con la de la plataforma, a todas; guardar sin clave lo descarta. **Tests:** cada estado HTTP, la clave ilegible, quién ve qué según quién paga y el proveedor desconocido.
15. **B14 · Versiones, borrado y fecha.** El worker toma solo turnos con `EngineVersion` conocido; el resto vence y devuelve el cupo (B12). Una conversación ilegible sale `available: false`, el `GET` da 409 y se puede borrar. Van los índices parciales. `AssistantTurnPreparationBuilder` usa primero la zona del perfil y pone la hora del pedido al final del mensaje, como el canal (0054 §1). **Tests:** el turno de otra versión, la conversación ilegible y que el pedido al proveedor de una persona no lleva nada de otra (216).
16. **B15 · El anillo y la traza.** `AiKeyRingControlService` y `HmacAiKeyRingControl`: la primera copia escribe un HMAC por clave y las demás lo recalculan. El chequeo `ai-keyring` se calcula al arrancar y deja la copia sin estar lista si no coincide: no toma turnos ni entradas de WhatsApp. El turno guarda `TraceParent`, y el worker abre una actividad con ese padre y la marca en error si falla. **Tests:** dos anillos contra la misma base y la traza.

### Front

1. **F1 · Fechas y números.** En `shared/format/`: la fecha con coma desde la cultura, `formatMonth` como rango, `formatPercent` pegado, y `parseDecimal` con la región de la cultura, que marca «1.500» como `numberAmbiguous`. `MonthField` muestra el rango cerrado y los meses con su número abierto, como `Asistente-Uso-Org`. **Tests:** `formatters.test.ts`, `parsers.test.ts`, `month.test.ts` y los casos compartidos.
2. **F2 · Avisos.** `sonner.tsx`, `index.css`, `FormError`, el cartel que bloquea y las franjas del teléfono, blancos con filo e ícono. `toastError` se va a los 10 s, y uno con acción queda. **Tests:** cada tipo y la duración; capturas en la compu y el teléfono.
3. **F3 · Lo apagado y la (i).** `InfoTip`, `Button.disabledReason`, `CheckboxField`, `RadioGroupField` y `RowAction`: se abren con clic, toque, Enter o Espacio sin hacer la acción, y se cierran con Escape o tocando afuera. Responde el control entero. `TooltipContent` respeta los saltos. En el teléfono reemplaza la franja oscura y la hoja chica. **Tests:** teclado, toque y axe.
4. **F4 · Un dato por columna y el ⋮.** `DataTable` vuelve a dibujar un texto secundario en gris que no se ordena; se revisan las columnas del núcleo que juntan dos datos y los encabezados con el ⋮. **Tests:** la celda y cada columna cambiada.
5. **F5 · Roles en áreas plegables (260),** con sus tableros. `PermissionPicker` y `RoleEditorPage` conservan su forma, con los detalles de la RO3-D:
   - toda área lleva su casilla (vacía, parcial o completa), también la de un solo permiso; se abren las que tienen algo, y un rol nuevo arranca con todas cerradas;
   - sin cuentas, buscador ni filtros; qué permite cada permiso va en la (i) del área, uno por renglón;
   - el Dueño y todo rol de sistema que no se cambia se leen con tildes, sin casillas y con sus áreas cerradas;
   - en el teléfono, filas de 44 px, y la flecha y la casilla del área con una zona de 44 px cada una, sin pisarse;
   - lo incluido se ve marcado y apagado, con su motivo en `disabledReason` («Lo incluye «Administrar usuarios».»), y lo toma de `includes`, sin repetir la lista. El editor siembra, compara y guarda lo que el rol permite, y el 409 compara igual.

   Va en la organización, con alcance de empresa, y en los permisos de un operador (`OperatorPermissionsDialog`, `InviteOperatorDialog` y `AccountPermissionsTab`). **Tests:** `PermissionPicker.test.tsx` y `RoleEditorPage.test.tsx`: lo incluido con su motivo, el Dueño cerrado y los toques en la flecha y en la casilla del área a 390 y 320.
6. **F6 · Las piezas.** El título de un grupo con su (i), el chip de solo lectura sin ✕, las teclas que mueven la pantalla con el foco en el menú, el desplegable de 48 en el teléfono y las confirmaciones del núcleo en una frase. **Tests:** de cada pieza.
7. **F7 · Sesión, conteos, topes y CUIT.** `BroadcastChannel` para que salir cierre las pestañas de ese lado (la pública o la de Plataforma). Cada pantalla pide solo los conteos que muestra. Una espera larga en horas y minutos. Los textos de los 403 y 429 nuevos. `taxIdValidation.ts` rechaza los once ceros. **Tests:** de cada uno.
8. **F8 · El asistente.** El borrado dice «Se borran también sus archivos.». Van también los estados de subida, la conversación no disponible, la pestaña que descarta lo desconocido y las fallas en las dos configuraciones, con `providerIssue` y `unsupportedProvider`. `ModelEditorDialog` va en dos pasos y con lectura y «Cambiar datos técnicos», sin plegar. **Tests:** cada estado y `ModelCatalog.test.tsx`.
9. **F9 · Los motivos de WhatsApp.** Los ocho códigos, con su texto, en `MessageThread.tsx` y en los resultados de una difusión. **Tests:** cada código.
10. **F10 · «Sin confirmar» y la entrega**, con los tableros que faltan: `invitationDelivery` en Usuarios, Usuario, los miembros de una empresa y las cuentas de Plataforma; «Sin confirmar» y «Marcar como revisado» en la conversación de la organización y de Plataforma, y en una difusión; la (i) de Auditoría de Plataforma. **Tests:** cada pantalla y axe.

## Qué verificar al arrancar ✓

Está contrastado en la tabla «Qué se verificó al arrancar» del plan. Al arrancar, el chat del contrato confirma la K0 (la puerta de la base de la 12 cerrada) y anota lo que difiera en «Cambios al contrato».

## Cómo se cierra ✓

Con una sola puerta (`/puerta`), sobre copias del corte:
- las suites completas de los dos repos y `contracts:check`;
- el E2E real, con los detectores de documentación en 0;
- la galería de lo cambiado a 1440, 390 y 320;
- el recorrido.

Sin prueba con Meta: lo que pide un número conectado queda para la I1 de la E10 (123).
