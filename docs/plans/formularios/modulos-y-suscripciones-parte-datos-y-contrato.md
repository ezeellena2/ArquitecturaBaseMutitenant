# Módulos y suscripciones: datos y contrato (spec, parte)

**Estado:** pendiente · parte de la [spec de Módulos y suscripciones](modulos-y-suscripciones.md), que dice qué leer, qué se construye y cómo se cierra.

## Contrato ✓

### La organización

Las rutas van en `Api/Controllers/Organization/OrganizationModulesController.cs`, bajo `/api/organization/modules`, con `[Access(Business)]` y `[HasPermission(Permissions.Modules.Manage)]`. No llevan `[FeatureGate]`: son del núcleo y responden con el módulo apagado. `{key}` es la clave del módulo: si no está en el catálogo o no se ofrece, la respuesta es 404.

| Ruta | Pide | Devuelve |
| --- | --- | --- |
| `GET /` | — | 200 `[OrganizationModuleResponse]` |
| `GET /{key}/checkout` | — | 200 `{priceId, price, firstChargeOn, includedLimits[], publicKey, mode}` |
| `POST /{key}/requests` `[Idempotent]` | `{}` | 201 `{id}` |
| `DELETE /requests/{id}` | `{version}` | 204: cancela un pedido pendiente |
| `POST /{key}/trial` `[Idempotent]` | `{}` | 201 `{endsAtUtc}` |
| `POST /{key}/subscription` `[Idempotent]` | `{priceId, cardToken, consent}` | 201 `{id, state}`; `state` es `Pending` si Mercado Pago no confirmó |
| `PUT /{key}/subscription/card` | `{version, cardToken}` | 200 `VersionResponse` |
| `POST /{key}/subscription/cancel` | `{version}` | 200 `VersionResponse` |
| `POST /{key}/exports` `[Idempotent]` | `{}` | 202 `{id}` |
| `GET /exports/{id}` | — | 200 `{id, key, state, readyAtUtc?, sizeBytes?}` |
| `GET /exports/{id}/file` | — | 200 `.zip`, adjunto, `no-store` y `nosniff` |

**`OrganizationModuleResponse`:**
- `{key, state, price?, nextPrice?, trial, request?, subscription?, data?, can[]}`;
- `state` es `Off`, `Trial`, `Requested`, `On`, `Subscribed`, `Grace`, `Ending` o `Suspended`;
- `price` es `Money` (`{amount, currency}`), y `nextPrice` suma `startsOn`;
- `trial` es `{allowed, days, used, endsAtUtc?}`;
- `request` es `{id, state, reason?, version}`, con el motivo del rechazo;
- `subscription` es `{id, state, nextChargeOn?, endsOn?, graceUntilUtc?, cardLast4?, version}`;
- `data` es `{offSinceUtc, purgeOn, export?: {id, state}}`;
- `can` trae `request`, `cancelRequest`, `trial`, `subscribe`, `changeCard`, `cancel` y `export`, y lo calcula el servidor.

**`cardToken`** sobrescribe `ToString()`, igual que el contrato que lo lleva. Nunca se loguea, se guarda ni se audita. `consent` tiene que ser `true`.

### Plataforma

Todas las rutas llevan `[Access(Platform)]`. Los cambios piden motivo y dejan su `SecurityEvent` (`PlatformActionGuard`).

| Ruta | Permiso | Pide | Devuelve |
| --- | --- | --- | --- |
| `GET /api/platform/modules` | `platform.modules.read` | — | 200 `[PlatformModuleResponse]` |
| `GET /api/platform/modules/{key}` | `platform.modules.read` | — | 200, con sus precios |
| `PUT /api/platform/modules/{key}` | `platform.modules.manage` | `{offered, allowsRequest, allowsTrial, allowsSubscription, version, reason}` | 200 `VersionResponse` |
| `POST /api/platform/modules/{key}/prices` `[Idempotent]` | `platform.modules.manage` | `{amount, startsOn, limits[{key, value}], reason}` | 201 `{id}` |
| `GET /api/platform/modules/requests/paged` y `/filter-counts` | `platform.modules.read` | estado y organización | una página |
| `POST /api/platform/modules/requests/{id}/approve` `[Idempotent]` | `platform.modules.manage` | `{version, reason, expiresOn?}` | 200 `VersionResponse` |
| `POST /api/platform/modules/requests/{id}/reject` `[Idempotent]` | `platform.modules.manage` | `{version, reason}` | 200 `VersionResponse` |
| `GET /api/platform/modules/subscriptions/paged` y `/filter-counts` | `platform.modules.read` | estado, módulo y organización | una página |
| `GET /api/platform/modules/subscriptions/{id}` | `platform.modules.read` | — | 200, con sus cobros |
| `GET /api/platform/tenants/{id}/features` (cambia) | `platform.tenants.read` | — | suma `rows[{source, state, expiresAtUtc?, graceUntilUtc?, suspendedAtUtc?}]` |
| `GET /api/platform/tenants/{id}/limits` | `platform.tenants.read` | — | 200 `[{key, module, platformMax, planValue?, override?, effective}]` |
| `PUT /api/platform/tenants/{id}/limits/{key}` | `platform.tenants.manage` | `{value, expiresOn?, version, reason}` | 200 `VersionResponse` |
| `DELETE /api/platform/tenants/{id}/limits/{key}` | `platform.tenants.manage` | `{version, reason}` | 204 |
| `GET` y `PUT /api/platform/settings` (cambian) | `platform.settings.manage` | suman los cinco plazos | como hoy |
| `GET /api/platform/payments/mercadopago` | `platform.settings.manage` | — | `{publicKey?, accessTokenLast4?, webhookSecretLast4?, mode, webhookUrl, liveAvailable, version}` |
| `PUT /api/platform/payments/mercadopago` | `platform.settings.manage` | `{publicKey, accessToken{action, value?}, webhookSecret{action, value?}, mode, version, reason}` | 200 `VersionResponse` |

