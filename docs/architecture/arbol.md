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


Cada fila es una carpeta que lleva `AGENTS.md` + `CLAUDE.md`. Los punteros se escriben **al crear la carpeta**, en la misma tarea, en la etapa del plan donde nace. La primera parte de cada ruta indica la capa: `Domain/X` significa `src/ArquitecturaBaseMultitenant.Domain/X`, `Application/X` significa `src/ArquitecturaBaseMultitenant.Application/X`, y lo mismo para Infrastructure y Api.

| Carpeta | Qué va / qué no | Fichas | Copiá de |
|---|---|---|---|
| `Domain/` | entidades, value objects, `<X>Errors`, catálogos. Sin paquetes, sin EF ni Identity | capas-y-flujo, result-y-errores, persistencia-ef | `Domain/Authorization/Role.cs` (E4) |
| `Domain/Common/` | `Entity`, `ValueObject`; marcas `IAuditable`, `ISoftDeletable`, `IVersioned` y las de las tres clases de datos: `ITenantOwned` (+ `ICompanyOwned`), `IPublishedByBusiness`, `IConsumerBusinessShared`; `Party`/`PartyPolicy`, `NotAuditedAttribute` y `TextLimits`. **No** se agregan marcas sin ADR | multitenancy, auditoria, concurrencia, textos-libres | — |
| `Domain/ValueObjects/` | `Money`, `Email`, `PhoneNumber`, `TaxId`: un dato con forma propia es un value object, nunca un `string` suelto | numeros-y-moneda, emails, telefonos, identificacion-fiscal | `Money.cs`, `Email.cs` (E1) |
| `Domain/ValueObjects/TaxIdValidators/` | Algoritmos de identificación fiscal puros; los tipos habilitados salen del catálogo en Application | identificacion-fiscal, datos-de-referencia | `ArgentineCuitValidator.cs` (E6) |
| `Domain/ReferenceData/` | entidades globales de los cinco catálogos y sus traducciones, sin dependencia de Application ni RLS | datos-de-referencia, capas-y-flujo | `Currency.cs` (E2) |
| `Domain/Features/` | catálogo de módulos | modulos-habilitados | `Features.cs` (E5) |
| `Domain/<Módulo>/` | entidades y `<X>Errors` del módulo (`Domain/WhatsApp/` nace en la E8); se borra junto con sus `Modules/` (E8) | modulos | — |
| `Application/Services/` | servicio + helpers con un sufijo de la lista cerrada de capas-y-flujo (`Policy`, `Guard`, `Issuer`, `Verifier`, `Linker` y los demás de la tabla) | guardado, result-y-errores, validacion, logs, multitenancy, capas-y-flujo | `Services/Roles/RoleService.cs` (E4) |
| `Application/Interfaces/Services/` | una interfaz por servicio; lo único que inyecta un controller | capas-y-flujo | `IRoleService.cs` (E4) |
| `Application/Interfaces/Persistence/` | `I<X>Repository` (escribe), `I<X>Reader` (lee), `IUnitOfWork`, `ITenantScope`. Nada de `IQueryable` | persistencia-ef, paginado-y-busqueda | `IRoleRepository.cs`, `IRoleReader.cs` (E4) |
| `Application/Interfaces/ReferenceData/` | puertos de moneda, país, zona, cultura y tipo fiscal; Domain nunca los referencia | datos-de-referencia, capas-y-flujo | `ICurrencyCatalog.cs` (E1) |
| `Application/Interfaces/Integrations/Phones/` | puerto de formato de lectura E1; parser de entrada E3 | telefonos, capas-y-flujo | `IPhoneNumberDisplayFormatter.cs` (E1) |
| `Application/Interfaces/Integrations/` | puertos a lo externo, por tema | capas-y-flujo, modulos | — |
| `Application/Models/` | `*Request`, `*Response`, `ReadModels/*Row`; montos en `Money`, fechas `*Utc`/`DateOnly` | numeros-y-moneda, fechas-y-zonas, paginado-y-busqueda | `Models/Roles/` (E4) |
| `Application/Models/ReferenceData/` | respuesta de los cinco catálogos traducidos; no persistencia ni listas fijas | datos-de-referencia, textos-y-traducciones | `ReferenceDataResponse.cs` (E1) |
| `Application/Validation/` | un validador por request, con `ValidationRules` | validacion, textos-y-traducciones | `CreateRoleRequestValidator.cs` (E4) |
| `Application/Services/ReferenceData/` | servicio que combina los puertos y entrega los catálogos a la Api | datos-de-referencia, capas-y-flujo | `ReferenceDataService.cs` (E1) |
| `Application/Resources/` | `.resx` es + en; la clave de un error es su código | textos-y-traducciones | `Errors.resx` (E1) |
| `Application/Common/Formatting/` | `DisplayFormatter` y perfiles de cultura. **Único** lugar que formatea en el back | numeros-y-moneda, fechas-y-zonas | — |
| `Application/Common/Exceptions/` | excepciones técnicas que Infrastructure le pasa a Application (concurrencia, índice único, clave foránea, cursor); las reglas de negocio devuelven `Result` | result-y-errores, concurrencia | `UniqueConstraintViolationException.cs` (E2) |
| `Application/Common/Logging/` | `OperationLog` y sus `[LoggerMessage]`: inicio, fin y código de error de cada método público de un servicio | logs | `OperationLog.cs` (E1) |
| `Application/Configuration/<Área>/` | opciones de Application por área, validadas al arrancar y sin secretos; se registran en el `*Registration.cs` de su área | capas-y-flujo, fechas-y-zonas | `Auth/LoginCodeOptions.cs` (E3) |
| `Application/Modules/<Módulo>/` | un módulo quitable; el núcleo no lo referencia | modulos | `Modules/WhatsApp/` (E8) |
| `Infrastructure/Persistence/` | `ApplicationDbContext`, `UnitOfWork`, `TenantContext` y los límites de persistencia; el tenant nunca sale del host ni de datos HTTP | persistencia-ef, multitenancy, guardado | `TenantContext.cs` (E2) |
| `Infrastructure/Persistence/Configurations/<Esquema>/` | una `IEntityTypeConfiguration` por entidad; `decimal` con precisión; enums como texto | persistencia-ef, numeros-y-moneda, multitenancy | `Tenant/RoleConfiguration.cs` (E4) |
| `Infrastructure/Persistence/Configurations/Platform/ReferenceData/` | una configuración EF por tabla de referencia global, sin RLS | datos-de-referencia, persistencia-ef | `CurrencyConfiguration.cs` (E2) |
| `Infrastructure/Persistence/Migrations/` | se generan con el comando de la guía; cada tabla nueva llama al helper RLS de su clase (`EnableTenantRls`, `EnablePublicRls` o `EnablePartiesRls`) + índices del `SortMap` | persistencia-ef, multitenancy, paginado-y-busqueda | `docs/guides/migracion.md` (E2) |
| `Infrastructure/Persistence/Repositories/` | EF para escribir un agregado; exige la transacción del caso de uso | guardado, persistencia-ef | `RoleRepository.cs` (E4) |
| `Infrastructure/Persistence/Readers/` | proyecciones `AsNoTracking` → `*Row`; desde E2, `ReferenceDataReader` lee catálogos globales con HybridCache y scope propio; `SortMap`, `ApplySearch`, `ToPagedResultAsync` | datos-de-referencia, paginado-y-busqueda, multitenancy | `ReferenceDataReader.cs` (E2), `RoleReader.cs` (E4), `CompanyReader.cs` (E6) |
| `scripts/arneses/` | arneses ejecutables: detectan, arreglan lo mecánico y llaman a un modelo barato; sin npm; los tests no leen el repo real ni llaman a un modelo | tests | `scripts/datos-de-referencia/generar.test.mjs` |
| `Infrastructure/Persistence/Seed/ReferenceData/` | cinco JSON generados de ISO/IANA/CLDR y fuentes editables del script; no se editan a mano | datos-de-referencia | `currencies.json` (E1) |
| `scripts/datos-de-referencia/` | fuentes editables, snapshots/lock fijados y generador de los cinco JSON; tests sin red | datos-de-referencia | `generar.mjs` (E1) |
| `Infrastructure/ReferenceData/` | adaptador JSON de los cinco catálogos en E1; desde E2 solo alimenta el seed y el exportador OpenAPI, mientras el runtime usa Reader con HybridCache | datos-de-referencia, capas-y-flujo | `JsonReferenceDataCatalog.cs` (E1) |
| `Infrastructure/Phones/` | adaptador libphonenumber de presentación E1; parser de entrada E3 | telefonos, capas-y-flujo | `LibPhoneNumberDisplayFormatter.cs` (E1) |
| `Infrastructure/Persistence/Readers/Platform/` | **única** lista blanca para ignorar el filtro `"Tenant"` | multitenancy | — |
| `Infrastructure/Features/` | filtro de módulos por tenant | modulos-habilitados | `TenantFeatureFilter.cs` (E5) |
| `Infrastructure/BackgroundJobs/` | workers: un scope DI por organización y `ITenantScope.Enter` antes de abrir la transacción | multitenancy, tests | `TenantJobRunner.cs` (E2) |
| `Infrastructure/Caching/` | HybridCache: claves de `CacheKeys` con prefijo de alcance; se invalida después del commit | multitenancy, guardado | `HybridCacheExtensions.cs` (E2) |
| `Infrastructure/Idempotency/` | persistencia técnica de `[Idempotent]` en `platform`: reserva, respuesta guardada y limpieza | idempotencia, persistencia-ef | `IdempotencyStore.cs` (E2) |
| `Infrastructure/Identity/Deletion/` | participantes de la baja de una cuenta: trabajan en el alcance y la transacción que abre el worker; nunca `Enter` ni `SaveChanges` | datos-personales, multitenancy, auditoria | `MembershipDeletionParticipant.cs` (E3) |
| `Infrastructure/Messaging/` | outbox persistente y su dispatcher; el payload cifrado nunca va a los logs | guardado, logs | `Outbox.cs` (E3) |
| `Infrastructure/Messaging/Email/Templates/` | plantillas HTML embebidas: estructura y marcadores `{{Nombre}}`; el texto sale de `Notifications.resx` | textos-y-traducciones, logs | `LoginCode.html` (E3) |
| `Infrastructure/Modules/<Módulo>/` | adaptadores del módulo | modulos | — |
| `Api/Modules/<Módulo>/` | lo HTTP del módulo (su `*ApiModule`, controllers como el del webhook, convención de rutas condicional); el núcleo no lo referencia | modulos | — |
| `Api/Controllers/` | controllers finos: contrato → servicio → `ToActionResult`; `[Access]` + permiso, `[PublicSite]` (subdominio de una organización publicada) o solo `[AllowAnonymous]` si el controller está en la lista de `AccessDeclarationTests` | api-http, permisos, multitenancy | `Organization/RolesController.cs` (E4) |
| `Api/Controllers/ReferenceData/` | ruta anónima de catálogos globales, con ETag; no recibe catálogos directamente | datos-de-referencia, api-http | `ReferenceDataController.cs` (E1) |
| `Api/Contracts/` | `*HttpRequest` / `*Query`, props nullable, `ToString()` sin datos personales; los de edición y borrado traen `version` | api-http, paginado-y-busqueda, concurrencia | `Organization/RolesHttpRequests.cs` (E4) |
| `Api/Contracts/ReferenceData/` | contrato de salida de los cinco catálogos para OpenAPI | datos-de-referencia, api-http | `ReferenceDataHttpResponse.cs` (E1) |
| `Api/Idempotency/` | `[Idempotent]` y su filtro; nada más va acá | idempotencia | `IdempotencyFilter.cs` (E2) |
| `Api/Json/` | conversores globales (UTC, `Money`, texto normalizado) | textos-libres, fechas-y-zonas, numeros-y-moneda | `NormalizedStringJsonConverter.cs` (E1) |
| `Api/Features/` | puerta de los módulos opcionales: `FeatureGateMiddleware` oculta con 404 un módulo apagado; no reemplaza `[Access]` ni el permiso | modulos-habilitados, api-http, permisos | `FeatureGateMiddleware.cs` (E5) |
| `Api/Tenancy/` | acceso firmado y contexto público por host, sin mezclar organizaciones | multitenancy, permisos, api-http | `PublicSiteResolutionMiddleware.cs` (E7) |
| `Api/Legal/` | `LegalAcceptanceMiddleware`: 403 `Legal.AcceptanceRequired` hasta aceptar los documentos vigentes; no escribe ni accede a EF | datos-personales, api-http | `LegalAcceptanceMiddleware.cs` (E3) |
| `Api/RateLimiting/` | políticas de ingreso por IP (`RateLimitPolicies`), con 429 ProblemDetails y `Retry-After` | api-http | `RateLimitPolicies.cs` (E3) |
| `tests/*.Application.UnitTests/Services/` | tests del servicio con dobles a mano | tests | `Services/Roles/RoleServiceWriteTests.cs` (E4) |
| `tests/*.Api.IntegrationTests/` | rutas + aislamiento entre tenants | tests, multitenancy | `Organization/RolesTests.cs` (E4) |
| `tests/*.Api.IntegrationTests/TestFeatures/` | controllers y entidades solo para tests, expuestos por `TestControllerApplicationPart`; nunca en `src/` ni en una migración productiva | tests, multitenancy | `TestFeatures/Isolation/WidgetsController.cs` (E2) |
| `tests/*.Application.UnitTests/Persistence/` | dobles de persistencia y sus pruebas, sin base real | tests, guardado | `FakeUnitOfWorkTests.cs` (E2) |
| `tests/*.Application.UnitTests/Formatting/` | protocolo de versiones y formatos del backend bajo distintas culturas | numeros-y-moneda, tests | `VersionWireFormatterTests.cs` (E7) |
| `tests/*.Domain.UnitTests/Tenancy/` | reglas puras de organizaciones y partes de datos compartidos | multitenancy, result-y-errores, tests | `PartyPolicyTests.cs` (E7) |

