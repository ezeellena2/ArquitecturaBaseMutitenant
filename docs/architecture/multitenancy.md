# Multitenancy B2B + B2C: identidades, perfiles, organizaciones y aislamiento

> Complementa a [`backend.md`](backend.md). Cambiar una regla de este documento requiere un ADR.

**El flujo de cuenta es como el de Mercado Libre, pero el producto no es un marketplace.** Una persona se registra sola y entra con **su perfil personal** (B2C), que tiene sus propias funcionalidades. Si crea una organización o la invitan a una, con **la misma cuenta** entra también como esa organización (B2B), que tiene otras funcionalidades. Pasa de un perfil a otro con un selector. El núcleo de ArquitecturaBase (identidad, roles, configuración, WhatsApp) queda incluido en este modelo.

## 1. Conceptos

| Concepto | Qué es | En pantalla |
|---|---|---|
| **Identidad** (`identity.AspNetUsers`) | la persona: email, teléfono, cultura, zona y métodos de ingreso. **Global, una por persona** | "Tu cuenta" |
| **Tenant** (`platform.Tenants`) | la unidad de aislamiento. Tiene un `Kind` | nunca se dice "tenant" |
| Tenant `Personal` | el espacio B2C de una identidad. Se crea solo al registrarse, uno por identidad, y su único miembro es el dueño | "Personal" |
| Tenant `Business` | una organización B2B con empresas, miembros y roles | "Organización" (o su nombre) |
| **Membresía** (`tenant.Members`) | el vínculo identidad ↔ tenant, con estado. En un `Personal` hay exactamente una | — |
| **Perfil activo** | el tenant con el que se opera en esta sesión. Viaja en el token | selector "Personal / Acme SA" |
| **Empresa** (`tenant.Companies`) | subdivisión de una organización Business | "Empresa" |
| **Operador** | identidad con `AccountKind=Platform`, sin membresías | "Plataforma" |

```
Identidad (persona)
├─ Perfil Personal   → Tenant Kind=Personal   funcionalidades B2C del producto
├─ Perfil "Acme SA"  → Tenant Kind=Business   empresas, miembros, roles + funcionalidades B2B del producto
└─ Perfil "Beta SRL" → Tenant Kind=Business   (si además es miembro de otra organización)
```

**Los perfiles no comparten datos entre sí.** Lo que una persona hace en su perfil personal no se ve desde su organización, y viceversa. Lo único en común es la identidad: nombre, email, teléfono, cultura y zona.

## 2. Decisiones

| Decisión | Valor |
|---|---|
| Aislamiento | Una base compartida, `TenantId`, filtro de EF con nombre `"Tenant"` y **RLS forzado**. B2B y B2C usan **el mismo mecanismo**: el B2C es un tenant `Personal` |
| Identidad | Global. Email y teléfono únicos en todo el sistema. Una persona, una cuenta |
| Perfiles | Un `Personal` obligatorio y N `Business` por membresía. El límite de organizaciones propias por persona está en `PlatformSettings` |
| Resolución | Solo por el claim `tenant_id` del perfil activo. Nunca de un header, del body, de la query ni del host |
| Cambio de perfil | Tokens nuevos con `/connect/authorize?prompt=none&tenant=<id>`: el servidor valida la membresía y recuerda `LastActiveTenantId` |
| Registro B2C | Autoregistro abierto (código por email o WhatsApp, Google opcional). Se cierra con `PlatformSettings.ConsumerSignup` |
| Alta B2B | "Crear mi organización" desde el perfil personal (`BusinessSignup = Open | RequiresApproval | Closed`), o la crea un operador |
| Funcionalidades | Cada ruta declara para qué perfil es: `[TenantKind(Business)]`, `[TenantKind(Personal)]`, o ninguno si sirve para los dos |

## 3. Estados

```
Personal: Active ──(la plataforma suspende)──► Suspended ──► Active | Closed
Business: PendingApproval ─► Provisioning ─► Active ──► Suspended ──► Active | Closed
```

- Un perfil `Suspended` o `Closed` responde 403 `Tenancy.Tenant.Suspended` y el selector lo muestra deshabilitado.
- Suspender una organización **no** afecta el perfil personal de sus miembros.
- Suspender una **identidad** revoca todas sus sesiones.

