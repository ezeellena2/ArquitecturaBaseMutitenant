# Multitenancy (perfiles B2B y B2C)

**Regla:** el tenant sale **solo** del claim `tenant_id` del perfil activo. Toda entidad de negocio es `ITenantOwned` y vive en el esquema `tenant`, con RLS. Los perfiles no comparten datos.

## Cómo se hace
- **Entidad:** `: Entity, ITenantOwned` (más `IAuditable` si corresponde). El `TenantId` tiene `private set` y lo sella `TenantStampInterceptor`.
- **Configuración:** `ToTable("<Tabla>", Schemas.Tenant)`; clave e índices únicos empiezan por `TenantId`; FK a otra entidad del tenant, compuesta `(TenantId, XId)`.
- **Migración:** `migrationBuilder.EnableTenantRls(Schemas.Tenant, "<Tabla>")`.
- **Ruta:** `[TenantKind(Business)]` o `[TenantKind(Personal)]`, o ninguno si sirve para los dos.
- **Plataforma, worker o alta:** después de autorizar, `using (tenantScope.Enter(tenantId)) { … }`, y recién después se abre el límite.
- **Caché y locks:** `CacheKeys.Tenant(tenantId, …)` y `AdvisoryLockKeys.For(tenantId, …)`.

## Prohibido
- Leer el tenant de un header, del body, de la query o del host.
- Asignar `TenantId` a mano o cambiarlo.
- `IgnoreQueryFilters(["Tenant"])` fuera de `Readers/Platform`.
- Leer `ApplicationUser` fuera de `Infrastructure/Identity` y `MemberReader`.
- `ITenantScope.Enter` con una transacción abierta, o fuera de la lista blanca.
- Responder 403 por un recurso de otro tenant: es 404.

## Copiá de
- `Domain/Authorization/Role.cs` y `Infrastructure/Persistence/Configurations/Tenant/RoleConfiguration.cs` (E4)
- `tests/…Api.IntegrationTests/Tenancy/CrossTenantIsolationTests.cs` (E2)

## Lo verifica
- `TenantIsolationModelValidator`, al arrancar: esquema, filtro e índices.
- `RlsPolicyInventoryTests`, `RlsBarrierTests`, `RuntimeRoleTests`, `CrossTenantIsolationTests`, `WrongProfileKindTests`.
- `DataClassificationTests`, `TenantScopeUsageTests`, `QueryFilterBypassTests`, `IdentityAccessTests`, `TenantKindDeclarationTests`, `CacheKeyScopeTests`.

## Detalle
[multitenancy.md](../architecture/multitenancy.md)
