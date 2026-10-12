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
│   ├── reviews/  informes de cierre de etapa y revisiones, con sus anexos
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
| `scripts/arneses/` | arneses ejecutables: detectan, arreglan lo mecánico y llaman a un modelo barato. | [tests.md](../rules/tests.md) | `scripts/datos-de-referencia/generar.test.mjs` | [AGENTS.md](../../scripts/arneses/AGENTS.md) |
| `scripts/datos-de-referencia/` | fuentes editables, snapshots/lock fijados y generador de los cinco JSON. | [datos-de-referencia.md](../rules/datos-de-referencia.md) | `scripts/datos-de-referencia/generar.mjs` | [AGENTS.md](../../scripts/datos-de-referencia/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Api/` | Piezas de su capa y área. | [capas-y-flujo.md](../rules/capas-y-flujo.md) | `src/ArquitecturaBaseMultitenant.ServiceDefaults/Extensions.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Api/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Api/Authorization/` | Piezas de su capa y área. | [roles.md](../features/roles.md) | `src/ArquitecturaBaseMultitenant.Api/Authorization/HasPermissionAttribute.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Api/Authorization/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Api/Contracts/` | *HttpRequest / *Query, props nullable, ToString() sin datos personales. | [api-http.md](../rules/api-http.md) | `src/ArquitecturaBaseMultitenant.Api/Contracts/Organization/RolesHttpRequests.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Api/Contracts/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Api/Contracts/Account/` | Piezas de su capa y área. | [sitio-publico.md](../features/sitio-publico.md) | `src/ArquitecturaBaseMultitenant.Api/Contracts/PublicSite/DirectoryQuery.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Api/Contracts/Account/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Api/Contracts/Auth/` | Piezas de su capa y área. | [sitio-publico.md](../features/sitio-publico.md) | `src/ArquitecturaBaseMultitenant.Api/Contracts/PublicSite/DirectoryQuery.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Api/Contracts/Auth/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Api/Contracts/Common/` | Piezas de su capa y área. | [sitio-publico.md](../features/sitio-publico.md) | `src/ArquitecturaBaseMultitenant.Api/Contracts/PublicSite/DirectoryQuery.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Api/Contracts/Common/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Api/Contracts/Organization/` | Piezas de su capa y área. | [sitio-publico.md](../features/sitio-publico.md) | `src/ArquitecturaBaseMultitenant.Api/Contracts/PublicSite/DirectoryQuery.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Api/Contracts/Organization/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Api/Contracts/Platform/` | Piezas de su capa y área. | [sitio-publico.md](../features/sitio-publico.md) | `src/ArquitecturaBaseMultitenant.Api/Contracts/PublicSite/DirectoryQuery.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Api/Contracts/Platform/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Api/Contracts/PublicSite/` | Piezas de su capa y área. | [sitio-publico.md](../features/sitio-publico.md) | `src/ArquitecturaBaseMultitenant.Api/Contracts/PublicSite/DirectoryQuery.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Api/Contracts/PublicSite/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Api/Contracts/ReferenceData/` | contrato de salida de los cinco catálogos para OpenAPI. | [datos-de-referencia.md](../rules/datos-de-referencia.md) | `src/ArquitecturaBaseMultitenant.Api/Contracts/ReferenceData/ReferenceDataHttpResponse.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Api/Contracts/ReferenceData/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Api/Controllers/` | controllers finos: contrato → servicio → ToActionResult. | [api-http.md](../rules/api-http.md) | `src/ArquitecturaBaseMultitenant.Api/Controllers/Organization/RolesController.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Api/Controllers/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Api/Controllers/Account/` | Piezas de su capa y área. | [sitio-publico.md](../features/sitio-publico.md) | `src/ArquitecturaBaseMultitenant.Api/Controllers/PublicSite/DirectoryController.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Api/Controllers/Account/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Api/Controllers/Auth/` | Piezas de su capa y área. | [sitio-publico.md](../features/sitio-publico.md) | `src/ArquitecturaBaseMultitenant.Api/Controllers/PublicSite/DirectoryController.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Api/Controllers/Auth/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Api/Controllers/Organization/` | Piezas de su capa y área. | [sitio-publico.md](../features/sitio-publico.md) | `src/ArquitecturaBaseMultitenant.Api/Controllers/PublicSite/DirectoryController.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Api/Controllers/Organization/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Api/Controllers/Personal/` | Piezas de su capa y área. | [sitio-publico.md](../features/sitio-publico.md) | `src/ArquitecturaBaseMultitenant.Api/Controllers/PublicSite/DirectoryController.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Api/Controllers/Personal/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Api/Controllers/Platform/` | Piezas de su capa y área. | [sitio-publico.md](../features/sitio-publico.md) | `src/ArquitecturaBaseMultitenant.Api/Controllers/PublicSite/DirectoryController.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Api/Controllers/Platform/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Api/Controllers/PublicSite/` | Piezas de su capa y área. | [sitio-publico.md](../features/sitio-publico.md) | `src/ArquitecturaBaseMultitenant.Api/Controllers/PublicSite/DirectoryController.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Api/Controllers/PublicSite/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Api/Controllers/ReferenceData/` | ruta anónima de catálogos globales, con ETag. | [datos-de-referencia.md](../rules/datos-de-referencia.md) | `src/ArquitecturaBaseMultitenant.Api/Controllers/ReferenceData/ReferenceDataController.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Api/Controllers/ReferenceData/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Api/Features/` | puerta de los módulos opcionales: FeatureGateMiddleware oculta con 404 un módulo apagado. | [modulos-habilitados.md](../rules/modulos-habilitados.md) | `src/ArquitecturaBaseMultitenant.Api/Features/FeatureGateMiddleware.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Api/Features/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Api/Idempotency/` | [Idempotent] y su filtro. | [idempotencia.md](../rules/idempotencia.md) | `src/ArquitecturaBaseMultitenant.Api/Idempotency/IdempotencyFilter.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Api/Idempotency/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Api/Json/` | conversores globales (UTC, Money, texto normalizado). | [textos-libres.md](../rules/textos-libres.md) | `src/ArquitecturaBaseMultitenant.Api/Json/NormalizedStringJsonConverter.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Api/Json/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Api/Legal/` | LegalAcceptanceMiddleware: 403 Legal. | [datos-personales.md](../rules/datos-personales.md) | `src/ArquitecturaBaseMultitenant.Api/Legal/LegalAcceptanceMiddleware.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Api/Legal/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Api/Modules/Assistant/` | lo HTTP del módulo (su *ApiModule, controllers como el del webhook, convención. | [whatsapp.md](../features/whatsapp.md) | `src/ArquitecturaBaseMultitenant.Api/Modules/WhatsApp/Contracts/WhatsAppWebhookHttpRequest.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Api/Modules/Assistant/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Api/Modules/WhatsApp/` | lo HTTP del módulo (su *ApiModule, controllers como el del webhook, convención. | [whatsapp.md](../features/whatsapp.md) | `src/ArquitecturaBaseMultitenant.Api/Modules/WhatsApp/Contracts/WhatsAppWebhookHttpRequest.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Api/Modules/WhatsApp/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Api/RateLimiting/` | políticas de ingreso por IP (RateLimitPolicies), con 429 ProblemDetails y Retry-After. | [api-http.md](../rules/api-http.md) | `src/ArquitecturaBaseMultitenant.Api/RateLimiting/RateLimitPolicies.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Api/RateLimiting/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Api/Tenancy/` | acceso firmado y contexto público por host, sin mezclar organizaciones. | [organizaciones.md](../features/organizaciones.md) | `src/ArquitecturaBaseMultitenant.Api/Tenancy/PublicSiteResolutionMiddleware.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Api/Tenancy/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.AppHost/` | Piezas de su capa y área. | [capas-y-flujo.md](../rules/capas-y-flujo.md) | `src/ArquitecturaBaseMultitenant.ServiceDefaults/Extensions.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.AppHost/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/` | Piezas de su capa y área. | [capas-y-flujo.md](../rules/capas-y-flujo.md) | `src/ArquitecturaBaseMultitenant.ServiceDefaults/Extensions.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Common/Exceptions/` | excepciones técnicas que Infrastructure le pasa a Application (concurrencia, índice único, clave. | [result-y-errores.md](../rules/result-y-errores.md) | `src/ArquitecturaBaseMultitenant.Application/Common/Exceptions/UniqueConstraintViolationException.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Common/Exceptions/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Common/Formatting/` | DisplayFormatter y perfiles de cultura. | [numeros-y-moneda.md](../rules/numeros-y-moneda.md) | `src/ArquitecturaBaseMultitenant.Application/Common/Formatting/CultureProfiles.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Common/Formatting/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Common/Logging/` | OperationLog y sus [LoggerMessage]: inicio, fin y código de error de cada. | [logs.md](../rules/logs.md) | `src/ArquitecturaBaseMultitenant.Application/Common/Logging/OperationLog.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Common/Logging/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Configuration/` | Piezas de su capa y área. | [capas-y-flujo.md](../rules/capas-y-flujo.md) | `src/ArquitecturaBaseMultitenant.Application/Configuration/Auth/BrowserSessionKeys.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Configuration/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Configuration/Auth/` | opciones de Application por área, validadas al arrancar y sin secretos. | [capas-y-flujo.md](../rules/capas-y-flujo.md) | `src/ArquitecturaBaseMultitenant.Application/Configuration/Messaging/OutboxOptions.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Configuration/Auth/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Configuration/Invitations/` | opciones de Application por área, validadas al arrancar y sin secretos. | [capas-y-flujo.md](../rules/capas-y-flujo.md) | `src/ArquitecturaBaseMultitenant.Application/Configuration/Messaging/OutboxOptions.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Configuration/Invitations/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Configuration/Messaging/` | opciones de Application por área, validadas al arrancar y sin secretos. | [capas-y-flujo.md](../rules/capas-y-flujo.md) | `src/ArquitecturaBaseMultitenant.Application/Configuration/Messaging/OutboxOptions.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Configuration/Messaging/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Interfaces/Integrations/` | puertos a lo externo, por tema. | [capas-y-flujo.md](../rules/capas-y-flujo.md) | `src/ArquitecturaBaseMultitenant.Application/Interfaces/Integrations/Caching/IAccessStatusCache.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Interfaces/Integrations/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Interfaces/Integrations/Phones/` | puerto de formato de lectura E1. | [telefonos.md](../rules/telefonos.md) | `src/ArquitecturaBaseMultitenant.Application/Interfaces/Integrations/Phones/IPhoneNumberDisplayFormatter.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Interfaces/Integrations/Phones/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Interfaces/Persistence/` | I<X>Repository (escribe), I<X>Reader (lee), IUnitOfWork, ITenantScope. | [persistencia-ef.md](../rules/persistencia-ef.md) | `src/ArquitecturaBaseMultitenant.Application/Interfaces/Persistence/IRoleRepository.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Interfaces/Persistence/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Interfaces/ReferenceData/` | puertos de moneda, país, zona, cultura y tipo fiscal. | [datos-de-referencia.md](../rules/datos-de-referencia.md) | `src/ArquitecturaBaseMultitenant.Application/Interfaces/ReferenceData/ICurrencyCatalog.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Interfaces/ReferenceData/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Interfaces/Services/` | una interfaz por servicio. | [capas-y-flujo.md](../rules/capas-y-flujo.md) | `src/ArquitecturaBaseMultitenant.Application/Interfaces/Services/IRoleService.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Interfaces/Services/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Models/` | *Request, *Response, ReadModels/*Row. | [numeros-y-moneda.md](../rules/numeros-y-moneda.md) | `src/ArquitecturaBaseMultitenant.Application/Models/Auditing/OrganizationAuditModels.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Models/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Models/Auditing/` | Piezas de su capa y área. | [whatsapp.md](../features/whatsapp.md) | `src/ArquitecturaBaseMultitenant.Application/Services/Roles/RoleService.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Models/Auditing/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Models/Auth/` | Piezas de su capa y área. | [whatsapp.md](../features/whatsapp.md) | `src/ArquitecturaBaseMultitenant.Application/Services/Roles/RoleService.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Models/Auth/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Models/Common/` | Piezas de su capa y área. | [whatsapp.md](../features/whatsapp.md) | `src/ArquitecturaBaseMultitenant.Application/Services/Roles/RoleService.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Models/Common/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Models/Companies/` | Piezas de su capa y área. | [whatsapp.md](../features/whatsapp.md) | `src/ArquitecturaBaseMultitenant.Application/Services/Roles/RoleService.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Models/Companies/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Models/Engagement/` | Piezas de su capa y área. | [whatsapp.md](../features/whatsapp.md) | `src/ArquitecturaBaseMultitenant.Application/Services/Roles/RoleService.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Models/Engagement/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Models/Execution/` | Piezas de su capa y área. | [whatsapp.md](../features/whatsapp.md) | `src/ArquitecturaBaseMultitenant.Application/Services/Roles/RoleService.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Models/Execution/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Models/Features/` | Piezas de su capa y área. | [whatsapp.md](../features/whatsapp.md) | `src/ArquitecturaBaseMultitenant.Application/Services/Roles/RoleService.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Models/Features/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Models/Files/` | Piezas de su capa y área. | [whatsapp.md](../features/whatsapp.md) | `src/ArquitecturaBaseMultitenant.Application/Services/Roles/RoleService.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Models/Files/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Models/Help/` | Piezas de su capa y área. | [whatsapp.md](../features/whatsapp.md) | `src/ArquitecturaBaseMultitenant.Application/Services/Roles/RoleService.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Models/Help/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Models/Home/` | Piezas de su capa y área. | [whatsapp.md](../features/whatsapp.md) | `src/ArquitecturaBaseMultitenant.Application/Services/Roles/RoleService.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Models/Home/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Models/Idempotency/` | Piezas de su capa y área. | [whatsapp.md](../features/whatsapp.md) | `src/ArquitecturaBaseMultitenant.Application/Services/Roles/RoleService.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Models/Idempotency/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Models/Identity/` | Piezas de su capa y área. | [whatsapp.md](../features/whatsapp.md) | `src/ArquitecturaBaseMultitenant.Application/Services/Roles/RoleService.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Models/Identity/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Models/Invitations/` | Piezas de su capa y área. | [whatsapp.md](../features/whatsapp.md) | `src/ArquitecturaBaseMultitenant.Application/Services/Roles/RoleService.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Models/Invitations/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Models/Legal/` | Piezas de su capa y área. | [whatsapp.md](../features/whatsapp.md) | `src/ArquitecturaBaseMultitenant.Application/Services/Roles/RoleService.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Models/Legal/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Models/Messaging/` | Piezas de su capa y área. | [whatsapp.md](../features/whatsapp.md) | `src/ArquitecturaBaseMultitenant.Application/Services/Roles/RoleService.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Models/Messaging/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Models/Notifications/` | Piezas de su capa y área. | [whatsapp.md](../features/whatsapp.md) | `src/ArquitecturaBaseMultitenant.Application/Services/Roles/RoleService.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Models/Notifications/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Models/OrganizationUsers/` | Piezas de su capa y área. | [whatsapp.md](../features/whatsapp.md) | `src/ArquitecturaBaseMultitenant.Application/Services/Roles/RoleService.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Models/OrganizationUsers/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Models/Platform/` | Piezas de su capa y área. | [whatsapp.md](../features/whatsapp.md) | `src/ArquitecturaBaseMultitenant.Application/Services/Roles/RoleService.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Models/Platform/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Models/Profile/` | Piezas de su capa y área. | [whatsapp.md](../features/whatsapp.md) | `src/ArquitecturaBaseMultitenant.Application/Services/Roles/RoleService.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Models/Profile/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Models/PublicSite/` | Piezas de su capa y área. | [whatsapp.md](../features/whatsapp.md) | `src/ArquitecturaBaseMultitenant.Application/Services/Roles/RoleService.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Models/PublicSite/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Models/ReferenceData/` | respuesta de los cinco catálogos traducidos. | [datos-de-referencia.md](../rules/datos-de-referencia.md) | `src/ArquitecturaBaseMultitenant.Application/Models/ReferenceData/ReferenceDataResponse.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Models/ReferenceData/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Models/Reports/` | Piezas de su capa y área. | [whatsapp.md](../features/whatsapp.md) | `src/ArquitecturaBaseMultitenant.Application/Services/Roles/RoleService.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Models/Reports/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Models/Request/` | Piezas de su capa y área. | [whatsapp.md](../features/whatsapp.md) | `src/ArquitecturaBaseMultitenant.Application/Services/Roles/RoleService.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Models/Request/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Models/Roles/` | Piezas de su capa y área. | [whatsapp.md](../features/whatsapp.md) | `src/ArquitecturaBaseMultitenant.Application/Services/Roles/RoleService.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Models/Roles/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Models/Settings/` | Piezas de su capa y área. | [whatsapp.md](../features/whatsapp.md) | `src/ArquitecturaBaseMultitenant.Application/Services/Roles/RoleService.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Models/Settings/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Models/Tenancy/` | Piezas de su capa y área. | [whatsapp.md](../features/whatsapp.md) | `src/ArquitecturaBaseMultitenant.Application/Services/Roles/RoleService.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Models/Tenancy/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Models/Time/` | Piezas de su capa y área. | [whatsapp.md](../features/whatsapp.md) | `src/ArquitecturaBaseMultitenant.Application/Services/Roles/RoleService.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Models/Time/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Models/WhatsApp/` | Piezas de su capa y área. | [whatsapp.md](../features/whatsapp.md) | `src/ArquitecturaBaseMultitenant.Application/Services/Roles/RoleService.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Models/WhatsApp/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Modules/Assistant/` | un módulo quitable. | [whatsapp.md](../features/whatsapp.md) | `src/ArquitecturaBaseMultitenant.Application/Modules/WhatsApp/Interfaces/Integrations/IWhatsAppAvailability.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Modules/Assistant/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Modules/WhatsApp/` | un módulo quitable. | [whatsapp.md](../features/whatsapp.md) | `src/ArquitecturaBaseMultitenant.Application/Modules/WhatsApp/Interfaces/Integrations/IWhatsAppAvailability.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Modules/WhatsApp/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Resources/` | . | [textos-y-traducciones.md](../rules/textos-y-traducciones.md) | `src/ArquitecturaBaseMultitenant.Application/Resources/Errors.resx` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Resources/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Services/` | servicio + helpers con un sufijo de la lista cerrada de capas-y-flujo. | [guardado.md](../rules/guardado.md) | `src/ArquitecturaBaseMultitenant.Application/Services/Roles/RoleService.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Services/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Services/Auditing/` | Piezas de su capa y área. | [roles.md](../features/roles.md) | `src/ArquitecturaBaseMultitenant.Application/Services/Roles/PermissionService.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Services/Auditing/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Services/Auth/` | Piezas de su capa y área. | [roles.md](../features/roles.md) | `src/ArquitecturaBaseMultitenant.Application/Services/Roles/PermissionService.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Services/Auth/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Services/Companies/` | Piezas de su capa y área. | [roles.md](../features/roles.md) | `src/ArquitecturaBaseMultitenant.Application/Services/Roles/PermissionService.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Services/Companies/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Services/Home/` | Piezas de su capa y área. | [roles.md](../features/roles.md) | `src/ArquitecturaBaseMultitenant.Application/Services/Roles/PermissionService.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Services/Home/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Services/Identity/` | Piezas de su capa y área. | [roles.md](../features/roles.md) | `src/ArquitecturaBaseMultitenant.Application/Services/Roles/PermissionService.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Services/Identity/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Services/Invitations/` | Piezas de su capa y área. | [roles.md](../features/roles.md) | `src/ArquitecturaBaseMultitenant.Application/Services/Roles/PermissionService.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Services/Invitations/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Services/Legal/` | Piezas de su capa y área. | [roles.md](../features/roles.md) | `src/ArquitecturaBaseMultitenant.Application/Services/Roles/PermissionService.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Services/Legal/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Services/Messaging/` | Piezas de su capa y área. | [roles.md](../features/roles.md) | `src/ArquitecturaBaseMultitenant.Application/Services/Roles/PermissionService.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Services/Messaging/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Services/OrganizationSettings/` | Piezas de su capa y área. | [roles.md](../features/roles.md) | `src/ArquitecturaBaseMultitenant.Application/Services/Roles/PermissionService.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Services/OrganizationSettings/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Services/OrganizationUsers/` | Piezas de su capa y área. | [roles.md](../features/roles.md) | `src/ArquitecturaBaseMultitenant.Application/Services/Roles/PermissionService.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Services/OrganizationUsers/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Services/Personal/` | Piezas de su capa y área. | [roles.md](../features/roles.md) | `src/ArquitecturaBaseMultitenant.Application/Services/Roles/PermissionService.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Services/Personal/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Services/Platform/` | Piezas de su capa y área. | [roles.md](../features/roles.md) | `src/ArquitecturaBaseMultitenant.Application/Services/Roles/PermissionService.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Services/Platform/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Services/Profile/` | Piezas de su capa y área. | [roles.md](../features/roles.md) | `src/ArquitecturaBaseMultitenant.Application/Services/Roles/PermissionService.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Services/Profile/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Services/PublicSite/` | Piezas de su capa y área. | [roles.md](../features/roles.md) | `src/ArquitecturaBaseMultitenant.Application/Services/Roles/PermissionService.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Services/PublicSite/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Services/ReferenceData/` | servicio que combina los puertos y entrega los catálogos a la Api. | [datos-de-referencia.md](../rules/datos-de-referencia.md) | `src/ArquitecturaBaseMultitenant.Application/Services/ReferenceData/ReferenceDataService.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Services/ReferenceData/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Services/Roles/` | Piezas de su capa y área. | [roles.md](../features/roles.md) | `src/ArquitecturaBaseMultitenant.Application/Services/Roles/PermissionService.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Services/Roles/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Validation/` | un validador por request, con ValidationRules. | [validacion.md](../rules/validacion.md) | `src/ArquitecturaBaseMultitenant.Application/Validation/Roles/CreateRoleRequestValidator.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Validation/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Validation/Auditing/` | Piezas de su capa y área. | [roles.md](../features/roles.md) | `src/ArquitecturaBaseMultitenant.Application/Validation/Roles/CreateRoleRequestValidator.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Validation/Auditing/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Validation/Auth/` | Piezas de su capa y área. | [roles.md](../features/roles.md) | `src/ArquitecturaBaseMultitenant.Application/Validation/Roles/CreateRoleRequestValidator.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Validation/Auth/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Validation/Companies/` | Piezas de su capa y área. | [roles.md](../features/roles.md) | `src/ArquitecturaBaseMultitenant.Application/Validation/Roles/CreateRoleRequestValidator.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Validation/Companies/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Validation/Identity/` | Piezas de su capa y área. | [roles.md](../features/roles.md) | `src/ArquitecturaBaseMultitenant.Application/Validation/Roles/CreateRoleRequestValidator.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Validation/Identity/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Validation/Invitations/` | Piezas de su capa y área. | [roles.md](../features/roles.md) | `src/ArquitecturaBaseMultitenant.Application/Validation/Roles/CreateRoleRequestValidator.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Validation/Invitations/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Validation/Legal/` | Piezas de su capa y área. | [roles.md](../features/roles.md) | `src/ArquitecturaBaseMultitenant.Application/Validation/Roles/CreateRoleRequestValidator.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Validation/Legal/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Validation/OrganizationSettings/` | Piezas de su capa y área. | [roles.md](../features/roles.md) | `src/ArquitecturaBaseMultitenant.Application/Validation/Roles/CreateRoleRequestValidator.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Validation/OrganizationSettings/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Validation/OrganizationUsers/` | Piezas de su capa y área. | [roles.md](../features/roles.md) | `src/ArquitecturaBaseMultitenant.Application/Validation/Roles/CreateRoleRequestValidator.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Validation/OrganizationUsers/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Validation/Platform/` | Piezas de su capa y área. | [roles.md](../features/roles.md) | `src/ArquitecturaBaseMultitenant.Application/Validation/Roles/CreateRoleRequestValidator.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Validation/Platform/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Validation/Profile/` | Piezas de su capa y área. | [roles.md](../features/roles.md) | `src/ArquitecturaBaseMultitenant.Application/Validation/Roles/CreateRoleRequestValidator.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Validation/Profile/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Validation/PublicSite/` | Piezas de su capa y área. | [roles.md](../features/roles.md) | `src/ArquitecturaBaseMultitenant.Application/Validation/Roles/CreateRoleRequestValidator.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Validation/PublicSite/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Validation/Roles/` | Piezas de su capa y área. | [roles.md](../features/roles.md) | `src/ArquitecturaBaseMultitenant.Application/Validation/Roles/CreateRoleRequestValidator.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Validation/Roles/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Domain/` | entidades, value objects, <X>Errors, catálogos. | [capas-y-flujo.md](../rules/capas-y-flujo.md) | `src/ArquitecturaBaseMultitenant.Domain/Assistant/AssistantAttachment.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Domain/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Domain/.artifacts/` | entidades y <X>Errors del módulo (Domain/WhatsApp/ nace en la E8). | [whatsapp.md](../features/whatsapp.md) | `src/ArquitecturaBaseMultitenant.Domain/WhatsApp/WhatsAppCodeErrors.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Domain/.artifacts/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Domain/Assistant/` | entidades y <X>Errors del módulo (Domain/WhatsApp/ nace en la E8). | [whatsapp.md](../features/whatsapp.md) | `src/ArquitecturaBaseMultitenant.Domain/WhatsApp/WhatsAppCodeErrors.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Domain/Assistant/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Domain/Auditing/` | entidades y <X>Errors del módulo (Domain/WhatsApp/ nace en la E8). | [whatsapp.md](../features/whatsapp.md) | `src/ArquitecturaBaseMultitenant.Domain/WhatsApp/WhatsAppCodeErrors.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Domain/Auditing/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Domain/Authentication/` | entidades y <X>Errors del módulo (Domain/WhatsApp/ nace en la E8). | [whatsapp.md](../features/whatsapp.md) | `src/ArquitecturaBaseMultitenant.Domain/WhatsApp/WhatsAppCodeErrors.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Domain/Authentication/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Domain/Authorization/` | entidades y <X>Errors del módulo (Domain/WhatsApp/ nace en la E8). | [whatsapp.md](../features/whatsapp.md) | `src/ArquitecturaBaseMultitenant.Domain/WhatsApp/WhatsAppCodeErrors.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Domain/Authorization/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Domain/Common/` | Entity, ValueObject. | [multitenancy.md](../rules/multitenancy.md) | `src/ArquitecturaBaseMultitenant.Domain/Common/Entity.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Domain/Common/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Domain/Companies/` | entidades y <X>Errors del módulo (Domain/WhatsApp/ nace en la E8). | [whatsapp.md](../features/whatsapp.md) | `src/ArquitecturaBaseMultitenant.Domain/WhatsApp/WhatsAppCodeErrors.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Domain/Companies/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Domain/Features/` | catálogo de módulos. | [modulos-habilitados.md](../rules/modulos-habilitados.md) | `src/ArquitecturaBaseMultitenant.Domain/Features/Features.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Domain/Features/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Domain/Invitations/` | entidades y <X>Errors del módulo (Domain/WhatsApp/ nace en la E8). | [whatsapp.md](../features/whatsapp.md) | `src/ArquitecturaBaseMultitenant.Domain/WhatsApp/WhatsAppCodeErrors.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Domain/Invitations/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Domain/Legal/` | entidades y <X>Errors del módulo (Domain/WhatsApp/ nace en la E8). | [whatsapp.md](../features/whatsapp.md) | `src/ArquitecturaBaseMultitenant.Domain/WhatsApp/WhatsAppCodeErrors.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Domain/Legal/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Domain/Messaging/` | entidades y <X>Errors del módulo (Domain/WhatsApp/ nace en la E8). | [whatsapp.md](../features/whatsapp.md) | `src/ArquitecturaBaseMultitenant.Domain/WhatsApp/WhatsAppCodeErrors.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Domain/Messaging/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Domain/Platform/` | entidades y <X>Errors del módulo (Domain/WhatsApp/ nace en la E8). | [whatsapp.md](../features/whatsapp.md) | `src/ArquitecturaBaseMultitenant.Domain/WhatsApp/WhatsAppCodeErrors.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Domain/Platform/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Domain/PublicSite/` | entidades y <X>Errors del módulo (Domain/WhatsApp/ nace en la E8). | [whatsapp.md](../features/whatsapp.md) | `src/ArquitecturaBaseMultitenant.Domain/WhatsApp/WhatsAppCodeErrors.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Domain/PublicSite/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Domain/ReferenceData/` | entidades globales de los cinco catálogos y sus traducciones, sin dependencia de. | [datos-de-referencia.md](../rules/datos-de-referencia.md) | `src/ArquitecturaBaseMultitenant.Domain/ReferenceData/Currency.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Domain/ReferenceData/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Domain/Results/` | entidades y <X>Errors del módulo (Domain/WhatsApp/ nace en la E8). | [whatsapp.md](../features/whatsapp.md) | `src/ArquitecturaBaseMultitenant.Domain/WhatsApp/WhatsAppCodeErrors.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Domain/Results/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Domain/Settings/` | entidades y <X>Errors del módulo (Domain/WhatsApp/ nace en la E8). | [whatsapp.md](../features/whatsapp.md) | `src/ArquitecturaBaseMultitenant.Domain/WhatsApp/WhatsAppCodeErrors.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Domain/Settings/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Domain/Tenancy/` | entidades y <X>Errors del módulo (Domain/WhatsApp/ nace en la E8). | [whatsapp.md](../features/whatsapp.md) | `src/ArquitecturaBaseMultitenant.Domain/WhatsApp/WhatsAppCodeErrors.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Domain/Tenancy/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Domain/Users/` | entidades y <X>Errors del módulo (Domain/WhatsApp/ nace en la E8). | [whatsapp.md](../features/whatsapp.md) | `src/ArquitecturaBaseMultitenant.Domain/WhatsApp/WhatsAppCodeErrors.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Domain/Users/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Domain/ValueObjects/` | Money, Email, PhoneNumber, TaxId: un dato con forma propia es un value. | [numeros-y-moneda.md](../rules/numeros-y-moneda.md) | `src/ArquitecturaBaseMultitenant.Domain/ValueObjects/Money.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Domain/ValueObjects/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Domain/ValueObjects/TaxIdValidators/` | Algoritmos de identificación fiscal puros. | [identificacion-fiscal.md](../rules/identificacion-fiscal.md) | `src/ArquitecturaBaseMultitenant.Domain/ValueObjects/TaxIdValidators/ArgentineCuitValidator.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Domain/ValueObjects/TaxIdValidators/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Domain/WhatsApp/` | entidades y <X>Errors del módulo (Domain/WhatsApp/ nace en la E8). | [whatsapp.md](../features/whatsapp.md) | `src/ArquitecturaBaseMultitenant.Domain/WhatsApp/WhatsAppCodeErrors.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Domain/WhatsApp/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Infrastructure/` | Piezas de su capa y área. | [capas-y-flujo.md](../rules/capas-y-flujo.md) | `src/ArquitecturaBaseMultitenant.ServiceDefaults/Extensions.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Infrastructure/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Infrastructure/.artifacts/` | Piezas de su capa y área. | [whatsapp.md](../features/whatsapp.md) | `src/ArquitecturaBaseMultitenant.Application/Services/Roles/RoleService.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Infrastructure/.artifacts/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Infrastructure/BackgroundJobs/` | workers: un scope DI por organización y ITenantScope. | [multitenancy.md](../rules/multitenancy.md) | `src/ArquitecturaBaseMultitenant.Infrastructure/BackgroundJobs/TenantJobRunner.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Infrastructure/BackgroundJobs/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Infrastructure/Caching/` | HybridCache: claves de CacheKeys con prefijo de alcance. | [multitenancy.md](../rules/multitenancy.md) | `src/ArquitecturaBaseMultitenant.Infrastructure/Caching/HybridCacheExtensions.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Infrastructure/Caching/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Infrastructure/Features/` | filtro de módulos por tenant. | [modulos-habilitados.md](../rules/modulos-habilitados.md) | `src/ArquitecturaBaseMultitenant.Infrastructure/Features/TenantFeatureFilter.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Infrastructure/Features/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Infrastructure/Help/` | Piezas de su capa y área. | [whatsapp.md](../features/whatsapp.md) | `src/ArquitecturaBaseMultitenant.Application/Services/Roles/RoleService.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Infrastructure/Help/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Infrastructure/Idempotency/` | persistencia técnica de [Idempotent] en platform: reserva, respuesta guardada y limpieza. | [idempotencia.md](../rules/idempotencia.md) | `src/ArquitecturaBaseMultitenant.Infrastructure/Idempotency/IdempotencyStore.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Infrastructure/Idempotency/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Infrastructure/Identity/` | Piezas de su capa y área. | [whatsapp.md](../features/whatsapp.md) | `src/ArquitecturaBaseMultitenant.Application/Services/Roles/RoleService.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Infrastructure/Identity/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Infrastructure/Identity/Deletion/` | participantes de la baja de una cuenta: trabajan en el alcance y. | [datos-personales.md](../rules/datos-personales.md) | `src/ArquitecturaBaseMultitenant.Infrastructure/Identity/Deletion/MembershipDeletionParticipant.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Infrastructure/Identity/Deletion/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Infrastructure/Messaging/` | outbox persistente y su dispatcher. | [guardado.md](../rules/guardado.md) | `src/ArquitecturaBaseMultitenant.Infrastructure/Messaging/Outbox.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Infrastructure/Messaging/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Infrastructure/Messaging/Email/Templates/` | plantillas HTML embebidas: estructura y marcadores {{Nombre}}. | [textos-y-traducciones.md](../rules/textos-y-traducciones.md) | `src/ArquitecturaBaseMultitenant.Infrastructure/Messaging/Email/Templates/LoginCode.html` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Infrastructure/Messaging/Email/Templates/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Infrastructure/Modules/` | Piezas de su capa y área. | [whatsapp.md](../features/whatsapp.md) | `src/ArquitecturaBaseMultitenant.Application/Services/Roles/RoleService.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Infrastructure/Modules/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Infrastructure/Modules/Assistant/` | adaptadores del módulo. | [whatsapp.md](../features/whatsapp.md) | `src/ArquitecturaBaseMultitenant.Infrastructure/Modules/WhatsApp/Disabled/DisabledWhatsAppAvailability.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Infrastructure/Modules/Assistant/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Infrastructure/Modules/WhatsApp/` | adaptadores del módulo. | [whatsapp.md](../features/whatsapp.md) | `src/ArquitecturaBaseMultitenant.Infrastructure/Modules/WhatsApp/Disabled/DisabledWhatsAppAvailability.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Infrastructure/Modules/WhatsApp/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/` | ApplicationDbContext, UnitOfWork, TenantContext y los límites de persistencia. | [persistencia-ef.md](../rules/persistencia-ef.md) | `src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/TenantContext.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/Configurations/Identity/` | una IEntityTypeConfiguration por entidad. | [persistencia-ef.md](../rules/persistencia-ef.md) | `src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/Configurations/Tenant/AuditEntryConfiguration.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/Configurations/Identity/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/Configurations/Platform/` | una IEntityTypeConfiguration por entidad. | [persistencia-ef.md](../rules/persistencia-ef.md) | `src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/Configurations/Tenant/AuditEntryConfiguration.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/Configurations/Platform/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/Configurations/Platform/ReferenceData/` | una configuración EF por tabla de referencia global, sin RLS. | [datos-de-referencia.md](../rules/datos-de-referencia.md) | `src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/Configurations/Platform/ReferenceData/CurrencyConfiguration.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/Configurations/Platform/ReferenceData/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/Configurations/PublicSite/` | una IEntityTypeConfiguration por entidad. | [persistencia-ef.md](../rules/persistencia-ef.md) | `src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/Configurations/Tenant/AuditEntryConfiguration.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/Configurations/PublicSite/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/Configurations/Tenant/` | una IEntityTypeConfiguration por entidad. | [persistencia-ef.md](../rules/persistencia-ef.md) | `src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/Configurations/Tenant/AuditEntryConfiguration.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/Configurations/Tenant/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/Migrations/` | se generan con el comando de la guía. | [persistencia-ef.md](../rules/persistencia-ef.md) | `docs/guides/migracion.md` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/Migrations/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/Readers/` | proyecciones AsNoTracking → *Row. | [datos-de-referencia.md](../rules/datos-de-referencia.md) | `src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/Readers/ReferenceDataReader.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/Readers/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/Repositories/` | EF para escribir un agregado. | [guardado.md](../rules/guardado.md) | `src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/Repositories/RoleRepository.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/Repositories/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/Seed/ReferenceData/` | cinco JSON generados de ISO/IANA/CLDR y fuentes editables del script. | [datos-de-referencia.md](../rules/datos-de-referencia.md) | `src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/Seed/ReferenceData/currencies.json` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/Seed/ReferenceData/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Infrastructure/Phones/` | adaptador libphonenumber de presentación E1. | [telefonos.md](../rules/telefonos.md) | `src/ArquitecturaBaseMultitenant.Infrastructure/Phones/LibPhoneNumberDisplayFormatter.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Infrastructure/Phones/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Infrastructure/ReferenceData/` | adaptador JSON de los cinco catálogos en E1. | [datos-de-referencia.md](../rules/datos-de-referencia.md) | `src/ArquitecturaBaseMultitenant.Infrastructure/ReferenceData/JsonReferenceDataCatalog.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Infrastructure/ReferenceData/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Infrastructure/RequestContext/` | Piezas de su capa y área. | [whatsapp.md](../features/whatsapp.md) | `src/ArquitecturaBaseMultitenant.Application/Services/Roles/RoleService.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Infrastructure/RequestContext/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Infrastructure/Security/` | Piezas de su capa y área. | [whatsapp.md](../features/whatsapp.md) | `src/ArquitecturaBaseMultitenant.Application/Services/Roles/RoleService.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Infrastructure/Security/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Infrastructure/Time/` | Piezas de su capa y área. | [whatsapp.md](../features/whatsapp.md) | `src/ArquitecturaBaseMultitenant.Application/Services/Roles/RoleService.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Infrastructure/Time/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Infrastructure/WhatsApp/` | Piezas de su capa y área. | [whatsapp.md](../features/whatsapp.md) | `src/ArquitecturaBaseMultitenant.Application/Services/Roles/RoleService.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Infrastructure/WhatsApp/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.ServiceDefaults/` | Piezas de su capa y área. | [capas-y-flujo.md](../rules/capas-y-flujo.md) | `src/ArquitecturaBaseMultitenant.ServiceDefaults/Extensions.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.ServiceDefaults/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/` | Piezas de su capa y área. | [capas-y-flujo.md](../rules/capas-y-flujo.md) | `src/ArquitecturaBaseMultitenant.Application/Services/Roles/RoleService.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Modules/Assistant/` | Piezas de su capa y área. | [whatsapp.md](../features/whatsapp.md) | `src/ArquitecturaBaseMultitenant.Application/Services/Roles/RoleService.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Modules/Assistant/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Modules/TechnicalContribution/` | Piezas de su capa y área. | [whatsapp.md](../features/whatsapp.md) | `src/ArquitecturaBaseMultitenant.Application/Services/Roles/RoleService.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Modules/TechnicalContribution/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Modules/WhatsApp/` | Piezas de su capa y área. | [whatsapp.md](../features/whatsapp.md) | `src/ArquitecturaBaseMultitenant.Application/Services/Roles/RoleService.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Modules/WhatsApp/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Application.UnitTests/` | Piezas de su capa y área. | [capas-y-flujo.md](../rules/capas-y-flujo.md) | `src/ArquitecturaBaseMultitenant.Application/Services/Roles/RoleService.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Application.UnitTests/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Application.UnitTests/Modules/Assistant/` | Piezas de su capa y área. | [whatsapp.md](../features/whatsapp.md) | `src/ArquitecturaBaseMultitenant.Application/Services/Roles/RoleService.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Application.UnitTests/Modules/Assistant/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Application.UnitTests/Modules/WhatsApp/` | Piezas de su capa y área. | [whatsapp.md](../features/whatsapp.md) | `src/ArquitecturaBaseMultitenant.Application/Services/Roles/RoleService.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Application.UnitTests/Modules/WhatsApp/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.ArchitectureTests/` | Piezas de su capa y área. | [capas-y-flujo.md](../rules/capas-y-flujo.md) | `src/ArquitecturaBaseMultitenant.Application/Services/Roles/RoleService.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.ArchitectureTests/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.ArchitectureTests/Modules/Assistant/` | Piezas de su capa y área. | [whatsapp.md](../features/whatsapp.md) | `src/ArquitecturaBaseMultitenant.Application/Services/Roles/RoleService.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.ArchitectureTests/Modules/Assistant/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.ArchitectureTests/Modules/WhatsApp/` | Piezas de su capa y área. | [whatsapp.md](../features/whatsapp.md) | `src/ArquitecturaBaseMultitenant.Application/Services/Roles/RoleService.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.ArchitectureTests/Modules/WhatsApp/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Domain.UnitTests/` | Piezas de su capa y área. | [capas-y-flujo.md](../rules/capas-y-flujo.md) | `src/ArquitecturaBaseMultitenant.Application/Services/Roles/RoleService.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Domain.UnitTests/AGENTS.md) |
