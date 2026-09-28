# Arquitectura del backend

> Documento canónico. Si otro documento contradice a este, manda este. El aislamiento entre organizaciones tiene su propio documento, [`multitenancy.md`](multitenancy.md), y se lee junto con este.

ArquitecturaBaseMultitenant es la versión multitenant de `../ArquitecturaBase`. **Copia sus convenciones tal cual** (capas, MVC, Result, una sola forma de guardar, ProblemDetails, resources, UTC, `[LoggerMessage]`, tests de arquitectura) y les suma:
- **B2B + B2C como en Mercado Libre, para cualquier tipo de negocio:** una cuenta por persona con **dos accesos que no se mezclan**: como **persona** (B2C: su espacio personal y lo que pide a las empresas) y como **empresa** (B2B: las organizaciones donde trabaja). Una persona B2C no crea empresas; eso es el alta B2B, "Registrá tu empresa". Cada organización puede tener su **página pública en un subdominio**, y las personas interactúan con ella mediante datos compartidos entre las dos partes. La plantilla trae los accesos y la mecánica; los módulos de negocio los pone cada producto (multitenancy.md);
- el aislamiento por tenant, con dos barreras: EF y RLS de PostgreSQL;
- **una sola forma de representar y mostrar los datos** (fechas, números, moneda, porcentajes, vacíos) en todo el sistema: ver §18 y el documento de formatos del front;
- las empresas dentro de cada organización;
- el área de plataforma, para los operadores.

Las mejoras que el plan maestro de ArquitecturaBase dejó para después ya vienen resueltas: un solo `IRequestValidator`, `ISignInService` chico en lugar de `IIdentityService`, sufijos de helpers fijos, `*Row` para proyecciones y subcarpetas en `Integrations`.

---

## 1. Stack

| Tema | Elección |
|---|---|
| Runtime | .NET 10 (`global.json` SDK 10.0.400, `rollForward: latestFeature`, runner Microsoft.Testing.Platform) |
| Web | ASP.NET Core MVC (controllers). Sin Minimal APIs de negocio |
| Datos | PostgreSQL 18 + EF Core 10 (Npgsql). Una base, esquemas `platform`, `identity`, `tenant`, `public_site` y `engagement` |
| Identidad | ASP.NET Core Identity (solo usuarios, sin roles de Identity) + OpenIddict 7 (servidor y validación en la misma Api) |
| Validación | FluentValidation 12 |
| Caché | `HybridCache` |
| Orquestación local | Aspire 13.5 (AppHost + ServiceDefaults) |
| Observabilidad | `Microsoft.Extensions.Logging` + OpenTelemetry (sin Serilog) |
| Tests | xUnit v3, Testcontainers (`postgres:18.x`), NetArchTest + Mono.Cecil, `FakeTimeProvider`, dobles a mano (sin Moq ni FluentAssertions) |
| Build | `TreatWarningsAsErrors`, `AnalysisLevel=latest-recommended`, `EnforceCodeStyleInBuild`, BannedApiAnalyzers, Central Package Management |

Las versiones son las mismas de `ArquitecturaBase/Directory.Packages.props`. `NodaTime` no se usa: alcanza con `TimeZoneInfo` e IDs IANA.

---

## 2. Solución y proyectos

```
ArquitecturaBaseMultitenant.slnx
├─ src/
│  ├─ ArquitecturaBaseMultitenant.Domain            (sin paquetes, solo BCL)
│  ├─ ArquitecturaBaseMultitenant.Application       → Domain
│  ├─ ArquitecturaBaseMultitenant.Infrastructure    → Application
│  ├─ ArquitecturaBaseMultitenant.Api               → Application, Infrastructure (solo Program.cs), ServiceDefaults
│  ├─ ArquitecturaBaseMultitenant.ServiceDefaults   (IsAspireSharedProject)
│  └─ ArquitecturaBaseMultitenant.AppHost           → Api (recurso Aspire)
└─ tests/
   ├─ ArquitecturaBaseMultitenant.Domain.UnitTests
   ├─ ArquitecturaBaseMultitenant.Application.UnitTests
   ├─ ArquitecturaBaseMultitenant.Api.IntegrationTests   (Api + persistencia + RLS, con Testcontainers)
   └─ ArquitecturaBaseMultitenant.ArchitectureTests
```

**No hay carpeta `tools/`.**
- El primer operador de plataforma sale del seed (`Seed:PlatformOwner`), como el `InitialAdmin` de la base.
- El rol de base de datos en runtime lo crea el bootstrap de desarrollo, o el DBA en producción.
- Las migraciones fuera de Development se aplican con un migration bundle.

Si más adelante hace falta una CLI, la decisión va con su ADR.

### Archivos de raíz

```
.editorconfig            estilo + supresiones justificadas
.gitignore               VisualStudio + .local/, *.env, appsettings.*.local.json
AGENTS.md / CLAUDE.md    índice de reglas (CLAUDE.md importa @AGENTS.md)
BannedSymbols.txt        DateTime.Now/UtcNow/Today, DateTimeOffset.Now/UtcNow → "Usá TimeProvider inyectado"
Directory.Build.props    net10.0, Nullable, ImplicitUsings, TreatWarningsAsErrors, GenerateDocumentationFile (NoWarn CS1591)
Directory.Packages.props versiones (CPM) + GlobalPackageReference BannedApiAnalyzers
global.json              SDK + runner de tests
aspire.config.json       apunta al AppHost
README.md                cómo levantar, probar y desplegar
docs/                    architecture/ decisions/ rules/ operations/ guides/ features/ plans/ contracts/ history/
.github/workflows/       ci.yml (build + test + contrato OpenAPI)
```

---

## 3. Capas y dependencias

Lo verifica `ArchitectureTests`.

| Proyecto | Puede referenciar | Contiene |
|---|---|---|
| Domain | nada | entidades, value objects, `<Entidad>Errors`, `Result`, catálogos de permisos, enums |
| Application | Domain, FluentValidation, algunos `Microsoft.Extensions.*` (lista blanca en `ApplicationPackagesTests`) | interfaces y servicios de casos de uso, modelos `*Request/*Response/*Row`, validadores, puertos de persistencia e integración, resources |
| Infrastructure | Application, Domain | EF Core, `ApplicationDbContext`, interceptores, repositorios, readers, Identity, OpenIddict, correo, WhatsApp, outbox, workers, caché, RLS |
| Api | Application, Infrastructure (solo desde `Program.cs`), ServiceDefaults | controllers, contratos HTTP, autorización, adaptadores de la petición, manejo de errores, hosting del SPA |

**El recorrido de un request de negocio es fijo:**

```
Api/Controllers
  → Application/Interfaces/Services        (I<X>Service)
    → Application/Services/<Área>          (<X>Service + helpers)
      → Application/Interfaces/Persistence (I<X>Repository, I<X>Reader, IUnitOfWork)
      → Application/Interfaces/Integrations/<Tema>
        → Infrastructure
```

Prohibido:
- `ICommand`, `IQuery`, handlers, MediatR, `Application/Features`, repositorio genérico, Scrutor, AutoMapper, Minimal APIs de negocio;
- que `IQueryable` o `Expression<>` salgan de Infrastructure;
- que Application use `HttpContext`, EF o `ApplicationDbContext`.

### Principios SOLID aplicados (cómo se ven en el código)

| Principio | Regla concreta |
|---|---|
| **S**, responsabilidad única | Un servicio por área funcional, con **8 dependencias como máximo** (lo verifica un test). Lo que crece se parte en helpers con sufijo fijo: `*Policy` (decide), `*Guard` (verifica invariantes y devuelve `Error?`), `*Issuer` (emite códigos, enlaces o invitaciones), `*Verifier` (valida lo emitido), `*Linker` (vincula entidades). Los helpers no guardan ni reciben `IUnitOfWork` |
| **O**, abierto/cerrado | Módulos quitables (`Modules/WhatsApp` en Application, Infrastructure y Api), que se enchufan al núcleo por puertos: `ILoginCodeChannel`, `IInvitationChannel`, `IAccountNoticeChannel`, `IPhoneLinkObserver`. El núcleo no cambia cuando se agrega o se quita un módulo, y no referencia ningún `*.Modules.*` (test). Los errores y permisos nuevos se suman a catálogos, sin tocar el mapper |
| **L**, sustitución de Liskov | Las implementaciones de un puerto respetan el contrato completo, incluido el "fallo cerrado" (por ejemplo, `DisabledWhatsAppAvailability` responde "no disponible", no lanza). Los dobles de test implementan la misma semántica (`FakeUnitOfWork` usa la misma `CommitPolicy`) |
| **I**, segregación de interfaces | Lectura y escritura separadas (`I<X>Reader` / `I<X>Repository`). `ISignInService` con 12 miembros como máximo (test). `ICurrentUser`, `ITenantContext` e `IRequestInfo` separados |
| **D**, inversión de dependencias | Application define los puertos; Infrastructure y Api los implementan. Cada capa se registra en su `DependencyInjection.cs` y `Program.cs` solo compone |

---

## 4. Carpetas y archivos que marcan el patrón

El árbol completo, archivo por archivo y con la etapa en que nace cada pieza, está en [`arbol.md`](arbol.md). Si este árbol y aquel no coinciden, se corrigen los dos.

Solo se listan las carpetas y los archivos que marcan el patrón. Un área nueva copia la forma de **Roles**, que es el área de referencia ([ADR 0004](../decisions/README.md)).

### 4.1 Domain

