# Árbol del backend

> Estructura objetivo, archivo por archivo. `[E#]` indica la etapa del [plan](../plans/2026-09-27-plan-de-desarrollo.md) en que nace la carpeta o el archivo; sin marca, hereda la etapa de su carpeta. Cada proyecto nace vacío en la E0 (tarea 0.2) y sus carpetas llevan su propia marca. Los detalles de cada pieza están en [backend.md](backend.md) y [multitenancy.md](multitenancy.md). `bin/`, `obj/` y `wwwroot/` compilado no se listan.

## Raíz

```
ArquitecturaBaseMutitenant/
├── .agents/
│   └── skills/
│       └── arnes/
│           └── SKILL.md                              skill /arnes <nombre>: corre un arnés ejecutable y muestra el resumen (Codex la lee de acá; --instalar la copia a .claude/skills/)
├── .github/
│   └── workflows/
│       └── ci.yml                                    [E0] build + test (Docker); desde la E1 verifica docs/contracts/openapi.json
├── docs/
│   ├── architecture/
│   │   ├── backend.md                                arquitectura canónica
│   │   ├── datos-de-referencia.md                    [E1] ADR 0036: JSON E1, tablas y seed E2
│   │   ├── multitenancy.md                           accesos B2C/B2B, tres clases de datos, subdominios, aislamiento
│   │   ├── arnes.md                                  cómo se guía a quien programa: fichas, punteros, verificación
│   │   ├── estandares.md                             inventario de estándares transversales (definidos, P1 a P10 adoptados, propuestos)
│   │   └── arbol.md                                  este archivo
│   ├── contracts/
│   │   ├── openapi.json                              [E1] generado por el build; el front genera sus tipos desde acá
│   │   └── format-cases.json                         [E1] casos de formato compartidos: valor + cultura + zona → texto esperado
│   ├── decisions/
│   │   ├── README.md                                 índice de los ADR
│   │   └── NNNN-<titulo>.md                          solo las decisiones que no entran en una línea del índice
│   ├── rules/                                        fichas del arnés: README + una ficha por tema (guardado, fechas, números…); índice en rules/README.md
│   ├── features/                                     reglas funcionales por área
│   │   ├── identidad.md                              [E3] registro de personas y de empresas, accesos, invitaciones
│   │   ├── roles.md                                  [E4] cómo copiar el área de referencia
│   │   ├── plataforma.md                             [E5] operadores, organizaciones, cuentas, recuperaciones, legales y auditoría
│   │   ├── organizaciones.md                         [E6] usuarios, empresas, membresías, filtros y conteos
│   │   ├── inicio.md                                 inicio de la organización: tarjetas por área y «Para resolver» (ADR 0040)
│   │   ├── sitio-publico.md                           [E7] publicación por subdominio y directorio
│   │   ├── personal.md                               [E7] receta de módulos B2C e interacción
│   │   ├── asistente.md                              Contrato, accesos y persistencia del asistente
│   │   ├── contrato-http.md                          Contrato HTTP y errores técnicos transversales
│   │   └── whatsapp.md                               [E8]
│   ├── operations/
│   │   ├── configuracion.md                          Google, Gmail y WhatsApp: claves, secretos y pasos en Google y Meta
│   │   ├── secretos.plantilla.json                   forma del archivo de secretos (se copia FUERA del repo)
│   │   ├── whatsapp-plantillas.md                    cuándo y cómo crear cada plantilla de WhatsApp en Meta
│   │   └── runbook.md                                [E10]
│   ├── plans/
│   │   ├── 2026-09-27-plan-de-desarrollo.md          plan maestro
│   │   └── AAAA-MM-DD-etapa-N-<tema>.md              plan detallado de cada etapa
│   ├── guides/                                       recetas: agregar un área, permiso nuevo, migración, prefijo de backend, arneses
│   ├── reviews/                                      informes de cierre de etapa y revisiones, con sus anexos
│   └── backlog.md                                    lo que queda para más adelante
├── scripts/
│   ├── datos-de-referencia/                          [E1] generación desde ISO 4217, ISO 3166, IANA y CLDR
│   │   ├── generar.mjs                               [E1] regenera los cinco JSON de salida; nunca se editan a mano
│   │   ├── generar.test.mjs                          [E1] snapshots/hashes y salida byte a byte, sin red
│   │   ├── package.json                              [E1] parser XML, CLDR y libphonenumber exactos
│   │   ├── package-lock.json                         [E1] dependencias transitivas fijadas
│   │   ├── .node-version                             [E1] Node/ICU fijados para Intl.DisplayNames
│   │   ├── sources.lock.json                         [E1] URL, versión y SHA-256 de SIX, validez CLDR e IANA
│   │   ├── habilitados.json                          [E1] configuración fuente de IsEnabled y SortOrder, no lista en código
│   │   ├── cultures.source.json                      [E1] culturas y patrones editables; cultures.json es salida
│   │   ├── tax-id-types.source.json                  [E1] tipos fiscales editables; tax-id-types.json es salida
│   │   ├── sources/                                  [E1] snapshots versionados; solo --refresh los actualiza
│   │   │   ├── iso4217-list-one.xml                  [E1] SIX ISO 4217 List One
│   │   │   ├── cldr-region-validity.xml              [E1] CLDR 48.2, regiones regulares para ISO 3166-1
│   │   │   └── iana-zone1970.tab                    [E1] IANA tzdb, zonas canónicas y países
│   │   ├── ciudades.es.json                          [E1] excepciones de nombres de ciudad en español
│   │   └── ciudades.en.json                          [E1] excepciones de nombres de ciudad en inglés
│   ├── arneses/                                      arneses ejecutables: detectan (0 tokens), arreglan lo mecánico y llaman a un modelo barato (ADR 0037)
│   │   ├── AGENTS.md
│   │   ├── CLAUDE.md
│   │   ├── README.md                                 qué es un arnés ejecutable, cómo se corre, catálogo, motores y programación
│   │   ├── arnes.mjs                                 CLI único: lista, detecta, repara, llama al modelo, verifica y commitea
│   │   ├── arnes.test.mjs                            tests del CLI con fixtures, sin red ni modelo
│   │   ├── corrida.test.mjs                          corrida completa con ejecutables falsos de claude y codex
│   │   ├── programar.ps1                             alta, baja y consulta de la tarea programada de Windows
│   │   ├── comun/                                    piezas compartidas por todos los arneses
│   │   │   ├── repos.mjs                             ubica back y front; lee ClosedStages de cada uno
│   │   │   ├── archivos.mjs                          índice del árbol de archivos
│   │   │   ├── markdown.mjs                          enlaces, anclas, rutas entre backticks, tablas y marcas de etapa
│   │   │   ├── arbolmd.mjs                           parser de docs/architecture/arbol.md
│   │   │   ├── rutas.mjs                             resuelve rutas de los docs contra el repo (capas, proyectos y repo hermano)
│   │   │   ├── simbolos.mjs                          índice de tipos y miembros: C# por regex, TS por export
│   │   │   ├── similitud.mjs                         distancia de edición y candidatos parecidos
│   │   │   ├── hallazgos.mjs                         forma del hallazgo, id estable, orden y filtro de ignorados
│   │   │   ├── parches.mjs                           aplica parches genéricos con verificación del texto y de los permisos
│   │   │   ├── glob.mjs                              glob mínimo para excluir y permisos
│   │   │   ├── git.mjs                               git y el historial de archivos borrados
│   │   │   ├── worktree.mjs                          crea y quita el worktree y la rama de la corrida
│   │   │   ├── proceso.mjs                           ejecuta procesos y resuelve los shims de npm en Windows
│   │   │   ├── guardia.mjs                           revierte lo tocado fuera de permisos y hace fallar la corrida
│   │   │   ├── resultado.mjs                         extrae y valida la respuesta final del modelo
│   │   │   ├── corrida.mjs                           la corrida completa de un repo, paso a paso
│   │   │   ├── informe.mjs                           escribe .local/arneses/<arnés>/<fecha>.json y .md
│   │   │   ├── comun.test.mjs
│   │   │   ├── motores.test.mjs                      worktree, guardia, motores y shims, con ejecutables falsos
│   │   │   ├── motores/                              misma interfaz para los dos motores
│   │   │   │   ├── claude.mjs                        claude -p con solo Read, Edit, Write, Glob y Grep
│   │   │   │   └── codex.mjs                         codex exec con las instrucciones por stdin
│   │   │   └── fakes/                                ejecutables falsos de claude y codex para los tests
│   │   │       ├── claude-falso.mjs
│   │   │       ├── codex-falso.mjs
│   │   │       └── escenario.mjs
│   │   ├── realinear-docs/                           el primer arnés: alinea docs y punteros con el código
│   │   │   ├── arnes.json                            manifiesto: repos, detectores, permisos, modelo y verificación
│   │   │   ├── ignorados.json                        hallazgos descartados con motivo; se revisan en el diff
│   │   │   ├── instrucciones.md                      instrucciones del modelo, iguales para los dos motores
│   │   │   ├── resultado.schema.json                 forma de la respuesta final del modelo
│   │   │   ├── realinear-docs.test.mjs               un test por detector, sobre fixtures
│   │   │   ├── detectores/                           solo leen; nunca escriben ni llaman al modelo
│   │   │   │   ├── referencias.mjs                   enlaces, anclas y rutas entre backticks que no existen
│   │   │   │   ├── simbolos.mjs                      tipos y miembros nombrados en los docs que ya no existen
│   │   │   │   ├── arbol.mjs                         arbol.md contra el árbol real
│   │   │   │   ├── punteros.mjs                      AGENTS.md y CLAUDE.md contra el mapa de arnes.md §3
│   │   │   │   ├── indices.mjs                       índices de fichas, decisiones y la tabla del AGENTS.md raíz
│   │   │   │   └── catalogos.mjs                     permisos, códigos de error y rutas HTTP de docs/features
│   │   │   └── fixtures/                             mini repos falsos (back y front) con cada tipo de desvío
│   │   └── _plantilla/                               se copia para crear un arnés nuevo; el CLI ignora las carpetas con _
│   └── secretos/
│       ├── importar-desde-arquitecturabase.ps1       copia los 5 secretos de ArquitecturaBase sin mostrarlos
│       ├── cargar-desde-archivo.ps1                  carga un JSON guardado fuera del repo
│       └── verificar.ps1                             qué falta, sin mostrar valores
├── src/                                              ver abajo
├── tests/                                            ver abajo
├── tools/                                            solo el soporte del E2E real, nunca para operar (ADR 0017, enmienda)
│   └── ArquitecturaBaseMultitenant.RealE2ESetup/      prepara la cuenta, el espacio personal y la empresa del E2E real en su base aislada
│       ├── ArquitecturaBaseMultitenant.RealE2ESetup.csproj Proyecto de soporte del E2E real aislado
│       ├── Program.cs                                 Preparador aislado y despacho de órdenes del runner
│       ├── PlatformCommand.cs                         Estados iniciales de Plataforma para el E2E aislado
│       ├── PublicSiteSetup.cs                         Publicaciones activa y suspendida propias del recorrido público E7
│       ├── LegalVersionCommand.cs                     Publica términos de prueba por orden del runner
│       ├── LegacyBrowserSessionCommand.cs             Reproduce cookies anteriores a ADR 0044 en aislamiento
│       ├── LoginCodeCommand.cs                        Vence un desafío Login real exclusivamente en la base E2E
│       ├── InvitationCommand.cs                       Prepara escenarios de invitación por orden del runner
│       └── AccountEmailCaptures.cs                    Capturas HTML de correos reales en español e inglés
├── .editorconfig                                     [E0] estilo y supresiones justificadas
├── .gitignore                                        [E0] VisualStudio + .local/ + *.env + appsettings.*.local.json
├── .gitattributes                                    [E1] snapshots sin conversión de bytes y LF fijo para JSON generados
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
│   ├── NotAuditedAttribute.cs                        [E2] excluye la propiedad del rastro de auditoría
│   ├── IConsumerBusinessShared.cs                    Copia de datos visible solo a una persona y una…
│   ├── IPublishedByBusiness.cs                       Dato público perteneciente a la página de una organización
│   ├── IVersioned.cs                                 Versión de concurrencia que Infrastructure mapea a xmin
│   ├── Party.cs                                      Identifica a la persona o a la empresa como parte…
│   ├── PartyPolicy.cs                                Result por parte Consumer o Business; tercero404 y parte equivocada403
│   ├── PartyErrors.cs                                Tenancy.Party.NotAllowed
│   ├── IGlobalSecurityAudited.cs                     Un dato global auditable exige evidencia de seguridad dentro de…
│   └── TextLimits.cs                                 Centraliza los límites de texto que comparten las reglas de…
├── Results/                                          [E1]
│   ├── Error.cs                                      record + fábricas Failure/Validation/NotFound/Conflict…
│   ├── ErrorType.cs
│   ├── Result.cs                                     Result y Result<T>, con conversiones implícitas
│   ├── ContractErrors.cs                             Estado técnico temporal de una ruta publicada antes de implementar…
│   └── ValidationError.cs                            "Validation.Failed" + errores por campo
├── ValueObjects/                                     [E1]
│   ├── Money.cs                                      [E1] Amount (decimal) + Currency; suma solo con la misma moneda; Round(minorUnits)
│   ├── CurrencyCode.cs                               [E1] solo sintaxis alfa-3; ICurrencyCatalog de Application valida existencia y habilitación
│   ├── CultureCode.cs                                [E1] sintaxis idioma-región; ICultureCatalog valida habilitación
│   ├── CurrencyCodeErrors.cs                         [E1] ReferenceData.Currency.Invalid; texto localizado en E1 tarea 10
│   ├── CultureCodeErrors.cs                          [E1] ReferenceData.Culture.Invalid; texto localizado en E1 tarea 10
│   ├── Email.cs                                      [E1] normalizado (minúsculas, NFC, IDN), validado
│   ├── PhoneNumber.cs                                [E1] E.164; PhoneUsage se suma en E3
│   ├── TaxId.cs                                      Identificación fiscal normalizada como país, tipo de catálogo y número…
│   ├── TaxIdErrors.cs                                TaxIds.TaxId.Invalid
│   └── TaxIdValidators/
│       └── ArgentineCuitValidator.cs                 Comprueba formato, prefijo admitido y dígito verificador CUIT/CUIL de Argentina
├── ReferenceData/                                    [E2] entidades globales de platform, sin RLS
│   ├── Currency.cs
│   ├── CurrencyTranslation.cs
│   ├── Country.cs
│   ├── CountryTranslation.cs
│   ├── ReferenceTimeZone.cs
│   ├── TimeZoneCountry.cs
│   ├── TimeZoneTranslation.cs
│   ├── Culture.cs
│   ├── CultureTranslation.cs
│   ├── TaxIdType.cs
│   └── TaxIdTypeTranslation.cs
├── Tenancy/                                          [E2–E3]
│   ├── Tenant.cs                                     Kind, Status, Name, Slug?; métodos Activate/Suspend/Close
│   ├── TenantKind.cs                                 Personal | Business
│   ├── TenantStatus.cs                               PendingApproval | Provisioning | Active | Suspended | Closed
│   ├── TenantErrors.cs                               Tenancy.Tenant.* (Suspended, PendingApproval y Closed → 403; los decide TenantResolutionMiddleware)
│   ├── Member.cs                                     UserId, TenantId, Status, JoinedAtUtc; sin IsOwner: el Dueño sale solo del rol TenantAdmin
│   ├── MemberStatus.cs                               Invited | Active | Inactive | Removed
│   ├── MemberErrors.cs                               Tenancy.Member.* (Inactive: por la puerta de empresas, su única membresía está deshabilitada)
│   ├── AccessErrors.cs                               Tenancy.Access.Wrong, .NotMember, .ConsumerCannotCreateBusiness
│   ├── TenantSlug.cs                                 Dirección de subdominio normalizada según el patrón de Registro-Empresa
│   ├── TenantRegistrationSource.cs                   Origen del alta global, independiente de quién usa la organización
│   ├── ReservedTenantSlug.cs                         Nombres de subdominio que la plataforma no entrega a organizaciones
│   ├── OrganizationEmailDomain.cs                    Nombre DNS normalizado para identificar los correos de una organización
│   ├── MemberRemovalReason.cs                        Identifica por qué se retiró una membresía
│   ├── Company.cs                                    Name, TaxId?, TimeZoneId?, Status
│   ├── CompanyErrors.cs                              Companies.Company.*
│   ├── CompanyMembership.cs                          UserId, CompanyId, Status
│   ├── CompanyMembershipErrors.cs                    Companies.Membership.* (último CompanyAdmin…)
│   ├── CompanyMembershipStatus.cs
│   ├── CompanyStatus.cs
│   ├── TenantDomain.cs                               Reserva un dominio de correo global y conserva su desafío…
│   ├── TenantDomainErrors.cs                         Settings.TenantDomain.* (dominio inválido, repetido o inexistente)
│   ├── TenantDomainStatus.cs                         Pending | Verified | Removed
│   └── TenantSlugErrors.cs                           Tenancy.TenantSlug.Invalid, .Reserved y .Taken
├── Users/                                            [E1–E3] la identidad global (la entidad EF está en Infrastructure)
│   ├── Access.cs                                     [E3] Consumer | Business | Platform (el acceso; un usuario no-plataforma puede tener los dos primeros)
│   ├── UserStatus.cs                                 [E3] Active | Suspended | PendingDeletion | Deleted
│   ├── EmailErrors.cs                                [E1] Users.Email.Invalid (lo devuelve Email.Create)
│   ├── PhoneErrors.cs                                [E1] Users.Phone.Invalid; .NotMobile en E3, con PhoneUsage (el país que no acepta WhatsApp es un error del módulo)
│   ├── AccountLifecycle.cs                           Reglas de estado y fechas de la cuenta global, independientes…
│   └── UserErrors.cs                                 [E3] Users.User.*
├── Authentication/                                   [E3]
│   ├── LoginCode.cs
│   ├── LoginCodeChannel.cs                          valor ("email", …): los módulos suman canales (IsKnown lo valida)
│   ├── LoginCodePurpose.cs                           Login | Signup | VerifyDestination | BusinessSignup | AccountRecovery
│   ├── LoginCodeDestination.cs
│   ├── LoginLink.cs                                 [E8]
│   ├── LoginMethod.cs                                entidad de identity.LoginMethods (ADR 0033, parte 3a): Type, Value único por tipo, IsPrimary, VerifiedAtUtc, ManagedByTenantId?
│   ├── LoginMethodType.cs                            Email | Phone | Google; Phone requiere un canal disponible registrado por el módulo
│   ├── LoginAudit.cs
│   ├── LoginAuditMethod.cs                           Code | Google | WhatsAppCode | WhatsAppLink: con qué se entró; lo usa LoginAudit (en ArquitecturaBase se llama LoginMethod)
│   ├── LoginCodeErrors.cs                            Auth.LoginCode.*
│   ├── LoginChannelErrors.cs                         [E8] canal de ingreso registrado no disponible
│   ├── LoginLinkErrors.cs                            [E8] Auth.LoginLink.*
│   ├── SignupErrors.cs                               Auth.Signup.* (cerrado, email tomado…)
│   ├── GoogleMethodErrors.cs                        Define el conflicto que impide vincular un identificador de Google…
│   ├── ExternalLoginErrors.cs                       Centraliza los rechazos del ingreso con Google
│   ├── AccountErrors.cs                              Identity.Account.* (LockedOut, Suspended, PendingDeletion con la fecha y el cancelTicket)
│   ├── BusinessSignupErrors.cs                       alta de empresa: Tenancy.Organization.LimitReached y Auth.BusinessSignup.*
│   ├── LoginMethodChange.cs                         Cambio de método que determina el texto del aviso de…
│   └── LoginMethodErrors.cs                          Centraliza los errores al agregar, verificar, quitar o elegir un…
├── Authorization/                                    [E4]
│   ├── Permissions.cs                                organización + empresa, sin prefijo (users.read, roles.manage…, company.members.manage), con All,
│   │                                                 OrganizationScoped, CompanyScoped
│   ├── PersonalPermissions.cs                        personal.*, implícitos de la persona en su espacio personal
│   ├── PlatformPermissions.cs                        platform.tenants.read/.manage, .accounts.read/.manage, .recoveries.manage, .legal.manage,
│   │                                                 .operators.manage, .audit.read, .settings.manage; platform.whatsapp.manage recién en la E11
│   ├── Role.cs                                       TenantId, Name, Scope, CompanyId? (SpecificCompany), IsSystem, Permissions
│   ├── RoleScope.cs                                  Organization | AnyCompany | SpecificCompany; en pantalla, toda la organización o una
│   │                                                 empresa; AnyCompany queda para CompanyAdmin y la API (ADR 0034, enmienda)
│   ├── RoleAssignment.cs                             UserId, RoleId, CompanyId?
│   ├── SystemRoles.cs                                TenantAdmin (Dueño) y CompanyAdmin (Administrador de empresa, AnyCompany)
│   ├── RoleErrors.cs                                 Roles.Role.*
│   └── RoleAssignmentErrors.cs                       Roles.RoleAssignment.*: dónde vale un rol, un solo código para Usuarios y Empresas
├── Settings/                                         [E3]
│   ├── TenantSettings.cs                             DefaultCulture, DefaultTimeZoneId, DefaultCurrency
│   ├── PlatformSettings.cs                           [E3] ConsumerSignup, BusinessSignup, MaxOwnedOrganizations, AccountDeletionGraceDays (30); la E5
│   │                                                 suma el servicio, el controller y la pantalla para editarlos
│   ├── ConsumerSignupMode.cs                         [E3] Open | Closed
│   ├── BusinessSignupMode.cs                         [E3] Open | RequiresApproval | Closed
│   └── SettingsErrors.cs
├── Platform/                                         [E5]
│   ├── PlatformPermissionAssignment.cs               permisos individuales por operador, sin roles
│   ├── PlatformOperatorErrors.cs                     Platform.Operator.*
│   ├── AccountRecoveryErrors.cs                      El pedido no tiene un código verificado con su nonce,…
│   ├── AccountRecoveryRequest.cs                     Pedido público de «Recuperar mi cuenta» (multitenancy.md §3.1)
│   ├── AccountRecoveryStatus.cs                      Estado de un pedido de «Recuperar mi cuenta»
│   └── PlatformAuditErrors.cs                        Platform.Audit.NotFound
├── Auditing/                                         [E2–E3]
│   ├── AuditEntry.cs                                 [E2] tenant, append-only
│   ├── AuditAction.cs                                [E2] Created | Updated | Deleted | Restored | Custom
│   ├── AuditActorKind.cs                             [E2] User | PlatformOperator | System
│   ├── SecurityEvent.cs                              [E3] esquema platform, append-only: Type, ActorId, TargetTenantId?, Reason? (cuenta: métodos de ingreso y baja; plataforma: acciones de operadores, siempre con TargetTenantId y motivo)
│   ├── SecurityEventChanges.cs                       Diff de valores técnicos
│   ├── AuditChangeKeys.cs                            Claves y valores del resumen de cambios de un AuditEntry…
│   └── SecurityEventType.cs                          [E3] los de la cuenta; la E5 suma los de los operadores
├── Messaging/                                        [E3]
│   ├── OutboxMessage.cs                              Channel, payload cifrado, Attempts, NextAttemptAtUtc
│   ├── OutboxChannel.cs                             valor ("email", …): cada módulo suma el suyo
│   └── OutboxStatus.cs                               Pending | Sent | Failed
├── Features/
│   ├── FeatureDefinition.cs                          Describe un módulo del producto
│   ├── FeatureScope.cs                               Accesos en los que existe un módulo opcional
│   ├── Features.cs                                   Catálogo de módulos reales del producto
│   ├── TenantFeature.cs                              Habilitación global de un módulo para un espacio, con vencimiento…
│   ├── TenantFeatureErrors.cs                        Errores de catálogo y habilitación global de módulos opcionales
│   └── TenantFeatureSource.cs                        Origen de la habilitación de un módulo por Plataforma
├── Invitations/
│   ├── Invitation.cs                                 [E3] TenantId, email/teléfono, TokenHash, ExpiresAtUtc, roles iniciales
│   ├── InvitationChannel.cs                          [E3] valor ("email", …): cada módulo registra el suyo
│   ├── InvitationErrors.cs                           [E3]
│   └── InvitationStatus.cs                           [E3] Pending | Accepted | Revoked | Expired
├── Legal/
│   ├── AccountDeletionCancelTicket.cs                Prueba de ingreso para cancelar baja: cuenta, retorno y un uso en cinco minutos
│   ├── ConsumerDataRetention.cs                      Ledger técnico de copia retenida: clave de tabla, fila/partes, motivo, vencimiento y finalización
│   ├── AccountDeletionErrors.cs                      Reúne los motivos que impiden pedir o cancelar una baja…
│   ├── LegalAcceptance.cs                            Prueba qué versión legal aceptó una cuenta y cuándo
│   ├── LegalDocument.cs                              Identifica una versión de términos o privacidad y desde cuándo…
│   ├── LegalDocumentContent.cs                       Asocia el texto de una versión legal con la cultura…
│   ├── LegalDocumentKind.cs                          Distingue términos y condiciones de política de privacidad
│   ├── LegalErrors.cs                                Reúne los rechazos por documentos legales pendientes, inexistentes o cuya…
│   └── LegalPublicationErrors.cs                     Errores de Plataforma › Documentos legales al publicar una versión…
├── PublicSite/
│   ├── PublicPage.cs                                 Contenido editable de una página pública empresarial y su estado…
│   └── PublicPageErrors.cs                           PublicSite.PublicPage.* (validación, bloqueo de moderación, inexistente)
└── WhatsApp/                                         [E8]
    ├── WhatsAppContact.cs                            wa_id ↔ identidad (esquema identity)
    ├── WhatsAppMessage.cs
    ├── WhatsAppMessageDirection.cs
    ├── WhatsAppMessageKind.cs
    ├── WhatsAppMessageStatus.cs
    ├── WhatsAppCodeQuota.cs                          Ledger técnico por número de plataforma/día UTC, sin destino ni usuario
    ├── WhatsAppCodeErrors.cs                         errores de cuota global de códigos
    ├── WhatsAppErrors.cs                             errores de firma y payload del webhook; la política de países la implementa WhatsAppChannelSettings
    └── WhatsAppChannel.cs                            [E11] PhoneNumberId → TenantId
```

