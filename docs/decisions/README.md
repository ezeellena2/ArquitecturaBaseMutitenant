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
| 0012 | Empresas dentro de la organización. `companyId` va en la URL, nunca en el token. Roles con alcance Organization o Company | Aceptada 2026-09-27 |
| 0013 | Un solo cliente OIDC, `web`. El área la deciden `account_kind` y `tenant_kind`. Los permisos no viajan en el token | Aceptada 2026-09-27 |
| 0014 | Outbox persistente (correo y WhatsApp) en lugar de colas en memoria. **Difiere a propósito de ArquitecturaBase**, que mantiene `EmailQueue` y `WhatsAppSendQueue` en memoria: con varias organizaciones y réplicas no se puede perder una invitación en un reinicio | Aceptada 2026-09-27 |
| 0015 | Auditoría automática de cambios (`AuditTrailInterceptor`) más eventos explícitos, en la misma transacción | Aceptada 2026-09-27 |
| 0016 | El front genera sus tipos desde `docs/contracts/openapi.json` (openapi-typescript), y el CI verifica que no haya diferencias | Aceptada 2026-09-27 |
| 0017 | Sin carpeta `tools/`: el operador inicial sale del seed y los roles de BD del bootstrap o del DBA | Aceptada 2026-09-27 |
| 0018 | **B2B + B2C con una sola cuenta** (el flujo de cuenta de Mercado Libre; el producto **no** es un marketplace). Una identidad global por persona, con un perfil Personal (tenant `Kind=Personal`, creado al registrarse) y N perfiles Business por membresía. Los perfiles no comparten datos. El token lleva el perfil activo, y se cambia con `authorize?prompt=none&tenant=` | Aceptada 2026-09-27 |
| 0019 | **Presentación unificada de datos.** Contrato invariante en la API (UTC con `Z`, `DateOnly`, `decimal`, `Money` con moneda ISO, porcentaje como fracción, `null` para vacío). Se formatea solo en `shared/format` (front) y en `DisplayFormatter` (back), con perfiles fijos por cultura (`es-AR`, `en-US`), y los dos lados se prueban contra `docs/contracts/format-cases.json` | Aceptada 2026-09-27 |
| 0020 | Autoregistro B2C abierto y "Crear mi organización" en autoservicio, los dos configurables en `PlatformSettings` (`ConsumerSignup`, `BusinessSignup`) | Aceptada 2026-09-27 |
| 0021 | Las rutas declaran el tipo de perfil con `[TenantKind(...)]`. El perfil personal no tiene roles: su dueño tiene implícitos los permisos `personal.*` | Aceptada 2026-09-27 |

**Descartado a propósito:**
- un marketplace, con datos públicos o compartidos entre tenants (malinterpretado el 27 de septiembre y retirado el mismo día). Si un producto necesita compartir datos entre tenants, lleva su propio ADR;
- una base por tenant (costo operativo);
- subdominios por tenant;
- nueve roles de base de datos y clases `*Transaction` por caso de uso (el intento del 26 de septiembre);
- un IdP externo para los operadores. Los operadores tienen TOTP propio desde la Etapa 8.
