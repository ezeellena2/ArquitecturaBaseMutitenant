# Etapa 8 · Consolidación y contrato

## Alcance

Parte 1 del [plan ejecutable](../plans/2026-10-07-etapa-8-whatsapp.md), sobre lienzo v115 `bb7c78de`. T00 se verifica y commitea antes de T01. No cierra E8 ni implementa el módulo o las pantallas. Trabajo en main, sin push.

## T00 · Consolidación verificada

- Cuenta, método, Personal y consentimiento son pasos separados; altas correo/Google/invitación/operador usan los helpers comunes. Invitación por OTP no consiente legales; operador no recibe Personal.
- Intentos activos fallidos y lockout se verifican en un helper común, manteniendo propósitos, auditoría y CommitPolicy de cada llamador.
- Identidad externa/correo → cuenta → destino de código; Mi cuenta, invitación de operador y recuperación usan el mismo lock que Google y releen propiedad. Vínculo verificado y copia del principal se comparten.
- `AccountLifecycle` concentra estados y fechas en Domain; Infrastructure aplica y anonimiza. Avisos usan `LoginMethodChange` y el renderer mantiene el mapeo localizado existente.
- Convención `Recorder` agregada a la lista cerrada, sin excepción por clase. No hay ReauthTicket ni alta de Phone.

## Validación T00

- Domain: 4 casos nuevos de ciclo de vida verdes, después del rojo por ausencia de la regla.
- Application: 514 tests existentes verdes y 7 nuevos focales verdes.
- Arquitectura: 149 tests verdes, incluyendo sufijos, capas y DI.
- Integración inicial: 116/117 verdes. La barrera del test de concurrencia se adaptó al lock temprano; luego sus 2 casos pasan. No se relajó la comprobación de consumo único.
- Google, operadores y seguridad: 30 casos existentes verdes; las 4 integraciones nuevas de carreras/rollback pasan después de corregir la configuración de redirecciones de sus clientes. Invitación sin consentimiento ya se verifica por las dos puertas en `InvitedLoginCodeTests`.
- Build de solución sin advertencias; no se afirma la puerta completa de E8, E2E, Meta ni túnel.

## Decisiones tomadas

- El reader transaccional `UserRepository.GetByIdAsync` observa primero las cuentas locales del contexto: una alta o estado preparado todavía no es visible en la base hasta el commit. Se conserva una sola forma de guardar.
- El linker permite omitir la marca de dominio en el alta de operador para conservar la conducta anterior; no amplía su acceso.

## T01

Contrato verificado, en registro de commits. OpenAPI sale del exportador MSBuild y los tipos de `npm run contracts`, sin edición manual. T00 está en `f612a51`; los planes v115 se actualizaron primero y solos en `db0444f` (E8) y `2c0fb3a` (E8b).

### Rutas y esquemas

- Nuevas rutas: `POST /api/auth/login-link/preview`, `POST /api/auth/login-link/redeem`, `GET/POST /webhooks/whatsapp`.
- Cuerpos aditivos de ingreso/registro/alta empresarial/recuperación/Mi cuenta: `phone` y `channel`; anterior de recuperación `previousEmail` o `previousPhone`. Los cuerpos de correo conservan su uso. `AddLoginMethodHttpRequest` reemplaza el nombre HTTP de alta de correo, con la misma ruta; el alias front anterior sigue compatible.
- OpenAPI declara exclusión de destinos, patrón de token/nonce de 43 caracteres, código ASCII, `access=consumer`, capabilities con `contactPhone` opcional y ProblemDetails con `attemptsLeft`/`retryAfter`. Incluye ejemplos y `x-error-codes` con status, campo y textos es/en, headers de idempotencia y cookies, Bearer y la cookie/antiforgery de alta empresarial; decisión asistida conserva `platform.recoveries.manage`, reason/version y SecurityEvent.
- El catálogo HTTP respeta `FieldErrors`: correo desconocido/ocupado sigue siendo 400 en formularios, aunque la fábrica Domain base use NotFound/Conflict. No cambia la conducta de esas rutas.
- Comparación contra T00: solo cambian los 19 paths de identidad/recuperación incluidos en esta parte y los 3 paths nuevos; los restantes paths y esquemas de otras áreas quedan iguales. `PhoneInputHttpRequest` existente se reutiliza; no se crea otra representación de teléfono.

### Puertos congelados