```
Domain/
├─ Common/
│  ├─ Entity.cs                  Id Guid v7, private init; constructor protegido para EF
│  ├─ ValueObject.cs
│  ├─ IAuditable.cs              CreatedAtUtc, CreatedBy, ModifiedAtUtc, ModifiedBy
│  ├─ ISoftDeletable.cs          IsDeleted, DeletedAtUtc, DeletedBy
│  ├─ ITenantOwned.cs            dato privado: Guid TenantId (lo sella el interceptor, inmutable)
│  ├─ ICompanyOwned.cs           Guid CompanyId (siempre también ITenantOwned)
│  ├─ IPublishedByBusiness.cs    dato público: BusinessTenantId, IsPublished
│  └─ IConsumerBusinessShared.cs dato compartido: ConsumerTenantId (espacio personal), BusinessTenantId (organización)
├─ Results/                      Error, ErrorType, Result, Result<T>, ValidationError
├─ ValueObjects/                 Email, PhoneNumber, TaxId (país + tipo + número; validadores en TaxIdValidators/),
│                                Money (monto + moneda ISO 4217), CurrencyCode, CultureCode
├─ Tenancy/                      Tenant, TenantKind (Personal|Business), TenantStatus, Slug, ReservedSlugs, Member, MemberStatus,
│                                TenantErrors, MemberErrors, AccessErrors, Invitation, InvitationChannel (valor), InvitationStatus,
│                                InvitationErrors
├─ Companies/                    Company, CompanyStatus, CompanyErrors, CompanyMembership, CompanyMembershipStatus,
│                                CompanyMembershipErrors
├─ PublicSite/                   PublicPage (nombre, logo, descripción, contacto; bloqueo de la plataforma: PublishBlockedAtUtc,
│                                PublishBlockedReason, PublishBlockedByUserId), PublicPageStatus (Draft|Published), PublicPageErrors
├─ Users/                        Access (Consumer|Business|Platform), UserStatus (Active|Suspended|PendingDeletion|Deleted),
│                                UserErrors (las invitaciones están en Tenancy/)
├─ Authentication/               LoginCode, LoginLink, LoginAudit, LoginMethod, LoginCodeChannel, *Errors
├─ Authorization/
│  ├─ Permissions.cs             catálogo de la organización (tenant) y de empresa, con All / OrganizationScoped / CompanyScoped
│  ├─ PersonalPermissions.cs     catálogo personal (personal.*), implícitos de la persona en su espacio personal
│  ├─ PlatformPermissions.cs     catálogo de plataforma (separado a propósito)
│  ├─ Role.cs                    TenantId, Name, Description, Scope, CompanyId? (solo con SpecificCompany), IsSystem, Permissions
│  ├─ RoleScope.cs
│  ├─ RoleAssignment.cs          UserId, RoleId, CompanyId? (null ⇒ toda la organización)
│  ├─ SystemRoles.cs             TenantAdmin ("Dueño"), CompanyAdmin ("Administrador")
│  └─ RoleErrors.cs
├─ Platform/                     PlatformRole (Owner|Support), PlatformRoleAssignment, PlatformErrors
├─ Settings/                     TenantSettings (cultura, zona y moneda por defecto), PlatformSettings, SettingsErrors
├─ Legal/                        LegalDocument, LegalDocumentContent (una fila por cultura), LegalDocumentKind, LegalAcceptance,
│                                LegalErrors, AccountDeletionErrors (ReauthRequired, LastAdmin, PlatformOperator, AlreadyPending,
│                                Blocked, NotPending); DataExport y DataExportStatus llegan en E10
├─ Features/                     Features.cs: catálogo de módulos con su clave, su lado y si viene prendido por defecto (P8)
├─ Auditing/                     AuditEntry (tenant), SecurityEvent (plataforma), AuditAction, AuditActorKind
├─ Messaging/                    OutboxMessage, OutboxChannel (valor: "email" y los que sumen los módulos), OutboxStatus
└─ WhatsApp/                     entidades del módulo (Etapa 8): WhatsAppContact, WhatsAppMessage, WhatsAppChannel, *Errors
```

Reglas de Domain:
- Sin eventos de dominio ([ADR 0003](../decisions/README.md)).
- Fábricas estáticas que validan invariantes con `ArgumentException` (un bug) y métodos que devuelven `Result` para las reglas de negocio.
- Propiedades con `private set` y constructor privado para EF.

### 4.2 Application

```
Application/
├─ DependencyInjection.cs              AddApplication(): options, servicios, helpers y validadores, uno por uno y explícitos
├─ Common/
│  ├─ Pagination/                      PagedRequest, PagedResult<T>, CursorRequest, CursorResult<T>, SortDescriptor
│  ├─ Validation/                      IRequestValidator, RequestValidator, ValidationRules, FieldErrors, PagedRequestValidator<T>,
│  │                                   CursorRequestValidator<T>
│  ├─ Logging/                         OperationLog.RunAsync(logger, "Operación", trabajo): inicio, fin y código de error
│  ├─ Formatting/                      DisplayFormatter, CultureProfiles, SupportedCultures (§18)
│  └─ Exceptions/                      UniqueConstraintViolationException, ConcurrencyConflictException
├─ Configuration/<Área>/               opciones funcionales (LoginCodeOptions, InvitationOptions…) con SectionName
├─ Interfaces/
│  ├─ Services/                        I<X>Service, uno por área (lo que inyectan los controllers), entre ellos IAccountDeletionService
│  ├─ Persistence/                     IUnitOfWork, CommitPolicy, I<X>Repository, I<X>Reader, ITenantScope, IAuditLog,
│  │                                   ILegalRepository, ILegalReader
│  └─ Integrations/
│     ├─ Request/                      ICurrentUser, ITenantContext, IRequestInfo, IPublicOrigin, IPublicSiteContext
│     ├─ Identity/                     ISignInService, IPermissionService, IPlatformPermissionService, ITokenRevoker
│     ├─ Security/                     ISecureTokenGenerator, ILoginCodeGenerator, ILoginCodeHasher, IPayloadProtector
│     ├─ Messaging/                    IOutbox, IEmailTemplateRenderer
│     ├─ Time/                         ITimeZoneService
│     ├─ Phones/                       IPhoneNumberParser
│     ├─ Legal/                        IAccountDeletionParticipant (CheckAsync, OnRequestedAsync, OnCancelledAsync y ExecuteAsync),
│     │                                IRetainedOnConsumerDeletion (E7)
│     ├─ Features/                     IFeatureService
│     (en Messaging/ y Phones/ están los puertos que usan los módulos: ILoginCodeChannel, IInvitationChannel,
│      IAccountNoticeChannel, IPhoneLinkObserver)
├─ Models/<Área>/                      <Acción>Request, <X>Response, ReadModels/<X>Row
├─ Validation/<Área>/                  <Acción>RequestValidator (internal sealed, AbstractValidator<T>)
├─ Services/
│  ├─ Auth/                            AccountService (registro B2C), LoginCodeService, LoginLinkService, ExternalLoginService,
│  │                                   ConnectService (emisión + cambio de acceso u organización), LoginCodeIssuer, LoginCodeVerifier,
│  │                                   LoginLinkIssuer, SignupPolicy, AccessSwitchPolicy, UserCultures
│  ├─ Identity/                        ReauthVerifier: el ReauthTicket de 5 minutos que usan la baja y los cambios de métodos de ingreso
│  ├─ Invitations/                     InvitationService, InvitationIssuer (lo usan UserService y TenantAdministrationService)
│  ├─ Profile/                         ProfileService (cuenta, idioma, zona), DestinationCodeVerifier
│  ├─ Legal/                           LegalService (documentos vigentes y aceptación), AccountDeletionService y AccountDeletionPolicy
│  │                                   (pedir, cancelar y ejecutar la baja; el bloqueo del único Dueño, en E4), Participants/
│  │                                   (espacio personal, aceptaciones, outbox y Memberships en E3; Recovery en E5, Engagement
│  │                                   en E7); sus modelos y validadores van en Models/Legal/ y Validation/Legal/
│  ├─ Organizations/                   BusinessSignupService ("Registrá tu empresa", alta B2B), BusinessSignupPolicy (modo de alta
│  │                                   y límite por persona), TenantProvisioner (idempotente)
│  ├─ PublicSite/                      PublicPageService (datos de la página pública; publicar y despublicar), DirectoryService
│  ├─ Personal/                        acá van los servicios de los módulos B2C del producto
│  ├─ Users/                           UserService, UserGuard, LastTenantAdminGuard, AccountAccessRevoker
│  ├─ Roles/                           RoleService, RoleGuard           ← ÁREA DE REFERENCIA
│  ├─ Companies/                       CompanyService, CompanyGuard, CompanyMemberService, LastCompanyAdminGuard
│  ├─ Settings/                        TenantSettingsService
│  ├─ Auditing/                        AuditLogService (lectura)
│  ├─ Time/                            TimeZoneCatalogService
│  ├─ Platform/                        TenantAdministrationService, PlatformAccountService (buscar y suspender cuentas, lo que
│  │                                   revoca sus sesiones; «Dar de baja» con motivo, ADR 0035), PlatformOperatorService,
│  │                                   PlatformAuditService, PlatformSettingsService, PlatformActionGuard
├─ Modules/WhatsApp/                   módulo quitable: Configuration, Interfaces, Models, Services, Resources y WhatsAppModule.cs
└─ Resources/
   ├─ Errors.resx / Errors.en.resx             clave = código del error + Title.<ErrorType>
   ├─ Validation.resx / .en.resx
   ├─ Permissions.resx / .en.resx               Area.<x>, Permission.<code>, PermissionDescription.<code>, Role.<system>
   ├─ Notifications.resx / .en.resx             textos de correo y WhatsApp (asunto, cuerpo, bot)
   ├─ Audit.resx / .en.resx                     AuditAction.<code>, Entity.<tipo>
   └─ ErrorTexts.cs, ValidationTexts.cs, PermissionTexts.cs, NotificationTexts.cs, AuditTexts.cs
```

`NeutralLanguage=es` y `InternalsVisibleTo` para Application.UnitTests y Api.IntegrationTests. Los servicios y helpers son `internal sealed partial`: `partial` porque llevan `[LoggerMessage]`.

### 4.3 Infrastructure