## src/ArquitecturaBaseMultitenant.Application `[E0]`

Casos de uso, puertos, modelos, validación y textos. Referencia solo a Domain.

```
ArquitecturaBaseMultitenant.Application/
├── ArquitecturaBaseMultitenant.Application.csproj    NeutralLanguage=es, InternalsVisibleTo tests
├── DependencyInjection.cs                            [E1] AddApplication(): llama a Services/<Área>/<Área>Registration.cs, con las opciones de cada área
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
│   │   ├── ValidationRules.cs                        [E1] Required, MaxLength, ValidEmail y ValidCurrency/Country/TimeZone/Culture/TaxIdType
│   │   │                                             contra catálogos; ValidPermissions en [E4]
│   │   ├── FieldErrors.cs
│   │   ├── PagedRequestValidator.cs                  página, tamaño permitido, sort en SortableFields, búsqueda ≤ 100
│   │   └── CursorRequestValidator.cs                 cursor válido, límite ≤ 100
│   ├── Logging/
│   │   └── OperationLog.cs                           LogHandling/LogHandled/LogFailed con [LoggerMessage]
│   ├── Text/
│   │   ├── TextNormalizer.cs                         [E1] trim, NFC, sin caracteres de control ni de ancho cero
│   │   └── PersonalDataMask.cs                       [E5] el único enmascarado: correo «lu•••@dominio», teléfono «•••• 4521»
│   ├── Formatting/                                   [E1] presentación unificada del lado del back (backend.md §18)
│   │   ├── DisplayFormatter.cs                       [E1] todos los tipos de formatos.md, con cultura y zona explícitas
│   │   ├── CultureProfiles.cs                        [E1] adapta patrones de ICultureCatalog (JSON E1, tabla E2), sin lista fija
│   │   ├── DisplayFormatContext.cs                   Perfil resuelto una vez para componer varios textos de una…
│   │   ├── VersionWireFormatter.cs                    Versiones invariantes para URLs y validadores HTTP
│   │   └── SupportedCultures.cs                      [E1] lee ICultureCatalog; es-AR y en-US habilitadas inicialmente
│   └── Exceptions/                                    [E2]
│       ├── UniqueConstraintViolationException.cs     [E2] con UnitOfWork
│       ├── ForeignKeyViolationException.cs           [E6] con UnitOfWork
│       ├── InvalidCursorException.cs                 [E2] Api la traduce sin depender de Infrastructure
│       └── ConcurrencyConflictException.cs           Una edición simultánea cambió la versión de la fila antes…
├── Configuration/                                    [E3]
│   ├── Auth/                                         [E3]
│   │   ├── LoginCodeOptions.cs
│   │   ├── LoginLinkOptions.cs                       [E8]
│   │   └── BrowserSessionKeys.cs                     Centraliza la clave de sesión del navegador que vincula autenticación…
│   ├── Messaging/
│   │   └── OutboxOptions.cs                          [E3] reintentos y backoff
│   └── Invitations/
│       └── InvitationOptions.cs
├── Interfaces/                                       [E1]
│   ├── Services/                                     lo que inyectan los controllers
│   │   ├── IAccountService.cs                        [E3] registro B2C
│   │   ├── ILoginCodeService.cs                      [E3]
│   │   ├── ILoginLinkService.cs                      [E8] enlace de un solo uso del bot
│   │   ├── ILoginLinkReplayService.cs                [E8] revalida resultado cacheado sin cookie ni ticket nuevo
│   │   ├── IExternalLoginService.cs                  [E3]
│   │   ├── IConnectService.cs                        [E3] emisión de tokens y cambio de acceso u organización
│   │   ├── IConnectLogoutService.cs                  [E3] cierre de la sesión Identity y revocación OpenIddict
│   │   ├── IInvitationService.cs                     [E3] vista previa y aceptación
│   │   ├── IProfileService.cs                        [E3] /api/me
│   │   ├── IBusinessSignupService.cs                  [E6] "Registrá tu empresa" (alta B2B, aparte del acceso B2C)
│   │   ├── IRoleService.cs                           [E4]
│   │   ├── IPublicSiteService.cs                     [E7] página publicada del sitio resuelto
│   │   ├── IDirectoryService.cs                      [E7] directorio paginado y logo público por slug
│   │   ├── ICompanyService.cs                        [E6]
│   │   ├── ITenantAdministrationService.cs           [E5]
│   │   ├── IPlatformAccountService.cs                [E5]
│   │   ├── IPlatformOperatorService.cs               [E5]
│   │   ├── IPlatformAuditService.cs                  [E5]
│   │   ├── IReferenceDataService.cs                  [E1] cinco catálogos traducidos, GET /api/reference-data
│   │   ├── IOutboxDispatchService.cs                 [E3] límite de envío/reintento del outbox
│   │   ├── IPlatformScopeAccessService.cs            Autoriza y confirma el evento previo a entrar al alcance…
│   │   ├── IPlatformRecoveryService.cs               Bandeja Recuperaciones de Plataforma
│   │   ├── IPlatformFeatureService.cs                Administra habilitaciones globales de organizaciones, sin acceder a filas privadas
│   │   ├── IPendingItemSource.cs                     Puerto de «Para resolver» (ADR 0040)
│   │   ├── ILoginMethodsService.cs                   Informa qué canales de ingreso están disponibles en esta instalación
│   │   ├── ILoginMethodManagementService.cs          Gestiona correos de ingreso de la cuenta
│   │   ├── ILegalService.cs                          Entrega el contenido público de la versión legal vigente en…
│   │   ├── ILegalAcceptanceService.cs                Consulta documentos pendientes y registra su aceptación para que una…
│   │   ├── ILastTenantAdminGuard.cs                  La regla de la única Dueña (ADR 0035)
│   │   ├── IInvitationExpiryService.cs               El vencimiento de las invitaciones en la auditoría de la…
│   │   ├── IHomeService.cs                           Inicio de la organización activa
│   │   ├── IConnectAuthorizationService.cs           Persiste la autorización OpenID Connect para una cuenta y acceso…
│   │   ├── IAccountRecoveryNoticeService.cs          El aviso a la cuenta encontrada por un pedido de…
│   │   ├── IAccountLoginMethodsService.cs            Entrega los métodos de ingreso visibles de la cuenta actual…
│   │   ├── IAccountGoogleService.cs                  Vincula una identidad Google verificada con la cuenta que ya…
│   │   ├── IAccountDeletionProcessingService.cs      Coordina la baja diferida
│   │   ├── IPlatformSettingsService.cs               [E5]
│   │   ├── IAccountDeletionService.cs                Permite a la cuenta solicitar o cancelar su baja según…
│   │   ├── IAccountRecoveryService.cs                «Recuperar mi cuenta», sin sesión
│   │   ├── ICompanyMembershipService.cs              miembros de una empresa y sus roles
│   │   ├── IFeatureService.cs                        Habilitaciones efectivas del espacio activo para pantallas, workers y mensajes
│   │   ├── ILastCompanyAdminGuard.cs                 protege al último Administrador activo de una empresa
│   │   ├── IOrganizationAuditService.cs              auditoría de la organización: listado, «Quién» y detalle
│   │   ├── IOrganizationDomainService.cs             dominios de correo de la organización: registrar, verificar y retirar
│   │   ├── IOrganizationSettingsService.cs           configuración de la organización: nombre, cultura, zona y moneda
│   │   ├── IOrganizationUserService.cs               Usuarios: miembros e invitaciones de la organización
│   │   ├── IPermissionService.cs                     [E4] permisos efectivos en la organización + invalidación
│   │   ├── IPlatformAccountDeletionService.cs        baja de una cuenta iniciada por Plataforma (ADR 0035)
│   │   ├── IPlatformAccountStateService.cs           suspender, reactivar y cerrar las sesiones de una cuenta
│   │   ├── IPlatformDomainService.cs                 Plataforma verifica o retira el dominio de una organización
│   │   ├── IPlatformLegalService.cs                  Plataforma › Documentos legales
│   │   ├── IPlatformPermissionService.cs             [E4]
│   │   ├── IPlatformPublicSiteService.cs             moderación de la página pública desde Plataforma
│   │   ├── IPublicPageService.cs                     página pública de la organización: borrador, publicar y retirar
│   │   └── ITenantSlugAvailabilityService.cs         disponibilidad de una dirección pública antes del alta
│   ├── ReferenceData/                                [E1] puertos de Application; Domain no los referencia
│   │   ├── ICurrencyCatalog.cs                        código, IsEnabled y MinorUnits desde JSON E1 / base E2
│   │   ├── ICountryCatalog.cs
│   │   ├── ITimeZoneCatalog.cs
│   │   ├── ICultureCatalog.cs                         patrones de formato y culturas habilitadas
│   │   └── ITaxIdTypeCatalog.cs
│   ├── Persistence/                                  implementados en Infrastructure/Persistence
│   │   ├── IUnitOfWork.cs                            [E2]
│   │   ├── IOutboxDispatchStore.cs                   [E3/E8] Claim/Complete dentro de UoW; lease durable, sin transporte
│   │   ├── CommitPolicy.cs                           [E2] OnSuccess | OnAnyResult
│   │   ├── CommitPolicyExtensions.cs                 [E2]
│   │   ├── ITenantScope.cs                           [E2] Enter(tenantId)
│   │   ├── IAuditLog.cs                              [E2] eventos explícitos de auditoría
│   │   ├── IIdempotencyStore.cs                      [E2] reserva técnica y respuesta; Api depende del puerto
│   │   ├── ITenantRepository.cs                      [E3]
│   │   ├── ITenantReader.cs                          [E3] desde acá TenantJobRunner recorre las organizaciones activas
│   │   ├── IMemberRepository.cs                      [E3]
│   │   ├── IMemberReader.cs                          [E3] punto de partida de "usuarios de mi organización"
│   │   ├── IUserRepository.cs                        [E3] escrituras de la identidad
│   │   ├── IInvitationRepository.cs                  [E3]
│   │   ├── ILoginCodeRepository.cs                   [E3]
│   │   ├── ILoginLinkRepository.cs                   [E8]
│   │   ├── ILoginAuditRepository.cs                  [E3]
│   │   ├── ITenantSettingsRepository.cs              [E3]
│   │   ├── ITenantSettingsReader.cs                  [E3]
│   │   ├── IRoleRepository.cs                        [E4]
│   │   ├── IRoleReader.cs                            [E4]
│   │   ├── IRoleAssignmentRepository.cs              [E4]
│   │   ├── IPlatformSettingsRepository.cs            [E3]
│   │   ├── IPlatformSettingsReader.cs                [E3]
│   │   ├── IPlatformPermissionAssignmentRepository.cs [E5]
│   │   ├── IPlatformPermissionAssignmentReader.cs    [E5]
│   │   ├── IPlatformReader.cs                        [E5] organizaciones, identidades y operadores (vista de plataforma)
│   │   ├── ISecurityEventRepository.cs               [E3]
│   │   ├── ISecurityEventReader.cs                   [E5]
│   │   ├── ICompanyRepository.cs                     [E6]
│   │   ├── ICompanyReader.cs                         [E6]
│   │   ├── ICompanyMembershipRepository.cs           [E6]
│   │   ├── ICompanyMembershipReader.cs               [E6]
│   │   ├── UniqueKeys.cs                             Nombres de los índices únicos que la base hace cumplir…
│   │   ├── IUserTenantAccessReader.cs                Consulta los accesos de una identidad global para selección y…
│   │   ├── ITenantSlugAvailabilityReader.cs          Comprueba reservas de plataforma y direcciones ya asignadas a organizaciones
│   │   ├── ITenantProvisioningReader.cs              Identifica el Dueño y la primera empresa bajo el alcance…
│   │   ├── ITenantFeatureRepository.cs               Escritura global y transaccional de módulos por espacio, con versión
│   │   ├── ITenantFeatureReader.cs                   Proyección global de habilitaciones de un espacio, sin consultar sus…
│   │   ├── ITenantAdminReader.cs                     Lee Dueños activos desde el índice global permitido de membresías
│   │   ├── ITenantAdminLock.cs                       Serializa cambios de asignaciones Dueño con el chequeo de baja…
│   │   ├── IPlatformTenantRepository.cs              Bloquea el estado global antes de su lectura y verifica…
│   │   ├── IPlatformAccountReader.cs                 Proyección global de cuentas para Plataforma
│   │   ├── IPersonalSpaceLock.cs                     Serializa la creación del espacio personal de una identidad entre…
│   │   ├── IPendingTenantOwnerRepository.cs          Guarda, dentro de la UoW del alta, el contacto del…
│   │   ├── IOrganizationUserReader.cs                Lee integrantes y sus relaciones dentro de la organización activa
│   │   ├── ILoginMethodMembershipReader.cs           Comprueba solo el acceso activo de una cuenta al dueño…
│   │   ├── ILoginInvitationReader.cs                  Invitaciones vigentes por correo, sin leer filas privadas
│   │   ├── ILoginInvitationRepository.cs              Aceptación por correo probado bajo RLS y UoW
│   │   ├── IInvitationReader.cs                      Empresas y roles de la persona invitada, primero los de…
│   │   ├── IExternalLoginLock.cs                     Serializa callbacks del mismo Google subject o correo verificado
│   │   ├── IConnectAuthorizationStore.cs             Persiste la autorización OpenID Connect que vincula sesión, cuenta, acceso,…
│   │   ├── ICompanyAdminReader.cs                    Cuenta Administradores efectivos de una empresa en el tenant activo
│   │   ├── ICompanyAdminLock.cs                      Serializa cambios de asignaciones de Administrador de la misma empresa
│   │   ├── IAccountRecoveryRepository.cs             Pedidos de «Recuperar mi cuenta», globales
│   │   ├── IAccountRecoveryReader.cs                 Lee la bandeja global de «Recuperar mi cuenta» con el…
│   │   ├── IAccountDeletionTenantReader.cs           Proyección de alcances de eliminación, sin datos privados ni perfiles…
│   │   ├── IAccountDeletionCancelTicketRepository.cs  Busca y guarda la prueba específica de cancelar baja
│   │   ├── IAccountDeletionRepository.cs             Reclamo durable y purga de credenciales globales de una cuenta…
│   │   ├── IConsumerDataRetentionReader.cs           Lote acotado de retenciones vencidas, sin copia personal
│   │   ├── IConsumerDataRetentionRepository.cs       Lock y relectura de metadatos en alcance Consumer y UoW del servicio
│   │   ├── ForeignKeys.cs                            Nombres de las claves foráneas que la base hace cumplir…
│   │   ├── ILegalReader.cs                           Lee documentos legales vigentes y aceptaciones pendientes para mostrarlos sin…
│   │   ├── ILegalRepository.cs                       Consulta y agrega versiones legales y aceptaciones dentro de la…
│   │   ├── ILoginMethodReader.cs                     Proyecta los métodos de ingreso de una cuenta para la…
│   │   ├── ILoginMethodRepository.cs                 Bloquea y modifica métodos de ingreso de una identidad global…
│   │   ├── IOrganizationAuditReader.cs               lee la auditoría de la organización con cursor y filtros
│   │   ├── IPublicPageReader.cs                      lee la página pública de la organización activa
│   │   ├── IPublicSiteReader.cs                      [E7] resolución y contenido publicado, sin contexto privado
│   │   ├── IDirectoryReader.cs                       [E7] listado y logo con filtros públicos explícitos
│   │   ├── IPublicPageRepository.cs                  escribe la página pública dentro de la transacción del servicio
│   │   ├── ITenantDomainReader.cs                    lee los dominios de la organización con sus correos administrados
│   │   └── ITenantDomainRepository.cs                escribe los dominios de correo globales, con lock y versión
│   └── Integrations/
│       ├── Request/                                  [E2–E3]
│       │   ├── ICurrentUser.cs                       [E2 shell] UserId, Access (Consumer | Business | Platform); claims reales E3
│       │   ├── ITenantContext.cs                     [E2] TenantId, TenantKind, RequiredTenantId (del acceso activo)
│       │   ├── IRequestInfo.cs                       [E3] IP, user agent
│       │   ├── IPublicSiteContext.cs                 [E7] BusinessTenantId? y Slug? del sitio publicado
│       │   ├── ITenantAccessInitializer.cs           Fija el tenant de un acceso ya autenticado, antes de…
│       │   ├── IInvitationFlowContext.cs             Identidad válida y continuación protegida del navegador, sin seleccionar un…
│       │   ├── IExecutionActorContext.cs             Actor actual de la ejecución
│       │   ├── IExecutionActorScope.cs               Ámbito del actor de ejecución
│       │   └── IPublicOrigin.cs                      [E3] origen público para armar enlaces
│       ├── Identity/                                 [E3–E4]
│       │   ├── ISignInService.cs                     técnico: sign-in, bloqueos, revocar sesiones (≤12 miembros)
│       │   ├── IUserLookup.cs                        búsqueda global por email o teléfono
│       │   ├── ITokenRevoker.cs
│       │   ├── IUserStatusReader.cs                  Consulta global acotada al estado de una identidad, sin exponer…
│       │   ├── ILoginLinkIssuer.cs                   Emite dentro de la UoW del llamador, ligado al método…
│       │   ├── IPhoneLinkObserver.cs                 [E8] cambios de Phone dentro de la UoW del núcleo, sin referencia al módulo
│       │   └── IGoogleAvailability.cs                [E3]
│       ├── Security/                                 [E3]
│       │   ├── ISecureTokenGenerator.cs
│       │   ├── ILoginCodeGenerator.cs
│       │   ├── ILoginCodeHasher.cs
│       │   ├── IPayloadProtector.cs                  cifrado del payload del outbox
│       │   └── IInvitationTokenProtector.cs          protege el token de invitación junto con los ids de su alcance
│       ├── Messaging/                                [E3]
│       │   ├── IOutbox.cs                            Enqueue(canal registrado) dentro del límite
│       │   ├── IChannelSender.cs                    emisor técnico por canal, consumido fuera de UoW por OutboxDeliveryProcessor
│       │   ├── IChannelReceiptSender.cs             [E8] transporte con recibo técnico neutral y seguro
│       │   ├── IOutboxDeliveryObserver.cs           [E8] metadatos de aceptación vigente dentro de Complete
│       │   ├── ILoginCodeAdmission.cs               [E8] reserva técnica previa al código dentro de la UoW del emisor
│       │   ├── ILoginCodeChannel.cs                 [E3] envía un código por un canal; el núcleo trae "email"
│       │   ├── IInvitationChannel.cs                [E3] envía una invitación; el núcleo trae "email"
│       │   ├── IAccountNoticeChannel.cs              [E3] envía un AccountNotice a un método de ingreso; el núcleo trae "email" (Email) y el
│       │   │                                         módulo de WhatsApp registra "whatsapp" (Phone)
│       │   └── IEmailTemplateRenderer.cs
│       ├── Localization/
│       │   └── IErrorTextSource.cs                  [E8] traducción neutral de errores aportados por módulos
│       ├── Caching/
│       │   ├── ITenantSettingsCache.cs               Invalida las preferencias de la organización después de guardar cambios…
│       │   ├── ITenantFeatureCache.cs                Cachea las habilitaciones crudas de cada espacio y se invalida…
│       │   ├── IRolePermissionCache.cs               Cachea el catálogo de permisos de un rol dentro de…
│       │   ├── IPlatformSettingsCache.cs             Invalida las reglas globales del registro después de un cambio…
│       │   ├── IAccessStatusCache.cs                 Estados de acceso con TTL breve e invalidación explícita al…
│       │   └── ITenantStatusCache.cs                 [E3] invalidar al suspender o reactivar
│       ├── Time/
│       │   └── ITimeZoneService.cs                   [E1] conversión y GetDayRangeUtc con TimeZoneInfo; catálogo en ReferenceData/
│       ├── Phones/
│       │   ├── IPhoneNumberDisplayFormatter.cs       [E1] formato nacional/internacional de E.164 sin lista de países
│       │   ├── IPhoneNumberParser.cs                 [E3] Parse(country, number, PhoneUsage) → Result<PhoneNumber>
│       │   └── PhoneUsage.cs                         [E3] Any | Mobile; no nombra WhatsApp (sus países los controla el módulo)
│       ├── Dns/
│       │   └── IDnsTxtLookup.cs                      consulta los registros TXT de un dominio
│       ├── Features/
│       │   ├── IFeatureAvailabilityPolicy.cs         True/false decide este acceso
│       │   ├── IFeatureDefinitionContributor.cs      Aporte de definiciones de funcionalidades
│       │   ├── IModuleCapabilityContributor.cs       Listado de capacidades de módulos
│       │   └── IFeatureCatalog.cs                    Catálogo cerrado de módulos reales
│       └── Legal/
│           ├── IConsumerCopyDeletion.cs              Reemplazo de una copia por clave estable de tabla y fila, dentro del alcance y UoW llamadores
│           ├── IRetainedOnConsumerDeletion.cs        Política por tipo compartido, motivo técnico y plazo
│           └── IAccountDeletionParticipant.cs        Hooks de cuenta en la UoW llamadora
├── Models/                                           [E1] *Request (entrada), *Response (salida), ReadModels/*Row (proyección)
│   ├── Auth/                                         [E3]
│   │   ├── SignupRequest.cs
│   │   ├── VerifySignupRequest.cs
│   │   ├── RequestLoginCodeRequest.cs
│   │   ├── RequestLoginCodeResponse.cs
│   │   ├── VerifyLoginCodeRequest.cs
│   │   ├── VerifyLoginCodeResponse.cs
│   │   ├── LoginMethodsResponse.cs                   respuesta de GET /api/auth/methods: canales encendidos en formato neutro; en E3, correo y Google;
│   │   │                                             en E8, el módulo suma WhatsApp y sus países (channels: [{ key: "whatsapp", countries: [...] }])
│   │   ├── ConnectUser.cs                            identidad + acceso y tenant elegidos, para armar el principal
│   │   ├── AccessSelectionRequest.cs                 acceso (consumer|business) y tenant pedidos en authorize
│   │   ├── ExternalSignInResponse.cs                 Devuelve el destino al que continúa el navegador después de…
│   │   ├── ExternalSignInRequest.cs                  ingreso o registro con Google; solo el registro crea una identidad
│   │   ├── BusinessSignupResponse.cs                 Confirma el alta, su estado y el correo principal de…
│   │   ├── AccountRecoveryModels.cs                  pedidos de código, verificación y envío de «Recuperar mi cuenta»
│   │   ├── ReturnUrls.cs
│   │   ├── BusinessSignupRequest.cs                  nombre, slug y CUIT de la empresa, y datos de quien la registra
│   │   ├── TenantSlugAvailability.cs                 Available | Reserved | Taken
│   │   ├── LoginLinkModels.cs                        El secreto se entrega solo al adaptador que encola el…
│   │   └── TenantSlugAvailabilityResponse.cs         respuesta de la consulta de una dirección pública
│   ├── Invitations/                                  [E3]
│   │   ├── PreviewInvitationRequest.cs               el token, en el cuerpo (nunca en la URL)
│   │   ├── AcceptInvitationRequest.cs                con y sin cuenta previa
│   │   ├── ProvenInvitationToken.cs                  El token del pedido o de la continuación, ya probado,…
│   │   ├── InvitationRow.cs                          Proyección interna del alcance autorizado
│   │   ├── InvitationNotice.cs                       aviso de invitación: los roles van como se guardaron
│   │   ├── InvitationGrantRow.cs                     Lo que ofrece una invitación
│   │   ├── InvitationPreviewResponse.cs
│   │   ├── AcceptInvitationResponse.cs               resultado de aceptar una invitación
│   │   ├── InvitationAcceptanceContext.cs            datos y resultado de aceptar una invitación
│   │   ├── InvitationContinuation.cs                 continuación protegida del flujo de invitación en el navegador
│   │   ├── InvitationPreviewState.cs                 estado de la invitación que se muestra antes de aceptarla
│   │   ├── InvitationTokenData.cs                    lo que protege el token de una invitación
│   │   ├── IssueInvitationRequest.cs                 pedido de emitir una invitación, de la organización o de Plataforma
│   │   ├── LoginInvitationContext.cs                  Alcances y proyecciones de invitaciones por correo demostrado
│   │   ├── InvitationDeliveryDestination.cs          Transporte de una invitación cuya identidad y prueba siguen siendo…
│   │   └── IssuedInvitation.cs                       invitación emitida, lista para encolar su entrega
│   ├── Profile/                                      [E3]
│   │   ├── MeResponse.cs                             cuenta + acceso activo + espacio personal + organizaciones + permisos + preferencias efectivas (cultura, zona, moneda) + features
│   │   ├── OrganizationSummary.cs                         id, nombre, slug y estado (para elegir organización)
│   │   ├── EffectivePermissions.cs                   organización + por empresa + Plataforma
│   │   ├── ModuleCapabilityResponse.cs               Disponibilidad y permisos de capacidades de módulos
│   │   └── UpdateMeRequest.cs
│   ├── PublicSite/                                   [E6–E7]
│   │   ├── PublicPageModels.cs                       [E6] borrador privado, guardado, publicación y logo
│   │   ├── PublicSiteResponse.cs                     [E7] ocho datos públicos, sin estado ni ids
│   │   ├── PublicSiteRow.cs                          [E7] proyecciones de resolución, contenido y logo
│   │   ├── ListDirectoryRequest.cs                   [E7] pedido paginado de búsqueda y orden
│   │   └── DirectoryEntryRow.cs                      [E7] slug, nombre, descripción y URL de logo
│   ├── Roles/                                        [E4]
│   │   ├── ListRolesRequest.cs
│   │   ├── RoleResponse.cs
│   │   ├── CreateRoleRequest.cs
│   │   ├── UpdateRoleRequest.cs
│   │   ├── DeleteRoleRequest.cs
│   │   ├── PermissionGroupResponse.cs
│   │   ├── RoleScopeFilter.cs                    Ámbito visible en el filtro del listado de roles
│   │   └── RoleRow.cs
│   ├── Companies/                                    [E6]
│   │   ├── ListCompaniesRequest.cs
│   │   ├── CompanyResponse.cs
│   │   ├── CompanyRequests.cs                        alta, edición y estado de una empresa; el CUIT, solo el número (D6)
│   │   └── CompanyMemberModels.cs                    Una empresa activa donde la persona es miembro activo, con…
│   ├── Settings/                                     [E6]
│   │   ├── PlatformSettingsRow.cs                Ajustes globales publicados a los casos de uso
│   │   └── OrganizationSettingsModels.cs             pedido y respuesta de la configuración de la organización
│   ├── Auditing/                                     [E6]
│   │   └── OrganizationAuditModels.cs                pedido, filas, «Quién» y detalle de la auditoría de la organización
│   ├── Time/                                         [E1]
│   │   └── DayRangeUtc.cs
│   ├── ReferenceData/                                [E1]
│   │   └── ReferenceDataResponse.cs                  cinco catálogos traducidos; zona con ID IANA, CountryCodes[] y ciudad
│   ├── Platform/                                     [E5]
│   │   ├── PlatformAccountModels.cs                  Estado que ve Plataforma
│   │   ├── PlatformAuditModels.cs                    Plataforma › Auditoría
│   │   ├── PlatformFeatureResponse.cs                Habilitación administrable
│   │   ├── PlatformLegalModels.cs                    Publicar una versión
│   │   ├── PlatformOperatorModels.cs                 Invitar un operador
│   │   ├── PlatformPublicSiteResponse.cs             Solo metadatos de publicación
│   │   ├── PlatformRecoveryModels.cs                 Bandeja Recuperaciones e hint de identidad proyectado para locks de aprobación
│   │   ├── PlatformSettingsModels.cs                 Los correos de contacto que muestran Ingreso y Registro, sin…
│   │   ├── PlatformTenantModels.cs                   listado, detalle con Dueños, historial, conteos, alta y estado de organizaciones
│   │   └── UpdatePlatformFeatureRequest.cs           Cambio explícito de un módulo global, con concurrencia y motivo
│   ├── Notifications/                                [E3]
│   │   └── AccountNotice.cs                          jerarquía cerrada: LoginMethodChanged (qué pasó, método enmascarado, fecha), ReviewLoginMethods (organización), DeletionRequested (fecha), DeletionCancelled, AccountDeleted
│   ├── Messaging/                                    [E3]
│   │   ├── OutboxDispatchClaim.cs                Una reserva durable
│   │   ├── OutboxDeliveryReceipt.cs              Resultado técnico seguro
│   │   └── EmailMessage.cs
│   ├── Features/
│   │   └── TenantFeatureRow.cs                       Datos persistidos sin cachear el resultado temporal de la habilitación
│   ├── Home/
│   │   └── HomeModels.cs                             Inicio de la organización (ADR 0040)
│   ├── Identity/
│   │   ├── AccountLoginMethodsResponse.cs            Entrega métodos de ingreso y capacidades de gestión de la…
│   │   ├── AddLoginEmailRequest.cs                   Transporta el correo que se quiere agregar a una cuenta…
│   │   ├── ChangeLoginMethodRequest.cs               Identifica el método que se cambiará o quitará y lleva…
│   │   ├── ExternalLogin.cs                          Claims seleccionados del proveedor externo después del callback
│   │   ├── GoogleAccountLinkState.cs                 Propiedades del protocolo protegidas por el middleware OAuth, nunca recibidas…
│   │   ├── GoogleChallengeResponse.cs                Entrega la URL del desafío Google al controller sin exponerla…
│   │   ├── LinkGoogleRequest.cs                      Lleva la cuenta esperada desde el state OAuth protegido y…
│   │   ├── LoginMethodCodeResponse.cs                Devuelve el método al que se envió un código y…
│   │   ├── LoginMethodRow.cs                         Método de ingreso visible para los casos de uso de…
│   │   ├── UserAccountRow.cs                         Datos globales de la cuenta sin exponer ApplicationUser fuera de…
│   │   ├── AddLoginMethodRequest.cs                  Alta propia por canal registrado
│   │   ├── VerifyLoginMethodRequest.cs               Entrega el código para verificar un método de ingreso sin…
│   ├── Legal/
│   │   ├── AcceptLegalRequest.cs                     Identifica el documento y la versión exacta que una persona…
│   │   ├── AccountDeletionContext.cs                 Entrega a cada participante la cuenta y, cuando corresponde, el…
│   │   ├── AccountDeletionWork.cs                    Transporta el lease de una baja reclamada y sus organizaciones…
│   │   ├── ConsumerDataRetentionWork.cs              Retención y Consumer que se abre antes de la transacción
│   │   ├── CancelAccountDeletionRequest.cs           Lleva el comprobante temporal que cancela una baja pendiente sin…
│   │   ├── LegalDocumentRow.cs                       Versión y texto vigente de un documento legal en una…
│   │   ├── PendingDeletionState.cs                   Expone la fecha programada y el comprobante temporal para cancelar…
│   │   ├── PendingLegalDocumentResponse.cs           Identifica un documento legal y su versión pendiente de aceptación
│   │   └── RequestAccountDeletionRequest.cs          Lleva el motivo de baja de la sesión iniciada
│   ├── OrganizationUsers/
│   │   └── OrganizationUserModels.cs                 Una persona de la organización
│   ├── Common/
│   │   ├── PhoneInputRequest.cs                      País y número sin normalizar
│   │   └── VersionResponse.cs                        La versión que quedó después de un guardado; el front la toma como base…
│   └── Tenancy/
│       ├── MemberRow.cs                              Membresía visible solo desde el tenant activo
│       ├── OrganizationDomainModels.cs               Un dominio de la organización con los correos verificados de…
│       ├── TenantRow.cs
│       ├── TenantSettingsRow.cs                      Preferencias por defecto del tenant activo
│       └── UserTenantAccessRow.cs                    Acceso de una cuenta leído del índice global y de…
├── Validation/                                       [E1] un *RequestValidator (internal sealed) por request que lo necesite
│   ├── Auth/                                         [E3]
│   │   ├── SignupRequestValidator.cs
│   │   ├── VerifySignupRequestValidator.cs
│   │   ├── RequestLoginCodeRequestValidator.cs
│   │   ├── VerifyLoginCodeRequestValidator.cs
│   │   ├── ExternalSignInRequestValidator.cs        Exige un retorno de autorización seguro y, solo para altas…
│   │   ├── AccountRecoveryRequestValidators.cs      Método anterior y nuevo exclusivos Email/Phone, nonce y código
│   │   ├── LoginLinkRequestValidators.cs            [E8] token seguro y acceso consumer
│   │   └── BusinessSignupRequestValidator.cs
│   ├── Invitations/                                  [E3]
│   │   └── AcceptInvitationRequestValidator.cs
│   ├── Profile/                                      [E3]
│   │   └── UpdateMeRequestValidator.cs
│   ├── PublicSite/                                   [E6–E7]
│   │   ├── UpdatePublicPageRequestValidator.cs       [E6]
│   │   └── ListDirectoryRequestValidator.cs          [E7]
│   ├── Roles/                                        [E4]
│   │   ├── ListRolesRequestValidator.cs
│   │   ├── CreateRoleRequestValidator.cs
│   │   ├── UpdateRoleRequestValidator.cs
│   │   └── DeleteRoleRequestValidator.cs             exige la versión al borrar un rol
│   ├── Companies/                                    [E6]
│   │   ├── ListCompaniesRequestValidator.cs
│   │   ├── CompanyMemberValidators.cs                validadores de los pedidos de miembros de una empresa
│   │   └── CompanyRequestValidators.cs               validadores del alta, la edición y el estado de una empresa
│   ├── Auditing/
│   │   └── ListOrganizationAuditRequestValidator.cs  período, tema, búsqueda y «Quién» (actorId o actorKind)
│   ├── Platform/                                     [E5]
│   │   ├── UpdatePlatformSettingsRequestValidator.cs
│   │   ├── ListPlatformAuditRequestValidator.cs      Período y tipo de la lista cerrada del tablero
│   │   ├── ListPlatformTenantHistoryRequestValidator.cs cursor after/limit del historial por organización
│   │   ├── PlatformAccountRequestValidators.cs       Motivo de una acción sobre una cuenta; versión y motivo en recuperaciones y operadores
│   │   ├── PlatformOperatorRequestValidators.cs      Exige el correo
│   │   ├── PlatformTenantRequestValidators.cs        validadores del listado, el alta y el estado de organizaciones
│   │   ├── PublishLegalDocumentRequestValidator.cs   Tipo, fecha y textos presentes
│   │   └── UpdatePlatformFeatureRequestValidator.cs  origen, vencimiento, motivo y versión al cambiar un módulo
│   ├── Identity/
│   │   ├── AddLoginEmailRequestValidator.cs          Exige un correo antes de iniciar el alta de otro…
│   │   ├── ChangeLoginMethodRequestValidator.cs      Exige el identificador del método objetivo
│   │   ├── AddLoginMethodRequestValidator.cs         Exige correo cuando no se proporciona teléfono
│   │   ├── VerifyLoginMethodRequestValidator.cs      Exige el método de correo y un código numérico antes…
│   ├── Legal/
│   │   ├── AcceptLegalRequestValidator.cs            Exige identificadores y versiones legales válidos, sin duplicados, antes de…
│   │   └── RequestAccountDeletionRequestValidator.cs Exige una razón de baja acotada antes de iniciar el…
│   ├── OrganizationSettings/
│   │   ├── RegisterOrganizationDomainRequestValidator.cs exige un dominio de correo válido
│   │   ├── RemoveOrganizationDomainRequestValidator.cs exige el dominio y su versión al retirarlo
│   │   └── UpdateOrganizationSettingsRequestValidator.cs nombre, cultura, zona, moneda y versión de la configuración
│   └── OrganizationUsers/
│       └── OrganizationUserValidators.cs             validadores del listado, invitar, editar y cambiar el estado en Usuarios
├── Services/                                         [E1] servicios internal sealed partial; helpers sin IUnitOfWork
│   ├── Auth/                                         [E3]
│   │   ├── AccountService.cs                         registro de personas (identidad + espacio personal)
│   │   ├── PersonalSpaceProvisioner.cs               [E3] prepara Personal antes de la UoW y delega las escrituras comunes
│   │   ├── TenantSpaceProvisioner.cs                 [E3] agrega Tenant, miembros y ajustes en el scope activo; lo reutiliza el seed Development y luego E6
│   │   ├── SignupPolicy.cs                           ¿el registro está abierto? ¿el email está libre?
│   │   ├── LoginCodeService.cs
│   │   ├── LoginCodeRequester.cs                     [E3] emisión y mensaje dentro de la UoW de LoginCodeService
│   │   ├── LoginDestinationResolver.cs               [E8] normaliza Email/Phone Mobile y delega disponibilidad/política de alta al canal
│   │   ├── LoginVerifier.cs                          [E3] verificación, intentos y auditoría dentro de esa UoW
│   │   ├── LoginCodeIssuer.cs
│   │   ├── LoginCodeVerifier.cs
│   │   ├── LoginLinkService.cs                       [E8]
│   │   ├── LoginLinkIssuer.cs                        [E8]
│   │   ├── LoginLinkGuard.cs                         [E8] snapshot de propietario/método/cuenta y locks de canje
│   │   ├── LoginLinkVerifier.cs                      [E8] consume prueba y conserva gate de baja/estado
│   │   ├── LoginLinkReplayService.cs                 [E8] lectura fresca de éxito o baja pendiente
│   │   ├── ConnectService.cs
│   │   ├── ConnectLogoutService.cs                   [E3]
│   │   ├── AccessSwitchPolicy.cs                     acceso pedido válido: membresía activa (B2B) o identidad activa (B2C)
│   │   ├── UserCultureResolver.cs                    cultura efectiva para mensajes en segundo plano
│   │   ├── TenantProvisioningReplayVerifier.cs       Comprueba que un replay de provisionamiento coincida con el espacio…
│   │   ├── SignupExistingMethodVerifier.cs           Valida la cuenta existente y confirma su método de correo…
│   │   ├── SignupCodeVerifier.cs                     Bloquea, relee y verifica el código antes de registrar o…
│   │   ├── SignupCodeRequester.cs                    Aplica la política de alta y encola el código en…
│   │   ├── SignupAccountRegistrar.cs                 Crea identidades por correo y sus consentimientos
│   │   ├── LoginMethodsService.cs                    Informa al front qué canales de código y proveedores externos…
│   │   ├── LoginCodeDeliveryIssuer.cs                 Encola códigos respetando estado y disponibilidad del método
│   │   ├── LoginInvitationResolver.cs                 Resuelve invitaciones antes de la transacción de ingreso
│   │   ├── LoginCodeAttemptGuard.cs                  Coordina lock, intentos fallidos y auditoría del código de ingreso
│   │   ├── LoginAccountGuard.cs                      Ordena cuenta antes de destino y revalida el ingreso
│   │   ├── GoogleAccountResolver.cs                  Resuelve el subject global bajo lock, vincula correo verificado o…
│   │   ├── GoogleAccountRegistrar.cs                 Prepara el registro Google personal o empresarial con ambos consentimientos…
│   │   ├── GoogleAccountMethodFinalizer.cs           Persiste el ingreso Google y sincroniza el contacto primario con…
│   │   ├── ConnectAuthorizationService.cs            Guarda una autorización OpenID Connect para la cuenta, acceso y…
│   │   ├── BusinessOrganizationCreator.cs            Crea la organización con su primera empresa, o reproduce un…
│   │   ├── AuthRegistration.cs                       Registra el ingreso, el registro de personas y de empresas,…
│   │   ├── AccountRecoveryGuard.cs                   Lecturas/política y locks de identidad→cuenta→código, sin UoW ni escrituras propias
│   │   ├── AccountRecoveryNotifier.cs                Mensajes de «Recuperar mi cuenta», dentro de la UoW del…
│   │   ├── AccountRecoveryNoticeService.cs           Toma los pedidos de «Recuperar mi cuenta» cuyo aviso a…
│   │   ├── ExternalLoginService.cs                   [E3]
│   │   ├── AccountRecoveryService.cs                 Recuperación Email/Phone sin sesión (ADR 0043), prueba con nonce y revalidación
│   │   ├── BusinessSignupPolicy.cs                   modo de alta y límite por persona
│   │   ├── BusinessSignupService.cs
│   │   ├── GoogleAccountGuard.cs                     controla la cuenta antes de entrar con Google; un operador entra solo con código
│   │   ├── TenantProvisioner.cs                      idempotente: settings, roles de sistema, 1ª empresa, membresía de Dueño (TenantAdmin) y página pública en Draft
│   │   ├── LoginCodeAttemptVerifier.cs               Verifica el propósito del código y conserva intentos/lockout solo ante…
│   │   └── TenantSlugAvailabilityService.cs          consulta si una dirección pública está disponible, reservada o tomada
│   ├── Invitations/                                  [E3]
│   │   ├── InvitationService.cs                      vista previa y aceptación, con y sin cuenta previa (sin cuenta crea la identidad sin
│   │   │                                             espacio personal)
│   │   ├── InvitationsRegistration.cs                Registra las invitaciones (emitir, mostrar, aceptar y registrar su vencimiento),…
│   │   ├── InvitationPreviewResolver.cs              Prueba el token antes de entrar y mantiene la continuación…
│   │   ├── InvitationPreviewBuilder.cs               Lee exclusivamente dentro del alcance probado y no consume ni…
│   │   ├── InvitationIssuingGuard.cs                 Serializa destinos compartidos y comprueba organización y dueño sin crear…
│   │   ├── InvitationIdentityDefaultsResolver.cs     Elige preferencias desde la organización y catálogos
│   │   ├── InvitationExpiryService.cs                «Venció la invitación a …» (tablero Auditoria-Org)
│   │   ├── InvitationDeliveryIssuer.cs               Usa datos actuales y cultura/zona explícitas para encolar el canal…
│   │   ├── InvitationAcceptanceResolver.cs           Prueba el token y, adentro de su organización, lee la…
│   │   ├── InvitationIssuer.cs                       emite y encola por IInvitationChannel; sin ruta propia: lo usan PlatformTenantCreator
│   │   │                                             (E5) y OrganizationUserInvitationWriter (E6)
│   │   ├── InvitationAcceptanceGuard.cs              bloquea y controla al dueño del correo antes de aceptar
│   │   ├── LoginInvitationAcceptanceWriter.cs         Acepta todas las invitaciones vigentes del correo demostrado
│   │   ├── InvitationAcceptanceWriter.cs             acepta la invitación: membresía, roles y empresas
│   │   ├── InvitationAccountRegistrar.cs             crea la cuenta de quien acepta sin tenerla, con su correo
│   │   └── InvitationSessionRestorer.cs              después de aceptar, actualiza el estado del acceso y la continuación
│   ├── Profile/                                      [E3]
│   │   ├── ProfileService.cs
│   │   ├── ProfileSnapshotBuilder.cs             Compone la cuenta y sus accesos globales con las preferencias…
│   │   ├── ProfileEffectivePermissionsBuilder.cs      Compone los permisos del acceso activo desde organización, empresas o Plataforma
│   │   ├── ProfileRegistration.cs                Registra el perfil de la persona y sus preferencias
│   │   ├── ProfileModulesBuilder.cs              Construcción de módulos y capacidades del perfil
│   │   └── ProfilePreferencesResolver.cs             Resuelve la moneda visible del perfil desde preferencias autorizadas y…
│   ├── Roles/                                        [E4] ← ÁREA DE REFERENCIA
│   │   ├── RoleService.cs
│   │   ├── RolesRegistration.cs                  Registra los roles y los permisos efectivos
│   │   ├── RoleAssignmentGuard.cs                Protege dónde vale cada rol al asignarlo, en Usuarios y…
│   │   ├── PermissionService.cs                      [E4] HybridCache t:{tenant}:perm:{user}
│   │   └── PlatformPermissionService.cs              [E4]
│   ├── Companies/                                    [E6]
│   │   ├── CompanyService.cs
│   │   ├── CompanyReferenceDataResolver.cs       Resuelve los datos de referencia admitidos por la ficha de…
│   │   ├── CompanyMemberStatePolicy.cs           El estado de un miembro de empresa, como especificación
│   │   ├── CompanyMembershipWriter.cs            Escribe membresías de empresa dentro de la transacción del servicio,…
│   │   ├── CompanyMembershipService.cs           Administra los miembros y roles de una empresa sin alterar…
│   │   ├── CompanyCreator.cs                     Agrega una empresa nueva junto con su primera membresía y…
│   │   ├── CompaniesRegistration.cs              Registra las empresas de la organización y sus miembros
│   │   └── LastCompanyAdminGuard.cs
│   ├── Auditing/
│   │   ├── OrganizationAuditEntryBuilder.cs          Arma las entradas de la auditoría de la organización como…
│   │   ├── AuditingRegistration.cs                   Registra la consulta de la auditoría de la organización
│   │   └── OrganizationAuditService.cs               arma la auditoría de la organización en el idioma de quien mira
│   ├── ReferenceData/                                [E1]
│   │   ├── ReferenceDataRegistration.cs              Registra los datos de referencia
│   │   └── ReferenceDataService.cs                   usa los cinco catálogos, devuelve ReferenceDataResponse
│   ├── Messaging/                                    [E3]
│   │   ├── OutboxDispatchService.cs                  [E8] ClaimNext/Complete, una UoW por operación; transporte fuera en el processor
│   │   └── MessagingRegistration.cs                  Registra el despacho del outbox, con sus opciones
│   ├── Platform/                                     [E5]
│   │   ├── TenantAdministrationService.cs
│   │   ├── PlatformAccountService.cs
│   │   ├── PlatformOperatorService.cs
│   │   ├── PlatformAuditService.cs
│   │   ├── PlatformSettingsService.cs
│   │   ├── PlatformActionGuard.cs                    motivo obligatorio + SecurityEvent antes de Enter
│   │   ├── PlatformAccountDeletionService.cs         Baja iniciada por Plataforma (ADR 0035)
│   │   ├── PlatformAccountStateService.cs            Suspende, reactiva o cierra las sesiones de una cuenta desde…
│   │   ├── PlatformAuditCategories.cs                Los tipos del filtro de Auditoría del tablero y a…
│   │   ├── PlatformAuditWriter.cs                    Agrega el SecurityEvent del operador con su destino, el diff…
│   │   ├── PlatformDomainService.cs                  Verifica o retira un dominio de correo de una organización…
│   │   ├── PlatformFeatureListBuilder.cs             lista los módulos de una organización con su habilitación
│   │   ├── PlatformFeatureService.cs                 módulos por organización: consulta y cambio con motivo
│   │   ├── PlatformFeatureWriter.cs                  guarda el cambio de un módulo, con versión y SecurityEvent
│   │   ├── PlatformLegalService.cs                   Plataforma › Documentos legales
│   │   ├── PlatformOperatorGuard.cs                  Protege al último operador activo con platform.operators.manage
│   │   ├── PlatformOperatorInvitationNotifier.cs     Encola, dentro de la UoW del llamador, el aviso «Invitación…
│   │   ├── PlatformOperatorInvitationService.cs      Invita operadores (ADR 0042)
│   │   ├── PlatformOperatorProvisioner.cs            Crea, dentro de la UoW del llamador, la identidad exclusiva…
│   │   ├── PlatformPublicSiteService.cs              moderación de la página pública: despublicar y permitir publicar
│   │   ├── PlatformPublicSiteWriter.cs               aplica la moderación, su auditoría y los avisos a los Dueños
│   │   ├── PlatformRecoveryDecisionService.cs        Resuelve pedidos de «Recuperar mi cuenta»
│   │   ├── PlatformRecoveryNotifier.cs               Avisos de la decisión, dentro de la UoW
│   │   ├── PlatformRecoveryService.cs                Bandeja Recuperaciones
│   │   ├── PlatformRegistration.cs                   Registra los casos de uso de Plataforma
│   │   ├── PlatformScopeAccessService.cs             autoriza y registra el SecurityEvent antes de entrar a una organización
│   │   ├── PlatformSettingsAuditWriter.cs            Prepara el evento del operador en la transacción que modifica…
│   │   ├── PlatformTenantActivator.cs                aprueba o reintenta la activación de una organización
│   │   ├── PlatformTenantCreator.cs                  alta de una organización desde Plataforma, con su Dueño invitado
│   │   ├── PlatformTenantNotifier.cs                 Encola los avisos previstos a Dueños activos, usando contactos disponibles…
│   │   ├── PlatformTenantSlugResolver.cs             elige una dirección pública libre a partir del nombre
│   │   └── PlatformTenantStateWriter.cs              Prepara una transición global dentro de la UoW del servicio
│   ├── Home/
│   │   ├── HomeRegistration.cs                       Registra los inicios de la organización y del miembro
│   │   ├── HomeService.cs                            Arma el inicio en un solo pedido de lectura (ADR…
│   │   ├── MemberHomeCompaniesBuilder.cs             «Tus empresas» de Inicio-Miembro
│   │   ├── OrganizationHomeAreasBuilder.cs           Las tarjetas de Inicio-Org
│   │   └── PendingItemOrder.cs                       Orden de «Para resolver» (ADR 0040)
│   ├── Identity/
│   │   ├── AccountGoogleService.cs                   Vincula Google como método de ingreso de una cuenta que…
│   │   ├── AccountLoginMethodsService.cs             Construye la lista de métodos de ingreso de la cuenta,…
│   │   ├── AccountNoticeIssuer.cs                    Encola dentro de la UoW del caso de uso, sin…
│   │   ├── GoogleProfileWriter.cs                    Normaliza e importa las dos claims de nombres Google en partes vacías (A7/ADR 0046)
│   │   ├── GoogleMethodLinkFinalizer.cs              Completa el alta Google
│   │   ├── GoogleMethodLinker.cs                     Aplica las reglas para sumar un ingreso Google a una…
│   │   ├── IdentityRegistration.cs                   Registra la gestión de métodos de ingreso y Google
│   │   ├── LoginMethodAvailabilityPolicy.cs          Decide qué métodos verificados pueden usarse para ingresar o respaldar…
│   │   ├── LoginMethodEditor.cs                      Dentro de la transacción modifica métodos y conserva el principal
│   │   ├── LoginMethodGuard.cs                       Bloquea la identidad global y verifica que la cuenta permita…
│   │   ├── LoginMethodIssuer.cs                      Agrega un correo de ingreso o reenvía su código de…
│   │   ├── LoginMethodManagementService.cs           Coordina alta, verificación, elección del principal y baja de métodos…
│   │   ├── LoginMethodNotifier.cs                    Registra un evento de seguridad y encola el aviso de…
│   │   ├── LoginMethodLinker.cs                      Vínculo verificado y copia del principal en la UoW del…
│   │   ├── LegalConsentRecorder.cs                   Registra únicamente consentimiento explícito, dentro de la UoW del llamador
│   │   ├── AccountRegistrar.cs                       Crea la identidad global
│   │   ├── LoginMethodVerifier.cs                    Verifica Email/Phone nuevo con hint proyectado y entidad fresca tras locks
│   ├── Legal/
│   │   ├── AccountDeletionCancelIssuer.cs            Emite un comprobante de cancelación para una cuenta pendiente de…
│   │   ├── AccountDeletionCanceller.cs               Valida y consume el comprobante de cancelación de baja, reactiva…
│   │   ├── AccountDeletionNotifier.cs                Registra eventos de seguridad y encola avisos de solicitud, cancelación…
│   │   ├── AccountDeletionPolicy.cs                  Impide pedir la baja cuando la cuenta es operadora, ya…
│   │   ├── AccountDeletionProcessingService.cs       Reclama bajas vencidas y procesa cada tenant en una transacción…
│   │   ├── AccountDeletionProcessor.cs               Ejecuta la limpieza de cada organización y luego purga credenciales…
│   │   ├── AccountDeletionRequester.cs               La baja que pide la persona
│   │   ├── AccountDeletionScheduler.cs               Programa una baja ya autorizada, dentro de la transacción del…
│   │   ├── AccountDeletionService.cs                 Expone solicitud y cancelación de baja con su UnitOfWork
│   │   ├── AccountDeletionSessionRestorer.cs         Después del commit de una baja o cancelación, invalida el…
│   │   ├── LastTenantAdminGuard.cs                   Dueños activos = TenantAdmin con identidad Active (la misma lectura que AccountDeletionPolicy)
│   │   ├── LegalAcceptanceGuard.cs                   Consulta qué documentos legales vigentes faltan aceptar a la cuenta…
│   │   ├── LegalAcceptanceService.cs                 Entrega documentos pendientes y registra su aceptación con validación previa…
│   │   ├── LegalAcceptanceWriter.cs                  Bloquea la cuenta, relee las versiones legales vigentes y guarda…
│   │   ├── LegalRegistration.cs                      Registra los documentos legales y su aceptación, la baja de…
│   │   └── LegalService.cs                           Lee la versión pública vigente usando sólo culturas habilitadas del…
│   ├── OrganizationSettings/
│   │   ├── LoginMethodDomainLinker.cs                Marca solo métodos cuyo correo coincide con un dominio verificado,…
│   │   ├── OrganizationDomainPendingItems.cs         «Para resolver» de Configuración (ADR 0040)
│   │   ├── OrganizationDomainRegistrar.cs            Registra un dominio único y crea el token de verificación…
│   │   ├── OrganizationDomainRemover.cs              Retira el dominio dentro de la transacción del llamador, conservando…
│   │   ├── OrganizationDomainService.cs              Valida las solicitudes y coordina el ciclo de vida del…
│   │   ├── OrganizationDomainVerifier.cs             Consulta el TXT fuera de la transacción y, dentro de…
│   │   ├── OrganizationSettingsRegistration.cs       Registra la configuración de la organización y sus dominios, con…
│   │   └── OrganizationSettingsService.cs            Lee y actualiza el nombre y los datos predeterminados de…
│   ├── OrganizationUsers/
│   │   ├── CompanyInvitationPolicy.cs                Qué invitación se resuelve desde la ficha de una empresa
│   │   ├── OrganizationAvailableLoginMethodsResolver.cs Selecciona los ingresos verificados que siguen disponibles después de salir…
│   │   ├── OrganizationMembershipEndNoticeBuilder.cs Registra el evento de seguridad y prepara los datos de…
│   │   ├── OrganizationMembershipEndNotifier.cs      Notifica una salida usando solo métodos de ingreso que la…
│   │   ├── OrganizationUserAccessRemover.cs          Limpia accesos y membresías de empresa al revocar una invitación…
│   │   ├── OrganizationUserAdministratorGuard.cs     Impide quitar al último Dueño o Administrador activo durante cambios…
│   │   ├── OrganizationUserAssignmentWriter.cs       Valida roles por alcance y sincroniza asignaciones y membresías de…
│   │   ├── OrganizationUserAssignments.cs            roles y empresas deseados y actuales de una persona
│   │   ├── OrganizationUserEditor.cs                 Edita estado, roles y membresías dentro de la transacción del…
│   │   ├── OrganizationUserInvitationWriter.cs       Invita, reenvía y revoca dentro de la transacción del servicio
│   │   ├── OrganizationUserPendingItems.cs           «Para resolver» de Usuarios (ADR 0040)
│   │   ├── OrganizationUserRemover.cs                Quita al miembro y revoca sus invitaciones y accesos de…
│   │   ├── OrganizationUserService.cs                Administra miembros e invitaciones sin alterar la cuenta global ni…
│   │   ├── OrganizationUserStatePolicy.cs            El estado de una persona de la organización, como especificación
│   │   ├── OrganizationUserStatusEditor.cs           Activa o desactiva miembros respetando bajas pendientes y la protección…
│   │   └── OrganizationUsersRegistration.cs          Registra los usuarios de la organización (miembros, invitaciones y roles),…
│   └── PublicSite/
│       ├── PublicPageEditor.cs                       Guarda contenido, slug y publicación de la página dentro de…
│       ├── PublicPagePendingItems.cs                 «Para resolver» de la Página pública (ADR 0040)
│       ├── PublicPageService.cs                      lee, guarda, publica y retira la página pública
│       ├── PublicSiteService.cs                      [E7] lectura anónima del sitio publicado resuelto
│       ├── DirectoryService.cs                       [E7] listado paginado y logo público por slug
│       └── PublicSiteRegistration.cs                 Registra la página pública de la organización, con su fuente…
├── Modules/                                         [E8] módulos quitables: el núcleo no los referencia (test)
│   └── WhatsApp/                                    AddWhatsAppModule() registra todo lo de esta carpeta
│       ├── WhatsAppModule.cs                         AddWhatsAppModule(): servicios, canales y opciones
│       ├── Interfaces/
│       │   ├── Services/
│       │   │   ├── IWhatsAppWebhookService.cs
│       │   │   ├── IWhatsAppInboundService.cs
│       │   │   └── IWhatsAppMessageRetentionService.cs [E8] mantenimiento independiente del canal
│       │   ├── Persistence/
│       │   │   ├── IWhatsAppContactRepository.cs
│       │   │   ├── IWhatsAppMessageRepository.cs
│       │   │   ├── IWhatsAppCodeQuotaRepository.cs
│       │   │   └── UniqueKeys.cs                    índices que traducen dedupe/contacto
│       │   └── Integrations/
│       │       ├── IWhatsAppAvailability.cs
│       │       ├── IWhatsAppChannelSettings.cs
│       │       ├── IWhatsAppTemplateRenderer.cs
│       │       ├── IWhatsAppSignatureValidator.cs
│       │       ├── IWhatsAppWebhookReader.cs
│       │       ├── IWhatsAppWebhookReplay.cs         repetición técnica en scope fresco tras colisión
│       │       ├── IWhatsAppInboundConsumer.cs       efectos/reply dentro de UoW del procesador
│       │       ├── IWhatsAppInboundSettings.cs
│       │       └── IWhatsAppInboundSignal.cs
│       ├── Models/
│       │   ├── BotButtons.cs                        [E8] IDs públicos fijos de ingreso/recuperación
│       │   ├── WhatsAppOutboundMessage.cs            jerarquía cerrada: texto, botones, código, invitación, aviso de la cuenta
│       │   ├── WhatsAppWebhookRequest.cs
│       │   ├── WhatsAppWebhookBatch.cs
│       │   └── WhatsAppInboundContext.cs             claim/hint/context original nullable, sin apropiación posterior
│       ├── Services/
│       │   ├── WhatsAppWebhookService.cs
│       │   ├── WhatsAppWebhookWriter.cs              locks ordenados, dedupe, contactos y snapshots
│       │   ├── WhatsAppInboundService.cs
│       │   ├── WhatsAppInboundGuard.cs               hints sin tracking → locks → lectura fresca/lease
│       │   ├── WhatsAppSafeTextPolicy.cs             OTP agrupado/enlaces/tokens metadata-only
│       │   ├── WhatsAppDeliveryObserver.cs           aceptación vigente y callbacks tempranos
│       │   ├── WhatsAppCodeQuotaIssuer.cs            admisión neutral en UoW del emisor
│       │   ├── WhatsAppLoginCodeChannel.cs           implementa ILoginCodeChannel ("whatsapp")
│       │   ├── WhatsAppInvitationChannel.cs          implementa IInvitationChannel ("whatsapp")
│       │   ├── WhatsAppAccountNoticeChannel.cs       implementa IAccountNoticeChannel ("whatsapp", métodos Phone)
│       │   ├── WhatsAppPhoneLinkObserver.cs          [E8] hooks dentro de UoW, vínculo propio sin robar contacto
│       │   ├── WhatsAppContactRemover.cs             [E8] revoca enlaces y limpia por snapshot original; retiro físico propio
│       │   ├── WhatsAppAccountDeletionParticipant.cs [E8] limpieza del tramo global, no de tenant
│       │   ├── WhatsAppMessageRetentionService.cs    [E8] UoW/lote de 100, plazo fijo por ReceivedAtUtc
│       │   ├── WhatsAppBotWriter.cs                  [E8] consumer dentro de la UoW inbound
│       │   └── WhatsAppBotReplyBuilder.cs            [E8] resources/perfil/marca y ventana cifrada
│       └── Resources/
│           ├── WhatsAppTexts.resx / .en.resx         textos del bot y de las plantillas
│           └── WhatsAppTexts.cs
└── Resources/                                        [E1] es (neutral) + en; claves en paridad
    ├── Errors.resx                                   [E1] código del error → texto; Title.<ErrorType>
    ├── Errors.en.resx
    ├── Validation.resx                               [E1]
    ├── Validation.en.resx
    ├── Formatting.resx                               [E1] textos variables de formato, enum y booleano; paridad es/en
    ├── Formatting.en.resx
    ├── Permissions.resx                              [E4] Area.*, Permission.*, PermissionDescription.*, Role.*
    ├── Permissions.en.resx                          [E4]
    ├── Notifications.resx                            [E3] asuntos y cuerpos de correo (también los avisos de la cuenta por correo)
    ├── Notifications.en.resx                        [E3]
    ├── Audit.resx                                    [E6] AuditAction.*, Entity.*
    ├── Audit.en.resx                                [E6]
    ├── ErrorTexts.cs                                [E1]
    ├── ValidationTexts.cs                           [E1]
    ├── FormattingTexts.cs                           [E1]
    ├── PermissionTexts.cs                           [E4]
    ├── NotificationTexts.cs                         [E3] con cultura explícita
    ├── RoleTexts.cs                                  Traduce únicamente roles de sistema
    ├── AuditTexts.cs                                 [E6]
    ├── RoleNames.en.resx                             nombres y descripciones de los roles de sistema (en)
    └── RoleNames.resx                                nombres y descripciones de los roles de sistema (es)
```

