# Árbol del backend

> Estructura objetivo, archivo por archivo. `[E#]` indica la etapa del [plan](../plans/2026-09-27-plan-de-desarrollo.md) en que nace la carpeta o el archivo; sin marca, hereda la etapa de su carpeta. Cada proyecto nace vacío en la E0 (tarea 0.2) y sus carpetas llevan su propia marca. Los detalles de cada pieza están en [backend.md](backend.md) y [multitenancy.md](multitenancy.md). `bin/`, `obj/` y `wwwroot/` compilado no se listan.

## Raíz

```
ArquitecturaBaseMutitenant/
├── .github/
│   └── workflows/
│       └── ci.yml                                    [E0] build + test (Docker) + verificación de docs/contracts/openapi.json
├── docs/
│   ├── architecture/
│   │   ├── backend.md                                arquitectura canónica
│   │   ├── multitenancy.md                           accesos B2C/B2B, tres clases de datos, subdominios, aislamiento
│   │   ├── arnes.md                                  cómo se guía a quien programa: fichas, punteros, verificación
│   │   ├── estandares.md                             inventario de estándares transversales (definidos, P1 a P10 adoptados, propuestos)
│   │   └── arbol.md                                  este archivo
│   ├── contracts/
│   │   ├── openapi.json                              [E1] generado por el build; el front genera sus tipos desde acá
│   │   └── format-cases.json                         [E1] casos de formato compartidos: valor + cultura + zona → texto esperado
│   ├── decisions/
│   │   ├── README.md                                 índice de los ADR
│   │   └── NNNN-<titulo>.md                          uno por decisión, al implementarla
│   ├── rules/                                        fichas del arnés: README + una ficha por tema (guardado, fechas, números…); índice en rules/README.md
│   ├── features/                                     reglas funcionales por área
│   │   ├── identidad.md                              [E3] registro de personas y de empresas, accesos, invitaciones
│   │   ├── roles.md                                  [E4] cómo copiar el área de referencia
│   │   ├── plataforma.md                             [E5]
│   │   ├── organizaciones.md                         [E6] usuarios, empresas, membresías, filtros y conteos
│   │   ├── personal.md                               [E7]
│   │   └── whatsapp.md                               [E8]
│   ├── operations/
│   │   ├── configuracion.md                          Google, Gmail y WhatsApp: claves, secretos y pasos en Google y Meta
│   │   ├── secretos.plantilla.json                   forma del archivo de secretos (se copia FUERA del repo)
│   │   ├── whatsapp-plantillas.md                    cuándo y cómo crear cada plantilla de WhatsApp en Meta
│   │   └── runbook.md                                [E10]
│   ├── plans/
│   │   ├── 2026-09-27-plan-de-desarrollo.md          plan maestro
│   │   └── AAAA-MM-DD-etapa-N-<tema>.md              plan detallado de cada etapa
│   └── history/                                      planes cerrados (HISTÓRICO)
├── scripts/
│   └── secretos/
│       ├── importar-desde-arquitecturabase.ps1       copia los 5 secretos de ArquitecturaBase sin mostrarlos
│       ├── cargar-desde-archivo.ps1                  carga un JSON guardado fuera del repo
│       └── verificar.ps1                             qué falta, sin mostrar valores
├── src/                                              ver abajo
├── tests/                                            ver abajo
├── .editorconfig                                     [E0] estilo y supresiones justificadas
├── .gitignore                                        [E0] VisualStudio + .local/ + *.env + appsettings.*.local.json
├── AGENTS.md                                         índice de reglas
├── CLAUDE.md                                         @AGENTS.md
├── ArquitecturaBaseMultitenant.slnx                  [E0]
├── BannedSymbols.txt                                 [E0] DateTime.Now/UtcNow/Today, DateTimeOffset.Now/UtcNow
├── Directory.Build.props                             [E0] net10.0, Nullable, TreatWarningsAsErrors, analizadores
├── Directory.Packages.props                          [E0] versiones centralizadas (CPM)
├── global.json                                       [E0] SDK 10.0.400 + Microsoft.Testing.Platform
├── aspire.config.json                                [E0]
├── LICENSE
└── README.md                                         cómo levantar, probar y desplegar
```

## src/ArquitecturaBaseMultitenant.Domain `[E0]`

Sin paquetes. Entidades, reglas puras y catálogos.

```
ArquitecturaBaseMultitenant.Domain/
├── ArquitecturaBaseMultitenant.Domain.csproj
├── Common/                                           [E1–E2]
│   ├── Entity.cs                                     Id Guid v7 (private init) + ctor protegido para EF
│   ├── ValueObject.cs
│   ├── IAuditable.cs                                 [E2] CreatedAtUtc, CreatedBy, ModifiedAtUtc, ModifiedBy
│   ├── ISoftDeletable.cs                             [E2] IsDeleted, DeletedAtUtc, DeletedBy
│   ├── ITenantOwned.cs                               [E2] dato privado: TenantId
│   ├── ICompanyOwned.cs                              [E2] CompanyId (además de ITenantOwned)
│   └── NotAuditedAttribute.cs                        [E2] excluye la propiedad del rastro de auditoría
├── Results/                                          [E1]
│   ├── Error.cs                                      record + fábricas Failure/Validation/NotFound/Conflict…
│   ├── ErrorType.cs
│   ├── Result.cs                                     Result y Result<T>, con conversiones implícitas
│   └── ValidationError.cs                            "Validation.Failed" + errores por campo
├── ValueObjects/                                     [E1]
│   ├── Money.cs                                      [E1] Amount (decimal) + Currency; suma solo con la misma moneda; Round()
│   ├── CurrencyCode.cs                               [E1] ISO 4217 + decimales de la moneda
│   ├── CultureCode.cs                                [E1] es-AR | en-US (SupportedCultures)
│   ├── Email.cs                                      [E1] normalizado (minúsculas, NFC, IDN), validado
│   └── PhoneNumber.cs                                [E1] E.164; PhoneUsage se suma en E3
├── Tenancy/                                          [E2–E3]
│   ├── Tenant.cs                                     Kind, Status, Name, Slug?; métodos Activate/Suspend/Close
│   ├── TenantKind.cs                                 Personal | Business
│   ├── TenantStatus.cs                               PendingApproval | Provisioning | Active | Suspended | Closed
│   ├── TenantErrors.cs                               Tenancy.Tenant.* (Suspended → 403, organización o espacio personal suspendido)
│   ├── Member.cs                                     UserId, TenantId, Status, IsOwner, JoinedAtUtc
│   ├── MemberStatus.cs                               Invited | Active | Inactive | Removed
│   ├── MemberErrors.cs                               Tenancy.Member.*
│   ├── AccessErrors.cs                               Tenancy.Access.Wrong, .NotMember, .ConsumerCannotCreateBusiness
│   ├── Invitation.cs                                 [E3] TenantId, email/teléfono, TokenHash, ExpiresAtUtc, roles iniciales
│   ├── InvitationChannel.cs                         [E3] valor ("email", …): cada módulo registra el suyo
│   ├── InvitationStatus.cs                           [E3] Pending | Accepted | Revoked | Expired
│   └── InvitationErrors.cs                           [E3]
├── Users/                                            [E1–E3] la identidad global (la entidad EF está en Infrastructure)
│   ├── Access.cs                                     [E3] Consumer | Business | Platform (el acceso; un usuario no-plataforma puede tener los dos primeros)
│   ├── UserStatus.cs                                 [E3] Active | Suspended | PendingDeletion | Deleted
│   ├── EmailErrors.cs                                [E1] Users.Email.Invalid (lo devuelve Email.Create)
│   ├── PhoneErrors.cs                                [E1] Users.Phone.Invalid; .NotMobile y .CountryNotAllowed en E3, con PhoneUsage
│   └── UserErrors.cs                                 [E3] Users.User.*
├── Authentication/                                   [E3]
│   ├── LoginCode.cs
│   ├── LoginCodeChannel.cs                          valor ("email", …): los módulos suman canales (IsKnown lo valida)
│   ├── LoginCodePurpose.cs                           Login | Signup | VerifyDestination | Reauthenticate (lo usa ReauthVerifier)
│   ├── LoginCodeDestination.cs
│   ├── LoginLink.cs
│   ├── LoginMethod.cs                                entidad de identity.LoginMethods (ADR 0033, parte 3b): Type, Value único por tipo, IsPrimary, VerifiedAtUtc, ManagedByTenantId?
│   ├── LoginMethodType.cs                            Email | Phone | Google
│   ├── LoginAudit.cs
│   ├── LoginAuditMethod.cs                           Code | Google | WhatsAppCode | WhatsAppLink: con qué se entró; lo usa LoginAudit (en ArquitecturaBase se llama LoginMethod)
│   ├── LoginCodeErrors.cs                            Auth.LoginCode.*
│   ├── LoginLinkErrors.cs                            Auth.LoginLink.*
│   ├── SignupErrors.cs                               Auth.Signup.* (cerrado, email tomado…)
│   └── AccountErrors.cs                              Identity.Account.* (LockedOut, Suspended, PendingDeletion con la fecha y el cancelTicket)
├── Authorization/                                    [E4]
│   ├── Permissions.cs                                organización + empresa, con All, OrganizationScoped, CompanyScoped
│   ├── PersonalPermissions.cs                        personal.*, implícitos de la persona en su espacio personal
│   ├── PlatformPermissions.cs                        platform.*
│   ├── Role.cs                                       TenantId, Name, Scope, CompanyId? (SpecificCompany), IsSystem, Permissions
│   ├── RoleScope.cs                                  Organization | AnyCompany | SpecificCompany
│   ├── RoleAssignment.cs                             UserId, RoleId, CompanyId?
│   ├── SystemRoles.cs                                TenantAdmin, CompanyAdmin
│   └── RoleErrors.cs                                 Roles.Role.*
├── Companies/                                        [E6]
│   ├── Company.cs                                    Name, TaxId?, TimeZoneId?, Status
│   ├── CompanyStatus.cs
│   ├── CompanyErrors.cs                              Companies.Company.*
│   ├── CompanyMembership.cs                          UserId, CompanyId, Status
│   ├── CompanyMembershipStatus.cs
│   └── CompanyMembershipErrors.cs                    Companies.Membership.* (último CompanyAdmin…)
├── Settings/                                         [E3–E5]
│   ├── TenantSettings.cs                             DefaultCulture, DefaultTimeZoneId, DefaultCurrency
│   ├── PlatformSettings.cs                           [E3] ConsumerSignup, AccountDeletionGraceDays (30); la E5 suma BusinessSignup y MaxOwnedOrganizations
│   ├── ConsumerSignupMode.cs                         [E3] Open | Closed
│   ├── BusinessSignupMode.cs                         [E5] Open | RequiresApproval | Closed
│   └── SettingsErrors.cs
├── Platform/                                         [E5]
│   ├── PlatformRole.cs                               Owner | Support
│   ├── PlatformRoleAssignment.cs
│   └── PlatformErrors.cs                             Platform.*
├── Auditing/                                         [E2–E3]
│   ├── AuditEntry.cs                                 [E2] tenant, append-only
│   ├── AuditAction.cs                                [E2] Created | Updated | Deleted | Restored | Custom
│   ├── AuditActorKind.cs                             [E2] User | PlatformOperator | System
│   ├── SecurityEvent.cs                              [E3] esquema platform, append-only: Type, ActorId, TargetTenantId?, Reason? (cuenta: métodos de ingreso y baja; plataforma: acciones de operadores, siempre con TargetTenantId y motivo)
│   └── SecurityEventType.cs                          [E3] los de la cuenta; la E5 suma los de los operadores
├── Messaging/                                        [E3]
│   ├── OutboxMessage.cs                              Channel, payload cifrado, Attempts, NextAttemptAtUtc
│   ├── OutboxChannel.cs                             valor ("email", …): cada módulo suma el suyo
│   └── OutboxStatus.cs                               Pending | Sent | Failed
└── WhatsApp/                                         [E8]
    ├── WhatsAppContact.cs                            wa_id ↔ identidad (esquema identity)
    ├── WhatsAppMessage.cs
    ├── WhatsAppMessageDirection.cs
    ├── WhatsAppMessageKind.cs
    ├── WhatsAppMessageStatus.cs
    ├── WhatsAppText.cs
    ├── WhatsAppErrors.cs
    └── WhatsAppChannel.cs                            [E11] PhoneNumberId → TenantId
```