| Puerto | Contrato y responsabilidad |
|---|---|
| `ILoginCodeChannel` | `Key`, `SupportedMethodType`, `IsAvailable`, `Countries`, `ContactPhone`; `Result ValidateNewDestination(LoginCodeDestination, string? country)`. Reutiliza el value object Domain existente. Solo alta nueva valida país; el núcleo enumera registrados y no conoce una clave WhatsApp. Render/enqueue mantienen outbox y firmas vigentes. |
| `IInvitationChannel` | Tipo soportado/disponibilidad y `EnqueueAsync(InvitationDeliveryDestination, InvitationNotice, CultureProfile, recipientUserId, ct)`. Email o Phone construidos desde value objects. Email identifica la invitación; Phone solo entrega a la misma cuenta: principal Phone verificado o el verificado más antiguo. Sin cuenta/Phone utilizable: error antes de miembro, invitación u outbox; sin fallback. Selección bajo lock de cuenta después del lock de identidad. |
| `IPhoneLinkObserver` | `VerifiedAsync(userId, methodId, PhoneNumber, ct)`, `RemovedAsync(...)`, `AccountDeletedAsync(userId, ct)` en la UoW llamadora. Cambiar solo el principal no genera ese evento. Implementación de módulo pendiente. |
| `ILoginLinkIssuer` | `IssueAsync(userId, methodId, ct) → Result<IssuedLoginLink>` e `InvalidateForMethodAsync(...) → Result`, en la UoW llamadora. Núcleo rechaza operador bajo lock; token/expiración son datos internos con ToString seguro. Persistencia y emisión real pendientes T07. |
| `ILoginLinkService` | Preview y redeem con modelos separados del HTTP, sin tokens OIDC en body. T01 devuelve 501 y no crea cookie. |

El envelope técnico del webhook conserva `OriginalBody` para HMAC; T05 debe capturar los bytes originales con buffering limitado antes de usarlos, jamás validar una reserialización. En T01 no se aceptan mensajes ni se procesa una firma.

### Límite de lo implementado

Phone y enlaces devuelven 501 `General.Contract.NotImplemented`; correo conserva su implementación. Webhook tiene contribuidor registrado **apagado siempre** en T01 y responde 404 común: el exportador incluye la ruta sin claves. Una disponibilidad de prueba habilita solo el stub 501, nunca una sesión. T02 sustituye disponibilidad por opciones reales; T05 completa firma/entrada, T07 enlaces, y se retiran los 501 antes del cierre E8.

La prueba de operador usa un Phone ya verificado, exige fallo del issuer sin enlace y comprueba la copia es «Para entrar, usá la web.», sin URL, operador ni Plataforma. El despacho real de esa respuesta, la invalidez del enlace previo y su replay se prueban al implementar T07 y en la puerta Meta: no se afirma esa integración todavía.

### Validación T01

- Build de solución: 0 advertencias y 0 errores.
- Arquitectura: 149/149 verdes; las guardas de entrada/servicio reconocen el namespace de módulo por su forma, sin excepción por clase. Rutas técnicas requieren un contribuidor explícito y no un FeatureGate de organización.
- Application focal: 19/19 verdes (operador, capabilities genéricas, destino de invitación, recursos/DI y flujo de correo).
- Integración: 50/50 verdes (rutas/contrato/cuenta/registro/recuperación/invitaciones); última comprobación de los 14 casos de OpenAPI y esqueletos en verde tras los metadatos finales. Stub sin cookie, destinos excluyentes, disponibilidad apagada/retirada/activa de prueba y paridad versión/runtime.
- Front: generación y `contracts:check` verdes, build verde y 30/30 tests focales. El test tipado comprueba cuerpos reales Phone y anterior Phone. Lint global termina sin errores y conserva advertencias de archivos ajenos a esta parte; lint de los archivos propios sin avisos. Vitest se corrió fuera del sandbox después de su fallo de archivos temporales, no de conducta.
- No hay pantallas, migraciones, Meta, asistente ni puerta E8 completa en esta parte.

### Decisiones técnicas tomadas en T01

- `IHttpContractContributor` permite que el módulo aporte catálogo/ejemplos sin una referencia del exportador del núcleo al módulo. Cada capa registra sus servicios en su propia composición; Program tiene una sentencia removible.
- Los ejemplos ficticios de país/cultura/zona viven en `docs/contracts/examples/identity.json`, embebido para exportar sin conexión, sin listas de referencia en código ni ampliación de sus guardas.
- 501 tiene tipo Result y resources propios para declarar pendientes sin una excepción de negocio. No se usa como implementación final.
- La policy temporal del webhook está registrada a 120 peticiones/IP/minuto y con máximo 5 MiB; no es la cuota UTC de códigos. T05 completa opciones/rechazo específico conforme a operaciones; en T01 runtime está apagado.

### Commits y comprobación entre repos

Se agregan los dos hashes al completar los commits, junto con SHA-256 del OpenAPI normalizado a LF. No abrir los chats paralelos hasta esa anotación.