```
Infrastructure/
├─ DependencyInjection.cs              AddInfrastructure(cfg, env) → llama a los *Registration
├─ Persistence/
│  ├─ ApplicationDbContext.cs          IdentityUserContext<ApplicationUser, Guid> + IDataProtectionKeyContext; esquemas y filtros
│  ├─ PersistenceRegistration.cs
│  ├─ UnitOfWork.cs                    ExecuteInTransactionAsync + SET LOCAL app.tenant_id
│  ├─ TenantContext.cs                 holder scoped (implementa ITenantContext y ITenantScope)
│  ├─ UniqueViolations.cs
│  ├─ Configurations/
│  │  ├─ Platform/                     TenantConfiguration, PlatformSettingsConfiguration, SecurityEventConfiguration,
│  │  │                                OutboxMessageConfiguration, PlatformRoleAssignmentConfiguration, IdempotencyKeyConfiguration,
│  │  │                                LegalDocumentConfiguration, LegalDocumentContentConfiguration, TenantFeatureConfiguration
│  │  ├─ Identity/                     ApplicationUserConfiguration, LoginMethodConfiguration (índice único Type+Value),
│  │  │                                LegalAcceptanceConfiguration, LoginCodeConfiguration, LoginLinkConfiguration…
│  │  ├─ Tenant/                       MemberConfiguration, CompanyConfiguration, CompanyMembershipConfiguration,
│  │  │                                RoleConfiguration, RoleAssignmentConfiguration, TenantSettingsConfiguration,
│  │  │                                AuditEntryConfiguration…
│  │  ├─ PublicSite/                   PublicPageConfiguration (esquema public_site, EnablePublicRls)
│  ├─ Interceptors/
│  │  ├─ TenantConnectionInterceptor.cs    set_config('app.tenant_id') al abrir la conexión
│  │  ├─ TenantStampInterceptor.cs         sella la columna de tenant de su clase en los Added; rechaza cambios de TenantId,
│  │  │                                    BusinessTenantId o ConsumerTenantId
│  │  ├─ AuditableEntityInterceptor.cs
│  │  ├─ SoftDeleteInterceptor.cs
│  │  └─ AuditTrailInterceptor.cs          genera AuditEntry con el diff de las entidades IAuditable
│  ├─ Rls/
│  │  ├─ RlsMigrationBuilderExtensions.cs  migrationBuilder.EnableTenantRls("tenant", "<Tabla>")
│  │  ├─ TenantIsolationModelValidator.cs  falla al arrancar si una entidad no está clasificada, o no tiene esquema o filtro
│  │  └─ RuntimeRoleValidator.cs           al arrancar: el login de runtime no es superuser, ni BYPASSRLS, ni dueño
│  ├─ Extensions/                      ModelBuilderExtensions (filtros con nombre "Tenant", "Public", "Parties" y "SoftDelete"),
│  │                                   QueryableExtensions (ApplySort, ToPagedResultAsync), AdvisoryLockExtensions,
│  │                                   AdvisoryLockKeys, TransactionExtensions (RequireTransaction)
│  ├─ Repositories/                    <X>Repository (internal sealed)
│  ├─ Readers/                         <X>Reader (AsNoTracking + proyección a *Row/*Response)
│  ├─ Migrations/                      una sola carpeta; RLS y grants dentro de las migraciones
│  ├─ Seed/                            PlatformSeeder (operador inicial), OpenIddictSeeder, DevelopmentSeeder
│  └─ DatabaseBootstrapExtensions.cs   solo en Development: rol de runtime + migraciones + seed
├─ Identity/
│  ├─ ApplicationUser.cs               IsPlatformOperator, Status (Active | Suspended | PendingDeletion | Deleted), Culture, TimeZoneId,
│  │                                   DisplayName, LastBusinessTenantId (la última organización usada en el lado empresa; no se vuelve
│  │                                   al último lado), DeletionRequestedAtUtc, DeletionScheduledForUtc, DeletionReason, DeletedAtUtc.
│  │                                   Email y PhoneNumber son solo una copia del método principal, sin índice único (la unicidad está
│  │                                   en LoginMethods). Sin setters públicos: los cambios pasan por métodos que aplican las reglas
│  │                                   de la cuenta
│  ├─ IdentityRegistration.cs          Identity core, cookies de /account y /connect, DataProtection, Google (se enciende con su ClientId)
│  ├─ SignInService.cs                 (ISignInService)
│  ├─ PermissionService.cs             permisos efectivos cacheados por tenant+usuario (+empresa)
│  ├─ PlatformPermissionService.cs
│  └─ OpenIddict/                      OpenIddictRegistration, AuthServerDefaults, CertificateLoader, TokenRevoker, WebClientOptions
├─ Messaging/
│  ├─ Outbox.cs                        IOutbox: inserta OutboxMessage dentro de la transacción del caso de uso
│  ├─ OutboxDispatcher.cs              BackgroundService: FOR UPDATE SKIP LOCKED, reintentos con backoff
│  ├─ Email/                           EmailChannelSender, IEmailTransport (SmtpEmailTransport; PickupDirectoryEmailTransport en Development),
│  │                                   MimeMessageFactory, EmailTemplateRenderer, Templates/*.html
│  └─ MessagingRegistration.cs
├─ Modules/WhatsApp/                   Cloud/, Webhook/, Inbound/, Retention/, Persistence/ (sus configuraciones EF),
│                                      WhatsAppInfrastructureModule, WhatsAppOptions(+Validator), Disabled/
├─ Security/                           SecureTokenGenerator, LoginCodeGenerator, LoginCodeHasher, PayloadProtector
├─ Time/                               TimeZoneService (TimeZoneInfo, IANA)
├─ Phones/                             LibPhoneNumberParser
├─ Caching/                            CacheKeys (siempre con prefijo de alcance: t: / s: / u: / p:), CachingRegistration
├─ Idempotency/                        IdempotencyStore, IdempotencyCleanupWorker (P6)
├─ Features/                           FeaturesRegistration, TenantFeatureFilter, FeatureService (P8)
├─ Legal/                              AccountDeletionWorker (cada hora, SKIP LOCKED; toda cuenta con DeletionScheduledForUtc vencido,
│                                      esté PendingDeletion o Suspended, por pasos: multitenancy.md §3.2)
└─ BackgroundJobs/                     TenantJobRunner (recorre organizaciones activas y ejecuta en su alcance)
```

### 4.4 Api

```
Api/
├─ Program.cs                          compone: AddServiceDefaults, AddApplication, AddInfrastructure, AddPresentation,
│                                      AddWhatsAppModule (una línea por módulo) + pipeline
├─ DependencyInjection.cs              AddPresentation(): ProblemDetails, exception handler, auth, localización, rate limit,
│                                      OpenAPI, controllers + JSON
├─ appsettings.json / appsettings.Development.json
├─ Controllers/
│  ├─ Auth/                            ConnectController, SignupController (persona), LoginCodeController, LoginLinkController,
│  │                                   ExternalLoginController, InvitationsController, LoginMethodsController,
│  │                                   DeletionCancelController (POST /api/auth/deletion/cancel)
│  ├─ Account/                         MeController (cuenta y accesos), BusinessSignupController ("Registrá tu empresa"),
│  │                                   TimeZonesController, LegalController (GET /api/legal/current, anónimo; POST /api/legal/accept),
│  │                                   AccountDeletionController (POST /api/me/deletion)
│  ├─ Organization/                    [Access(Business)] UsersController, RolesController, PermissionsController,
│  │                                   CompaniesController, CompanyMembersController, SettingsController, AuditController,
│  │                                   PublicPageAdminController (api/public-site: mi página pública, pantalla /org/pagina,
│  │                                   publicpage.manage)
│  ├─ Personal/                        [Access(Consumer)] los controllers de los módulos B2C del producto
│  ├─ PublicSite/                      PublicPageController ([PublicSite][AllowAnonymous], página por subdominio),
│  │                                   DirectoryController ([AllowAnonymous], dominio principal: lee solo lo publicado)
│  ├─ Platform/                        PlatformTenantsController (incluye moderar la página pública: POST
│  │                                   /api/platform/tenants/{id}/public-site/unpublish y .../allow-publish, con motivo),
│  │                                   PlatformAccountsController (incluye la baja iniciada por la plataforma,
│  │                                   POST /api/platform/accounts/{id}/deletion, E5), PlatformOperatorsController,
│  │                                   PlatformAuditController, PlatformSettingsController
├─ Contracts/<Área>/                   <Acción>HttpRequest, <X>Query (records sealed, props nullable; ToString() sin PII)
├─ Authorization/
│  ├─ HasPermissionAttribute.cs              permiso de organización
│  ├─ HasCompanyPermissionAttribute.cs       permiso evaluado en la empresa {companyId} de la ruta
│  ├─ HasPlatformPermissionAttribute.cs      solo access=platform
│  ├─ PermissionPolicyProvider.cs            arma "perm:<p>", "cperm:<p>", "pperm:<p>"
│  └─ *AuthorizationHandler.cs, *Requirement.cs
├─ RequestContext/                     CurrentUser (claims), RequestInfo (IP, user agent)
├─ Tenancy/
│  ├─ TenantResolutionMiddleware.cs    claims access + tenant_id + tenant_kind → TenantContext; activos (caché)
│  ├─ PublicSiteResolutionMiddleware.cs subdominio → IPublicSiteContext (solo datos públicos)
│  ├─ AccessAttribute.cs               [Access(Consumer|Business|Platform)] → 403 Tenancy.Access.Wrong
│  ├─ PublicSiteAttribute.cs           [PublicSite]: la ruta necesita un subdominio de empresa publicado
│  └─ TenantClaimTypes.cs              access, tenant_id, tenant_kind
├─ Authentication/                     OpenIdPrincipalFactory (claims y destinos)
├─ ErrorHandling/                      ApiErrorCodes, ProblemDetailsMapper, ControllerResultExtensions, GlobalExceptionHandler,
│                                      MvcInvalidModelStateResponseFactory, EmptyJsonBodyContentTypeFilter
├─ Json/                               UtcDateTimeConverter
├─ Localization/                       LocalizationExtensions (es por defecto, en; Accept-Language)
├─ OpenApi/                            OpenApiExtensions, ProblemResponsesConvention, ProducesProblemAttribute
├─ RateLimiting/                       RateLimitingExtensions, RateLimitingOptions (por IP y por organización)
├─ Idempotency/                        IdempotentAttribute, IdempotencyFilter
├─ Features/                           DisabledFeatureHandler (módulo apagado → 404)
├─ Legal/                              LegalAcceptanceMiddleware
├─ Modules/WhatsApp/                   WhatsAppApiModule, WhatsAppWebhookController, ConditionalWhatsAppRouteConvention
└─ Hosting/                            ForwardedHeadersExtensions, SecurityHeadersExtensions, SpaExtensions (BackendPrefixes)
```

### 4.5 AppHost y ServiceDefaults

- **`AppHost/AppHost.cs`:**
  - `AddPostgres("postgres", password, port: 5434)`, con un volumen persistente `arquitecturabase-multitenant-pgdata` y `ContainerLifetime.Persistent`.
  - `AddDatabase("appdb")`.
  - Api en `https://localhost:7280` (ArquitecturaBase usa 7180, así los dos pueden correr a la vez). En [E0] recibe `appdb` de Aspire. En [E2], `appdb` pasa al login de runtime `mt_app` y se agrega `appdb-admin`, del dueño, solo en Development.
  - `AddViteApp("front", "../../../ArquitecturaBaseMutitenantFront")` en https 5174.
  - DevTunnel opcional para el webhook de WhatsApp.