## src/ArquitecturaBaseMultitenant.Application `[E0]`

Casos de uso, puertos, modelos, validación y textos. Referencia solo a Domain.

```
ArquitecturaBaseMultitenant.Application/
├── ArquitecturaBaseMultitenant.Application.csproj    NeutralLanguage=es, InternalsVisibleTo tests
├── DependencyInjection.cs                            [E1] AddApplication(): registro explícito
├── Common/                                           [E1]
│   ├── Pagination/
│   │   ├── PagedRequest.cs                           Page, PageSize (10 por defecto | 20 | 50 | 100), Sort, Search
│   │   ├── PagedResult.cs                            Items, Page, PageSize, TotalCount, TotalPages, HasPrevious, HasNext
│   │   ├── CursorRequest.cs                          After (opaco), Limit (máx. 100): tablas que solo crecen
│   │   ├── CursorResult.cs                           Items, NextCursor, HasMore (sin total)
│   │   └── SortDescriptor.cs
│   ├── Validation/
│   │   ├── IRequestValidator.cs                      un solo validador inyectable
│   │   ├── RequestValidator.cs
│   │   ├── ValidationRules.cs                        Required, MaxLength, ValidEmail, ValidTimeZone, ValidPermissions…
│   │   ├── FieldErrors.cs
│   │   ├── PagedRequestValidator.cs                  página, tamaño permitido, sort en SortableFields, búsqueda ≤ 100
│   │   └── CursorRequestValidator.cs                 cursor válido, límite ≤ 100
│   ├── Logging/
│   │   └── OperationLog.cs                           LogHandling/LogHandled/LogFailed con [LoggerMessage]
│   ├── Formatting/                                   [E1] presentación unificada del lado del back (backend.md §18)
│   │   ├── DisplayFormatter.cs                       Date, DateTime, Time, DateLong, Integer, Decimal, Money, Percent,
│   │   │                                             Phone, TaxId, Empty; siempre con cultura y zona explícitas
│   │   ├── CultureProfiles.cs                        es-AR y en-US: patrones, 24/12 h, separadores (espejo del front)
│   │   └── SupportedCultures.cs                      es-AR (por defecto), en-US
│   └── Exceptions/
│       └── UniqueConstraintViolationException.cs
├── Configuration/                                    [E3]
│   ├── Auth/                                         [E3]
│   │   ├── LoginCodeOptions.cs
│   │   ├── LoginLinkOptions.cs
│   │   ├── SignupOptions.cs
│   │   └── InvitationOptions.cs
│   └── Messaging/
│       └── OutboxOptions.cs                         [E3] reintentos y backoff
├── Interfaces/                                       [E1]
│   ├── Services/                                     lo que inyectan los controllers
│   │   ├── IAccountService.cs                        [E3] registro B2C
│   │   ├── ILoginCodeService.cs                      [E3]
│   │   ├── ILoginLinkService.cs                      [E3]
│   │   ├── IExternalLoginService.cs                  [E3]
│   │   ├── IConnectService.cs                        [E3] emisión de tokens y cambio de acceso u organización
│   │   ├── IInvitationService.cs                     [E3] vista previa y aceptación
│   │   ├── IProfileService.cs                        [E3] /api/me
│   │   ├── IBusinessSignupService.cs                  [E6] "Registrá tu empresa" (alta B2B, aparte del acceso B2C)
│   │   ├── IUserService.cs                           [E6]
│   │   ├── IRoleService.cs                           [E4]
│   │   ├── ICompanyService.cs                        [E6]
│   │   ├── ICompanyMemberService.cs                  [E6]
│   │   ├── ITenantSettingsService.cs                 [E6]
│   │   ├── IAuditLogService.cs                       [E6]
│   │   ├── ITenantAdministrationService.cs           [E5]
│   │   ├── IPlatformAccountService.cs                [E5]
│   │   ├── IPlatformOperatorService.cs               [E5]
│   │   ├── IPlatformAuditService.cs                  [E5]
│   │   ├── ITimeZoneCatalogService.cs                [E1] catálogo traducido para GET /api/time-zones
│   │   └── IPlatformSettingsService.cs              [E5]
│   ├── Persistence/                                  implementados en Infrastructure/Persistence
│   │   ├── IUnitOfWork.cs                            [E2]
│   │   ├── CommitPolicy.cs                           [E2] OnSuccess | OnAnyResult
│   │   ├── CommitPolicyExtensions.cs                 [E2]
│   │   ├── ITenantScope.cs                           [E2] Enter(tenantId)
│   │   ├── IAuditLog.cs                              [E2] eventos explícitos de auditoría
│   │   ├── ITenantRepository.cs                      [E3]
│   │   ├── ITenantReader.cs                          [E3] desde acá TenantJobRunner recorre las organizaciones activas
│   │   ├── IMemberRepository.cs                      [E3]
│   │   ├── IMemberReader.cs                          [E3] punto de partida de "usuarios de mi organización"
│   │   ├── IUserRepository.cs                        [E3] escrituras de la identidad
│   │   ├── IInvitationRepository.cs                  [E3]
│   │   ├── ILoginCodeRepository.cs                   [E3]
│   │   ├── ILoginLinkRepository.cs                   [E3]
│   │   ├── ILoginAuditRepository.cs                  [E3]
│   │   ├── ITenantSettingsRepository.cs              [E3]
│   │   ├── ITenantSettingsReader.cs                  [E3]
│   │   ├── IRoleRepository.cs                        [E4]
│   │   ├── IRoleReader.cs                            [E4]
│   │   ├── IRoleAssignmentRepository.cs              [E4]
│   │   ├── IPermissionReader.cs                      [E4]
│   │   ├── IPlatformSettingsRepository.cs            [E5]
│   │   ├── IPlatformSettingsReader.cs                [E3]
│   │   ├── IPlatformRoleAssignmentRepository.cs      [E5]
│   │   ├── IPlatformReader.cs                        [E5] organizaciones, identidades y operadores (vista de plataforma)
│   │   ├── ISecurityEventRepository.cs               [E3]
│   │   ├── ISecurityEventReader.cs                   [E5]
│   │   ├── ICompanyRepository.cs                     [E6]
│   │   ├── ICompanyReader.cs                         [E6]
│   │   ├── ICompanyMembershipRepository.cs           [E6]
│   │   ├── ICompanyMembershipReader.cs               [E6]
│   │   └── IAuditEntryReader.cs                     [E6]
│   └── Integrations/
│       ├── Request/                                  [E2–E3]
│       │   ├── ICurrentUser.cs                       UserId, Access (Consumer | Business | Platform)
│       │   ├── ITenantContext.cs                     TenantId, TenantKind, RequiredTenantId (del acceso activo)
│       │   ├── IRequestInfo.cs                       IP, user agent
│       │   └── IPublicOrigin.cs                      origen público para armar enlaces
│       ├── Identity/                                 [E3–E4]
│       │   ├── ISignInService.cs                     técnico: sign-in, bloqueos, revocar sesiones (≤12 miembros)
│       │   ├── IUserLookup.cs                        búsqueda global por email o teléfono
│       │   ├── IPermissionService.cs                 [E4] permisos efectivos en la organización + invalidación
│       │   ├── IPlatformPermissionService.cs         [E4]
│       │   ├── ITokenRevoker.cs
│       │   └── IGoogleAvailability.cs                [E3]
│       ├── Security/                                 [E3]
│       │   ├── ISecureTokenGenerator.cs
│       │   ├── ILoginCodeGenerator.cs
│       │   ├── ILoginCodeHasher.cs
│       │   └── IPayloadProtector.cs                  cifrado del payload del outbox
│       ├── Messaging/                                [E3]
│       │   ├── IOutbox.cs                            Enqueue(canal registrado) dentro del límite
│       │   ├── ILoginCodeChannel.cs                 [E3] envía un código por un canal; el núcleo trae "email"
│       │   ├── IInvitationChannel.cs                [E3] envía una invitación; el núcleo trae "email"
│       │   ├── IAccountNoticeChannel.cs              [E3] envía un aviso de la cuenta a un método de ingreso; el núcleo trae "email"
│       │   └── IEmailTemplateRenderer.cs
│       ├── Caching/
│       │   └── ITenantStatusCache.cs                 [E3] invalidar al suspender o reactivar
│       ├── Time/
│       │   └── ITimeZoneService.cs                   [E1] IsValid, GetDayRangeUtc, catálogo
│       └── Phones/
│           ├── IPhoneNumberParser.cs                [E3] Parse(country, number, PhoneUsage) → Result<PhoneNumber>; Mask
│           ├── PhoneUsage.cs                        [E3] Any | Mobile | WhatsApp
│           └── IPhoneLinkObserver.cs                [E3] el núcleo avisa cambios de teléfono; el módulo suelta el contacto
├── Models/                                           [E1] *Request (entrada), *Response (salida), ReadModels/*Row (proyección)
│   ├── Auth/                                         [E3]
│   │   ├── SignupRequest.cs
│   │   ├── VerifySignupRequest.cs
│   │   ├── RequestLoginCodeRequest.cs
│   │   ├── RequestLoginCodeResponse.cs
│   │   ├── VerifyLoginCodeRequest.cs
│   │   ├── VerifyLoginCodeResponse.cs
│   │   ├── RequestLoginLinkRequest.cs
│   │   ├── ConsumeLoginLinkRequest.cs
│   │   ├── LoginLinkPreviewResponse.cs
│   │   ├── LoginMethodsResponse.cs                   respuesta de GET /api/auth/methods (medios de ingreso encendidos)
│   │   ├── ConnectUser.cs                            identidad + acceso y tenant elegidos, para armar el principal
│   │   ├── AccessSelectionRequest.cs                 acceso (consumer|business) y tenant pedidos en authorize
│   │   └── ReturnUrls.cs
│   ├── Invitations/                                  [E3]
│   │   ├── AcceptInvitationRequest.cs
│   │   └── InvitationPreviewResponse.cs
│   ├── Profile/                                      [E3]
│   │   ├── MeResponse.cs                             cuenta + acceso activo + espacio personal + organizaciones + permisos + preferencias efectivas (cultura, zona, moneda) + features
│   │   ├── OrganizationSummary.cs                         id, nombre, slug y estado (para elegir organización)
│   │   ├── EffectivePermissions.cs                   organización + por empresa
│   │   ├── UpdateMeRequest.cs
│   │   └── VerifyDestinationRequest.cs
│   ├── Organizations/                                [E6]
│   │   └── BusinessSignupRequest.cs                  nombre, slug y CUIT de la empresa, y datos de quien la registra
│   ├── PublicSite/                                   [E6–E7]
│   │   ├── PublicPageResponse.cs                     [E6] nombre, logo, descripción, contacto, slug y estado
│   │   ├── UpdatePublicPageRequest.cs                [E6]
│   │   ├── ListDirectoryRequest.cs                   [E7] paginado del directorio
│   │   └── ReadModels/
│   │       └── DirectoryEntryRow.cs                  [E7]
│   ├── Users/                                        [E6]
│   │   ├── ListUsersRequest.cs                       SortableFields
│   │   ├── UserDetailResponse.cs
│   │   ├── UserFilterCountsResponse.cs
│   │   ├── InviteUserRequest.cs
│   │   ├── UpdateUserRequest.cs
│   │   ├── SetUserRolesRequest.cs
│   │   ├── ChangeUserStatusRequest.cs
│   │   ├── ResendInvitationRequest.cs
│   │   └── ReadModels/
│   │       └── UserRow.cs
│   ├── Roles/                                        [E4]
│   │   ├── ListRolesRequest.cs
│   │   ├── RoleResponse.cs
│   │   ├── CreateRoleRequest.cs
│   │   ├── UpdateRoleRequest.cs
│   │   ├── DeleteRoleRequest.cs
│   │   ├── PermissionGroupResponse.cs
│   │   └── ReadModels/
│   │       └── RoleRow.cs
│   ├── Companies/                                    [E6]
│   │   ├── ListCompaniesRequest.cs
│   │   ├── CompanyResponse.cs
│   │   ├── CreateCompanyRequest.cs
│   │   ├── UpdateCompanyRequest.cs
│   │   ├── ListCompanyMembersRequest.cs
│   │   ├── AddCompanyMemberRequest.cs
│   │   ├── UpdateCompanyMemberRolesRequest.cs
│   │   └── ReadModels/
│   │       ├── CompanyRow.cs
│   │       └── CompanyMemberRow.cs
│   ├── Settings/                                     [E6]
│   │   ├── TenantSettingsResponse.cs
│   │   └── UpdateTenantSettingsRequest.cs
│   ├── Auditing/                                     [E6]
│   │   ├── ListAuditEntriesRequest.cs
│   │   └── ReadModels/
│   │       └── AuditEntryRow.cs
│   ├── Time/                                         [E1]
│   │   ├── TimeZoneResponse.cs
│   │   └── DayRangeUtc.cs
│   ├── Platform/                                     [E5]
│   │   ├── ListTenantsRequest.cs
│   │   ├── TenantDetailResponse.cs
│   │   ├── CreateTenantRequest.cs
│   │   ├── ChangeTenantStatusRequest.cs              con motivo
│   │   ├── ListAccountsRequest.cs
│   │   ├── ChangeAccountStatusRequest.cs             con motivo
│   │   ├── AddOperatorRequest.cs
│   │   ├── ListSecurityEventsRequest.cs
│   │   ├── PlatformSettingsResponse.cs
│   │   ├── UpdatePlatformSettingsRequest.cs
│   │   └── ReadModels/
│   │       ├── TenantRow.cs
│   │       ├── AccountRow.cs
│   │       ├── OperatorRow.cs
│   │       └── SecurityEventRow.cs
│   ├── Notifications/                                [E3]
│   │   └── AccountNotice.cs                          jerarquía cerrada: LoginMethodChanged (qué pasó, método enmascarado, fecha), ReviewLoginMethods (organización), DeletionRequested (fecha), DeletionCancelled, AccountDeleted
│   └── Messaging/                                    [E3]
│       └── EmailMessage.cs
├── Validation/                                       [E1] un *RequestValidator (internal sealed) por request que lo necesite
│   ├── Auth/                                         [E3]
│   │   ├── SignupRequestValidator.cs
│   │   ├── VerifySignupRequestValidator.cs
│   │   ├── RequestLoginCodeRequestValidator.cs
│   │   ├── VerifyLoginCodeRequestValidator.cs
│   │   └── RequestLoginLinkRequestValidator.cs
│   ├── Invitations/                                  [E3]
│   │   └── AcceptInvitationRequestValidator.cs
│   ├── Profile/                                      [E3]
│   │   └── UpdateMeRequestValidator.cs
│   ├── Organizations/                                [E6]
│   │   └── BusinessSignupRequestValidator.cs
│   ├── PublicSite/                                   [E6–E7]
│   │   ├── UpdatePublicPageRequestValidator.cs       [E6]
│   │   └── ListDirectoryRequestValidator.cs          [E7]
│   ├── Users/                                        [E6]
│   │   ├── ListUsersRequestValidator.cs
│   │   ├── InviteUserRequestValidator.cs
│   │   ├── UpdateUserRequestValidator.cs
│   │   └── SetUserRolesRequestValidator.cs
│   ├── Roles/                                        [E4]
│   │   ├── ListRolesRequestValidator.cs
│   │   ├── CreateRoleRequestValidator.cs
│   │   └── UpdateRoleRequestValidator.cs
│   ├── Companies/                                    [E6]
│   │   ├── ListCompaniesRequestValidator.cs
│   │   ├── CreateCompanyRequestValidator.cs
│   │   ├── UpdateCompanyRequestValidator.cs
│   │   ├── AddCompanyMemberRequestValidator.cs
│   │   └── UpdateCompanyMemberRolesRequestValidator.cs
│   ├── Settings/
│   │   └── UpdateTenantSettingsRequestValidator.cs   [E6]
│   ├── Auditing/
│   │   └── ListAuditEntriesRequestValidator.cs       [E6]
│   └── Platform/                                     [E5]
│       ├── ListTenantsRequestValidator.cs
│       ├── CreateTenantRequestValidator.cs
│       ├── ChangeTenantStatusRequestValidator.cs
│       ├── ChangeAccountStatusRequestValidator.cs
│       ├── AddOperatorRequestValidator.cs
│       └── UpdatePlatformSettingsRequestValidator.cs
├── Services/                                         [E1] internal sealed partial; helpers con sufijo fijo y sin IUnitOfWork
│   ├── Auth/                                         [E3]
│   │   ├── AccountService.cs                         registro de personas (identidad + espacio personal)
│   │   ├── SignupPolicy.cs                           ¿el registro está abierto? ¿el email está libre?
│   │   ├── LoginCodeService.cs
│   │   ├── LoginCodeIssuer.cs
│   │   ├── LoginCodeVerifier.cs
│   │   ├── LoginLinkService.cs
│   │   ├── LoginLinkIssuer.cs
│   │   ├── ConnectService.cs
│   │   ├── AccessSwitchPolicy.cs                     acceso pedido válido: membresía activa (B2B) o identidad activa (B2C)
│   │   ├── UserCultures.cs                           cultura efectiva para mensajes en segundo plano
│   │   └── ExternalLoginService.cs                   [E3]
│   ├── Invitations/                                  [E3]
│   │   ├── InvitationService.cs
│   │   └── InvitationIssuer.cs                       lo usan UserService y TenantAdministrationService
│   ├── Profile/                                      [E3]
│   │   ├── ProfileService.cs
│   │   └── DestinationCodeVerifier.cs
│   ├── Organizations/                                [E6]
│   │   ├── BusinessSignupService.cs    
│   │   ├── BusinessSignupPolicy.cs                   modo de alta y límite por persona
│   │   └── TenantProvisioner.cs                      idempotente: settings, roles de sistema, 1ª empresa, membresía de Dueño (TenantAdmin) y página pública en Draft
│   ├── Users/                                        [E6]
│   │   ├── UserService.cs
│   │   ├── UserGuard.cs
│   │   ├── LastTenantAdminGuard.cs
│   │   └── AccountAccessRevoker.cs
│   ├── Roles/                                        [E4] ← ÁREA DE REFERENCIA
│   │   ├── RoleService.cs
│   │   └── RoleGuard.cs
│   ├── Companies/                                    [E6]
│   │   ├── CompanyService.cs
│   │   ├── CompanyGuard.cs
│   │   ├── CompanyMemberService.cs
│   │   └── LastCompanyAdminGuard.cs
│   ├── Settings/
│   │   └── TenantSettingsService.cs                  [E6]
│   ├── Auditing/
│   │   └── AuditLogService.cs                        [E6]
│   ├── Time/                                         [E1]
│   │   └── TimeZoneCatalogService.cs                 usa ITimeZoneService; devuelve TimeZoneResponse
│   └── Platform/                                     [E5]
│       ├── TenantAdministrationService.cs
│       ├── PlatformAccountService.cs
│       ├── PlatformOperatorService.cs
│       ├── PlatformAuditService.cs
│       ├── PlatformSettingsService.cs
│       └── PlatformActionGuard.cs                    motivo obligatorio + SecurityEvent antes de Enter
├── Modules/                                         [E8] módulos quitables: el núcleo no los referencia (test)
│   └── WhatsApp/                                    AddWhatsAppModule() registra todo lo de esta carpeta
│       ├── WhatsAppModule.cs                         AddWhatsAppModule(): servicios, canales y opciones
│       ├── Configuration/
│       │   └── WhatsAppLoginOptions.cs
│       ├── Interfaces/
│       │   ├── IWhatsAppWebhookService.cs
│       │   ├── IWhatsAppInboundService.cs
│       │   ├── IWhatsAppContactRepository.cs
│       │   ├── IWhatsAppMessageRepository.cs
│       │   ├── IWhatsAppAvailability.cs
│       │   ├── IWhatsAppSignatureValidator.cs
│       │   ├── IWhatsAppWebhookReader.cs
│       │   └── IWhatsAppInboundSignal.cs
│       ├── Models/
│       │   ├── WhatsAppOutboundMessage.cs            jerarquía cerrada: texto, botones, código, invitación, aviso
│       │   ├── WhatsAppWebhookBatch.cs
│       │   └── BotButtons.cs
│       ├── Services/
│       │   ├── WhatsAppWebhookService.cs
│       │   ├── WhatsAppInboundService.cs
│       │   ├── WhatsAppContactLinker.cs
│       │   ├── WhatsAppLoginCodeChannel.cs           implementa ILoginCodeChannel ("whatsapp")
│       │   ├── WhatsAppInvitationChannel.cs          implementa IInvitationChannel ("whatsapp")
│       │   ├── WhatsAppAccountNoticeChannel.cs       implementa IAccountNoticeChannel ("whatsapp")
│       │   ├── WhatsAppPhoneLinkObserver.cs          implementa IPhoneLinkObserver
│       │   └── BotReply.cs
│       └── Resources/
│           ├── WhatsAppTexts.resx / .en.resx         textos del bot y de las plantillas
│           └── WhatsAppTexts.cs
└── Resources/                                        [E1] es (neutral) + en; claves en paridad
    ├── Errors.resx                                   [E1] código del error → texto; Title.<ErrorType>
    ├── Errors.en.resx
    ├── Validation.resx                               [E1]
    ├── Validation.en.resx
    ├── Permissions.resx                              [E4] Area.*, Permission.*, PermissionDescription.*, Role.*
    ├── Permissions.en.resx
    ├── Notifications.resx                            [E3] asuntos y cuerpos de correo (también los avisos de la cuenta por correo)
    ├── Notifications.en.resx
    ├── Audit.resx                                    [E6] AuditAction.*, Entity.*
    ├── Audit.en.resx
    ├── ErrorMessages.cs
    ├── ValidationMessages.cs
    ├── PermissionTexts.cs
    ├── NotificationTexts.cs                          con cultura explícita
    └── AuditTexts.cs
```

