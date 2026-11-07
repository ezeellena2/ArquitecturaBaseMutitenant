# Etapa 10 · Despliegue y operación

**Estado:** pendiente · plan corto de la E10. Alcance exclusivo de [Etapa 10 de la hoja de ruta](2026-09-27-plan-de-desarrollo.md#etapa-10-despliegue-y-operación), ADR vigentes y fichas. Roles sigue siendo el modelo de código; las decisiones del usuario prevalecen. Este plan no contrata servicios ni publica la aplicación.

## Forma de trabajo

**Se programa cuando termine la prueba de punta a punta de todo el sistema en local, que hace Codex, en paralelo con las cuentas** (decisiones del usuario del 2026-10-20). La 9 ya cerró. Las decisiones P1–P7 las tomó el usuario el 2026-10-16, y la P8 el 2026-10-18 (al final del plan).

K1, B1 a B5 y F1 a F4 no esperan las cuentas: el almacenamiento se prueba con su emulador, y la imagen y el despliegue se arman sin publicar. Mientras tanto, el usuario prepara:
- la suscripción de Azure, con la región y el presupuesto confirmados con la cotización (app, base, Redis y Blob en la misma región), y con ella el destino de trazas y la red de la base (privada o firewall);
- el dominio comprado;
- para WhatsApp (P8), el portafolio de Meta de la plataforma, con su nombre y su marca, el número real y la app de producción.

El Coordinador los registra cuando estén. Lo que los necesita se hace con ellos, en I1: la prueba contra la cuenta real del almacenamiento, el despliegue y la integración. Se implementa un solo despliegue y un solo adaptador de nube, los elegidos, sin construir alternativas.

Contrato primero y commiteado en ambos repos; Back y Front en paralelo; integración y una `/puerta` al final. Commits chicos en español, con pathspec, sin push; cada dueño toca su repo. Tests de lógica dirigidos, build afectado e integración/seguridad del área al terminarla; suites completas una vez en puerta. Después de un arreglo, sólo lo afectado, con las excepciones transversales de AGENTS. Sin actas, evidencia versionada ni revisiones por tarea; worktrees sólo para puerta. Diseño es el único que modifica el lienzo.

## Contrato · K1

El Coordinador publica `docs/contracts/openapi.json`, DTO HTTP, stubs 501 sin efectos, errores/textos es/en y pruebas de contrato. Front ejecuta `npm run contracts`. No se modifican rutas existentes ni se agrega administración de exportaciones.

### Exportar mis datos

Base `D = /api/me/data-export`. `[Access(Consumer, Business)]`, sólo la cuenta autenticada: ni `userId` ni `tenantId` de entrada, sin permiso de empresa y sin roles nuevos. Como las demás acciones propias, no pide reautenticación (0045). Estar en Business no convierte sus datos empresariales en personales. Plataforma no solicita exportaciones de otra persona.

| Método/ruta | Entrada | Éxito | Errores específicos |
|---|---|---|---|
| POST D | JSON `{}`; `Idempotency-Key: UUID` obligatorio | 202 `{id:uuid,state:"Queued",requestedAtUtc:datetime}`; Location con la ruta de descarga | 400 `Identity.DataExport.EmailRequired`; 409 `Identity.DataExport.AccountUnavailable` |
| GET D/{id}/download | UUID propio; sin body ni token en URL | 200, archivo JSON UTF-8, `Content-Disposition: attachment; filename="mis-datos.json"` | 404 `Identity.DataExport.NotFound` / `Identity.DataExport.Expired`; 409 `Identity.DataExport.NotReady` / `Identity.DataExport.Failed`; 503 `Identity.DataExport.StorageUnavailable` |

POST usa `[Idempotent]` con respuesta sólo técnica: mismo cuerpo/clave conserva id, fecha y 202 y no repite trabajo/correo; dos claves son dos solicitudes. GET no consume el archivo: se puede volver a descargar durante su vigencia. No hay historial ni polling nuevo.

El aviso se manda **sólo por correo**. Se elige el principal utilizable con correo verificado (`Email` o `Google.ContactEmail`); luego otro correo verificado utilizable, orden estable por id, sin duplicarlo. Sin destinatario, 400: «Agregá y verificá un correo para recibir tus datos.» No se elige un teléfono ni se usa directamente `AspNetUsers.Email`. Se revalida la propiedad/utilidad del método al preparar y enviar el aviso, siguiendo el outbox existente.

Al terminar el JSON se fija `readyAtUtc` y `expiresAtUtc = readyAtUtc + 48 horas`, con TimeProvider. En la misma UoW se marca Ready y se encola «Exportación de datos lista», con enlace al dominio principal `/cuenta?dataExport={id}` y vencimiento formateado por DisplayFormatter, cultura/zona explícitas. El enlace no contiene credenciales: al abrirlo exige iniciar sesión con la misma cuenta y descarga mediante httpClient autenticado. No entrega URL pública del bucket ni SAS/presigned URL. La demora del correo no amplía las 48 horas. El correo sigue el tablero Mensajes, variante «Exportación de datos lista».

Todos los errores son ProblemDetails con `code`, `traceId` y resources es/en. Además: 400 `Request.Invalid`/`Validation.Failed`; 401 `Http.Unauthorized`; 403 `Tenancy.Access.Wrong`, `Http.Forbidden` o `Legal.AcceptanceRequired`; 429 `Security.RateLimit.Exceeded` con `retryAfter` y Retry-After, heredado de E9. Idempotencia conserva 400 `Request.IdempotencyKeyRequired`, 409 `Request.InProgress`, 422 `Request.IdempotencyKeyReused`. POST declara 202/400/401/403/409/422/429; GET, 200/400/401/403/404/409/429/503. Recurso ajeno/revocado siempre 404 NotFound; vencimiento propio dice «Este enlace venció. Volvé a pedir tus datos.»; NotReady, «Todavía estamos preparando tus datos.»; Failed, «No pudimos preparar tus datos. Volvé a pedirlos.»; AccountUnavailable, «Tu cuenta no permite preparar una exportación ahora.»; StorageUnavailable, «No pudimos descargar tus datos. Probá de nuevo.» GET y correo nunca exponen el estado de otra cuenta. Respuestas de descarga, errores y navegación del enlace: `Cache-Control: no-store`, sin caché Redis.

### Contenido y preparación

Archivo con `{schemaVersion:1,requestedAtUtc,generatedAtUtc,account,personal,shared,modules}`: contrato de archivo versionado, sin versionar la API (0005). `account`: nombre/apellido, preferencias, métodos propios sin credenciales, consentimientos y fechas; `personal`: preferencias y datos propios, null sin espacio Personal; `shared`: sólo copias del lado de la persona; `modules`: secciones aportadas por los módulos instalados. JSON invariante: instantes UTC Z, fechas civiles YYYY-MM-DD, null sin dato y Money con moneda. Se leen lotes/cursor y se escribe un stream temporal privado, sin cargar toda la exportación en memoria. `generatedAtUtc` identifica el cierre de la lectura; no se promete un snapshot histórico anterior a los cambios del usuario.

- Asistente: conversaciones/mensajes propios del acceso Personal y metadatos de sus adjuntos; nunca conversaciones Business ni Plataforma, presupuestos empresariales o claves. No cambia las retenciones de 0048/0051.
- WhatsApp: inventario personal de identidad/Personal de la feature vigente y B7 de E11: contactos y vínculos propios, mensajes propios por propietario original o copia personal autorizada, metadatos/estados y texto todavía retenido. No se infiere dueño por el titular actual de un teléfono ni se exporta la bandeja de la empresa. Reasignar un contacto no transfiere historia.
- No incluye datos privados empresariales, secretos, tokens, hashes, enlaces de acceso, payloads de outbox ni adjuntos binarios dentro del JSON. Un módulo apagado conserva su obligación de exportar datos personales retenidos; retirarlo elimina su contribuidor, sin referencias del núcleo a sus tipos.

`platform.DataExports` es metadato global de cuenta: propietario, estado, fechas, lease, referencia opaca al archivo y deduplicación del aviso; nunca el JSON. Se clasifica por tipo exacto en las guardas de persistencia; cada consulta filtra propietario. Estados internos Queued→Processing→Ready, con Failed/Revoked/Expired y reintento durable por lease. Opciones ValidateOnStart: sondeo de 30 s, lease de 15 min, máximo de 8 intentos, lotes de 100 y gracia de huérfanos de 24 h. El servicio público abre una UoW por fase; el worker toma trabajo con lock/SKIP LOCKED, libera la transacción antes de leer/escribir almacenamiento y confirma sólo con lease válido. Cada fuente de datos entra únicamente al scope Personal derivado de la cuenta; no recorre Business para leerlo como datos personales.

Decisiones técnicas: nuevo puerto neutral `IAccountDataExportSource` en Application para contribuciones del núcleo/módulos, siguiendo la composición de `IAccountDeletionParticipant`; sin helpers genéricos ni nuevos puertos de formularios. Cuenta, estado y propiedad se revalidan antes de publicar y antes de abrir el stream. Un participante de baja revoca exportaciones al pedir la baja y registra eliminación durable del archivo; la limpieza física va después del commit, con reintentos y Delete idempotente, sin IO externo dentro de la UoW. El vencimiento aplica aunque el borrado físico tarde; crash entre subir/confirmar usa IFileStoreMaintenance. No se marca Ready ni se manda correo por archivo incompleto. Auditoría técnica sin JSON, contacto completo ni storageKey.

### Contenedor y operación

Una imagen Linux .NET 10 contiene la API y el build Vite en `wwwroot`, mismo origen por host (0031), sin servidor Vite productivo ni CORS nuevo. Build multietapa Node/.NET con ambos repos hermanos y SHAs explícitos; issuer principal público en `VITE_AUTH_ISSUER` al compilar y `Authentication:Issuer` al ejecutar, idénticos. Nada de issuer desde Host ni `localtest.me` en el producto. Usuario sin privilegios, ICU disponible, fuentes actuales del PDF y directorio temporal privado fuera de wwwroot; secretos fuera de capas/argumentos de build. Una réplica mínima siempre activa para outbox/trabajos; las réplicas mantienen leases existentes.

Se conserva `/alive` como liveness y `/health` como readiness; `/health` todavía no revisa PostgreSQL (backlog 54). `/assets` sale precomprimido y con `max-age=31536000, immutable`; el resto de wwwroot, con `no-cache`; el JSON de la API, sin comprimir. El `index.html` de un subdominio publicado lleva `<title>`, `description` y `og:*` de su organización (vista previa del enlace). Forwarded headers sólo desde el proxy conocido; HTTPS, cookies y CSP de E9, incluidos Google y SDK/marco Facebook. DNS principal y `*.<principal>` apuntan al mismo ingreso; certificado cubre ambos nombres. Subdominio sigue resolviendo sólo lo público, tenant privado sólo por claim. No hay endpoint nuevo de operación.

**Base:** PostgreSQL 18 compatible con los tests actuales, ICU `es-AR` al crear la base, RLS forzado y separación mt_owner/mt_app. El DBA/bootstrap del proveedor crea roles y base; migration bundle con mt_owner en job previo, una ejecución serializada por ambiente; la API usa sólo mt_app, no migra en Production. El seed idempotente existente se conserva. La factory de diseño admite `Migrations:ConnectionString` desde el ambiente del job, en lugar de su conexión fija; sin esa variable conserva el scaffolding offline. El bundle no se ejecuta desde la API ni recibe secretos por argumentos impresos. dotnet-ef usa la versión EF central, sin otra versión independiente.

**Archivos:** implementar únicamente S3 o Azure Blob de P4 como IFileStore **e IFileStoreMaintenance**; disco sólo Development/Testing. Mantener keys opacas, longitud exacta, borrado idempotente, confirmación y búsqueda acotada de huérfanos por categoría/fecha. Contenedor/bucket privado, TLS, cifrado en reposo e identidad de carga de trabajo del host con permisos mínimos; no ACL públicas ni fallback a disco en Production. La capa elegida no altera contratos de Asistente y servirá a la E18 sin programarla. **Sin prefijo ni contenedor por organización:** las claves siguen opacas en un solo contenedor privado. El aislamiento lo dan la metadata de cada módulo, con RLS, y la Api, y borrar lo de una organización recorre esa metadata. La E18 lo usa así.

**CI/CD:** front descarga el back hermano y corre contracts:check obligatoriamente; eliminar la salida exitosa por repo ausente. Token de checkout `secrets.CONTRACTS_READ_TOKEN || github.token`: lector de la contraparte para privados, lectura pública con el token estándar; `persist-credentials:false`. Jobs de PR sin secretos privilegiados, sin pull_request_target ejecutando código del PR; un checkout fallido no da verde. Release toma dos SHAs verificados, construye imagen/bundle y despliega por digest usando GitHub Actions/OIDC de P1. `id-token:write` sólo en despliegue, trust con audience y subject reales de repo/ref y rol por ambiente; sin claves cloud permanentes ni depender de ambientes protegidos de GitHub que Free privado no incluya. Promoción manual del artefacto verificado, sin recompilarlo; bundle antes de tráfico y rollback de imagen compatible, nunca downgrade automático de base.

**Secretos/certificados:** claves existentes de configuracion.md y scripts/secretos, PFX distintos para firma/cifrado OpenIddict y Data Protection, anillo persistido compartido; Redis administrado con TLS según 0052. Gestor de secretos por ambiente, sin valores en logs/artefactos. B4 agrega listas `Previous` de `{Base64,Password}` en `Authentication:Certificates:{Signing,Encryption}` y `DataProtection:Certificate`: se emite con el actual y se valida/descifra con los anteriores; probar entre réplicas y no retirar uno mientras existan tokens o datos que dependan de él. Correo de producción (P6): el SMTP de Gmail que ya se usa, con contraseña de aplicación (unos 500 correos por día, compartidos con ArquitecturaBase), con prueba de entrega. Pasar a un servicio transaccional con dominio verificado (SPF, DKIM y DMARC) es solo configuración y va en «Al lanzar un producto». Nada nuevo de IA, formularios o negocio.

### WhatsApp en producción (P8)

Desde la 10, producción tiene WhatsApp: el número real de la plataforma, con los códigos de ingreso, el bot y los avisos del sistema de la E8, y el alta embebida para que cada organización conecte el suyo (E11). No se cambia código de WhatsApp: son configuración, secretos y verificación.

- **Dos apps de Meta.** La actual, «Plantilla Multitenant» (AppId 1482265200631609), queda para desarrollo, con el túnel y la WABA de prueba. Producción usa una app nueva del portafolio de la plataforma. El motivo: una app tiene una sola URL de webhook. El override por WABA o por número cubre `messages`, `calls` e `history`, pero `account_update` (que avisa que terminó un alta embebida) y el estado de las plantillas van siempre a la URL de la app ([override](https://developers.facebook.com/documentation/business-messaging/whatsapp/webhooks/override/)).
- **La app de producción** tiene:
  - el webhook HTTPS en el dominio principal, con los campos de la E11 y su `VerifyToken`;
  - en Facebook Login for Business, prendidos Client OAuth, Web OAuth, Enforce HTTPS, Embedded Browser OAuth, Strict Mode y el ingreso con el SDK de JavaScript;
  - el dominio principal exacto, sin comodín, en Allowed domains y en Valid OAuth redirect URIs. El alta embebida se abre solo en el dominio principal, porque el tenant sale del claim ([implementación](https://developers.facebook.com/documentation/business-messaging/whatsapp/embedded-signup/implementation/));
  - una configuración del alta con un acceso que no vence, el valor por defecto de Meta para la comunicación entre servidores. Así deja de existir el vencimiento a los 60 días de la configuración de desarrollo.
- **El número de la plataforma:**
  - es real, con código de área, recibe SMS o llamadas y no está en WhatsApp; si está, se borra antes;
  - lleva un PIN de 6 dígitos y su nombre visible, y su WABA tiene medio de pago;
  - antes de abrir, las 16 plantillas del sistema del [inventario](../operations/whatsapp-plantillas.md) se crean y se aprueban en esa WABA.
- **Los límites:**
  - hasta verificar la empresa, 10 organizaciones nuevas cada 7 días, 250 destinatarios por día fuera de la ventana de 24 horas y 2 números;
  - con la verificación de la empresa y App Review, 200 organizaciones por semana, 2.000 destinatarios y 20 números;
  - App Review pide acceso avanzado a `whatsapp_business_messaging` y `whatsapp_business_management`, con dos videos: un mensaje enviado y recibido, y la creación de una plantilla;
  - la verificación de la empresa tarda unos días y App Review, cerca de 24 horas ([Tech Provider](https://developers.facebook.com/documentation/business-messaging/whatsapp/solution-providers/get-started-for-tech-providers)).
- **Los secretos** van por nombre en el gestor del ambiente: los `WhatsApp:*` de [configuracion](../operations/configuracion.md), con los valores de la app y de la WABA de producción. Nunca van en el repo ni en la imagen.

Fuentes técnicas: [bundles EF](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying#bundles), OIDC de GitHub para [AWS](https://docs.github.com/en/actions/how-tos/secure-your-work/security-harden-deployments/oidc-in-aws) y [Azure](https://docs.github.com/en/actions/how-tos/secure-your-work/security-harden-deployments/oidc-in-azure), y [disponibilidad de ambientes GitHub](https://docs.github.com/en/actions/how-tos/deploy/configure-and-manage-deployments/manage-environments). Se aplican los parámetros aprobados en P1–P7, no se construyen dos plataformas.

## Back

Rutas de código abreviadas bajo `src/ArquitecturaBaseMultitenant.<Capa>/`; tests bajo `tests/ArquitecturaBaseMultitenant.<Proyecto>/`. Archivos nuevos de las tareas: **E10**, todavía no implementados.

| Tarea/estado | Archivos y trabajo | Copiá de (existente) | Tests |
|---|---|---|---|
| B1 · Pendiente | Domain/Legal/DataExport.cs y errores; Application/Services/Legal, modelos/validator/puertos; Infrastructure/Persistence, migración y Api/Controllers/Account/DataExportsController.cs. POST, ownership, estados, idempotencia y fuentes de cuenta/Personal | `Application/Services/Roles/RoleService.cs` para validación/OperationLog/UoW; `Api/Controllers/Account/AccountDeletionController.cs` para acceso propio; `Application/Services/Legal/AccountDeletionProcessingService.cs` para fases/scopes | Nuevos DataExportContractTests y DataExportTests: dos cuentas, Consumer/Business, ajeno 404, no datos de empresa, correo Email/Google, sin correo, clave/replay/carrera; clasificación global, capas, acceso y resources |
| B2 · Pendiente | Worker/servicio de procesamiento, fuentes neutrales en Modules/{Assistant,WhatsApp}; AccountNotice/template email y participante de baja, limpieza/leases | `Application/Interfaces/Integrations/Legal/IAccountDeletionParticipant.cs` para composición; `Application/Services/Messaging/OutboxDispatchService.cs` para lease; `Infrastructure/Messaging/Email/EmailAccountNoticeChannel.cs` para correo; `Infrastructure/Modules/Assistant/Retention/AssistantRetentionWorker.cs` para borrado fuera de UoW | Nuevos DataExportProcessingTests: dos workers, reinicio, archivo parcial, expiración exacta de 48 h, reintentos sin segundo aviso; baja durante preparación/descarga, limpieza fallida durable; módulos apagados, retiro por guardas, teléfono reciclado; SMTP sólo correo |
| B3 · Pendiente | Infrastructure/Files: adaptador y opciones de P4, registro explícito/ValidateOnStart; versiones sólo Directory.Packages.props | `Application/Interfaces/Integrations/Files/IFileStore.cs`, `IFileStoreMaintenance.cs` de esa carpeta y `Infrastructure/Files/LocalFileStore.cs`; `Application/Modules/Assistant/Services/AssistantAttachmentService.cs` para subir/confirmar | Suite de contrato común con Local y emulador del proveedor elegido: Put/OpenRead/Delete, bytes exactos, keys ajenas/inválidas, confirmación/huérfanos acotados; Asistente sube/lee/borra sin cambio HTTP y nada público; smoke contra cuenta aislada real |
| B4 · Pendiente | Nuevos Dockerfile, Dockerfile.dockerignore, `.github/workflows/deploy.yml`, scripts/deploy y configuración de P1; bundle, DesignTimeApplicationDbContextFactory y release de dos repos; Previous en IdentityRegistration/OpenIddictRegistration. Ajustes mínimos en `.github/workflows/ci.yml` y scripts/secretos para verificar nombres productivos, incluidos los de WhatsApp (P8), y la CSP del SDK de Facebook en el dominio principal. Estáticos, `/health` y subdominio («Contenedor y operación»); sondas de ACA; `Maximum Pool Size` acorde al B1ms y las réplicas; `OTEL_*` de las trazas | `Api/Program.cs`, `Api/Hosting/SpaExtensions.cs`, `Api/Hosting/ForwardedHeadersExtensions.cs`; `Infrastructure/Caching/RedisHealthCheck.cs` para PostgreSQL; `Infrastructure/Persistence/DatabaseBootstrapExtensions.cs` para SQL ICU/roles (no invocar su recreación Development en Production) y `Infrastructure/Persistence/DesignTimeApplicationDbContextFactory.cs`; `Infrastructure/Identity/OpenIddict/CertificateLoader.cs`; `.github/workflows/ci.yml` para acciones fijadas | Nuevos DeploymentConfigurationTests; ampliar Hosting/SpaHostingTests y Auth/StartupConfigurationTests. Imagen sin SDK/secretos, rutas profundas/404/405, proxy/issuer/subdominio; bundle dos veces y migración previa→actual, mt_app sin DDL/bypass; restauración y rotación entre réplicas. HealthCheckTests: sin PostgreSQL, `/health` falla y `/alive` no; SpaHostingTests: `/assets` y metadatos del subdominio |
| B5 · Pendiente | Completar [runbook](../operations/runbook.md) con los comandos concretos de P1–P8 y responsables: los pasos de Meta de P8 y el dominio de producción en el cliente de Google (orígenes de JavaScript y URI de redirección); configuración, feature identidad/inventarios y fichas afectadas | `docs/operations/configuracion.md`, `scripts/secretos/verificar.ps1`, `Infrastructure/Persistence/Rls/RuntimeRoleValidator.cs` | Ensayo de backup/restore y checklist de lanzamiento en I1; detector en 0. Guardas de configuración/contrato cubren lo ejecutable; no tests que comparen párrafos |

**Texto para «Multitenant · 10 · Back» (Claude):**

> Leé AGENTS.md, fichas, ADR y docs/plans/2026-10-16-etapa-10-despliegue.md. La 9 ya cerró: empezá con K1 publicado, sin esperar las cuentas. Probá el almacenamiento con su emulador y dejá para I1 la prueba contra la cuenta real y el despliegue real. Hacé B1–B5 sólo en el back; implementá un proveedor de archivos y un despliegue, los aprobados. Conservá IFileStoreMaintenance, leases/UoW y privacidad de los módulos. Copiá por responsabilidad los archivos citados; tests focales y de integración/seguridad del área. Commits chicos en español, pathspec, sin push; suites completas sólo en puerta. No adelantes formularios. Cambios técnicos del contrato por Coordinador y Cambios al contrato; secretos sólo por nombre.

## Front

Rutas bajo `../ArquitecturaBaseMutitenantFront/`; tableros Cuenta/M-Cuenta y Mensajes, variante «Exportación de datos lista». Sin pantalla de historial nueva ni confirmación de identidad para esta acción.

| Tarea/estado | Archivos y trabajo | Copiá de (existente) | Tests |
|---|---|---|---|
| F1 · Pendiente | `src/areas/personal/account/pages/AccountPage.tsx`, API/estado de exportación y locales es/en. Botón Privacidad, key al abrir operación, 202 sin polling, estados de envío/error y falta de correo con motivo | `src/shared/api/useIdempotentMutation.ts`, `src/areas/personal/account/pages/AccountPage.tsx` y `src/shared/ui/ImportantNotice.tsx` | AccountPage.test.tsx: doble clic/red/replay, sin correo/Google, desmontar, 429 sin reenvío automático, error por code, teclado/axe y móvil |
| F2 · Pendiente | Cuenta procesa query dataExport tras login, descarga autenticada y errores/expiración; limpia estado al cambiar cuenta/acceso | `src/shared/api/httpClient.ts` con api.response; patrones de `src/modules/assistant/api/export.ts` y `src/modules/assistant/lib/privateDownload.ts`, con contexto/errores de cuenta y sin importar el módulo; `src/auth/SessionRecovery.tsx` para vuelta a sesión | Nuevos DataExportDownload tests: enlace de correo, sesión ausente/distinta, expired/notReady/503, no guardar tokens/JSON, cancelar stream y revocar object URL, sin descargar un error como archivo |
| F3 · Pendiente | `.github/workflows/ci.yml`, `scripts/check-contracts.mjs`, pruebas y release coordinada con B4; build con issuer de P5 | `scripts/generate-contracts.mjs`, `scripts/validation-contracts.test.mjs`, `.github/workflows/ci.yml` | Ausencia de hermano en CI falla; archivo/schema/validation divergentes fallan, checkout privado sin permiso falla; caso válido compara ambos; build/issuer/subdominio, sin saltar chequeo |
| F4 · Pendiente | MSW, e2e, captura/galería y recorrido E10, documentación de la conducta cambiada | `src/areas/personal/account/pages/AccountPage.test.tsx` y scripts vigentes de galería/recorrido E11 | Recorrido I1, tests focales/lint/tsc durante trabajo; suites completas en puerta |
| F5 · Pasa a la base de la 12 (2026-10-25): no se hace en la 10 | Botones y avisos según la decisión del usuario del 2026-10-24 (`docs/rules/pantallas-y-ui.md`). Primero, la banda: `useEditPage` deja de devolver `unsaved` («Cambios sin guardar») y cada pantalla que lo usa lo saca; «¿Salir sin guardar?» queda. Después, la espera: `Button` con `pending` se ve como apagado, con el aro y su mismo texto, y sale `pendingLabel` («Guardando…», «Invitando…», «Pidiendo…» y los demás), con sus locales es/en. Por último, los botones que hoy muestran «Reintentá en …» (`useRetryAfterCountdown`) conservan su texto: quedan apagados mientras dura la espera, y el tiempo va en el aviso de error de siempre. Mientras dura un 429 con Retry-After en el ingreso, ese error dice «Hiciste muchos pedidos seguidos. Probá de nuevo en {{time}}.», con la cuenta viva (tableros Ingreso y Enlace de la v137). Reemplaza `resendTooSoon` y los dos `tooManyRequests` de `src/locales/es/auth.json`, con sus pares en inglés. «Reenviar en 30 s» queda como está. En el ⋮ de una fila, `RowAction.disabledReason` deja de mostrarse debajo de la opción: va en su tooltip y como descripción accesible, también en la hoja del teléfono | `src/shared/hooks/useEditPage.tsx`, `src/shared/ui/Button.tsx`, `src/shared/api/useRetryAfterCountdown.ts`, `src/shared/ui/RowActions.tsx`, `src/locales/{es,en}/auth.json` | Los tests que hoy esperan «Cambios sin guardar», un texto de espera o «Reintentá en …» en un botón (como `PublicPagePage.test.tsx`) pasan a esperar la banda sin él y el botón con su texto |

**Texto para «Multitenant · 10 · Front» (Claude):**

> Leé AGENTS.md de ambos repos, fichas y el plan de E10 del back. La 9 ya cerró: con K1 publicado, regenerá tipos y hacé F1–F4 sólo en el front, copiando Cuenta/M-Cuenta y las piezas citadas. Conservá la sesión como prueba, sin reauth personal ni URLs públicas del archivo. Activá el chequeo cruzado obligatorio en CI, también para repos privados. Los estados y textos salen de los tableros de la v135; si falta uno, pedíselo a Multitenant · Diseño, sin dibujarlo. Commits chicos con pathspec, sin push; tests focales y una sola puerta final. Informá cambios de contrato al Coordinador antes de publicarlos.

## Integración y puerta

- **I1 · Pendiente.** Coordinador conecta API/SPA reales y entorno productivo aislado con recursos aprobados. Ensaya imagen, bundle mt_owner y runtime mt_app/ICU/RLS; issuer principal y dos subdominios sin cruce, TOTP/cookies E9, Google, SMTP real y archivo nube. Recorrido: pedir exportación, recibir correo, login misma cuenta y descargar JSON; cuenta ajena 404; avanzar reloj aislado 48 h; baja concurrente borra archivos; módulo apagado sin fuga. Ensaya backup/restore con anillo y archivos, rotación PFX y rollback de imagen. La prueba real del correo confirma entrega sin registrar el enlace ni sus datos. Con WhatsApp real en producción (P8) prueba el código de ingreso por el número de la plataforma, el bot y una plantilla del sistema fuera de la ventana. También prueba una organización que conecta su número con el alta embebida, recibe y responde. Esa prueba es la que la 11 dejó para acá al cerrar el 2026-10-21. Se suman las pruebas a mano y los casos bloqueados del informe de punta a punta del 2026-10-20 (`docs/design/capturas/e2e-completo/2026-10-20/` del front). Una falla que aparezca en algo ya cerrado se arregla dentro de la 10, sin reabrir su etapa.
- **I2 · Pendiente.** `/puerta`, una vez: build back sin warnings/dotnet test Docker; front build/lint/test/contracts:check; E2E real; detector en 0; galería lado a lado sólo Cuenta y estados cambiados E10; `/recorrido`. No repetir retiro de módulos ya verificado; sus guardas siguen verdes. Tests front/E2E solos según skill; arreglos al dueño, luego sólo lo afectado. Tabla final corta, salidas `.local/`, apagar Aspire y quitar worktrees de puerta. El despliegue reutiliza el artefacto que pasó estas comprobaciones; la promoción requiere las cuentas y presupuesto decididos por el usuario.

**Texto para «Multitenant · 10 · Coordinador» (Claude):**

> Leé AGENTS.md de ambos repos, /plan-etapa, etapa-en-paralelo y este plan. La 9 ya cerró. Publicá K1 en ambos repos y habilitá Back/Front en paralelo con estos textos, sin esperar las cuentas. Registrá P1–P8 cuando el usuario tenga Azure, el dominio y Meta; lo que los necesita va en I1. Los tableros ya están aprobados (v135). Coordiná SHAs, contrato, artefactos y permisos de lectura/OIDC. Integrá I1 y una /puerta con /recorrido, sin repetir suites por cada arreglo. No contrates recursos fuera del presupuesto aprobado ni publiques credenciales. Dejá tabla final y runbook aplicable; sin push, sin lienzo ni funcionalidades fuera de E10.

## Relación con otras etapas

- **E9:** recibe TOTP, opciones, cuotas y telemetría; conserva 0052, Redis administrado TLS y anillo compartido, sin rediseñar caché.
- **E8b/E11:** nube sirve al Asistente; exportación respeta autoría y retención del inventario WhatsApp. WhatsApp funciona en producción desde la 10 (P8), y el acceso del alta embebida de producción no vence.
- **12 a 25 (formularios):** reciben operación y almacenamiento de E10; no se implementan formularios, adjuntos WhatsApp/E18 ni IStructured* de las specs. La F5 de botones pasó a la base de la 12.

## Tableros (lienzo v135)

Los aprobó el usuario el 2026-10-20 en `Cuenta` y `M-Cuenta` (front 6bf8ec65), con el recorrido «Pedir mis datos». El correo «Exportación de datos lista» de Mensajes no cambia. Mandan sobre este plan en estos estados y textos:
- **Pidiendo:** el botón dice «Pidiendo…» y queda apagado.
- **202:** aviso verde, «Recibimos tu pedido. Te mandamos tus datos por correo cuando estén listos.».
- **Sin red:** aviso rojo con «Reintentar» y ✕, «No pudimos conectarnos. Revisá tu conexión.».
- **429:** cartel «Hiciste muchos pedidos seguidos. Esperá un momento.» y el botón «Reintentá en 0:41», como en la E9.
- **400 `EmailRequired`:**
  - «Exportar» queda apagado, con un ⓘ junto al rótulo; los dos abren el motivo «No podemos mandarte tus datos»;
  - el motivo dice «Agregá y verificá un correo para recibir tus datos.»;
  - en qué hacer, ofrece «Agregá un correo en «Cómo entrás»» o «O vinculá tu cuenta de Google», con el botón «Entendido».
- **Desde el enlace del correo:** el cartel «Descargando tus datos…» y después el aviso verde «Descargamos tus datos.».
- **Avisos rojos:**
  - `Expired`: «Este enlace venció. Volvé a pedir tus datos.»;
  - `NotFound`: «No encontramos esos datos en tu cuenta. Si los pediste con otra, entrá con esa.» (no dice si el pedido existe);
  - `StorageUnavailable` (503): «No pudimos descargar tus datos. Probá de nuevo.», con «Reintentar».

## Cambios al contrato

## Decisiones del usuario (2026-10-16)

Los precios consultados el 2026-10-16 están en la versión anterior de esta sección (8efcec6d). Los de Azure, la base y Redis se cotizan antes de crear recursos; ninguna recomendación autoriza gasto.

| # | Decisión |
|---|---|
| P1 | **Azure Container Apps**, consumo con una réplica mínima siempre activa. La región (Brazil South por cercanía o East US 2 por precio) y el presupuesto se confirman con la cotización, antes de crear recursos. |
| P2 | **Azure Database for PostgreSQL Flexible Server 18**, B1ms con 32 GB y backup automático. Antes de contratar se verifica la versión 18, ICU `es-AR` y que pase RuntimeRoleValidator. |
| P3 | **Redis Cloud Essentials pago, con TLS**, en la misma región que la app. El plan gratis no sirve. |
| P4 | **Azure Blob Hot LRS**, privado. Se programa solo ese adaptador. |
| P5 | **Dominio propio, que compra el usuario**; DNS en Cloudflare (solo DNS, gratis) y certificado comodín de Let's Encrypt por DNS-01 para el dominio y sus subdominios. |
| P6 | **El Gmail actual por SMTP**, hasta lanzar un producto. |
| P7 | **Repos privados en GitHub Free**; el chequeo cruzado usa un token de solo lectura. |
| P8 | **WhatsApp prendido en producción desde la 10** (2026-10-18): el número real de la plataforma y el alta embebida para que cada organización conecte el suyo. Es el centro del negocio. Dos apps de Meta, una de desarrollo y otra de producción (ver «WhatsApp en producción»). |