`Application/Services/Personal/` y `Api/Controllers/Personal/` son carpetas reservadas del producto con sus punteros y la receta `docs/features/personal.md` (E7). Permanecen sin código de negocio en la plantilla. Sus cuatro punteros están declarados nominalmente en `scripts/arneses/realinear-docs/ignorados.json` para que el detector no los retire como carpetas abandonadas.

**Ejemplo de puntero** (`Application/Services/AGENTS.md`):

```markdown
Servicios de casos de uso. Uno por área, con helpers de un sufijo de la lista cerrada de capas-y-flujo.md (`Policy`, `Guard`, `Issuer`…).
No va acá: EF, HttpContext ni tipos de Infrastructure o Api.
Antes de escribir, leé: docs/rules/capas-y-flujo.md · guardado.md · result-y-errores.md · validacion.md · logs.md · multitenancy.md
Copiá de: Services/Roles/RoleService.cs (E4, el área de referencia).
```

**Carpeta de un área** (`Services/Roles/`, `Models/Roles/`, `Validation/Roles/`, `Domain/Authorization/`…): no es una fila del mapa. Lleva un `AGENTS.md` de una línea, "Antes de tocar esto, leé `docs/features/<área>.md`", más su `CLAUDE.md`, y se escribe cuando nace ese documento: Roles es la primera, en la Etapa 4. Las carpetas que se agrupan por acceso y no por área (`Api/Controllers/Organization/`, `Api/Contracts/Organization/`, `tests/*.Api.IntegrationTests/Organization/`) juntan varias áreas: su puntero lleva una línea por área, cada una con su `docs/features/<área>.md`, como `Api/Controllers/AGENTS.md` de ArquitecturaBase.

La correspondencia verificable entre `docs/features/` y las carpetas de área o agrupadas se define en la Etapa 4, junto con la receta de Roles; ahí se amplía `HarnessTests`. Los punteros de cada carpeta se siguen escribiendo en la etapa en que nace.