## src/ArquitecturaBaseMultitenant.Infrastructure `[E0]`

EF Core, Identity, OpenIddict, mensajería, WhatsApp y adaptadores técnicos. Referencia a Application.

```
ArquitecturaBaseMultitenant.Infrastructure/
├── ArquitecturaBaseMultitenant.Infrastructure.csproj  FrameworkReference AspNetCore; Templates como EmbeddedResource
├── DependencyInjection.cs                             [E1] AddInfrastructure(cfg, env) → llama a cada *Registration
├── Persistence/                                       [E1–E2]
│   ├── ApplicationDbContext.cs                        [E2] IdentityUserContext<ApplicationUser,Guid> + IDataProtectionKeyContext
│   ├── PersistenceRegistration.cs                     [E2] DbContext (appdb), interceptores, UoW, repositorios, readers
│   ├── UnitOfWork.cs                                  [E2]
│   ├── TenantContext.cs                               [E2] implementa ITenantContext + ITenantScope
│   ├── UniqueViolations.cs                            [E2] nombre de índice → error de negocio
│   ├── DatabaseBootstrapExtensions.cs                 [E2] solo Development: crea la base con ICU es-AR, crea mt_app, migra y hace el seed (appdb-admin)
│   ├── Configurations/                                una IEntityTypeConfiguration<T> por entidad
│   │   ├── Platform/
│   │   │   ├── TenantConfiguration.cs                 [E3]
│   │   │   ├── OutboxMessageConfiguration.cs          [E3]
│   │   │   ├── PlatformSettingsConfiguration.cs       [E3]
│   │   │   ├── PlatformRoleAssignmentConfiguration.cs [E5]
│   │   │   └── SecurityEventConfiguration.cs          [E3]
│   │   ├── Identity/
│   │   │   ├── ApplicationUserConfiguration.cs        [E3] Email y PhoneNumber sin índice único (copia del método principal)
│   │   │   ├── LoginCodeConfiguration.cs              [E3]
│   │   │   ├── LoginLinkConfiguration.cs              [E3]
│   │   │   └── LoginAuditConfiguration.cs             [E3]
│   │   └── Tenant/                                    datos privados (RLS)
│   │       ├── AuditEntryConfiguration.cs             [E2]
│   │       ├── MemberConfiguration.cs                 [E3]
│   │       ├── InvitationConfiguration.cs             [E3]
│   │       ├── TenantSettingsConfiguration.cs         [E3]
│   │       ├── RoleConfiguration.cs                   [E4]
│   │       ├── RoleAssignmentConfiguration.cs         [E4]
│   │       ├── CompanyConfiguration.cs                [E6]
│   │       └── CompanyMembershipConfiguration.cs      [E6]
│   ├── Interceptors/                                  [E2]
│   │   ├── TenantConnectionInterceptor.cs             set_config('app.tenant_id') al abrir la conexión
│   │   ├── TenantStampInterceptor.cs                  sella y protege las columnas de tenant
│   │   ├── SoftDeleteInterceptor.cs
│   │   ├── AuditableEntityInterceptor.cs
│   │   └── AuditTrailInterceptor.cs                   AuditEntry con el diff, en la misma transacción
│   ├── Rls/                                           [E2]
│   │   ├── RlsMigrationBuilderExtensions.cs           EnableTenantRls / EnablePublicRls / EnablePartiesRls + triggers
│   │   ├── RlsSql.cs                                  plantillas SQL de políticas y triggers
│   │   ├── TenantIsolationModelValidator.cs           toda entidad clasificada, con esquema y filtro correctos
│   │   └── RuntimeRoleValidator.cs                    aborta si mt_app es privilegiado o si la base no es ICU es-AR
│   ├── Extensions/                                    [E1–E2]
│   │   ├── ModelBuilderExtensions.cs                  [E2] filtros "Tenant", "Public", "Parties", "SoftDelete"
│   │   ├── QueryableExtensions.cs                     [E1–E2] ApplySort, ApplySearch, ToPagedResultAsync, ToCursorResultAsync
│   │   ├── SortMap.cs                                 [E1] campo del contrato → expresión; cada reader declara el suyo
│   │   ├── CursorCodec.cs                             [E2] (campo de orden, Id) ↔ base64url
│   │   ├── SearchFunctions.cs                         [E2] f_unaccent para EF (búsqueda sin acentos ni mayúsculas)
│   │   ├── AdvisoryLockExtensions.cs                  [E2]
│   │   ├── AdvisoryLockKeys.cs                        [E2] siempre con tenant
│   │   └── TransactionExtensions.cs                   [E2] RequireTransaction
│   ├── Repositories/                                  internal sealed; escrituras de agregados
│   │   ├── AuditLog.cs                                [E2] IAuditLog
│   │   ├── TenantRepository.cs                        [E3]
│   │   ├── MemberRepository.cs                        [E3]
│   │   ├── UserRepository.cs                          [E3]
│   │   ├── InvitationRepository.cs                    [E3]
│   │   ├── LoginCodeRepository.cs                     [E3]
│   │   ├── LoginLinkRepository.cs                     [E3]
│   │   ├── LoginAuditRepository.cs                    [E3]
│   │   ├── TenantSettingsRepository.cs                [E3]
│   │   ├── RoleRepository.cs                          [E4]
│   │   ├── RoleAssignmentRepository.cs                [E4]
│   │   ├── PlatformSettingsRepository.cs              [E5]
│   │   ├── PlatformRoleAssignmentRepository.cs        [E5]
│   │   ├── SecurityEventRepository.cs                 [E3]
│   │   ├── CompanyRepository.cs                       [E6]
│   │   └── CompanyMembershipRepository.cs           [E6]
│   ├── Readers/                                       AsNoTracking + proyección a *Row/*Response
│   │   ├── TenantReader.cs                            [E3]
│   │   ├── MemberReader.cs                            [E3] Members ⋈ AspNetUsers (el único camino a la identidad)
│   │   ├── TenantSettingsReader.cs                    [E3] con caché t:
│   │   ├── RoleReader.cs                              [E4]
│   │   ├── PermissionReader.cs                        [E4]
│   │   ├── PlatformSettingsReader.cs                  [E3] con caché p:
│   │   ├── CompanyReader.cs                           [E6]
│   │   ├── CompanyMembershipReader.cs                 [E6]
│   │   ├── AuditEntryReader.cs                        [E6]
│   │   └── Platform/                                  [E5] ÚNICA lista blanca para IgnoreQueryFilters
│   │       ├── PlatformReader.cs
│   │       └── SecurityEventReader.cs
│   ├── Migrations/                                    una sola carpeta; RLS y grants dentro de cada migración
│   │   ├── <ts>_InitialSchema.cs                      [E2] esquemas, grants a mt_app, funciones de triggers, unaccent,
│   │   │                                              pg_trgm, public.f_unaccent y platform.IdempotencyKeys
│   │   ├── <ts>_IdentityAndTenancy.cs                 [E3] usuarios, tenants, miembros, invitaciones, settings de organización y de plataforma,
│   │   │                                              eventos de seguridad, outbox
│   │   ├── <ts>_OpenIddict.cs                         [E3]
│   │   ├── <ts>_Roles.cs                              [E4]
│   │   ├── <ts>_Platform.cs                           [E5] roles de plataforma; PlatformSettings suma BusinessSignup y MaxOwnedOrganizations
│   │   ├── <ts>_Companies.cs                          [E6]
│   │   └── ApplicationDbContextModelSnapshot.cs
│   └── Seed/
│       ├── SeedExtensions.cs                          [E3] orden e idempotencia; corre en todos los ambientes, dentro de un
│       │                                              límite y con el advisory lock "seed:" (sin carreras entre réplicas)
│       ├── SeedOptions.cs                             [E3] Seed:PlatformOwner
│       ├── OpenIddictSeeder.cs                        [E3] cliente web + scope api
│       ├── PlatformSeeder.cs                          [E3] ajustes de plataforma + operador inicial (Seed:PlatformOwner); la E5 le asigna el rol Owner
│       └── DevelopmentSeeder.cs                       [E3] operador; Empresa A (Ana, Kevin); Kevin y Carla como personas
├── Identity/                                          [E3]
│   ├── ApplicationUser.cs                             IsPlatformOperator, Status, Culture, TimeZoneId, DisplayName, LastBusinessTenantId,
│   │                                                  DeletionRequestedAtUtc, DeletionScheduledForUtc, DeletionReason, DeletedAtUtc
│   ├── IdentityRegistration.cs                        Identity core, cookies /account y /connect, DataProtection
│   ├── SignInService.cs
│   ├── UserLookup.cs                                  búsqueda global (lista blanca)
│   ├── PermissionService.cs                           [E4] HybridCache t:{tenant}:perm:{user}
│   ├── PlatformPermissionService.cs                   [E4]
│   ├── IdentityResultExtensions.cs
│   ├── GoogleAvailability.cs                          [E3] Google se enciende con Authentication:Google:ClientId
│   └── OpenIddict/
│       ├── OpenIddictRegistration.cs                  code + PKCE + refresh, endpoints, validación local
│       ├── AuthServerDefaults.cs                      rutas, scopes, duraciones
│       ├── CertificateLoader.cs
│       ├── WebClientOptions.cs
│       └── TokenRevoker.cs                            revoca por identidad, por acceso o por organización
├── Messaging/                                         [E3]
│   ├── MessagingRegistration.cs
│   ├── Outbox.cs                                      IOutbox
│   ├── OutboxDispatcher.cs                            BackgroundService: SKIP LOCKED, backoff
│   ├── IChannelSender.cs                              internal: un emisor por canal
│   └── Email/
│       ├── EmailChannelSender.cs
│       ├── IEmailTransport.cs
│       ├── SmtpEmailTransport.cs
│       ├── PickupDirectoryEmailTransport.cs           Development: .eml en disco
│       ├── MimeMessageFactory.cs
│       ├── EmailTemplateRenderer.cs                   textos desde Notifications.resx
│       ├── EmailOptions.cs
│       ├── SmtpOptions.cs
│       ├── SmtpOptionsValidator.cs
│       └── Templates/
│           ├── _Layout.html
│           ├── LoginCode.html                         código de ingreso y código para verificar un correo nuevo (textos VerifyEmail, como en ArquitecturaBase)
│           ├── SignupCode.html
│           ├── Invitation.html
│           ├── LoginMethodChanged.html                método agregado, quitado o principal cambiado; va a todos los métodos (multitenancy.md §3.1)
│           ├── ReviewLoginMethods.html                membresía terminada: "Revisá tus métodos de ingreso"
│           ├── AccountDeletionRequested.html
│           ├── AccountDeletionCancelled.html
│           ├── AccountDeleted.html                    va al método principal y se encola antes de borrar los métodos (multitenancy.md §3.2)
│           └── DataExportReady.html                   [E10] enlace de descarga que vence a las 48 h; solo por correo
├── Modules/                                         [E8]
│   └── WhatsApp/                                    módulo opcional: se enciende con WhatsApp:PhoneNumberId
│       ├── WhatsAppInfrastructureModule.cs          AddWhatsAppModule(cfg): cliente, workers, health, persistencia
│       ├── WhatsAppOptions.cs
│       ├── WhatsAppOptionsValidator.cs
│       ├── WhatsAppTemplateCatalog.cs                las 7 plantillas de whatsapp-plantillas.md (codigo_ingreso, invitacion_organizacion, aviso_metodo_ingreso,
│       │                                             revisa_metodos_ingreso, baja_cuenta_pedida, baja_cuenta_cancelada, cuenta_eliminada): su clave
│       │                                             WhatsApp:Templates:<Nombre> y el orden de sus variables; lo usa Cloud/WhatsAppMessagePayload
│       ├── Cloud/
│       │   ├── IWhatsAppCloudClient.cs               internal
│       │   ├── WhatsAppCloudClient.cs                Graph v25.0, POST sin reintentos
│       │   ├── WhatsAppMessagePayload.cs
│       │   ├── WhatsAppSendResult.cs
│       │   └── WhatsAppChannelSender.cs              emisor del outbox para el canal "whatsapp"
│       ├── Webhook/
│       │   ├── WhatsAppSignatureValidator.cs
│       │   └── WhatsAppWebhookReader.cs
│       ├── Inbound/
│       │   ├── WhatsAppInboundSignal.cs
│       │   └── WhatsAppInboundProcessor.cs           BackgroundService, SKIP LOCKED
│       ├── Retention/
│       │   └── WhatsAppMessageRetentionService.cs    borra el texto a los 90 días
│       ├── Persistence/
│       │   ├── WhatsAppContactConfiguration.cs       esquema identity; las configuraciones las aplica el módulo
│       │   ├── WhatsAppMessageConfiguration.cs
│       │   ├── WhatsAppChannelConfiguration.cs       [E11] esquema platform
│       │   ├── WhatsAppContactRepository.cs
│       │   └── WhatsAppMessageRepository.cs
│       ├── WhatsAppAvailability.cs
│       ├── WhatsAppHealthCheck.cs
│       └── Disabled/
│           └── DisabledWhatsAppAvailability.cs      lo que queda registrado con el módulo apagado
├── Security/                                          [E3]
│   ├── SecureTokenGenerator.cs
│   ├── LoginCodeGenerator.cs
│   ├── LoginCodeHasher.cs
│   ├── LoginCodeHashOptions.cs
│   └── PayloadProtector.cs                            DataProtection
├── Caching/                                           [E2–E3]
│   ├── CachingRegistration.cs
│   ├── CacheKeys.cs                                   t: / s: / u: / p:
│   └── TenantStatusCache.cs                           [E3]
├── Time/                                              [E1]
│   └── TimeZoneService.cs                             [E1] TimeZoneInfo con IDs IANA
├── Phones/                                            [E3]
│   └── LibPhoneNumberParser.cs                        [E3]
└── BackgroundJobs/                                    [E2]
    ├── BackgroundJobsRegistration.cs
    └── TenantJobRunner.cs                             un scope DI por tenant + Enter; en la E2 recibe los tenantId (se prueba con Widget), y desde la E3
                                                       recorre las organizaciones activas con ITenantReader
```