- **Los permisos nuevos:** `platform.modules.read` y `platform.modules.manage`. El segundo entra en `RequiringRecentProof`.
- **Las claves** usan `action` (`keep`, `replace` o `clear`), como las del asistente. `replace` valida el token con Mercado Pago antes de abrir la transacción.
- **Al aprobar,** `expiresOn` es una fecha civil que se convierte al fin del día en la zona de la organización, como en `PUT /features`.

### El webhook

`POST /webhooks/mercadopago` va en `Api/Controllers/Billing/MercadoPagoWebhookController.cs`, con `[AllowAnonymous]`, `[RequestSizeLimit]` y su controller en la lista de `AccessDeclarationTests`.
- **Qué recibe:** el cuerpo de Mercado Pago (`id`, `type`, `action`, `data.id`) y los encabezados `x-signature` y `x-request-id`.
- **Qué responde:**
  - 200 sin cuerpo, después de guardar en la bandeja, también si el aviso ya estaba;
  - 401 con la firma inválida;
  - 404 sin conexión configurada.
- **Qué no hace:** nunca llama a Mercado Pago en el pedido.

### Errores

Los textos van en `Billing.resx` y `.en.resx`.

| Código | Tipo | Cuándo |
| --- | --- | --- |
| `Billing.Module.NotFound` | NotFound | la clave no está en el catálogo o no se ofrece |
| `Billing.ModuleRequest.NotAllowed` | Conflict | el módulo no se puede pedir |
| `Billing.ModuleRequest.AlreadyPending` | Conflict | ya hay un pedido pendiente de ese módulo |
| `Billing.ModuleRequest.AlreadyOn` | Conflict | el módulo ya está prendido por Plataforma, un pedido o una suscripción |
| `Billing.ModuleRequest.NotPending` | Conflict | aprobar, rechazar o cancelar uno ya resuelto |
| `Billing.ModuleRequest.NotFound` | NotFound | no existe o es de otra organización |
| `Billing.Trial.NotAllowed` | Conflict | el módulo no se puede probar, o ya está prendido |
| `Billing.Trial.AlreadyUsed` | Conflict | esa organización ya lo probó |
| `Billing.Subscription.NotAllowed` | Conflict | el módulo no admite suscripción |
| `Billing.Subscription.AlreadyExists` | Conflict | ya hay una suscripción viva de ese módulo |
| `Billing.Subscription.Unavailable` | Conflict | no hay conexión, o el modo no permite cobrar en ese ambiente |
| `Billing.Subscription.PriceChanged` | Conflict | `priceId` ya no es el vigente; lleva el nuevo |
| `Billing.Subscription.CardRejected` | Validation | Mercado Pago rechazó la tarjeta; va en el campo `card`, con un motivo de una lista cerrada |
| `Billing.Subscription.ProviderUnavailable` | ServiceUnavailable | Mercado Pago no respondió antes de crear nada |
| `Billing.Subscription.NotActive` | Conflict | cambiar la tarjeta o dar de baja una que no está viva o ya tiene la baja pedida |
| `Billing.Subscription.NotFound` | NotFound | no existe o es de otra organización |
| `Billing.Price.Invalid` | Validation | monto, topes o fecha fuera de rango; la fecha, antes de los días de aviso |
| `Billing.Price.Duplicate` | Conflict | ya hay un precio de ese módulo desde ese día |
| `Billing.LimitOverride.UnknownLimit` | NotFound | ningún módulo declara esa clave |
| `Billing.LimitOverride.OutOfRange` | Validation | bajo el mínimo o sobre el máximo de la plataforma |
| `Billing.Connection.KeyRejected` | Validation | Mercado Pago no aceptó el token |
| `Billing.Connection.LiveRequiresInvoicing` | Conflict | el modo «Real» sin facturación de ARCA |
| `Billing.Export.ModuleOn` | Conflict | exportar con el módulo prendido |
| `Billing.Export.DataPurged` | Conflict | los datos ya se borraron |
| `Billing.Export.InProgress` | Conflict | ya hay una exportación de ese módulo en curso |
| `Billing.Export.NotFound` | NotFound | no existe o es de otra organización |
| `Billing.Webhook.InvalidSignature` | Unauthorized | la firma no es válida o el `ts` es viejo |