- **`ServiceDefaults/Extensions.cs`:** como la base (OpenTelemetry, resiliencia, service discovery, `/health` y `/alive`).

### 4.6 Tests

```
tests/
├─ Directory.Build.props                  OutputType Exe, xunit.v3, <Using Include="Xunit"/>
├─ *.Domain.UnitTests/<Área>/
├─ *.Application.UnitTests/
│  ├─ Services/<Área>/                    <X>ServiceTests, <X>ServiceWriteTests
│  ├─ Resources/                          ResourceParityTests, PermissionTextsTests
│  └─ TestDoubles/                        FakeUnitOfWork, FakeTenantContext, InMemory<X>Repository, FakeOutbox…
├─ *.Api.IntegrationTests/
│  ├─ Support/                            ApiFactory (WebApplicationFactory + Testcontainers), AuthFlow, TestAuthHandler,
│  │                                      TenantFixture (Empresa A y B; Kevin como empresa y como persona; Carla),
│  │                                      CapturingOutbox
│  ├─ Tenancy/                            CrossTenantIsolationTests, AccessTests, SubdomainTests, PublicAndSharedRowsTests,
│  │                                      RlsPolicyInventoryTests, RuntimeRoleTests, CacheKeyScopeTests
│  ├─ Persistence/                        UnitOfWorkTests, PaginationTests, CursorPaginationTests, SearchTests, SortIndexTests
│  ├─ Hosting/                            HealthCheckTests, SpaHostingTests, SecurityHeadersTests
│  ├─ Legal/                              LegalAcceptanceTests, AccountDeletionTests
│  ├─ Contracts/                          ExplicitRouteInventoryTests, OpenApiTests, OpenApiContractTests
│  └─ <Área>/                             un archivo por controller
└─ *.ArchitectureTests/                   HarnessTests, LayerDependencyTests, ProjectReferencesTests, ApplicationPackagesTests,
                                          ApplicationPublicApiTests, ControllerServiceRepositoryTests, ControllerInputContractTests,
                                          PermissionAuthorizationTests, TransactionBoundaryTests, ErrorCodeTests,
                                          EntityConfigurationTests, DataClassificationTests, ServiceDependencyCountTests,
                                          MinimalApiRoutesTests, TenantScopeUsageTests, QueryFilterBypassTests, IdentityAccessTests,
                                          AccessDeclarationTests, VersionedContractTests, DecimalPrecisionTests, TextLimitsTests,
                                          AccountDeletionParticipantsTests, ModuleIsolationTests
```

Es un resumen. El árbol completo, con la etapa en que nace cada test, está en [`arbol.md`](arbol.md).

---

## 5. Patrón MVC: controller → servicio → repositorio

### Controller

- `public sealed class XController(IXService service) : ControllerBase`, con primary constructor, `[ApiController]`, `[Route("api/<recurso>")]` y `[Tags]`. No hay controller base.
- **Solo inyecta interfaces de `Application/Interfaces/Services`.** Nunca EF, repositorios ni readers (test).
- La entrada es un contrato de `Api/Contracts/<Área>`, mapeado a mano al `*Request` de Application.
- La salida es siempre `ToActionResult(this)`, `ToCreatedResult(this, nameof(Get), id => new { id })` o `ToAcceptedResult(this)`. Nunca `IsSuccess ? … : …`.
- Las rutas de la organización no llevan prefijo de acceso: `api/roles`, `api/permissions`, `api/users`, `api/users/invitations`, `api/companies/{companyId}` y `api/companies/{companyId}/members`, `api/settings` y `api/public-site`. El acceso lo declara `[Access]`, no la ruta, y "tenant" no aparece en ellas. Las excepciones son `/api/platform/...`, `/api/me/...`, `/api/auth/...` e `/api/invitations/...` (anónimo).
- Cada acción declara `[Access(...)]`, o `[PublicSite][AllowAnonymous]` si responde en el subdominio de una organización publicada (`PublicPageController`). Las rutas anónimas del dominio principal (ingreso, registro, "Registrá tu empresa", invitación, enlace, `GET` de los documentos legales, directorio, pedido de "Recuperar mi cuenta", cancelar la baja y webhooks) declaran solo `[AllowAnonymous]`, y `AccessDeclarationTests` las acepta únicamente si su controller está en la lista explícita del test. En B2B y plataforma, cada acción declara además su permiso (`[HasPermission]`, `[HasCompanyPermission]` o `[HasPlatformPermission]`). Las rutas B2C (`[Access(Consumer)]`) no piden permiso (§14). Además lleva `[ProducesResponseType<T>]`, y los errores extra van con `[ProducesProblem]`.
- Un `POST` que crea o envía lleva `[Idempotent]`, y una acción de un módulo lleva `[FeatureGate]`. Un `PUT` o `DELETE` de una entidad `IVersioned` recibe `version`, que es obligatoria ([concurrencia](../rules/concurrencia.md)).
- `POST` que crea → 201 con `Location` y `GET /{id}`. `PUT` → 204. `DELETE` → 204.

```csharp
[ApiController, Route("api/roles"), Tags("Roles"), Access(Access.Business)]
public sealed class RolesController(IRoleService service) : ControllerBase
{
    [HttpPut("{id:guid}"), HasPermission(Permissions.Roles.Manage), ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateRoleHttpRequest request, CancellationToken ct) =>
        (await service.UpdateAsync(new UpdateRoleRequest(id, request.Name, request.Description, request.Permissions, request.Version), ct))
            .ToActionResult(this);
}
```

### Servicio

- La interfaz va en `Interfaces/Services/IXService.cs` y la implementación en `Services/<Área>/XService.cs`, como `internal sealed partial`.
- Un método público que escribe sigue siempre este orden:
  1. **Validar afuera:** `await validator.ValidateAsync(request, ct)`. Si falla, devuelve el `ValidationError`.
  2. **Un solo límite:** `unitOfWork.ExecuteInTransactionAsync(ct => UpdateCoreAsync(request, ct), CommitPolicy.OnSuccess, ct)`.
  3. **Adentro** del límite: locks → lecturas → reglas (que devuelven `Error`) → escrituras.
  4. **Afuera, después del commit:** invalidar el caché. El método entero va envuelto en `OperationLog.RunAsync(logger, "UpdateRole", …)`, que registra el inicio, el fin y el código de error: una línea por método, nunca el request.
- Un servicio no llama a los métodos que escriben de otro servicio. Lo compartido baja a un helper.
- Los métodos que solo leen no abren límite: van directo al reader.

### Repositorio, reader y UnitOfWork

| Pieza | Para qué | Convención de nombres |
|---|---|---|
| `I<X>Repository` | escribir un agregado (seguido por EF) | `Get…` (entidad seguida o `null`), `Add`, `Remove` |
| `I<X>Reader` | lecturas, proyecciones y páginas | `Find…` (proyección), `List…` (colección o `PagedResult<*Row>`), `Exists…`, `Count…` |
| `IUnitOfWork` | único punto de guardado | `ExecuteInTransactionAsync<TResult>(work, CommitPolicy, ct) where TResult : Result` |

- `CommitPolicy.OnSuccess` es la regla; `OnAnyResult` se usa cuando también hay que guardar un fallo (intentos, códigos consumidos, auditoría de seguridad).
- La transacción es READ COMMITTED, un solo `SaveChanges`, sin anidar (anidar lanza). Ante una excepción: rollback y `ChangeTracker.Clear()`. El error 23505 se traduce a `UniqueConstraintViolationException`, y el servicio lo convierte en su `Error`. `DbUpdateConcurrencyException` se traduce a `ConcurrencyConflictException` (P1, ver [concurrencia](../rules/concurrencia.md)), y el servicio no la atrapa.
- **Multitenant:** al abrir el límite, `UnitOfWork` ejecuta `SELECT set_config('app.tenant_id', @id, true)` con el tenant del `ITenantContext`. El detalle está en [`multitenancy.md`](multitenancy.md).
- Locks: `pg_advisory_xact_lock` con las claves de `AdvisoryLockKeys`, que **siempre incluyen el TenantId**, o `FOR NO KEY UPDATE`. Exigen la transacción del caso de uso.
- Prohibido `ExecuteUpdate` y `ExecuteDelete` sobre entidades `IAuditable` o `ISoftDeletable`.

`TransactionBoundaryTests` verifica con el IL que solo los servicios que implementan `Interfaces/Services` reciben `IUnitOfWork`, y que nadie más llama a `SaveChanges`.

---

## 6. Result pattern y manejo de errores

### Tipos (`Domain/Results`)

```csharp
public enum ErrorType { Failure, Validation, Unauthorized, Forbidden, NotFound, Conflict, TooManyRequests }
public record Error(string Code, string Description, ErrorType Type, IReadOnlyDictionary<string, object?>? Metadata = null);
public sealed record ValidationError(IReadOnlyDictionary<string, string[]> Errors) : Error("Validation.Failed", ..., ErrorType.Validation);
public class Result { bool IsSuccess; Error Error; static Success(); implicit from Error }
public class Result<T> : Result { T Value; implicit from T y from Error }
```

### Catálogos de errores

- Son clases estáticas `<Entidad>Errors` en `Domain/<Área>`, con una constante `…Code` y un `static readonly Error` (o una fábrica si lleva metadata).
- El código tiene formato **`Area.Entidad.Motivo`** (tres partes o más), por ejemplo `Companies.Company.NameTaken`, `Tenancy.Tenant.Suspended` o `Roles.Role.HasUsers`. Es estable y es la clave en `Errors.resx`.
- La única excepción es una **lista cerrada de claves reservadas**: `Title.<ErrorType>`, `Validation.Failed`, las de `ApiErrorCodes` (`General.Unexpected`, `General.ConcurrencyConflict`, `Request.Invalid`, `Request.InProgress`, `Request.IdempotencyKeyRequired`, `Request.IdempotencyKeyReused` y `Http.*`) y `Legal.AcceptanceRequired`. Un código nuevo nunca entra en esa lista. `ErrorCodeTests` verifica que toda clave de `Errors.resx` siga el formato, salvo las de `ReservedKeys`, que además tienen que seguir existiendo en el resx.
- `Description` va en inglés y es el respaldo si falta la traducción.

### Excepciones vs Result

