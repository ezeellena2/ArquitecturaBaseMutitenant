# Multitenancy: accesos B2C y B2B, y las tres clases de datos

**Regla:** una persona tiene **una** cuenta con **dos accesos que no se mezclan**: como persona (`access=consumer`, su espacio personal) y como empresa (`access=business`, una organización). El tenant sale **solo** del claim `tenant_id` del acceso activo. Todo dato se clasifica como **privado**, **público** o **compartido**, y cada clase tiene su esquema, su filtro y su política RLS.

## Cómo se hace
- **Ruta:** declara su acceso:
  - `[Access(Consumer)]` para lo de las personas;
  - `[Access(Business)]` + permiso para la administración de una organización;
  - `[Access(Platform)]` para el backoffice;
  - `[PublicSite][AllowAnonymous]` para la página pública de un subdominio.
- **Dato privado:** `: Entity, ITenantOwned`, esquema `tenant`, `migrationBuilder.EnableTenantRls(Schemas.Tenant, "<Tabla>")`.
- **Dato público:** `IPublishedByBusiness` (`BusinessTenantId`, `IsPublished`), esquema `public_site`, `EnablePublicRls`. Se lee con `IPublicSiteContext` (el subdominio) y solo trae lo publicado.
- **Dato compartido:** `IConsumerBusinessShared` (`ConsumerTenantId` = espacio personal, `BusinessTenantId` = organización), esquema `engagement`, `EnablePartiesRls`.
  - Guarda una **copia** de lo que la otra parte necesita ver.
  - Cada cambio de estado pasa por `PartyPolicy.Require(entity, Party.Consumer|Party.Business)`.
- **El host del subdominio nunca da acceso a datos privados:** solo `IPublicSiteContext`, para lo público.
- **Plataforma, workers y altas:** `using (tenantScope.Enter(tenantId)) { … }` después de autorizar, y antes de abrir el límite.
- **Una persona (B2C) nunca crea una organización:** el alta es "Registrá tu empresa" (`BusinessSignupService`).
- **Caché y locks:** `CacheKeys.Tenant(…)`, `CacheKeys.PublicSite(…)`, `AdvisoryLockKeys.For(tenantId, …)`; en un recurso compartido, lock de fila.

## Prohibido
- Leer el tenant de un header, del body o de la query.
- Usar el subdominio para autorizar datos privados.
- Asignar a mano o cambiar `TenantId`, `BusinessTenantId` o `ConsumerTenantId`.
- `IgnoreQueryFilters` fuera de la lista blanca (`Readers/Platform`).
- Que una empresa lea la cuenta o el espacio personal de una persona: solo ve lo copiado en el dato compartido.
- Una ruta sin `[Access]` ni `[PublicSite]`.
- Responder 403 por un recurso ajeno: es 404.

## Copiá de
- Dato privado: `Domain/Authorization/Role.cs` (E4).
- Datos público y compartido: `TestFeatures/Isolation/Poster.cs` y `Deal.cs` (E2). La plantilla no trae módulos de negocio, así que estos son la referencia.

## Lo verifica
- `TenantIsolationModelValidator` al arrancar: toda entidad está clasificada, con su esquema y su filtro.
- `RlsPolicyInventoryTests`, `RlsBarrierTests`, `RuntimeRoleTests`.
- `CrossTenantIsolationTests`, `PublicAndSharedRowsTests`, `AccessTests` (acceso equivocado, B2C no crea empresas, accesos que no se mezclan).
- `SubdomainTests`: el host resuelve solo lo público; redirect URI solo para slugs publicados.
- `DataClassificationTests`, `TenantScopeUsageTests`, `QueryFilterBypassTests`, `IdentityAccessTests`, `AccessDeclarationTests`, `CacheKeyScopeTests`.

## Detalle
[multitenancy.md](../architecture/multitenancy.md)
