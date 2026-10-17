# Etapa 9 · Endurecimiento

Estado: plan (E9), sin implementar. Alcance completo y exclusivo de [Etapa 9 de la hoja de ruta](2026-09-27-plan-de-desarrollo.md#etapa-9-endurecimiento). Mandan las decisiones del usuario, las ADR vigentes, AGENTS y las fichas; Roles es el área de referencia.

## Forma de trabajo

**Se empieza a programar cuando el coordinador de la 11 termine su galería.** Las decisiones de producto P1–P3 las tomó el usuario el 2026-10-16 (al final del plan). **Primero van los correctivos de código de la revisión general** (`docs/plans/2026-10-16-correctivos-generales.md`), como primera tarea de Back y Front: F3 copia de `OperatorPermissionsDialog.tsx`, que hoy lista solo los nueve permisos del núcleo y no los de los módulos (R03); F4 copia de `RoleEditorPage.tsx`, que corrige y reenvía sin `sameOperation` (R05); y B2 toca `PermissionService.cs`, igual que R09. Mientras el AppHost de la 11 esté levantado desde main para las pruebas con Meta, el back de la 9 no compila: los dos coordinadores acuerdan esa ventana. Las tareas y el protocolo de este plan quedan definidos, sin variantes de implementación.

Modo rápido: contrato primero; Back y Front en paralelo, cada chat dueño de su repo; commits chicos en español, con pathspec y sin push. Tests dirigidos y build del proyecto afectado; integración/seguridad del área al terminarla. Una sola puerta final, sin revisiones por tarea, actas ni evidencia versionada. Las suites completas pasan una vez; después sólo lo afectado, con las excepciones de autenticación, persistencia base o composición que fija AGENTS. Worktrees sólo en `/puerta`. Diseño es el único que toca el lienzo.

## Contrato · K1

El **Coordinador** publica `docs/contracts/openapi.json`, los contratos HTTP y sus stubs 501, textos es/en y tests de contrato; Front regenera con `npm run contracts`. Ambos commits preceden B/F. Los cuerpos existentes mantienen sus campos; las adiciones siguientes se describen con esquemas cerrados, enums y todos los status. Los stubs no emiten cookies ni consumen códigos.

### Ingreso y segundo factor

ADR 0042: operador exclusivo, permisos individuales, sin roles ni perfiles personales. ADR 0043: nada lo identifica como operador antes de probar el primer factor. ADR 0044: cookie de Plataforma independiente de la pública. ADR 0045: Mi cuenta personal sigue sin reautenticación.

Después de verificar correo o Google para `access=platform`, se crea un desafío pendiente ligado al navegador, identidad, sello de seguridad y authorize validado. **No hay cookie de Plataforma ni tokens OIDC hasta terminar el segundo factor.** Una invitación de operador sólo queda aceptada al completar este ingreso. Las puertas públicas conservan `Tenancy.Access.PlatformOnly` después de probar posesión.

`POST /api/auth/login-code/verify` conserva su cuerpo y 200, y agrega `next: "authorize" | "platformFactor"` a `{returnUrl}`. El enlace de ingreso y el callback Google aplican la misma barrera antes de emitir sesión: Google redirige al ingreso con `factor=pending`, sin identidad ni prueba en URL; ese indicador sólo provoca consultar el desafío. Un authorize/refresh de operador sin evidencia de segundo factor vigente no entrega tokens. Las sesiones anteriores a E9 quedan revocadas en la migración del factor, incluido el seed, que configura su autenticador al volver a ingresar.

Base `A = /api/auth/platform-factor`. Controller `[AllowAnonymous]` incorporado por tipo exacto a `AccessDeclarationTests`; cada acción exige la cookie pendiente protegida, host principal y origen válido en escrituras. Esa cookie no autentica otros endpoints.

| Método/ruta | Cuerpo | Éxito | Prueba/permiso |
|---|---|---|---|
| GET A | — | 200, unión descrita abajo | Desafío pendiente |
| POST A/verify | `{method:"authenticator"\|"recoveryCode",code:string}` | 200 `{next:"authorize",returnUrl}` o `{next:"recoveryCodes"}` | Desafío; recovery sólo con factor ya configurado |
| POST A/complete | `{savedRecoveryCodes:true}` | 200 `{returnUrl}`; crea sesión de Plataforma | Desafío con alta verificada y códigos guardados |
| POST /api/platform/reauthentication | `{method:"authenticator"\|"recoveryCode",code:string}` | 200 `{verifiedAtUtc,recentUntilUtc}` | `[Access(Platform)]`, operador activo, sin permiso adicional |
| POST /api/platform/accounts/{id}/authenticator/reset | `{version:uint,reason:string}` | 200 `{version:uint}` | Platform + `platform.operators.manage` + reautenticación reciente |

GET A devuelve `{state:"verify",expiresAtUtc}`, `{state:"setup",expiresAtUtc,secret,otpauthUri}` o `{state:"recoveryCodes",expiresAtUtc,recoveryCodes:string[8]}`. El secreto se prepara en la UoW del primer factor; GET no escribe. Setup: QR generado localmente, clave copiable y TOTP de seis cifras. Códigos de recuperación: ocho, formato `XXXX-XXXX` del tablero, aleatorios, de un uso; sólo hashes permanentes. El alta se verifica con TOTP; conserva los ocho códigos cifrados únicamente en el desafío hasta completar/vencer. Copiar y descargar son acciones locales. Al completar se destruye ese material temporal. Ningún secreto llega a Redis, logs, analytics, URL ni storage del navegador; respuestas `Cache-Control: no-store`.

Decisiones técnicas: desafío de 10 minutos sin renovación deslizante; TOTP de 30 segundos, seis dígitos y tolerancia de un intervalo por lado, con TimeProvider; se impide reutilizar un intervalo ya consumido. Se comparte el contador de cuenta existente: diez fallos y bloqueo de 15 minutos, también para recuperación y alta; probar el primer factor no borra los fallos del segundo. La clave queda protegida con Data Protection; las réplicas comparten su anillo. No se agrega regeneración autónoma de códigos ni una recuperación alternativa.

Todos los POST nuevos llevan `[Idempotent]`. Los del factor usan `DurableReference` con `[IdempotencyDurable]`: binding global de identidad en PostgreSQL, ligado al desafío/navegador o sesión; el store global sólo guarda referencia técnica. Replay revalida cuenta, sello, acceso y prueba antes de reconstruir el resultado y emitir cookie tras commit. Consumo, cambio, binding y auditoría se confirman juntos. Códigos/hash del body no se loguean; los secretos temporales quedan cifrados fuera del store global. Dos pedidos con la misma clave consumen una vez; dos claves no reutilizan el mismo código. Un replay de reautenticación conserva su vencimiento original, no lo prolonga.

### Acciones sensibles y errores

La prueba reciente dura 10 minutos (P1); el ingreso completo cuenta como prueba. Se persiste ligada a la sesión concreta de Plataforma y al sello de seguridad, sin convertirla en permiso ni aceptar fechas del cliente. Nueva pestaña de la misma sesión la comparte; otra sesión no. Cerrar/revocar sesiones, quitar operador o restablecer el factor la invalida.

Exigen prueba reciente **todas las escrituras** de estos controllers existentes, con los mismos cuerpos, status y permisos actuales:

| Controller en `Api/Controllers/Platform/` | Rutas afectadas | Permiso |
|---|---|---|
| PlatformOperatorsController | Invitar; reenviar/revocar invitación; PUT/DELETE operador | `platform.operators.manage` |
| PlatformAccountsController | Suspender/reactivar, revoke-sessions, deletion | `platform.accounts.manage` |
| PlatformRecoveriesController | Approve/reject | `platform.recoveries.manage` |
| PlatformTenantsController | Crear, approve, retry-provisioning, reject, suspend, reactivate | `platform.tenants.manage` |
| PlatformFeaturesController, PlatformDomainsController, PlatformPublicSiteController | Editar módulos; verificar/quitar dominio; unpublish/allow-publish | `platform.tenants.manage` |
| PlatformLegalDocumentsController | Publicar documento | `platform.legal.manage` |
| PlatformSettingsController | PUT settings | `platform.settings.manage` |

Incluye las escrituras de configuración y gestión de módulos en Plataforma: Asistente (`platform.assistant.configure`) y WhatsApp (`platform.whatsapp.manage`); atender chats y consultar no piden otra prueba. K1 enumera sus acciones concretas en OpenAPI y la guarda las descubre por metadata de ruta instalada, sin dependencias del núcleo a los módulos. Control antes del replay y comprobación de sesión/sello en la UoW que escribe: una respuesta cacheada no saltea la barrera. Falta de permiso se resuelve antes de pedir el código.

Restablecer usa el snapshot `operatorVersion` de Cuenta-Plat; bajo lock verifica versión, operador aceptado y actor distinto del destinatario, y actualiza esa versión. Repite las restricciones existentes de `PlatformOperatorGuard`, sin permitir modificar al propio operador. Revoca sesiones/autorizaciones, desafío, clave y códigos del destinatario; mantiene sus permisos y el próximo ingreso exige setup. Audita actor, destino y motivo en la misma UoW.

Todo error es ProblemDetails con `code`, `traceId`, textos de resources es/en y errores por campo. `reason` usa los límites y normalización de las acciones actuales. Fechas UTC ISO con Z; ninguna fecha del cliente prueba antigüedad.

| Status/código | Conducta y texto es |
|---|---|
| 400 `Auth.TwoFactor.Invalid` | Campo code: «El código no es válido. Revisá que la hora de tu celular esté bien.» |
| 400 `Auth.TwoFactor.RecoveryInvalid` | Campo code: «Ese código no es válido o ya lo usaste.» |
| 401 `Auth.TwoFactor.ChallengeExpired` | Desafío ausente/vencido/invalidado: «Volvé a ingresar para continuar.» |
| 409 `Auth.TwoFactor.InvalidState` | Paso fuera de secuencia: «Volvé al paso actual para continuar.» |
| 403 `Auth.Platform.ReauthenticationRequired` | «Confirmá que sos vos para continuar.»; abre diálogo sin perder operación |
| 429 `Identity.Account.LockedOut` | Mismo bloqueo actual, `retryAfter` + Retry-After; no acepta más códigos |
| 400 `Validation.Failed` | Campos faltantes/formato, versiones y motivo; no consume intento criptográfico |
| 401 `Http.Unauthorized`; 403 `Http.Forbidden` | Falta sesión o permiso; el acceso incorrecto conserva el código publicado por AccessFilter |
| 404 `Users.User.NotFound` / `Platform.Operator.NotOperator` | Destinatario inexistente o que no es operador |
| 403 `Users.User.CannotModifySelf`; 409 `Platform.Operator.InvitationPending` / `Platform.Operator.LastManager` | Reset propio, invitación sin aceptar o protección del último administrador |
| 409 `General.ConcurrencyConflict` | Versión anterior; no pisa cambios ni repite reset |

GET A declara 200/401; verify, 200/400/401/409/429; complete, 200/400/401/409; reauthentication, 200/400/401/403/429; reset, 200/400/401/403/404/409/429. Idempotencia agrega a todos los POST 400 `Request.IdempotencyKeyRequired`, 409 `Request.InProgress` y 422 `Request.IdempotencyKeyReused`; las cuotas, 429. K1 conserva los códigos de acceso actuales y publica los anteriores por acción.

### Cuotas, auditoría y transporte

GET/PUT `/api/platform/settings` agregan **`rateLimits`** al DTO existente; en PUT omitirlo conserva cuotas, enviarlo exige los doce perfiles completos. Mantiene compatibilidad (ADR 0005), `version` y `reason` actuales, respuesta `{version}` y `platform.settings.manage` más prueba reciente. Cada perfil es `{permitLimit:int,windowSeconds:int}`; límites 1–1.000.000 y 1–86.400, sin cero para desactivar. Perfiles cerrados: `ip`, `publicIp`, `identity`, `tenant`, `loginCode`, `loginVerify`, `invitationPreview`, `invitationAccept`, `assistantQuickQuery`, `assistantConversation`, `assistantNewKey`, `whatsAppWebhook`. No se publica otro endpoint ni cuotas personales visibles.

Valores iniciales (P3, moderados): ip 300/60 s, publicIp 60/60 s, identity 120/60 s, tenant 600/60 s, assistantQuickQuery 20/60 s, assistantConversation 10/60 s y assistantNewKey 3/900 s. Se conservan loginCode 20/900 s, loginVerify 30/900, invitationPreview 60/900, invitationAccept 10/900 y webhook 120/60. Los defaults de arranque son opciones con ValidateOnStart; el seed los usa sólo al crear configuración. Cambiar las opciones no pisa cuotas ya guardadas.

Ventanas fijas UTC compartidas, sin cola. Se aplican las cuotas concurrentemente: IP real sólo de proxies confiables, particionada con HMAC sin guardar IP completa; identidad firmada; tenant sólo del claim. Público usa publicIp, más estricto, sin inventar una identidad/tenant por subdominio; una ruta pública autenticada suma su identidad. Límite por petición de API, excluyendo assets y probes. Asistente: perfiles específicos en POST quick, POST conversations de los tres accesos, y PUT settings cuando trae una clave nueva, antes de contactar Anthropic; preserva presupuesto/cuotas de ADR 0048/0049/0051. WhatsApp: cuotas generales en rutas E11 y perfil propio del webhook antes del procesamiento. Los módulos aportan metadata y se retiran con ella.

Redis ejecuta la admisión atómica por partición/ventana; caída: contadores PostgreSQL compartidos como respaldo, sin fallback por proceso ni reiniciar una cuota para habilitar llamadas externas. B3 prueba continuidad de consumo al cambiar de backend; los contadores durables se actualizan mediante un servicio de Application y su UoW antes de admitir la operación, sin anidar la transacción del negocio. El cache de datos conserva el fallback 200 de ADR 0052. Una cuota rechazada responde 429 `Security.RateLimit.Exceeded`, `retryAfter` entero y Retry-After; el webhook conserva `WhatsApp.Webhook.TooManyRequests`. Presentación (P2): aviso en el lugar de la acción, con cuenta regresiva y reintento manual, conservando el formulario; en lecturas, el estado de error de la página; el front no repite automáticamente una escritura por vencer el tiempo.

Auditoría conserva GET `/api/platform/audit` y `/{id}`, permiso `platform.audit.read`, cursor y filtros actuales. Agrega tipos de evento de alta/reset de autenticador, uso de recuperación y reautenticación; sin códigos, secretos o direcciones completas. El ingreso exitoso se registra al completar los factores, con método que identifica TOTP o recuperación. Restablecer conserva motivo; los eventos automáticos tienen motivo de sistema traducido.

Decisiones técnicas: [ADR 0052](../decisions/0052-cache-compartida-redis.md), Redis sin L1; opciones de seguridad conservan valores; cliente Anthropic por IHttpClientFactory, sin reintentos nuevos; permisos proyectados sólo del catálogo permitido; encabezados en éxitos y errores. CSP sin unsafe-eval ni scripts inline: permite `https://connect.facebook.net`, frames `https://facebook.com`/`https://*.facebook.com` y conexiones del SDK a esos orígenes; conserva Google, authorize y el puente del subdominio con orígenes validados. Las excepciones de estilos requeridas por componentes se acotan a estilos, nunca a scripts. Cookies Secure/HttpOnly, host-only, SameSite coherente con OIDC/Google y sin ampliar Domain a subdominios. Las pruebas fijan las directivas y la convivencia de ambas sesiones.

## Back

Rutas abreviadas bajo `src/ArquitecturaBaseMultitenant.<Capa>/`; tests bajo `tests/ArquitecturaBaseMultitenant.<Proyecto>/`. Los archivos/guardas nuevos de las tareas son **E9**, no código ya existente.

| Tarea / estado | Archivos y trabajo | Copiá de (existente) | Tests dirigidos |
|---|---|---|---|
| B1 · Pendiente | Domain/Authentication; Application/Services/Auth, Services/Platform, modelos/validadores/puertos; Infrastructure/Identity y EF; Api/Controllers/Auth, Platform y filtros. Factor, desafío/binding, sesiones, prueba reciente, reset y eventos. Una UoW pública, locks→lecturas→reglas→escrituras, efectos después del commit; fallos de códigos con CommitPolicy.OnAnyResult. | `Application/Services/Roles/RoleService.cs` para flujo; `Application/Services/Auth/LoginCodeService.cs` y `Infrastructure/Identity/SignInService.cs` para sesión tras commit; `Application/Services/Platform/PlatformOperatorService.cs` para versión/guard; `Api/Modules/Assistant/AssistantDurableAdapter.cs` para protocolo durable, sin importar el módulo | Nuevos PlatformTwoFactorTests y PlatformRecentAuthenticationTests; ampliar PlatformOperatorTests. Carreras, recovery de un uso, replay/revocación, no aceptar invitación antes del factor, seed/legacy sin bypass, separación de cookies y permisos; guardas de rutas/acceso/idempotencia/resources |
| B2 · Pendiente | AppHost/AppHost.cs; Infrastructure/Caching, Persistence/Readers, repositorio de generaciones/migración; Application/Services/Roles/PermissionService.cs; paquetes centrales. Ejecutar ADR 0052 y su inventario entero. Actualizar todos los escritores invalidadores, incluidos seeds/workers; lecturas agrupadas de roles/empresas, sin 2 + N | `Infrastructure/Caching/HybridCacheExtensions.cs` para scope propio; `RolePermissionCache.cs` y `CacheKeys.cs` de esa carpeta para alcance; se reemplazan su L1 e invalidación local, que no sirven como modelo distribuido | Nuevos DistributedCacheTests, CacheArchitectureTests y PermissionProjectionTests; extender CacheKeyScopeTests, AccessStatusCacheTests y RolePermissionCacheIsolationTests. Dos hosts, rollback/carreras, caída/reconexión, serialización sin PII; número de consultas independiente de N y no consultar empresas para permisos no proyectables |
| B3 · Pendiente | Api/RateLimiting; Domain/Settings/PlatformSettings.cs; modelos/validator/settings/EF; adaptador Redis y respaldo; metadata en Api/Modules/{Assistant,WhatsApp}. Opciones nuevas de tokens 5 min/15 min/30 días, cookie 30 días, idempotencia 24 h y limpieza 1 h, enlace 10 min, baja lote 100 y ciclo 1 h, webhook 120/min, todos ValidateOnStart y mismos valores | `Api/RateLimiting/RateLimitingExtensions.cs`, `Api/Modules/WhatsApp/RateLimiting/WhatsAppWebhookRateLimitPolicy.cs`; `Application/Configuration/Auth/LoginLinkOptions.cs` para opciones; `Application/Services/Platform/PlatformSettingsService.cs` para edición auditada | Nuevos DistributedRateLimitTests y SecurityOptionsTests: dos hosts, particiones, Retry-After, edición/409, Redis caído, sin llamadas al proveedor al rechazar ni cuotas reseteadas; arranque inválido; defaults exactos |
| B4 · Pendiente | Api/Hosting/SecurityHeadersExtensions.cs, registro de cookies y middleware de telemetría tras resolver acceso; ServiceDefaults/Extensions.cs. Spans con `tenant.id`/`tenant.kind` del contexto validado; Plataforma/anónimo sin tenant, sin usar Host como tenant firmado; sin ID como dimensión métrica | `Api/Hosting/SecurityHeadersExtensions.cs` y `Api/Tenancy/TenantResolutionMiddleware.cs` para orden/contexto | Extender Hosting/SecurityHeadersTests; nuevos TenantTelemetryTests y pruebas de cookies/CSRF, Google, issuer y Meta; headers también en 4xx/5xx |
| B5 · Pendiente | Infrastructure/Modules/Assistant/AnthropicChatClientFactory.cs y registro del módulo: transporte nombrado por IHttpClientFactory, clave por pedido sin DefaultRequestHeaders compartidos. Domain/Assistant/AssistantOrganizationPolicy.cs, mapeo/DTO/servicio/migración: quitar sólo DailyMessageLimit, MonthlyBudget y RetentionDays obsoletos | `Infrastructure/Modules/WhatsApp/WhatsAppInfrastructureModule.cs` para AddHttpClient; conservar SDK, timeouts y precisión IA actuales | Tests del módulo: mismo handler reutilizado sin cruzar claves, validar nueva clave/rate limit; migración sobre datos, conservación de límites vigentes y contratos sin campos muertos |
| B6 · Pendiente | ArchitectureTests/IdempotentActionsTests.cs: modo durable exige atributo y verificador concreto. Api.IntegrationTests/Tenancy: inventario de endpoints + casos cruzados, sin exclusiones por namespace. `/security-review` en el cierre, una revisión | `ArchitectureTests/IdempotentActionsTests.cs`; `Api.IntegrationTests/Platform/RolePermissionCacheIsolationTests.cs` para aislamiento real | Caso negativo sin atributo; matriz todas las rutas con IDs de otra organización/persona, empresas, adjuntos y E11; access Consumer/Business/Platform, RLS mt_app, privado/public_site/engagement, subdominio publicado/no publicado. Ningún dato/efecto ajeno; 404 donde corresponde |
| B7 · Pendiente | `.github/workflows/ci.yml`, configuración del formatter; commit propio de formato de todo el back, sin lógica. Nueva docs/rules/cache.md + índice y test; backend.md §17; features identidad/plataforma/roles y fichas afectadas por reglas/contrato | `ArchitectureTests/BuildConfigurationTests.cs` para guarda de CI; fichas e índices existentes para documentación | CI ejecuta `dotnet format ArquitecturaBaseMultitenant.slnx --verify-no-changes`; prueba focal de configuración; detector en 0, CacheArchitectureTests protege la nueva regla |

**Texto para «Multitenant · 9 · Back» (Claude):**

> Leé AGENTS.md, fichas, ADR vigentes y docs/plans/2026-10-16-etapa-9-endurecimiento.md. Esperá K1 publicado. Primero hacé las tareas de back de docs/plans/2026-10-16-correctivos-generales.md; después B1–B7, sólo en el back, con tests de lógica, integración y seguridad del área. Copiá los archivos citados por responsabilidad, respetando las sustituciones indicadas. Contrato cerrado; un cambio técnico pasa por el Coordinador y Cambios al contrato. No implementes nada fuera del alcance. Commits chicos en español, pathspec, sin push; formatter en commit propio. No corras suites completas ni puerta por tu cuenta. Actualizá sólo estados de tareas y documentación que cambie reglas o conducta.

## Front

Rutas bajo `../ArquitecturaBaseMutitenantFront/`. Tableros vigentes Ingreso/M-Ingreso, Cuenta-Plat/M-Cuenta-Plat, Auditoria-Plat y sus móviles; decisiones E9 de Criterio-Decisiones desde v117. Tokens sólo en memoria; tipos generados, TanStack Query/httpClient, es/en, formatos centrales, DetailGrid, opciones apagadas con motivo y nunca diálogo sobre diálogo.

| Tarea / estado | Archivos y trabajo | Copiá de (existente) | Tests dirigidos |
|---|---|---|---|
| F1 · Pendiente | `src/areas/public/auth/pages/LoginPage.tsx`, API/estado del factor y componentes nuevos; callback Google. Implementar ocho estados Operador, QR local, copiar/descargar, checkbox guardar; recargar consulta el desafío sin persistir secretos | `src/areas/public/auth/components/AuthCodeStep.tsx`, `src/shared/ui/OtpInput.tsx`, `src/areas/public/auth/pages/LoginPage.tsx`; el tablero manda en la presentación | LoginPage.test.tsx y nuevos PlatformFactor tests: recuperación inválida/usada, setup inválido, bloqueo, expiración, doble clic/red/replay, no completar antes de guardar, Google y acceso público sin filtración; axe |
| F2 · Pendiente | Nuevo `src/shared/platform/RecentAuthenticationDialog.tsx` y coordinador de acción pendiente; áreas Platform y módulos. Captura cuerpo/versión/clave al confirmar; ante 403 suspende el formulario, confirma factor y reenvía la misma operación una vez. Cancelar conserva borrador, navegación/cierre cancela el reenvío | `src/shared/platform/ReasonDialog.tsx`, `src/shared/api/useIdempotentMutation.ts`, `src/areas/platform/accounts/components/AccountDialogs.tsx` | Prueba cruzada en permisos, recuperación y configuración: sesión distinta, prueba vencida, 409 posterior, cambios con formulario abierto, cancelar/desmontar, foco, sin dos diálogos; axe |
| F3 · Pendiente | `src/areas/platform/accounts/pages/AccountPage.tsx`, componentes/API; `audit/pages/SecurityAuditPage.tsx`. Reset con motivo y operatorVersion capturada; invalidar cuenta/lista/auditoría tras éxito; nuevos eventos con los filtros existentes | `src/areas/platform/accounts/components/OperatorPermissionsDialog.tsx`; `src/shared/ui/DetailGrid.tsx`; `src/areas/platform/audit/pages/SecurityAuditPage.tsx` | AccountPage/SecurityAuditPage tests: sin permiso/propio/pendiente, reset concurrente, textos de motivo, fecha central, paginado/cursor sin regresión y móvil |
| F4 · Pendiente | `src/areas/platform/settings/pages/PlatformSettingsPage.tsx`, API/schema; `src/shared/api/httpClient.ts`, countdown y mensajes de áreas/módulos. Editor rateLimits y presentación P2, sin reenvío automático al terminar la espera | `PlatformSettingsPage.tsx` de esa ruta, `src/shared/api/useRetryAfterCountdown.ts`, `src/areas/business/roles/pages/RoleEditorPage.tsx` para snapshot de edición | Settings tests de versión/borrador/reauth; httpClient tests 429 sin tormenta ni repetición de efectos; casos Asistente y WhatsApp; campos accesibles y formatos |
| F5 · Pendiente | MSW, `e2e/`, scripts de galería/recorrido E9, locales y documentación de identidad/Plataforma al cambiar conducta | `src/areas/public/auth/pages/LoginPage.test.tsx` y scripts vigentes de E11 como estructura, sólo capturas E9 | Recorridos reales de I1; tests focales, lint y tsc al terminar tarea, suites completas reservadas a puerta |

**Texto para «Multitenant · 9 · Front» (Claude):**

> Leé AGENTS.md de ambos repos, fichas del front y el plan docs/plans/2026-10-16-etapa-9-endurecimiento.md del back. Tras K1, regenerá tipos; primero hacé las tareas de front de docs/plans/2026-10-16-correctivos-generales.md y después F1–F5, sólo en el front. Copiá los tableros E9 y las piezas citadas, sin dibujar ni agregar funciones. Los estados faltantes los resuelve Multitenant · Diseño con Para Diseño. Conservá cuerpo, versión y clave de la acción durante la reautenticación; no guardes secretos ni repitas efectos al vencer un límite. Tests focales, commits chicos con pathspec, sin push; suites completas una sola vez en puerta. Avisá al Coordinador cualquier cambio de contrato antes de publicarlo.

## Integración y puerta

- **I1 · Pendiente.** Coordinador integra con API real. E2E: operador del seed configura TOTP y guarda códigos; correo/Google exigen factor; recuperación se consume una vez; bloqueo real; ambas cookies conviven; acción sensible con prueba vencida/cancelación y 409; reset de otro operador revoca sesión/códigos y exige nueva alta; permisos sin rol; auditoría y límites con dos instancias; Redis caído y restaurado. No se pide a un operador real que entregue su clave: datos efímeros del entorno aislado.
- **I2 · Pendiente.** Una `/puerta`: build back sin warnings, dotnet test con Docker; front build/lint/test/contracts:check; test:e2e:real; detector auto/modelo/humano en 0; galería lado a lado sólo pantallas nuevas/cambiadas E9; `/recorrido` con estos pasos y `/security-review`. Tests del front y E2E solos según skill. No repetir retiro de módulos de E8/E8b/E11; sí mantener sus guardas verdes. Resultado en tabla corta, sin logs versionados; apagar Aspire y retirar worktrees de puerta.

**Texto para «Multitenant · 9 · Coordinador» (Claude):**

> Leé AGENTS.md de ambos repos, /plan-etapa, etapa-en-paralelo y este plan. Empezá al terminar el coordinador de E11 su galería; P1–P3 ya están decididas y «Para Diseño» ya lo tiene Multitenant · Diseño (16/10). Antes de B1–B7 y F1–F5 van los correctivos generales. Publicá K1 completo y tipos en ambos repos antes de habilitar Back/Front en paralelo con los textos de este plan. Aplicá ADR 0042–0045 y 0052, sin ampliar alcance. Coordiná cambios técnicos del contrato y avisá al otro dueño. Integrá I1 y corré I2 con /puerta y /recorrido, una revisión /security-review al cierre. Cada fallo vuelve a su dueño y se verifica sólo lo afectado. Dejá tabla final, sin actas, sin push ni cambios al lienzo.

## Relación con otras etapas

- **E8/E8b/E11:** conserva módulos quitables, precisión Money y presupuestos de IA; elimina sólo los tres campos muertos heredados. CSP permite el SDK/marco de Facebook y el rate limit cubre todas las rutas E11, incluido el webhook. El ingreso del bot sigue neutro para operadores (0049).
- **E10:** recibe Redis/TLS, anillo compartido y opciones documentadas; despliegue, certificados, nube, exportación de datos y CI cruzado siguen siendo E10.
- **E12–E17:** nacen con guardas de acceso, idempotencia, caché e inventario de rutas; E9 no implementa formularios ni sus puertos nuevos.

## Para Diseño

Texto para **«Multitenant · Diseño»**:

> Etapa 9: conservar Ingreso/M-Ingreso, Cuenta-Plat/M-Cuenta-Plat y Auditoría de v117 en adelante. Faltan el estado de reautenticación con código de recuperación y su error, bloqueo/expiración y cancelar conservando el formulario; Config-Plat/M-Config-Plat con el editor de los doce perfiles rateLimits del contrato, versión/motivo, y la presentación del límite P2; completar Confirmá que sos vos aplicado a las demás acciones sensibles sin diálogo sobre diálogo. Agregar alta/reset, recuperación y reautenticación a los ejemplos de Auditoría y su móvil. El ingreso conserva ocho códigos y checkbox, sin nueva pantalla. Copiar Criterio-Decisiones, DetailGrid y las reglas de opciones apagadas con motivo; no sumar acciones. Decisiones del usuario: la confirmación vale 10 minutos; el límite se muestra en el lugar de la acción con cuenta regresiva y reintento manual; las cuotas arrancan moderadas. Estos tableros van antes que los de la 13 y la 17.

## Cambios al contrato

## Decisiones del usuario (2026-10-16)

| # | Decisión |
|---|---|
| P1 | La confirmación del autenticador para acciones sensibles vale **10 minutos**; el ingreso completo inicia la ventana. |
| P2 | Al llegar a un límite: **aviso en el lugar de la acción, con cuenta regresiva y reintento manual**, sin perder el formulario; en lecturas, el estado de error de la página. Nunca se repite una escritura sola. |
| P3 | Cuotas iniciales **moderadas**: IP 300/min, pública 60/min, identidad 120/min, organización 600/min, consulta fija 20/min, conversación 10/min y clave nueva 3 cada 15 min. Los cinco perfiles existentes conservan sus valores; Plataforma edita todos con motivo. |