- **Regla de negocio** → `Result`. Nunca una excepción.
- **Bug o falla de infraestructura** → excepción. `GlobalExceptionHandler` responde 500 `General.Unexpected` con `traceId`, loguea con `[LoggerMessage]` y nunca expone el mensaje.
- **Excepción única: edición simultánea (P1).** `UnitOfWork` hace rollback y traduce `DbUpdateConcurrencyException` a `ConcurrencyConflictException`; `ProblemDetailsMapper` responde 409 `General.ConcurrencyConflict`. Es la única excepción que llega al mapper como respuesta de negocio, porque siempre se responde igual ([concurrencia](../rules/concurrencia.md)).

### Mapeo HTTP (`ProblemDetailsMapper`)

| ErrorType | Status |
|---|---|
| Validation | 400 (con `errors`: campo camelCase → mensajes) |
| Unauthorized | 401 |
| Forbidden | 403 |
| NotFound | 404 |
| Conflict | 409 |
| TooManyRequests | 429 (+ `retryAfter`) |
| Failure | 500 |

El cuerpo es `application/problem+json` con `title` y `detail` traducidos, `code`, `traceId`, `errors` y la metadata pública.
- Los errores que arma la propia Api sin pasar por un `Result` tienen su código en `ApiErrorCodes`, con su clave en `Errors.resx` y `Errors.en.resx`: `Request.Invalid`, `Http.Unauthorized`, `Http.Forbidden`, `Http.NotFound`, `Http.MethodNotAllowed`, `Http.TooManyRequests`, `General.Unexpected` y `General.ConcurrencyConflict`. También los de idempotencia, que emite `IdempotencyFilter` como ProblemDetails directamente, sin pasar por el mapper: 409 `Request.InProgress`, 422 `Request.IdempotencyKeyReused` y 400 `Request.IdempotencyKeyRequired` (ver [idempotencia](../rules/idempotencia.md)).
- El 400 de un cuerpo ilegible no trae `errors` a propósito.
- **Multitenant:** una entidad de otra organización responde **404**, nunca 403, para no revelar que existe. Una organización que no está activa responde 403 con el código de su estado, que decide `TenantResolutionMiddleware`: `Tenancy.Tenant.Suspended`, `Tenancy.Tenant.PendingApproval` o `Tenancy.Tenant.Closed` (multitenancy.md §7).

### Validación

- **Un solo `IRequestValidator`**, inyectado una vez por servicio: `Task<ValidationError?> ValidateAsync<T>(T request, CancellationToken ct)`. Resuelve los `IValidator<T>` y agrupa los errores por campo en camelCase con puntos.
- Los validadores son `internal sealed`, en `Validation/<Área>`, y usan `ValidationRules` (`Required()`, `MaxLength()`, `ValidEmail()`, `ValidTimeZone()`, `ValidPermissions(scope)`), con mensajes de `ValidationTexts`.
- `FieldErrors.On(error, "name")` ata un error de negocio a un campo del formulario.

---

## 7. Inyección de dependencias

| Capa | Punto de entrada | Qué registra |
|---|---|---|
| Application | `AddApplication()` | Options (`BindConfiguration + ValidateDataAnnotations + ValidateOnStart`), cada servicio y helper con `AddScoped`, uno por uno, validadores (`AddValidatorsFromAssembly(includeInternalTypes: true)`) e `IRequestValidator` |
| Infrastructure | `AddInfrastructure(cfg, env)` | `TryAddSingleton(TimeProvider.System)`, `TenantContext` (scoped, un registro para `ITenantContext` y otro para `ITenantScope`), interceptores (en orden: TenantStamp → SoftDelete → Auditable → AuditTrail; más TenantConnection), `AddDbContext`, `IUnitOfWork`, repositorios, readers y los subregistros `AddIdentityServices`, `AddOpenIddictServer`, `AddMessaging`, `AddCaching`. Los módulos se registran aparte, con su `AddWhatsAppModule()` en cada capa, llamado desde `Program.cs` |
| Api | `AddPresentation()` | ProblemDetails, `GlobalExceptionHandler`, autorización (policy provider y handlers), localización, rate limiting, OpenAPI, `AddControllers` con un solo `ConfigureJson` |

- Sin Scrutor y sin registrar por reflexión.
- Lifetimes:
  - **scoped**: servicios, repositorios, readers, `TenantContext` y `CurrentUser`;
  - **singleton**: `TimeProvider`, clientes HTTP tipados, `CacheKeys` y parsers sin estado.
- Un worker (`BackgroundService`) crea su propio scope por unidad de trabajo y fija el tenant con `ITenantScope`.

---

## 8. Contexto de la petición

| Interfaz (Application) | Implementación | Fuente |
|---|---|---|
| `ICurrentUser` | `Api/RequestContext/CurrentUser` | claims `sub` y `access` (`consumer`, `business` o `platform`) |
| `IPublicSiteContext` | `Api/Tenancy/PublicSiteContext` | la organización del subdominio; **solo** para leer lo público |
| `ITenantContext` | `Infrastructure/Persistence/TenantContext` | tenant del acceso activo (`TenantId` y `TenantKind`: el espacio personal en B2C, la organización en B2B). Lo carga `TenantResolutionMiddleware` desde los claims, o `ITenantScope.Enter(tenantId)` en plataforma, workers y altas |
| `IRequestInfo` | `Api/RequestContext/RequestInfo` | IP (después de ForwardedHeaders) y user agent |

- `ITenantContext.TenantId` **nunca** sale de un header, del body ni de la query.
- Las rutas de plataforma reciben el `tenantId` en la URL como **objetivo**; el servicio de plataforma, después de autorizar, entra con `ITenantScope.Enter(tenantId)`.

---

## 9. Persistencia

- **Un solo `ApplicationDbContext`**, con cinco esquemas:
  - **`platform`**: Tenants, TenantFeatures, TenantDomains (dominios de correo verificados), PlatformSettings, PlatformRoleAssignments, SecurityEvents, LegalDocuments y LegalDocumentContents (un texto por documento y por idioma), AccountRecoveryRequests ("Recuperar mi cuenta"), DataExports ("Exportar mis datos"), IdempotencyKeys, OutboxMessages, WhatsAppChannels, OpenIddict y DataProtection. Sin RLS.
  - **`identity`**: AspNetUsers (global, sin `TenantId`), **LoginMethods** (la única fuente de los correos y teléfonos de una cuenta, cada uno único en todo el sistema), LegalAcceptances, UserLogins, UserTokens, LoginCodes, LoginLinks, LoginAudits, WhatsAppContacts y WhatsAppMessages (el número de la plataforma habla con la identidad, no con un acceso). Sin RLS; solo la leen `Infrastructure/Identity`, `Infrastructure/Modules/WhatsApp` y los readers que parten de `Members` ([ADR 0011](../decisions/README.md)).
  - **`tenant`**, datos **privados** con **RLS forzado**: Members, Invitations, Companies, CompanyMemberships, Roles, RoleAssignments, TenantSettings, AuditEntries y lo privado que sumen los módulos B2B y B2C.
  - **`public_site`**, datos **públicos** de cada organización con RLS por publicación: PublicPages (nombre, logo, descripción, contacto y estado; el slug es el de la organización, en `platform.Tenants`) y lo que publiquen los módulos.
  - **`engagement`**, datos **compartidos** entre una persona y una organización, con RLS por partes: lo que definan los módulos (reservas, pedidos, solicitudes, mensajes).
- **Toda entidad nueva se clasifica** como privada (`ITenantOwned`), pública (`IPublishedByBusiness`) o compartida (`IConsumerBusinessShared`) antes de escribirla. Cada clase tiene su esquema, su filtro y su política RLS (multitenancy.md §4 y §9).
- Una configuración por entidad (test). Enums como texto (`HasConversion<string>().HasMaxLength(n)`). Las claves de las tablas de `tenant` son `(TenantId, …)`, sus índices únicos empiezan por `TenantId` y las FK a otras entidades del mismo tenant son compuestas `(TenantId, XId)`.
- **Filtros globales con nombre (EF 10), uno por clase de dato:** `"Tenant"` (privado, sobre toda `ITenantOwned`: `e.TenantId == tenantContext.TenantId`), `"Public"` (público, sobre toda `IPublishedByBusiness`: `IsPublished || BusinessTenantId == ctx.TenantId`), `"Parties"` (compartido, sobre toda `IConsumerBusinessShared`: `ConsumerTenantId == ctx.TenantId || BusinessTenantId == ctx.TenantId`) y `"SoftDelete"` (multitenancy.md §9). `IgnoreQueryFilters(["SoftDelete"])` quita solo el de borrados. Quitar `"Tenant"`, `"Public"` o `"Parties"` está prohibido fuera de la lista blanca (`Infrastructure/Persistence/Readers/Platform/`); lo verifica `QueryFilterBypassTests`.
- **Migraciones:** una carpeta, `Infrastructure/Persistence/Migrations`. Cada tabla nueva llama en su migración al helper RLS de su clase (`EnableTenantRls`, `EnablePublicRls` o `EnablePartiesRls`), y `RlsPolicyInventoryTests` falla si alguna tabla de `tenant`, `public_site` o `engagement` no tiene su política.

```
dotnet ef migrations add <Nombre> --project src/ArquitecturaBaseMultitenant.Infrastructure \
  --startup-project src/ArquitecturaBaseMultitenant.Api --output-dir Persistence/Migrations \
  -- --environment Development --ConnectionStrings:appdb-admin "Host=localhost;Port=5434;..."
```

- En Development se aplican al arrancar (`DatabaseBootstrapExtensions`, con la cadena `appdb-admin`). Fuera de Development: `dotnet ef migrations bundle`, como paso del despliegue. El seed es idempotente siempre.

### Paginado, orden y búsqueda

Hay **dos formas de paginar**, y cada listado declara cuál usa:

| Forma | Cuándo | Pedido | Respuesta |
|---|---|---|---|
| **Por páginas** (la regla) | listados administrativos: usuarios, roles, empresas, organizaciones, identidades | `PagedRequest`: `page` (desde 1), `pageSize` (**10 por defecto**; 10, 20, 50 o 100), `sort` (`"campo"` o `"-campo"`), `search` (máx. 100) y filtros propios | `PagedResult<T>`: `items`, `page`, `pageSize`, `totalCount`, `totalPages`, `hasPrevious`, `hasNext` |
| **Por cursor** (la excepción) | tablas que solo crecen: `AuditEntries`, `SecurityEvents`, `LoginAudits`, mensajes de WhatsApp | `CursorRequest`: `after` (cursor opaco), `limit` (máx. 100) y filtros propios | `CursorResult<T>`: `items`, `nextCursor`, `hasMore`, **sin total** |