## src/ArquitecturaBaseMultitenant.Api `[E0]`

Borde HTTP. Referencia a Application e Infrastructure (esta última solo desde `Program.cs`).

```
ArquitecturaBaseMultitenant.Api/
├── ArquitecturaBaseMultitenant.Api.csproj             genera docs/contracts/openapi.json en el build
├── Program.cs                                         compone + pipeline (backend.md §16)
├── DependencyInjection.cs                             [E1] AddPresentation()
├── appsettings.json
├── appsettings.Development.json
├── Properties/
│   └── launchSettings.json
├── Authentication/
│   └── OpenIdPrincipalFactory.cs                      [E3] sub, access, tenant_id, tenant_kind + destinos
├── Authorization/                                     [E4]
│   ├── HasPermissionAttribute.cs
│   ├── HasCompanyPermissionAttribute.cs
│   ├── HasPlatformPermissionAttribute.cs
│   ├── PermissionPolicyProvider.cs                    perm: / cperm: / pperm:
│   ├── PermissionRequirement.cs
│   ├── CompanyPermissionRequirement.cs
│   ├── PlatformPermissionRequirement.cs
│   ├── PermissionAuthorizationHandler.cs
│   ├── CompanyPermissionAuthorizationHandler.cs       lee {companyId} de la ruta
│   └── PlatformPermissionAuthorizationHandler.cs
├── Tenancy/                                           [E3]
│   ├── TenantResolutionMiddleware.cs
│   ├── AccessAttribute.cs                             [Access(Consumer|Business|Platform)]
│   ├── AccessFilter.cs                                403 Tenancy.Access.Wrong
│   ├── PublicSiteResolutionMiddleware.cs              [E7] subdominio → IPublicSiteContext (solo datos públicos)
│   ├── PublicSiteAttribute.cs                         [E7] [PublicSite]: la ruta necesita un subdominio de empresa publicado
│   ├── PublicSiteContext.cs                           [E7]
│   └── TenantClaimTypes.cs
├── RequestContext/                                    [E3]
│   ├── CurrentUser.cs
│   ├── RequestInfo.cs
│   └── PublicOrigin.cs
├── Controllers/
│   ├── Auth/                                          [E3]
│   │   ├── ConnectController.cs                       authorize (access=, tenant=), token, logout, userinfo
│   │   ├── SignupController.cs                        POST /api/auth/signup, /verify
│   │   ├── LoginCodeController.cs
│   │   ├── LoginLinkController.cs
│   │   ├── LoginMethodsController.cs                  GET /api/auth/methods [AllowAnonymous]: medios de ingreso encendidos (Google con su ClientId, WhatsApp)
│   │   ├── InvitationsController.cs                   GET preview, POST accept
│   │   └── ExternalLoginController.cs                 [E3]
│   ├── Account/
│   │   ├── MeController.cs                            [E3] GET/PUT /api/me
│   │   ├── BusinessSignupController.cs                [E6] POST /api/auth/business-signup ("Registrá tu empresa")
│   │   └── TimeZonesController.cs                     [E1] GET /api/time-zones
│   ├── Organization/                                  [Access(Business)]; suma PublicPageAdminController (E6: mi página pública)
│   │   ├── RolesController.cs                         [E4] ← referencia
│   │   ├── PermissionsController.cs                   [E4]
│   │   ├── UsersController.cs                         [E6]
│   │   ├── CompaniesController.cs                     [E6]
│   │   ├── CompanyMembersController.cs                [E6] api/companies/{companyId}/members
│   │   ├── SettingsController.cs                      [E6]
│   │   └── AuditController.cs                         [E6]
│   ├── Personal/                                      [E7] [Access(Consumer)]: acá van los módulos B2C del producto
│   └── Platform/                                      [E5] [Access(Platform)]
│       ├── PlatformTenantsController.cs
│       ├── PlatformAccountsController.cs
│       ├── PlatformOperatorsController.cs
│       ├── PlatformAuditController.cs
│       └── PlatformSettingsController.cs
├── Contracts/                                         entrada HTTP: records sealed, props nullable, ToString() sin PII
│   ├── Auth/                                          [E3]
│   │   ├── SignupHttpRequest.cs
│   │   ├── VerifySignupHttpRequest.cs
│   │   ├── RequestLoginCodeHttpRequest.cs
│   │   ├── VerifyLoginCodeHttpRequest.cs
│   │   ├── RequestLoginLinkHttpRequest.cs
│   │   ├── ConsumeLoginLinkHttpRequest.cs
│   │   └── AcceptInvitationHttpRequest.cs
│   ├── Account/
│   │   ├── UpdateMeHttpRequest.cs                     [E3]
│   │   ├── VerifyDestinationHttpRequest.cs            [E3]
│   │   └── BusinessSignupHttpRequest.cs               [E6] POST /api/auth/business-signup ("Registrá tu empresa")
│   ├── Organization/
│   │   ├── RolesQuery.cs                              [E4]
│   │   ├── CreateRoleHttpRequest.cs                   [E4]
│   │   ├── UpdateRoleHttpRequest.cs                   [E4]
│   │   ├── UsersQuery.cs                              [E6]
│   │   ├── InviteUserHttpRequest.cs                   [E6]
│   │   ├── UpdateUserHttpRequest.cs                   [E6]
│   │   ├── SetUserRolesHttpRequest.cs                 [E6]
│   │   ├── CompaniesQuery.cs                          [E6]
│   │   ├── CreateCompanyHttpRequest.cs                [E6]
│   │   ├── UpdateCompanyHttpRequest.cs                [E6]
│   │   ├── CompanyMembersQuery.cs                     [E6]
│   │   ├── AddCompanyMemberHttpRequest.cs             [E6]
│   │   ├── UpdateCompanyMemberRolesHttpRequest.cs     [E6]
│   │   ├── UpdateSettingsHttpRequest.cs               [E6]
│   │   ├── UpdatePublicPageHttpRequest.cs             [E6]
│   │   └── AuditQuery.cs                              [E6]
│   ├── PublicSite/                                    [E7]
│   │   └── DirectoryQuery.cs
│   └── Platform/                                      [E5]
│       ├── TenantsQuery.cs
│       ├── CreateTenantHttpRequest.cs
│       ├── ChangeTenantStatusHttpRequest.cs
│       ├── AccountsQuery.cs
│       ├── ChangeAccountStatusHttpRequest.cs
│       ├── AddOperatorHttpRequest.cs
│       ├── SecurityEventsQuery.cs
│       └── UpdatePlatformSettingsHttpRequest.cs
├── ErrorHandling/                                     [E1]
│   ├── ApiErrorCodes.cs
│   ├── ProblemDetailsMapper.cs
│   ├── ControllerResultExtensions.cs                  ToActionResult / ToCreatedResult / ToAcceptedResult
│   ├── GlobalExceptionHandler.cs
│   ├── MvcInvalidModelStateResponseFactory.cs
│   └── EmptyJsonBodyContentTypeFilter.cs
├── Json/                                              [E1]
│   ├── JsonConfiguration.cs                           un solo ConfigureJson
│   ├── UtcDateTimeConverter.cs                        ISO con Z; rechaza sin offset
│   ├── DateOnlyConverter.cs                           yyyy-MM-dd
│   ├── TimeOnlyConverter.cs                           HH:mm:ss
│   └── MoneyJsonConverter.cs                          { amount, currency }
├── Localization/
│   └── LocalizationExtensions.cs                      [E1]
├── OpenApi/                                           [E1]
│   ├── OpenApiExtensions.cs
│   ├── ProblemResponsesConvention.cs
│   └── ProducesProblemAttribute.cs
├── RateLimiting/                                      [E3]
│   ├── RateLimitingExtensions.cs
│   ├── RateLimitingOptions.cs
│   └── RateLimitPolicies.cs                           por IP: login-code, login-verify, invitation-accept, whatsapp-webhook; por organización: tenant-api
├── Modules/                                         [E8]
│   └── WhatsApp/
│       ├── WhatsAppApiModule.cs                      AddWhatsAppModule(): convención de rutas y rate limit del webhook
│       ├── WhatsAppWebhookController.cs              GET/POST /webhooks/whatsapp
│       ├── ConditionalWhatsAppRouteConvention.cs
│       └── WhatsAppRouteAttribute.cs
└── Hosting/                                           [E1]
    ├── ForwardedHeadersExtensions.cs
    ├── SecurityHeadersExtensions.cs
    └── SpaExtensions.cs                               BackendPrefixes (lista a mano)
```

