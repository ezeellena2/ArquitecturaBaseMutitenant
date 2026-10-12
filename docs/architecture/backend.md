# Arquitectura del backend

> Panorama de la arquitectura del backend. Las reglas mandan en las fichas de [`docs/rules/`](../rules/README.md) y en [`AGENTS.md`](../../AGENTS.md): si este documento las contradice, se corrige este. El aislamiento entre organizaciones tiene su propio documento, [`multitenancy.md`](multitenancy.md), y se lee junto con este.

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
| Caché | `HybridCache`: en memoria hasta la Etapa 9; desde ahí, Redis como caché compartida entre instancias (plan, Etapa 9) |
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

**`tools/` solo tiene el soporte del E2E real** (`RealE2ESetup`, que prepara su base aislada), nunca algo para operar ([ADR 0017](../decisions/README.md), enmienda del 2026-10-03):
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
| Domain | nada | entidades, value objects, `<Entidad>Errors`, `Result`, catálogos de permisos, enums; `CurrencyCode` valida sintaxis y `Money.Round(minorUnits)` recibe un dato |
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
| **S**, responsabilidad única | Un servicio por área funcional, con **8 dependencias de negocio como máximo** (lo verifica un test; ADR 0039). No cuentan `ILogger<T>`, `TimeProvider`, `IRequestValidator` ni `IUnitOfWork`; todo lo demás cuenta, incluido `IOptions<T>`. Lo que crece se parte por responsabilidad en helpers, con un sufijo de la lista cerrada de [capas-y-flujo](../rules/capas-y-flujo.md): `*Policy` (decide), `*Guard` (protege una regla y devuelve el error), `*Issuer` (emite códigos, tickets, invitaciones o avisos), `*Verifier` (verifica lo que entra), `*Linker` (vincula un método con algo de afuera) y los demás de esa tabla (`ApplicationServicesTests` la lee). Los helpers no guardan ni reciben `IUnitOfWork`; no crear clases que solo reenvían |
| **O**, abierto/cerrado | Módulos quitables (`Modules/WhatsApp` en Application, Infrastructure y Api), que se enchufan al núcleo por puertos: `ILoginCodeChannel`, `IInvitationChannel`, `IAccountNoticeChannel` y, en la E8, `IPhoneLinkObserver`. El núcleo no cambia cuando se agrega o se quita un módulo, y no referencia ningún `*.Modules.*` (test). Los errores y permisos nuevos se suman a catálogos, sin tocar el mapper |
| **L**, sustitución de Liskov | Las implementaciones de un puerto respetan el contrato completo, incluido el "fallo cerrado" (por ejemplo, `DisabledWhatsAppAvailability` responde "no disponible", no lanza). Los dobles de test implementan la misma semántica (`FakeUnitOfWork` usa la misma `CommitPolicy`) |
| **I**, segregación de interfaces | Lectura y escritura separadas (`I<X>Reader` / `I<X>Repository`). `ISignInService` con 12 miembros como máximo (test). `ICurrentUser`, `ITenantContext` e `IRequestInfo` separados |
| **D**, inversión de dependencias | Application define los puertos; Infrastructure y Api los implementan. Cada capa se registra en su `DependencyInjection.cs` y `Program.cs` solo compone |

---

## 4. Carpetas y archivos que marcan el patrón

El mapa de carpetas y los archivos modelo están en [`arbol.md`](arbol.md).

Solo se listan las carpetas y los archivos que marcan el patrón. Un área nueva copia la forma de **Roles**, que es el área de referencia ([ADR 0004](../decisions/README.md)).

### 4.1 Domain

```
Domain/
├─ Common/
│  ├─ Entity.cs                  Id Guid v7, private init; constructor protegido para EF
│  ├─ ValueObject.cs
│  ├─ IAuditable.cs              CreatedAtUtc, CreatedBy, ModifiedAtUtc, ModifiedBy
│  ├─ ISoftDeletable.cs          IsDeleted, DeletedAtUtc, DeletedBy
│  ├─ IVersioned.cs              versión de concurrencia (xmin)
│  ├─ ITenantOwned.cs            dato privado: Guid TenantId (lo sella el interceptor, inmutable)
│  ├─ ICompanyOwned.cs           Guid CompanyId (siempre también ITenantOwned)
│  ├─ IPublishedByBusiness.cs    dato público: BusinessTenantId, IsPublished
│  ├─ IConsumerBusinessShared.cs dato compartido: ConsumerTenantId (espacio personal), BusinessTenantId (organización), ReplaceConsumerCopy()
│  └─ Party, PartyPolicy, NotAuditedAttribute, TextLimits
├─ Results/                      Error, ErrorType, Result, Result<T>, ValidationError
├─ ValueObjects/                 Email, PhoneNumber, TaxId (país + tipo + número; validadores en TaxIdValidators/),
│                                Money (monto + moneda ISO 4217), CurrencyCode (sintaxis alfa-3), CultureCode (sintaxis)
├─ ReferenceData/                los cinco catálogos globales (monedas, países, zonas, culturas y tipos fiscales) y sus traducciones
├─ Tenancy/                      Tenant, TenantKind (Personal|Business), TenantStatus, TenantSlug, ReservedTenantSlug, Member,
│                                MemberStatus, Company, CompanyStatus, CompanyMembership, CompanyMembershipStatus, TenantDomain
│                                (dominio de correo de la organización), TenantErrors, MemberErrors, AccessErrors y los demás *Errors
├─ Invitations/                  Invitation, InvitationChannel (valor), InvitationStatus, InvitationErrors
├─ PublicSite/                   PublicPage (nombre, logo, descripción, contacto e IsPublished; bloqueo de la plataforma:
│                                PublishBlockedAtUtc, PublishBlockedReason, PublishBlockedByUserId), PublicPageErrors
├─ Users/                        Access (Consumer|Business|Platform), UserStatus (Active|Suspended|PendingDeletion|Deleted),
│                                UserErrors, EmailErrors, PhoneErrors
├─ Authentication/               LoginCode, LoginAudit, LoginMethod, LoginCodeChannel, *Errors (LoginLink, en la E8)
├─ Authorization/
│  ├─ Permissions.cs             catálogo de la organización (tenant) y de empresa, con All / OrganizationScoped / CompanyScoped
│  ├─ PersonalPermissions.cs     catálogo personal (personal.*), implícitos de la persona en su espacio personal
│  ├─ PlatformPermissions.cs     catálogo de plataforma (separado a propósito)
│  ├─ Role.cs                    TenantId, Name, Description, Scope, CompanyId? (solo con SpecificCompany), IsSystem, Permissions
│  ├─ RoleScope.cs
│  ├─ RoleAssignment.cs          UserId, RoleId, CompanyId? (null ⇒ toda la organización); Check dice dónde vale un rol
│  ├─ SystemRoles.cs             TenantAdmin ("Dueño"), CompanyAdmin ("Administrador")
│  └─ RoleErrors.cs, RoleAssignmentErrors.cs
├─ Platform/                     PlatformPermissionAssignment (permisos individuales por operador), AccountRecoveryRequest
│                                («Recuperar mi cuenta»), AccountRecoveryStatus y sus *Errors
├─ Settings/                     TenantSettings (cultura, zona y moneda por defecto), PlatformSettings, SettingsErrors
├─ Legal/                        LegalDocument, LegalDocumentContent (una fila por cultura), LegalDocumentKind, LegalAcceptance,
│                                LegalErrors, LegalPublicationErrors, AccountDeletionCancelTicket, ConsumerDataRetention (ledger técnico de copia retenida), AccountDeletionErrors (CancelProofInvalid, LeaseInvalid, LastAdmin,
│                                PlatformOperator, AlreadyPending, Blocked, NotPending); DataExport y DataExportStatus llegan en E10
├─ Features/                     Features.cs (catálogo de módulos: clave, lado y si viene prendido), TenantFeature (habilitación
│                                por espacio, con origen y vencimiento) (P8)
├─ Auditing/                     AuditEntry (tenant), SecurityEvent (plataforma), AuditAction, AuditActorKind, AuditChangeKeys
├─ Messaging/                    OutboxMessage, OutboxChannel (valor: "email" y los que sumen los módulos), OutboxStatus
└─ WhatsApp/                     entidades del módulo (Etapa 8): WhatsAppContact, WhatsAppMessage, WhatsAppCodeQuota, *Errors;
                                 WhatsAppChannel pertenece a E11
```

Reglas de Domain:
- Sin eventos de dominio ([ADR 0003](../decisions/README.md)).
- Fábricas estáticas que validan invariantes con `ArgumentException` (un bug) y métodos que devuelven `Result` para las reglas de negocio.
- Propiedades con `private set` y constructor privado para EF.

### 4.2 Application

