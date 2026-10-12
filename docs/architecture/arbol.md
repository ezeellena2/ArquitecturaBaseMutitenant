# Mapa de carpetas

El árbol ubica responsabilidades. Solo enumera archivos que sirven como modelo para copiar; agregar un archivo a una carpeta existente no requiere actualizarlo. Las carpetas de historia, contratos y diseño se muestran sin desglosar sus contenidos.

## Raíz

```text
├── .artifacts/
│   ├── comments-validation/
│   ├── etapa-3c/
│   ├── etapa-5/
│   ├── etapa-5-r04/
│   ├── etapa-5-role-check/
│   ├── publish-api/
│   │   └── en/
│   └── tanda-4f/
├── .github/
│   └── workflows/
├── .playwright-mcp/
├── docs/
│   ├── architecture/
│   ├── ayuda/
│   │   ├── assistant/
│   │   ├── business/
│   │   ├── identity/
│   │   ├── personal/
│   │   ├── platform/
│   │   └── whatsapp/
│   ├── contracts/
│   ├── decisions/
│   ├── features/  reglas funcionales por área
│   ├── guides/  recetas: agregar un área, permiso nuevo, migración, prefijo de backend, arneses
│   ├── operations/
│   ├── plans/
│   ├── reviews/  revisión activa de la 8b hasta el aviso del coordinador
│   ├── rules/  fichas del arnés: README + una ficha por tema (guardado, fechas, números…); índice en rules/README.md
│   └── specs/
├── scripts/
│   ├── arneses/  arneses ejecutables: detectan (0 tokens), arreglan lo mecánico y llaman a un modelo barato (ADR 0037)
│   │   ├── _plantilla/  se copia para crear un arnés nuevo; el CLI ignora las carpetas con _
│   │   │   └── detectores/
│   │   ├── comun/  piezas compartidas por todos los arneses
│   │   │   ├── fakes/  ejecutables falsos de claude y codex para los tests
│   │   │   └── motores/  misma interfaz para los dos motores
│   │   └── realinear-docs/  el primer arnés: alinea docs y punteros con el código
│   │       ├── detectores/  solo leen; nunca escriben ni llaman al modelo
│   │       └── fixtures/  mini repos falsos (back y front) con cada tipo de desvío
│   ├── datos-de-referencia/  generación desde ISO 4217, ISO 3166, IANA y CLDR
│   │   └── sources/  snapshots versionados; solo --refresh los actualiza
│   └── secretos/
├── src/  ver abajo
│   ├── ArquitecturaBaseMultitenant.Api/
│   │   ├── .artifacts/
│   │   │   └── etapa-5/
│   │   ├── .emails/
│   │   ├── Authentication/
│   │   ├── Authorization/
│   │   ├── Contracts/  entrada HTTP: records sealed, props nullable, ToString() sin PII
│   │   │   ├── Account/
│   │   │   ├── Auth/
│   │   │   ├── Common/
│   │   │   ├── Organization/
│   │   │   ├── Platform/
│   │   │   ├── PublicSite/
│   │   │   └── ReferenceData/
│   │   ├── Controllers/
│   │   │   ├── Account/
│   │   │   ├── Auth/
│   │   │   ├── Organization/  [Access(Business)]; rutas sin prefijo de acceso (el acceso lo declara [Access]); lo de
│   │   │   │   └── RolesController.cs  controller de referencia
│   │   │   ├── Personal/  punteros; módulos B2C añadidos por cada producto
│   │   │   ├── Platform/  [Access(Platform)]
│   │   │   ├── PublicSite/
│   │   │   └── ReferenceData/
│   │   ├── ErrorHandling/
│   │   ├── Features/
│   │   ├── Hosting/
│   │   ├── Idempotency/
│   │   ├── Json/
│   │   ├── Legal/
│   │   ├── Localization/
│   │   ├── Modules/
│   │   │   ├── Assistant/
│   │   │   │   ├── Contracts/
│   │   │   │   └── Controllers/
│   │   │   └── WhatsApp/
│   │   │       ├── Contracts/  request/query T01
│   │   │       ├── Controllers/
│   │   │       ├── Filters/
│   │   │       └── RateLimiting/
│   │   ├── OpenApi/
│   │   ├── Properties/
│   │   ├── RateLimiting/
│   │   ├── RequestContext/
│   │   └── Tenancy/
│   ├── ArquitecturaBaseMultitenant.AppHost/
│   │   └── Properties/
│   ├── ArquitecturaBaseMultitenant.Application/
│   │   ├── .artifacts/
│   │   │   └── etapa-5/
│   │   ├── Common/
│   │   │   ├── Exceptions/
│   │   │   ├── Formatting/  presentación unificada del lado del back (backend.md §18)
│   │   │   ├── Logging/
│   │   │   ├── Pagination/
│   │   │   ├── Text/
│   │   │   └── Validation/
│   │   ├── Configuration/
│   │   │   ├── Auth/
│   │   │   ├── Invitations/
│   │   │   └── Messaging/
│   │   ├── Interfaces/
│   │   │   ├── Integrations/
│   │   │   │   ├── Caching/
│   │   │   │   ├── Dns/
│   │   │   │   ├── Engagement/
│   │   │   │   ├── Execution/
│   │   │   │   ├── Features/
│   │   │   │   ├── Files/
│   │   │   │   ├── Help/
│   │   │   │   ├── Idempotency/
│   │   │   │   ├── Identity/
│   │   │   │   ├── Legal/
│   │   │   │   ├── Localization/
│   │   │   │   ├── Messaging/
│   │   │   │   ├── Phones/
│   │   │   │   ├── Request/
│   │   │   │   ├── Security/
│   │   │   │   └── Time/
│   │   │   ├── Persistence/  implementados en Infrastructure/Persistence
│   │   │   ├── ReferenceData/  puertos de Application; Domain no los referencia
│   │   │   └── Services/  lo que inyectan los controllers
│   │   ├── Models/  *Request (entrada), *Response (salida), ReadModels/*Row (proyección)
│   │   │   ├── Auditing/
│   │   │   ├── Auth/
│   │   │   ├── Common/
│   │   │   ├── Companies/
│   │   │   ├── Engagement/
│   │   │   ├── Execution/
│   │   │   ├── Features/
│   │   │   ├── Files/
│   │   │   ├── Help/
│   │   │   ├── Home/
│   │   │   ├── Idempotency/
│   │   │   ├── Identity/
│   │   │   ├── Invitations/
│   │   │   ├── Legal/
│   │   │   ├── Messaging/
│   │   │   ├── Notifications/
│   │   │   ├── OrganizationUsers/
│   │   │   ├── Platform/
│   │   │   ├── Profile/
│   │   │   ├── PublicSite/
│   │   │   ├── ReferenceData/
│   │   │   ├── Reports/
│   │   │   ├── Request/
│   │   │   ├── Roles/
│   │   │   ├── Settings/
│   │   │   ├── Tenancy/
│   │   │   ├── Time/
│   │   │   └── WhatsApp/
│   │   ├── Modules/  módulos quitables: el núcleo no los referencia (test)
│   │   │   ├── Assistant/
│   │   │   │   ├── Configuration/
│   │   │   │   ├── Interfaces/
│   │   │   │   │   ├── Integrations/
│   │   │   │   │   ├── Persistence/
│   │   │   │   │   └── Services/
│   │   │   │   ├── Models/
│   │   │   │   ├── Resources/
│   │   │   │   ├── Services/
│   │   │   │   ├── Tools/
│   │   │   │   │   ├── Business/
│   │   │   │   │   ├── Personal/
│   │   │   │   │   └── Platform/
│   │   │   │   └── Validation/
│   │   │   └── WhatsApp/  AddWhatsAppModule() registra todo lo de esta carpeta
│   │   │       ├── Interfaces/
│   │   │       │   ├── Integrations/
│   │   │       │   ├── Persistence/
│   │   │       │   └── Services/
│   │   │       ├── Models/
│   │   │       ├── Resources/
│   │   │       └── Services/
│   │   ├── Resources/  es (neutral) + en; claves en paridad
│   │   ├── Services/  servicios internal sealed partial; helpers sin IUnitOfWork
│   │   │   ├── Auditing/
│   │   │   ├── Auth/
│   │   │   ├── Companies/
│   │   │   ├── Home/
│   │   │   ├── Identity/
│   │   │   ├── Invitations/
│   │   │   ├── Legal/
│   │   │   ├── Messaging/
│   │   │   ├── OrganizationSettings/
│   │   │   ├── OrganizationUsers/
│   │   │   ├── Personal/
│   │   │   ├── Platform/
│   │   │   ├── Profile/
│   │   │   ├── PublicSite/
│   │   │   ├── ReferenceData/
│   │   │   └── Roles/  ← ÁREA DE REFERENCIA
│   │   │       └── RoleService.cs  servicio de referencia
│   │   └── Validation/  un *RequestValidator (internal sealed) por request que lo necesite
│   │       ├── Auditing/
│   │       ├── Auth/
│   │       ├── Companies/
│   │       ├── Identity/
│   │       ├── Invitations/
│   │       ├── Legal/
│   │       ├── OrganizationSettings/
│   │       ├── OrganizationUsers/
│   │       ├── Platform/
│   │       ├── Profile/
│   │       ├── PublicSite/
│   │       └── Roles/
│   ├── ArquitecturaBaseMultitenant.Domain/
│   │   ├── .artifacts/
│   │   │   └── etapa-5/
│   │   ├── Assistant/
│   │   ├── Auditing/
│   │   ├── Authentication/
│   │   ├── Authorization/
│   │   │   └── Role.cs  agregado de referencia
│   │   ├── Common/
│   │   ├── Companies/
│   │   ├── Features/
│   │   ├── Invitations/
│   │   ├── Legal/
│   │   ├── Messaging/
│   │   ├── Platform/
│   │   ├── PublicSite/
│   │   ├── ReferenceData/  entidades globales de platform, sin RLS
│   │   ├── Results/
│   │   ├── Settings/
│   │   ├── Tenancy/
│   │   ├── Users/  la identidad global (la entidad EF está en Infrastructure)
│   │   ├── ValueObjects/
│   │   │   └── TaxIdValidators/
│   │   └── WhatsApp/
│   ├── ArquitecturaBaseMultitenant.Infrastructure/
│   │   ├── .artifacts/
│   │   │   └── etapa-5/
│   │   ├── BackgroundJobs/
│   │   ├── Caching/
│   │   ├── Features/
│   │   ├── Help/
│   │   ├── Idempotency/
│   │   ├── Identity/
│   │   │   ├── Deletion/
│   │   │   └── OpenIddict/
│   │   ├── Messaging/
│   │   │   └── Email/
│   │   │       └── Templates/
│   │   ├── Modules/
│   │   │   ├── Assistant/
│   │   │   │   ├── Persistence/
│   │   │   │   │   ├── Configurations/
│   │   │   │   │   ├── Readers/
│   │   │   │   │   └── Repositories/
│   │   │   │   ├── ReferenceData/
│   │   │   │   └── Retention/
│   │   │   └── WhatsApp/  módulo opcional: se enciende con WhatsApp:PhoneNumberId
│   │   │       ├── Disabled/
│   │   │       ├── Inbound/
│   │   │       ├── Persistence/
│   │   │       ├── Retention/
│   │   │       └── Webhook/
│   │   ├── Persistence/
│   │   │   ├── AccessIndex/
│   │   │   ├── Configurations/  una IEntityTypeConfiguration<T> por entidad
│   │   │   │   ├── Identity/
│   │   │   │   ├── Platform/
│   │   │   │   │   └── ReferenceData/  una IEntityTypeConfiguration por tabla global, sin RLS
│   │   │   │   ├── PublicSite/
│   │   │   │   └── Tenant/  datos privados (RLS)
│   │   │   ├── Conventions/
│   │   │   ├── Extensions/
│   │   │   ├── Interceptors/
│   │   │   ├── Migrations/  una sola carpeta; RLS y grants dentro de cada migración
│   │   │   ├── Platform/
│   │   │   │   ├── Configurations/
│   │   │   │   └── Migrations/
│   │   │   ├── Readers/  AsNoTracking + proyección a *Row/*Response
│   │   │   ├── Repositories/  internal sealed; escrituras de agregados
│   │   │   │   └── RoleRepository.cs  repositorio de referencia
│   │   │   ├── Rls/
│   │   │   ├── Seed/
│   │   │   │   └── ReferenceData/  cinco JSON de salida versionados; no se editan a mano
│   │   │   ├── Tenant/
│   │   │   │   ├── Configurations/
│   │   │   │   ├── Interceptors/
│   │   │   │   └── Migrations/
│   │   │   └── WhatsApp/
│   │   ├── Phones/
│   │   ├── ReferenceData/
│   │   ├── RequestContext/
│   │   ├── Security/
│   │   ├── Time/
│   │   └── WhatsApp/
│   └── ArquitecturaBaseMultitenant.ServiceDefaults/
│       └── .artifacts/
│           └── etapa-5/
├── tests/  ver abajo
│   ├── ArquitecturaBaseMultitenant.Api.IntegrationTests/
│   │   ├── Api/
│   │   ├── Auth/
│   │   ├── Contracts/
│   │   ├── ErrorHandling/
│   │   ├── Features/
│   │   ├── Hosting/
│   │   ├── Identity/
│   │   ├── Json/
│   │   ├── Legal/
│   │   ├── Localization/
│   │   ├── Messaging/
│   │   ├── Modules/
│   │   │   ├── Assistant/  tests de integración del módulo
│   │   │   ├── TechnicalContribution/
│   │   │   │   └── Persistence/
│   │   │   └── WhatsApp/  tests de integración del módulo
│   │   │       └── TestFeatures/
│   │   ├── Organization/
│   │   ├── Persistence/
│   │   ├── Platform/
│   │   ├── PublicSite/
│   │   ├── ReferenceData/
│   │   ├── Security/
│   │   ├── Support/
│   │   ├── Tenancy/  los tests de multitenancy.md §13
│   │   ├── TestFeatures/  solo para tests, nunca en src/
│   │   │   ├── Access/  probe de ruta exclusiva Business, sólo para tests
│   │   │   ├── AssistantTurns/
│   │   │   ├── Auditing/
│   │   │   ├── Features/
│   │   │   ├── Idempotency/
│   │   │   ├── Isolation/
│   │   │   ├── Recorrido/
│   │   │   └── WhatsAppBrowser/
│   │   └── Time/
│   ├── ArquitecturaBaseMultitenant.Application.UnitTests/
│   │   ├── Common/
│   │   ├── Formatting/  Versiones para URLs y validadores HTTP
│   │   ├── Identity/
│   │   ├── Messaging/
│   │   ├── Modules/
│   │   │   ├── Assistant/
│   │   │   └── WhatsApp/
│   │   ├── Persistence/
│   │   ├── ReferenceData/
│   │   ├── Resources/
│   │   ├── Services/
│   │   │   ├── Auth/
│   │   │   ├── Companies/
│   │   │   ├── Home/
│   │   │   ├── Identity/
│   │   │   ├── Invitations/
│   │   │   ├── OrganizationUsers/
│   │   │   ├── Platform/
│   │   │   ├── Profile/
│   │   │   ├── PublicSite/
│   │   │   └── Roles/
│   │   └── TestDoubles/  cada fake nace con la interfaz que implementa
│   ├── ArquitecturaBaseMultitenant.ArchitectureTests/
│   │   ├── .artifacts/
│   │   │   └── etapa-5/
│   │   ├── Modules/
│   │   │   ├── Assistant/
│   │   │   └── WhatsApp/
│   │   └── Support/
│   └── ArquitecturaBaseMultitenant.Domain.UnitTests/
│       ├── Assistant/
│       ├── Auditing/
│       ├── Authentication/
│       ├── Authorization/
│       ├── Common/
│       ├── Features/
│       ├── Invitations/
│       ├── Legal/
│       ├── Messaging/
│       ├── Platform/
│       ├── PublicSite/
│       ├── ReferenceData/
│       ├── Results/
│       ├── Settings/
│       ├── Tenancy/
│       ├── Users/
│       ├── ValueObjects/
│       └── WhatsApp/
└── tools/  solo el soporte del E2E real, nunca para operar (ADR 0017, enmienda)
    └── ArquitecturaBaseMultitenant.RealE2ESetup/  prepara la cuenta, el espacio personal y la empresa del E2E real en su base aislada
```