## src/ArquitecturaBaseMultitenant.Infrastructure `[E0]`

EF Core, Identity, OpenIddict, mensajería, WhatsApp y adaptadores técnicos. Referencia a Application.

```
ArquitecturaBaseMultitenant.Infrastructure/
├── ArquitecturaBaseMultitenant.Infrastructure.csproj  FrameworkReference AspNetCore; Templates como EmbeddedResource
├── DependencyInjection.cs                             [E1] AddInfrastructure(cfg, env) → llama a cada *Registration
├── Persistence/                                       [E1–E2]
│   ├── ApplicationDbContext.cs                        [E2] IdentityUserContext<ApplicationUser,Guid> + IDataProtectionKeyContext
│   ├── Schemas.cs                                     [E2] nombres únicos de platform, identity, tenant, public_site y engagement
│   ├── PersistenceRegistration.cs                     [E2] DbContext (appdb), interceptores, UoW, repositorios, readers
│   ├── IPersistenceModelContributor.cs                [E8] configuraciones y tipos globales explícitos de un módulo
│   ├── PersistenceModelContributors.cs                [E8] descubrimiento sin secretos/red y PersistenceModelCacheKeyFactory por conjunto de contribuidores
│   ├── UnitOfWork.cs                                  [E2]
│   ├── LoginInvitationScope.cs                         Capacidad nominal del OTP para invitaciones y miembros exactos
│   ├── TenantContext.cs                               [E2] implementa ITenantContext + ITenantScope
│   ├── ConstraintViolations.cs                        [E2] índice único o clave foránea → excepción con su nombre
│   ├── DatabaseBootstrapExtensions.cs                 [E2] solo Development: crea la base con ICU es-AR, los roles mt_owner y mt_app, migra y hace el seed (appdb-admin)
│   ├── Configurations/                                una IEntityTypeConfiguration<T> por entidad
│   │   ├── Platform/
│   │   │   ├── DataProtectionKeyConfiguration.cs      [E2] claves de Data Protection en platform
│   │   │   ├── ReferenceData/                         [E2] una IEntityTypeConfiguration por tabla global, sin RLS
│   │   │   │   ├── CurrencyConfiguration.cs
│   │   │   │   ├── CurrencyTranslationConfiguration.cs
│   │   │   │   ├── CountryConfiguration.cs
│   │   │   │   ├── CountryTranslationConfiguration.cs
│   │   │   │   ├── TimeZoneConfiguration.cs
│   │   │   │   ├── TimeZoneCountryConfiguration.cs
│   │   │   │   ├── TimeZoneTranslationConfiguration.cs
│   │   │   │   ├── CultureConfiguration.cs
│   │   │   │   ├── CultureTranslationConfiguration.cs
│   │   │   │   ├── TaxIdTypeConfiguration.cs
│   │   │   │   └── TaxIdTypeTranslationConfiguration.cs
│   │   │   ├── TenantConfiguration.cs                 [E3]
│   │   │   ├── OutboxMessageConfiguration.cs          [E3]
│   │   │   ├── PlatformSettingsConfiguration.cs       [E3]
│   │   │   ├── PlatformPermissionAssignmentConfiguration.cs [E5]
│   │   │   ├── TenantCountersConfiguration.cs         Separa los agregados de la fila cuya versión protege las…
│   │   │   ├── PendingTenantOwnerConfiguration.cs     Una fila por invitación pendiente del Dueño de una organización…
│   │   │   ├── SecurityEventConfiguration.cs          [E3]
│   │   │   ├── AccountRecoveryRequestConfiguration.cs Pedidos globales de «Recuperar mi cuenta», sin RLS ni organización
│   │   │   ├── IdempotencyKeyConfiguration.cs         Mapea reservas y respuestas reutilizables de peticiones idempotentes
│   │   │   ├── LegalDocumentConfiguration.cs          Mapea las versiones publicadas de términos y privacidad
│   │   │   ├── LegalDocumentContentConfiguration.cs   Mapea el texto de una versión legal por cultura
│   │   │   ├── ReservedTenantSlugConfiguration.cs     direcciones públicas reservadas por la plataforma
│   │   │   ├── TenantDomainConfiguration.cs           Guarda reservas globales de dominio con unicidad y vínculo a…
│   │   │   └── TenantFeatureConfiguration.cs          Habilitaciones globales por espacio, únicas por clave y sin RLS
│   │   ├── Identity/
│   │   │   ├── ApplicationUserConfiguration.cs        [E3] Email y PhoneNumber sin índice único (copia del método principal)
│   │   │   ├── LoginMethodConfiguration.cs            [E3] 3a: índice único (Type, Value)
│   │   │   ├── AccountDeletionCancelTicketConfiguration.cs Mapea la prueba de cancelar baja y sus índices globales
│   │   │   ├── ConsumerDataRetentionConfiguration.cs  Ledger global sin copia personal, único por fila/partes y con índice de pendientes
│   │   │   ├── LoginCodeConfiguration.cs              [E3]
│   │   │   ├── LoginLinkConfiguration.cs              [E8]
│   │   │   ├── LoginInvitationConfiguration.cs         Índice global de invitaciones por destino y vencimiento
│   │   │   ├── MemberAccessRoleConfiguration.cs        Rol reservado por miembro con clave y FK compuestas
│   │   │   ├── UserTenantAccessConfiguration.cs       Mapea el índice global de perfiles de una cuenta
│   │   │   ├── IdentityUserTokenConfiguration.cs      Ubica los tokens técnicos de ASP.NET Identity en el esquema…
│   │   │   ├── IdentityUserLoginConfiguration.cs      Ubica los registros técnicos de proveedores externos de ASP.NET Identity…
│   │   │   ├── IdentityUserClaimConfiguration.cs      Ubica los claims técnicos de ASP.NET Identity en el esquema…
│   │   │   ├── LoginAuditConfiguration.cs             [E3]
│   │   │   └── LegalAcceptanceConfiguration.cs        Mapea qué versión legal aceptó cada cuenta y cuándo
│   │   ├── Tenant/                                    datos privados (RLS)
│   │   │   ├── AuditEntryConfiguration.cs             [E2]
│   │   │   ├── MemberConfiguration.cs                 [E3]
│   │   │   ├── InvitationConfiguration.cs             [E3]
│   │   │   ├── TenantSettingsConfiguration.cs         [E3]
│   │   │   ├── RoleConfiguration.cs                   [E4]
│   │   │   ├── RoleAssignmentConfiguration.cs         [E4]
│   │   │   ├── CompanyConfiguration.cs                [E6]
│   │   │   └── CompanyMembershipConfiguration.cs      [E6]
│   │   └── PublicSite/
│   │       └── PublicPageConfiguration.cs             página pública en public_site, una por organización
│   ├── Interceptors/                                  [E2]
│   │   ├── TenantConnectionInterceptor.cs             set_config('app.tenant_id') al abrir la conexión
│   │   ├── TenantStampInterceptor.cs                  sella y protege las columnas de tenant
│   │   ├── SoftDeleteInterceptor.cs
│   │   ├── AuditableEntityInterceptor.cs
│   │   ├── SystemCurrentUser.cs                       Representa una operación sin usuario autenticado, como el bootstrap de…
│   │   ├── RoleAssignmentTrail.cs                     Agrupa las asignaciones de rol de un guardado
│   │   ├── AuditActorResolver.cs                      Traduce el usuario y acceso actuales al actor que guarda…
│   │   ├── AccessChangeTrail.cs                       Quien entra a un lugar o sale de él deja…
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
│   │   ├── LoginLinkRepository.cs                     [E8]
│   │   ├── LoginAuditRepository.cs                    [E3]
│   │   ├── TenantSettingsRepository.cs                [E3]
│   │   ├── RoleRepository.cs                          [E4]
│   │   ├── RoleAssignmentRepository.cs                [E4]
│   │   ├── PlatformSettingsRepository.cs              [E3]
│   │   ├── PlatformPermissionAssignmentRepository.cs  [E5]
│   │   ├── SecurityEventRepository.cs                 [E3]
│   │   ├── CompanyRepository.cs                       [E6]
│   │   ├── TenantFeatureRepository.cs                 Modifica habilitaciones dentro del único límite del servicio
│   │   ├── TenantAdminLock.cs                         Comparte el lock de asignaciones Dueño entre baja de cuenta…
│   │   ├── PersonalSpaceLock.cs                       Serializa la creación o recuperación del espacio Personal de una…
│   │   ├── PendingTenantOwnerRepository.cs            Agrega la copia técnica dentro de la transacción del alta
│   │   ├── LegalRepository.cs                         Prepara documentos legales vigentes y aceptaciones de una cuenta dentro…
│   │   ├── ExternalLoginLock.cs                       Serializa el vínculo de una cuenta con un subject de…
│   │   ├── CompanyAdminLock.cs                        Adquiere un lock transaccional por empresa antes de tocar sus…
│   │   ├── AccountRecoveryRepository.cs               Pedidos globales; hint sin tracking y lectura fresca FOR NO KEY UPDATE
│   │   ├── AccountDeletionCancelTicketRepository.cs  Persiste la prueba específica por hash en la UoW
│   │   ├── AccountDeletionRepository.cs               Reclama cuentas vencidas con un lease durable y bloquea cada…
│   │   ├── ConsumerDataRetentionRepository.cs          Bloquea una retención con SKIP LOCKED y revalida su vencimiento
│   │   ├── CompanyMembershipRepository.cs             [E6]
│   │   ├── LoginInvitationRepository.cs                Locks y aceptación por OTP conservando RLS forzado
│   │   ├── LoginMethodRepository.cs                   Prepara los métodos de ingreso de una cuenta para altas…
│   │   ├── PlatformTenantRepository.cs                bloquea y modifica el estado global de una organización con versión
│   │   ├── PublicPageRepository.cs                    escribe la página pública dentro de la transacción
│   │   └── TenantDomainRepository.cs                  Prepara altas y cambios de dominios globales dentro de la…
│   ├── Readers/                                       AsNoTracking + proyección a *Row/*Response
│   │   ├── ReferenceDataReader.cs                     [E2] tablas platform + HybridCache p:ref:<catálogo>
│   │   ├── TenantReader.cs                            [E3]
│   │   ├── MemberReader.cs                            [E3] Members ⋈ AspNetUsers (el único camino a la identidad)
│   │   ├── TenantSettingsReader.cs                    [E3] con caché t:
│   │   ├── RoleReader.cs                              [E4]
│   │   ├── PlatformSettingsReader.cs                  [E3] con caché p:
│   │   ├── CompanyReader.cs                           [E6]
│   │   ├── CompanyMembershipReader.cs                 [E6]
│   │   ├── UserTenantAccessReader.cs                  Solo proyecta el índice global de accesos
│   │   ├── TenantFeatureReader.cs                     Lee habilitaciones globales sin seguimiento ni datos privados
│   │   ├── PlatformPermissionAssignmentReader.cs      Proyección de la asignación global vigente
│   │   ├── PlatformAccountReader.cs                   Lee la identidad global, sus métodos, el índice de accesos…
│   │   ├── OrganizationUserReader.cs                  Pagina miembros e invitaciones en SQL y completa roles/empresas solo…
│   │   ├── OrganizationAuditReader.cs                 auditoría de la organización con cursor, filtros y acciones de Plataforma
│   │   ├── MemberSearch.cs                            La búsqueda de personas que comparten Usuarios y Miembros de…
│   │   ├── LoginInvitationReader.cs                    Proyección de invitaciones vigentes para el destino solicitado
│   │   ├── LoginMethodMembershipReader.cs             Comprueba si la cuenta conserva una membresía y una organización…
│   │   ├── LegalReader.cs                             Proyecta la versión legal vigente por cultura y las aceptaciones…
│   │   ├── InvitationReader.cs                        Proyecta la invitación y sus datos actuales únicamente dentro del…
│   │   ├── CompanyAdminReader.cs                      Cuenta solo Administradores de empresa con membresía e identidad activas
│   │   ├── AccountRecoveryReader.cs                   Bandeja global de «Recuperar mi cuenta»
│   │   ├── AccountDeletionTenantReader.cs             Proyecta sus espacios y todas las organizaciones para limpiar invitaciones…
│   │   ├── ConsumerDataRetentionReader.cs             Proyecta hasta 100 pendientes vencidos ordenados por fecha e Id
│   │   ├── LoginMethodReader.cs                       Proyecta los métodos de ingreso de la cuenta solicitada para…
│   │   ├── PlatformReader.cs
│   │   ├── PublicPageReader.cs                        lee la página pública de la organización activa
│   │   ├── PublicSiteReader.cs                        [E7] consulta publicada/activa sin bypass ni caché
│   │   ├── DirectoryReader.cs                         [E7] filtros públicos, búsqueda e índices del directorio
│   │   ├── SecurityEventReader.cs
│   │   ├── TenantDomainReader.cs                      Expone únicamente los dominios activos de la organización técnica seleccionada,…
│   │   ├── TenantProvisioningReader.cs                identifica al Dueño y la primera empresa al reintentar un alta
│   │   └── TenantSlugAvailabilityReader.cs            comprueba reservas y direcciones ya tomadas
│   ├── Migrations/                                    una sola carpeta; RLS y grants dentro de cada migración
│   │   ├── <ts>_InitialSchema.cs                      [E2] esquemas, grants a mt_app, funciones de triggers, unaccent,
│   │   │                                              pg_trgm, public.f_unaccent, platform.IdempotencyKeys y tablas de referencia
│   │   ├── <ts>_IdentityAndTenancy.cs                 [E3] usuarios, tenants, miembros, invitaciones, settings de organización y de plataforma,
│   │   │                                              eventos de seguridad, outbox
│   │   ├── <ts>_OpenIddict.cs                         [E3]
│   │   ├── <ts>_Roles.cs                              [E4]
│   │   ├── <ts>_Platform.cs                           [E5] roles de plataforma
│   │   ├── <ts>_Companies.cs                          [E6]
│   │   ├── 20261006235613_RemoveAccountReauthentication.cs A5: limpia códigos anteriores, migra prueba de cancelar y grants
│   │   ├── 20261006235613_RemoveAccountReauthentication.Designer.cs Modelo generado por EF para A5
│   │   └── ApplicationDbContextModelSnapshot.cs
│   ├── TenantCounters.cs                              Agregados técnicos globales
│   ├── PendingTenantOwner.cs                          Contacto del Dueño invitado al crear una organización desde Plataforma,…
│   ├── DesignTimeApplicationDbContextFactory.cs       Permite generar migraciones sin arrancar la Api ni abrir conexiones
│   ├── Seed/                                          [E1–E2]
│   │   ├── ReferenceData/                             [E1] cinco JSON de salida versionados; no se editan a mano
│   │   │   ├── currencies.json                        [E1] ISO 4217, MinorUnits y traducciones con DisplaySymbol por cultura
│   │   │   ├── countries.json                         [E1] ISO 3166-1, relaciones internas y traducciones
│   │   │   ├── time-zones.json                        [E1] IANA/CLDR, IDs, CountryCodes[] y ciudades traducidas
│   │   │   ├── cultures.json                          [E1] patrones y nombres traducidos; es-AR y en-US habilitadas inicialmente
│   │   │   └── tax-id-types.json                      [E1] normativa fiscal y ValidatorKey
│   │   ├── ReferenceDataSeeder.cs                     [E2] upsert idempotente; nunca borra
│   │   ├── SeedExtensions.cs                          [E3] orden e idempotencia; mismo seed global en una UoW en Development y Production
│   │   │                                              con el advisory lock "seed:" (sin carreras entre réplicas)
│   │   ├── DatabaseSeeder.cs                          [E3] límite global y lock `seed:database`; no crea cuentas ni organizaciones de muestra
│   │   ├── OpenIddictSeeder.cs                        [E3] cliente web + scope api
│   │   ├── PlatformSeeder.cs                          [E3/E8] ajustes + operador inicial (Email, FirstName/LastName; Phone secundario opcional exclusivo), permisos individuales (ADR 0042)
│   │   └── LegalDocumentSeeder.cs                     [E3] 3a: versión base de términos y privacidad, con contenidos es y en
│   ├── AccessIndex/
│   │   ├── LoginInvitation.cs                          Proyección global mínima mantenida por triggers de invitaciones
│   │   ├── MemberAccessRole.cs                         Rol sistémico reservado por miembro, mantenido por triggers
│   │   └── UserTenantAccess.cs                        Proyección técnica global de membresías
│   └── Conventions/
│       ├── EmailConvention.cs                         Persiste el value object Email como su valor canónico en…
│       ├── PhoneNumberConvention.cs                   Persiste el value object PhoneNumber como su E.164 en una…
│       └── VersionedConvention.cs                     Vincula la versión de cada entidad editable a xmin de…
├── Identity/                                          [E2 shell CLR; modelo de cuenta E3]
│   ├── ApplicationUser.cs                             [E2 shell sin mapeo; E3] IsPlatformOperator, Status, Culture, TimeZoneId, FirstName, LastName, DisplayName persistido derivado, LastBusinessTenantId,
│   │                                                  DeletionRequestedAtUtc, DeletionScheduledForUtc, DeletionReason, DeletedAtUtc
│   ├── IdentityRegistration.cs                        Identity core, cookies /account y /connect, DataProtection
│   ├── SignInService.cs
│   ├── UserLookup.cs                                  búsqueda global por valor de método (lista blanca); Plataforma busca cuentas en PlatformAccountReader
│   ├── GoogleAvailability.cs                          [E3] Google se enciende con Authentication:Google:ClientId
│   ├── UserStatusReader.cs                            Lee sólo el estado de una identidad global
│   ├── CloudflareDnsTxtLookup.cs                      Consulta registros TXT públicos mediante el endpoint DNS-over-HTTPS de Cloudflare
│   ├── OpenIddict/
│   │   ├── OpenIddictRegistration.cs                  code + PKCE + refresh, endpoints, validación local
│   │   ├── SubdomainApplicationManager.cs              [E7] preserva clientes/URI fijos y amplía sólo web
│   │   ├── SubdomainRedirectUriValidator.cs            [E7] retorno del propio subdominio publicado, mismo issuer
│   │   ├── AuthServerDefaults.cs                      rutas, scopes, duraciones; issuer fijo (el dominio principal)
│   │   ├── CertificateLoader.cs
│   │   ├── WebClientOptions.cs
│   │   ├── ConnectAuthorizationStore.cs               Crea la autorización OIDC que vincula una sesión con la…
│   │   └── TokenRevoker.cs                            revoca por identidad, por acceso o por organización
│   └── Deletion/
│       ├── AccountDeletionParticipant.cs              El núcleo actual no bloquea ni cambia datos de tenant…
│       ├── EngagementCopyDeletion.cs                  Resuelve tablas compartidas y reemplaza copias manteniendo Parties, incluso SoftDelete
│       ├── EngagementDeletionParticipant.cs           Sexto participante: reemplazo o retención durante el paso Personal
│       ├── LegalAcceptanceDeletionParticipant.cs      Quita los metadatos personales de las aceptaciones legales al completar…
│       ├── MembershipDeletionParticipant.cs           Retira una membresía de la organización durante la baja de…
│       ├── OutboxDeletionParticipant.cs               Cancela avisos pendientes de una cuenta al pedir o ejecutar…
│       ├── PersonalSpaceDeletionParticipant.cs        Cierra el espacio Personal y retira sus ajustes y membresías…
│       └── RecoveryDeletionParticipant.cs             Cierra los pedidos de «Recuperar mi cuenta» de una cuenta…
├── Messaging/                                         [E3]
│   ├── MessagingRegistration.cs
│   ├── Outbox.cs                                      IOutbox
│   ├── OutboxDispatcher.cs                            BackgroundService: sondea y mantiene el scope SMTP del lote
│   ├── OutboxDeliveryProcessor.cs                     Claim confirmado → transporte fuera de UoW → Complete confirmado
│   ├── OutboxDispatchStore.cs                         SKIP LOCKED/lease durable; confirmación con lock y token, sin transporte
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
│       ├── EmailTemplateRenderer.Invitation.cs        correos de invitación, también como operador (lienzo v90)
│       ├── EmailRegistration.cs                       Elige y registra el transporte de correo, los renderizadores y…
│       ├── EmailOptionsValidator.cs                   Valida la configuración general de correo al arrancar, según el…
│       ├── EmailLoginCodeChannel.cs                   Encola el mensaje renderizado dentro de la transacción del caso…
│       ├── EmailInvitationChannel.cs                  Encola el correo de invitación cifrado en la transacción de…
│       ├── EmailAccountNoticeChannel.cs               Prepara los avisos de seguridad y cambios de cuenta para…
│       └── Templates/
│           ├── _Layout.html
│           ├── LoginCode.html                         código de ingreso y código para verificar un correo nuevo (textos VerifyEmail, como en ArquitecturaBase)
│           ├── SignupCode.html
│           ├── Invitation.html
│           ├── LoginMethodChanged.html                método agregado, quitado o principal cambiado; va a todos los métodos (multitenancy.md §3.1)
│           ├── AccountDeletionRequested.html
│           ├── AccountDeletionCancelled.html
│           ├── AccountDeleted.html                    va al método principal y se encola antes de borrar los métodos (multitenancy.md §3.2)
│           └── DataExportReady.html                   [E10] enlace de descarga que vence a las 48 h; solo por correo
├── Modules/                                         [E8]
│   └── WhatsApp/                                    módulo opcional: se enciende con WhatsApp:PhoneNumberId
│       ├── WhatsAppInfrastructureModule.cs          AddWhatsAppModule(cfg): cliente, workers, health, persistencia
│       ├── WhatsAppOptions.cs
│       ├── WhatsAppOptionsValidator.cs
│       ├── WhatsAppTemplateCatalog.cs                16 nombres/32 variantes es/en; clave WhatsApp:Templates:<Nombre>, cantidad/orden y botón
│       ├── WhatsAppTemplateRenderer.cs               variables con DisplayFormatter y perfil explícito; sin persistir contenido
│       ├── WhatsAppChannelSettings.cs                disponibilidad, país real E.164 de altas y ContactPhone público canonicalizado
│       ├── WhatsAppCloudClient.cs                    BCL HttpClient, Graph v25.0, un POST por intento, receipt seguro sin cuerpo del proveedor
│       ├── WhatsAppChannelSender.cs                  emisor del outbox "whatsapp", sin resiliencia ni logs HTTP heredados
│       ├── Webhook/
│       │   ├── WhatsAppSignatureValidator.cs
│       │   ├── WhatsAppWebhookReader.cs
│       │   └── WhatsAppWebhookReplay.cs
│       ├── Inbound/
│       │   ├── WhatsAppInboundSignal.cs
│       │   ├── WhatsAppInboundSettings.cs
│       │   └── WhatsAppInboundProcessor.cs           BackgroundService, SKIP LOCKED
│       ├── Retention/
│       │   └── WhatsAppMessageRetentionService.cs    worker: scopes al inicio/diario, sigue con canal OFF
│       ├── Persistence/
│       │   ├── WhatsAppModelContributor.cs           configura y declara tipos globales identity explícitos, sin depender del interruptor
│       │   ├── WhatsAppContactConfiguration.cs       esquema identity; las configuraciones las aplica el módulo
│       │   ├── WhatsAppMessageConfiguration.cs
│       │   ├── WhatsAppCodeQuotaConfiguration.cs     ledger técnico sin destinatario, UserId ni OTP
│       │   ├── WhatsAppCodeQuotaRepository.cs        lock por número/día, reserva UTC estable dentro de UoW
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
│   ├── InvitationTokenProtector.cs                    Protege los IDs de alcance junto al secreto de 256…
│   └── PayloadProtector.cs                            DataProtection
├── Caching/                                           [E2–E3]
│   ├── CachingRegistration.cs
│   ├── CacheKeys.cs                                   t: / s: / u: / p:
│   ├── HybridCacheExtensions.cs                       [E2] fábrica que lee en scope propio (adaptada de ArquitecturaBase)
│   ├── ReferenceDataCache.cs                          [E2] cinco claves p:ref: e invalidación después del seed
│   ├── TenantSettingsCache.cs                         Invalida las preferencias y el nombre organizacional cacheados por TenantSettingsReader
│   ├── TenantFeatureCache.cs                          cachea las filas globales de módulos de cada espacio
│   ├── RolePermissionCache.cs                         Cachea permisos por organización y rol
│   ├── AccessStatusCache.cs                           Cachea estado de identidad y membresía desde el índice global…
│   ├── TenantStatusCache.cs                           [E3]
│   └── PlatformSettingsCache.cs                       invalida la configuración global cacheada después de guardarla
├── Time/                                              [E1]
│   └── TimeZoneService.cs                             [E1] TimeZoneInfo con IDs IANA
├── ReferenceData/                                     [E1]
│   └── JsonReferenceDataCatalog.cs                    [E1] implementa los cinco puertos desde JSON; E2 se reemplaza por reader
├── Phones/                                            [E1–E3]
│   ├── LibPhoneNumberDisplayFormatter.cs             [E1] libphonenumber para mostrar E.164; sin reglas por país en Application
│   └── LibPhoneNumberParser.cs                        [E3]
├── BackgroundJobs/                                    [E2]
│   ├── BackgroundJobsRegistration.cs
│   ├── InvitationExpiryWorker.cs                      Registra en la auditoría de cada organización activa las invitaciones…
│   ├── AccountRecoveryNoticeWorker.cs                 Encola, fuera de la respuesta de «Recuperar mi cuenta», el…
│   ├── TenantJobRunner.cs                             un scope DI por tenant + Enter; en la E2 recibe los tenantId (se prueba con Widget), y desde la E3
│   │                                                  recorre las organizaciones activas con ITenantReader
│   └── AccountDeletionWorker.cs                       Un scope por reclamo, organización y cierre
├── Features/
│   ├── FeatureCatalog.cs                              Expone el catálogo canónico del producto
│   ├── FeatureService.cs                              Adapta FeatureManagement para el núcleo sin exponer la librería a…
│   ├── FeaturesRegistration.cs                        Registra el filtro scoped y conserva el apagado de emergencia…
│   └── TenantFeatureFilter.cs                         Filtro scoped del espacio firmado, con aislamiento de alcance y…
└── Idempotency/
    ├── IdempotencyCleanupWorker.cs                    Retira reservas vencidas
    ├── IdempotencyKey.cs                              Representa la reserva técnica de una petición idempotente y la…
    └── IdempotencyStore.cs                            Reserva y completa claves idempotentes en transacciones PostgreSQL propias, fuera…
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
│   ├── PendingDeletionCookie.cs                       Protege el estado de baja pendiente en una cookie breve…
│   └── OpenIdPrincipalFactory.cs                      [E3] sub, access, tenant_id, tenant_kind + destinos
├── Authorization/                                     [E4]
│   ├── HasPermissionAttribute.cs
│   ├── HasCompanyPermissionAttribute.cs
│   ├── HasPlatformPermissionAttribute.cs
│   ├── PermissionPolicyProvider.cs                    perm: / cperm: / pperm:
│   ├── PermissionRequirement.cs
│   └── PermissionAuthorizationHandler.cs
├── Tenancy/                                           [E3]
│   ├── TenantResolutionMiddleware.cs
│   ├── AccessAttribute.cs                             [Access(Consumer|Business|Platform)]
│   ├── AccessFilter.cs                                403 Tenancy.Access.Wrong
│   ├── PublicSiteResolutionMiddleware.cs              [E7] subdominio → IPublicSiteContext (solo datos públicos)
│   ├── PublicSiteAttribute.cs                         [E7] [PublicSite]: la ruta necesita un subdominio de empresa publicado
│   ├── PublicSiteContext.cs                           [E7] contexto público nullable, sin modificar tenant privado
│   ├── PublicSiteFilter.cs                            [E7] 404 uniforme si el sitio no está disponible
│   └── TenantClaimTypes.cs
├── RequestContext/                                    [E3]
│   ├── CurrentUser.cs
│   ├── RequestInfo.cs
│   ├── InvitationFlowContext.cs                  Continúa el flujo tras ingreso/F5 sin exponer el token a…
│   └── PublicOrigin.cs
├── Controllers/
│   ├── Auth/                                          [E3]
│   │   ├── ConnectController.cs                       authorize (access=, tenant=), token, logout, userinfo
│   │   ├── SignupController.cs                        POST /api/auth/signup, /verify
│   │   ├── LoginCodeController.cs
│   │   ├── LoginLinkController.cs                    [E8]
│   │   ├── LoginMethodsController.cs                  GET /api/auth/methods [AllowAnonymous]: en E3, correo y Google; WhatsApp aparece en E8 con los países del módulo
│   │   ├── InvitationsController.cs                   [AllowAnonymous] POST /api/invitations/preview (el token, en el cuerpo) y POST /api/invitations/accept
│   │   ├── TenantSlugAvailabilityController.cs        Consulta una dirección pública antes de completar el alta de…
│   │   ├── ContactController.cs                       Correos de ventas y de soporte que muestran Ingreso y…
│   │   ├── AccountDeletionCancelController.cs         Expone sin sesión el estado de baja pendiente desde una…
│   │   ├── ExternalLoginController.cs                 [E3]
│   │   ├── AccountRecoveryController.cs               «Recuperar mi cuenta», sin sesión
│   │   └── BusinessSignupController.cs                [E6] POST /api/auth/business-signup ("Registrá tu empresa")
│   ├── Account/
│   │   ├── MeController.cs                            [E3] GET/PUT /api/me
│   │   ├── LegalAcceptanceController.cs               Recibe la aceptación de documentos vigentes para desbloquear las rutas…
│   │   ├── AccountGoogleController.cs                 Inicia el desafío de Google para vincularlo a la cuenta…
│   │   ├── AccountDeletionController.cs               Solicita la baja de la cuenta actual con un comprobante…
│   │   ├── AccountLoginMethodsController.cs           Expone los métodos de la cuenta y delega su alta,…
│   │   └── LegalController.cs                         Sirve sin sesión la versión vigente de términos y privacidad…
│   ├── ReferenceData/                                [E1]
│   │   └── ReferenceDataController.cs                 GET /api/reference-data y /{catálogo} [AllowAnonymous]; lista de AccessDeclarationTests [E3]
│   ├── Organization/                                  [Access(Business)]; rutas sin prefijo de acceso (el acceso lo declara [Access]); lo de
│   │                                                  la organización entera, bajo api/organization
│   │   ├── RolesController.cs                         [E4] api/roles ← referencia
│   │   ├── PermissionsController.cs                   [E4] api/permissions
│   │   ├── CompaniesController.cs                     [E6] api/companies
│   │   ├── CompanyMembersController.cs                [E6] api/companies/{companyId}/members
│   │   ├── HomeController.cs                          Inicio de la organización activa (ADR 0040)
│   │   ├── CompanyRolesController.cs                  Los roles que se pueden asignar en una empresa, para…
│   │   ├── CompanyCandidatesController.cs             api/companies/{companyId}/candidates: a quién sumar, con el correo tapado
│   │   ├── CompanyInvitationsController.cs            api/companies/{companyId}/invitations: reenviar y revocar las de esa empresa
│   │   ├── OrganizationAuditController.cs             api/organization/audit: listado, «Quién» y detalle
│   │   ├── OrganizationDomainsController.cs           api/organization/settings/domains: registrar, verificar y retirar
│   │   ├── OrganizationSettingsController.cs          api/organization/settings: leer y guardar con versión
│   │   ├── OrganizationUsersController.cs             api/users: miembros e invitaciones
│   │   └── PublicPageController.cs                    api/organization/public-page: borrador, publicar y retirar
│   ├── PublicSite/                                    [E7]
│   │   ├── PublicSiteController.cs                    [PublicSite][AllowAnonymous]: página y logo del subdominio
│   │   └── DirectoryController.cs                     [AllowAnonymous]: directorio y logo por slug
│   ├── Personal/                                      [E7] punteros; módulos B2C añadidos por cada producto
│   └── Platform/                                      [E5] [Access(Platform)]
│       ├── PlatformTenantsController.cs
│       ├── PlatformAccountsController.cs
│       ├── PlatformOperatorsController.cs
│       ├── PlatformAuditController.cs
│       ├── PlatformSettingsController.cs
│       ├── PlatformDomainsController.cs               api/platform/tenants/{id}/domains
│       ├── PlatformFeaturesController.cs              api/platform/tenants/{id}/features
│       ├── PlatformLegalDocumentsController.cs        api/platform/legal-documents
│       ├── PlatformPublicSiteController.cs            api/platform/tenants/{id}/public-site: moderación
│       └── PlatformRecoveriesController.cs            api/platform/recoveries: bandeja Recuperaciones
├── Contracts/                                         entrada HTTP: records sealed, props nullable, ToString() sin PII
│   ├── ReferenceData/                                [E1]
│   │   └── ReferenceDataHttpResponse.cs               cinco catálogos traducidos; ETag
│   ├── Auth/                                          [E3]
│   │   ├── SignupHttpRequest.cs
│   │   ├── VerifySignupHttpRequest.cs
│   │   ├── RequestLoginCodeHttpRequest.cs
│   │   ├── VerifyLoginCodeHttpRequest.cs
│   │   ├── PreviewLoginLinkHttpRequest.cs            [E8] token opaco, sin consumo ni sesión
│   │   ├── RedeemLoginLinkHttpRequest.cs             [E8] token y access consumer explícito
│   │   ├── PreviewInvitationHttpRequest.cs
│   │   ├── GoogleSignupAntiforgeryHttpResponse.cs    Token de formulario de un solo origen para iniciar Registro…
│   │   ├── ExternalLoginQuery.cs                     Opciones del inicio del desafío
│   │   ├── CancelAccountDeletionHttpRequest.cs       Recibe la prueba que permite cancelar una baja pendiente y…
│   │   ├── AuthEmailMapping.cs                       Traduce el correo HTTP al value object sin exponerlo en…
│   │   ├── AccountRecoveryHttpRequests.cs            Email/Phone anterior y nuevo, canal y nonce del navegador
│   │   ├── AcceptInvitationHttpRequest.cs
│   │   ├── AuthContactMapping.cs                     Puente temporal de T01
│   │   └── BusinessSignupHttpRequest.cs               [E6] POST /api/auth/business-signup ("Registrá tu empresa")
│   ├── Account/
│   │   ├── UpdateMeHttpRequest.cs                     [E3]
│   │   ├── VerifyLoginMethodHttpRequest.cs            Recibe el código usado para verificar un método de ingreso…
│   │   ├── RequestAccountDeletionHttpRequest.cs       Recibe el motivo de baja y la prueba de titularidad…
│   │   ├── AddLoginMethodHttpRequest.cs                Recibe el correo que se quiere agregar a la cuenta…
│   │   └── AcceptLegalHttpRequest.cs                  Recibe por HTTP la lista de documentos y versiones que…
│   ├── Organization/
│   │   ├── RolesHttpRequests.cs                       contratos de roles y de los roles asignables en una empresa
│   │   ├── CompaniesHttpRequests.cs                   contratos de empresas; el CUIT, solo el número (D6)
│   │   ├── CompanyMembersHttpRequests.cs              contratos de miembros de empresa (entrada y salida)
│   │   ├── OrganizationAuditHttpQuery.cs              filtros y cursor de la auditoría de la organización
│   │   ├── OrganizationDomainsHttpRequests.cs         contrato para registrar un dominio de correo
│   │   ├── OrganizationSettingsHttpRequests.cs        contrato de la configuración de la organización
│   │   ├── OrganizationUsersHttpRequests.cs           contratos de Usuarios e invitaciones (entrada y salida)
│   │   └── PublicPageHttpRequests.cs                  contratos de la página pública (entrada y salida)
│   ├── PublicSite/                                    [E7]
│   │   ├── PublicSiteHttpResponse.cs                  correo legible y teléfono E.164, sin ids ni estado
│   │   └── DirectoryQuery.cs                          page, pageSize, sort y search
│   ├── Platform/                                      [E5]
│   │   ├── PlatformAccountHttpRequests.cs             filtros de Cuentas y pedidos de una acción con motivo (las de cuenta, sin versión)
│   │   ├── PlatformAuditQuery.cs                      filtros de la auditoría de Plataforma
│   │   ├── PlatformTenantHistoryQuery.cs              cursor after/limit del historial global de una organización
│   │   ├── PlatformOperatorHttpRequests.cs            invitar un operador y cambiar sus permisos
│   │   ├── PlatformRecoveryHttpRequests.cs            filtros de la bandeja Recuperaciones
│   │   ├── PlatformSettingsHttpRequest.cs             Los dos correos son obligatorios
│   │   ├── PlatformTenantHttpRequests.cs              filtros, alta, estado y reintento de organizaciones
│   │   ├── PublishLegalDocumentHttpRequest.cs         Una versión nueva
│   │   └── UpdatePlatformFeatureHttpRequest.cs        cambio de un módulo con motivo y versión
│   └── Common/
│       └── PhoneInputHttpRequest.cs                   País elegido y número escrito
├── ErrorHandling/                                     [E1]
│   ├── ApiErrorCodes.cs
│   ├── ProblemDetailsMapper.cs
│   ├── ControllerResultExtensions.cs                  ToActionResult / ToCreatedResult / ToAcceptedResult
│   ├── GlobalExceptionHandler.cs
│   ├── MvcInvalidModelStateResponseFactory.cs
│   ├── RetryAfterHeaderFormatter.cs                   Convierte segundos enteros al valor invariante del header HTTP Retry-After
│   └── EmptyJsonBodyContentTypeFilter.cs
├── Json/                                              [E1]
│   ├── JsonConfiguration.cs                           un solo ConfigureJson
│   ├── UtcDateTimeConverter.cs                        ISO con Z; rechaza sin offset
│   ├── DateOnlyConverter.cs                           yyyy-MM-dd
│   ├── TimeOnlyConverter.cs                           HH:mm:ss
│   ├── EmailJsonConverter.cs                          Usa el value object Email al deserializar para entregar al…
│   ├── MoneyJsonConverter.cs                          { amount, currency }
│   ├── NormalizedStringJsonConverter.cs               Limpia todo string de entrada antes de entregarlo al contrato…
│   └── RawTextAttribute.cs                            Preserva literalmente un token, código o texto firmado al deserializar
├── Localization/
│   └── LocalizationExtensions.cs                      [E1]
├── OpenApi/                                           [E1]
│   ├── OpenApiExtensions.cs
│   ├── ProblemResponsesConvention.cs
│   ├── OwnProtocolAttribute.cs                   Marca navegaciones de protocolo externo que declaran sus respuestas a…
│   ├── IHttpContractContributor.cs               Metadatos aportados por núcleo o módulos sin dependencias desde el…
│   ├── IdentityContractContributor.cs            Contrato de identidad E8, aditivo respecto de correo y separado…
│   ├── HttpContractDocumentation.cs              Forma común de ejemplos, catálogo de errores y headers
│   ├── ResponseVersionAttribute.cs               Ruta de propiedades CLR hasta la versión uint del éxito…
│   └── ProducesProblemAttribute.cs
├── RateLimiting/                                      [E3]
│   ├── RateLimitingExtensions.cs
│   ├── RateLimitingOptions.cs
│   └── RateLimitPolicies.cs                           por IP: login-code, login-verify, invitation-accept, whatsapp-webhook; por organización: tenant-api
├── Modules/                                         [E8]
│   └── WhatsApp/
│       ├── WhatsAppApiModule.cs                      AddWhatsAppModule(): convención de rutas y rate limit del webhook
│       ├── WhatsAppContractContributor.cs             exporta capacidad instalada sin secretos
│       ├── Controllers/WhatsAppWebhookController.cs   GET/POST /webhooks/whatsapp
│       ├── Contracts/                                request/query T01
│       ├── Filters/WhatsAppWebhookFilter.cs           firma/bytes reales antes del binder
│       └── RateLimiting/WhatsAppWebhookRateLimitPolicy.cs  429 modular con RetryAfterHeaderFormatter
│       ├── ConditionalWhatsAppRouteConvention.cs
│       └── WhatsAppRouteAttribute.cs
├── Hosting/                                           [E1]
│   ├── ForwardedHeadersExtensions.cs
│   ├── SecurityHeadersExtensions.cs
│   └── SpaExtensions.cs                               BackendPrefixes (lista a mano), iguales en el dominio principal y en cada subdominio;
│                                                      /swagger y /openapi solo en Development (SPA y Api en el mismo origen: sin CORS)
├── Features/
│   ├── DisabledFeatureHandler.cs                      Un módulo apagado responde como una ruta inexistente, sin exponer…
│   ├── InstalledModuleRouteMiddleware.cs              Sin contribuidor disponible, devuelve el 404 común antes de ejecutar…
│   ├── InstalledModuleRouteAttribute.cs               Capacidad técnica global instalada
│   ├── IInstalledModuleRouteAvailability.cs           Cada composición de módulo declara sus rutas técnicas y su…
│   ├── InstalledModuleRouteCategory.cs                Categorías de rutas de módulos instalados
│   └── FeatureGateMiddleware.cs                       Oculta módulos antes de evaluar permisos
├── Idempotency/
│   ├── IdempotencyFilter.cs                           Reserva antes del model binding y guarda el resultado solo…
│   ├── IdempotencyReplayAttribute.cs                  [E8] hook neutral antes de cualquier respuesta cacheada
│   ├── LoginLinkReplayVerifier.cs                    [E8] adapta body/status originales sin registrarlos
│   ├── IdempotencyMode.cs                             Modos de idempotencia y respuestas durables
│   └── IdempotentAttribute.cs                         Aplica el filtro de reserva y replay a cada POST…
└── Legal/
    └── LegalAcceptanceMiddleware.cs                   Bloquea rutas autenticadas mientras falte aceptar documentos vigentes
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
│   │   ├── MoneyTests.cs                              [E1] redondeo AwayFromZero con MinorUnits recibidos, monedas distintas no se suman
│   │   ├── CurrencyCodeTests.cs                       [E1] sintaxis alfa-3; existencia/habilitación en Application
│   │   ├── EmailTests.cs                              [E1]
│   │   ├── TenantSlugTests.cs                         tests de TenantSlug
│   │   ├── CultureCodeTests.cs                        Comprueba el formato y la igualdad canónica de códigos de…
│   │   ├── PhoneNumberTests.cs                        [E1]
│   │   └── TaxIdTests.cs                              tests de TaxId
│   ├── Tenancy/                                       [E2–E3]
│   │   ├── TenantTests.cs                             [E2] transiciones de estado
│   │   ├── MemberTests.cs                             [E3]
│   │   ├── TenantDomainTests.cs                       tests de TenantDomain
│   │   ├── ReservedTenantSlugTests.cs                 tests de ReservedTenantSlug
│   │   ├── PartyPolicyTests.cs                        Comprueba que una acción de datos compartidos la ejecute la…
│   │   └── CompanyTests.cs
│   ├── Authorization/                                 [E4]
│   │   ├── RoleAssignmentTests.cs                     La matriz completa de dónde vale un rol
│   │   └── RoleTests.cs                               [E4]
│   ├── Auditing/
│   │   ├── AuditEntryTests.cs                         Comprueba que una entrada de auditoría conserve actor, espacio, acción…
│   │   └── SecurityEventTests.cs                      Comprueba que los eventos de cuenta conserven el tipo, actor…
│   ├── Authentication/
│   │   ├── LoginAuditTests.cs                         Comprueba que la auditoría de ingreso conserve el resultado y…
│   │   ├── LoginCodeTests.cs                          Comprueba caducidad, intentos y uso único de los códigos
│   │   ├── LoginMethodTests.cs                        Comprueba creación, verificación y selección del método principal
│   ├── Common/
│   │   └── EntityAndValueObjectTests.cs               Comprueba la identidad y la igualdad de entidades y objetos…
│   ├── Features/
│   │   └── TenantFeatureTests.cs                      tests de TenantFeature
│   ├── Invitations/
│   │   └── InvitationTests.cs                         [E3]
│   ├── Legal/
│   │   ├── AccountDeletionCancelTicketTests.cs        Cuenta, retorno, uso único y vencimiento de la prueba
│   │   ├── ConsumerDataRetentionTests.cs              Vencimiento UTC y marca de reemplazo idempotente
│   │   └── LegalDocumentTests.cs                      Comprueba textos por cultura, versiones y aceptaciones legales exactas
│   ├── Messaging/
│   │   └── OutboxMessageTests.cs                      Comprueba el ciclo de un mensaje encolado, los reintentos y…
│   ├── Platform/
│   │   ├── AccountRecoveryRequestTests.cs             El pedido de «Recuperar mi cuenta» nace pendiente, con el…
│   │   ├── PlatformPermissionAssignmentTests.cs       Permisos individuales de un operador
│   │   └── PlatformTenantTests.cs                     tests de PlatformTenant
│   ├── PublicSite/
│   │   └── PublicPageTests.cs                         tests de PublicPage
│   ├── ReferenceData/
│   │   └── ReferenceEntitiesTests.cs                  Comprueba que los catálogos conserven claves naturales, traducciones y asociaciones…
│   └── Settings/
│       └── PlatformSettingsTests.cs                   Comprueba las reglas de registro y los valores de configuración…
│
├── ArquitecturaBaseMultitenant.Application.UnitTests/ [E0]
│   ├── ArquitecturaBaseMultitenant.Application.UnitTests.csproj
│   ├── Common/                                        [E1]
│   │   ├── RequestValidatorTests.cs
│   │   ├── PagedRequestValidatorTests.cs
│   │   ├── PersonalDataMaskTests.cs                   Un solo formato para correos y teléfonos enmascarados, el de…
│   │   ├── PaginationContractsTests.cs                Comprueba el cálculo de navegación, totales y páginas del contrato…
│   │   ├── OperationLogTests.cs                       Comprueba los registros de inicio, duración y resultado de una…
│   │   ├── FormatCasesContractTests.cs                Comprueba la estructura y cobertura del catálogo común de ejemplos…
│   │   ├── CursorRequestValidatorTests.cs             Comprueba tamaño de página y formato del cursor recibido
│   │   ├── CultureProfilesTests.cs                    Comprueba los perfiles de presentación obtenidos del catálogo y su…
│   │   ├── DisplayFormatterTests.cs                   [E1] recorre docs/contracts/format-cases.json
│   │   ├── LibPhoneNumberParserTests.cs               parseo de números argentinos y validación de celular
│   │   └── TextNormalizerTests.cs                     Comprueba limpieza, espacios, normalización Unicode y caracteres invisibles
│   ├── ReferenceData/                                 [E1]
│   │   ├── ReferenceDataCatalogTests.cs               JSON válidos, referencias internas y traducciones
│   │   ├── ReferenceDataServiceTests.cs               Comprueba composición, traducciones y búsqueda de los cinco catálogos
│   │   └── ReferenceDataValidationTests.cs            existencia/IsEnabled y MinorUnits desde Application
│   ├── Resources/                                     [E1]
│   │   ├── ResourceParityTests.cs                     claves y placeholders es = en
│   │   ├── ErrorTextsTests.cs
│   │   ├── RoleTextsTests.cs                          tests de RoleTexts
│   │   └── PermissionTextsTests.cs                    [E4]
│   ├── Services/                                      [E3]
│   │   ├── Auth/                                      [E3]
│   │   │   ├── LoginLinkServiceTests.cs              [E8]
│   │   │   ├── ConnectServiceTests.cs
│   │   │   ├── ConnectLogoutServiceTests.cs          [E3]
│   │   │   ├── TenantSlugAvailabilityServiceTests.cs  tests de TenantSlugAvailabilityService
│   │   │   ├── SignupValidationTests.cs               Comprueba correo, código y aceptación de términos en los pasos…
│   │   │   ├── ReturnUrlsTests.cs                     Comprueba que acceso y organización se obtengan únicamente de un…
│   │   │   ├── LoginCodeValidationTests.cs            Comprueba los datos de solicitud y verificación del código de…
│   │   │   ├── LoginCodeFlowTests.cs                  Comprueba orden de locks, lecturas, emisión y verificación de códigos
│   │   │   ├── AccessSwitchPolicyTests.cs
│   │   │   ├── LoginMethodsCapabilitiesTests.cs      tests de LoginMethodsCapabilities
│   │   │   ├── LoginLinkIssuerTests.cs               tests de LoginLinkIssuer
│   │   │   ├── LoginCodeAttemptVerifierTests.cs      tests de LoginCodeAttemptVerifier
│   │   │   └── TenantProvisionerTests.cs              idempotencia
│   │   ├── Invitations/
│   │   │   ├── InvitationDeliveryDestinationTests.cs  tests de InvitationDeliveryDestination
│   │   │   └── InvitationServiceTests.cs              [E3]
│   │   ├── Profile/
│   │   │   └── ProfileServiceTests.cs                 [E3]
│   │   ├── Roles/                                     [E4]
│   │   │   ├── RoleServiceTests.cs
│   │   │   ├── PlatformPermissionServiceTests.cs tests de PlatformPermissionService
│   │   │   └── RoleServiceWriteTests.cs
│   │   ├── Companies/                                 [E6]
│   │   │   ├── CompanyServiceTests.cs
│   │   │   └── LastCompanyAdminGuardTests.cs          tests de LastCompanyAdminGuard
│   │   ├── Platform/                                  [E5]
│   │   │   ├── PlatformScopeTests.cs                  tests de PlatformScope
│   │   │   ├── PlatformSettingsServiceTests.cs        tests de PlatformSettingsService
│   │   │   └── PlatformTenantSlugTests.cs             tests de PlatformTenantSlug
│   │   ├── Home/
│   │   │   └── PendingItemOrderTests.cs               orden de «Para resolver» (ADR 0040)
│   │   ├── OrganizationUsers/
│   │   │   └── OrganizationUserStatePolicyTests.cs    tests de OrganizationUserStatePolicy
│   │   └── PublicSite/
│   │   │   └── PublicPageValidationTests.cs           Cada regla del pedido de Página pública muestra su propio…
│   ├── Modules/WhatsApp/                             [E8]
│   │   ├── WhatsAppChannelsTests.cs                  capacidades, avisos y selección de destino
│   │   ├── WhatsAppBotTests.cs                       reply/operador, marca y límites del bot
│   │   └── WhatsAppCodeQuotaTests.cs                 admisión diaria dentro de UoW del emisor
│   ├── Formatting/                                    Versiones para URLs y validadores HTTP
│   │   └── VersionWireFormatterTests.cs               Dígitos ASCII invariantes para versiones en distintas culturas
│   ├── DependencyInjectionTests.cs                    AddApplication registra cada clase de Services una sola vez y…
│   ├── TestDoubles/                                   [E1–E6] cada fake nace con la interfaz que implementa
│   │   ├── FakeUnitOfWork.cs                          [E2] misma CommitPolicy; cuenta commits y rollbacks
│   │   ├── IdentityTestStore.cs                       [E8] doble de identidad global y locks para la consolidación y el contrato
│   │   ├── FakeTenantContext.cs                       [E2]
│   │   └── ServiceFixture.cs                          [E1] FakeTimeProvider + FakeLogger + RequestValidator real
│   ├── Identity/
│   │   ├── AccountDeletionPolicyTests.cs              Comprueba solicitud, cancelación y vencimiento de la baja
│   │   ├── GoogleNameRulesTests.cs                    Compatibilidad de nombres Google, límites y anonimización (A7)
│   │   └── ApplicationUserRulesTests.cs               Comprueba las reglas puras de la cuenta Identity
│   ├── Messaging/
│   │   └── ChannelContractsTests.cs                   Comprueba los contratos de canales y avisos de cuenta
│   └── Persistence/
│       └── FakeUnitOfWorkTests.cs                     Comprueba que el doble transaccional reproduzca las políticas de commit…
│
├── ArquitecturaBaseMultitenant.Api.IntegrationTests/  [E0]
│   ├── ArquitecturaBaseMultitenant.Api.IntegrationTests.csproj
│   ├── Support/                                       [E0–E3]
│   │   ├── ApiFactory.cs                              [E0] WebApplicationFactory; desde la E2, Testcontainers postgres:18.x (base ICU es-AR, con mt_app real)
│   │   ├── ApiTestGroup.cs                            colección única, en serie
│   │   ├── TenantFixture.cs                           [E2] en la E2, los tenantId de Empresa A, Empresa B y los espacios personales, para RLS; en la E3 suma
│   │   │                                              los usuarios Ana, Kevin, Beto y Carla
│   │   ├── RuntimeRoleConnection.cs                   [E2] consultas crudas como mt_app para probar RLS
│   │   ├── FailingCommitUnitOfWork.cs                 [E2]
│   │   ├── SampleAccountsFixture.cs                   Datos de recorrido creados únicamente en la base Testcontainers del…
│   │   ├── RecoveryJourney.cs                         Registra un pedido de «Recuperar mi cuenta» por la API…
│   │   ├── PlatformOperators.cs                       Convierte una cuenta de prueba en operadora con los permisos…
│   │   ├── PickupFixtureTests.cs                      Comprueba que cada fixture use un pickup temporal propio y…
│   │   ├── PickupCodeReader.cs                        Lee y consume únicamente el código del destinatario desde el…
│   │   ├── OrganizationJourney.cs                     Arma una organización con su Dueño y le suma miembros,…
│   │   ├── DevelopmentHostSetup.cs                    Configura un operador sintético para los tests que necesitan iniciar…
│   │   ├── AccountJourney.cs                          Recorre el registro por correo y obtiene una sesión consumer…
│   │   ├── PlatformJourney.cs                         prepara organizaciones y operadores para los tests de Plataforma
│   │   ├── SavedVersion.cs                            Un guardado con versión responde 200 con la versión que…
│   │   └── TestControllerApplicationPart.cs
│   ├── TestFeatures/                                  [E1] solo para tests, nunca en src/
│   │   ├── TestController.cs                          [E1] devuelve cada ErrorType
│   │   ├── Access/                                   [E3] probe de ruta exclusiva Business, sólo para tests
│   │   │   ├── PublicSiteProbeController.cs           Demuestra independencia entre el sitio público y el acceso privado
│   │   │   └── BusinessOnlyController.cs             [E3] demuestra 403 Consumer sin adelantar alta productiva E6
│   │   ├── Isolation/                                 [E2]
│   │   │   ├── Widget.cs                              ITenantOwned, IAuditable, ISoftDeletable e IVersioned
│   │   │   ├── Poster.cs                              IPublishedByBusiness: la referencia de un dato público
│   │   │   ├── Deal.cs                                IConsumerBusinessShared + PartyPolicy: la referencia de un dato compartido
│   │   │   ├── RetainedSharedRecord.cs                 Copia personal sintética con borrado lógico para probar la purga
│   │   │   ├── IsolationModelCustomizer.cs
│   │   │   ├── IsolationSchema.cs                     [E2] tras las migraciones reales, ApplyAsync(connection) crea estas tablas con las plantillas
│   │   │   │                                          de RlsSql (EnableTenantRls, EnablePublicRls, EnablePartiesRls), sin migraciones productivas
│   │   │   ├── WidgetsController.cs                   [E2] el 409 de ConcurrencyTests; [Access(Business)] desde la E3
│   │   │   ├── PostersController.cs                   [E7] [PublicSite]
│   │   │   ├── DealsController.cs                     [E7] creación pública Consumer y transiciones por PartyPolicy
│   │   │   ├── DealContracts.cs                       [E7] copia mínima, pedidos versionados y listado
│   │   │   ├── DealService.cs                         [E7] UoW real, partes, gates e idempotencia
│   │   │   ├── DealPersistence.cs                     [E7] reader/repositorio compartidos sin leer la cuenta
│   │   │   ├── EngagementFactory.cs                   [E7] API con modelo sintético, RLS e interceptores reales
│   │   │   ├── EngagementJourney.cs                   [E7] sesiones y fixture de publicación/interacción
│   │   │   ├── PersonalWidgetsController.cs           [E7] CRUD Consumer y probe Business
│   │   │   ├── PersonalWidgetContracts.cs             [E7] pedidos paginados y versionados
│   │   │   ├── PersonalWidgetService.cs               [E7] CRUD con la arquitectura de Roles
│   │   │   ├── PersonalWidgetPersistence.cs           [E7] reader/repositorio del espacio personal
│   │   │   ├── PersonalIsolationFactory.cs            [E7] API/modelo propios e infraestructura real
│   │   │   └── PersonalIsolationJourney.cs            [E7] personas y accesos separados de una identidad
│   │   ├── Auditing/
│   │   │   ├── AuditTestDbContext.cs                  Construye el modelo mínimo de entidades sintéticas y entradas de…
│   │   │   ├── AuditedRecord.cs                       Define entidades sintéticas privadas, públicas y compartidas para probar auditoría
│   │   │   ├── TestCurrentUser.cs                     Proporciona usuario y acceso controlados por cada test de auditoría
│   │   │   └── TestTenantContext.cs                   Proporciona el espacio controlado por cada test de auditoría
│   │   ├── Features/
│   │   │   └── FeatureProbeController.cs              rutas de prueba para la puerta de módulos (FeatureGateTests)
│   │   ├── WhatsAppQuotaProbeController.cs            Probe del filtro HTTP real alrededor de una reserva durable,…
│   │   ├── WhatsAppErrorProbeController.cs            Controller de prueba de errores de WhatsApp
│   │   └── Idempotency/
│   │       └── IdempotencyTestController.cs           El único efecto observable de este POST es el contador…
│   ├── Tenancy/                                       [E2] los tests de multitenancy.md §13
│   │   ├── CrossTenantIsolationTests.cs
│   │   ├── RlsBarrierTests.cs
│   │   ├── RlsPolicyInventoryTests.cs
│   │   ├── RuntimeRoleTests.cs
│   │   ├── TenantColumnsImmutabilityTests.cs
│   │   ├── PublicAndSharedRowsTests.cs                datos públicos y compartidos con filtros y RLS
│   │   ├── EngagementTests.cs                         [E7] dos partes, tercero404, transición403 y gates
│   │   ├── PersonalIsolationTests.cs                  [E7] CRUD cruzado, accesos, versión, replay y paginado
│   │   ├── TenantJobRunnerTests.cs                    [E2] un scope DI y un ITenantScope por tenant, sin herencia entre trabajos
│   │   ├── AccessTests.cs                             [E3] acceso equivocado, cambio de acceso con membresía, B2C no crea empresas
│   │   ├── SuspensionTests.cs                         [E3–E5] identidad y organización suspendidas; B2C intacto
│   │   ├── SubdomainTests.cs                          [E7] host/tenant separados, No disponible y protocolo OIDC
│   │   ├── UserTenantAccessReaderTests.cs             Comprueba que el reader liste únicamente las membresías de la…
│   │   ├── UserTenantAccessIndexTests.cs              Comprueba que la proyección global siga los cambios de membresía…
│   │   ├── TenantJobRunnerIdentityTests.cs            Comprueba que los workers recorran empresas activas y entren al…
│   │   ├── TenantContextTests.cs                      Comprueba entrada, anidación y restauración del contexto de espacio
│   │   ├── SuspensionJourneyTests.cs                  Recorre una sesión de empresa suspendida y su refresh con…
│   │   ├── RolePersistenceTests.cs                    Verifica en PostgreSQL real el aislamiento, las referencias y xmin…
│   │   ├── InvitationsHttpTests.cs                    tests de InvitationsHttp
│   │   ├── InvitationAcceptanceTests.cs               Reglas de aceptación con Identity, UoW, outbox y PostgreSQL reales
│   │   ├── IdentityTenancyModelTests.cs               Comprueba que el catálogo de espacios sea global y sus…
│   │   ├── IdentityRepositoryTests.cs                 Comprueba repositorios y readers de membresías y configuración
│   │   ├── HomeIsolationTests.cs                      el inicio no muestra datos de otra organización (ADR 0040)
│   │   ├── ConnectServiceConcurrencyTests.cs          Comprueba que el primer acceso personal simultáneo cree un único…
│   │   ├── CompanyPersistenceTests.cs                 Comprueba la persistencia versionada y el aislamiento real de Companies…
│   │   ├── AccessStatusCacheTests.cs                  Comprueba la caché de estado de cuenta y membresía y…
│   │   ├── CacheKeyScopeTests.cs
│   │   └── InvitationsTests.cs                        puerta de la 3c: InvitationIssuer invita a alguien sin cuenta y a alguien con cuenta, y las dos se aceptan
│   ├── ErrorHandling/                                 [E1]
│   │   ├── ErrorHandlingTests.cs
│   │   ├── CursorErrorMappingTests.cs                 [E2] cursor inválido → 400 con errors.after
│   │   ├── FrameworkErrorsTests.cs
│   │   ├── RateLimiterTests.cs                        Comprueba que el rechazo del rate limiter incluya ProblemDetails y…
│   │   └── ValidationProblemTests.cs
│   ├── Localization/                                  [E1]
│   │   └── LocalizationTests.cs                       [E1]
│   ├── Json/                                          [E1]
│   │   ├── UtcDateTimeTests.cs                        [E1]
│   │   ├── DateOnlyTimeOnlyTests.cs                   [E1]
│   │   ├── EnumAndOffsetJsonTests.cs                  Comprueba que los enums HTTP usen nombres y rechacen números
│   │   ├── EmailJsonTests.cs                          Comprueba que el value object de correo siga viajando como…
│   │   └── MoneyJsonTests.cs                          [E1] { amount, currency }; sintaxis alfa-3 inválida → 400
│   ├── ReferenceData/                                 [E1]
│   │   └── ReferenceDataApiTests.cs                   [AllowAnonymous], ETag, cinco catálogos y rutas por catálogo
│   ├── Hosting/                                       [E0–E1]
│   │   ├── HealthCheckTests.cs                        [E0]
│   │   ├── SpaHostingTests.cs                         [E1]
│   │   ├── ServiceProviderValidationTests.cs          El host de pruebas valida cada registro al construirse, como…
│   │   └── SecurityHeadersTests.cs                    [E1]
│   ├── Contracts/                                     [E1]
│   │   ├── ExplicitRouteInventoryTests.cs             [E1] inventario verbo + ruta
│   │   ├── OpenApiTests.cs                            [E1] cada operación de /api declara su 2xx con esquema y sus errores ProblemDetails; Swagger solo en Development
│   │   ├── SortMapTests.cs                            Comprueba que el orden solicitado use campos permitidos y direcciones…
│   │   ├── Etapa8ContractTests.cs                     Contrato T01
│   │   ├── AssistantErrorContractTests.cs             tests de AssistantErrorContract
│   │   ├── AssistantContractTests.cs                  tests de AssistantContract
│   │   └── OpenApiContractTests.cs                    [E1] openapi.json al día
│   ├── Persistence/                                   [E2]
│   │   ├── WidgetQueryFixture.cs                      [E2] datos Widget y conexión mt_app para búsqueda, orden y cursor
│   │   ├── DbContextModelTests.cs                     [E2] contexto único, DataProtection en platform e Identity sin tablas
│   │   ├── ModelFiltersTests.cs                       [E2] filtros con nombre y clasificación de las tres clases
│   │   ├── ReferenceDataSeederTests.cs                [E2] upsert, segundo arranque, zona compartida y deshabilitación sin borrar
│   │   ├── UnitOfWorkTests.cs
│   │   ├── PaginationTests.cs                         orden estable, página fuera de rango, sort no permitido
│   │   ├── CursorPaginationTests.cs                   sin saltos ni repetidos al insertar mientras se pagina
│   │   ├── SearchTests.cs                             "perez" encuentra "Pérez"; % y _ se escapan
│   │   ├── SortIndexTests.cs                          todo campo de un SortMap tiene índice (TenantId, campo, Id)
│   │   ├── CollationTests.cs                          orden real con ICU es-AR
│   │   ├── AuditingTests.cs
│   │   ├── AuditTrailTests.cs
│   │   ├── SoftDeleteTests.cs
│   │   ├── VersionedModelTests.cs                     Comprueba xmin como token generado de concurrencia y los conversores…
│   │   ├── TenantStampTests.cs                        Comprueba que el interceptor asigne a los datos nuevos el…
│   │   ├── TenantConnectionTests.cs                   Comprueba que una conexión reutilizada reciba el espacio de cada…
│   │   ├── SearchIndexTests.cs                        Cada columna que busca un reader con ApplySearch tiene su…
│   │   ├── RlsSqlTests.cs                             Comprueba las plantillas SQL de aislamiento privado, público y compartido
│   │   ├── ReferenceDataReaderTests.cs                Comprueba la composición de los cinco catálogos desde PostgreSQL
│   │   ├── ReferenceDataModelTests.cs                 Comprueba claves naturales, relaciones y esquema platform de los once…
│   │   ├── PlatformOperatorPersistenceTests.cs        tests de PlatformOperatorPersistence
│   │   ├── PlatformAndLegalRepositoryTests.cs         Comprueba que los repositorios globales exijan transacción al escribir
│   │   ├── OpenIddictMigrationTests.cs                Comprueba tablas y permisos de OpenIddict creados por migración
│   │   ├── IsolationSchemaTests.cs                    Comprueba el esquema sintético usado para probar las tres clases…
│   │   ├── IdentitySupportModelTests.cs               Comprueba el modelo global de configuración, eventos, outbox y legales
│   │   ├── AccountSessionMigrationTests.cs            Migra datos previos, limpia códigos y conserva cancelación
│   │   ├── UserNamesMigrationTests.cs                 Partición de datos anteriores sin rellenar cuentas eliminadas (A7)
│   │   ├── IdentityMigrationTests.cs                  Comprueba el SQL de las migraciones de identidad, legales y…
│   │   ├── BootstrapTests.cs                          Comprueba creación de base, esquemas y collation ICU es-AR
│   │   ├── AuditTrailRlsTests.cs                      Comprueba las políticas RLS del rastro de auditoría y su…
│   │   ├── AuditAndIdempotencyModelTests.cs           Comprueba esquemas, claves y columnas del rastro de auditoría y…
│   │   ├── AdvisoryLockKeysTests.cs                   Comprueba el formato estable de claves de locks por espacio,…
│   │   ├── MigrationsTests.cs                         falta una migración → falla
│   │   └── ConcurrencyTests.cs                        Comprueba que xmin detecte una edición simultánea y preserve el…
│   ├── Auth/                                          [E3]
│   │   ├── SignupTests.cs
│   │   ├── LoginCodeTests.cs
│   │   ├── LoginLinkTests.cs                          [E8]
│   │   ├── ConnectTests.cs
│   │   ├── LogoutTests.cs                            [E3] GET/POST /connect/logout
│   │   ├── LoginCodeTests.cs                           [E3/E6] LoginCodeTests.Inactive_managed_member_cannot_use_the_email_to_sign_into_personal_access bloquea el correo administrado sin membresía activa
│   │   ├── InvitedLoginCodeTests.cs                    Ingreso por código y aceptación de invitaciones vigentes
│   │   ├── LegacyBrowserSessionTests.cs                Recuperación de cookies antiguas conservando la sesión de Plataforma
│   │   ├── StartupConfigurationTests.cs               Comprueba la validación de Google y correo al arrancar
│   │   ├── RealLoginRateLimitTests.cs                 Comprueba los límites de las rutas reales de ingreso por…
│   │   ├── PrincipalFactoryTests.cs                   Comprueba los claims emitidos y reconstruidos durante el refresh
│   │   ├── OpenIddictServerTests.cs                   Comprueba el registro del servidor de autorización local y sus…
│   │   ├── LoginRateLimitTests.cs                     Comprueba que el límite de ingreso responda con el tiempo…
│   │   ├── LoginCodeEnumerationTests.cs               Comprueba que también se prepare el mensaje para una cuenta…
│   │   ├── IngressJourneyTests.cs                     Recorre registro e ingreso reales, cambio entre empresa y espacio…
│   │   ├── GoogleSignupConsentTests.cs                Comprueba que el desafío Google del registro requiera POST con…
│   │   ├── GoogleLoginTests.cs                        Comprueba ingreso y vinculación de Google con persistencia
│   │   ├── GoogleNamesTests.cs                        Claims GivenName/Surname reales, vacíos, versión y guardas (A7)
│   │   ├── GoogleEndpointJourneyTests.cs              Recorre desafío, callback, cookie y emisión de tokens del ingreso…
│   │   ├── BrowserSessionAuthorizationTests.cs        Comprueba el aislamiento de revocaciones entre sesiones, accesos y organizaciones
│   │   ├── PlatformSessionTests.cs                    Cookies separadas y convivencia pública con Plataforma (A3)
│   │   ├── AuthPipelineTests.cs                       Comprueba el orden de autenticación, resolución del espacio y autorización
│   │   ├── AuthEndpointsTests.cs                      Comprueba las puertas HTTP de ingreso y registro
│   │   ├── EmailAccountStatesTests.cs                 Estados de correo, campos, no emisión y límites (A2)
│   │   ├── AccountGoogleProtocolTests.cs              Comprueba la protección del desafío para vincular Google a una…
│   │   ├── AccountRecoveryTests.cs                    «Recuperar mi cuenta» (ADR 0043)
│   │   ├── BusinessSignupTests.cs                     [E6]
│   │   └── MeTests.cs
│   ├── Organization/                                  [E4]
│   │   ├── RolesTests.cs                              [E4]
│   │   ├── PermissionAuthorizationTests.cs            [E4]
│   │   ├── CompaniesTests.cs                          [E6]
│   │   ├── OrganizationDomainsTests.cs                 [E6] TXT, asociaciones de ManagedByTenantId, retención y aislamiento del dominio
│   │   ├── UniqueKeyRaceTests.cs                      Dos pedidos que pasan juntos el control previo chocan con…
│   │   ├── PublicPagesTests.cs                        tests de PublicPages
│   │   ├── OrganizationUsersTests.cs                  Recorre Usuarios e Invitaciones sobre PostgreSQL aislado con identidades y…
│   │   ├── OrganizationSettingsTests.cs               Verifica configuración, autorización y concurrencia con PostgreSQL y una identidad…
│   │   ├── OrganizationAuditTests.cs                  auditoría de la organización: una entrada por acción, «Quién» y el detalle
│   │   ├── MemberStateParityTests.cs                  Paridad del estado de un miembro
│   │   ├── HomeTests.cs                               Inicio de la organización (ADR 0040, tableros Inicio-Org e Inicio-Miembro)
│   │   ├── CompanyAdminTests.cs                       Administrador de empresa: a quién sumar y las invitaciones de su empresa
│   │   └── ForeignKeyRaceTests.cs                     Asignar un rol mientras otro pedido lo borra choca con…
│   ├── PublicSite/                                    [E7]
│   │   ├── PublicSiteTests.cs                         contrato, logo/ETag y 404 uniforme sin borrador privado
│   │   └── DirectoryTests.cs                          filtros, búsqueda, orden, paginado y logos revalidados
│   ├── Platform/                                      [E5]
│   │   ├── PlatformAccountAdministrationTests.cs      Consulta global de cuentas
│   │   ├── PlatformAccountDeletionTests.cs            Baja iniciada por Plataforma (ADR 0035)
│   │   ├── PlatformAccountStateTests.cs               Suspender, reactivar y cerrar sesiones de una cuenta desde Plataforma,…
│   │   ├── PlatformAuditOrganizationsTests.cs         Opciones globales de Auditoría con sus filtros cruzados y permiso audit.read
│   │   ├── PlatformDomainAdministrationTests.cs       tests de PlatformDomainAdministration
│   │   ├── PlatformLegalPublicationTests.cs           Plataforma › Documentos legales
│   │   ├── PlatformOperatorTests.cs                   Operadores de Plataforma (ADR 0042)
│   │   ├── PlatformOrganizationNoticeTests.cs         tests de PlatformOrganizationNotice
│   │   ├── PlatformProvisioningTests.cs               tests de PlatformProvisioning
│   │   ├── PlatformRecoveryTests.cs                   Plataforma › Recuperaciones
│   │   ├── PlatformScopeTests.cs                      tests de PlatformScope
│   │   ├── PlatformSecurityAuditTests.cs              Plataforma › Auditoría
│   │   ├── PlatformSettingsTests.cs                   tests de PlatformSettings
│   │   ├── PlatformTenantAdministrationTests.cs       tests de PlatformTenantAdministration
│   │   ├── PlatformTenantCountersTests.cs             tests de PlatformTenantCounters
│   │   ├── PlatformTenantDetailTests.cs               Dueños vigentes, estado efectivo e historial global por organización
│   │   ├── PlatformTenantProvisioningHttpTests.cs     tests de PlatformTenantProvisioningHttp
│   │   ├── PlatformOrganizationPhoneNoticeTests.cs    tests de PlatformOrganizationPhoneNotice
│   │   └── PublicPageModerationTests.cs               tests de PublicPageModeration
│   ├── Api/
│   │   ├── IdempotencyTests.cs                        Comprueba que pedidos repetidos con la misma clave ejecuten una…
│   │   ├── IdempotencyModeContractTests.cs            tests de IdempotencyModeContract
│   │   └── NormalizedInputTests.cs                    Comprueba la limpieza de texto en el límite HTTP y…
│   ├── Features/
│   │   ├── FeatureGateTests.cs                        tests de FeatureGate
│   │   ├── PlatformFeatureAdministrationTests.cs      tests de PlatformFeatureAdministration
│   │   ├── FeatureAvailabilityPolicyTests.cs          tests de FeatureAvailabilityPolicy
│   │   └── PlatformFeatureCivilExpiryTests.cs         fechas civiles, DST, lectura global y vencimiento UTC histórico
│   ├── Identity/
│   │   ├── AccountSessionActionsTests.cs              Acciones de Cuenta sin código extra, posesión y avisos
│   │   ├── AccountMethodsHttpTests.cs                 Comprueba los contratos HTTP de gestión de métodos de ingreso…
│   │   ├── AccountProfileTests.cs                     Comprueba la edición del perfil usando su versión leída
│   │   ├── AccountProfileNamesTests.cs                Nombres independientes opcionales, Unicode, límites y concurrencia (A6)
│   │   ├── ConcurrentDevelopmentSeedTests.cs          Comprueba que dos ejecuciones simultáneas del seed de muestra creen…
│   │   ├── DevelopmentSeedTests.cs                    Comprueba que la fixture de cuentas de muestra prepare una…
│   │   ├── IdentityModelTests.cs                      Comprueba esquemas, columnas y unicidad del modelo global de identidad
│   │   ├── IdentityRegistrationTests.cs               Comprueba el registro técnico de Identity, cookies y Google
│   │   ├── LoginCodeConcurrencyTests.cs               Comprueba que verificaciones simultáneas consuman un código una sola vez
│   │   ├── LoginMethodsTests.cs                       sumar, verificar, elegir el principal y quitar (con código en otro método); aviso en todos
│   │   ├── LoginPersistenceTests.cs                   Comprueba la persistencia de métodos, códigos, auditoría y preferencias de…
│   │   ├── ProductionSeedTests.cs                     Comprueba el seed de configuración, cliente web y documentos legales…
│   │   ├── IdentityConsolidationTests.cs              tests de IdentityConsolidation
│   │   └── SignInServiceTests.cs                      Comprueba los adaptadores de sesión, usuario actual y datos de…
│   ├── Legal/
│   │   ├── AccountDeletionLoginTests.cs               Comprueba la prueba de ingreso de una cuenta con baja…
│   │   ├── AccountDeletionParticipantsTests.cs        Comprueba que cada participante limpie únicamente los datos de la…
│   │   ├── AccountDeletionProcessingTests.cs          Comprueba el reclamo durable y la reanudación de una baja…
│   │   ├── AccountDeletionTests.cs                    Comprueba sesión, motivo e impedimentos al solicitar una baja
│   │   ├── AccountDeletionSharedTests.cs              Worker real: copia borrada, retención, otro Consumer, soft-delete, lock y lote acotado
│   │   └── LegalAcceptanceTests.cs                    Comprueba que nuevas versiones legales bloqueen las rutas hasta aceptarlas…
│   ├── Messaging/
│   │   ├── AccountNoticeTests.cs                      Comprueba que los avisos lleguen a contactos verificados sin duplicados
│   │   ├── EmailDeliveryTests.cs                      Comprueba configuración segura de SMTP y entrega a pickup
│   │   ├── EmailTemplateTests.cs                      Comprueba textos y estructura de las plantillas de ingreso y…
│   │   ├── OutboxDispatcherScopeTests.cs              Comprueba que un lote pueda reutilizar el sender pero abra…
│   │   ├── OutboxDispatcherTests.cs                   Comprueba despacho, confirmación y reintentos de mensajes pendientes
│   │   ├── OutboxIdleLoggingTests.cs                  Comprueba que un ciclo sin mensajes no genere registros informativos
│   │   ├── OutboxTests.cs                             Comprueba el encolado dentro de la transacción del caso de…
│   │   ├── PickupCodeReaderTests.cs                   tests de PickupCodeReader
│   │   ├── OutboxMigrationTests.cs                    Upgrade de datos históricos
│   │   ├── OutboxLeaseTests.cs                        Reinicio, leases viejos y atomicidad del receipt contra PostgreSQL real
│   │   ├── OutboxDeletionRaceTests.cs                 tests de OutboxDeletionRace
│   │   └── SmtpTransportTests.cs                      Comprueba que dos mensajes del mismo lote compartan una conexión…
│   ├── Security/
│   │   └── LoginCodeSecurityTests.cs                  Comprueba aleatoriedad, longitud y hash ligado a destino y propósito…
│   ├── Time/
│   │   └── TimeZoneServiceTests.cs                    Comprueba rangos UTC de días civiles según una zona IANA
│   └── Modules/WhatsApp/                            [E8] tests de integración del módulo
│       ├── WhatsAppPhoneLinkObserverTests.cs         vínculo/rollback y retiro propio sin robar contacto
│       ├── WhatsAppAccountLifecycleTests.cs          baja por snapshot y aviso final cifrado
│       ├── WhatsAppRetentionTests.cs                 ReceivedAtUtc 89/90/91, fecha externa futura, OFF y lotes
│       ├── WhatsAppBotTests.cs                       webhook firmado→cola→bot, IDs, original owner y ventana
│       ├── WhatsAppWebhookTests.cs                   firma, tamaño, ACK/dedupe, estados y snapshots
│       ├── WhatsAppInboundProcessorTests.cs          leases, crash, clock postlocks y rollback/backoff
│       ├── WhatsAppStartupTests.cs                   opciones y encendido/apagado
│       ├── WhatsAppTemplateTests.cs                  catálogo, variables y avisos
│       ├── WhatsAppCloudClientTests.cs               payload Meta, receipt y un POST por intento
│       ├── WhatsAppOutboxTests.cs                    outbox cifrado y observación de aceptación
│       ├── WhatsAppPersistenceTests.cs               modelo/grants e igualdad de vías
│       ├── WhatsAppChannelSettingsTests.cs           países reales/cambios y número público
│       ├── WhatsAppCodeQuotaPersistenceTests.cs       día UTC, concurrencia y cuota durable
│       ├── WhatsAppErrorLocalizationTests.cs
│       ├── WhatsAppOwnerSeedTests.cs                 Phone del operador sin apropiación
│       ├── WhatsAppIdentityTests.cs                  ingreso/registro y gestión de Phone
│       └── WhatsAppRecoveryTests.cs                  recuperación común y decisión asistida
│
└── ArquitecturaBaseMultitenant.ArchitectureTests/     [E0]
    ├── ArquitecturaBaseMultitenant.ArchitectureTests.csproj
    ├── SolutionRoot.cs
    ├── TransactionBoundaryTests.cs                    [E2] incluye ArchitectureIl para lectura de IL con Mono.Cecil
    ├── HarnessStage.cs                                [E0] lista explícita de las etapas cerradas (ClosedStages)
    ├── HarnessTests.cs                                [E0] punteros por carpeta, enlaces vivos y fichas completas; exige tests al cerrar su etapa según HarnessStage
    ├── LayerDependencyTests.cs                        [E0]
    ├── ProjectReferencesTests.cs                      [E0]
    ├── ApplicationPackagesTests.cs                    [E0] lista blanca de paquetes
    ├── MinimalApiRoutesTests.cs                       [E0] sin Map* de negocio
    ├── ApplicationPublicApiTests.cs                   [E1] sin IQueryable ni Expression
    ├── ErrorCodeTests.cs                              [E1] toda clave de Errors.resx sigue Area.Entidad.Motivo, salvo ReservedKeys (Title.<ErrorType>,
    │                                                  Validation.Failed, las de ApiErrorCodes y Legal.AcceptanceRequired), que deben existir en el resx
    ├── ControllerInputContractTests.cs                [E1]
    ├── ControllerServiceRepositoryTests.cs            [E1] los controllers solo inyectan I*Service
    ├── ApplicationServicesTests.cs                    [E1] cada *Service implementa su interfaz
    ├── ServiceDependencyCountTests.cs                 [E1] ≤ 8 dependencias
    ├── EntityConfigurationTests.cs                    [E2] una configuración por entidad
    ├── DataClassificationTests.cs                     [E2] toda entidad es ITenantOwned, IPublishedByBusiness o IConsumerBusinessShared, o está en la lista de
    │                                                  plataforma o identidad
    ├── DecimalPrecisionTests.cs                       [E1] ningún decimal sin HasPrecision; ningún double o float en entidades
    ├── NoManualFormattingTests.cs                     [E1] sin ToString("N"), ToString("C") ni formatos de fecha fuera de DisplayFormatter
    ├── ReferenceDataHardcodeTests.cs                  [E1] sin listas ISO/cultura/zona escritas en código fuera de JSON
    ├── TenantScopeUsageTests.cs                       [E2] ITenantScope solo en la lista blanca
    ├── QueryFilterBypassTests.cs                      [E2] IgnoreQueryFilters solo en Readers/Platform (salvo IgnoreQueryFilters(["SoftDelete"]))
    ├── IdentityAccessTests.cs                         [E3] ApplicationUser solo desde Identity/, ApplicationDbContext, Configurations/Identity,
    │                                                  MemberConfiguration, InvitationConfiguration, PlatformPermissionAssignmentConfiguration,
    │                                                  UserRepository, MemberReader, PlatformSeeder, Migrations/ y Rls/TenantIsolationModelValidator
    ├── AccessDeclarationTests.cs                      [E3] toda ruta declara [Access] o [PublicSite]; ReferenceDataController
    │                                                  está en la lista explícita [AllowAnonymous] del dominio principal
    ├── ValidationMessageTests.cs                      Exige que cada regla de cada validador de Application tenga…
    ├── UniqueKeysTests.cs                             Los nombres de los índices únicos y de las claves…
    ├── SensitiveToStringLoggingTests.cs               Comprueba que los contratos con datos sensibles oculten sus valores…
    ├── InjectedValidatorTests.cs                      Detecta servicios que construyen o inyectan validadores concretos en vez…
    ├── IdentityStorageAccessTests.cs                  Comprueba que solo los adaptadores autorizados lean los conjuntos globales…
    ├── ControllerOutputContractTests.cs               La salida de un controller (ADR 0002, enmienda del 2026-10-03)
    ├── BuildConfigurationTests.cs                     Comprueba la versión del SDK, los analizadores y las acciones…
    ├── EmailPropertyTests.cs                          Detecta correos expuestos como strings en entidades y modelos internos,…
    ├── IdempotentActionsTests.cs                      Detecta acciones POST que crean o envían sin declarar idempotencia
    ├── ModuleControllersTests.cs                      los controllers opcionales declaran su módulo; el núcleo no tiene
    ├── TextLimitsTests.cs                             Detecta límites de texto repetidos o distintos entre Domain, validación…
    ├── VersionedContractTests.cs                      Comprueba que las ediciones de entidades con versión reciban esa…
    ├── Support/
    │   └── CallSites.cs                               Las llamadas, los tipos nombrados y los literales de texto…
    ├── PersistenceModelComparisonTests.cs             tests de PersistenceModelComparison
    ├── PersistenceModelComparison.cs                  Compara el esquema completo y los metadatos EF que las…
    ├── ApplicationNamespaceShape.cs                   Los módulos conservan la misma forma de Services/Interfaces del núcleo
    ├── AssistantPersistenceModelTests.cs              tests de AssistantPersistenceModel
    ├── AssistantContractTests.cs                      tests de AssistantContract
    ├── AssistantApprovedContractTests.cs              tests de AssistantApprovedContract
    ├── AssistantAiBoundaryTests.cs                    ADR 0048
    ├── ModuleControlProbes.cs                         Clases de control para tests de arquitectura de módulos
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
│   ├── agregar-un-arnes.md                            receta para sumar un arnés ejecutable (scripts/arneses/)
│   ├── leer-backend.md                                cómo seguir un flujo del back, con el ejemplo de vincular Google
│   ├── migracion.md                                   [E2] comando + EnableTenantRls + índices de SortMap
│   ├── prefijo-de-backend.md                          [E1]
│   └── quitar-whatsapp.md                             [E8] borrar Domain/WhatsApp, Modules/WhatsApp en las tres capas y la
│                                                      línea de Program.cs; agregar una migración que borra sus tablas
└── features/<área>.md                                 reglas funcionales de cada área

src/**/<carpeta de un área>/AGENTS.md                  el puntero del arnés (arnes.md §3: qué va, qué no va, fichas, copiá de) más la línea
                                                       "Antes de tocar esto, leé docs/features/<área>.md"; en total, 3 a 8 líneas
src/**/<carpeta de un área>/CLAUDE.md                  @AGENTS.md
```