```
Application/
├─ DependencyInjection.cs              AddApplication(): un *Registration.cs por área (Services/<Área>/), con sus options,
│                                      servicios y helpers uno por uno; validadores y DisplayFormatter
├─ Common/
│  ├─ Pagination/                      PagedRequest, PagedResult<T>, CursorRequest, CursorResult<T>, SortDescriptor
│  ├─ Validation/                      IRequestValidator, RequestValidator, ValidationRules, FieldErrors, PagedRequestValidator<T>,
│  │                                   CursorRequestValidator<T>
│  ├─ Logging/                         OperationLog.RunAsync(logger, timeProvider, "Operación", trabajo): inicio, fin y código de error
│  ├─ Formatting/                      DisplayFormatter, CultureProfiles, SupportedCultures (desde el catálogo; §18)
│  ├─ Text/                            TextNormalizer, PersonalDataMask (correo y teléfono enmascarados)
│  └─ Exceptions/                      UniqueConstraintViolationException, ForeignKeyViolationException, ConcurrencyConflictException,
│                                      InvalidCursorException
├─ Configuration/<Área>/               opciones funcionales (LoginCodeOptions, InvitationOptions, OutboxOptions) con SectionName
├─ Interfaces/
│  ├─ Services/                        I<X>Service, uno por área (lo que inyectan los controllers), entre ellos IAccountDeletionService,
│  │                                   IPermissionService e IPlatformPermissionService; IPendingItemSource (ADR 0040)
│  ├─ ReferenceData/                   ICurrencyCatalog, ICountryCatalog, ITimeZoneCatalog, ICultureCatalog, ITaxIdTypeCatalog
│  ├─ Persistence/                     IUnitOfWork, CommitPolicy, I<X>Repository, I<X>Reader, ITenantScope, IAuditLog,
│  │                                   ILegalRepository, ILegalReader, UniqueKeys y ForeignKeys (los nombres de la base)
│  └─ Integrations/
│     ├─ Request/                      ICurrentUser, ITenantContext, ITenantAccessInitializer, IRequestInfo, IPublicOrigin,
│     │                                IInvitationFlowContext; IPublicSiteContext (BusinessTenantId? y Slug? públicos)
│     ├─ Identity/                     ISignInService, IUserLookup, IUserStatusReader, ITokenRevoker, IGoogleAvailability
│     ├─ Security/                     ISecureTokenGenerator, ILoginCodeGenerator, ILoginCodeHasher, IPayloadProtector,
│     │                                IInvitationTokenProtector
│     ├─ Messaging/                    IOutbox, IEmailTemplateRenderer, IChannelSender
│     ├─ Caching/                      ITenantSettingsCache, IRolePermissionCache, IAccessStatusCache… (se invalidan después del commit)
│     ├─ Time/                         ITimeZoneService
│     ├─ Phones/                       IPhoneNumberDisplayFormatter (E1), IPhoneNumberParser (E3)
│     ├─ Dns/                          IDnsTxtLookup (verificar el dominio de correo)
│     ├─ Legal/                        IAccountDeletionParticipant (CheckAsync, OnRequestedAsync, OnCancelledAsync y ExecuteAsync),
│     │                                IRetainedOnConsumerDeletion (política por tipo/motivo/plazo), IConsumerCopyDeletion (reemplazo en alcance y UoW del llamador)
│     ├─ Features/                     IFeatureCatalog (IFeatureService está en Services/)
│     (en Messaging/ e Identity/ están los puertos que usan los módulos: ILoginCodeChannel, IInvitationChannel,
│      IAccountNoticeChannel; IPhoneLinkObserver e ILoginLinkIssuer, en la E8)
├─ Models/<Área>/                      <Acción>Request, <X>Response y <X>Row (las filas de lectura, en la misma carpeta)
├─ Validation/<Área>/                  <Acción>RequestValidator (internal sealed, AbstractValidator<T>)
├─ Services/
│  ├─ Auth/                            ingreso, registro y alta de empresa: AccountService (registro B2C), LoginCodeService,
│  │                                   ExternalLoginService, ConnectService (emisión + cambio de acceso u organización),
│  │                                   BusinessSignupService, BusinessSignupPolicy y BusinessOrganizationCreator («Registrá tu
│  │                                   empresa», alta B2B), TenantProvisioner (idempotente; lo comparte Plataforma),
│  │                                   AccountRecoveryService («Recuperar mi cuenta»), SignupPolicy, AccessSwitchPolicy,
│  │                                   UserCultureResolver
│  ├─ Identity/                        métodos de ingreso de la cuenta: LoginMethodManagementService,
│  │                                   AccountGoogleService, GoogleProfileWriter (claims GivenName/Surname, sólo partes vacías); sesión iniciada y posesión del método nuevo (ADR 0045)
│  ├─ Invitations/                     InvitationService, InvitationIssuer (lo usan OrganizationUserInvitationWriter y
│  │                                   PlatformTenantCreator), InvitationExpiryService
│  ├─ Profile/                         ProfileService (nombre y apellido opcionales, idioma, zona), ProfileSnapshotBuilder
│  ├─ Legal/                           LegalService y LegalAcceptanceService (documentos vigentes y aceptación), AccountDeletionService,
│  │                                   AccountDeletionPolicy, AccountDeletionProcessor y AccountDeletionProcessingService (pedir, cancelar, ejecutar la baja y procesar copias retenidas),
│  │                                   LastTenantAdminGuard (la única Dueña, ADR 0035); los participantes de la baja están en
│  │                                   Infrastructure/Identity/Deletion; sus modelos y validadores van en Models/Legal/ y Validation/Legal/
│  ├─ Home/                            HomeService (inicio de la organización, ADR 0040), PendingItemOrder
│  ├─ OrganizationUsers/               OrganizationUserService (Usuarios: miembros e invitaciones) y sus helpers
│  ├─ Roles/                           RoleService, RoleAssignmentGuard, PermissionService (permisos efectivos cacheados),
│  │                                   PlatformPermissionService           ← ÁREA DE REFERENCIA
│  ├─ Companies/                       CompanyService, CompanyMembershipService, LastCompanyAdminGuard
│  ├─ OrganizationSettings/            OrganizationSettingsService, OrganizationDomainService (dominios de correo)
│  ├─ PublicSite/                      PublicPageService (la página de la organización: borrador, publicar y retirar);
│  │                                   PublicSiteService y DirectoryService (lectura pública y listado)
│  ├─ Personal/                        (E7) los servicios de los módulos B2C del producto
│  ├─ Auditing/                        OrganizationAuditService (lectura), OrganizationAuditEntryBuilder
│  ├─ Messaging/                       OutboxDispatchService
│  ├─ ReferenceData/                   ReferenceDataService (catálogos traducidos para GET /api/reference-data)
│  ├─ Platform/                        TenantAdministrationService, PlatformAccountService, PlatformAccountStateService,
│  │                                   PlatformAccountDeletionService (ADR 0035), PlatformOperatorService y
│  │                                   PlatformOperatorInvitationService (ADR 0042), PlatformRecoveryService, PlatformLegalService,
│  │                                   PlatformFeatureService, PlatformPublicSiteService, PlatformDomainService, PlatformAuditService,
│  │                                   PlatformSettingsService, PlatformActionGuard
├─ Modules/WhatsApp/                   (E8) módulo quitable: Configuration, Interfaces, Models, Services, Resources y WhatsAppModule.cs
└─ Resources/
   ├─ Errors.resx / Errors.en.resx             clave = código del error + Title.<ErrorType>
   ├─ Validation.resx / .en.resx
   ├─ Formatting.resx / .en.resx               relativos, unidades, booleanos y enums de los mensajes
   ├─ Permissions.resx / .en.resx               Area.<x>, Permission.<code>, PermissionDescription.<code>
   ├─ RoleNames.resx / .en.resx                 SystemRole.<rol>: nombre y descripción de los roles de sistema
   ├─ Notifications.resx / .en.resx             textos de correo y WhatsApp (asunto, cuerpo, bot)
   ├─ Audit.resx / .en.resx                     AuditAction.<code>, AuditSummary.<Entidad>, Entity.*, Field.*, Value.*, SecurityEvent.*
   └─ ErrorTexts.cs, ValidationTexts.cs, FormattingTexts.cs, PermissionTexts.cs, RoleTexts.cs, NotificationTexts.cs, AuditTexts.cs
```

`NeutralLanguage=es` y `InternalsVisibleTo` para Application.UnitTests y Api.IntegrationTests. Los servicios y helpers son `internal sealed partial`: `partial` porque llevan `[LoggerMessage]`.

El tope de constructores es de **8 dependencias de negocio**: excluye únicamente `ILogger<T>`, `TimeProvider`, `IRequestValidator` e `IUnitOfWork`; `IOptions<T>` y cualquier otra dependencia cuentan ([ADR 0039](../decisions/0039-tope-de-dependencias-de-negocio.md)). Si una clase se pasa, se divide por responsabilidad, sin clases que solo reenvían. La transacción la abre el método público del servicio de entrada, como `RoleService`; sus helpers trabajan dentro y no reciben `IUnitOfWork`. No se agregan excepciones de helpers a `TransactionBoundaryTests`. La consolidación previa a la E7 llevó a esta regla todos los servicios que existían (bloque 3).

### 4.3 Infrastructure

```
Infrastructure/
├─ DependencyInjection.cs              AddInfrastructure(cfg, env) → llama a los *Registration
├─ Persistence/
│  ├─ ApplicationDbContext.cs          IdentityUserContext<ApplicationUser, Guid> + IDataProtectionKeyContext; esquemas y filtros
│  ├─ PersistenceRegistration.cs
│  ├─ UnitOfWork.cs                    ExecuteInTransactionAsync + SET LOCAL app.tenant_id
│  ├─ TenantContext.cs                 holder scoped (implementa ITenantContext y ITenantScope)
│  ├─ ConstraintViolations.cs          traduce las violaciones de UniqueKeys y ForeignKeys
│  ├─ Configurations/
│  │  ├─ Platform/                     TenantConfiguration, PlatformSettingsConfiguration, SecurityEventConfiguration,
│  │  │                                OutboxMessageConfiguration, PlatformPermissionAssignmentConfiguration, IdempotencyKeyConfiguration,
│  │  │                                LegalDocumentConfiguration, LegalDocumentContentConfiguration, TenantFeatureConfiguration,
│  │  │                                TenantDomainConfiguration, AccountRecoveryRequestConfiguration…;
│  │  │                                ReferenceData/ (una configuración por catálogo y por tabla de traducción, E2, sin RLS)
│  │  ├─ Identity/                     ApplicationUserConfiguration, LoginMethodConfiguration (un método verificado es único en todo
│  │  │                                el sistema; uno pendiente, dentro de su cuenta), LegalAcceptanceConfiguration,
│  │  │                                LoginCodeConfiguration…
│  │  ├─ Tenant/                       MemberConfiguration, CompanyConfiguration, CompanyMembershipConfiguration,
│  │  │                                RoleConfiguration, RoleAssignmentConfiguration, TenantSettingsConfiguration,
│  │  │                                InvitationConfiguration, AuditEntryConfiguration…
│  │  ├─ PublicSite/                   PublicPageConfiguration (esquema public_site, EnablePublicRls)
│  ├─ Conventions/                     IVersioned → xmin, Email y PhoneNumber como su valor canónico
│  ├─ Interceptors/
│  │  ├─ TenantConnectionInterceptor.cs    set_config('app.tenant_id') al abrir la conexión
│  │  ├─ TenantStampInterceptor.cs         sella la columna de tenant de su clase en los Added; rechaza cambios de TenantId,
│  │  │                                    BusinessTenantId o ConsumerTenantId
│  │  ├─ AuditableEntityInterceptor.cs
│  │  ├─ SoftDeleteInterceptor.cs
│  │  ├─ AuditTrailInterceptor.cs          genera AuditEntry con el diff de las entidades IAuditable
│  │  └─ RoleAssignmentTrail.cs, AccessChangeTrail.cs   una entrada por persona y lugar, y por entrada o salida (auditoria.md)
│  ├─ Rls/
│  │  ├─ RlsMigrationBuilderExtensions.cs  migrationBuilder.EnableTenantRls("tenant", "<Tabla>")
│  │  ├─ TenantIsolationModelValidator.cs  falla al arrancar si una entidad no está clasificada, o no tiene esquema o filtro
│  │  └─ RuntimeRoleValidator.cs           al arrancar: el login de runtime no es superuser, ni BYPASSRLS, ni dueño
│  ├─ Extensions/                      ModelBuilderExtensions (filtros con nombre "Tenant", "Public", "Parties" y "SoftDelete"),
│  │                                   QueryableExtensions (ApplySort, ApplySearch, ToPagedResultAsync), AdvisoryLockExtensions,
│  │                                   AdvisoryLockKeys, TransactionExtensions (RequireTransaction)
│  ├─ AccessIndex/                     UserTenantAccess: proyección global de las membresías de cada cuenta
│  ├─ Repositories/                    <X>Repository (internal sealed)
│  ├─ Readers/                         <X>Reader (AsNoTracking + proyección a *Row/*Response)
│  ├─ Migrations/                      una sola carpeta; RLS y grants dentro de las migraciones
│  ├─ Seed/                            ReferenceData/*.json (E1), ReferenceDataSeeder (E2, upsert idempotente),
│  │                                   PlatformSeeder (operador inicial, con todos los permisos de Plataforma), OpenIddictSeeder,
│  │                                   LegalDocumentSeeder, DatabaseSeeder
│  └─ DatabaseBootstrapExtensions.cs   solo en Development: roles mt_owner y mt_app + migraciones + seed
├─ Identity/
│  ├─ ApplicationUser.cs               IsPlatformOperator, Status (Active | Suspended | PendingDeletion | Deleted), Culture, TimeZoneId,
│  │                                   FirstName y LastName opcionales, DisplayName persistido derivado (ADR 0046), LastBusinessTenantId (la última organización usada en el lado empresa; no se vuelve
│  │                                   al último lado), DeletionRequestedAtUtc, DeletionScheduledForUtc, DeletionReason, DeletedAtUtc.
│  │                                   Email y PhoneNumber son solo una copia del método principal, sin índice único (la unicidad está
│  │                                   en LoginMethods). Sin setters públicos: los cambios pasan por métodos que aplican las reglas
│  │                                   de la cuenta
│  ├─ IdentityRegistration.cs          Identity core, cookies de /account y /connect, DataProtection, Google (se enciende con su ClientId)
│  ├─ SignInService.cs                 (ISignInService)
│  ├─ UserLookup.cs, UserStatusReader.cs   búsqueda global por valor de método y estado de una identidad
│  ├─ CloudflareDnsTxtLookup.cs        (IDnsTxtLookup) consulta TXT por DNS sobre HTTPS
│  ├─ Deletion/                        participantes de la baja de una cuenta (espacio personal, membresías, aceptaciones,
│  │                                   outbox y pedidos de recuperación)
│  └─ OpenIddict/                      OpenIddictRegistration, AuthServerDefaults, CertificateLoader, TokenRevoker, WebClientOptions
├─ Messaging/
│  ├─ Outbox.cs                        IOutbox: inserta OutboxMessage dentro de la transacción del caso de uso
│  ├─ OutboxDispatcher.cs              BackgroundService: FOR UPDATE SKIP LOCKED, reintentos con backoff
│  ├─ Email/                           EmailChannelSender, IEmailTransport (SmtpEmailTransport; PickupDirectoryEmailTransport en Development),
│  │                                   MimeMessageFactory, EmailTemplateRenderer, Templates/*.html
│  └─ MessagingRegistration.cs
├─ Modules/WhatsApp/                   (E8) cliente/renderers/opciones en la raíz; Webhook/, Inbound/, Retention/, Persistence/ (sus configuraciones EF),
│                                      WhatsAppInfrastructureModule, WhatsAppOptions(+Validator), Disabled/
├─ Security/                           SecureTokenGenerator, LoginCodeGenerator, LoginCodeHasher, PayloadProtector, InvitationTokenProtector
├─ Time/                               TimeZoneService (TimeZoneInfo, IANA)
├─ ReferenceData/                      JsonReferenceDataCatalog (E1: JSON versionados; E2: ReferenceDataReader + HybridCache)
├─ Phones/                             LibPhoneNumberDisplayFormatter (E1), LibPhoneNumberParser (E3)
├─ Caching/                            CacheKeys (siempre con prefijo de alcance: t: / s: / u: / p:), CachingRegistration
├─ Idempotency/                        IdempotencyStore, IdempotencyCleanupWorker (P6)
├─ Features/                           FeaturesRegistration, TenantFeatureFilter, FeatureService (P8)
└─ BackgroundJobs/                     TenantJobRunner (recorre organizaciones activas y ejecuta en su alcance), AccountDeletionWorker
                                       (cada hora, SKIP LOCKED; toda cuenta con DeletionScheduledForUtc vencido, esté PendingDeletion
                                       o Suspended, por pasos; además, hasta 100 copias retenidas vencidas, con scope nuevo y Enter antes de UoW: multitenancy.md §3.2), AccountRecoveryNoticeWorker, InvitationExpiryWorker
```