## Mapa de carpetas → punteros

Decisión del usuario (2026-10-11): cada raíz de capa, área, módulo, proyecto y shared/ lleva AGENTS.md (hasta tres líneas: responsabilidad, ficha, modelo) y CLAUDE.md (`@AGENTS.md`). Las subcarpetas api/, pages/, components/ y tabs/ de una feature del front heredan su puntero.

| Carpeta | Qué va | Fichas | Copiá de | Puntero |
|---|---|---|---|---|
| `scripts/arneses/` | arneses ejecutables: detectan, arreglan lo mecánico y llaman a un modelo barato. | [docs/rules/tests.md](../rules/tests.md) | `scripts/datos-de-referencia/generar.test.mjs` | [AGENTS.md](../../scripts/arneses/AGENTS.md) |
| `scripts/datos-de-referencia/` | fuentes editables, snapshots/lock fijados y generador de los cinco JSON. | [docs/rules/datos-de-referencia.md](../rules/datos-de-referencia.md) | `scripts/datos-de-referencia/generar.mjs` | [AGENTS.md](../../scripts/datos-de-referencia/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Api/` | Piezas de su capa y área. | [docs/rules/capas-y-flujo.md](../rules/capas-y-flujo.md) | `src/ArquitecturaBaseMultitenant.Api/Authentication/OpenIdPrincipalFactory.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Api/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Api/Authorization/` | Piezas de su capa y área. | [docs/features/roles.md](../features/roles.md) | `src/ArquitecturaBaseMultitenant.Api/Authorization/HasPermissionAttribute.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Api/Authorization/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Api/Contracts/` | *HttpRequest / *Query, props nullable, ToString() sin datos personales. | [docs/rules/api-http.md](../rules/api-http.md) | `src/ArquitecturaBaseMultitenant.Api/Contracts/Organization/RolesHttpRequests.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Api/Contracts/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Api/Contracts/Account/` | Piezas de su capa y área. | [docs/features/identidad.md](../features/identidad.md) | `src/ArquitecturaBaseMultitenant.Api/Contracts/Account/AcceptLegalHttpRequest.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Api/Contracts/Account/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Api/Contracts/Auth/` | Piezas de su capa y área. | [docs/features/identidad.md](../features/identidad.md) | `src/ArquitecturaBaseMultitenant.Api/Contracts/Auth/AcceptInvitationHttpRequest.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Api/Contracts/Auth/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Api/Contracts/Common/` | Piezas de su capa y área. | [docs/rules/capas-y-flujo.md](../rules/capas-y-flujo.md) | `src/ArquitecturaBaseMultitenant.Api/Contracts/Common/PhoneInputHttpRequest.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Api/Contracts/Common/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Api/Contracts/Organization/` | Piezas de su capa y área. | [docs/features/organizaciones.md](../features/organizaciones.md) | `src/ArquitecturaBaseMultitenant.Api/Contracts/Organization/CompaniesHttpRequests.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Api/Contracts/Organization/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Api/Contracts/Platform/` | Piezas de su capa y área. | [docs/features/plataforma.md](../features/plataforma.md) | `src/ArquitecturaBaseMultitenant.Api/Contracts/Platform/PlatformAccountHttpRequests.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Api/Contracts/Platform/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Api/Contracts/PublicSite/` | Piezas de su capa y área. | [docs/features/sitio-publico.md](../features/sitio-publico.md) | `src/ArquitecturaBaseMultitenant.Api/Contracts/PublicSite/DirectoryQuery.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Api/Contracts/PublicSite/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Api/Contracts/ReferenceData/` | contrato de salida de los cinco catálogos para OpenAPI. | [docs/rules/datos-de-referencia.md](../rules/datos-de-referencia.md) | `src/ArquitecturaBaseMultitenant.Api/Contracts/ReferenceData/ReferenceDataHttpResponse.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Api/Contracts/ReferenceData/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Api/Controllers/` | controllers finos: contrato → servicio → ToActionResult. | [docs/rules/api-http.md](../rules/api-http.md) | `src/ArquitecturaBaseMultitenant.Api/Controllers/Organization/RolesController.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Api/Controllers/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Api/Controllers/Account/` | Piezas de su capa y área. | [docs/features/identidad.md](../features/identidad.md) | `src/ArquitecturaBaseMultitenant.Api/Controllers/Account/AccountDeletionController.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Api/Controllers/Account/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Api/Controllers/Auth/` | Piezas de su capa y área. | [docs/features/identidad.md](../features/identidad.md) | `src/ArquitecturaBaseMultitenant.Api/Controllers/Auth/AccountDeletionCancelController.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Api/Controllers/Auth/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Api/Controllers/Organization/` | Piezas de su capa y área. | [docs/features/organizaciones.md](../features/organizaciones.md) | `src/ArquitecturaBaseMultitenant.Api/Controllers/Organization/CompaniesController.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Api/Controllers/Organization/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Api/Controllers/Personal/` | Piezas de su capa y área. | [docs/features/personal.md](../features/personal.md) | `src/ArquitecturaBaseMultitenant.Application/Services/Roles/RoleService.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Api/Controllers/Personal/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Api/Controllers/Platform/` | Piezas de su capa y área. | [docs/features/plataforma.md](../features/plataforma.md) | `src/ArquitecturaBaseMultitenant.Api/Controllers/Platform/PlatformAccountsController.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Api/Controllers/Platform/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Api/Controllers/PublicSite/` | Piezas de su capa y área. | [docs/features/sitio-publico.md](../features/sitio-publico.md) | `src/ArquitecturaBaseMultitenant.Api/Controllers/PublicSite/DirectoryController.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Api/Controllers/PublicSite/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Api/Controllers/ReferenceData/` | ruta anónima de catálogos globales, con ETag. | [docs/rules/datos-de-referencia.md](../rules/datos-de-referencia.md) | `src/ArquitecturaBaseMultitenant.Api/Controllers/ReferenceData/ReferenceDataController.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Api/Controllers/ReferenceData/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Api/Features/` | puerta de los módulos opcionales: FeatureGateMiddleware oculta con 404 un módulo apagado. | [docs/rules/modulos-habilitados.md](../rules/modulos-habilitados.md) | `src/ArquitecturaBaseMultitenant.Api/Features/FeatureGateMiddleware.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Api/Features/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Api/Idempotency/` | [Idempotent] y su filtro. | [docs/rules/idempotencia.md](../rules/idempotencia.md) | `src/ArquitecturaBaseMultitenant.Api/Idempotency/IdempotencyFilter.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Api/Idempotency/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Api/Json/` | conversores globales (UTC, Money, texto normalizado). | [docs/rules/textos-libres.md](../rules/textos-libres.md) | `src/ArquitecturaBaseMultitenant.Api/Json/NormalizedStringJsonConverter.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Api/Json/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Api/Legal/` | LegalAcceptanceMiddleware: 403 Legal. | [docs/rules/datos-personales.md](../rules/datos-personales.md) | `src/ArquitecturaBaseMultitenant.Api/Legal/LegalAcceptanceMiddleware.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Api/Legal/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Api/Modules/Assistant/` | Rutas del Asistente. | [Asistente](../features/asistente.md) | `src/ArquitecturaBaseMultitenant.Api/Modules/Assistant/Controllers/BusinessAssistantController.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Api/Modules/Assistant/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Api/Modules/WhatsApp/` | lo HTTP del módulo (su *ApiModule, controllers como el del webhook, convención. | [docs/features/whatsapp.md](../features/whatsapp.md) | `src/ArquitecturaBaseMultitenant.Api/Modules/WhatsApp/Contracts/WhatsAppWebhookHttpRequest.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Api/Modules/WhatsApp/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Api/RateLimiting/` | políticas de ingreso por IP (RateLimitPolicies), con 429 ProblemDetails y Retry-After. | [docs/rules/api-http.md](../rules/api-http.md) | `src/ArquitecturaBaseMultitenant.Api/RateLimiting/RateLimitPolicies.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Api/RateLimiting/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Api/Tenancy/` | acceso firmado y contexto público por host, sin mezclar organizaciones. | [docs/features/organizaciones.md](../features/organizaciones.md) | `src/ArquitecturaBaseMultitenant.Api/Tenancy/PublicSiteResolutionMiddleware.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Api/Tenancy/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.AppHost/` | Piezas de su capa y área. | [docs/rules/capas-y-flujo.md](../rules/capas-y-flujo.md) | `src/ArquitecturaBaseMultitenant.AppHost/AppHost.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.AppHost/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/` | Piezas de su capa y área. | [docs/rules/capas-y-flujo.md](../rules/capas-y-flujo.md) | `src/ArquitecturaBaseMultitenant.Application/Common/Exceptions/ConcurrencyConflictException.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Common/Exceptions/` | excepciones técnicas que Infrastructure le pasa a Application (concurrencia, índice único, clave. | [docs/rules/result-y-errores.md](../rules/result-y-errores.md) | `src/ArquitecturaBaseMultitenant.Application/Common/Exceptions/UniqueConstraintViolationException.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Common/Exceptions/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Common/Formatting/` | DisplayFormatter y perfiles de cultura. | [docs/rules/numeros-y-moneda.md](../rules/numeros-y-moneda.md) | `src/ArquitecturaBaseMultitenant.Application/Common/Formatting/CultureProfiles.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Common/Formatting/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Common/Logging/` | OperationLog y sus [LoggerMessage]: inicio, fin y código de error de cada. | [docs/rules/logs.md](../rules/logs.md) | `src/ArquitecturaBaseMultitenant.Application/Common/Logging/OperationLog.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Common/Logging/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Configuration/` | Piezas de su capa y área. | [docs/rules/capas-y-flujo.md](../rules/capas-y-flujo.md) | `src/ArquitecturaBaseMultitenant.Application/Configuration/Auth/BrowserSessionKeys.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Configuration/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Configuration/Auth/` | opciones de Application por área, validadas al arrancar y sin secretos. | [docs/features/identidad.md](../features/identidad.md) | `src/ArquitecturaBaseMultitenant.Application/Configuration/Auth/LoginCodeOptions.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Configuration/Auth/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Configuration/Invitations/` | opciones de Application por área, validadas al arrancar y sin secretos. | [docs/features/organizaciones.md](../features/organizaciones.md) | `src/ArquitecturaBaseMultitenant.Application/Configuration/Invitations/InvitationOptions.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Configuration/Invitations/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Configuration/Messaging/` | opciones de Application por área, validadas al arrancar y sin secretos. | [docs/rules/capas-y-flujo.md](../rules/capas-y-flujo.md) | `src/ArquitecturaBaseMultitenant.Application/Configuration/Messaging/OutboxOptions.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Configuration/Messaging/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Interfaces/Integrations/` | puertos a lo externo, por tema. | [docs/rules/capas-y-flujo.md](../rules/capas-y-flujo.md) | `src/ArquitecturaBaseMultitenant.Application/Interfaces/Integrations/Caching/IAccessStatusCache.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Interfaces/Integrations/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Interfaces/Integrations/Phones/` | puerto de formato de lectura E1. | [docs/rules/telefonos.md](../rules/telefonos.md) | `src/ArquitecturaBaseMultitenant.Application/Interfaces/Integrations/Phones/IPhoneNumberDisplayFormatter.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Interfaces/Integrations/Phones/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Interfaces/Persistence/` | I<X>Repository (escribe), I<X>Reader (lee), IUnitOfWork, ITenantScope. | [docs/rules/persistencia-ef.md](../rules/persistencia-ef.md) | `src/ArquitecturaBaseMultitenant.Application/Interfaces/Persistence/IRoleRepository.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Interfaces/Persistence/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Interfaces/ReferenceData/` | puertos de moneda, país, zona, cultura y tipo fiscal. | [docs/rules/datos-de-referencia.md](../rules/datos-de-referencia.md) | `src/ArquitecturaBaseMultitenant.Application/Interfaces/ReferenceData/ICurrencyCatalog.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Interfaces/ReferenceData/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Interfaces/Services/` | una interfaz por servicio. | [docs/rules/capas-y-flujo.md](../rules/capas-y-flujo.md) | `src/ArquitecturaBaseMultitenant.Application/Interfaces/Services/IRoleService.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Interfaces/Services/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Models/` | *Request, *Response, ReadModels/*Row. | [docs/rules/numeros-y-moneda.md](../rules/numeros-y-moneda.md) | `src/ArquitecturaBaseMultitenant.Application/Models/Auditing/OrganizationAuditModels.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Models/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Models/Auditing/` | Piezas de su capa y área. | [docs/rules/capas-y-flujo.md](../rules/capas-y-flujo.md) | `src/ArquitecturaBaseMultitenant.Application/Models/Auditing/OrganizationAuditModels.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Models/Auditing/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Models/Auth/` | Piezas de su capa y área. | [docs/features/identidad.md](../features/identidad.md) | `src/ArquitecturaBaseMultitenant.Application/Models/Auth/AccessSelectionRequest.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Models/Auth/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Models/Common/` | Piezas de su capa y área. | [docs/rules/capas-y-flujo.md](../rules/capas-y-flujo.md) | `src/ArquitecturaBaseMultitenant.Application/Models/Common/PhoneInputRequest.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Models/Common/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Models/Companies/` | Piezas de su capa y área. | [docs/features/organizaciones.md](../features/organizaciones.md) | `src/ArquitecturaBaseMultitenant.Application/Models/Companies/CompanyMemberModels.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Models/Companies/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Models/Engagement/` | Piezas de su capa y área. | [docs/rules/capas-y-flujo.md](../rules/capas-y-flujo.md) | `src/ArquitecturaBaseMultitenant.Application/Models/Engagement/ConsumerInteractionResponse.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Models/Engagement/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Models/Execution/` | Piezas de su capa y área. | [docs/rules/capas-y-flujo.md](../rules/capas-y-flujo.md) | `src/ArquitecturaBaseMultitenant.Application/Services/Roles/RoleService.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Models/Execution/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Models/Features/` | Piezas de su capa y área. | [docs/rules/capas-y-flujo.md](../rules/capas-y-flujo.md) | `src/ArquitecturaBaseMultitenant.Application/Models/Features/TenantFeatureRow.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Models/Features/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Models/Files/` | Piezas de su capa y área. | [docs/rules/capas-y-flujo.md](../rules/capas-y-flujo.md) | `src/ArquitecturaBaseMultitenant.Application/Models/Files/FileObject.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Models/Files/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Models/Help/` | Piezas de su capa y área. | [docs/rules/capas-y-flujo.md](../rules/capas-y-flujo.md) | `src/ArquitecturaBaseMultitenant.Application/Models/Help/HelpArticleModels.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Models/Help/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Models/Home/` | Piezas de su capa y área. | [docs/features/inicio.md](../features/inicio.md) | `src/ArquitecturaBaseMultitenant.Application/Models/Home/HomeModels.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Models/Home/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Models/Idempotency/` | Piezas de su capa y área. | [docs/rules/capas-y-flujo.md](../rules/capas-y-flujo.md) | `src/ArquitecturaBaseMultitenant.Application/Models/Idempotency/IdempotentReference.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Models/Idempotency/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Models/Identity/` | Piezas de su capa y área. | [docs/features/identidad.md](../features/identidad.md) | `src/ArquitecturaBaseMultitenant.Application/Models/Identity/AccountLoginMethodsResponse.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Models/Identity/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Models/Invitations/` | Piezas de su capa y área. | [docs/features/organizaciones.md](../features/organizaciones.md) | `src/ArquitecturaBaseMultitenant.Application/Models/Invitations/AcceptInvitationRequest.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Models/Invitations/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Models/Legal/` | Piezas de su capa y área. | [docs/rules/capas-y-flujo.md](../rules/capas-y-flujo.md) | `src/ArquitecturaBaseMultitenant.Application/Models/Legal/AcceptLegalRequest.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Models/Legal/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Models/Messaging/` | Piezas de su capa y área. | [docs/rules/capas-y-flujo.md](../rules/capas-y-flujo.md) | `src/ArquitecturaBaseMultitenant.Application/Models/Messaging/EmailMessage.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Models/Messaging/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Models/Notifications/` | Piezas de su capa y área. | [docs/rules/capas-y-flujo.md](../rules/capas-y-flujo.md) | `src/ArquitecturaBaseMultitenant.Application/Models/Notifications/AccountNotice.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Models/Notifications/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Models/OrganizationUsers/` | Piezas de su capa y área. | [docs/features/organizaciones.md](../features/organizaciones.md) | `src/ArquitecturaBaseMultitenant.Application/Models/OrganizationUsers/OrganizationUserModels.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Models/OrganizationUsers/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Models/Platform/` | Piezas de su capa y área. | [docs/features/plataforma.md](../features/plataforma.md) | `src/ArquitecturaBaseMultitenant.Application/Models/Platform/PlatformAccountModels.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Models/Platform/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Models/Profile/` | Piezas de su capa y área. | [docs/rules/capas-y-flujo.md](../rules/capas-y-flujo.md) | `src/ArquitecturaBaseMultitenant.Application/Models/Profile/EffectivePermissions.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Models/Profile/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Models/PublicSite/` | Piezas de su capa y área. | [docs/features/sitio-publico.md](../features/sitio-publico.md) | `src/ArquitecturaBaseMultitenant.Application/Models/PublicSite/DirectoryEntryRow.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Models/PublicSite/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Models/ReferenceData/` | respuesta de los cinco catálogos traducidos. | [docs/rules/datos-de-referencia.md](../rules/datos-de-referencia.md) | `src/ArquitecturaBaseMultitenant.Application/Models/ReferenceData/ReferenceDataResponse.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Models/ReferenceData/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Models/Reports/` | Piezas de su capa y área. | [docs/rules/capas-y-flujo.md](../rules/capas-y-flujo.md) | `src/ArquitecturaBaseMultitenant.Application/Services/Roles/RoleService.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Models/Reports/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Models/Request/` | Piezas de su capa y área. | [docs/rules/capas-y-flujo.md](../rules/capas-y-flujo.md) | `src/ArquitecturaBaseMultitenant.Application/Models/Request/ExecutionActor.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Models/Request/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Models/Roles/` | Piezas de su capa y área. | [docs/features/roles.md](../features/roles.md) | `src/ArquitecturaBaseMultitenant.Application/Models/Roles/CreateRoleRequest.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Models/Roles/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Models/Settings/` | Piezas de su capa y área. | [docs/rules/capas-y-flujo.md](../rules/capas-y-flujo.md) | `src/ArquitecturaBaseMultitenant.Application/Models/Settings/OrganizationSettingsModels.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Models/Settings/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Models/Tenancy/` | Piezas de su capa y área. | [docs/features/organizaciones.md](../features/organizaciones.md) | `src/ArquitecturaBaseMultitenant.Application/Models/Tenancy/ActiveTenantAdminEmail.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Models/Tenancy/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Models/Time/` | Piezas de su capa y área. | [docs/rules/capas-y-flujo.md](../rules/capas-y-flujo.md) | `src/ArquitecturaBaseMultitenant.Application/Models/Time/DayRangeUtc.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Models/Time/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Models/WhatsApp/` | Piezas de su capa y área. | [docs/features/whatsapp.md](../features/whatsapp.md) | `src/ArquitecturaBaseMultitenant.Application/Services/Roles/RoleService.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Models/WhatsApp/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Modules/Assistant/` | Casos de uso del Asistente. | [Asistente](../features/asistente.md) | `src/ArquitecturaBaseMultitenant.Application/Modules/Assistant/Services/AssistantConversationService.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Modules/Assistant/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Modules/WhatsApp/` | un módulo quitable. | [docs/features/whatsapp.md](../features/whatsapp.md) | `src/ArquitecturaBaseMultitenant.Application/Modules/WhatsApp/Interfaces/Integrations/IWhatsAppAvailability.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Modules/WhatsApp/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Resources/` | . | [docs/rules/textos-y-traducciones.md](../rules/textos-y-traducciones.md) | `src/ArquitecturaBaseMultitenant.Application/Resources/Errors.resx` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Resources/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Services/` | servicio + helpers con un sufijo de la lista cerrada de capas-y-flujo. | [docs/rules/guardado.md](../rules/guardado.md) | `src/ArquitecturaBaseMultitenant.Application/Services/Roles/RoleService.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Services/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Services/Auditing/` | Piezas de su capa y área. | [docs/rules/capas-y-flujo.md](../rules/capas-y-flujo.md) | `src/ArquitecturaBaseMultitenant.Application/Services/Roles/RoleService.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Services/Auditing/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Services/Auth/` | Piezas de su capa y área. | [docs/features/identidad.md](../features/identidad.md) | `src/ArquitecturaBaseMultitenant.Application/Services/Auth/AccessSwitchPolicy.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Services/Auth/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Services/Companies/` | Piezas de su capa y área. | [docs/features/organizaciones.md](../features/organizaciones.md) | `src/ArquitecturaBaseMultitenant.Application/Services/Companies/CompaniesRegistration.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Services/Companies/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Services/Home/` | Piezas de su capa y área. | [docs/features/inicio.md](../features/inicio.md) | `src/ArquitecturaBaseMultitenant.Application/Services/Home/HomeRegistration.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Services/Home/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Services/Identity/` | Piezas de su capa y área. | [docs/features/identidad.md](../features/identidad.md) | `src/ArquitecturaBaseMultitenant.Application/Services/Identity/AccountGoogleService.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Services/Identity/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Services/Invitations/` | Piezas de su capa y área. | [docs/features/organizaciones.md](../features/organizaciones.md) | `src/ArquitecturaBaseMultitenant.Application/Services/Invitations/InvitationAcceptanceGuard.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Services/Invitations/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Services/Legal/` | Piezas de su capa y área. | [docs/rules/capas-y-flujo.md](../rules/capas-y-flujo.md) | `src/ArquitecturaBaseMultitenant.Application/Services/Legal/AccountDeletionCancelIssuer.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Services/Legal/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Services/Messaging/` | Piezas de su capa y área. | [docs/rules/capas-y-flujo.md](../rules/capas-y-flujo.md) | `src/ArquitecturaBaseMultitenant.Application/Services/Messaging/MessagingRegistration.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Services/Messaging/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Services/OrganizationSettings/` | Piezas de su capa y área. | [docs/features/organizaciones.md](../features/organizaciones.md) | `src/ArquitecturaBaseMultitenant.Application/Services/OrganizationSettings/LoginMethodDomainLinker.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Services/OrganizationSettings/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Services/OrganizationUsers/` | Piezas de su capa y área. | [docs/features/organizaciones.md](../features/organizaciones.md) | `src/ArquitecturaBaseMultitenant.Application/Services/OrganizationUsers/CompanyInvitationPolicy.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Services/OrganizationUsers/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Services/Personal/` | Piezas de su capa y área. | [docs/features/personal.md](../features/personal.md) | `src/ArquitecturaBaseMultitenant.Application/Services/Roles/RoleService.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Services/Personal/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Services/Platform/` | Piezas de su capa y área. | [docs/features/plataforma.md](../features/plataforma.md) | `src/ArquitecturaBaseMultitenant.Application/Services/Platform/PlatformAccountDeletionService.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Services/Platform/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Services/Profile/` | Piezas de su capa y área. | [docs/rules/capas-y-flujo.md](../rules/capas-y-flujo.md) | `src/ArquitecturaBaseMultitenant.Application/Services/Profile/ProfileEffectivePermissionsBuilder.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Services/Profile/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Services/PublicSite/` | Piezas de su capa y área. | [docs/features/sitio-publico.md](../features/sitio-publico.md) | `src/ArquitecturaBaseMultitenant.Application/Services/PublicSite/DirectoryService.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Services/PublicSite/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Services/ReferenceData/` | servicio que combina los puertos y entrega los catálogos a la Api. | [docs/rules/datos-de-referencia.md](../rules/datos-de-referencia.md) | `src/ArquitecturaBaseMultitenant.Application/Services/ReferenceData/ReferenceDataService.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Services/ReferenceData/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Services/Roles/` | Piezas de su capa y área. | [docs/features/roles.md](../features/roles.md) | `src/ArquitecturaBaseMultitenant.Application/Services/Roles/PermissionService.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Services/Roles/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Validation/` | un validador por request, con ValidationRules. | [docs/rules/validacion.md](../rules/validacion.md) | `src/ArquitecturaBaseMultitenant.Application/Validation/Roles/CreateRoleRequestValidator.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Validation/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Validation/Auditing/` | Piezas de su capa y área. | [docs/rules/capas-y-flujo.md](../rules/capas-y-flujo.md) | `src/ArquitecturaBaseMultitenant.Application/Validation/Auditing/ListOrganizationAuditRequestValidator.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Validation/Auditing/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Validation/Auth/` | Piezas de su capa y área. | [docs/features/identidad.md](../features/identidad.md) | `src/ArquitecturaBaseMultitenant.Application/Validation/Auth/AccountRecoveryRequestValidators.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Validation/Auth/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Validation/Companies/` | Piezas de su capa y área. | [docs/features/organizaciones.md](../features/organizaciones.md) | `src/ArquitecturaBaseMultitenant.Application/Validation/Companies/CompanyMemberValidators.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Validation/Companies/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Validation/Identity/` | Piezas de su capa y área. | [docs/features/identidad.md](../features/identidad.md) | `src/ArquitecturaBaseMultitenant.Application/Validation/Identity/AddLoginEmailRequestValidator.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Validation/Identity/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Validation/Invitations/` | Piezas de su capa y área. | [docs/features/organizaciones.md](../features/organizaciones.md) | `src/ArquitecturaBaseMultitenant.Application/Validation/Invitations/AcceptInvitationRequestValidator.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Validation/Invitations/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Validation/Legal/` | Piezas de su capa y área. | [docs/rules/capas-y-flujo.md](../rules/capas-y-flujo.md) | `src/ArquitecturaBaseMultitenant.Application/Validation/Legal/AcceptLegalRequestValidator.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Validation/Legal/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Validation/OrganizationSettings/` | Piezas de su capa y área. | [docs/features/organizaciones.md](../features/organizaciones.md) | `src/ArquitecturaBaseMultitenant.Application/Validation/OrganizationSettings/RegisterOrganizationDomainRequestValidator.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Validation/OrganizationSettings/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Validation/OrganizationUsers/` | Piezas de su capa y área. | [docs/features/organizaciones.md](../features/organizaciones.md) | `src/ArquitecturaBaseMultitenant.Application/Validation/OrganizationUsers/OrganizationUserValidators.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Validation/OrganizationUsers/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Validation/Platform/` | Piezas de su capa y área. | [docs/features/plataforma.md](../features/plataforma.md) | `src/ArquitecturaBaseMultitenant.Application/Validation/Platform/ListPlatformAuditRequestValidator.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Validation/Platform/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Validation/Profile/` | Piezas de su capa y área. | [docs/rules/capas-y-flujo.md](../rules/capas-y-flujo.md) | `src/ArquitecturaBaseMultitenant.Application/Validation/Profile/UpdateMeRequestValidator.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Validation/Profile/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Validation/PublicSite/` | Piezas de su capa y área. | [docs/features/sitio-publico.md](../features/sitio-publico.md) | `src/ArquitecturaBaseMultitenant.Application/Validation/PublicSite/ListDirectoryRequestValidator.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Validation/PublicSite/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Validation/Roles/` | Piezas de su capa y área. | [docs/features/roles.md](../features/roles.md) | `src/ArquitecturaBaseMultitenant.Application/Validation/Roles/CreateRoleRequestValidator.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Validation/Roles/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Domain/` | entidades, value objects, <X>Errors, catálogos. | [docs/rules/capas-y-flujo.md](../rules/capas-y-flujo.md) | `src/ArquitecturaBaseMultitenant.Domain/Assistant/AssistantAttachment.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Domain/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Domain/Assistant/` | entidades y <X>Errors del módulo (Domain/WhatsApp/ nace en la E8). | [docs/features/asistente.md](../features/asistente.md) | `src/ArquitecturaBaseMultitenant.Domain/Assistant/AssistantAttachment.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Domain/Assistant/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Domain/Auditing/` | entidades y <X>Errors del módulo (Domain/WhatsApp/ nace en la E8). | [docs/rules/modulos.md](../rules/modulos.md) | `src/ArquitecturaBaseMultitenant.Domain/Auditing/AuditAction.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Domain/Auditing/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Domain/Authentication/` | entidades y <X>Errors del módulo (Domain/WhatsApp/ nace en la E8). | [docs/rules/modulos.md](../rules/modulos.md) | `src/ArquitecturaBaseMultitenant.Domain/Authentication/AccountErrors.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Domain/Authentication/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Domain/Authorization/` | entidades y <X>Errors del módulo (Domain/WhatsApp/ nace en la E8). | [docs/features/roles.md](../features/roles.md) | `src/ArquitecturaBaseMultitenant.Domain/Authorization/Permissions.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Domain/Authorization/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Domain/Common/` | Entity, ValueObject. | [docs/rules/multitenancy.md](../rules/multitenancy.md) | `src/ArquitecturaBaseMultitenant.Domain/Common/Entity.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Domain/Common/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Domain/Companies/` | entidades y <X>Errors del módulo (Domain/WhatsApp/ nace en la E8). | [docs/features/organizaciones.md](../features/organizaciones.md) | `src/ArquitecturaBaseMultitenant.Application/Services/Roles/RoleService.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Domain/Companies/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Domain/Features/` | catálogo de módulos. | [docs/rules/modulos-habilitados.md](../rules/modulos-habilitados.md) | `src/ArquitecturaBaseMultitenant.Domain/Features/Features.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Domain/Features/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Domain/Invitations/` | entidades y <X>Errors del módulo (Domain/WhatsApp/ nace en la E8). | [docs/features/organizaciones.md](../features/organizaciones.md) | `src/ArquitecturaBaseMultitenant.Domain/Invitations/Invitation.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Domain/Invitations/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Domain/Legal/` | entidades y <X>Errors del módulo (Domain/WhatsApp/ nace en la E8). | [docs/rules/modulos.md](../rules/modulos.md) | `src/ArquitecturaBaseMultitenant.Domain/Legal/AccountDeletionCancelTicket.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Domain/Legal/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Domain/Messaging/` | entidades y <X>Errors del módulo (Domain/WhatsApp/ nace en la E8). | [docs/rules/modulos.md](../rules/modulos.md) | `src/ArquitecturaBaseMultitenant.Domain/Messaging/OutboxChannel.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Domain/Messaging/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Domain/Platform/` | entidades y <X>Errors del módulo (Domain/WhatsApp/ nace en la E8). | [docs/features/plataforma.md](../features/plataforma.md) | `src/ArquitecturaBaseMultitenant.Domain/Platform/AccountRecoveryErrors.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Domain/Platform/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Domain/PublicSite/` | entidades y <X>Errors del módulo (Domain/WhatsApp/ nace en la E8). | [docs/features/sitio-publico.md](../features/sitio-publico.md) | `src/ArquitecturaBaseMultitenant.Domain/PublicSite/PublicPage.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Domain/PublicSite/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Domain/ReferenceData/` | entidades globales de los cinco catálogos y sus traducciones, sin dependencia de. | [docs/rules/datos-de-referencia.md](../rules/datos-de-referencia.md) | `src/ArquitecturaBaseMultitenant.Domain/ReferenceData/Currency.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Domain/ReferenceData/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Domain/Results/` | entidades y <X>Errors del módulo (Domain/WhatsApp/ nace en la E8). | [docs/rules/modulos.md](../rules/modulos.md) | `src/ArquitecturaBaseMultitenant.Domain/Results/ContractErrors.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Domain/Results/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Domain/Settings/` | entidades y <X>Errors del módulo (Domain/WhatsApp/ nace en la E8). | [docs/rules/modulos.md](../rules/modulos.md) | `src/ArquitecturaBaseMultitenant.Domain/Settings/BusinessSignupMode.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Domain/Settings/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Domain/Tenancy/` | entidades y <X>Errors del módulo (Domain/WhatsApp/ nace en la E8). | [docs/features/organizaciones.md](../features/organizaciones.md) | `src/ArquitecturaBaseMultitenant.Domain/Tenancy/AccessErrors.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Domain/Tenancy/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Domain/Users/` | entidades y <X>Errors del módulo (Domain/WhatsApp/ nace en la E8). | [docs/rules/modulos.md](../rules/modulos.md) | `src/ArquitecturaBaseMultitenant.Domain/Users/Access.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Domain/Users/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Domain/ValueObjects/` | Money, Email, PhoneNumber, TaxId: un dato con forma propia es un value. | [docs/rules/numeros-y-moneda.md](../rules/numeros-y-moneda.md) | `src/ArquitecturaBaseMultitenant.Domain/ValueObjects/Money.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Domain/ValueObjects/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Domain/ValueObjects/TaxIdValidators/` | Algoritmos de identificación fiscal puros. | [docs/rules/identificacion-fiscal.md](../rules/identificacion-fiscal.md) | `src/ArquitecturaBaseMultitenant.Domain/ValueObjects/TaxIdValidators/ArgentineCuitValidator.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Domain/ValueObjects/TaxIdValidators/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Domain/WhatsApp/` | entidades y <X>Errors del módulo (Domain/WhatsApp/ nace en la E8). | [docs/features/whatsapp.md](../features/whatsapp.md) | `src/ArquitecturaBaseMultitenant.Domain/WhatsApp/WhatsAppCodeErrors.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Domain/WhatsApp/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Infrastructure/` | Piezas de su capa y área. | [docs/rules/capas-y-flujo.md](../rules/capas-y-flujo.md) | `src/ArquitecturaBaseMultitenant.Infrastructure/BackgroundJobs/AccountDeletionWorker.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Infrastructure/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Infrastructure/BackgroundJobs/` | workers: un scope DI por organización y ITenantScope. | [docs/rules/multitenancy.md](../rules/multitenancy.md) | `src/ArquitecturaBaseMultitenant.Infrastructure/BackgroundJobs/TenantJobRunner.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Infrastructure/BackgroundJobs/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Infrastructure/Caching/` | HybridCache: claves de CacheKeys con prefijo de alcance. | [docs/rules/multitenancy.md](../rules/multitenancy.md) | `src/ArquitecturaBaseMultitenant.Infrastructure/Caching/HybridCacheExtensions.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Infrastructure/Caching/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Infrastructure/Features/` | filtro de módulos por tenant. | [docs/rules/modulos-habilitados.md](../rules/modulos-habilitados.md) | `src/ArquitecturaBaseMultitenant.Infrastructure/Features/TenantFeatureFilter.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Infrastructure/Features/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Infrastructure/Help/` | Piezas de su capa y área. | [docs/rules/capas-y-flujo.md](../rules/capas-y-flujo.md) | `src/ArquitecturaBaseMultitenant.Infrastructure/Help/RepositoryHelpArticleReader.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Infrastructure/Help/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Infrastructure/Idempotency/` | persistencia técnica de [Idempotent] en platform: reserva, respuesta guardada y limpieza. | [docs/rules/idempotencia.md](../rules/idempotencia.md) | `src/ArquitecturaBaseMultitenant.Infrastructure/Idempotency/IdempotencyStore.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Infrastructure/Idempotency/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Infrastructure/Identity/` | Piezas de su capa y área. | [docs/features/identidad.md](../features/identidad.md) | `src/ArquitecturaBaseMultitenant.Infrastructure/Identity/ApplicationUser.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Infrastructure/Identity/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Infrastructure/Identity/Deletion/` | participantes de la baja de una cuenta: trabajan en el alcance y. | [docs/rules/datos-personales.md](../rules/datos-personales.md) | `src/ArquitecturaBaseMultitenant.Infrastructure/Identity/Deletion/MembershipDeletionParticipant.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Infrastructure/Identity/Deletion/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Infrastructure/Messaging/` | outbox persistente y su dispatcher. | [docs/rules/guardado.md](../rules/guardado.md) | `src/ArquitecturaBaseMultitenant.Infrastructure/Messaging/Outbox.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Infrastructure/Messaging/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Infrastructure/Messaging/Email/Templates/` | plantillas HTML embebidas: estructura y marcadores {{Nombre}}. | [docs/rules/textos-y-traducciones.md](../rules/textos-y-traducciones.md) | `src/ArquitecturaBaseMultitenant.Infrastructure/Messaging/Email/Templates/LoginCode.html` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Infrastructure/Messaging/Email/Templates/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Infrastructure/Modules/` | Piezas de su capa y área. | [docs/rules/capas-y-flujo.md](../rules/capas-y-flujo.md) | `src/ArquitecturaBaseMultitenant.Infrastructure/Modules/Assistant/AnthropicChatClientFactory.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Infrastructure/Modules/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Infrastructure/Modules/Assistant/` | adaptadores del módulo. | [docs/features/asistente.md](../features/asistente.md) | `src/ArquitecturaBaseMultitenant.Infrastructure/Modules/Assistant/AnthropicChatClientFactory.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Infrastructure/Modules/Assistant/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Infrastructure/Modules/WhatsApp/` | adaptadores del módulo. | [docs/features/whatsapp.md](../features/whatsapp.md) | `src/ArquitecturaBaseMultitenant.Infrastructure/Modules/WhatsApp/Disabled/DisabledWhatsAppAvailability.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Infrastructure/Modules/WhatsApp/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/` | ApplicationDbContext, UnitOfWork, TenantContext y los límites de persistencia. | [docs/rules/persistencia-ef.md](../rules/persistencia-ef.md) | `src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/TenantContext.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/Configurations/Identity/` | una IEntityTypeConfiguration por entidad. | [docs/features/identidad.md](../features/identidad.md) | `src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/Configurations/Tenant/RoleConfiguration.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/Configurations/Identity/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/Configurations/Platform/` | una IEntityTypeConfiguration por entidad. | [docs/features/plataforma.md](../features/plataforma.md) | `src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/Configurations/Platform/AccountRecoveryNoticeConfiguration.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/Configurations/Platform/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/Configurations/Platform/ReferenceData/` | una configuración EF por tabla de referencia global, sin RLS. | [docs/rules/datos-de-referencia.md](../rules/datos-de-referencia.md) | `src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/Configurations/Platform/ReferenceData/CurrencyConfiguration.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/Configurations/Platform/ReferenceData/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/Configurations/PublicSite/` | una IEntityTypeConfiguration por entidad. | [docs/features/sitio-publico.md](../features/sitio-publico.md) | `src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/Configurations/PublicSite/PublicPageConfiguration.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/Configurations/PublicSite/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/Configurations/Tenant/` | una IEntityTypeConfiguration por entidad. | [docs/rules/persistencia-ef.md](../rules/persistencia-ef.md) | `src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/Configurations/Tenant/AuditEntryConfiguration.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/Configurations/Tenant/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/Migrations/` | se generan con el comando de la guía. | [docs/rules/persistencia-ef.md](../rules/persistencia-ef.md) | `docs/guides/migracion.md` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/Migrations/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/Readers/` | proyecciones AsNoTracking → *Row. | [docs/rules/datos-de-referencia.md](../rules/datos-de-referencia.md) | `src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/Readers/ReferenceDataReader.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/Readers/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/Repositories/` | EF para escribir un agregado. | [docs/rules/guardado.md](../rules/guardado.md) | `src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/Repositories/RoleRepository.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/Repositories/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/Seed/ReferenceData/` | cinco JSON generados de ISO/IANA/CLDR y fuentes editables del script. | [docs/rules/datos-de-referencia.md](../rules/datos-de-referencia.md) | `src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/Seed/ReferenceData/currencies.json` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/Seed/ReferenceData/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Infrastructure/Phones/` | adaptador libphonenumber de presentación E1. | [docs/rules/telefonos.md](../rules/telefonos.md) | `src/ArquitecturaBaseMultitenant.Infrastructure/Phones/LibPhoneNumberDisplayFormatter.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Infrastructure/Phones/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Infrastructure/ReferenceData/` | adaptador JSON de los cinco catálogos en E1. | [docs/rules/datos-de-referencia.md](../rules/datos-de-referencia.md) | `src/ArquitecturaBaseMultitenant.Infrastructure/ReferenceData/JsonReferenceDataCatalog.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Infrastructure/ReferenceData/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Infrastructure/RequestContext/` | Piezas de su capa y área. | [docs/rules/capas-y-flujo.md](../rules/capas-y-flujo.md) | `src/ArquitecturaBaseMultitenant.Infrastructure/RequestContext/ExecutionActorContext.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Infrastructure/RequestContext/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Infrastructure/Security/` | Piezas de su capa y área. | [docs/rules/capas-y-flujo.md](../rules/capas-y-flujo.md) | `src/ArquitecturaBaseMultitenant.Infrastructure/Security/InvitationTokenProtector.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Infrastructure/Security/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Infrastructure/Time/` | Piezas de su capa y área. | [docs/rules/capas-y-flujo.md](../rules/capas-y-flujo.md) | `src/ArquitecturaBaseMultitenant.Infrastructure/Time/TimeZoneService.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Infrastructure/Time/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Infrastructure/WhatsApp/` | Piezas de su capa y área. | [docs/features/whatsapp.md](../features/whatsapp.md) | `src/ArquitecturaBaseMultitenant.Application/Services/Roles/RoleService.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Infrastructure/WhatsApp/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.ServiceDefaults/` | Piezas de su capa y área. | [docs/rules/capas-y-flujo.md](../rules/capas-y-flujo.md) | `src/ArquitecturaBaseMultitenant.ServiceDefaults/Extensions.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.ServiceDefaults/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/` | Piezas de su capa y área. | [docs/rules/capas-y-flujo.md](../rules/capas-y-flujo.md) | `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Organization/RolesTests.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Modules/Assistant/` | Piezas de su capa y área. | [docs/features/asistente.md](../features/asistente.md) | `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Modules/Assistant/AssistantTestData.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Modules/Assistant/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Modules/TechnicalContribution/` | Piezas de su capa y área. | [docs/rules/capas-y-flujo.md](../rules/capas-y-flujo.md) | `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Modules/TechnicalContribution/Persistence/TechnicalContribution.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Modules/TechnicalContribution/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Modules/WhatsApp/` | Piezas de su capa y área. | [docs/features/whatsapp.md](../features/whatsapp.md) | `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Modules/WhatsApp/TestControllerApplicationPart.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Modules/WhatsApp/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Application.UnitTests/` | Piezas de su capa y área. | [docs/rules/capas-y-flujo.md](../rules/capas-y-flujo.md) | `tests/ArquitecturaBaseMultitenant.Application.UnitTests/ModuleServiceControlProbe.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Application.UnitTests/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Application.UnitTests/Modules/Assistant/` | Piezas de su capa y área. | [docs/features/asistente.md](../features/asistente.md) | `src/ArquitecturaBaseMultitenant.Application/Services/Roles/RoleService.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Application.UnitTests/Modules/Assistant/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Application.UnitTests/Modules/WhatsApp/` | Piezas de su capa y área. | [docs/features/whatsapp.md](../features/whatsapp.md) | `src/ArquitecturaBaseMultitenant.Application/Services/Roles/RoleService.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Application.UnitTests/Modules/WhatsApp/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.ArchitectureTests/` | Piezas de su capa y área. | [docs/rules/capas-y-flujo.md](../rules/capas-y-flujo.md) | `tests/ArquitecturaBaseMultitenant.ArchitectureTests/ApplicationNamespaceShape.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.ArchitectureTests/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.ArchitectureTests/Modules/Assistant/` | Piezas de su capa y área. | [docs/features/asistente.md](../features/asistente.md) | `src/ArquitecturaBaseMultitenant.Application/Services/Roles/RoleService.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.ArchitectureTests/Modules/Assistant/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.ArchitectureTests/Modules/WhatsApp/` | Piezas de su capa y área. | [docs/features/whatsapp.md](../features/whatsapp.md) | `src/ArquitecturaBaseMultitenant.Application/Services/Roles/RoleService.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.ArchitectureTests/Modules/WhatsApp/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Domain.UnitTests/` | Piezas de su capa y área. | [docs/rules/capas-y-flujo.md](../rules/capas-y-flujo.md) | `src/ArquitecturaBaseMultitenant.Application/Services/Roles/RoleService.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Domain.UnitTests/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Api/` | Piezas de Api. | [docs/rules/tests.md](../rules/tests.md) | `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Api/IdempotencyModeContractTests.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Api/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Auth/` | Piezas de Auth. | [docs/rules/tests.md](../rules/tests.md) | `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Auth/AccountGoogleProtocolTests.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Auth/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Contracts/` | Piezas de Contracts. | [docs/rules/tests.md](../rules/tests.md) | `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Contracts/Etapa8ContractTests.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Contracts/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/ErrorHandling/` | Piezas de ErrorHandling. | [docs/rules/tests.md](../rules/tests.md) | `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/ErrorHandling/CursorErrorMappingTests.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/ErrorHandling/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Features/` | Piezas de Features. | [docs/rules/tests.md](../rules/tests.md) | `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Features/FeatureAvailabilityPolicyTests.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Features/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Hosting/` | Piezas de Hosting. | [docs/rules/tests.md](../rules/tests.md) | `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Hosting/HealthCheckTests.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Hosting/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Identity/` | Piezas de Identity. | [docs/rules/tests.md](../rules/tests.md) | `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Identity/AccountMethodsHttpTests.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Identity/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Json/` | Piezas de Json. | [docs/rules/tests.md](../rules/tests.md) | `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Json/DateOnlyTimeOnlyTests.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Json/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Legal/` | Piezas de Legal. | [docs/rules/tests.md](../rules/tests.md) | `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Legal/AccountDeletionLoginTests.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Legal/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Localization/` | Piezas de Localization. | [docs/rules/tests.md](../rules/tests.md) | `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Localization/LocalizationTests.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Localization/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Messaging/` | Piezas de Messaging. | [docs/rules/tests.md](../rules/tests.md) | `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Messaging/AccountNoticeTests.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Messaging/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Modules/` | Piezas de Modules. | [docs/rules/tests.md](../rules/tests.md) | `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Modules/Assistant/AssistantAuthorizationTests.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Modules/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Organization/` | Piezas de Organization. | [docs/rules/tests.md](../rules/tests.md) | `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Organization/CompaniesTests.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Organization/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Persistence/` | Piezas de Persistence. | [docs/rules/tests.md](../rules/tests.md) | `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Persistence/AccountRecoveryNoticeMigrationTests.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Persistence/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Platform/` | Piezas de Platform. | [docs/rules/tests.md](../rules/tests.md) | `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Platform/PlatformAccountAdministrationTests.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Platform/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/PublicSite/` | Piezas de PublicSite. | [docs/rules/tests.md](../rules/tests.md) | `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/PublicSite/DirectoryTests.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/PublicSite/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/ReferenceData/` | Piezas de ReferenceData. | [docs/rules/tests.md](../rules/tests.md) | `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/ReferenceData/ReferenceDataApiTests.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/ReferenceData/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Security/` | Piezas de Security. | [docs/rules/tests.md](../rules/tests.md) | `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Security/LoginCodeSecurityTests.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Security/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Support/` | Piezas de Support. | [docs/rules/tests.md](../rules/tests.md) | `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Support/AccountJourney.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Support/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Tenancy/` | Piezas de Tenancy. | [docs/rules/tests.md](../rules/tests.md) | `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Tenancy/AccessStatusCacheTests.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Tenancy/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/TestFeatures/` | Piezas de TestFeatures. | [docs/rules/tests.md](../rules/tests.md) | `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/TestFeatures/Access/BusinessOnlyController.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/TestFeatures/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Time/` | Piezas de Time. | [docs/rules/tests.md](../rules/tests.md) | `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Time/TimeZoneServiceTests.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Time/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Application.UnitTests/Common/` | Piezas de Common. | [docs/rules/tests.md](../rules/tests.md) | `tests/ArquitecturaBaseMultitenant.Application.UnitTests/Common/CultureProfilesTests.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Application.UnitTests/Common/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Application.UnitTests/Formatting/` | Piezas de Formatting. | [docs/rules/tests.md](../rules/tests.md) | `tests/ArquitecturaBaseMultitenant.Application.UnitTests/Formatting/VersionWireFormatterTests.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Application.UnitTests/Formatting/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Application.UnitTests/Identity/` | Piezas de Identity. | [docs/rules/tests.md](../rules/tests.md) | `tests/ArquitecturaBaseMultitenant.Application.UnitTests/Identity/AccountDeletionPolicyTests.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Application.UnitTests/Identity/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Application.UnitTests/Messaging/` | Piezas de Messaging. | [docs/rules/tests.md](../rules/tests.md) | `tests/ArquitecturaBaseMultitenant.Application.UnitTests/Messaging/ChannelContractsTests.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Application.UnitTests/Messaging/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Application.UnitTests/Modules/` | Piezas de Modules. | [docs/rules/tests.md](../rules/tests.md) | `tests/ArquitecturaBaseMultitenant.Application.UnitTests/Modules/Assistant/AssistantContractValidationTests.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Application.UnitTests/Modules/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Application.UnitTests/Persistence/` | Piezas de Persistence. | [docs/rules/tests.md](../rules/tests.md) | `tests/ArquitecturaBaseMultitenant.Application.UnitTests/Persistence/FakeUnitOfWorkTests.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Application.UnitTests/Persistence/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Application.UnitTests/ReferenceData/` | Piezas de ReferenceData. | [docs/rules/tests.md](../rules/tests.md) | `tests/ArquitecturaBaseMultitenant.Application.UnitTests/ReferenceData/ReferenceDataCatalogTests.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Application.UnitTests/ReferenceData/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Application.UnitTests/Resources/` | Piezas de Resources. | [docs/rules/tests.md](../rules/tests.md) | `tests/ArquitecturaBaseMultitenant.Application.UnitTests/Resources/ErrorTextsTests.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Application.UnitTests/Resources/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Application.UnitTests/Services/` | Piezas de Services. | [docs/rules/tests.md](../rules/tests.md) | `tests/ArquitecturaBaseMultitenant.Application.UnitTests/Services/Auth/AccessSwitchPolicyTests.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Application.UnitTests/Services/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Application.UnitTests/TestDoubles/` | Piezas de TestDoubles. | [docs/rules/tests.md](../rules/tests.md) | `tests/ArquitecturaBaseMultitenant.Application.UnitTests/TestDoubles/FakeTenantContext.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Application.UnitTests/TestDoubles/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.ArchitectureTests/Modules/` | Piezas de Modules. | [docs/rules/tests.md](../rules/tests.md) | `tests/ArquitecturaBaseMultitenant.ArchitectureTests/Modules/Assistant/ApplicationServicesTests.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.ArchitectureTests/Modules/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.ArchitectureTests/Support/` | Piezas de Support. | [docs/rules/tests.md](../rules/tests.md) | `tests/ArquitecturaBaseMultitenant.ArchitectureTests/Support/CallSites.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.ArchitectureTests/Support/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Domain.UnitTests/Assistant/` | Piezas de Assistant. | [docs/rules/tests.md](../rules/tests.md) | `tests/ArquitecturaBaseMultitenant.Domain.UnitTests/Assistant/AssistantSnapshotRetentionTests.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Domain.UnitTests/Assistant/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Domain.UnitTests/Auditing/` | Piezas de Auditing. | [docs/rules/tests.md](../rules/tests.md) | `tests/ArquitecturaBaseMultitenant.Domain.UnitTests/Auditing/AuditEntryTests.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Domain.UnitTests/Auditing/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Domain.UnitTests/Authentication/` | Piezas de Authentication. | [docs/rules/tests.md](../rules/tests.md) | `tests/ArquitecturaBaseMultitenant.Domain.UnitTests/Authentication/LoginAuditTests.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Domain.UnitTests/Authentication/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Domain.UnitTests/Authorization/` | Piezas de Authorization. | [docs/rules/tests.md](../rules/tests.md) | `tests/ArquitecturaBaseMultitenant.Domain.UnitTests/Authorization/RoleAssignmentTests.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Domain.UnitTests/Authorization/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Domain.UnitTests/Common/` | Piezas de Common. | [docs/rules/tests.md](../rules/tests.md) | `tests/ArquitecturaBaseMultitenant.Domain.UnitTests/Common/EntityAndValueObjectTests.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Domain.UnitTests/Common/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Domain.UnitTests/Features/` | Piezas de Features. | [docs/rules/tests.md](../rules/tests.md) | `tests/ArquitecturaBaseMultitenant.Domain.UnitTests/Features/TenantFeatureTests.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Domain.UnitTests/Features/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Domain.UnitTests/Invitations/` | Piezas de Invitations. | [docs/rules/tests.md](../rules/tests.md) | `tests/ArquitecturaBaseMultitenant.Domain.UnitTests/Invitations/InvitationTests.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Domain.UnitTests/Invitations/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Domain.UnitTests/Legal/` | Piezas de Legal. | [docs/rules/tests.md](../rules/tests.md) | `tests/ArquitecturaBaseMultitenant.Domain.UnitTests/Legal/AccountDeletionCancelTicketTests.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Domain.UnitTests/Legal/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Domain.UnitTests/Messaging/` | Piezas de Messaging. | [docs/rules/tests.md](../rules/tests.md) | `tests/ArquitecturaBaseMultitenant.Domain.UnitTests/Messaging/OutboxMessageTests.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Domain.UnitTests/Messaging/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Domain.UnitTests/Platform/` | Piezas de Platform. | [docs/rules/tests.md](../rules/tests.md) | `tests/ArquitecturaBaseMultitenant.Domain.UnitTests/Platform/AccountRecoveryRequestTests.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Domain.UnitTests/Platform/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Domain.UnitTests/PublicSite/` | Piezas de PublicSite. | [docs/rules/tests.md](../rules/tests.md) | `tests/ArquitecturaBaseMultitenant.Domain.UnitTests/PublicSite/PublicPageTests.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Domain.UnitTests/PublicSite/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Domain.UnitTests/ReferenceData/` | Piezas de ReferenceData. | [docs/rules/tests.md](../rules/tests.md) | `tests/ArquitecturaBaseMultitenant.Domain.UnitTests/ReferenceData/ReferenceEntitiesTests.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Domain.UnitTests/ReferenceData/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Domain.UnitTests/Results/` | Piezas de Results. | [docs/rules/tests.md](../rules/tests.md) | `tests/ArquitecturaBaseMultitenant.Domain.UnitTests/Results/ResultTests.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Domain.UnitTests/Results/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Domain.UnitTests/Settings/` | Piezas de Settings. | [docs/rules/tests.md](../rules/tests.md) | `tests/ArquitecturaBaseMultitenant.Domain.UnitTests/Settings/PlatformSettingsTests.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Domain.UnitTests/Settings/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Domain.UnitTests/Tenancy/` | Piezas de Tenancy. | [docs/rules/tests.md](../rules/tests.md) | `tests/ArquitecturaBaseMultitenant.Domain.UnitTests/Tenancy/CompanyTests.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Domain.UnitTests/Tenancy/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Domain.UnitTests/Users/` | Piezas de Users. | [docs/rules/tests.md](../rules/tests.md) | `tests/ArquitecturaBaseMultitenant.Domain.UnitTests/Users/AccountLifecycleTests.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Domain.UnitTests/Users/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Domain.UnitTests/ValueObjects/` | Piezas de ValueObjects. | [docs/rules/tests.md](../rules/tests.md) | `tests/ArquitecturaBaseMultitenant.Domain.UnitTests/ValueObjects/CultureCodeTests.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Domain.UnitTests/ValueObjects/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Domain.UnitTests/WhatsApp/` | Piezas de WhatsApp. | [docs/rules/tests.md](../rules/tests.md) | `tests/ArquitecturaBaseMultitenant.Domain.UnitTests/WhatsApp/WhatsAppMessageTests.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Domain.UnitTests/WhatsApp/AGENTS.md) |