## 4. Contexto de tenant

| Pieza | Capa | Rol |
|---|---|---|
| `ICurrentUser` | Application | `UserId` y `AccountKind` (`User` \| `Platform`) |
| `ITenantContext` | Application | `TenantId?`, `TenantKind?` y `RequiredTenantId` |
| `ITenantScope` | Application (`Interfaces/Persistence`) | `IDisposable Enter(Guid tenantId)` |
| `TenantContext` | Infrastructure | holder scoped |
| `TenantResolutionMiddleware` | Api | lee `tenant_id` y `tenant_kind` del token; verifica en caché que la identidad, la membresía y el tenant estén activos |

**Reglas**
1. Con el perfil equivocado, `[TenantKind]` responde 403 `Tenancy.Profile.WrongKind`.
2. En un request de usuario, el tenant se fija una sola vez desde el token. `Enter` sobre ese scope **lanza**.
3. Plataforma, workers y altas entran con `ITenantScope.Enter(tenantId)`: un scope DI por tenant, y nunca con una transacción abierta. Solo lo usan `Services/Platform`, `Services/Auth` (registro), `Services/Organizations` (alta), `Infrastructure/BackgroundJobs`, `Infrastructure/Modules/WhatsApp` y `Seed`. Lo verifica `TenantScopeUsageTests`.

## 5. Barrera 1: EF Core

- **`ITenantOwned`** en toda entidad de negocio, sea B2B o B2C. `TenantStampInterceptor` sella el `TenantId` al insertar y **lanza** si viene otro o si se lo quiere cambiar.
- **Filtro con nombre `"Tenant"`** (`e.TenantId == ctx.TenantId`), aplicado por convención a toda `ITenantOwned`. Sin tenant no devuelve filas (fallo cerrado). Además está `"SoftDelete"`.
- **`TenantIsolationModelValidator`** hace fallar el arranque si una entidad `ITenantOwned` no está en el esquema `tenant`, no tiene el filtro o tiene un índice único que no empieza por `TenantId`.
- `IgnoreQueryFilters(["Tenant"])` solo se permite en la lista blanca de `Infrastructure/Persistence/Readers/Platform`, y lo verifica un test.

## 6. Barrera 2: PostgreSQL RLS

### Roles de base de datos

| Rol | Uso | Atributos |
|---|---|---|
| `mt_owner` | dueño del esquema y migraciones | LOGIN; dueño de las tablas |
| `mt_app` | runtime de la Api | LOGIN, **NOSUPERUSER, NOBYPASSRLS**, no es dueño; DML sobre `platform`, `identity` y `tenant` |

- `RuntimeRoleValidator` aborta el arranque si `mt_app` es privilegiado.
- En Development, `DatabaseBootstrapExtensions` crea `mt_app`. En producción lo crea el DBA.

### Política

Cada tabla de `tenant` la recibe en su migración, con `migrationBuilder.EnableTenantRls("tenant", "<Tabla>")`:

```sql
ALTER TABLE tenant."X" ENABLE ROW LEVEL SECURITY;
ALTER TABLE tenant."X" FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON tenant."X"
  USING      ("TenantId" = nullif(current_setting('app.tenant_id', true), '')::uuid)
  WITH CHECK ("TenantId" = nullif(current_setting('app.tenant_id', true), '')::uuid);
```

Además:
- el trigger `prevent_tenant_change` en cada tabla;
- `prevent_update_delete` en `AuditEntries`.

### Cómo llega el tenant a Postgres

- `TenantConnectionInterceptor` ejecuta `set_config('app.tenant_id', <tenant o ''>, false)` al abrir la conexión.
- `UnitOfWork` lo vuelve a fijar como local a la transacción y verifica que coincida con el contexto.

## 7. Identidad y membresías

- `identity.AspNetUsers` es global: **no tiene `TenantId` ni RLS**. Ningún lector de negocio la consulta directo. "Usuarios de mi organización" parte de `tenant.Members` (con RLS) y hace join a la identidad para traer el nombre y el email. Lo verifica `IdentityAccessTests`.
- La búsqueda global por email o teléfono vive **solo** en `Infrastructure/Identity` (`SignInService`, `UserLookup`).
- Invitar a alguien a una organización:
  - **si ya tiene cuenta**, se crea `Member(Invited)` y, al aceptar, la organización aparece en su selector;
  - **si no tiene cuenta**, al aceptar se crea la identidad **y** su perfil personal.

