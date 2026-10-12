# Mapa de carpetas

El árbol ubica responsabilidades. Solo enumera archivos que sirven como modelo para copiar; agregar un archivo a una carpeta existente no requiere actualizarlo. Las carpetas de historia, contratos y diseño se muestran sin desglosar sus contenidos.

## Raíz

```text
├── .github/
│   └── workflows/
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
│   │   │   └── Time/
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
│   │   ├── BackgroundJobs/
│   │   ├── Caching/
│   │   ├── Features/
│   │   ├── Files/  almacenamiento privado local y journal de compensación
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
│   │   │   │   ├── Export/  exportación Markdown/PDF del chat
│   │   │   │   │   └── Fonts/  fuente y licencia del PDF
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
│   │   │   ├── Readers/  AsNoTracking + proyección a *Row/*Response
│   │   │   ├── Repositories/  internal sealed; escrituras de agregados
│   │   │   │   └── RoleRepository.cs  repositorio de referencia
│   │   │   ├── Rls/
│   │   │   ├── Seed/
│   │   │   │   └── ReferenceData/  cinco JSON de salida versionados; no se editan a mano
│   │   ├── Phones/
│   │   ├── ReferenceData/
│   │   ├── RequestContext/
│   │   ├── Security/
│   │   └── Time/
│   └── ArquitecturaBaseMultitenant.ServiceDefaults/
├── tests/  ver abajo
│   ├── ArquitecturaBaseMultitenant.Api.IntegrationTests/
│   │   ├── Api/
│   │   ├── Auth/
│   │   ├── Contracts/
│   │   ├── ErrorHandling/
│   │   ├── Features/
│   │   ├── Files/  pruebas del almacenamiento privado y compensaciones
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
│   │   │   ├── AssistantBrowser/  controles privados del recorrido real aislado
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
| `src/ArquitecturaBaseMultitenant.Api/` | Composición de la API HTTP, endpoints y adaptadores de la petición. | [docs/rules/capas-y-flujo.md](../rules/capas-y-flujo.md) | `src/ArquitecturaBaseMultitenant.Api/Authentication/OpenIdPrincipalFactory.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Api/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Api/Authorization/` | Autorización de rutas y permisos HTTP. | [docs/features/roles.md](../features/roles.md) | `src/ArquitecturaBaseMultitenant.Api/Authorization/HasPermissionAttribute.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Api/Authorization/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Api/Contracts/` | Contratos de petición y respuesta HTTP. | [docs/rules/api-http.md](../rules/api-http.md) | `src/ArquitecturaBaseMultitenant.Api/Contracts/Organization/RolesHttpRequests.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Api/Contracts/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Api/Contracts/Account/` | Contratos de petición y respuesta HTTP de cuenta. | [docs/features/identidad.md](../features/identidad.md) | `src/ArquitecturaBaseMultitenant.Api/Contracts/Account/AcceptLegalHttpRequest.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Api/Contracts/Account/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Api/Contracts/Auth/` | Contratos de petición y respuesta HTTP de ingreso. | [docs/features/identidad.md](../features/identidad.md) | `src/ArquitecturaBaseMultitenant.Api/Contracts/Auth/AcceptInvitationHttpRequest.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Api/Contracts/Auth/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Api/Contracts/Common/` | Contratos de petición y respuesta HTTP de uso común. | [docs/rules/capas-y-flujo.md](../rules/capas-y-flujo.md) | `src/ArquitecturaBaseMultitenant.Api/Contracts/Common/PhoneInputHttpRequest.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Api/Contracts/Common/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Api/Contracts/Organization/` | Contratos de petición y respuesta HTTP de organización. | [docs/features/organizaciones.md](../features/organizaciones.md) | `src/ArquitecturaBaseMultitenant.Api/Contracts/Organization/CompaniesHttpRequests.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Api/Contracts/Organization/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Api/Contracts/Platform/` | Contratos de petición y respuesta HTTP de Plataforma. | [docs/features/plataforma.md](../features/plataforma.md) | `src/ArquitecturaBaseMultitenant.Api/Contracts/Platform/PlatformAccountHttpRequests.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Api/Contracts/Platform/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Api/Contracts/PublicSite/` | Contratos de petición y respuesta HTTP de sitio público. | [docs/features/sitio-publico.md](../features/sitio-publico.md) | `src/ArquitecturaBaseMultitenant.Api/Contracts/PublicSite/DirectoryQuery.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Api/Contracts/PublicSite/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Api/Contracts/ReferenceData/` | Contratos de petición y respuesta HTTP de datos de referencia. | [docs/rules/datos-de-referencia.md](../rules/datos-de-referencia.md) | `src/ArquitecturaBaseMultitenant.Api/Contracts/ReferenceData/ReferenceDataHttpResponse.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Api/Contracts/ReferenceData/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Api/Controllers/` | Controllers HTTP. | [docs/rules/api-http.md](../rules/api-http.md) | `src/ArquitecturaBaseMultitenant.Api/Controllers/Organization/RolesController.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Api/Controllers/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Api/Controllers/Account/` | Controllers HTTP de cuenta. | [docs/features/identidad.md](../features/identidad.md) | `src/ArquitecturaBaseMultitenant.Api/Controllers/Account/AccountDeletionController.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Api/Controllers/Account/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Api/Controllers/Auth/` | Controllers HTTP de ingreso. | [docs/features/identidad.md](../features/identidad.md) | `src/ArquitecturaBaseMultitenant.Api/Controllers/Auth/AccountDeletionCancelController.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Api/Controllers/Auth/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Api/Controllers/Organization/` | Controllers HTTP de organización. | [docs/features/organizaciones.md](../features/organizaciones.md) | `src/ArquitecturaBaseMultitenant.Api/Controllers/Organization/CompaniesController.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Api/Controllers/Organization/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Api/Controllers/Personal/` | Controllers HTTP de acceso personal. | [docs/features/personal.md](../features/personal.md) | `src/ArquitecturaBaseMultitenant.Application/Services/Roles/RoleService.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Api/Controllers/Personal/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Api/Controllers/Platform/` | Controllers HTTP de Plataforma. | [docs/features/plataforma.md](../features/plataforma.md) | `src/ArquitecturaBaseMultitenant.Api/Controllers/Platform/PlatformAccountsController.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Api/Controllers/Platform/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Api/Controllers/PublicSite/` | Controllers HTTP de sitio público. | [docs/features/sitio-publico.md](../features/sitio-publico.md) | `src/ArquitecturaBaseMultitenant.Api/Controllers/PublicSite/DirectoryController.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Api/Controllers/PublicSite/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Api/Controllers/ReferenceData/` | Controllers HTTP de datos de referencia. | [docs/rules/datos-de-referencia.md](../rules/datos-de-referencia.md) | `src/ArquitecturaBaseMultitenant.Api/Controllers/ReferenceData/ReferenceDataController.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Api/Controllers/ReferenceData/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Api/Features/` | Adaptadores HTTP de Features. | [docs/rules/modulos-habilitados.md](../rules/modulos-habilitados.md) | `src/ArquitecturaBaseMultitenant.Api/Features/FeatureGateMiddleware.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Api/Features/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Api/Idempotency/` | Adaptadores HTTP de idempotencia. | [docs/rules/idempotencia.md](../rules/idempotencia.md) | `src/ArquitecturaBaseMultitenant.Api/Idempotency/IdempotencyFilter.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Api/Idempotency/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Api/Json/` | Adaptadores HTTP de Json. | [docs/rules/textos-libres.md](../rules/textos-libres.md) | `src/ArquitecturaBaseMultitenant.Api/Json/NormalizedStringJsonConverter.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Api/Json/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Api/Legal/` | Adaptadores HTTP de documentos legales. | [docs/rules/datos-personales.md](../rules/datos-personales.md) | `src/ArquitecturaBaseMultitenant.Api/Legal/LegalAcceptanceMiddleware.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Api/Legal/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Api/Modules/Assistant/` | Rutas del Asistente. | [Asistente](../features/asistente.md) | `src/ArquitecturaBaseMultitenant.Api/Modules/Assistant/Controllers/BusinessAssistantController.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Api/Modules/Assistant/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Api/Modules/WhatsApp/` | Composición del módulo de WhatsApp en la capa. | [docs/features/whatsapp.md](../features/whatsapp.md) | `src/ArquitecturaBaseMultitenant.Api/Modules/WhatsApp/Contracts/WhatsAppWebhookHttpRequest.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Api/Modules/WhatsApp/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Api/RateLimiting/` | Adaptadores HTTP de Rate Limiting. | [docs/rules/api-http.md](../rules/api-http.md) | `src/ArquitecturaBaseMultitenant.Api/RateLimiting/RateLimitPolicies.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Api/RateLimiting/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Api/Tenancy/` | Resolución de organización y subdominio para la petición. | [docs/features/organizaciones.md](../features/organizaciones.md) | `src/ArquitecturaBaseMultitenant.Api/Tenancy/PublicSiteResolutionMiddleware.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Api/Tenancy/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.AppHost/` | Composición y arranque del entorno Aspire. | [docs/rules/capas-y-flujo.md](../rules/capas-y-flujo.md) | `src/ArquitecturaBaseMultitenant.AppHost/AppHost.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.AppHost/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/` | Casos de uso, modelos y puertos de la aplicación. | [docs/rules/capas-y-flujo.md](../rules/capas-y-flujo.md) | `src/ArquitecturaBaseMultitenant.Application/Common/Exceptions/ConcurrencyConflictException.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Common/Exceptions/` | Adaptadores HTTP de uso común / Exceptions. | [docs/rules/result-y-errores.md](../rules/result-y-errores.md) | `src/ArquitecturaBaseMultitenant.Application/Common/Exceptions/UniqueConstraintViolationException.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Common/Exceptions/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Common/Formatting/` | Adaptadores HTTP de uso común / Formatting. | [docs/rules/numeros-y-moneda.md](../rules/numeros-y-moneda.md) | `src/ArquitecturaBaseMultitenant.Application/Common/Formatting/CultureProfiles.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Common/Formatting/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Common/Logging/` | Adaptadores HTTP de uso común / logs. | [docs/rules/logs.md](../rules/logs.md) | `src/ArquitecturaBaseMultitenant.Application/Common/Logging/OperationLog.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Common/Logging/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Configuration/` | Opciones de configuración. | [docs/rules/capas-y-flujo.md](../rules/capas-y-flujo.md) | `src/ArquitecturaBaseMultitenant.Application/Configuration/Auth/BrowserSessionKeys.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Configuration/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Configuration/Auth/` | Opciones de configuración de ingreso. | [docs/features/identidad.md](../features/identidad.md) | `src/ArquitecturaBaseMultitenant.Application/Configuration/Auth/LoginCodeOptions.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Configuration/Auth/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Configuration/Invitations/` | Opciones de configuración de invitaciones. | [docs/features/organizaciones.md](../features/organizaciones.md) | `src/ArquitecturaBaseMultitenant.Application/Configuration/Invitations/InvitationOptions.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Configuration/Invitations/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Configuration/Messaging/` | Opciones de configuración de Messaging. | [docs/rules/capas-y-flujo.md](../rules/capas-y-flujo.md) | `src/ArquitecturaBaseMultitenant.Application/Configuration/Messaging/OutboxOptions.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Configuration/Messaging/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Interfaces/Integrations/` | Puertos de integraciones externas. | [docs/rules/capas-y-flujo.md](../rules/capas-y-flujo.md) | `src/ArquitecturaBaseMultitenant.Application/Interfaces/Integrations/Caching/IAccessStatusCache.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Interfaces/Integrations/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Interfaces/Integrations/Phones/` | Puertos de integraciones externas para teléfonos. | [docs/rules/telefonos.md](../rules/telefonos.md) | `src/ArquitecturaBaseMultitenant.Application/Interfaces/Integrations/Phones/IPhoneNumberDisplayFormatter.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Interfaces/Integrations/Phones/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Interfaces/Persistence/` | Puertos de persistencia. | [docs/rules/persistencia-ef.md](../rules/persistencia-ef.md) | `src/ArquitecturaBaseMultitenant.Application/Interfaces/Persistence/IRoleRepository.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Interfaces/Persistence/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Interfaces/ReferenceData/` | Puertos de la aplicación para datos de referencia. | [docs/rules/datos-de-referencia.md](../rules/datos-de-referencia.md) | `src/ArquitecturaBaseMultitenant.Application/Interfaces/ReferenceData/ICurrencyCatalog.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Interfaces/ReferenceData/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Interfaces/Services/` | Puertos de servicios. | [docs/rules/capas-y-flujo.md](../rules/capas-y-flujo.md) | `src/ArquitecturaBaseMultitenant.Application/Interfaces/Services/IRoleService.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Interfaces/Services/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Models/` | Modelos de entrada y salida. | [docs/rules/numeros-y-moneda.md](../rules/numeros-y-moneda.md) | `src/ArquitecturaBaseMultitenant.Application/Models/Auditing/OrganizationAuditModels.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Models/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Models/Auditing/` | Modelos de entrada y salida de auditoría. | [docs/rules/capas-y-flujo.md](../rules/capas-y-flujo.md) | `src/ArquitecturaBaseMultitenant.Application/Models/Auditing/OrganizationAuditModels.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Models/Auditing/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Models/Auth/` | Modelos de entrada y salida de ingreso. | [docs/features/identidad.md](../features/identidad.md) | `src/ArquitecturaBaseMultitenant.Application/Models/Auth/AccessSelectionRequest.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Models/Auth/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Models/Common/` | Modelos de entrada y salida de uso común. | [docs/rules/capas-y-flujo.md](../rules/capas-y-flujo.md) | `src/ArquitecturaBaseMultitenant.Application/Models/Common/PhoneInputRequest.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Models/Common/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Models/Companies/` | Modelos de entrada y salida de empresas. | [docs/features/organizaciones.md](../features/organizaciones.md) | `src/ArquitecturaBaseMultitenant.Application/Models/Companies/CompanyMemberModels.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Models/Companies/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Models/Engagement/` | Modelos de entrada y salida de Engagement. | [docs/rules/capas-y-flujo.md](../rules/capas-y-flujo.md) | `src/ArquitecturaBaseMultitenant.Application/Models/Engagement/ConsumerInteractionResponse.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Models/Engagement/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Models/Execution/` | Modelos de entrada y salida de contexto de ejecución. | [docs/rules/capas-y-flujo.md](../rules/capas-y-flujo.md) | `src/ArquitecturaBaseMultitenant.Application/Models/Request/ExecutionActor.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Models/Execution/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Models/Features/` | Modelos de entrada y salida de Features. | [docs/rules/capas-y-flujo.md](../rules/capas-y-flujo.md) | `src/ArquitecturaBaseMultitenant.Application/Models/Features/TenantFeatureRow.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Models/Features/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Models/Files/` | Modelos de entrada y salida de Files. | [docs/rules/capas-y-flujo.md](../rules/capas-y-flujo.md) | `src/ArquitecturaBaseMultitenant.Application/Models/Files/FileObject.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Models/Files/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Models/Help/` | Modelos de entrada y salida de Help. | [docs/rules/capas-y-flujo.md](../rules/capas-y-flujo.md) | `src/ArquitecturaBaseMultitenant.Application/Models/Help/HelpArticleModels.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Models/Help/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Models/Home/` | Modelos de entrada y salida de Home. | [docs/features/inicio.md](../features/inicio.md) | `src/ArquitecturaBaseMultitenant.Application/Models/Home/HomeModels.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Models/Home/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Models/Idempotency/` | Modelos de entrada y salida de idempotencia. | [docs/rules/capas-y-flujo.md](../rules/capas-y-flujo.md) | `src/ArquitecturaBaseMultitenant.Application/Models/Idempotency/IdempotentReference.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Models/Idempotency/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Models/Identity/` | Modelos de entrada y salida de identidad. | [docs/features/identidad.md](../features/identidad.md) | `src/ArquitecturaBaseMultitenant.Application/Models/Identity/AccountLoginMethodsResponse.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Models/Identity/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Models/Invitations/` | Modelos de entrada y salida de invitaciones. | [docs/features/organizaciones.md](../features/organizaciones.md) | `src/ArquitecturaBaseMultitenant.Application/Models/Invitations/AcceptInvitationRequest.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Models/Invitations/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Models/Legal/` | Modelos de entrada y salida de documentos legales. | [docs/rules/capas-y-flujo.md](../rules/capas-y-flujo.md) | `src/ArquitecturaBaseMultitenant.Application/Models/Legal/AcceptLegalRequest.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Models/Legal/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Models/Messaging/` | Modelos de entrada y salida de Messaging. | [docs/rules/capas-y-flujo.md](../rules/capas-y-flujo.md) | `src/ArquitecturaBaseMultitenant.Application/Models/Messaging/EmailMessage.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Models/Messaging/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Models/Notifications/` | Modelos de entrada y salida de notificaciones. | [docs/rules/capas-y-flujo.md](../rules/capas-y-flujo.md) | `src/ArquitecturaBaseMultitenant.Application/Models/Notifications/AccountNotice.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Models/Notifications/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Models/OrganizationUsers/` | Modelos de entrada y salida de Organization Users. | [docs/features/organizaciones.md](../features/organizaciones.md) | `src/ArquitecturaBaseMultitenant.Application/Models/OrganizationUsers/OrganizationUserModels.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Models/OrganizationUsers/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Models/Platform/` | Modelos de entrada y salida de Plataforma. | [docs/features/plataforma.md](../features/plataforma.md) | `src/ArquitecturaBaseMultitenant.Application/Models/Platform/PlatformAccountModels.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Models/Platform/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Models/Profile/` | Modelos de entrada y salida de Profile. | [docs/rules/capas-y-flujo.md](../rules/capas-y-flujo.md) | `src/ArquitecturaBaseMultitenant.Application/Models/Profile/EffectivePermissions.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Models/Profile/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Models/PublicSite/` | Modelos de entrada y salida de sitio público. | [docs/features/sitio-publico.md](../features/sitio-publico.md) | `src/ArquitecturaBaseMultitenant.Application/Models/PublicSite/DirectoryEntryRow.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Models/PublicSite/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Models/ReferenceData/` | Modelos de entrada y salida de datos de referencia. | [docs/rules/datos-de-referencia.md](../rules/datos-de-referencia.md) | `src/ArquitecturaBaseMultitenant.Application/Models/ReferenceData/ReferenceDataResponse.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Models/ReferenceData/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Models/Reports/` | Modelos de entrada y salida de informes. | [docs/rules/capas-y-flujo.md](../rules/capas-y-flujo.md) | `src/ArquitecturaBaseMultitenant.Application/Models/Roles/RoleRow.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Models/Reports/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Models/Request/` | Modelos de entrada y salida de contexto de petición. | [docs/rules/capas-y-flujo.md](../rules/capas-y-flujo.md) | `src/ArquitecturaBaseMultitenant.Application/Models/Request/ExecutionActor.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Models/Request/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Models/Roles/` | Modelos de entrada y salida de roles. | [docs/features/roles.md](../features/roles.md) | `src/ArquitecturaBaseMultitenant.Application/Models/Roles/CreateRoleRequest.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Models/Roles/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Models/Settings/` | Modelos de entrada y salida de Settings. | [docs/rules/capas-y-flujo.md](../rules/capas-y-flujo.md) | `src/ArquitecturaBaseMultitenant.Application/Models/Settings/OrganizationSettingsModels.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Models/Settings/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Models/Tenancy/` | Modelos de entrada y salida de Tenancy. | [docs/features/organizaciones.md](../features/organizaciones.md) | `src/ArquitecturaBaseMultitenant.Application/Models/Tenancy/ActiveTenantAdminEmail.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Models/Tenancy/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Models/Time/` | Modelos de entrada y salida de tiempo. | [docs/rules/capas-y-flujo.md](../rules/capas-y-flujo.md) | `src/ArquitecturaBaseMultitenant.Application/Models/Time/DayRangeUtc.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Models/Time/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Modules/Assistant/` | Casos de uso del Asistente. | [Asistente](../features/asistente.md) | `src/ArquitecturaBaseMultitenant.Application/Modules/Assistant/Services/AssistantConversationService.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Modules/Assistant/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Modules/WhatsApp/` | Composición del módulo de WhatsApp en la capa. | [docs/features/whatsapp.md](../features/whatsapp.md) | `src/ArquitecturaBaseMultitenant.Application/Modules/WhatsApp/Interfaces/Integrations/IWhatsAppAvailability.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Modules/WhatsApp/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Resources/` | Resources de textos visibles en español e inglés. | [docs/rules/textos-y-traducciones.md](../rules/textos-y-traducciones.md) | `src/ArquitecturaBaseMultitenant.Application/Resources/Errors.resx` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Resources/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Services/` | Servicios de aplicación y sus helpers. | [docs/rules/guardado.md](../rules/guardado.md) | `src/ArquitecturaBaseMultitenant.Application/Services/Roles/RoleService.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Services/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Services/Auditing/` | Servicios de aplicación y sus helpers de auditoría. | [docs/rules/capas-y-flujo.md](../rules/capas-y-flujo.md) | `src/ArquitecturaBaseMultitenant.Application/Services/Auditing/AuditingRegistration.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Services/Auditing/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Services/Auth/` | Servicios de aplicación y sus helpers de ingreso. | [docs/features/identidad.md](../features/identidad.md) | `src/ArquitecturaBaseMultitenant.Application/Services/Auth/AccessSwitchPolicy.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Services/Auth/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Services/Companies/` | Servicios de aplicación y sus helpers de empresas. | [docs/features/organizaciones.md](../features/organizaciones.md) | `src/ArquitecturaBaseMultitenant.Application/Services/Companies/CompaniesRegistration.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Services/Companies/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Services/Home/` | Servicios de aplicación y sus helpers de Home. | [docs/features/inicio.md](../features/inicio.md) | `src/ArquitecturaBaseMultitenant.Application/Services/Home/HomeRegistration.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Services/Home/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Services/Identity/` | Servicios de aplicación y sus helpers de identidad. | [docs/features/identidad.md](../features/identidad.md) | `src/ArquitecturaBaseMultitenant.Application/Services/Identity/AccountGoogleService.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Services/Identity/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Services/Invitations/` | Servicios de aplicación y sus helpers de invitaciones. | [docs/features/organizaciones.md](../features/organizaciones.md) | `src/ArquitecturaBaseMultitenant.Application/Services/Invitations/InvitationAcceptanceGuard.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Services/Invitations/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Services/Legal/` | Servicios de aplicación y sus helpers de documentos legales. | [docs/rules/capas-y-flujo.md](../rules/capas-y-flujo.md) | `src/ArquitecturaBaseMultitenant.Application/Services/Legal/AccountDeletionCancelIssuer.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Services/Legal/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Services/Messaging/` | Servicios de aplicación y sus helpers de Messaging. | [docs/rules/capas-y-flujo.md](../rules/capas-y-flujo.md) | `src/ArquitecturaBaseMultitenant.Application/Services/Messaging/MessagingRegistration.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Services/Messaging/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Services/OrganizationSettings/` | Servicios de aplicación y sus helpers de Organization Settings. | [docs/features/organizaciones.md](../features/organizaciones.md) | `src/ArquitecturaBaseMultitenant.Application/Services/OrganizationSettings/LoginMethodDomainLinker.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Services/OrganizationSettings/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Services/OrganizationUsers/` | Servicios de aplicación y sus helpers de Organization Users. | [docs/features/organizaciones.md](../features/organizaciones.md) | `src/ArquitecturaBaseMultitenant.Application/Services/OrganizationUsers/CompanyInvitationPolicy.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Services/OrganizationUsers/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Services/Personal/` | Servicios de aplicación y sus helpers de acceso personal. | [docs/features/personal.md](../features/personal.md) | `src/ArquitecturaBaseMultitenant.Application/Services/Roles/RoleService.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Services/Personal/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Services/Platform/` | Servicios de aplicación y sus helpers de Plataforma. | [docs/features/plataforma.md](../features/plataforma.md) | `src/ArquitecturaBaseMultitenant.Application/Services/Platform/PlatformAccountDeletionService.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Services/Platform/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Services/Profile/` | Servicios de aplicación y sus helpers de Profile. | [docs/rules/capas-y-flujo.md](../rules/capas-y-flujo.md) | `src/ArquitecturaBaseMultitenant.Application/Services/Profile/ProfileEffectivePermissionsBuilder.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Services/Profile/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Services/PublicSite/` | Servicios de aplicación y sus helpers de sitio público. | [docs/features/sitio-publico.md](../features/sitio-publico.md) | `src/ArquitecturaBaseMultitenant.Application/Services/PublicSite/DirectoryService.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Services/PublicSite/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Services/ReferenceData/` | Servicios de aplicación y sus helpers de datos de referencia. | [docs/rules/datos-de-referencia.md](../rules/datos-de-referencia.md) | `src/ArquitecturaBaseMultitenant.Application/Services/ReferenceData/ReferenceDataService.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Services/ReferenceData/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Services/Roles/` | Servicios de aplicación y sus helpers de roles. | [docs/features/roles.md](../features/roles.md) | `src/ArquitecturaBaseMultitenant.Application/Services/Roles/PermissionService.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Services/Roles/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Validation/` | Validadores de entrada. | [docs/rules/validacion.md](../rules/validacion.md) | `src/ArquitecturaBaseMultitenant.Application/Validation/Roles/CreateRoleRequestValidator.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Validation/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Validation/Auditing/` | Validadores de entrada de auditoría. | [docs/rules/capas-y-flujo.md](../rules/capas-y-flujo.md) | `src/ArquitecturaBaseMultitenant.Application/Validation/Auditing/ListOrganizationAuditRequestValidator.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Validation/Auditing/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Validation/Auth/` | Validadores de entrada de ingreso. | [docs/features/identidad.md](../features/identidad.md) | `src/ArquitecturaBaseMultitenant.Application/Validation/Auth/AccountRecoveryRequestValidators.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Validation/Auth/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Validation/Companies/` | Validadores de entrada de empresas. | [docs/features/organizaciones.md](../features/organizaciones.md) | `src/ArquitecturaBaseMultitenant.Application/Validation/Companies/CompanyMemberValidators.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Validation/Companies/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Validation/Identity/` | Validadores de entrada de identidad. | [docs/features/identidad.md](../features/identidad.md) | `src/ArquitecturaBaseMultitenant.Application/Validation/Identity/AddLoginEmailRequestValidator.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Validation/Identity/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Validation/Invitations/` | Validadores de entrada de invitaciones. | [docs/features/organizaciones.md](../features/organizaciones.md) | `src/ArquitecturaBaseMultitenant.Application/Validation/Invitations/AcceptInvitationRequestValidator.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Validation/Invitations/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Validation/Legal/` | Validadores de entrada de documentos legales. | [docs/rules/capas-y-flujo.md](../rules/capas-y-flujo.md) | `src/ArquitecturaBaseMultitenant.Application/Validation/Legal/AcceptLegalRequestValidator.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Validation/Legal/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Validation/OrganizationSettings/` | Validadores de entrada de Organization Settings. | [docs/features/organizaciones.md](../features/organizaciones.md) | `src/ArquitecturaBaseMultitenant.Application/Validation/OrganizationSettings/RegisterOrganizationDomainRequestValidator.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Validation/OrganizationSettings/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Validation/OrganizationUsers/` | Validadores de entrada de Organization Users. | [docs/features/organizaciones.md](../features/organizaciones.md) | `src/ArquitecturaBaseMultitenant.Application/Validation/OrganizationUsers/OrganizationUserValidators.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Validation/OrganizationUsers/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Validation/Platform/` | Validadores de entrada de Plataforma. | [docs/features/plataforma.md](../features/plataforma.md) | `src/ArquitecturaBaseMultitenant.Application/Validation/Platform/ListPlatformAuditRequestValidator.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Validation/Platform/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Validation/Profile/` | Validadores de entrada de Profile. | [docs/rules/capas-y-flujo.md](../rules/capas-y-flujo.md) | `src/ArquitecturaBaseMultitenant.Application/Validation/Profile/UpdateMeRequestValidator.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Validation/Profile/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Validation/PublicSite/` | Validadores de entrada de sitio público. | [docs/features/sitio-publico.md](../features/sitio-publico.md) | `src/ArquitecturaBaseMultitenant.Application/Validation/PublicSite/ListDirectoryRequestValidator.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Validation/PublicSite/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Application/Validation/Roles/` | Validadores de entrada de roles. | [docs/features/roles.md](../features/roles.md) | `src/ArquitecturaBaseMultitenant.Application/Validation/Roles/CreateRoleRequestValidator.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Application/Validation/Roles/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Domain/` | Entidades, valores y errores de dominio. | [docs/rules/capas-y-flujo.md](../rules/capas-y-flujo.md) | `src/ArquitecturaBaseMultitenant.Domain/Assistant/AssistantAttachment.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Domain/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Domain/Assistant/` | Entidades, valores y errores de asistente. | [docs/features/asistente.md](../features/asistente.md) | `src/ArquitecturaBaseMultitenant.Domain/Assistant/AssistantAttachment.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Domain/Assistant/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Domain/Auditing/` | Entidades, valores y errores de auditoría. | [docs/rules/modulos.md](../rules/modulos.md) | `src/ArquitecturaBaseMultitenant.Domain/Auditing/AuditAction.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Domain/Auditing/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Domain/Authentication/` | Entidades, valores y errores de Authentication. | [docs/rules/modulos.md](../rules/modulos.md) | `src/ArquitecturaBaseMultitenant.Domain/Authentication/AccountErrors.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Domain/Authentication/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Domain/Authorization/` | Autorización de rutas y permisos HTTP. | [docs/features/roles.md](../features/roles.md) | `src/ArquitecturaBaseMultitenant.Domain/Authorization/Permissions.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Domain/Authorization/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Domain/Common/` | Entidades, valores y errores de uso común. | [docs/rules/multitenancy.md](../rules/multitenancy.md) | `src/ArquitecturaBaseMultitenant.Domain/Common/Entity.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Domain/Common/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Domain/Companies/` | Entidades, valores y errores de empresas. | [docs/features/organizaciones.md](../features/organizaciones.md) | `src/ArquitecturaBaseMultitenant.Domain/Tenancy/Company.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Domain/Companies/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Domain/Features/` | Entidades, valores y errores de Features. | [docs/rules/modulos-habilitados.md](../rules/modulos-habilitados.md) | `src/ArquitecturaBaseMultitenant.Domain/Features/Features.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Domain/Features/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Domain/Invitations/` | Entidades, valores y errores de invitaciones. | [docs/features/organizaciones.md](../features/organizaciones.md) | `src/ArquitecturaBaseMultitenant.Domain/Invitations/Invitation.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Domain/Invitations/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Domain/Legal/` | Entidades, valores y errores de documentos legales. | [docs/rules/modulos.md](../rules/modulos.md) | `src/ArquitecturaBaseMultitenant.Domain/Legal/AccountDeletionCancelTicket.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Domain/Legal/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Domain/Messaging/` | Entidades, valores y errores de Messaging. | [docs/rules/modulos.md](../rules/modulos.md) | `src/ArquitecturaBaseMultitenant.Domain/Messaging/OutboxChannel.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Domain/Messaging/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Domain/Platform/` | Entidades, valores y errores de Plataforma. | [docs/features/plataforma.md](../features/plataforma.md) | `src/ArquitecturaBaseMultitenant.Domain/Platform/AccountRecoveryErrors.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Domain/Platform/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Domain/PublicSite/` | Entidades, valores y errores de sitio público. | [docs/features/sitio-publico.md](../features/sitio-publico.md) | `src/ArquitecturaBaseMultitenant.Domain/PublicSite/PublicPage.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Domain/PublicSite/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Domain/ReferenceData/` | Entidades, valores y errores de datos de referencia. | [docs/rules/datos-de-referencia.md](../rules/datos-de-referencia.md) | `src/ArquitecturaBaseMultitenant.Domain/ReferenceData/Currency.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Domain/ReferenceData/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Domain/Results/` | Entidades, valores y errores de Results. | [docs/rules/modulos.md](../rules/modulos.md) | `src/ArquitecturaBaseMultitenant.Domain/Results/ContractErrors.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Domain/Results/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Domain/Settings/` | Entidades, valores y errores de Settings. | [docs/rules/modulos.md](../rules/modulos.md) | `src/ArquitecturaBaseMultitenant.Domain/Settings/BusinessSignupMode.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Domain/Settings/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Domain/Tenancy/` | Resolución de organización y subdominio para la petición. | [docs/features/organizaciones.md](../features/organizaciones.md) | `src/ArquitecturaBaseMultitenant.Domain/Tenancy/AccessErrors.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Domain/Tenancy/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Domain/Users/` | Entidades, valores y errores de Users. | [docs/rules/modulos.md](../rules/modulos.md) | `src/ArquitecturaBaseMultitenant.Domain/Users/Access.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Domain/Users/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Domain/ValueObjects/` | Entidades, valores y errores de Value Objects. | [docs/rules/numeros-y-moneda.md](../rules/numeros-y-moneda.md) | `src/ArquitecturaBaseMultitenant.Domain/ValueObjects/Money.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Domain/ValueObjects/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Domain/ValueObjects/TaxIdValidators/` | Entidades, valores y errores de Tax Id Validators. | [docs/rules/identificacion-fiscal.md](../rules/identificacion-fiscal.md) | `src/ArquitecturaBaseMultitenant.Domain/ValueObjects/TaxIdValidators/ArgentineCuitValidator.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Domain/ValueObjects/TaxIdValidators/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Domain/WhatsApp/` | Entidades, valores y errores de WhatsApp. | [docs/features/whatsapp.md](../features/whatsapp.md) | `src/ArquitecturaBaseMultitenant.Domain/WhatsApp/WhatsAppCodeErrors.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Domain/WhatsApp/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Infrastructure/` | Adaptadores técnicos y persistencia. | [docs/rules/capas-y-flujo.md](../rules/capas-y-flujo.md) | `src/ArquitecturaBaseMultitenant.Infrastructure/BackgroundJobs/AccountDeletionWorker.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Infrastructure/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Infrastructure/BackgroundJobs/` | Adaptadores técnicos de Background Jobs. | [docs/rules/multitenancy.md](../rules/multitenancy.md) | `src/ArquitecturaBaseMultitenant.Infrastructure/BackgroundJobs/TenantJobRunner.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Infrastructure/BackgroundJobs/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Infrastructure/Caching/` | Adaptadores técnicos de caché. | [docs/rules/multitenancy.md](../rules/multitenancy.md) | `src/ArquitecturaBaseMultitenant.Infrastructure/Caching/HybridCacheExtensions.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Infrastructure/Caching/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Infrastructure/Features/` | Adaptadores técnicos de Features. | [docs/rules/modulos-habilitados.md](../rules/modulos-habilitados.md) | `src/ArquitecturaBaseMultitenant.Infrastructure/Features/TenantFeatureFilter.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Infrastructure/Features/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Infrastructure/Help/` | Adaptadores técnicos de Help. | [docs/rules/capas-y-flujo.md](../rules/capas-y-flujo.md) | `src/ArquitecturaBaseMultitenant.Infrastructure/Help/RepositoryHelpArticleReader.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Infrastructure/Help/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Infrastructure/Idempotency/` | Adaptadores técnicos de idempotencia. | [docs/rules/idempotencia.md](../rules/idempotencia.md) | `src/ArquitecturaBaseMultitenant.Infrastructure/Idempotency/IdempotencyStore.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Infrastructure/Idempotency/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Infrastructure/Identity/` | Adaptadores técnicos de identidad. | [docs/features/identidad.md](../features/identidad.md) | `src/ArquitecturaBaseMultitenant.Infrastructure/Identity/ApplicationUser.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Infrastructure/Identity/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Infrastructure/Identity/Deletion/` | Adaptadores técnicos de identidad / Deletion. | [docs/rules/datos-personales.md](../rules/datos-personales.md) | `src/ArquitecturaBaseMultitenant.Infrastructure/Identity/Deletion/LegalAcceptanceDeletionParticipant.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Infrastructure/Identity/Deletion/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Infrastructure/Messaging/` | Adaptadores técnicos de Messaging. | [docs/rules/guardado.md](../rules/guardado.md) | `src/ArquitecturaBaseMultitenant.Infrastructure/Messaging/Outbox.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Infrastructure/Messaging/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Infrastructure/Messaging/Email/Templates/` | Adaptadores técnicos de Messaging / correo / Templates. | [docs/rules/textos-y-traducciones.md](../rules/textos-y-traducciones.md) | `src/ArquitecturaBaseMultitenant.Infrastructure/Messaging/Email/Templates/LoginCode.html` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Infrastructure/Messaging/Email/Templates/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Infrastructure/Modules/` | Módulos opcionales y su composición en la capa. | [docs/rules/capas-y-flujo.md](../rules/capas-y-flujo.md) | `src/ArquitecturaBaseMultitenant.Infrastructure/Modules/Assistant/AnthropicChatClientFactory.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Infrastructure/Modules/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Infrastructure/Modules/Assistant/` | Adaptadores técnicos de módulos / asistente. | [docs/features/asistente.md](../features/asistente.md) | `src/ArquitecturaBaseMultitenant.Infrastructure/Modules/Assistant/AnthropicChatClientFactory.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Infrastructure/Modules/Assistant/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Infrastructure/Modules/WhatsApp/` | Adaptadores técnicos de módulos / WhatsApp. | [docs/features/whatsapp.md](../features/whatsapp.md) | `src/ArquitecturaBaseMultitenant.Infrastructure/Modules/WhatsApp/Disabled/DisabledWhatsAppAvailability.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Infrastructure/Modules/WhatsApp/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/` | Adaptadores técnicos de persistencia. | [docs/rules/persistencia-ef.md](../rules/persistencia-ef.md) | `src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/TenantContext.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/Configurations/Identity/` | Configuraciones EF del esquema identidad. | [docs/features/identidad.md](../features/identidad.md) | `src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/Configurations/Identity/AccountDeletionCancelTicketConfiguration.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/Configurations/Identity/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/Configurations/Platform/` | Configuraciones EF del esquema Plataforma. | [docs/features/plataforma.md](../features/plataforma.md) | `src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/Configurations/Platform/AccountRecoveryNoticeConfiguration.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/Configurations/Platform/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/Configurations/Platform/ReferenceData/` | Configuraciones EF del esquema datos de referencia. | [docs/rules/datos-de-referencia.md](../rules/datos-de-referencia.md) | `src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/Configurations/Platform/ReferenceData/CurrencyConfiguration.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/Configurations/Platform/ReferenceData/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/Configurations/PublicSite/` | Configuraciones EF del esquema sitio público. | [docs/features/sitio-publico.md](../features/sitio-publico.md) | `src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/Configurations/PublicSite/PublicPageConfiguration.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/Configurations/PublicSite/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/Configurations/Tenant/` | Configuraciones EF del esquema Tenant. | [docs/rules/persistencia-ef.md](../rules/persistencia-ef.md) | `src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/Configurations/Tenant/AuditEntryConfiguration.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/Configurations/Tenant/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/Migrations/` | Migraciones EF y snapshot del modelo de persistencia. | [docs/rules/persistencia-ef.md](../rules/persistencia-ef.md) | `src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/Migrations/20260928234948_InitialSchema.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/Migrations/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/Readers/` | Lectores EF. | [docs/rules/datos-de-referencia.md](../rules/datos-de-referencia.md) | `src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/Readers/ReferenceDataReader.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/Readers/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/Repositories/` | Repositorios EF. | [docs/rules/guardado.md](../rules/guardado.md) | `src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/Repositories/RoleRepository.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/Repositories/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/Seed/ReferenceData/` | Adaptadores técnicos de persistencia / Seed / datos de referencia. | [docs/rules/datos-de-referencia.md](../rules/datos-de-referencia.md) | `src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/Seed/ReferenceData/currencies.json` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/Seed/ReferenceData/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Infrastructure/Phones/` | Adaptadores técnicos de teléfonos. | [docs/rules/telefonos.md](../rules/telefonos.md) | `src/ArquitecturaBaseMultitenant.Infrastructure/Phones/LibPhoneNumberDisplayFormatter.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Infrastructure/Phones/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Infrastructure/ReferenceData/` | Adaptadores técnicos de datos de referencia. | [docs/rules/datos-de-referencia.md](../rules/datos-de-referencia.md) | `src/ArquitecturaBaseMultitenant.Infrastructure/ReferenceData/JsonReferenceDataCatalog.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Infrastructure/ReferenceData/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Infrastructure/RequestContext/` | Adaptadores técnicos de Request Context. | [docs/rules/capas-y-flujo.md](../rules/capas-y-flujo.md) | `src/ArquitecturaBaseMultitenant.Infrastructure/RequestContext/ExecutionActorContext.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Infrastructure/RequestContext/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Infrastructure/Security/` | Adaptadores técnicos de seguridad. | [docs/rules/capas-y-flujo.md](../rules/capas-y-flujo.md) | `src/ArquitecturaBaseMultitenant.Infrastructure/Security/InvitationTokenProtector.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Infrastructure/Security/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Infrastructure/Time/` | Adaptadores técnicos de tiempo. | [docs/rules/capas-y-flujo.md](../rules/capas-y-flujo.md) | `src/ArquitecturaBaseMultitenant.Infrastructure/Time/TimeZoneService.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Infrastructure/Time/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.ServiceDefaults/` | Configuración común de observabilidad, resiliencia y salud. | [docs/rules/capas-y-flujo.md](../rules/capas-y-flujo.md) | `src/ArquitecturaBaseMultitenant.ServiceDefaults/Extensions.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.ServiceDefaults/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/` | Proyecto de pruebas de integración. | [docs/rules/capas-y-flujo.md](../rules/capas-y-flujo.md) | `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Organization/RolesTests.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Modules/Assistant/` | Pruebas de integración: módulos / asistente. | [docs/features/asistente.md](../features/asistente.md) | `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Modules/Assistant/AssistantTestData.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Modules/Assistant/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Modules/TechnicalContribution/` | Pruebas de integración: módulos / Technical Contribution. | [docs/rules/capas-y-flujo.md](../rules/capas-y-flujo.md) | `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Modules/TechnicalContribution/Persistence/TechnicalContribution.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Modules/TechnicalContribution/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Modules/WhatsApp/` | Pruebas de integración: módulos / WhatsApp. | [docs/features/whatsapp.md](../features/whatsapp.md) | `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Modules/WhatsApp/TestControllerApplicationPart.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Modules/WhatsApp/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Application.UnitTests/` | Proyecto de pruebas de unidad. | [docs/rules/capas-y-flujo.md](../rules/capas-y-flujo.md) | `tests/ArquitecturaBaseMultitenant.Application.UnitTests/ModuleServiceControlProbe.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Application.UnitTests/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Application.UnitTests/Modules/Assistant/` | Pruebas de unidad: módulos / asistente. | [docs/features/asistente.md](../features/asistente.md) | `tests/ArquitecturaBaseMultitenant.Application.UnitTests/Modules/Assistant/AssistantContractValidationTests.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Application.UnitTests/Modules/Assistant/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Application.UnitTests/Modules/WhatsApp/` | Pruebas de unidad: módulos / WhatsApp. | [docs/features/whatsapp.md](../features/whatsapp.md) | `tests/ArquitecturaBaseMultitenant.Application.UnitTests/Modules/WhatsApp/DependencyInjectionTests.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Application.UnitTests/Modules/WhatsApp/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.ArchitectureTests/` | Proyecto de pruebas de arquitectura. | [docs/rules/capas-y-flujo.md](../rules/capas-y-flujo.md) | `tests/ArquitecturaBaseMultitenant.ArchitectureTests/ApplicationNamespaceShape.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.ArchitectureTests/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.ArchitectureTests/Modules/Assistant/` | Pruebas de arquitectura: módulos / asistente. | [docs/features/asistente.md](../features/asistente.md) | `tests/ArquitecturaBaseMultitenant.ArchitectureTests/Modules/Assistant/ApplicationServicesTests.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.ArchitectureTests/Modules/Assistant/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.ArchitectureTests/Modules/WhatsApp/` | Pruebas de arquitectura: módulos / WhatsApp. | [docs/features/whatsapp.md](../features/whatsapp.md) | `tests/ArquitecturaBaseMultitenant.ArchitectureTests/Modules/WhatsApp/AccessDeclarationTests.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.ArchitectureTests/Modules/WhatsApp/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Domain.UnitTests/` | Proyecto de pruebas de unidad. | [docs/rules/capas-y-flujo.md](../rules/capas-y-flujo.md) | `tests/ArquitecturaBaseMultitenant.Domain.UnitTests/Authorization/RoleAssignmentTests.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Domain.UnitTests/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Api/` | Pruebas de integración: Api. | [docs/rules/tests.md](../rules/tests.md) | `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Api/IdempotencyModeContractTests.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Api/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Auth/` | Pruebas de integración: ingreso. | [docs/rules/tests.md](../rules/tests.md) | `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Auth/AccountGoogleProtocolTests.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Auth/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Contracts/` | Pruebas de integración: Contracts. | [docs/rules/tests.md](../rules/tests.md) | `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Contracts/Etapa8ContractTests.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Contracts/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/ErrorHandling/` | Pruebas de integración: Error Handling. | [docs/rules/tests.md](../rules/tests.md) | `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/ErrorHandling/CursorErrorMappingTests.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/ErrorHandling/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Features/` | Pruebas de integración: Features. | [docs/rules/tests.md](../rules/tests.md) | `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Features/FeatureAvailabilityPolicyTests.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Features/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Hosting/` | Pruebas de integración: Hosting. | [docs/rules/tests.md](../rules/tests.md) | `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Hosting/HealthCheckTests.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Hosting/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Identity/` | Pruebas de integración: identidad. | [docs/rules/tests.md](../rules/tests.md) | `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Identity/AccountMethodsHttpTests.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Identity/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Json/` | Pruebas de integración: Json. | [docs/rules/tests.md](../rules/tests.md) | `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Json/DateOnlyTimeOnlyTests.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Json/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Legal/` | Pruebas de integración: documentos legales. | [docs/rules/tests.md](../rules/tests.md) | `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Legal/AccountDeletionLoginTests.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Legal/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Localization/` | Pruebas de integración: Localization. | [docs/rules/tests.md](../rules/tests.md) | `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Localization/LocalizationTests.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Localization/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Messaging/` | Pruebas de integración: Messaging. | [docs/rules/tests.md](../rules/tests.md) | `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Messaging/AccountNoticeTests.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Messaging/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Modules/` | Pruebas de integración: módulos. | [docs/rules/tests.md](../rules/tests.md) | `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Modules/Assistant/AssistantAuthorizationTests.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Modules/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Organization/` | Pruebas de integración: organización. | [docs/rules/tests.md](../rules/tests.md) | `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Organization/CompaniesTests.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Organization/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Persistence/` | Pruebas de integración: persistencia. | [docs/rules/tests.md](../rules/tests.md) | `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Persistence/AccountRecoveryNoticeMigrationTests.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Persistence/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Platform/` | Pruebas de integración: Plataforma. | [docs/rules/tests.md](../rules/tests.md) | `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Platform/PlatformAccountAdministrationTests.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Platform/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/PublicSite/` | Pruebas de integración: sitio público. | [docs/rules/tests.md](../rules/tests.md) | `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/PublicSite/DirectoryTests.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/PublicSite/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/ReferenceData/` | Pruebas de integración: datos de referencia. | [docs/rules/tests.md](../rules/tests.md) | `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/ReferenceData/ReferenceDataApiTests.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/ReferenceData/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Security/` | Pruebas de integración: seguridad. | [docs/rules/tests.md](../rules/tests.md) | `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Security/LoginCodeSecurityTests.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Security/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Support/` | Pruebas de integración: Support. | [docs/rules/tests.md](../rules/tests.md) | `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Support/AccountJourney.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Support/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Tenancy/` | Pruebas de integración: Tenancy. | [docs/rules/tests.md](../rules/tests.md) | `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Tenancy/AccessStatusCacheTests.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Tenancy/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/TestFeatures/` | Pruebas de integración: Test Features. | [docs/rules/tests.md](../rules/tests.md) | `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/TestFeatures/Access/BusinessOnlyController.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/TestFeatures/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Time/` | Pruebas de integración: tiempo. | [docs/rules/tests.md](../rules/tests.md) | `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Time/TimeZoneServiceTests.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Time/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Application.UnitTests/Common/` | Pruebas de unidad: uso común. | [docs/rules/tests.md](../rules/tests.md) | `tests/ArquitecturaBaseMultitenant.Application.UnitTests/Common/CultureProfilesTests.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Application.UnitTests/Common/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Application.UnitTests/Formatting/` | Pruebas de unidad: Formatting. | [docs/rules/tests.md](../rules/tests.md) | `tests/ArquitecturaBaseMultitenant.Application.UnitTests/Formatting/VersionWireFormatterTests.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Application.UnitTests/Formatting/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Application.UnitTests/Identity/` | Pruebas de unidad: identidad. | [docs/rules/tests.md](../rules/tests.md) | `tests/ArquitecturaBaseMultitenant.Application.UnitTests/Identity/AccountDeletionPolicyTests.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Application.UnitTests/Identity/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Application.UnitTests/Messaging/` | Pruebas de unidad: Messaging. | [docs/rules/tests.md](../rules/tests.md) | `tests/ArquitecturaBaseMultitenant.Application.UnitTests/Messaging/ChannelContractsTests.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Application.UnitTests/Messaging/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Application.UnitTests/Modules/` | Pruebas de unidad: módulos. | [docs/rules/tests.md](../rules/tests.md) | `tests/ArquitecturaBaseMultitenant.Application.UnitTests/Modules/Assistant/AssistantContractValidationTests.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Application.UnitTests/Modules/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Application.UnitTests/Persistence/` | Pruebas de unidad: persistencia. | [docs/rules/tests.md](../rules/tests.md) | `tests/ArquitecturaBaseMultitenant.Application.UnitTests/Persistence/FakeUnitOfWorkTests.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Application.UnitTests/Persistence/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Application.UnitTests/ReferenceData/` | Pruebas de unidad: datos de referencia. | [docs/rules/tests.md](../rules/tests.md) | `tests/ArquitecturaBaseMultitenant.Application.UnitTests/ReferenceData/ReferenceDataCatalogTests.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Application.UnitTests/ReferenceData/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Application.UnitTests/Resources/` | Pruebas de unidad: Resources. | [docs/rules/tests.md](../rules/tests.md) | `tests/ArquitecturaBaseMultitenant.Application.UnitTests/Resources/ErrorTextsTests.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Application.UnitTests/Resources/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Application.UnitTests/Services/` | Pruebas de unidad: Services. | [docs/rules/tests.md](../rules/tests.md) | `tests/ArquitecturaBaseMultitenant.Application.UnitTests/Services/Auth/AccessSwitchPolicyTests.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Application.UnitTests/Services/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Application.UnitTests/TestDoubles/` | Pruebas de unidad: Test Doubles. | [docs/rules/tests.md](../rules/tests.md) | `tests/ArquitecturaBaseMultitenant.Application.UnitTests/TestDoubles/FakeTenantContext.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Application.UnitTests/TestDoubles/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.ArchitectureTests/Modules/` | Pruebas de arquitectura: módulos. | [docs/rules/tests.md](../rules/tests.md) | `tests/ArquitecturaBaseMultitenant.ArchitectureTests/Modules/Assistant/ApplicationServicesTests.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.ArchitectureTests/Modules/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.ArchitectureTests/Support/` | Pruebas de arquitectura: Support. | [docs/rules/tests.md](../rules/tests.md) | `tests/ArquitecturaBaseMultitenant.ArchitectureTests/Support/CallSites.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.ArchitectureTests/Support/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Domain.UnitTests/Assistant/` | Pruebas de unidad: asistente. | [docs/rules/tests.md](../rules/tests.md) | `tests/ArquitecturaBaseMultitenant.Domain.UnitTests/Assistant/AssistantSnapshotRetentionTests.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Domain.UnitTests/Assistant/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Domain.UnitTests/Auditing/` | Pruebas de unidad: auditoría. | [docs/rules/tests.md](../rules/tests.md) | `tests/ArquitecturaBaseMultitenant.Domain.UnitTests/Auditing/AuditEntryTests.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Domain.UnitTests/Auditing/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Domain.UnitTests/Authentication/` | Pruebas de unidad: Authentication. | [docs/rules/tests.md](../rules/tests.md) | `tests/ArquitecturaBaseMultitenant.Domain.UnitTests/Authentication/LoginAuditTests.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Domain.UnitTests/Authentication/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Domain.UnitTests/Authorization/` | Pruebas de unidad: roles y permisos. | [docs/rules/tests.md](../rules/tests.md) | `tests/ArquitecturaBaseMultitenant.Domain.UnitTests/Authorization/RoleAssignmentTests.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Domain.UnitTests/Authorization/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Domain.UnitTests/Common/` | Pruebas de unidad: uso común. | [docs/rules/tests.md](../rules/tests.md) | `tests/ArquitecturaBaseMultitenant.Domain.UnitTests/Common/EntityAndValueObjectTests.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Domain.UnitTests/Common/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Domain.UnitTests/Features/` | Pruebas de unidad: Features. | [docs/rules/tests.md](../rules/tests.md) | `tests/ArquitecturaBaseMultitenant.Domain.UnitTests/Features/TenantFeatureTests.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Domain.UnitTests/Features/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Domain.UnitTests/Invitations/` | Pruebas de unidad: invitaciones. | [docs/rules/tests.md](../rules/tests.md) | `tests/ArquitecturaBaseMultitenant.Domain.UnitTests/Invitations/InvitationTests.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Domain.UnitTests/Invitations/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Domain.UnitTests/Legal/` | Pruebas de unidad: documentos legales. | [docs/rules/tests.md](../rules/tests.md) | `tests/ArquitecturaBaseMultitenant.Domain.UnitTests/Legal/AccountDeletionCancelTicketTests.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Domain.UnitTests/Legal/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Domain.UnitTests/Messaging/` | Pruebas de unidad: Messaging. | [docs/rules/tests.md](../rules/tests.md) | `tests/ArquitecturaBaseMultitenant.Domain.UnitTests/Messaging/OutboxMessageTests.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Domain.UnitTests/Messaging/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Domain.UnitTests/Platform/` | Pruebas de unidad: Plataforma. | [docs/rules/tests.md](../rules/tests.md) | `tests/ArquitecturaBaseMultitenant.Domain.UnitTests/Platform/AccountRecoveryRequestTests.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Domain.UnitTests/Platform/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Domain.UnitTests/PublicSite/` | Pruebas de unidad: sitio público. | [docs/rules/tests.md](../rules/tests.md) | `tests/ArquitecturaBaseMultitenant.Domain.UnitTests/PublicSite/PublicPageTests.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Domain.UnitTests/PublicSite/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Domain.UnitTests/ReferenceData/` | Pruebas de unidad: datos de referencia. | [docs/rules/tests.md](../rules/tests.md) | `tests/ArquitecturaBaseMultitenant.Domain.UnitTests/ReferenceData/ReferenceEntitiesTests.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Domain.UnitTests/ReferenceData/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Domain.UnitTests/Results/` | Pruebas de unidad: Results. | [docs/rules/tests.md](../rules/tests.md) | `tests/ArquitecturaBaseMultitenant.Domain.UnitTests/Results/ResultTests.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Domain.UnitTests/Results/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Domain.UnitTests/Settings/` | Pruebas de unidad: Settings. | [docs/rules/tests.md](../rules/tests.md) | `tests/ArquitecturaBaseMultitenant.Domain.UnitTests/Settings/PlatformSettingsTests.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Domain.UnitTests/Settings/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Domain.UnitTests/Tenancy/` | Pruebas de unidad: Tenancy. | [docs/rules/tests.md](../rules/tests.md) | `tests/ArquitecturaBaseMultitenant.Domain.UnitTests/Tenancy/CompanyTests.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Domain.UnitTests/Tenancy/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Domain.UnitTests/Users/` | Pruebas de unidad: Users. | [docs/rules/tests.md](../rules/tests.md) | `tests/ArquitecturaBaseMultitenant.Domain.UnitTests/Users/AccountLifecycleTests.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Domain.UnitTests/Users/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Domain.UnitTests/ValueObjects/` | Pruebas de unidad: Value Objects. | [docs/rules/tests.md](../rules/tests.md) | `tests/ArquitecturaBaseMultitenant.Domain.UnitTests/ValueObjects/CultureCodeTests.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Domain.UnitTests/ValueObjects/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Domain.UnitTests/WhatsApp/` | Pruebas de unidad: WhatsApp. | [docs/rules/tests.md](../rules/tests.md) | `tests/ArquitecturaBaseMultitenant.Domain.UnitTests/WhatsApp/WhatsAppMessageTests.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Domain.UnitTests/WhatsApp/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Infrastructure/Files/` | Almacenamiento privado local y journal de compensación. | [docs/rules/capas-y-flujo.md](../rules/capas-y-flujo.md) | `src/ArquitecturaBaseMultitenant.Infrastructure/Files/LocalFileStore.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Infrastructure/Files/AGENTS.md) |
| `src/ArquitecturaBaseMultitenant.Infrastructure/Modules/Assistant/Export/` | Exportación Markdown/PDF de chats del asistente. | [docs/rules/capas-y-flujo.md](../rules/capas-y-flujo.md) | `src/ArquitecturaBaseMultitenant.Infrastructure/Modules/Assistant/Export/AssistantExportRenderer.cs` | [AGENTS.md](../../src/ArquitecturaBaseMultitenant.Infrastructure/Modules/Assistant/Export/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Files/` | Pruebas del almacenamiento privado y sus compensaciones. | [docs/rules/tests.md](../rules/tests.md) | `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Files/LocalFileStoreTests.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Files/AGENTS.md) |
| `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/TestFeatures/AssistantBrowser/` | Fixture privada del recorrido del asistente en Testing aislado. | [docs/rules/tests.md](../rules/tests.md) | `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/TestFeatures/AssistantBrowser/AssistantFileBridge.cs` | [AGENTS.md](../../tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/TestFeatures/AssistantBrowser/AGENTS.md) |