**Reglas**
1. **Orden estable:** `ApplySort(sort, SortMap, defaultSort)` siempre desempata por `Id`. Cada reader declara su `SortMap<TEntity>` (campo del contrato → expresión) como un campo estático, con los mismos nombres que `SortableFields`. Un `sort` que no está en la lista da 400 `Validation.Failed`, con el campo `sort` y el mensaje `SortNotAllowed`.
2. **Página fuera de rango:** si se pide la página 8 y hay 5, la respuesta trae `items` vacío, el `totalCount` real y la `page` pedida. No es un error. El front salta solo a la última.
3. **Búsqueda sin acentos ni mayúsculas:** `ApplySearch(search, x => x.Name, x => x.Email…)` es el único lugar donde se busca. Traduce a `f_unaccent(lower(col)) LIKE f_unaccent(lower('%texto%'))`: "perez" encuentra a "Pérez". La migración inicial crea las extensiones `unaccent` y `pg_trgm` y la función inmutable `public.f_unaccent`. Cada columna buscable lleva un índice GIN trigram sobre `f_unaccent(lower(col))`. Los comodines del usuario (`%`, `_`) se escapan.
4. **Índices con tenant adelante:** cada campo de un `SortMap` sobre una entidad `ITenantOwned` tiene un índice `(TenantId, campo, Id)`. `SortIndexTests` recorre los `SortMap` y el modelo de EF, y falla si falta alguno.
5. **Filtros y conteos con una sola función:** el listado y `GET /api/<recurso>/filter-counts` usan el mismo `Apply<Recurso>Filters(query, request)`. Un valor de filtro inexistente da una lista vacía, no un error. Los conteos responden, para cada opción, cuántas filas quedan con el resto de los filtros aplicados.
6. **Cursor:** codifica `(campo de orden, Id)` del último ítem en base64url, y es opaco para el front. Un cursor inválido da 400 `Validation.Failed` con el campo `after`. El orden es fijo (más nuevo primero) y no admite `sort`.
7. Los modelos van en `Application/Common/Pagination/` (`PagedRequest`, `PagedResult<T>`, `CursorRequest`, `CursorResult<T>`, `SortDescriptor`) y los validadores en `Common/Validation` (`PagedRequestValidator<T>`, `CursorRequestValidator<T>`). Los helpers de EF están en `Infrastructure/Persistence/Extensions/QueryableExtensions.cs` (`ApplySort`, `ApplySearch`, `ToPagedResultAsync`, `ToCursorResultAsync`).

---

## 10. Auditoría

Hay tres niveles, cada uno con su propósito:

1. **Marcas en la fila (`IAuditable`):** `CreatedAtUtc/CreatedBy/ModifiedAtUtc/ModifiedBy`. Las completa `AuditableEntityInterceptor` con `ICurrentUser.UserId` y `TimeProvider`. Nunca se setean a mano.
2. **Rastro de cambios (`tenant.AuditEntries`, append-only):**
   - `AuditTrailInterceptor` genera una entrada por cada `IAuditable` agregada, modificada o borrada. Guarda TenantId, `ActorKind` (User, PlatformOperator, System), ActorId, `Action` (Created, Updated, Deleted, Restored), EntityType, EntityId, `Changes` (jsonb con campo → anterior y nuevo) y `OccurredAtUtc`.
   - Se escribe **en la misma transacción** que el cambio.
   - Las propiedades marcadas `[NotAudited]` (tokens, hashes) no se guardan.
   - Para eventos que no son un cambio de entidad (exportaciones, reenvío de invitación), el servicio llama a `IAuditLog.Record(action, entity, id, data)` dentro del límite.
   - RLS y un trigger `prevent_update_delete` hacen la tabla inmutable.
3. **Seguridad (`platform.SecurityEvents` e `identity.LoginAudits`):** ingresos, intentos fallidos, cambios de rol de plataforma, acciones de operadores sobre una organización o una cuenta (aprobar, suspender, despublicar su página o permitir publicarla, iniciar la baja, resolver una recuperación; siempre con la organización o la cuenta afectada y el motivo), cambios de métodos de ingreso y la baja de una cuenta (`AccountDeletionRequested`, su cancelación y `AccountDeleted`). La leen solo los operadores con `platform.audit.read`.

La pantalla "Auditoría" de la organización lee `AuditEntries` paginadas y filtradas por entidad, actor y fecha, con los textos traducidos desde `Audit.resx`.

---

## 11. Fechas, UTC y zonas horarias

- El reloj es siempre `TimeProvider` inyectado. `BannedSymbols.txt` rompe el build con `DateTime.Now`, `DateTime.UtcNow` y similares. En los tests se usa `FakeTimeProvider`.
- Tipos: `DateTime` en UTC con sufijo `Utc`, `DateOnly` para fechas civiles, y `timestamptz` en Postgres.
- En JSON, `UtcDateTimeConverter` sale siempre con `Z` y rechaza entradas sin offset (400).
- **Zona efectiva** del usuario: `User.TimeZoneId` → `Company.TimeZoneId` (en pantallas de una empresa) → `TenantSettings.DefaultTimeZoneId` → `"America/Argentina/Buenos_Aires"`. Los IDs son IANA y se validan con `ITimeZoneService.IsValid`.
- **La conversión para mostrar la hace el front.** El backend solo convierte cuando la regla lo exige (rangos "del día" en reportes: `ITimeZoneService.GetDayRangeUtc(DateOnly, tz)`, con horario de verano y días saltados).
- `GET /api/time-zones` devuelve el catálogo traducido.

---

## 12. Idioma, traducciones y resources

- **Cultura** es una sola preferencia con idioma y región (`es-AR`, `en-US`), no dos. El **idioma** de los textos sale de su primera parte (`es`, `en`) y el **formato** de números y fechas, de la cultura completa (§18).
- Culturas soportadas: `es-AR` (por defecto) y `en-US`. Se agregan otras sumándolas a `SupportedCultures`.
- La fuente en un request es `Accept-Language`: el front manda la cultura efectiva. `UseRequestLocalization` va antes de `UseExceptionHandler`.
- En segundo plano (correo y WhatsApp), la cultura es `User.Culture` → `TenantSettings.DefaultCulture` → `es-AR`, y se pasa **explícita** (`NotificationTexts.Get(key, culture, args)`).
- No se usa `IStringLocalizer`. Hay envoltorios estáticos sobre `ResourceManager`: `ErrorTexts`, `ValidationTexts`, `PermissionTexts`, `NotificationTexts` y `AuditTexts`.
- Toda clave va en los dos idiomas (`ResourceParityTests` compara claves y placeholders).
- Textos en español rioplatense con voseo. Identificadores, logs y mensajes de excepción, en inglés.
- **En pantalla nunca se dice "tenant"**: se dice "Organización". `TenantAdmin` se muestra como "Dueño" y `CompanyAdmin` como "Administrador". En el código sigue `Tenant`.

---

## 13. Autenticación: Identity + OpenIddict

- OpenIddict 7 corre como **servidor en la misma Api**:
  - Authorization Code + PKCE (obligatorio) + refresh tokens. Sin client credentials.
  - Endpoints `connect/authorize|token|logout|userinfo|revoke`.
  - Scopes `openid profile email offline_access api`.
- **Un solo cliente público, `web`**, para el SPA. El área la decide el claim `access`: `consumer` → espacio personal y páginas públicas; `business` → la organización; `platform` → el backoffice. Las páginas públicas de cada subdominio usan el mismo cliente, con redirect URIs validadas contra los slugs publicados (`SubdomainRedirectUriValidator`).
- **El issuer es fijo** (el dominio principal). Desde un subdominio, `authorize` y `logout` son navegaciones al dominio principal, donde vive la cookie de `/connect`; el canje del código, la renovación y `userinfo` van a `/connect/*` del propio origen del subdominio (multitenancy.md §5).
- Duraciones: código 5 min, access token 15 min, refresh 30 días con rotación. `EnableTokenEntryValidation` permite la revocación inmediata.
- **Claims** (`OpenIdPrincipalFactory`): `sub`, `name`, `email`, `access` (`consumer` | `business` | `platform`) y el tenant del acceso activo: `tenant_id` y `tenant_kind` (`personal` | `business`). **Ni los permisos ni los roles viajan en el token**: se consultan en vivo, con caché.
- **Ingreso sin contraseña**, igual que la base: en la E3, código por correo y **Google**; el módulo de WhatsApp suma código por teléfono y enlace de un solo uso en la E8. **Autoregistro de personas** (`POST /api/auth/signup`, o el primer ingreso con Google), que crea la identidad y su espacio personal. Está abierto por defecto y se cierra con `PlatformSettings.ConsumerSignup` (`Open` | `Closed`; cerrado, el registro responde «Registro cerrado»). **Una persona no crea empresas:** el alta B2B es aparte (`POST /api/auth/business-signup`, "Registrá tu empresa") y la rigen `PlatformSettings.BusinessSignup` (`Open` | `RequiresApproval` | `Closed`) y el límite de organizaciones por persona (`MaxOwnedOrganizations`), con `BusinessSignupPolicy` (ADR 0020). Los correos salen por **Gmail (SMTP)**, también en desarrollo. La configuración usa las mismas claves que ArquitecturaBase y se carga con los scripts de `scripts/secretos/`: ver [`docs/operations/configuracion.md`](../operations/configuracion.md).
- **Identidad global, dos accesos:** cada método de ingreso (`LoginMethods`: correo, teléfono o Google, tipo + valor) es único en todo el sistema, y una cuenta puede tener varios; `AspNetUsers.Email` y `PhoneNumber` son solo una copia del método principal (multitenancy.md §3.1 y §12). Una persona tiene **una** cuenta. Ingresa **como persona** (por "Ingresá", `/login`: su espacio personal y las páginas públicas) o **como empresa** (por "Ingresá como empresa", `/login/empresa`: sus organizaciones, en `/org`). No se vuelve "al último lado": se entra al de la puerta elegida. **Para cambiar de acceso o de organización** se pide `/connect/authorize?prompt=none&access=<consumer|business>&tenant=<id>`: `ConnectService` valida la membresía y emite tokens nuevos (multitenancy.md §3 y §10).
- Cookies de Identity solo para `/account` y `/connect` (HttpOnly, Secure, SameSite Lax). La Api usa bearer.
- En cada request autenticado, `TenantResolutionMiddleware` verifica (con caché de 60 s, invalidado al cambiar de estado) que la identidad, la membresía y el tenant del acceso activo estén activos. Suspender una organización revoca los tokens emitidos para ella; suspender una identidad, todos los suyos, en los dos accesos.
- **Operadores de plataforma:** `access=platform`, sin membresías ni espacio personal, con el mismo mecanismo de ingreso. El segundo factor (TOTP) es obligatorio para ellos desde la Etapa 9 (endurecimiento).
- **Invitaciones a una organización:** `Member(Invited)` + `Invitation` (token con hash, vence), que emite `InvitationIssuer` → correo o WhatsApp por outbox (`IInvitationChannel`). Desde la organización se invita con `POST /api/users/invitations` (reenviar y revocar, bajo `api/users/invitations/{id}`). El `InvitationsController` anónimo atiende la vista previa (`POST /api/invitations/preview`: el token va en el cuerpo, nunca en la URL) y la aceptación (`POST /api/invitations/accept`), con y sin cuenta previa. Si la persona no tenía cuenta, la aceptación crea su identidad (sin espacio personal: ese nace la primera vez que entra como persona).
- **Baja de una cuenta** (multitenancy.md §3.2, [ADR 0035](../decisions/README.md)): la pide la persona (`POST /api/me/deletion`) o un operador (`POST /api/platform/accounts/{id}/deletion`), se cancela durante la gracia con `POST /api/auth/deletion/cancel` y la ejecuta `AccountDeletionWorker`; cada módulo participa con `IAccountDeletionParticipant`.

