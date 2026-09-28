# Decisiones de arquitectura (ADR)

Cada decisión se registra en un archivo `NNNN-titulo.md` cuando se implementa, con este formato: contexto, decisión, alternativas descartadas y consecuencias. Esta tabla es el índice. Las decisiones 0001 a 0007 se heredan de `../ArquitecturaBase` sin cambios; de la 0008 en adelante son propias del multitenant.

| # | Decisión | Estado |
|---|---|---|
| 0001 | Transacción explícita por caso de uso: `IUnitOfWork.ExecuteInTransactionAsync(work, CommitPolicy, ct)` es la única forma de guardar | Heredada |
| 0002 | Todo body o query de entrada tiene un contrato en `Api/Contracts/<Área>`, con mapeo manual. Las respuestas son los `*Response` de Application | Heredada |
| 0003 | Sin eventos de dominio | Heredada |
| 0004 | **Roles** es el área de referencia para copiar | Heredada |
| 0005 | Sin versionado de API. El primer cambio incompatible introduce `Asp.Versioning` | Heredada |
| 0006 | Seed idempotente siempre. Fuera de Development, las migraciones se aplican con un migration bundle | Heredada |
| 0007 | WhatsApp es un módulo opcional y quitable: `Modules/WhatsApp` en cada capa, puertos `ILoginCodeChannel`, `IInvitationChannel` e `IPhoneLinkObserver`, y un `AddWhatsAppModule()` por capa. Es el diseño de la Etapa 6 de ArquitecturaBase; si esa etapa lo ajusta, se ajusta acá también | Heredada |
| 0008 | Aislamiento: una base compartida, `TenantId`, filtro de EF con nombre `"Tenant"` y RLS forzado en el esquema `tenant` | Aceptada 2026-09-27 |
| 0009 | ~~Una cuenta pertenece a una organización~~ **Reemplazada por 0018** el mismo día. Sigue vigente que el tenant se resuelve solo por el claim `tenant_id` | Reemplazada |
| 0010 | Dos roles de base de datos: `mt_owner` (migraciones) y `mt_app` (runtime sin privilegios). Se validan al arrancar | Aceptada 2026-09-27 |
| 0011 | `identity.AspNetUsers` es global, sin `TenantId` ni RLS. Los readers de negocio parten de `tenant.Members`, y la búsqueda global vive solo en `Infrastructure/Identity` | Aceptada 2026-09-27 |
| 0012 | Empresas dentro de la organización. `companyId` va en la URL, nunca en el token. Roles con alcance de organización o de empresa (los tres alcances, `Organization`, `AnyCompany` y `SpecificCompany`, en la 0034) | Aceptada 2026-09-27 |
| 0013 | Un solo cliente OIDC, `web`. El área la decide el claim `access` (`consumer`, `business` o `platform`). Los permisos no viajan en el token | Aceptada 2026-09-27 |
| 0014 | Outbox persistente (correo y WhatsApp) en lugar de colas en memoria. **Difiere a propósito de ArquitecturaBase**, que mantiene `EmailQueue` y `WhatsAppSendQueue` en memoria: con varias organizaciones y réplicas no se puede perder una invitación en un reinicio | Aceptada 2026-09-27 |
| 0015 | Auditoría automática de cambios (`AuditTrailInterceptor`) más eventos explícitos, en la misma transacción | Aceptada 2026-09-27 |
| 0016 | El front genera sus tipos desde `docs/contracts/openapi.json` (openapi-typescript), y el CI verifica que no haya diferencias | Aceptada 2026-09-27 |
| 0017 | Sin carpeta `tools/`: el operador inicial sale del seed y los roles de BD del bootstrap o del DBA | Aceptada 2026-09-27 |
| 0018 | ~~Perfiles con selector (Personal + organizaciones)~~ **Reemplazada por 0030**. Texto original: **B2B + B2C con una sola cuenta** (el flujo de cuenta de Mercado Libre; el producto **no** es un marketplace). Una identidad global por persona, con un perfil Personal (tenant `Kind=Personal`, creado al registrarse) y N perfiles Business por membresía. Los perfiles no comparten datos. El token lleva el perfil activo, y se cambia con `authorize?prompt=none&tenant=` | Reemplazada |
| 0019 | **Presentación unificada de datos.** Contrato invariante en la API (UTC con `Z`, `DateOnly`, `decimal`, `Money` con moneda ISO, porcentaje como fracción, `null` para vacío). Se formatea solo en `shared/format` (front) y en `DisplayFormatter` (back), con perfiles fijos por cultura (`es-AR`, `en-US`), y los dos lados se prueban contra `docs/contracts/format-cases.json` | Aceptada 2026-09-27 |
| 0020 | Autoregistro de personas abierto y alta de empresas **aparte** ("Registrá tu empresa", nunca desde el acceso B2C), los dos configurables en `PlatformSettings` (`ConsumerSignup`, `BusinessSignup`) | Aceptada 2026-09-27 (corregida el mismo día) |
| 0021 | Las rutas declaran su acceso con `[Access(...)]` (Consumer, Business o Platform) o `[PublicSite]`. El acceso B2C no tiene roles: la persona tiene implícitos los permisos `personal.*`; sobre datos compartidos decide `PartyPolicy` | Aceptada 2026-09-27 (corregida el mismo día) |
| 0022 | Integraciones con **las mismas claves de configuración que ArquitecturaBase** (`Authentication:Google:*`, `Email:Smtp:*`, `WhatsApp:*`). Gmail por SMTP también en desarrollo; Google para ingresar y registrarse (desde la Etapa 3); secretos solo en user-secrets o variables de entorno, cargados con `scripts/secretos/` | Aceptada 2026-09-27 |
| 0023 | **Concurrencia optimista**: `IVersioned` mapeado a `xmin`, `version` obligatoria en los contratos de edición y borrado, y 409 `General.ConcurrencyConflict` | Aceptada 2026-09-27 |
| 0024 | **Collation ICU `es-AR`** para toda la base; verificada al arrancar | Aceptada 2026-09-27 |
| 0025 | **Datos de entrada normalizados**: textos limpios por un conversor global y `TextLimits`; `Email`, `PhoneNumber` y `TaxId` como value objects | Aceptada 2026-09-27 |
| 0026 | **Idempotencia** en los `POST` que crean o envían (`[Idempotent]` + `Idempotency-Key`, con reserva por índice único) | Aceptada 2026-09-27 |
| 0027 | **Términos y privacidad versionados**, aceptación registrada y exportar los datos (la baja de la cuenta se decide en la 0035) | Aceptada 2026-09-27 |
| 0028 | **Módulos por organización** con `Microsoft.FeatureManagement` y un filtro por tenant; módulo ≠ permiso; un módulo apagado responde 404 | Aceptada 2026-09-27 |
| 0029 | **Accesibilidad WCAG 2.2 AA** con `vitest-axe` en cada test de pantalla, y **diseño adaptable** a 390, 768 y 1440 declarado por columna | Aceptada 2026-09-27 |
| 0030 | **Una cuenta, dos accesos que no se mezclan:** como persona (B2C, espacio personal) y como empresa (B2B, organizaciones). Se elige al ingresar y se cambia sin volver a ingresar. Una persona B2C no crea empresas | Aceptada 2026-09-27 |
| 0031 | **Página pública por organización en un subdominio** (`<slug>.plataforma.com`), un solo SPA, resolución por host **solo para datos públicos**, redirect URI validada contra los slugs publicados; dominio propio, más adelante | Aceptada 2026-09-27 |
| 0032 | **Tres clases de datos** (reincorporada): privado (`tenant`), público (`public_site`) y compartido persona ↔ empresa (`engagement`), cada una con su interfaz, filtro y política RLS. La plantilla trae **solo la mecánica**; los módulos de negocio los pone cada producto | Aceptada 2026-09-27 |
| 0033 | **Varios métodos de ingreso por cuenta** (correos, teléfonos, Google), todos verificados y únicos, uno principal. Los correos de un dominio verificado de una organización quedan **administrados**: dejan de servir para ingresar cuando termina la membresía. Aviso fijo mientras la cuenta dependa solo de métodos de una empresa; "Recuperar mi cuenta" asistida como último recurso | Aceptada 2026-09-27 |
| 0034 | **Empresas planas** (sin matriz ni herencia); **roles con tres alcances**: toda la organización, cada empresa (se elige al asignar) o una empresa puntual; **catálogo de permisos fino** (ver, invitar, administrar y asignar por separado; más la página pública) | Aceptada 2026-09-27 |
| 0035 | **Baja de una cuenta** con 30 días de gracia: `Active → PendingDeletion → Deleted`, con reautenticación. El único Dueño de una organización no cerrada y los operadores no pueden darse de baja. Revoca las sesiones; ingresar durante la gracia ofrece cancelar. La eliminación anonimiza la identidad (la fila queda), borra los métodos y el espacio personal, quita las membresías, anonimiza la copia en los datos compartidos y conserva la auditoría. Los módulos se suman con `IAccountDeletionParticipant` | Aceptada 2026-09-27 |

**Descartado a propósito:**
- un marketplace **de productos** con catálogo, carrito y compras como parte de la plantilla (el 27 de septiembre se lo sacó; después se reincorporó solo la mecánica genérica, en la 0032);
- una base por tenant (costo operativo);
- resolver el tenant de la sesión por subdominio (el tenant sale solo del claim `tenant_id`; el subdominio resuelve solo lo público, ver 0031);
- nueve roles de base de datos y clases `*Transaction` por caso de uso (el intento del 26 de septiembre);
- un IdP externo para los operadores. Los operadores tienen TOTP propio desde la Etapa 9.
