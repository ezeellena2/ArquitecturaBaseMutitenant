# Multitenancy: accesos B2C y B2B, y las tres clases de datos

**Regla:** una persona tiene **una** cuenta con **dos accesos que no se mezclan**: como persona (`access=consumer`, su espacio personal) y como empresa (`access=business`, una organización). El tenant sale **solo** del claim `tenant_id` del acceso activo. Todo dato se clasifica como **privado**, **público** o **compartido**, y cada clase tiene su esquema, su filtro y su política RLS.

## Cómo se hace
- **Ruta:** declara su acceso:
  - `[Access(Consumer)]` para lo de las personas;
  - `[Access(Business)]` + permiso para la administración de una organización;
  - `[Access(Platform)]` para el backoffice;
  - `[PublicSite][AllowAnonymous]` solo para lo que responde en el subdominio de una organización publicada (`PublicPageController`);
  - solo `[AllowAnonymous]` para las rutas anónimas del dominio principal (ingreso, registro, "Registrá tu empresa", invitación, enlace, `GET` de documentos legales, `GET /api/reference-data` y sus cinco rutas por catálogo, directorio, pedido de "Recuperar mi cuenta", cancelar la baja, webhooks), con su controller en la lista explícita de `AccessDeclarationTests`.
- **Dato privado:** `: Entity, ITenantOwned`, esquema `tenant`, `migrationBuilder.EnableTenantRls(Schemas.Tenant, "<Tabla>")`.
- **Dato público:** `IPublishedByBusiness` (`BusinessTenantId`, `IsPublished`), esquema `public_site`, `EnablePublicRls`. Se lee con `IPublicSiteContext` (el subdominio) y solo trae lo publicado.
- **Dato compartido:** `IConsumerBusinessShared` (`ConsumerTenantId` = espacio personal, `BusinessTenantId` = organización), esquema `engagement`, `EnablePartiesRls`.
  - Guarda una **copia** de lo que la otra parte necesita ver.
  - Cada cambio de estado pasa por `PartyPolicy.Require(entity, Party.Consumer|Party.Business)`.
- **Trigger de columnas tenant:** `prevent_tenant_change` recibe los nombres como literales sin comillas dobles dentro (`'TenantId'`, no `'"TenantId"'`). Al corregir un helper de RLS, agregá una migración que recree los triggers ya instalados; cambiar solo el helper no actualiza bases migradas.
- **El host del subdominio nunca da acceso a datos privados:** solo `IPublicSiteContext`, para lo público.
- **Perfiles de una identidad:** `identity.UserTenantAccesses` es la excepción global explícita para elegir acceso y mostrar `/api/me`. Un trigger copia `Member` en su misma transacción; el runtime solo puede leer la proyección por `UserId` y une `platform.Tenants` para nombre y estado. Solo ConnectService, TenantResolutionMiddleware y `/api/me` consumen ese lector; no se ignoran filtros ni se lee `tenant.Members` de otro alcance.
- **Plataforma, workers y altas:** `using (tenantScope.Enter(tenantId)) { … }` después de autorizar, y antes de abrir el límite. En plataforma, además, con motivo obligatorio y el `SecurityEvent` registrado antes de `Enter` (`PlatformActionGuard`).
- **Seed de Development:** `DevelopmentSeeder` entra a cada scope candidato antes de la UoW para comprobar `tenant.Members` bajo RLS; no usa la proyección global de accesos. Es una excepción nominal en `TenantScopeUsageTests`, distinta de la excepción de `DatabaseSeeder` para coordinar las UoW del seed.
- **Una persona (B2C) nunca crea una organización:** el alta es "Registrá tu empresa" (`BusinessSignupService`).
- **Caché y locks:** `CacheKeys.Tenant(…)`, `CacheKeys.PublicSite(…)`, `CacheKeys.User(…)` y `CacheKeys.Platform(…)` producen claves `t:`, `s:`, `u:` y `p:`; los identificadores son GUID en formato `N`. `AdvisoryLockKeys.For(tenantId, recurso, id)` produce `t:{tenantId:N}:lock:{recurso}:{id}` y se toma dentro del límite del caso de uso. En un recurso compartido, lock de fila.

## Prohibido
- Leer el tenant de un header, del body o de la query.
- Usar el subdominio para autorizar datos privados.
- Asignar a mano o cambiar `TenantId`, `BusinessTenantId` o `ConsumerTenantId`.
- Quitar los filtros `"Tenant"`, `"Public"` o `"Parties"` (con `IgnoreQueryFilters()` sin nombres o con alguno de esos nombres) fuera de la lista blanca (`Readers/Platform`). `IgnoreQueryFilters(["SoftDelete"])`, para ver lo borrado, sí se permite ([persistencia-ef](persistencia-ef.md)).
- Que una empresa lea la cuenta o el espacio personal de una persona: solo ve lo copiado en el dato compartido.
- Una ruta sin `[Access]` ni `[PublicSite]`, salvo las anónimas del dominio principal de la lista de `AccessDeclarationTests`.
- `[PublicSite]` en una ruta del dominio principal (`DirectoryController` es `[AllowAnonymous]`).
- "tenant" en una ruta: el acceso lo declara `[Access]`, no la ruta ([api-http](api-http.md)).
- Responder 403 por un recurso ajeno: es 404.

## Copiá de
- Dato privado: `Domain/Authorization/Role.cs` (E4).
- Datos público y compartido: `TestFeatures/Isolation/Poster.cs` y `Deal.cs` (E2). La plantilla no trae módulos de negocio, así que estos son la referencia.

## Lo verifica
- `TenantIsolationModelValidator` (E2) al arrancar: toda entidad está clasificada, con su esquema y su filtro.
- `RlsPolicyInventoryTests` (E2), `RlsBarrierTests` (E2), `RuntimeRoleTests` (E2).
- `CrossTenantIsolationTests` (E2), `PublicAndSharedRowsTests` (E2), `AccessTests` (E3): acceso equivocado, B2C no crea empresas, accesos que no se mezclan.
- `SubdomainTests` (E7): el host resuelve solo lo público; redirect URI solo para slugs publicados.
- `DataClassificationTests` (E2), `TenantScopeUsageTests` (E2), `QueryFilterBypassTests` (E2), `IdentityAccessTests` (E3), `AccessDeclarationTests` (E3) con la lista explícita de controllers anónimos del dominio principal, `CacheKeyScopeTests` (E2).

## Detalle
[multitenancy.md](../architecture/multitenancy.md)