## src/ArquitecturaBaseMultitenant.AppHost y .ServiceDefaults `[E0]`

```
ArquitecturaBaseMultitenant.AppHost/
├── ArquitecturaBaseMultitenant.AppHost.csproj         Aspire.AppHost.Sdk 13.5.x
├── AppHost.cs                                         Postgres :5434 (volumen persistente) + appdb + api (appdb, appdb-admin) + front (Vite :5174) + DevTunnel opcional
├── appsettings.json
├── appsettings.Development.json                       contraseña local de Postgres (solo dev)
└── Properties/
    └── launchSettings.json

ArquitecturaBaseMultitenant.ServiceDefaults/
├── ArquitecturaBaseMultitenant.ServiceDefaults.csproj IsAspireSharedProject
└── Extensions.cs                                      AddServiceDefaults, OpenTelemetry, resiliencia, /health, /alive
```

## tests/

```
tests/
├── Directory.Build.props                              [E0] OutputType Exe, xunit.v3, <Using Include="Xunit"/>
│
├── ArquitecturaBaseMultitenant.Domain.UnitTests/      [E0]
│   ├── ArquitecturaBaseMultitenant.Domain.UnitTests.csproj
│   ├── Results/                                       [E1]
│   │   ├── ResultTests.cs
│   │   └── ValidationErrorTests.cs
│   ├── ValueObjects/                                  [E1]
│   │   ├── MoneyTests.cs                              [E1] redondeo AwayFromZero, monedas distintas no se suman
│   │   ├── EmailTests.cs                              [E1]
│   │   └── PhoneNumberTests.cs                        [E1]
│   ├── Tenancy/                                       [E2–E3]
│   │   ├── TenantTests.cs                             [E2] transiciones de estado
│   │   ├── MemberTests.cs                             [E3]
│   │   └── InvitationTests.cs                         [E3]
│   ├── Authorization/                                 [E4]
│   │   ├── PermissionsTests.cs                        [E4] catálogos separados y sin duplicados
│   │   └── RoleTests.cs                               [E4]
│   └── Companies/                                     [E6]
│       └── CompanyTests.cs
│
├── ArquitecturaBaseMultitenant.Application.UnitTests/ [E0]
│   ├── ArquitecturaBaseMultitenant.Application.UnitTests.csproj
│   ├── Common/                                        [E1]
│   │   ├── RequestValidatorTests.cs
│   │   ├── PagedRequestValidatorTests.cs
│   │   └── DisplayFormatterTests.cs                   [E1] recorre docs/contracts/format-cases.json
│   ├── Resources/                                     [E1]
│   │   ├── ResourceParityTests.cs                     claves y placeholders es = en
│   │   ├── ErrorMessagesTests.cs
│   │   └── PermissionTextsTests.cs                    [E4]
│   ├── Services/                                      [E3]
│   │   ├── Auth/                                      [E3]
│   │   │   ├── AccountServiceTests.cs                 el registro crea identidad + espacio personal
│   │   │   ├── LoginCodeServiceTests.cs
│   │   │   ├── LoginLinkServiceTests.cs
│   │   │   ├── ConnectServiceTests.cs
│   │   │   └── AccessSwitchPolicyTests.cs  
│   │   ├── Invitations/
│   │   │   └── InvitationServiceTests.cs              [E3]
│   │   ├── Profile/
│   │   │   └── ProfileServiceTests.cs                 [E3]
│   │   ├── Organizations/                             [E6]
│   │   │   ├── BusinessSignupServiceTests.cs    
│   │   │   └── TenantProvisionerTests.cs              idempotencia
│   │   ├── Roles/                                     [E4]
│   │   │   ├── RoleServiceTests.cs
│   │   │   └── RoleServiceWriteTests.cs
│   │   ├── Users/
│   │   │   └── UserServiceTests.cs                    [E6]
│   │   ├── Companies/                                 [E6]
│   │   │   ├── CompanyServiceTests.cs
│   │   │   └── CompanyMemberServiceTests.cs
│   │   ├── Settings/
│   │   │   └── TenantSettingsServiceTests.cs          [E6]
│   │   ├── Platform/                                  [E5]
│   │   │   ├── TenantAdministrationServiceTests.cs
│   │   │   ├── PlatformAccountServiceTests.cs
│   │   │   └── PlatformOperatorServiceTests.cs
│   │   └── Modules/WhatsApp/                        [E8]
│   │       ├── WhatsAppInboundServiceTests.cs
│   │       └── BotReplyTests.cs
│   └── TestDoubles/                                   [E1]
│       ├── FakeUnitOfWork.cs                          misma CommitPolicy; cuenta commits y rollbacks
│       ├── FakeTenantContext.cs
│       ├── FakeCurrentUser.cs
│       ├── FakeOutbox.cs
│       ├── FakePermissionService.cs
│       ├── FakeSignInService.cs
│       ├── ServiceFixture.cs                          FakeTimeProvider + FakeLogger + RequestValidator real
│       └── InMemory/                                  un InMemory<X>Repository por puerto que se use en tests
│
├── ArquitecturaBaseMultitenant.Api.IntegrationTests/  [E0]
│   ├── ArquitecturaBaseMultitenant.Api.IntegrationTests.csproj
│   ├── Support/                                       [E0–E3]
│   │   ├── ApiFactory.cs                              [E0] WebApplicationFactory; desde la E2, Testcontainers postgres:18.x (base ICU es-AR, con mt_app real)
│   │   ├── ApiTestGroup.cs                            colección única, en serie
│   │   ├── TenantFixture.cs                           [E2] en la E2, los tenantId de Empresa A, Empresa B y los espacios personales, para RLS; en la E3 suma
│   │   │                                              los usuarios Ana, Kevin, Beto y Carla
│   │   ├── AuthFlow.cs                                [E3] login real: código → authorize (access=, tenant=) → token
│   │   ├── TestAuthHandler.cs                         atajo: X-Test-UserId + X-Test-TenantId
│   │   ├── RuntimeRoleConnection.cs                   [E2] consultas crudas como mt_app para probar RLS
│   │   ├── CapturingOutbox.cs                         [E3]
│   │   ├── FailingCommitUnitOfWork.cs                 [E2]
│   │   └── HttpExtensions.cs
│   ├── TestFeatures/                                  [E1] solo para tests, nunca en src/
│   │   ├── TestController.cs                          [E1] devuelve cada ErrorType
│   │   ├── TestControllerApplicationPart.cs
│   │   └── Isolation/                                 [E2]
│   │       ├── Widget.cs                              ITenantOwned, IAuditable, ISoftDeletable
│   │       ├── Poster.cs                              IPublishedByBusiness: la referencia de un dato público
│   │       ├── Deal.cs                                IConsumerBusinessShared + PartyPolicy: la referencia de un dato compartido
│   │       ├── IsolationModelCustomizer.cs
│   │       ├── WidgetsController.cs                   [E2] el 409 de ConcurrencyTests; [Access(Business)] desde la E3
│   │       ├── PostersController.cs                   [E7] [PublicSite]
│   │       ├── DealsController.cs                     [E7] [Access(Consumer)] + [Access(Business)]: el flujo persona → empresa con PartyPolicy
│   │       └── PersonalWidgetsController.cs           [E7] [Access(Consumer)]: la receta de un módulo B2C
│   ├── Tenancy/                                       [E2] los tests de multitenancy.md §13
│   │   ├── CrossTenantIsolationTests.cs
│   │   ├── RlsBarrierTests.cs
│   │   ├── RlsPolicyInventoryTests.cs
│   │   ├── RuntimeRoleTests.cs
│   │   ├── TenantColumnsImmutabilityTests.cs
│   │   ├── PublicAndSharedRowsTests.cs                datos públicos y compartidos, con RLS; los casos por la página pública se suman en la E7
│   │   ├── AccessTests.cs                             [E3] acceso equivocado, cambio de acceso con membresía, B2C no crea empresas
│   │   ├── SuspensionTests.cs                         [E3–E5] identidad y organización suspendidas (E3); suspender desde la plataforma, B2C intacto y página "no disponible" (E5)
│   │   ├── SubdomainTests.cs                          [E7]
│   │   └── CacheKeyScopeTests.cs
│   ├── ErrorHandling/                                 [E1]
│   │   ├── ErrorHandlingTests.cs
│   │   ├── FrameworkErrorsTests.cs
│   │   └── ValidationProblemTests.cs
│   ├── Localization/                                  [E1]
│   │   └── LocalizationTests.cs                       [E1]
│   ├── Json/                                          [E1]
│   │   ├── UtcDateTimeTests.cs                        [E1]
│   │   ├── DateOnlyTimeOnlyTests.cs                   [E1]
│   │   └── MoneyJsonTests.cs                          [E1] { amount, currency }; moneda inválida → 400
│   ├── Hosting/                                       [E0–E1]
│   │   ├── HealthCheckTests.cs                        [E0]
│   │   ├── SpaHostingTests.cs                         [E1]
│   │   └── SecurityHeadersTests.cs                    [E1]
│   ├── Contracts/                                     [E1]
│   │   ├── ExplicitRouteInventoryTests.cs             [E1] inventario verbo + ruta
│   │   ├── OpenApiTests.cs                            [E1] cada operación de /api declara su 2xx con esquema y sus errores ProblemDetails; Swagger solo en Development
│   │   └── OpenApiContractTests.cs                    [E1] openapi.json al día
│   ├── Persistence/                                   [E2]
│   │   ├── UnitOfWorkTests.cs
│   │   ├── PaginationTests.cs                         orden estable, página fuera de rango, sort no permitido
│   │   ├── CursorPaginationTests.cs                   sin saltos ni repetidos al insertar mientras se pagina
│   │   ├── SearchTests.cs                             "perez" encuentra "Pérez"; % y _ se escapan
│   │   ├── SortIndexTests.cs                          todo campo de un SortMap tiene índice (TenantId, campo, Id)
│   │   ├── AuditingTests.cs
│   │   ├── AuditTrailTests.cs
│   │   ├── SoftDeleteTests.cs
│   │   └── MigrationsTests.cs                         falta una migración → falla
│   ├── Configuration/                                 [E3] la Api no arranca y nombra la clave que falta
│   │   ├── SmtpOptionsValidatorTests.cs
│   │   └── GoogleOptionsTests.cs
│   ├── Auth/                                          [E3]
│   │   ├── SignupTests.cs
│   │   ├── LoginCodeTests.cs
│   │   ├── LoginLinkTests.cs
│   │   ├── ConnectTests.cs
│   │   ├── AuthMethodsTests.cs                        qué medios de ingreso están encendidos según la configuración (GET /api/auth/methods)
│   │   ├── ManagedEmailTests.cs                       un exmiembro no puede ingresar con el correo de la empresa
│   │   └── InvitationsTests.cs
│   ├── Account/                                       [E3]
│   │   ├── MeTests.cs
│   │   ├── LoginMethodsTests.cs                       sumar, verificar, elegir el principal y quitar (con código en otro método); aviso en todos
│   │   └── BusinessSignupTests.cs                     [E6]
│   ├── Organization/                                  [E4]
│   │   ├── RolesTests.cs                              [E4]
│   │   ├── PermissionAuthorizationTests.cs            [E4]
│   │   ├── UsersTests.cs                              [E6]
│   │   ├── CompaniesTests.cs                          [E6]
│   │   ├── CompanyMembersTests.cs                     [E6]
│   │   ├── SettingsTests.cs                           [E6]
│   │   └── AuditTests.cs                              [E6]
│   ├── Platform/                                      [E5]
│   │   ├── PlatformTenantsTests.cs
│   │   ├── PlatformAccountsTests.cs
│   │   ├── PlatformOperatorsTests.cs
│   │   └── PlatformAuditTests.cs
│   └── Modules/WhatsApp/                            [E8] tests de integración del módulo
│       ├── WhatsAppWebhookTests.cs
│       ├── WhatsAppOptionsValidatorTests.cs
│       ├── WhatsAppTemplateCatalogTests.cs            cada plantilla del catálogo tiene su WhatsApp:Templates:<Nombre> en appsettings.json y el orden de sus
│       │                                              variables coincide con el payload que se envía
│       └── WhatsAppInboundProcessorTests.cs
│
└── ArquitecturaBaseMultitenant.ArchitectureTests/     [E0]
    ├── ArquitecturaBaseMultitenant.ArchitectureTests.csproj
    ├── SolutionRoot.cs
    ├── Support/
    │   └── CallSites.cs                               lectura de IL con Mono.Cecil
    ├── HarnessTests.cs                                [E0] punteros por carpeta, enlaces vivos, fichas completas
    ├── LayerDependencyTests.cs                        [E0]
    ├── ProjectReferencesTests.cs                      [E0]
    ├── ApplicationPackagesTests.cs                    [E0] lista blanca de paquetes
    ├── MinimalApiRoutesTests.cs                       [E0] sin Map* de negocio
    ├── ApplicationPublicApiTests.cs                   [E1] sin IQueryable ni Expression
    ├── ErrorCodeTests.cs                              [E1] formato Area.Entidad.Motivo + clave en resx
    ├── ControllerInputContractTests.cs                [E1]
    ├── ControllerServiceRepositoryTests.cs            [E1] los controllers solo inyectan I*Service
    ├── ApplicationServicesTests.cs                    [E1] cada *Service implementa su interfaz
    ├── ServiceDependencyCountTests.cs                 [E1] ≤ 8 dependencias
    ├── TransactionBoundaryTests.cs                    [E2] solo los servicios reciben IUnitOfWork; nadie más guarda
    ├── EntityConfigurationTests.cs                    [E2] una configuración por entidad
    ├── DataClassificationTests.cs                     [E2] toda entidad es ITenantOwned, IPublishedByBusiness o IConsumerBusinessShared, o está en la lista de
    │                                                  plataforma o identidad
    ├── DecimalPrecisionTests.cs                       [E1] ningún decimal sin HasPrecision; ningún double o float en entidades
    ├── NoManualFormattingTests.cs                     [E1] sin ToString("N"), ToString("C") ni formatos de fecha fuera de DisplayFormatter
    ├── TenantScopeUsageTests.cs                       [E2] ITenantScope solo en la lista blanca
    ├── QueryFilterBypassTests.cs                      [E2] IgnoreQueryFilters solo en Readers/Platform (salvo IgnoreQueryFilters(["SoftDelete"]))
    ├── IdentityAccessTests.cs                         [E3] ApplicationUser solo desde Identity/ (con Configurations/Identity), ApplicationDbContext,
    │                                                  UserRepository, MemberReader, Readers/Platform y Seed/
    ├── AccessDeclarationTests.cs                      [E3] toda ruta de negocio declara [Access] o [PublicSite]
    ├── PermissionAuthorizationTests.cs                [E4] permisos, nunca roles ni Policy a mano; cada [HasPermission]
    │                                                  nombra un permiso que existe en su catálogo
    └── ModuleIsolationTests.cs                        [E8] el núcleo no referencia ningún namespace *.Modules.*
```