## 8. Flujos

### Registro B2C
1. `POST /api/auth/signup` con email o teléfono → se envía un código.
2. Al verificar el código, `AccountService.RegisterAsync`:
   - crea la identidad;
   - `Enter(nuevo tenant Personal)`;
   - en una transacción crea `Tenant(Personal, Active)`, `Member(Owner)`, `TenantSettings` (cultura y zona del navegador) y la auditoría.
3. Emite tokens con `tenant_kind=personal`.

### Crear mi organización
`POST /api/me/organizations`, desde cualquier perfil:
1. Crea `Tenant(Business)` en `PendingApproval` o en `Provisioning`, según `BusinessSignup`.
2. `Enter(tenant)` y `TenantProvisioner` (idempotente), en una transacción, crea:
   - `TenantSettings`;
   - los roles de sistema;
   - la primera empresa;
   - `Member(Active)` y TenantAdmin para quien la crea;
   - la auditoría.
3. Pasa a `Active` y aparece en el selector.

Si la crea un operador (`POST /api/platform/tenants`), el provisioning es el mismo, pero el admin inicial recibe una invitación.

### Cambio de perfil
1. `signinSilent({ extraQueryParams: { tenant: id } })` → `/connect/authorize?prompt=none&tenant=<id>`.
2. `ConnectService` valida con `ProfileSwitchPolicy` y guarda `LastActiveTenantId`.
3. Emite tokens nuevos con el `tenant_id` y el `tenant_kind` elegidos, y revoca el refresh token anterior.

Al ingresar se usa `LastActiveTenantId`; si no hay, el perfil personal.

## 9. Plataforma

- `AccountKind=Platform`, sin membresías ni perfil personal. El mismo ingreso, más TOTP obligatorio desde la Etapa 8. Las rutas `api/platform/*` exigen `account_kind=platform`.
- La plataforma gestiona:
  - identidades: suspenderlas;
  - organizaciones: aprobar, suspender y cerrar;
  - perfiles personales: suspenderlos.
- Toda acción sobre un tenant pasa por `Enter`, registra un `SecurityEvent` con el motivo y se audita con `ActorKind=PlatformOperator`.

## 10. Caché, locks y unicidad

- Prefijos de caché:
  - `t:{tenantId}:` para los datos de un tenant;
  - `u:{userId}:` para la identidad (los perfiles del selector);
  - `p:` para la plataforma.
- Locks: `AdvisoryLockKeys.For(tenantId, recurso, id)`.
- Índices únicos: `(TenantId, …)`. Son globales solo el email y el teléfono de la identidad, el `Slug` de la organización y el `PhoneNumberId` de WhatsApp.

## 11. Tests obligatorios de aislamiento

`TenantFixture` arma este escenario:
- Ana, con su perfil personal y admin de la organización A;
- Beto, con su perfil personal y admin de la organización B.

| Test | Qué garantiza |
|---|---|
| `Every_tenant_route_hides_other_tenant_rows` | Con el perfil A nunca aparecen IDs de B, y un GET por id de B da 404 |
| `Personal_profile_is_isolated_from_own_organization` | Ana en su perfil personal no ve datos de A, y en A no ve su perfil personal |
| `Personal_profiles_are_isolated_from_each_other` | El perfil personal de Ana no ve el de Beto |
| `Wrong_profile_kind_is_forbidden` | Una ruta `[TenantKind(Business)]` con el perfil personal devuelve 403, y al revés también |
| `Profile_switch_requires_active_membership` | `tenant=<B>` para Ana es rechazado |
| `Rls_blocks_queries_without_tenant` / `..._even_with_filters_ignored` | La barrera 2 funciona por su cuenta |
| `Every_tenant_table_has_forced_rls_policy` | Inventario de políticas |
| `Runtime_role_is_not_privileged`, `Tenant_id_cannot_change` | Rol de runtime sin privilegios y TenantId inmutable |
| `Suspended_business_does_not_affect_personal_profile` | Suspender A no afecta el perfil personal de Ana |
| `Cache_keys_are_scoped` | Ninguna clave de caché se arma sin su prefijo |