**Idempotencia y concurrencia:**
- **Llevan `[Idempotent]`:** pedir, probar, suscribirse, exportar, un precio nuevo, aprobar y rechazar.
- **La suscripción:** el front crea la clave al abrir la página. Después de un 4xx, renueva la clave y pide un token nuevo, porque el token sirve una sola vez.
- **Llevan `version` y el 409:** las marcas del catálogo, cancelar un pedido, aprobar, rechazar, cambiar la tarjeta, dar de baja, el tope de una organización, la configuración y la conexión.
- **Los ejemplos** de cada respuesta salen de una corrida real, con sus estados y sus errores.

## Datos ✓

**La clasificación:** todas las tablas van en `platform` y son globales, como `TenantFeatures`, salvo la exportación.
- **El inventario:** cada tabla entra en `DataClassificationTests` con su motivo.
- **Cómo lee la organización:** solo por readers que filtran por el `tenant_id` del claim, nunca por un dato del pedido.
- **Cómo lee Plataforma:** todo, con su permiso.
- **La migración** es aditiva: la versión anterior no las lee (0060).
- **Todas** llevan versión (`xmin`) y quién y cuándo.

| Tabla | Columnas | Claves e índices |
| --- | --- | --- |
| `ModuleCatalogEntries` | `FeatureKey` (≤64), `IsOffered`, `AllowsRequest`, `AllowsTrial` y `AllowsSubscription` | única por `FeatureKey`. Sin fila, el módulo no se ofrece |
| `ModulePrices` | `Id`, `FeatureKey`, `Amount` (`numeric(18,2)`), `Currency` (ARS), `Interval` (`Month`), `StartsOn` y `StartsAtUtc`, `Limits` (jsonb `[{key, value}]`), `ProviderPlanId?`, `NoticeSentAtUtc?`, `AppliedAtUtc?` y `Reason` (≤500) | única por `(FeatureKey, StartsOn)`; índice `(FeatureKey, StartsAtUtc)`. Nunca cambian el monto, la fecha ni los topes |
| `ModuleRequests` | `Id`, `TenantId`, `FeatureKey`, `State` (`Pending`, `Approved`, `Rejected` o `Cancelled`), `RequestedBy`, `DecidedBy?`, `DecidedAtUtc?`, `DecisionReason?` (≤500) y `ExpiresOn?` | única parcial `(TenantId, FeatureKey)` con `State = 'Pending'`; índice `(State, CreatedAtUtc)` |
| `ModuleTrials` | `TenantId`, `FeatureKey`, `StartedAtUtc`, `EndsAtUtc`, `StartedBy` y `EndNoticeSentAtUtc?` | única por `(TenantId, FeatureKey)`, para siempre: es el registro de las pruebas |
| `ModuleSubscriptions` | `Id`, `TenantId`, `FeatureKey`, `PriceId`, `Provider`, `ProviderSubscriptionId?` (≤64), `State`, `TrialEndsAtUtc?`, `CurrentPeriodEndUtc?`, `NextChargeAtUtc?`, `CancelAtPeriodEnd`, `CancelRequestedAtUtc?`, `ProviderCanceledAtUtc?`, `CanceledAtUtc?`, `CancelReason?` (`Organization`, `Provider`, `OrganizationClosed` o `CreationFailed`), `PastDueSinceUtc?`, `CardLast4?`, `ConsentAtUtc`, `SubscribedBy`, `NextReconcileAtUtc`, `LeaseId?` y `LeaseUntilUtc?` | única parcial `(TenantId, FeatureKey)` con `State <> 'Canceled'`; única `(Provider, ProviderSubscriptionId)`; índice `(NextReconcileAtUtc)` |
| `SubscriptionCharges` | `Id`, `SubscriptionId`, `TenantId`, `Provider`, `ProviderPaymentId` (≤64), `Amount`, `Currency`, `Status` (`Pending`, `Approved`, `Rejected`, `Refunded` o `Duplicate`), `PeriodStartUtc`, `PeriodEndUtc`, `ChargedAtUtc?`, `RejectionCode?`, `InvoiceStatus` (`Pending` o `Issued`) e `InvoiceId?` | única `(Provider, ProviderPaymentId)`; única parcial `(SubscriptionId, PeriodStartUtc)` con `Status = 'Approved'`: el segundo aprobado del período queda `Duplicate`; índice `(TenantId, ChargedAtUtc)` |
| `PaymentWebhookInbox` | `Id`, `Provider`, `EventKey` (≤128), `Topic`, `ResourceId`, `ReceivedAtUtc`, `Status`, `Attempts`, `LeaseId?`, `LeaseUntilUtc?`, `NextAttemptAtUtc?`, `ProcessedAtUtc?` y `LastErrorCode?` | única `(Provider, EventKey)`; índice `(Status, NextAttemptAtUtc, LeaseUntilUtc)`. No guarda el cuerpo |
| `TenantLimitOverrides` | `Id`, `TenantId`, `LimitKey` (≤80), `Value` (`numeric(18,4)`), `ExpiresOn?`, `ExpiresAtUtc?` y `Reason` | única `(TenantId, LimitKey)`. Vencido, el resolvedor lo ignora |
| `ModuleDataRetentions` | `Id`, `TenantId`, `FeatureKey`, `OffSinceUtc`, `PurgeDueAtUtc`, `NoticeSentAtUtc?`, `PurgedAtUtc?`, `State` (`Kept`, `Noticed`, `Purging`, `Purged` o `Reopened`), `LeaseId?` y `LeaseUntilUtc?` | única parcial `(TenantId, FeatureKey)` con `State` en `Kept`, `Noticed` o `Purging` |
| `PaymentProviderSettings` | `Provider`, `PublicKey?`, `EncryptedAccessToken?` y `EncryptedWebhookSecret?` (bytea, `[NotAudited]`), `AccessTokenLast4?`, `WebhookSecretLast4?` y `Mode` (`Test` o `Live`) | una fila por proveedor; cada cambio deja su `SecurityEvent` con motivo en la misma transacción, como `PlatformSettings` |