---

## 14. Autorización

- **Acceso B2C (espacio personal):** no tiene roles. La persona tiene implícitos todos los permisos `personal.*` (los que declare cada módulo B2C del producto). Las rutas B2C llevan `[Access(Consumer)]` y no piden permisos. Sobre un dato compartido con una empresa, lo que puede hacer cada parte lo decide `PartyPolicy`.
- **Cuatro catálogos de permisos** (constantes en `Domain/Authorization`, textos en `Permissions.resx`):
  - **Organización** (catálogo fino, ADR 0034), en `Permissions.cs`, sin prefijo, como en ArquitecturaBase (nunca `tenant.users.read`):
    - `users.read` (ver usuarios), `users.invite` (invitar, reenviar y revocar invitaciones), `users.manage` (editar, deshabilitar y habilitar);
    - `roles.read`, `roles.manage` (crear, editar e inactivar roles), `roles.assign` (dar y quitar roles);
    - `companies.read`, `companies.manage`;
    - `settings.read`, `settings.manage` (incluye verificar el dominio de correo);
    - `audit.read`;
    - `publicpage.manage` (la página pública).
  - **Empresa**, también en `Permissions.cs`: `company.members.read`, `company.members.manage` (y los que traigan los módulos).
  - **Personal**, en `PersonalPermissions.cs`: los `personal.*`, implícitos de la persona en su espacio personal. Los declara cada módulo B2C del producto y no se asignan por rol.
  - **Plataforma**, en `PlatformPermissions.cs`:
    - `platform.tenants.read`, `platform.tenants.manage` (aprobar, rechazar, suspender, reactivar y cerrar una organización, prender y apagar sus módulos, ver, verificar y quitar con motivo su dominio verificado y moderar su página pública: despublicar y «Permitir publicar»);
    - `platform.accounts.read` (buscar y ver cuentas), `platform.accounts.manage` (suspender, reactivar, cerrar sesiones e iniciar la baja con motivo);
    - `platform.recoveries.manage` (aprobar o rechazar «Recuperar mi cuenta»);
    - `platform.legal.manage` (publicar una versión nueva de términos o privacidad);
    - `platform.operators.manage`, `platform.audit.read`, `platform.settings.manage`;
    - `platform.whatsapp.manage` no entra al catálogo hasta la Etapa 11 (canales de WhatsApp por organización), porque ninguna ruta anterior lo usa.
- **Roles de plataforma** (`PlatformRole`): **Owner** tiene todos los permisos de plataforma; **Support** tiene `platform.tenants.read`, `platform.accounts.read`, `platform.recoveries.manage` y `platform.audit.read`.
- **Roles de la organización** (`tenant.Roles`), con **tres alcances** (en pantalla, la columna "Vale en"):
  - **`Organization`** ("Toda la organización"): se asigna sin empresa y vale en la organización y en todas sus empresas.
  - **`AnyCompany`** ("Cada empresa"): es un rol de empresa reutilizable; se asigna **con** una empresa y vale solo en esa. Ejemplo: el Administrador de empresa.
  - **`SpecificCompany`** ("Solo en <empresa>"): el rol pertenece a **una** empresa (`Role.CompanyId`) y solo se puede asignar en ella. Sirve para que cada empresa tenga sus propios roles.
  - Las empresas son **planas**: no hay empresa matriz ni herencia entre empresas (ADR 0034).
  - `TenantAdmin` y `CompanyAdmin` son de sistema: inmutables y no se borran. Se protege al último `TenantAdmin` y al último `CompanyAdmin` de cada empresa.
  - El Dueño sale **solo** del rol `TenantAdmin` (`RoleAssignment`), nunca de un dato de `Member`. "Dueños activos" son los que tienen `TenantAdmin` con la identidad `Active` (una baja pedida no cuenta); esa misma lectura la usan `AccountDeletionPolicy` y `LastTenantAdminGuard`.
- **Atributos**, nunca roles ni `[Authorize(Policy=…)]` a mano (test):
  - `[HasPermission(Permissions.Users.Manage)]` para la organización.
  - `[HasCompanyPermission(Permissions.Company.Members.Manage)]`: lee `{companyId}` de la ruta y verifica que la empresa sea de la organización y que el usuario tenga el permiso ahí.
  - `[HasPlatformPermission(PlatformPermissions.Tenants.Manage)]`: además exige `access=platform`.
- `IPermissionService.GetEffectiveAsync(userId, companyId?)` usa HybridCache con la clave `t:{tenantId}:perm:{userId}`, invalidada por usuario o por rol.
- Las rutas de organización llevan `[Access(Business)]` **y** el permiso. Con otro acceso responden 403 `Tenancy.Access.Wrong`.
- `GET /api/me` devuelve la cuenta, el **acceso activo**, si tiene espacio personal y sus **organizaciones** (id, nombre y estado, para elegir en el acceso B2B), el tenant activo, sus permisos efectivos (de organización y por empresa), las preferencias efectivas de cultura, zona y moneda y, desde la Etapa 5, los módulos prendidos del acceso activo (`features`, [modulos-habilitados](../rules/modulos-habilitados.md)), para que el front arme el menú y los formatos (`useFormat`). El front decide solo la experiencia de uso; **el backend decide el acceso**.

---

## 15. WhatsApp: un módulo quitable

La estructura sigue el diseño de la Etapa 6 del plan maestro de `../ArquitecturaBase` ([ADR 0007](../decisions/README.md)), para que las dos plantillas se armen igual.

- **Puertos en el núcleo, adaptadores en el módulo.** El núcleo nunca nombra a WhatsApp:
  - `ILoginCodeChannel` e `IInvitationChannel`: el núcleo trae el canal `"email"`. El módulo registra `"whatsapp"`. `LoginCodeChannel` e `InvitationChannel` son **valores**, no enums: el núcleo valida contra los canales registrados.
  - `IAccountNoticeChannel` (en `Application/Interfaces/Integrations/Messaging/`, Etapa 3), con el mismo patrón: recibe un `AccountNotice` cerrado (`LoginMethodChanged`, con qué pasó, el método enmascarado y la fecha; `ReviewLoginMethods`, con la organización; `DeletionRequested`, con la fecha; `DeletionCancelled`; `AccountDeleted`; y los de «Recuperar mi cuenta», al método nuevo: `RecoveryReceived`, `RecoveryApproved` y `RecoveryRejected`). Cada implementación atiende un tipo de método: el núcleo trae la de `Email` (canal `"email"`) y el módulo registra la de `Phone` (canal `"whatsapp"`). Sin el módulo, los teléfonos no reciben aviso, y no es un error. Siempre sale por el outbox, cifrado. "Exportación lista" sale solo por correo.
  - `IPhoneLinkObserver`: la cuenta y la administración avisan que cambió un teléfono, y el módulo suelta el contacto e invalida los enlaces.
  - El teléfono como dato de la cuenta e `IPhoneNumberParser` siguen en el núcleo. `PhoneUsage` queda en `Any | Mobile` y no nombra WhatsApp (`Mobile`: solo celular). En la E3 `Phone` es solo modelo; el ingreso, registro y la gestión de un teléfono se habilitan al registrar el canal del módulo en la E8.
- **Países de WhatsApp:** los controla el módulo en sus adaptadores y flujos (código por WhatsApp, vínculo y registro por WhatsApp), con `WhatsApp:AllowedCountries`, que se lee solo dentro de `Modules/WhatsApp`, y un error propio del módulo sobre el campo `phone`. Se controla solo para un número **nuevo**: achicar la lista no invalida un número existente. `GET /api/auth/methods` (núcleo) devuelve los canales encendidos con un formato neutro, y el módulo aporta sus países (`channels: [{ key: "whatsapp", countries: [...] }]`).
- **Carpetas:**
  - `Domain/WhatsApp`;
  - `Application/Modules/WhatsApp/{Configuration,Interfaces,Models,Services,Resources}`;
  - `Infrastructure/Modules/WhatsApp/{Cloud,Webhook,Inbound,Retention,Persistence}`;
  - `Api/Modules/WhatsApp`.

  El módulo aplica sus propias `IEntityTypeConfiguration`, con un `ApplyConfigurationsFromAssembly` filtrado por namespace.