### 4.4 Api

```
Api/
├─ Program.cs                          compone: AddServiceDefaults, AddApplication, AddInfrastructure, AddPresentation
│                                      (AddWhatsAppModule, una línea por módulo, en la E8) + pipeline
├─ DependencyInjection.cs              AddPresentation(): ProblemDetails, exception handler, auth, localización, rate limit,
│                                      OpenAPI, controllers + JSON
├─ appsettings.json / appsettings.Development.json
├─ Controllers/
│  ├─ Auth/                            ConnectController, SignupController (persona), LoginCodeController, ExternalLoginController,
│  │                                   InvitationsController, LoginMethodsController, BusinessSignupController («Registrá tu
│  │                                   empresa», POST /api/auth/business-signup), TenantSlugAvailabilityController, ContactController,
│  │                                   AccountRecoveryController («Recuperar mi cuenta»), AccountDeletionCancelController
│  │                                   (POST /api/auth/deletion/cancel); LoginLinkController, en la E8
│  ├─ Account/                         MeController (cuenta y accesos), AccountLoginMethodsController, AccountGoogleController,
│  │                                   LegalController (GET /api/legal/terms y /privacy, anónimos),
│  │                                   LegalAcceptanceController (GET /api/legal/current y POST /api/legal/accept),
│  │                                   AccountDeletionController (POST /api/me/deletion)
│  ├─ ReferenceData/                   ReferenceDataController ([AllowAnonymous]; GET /api/reference-data y por catálogo)
│  ├─ Organization/                    [Access(Business)] HomeController, OrganizationUsersController (api/users), RolesController,
│  │                                   PermissionsController, CompaniesController, CompanyMembersController, CompanyRolesController,
│  │                                   CompanyCandidatesController, CompanyInvitationsController,
│  │                                   OrganizationSettingsController y OrganizationDomainsController (api/organization/settings),
│  │                                   PublicPageController (api/organization/public-page: mi página pública, publicpage.manage),
│  │                                   OrganizationAuditController (api/organization/audit)
│  ├─ Personal/                        (E7) [Access(Consumer)] los controllers de los módulos B2C del producto
│  ├─ PublicSite/                      (E7) PublicSiteController ([PublicSite][AllowAnonymous], página por subdominio),
│  │                                   DirectoryController ([AllowAnonymous], dominio principal: lee solo lo publicado)
│  ├─ Platform/                        PlatformTenantsController, PlatformPublicSiteController (moderar la página pública: POST
│  │                                   .../public-site/unpublish y .../allow-publish, con motivo), PlatformDomainsController,
│  │                                   PlatformFeaturesController, PlatformAccountsController (incluye la baja iniciada por la
│  │                                   plataforma, POST /api/platform/accounts/{id}/deletion), PlatformOperatorsController,
│  │                                   PlatformRecoveriesController, PlatformLegalDocumentsController, PlatformAuditController,
│  │                                   PlatformSettingsController
├─ Contracts/<Área>/                   <Acción>HttpRequest, <X>Query (records sealed, props nullable; ToString() sin PII);
│                                      <X>HttpResponse solo con value objects o de protocolo (ADR 0002, enmienda)
├─ Authorization/
│  ├─ HasPermissionAttribute.cs              permiso de organización
│  ├─ HasCompanyPermissionAttribute.cs       permiso evaluado en la empresa {companyId} de la ruta
│  ├─ HasPlatformPermissionAttribute.cs      solo access=platform
│  ├─ PermissionPolicyProvider.cs            arma "perm:<p>", "cperm:<p>", "pperm:<p>"
│  └─ PermissionAuthorizationHandler.cs, PermissionRequirement.cs   un solo handler para los tres catálogos
├─ RequestContext/                     CurrentUser (claims), RequestInfo (IP, user agent), PublicOrigin, InvitationFlowContext
├─ Tenancy/
│  ├─ TenantResolutionMiddleware.cs    claims access + tenant_id + tenant_kind → TenantContext; activos (caché)
│  ├─ AccessAttribute.cs, AccessFilter.cs   [Access(Consumer|Business|Platform)] → 403 Tenancy.Access.Wrong
│  ├─ TenantClaimTypes.cs              access, tenant_id, tenant_kind
│  └─ (E7) PublicSiteResolutionMiddleware, PublicSiteAttribute y PublicSiteContext: subdominio → IPublicSiteContext
├─ Authentication/                     OpenIdPrincipalFactory (claims y destinos), PendingDeletionCookie
├─ ErrorHandling/                      ApiErrorCodes, ProblemDetailsMapper, ControllerResultExtensions, GlobalExceptionHandler,
│                                      MvcInvalidModelStateResponseFactory, EmptyJsonBodyContentTypeFilter, RetryAfterHeaderFormatter
├─ Json/                               UtcDateTimeConverter, DateOnly y TimeOnly, MoneyJsonConverter, EmailJsonConverter,
│                                      NormalizedStringJsonConverter, RawTextAttribute
├─ Localization/                       LocalizationExtensions (es por defecto, en; Accept-Language)
├─ OpenApi/                            OpenApiExtensions, ProblemResponsesConvention, ProducesProblemAttribute, OwnProtocolAttribute
├─ RateLimiting/                       RateLimitPolicies, RateLimitingExtensions, RateLimitingOptions (por IP y por organización)
├─ Idempotency/                        IdempotentAttribute, IdempotencyFilter
├─ Features/                           FeatureGateMiddleware, DisabledFeatureHandler (módulo apagado → 404)
├─ Legal/                              LegalAcceptanceMiddleware
├─ Modules/WhatsApp/                   (E8) WhatsAppApiModule, WhatsAppWebhookController, ConditionalWhatsAppRouteConvention
└─ Hosting/                            ForwardedHeadersExtensions, SecurityHeadersExtensions, SpaExtensions (BackendPrefixes)
```

### 4.5 AppHost y ServiceDefaults

- **`AppHost/AppHost.cs`:**
  - `AddPostgres("postgres", password, port: 5434)`, con un volumen persistente `arquitecturabase-multitenant-pgdata` y `ContainerLifetime.Persistent`.
  - Desde [E2], tres cadenas explícitas: `postgres-bootstrap` (superusuario, solo para preparar roles y base en Development), `appdb-admin` (`mt_owner`, migraciones y seed en Development) y `appdb` (`mt_app`, operaciones de la Api). Las contraseñas son parámetros secretos de Aspire; `AddDatabase("appdb")` de [E0] se retira porque crearía la base antes de elegir ICU `es-AR`.
  - Api en `https://localhost:7280` (ArquitecturaBase usa 7180, así los dos pueden correr a la vez). El bootstrap crea o comprueba la base ICU `es-AR` antes de servir requests y rechaza una base preexistente con tablas y collation equivocada; el arranque normal comprueba que `mt_app` no tenga privilegios para saltar RLS.
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
│  └─ TestDoubles/                        FakeUnitOfWork, FakeTenantContext, ServiceFixture
├─ *.Api.IntegrationTests/
│  ├─ Support/                            ApiFactory (WebApplicationFactory + Testcontainers), TenantFixture, AccountJourney y
│  │                                      OrganizationJourney (registro e ingreso reales), PlatformJourney, PickupCodeReader
│  ├─ TestFeatures/                       controllers y entidades solo para tests (Isolation, Access, Auditing, Features…)
│  ├─ Tenancy/                            CrossTenantIsolationTests, AccessTests, PublicAndSharedRowsTests, RlsPolicyInventoryTests,
│  │                                      RuntimeRoleTests, CacheKeyScopeTests, SubdomainTests, EngagementTests y PersonalIsolationTests
│  ├─ Persistence/                        UnitOfWorkTests, PaginationTests, CursorPaginationTests, SearchTests, SortIndexTests
│  ├─ Hosting/                            HealthCheckTests, SpaHostingTests, SecurityHeadersTests
│  ├─ Legal/                              LegalAcceptanceTests, AccountDeletionTests
│  ├─ Contracts/                          ExplicitRouteInventoryTests, OpenApiTests, OpenApiContractTests
│  └─ <Área>/                             un archivo por controller
└─ *.ArchitectureTests/                   HarnessTests, LayerDependencyTests, ProjectReferencesTests, ApplicationPackagesTests,
                                          ApplicationPublicApiTests, ApplicationServicesTests, ControllerServiceRepositoryTests,
                                          ControllerInputContractTests, ControllerOutputContractTests, PermissionAuthorizationTests,
                                          TransactionBoundaryTests, ErrorCodeTests, EntityConfigurationTests, DataClassificationTests,
                                          ServiceDependencyCountTests, MinimalApiRoutesTests, TenantScopeUsageTests,
                                          QueryFilterBypassTests, IdentityAccessTests, AccessDeclarationTests, VersionedContractTests,
                                          DecimalPrecisionTests, TextLimitsTests; ModuleIsolationTests, en la E8