**`tenant.ModuleDataExports`** es privada (`ITenantOwned`, con RLS forzado).
- **Columnas:** `Id`, `TenantId`, `FeatureKey`, `State` (`Pending`, `Building`, `Ready` o `Failed`), `RequestedBy`, `FileKey?` (opaca, de `IFileStore`, con la categoría `module-export`), `SizeBytes?`, `ReadyAtUtc?`, `LeaseId?` y `LeaseUntilUtc?`.
- **Clave:** única parcial `(TenantId, FeatureKey)` con `State` en `Pending` o `Building`.
- **Una por módulo:** una nueva lista borra el archivo de la anterior.

**`platform.TenantFeatures`:**
- **Las columnas,** si las Costuras no las sumaron: `State` (`Active`, `Grace`, `Suspended` o `Ended`), `SourceRefId?` (el pedido o la suscripción), `GraceUntilUtc?`, `SuspendedAtUtc?` y `EndedAtUtc?`.
- **Las filas nuevas:** empiezan a escribirse las `Approved` y `Subscription` (0069 §8).
- **Quién las escribe:** solo `ModuleUsageWriter`. Mantiene `Enabled` y `ExpiresAtUtc` de acuerdo con el estado: `Suspended` y `Ended` van con `Enabled = false`.

**`platform.PlatformSettings`** suma los cinco plazos como `int`, con su valor de entrada.

**Los permisos:**
- **`modules.manage`** va en `Permissions.cs`, en `OrganizationScoped`, en el área `Area.modules` («Módulos») y en `Permissions.resx` y `.en.resx`. La migración `<fecha>_SystemRoleModulesManage.cs` se lo da a TenantAdmin y avanza la generación de roles.
- **`platform.modules.read` y `platform.modules.manage`** van en `PlatformPermissions.cs`, con sus textos y la migración `<fecha>_PlatformOperatorModulesPermissions.cs` para el operador inicial.

**La caché:** no hay caché nueva. Lo que se escribe en `TenantFeatures` pasa por la unidad de trabajo y avanza la etiqueta de módulos (`t:…:features`). Si el resolvedor de las Costuras cachea, su etiqueta avanza con `TenantLimitOverrides` y con `ModulePrices`.

**Los datos personales** (ADR 0062):
- **Sin datos personales:** `SubscriptionCharges` y `PaymentWebhookInbox`. La bandeja se purga a los 30 días de procesado cada aviso, con `Billing:Inbox:RetentionDays`, un valor técnico.
- **Con el id de una persona:** `RequestedBy`, `StartedBy` y `SubscribedBy` guardan solo el id, que se conserva como en la auditoría, porque la identidad se anonimiza con la baja.
- **El archivo de una exportación** lleva datos de la organización: lo borran el borrado del módulo y la exportación siguiente.
- **Nunca se guarda:** la tarjeta, el token ni el correo que se manda a Mercado Pago.
