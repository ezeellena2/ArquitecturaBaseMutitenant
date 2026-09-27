# Árbol del backend

> Estructura objetivo, archivo por archivo. `[E#]` indica la etapa del [plan](../plans/2026-09-27-plan-de-desarrollo.md) en que nace la carpeta o el archivo; sin marca, hereda la etapa de su carpeta. Los detalles de cada pieza están en [backend.md](backend.md) y [multitenancy.md](multitenancy.md). `bin/`, `obj/` y `wwwroot/` compilado no se listan.

## Raíz

```
ArquitecturaBaseMutitenant/
├── .github/
│   └── workflows/
│       └── ci.yml                                    [E0] build + test (Docker) + verificación de docs/contracts/openapi.json
├── docs/
│   ├── architecture/
│   │   ├── backend.md                                arquitectura canónica
│   │   ├── multitenancy.md                           perfiles, tenants y aislamiento
│   │   ├── arnes.md                                  cómo se guía a quien programa: fichas, punteros, verificación
│   │   └── arbol.md                                  este archivo
│   ├── contracts/
│   │   ├── openapi.json                              [E1] generado por el build; el front genera sus tipos desde acá
│   │   └── format-cases.json                         [E1] casos de formato compartidos: valor + cultura + zona → texto esperado
│   ├── decisions/
│   │   ├── README.md                                 índice de los ADR
│   │   └── NNNN-<titulo>.md                          uno por decisión, al implementarla
│   ├── rules/                                        fichas del arnés: README + 16 temas (guardado, fechas, números…)
│   ├── features/                                     reglas funcionales por área
│   │   ├── identidad.md                              [E3] registro, ingreso, perfiles, invitaciones
│   │   ├── roles.md                                  [E4] cómo copiar el área de referencia
│   │   ├── plataforma.md                             [E5]
│   │   ├── organizaciones.md                         [E6] usuarios, empresas, membresías, filtros y conteos
│   │   ├── personal.md                               [E7]
│   │   ├── whatsapp.md                               [E8]
│   ├── operations/
│   │   ├── configuracion.md                          Google, Gmail y WhatsApp: claves, secretos y pasos en Google y Meta
│   │   ├── secretos.plantilla.json                   forma del archivo de secretos (se copia FUERA del repo)
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

## src/ArquitecturaBaseMultitenant.Domain `[E1]`

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
├── Results/
│   ├── Error.cs                                      record + fábricas Failure/Validation/NotFound/Conflict…
│   ├── ErrorType.cs
│   ├── Result.cs                                     Result y Result<T>, con conversiones implícitas
│   └── ValidationError.cs                            "Validation.Failed" + errores por campo
├── ValueObjects/
│   ├── Money.cs                                      [E1] Amount (decimal) + Currency; suma solo con la misma moneda; Round()
│   ├── CurrencyCode.cs                               [E1] ISO 4217 + decimales de la moneda
│   ├── CultureCode.cs                                [E1] es-AR | en-US (SupportedCultures)
│   ├── Email.cs                                      [E3]
│   └── PhoneNumber.cs                                [E3]
├── Tenancy/                                          [E2–E3]
│   ├── Tenant.cs                                     Kind, Status, Name, Slug?; métodos Activate/Suspend/Close
│   ├── TenantKind.cs                                 Personal | Business
│   ├── TenantStatus.cs                               PendingApproval | Provisioning | Active | Suspended | Closed
│   ├── TenantErrors.cs                               Tenancy.Tenant.*
│   ├── Member.cs                                     UserId, TenantId, Status, IsOwner, JoinedAtUtc
│   ├── MemberStatus.cs                               Invited | Active | Inactive
│   ├── MemberErrors.cs                               Tenancy.Member.*
│   ├── ProfileErrors.cs                              Tenancy.Profile.WrongKind, .NotMember, .Suspended
│   ├── Invitation.cs                                 [E3] TenantId, email/teléfono, TokenHash, ExpiresAtUtc, roles iniciales
│   ├── InvitationChannel.cs                         [E3] valor ("email", …): cada módulo registra el suyo
│   ├── InvitationStatus.cs                           [E3] Pending | Accepted | Revoked | Expired
│   └── InvitationErrors.cs                           [E3]
├── Users/                                            [E3] la identidad global (la entidad EF está en Infrastructure)
│   ├── AccountKind.cs                                User | Platform
│   ├── UserStatus.cs                                 Active | Suspended
│   └── UserErrors.cs                                 Users.User.*
├── Authentication/                                   [E3]
│   ├── LoginCode.cs
│   ├── LoginCodeChannel.cs                          valor ("email", …): los módulos suman canales (IsKnown lo valida)
│   ├── LoginCodePurpose.cs                           Login | Signup | VerifyDestination
│   ├── LoginCodeDestination.cs
│   ├── LoginLink.cs
│   ├── LoginMethod.cs
│   ├── LoginAudit.cs
│   ├── LoginCodeErrors.cs                            Auth.LoginCode.*
│   ├── LoginLinkErrors.cs                            Auth.LoginLink.*
│   ├── SignupErrors.cs                               Auth.Signup.* (cerrado, email tomado…)
│   └── AccountErrors.cs                              Auth.Account.*
├── Authorization/                                    [E4]
│   ├── Permissions.cs                                organización + empresa, con All, OrganizationScoped, CompanyScoped
│   ├── PersonalPermissions.cs                        personal.*, implícitos del dueño del perfil personal
│   ├── PlatformPermissions.cs                        platform.*
│   ├── Role.cs                                       TenantId, Name, Scope, IsSystem, Permissions
│   ├── RoleScope.cs                                  Organization | Company
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
│   ├── PlatformSettings.cs                           [E5] ConsumerSignup, BusinessSignup, MaxOwnedOrganizations
│   ├── ConsumerSignupMode.cs                         [E5] Open | Closed
│   ├── BusinessSignupMode.cs                         [E5] Open | RequiresApproval | Closed
│   └── SettingsErrors.cs
├── Platform/                                         [E5]
│   ├── PlatformRole.cs                               Owner | Support
│   ├── PlatformRoleAssignment.cs
│   └── PlatformErrors.cs                             Platform.*
├── Auditing/                                         [E2–E5]
│   ├── AuditEntry.cs                                 [E2] tenant, append-only
│   ├── AuditAction.cs                                [E2] Created | Updated | Deleted | Restored | Custom
│   ├── AuditActorKind.cs                             [E2] User | PlatformOperator | System
│   ├── SecurityEvent.cs                              [E5] plataforma: TargetTenantId?, Reason
│   └── SecurityEventType.cs                          [E5]
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

## src/ArquitecturaBaseMultitenant.Application `[E1]`

Casos de uso, puertos, modelos, validación y textos. Referencia solo a Domain.

```
ArquitecturaBaseMultitenant.Application/
├── ArquitecturaBaseMultitenant.Application.csproj    NeutralLanguage=es, InternalsVisibleTo tests
├── DependencyInjection.cs                            AddApplication(): registro explícito
├── Common/
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
├── Configuration/
│   ├── Auth/                                         [E3]
│   │   ├── LoginCodeOptions.cs
│   │   ├── LoginLinkOptions.cs
│   │   ├── SignupOptions.cs
│   │   └── InvitationOptions.cs
│   └── Messaging/
│       └── OutboxOptions.cs                         [E3] reintentos y backoff
├── Interfaces/
│   ├── Services/                                     lo que inyectan los controllers
│   │   ├── IAccountService.cs                        [E3] registro B2C
│   │   ├── ILoginCodeService.cs                      [E3]
│   │   ├── ILoginLinkService.cs                      [E3]
│   │   ├── IExternalLoginService.cs                  [E11]
│   │   ├── IConnectService.cs                        [E3] emisión de tokens y cambio de perfil
│   │   ├── IInvitationService.cs                     [E3] vista previa y aceptación
│   │   ├── IProfileService.cs                        [E3] /api/me
│   │   ├── IOrganizationSignupService.cs             [E6] "Crear mi organización" y "Mis organizaciones"
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
│   │   └── IPlatformSettingsService.cs              [E5]
│   ├── Persistence/                                  implementados en Infrastructure/Persistence
│   │   ├── IUnitOfWork.cs                            [E2]
│   │   ├── CommitPolicy.cs                           [E2] OnSuccess | OnAnyResult
│   │   ├── CommitPolicyExtensions.cs                 [E2]
│   │   ├── ITenantScope.cs                           [E2] Enter(tenantId)
│   │   ├── IAuditLog.cs                              [E2] eventos explícitos de auditoría
│   │   ├── ITenantRepository.cs                      [E3]
│   │   ├── ITenantReader.cs                          [E3] también lo usa TenantJobRunner
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
│   │   ├── IPlatformSettingsReader.cs                [E5]
│   │   ├── IPlatformRoleAssignmentRepository.cs      [E5]
│   │   ├── IPlatformReader.cs                        [E5] organizaciones, identidades y operadores (vista de plataforma)
│   │   ├── ISecurityEventRepository.cs               [E5]
│   │   ├── ISecurityEventReader.cs                   [E5]
│   │   ├── ICompanyRepository.cs                     [E6]
│   │   ├── ICompanyReader.cs                         [E6]
│   │   ├── ICompanyMembershipRepository.cs           [E6]
│   │   ├── ICompanyMembershipReader.cs               [E6]
│   │   └── IAuditEntryReader.cs                     [E6]
│   └── Integrations/
│       ├── Request/                                  [E2–E3]
│       │   ├── ICurrentUser.cs                       UserId, AccountKind
│       │   ├── ITenantContext.cs                     TenantId, TenantKind, RequiredTenantId
│       │   ├── IRequestInfo.cs                       IP, user agent
│       │   └── IPublicOrigin.cs                      origen público para armar enlaces
│       ├── Identity/                                 [E3–E4]
│       │   ├── ISignInService.cs                     técnico: sign-in, bloqueos, revocar sesiones (≤12 miembros)
│       │   ├── IUserLookup.cs                        búsqueda global por email o teléfono
│       │   ├── IPermissionService.cs                 [E4] permisos efectivos del perfil + invalidación
│       │   ├── IPlatformPermissionService.cs         [E5]
│       │   ├── ITokenRevoker.cs
│       │   └── IGoogleAvailability.cs                [E3]
│       ├── Security/                                 [E3]
│       │   ├── ISecureTokenGenerator.cs
│       │   ├── ILoginCodeGenerator.cs
│       │   ├── ILoginCodeHasher.cs
│       │   └── IPayloadProtector.cs                  cifrado del payload del outbox
│       ├── Messaging/                                [E3]
│       │   ├── IOutbox.cs                            Enqueue(email o WhatsApp) dentro del límite
│       │   ├── ILoginCodeChannel.cs                 [E3] envía un código por un canal; el núcleo trae "email"
│       │   ├── IInvitationChannel.cs                [E3] envía una invitación; el núcleo trae "email"
│       │   └── IEmailTemplateRenderer.cs
│       ├── Caching/
│       │   └── ITenantStatusCache.cs                 [E3] invalidar al suspender o reactivar
│       ├── Time/
│       │   └── ITimeZoneService.cs                   [E1] IsValid, GetDayRangeUtc, catálogo
│       └── Phones/
│           ├── IPhoneNumberParser.cs                [E3]
│           └── IPhoneLinkObserver.cs                [E3] el núcleo avisa cambios de teléfono; el módulo suelta el contacto
├── Models/                                           *Request (entrada), *Response (salida), ReadModels/*Row (proyección)
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
│   │   ├── LoginMethodsResponse.cs
│   │   ├── ConnectUser.cs                            identidad + perfil elegido, para armar el principal
│   │   ├── ProfileSelectionRequest.cs                tenant pedido en authorize
│   │   └── ReturnUrls.cs
│   ├── Invitations/                                  [E3]
│   │   ├── AcceptInvitationRequest.cs
│   │   └── InvitationPreviewResponse.cs
│   ├── Profile/                                      [E3]
│   │   ├── MeResponse.cs                             cuenta + perfiles + perfil activo + permisos
│   │   ├── ProfileSummary.cs                         id, tipo, nombre y estado (para el selector)
│   │   ├── EffectivePermissions.cs                   organización + por empresa
│   │   ├── UpdateMeRequest.cs
│   │   ├── VerifyDestinationRequest.cs
│   │   ├── CreateOrganizationRequest.cs              [E6]
│   │   └── ReadModels/
│   │       └── MyOrganizationRow.cs                  [E6]
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
│   │   └── ReadModels/
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
│   └── Messaging/                                    [E3]
│       └── EmailMessage.cs
├── Validation/                                       un *RequestValidator (internal sealed) por request que lo necesite
│   ├── Auth/                                         [E3]
│   │   ├── SignupRequestValidator.cs
│   │   ├── VerifySignupRequestValidator.cs
│   │   ├── RequestLoginCodeRequestValidator.cs
│   │   ├── VerifyLoginCodeRequestValidator.cs
│   │   ├── RequestLoginLinkRequestValidator.cs
│   │   └── AcceptInvitationRequestValidator.cs
│   ├── Profile/                                      [E3]
│   │   ├── UpdateMeRequestValidator.cs
│   │   └── CreateOrganizationRequestValidator.cs     [E6]
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
├── Services/                                         internal sealed partial; helpers con sufijo fijo y sin IUnitOfWork
│   ├── Auth/                                         [E3]
│   │   ├── AccountService.cs                         registro B2C (identidad + perfil personal)
│   │   ├── SignupPolicy.cs                           ¿el registro está abierto? ¿el email está libre?
│   │   ├── LoginCodeService.cs
│   │   ├── LoginCodeIssuer.cs
│   │   ├── LoginCodeVerifier.cs
│   │   ├── LoginLinkService.cs
│   │   ├── LoginLinkIssuer.cs
│   │   ├── ConnectService.cs
│   │   ├── ProfileSwitchPolicy.cs                    membresía activa + tenant activo
│   │   ├── UserCultures.cs                           cultura efectiva para mensajes en segundo plano
│   │   └── ExternalLoginService.cs                   [E11]
│   ├── Invitations/                                  [E3]
│   │   ├── InvitationService.cs
│   │   └── InvitationIssuer.cs                       lo usan UserService y TenantAdministrationService
│   ├── Profile/                                      [E3]
│   │   ├── ProfileService.cs
│   │   └── DestinationCodeVerifier.cs
│   ├── Organizations/                                [E6]
│   │   ├── OrganizationSignupService.cs
│   │   ├── BusinessSignupPolicy.cs                   modo de alta y límite por persona
│   │   └── TenantProvisioner.cs                      idempotente: settings, roles de sistema, 1ª empresa, admin
│   ├── Users/                                        [E6]
│   │   ├── UserService.cs
│   │   ├── UserGuard.cs
│   │   ├── LastTenantAdminGuard.cs
│   │   └── AccountAccessRevoker.cs
│   ├── Roles/                                        [E4] ← ÁREA DE REFERENCIA
│   │   ├── RoleService.cs
│   │   ├── RoleGuard.cs
│   ├── Companies/                                    [E6]
│   │   ├── CompanyService.cs
│   │   ├── CompanyGuard.cs
│   │   ├── CompanyMemberService.cs
│   │   └── LastCompanyAdminGuard.cs
│   ├── Settings/
│   │   └── TenantSettingsService.cs                  [E6]
│   ├── Auditing/
│   │   └── AuditLogService.cs                        [E6]
│   ├── Time/
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
│       │   ├── WhatsAppOutboundMessage.cs            jerarquía cerrada: texto, botones, código, invitación
│       │   ├── WhatsAppWebhookBatch.cs
│       │   └── BotButtons.cs
│       ├── Services/
│       │   ├── WhatsAppWebhookService.cs
│       │   ├── WhatsAppInboundService.cs
│       │   ├── WhatsAppContactLinker.cs
│       │   ├── WhatsAppLoginCodeChannel.cs           implementa ILoginCodeChannel ("whatsapp")
│       │   ├── WhatsAppInvitationChannel.cs          implementa IInvitationChannel ("whatsapp")
│       │   ├── WhatsAppPhoneLinkObserver.cs          implementa IPhoneLinkObserver
│       │   └── BotReply.cs
│       └── Resources/
│           ├── WhatsAppTexts.resx / .en.resx         textos del bot y de las plantillas
│           └── WhatsAppTexts.cs
└── Resources/                                        es (neutral) + en; claves en paridad
    ├── Errors.resx                                   [E1] código del error → texto; Title.<ErrorType>
    ├── Errors.en.resx
    ├── Validation.resx                               [E1]
    ├── Validation.en.resx
    ├── Permissions.resx                              [E4] Area.*, Permission.*, PermissionDescription.*, Role.*
    ├── Permissions.en.resx
    ├── Notifications.resx                            [E3] asuntos y cuerpos de correo, textos de WhatsApp y bot
    ├── Notifications.en.resx
    ├── Audit.resx                                    [E6] AuditAction.*, Entity.*
    ├── Audit.en.resx
    ├── ErrorMessages.cs
    ├── ValidationMessages.cs
    ├── PermissionTexts.cs
    ├── NotificationTexts.cs                          con cultura explícita
    └── AuditTexts.cs
```

## src/ArquitecturaBaseMultitenant.Infrastructure `[E2]`

EF Core, Identity, OpenIddict, mensajería, WhatsApp y adaptadores técnicos. Referencia a Application.

```
ArquitecturaBaseMultitenant.Infrastructure/
├── ArquitecturaBaseMultitenant.Infrastructure.csproj  FrameworkReference AspNetCore; Templates como EmbeddedResource
├── DependencyInjection.cs                             AddInfrastructure(cfg, env) → llama a cada *Registration
├── Persistence/
│   ├── ApplicationDbContext.cs                        IdentityUserContext<ApplicationUser,Guid> + IDataProtectionKeyContext
│   ├── PersistenceRegistration.cs                     DbContext (appdb), interceptores, UoW, repositorios, readers
│   ├── UnitOfWork.cs
│   ├── TenantContext.cs                               implementa ITenantContext + ITenantScope
│   ├── UniqueViolations.cs                            nombre de índice → error de negocio
│   ├── DatabaseBootstrapExtensions.cs                 solo Development: crea mt_app, migra y hace el seed (appdb-admin)
│   ├── Configurations/                                una IEntityTypeConfiguration<T> por entidad
│   │   ├── Platform/
│   │   │   ├── TenantConfiguration.cs                 [E3]
│   │   │   ├── OutboxMessageConfiguration.cs          [E3]
│   │   │   ├── PlatformSettingsConfiguration.cs       [E5]
│   │   │   ├── PlatformRoleAssignmentConfiguration.cs [E5]
│   │   │   └── SecurityEventConfiguration.cs        [E5]
│   │   ├── Identity/
│   │   │   ├── ApplicationUserConfiguration.cs        [E3] índices únicos globales de email y teléfono
│   │   │   ├── LoginCodeConfiguration.cs              [E3]
│   │   │   ├── LoginLinkConfiguration.cs              [E3]
│   │   │   └── LoginAuditConfiguration.cs           [E3]
│   │   ├── Tenant/                                    datos privados (RLS)
│   │   │   ├── AuditEntryConfiguration.cs             [E2]
│   │   │   ├── MemberConfiguration.cs                 [E3]
│   │   │   ├── InvitationConfiguration.cs             [E3]
│   │   │   ├── TenantSettingsConfiguration.cs         [E3]
│   │   │   ├── RoleConfiguration.cs                   [E4]
│   │   │   ├── RoleAssignmentConfiguration.cs         [E4]
│   │   │   ├── CompanyConfiguration.cs                [E6]
│   │   │   ├── CompanyMembershipConfiguration.cs      [E6]
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
│   │   └── RuntimeRoleValidator.cs                    aborta si mt_app es privilegiado
│   ├── Extensions/
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
│   │   ├── SecurityEventRepository.cs                 [E5]
│   │   ├── CompanyRepository.cs                       [E6]
│   │   └── CompanyMembershipRepository.cs           [E6]
│   ├── Readers/                                       AsNoTracking + proyección a *Row/*Response
│   │   ├── TenantReader.cs                            [E3]
│   │   ├── MemberReader.cs                            [E3] Members ⋈ AspNetUsers (el único camino a la identidad)
│   │   ├── TenantSettingsReader.cs                    [E3] con caché t:
│   │   ├── RoleReader.cs                              [E4]
│   │   ├── PermissionReader.cs                        [E4]
│   │   ├── PlatformSettingsReader.cs                  [E5] con caché p:
│   │   ├── CompanyReader.cs                           [E6]
│   │   ├── CompanyMembershipReader.cs                 [E6]
│   │   ├── AuditEntryReader.cs                        [E6]
│   │   └── Platform/                                  [E5] ÚNICA lista blanca para IgnoreQueryFilters
│   │       ├── PlatformReader.cs
│   │       └── SecurityEventReader.cs
│   ├── Migrations/                                    una sola carpeta; RLS y grants dentro de cada migración
│   │   ├── <ts>_InitialSchema.cs                      [E2] esquemas, grants a mt_app, funciones de triggers, unaccent,
│   │   │                                              pg_trgm y public.f_unaccent
│   │   ├── <ts>_IdentityAndTenancy.cs                 [E3] usuarios, tenants, miembros, invitaciones, settings, outbox
│   │   ├── <ts>_OpenIddict.cs                         [E3]
│   │   ├── <ts>_Roles.cs                              [E4]
│   │   ├── <ts>_Platform.cs                           [E5]
│   │   ├── <ts>_Companies.cs                          [E6]
│   │   └── ApplicationDbContextModelSnapshot.cs
│   └── Seed/
│       ├── SeedExtensions.cs                          [E3] orden e idempotencia; corre en todos los ambientes, dentro de un
│       │                                              límite y con el advisory lock "seed:" (sin carreras entre réplicas)
│       ├── SeedOptions.cs                             [E3] Seed:PlatformOwner, Seed:Demo
│       ├── OpenIddictSeeder.cs                        [E3] cliente web + scope api
│       ├── PlatformSeeder.cs                          [E5] PlatformSettings + primer dueño
│       └── DevelopmentSeeder.cs                       [E3] Ana (Personal + "Demo"), Beto (Personal)
├── Identity/                                          [E3]
│   ├── ApplicationUser.cs                             AccountKind, Status, Culture, TimeZoneId, DisplayName, LastActiveTenantId
│   ├── IdentityRegistration.cs                        Identity core, cookies /account y /connect, DataProtection
│   ├── SignInService.cs
│   ├── UserLookup.cs                                  búsqueda global (lista blanca)
│   ├── PermissionService.cs                           [E4] HybridCache t:{tenant}:perm:{user}
│   ├── PlatformPermissionService.cs                   [E5]
│   ├── IdentityResultExtensions.cs
│   ├── GoogleAvailability.cs                          [E3] Google se enciende con Authentication:Google:ClientId
│   └── OpenIddict/
│       ├── OpenIddictRegistration.cs                  code + PKCE + refresh, endpoints, validación local
│       ├── AuthServerDefaults.cs                      rutas, scopes, duraciones
│       ├── CertificateLoader.cs
│       ├── WebClientOptions.cs
│       └── TokenRevoker.cs                            revoca por identidad o por perfil
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
│           ├── LoginCode.html
│           ├── SignupCode.html
│           └── Invitation.html
├── Modules/                                         [E8]
│   └── WhatsApp/                                    módulo opcional: se enciende con WhatsApp:PhoneNumberId
│       ├── WhatsAppInfrastructureModule.cs          AddWhatsAppModule(cfg): cliente, workers, health, persistencia
│       ├── WhatsAppOptions.cs
│       ├── WhatsAppOptionsValidator.cs
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
│   ├── CacheKeys.cs                                   t: / u: / p:
│   └── TenantStatusCache.cs                           [E3]
├── Time/
│   └── TimeZoneService.cs                             [E1] TimeZoneInfo con IDs IANA
├── Phones/
│   └── LibPhoneNumberParser.cs                        [E3]
└── BackgroundJobs/                                    [E2]
    ├── BackgroundJobsRegistration.cs
    └── TenantJobRunner.cs                             un scope DI por tenant + Enter
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
│   └── OpenIdPrincipalFactory.cs                      [E3] sub, account_kind, tenant_id, tenant_kind + destinos
├── Authorization/                                     [E4]
│   ├── HasPermissionAttribute.cs
│   ├── HasCompanyPermissionAttribute.cs
│   ├── HasPlatformPermissionAttribute.cs              [E5]
│   ├── PermissionPolicyProvider.cs                    perm: / cperm: / pperm:
│   ├── PermissionRequirement.cs
│   ├── CompanyPermissionRequirement.cs
│   ├── PlatformPermissionRequirement.cs               [E5]
│   ├── PermissionAuthorizationHandler.cs
│   ├── CompanyPermissionAuthorizationHandler.cs       lee {companyId} de la ruta
│   └── PlatformPermissionAuthorizationHandler.cs      [E5]
├── Tenancy/                                           [E3]
│   ├── TenantResolutionMiddleware.cs
│   ├── TenantKindAttribute.cs                         [TenantKind(Business|Personal)]
│   ├── TenantKindFilter.cs                            403 Tenancy.Profile.WrongKind
│   └── TenantClaimTypes.cs
├── RequestContext/                                    [E3]
│   ├── CurrentUser.cs
│   ├── RequestInfo.cs
│   └── PublicOrigin.cs
├── Controllers/
│   ├── Auth/                                          [E3]
│   │   ├── ConnectController.cs                       authorize (con tenant=), token, logout, userinfo
│   │   ├── SignupController.cs                        POST /api/auth/signup, /verify
│   │   ├── LoginCodeController.cs
│   │   ├── LoginLinkController.cs
│   │   ├── LoginMethodsController.cs
│   │   ├── InvitationsController.cs                   GET preview, POST accept
│   │   └── ExternalLoginController.cs                 [E11]
│   ├── Account/
│   │   ├── MeController.cs                            [E3] GET/PUT /api/me
│   │   ├── MyOrganizationsController.cs               [E6] GET/POST /api/me/organizations
│   │   └── TimeZonesController.cs                     [E1] GET /api/time-zones
│   ├── Organization/                                  [TenantKind(Business)]
│   │   ├── RolesController.cs                         [E4] ← referencia
│   │   ├── PermissionsController.cs                   [E4]
│   │   ├── UsersController.cs                         [E6]
│   │   ├── CompaniesController.cs                     [E6]
│   │   ├── CompanyMembersController.cs                [E6] api/companies/{companyId}/members
│   │   ├── SettingsController.cs                      [E6]
│   │   └── AuditController.cs                         [E6]
│   ├── Personal/                                      [E7] [TenantKind(Personal)]: acá van los módulos B2C del producto
│   └── Platform/                                      [E5] account_kind=platform
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
│   │   └── CreateOrganizationHttpRequest.cs           [E6]
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
│   │   └── AuditQuery.cs                              [E6]
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
│   └── RateLimitPolicies.cs                           login-code, login-verify, signup, invitation-accept, whatsapp-webhook, profile-api
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
├── ArquitecturaBaseMultitenant.Domain.UnitTests/      [E1]
│   ├── ArquitecturaBaseMultitenant.Domain.UnitTests.csproj
│   ├── Results/
│   │   ├── ResultTests.cs
│   │   └── ValidationErrorTests.cs
│   ├── ValueObjects/
│   │   ├── MoneyTests.cs                              [E1] redondeo AwayFromZero, monedas distintas no se suman
│   │   ├── EmailTests.cs                              [E3]
│   │   └── PhoneNumberTests.cs                        [E3]
│   ├── Tenancy/
│   │   ├── TenantTests.cs                             [E2] transiciones de estado
│   │   ├── MemberTests.cs                             [E3]
│   │   └── InvitationTests.cs                         [E3]
│   ├── Authorization/
│   │   ├── PermissionsTests.cs                        [E4] catálogos separados y sin duplicados
│   │   └── RoleTests.cs                               [E4]
│   ├── Companies/
│   │   └── CompanyTests.cs                            [E6]
│
├── ArquitecturaBaseMultitenant.Application.UnitTests/ [E1]
│   ├── ArquitecturaBaseMultitenant.Application.UnitTests.csproj
│   ├── Common/
│   │   ├── RequestValidatorTests.cs
│   │   ├── PagedRequestValidatorTests.cs
│   │   └── DisplayFormatterTests.cs                   [E1] recorre docs/contracts/format-cases.json
│   ├── Resources/
│   │   ├── ResourceParityTests.cs                     claves y placeholders es = en
│   │   ├── ErrorMessagesTests.cs
│   │   └── PermissionTextsTests.cs                    [E4]
│   ├── Services/
│   │   ├── Auth/                                      [E3]
│   │   │   ├── AccountServiceTests.cs                 el registro crea identidad + perfil personal
│   │   │   ├── LoginCodeServiceTests.cs
│   │   │   ├── LoginLinkServiceTests.cs
│   │   │   ├── ConnectServiceTests.cs
│   │   │   └── ProfileSwitchPolicyTests.cs
│   │   ├── Invitations/
│   │   │   └── InvitationServiceTests.cs              [E3]
│   │   ├── Profile/
│   │   │   └── ProfileServiceTests.cs                 [E3]
│   │   ├── Organizations/                             [E6]
│   │   │   ├── OrganizationSignupServiceTests.cs
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
│   └── TestDoubles/
│       ├── FakeUnitOfWork.cs                          misma CommitPolicy; cuenta commits y rollbacks
│       ├── FakeTenantContext.cs
│       ├── FakeCurrentUser.cs
│       ├── FakeOutbox.cs
│       ├── FakePermissionService.cs
│       ├── FakeSignInService.cs
│       ├── ServiceFixture.cs                          FakeTimeProvider + FakeLogger + RequestValidator real
│       └── InMemory/                                  un InMemory<X>Repository por puerto que se use en tests
│
├── ArquitecturaBaseMultitenant.Api.IntegrationTests/  [E1]
│   ├── ArquitecturaBaseMultitenant.Api.IntegrationTests.csproj
│   ├── Support/
│   │   ├── ApiFactory.cs                              WebApplicationFactory + Testcontainers postgres:18.x (con mt_app real)
│   │   ├── ApiTestGroup.cs                            colección única, en serie
│   │   ├── TenantFixture.cs                           [E2] Ana, Beto, sus perfiles personales, organizaciones A y B
│   │   ├── AuthFlow.cs                                [E3] login real: código → authorize (tenant=) → token
│   │   ├── TestAuthHandler.cs                         atajo: X-Test-UserId + X-Test-TenantId
│   │   ├── RuntimeRoleConnection.cs                   [E2] consultas crudas como mt_app para probar RLS
│   │   ├── CapturingOutbox.cs                         [E3]
│   │   ├── FailingCommitUnitOfWork.cs                 [E2]
│   │   └── HttpExtensions.cs
│   ├── TestFeatures/                                  solo para tests, nunca en src/
│   │   ├── TestController.cs                          [E1] devuelve cada ErrorType
│   │   ├── TestControllerApplicationPart.cs
│   │   └── Isolation/                                 [E2]
│   │       ├── Widget.cs                              ITenantOwned, IAuditable, ISoftDeletable
│   │       ├── IsolationModelCustomizer.cs
│   │       ├── WidgetsController.cs                   [TenantKind(Business)]
│   │       └── PersonalWidgetsController.cs           [E7] [TenantKind(Personal)]: la receta de un módulo B2C
│   ├── Tenancy/                                       [E2] los tests de multitenancy.md §11
│   │   ├── CrossTenantIsolationTests.cs
│   │   ├── RlsBarrierTests.cs
│   │   ├── RlsPolicyInventoryTests.cs
│   │   ├── RuntimeRoleTests.cs
│   │   ├── TenantColumnsImmutabilityTests.cs
│   │   ├── ProfileSwitchTests.cs                      [E3]
│   │   ├── WrongProfileKindTests.cs                   [E3]
│   │   ├── SuspensionTests.cs                         [E5]
│   │   └── CacheKeyScopeTests.cs
│   ├── ErrorHandling/                                 [E1]
│   │   ├── ErrorHandlingTests.cs
│   │   ├── FrameworkErrorsTests.cs
│   │   └── ValidationProblemTests.cs
│   ├── Localization/
│   │   └── LocalizationTests.cs                       [E1]
│   ├── Json/
│   │   ├── UtcDateTimeTests.cs                        [E1]
│   │   ├── DateOnlyTimeOnlyTests.cs                   [E1]
│   │   └── MoneyJsonTests.cs                          [E1] { amount, currency }; moneda inválida → 400
│   ├── Hosting/                                       [E1]
│   │   ├── HealthCheckTests.cs
│   │   ├── SpaHostingTests.cs
│   │   └── SecurityHeadersTests.cs
│   ├── Contracts/
│   │   ├── ExplicitRouteInventoryTests.cs             [E1] inventario verbo + ruta
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
│   ├── Auth/                                          [E3]
│   │   ├── SignupTests.cs
│   │   ├── LoginCodeTests.cs
│   │   ├── LoginLinkTests.cs
│   │   ├── ConnectTests.cs
│   │   └── InvitationsTests.cs
│   ├── Account/
│   │   ├── MeTests.cs                                 [E3]
│   │   └── MyOrganizationsTests.cs                    [E6]
│   ├── Organization/
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
    ├── DataClassificationTests.cs                     [E2] toda entidad es ITenantOwned o está en la lista de plataforma o identidad
    ├── DecimalPrecisionTests.cs                       [E1] ningún decimal sin HasPrecision; ningún double o float en entidades
    ├── NoManualFormattingTests.cs                     [E1] sin ToString("N"), ToString("C") ni formatos de fecha fuera de DisplayFormatter
    ├── TenantScopeUsageTests.cs                       [E2] ITenantScope solo en la lista blanca
    ├── QueryFilterBypassTests.cs                      [E2] IgnoreQueryFilters solo en Readers/Platform e Identity
    ├── IdentityAccessTests.cs                         [E3] ApplicationUser solo desde Identity/ y MemberReader
    ├── TenantKindDeclarationTests.cs                  [E3] toda ruta de negocio declara [TenantKind] o [AllowAnonymous]
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
│   │                                                  con [TenantKind] → tests (aislamiento incluido) → inventario
│   ├── permiso-nuevo.md                               [E4]
│   ├── migracion.md                                   [E2] comando + EnableTenantRls + índices de SortMap
│   ├── prefijo-de-backend.md                          [E1]
│   └── quitar-whatsapp.md                             [E8] borrar Modules/WhatsApp en las tres capas + la línea de Program.cs
└── features/<área>.md                                 reglas funcionales de cada área

src/**/<carpeta de un área>/AGENTS.md                  una línea: "Antes de tocar esto, leé docs/features/<área>.md"
src/**/<carpeta de un área>/CLAUDE.md                  @AGENTS.md
```