## Documentación en capas (como ArquitecturaBase, Etapa 5 de su plan)

```
docs/
├── guides/                                            recetas paso a paso, con enlaces a los archivos de Roles
│   ├── agregar-un-area.md                             [E4] B2B o B2C: entidad → configuración → migración con RLS →
│   │                                                  errores → permiso → repositorio y lector → servicio → controller
│   │                                                  con [Access] → tests (aislamiento incluido) → inventario
│   ├── permiso-nuevo.md                               [E4]
│   ├── migracion.md                                   [E2] comando + EnableTenantRls + índices de SortMap
│   ├── prefijo-de-backend.md                          [E1]
│   └── quitar-whatsapp.md                             [E8] borrar Domain/WhatsApp, Modules/WhatsApp en las tres capas y la
│                                                      línea de Program.cs; agregar una migración que borra sus tablas
└── features/<área>.md                                 reglas funcionales de cada área

src/**/<carpeta de un área>/AGENTS.md                  el puntero del arnés (arnes.md §3: qué va, qué no va, fichas, copiá de) más la línea
                                                       "Antes de tocar esto, leé docs/features/<área>.md"; en total, 3 a 8 líneas
src/**/<carpeta de un área>/CLAUDE.md                  @AGENTS.md
```

## Piezas de los estándares P1 a P10 (adoptados el 2026-09-27)