- **Registro:** un `AddWhatsAppModule()` por capa, llamado desde `Program.cs`. Infrastructure no registra servicios de Application.
- **Encendido:** se enciende con `WhatsApp:PhoneNumberId`; sin él quedan las implementaciones de `Disabled/`. Las rutas del webhook se mapean solo si hay `AppSecret` y `VerifyToken`.
- **Quitarlo:** se sigue `docs/guides/quitar-whatsapp.md`: borrar las tres carpetas `Modules/WhatsApp` y `Domain/WhatsApp`, la línea de `Program.cs` y agregar una migración que borra sus tablas. El build y los tests del núcleo tienen que quedar en verde. `ModuleIsolationTests` verifica que el núcleo no referencie `*.Modules.*`.
- **Número de la plataforma** (Etapa 8): códigos de ingreso, invitaciones, avisos de la cuenta (`WhatsAppAccountNoticeChannel`) y el bot de ingreso. Plantillas (es y en): `codigo_ingreso`, `invitacion_organizacion` y, para los avisos de la cuenta, `aviso_metodo_ingreso`, `revisa_metodos_ingreso`, `baja_cuenta_pedida`, `baja_cuenta_cancelada` y `cuenta_eliminada`. Cuándo y cómo se crea cada plantilla: [`docs/operations/whatsapp-plantillas.md`](../operations/whatsapp-plantillas.md).
- **Envío por el outbox persistente** (`platform.OutboxMessages`), no en memoria. Esto **difiere a propósito** de ArquitecturaBase, que usa colas en memoria (`WhatsAppSendQueue`, `EmailQueue`) y documenta que un reinicio las pierde. Con varias organizaciones y réplicas, perder una invitación o un código no es aceptable (ADR 0014):
  - el caso de uso inserta el mensaje dentro de su transacción;
  - `OutboxDispatcher` lo envía;
  - el payload con códigos o enlaces va cifrado con DataProtection;
  - un reinicio no pierde mensajes.
- **Webhook** `GET/POST /webhooks/whatsapp`: anónimo, con rate limit y cuerpo de 5 MB como máximo.
  - Firma HMAC `X-Hub-Signature-256` sobre los bytes exactos del cuerpo.
  - Idempotente por `wamid`.
  - El procesamiento es asíncrono (`WhatsAppInboundProcessor`, con `SKIP LOCKED`).
- **Regla de oro:** un mensaje entrante nunca abre una sesión; solo puede producir un enlace de un solo uso.
- **Canales por organización** (Etapa 11, opcional): `platform.WhatsAppChannels` mapea `PhoneNumberId` → `TenantId`. El procesador resuelve el tenant por el número de destino y entra con `ITenantScope`.
- **Privacidad:** los números se enmascaran en los logs y el texto de los mensajes se borra a los 90 días.

---

## 16. Pipeline HTTP (`Program.cs`)

```
UseForwardedHeaders → UseSecurityHeaders → UseRequestLocalization → UseExceptionHandler → UseStatusCodePages
→ UseRateLimiter → UseAuthentication → TenantResolutionMiddleware → PublicSiteResolutionMiddleware
→ LegalAcceptanceMiddleware → UseAuthorization
→ (Development: bootstrap de base de datos, OpenAPI y Swagger UI | resto: UseHsts) → UseHttpsRedirection
→ UseStaticFiles → MapDefaultEndpoints → MapControllers → UseSpaFallback
```

Todo middleware que pueda cortar con un error va después de `UseStatusCodePages`.

---

## 17. Logging, OpenAPI, health, rate limiting, caché

- **Logging:** `[LoggerMessage]` obligatorio (`CA1848` como warning, que rompe el build). Nunca se registran códigos, tokens, enlaces, emails completos ni teléfonos. Cada log de negocio lleva un scope con `TenantId`.
- **OpenAPI:** `/openapi/v1.json` y Swagger UI solo en Development. El build exporta `docs/contracts/openapi.json` (`Microsoft.Extensions.ApiDescription.Server`), que el front usa para generar sus tipos. El CI falla si el archivo commiteado no coincide con el generado.
- **Health:** `/health` (readiness: base de datos, rol de runtime y WhatsApp) y `/alive`.
- **Rate limiting:**
  - por IP: `login-code`, `login-verify`, `invitation-accept` y `whatsapp-webhook`;
  - por organización: `tenant-api`, una ventana deslizante sobre `/api`.
  - Un 429 sale como ProblemDetails con `retryAfter`.
- **Caché:** HybridCache con claves de `CacheKeys`, siempre con prefijo: `t:{tenantId}:` (privado), `s:{businessTenantId}:` (página pública; se invalida al publicar), `u:{userId}:` (identidad y accesos) o `p:` (plataforma). Un test verifica que ninguna clave se arme sin prefijo.

---

## 18. Representación y formato de datos (unificado)

**Regla:** el backend **representa** los datos con tipos fijos e invariantes; quien **muestra** usa siempre el mismo formateador:
- el front, con `shared/format`;
- el backend, en correos, WhatsApp y documentos, con `DisplayFormatter`.

Nadie formatea a mano. El catálogo visual completo (cómo se ve cada tipo) está en `../ArquitecturaBaseMutitenantFront/docs/architecture/formatos.md`, y los dos lados se prueban contra los **mismos casos**: `docs/contracts/format-cases.json`.

### Contrato en la API (JSON)

| Dato | Tipo C# | JSON | Base de datos | Regla |
|---|---|---|---|---|
| Instante | `DateTime` (UTC) | `"2026-09-27T17:35:00Z"` | `timestamptz` | sufijo `Utc`; sin offset → 400 |
| Fecha civil | `DateOnly` | `"2026-09-27"` | `date` | sin zona, nunca se convierte |
| Hora civil | `TimeOnly` | `"14:30:00"` | `time` | horarios de atención, turnos |
| Entero | `int` / `long` | número | `integer` / `bigint` | |
| Decimal | `decimal` | número | `numeric(p,s)` explícito en la configuración EF | nunca `double` ni `float` para montos o cantidades |
| Moneda | `Money` (`Amount` + `Currency`) | `{ "amount": 1234.5, "currency": "ARS" }` | dos columnas: `numeric(19,4)` + `char(3)` | ISO 4217; el importe nunca viaja sin su moneda |
| Porcentaje | `decimal` como fracción | `0.125` (= 12,5 %) | `numeric(9,6)` | siempre fracción, nunca 12.5 |
| Enum | enum | `"Active"` (texto) | texto | el front lo traduce, el backend no manda textos |
| Teléfono | `PhoneNumber` | `"+5491155551234"` (E.164) | texto | |
| CUIT / id fiscal | `TaxId` (`Country` + `Type` + `Number`) | `{ "country": "AR", "type": "CUIT", "number": "20123456786" }` | tres columnas: `TaxCountry char(2)` + `TaxType varchar(8)` + `TaxNumber varchar(20)` | solo dígitos, validado con el dígito verificador; el formato con guiones (`20-12345678-6`) es de presentación |
| Vacío | `null` | `null` | `NULL` | nunca `""`, `0` ni `"N/A"` para "no hay dato" |

- **Redondeo:** `MidpointRounding.AwayFromZero`, a la cantidad de decimales de la moneda (ARS y USD: 2), **en el backend y al momento de calcular** (por línea y después el total). El front **no calcula** montos, solo los muestra; los totales vienen calculados.
- **Moneda por defecto** de la organización: `TenantSettings.DefaultCurrency` (ARS). Un importe siempre lleva su moneda explícita, aunque sea la de por defecto.
- Límite de precisión: JSON number alcanza para importes de hasta 13 dígitos enteros con 2 decimales. Más que eso requiere un ADR.

### Formato del lado del backend

- `Application/Common/Formatting/DisplayFormatter.cs` (BCL pura, `CultureInfo`) expone en E1 `Date`, `DateTime`, `Time`, `DateLong`, `Integer`, `Decimal`, `Money`, `Percent`, `Phone` y `Empty`; suma `TaxId` en E6, junto con el value object. Siempre recibe la **cultura y la zona explícitas**.
- Lo usan las plantillas de correo, los textos de WhatsApp y las exportaciones.
- `DisplayFormatterTests` recorre `docs/contracts/format-cases.json`; el front corre el mismo archivo. Si los dos lados no producen el mismo texto, falla el CI.

---

## 19. Front y hosting

- El SPA vive en `../ArquitecturaBaseMutitenantFront`. El SPA y la Api se sirven desde el **mismo origen en cada host**, el dominio principal y cada `<slug>.plataforma.com`: en producción desde wwwroot, con DNS y certificado comodín; en desarrollo, con el proxy de Vite por host (`*.localtest.me`). **No hay CORS**, tampoco en los subdominios.
- `BackendPrefixes` es una lista a mano, y se atiende igual en todos los hosts: `/api`, `/account`, `/connect`, `/signin-google`, `/.well-known`, `/webhooks`, `/health` y `/alive`. Un prefijo nuevo se suma ahí, al `SpaHostingTests` y al `server.proxy` de `vite.config.ts`.
- La arquitectura del front está en `../ArquitecturaBaseMutitenantFront/docs/architecture/frontend.md`.

---

## 20. Reglas de datos que se aplican solas

Estándares adoptados el 2026-09-27 (P1 a P8 de [`estandares.md`](estandares.md)). Cada uno tiene su ficha y su verificación. La idea es que **quien programa no tenga que acordarse**: la mayoría se aplica por un conversor, una convención o un atributo.

| Qué | Cómo se aplica | Ficha |
|---|---|---|
| Textos que entran | `NormalizedStringJsonConverter` global (trim, NFC, sin invisibles; vacío → `null`) + tipos de texto con `TextLimits` | [textos-libres](../rules/textos-libres.md) |
| Correos | value object `Email` (minúsculas, IDN, único) + conversor EF por convención | [emails](../rules/emails.md) |
| Teléfonos | value object `PhoneNumber` (E.164) + `IPhoneNumberParser` con `PhoneUsage` | [telefonos](../rules/telefonos.md) |
| CUIT, CUIL y DNI | value object `TaxId` (país + tipo + dígitos) con dígito verificador | [identificacion-fiscal](../rules/identificacion-fiscal.md) |
| Ediciones simultáneas | `IVersioned` → `xmin`; `version` en el contrato; 409 `General.ConcurrencyConflict` | [concurrencia](../rules/concurrencia.md) |
| Orden alfabético | base creada con collation ICU `es-AR`; se verifica al arrancar | [persistencia-ef](../rules/persistencia-ef.md) |
| Doble clic o reintento | `[Idempotent]` + `Idempotency-Key` + `platform.IdempotencyKeys` | [idempotencia](../rules/idempotencia.md) |
| Términos y privacidad | `LegalDocuments` versionados, `LegalAcceptances`, `LegalAcceptanceMiddleware` | [datos-personales](../rules/datos-personales.md) |
| Módulos por organización | `Microsoft.FeatureManagement` + `TenantFeatureFilter` + `[FeatureGate]` (404 si está apagado) | [modulos-habilitados](../rules/modulos-habilitados.md) |

**Pipeline:** `LegalAcceptanceMiddleware` va después de `TenantResolutionMiddleware` y antes de `UseAuthorization`; actúa solo con identidades autenticadas, nunca en rutas anónimas. `IdempotencyFilter` y `[FeatureGate]` son filtros de MVC, así que corren en la acción.