## Piezas de etapas futuras

Las piezas de los estándares P1 a P10 y de las ADR 0030 a 0035 que ya existen están en el árbol de arriba, en su carpeta real. Acá quedan solo las principales de las etapas que todavía no empezaron. Las piezas de la E7 están en sus carpetas reales arriba; el alcance de la E10 está en el [plan maestro](../plans/2026-09-27-plan-de-desarrollo.md). Rutas completas, para no romper las ramas de arriba.

```
src/ArquitecturaBaseMultitenant.Domain/
└── Legal/DataExport.cs · DataExportStatus.cs                            [E10] platform.DataExports: el archivo de «Exportar mis datos», vence a las 48 h

src/ArquitecturaBaseMultitenant.Application/
├── Interfaces/Services/IDataExportService.cs                            [E10]
├── Interfaces/Persistence/IDataExportRepository.cs                      [E10]
└── Services/Legal/DataExportService.cs                                  [E10]

src/ArquitecturaBaseMultitenant.Infrastructure/
├── Persistence/Configurations/Platform/DataExportConfiguration.cs        [E10]
├── Persistence/Repositories/DataExportRepository.cs                     [E10]
└── BackgroundJobs/DataExportWorker.cs                                   [E10] arma el archivo y lo borra a las 48 h

src/ArquitecturaBaseMultitenant.Api/
└── Controllers/Account/DataExportController.cs                          [E10] POST /api/me/data-export [Idempotent] y la descarga del enlace que vence a las 48 h

tests/
└── *.Api.IntegrationTests/Account/DataExportTests.cs                    [E10]
```