Rutas completas, para no romper las ramas de arriba. Cada una se crea en la etapa marcada, junto con su ficha de `docs/rules/`.

```
src/ArquitecturaBaseMultitenant.Domain/
├── Common/IVersioned.cs                                  [E2] P1  uint Version (xmin)
├── Common/TextLimits.cs                                  [E1] P4  PersonName 100, OrganizationName 120, ShortName 60, Description 500, LongText 4000
├── ValueObjects/Email.cs                                 [E1] P3  normalizado (minúsculas, NFC, IDN), validado
├── ValueObjects/TaxId.cs                                 [E6] P5  país + tipo + dígitos
├── ValueObjects/TaxIdValidators/ArgentineCuitValidator.cs [E6] P5 dígito verificador módulo 11 (CUIT y CUIL)
├── ValueObjects/TaxIdValidators/ArgentineDniValidator.cs  [E6] P5
├── Legal/LegalDocument.cs · LegalDocumentKind.cs · LegalAcceptance.cs · LegalErrors.cs   [E3] P7
└── Features/Features.cs                                  [E5] P8  catálogo de módulos (clave, lado: Consumer, Business o los dos, prendido por defecto)

src/ArquitecturaBaseMultitenant.Application/
├── Common/Exceptions/ConcurrencyConflictException.cs     [E2] P1  la lanza UnitOfWork ante DbUpdateConcurrencyException; ProblemDetailsMapper → 409 General.ConcurrencyConflict
├── Common/Text/TextNormalizer.cs                         [E1] P4  trim, NFC, sin caracteres de control ni de ancho cero
├── Interfaces/Integrations/Features/IFeatureService.cs   [E5] P8
├── Interfaces/Persistence/ILegalRepository.cs · ILegalReader.cs   [E3] P7
├── Services/Legal/LegalService.cs                        [E3] P7  documentos vigentes y aceptación; exportar mis datos en E10 (la baja es AccountDeletionService, E3)
└── Models/Legal/ · Validation/Legal/                     [E3] P7

src/ArquitecturaBaseMultitenant.Infrastructure/
├── Persistence/Configurations/Platform/IdempotencyKeyConfiguration.cs   [E2] P6
├── Persistence/Configurations/Platform/LegalDocumentConfiguration.cs    [E3] P7
├── Persistence/Configurations/Platform/TenantFeatureConfiguration.cs    [E5] P8
├── Persistence/Configurations/Identity/LegalAcceptanceConfiguration.cs  [E3] P7
├── Persistence/Conventions/VersionedConvention.cs        [E2] P1  IVersioned → xmin IsRowVersion
├── Persistence/Conventions/EmailConvention.cs            [E2] P3  conversor de valor de Email
├── Idempotency/IdempotencyStore.cs · IdempotencyCleanupWorker.cs        [E2] P6
└── Features/FeaturesRegistration.cs · TenantFeatureFilter.cs · FeatureService.cs   [E5] P8

src/ArquitecturaBaseMultitenant.Api/
├── Json/NormalizedStringJsonConverter.cs · RawTextAttribute.cs          [E1] P4
├── Idempotency/IdempotentAttribute.cs                                   [E1] P6
├── Idempotency/IdempotencyFilter.cs                                     [E2] P6
├── Features/DisabledFeatureHandler.cs                    [E5] P8  módulo apagado → 404 ProblemDetails
├── Legal/LegalAcceptanceMiddleware.cs                    [E3] P7
├── Controllers/Account/LegalController.cs                [E3] P7  GET /api/legal/current, POST /api/legal/accept
├── Contracts/Common/PhoneInputHttpRequest.cs                            [E1] teléfono
└── Contracts/Common/TaxIdHttpRequest.cs                                 [E6] P5

tests/
├── *.Domain.UnitTests/ValueObjects/EmailTests.cs · TaxIdTests.cs        P3 · P5
├── *.Application.UnitTests/Common/TextNormalizerTests.cs                P4
├── *.Api.IntegrationTests/Persistence/ConcurrencyTests.cs · CollationTests.cs   P1 · P2
├── *.Api.IntegrationTests/Api/IdempotencyTests.cs (E2) · NormalizedInputTests.cs   P6 · P4
├── *.Api.IntegrationTests/Features/FeatureGateTests.cs                  P8
├── *.Api.IntegrationTests/Legal/LegalAcceptanceTests.cs                 P7
└── *.ArchitectureTests/VersionedContractTests.cs · TextLimitsTests.cs · EmailPropertyTests.cs ·
    TaxIdPropertyTests.cs · IdempotentActionsTests.cs · ModuleControllersTests.cs          P1 · P4 · P3 · P5 · P6 · P8
```

## Piezas del modelo de accesos, sitio público e interacción (ADR 0030 a 0032)

```
src/ArquitecturaBaseMultitenant.Domain/
├── Common/IPublishedByBusiness.cs · IConsumerBusinessShared.cs          [E2] clases pública y compartida
├── Common/Party.cs · PartyPolicy.cs                                     [E2] Consumer | Business; qué parte puede hacer qué
├── Tenancy/Slug.cs · ReservedSlugs.cs                                   [E6] slug único de la organización (subdominio)
└── PublicSite/PublicPage.cs · PublicPageStatus.cs · PublicPageErrors.cs [E6] nombre, logo, descripción, contacto; Draft | Published; bloqueo de la plataforma (fecha UTC y motivo)

src/ArquitecturaBaseMultitenant.Application/
├── Interfaces/Integrations/Request/IPublicSiteContext.cs                [E7] organización del subdominio (solo lo público)
├── Services/PublicSite/PublicPageService.cs · DirectoryService.cs       [E6–E7]
└── Services/Organizations/BusinessSignupService.cs                      [E6] "Registrá tu empresa"

src/ArquitecturaBaseMultitenant.Infrastructure/
├── Persistence/Configurations/PublicSite/PublicPageConfiguration.cs     [E6] esquema public_site, EnablePublicRls
├── Persistence/Readers/PublicSite/PublicPageReader.cs · DirectoryReader.cs  [E7] caché con prefijo del sitio público
└── Identity/OpenIddict/SubdomainRedirectUriValidator.cs                 [E7] redirect URI solo para slugs publicados

src/ArquitecturaBaseMultitenant.Api/
├── Tenancy/PublicSiteResolutionMiddleware.cs · PublicSiteAttribute.cs · PublicSiteContext.cs   [E7]
├── Controllers/PublicSite/PublicPageController.cs · DirectoryController.cs   [E7] [PublicSite][AllowAnonymous]
├── Controllers/Organization/PublicPageAdminController.cs               [E6] [Access(Business)] mi página pública
└── Controllers/Account/BusinessSignupController.cs                       [E6] POST /api/auth/business-signup

tests/
├── *.Api.IntegrationTests/Tenancy/AccessTests.cs                        [E3]
├── *.Api.IntegrationTests/Tenancy/SubdomainTests.cs                     [E7]
├── *.Api.IntegrationTests/Tenancy/PublicAndSharedRowsTests.cs           [E2] casos de datos y RLS; los de la página pública se suman en la E7
├── *.Api.IntegrationTests/TestFeatures/Isolation/Poster.cs · Deal.cs    [E2]
└── *.Api.IntegrationTests/TestFeatures/Isolation/PostersController.cs · DealsController.cs  [E7] [PublicSite] y [Access(Consumer)]/[Access(Business)] (plan E7, puntos 1b y 3)
```

## Piezas de los métodos de ingreso (ADR 0033)

La entidad y su configuración EF están en "Tablas que completan los flujos del lienzo". Parte 3b de la Etapa 3.