```

Es un resumen. El árbol completo, con la etapa en que nace cada test, está en [`arbol.md`](arbol.md).

---

## 5. Patrón MVC: controller → servicio → repositorio

### Controller

- `public sealed class XController(IXService service) : ControllerBase`, con primary constructor, `[ApiController]`, `[Route("api/<recurso>")]` y `[Tags]`. No hay controller base.
- **Solo inyecta interfaces de `Application/Interfaces/Services`.** Nunca EF, repositorios ni readers (test).
- La entrada es un contrato de `Api/Contracts/<Área>`, mapeado a mano al `*Request` de Application.
- La salida es siempre `ToActionResult(this)`, `ToCreatedResult(this, nameof(Get), id => new { id })` o `ToAcceptedResult(this)`. Nunca `IsSuccess ? … : …`.
- Las rutas de la organización no llevan prefijo de acceso: `api/home`, `api/roles`, `api/permissions`, `api/users`, `api/users/invitations`, `api/companies/{companyId}` y sus `members` y `roles`. Lo que es de la organización entera va bajo `api/organization`: `settings`, `settings/domains`, `public-page` y `audit` (manda el código, como dijo la revisión de alineación del 2026-10-03). El acceso lo declara `[Access]`, no la ruta, y "tenant" no aparece en ellas. Las excepciones son `/api/platform/...`, `/api/me/...`, `/api/auth/...` e `/api/invitations/...` (anónimo).
- Cada acción declara `[Access(...)]`, o `[PublicSite][AllowAnonymous]` si responde en el subdominio de una organización publicada (`PublicSiteController` (E7)). Las rutas anónimas del dominio principal (ingreso, registro, "Registrá tu empresa", invitación, enlace, `GET` de los documentos legales, `GET /api/reference-data` y sus rutas por catálogo, directorio, pedido de "Recuperar mi cuenta", correos de contacto, cancelar la baja y webhooks) declaran solo `[AllowAnonymous]`, y `AccessDeclarationTests` las acepta únicamente si su controller está en la lista explícita del test. `ReferenceDataController` entra en esa lista al nacer el test en E3. En B2B y plataforma, cada acción declara además su permiso (`[HasPermission]`, `[HasCompanyPermission]` o `[HasPlatformPermission]`). Las rutas B2C (`[Access(Consumer)]`) no piden permiso (§14). Además lleva `[ProducesResponseType<T>]`, y los errores extra van con `[ProducesProblem]`.
- Un `POST` que crea o envía lleva `[Idempotent]`, y una acción de un módulo lleva `[FeatureGate]`. Un `PUT` o `DELETE` de una entidad `IVersioned` recibe `version`, que es obligatoria ([concurrencia](../rules/concurrencia.md)).
- `POST` que crea → 201 con `Location` y `GET /{id}`. `PUT` con `version` → 200 con la versión nueva (`VersionResponse`, [concurrencia](../rules/concurrencia.md)); los demás `PUT` y `DELETE` → 204.

```csharp
[ApiController, Route("api/roles"), Tags("Roles"), Access(Access.Business)]
public sealed class RolesController(IRoleService service) : ControllerBase
{
    [HttpPut("{id:guid}"), HasPermission(Permissions.Roles.Manage), ProducesResponseType<VersionResponse>(StatusCodes.Status200OK)]
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
  4. **Afuera, después del commit:** invalidar el caché y, si es un guardado con versión, responder `VersionResponse.Of(result)` con la entidad que devolvió el trabajo ([concurrencia](../rules/concurrencia.md)). El método entero va envuelto en `OperationLog.RunAsync(logger, timeProvider, "UpdateRole", …)`, que registra el inicio, el fin y el código de error: una línea por método, nunca el request.
- Un servicio no llama a los métodos que escriben de otro servicio. Lo compartido baja a un helper.
- Los métodos que solo leen no abren límite: van directo al reader.

### Repositorio, reader y UnitOfWork

| Pieza | Para qué | Convención de nombres |
|---|---|---|
| `I<X>Repository` | escribir un agregado (seguido por EF) | `Get…` (entidad seguida o `null`), `Add`, `Remove` |
| `I<X>Reader` | lecturas, proyecciones y páginas | `Find…` (proyección), `List…` (colección o `PagedResult<*Row>`), `Exists…`, `Count…` |
| `IUnitOfWork` | único punto de guardado | `ExecuteInTransactionAsync<TResult>(work, CommitPolicy, ct) where TResult : Result` |

- `CommitPolicy.OnSuccess` es la regla; `OnAnyResult` se usa cuando también hay que guardar un fallo (intentos, códigos consumidos, auditoría de seguridad).
- La transacción es READ COMMITTED, un solo `SaveChanges`, sin anidar (anidar lanza). Ante una excepción: rollback y `ChangeTracker.Clear()`. El error 23505 se traduce a `UniqueConstraintViolationException`, y el servicio lo convierte en su `Error` comparando `ConstraintName` con `UniqueKeys` ([persistencia-ef](../rules/persistencia-ef.md)). Los errores 23503 y 23001 de una clave foránea se traducen a `ForeignKeyViolationException`, que el servicio compara con `ForeignKeys`. `DbUpdateConcurrencyException` se traduce a `ConcurrencyConflictException` (P1, ver [concurrencia](../rules/concurrencia.md)), y el servicio no la atrapa.
- **Multitenant:** al abrir el límite, `UnitOfWork` ejecuta `SELECT set_config('app.tenant_id', @id, true)` con el tenant del `ITenantContext`. El detalle está en [`multitenancy.md`](multitenancy.md).
- Locks de un caso de uso tenant: `pg_advisory_xact_lock` con `AdvisoryLockKeys.For(tenantId, recurso, id)`, que siempre incluye el `TenantId`, o `FOR NO KEY UPDATE`. El seed global de referencias usa `AdvisoryLockKeys.ReferenceDataSeed`, sin tenant. Ambos exigen un límite de `IUnitOfWork` abierto.
- Prohibido `ExecuteUpdate` y `ExecuteDelete` sobre entidades `IAuditable` o `ISoftDeletable`.

`TransactionBoundaryTests` verifica con el IL que solo los servicios que implementan `Interfaces/Services` y los dos seeds técnicos `ReferenceDataSeeder` y `DatabaseSeeder` reciben `IUnitOfWork`, y que nadie salvo `UnitOfWork` llama a `SaveChanges`.

`DatabaseSeeder` confirma el mismo seed global en Development y Production: referencias, cliente web, ajustes, operador inicial y legales v1. No crea usuarios ni organizaciones de demostración. El operador sale de `Seed:PlatformOwner:*`, no tiene espacio Personal ni contraseña y el seed es idempotente.

---

## 6. Result pattern y manejo de errores

### Tipos (`Domain/Results`)

```csharp
public enum ErrorType { Failure, Validation, Unauthorized, Forbidden, NotFound, Conflict, TooManyRequests, ServiceUnavailable }
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
| ServiceUnavailable | 503 |
| Failure | 500 |

El cuerpo es `application/problem+json` con `title` y `detail` traducidos, `code`, `traceId`, `errors` y la metadata pública.
- Los errores que arma la propia Api sin pasar por un `Result` tienen su código en `ApiErrorCodes`, con su clave en `Errors.resx` y `Errors.en.resx`: `Request.Invalid`, `Http.Unauthorized`, `Http.Forbidden`, `Http.NotFound`, `Http.MethodNotAllowed`, `Http.TooManyRequests`, `Http.ServiceUnavailable`, `General.Unexpected` y `General.ConcurrencyConflict`. También los de idempotencia, que emite `IdempotencyFilter` como ProblemDetails directamente, sin pasar por el mapper: 409 `Request.InProgress`, 422 `Request.IdempotencyKeyReused` y 400 `Request.IdempotencyKeyRequired` (ver [idempotencia](../rules/idempotencia.md)).
- El 400 de un cuerpo ilegible no trae `errors` a propósito.
- **Multitenant:** una entidad de otra organización responde **404**, nunca 403, para no revelar que existe. Una organización que no está activa responde 403 con el código de su estado, que decide `TenantResolutionMiddleware`: `Tenancy.Tenant.Suspended`, `Tenancy.Tenant.PendingApproval` o `Tenancy.Tenant.Closed` (multitenancy.md §7).

### Validación

- **Un solo `IRequestValidator`**, inyectado una vez por servicio: `Task<ValidationError?> ValidateAsync<T>(T request, CancellationToken ct)`. Resuelve los `IValidator<T>` y agrupa los errores por campo en camelCase con puntos.
- Los validadores son `internal sealed`, en `Validation/<Área>`, y usan `ValidationRules` (`Required()`, `MaxLength()`, `ValidEmail()`, `ValidCurrency()`, `ValidCountry()`, `ValidTimeZone()`, `ValidCulture()`, `ValidTaxIdType()`), con mensajes de `ValidationTexts`. Las cinco reglas de referencia consultan los catálogos desde Application; un código deshabilitado no entra en un dato nuevo. `ValidPermissions(scope)` nace en E4 junto con el catálogo de permisos.
- `FieldErrors.On(error, "name")` ata un error de negocio a un campo del formulario.

---

## 7. Inyección de dependencias

| Capa | Punto de entrada | Qué registra |
|---|---|---|
| Application | `AddApplication()` | Un `<Área>Registration.cs` por área en `Services/<Área>/`, con sus options (`BindConfiguration + ValidateDataAnnotations + ValidateOnStart`) y cada servicio y helper con `AddScoped`, uno por uno y una sola vez; validadores (`AddValidatorsFromAssembly(includeInternalTypes: true)`), `IRequestValidator` y `ReferenceDataService` (los puertos viven aquí, los implementa Infrastructure) |
| Infrastructure | `AddInfrastructure(cfg, env)` | En E1, `JsonReferenceDataCatalog` para los cinco puertos y `TryAddSingleton(TimeProvider.System)`, sin base. Desde E2, `ReferenceDataReader`/HybridCache y `ReferenceDataSeeder` reemplazan el adaptador JSON; se agregan `TenantContext` (scoped, un registro para `ITenantContext` y otro para `ITenantScope`), interceptores (en orden: TenantStamp → SoftDelete → Auditable → AuditTrail; más TenantConnection), `AddDbContext`, `IUnitOfWork`, repositorios, readers y los subregistros `AddIdentityServices`, `AddOpenIddictServer`, `AddMessaging`, `AddCaching`. Los módulos se registran aparte, con su `AddWhatsAppModule()` en cada capa, llamado desde `Program.cs` |
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
| `IPublicSiteContext` | `Api/Tenancy/PublicSiteContext` | `BusinessTenantId?` y `Slug?` de la organización publicada y activa del subdominio; **solo** para leer lo público |
| `ITenantContext` | `Infrastructure/Persistence/TenantContext` | tenant del acceso activo (`TenantId` y `TenantKind`: el espacio personal en B2C, la organización en B2B). Lo carga `TenantResolutionMiddleware` desde los claims, o `ITenantScope.Enter(tenantId)` en plataforma, workers y altas |
| `IRequestInfo` | `Api/RequestContext/RequestInfo` | IP (después de ForwardedHeaders) y user agent |

- `ITenantContext.TenantId` **nunca** sale de un header, del body ni de la query.
- `PublicSiteResolutionMiddleware` consulta `IPublicSiteReader` una vez por pedido y solo para rutas `[PublicSite]`. El host debe ser exactamente `<slug>.<host del issuer>`, sin distinguir mayúsculas y con el mismo puerto; `TenantSlug.Create` valida la etiqueta y el reader descarta los nombres reservados. Sin sitio, `[PublicSite]` devuelve 404 `PublicSite.PublicPage.NotFound` con `traceId`. El contexto público nunca modifica `ITenantContext` y todavía no usa caché (Etapa 9).
- Las rutas de plataforma reciben el `tenantId` en la URL como **objetivo**; el servicio de plataforma, después de autorizar, entra con `ITenantScope.Enter(tenantId)`.

---

## 9. Persistencia

- **Un solo `ApplicationDbContext`**, con cinco esquemas:
  - **`platform`**: Tenants, TenantFeatures, TenantDomains (dominios de correo verificados), PlatformSettings, PlatformPermissionAssignments, SecurityEvents, LegalDocuments y LegalDocumentContents (un texto por documento y por idioma), AccountRecoveryRequests ("Recuperar mi cuenta"), ReservedTenantSlugs, TenantCounters, PendingTenantOwners, los cinco catálogos de referencia, IdempotencyKeys, OutboxMessages, OpenIddict y DataProtection; DataExports ("Exportar mis datos") llega en la E10 y WhatsAppChannels en la E11. Sin RLS.
  - **`identity`**: AspNetUsers (global, sin `TenantId`), **LoginMethods** (la única fuente de los correos y teléfonos de una cuenta, cada uno único en todo el sistema), `UserTenantAccesses` (proyección global de membresías), LegalAcceptances, UserLogins, UserTokens, LoginCodes, LoginAudits, AccountDeletionCancelTickets y ConsumerDataRetentions (ledger técnico sin copia personal); LoginLinks, WhatsAppContacts y WhatsAppMessages pertenecen a E8 (el número de la plataforma habla con la identidad, no con un acceso); WhatsAppCodeQuotas es el ledger técnico por número de plataforma/día UTC, sin destinatario ni OTP. Sin RLS; la leen `Infrastructure/Identity` y una lista cerrada de readers y repositorios de `Infrastructure/Persistence` que fija `IdentityStorageAccessTests` (inspecciona las llamadas IL a los DbSet globales y `Set<T>()` en todo Infrastructure): los de métodos de ingreso, códigos, legales y baja; los que parten de `Members` (Usuarios, empresas, invitaciones, dominios y auditoría); los de Plataforma (`PlatformReader`, `PlatformAccountReader`, `AccountRecoveryReader`, `SecurityEventReader` y `PlatformPermissionAssignmentRepository`) y `PlatformSeeder`. El índice de membresías se consulta solo para `ConnectService`, `TenantResolutionMiddleware` (por `AccessStatusCache`) y `/api/me` (por `ProfileSnapshotBuilder`); la gestión de métodos administrados usa un puerto específico `ILoginMethodMembershipReader` que devuelve solo la disponibilidad de la membresía/organización por UserId y TenantId. La búsqueda global por valor de un método de ingreso vive en `Infrastructure/Identity/UserLookup`; la única excepción es la búsqueda de cuentas de Plataforma, `PlatformAccountReader`, que busca por nombre, por el valor de los métodos y por el correo de contacto de Google ([ADR 0011](../decisions/README.md), T11 de la Etapa 5).
  - **`tenant`**, datos **privados** con **RLS forzado**: Members, Invitations, Companies, CompanyMemberships, Roles, RoleAssignments, TenantSettings, AuditEntries y lo privado que sumen los módulos B2B y B2C.
  - **`public_site`**, datos **públicos** de cada organización con RLS por publicación: PublicPages (nombre, logo, descripción, contacto y estado; el slug es el de la organización, en `platform.Tenants`) y lo que publiquen los módulos.
  - **`engagement`**, datos **compartidos** entre una persona y una organización, con RLS por partes: lo que definan los módulos (reservas, pedidos, solicitudes, mensajes).
- **Toda entidad nueva se clasifica** como privada (`ITenantOwned`), pública (`IPublishedByBusiness`) o compartida (`IConsumerBusinessShared`) antes de escribirla. Cada clase tiene su esquema, su filtro y su política RLS (multitenancy.md §4 y §9).
- Una configuración por entidad (test). Enums como texto (`HasConversion<string>().HasMaxLength(n)`). Las claves de las tablas de `tenant` son `(TenantId, …)`, sus índices únicos empiezan por `TenantId` y las FK a otras entidades del mismo tenant son compuestas `(TenantId, XId)`.
- **Filtros globales con nombre (EF 10), uno por clase de dato:** `"Tenant"` (privado, sobre toda `ITenantOwned`: `e.TenantId == tenantContext.TenantId`), `"Public"` (público, sobre toda `IPublishedByBusiness`: `IsPublished || BusinessTenantId == ctx.TenantId`), `"Parties"` (compartido, sobre toda `IConsumerBusinessShared`: `ConsumerTenantId == ctx.TenantId || BusinessTenantId == ctx.TenantId`) y `"SoftDelete"` (multitenancy.md §9). `IgnoreQueryFilters(["SoftDelete"])` quita solo el de borrados. Quitar `"Tenant"`, `"Public"` o `"Parties"` está prohibido fuera de la lista blanca de `QueryFilterBypassTests`, el namespace `Persistence.Readers.Platform`: hoy está vacío, porque ningún reader los quita.
- **Baja de la copia compartida:** `Infrastructure/Identity/Deletion/EngagementDeletionParticipant` trabaja en la UoW y el alcance Personal del llamador. Enumera los tipos compartidos del modelo y reemplaza todos los campos personales con `IConsumerBusinessShared.ReplaceConsumerCopy()`, conservando resumen y estado. La purga incluye filas con borrado lógico: omite sólo `SoftDelete`, nunca `Parties`. El marcador de copia borrada y los campos copiados llevan `[NotAudited]`; el lector traduce el nombre borrado sin inferirlo de un `null` ni persistir traducciones.
- **Retención legal de una copia:** `IRetainedOnConsumerDeletion` es una política de Application por tipo, código de motivo y plazo positivo; Infrastructure valida su mapeo y resuelve la clave estable `engagement.<tabla>`. `identity.ConsumerDataRetentions`, global y sin copia personal, guarda claves de fila/partes, motivo, `RetainUntilUtc` y `CopyReplacedAtUtc`. Una combinación tipo + partes + fila tiene un único registro y el vencimiento se fija una vez; se conserva al completarlo. `AccountDeletionProcessingService` lista hasta 100 vencidos y entra a cada Personal antes de abrir su UoW, aunque esté cerrado. El lock de metadatos `SKIP LOCKED`, el reemplazo tracked y la marca durable se confirman juntos, sin otro lease.
- **Migraciones:** una carpeta, `Infrastructure/Persistence/Migrations`. Cada tabla nueva llama en su migración al helper RLS de su clase (`EnableTenantRls`, `EnablePublicRls` o `EnablePartiesRls`), y `RlsPolicyInventoryTests` falla si alguna tabla de `tenant`, `public_site` o `engagement` no tiene su política.

```
dotnet ef migrations add <Nombre> --project src/ArquitecturaBaseMultitenant.Infrastructure \
  --startup-project src/ArquitecturaBaseMultitenant.Api --output-dir Persistence/Migrations
```

- `DesignTimeApplicationDbContextFactory` permite generar la migración sin arrancar la Api ni conectarse a una base. En Development se aplican al arrancar (`DatabaseBootstrapExtensions`, con la cadena `appdb-admin`). Fuera de Development: `dotnet ef migrations bundle`, como paso del despliegue. El seed es idempotente siempre.

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
   - `AuditTrailInterceptor` genera una entrada por cada `IAuditable` de tenant agregada, modificada o borrada. Guarda TenantId, `ActorKind` (User, PlatformOperator, System), ActorId, `Action` (Created, Updated, Deleted, Restored), EntityType, EntityId, `Changes` (jsonb con campo → anterior y nuevo) y `OccurredAtUtc`.
   - Se escribe **en la misma transacción** que el cambio.
   - Las propiedades marcadas `[NotAudited]` (tokens, hashes) no se guardan.
   - Para eventos que no son un cambio de entidad (exportaciones, reenvío de invitación), el servicio llama a `IAuditLog.Record(action, entity, id, data)` dentro del límite.
   - RLS y un trigger `prevent_update_delete` hacen la tabla inmutable.
3. **Seguridad (`platform.SecurityEvents` e `identity.LoginAudits`):** ingresos, intentos fallidos, cambios de permisos de un operador, acciones de operadores sobre una organización o una cuenta (aprobar, suspender, despublicar su página o permitir publicarla, iniciar la baja, resolver una recuperación; siempre con la organización o la cuenta afectada y el motivo), cambios de métodos de ingreso y la baja de una cuenta (`AccountDeletionRequested`, su cancelación y `AccountDeleted`). La leen solo los operadores con `platform.audit.read`.

`PlatformSettings` es global: sus cambios (incluido el seed inicial) requieren un `SecurityEvent` con motivo en la misma transacción, sin `AuditEntry` tenant. `AuditTrailInterceptor` comprueba esa presencia.

Un guardado deja una sola entrada por acción: `RoleAssignmentTrail` junta los roles de una persona en un lugar y `AccessChangeTrail`, su entrada o su salida y el alta de una empresa; nadie suma otra a mano ([auditoria](../rules/auditoria.md)). La pantalla "Auditoría" de la organización lee `AuditEntries` con cursor, filtradas por período, tema, búsqueda y «Quién» (una persona, el Sistema o la Plataforma), suma las acciones de Plataforma sobre la organización desde sus `SecurityEvent` y traduce los textos desde `Audit.resx`.

---

## 11. Fechas, UTC y zonas horarias

- El reloj es siempre `TimeProvider` inyectado. `BannedSymbols.txt` rompe el build con `DateTime.Now`, `DateTime.UtcNow` y similares. En los tests se usa `FakeTimeProvider`.
- Tipos: `DateTime` en UTC con sufijo `Utc`, `DateOnly` para fechas civiles, y `timestamptz` en Postgres.
- En JSON, `UtcDateTimeConverter` sale siempre con `Z` y rechaza entradas sin offset (400).
- **Zona efectiva** del usuario: `User.TimeZoneId` → `Company.TimeZoneId` (en pantallas de una empresa) → `TenantSettings.DefaultTimeZoneId` → la zona por defecto de la cultura (`America/Argentina/Buenos_Aires` para `es-AR`). Los IDs son IANA; `ITimeZoneService` hace la conversión temporal y `ValidTimeZone()` consulta `ITimeZoneCatalog` para aceptar una zona habilitada en un dato nuevo.
- **La conversión para mostrar la hace el front.** El backend solo convierte cuando la regla lo exige (rangos "del día" en reportes: `ITimeZoneService.GetDayRangeUtc(DateOnly, tz)`, con horario de verano y días saltados).
- `GET /api/reference-data` y `GET /api/reference-data/time-zones` devuelven todas las zonas con `isEnabled`, ID IANA, `CountryCodes[]` y ciudad traducida desde los datos de referencia. Son rutas `[AllowAnonymous]` de `ReferenceDataController` (lista explícita de `AccessDeclarationTests` desde E3). Los selectores ofrecen solo las habilitadas y los valores ya guardados pueden mostrar una zona deshabilitada. El offset se calcula con `TimeProvider` al mostrar y nunca se guarda; `GET /api/time-zones` queda reemplazado (ADR 0036). En E2 `TimeZoneCountries` conserva las asociaciones de varios países por zona.

---

## 12. Idioma, traducciones y resources

- **Cultura** es una sola preferencia con idioma y región (`es-AR`, `en-US`), no dos. El **idioma** de los textos sale de su primera parte (`es`, `en`) y el **formato** de números y fechas, de la cultura completa (§18).
- Culturas iniciales habilitadas: `es-AR` (por defecto) y `en-US`, declaradas en `cultures.json` (E1) y luego en `platform.Cultures` (E2). `SupportedCultures` lee `ICultureCatalog`; agregar otra cultura exige los resources y casos de formato de [datos-de-referencia.md](datos-de-referencia.md) §6, sin cambiar una lista de códigos en C#.
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
- **Un solo cliente público, `web`**, para el SPA. El área la decide el claim `access`: `consumer` → espacio personal y páginas públicas; `business` → la organización; `platform` → el backoffice. `SubdomainApplicationManager<TApplication>`, registrado como genérico abierto de OpenIddict, conserva las URI fijas y delega a `SubdomainRedirectUriValidator` únicamente los retornos dinámicos de `web`: `/auth/callback`, `/silent-renew.html` y `/` para la salida. Exige una etiqueta válida sobre el host, esquema y puerto del issuer, sin userinfo, query ni fragmento, con publicación activa y no bloqueada; también amplía la búsqueda de cliente para salir sin `client_id`. No guarda comodines ni desactiva validaciones del protocolo.
- **El issuer es fijo** (el dominio principal). Desde un subdominio, `authorize` y `logout` son navegaciones al dominio principal, donde vive la cookie de `/connect`; el canje del código, la renovación y `userinfo` van a `/connect/*` del propio origen del subdominio (multitenancy.md §5).
- **Recuperación silenciosa en un subdominio:** `ConnectController` usa `GetActiveUserAsync` para `consumer` con `prompt=none`; una cuenta sin espacio personal recibe `login_required` sin crearlo. El ingreso interactivo conserva el alta, y Business mantiene `SelectAccessAsync` para recordar la organización al cambiar de perfil en silencio. La CSP amplía sólo `frame-ancestors` de `/connect/authorize` y `frame-src` del HTML de un subdominio con el esquema y puerto del issuer; las demás directivas y respuestas no cambian. `SubdomainTests` recorre PKCE, issuer y claims, token/userinfo/refresh/revocación del propio origen, salida con y sin cliente/hint y límites de publicación; `SecurityHeadersTests` fija la apertura limitada.
- Duraciones: código 5 min, access token 15 min, refresh 30 días con rotación. `EnableTokenEntryValidation` permite la revocación inmediata.
- **Claims** (`OpenIdPrincipalFactory`): `sub`, `name`, `email`, `access` (`consumer` | `business` | `platform`) y el tenant del acceso activo: `tenant_id` y `tenant_kind` (`personal` | `business`). `access`, `tenant_id` y `tenant_kind` viajan tanto en el access token como en el id token firmado: el SPA lee este último para elegir la ruta del acceso activo. **Ni los permisos ni los roles viajan en el token**: se consultan en vivo, con caché.
- **Ingreso sin contraseña**, igual que la base: en la E3, código por correo y **Google**; el módulo de WhatsApp suma código por teléfono y enlace de un solo uso en la E8. **Autoregistro de personas** (`POST /api/auth/signup` o Google iniciado desde Registro), que crea la identidad y su espacio personal. Google iniciado desde Ingreso solo acepta cuentas existentes. Está abierto por defecto y se cierra con `PlatformSettings.ConsumerSignup` (`Open` | `Closed`; cerrado, el registro responde «Registro cerrado»). **Una persona no crea empresas:** el alta B2B es aparte (`POST /api/auth/business-signup`, "Registrá tu empresa") y la rigen `PlatformSettings.BusinessSignup` (`Open` | `RequiresApproval` | `Closed`) y el límite de organizaciones por persona (`MaxOwnedOrganizations`), con `BusinessSignupPolicy` (ADR 0020). Los correos salen por **Gmail (SMTP)**, también en desarrollo. La configuración usa las mismas claves que ArquitecturaBase y se carga con los scripts de `scripts/secretos/`: ver [`docs/operations/configuracion.md`](../operations/configuracion.md).
- **Identidad global, dos accesos:** cada método de ingreso (`LoginMethods`: correo, teléfono o Google, tipo + valor) es único en todo el sistema, y una cuenta puede tener varios; `AspNetUsers.Email` y `PhoneNumber` son solo una copia del método principal (multitenancy.md §3.1 y §12). Una persona tiene **una** cuenta. Ingresa **como persona** (por "Ingresá", `/login`: su espacio personal y las páginas públicas) o **como empresa** (por "Ingresá como empresa", `/login/empresa`: sus organizaciones, en `/org`). No se vuelve "al último lado": se entra al de la puerta elegida. **Para cambiar de acceso o de organización** se pide `/connect/authorize?prompt=none&access=<consumer|business>&tenant=<id>`: `ConnectService` valida la membresía y emite tokens nuevos (multitenancy.md §3 y §10).
- Cookies de Identity solo para `/account` y `/connect` (HttpOnly, Secure, SameSite Lax). La Api usa bearer.
- La cookie de cada ingreso lleva un identificador aleatorio de sesión de navegador. Cada autorización OpenIddict conserva ese identificador; al salir se revocan todas las autorizaciones y refresh tokens de esa sesión, incluidos los accesos anteriores, sin cerrar las sesiones de otros dispositivos. La renovación conserva el identificador de autorización.
- La autorización conserva también `access` y, si corresponde, `tenant_id`, para que `TokenRevoker` pueda revocar un acceso o una organización. `RevokeTenantAsync` aún recorre todas las autorizaciones; antes de conectarlo a la suspensión en la E9 se debe reemplazar ese recorrido por una búsqueda indexada.
- Si la renovación del token falla, el SPA inicia una autorización con `prompt=login`: `ConnectController` presenta el formulario aunque la cookie de Identity siga viva y quita ese prompt del `returnUrl` interno. Tras verificar el código, el authorize reanudado usa la cookie nueva sin volver al formulario.
- En cada request autenticado, `TenantResolutionMiddleware` verifica (con caché de 60 s, invalidado al cambiar de estado) que la identidad, la membresía y el tenant del acceso activo estén activos. Suspender una organización revoca los tokens emitidos para ella; suspender una identidad, todos los suyos, en los dos accesos.
- **Operadores de plataforma:** `access=platform`, sin membresías ni espacio personal, con el mismo mecanismo de ingreso. Su cuenta es exclusiva de Plataforma y se invita por correo, con permisos individuales y sin roles ([ADR 0042](../decisions/0042-operadores-de-plataforma.md)). El segundo factor (TOTP) es obligatorio para ellos desde la Etapa 9 (endurecimiento).
- **Invitaciones a una organización (E3c):** `Member(Invited)` + `Invitation` (hash del token, vence), emitida por el helper interno `InvitationIssuer` dentro de la UoW de su llamador. En 3c solo se registra el canal Email, que encola por outbox (`IInvitationChannel`); el canal WhatsApp pertenece a E8. El issuer no tiene ruta propia. El `InvitationsController` anónimo atiende vista previa (`POST /api/invitations/preview`) y aceptación (`POST /api/invitations/accept`); el token llega en el cuerpo JSON, nunca en URL, query ni logs. Acepta una cuenta existente o crea identidad sin espacio Personal; este último nace cuando la persona entra por la puerta Consumer. La emisión administrativa, reenvío y revocación HTTP se incorporan en E5/E6.
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
- **Permisos de plataforma por operador** (`PlatformPermissionAssignment`): cada operador tiene las claves elegidas de a una, al menos una y sin repetir; no hay roles de plataforma (Owner y Support se retiraron en la Etapa 5, con una migración que conservó sus permisos efectivos).
- **Roles de la organización** (`tenant.Roles`), con **tres alcances** (en pantalla, la columna "Vale en"). Al crear un rol se elige toda la organización o una empresa; `AnyCompany` («Cada empresa») queda para el rol de sistema Administrador de empresa y para la API ([ADR 0034](../decisions/README.md), enmienda del 2026-10-03):
  - **`Organization`** ("Toda la organización"): se asigna sin empresa y vale en la organización y en todas sus empresas.
  - **`AnyCompany`** ("Cada empresa"): es un rol de empresa reutilizable; se asigna **con** una empresa y vale solo en esa. Ejemplo: el Administrador de empresa.
  - **`SpecificCompany`** ("Solo en <empresa>"): el rol pertenece a **una** empresa (`Role.CompanyId`) y solo se puede asignar en ella. Sirve para que cada empresa tenga sus propios roles.
  - Las empresas son **planas**: no hay empresa matriz ni herencia entre empresas (ADR 0034).
  - `TenantAdmin` y `CompanyAdmin` son de sistema: inmutables y no se borran. Se protege al último `TenantAdmin` y al último `CompanyAdmin` de cada empresa.
  - El Dueño sale **solo** del rol `TenantAdmin` (`RoleAssignment`), nunca de un dato de `Member`. "Dueños activos" son los que tienen `TenantAdmin` con la identidad `Active` (una baja pedida no cuenta); esa misma lectura la usan `AccountDeletionPolicy` y `LastTenantAdminGuard`.
- **Atributos**, nunca roles ni `[Authorize(Policy=…)]` a mano (test):
  - `[HasPermission(Permissions.Users.Manage)]` para la organización.
  - `[HasCompanyPermission(Permissions.Company.Members.Manage)]`: lee `{companyId}` de la ruta y verifica que la empresa sea de la organización y que el usuario tenga el permiso ahí. Con `organizationPermission`, también alcanza ese permiso de la organización (la ficha de una empresa: `companies.read` o `company.members.read` en esa empresa).
  - `[HasPlatformPermission(PlatformPermissions.Tenants.Manage)]`: además exige `access=platform`.
- `IPermissionService` (`GetOrganizationPermissionsAsync`, `GetCompanyPermissionsAsync` y `GetPermissionsByCompanyAsync`) suma los permisos de los roles de la persona. Los de cada rol se cachean en HybridCache con la clave `t:{tenantId}:role-permissions:v2:{roleId}` (ids en formato `N`) y se invalidan al cambiar el rol (`InvalidateRoleAsync`), después del commit. La versión evita leer entradas de plantillas anteriores al upgrade de roles de sistema. La migración se aplica antes de aceptar tráfico y se reinician todas las instancias del despliegue anterior: el SQL no invalida su caché local.
- Las rutas de organización llevan `[Access(Business)]` **y** el permiso. Con otro acceso responden 403 `Tenancy.Access.Wrong`.
- `GET /api/me` devuelve la cuenta, el **acceso activo**, si tiene espacio personal y sus **organizaciones** (id, nombre y estado, para elegir en el acceso B2B), el tenant activo, sus permisos efectivos (de organización y por empresa), las preferencias efectivas de cultura, zona y moneda y, desde la Etapa 5, los módulos prendidos del acceso activo (`features`, [modulos-habilitados](../rules/modulos-habilitados.md)), para que el front arme el menú y los formatos (`useFormat`). El front decide solo la experiencia de uso; **el backend decide el acceso**.

---

## 15. WhatsApp: un módulo quitable

La estructura sigue el diseño de la Etapa 6 del plan maestro de `../ArquitecturaBase` ([ADR 0007](../decisions/README.md)), para que las dos plantillas se armen igual. El módulo nace en la Etapa 8: composición/persistencia/adaptadores, Phone en identidad/recuperación, webhook durable, bot/enlaces y privacidad están implementados. La atención por organización (Etapa 11) sigue la [ADR 0038](../decisions/0038-whatsapp-de-atencion.md). Las precisiones de ingreso/avisos y la relación con el asistente están en [0049](../decisions/0049-precisiones-whatsapp-y-asistente.md) y el [plan E8](../plans/2026-10-07-etapa-8-whatsapp.md).

- **Puertos en el núcleo, adaptadores en el módulo.** El núcleo nunca nombra a WhatsApp:
  - `ILoginCodeChannel` e `IInvitationChannel`: el núcleo trae el canal `"email"`. El módulo registra `"whatsapp"`. `LoginCodeChannel` e `InvitationChannel` son **valores**, no enums: el núcleo valida contra los canales registrados.
  - `IAccountNoticeChannel` (en `Application/Interfaces/Integrations/Messaging/`, Etapa 3), con el mismo patrón: recibe un `AccountNotice` cerrado (`LoginMethodChanged`, con qué pasó, el método enmascarado y la fecha; `ReviewLoginMethods`, con la organización; `DeletionRequested`, con la fecha; `DeletionCancelled`; `AccountDeleted`; y los de «Recuperar mi cuenta», al método nuevo: `RecoveryReceived`, `RecoveryApproved` y `RecoveryRejected`). Cada implementación atiende un tipo de método: el núcleo trae la de `Email` (canal `"email"`) y el módulo registra la de `Phone` (canal `"whatsapp"`). Sin el módulo, los teléfonos no reciben aviso, y no es un error. Siempre sale por el outbox, cifrado. "Exportación lista" sale solo por correo.
  - `IPhoneLinkObserver` e `ILoginLinkIssuer` (en `Application/Interfaces/Integrations/Identity/`, E8): hooks Phone dentro de la UoW del núcleo y emisión de enlaces dentro de la UoW del bot; ninguno abre otra transacción.
  - El teléfono como dato de la cuenta e `IPhoneNumberParser` siguen en el núcleo. `PhoneUsage` queda en `Any | Mobile` y no nombra WhatsApp (`Mobile`: solo celular). En la E3 `Phone` es solo modelo; el ingreso, registro y la gestión de un teléfono se habilitan al registrar el canal del módulo en la E8.
- **Países de WhatsApp:** los controla el módulo en sus adaptadores y flujos (código por WhatsApp, vínculo y registro por WhatsApp), con `WhatsApp:AllowedCountries`, que se lee solo dentro de `Modules/WhatsApp`, y un error propio del módulo sobre el campo `phone`. Se controla para un número **nuevo o pendiente**: achicar la lista no invalida uno ya verificado; un pendiente debe cumplir la lista vigente al reenviar/verificar. `GET /api/auth/methods` (núcleo) devuelve los canales encendidos con un formato neutro, y el módulo aporta sus países (`channels: [{ key: "whatsapp", countries: [...] }]`).
- **Carpetas:**
  - `Domain/WhatsApp`;
  - `Application/Modules/WhatsApp/{Interfaces,Models,Services,Resources}`;
  - `Infrastructure/Modules/WhatsApp/` con cliente, catálogo/renderers y opciones en la raíz; subcarpetas `Webhook`, `Inbound`, `Retention` y `Persistence`;
  - `Api/Modules/WhatsApp`.

  `WhatsAppModelContributor` aplica sus propias `IEntityTypeConfiguration`, con un `ApplyConfigurationsFromAssembly` filtrado por namespace, y declara explícitamente los tipos globales del módulo en `identity`. `ApplicationDbContext` recibe contribuidores neutrales; DesignTime/bootstrap los descubren sin secretos ni red. La clave de caché del modelo incluye el conjunto de contribuidores. Apagar el canal no cambia el esquema.
- **Registro:** un `AddWhatsAppModule()` por capa, llamado desde `Program.cs`. Infrastructure no registra servicios de Application.
- **Encendido (E8):** se enciende con `WhatsApp:PhoneNumberId`; sin él quedan las implementaciones de `Disabled/`. Las rutas del webhook están disponibles en runtime solo si hay `AppSecret` y `VerifyToken`; la convención neutral de contrato las exporta aunque estén apagadas.
- **Quitarlo (E8):** seguir [quitar-whatsapp.md](../guides/quitar-whatsapp.md). En copia descartable, excluir contribución EF actual, generar migración Drop conservando historia, retirar carpetas/composición y probar base vacía/migrada; núcleo, correo y Google deben seguir verdes. `ModuleIsolationTests` comprueba también el aislamiento de Domain. No borrar directamente tablas/migraciones desde el checkout compartido. La prueba de fuego real pertenece a Integración.
- **Contribuciones neutrales de E8:** modelo EF con tipos globales explícitos y mismo modelo runtime/DesignTime/bootstrap/tests; guardas de servicios/controllers/UoW/DI que reconocen módulos por forma de namespace/contrato, sin excepciones de clase. La comparación de modelos incluye diferencias relacionales, metadatos y filtros, con controles negativos de índice, FK, xmin, nulabilidad y filtro. WhatsApp identidad es capacidad técnica por configuración, sin feature comercial por organización ni permisos E11. Assistant copia esos puntos sin importarlo.
  `WhatsAppPersistenceTests` agrega una segunda contribución técnica de test con inventario neutral explícito en contextos runtime DI, opciones DesignTime y bootstrap: modelo completo igual, una configuración por modelo y canales/capacidades WhatsApp únicos. No amplía `Discover` para cargar assemblies de tests; la comparación con las factories productivas reales sigue en su prueba baseline. E8b puede aportar su contribuidor en Infrastructure siguiendo ese descubrimiento y registrar sus servicios por capa, conservando los demás.
- **Número de la plataforma** (Etapa 8): códigos, invitaciones, avisos y bot. Catálogo resuelto: **16 nombres es/en**, Authentication + 15 Utility, con recuperación, variantes de baja por Plataforma y los tres avisos de organización de 0049. Dos avisos «en uso» derogados por 0043; Mi cuenta no usa códigos para confirmar cambios (0045). Inventario/paquete/carga: [plantillas](../operations/whatsapp-plantillas.md).
- **Bot de ingreso:** enlace solo para cuenta común, entra como persona y después cambia a empresa con Perfiles. Número de operador recibe solo «Para entrar, usá la web.», sin enlace ni mención de Plataforma; entra exclusivamente por `/plataforma` con código. Preview/redeem revalidan que no sea operadora. Desconocido va al registro personal WhatsApp en web con términos/OTP; alta cerrada ofrece ingreso/recuperación. Nunca crea identidad ni sesión al recibir el mensaje.
  - `LoginLinkIssuer` conserva hash, vida fija de diez minutos, cooldown y límite de emisiones en la UoW del bot; una emisión nueva invalida la anterior. Preview y replay son lecturas con snapshot fresco; canje bloquea cuenta/enlace y consume antes de emitir cookie pública tras commit. Gate legal y baja pendiente conservan sus controles. El hook explícito de replay valida también el 403 con ticket de baja, sin emitir ticket/cookie de nuevo. Detalle y encajes Front: [Identidad](../features/identidad.md) y «Cambios al contrato» del plan.
  - El bot utiliza el propietario original de la entrada, nunca el actual para reasignar historia. Nombre de plataforma desde `Email:AppName`; operador conserva su frase exclusiva. Un CTA tiene una URL; los dos botones de registro cerrado usan IDs públicos de respuesta y la selección genera el CTA correspondiente. El sender descarta replies libres cuyo cierre de ventana `LastInboundAtUtc + 24 h` ya pasó; no llama a Meta y el outbox limpia el payload terminal.
- **Envío por el outbox persistente** (`platform.OutboxMessages`), no en memoria. Esto **difiere a propósito** de ArquitecturaBase, que usa colas en memoria (`WhatsAppSendQueue`, `EmailQueue`) y documenta que un reinicio las pierde. Con varias organizaciones y réplicas, perder una invitación o un código no es aceptable (ADR 0014):
  - el caso de uso inserta el mensaje dentro de su transacción;
  - `OutboxDispatchService.ClaimNextAsync` reclama una fila con `SKIP LOCKED`, incrementa su intento y confirma un lease durable en una UoW;
  - `OutboxDeliveryProcessor` descifra y llama al transporte fuera de esa transacción; SMTP conserva su scope de lote y Meta no agrega reintentos propios al POST;
  - `OutboxDispatchService.CompleteAsync` bloquea y relee la fila en otra UoW, comprueba el token del lease y aplica el receipt neutral. Una confirmación vieja no revive una fila Sent/Cancelled ni modifica el lease de otra réplica. `IOutboxDeliveryObserver` registra metadatos de una aceptación vigente en esa misma UoW;
  - el payload con códigos o enlaces va cifrado con DataProtection y se elimina al confirmar Sent, cancelar o agotar los intentos. La migración limpia también payloads terminales anteriores;
  - un lease vencido puede retomarse después de una caída, y los reclamos abandonados también consumen intentos. La parada impide comenzar la siguiente entrega; la actual termina y confirma con timeouts propios acotados.
  El proveedor y PostgreSQL no comparten transacción: si el proceso cae después de que SMTP o Meta acepta el mensaje y antes de confirmar `Sent`, un reintento puede duplicarlo. La garantía es que un mensaje **confirmado** como `Sent` no se vuelve a despachar. Sent de Meta significa aceptado; delivered/read requieren sus callbacks reales.
- **Webhook** `GET/POST /webhooks/whatsapp`: anónimo, con rate limit y cuerpo de 5 MiB como máximo, medido antes del binder.
  - Firma HMAC `X-Hub-Signature-256` sobre los bytes exactos del cuerpo, comparación constante antes de parsear.
  - Deduplica `(PhoneNumberId, Wamid, EventType)`. ACK después del commit; callbacks fuera de orden no retroceden delivered/read ni disparan el bot.
  - E8 procesa únicamente el destino configurado, sin resolver organizaciones. Guarda propietario/método original al insertar; anonimato y propiedad no se recalculan con el vínculo actual.
  - El procesamiento es asíncrono (`WhatsAppInboundProcessor`, con `SKIP LOCKED`, lease durable, backoff y scopes frescos). El consumer y reply en outbox comparten la UoW; el lease se verifica después de esperar locks y al completar. Sin consumer no se reclama trabajo.
- **Regla de oro:** un mensaje entrante nunca abre una sesión; solo puede producir un enlace de un solo uso.
- **Canales por organización** (Etapa 11, [ADR 0038](../decisions/0038-whatsapp-de-atencion.md)): `platform.WhatsAppChannels` mapea `PhoneNumberId` → `TenantId`. El procesador resuelve el tenant por el número de destino y entra con `ITenantScope`.
- **Privacidad:** el observer y el participante de baja actúan dentro de la UoW llamadora, sin locks Phone después del de cuenta. Retiran sólo contactos todavía propios y limpian texto/ContactId por snapshot original, sin transferir historia A→B ni anonimato. Retención fija de 90 días desde ReceivedAtUtc, lotes tracked de 100 con FOR UPDATE SKIP LOCKED y scopes nuevos; sigue con el canal apagado. La exportación OpenAPI no inicia ese worker. Detalle: [WhatsApp](../features/whatsapp.md) y [datos personales](../rules/datos-personales.md).

---

## 16. Pipeline HTTP (`Program.cs`)

```
UseForwardedHeaders → UseSecurityHeaders → UseRequestLocalization → UseExceptionHandler → UseStatusCodePages
→ UseRateLimiter → UseAuthentication → TenantResolutionMiddleware → PublicSiteResolutionMiddleware
→ LegalAcceptanceMiddleware → FeatureGateMiddleware → UseAuthorization
→ (Development: OpenAPI y Swagger UI | resto: UseHsts) → UseHttpsRedirection
→ UseStaticFiles (si hay wwwroot) → MapDefaultEndpoints → MapControllers → UseSpaFallback
```

Todo middleware que pueda cortar con un error va después de `UseStatusCodePages`.
Antes del pipeline, `Program.cs` prepara la base: en Development, el bootstrap (roles, base ICU y migraciones); fuera de él, la validación del rol de runtime; y el seed, salvo en Testing. `FeatureGateMiddleware` (E5) oculta con 404 un módulo apagado después de validar el acceso y antes de autorizar ([modulos-habilitados](../rules/modulos-habilitados.md)).

`PublicSiteResolutionMiddleware` resuelve el host solo si el endpoint declara `[PublicSite]`. `PublicSiteReader` une la página pública con la organización, sin ignorar filtros ni cambiar el contexto privado: exige página publicada, sin bloqueo de moderación, organización Business activa y slug no reservado. Las rutas privadas no consultan ese reader.

---

## 17. Logging, OpenAPI, health, rate limiting, caché

- **Logging:** `[LoggerMessage]` obligatorio (`CA1848` como warning, que rompe el build). Nunca se registran códigos, tokens, enlaces, emails completos ni teléfonos. Cada log de negocio lleva un scope con `TenantId`.
- **OpenAPI:** `/openapi/v1.json` y Swagger UI solo en Development. El build exporta `docs/contracts/openapi.json` con `Microsoft.Extensions.ApiDescription.Server`, como en ArquitecturaBase; el front lo usa para generar sus tipos. El CI falla si el archivo commiteado no coincide con el generado.
- **Health:** `/health` (readiness: base de datos, rol de runtime y WhatsApp) y `/alive`.
- **Rate limiting:**
  - por IP: `login-code`, `login-verify`, `invitation-accept` y `whatsapp-webhook`;
  - por organización: `tenant-api`, una ventana deslizante sobre `/api`.
  - Un 429 sale como ProblemDetails con `retryAfter` en segundos; `shared/api/httpClient` del front lo convierte en `ApiError.retryAfterSeconds` para la cuenta regresiva del botón.
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
| CUIT / id fiscal | `TaxId` (`Country` + `Type` + `Number`) | `{ "country": "AR", "type": "AR-CUIT", "number": "20123456786" }` (E6) | tres columnas: `TaxCountry char(2)` + `TaxType varchar(16)` + `TaxNumber varchar(20)` | `type` es el código de `TaxIdTypes`; solo dígitos en `number`, validado con el dígito verificador; los guiones de `20-12345678-6` son solo de presentación |
| Vacío | `null` | `null` | `NULL` | nunca `""`, `0` ni `"N/A"` para "no hay dato" |

- **Redondeo:** `MidpointRounding.AwayFromZero`, a los `MinorUnits` que Application obtiene de `ICurrencyCatalog` y pasa a `Money.Round(minorUnits)`, **en el backend y al momento de calcular** (por línea y después el total). `CurrencyCode.Create(code)` en Domain solo valida la sintaxis alfa-3; `ValidCurrency()` en Application comprueba existencia y habilitación del código para una entrada nueva. El front **no calcula** montos, solo los muestra; los totales vienen calculados.
- **Excepción técnica IA ([ADR 0051](../decisions/0051-precision-tecnica-de-costos-ia.md), autorizada por el usuario):** las tarifas `Money` por millón y sus snapshots conservan hasta seis decimales (`numeric(18,6)`), y los costos/reservas técnicos hasta doce (`numeric(24,12)`), con `AwayFromZero` a esa escala al normalizar. No se redondea cada llamada a centavos antes de acumular; presupuesto disponible y umbrales usan ese acumulado persistido. Las tarifas nuevas con escala excesiva se rechazan sin truncamiento. Presupuestos ingresados y presentación conservan los `MinorUnits` del catálogo y `DisplayFormatter`. La excepción alcanza sólo estimaciones IA, sin modificar importes comerciales ni el contrato monetario HTTP.
- **Moneda por defecto** de la organización: `TenantSettings.DefaultCurrency` (inicialmente ARS desde el catálogo). Un importe siempre lleva su moneda explícita, aunque sea la de por defecto.
- Límite de precisión: JSON number alcanza para importes de hasta 13 dígitos enteros con 2 decimales. Más que eso requiere un ADR.

### Formato del lado del backend

- `Application/Common/Formatting/DisplayFormatter.cs` presenta desde E1 **todos** los tipos de `formatos.md` en cada cultura habilitada: fechas absolutas, relativas y rangos; enteros, decimales, cantidades y compactos; porcentaje, dinero, tamaño de archivo, duración, teléfono, zona, cultura, identificación fiscal, correo, enum, booleano, vacío y texto. Recibe cultura y zona explícitas, y el instante de `TimeProvider` para los formatos relativos. Usa los patrones del catálogo `Cultures` y `CurrencyTranslations.DisplaySymbol` por cultura para los montos (JSON E1, tablas E2), no perfiles ni símbolos escritos a mano. Los textos variables salen de `Formatting.resx` y `.en.resx`. El teléfono usa `IPhoneNumberDisplayFormatter` con adaptador libphonenumber en Infrastructure para mantener Application sin esa dependencia. El formato fiscal E1 usa los campos del contrato; el value object `TaxId` llega en E6.
- Al presentar dinero, el signo se decide después de redondear a `MinorUnits`, para que un importe negativo que se muestra como cero no lleve `-`. En compacto y tamaño de archivo, si el valor redondeado alcanza 1000 se elige la unidad siguiente antes de producir el texto. `format-cases.json` fija ambos límites en back y front.
- Los mensajes de negocio crean una vez `DisplayFormatContext` con `CreateAsync(culture, timeZone)` y llaman métodos tipados (`FormatInstant`, `FormatDecimal`, `FormatPercent`, `FormatMoneyAsync`, `FormatPhoneAsync`). El despachador `FormatAsync(string, JsonElement, ...)` es interno y se limita a las pruebas de `format-cases.json`.
- `docs/contracts/format-cases.json` tiene `now` fijo (`2026-09-27T15:00:00Z`) y casos `{ id, type, culture, timeZone, input, expected | error }` para todos los tipos y ambas culturas iniciales, incluidos importes con 0, 2 y 3 decimales y teléfonos nacionales e internacionales. Cada caso fija el texto `expected` o el código de validación `error`, nunca ambos; `expected` manda si una librería da otra variante ([datos-de-referencia.md](datos-de-referencia.md) §7).
- Lo usan las plantillas de correo, los textos de WhatsApp y las exportaciones.
- `DisplayFormatterTests` recorre `docs/contracts/format-cases.json`; el front corre el mismo archivo. Si los dos lados no producen el mismo texto, falla el CI.

---

## 19. Front y hosting

- El SPA vive en `../ArquitecturaBaseMutitenantFront`. El SPA y la Api se sirven desde el **mismo origen en cada host**, el dominio principal y cada `<slug>.plataforma.com`: en producción desde wwwroot, con DNS y certificado comodín; en desarrollo, con el proxy de Vite por host (`*.localtest.me`). **No hay CORS**, tampoco en los subdominios.
- `BackendPrefixes` es una lista a mano, y se atiende igual en todos los hosts: `/api`, `/account`, `/connect`, `/signin-google`, `/.well-known`, `/webhooks`, `/health` y `/alive`. También reserva `/swagger` y `/openapi` para que fuera de Development devuelvan 404 en vez del SPA; sus endpoints y el proxy de Vite solo existen en Development. Un prefijo nuevo se suma ahí, al `SpaHostingTests` y al `server.proxy` de `vite.config.ts`.
- En desarrollo, el issuer es `https://localtest.me:5174/`: AppHost pasa el mismo valor a Api, Vite y RealE2ESetup. Los callbacks fijos son del dominio principal; los dinámicos conservan origen y puerto del subdominio publicado. Google registra `https://localtest.me:5174/signin-google`. No se siembran sitios de ejemplo en Development.
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