```
src/ArquitecturaBaseMultitenant.Domain/
└── Authentication/LoginMethodErrors.cs                                  [E3] Auth.LoginMethod.* (valor ya tomado, no queda ningún método propio o activo, falta la reautenticación, no encontrado)

src/ArquitecturaBaseMultitenant.Application/
├── Interfaces/Services/ILoginMethodService.cs                           [E3]
├── Interfaces/Persistence/ILoginMethodRepository.cs · ILoginMethodReader.cs  [E3]
├── Models/Identity/AddLoginMethodRequest.cs · SetPrimaryLoginMethodRequest.cs  [E3] la verificación reusa Models/Profile/VerifyDestinationRequest
├── Models/Identity/RemoveLoginMethodRequest.cs                          [E3] con el ReauthTicket
├── Models/Identity/ReadModels/LoginMethodRow.cs                         [E3] los métodos de la cuenta, enmascarados (no es Models/Auth/LoginMethodsResponse)
├── Validation/Identity/AddLoginMethodRequestValidator.cs                [E3] y los de SetPrimary y Remove, si los necesitan
├── Services/Identity/LoginMethodService.cs                              [E3] lista, suma, verifica (DestinationCodeVerifier, LoginCodePurpose.VerifyDestination),
│                                                                        elige el principal y quita (los dos, con ReauthVerifier); al cambiar el principal copia el valor
│                                                                        a AspNetUsers.Email/PhoneNumber por IUserRepository; registra el SecurityEvent y avisa en todos
│                                                                        los métodos
└── Services/Identity/ManagedLoginMethodRevoker.cs                       [E3] helper sin IUnitOfWork: desactiva los correos administrados y avisa; lo llama quien termina la
                                                                         membresía, dentro de su transacción (E6: UserService, AccountAccessRevoker)

src/ArquitecturaBaseMultitenant.Infrastructure/
├── Persistence/Repositories/LoginMethodRepository.cs                    [E3]
└── Persistence/Readers/LoginMethodReader.cs                             [E3]

src/ArquitecturaBaseMultitenant.Api/
├── Controllers/Account/AccountLoginMethodsController.cs                 [E3] rutas bajo /api/me/login-methods (las fija el plan detallado de la Etapa 3); distinto
│                                                                        de Auth/LoginMethodsController, el GET anónimo de los medios encendidos
└── Contracts/Account/AddLoginMethodHttpRequest.cs · SetPrimaryLoginMethodHttpRequest.cs · RemoveLoginMethodHttpRequest.cs  [E3] la verificación reusa VerifyDestinationHttpRequest

tests/
├── *.Api.IntegrationTests/Account/LoginMethodsTests.cs                  [E3] sumar, verificar, principal, quitar con código en otro método; siempre queda al
│                                                                        menos uno propio o activo; aviso en todos
├── *.Api.IntegrationTests/Auth/ManagedEmailTests.cs                     [E3] un exmiembro no puede ingresar con el correo de la empresa
└── *.Application.UnitTests/Services/Identity/LoginMethodServiceTests.cs  [E3]
```

## Piezas de la baja de una cuenta (ADR 0035)

```
src/ArquitecturaBaseMultitenant.Domain/
└── Legal/AccountDeletionErrors.cs                                       [E3] ReauthRequired, LastAdmin, PlatformOperator, AlreadyPending, Blocked, NotPending

src/ArquitecturaBaseMultitenant.Application/
├── Interfaces/Services/IAccountDeletionService.cs                       [E3]
├── Interfaces/Integrations/Legal/IAccountDeletionParticipant.cs         [E3] CheckAsync, OnRequestedAsync, OnCancelledAsync, ExecuteAsync
├── Interfaces/Integrations/Legal/IRetainedOnConsumerDeletion.cs         [E7] retención legal declarada por un módulo
├── Services/Legal/AccountDeletionService.cs · AccountDeletionPolicy.cs  [E3] pedir, cancelar y ejecutar
├── Services/Identity/ReauthVerifier.cs                                  [E3] ReauthTicket de 5 minutos (baja y cambios de métodos)
└── Services/Legal/Participants/                                         [E3–E10] PersonalSpace, LegalAcceptances, Outbox, Recovery (E5), Memberships (E6), Engagement (E7), Exports (E10)

src/ArquitecturaBaseMultitenant.Infrastructure/
└── Legal/AccountDeletionWorker.cs                                       [E3] cada hora, SKIP LOCKED, una cuenta por transacción; ejecuta IAccountDeletionService (la identidad se anonimiza por IUserRepository)

src/ArquitecturaBaseMultitenant.Api/
├── Controllers/Account/AccountDeletionController.cs                     [E3] POST /api/me/deletion
├── Controllers/Auth/DeletionCancelController.cs                         [E3] POST /api/auth/deletion/cancel
└── Controllers/Platform/PlatformAccountsController.cs                   [E5] + POST /api/platform/accounts/{id}/deletion

tests/
├── *.Api.IntegrationTests/Legal/AccountDeletionTests.cs                 [E3]
└── *.ArchitectureTests/AccountDeletionParticipantsTests.cs              [E3] toda entidad con datos de una identidad tiene participante
```

## Tablas que completan los flujos del lienzo

```
src/ArquitecturaBaseMultitenant.Domain/
├── Authentication/LoginMethod.cs                                        [E3] identity.LoginMethods: Type, Value (único por tipo), IsPrimary, VerifiedAtUtc, ManagedByTenantId?
├── Legal/LegalDocument.cs · LegalDocumentContent.cs                     [E3] versión + una fila de texto por cultura
├── Tenancy/TenantDomain.cs · TenantDomainStatus.cs                      [E5] platform.TenantDomains: Domain (único), TxtToken, Pending | Verified
├── Users/AccountRecoveryRequest.cs · RecoveryRequestStatus.cs           [E5] platform.AccountRecoveryRequests: Pending | Approved | Rejected
└── Legal/DataExport.cs · DataExportStatus.cs                            [E10] platform.DataExports: archivo, vence a las 48 h

src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/Configurations/
├── Identity/LoginMethodConfiguration.cs                                 [E3] índice único (Type, Value)
├── Platform/LegalDocumentContentConfiguration.cs                        [E3] clave (LegalDocumentId, Culture)
├── Platform/TenantDomainConfiguration.cs                                [E5] índice único Domain
├── Platform/AccountRecoveryRequestConfiguration.cs                      [E5]
└── Platform/DataExportConfiguration.cs                                  [E10]
```

## Piezas que completan los flujos del lienzo

Servicios, rutas y tests de las tablas de arriba: Recuperar mi cuenta, dominio verificado, Legales, la ficha de la cuenta en la plataforma, la moderación de la página pública y Exportar mis datos. Recuperaciones, legales y cuentas suman sus permisos de plataforma en `PlatformPermissions.cs`, `Permissions.resx` y `.en.resx`, y el seed.

```
src/ArquitecturaBaseMultitenant.Application/
├── Interfaces/Services/IAccountRecoveryService.cs · ITenantDomainService.cs  [E5]
├── Interfaces/Services/IPlatformLegalService.cs                         [E5]
├── Interfaces/Services/IDataExportService.cs                            [E10]
├── Interfaces/Persistence/IAccountRecoveryRequestRepository.cs · IAccountRecoveryRequestReader.cs  [E5]
├── Interfaces/Persistence/ITenantDomainRepository.cs · ITenantDomainReader.cs  [E5]
├── Interfaces/Persistence/IDataExportRepository.cs                      [E10]
├── Interfaces/Integrations/Dns/IDnsTxtResolver.cs                       [E5] consulta el registro TXT del dominio
├── Services/Identity/AccountRecoveryService.cs                          [E5] el pedido público en tres pasos (método de antes → método nuevo verificado con
│                                                                        código → pedido recibido) y la revisión del operador (aprobar o rechazar con motivo)
├── Services/Organizations/TenantDomainService.cs · TenantDomainVerifier.cs  [E5] pedir el dominio, emitir el TxtToken, verificar y quitar; al quedar Verified,
│                                                                        marca ManagedByTenantId en los LoginMethods de ese dominio
├── Services/Platform/PlatformLegalService.cs                            [E5] lista versiones y publica una nueva (nunca edita una publicada); reusa ILegalRepository
│                                                                        e ILegalReader (E3)
├── Services/Legal/DataExportService.cs                                  [E10]
├── Models/Identity/StartAccountRecoveryRequest.cs · VerifyAccountRecoveryRequest.cs  [E5]
├── Models/Platform/ListAccountRecoveryRequestsRequest.cs · ReviewAccountRecoveryRequest.cs  [E5] la revisión lleva motivo
├── Models/Platform/PublishLegalDocumentRequest.cs · ListLegalDocumentsRequest.cs  [E5] tipo, vigencia y el texto en todas las culturas soportadas
├── Models/Platform/AccountDetailResponse.cs                             [E5] métodos enmascarados, organizaciones y estado (incluida "Baja iniciada por la
│                                                                        plataforma"); el historial sale de ISecurityEventReader
├── Models/Platform/PlatformAccountDeletionRequest.cs                    [E5] con motivo; lo ejecuta IAccountDeletionService (E3) con la plataforma como iniciadora
├── Models/Platform/ReadModels/AccountRecoveryRequestRow.cs · LegalDocumentRow.cs  [E5]
├── Models/PublicSite/UnpublishPublicPageRequest.cs                      [E6] con motivo: la plataforma despublica con PublicPageService y bloquea la publicación
└── Validation/Identity/ · Validation/Platform/ · Validation/PublicSite/  [E5–E6] un validador por request; el de PublishLegalDocumentRequest exige todas las culturas

src/ArquitecturaBaseMultitenant.Infrastructure/
├── Dns/DnsTxtResolver.cs                                                [E5]
├── Persistence/Repositories/AccountRecoveryRequestRepository.cs · TenantDomainRepository.cs  [E5]
├── Persistence/Readers/AccountRecoveryRequestReader.cs · TenantDomainReader.cs  [E5]
├── Persistence/Repositories/DataExportRepository.cs                     [E10]
└── Legal/DataExportWorker.cs                                            [E10] arma el archivo y lo borra a las 48 h

src/ArquitecturaBaseMultitenant.Api/
├── Controllers/Auth/AccountRecoveryController.cs                        [E5] [AllowAnonymous], con límite de pedidos y [Idempotent] en el POST: los tres pasos de /recuperar
├── Controllers/Platform/PlatformRecoveriesController.cs                 [E5] listar, aprobar o rechazar con motivo (/plataforma/recuperaciones)
├── Controllers/Platform/PlatformLegalController.cs                      [E5] listar y publicar una versión nueva (/plataforma/legales)
├── Controllers/Platform/PlatformAccountsController.cs                   [E5] + la ficha de la cuenta (AccountDetailResponse)
├── Controllers/Platform/PlatformTenantsController.cs                    [E5] + la pestaña Dominio verificado; [E6] + despublicar la página pública con motivo
├── Controllers/Organization/TenantDomainsController.cs                  [E6] [Access(Business)]: sumar el dominio, ver el TXT y verificar (/org/configuracion)
├── Controllers/Account/DataExportController.cs                          [E10] POST /api/me/data-export [Idempotent] y la descarga del enlace que vence a las 48 h
└── Contracts/Auth/ · Contracts/Platform/ · Contracts/Organization/ · Contracts/Account/  [E5–E10] un *HttpRequest o *Query por cada request de arriba

tests/
├── *.Api.IntegrationTests/Auth/AccountRecoveryTests.cs                  [E5]
├── *.Api.IntegrationTests/Platform/LegalPublishingTests.cs              [E5]
├── *.Api.IntegrationTests/Organization/TenantDomainsTests.cs            [E6] aislamiento y dominio único en todo el sistema
└── *.Api.IntegrationTests/Account/DataExportTests.cs                    [E10]
```
