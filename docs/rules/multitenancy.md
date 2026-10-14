# Multitenancy: accesos B2C y B2B, y las tres clases de datos

**Regla:** una persona tiene **una** cuenta con **dos accesos que no se mezclan**: como persona (`access=consumer`, su espacio personal) y como empresa (`access=business`, una organización). El tenant sale **solo** del claim `tenant_id` del acceso activo. Todo dato se clasifica como **privado**, **público** o **compartido**, y cada clase tiene su esquema, su filtro y su política RLS.

## Cómo se hace
- **Ruta:** declara su acceso:
  - `[Access(Consumer)]` para lo de las personas;
  - `[Access(Business)]` + permiso para la administración de una organización;
  - `[Access(Platform)]` para el backoffice;
  - `[PublicSite][AllowAnonymous]` solo para lo que responde en el subdominio de una organización publicada (`PublicSiteController` (E7));
  - solo `[AllowAnonymous]` para el dominio principal: inventario único en `AccessDeclarationTests`.
- **Dato privado:** `: Entity, ITenantOwned`, esquema `tenant`, `migrationBuilder.EnableTenantRls(Schemas.Tenant, "<Tabla>")`.
- **Dato público:** `IPublishedByBusiness` (`BusinessTenantId`, `IsPublished`), esquema `public_site`, `EnablePublicRls`. Se lee con `IPublicSiteContext`, el del subdominio, y solo trae lo publicado.
- **Dato compartido:** `IConsumerBusinessShared` (`ConsumerTenantId` = espacio personal, `BusinessTenantId` = organización), esquema `engagement`, `EnablePartiesRls`.
  - Guarda una **copia** de lo que la otra parte necesita ver.
  - Cada cambio de estado pasa por `PartyPolicy.Require(entity, Party.Consumer|Party.Business)`: una parte visible equivocada recibe403 `Tenancy.Party.NotAllowed`; un tercero recibe404 antes de mutar.
  - Una creación desde el sitio lleva `[PublicSite][Access(Consumer)][Idempotent]`; los accesos efectivos se intersectan. La contraparte sale del contexto público y su gate, nunca del body. El interceptor conserva el espacio personal del token.
  - La copia personal lleva `[NotAudited]` y `ReplaceConsumerCopy()`; la política legal de Application y su purga están en [Personal](../features/personal.md).
- **Trigger de columnas tenant:** `prevent_tenant_change` recibe los nombres como literales sin comillas dobles dentro (`'TenantId'`, no `'"TenantId"'`). Al corregir un helper de RLS, agregá una migración que recree los triggers ya instalados; cambiar solo el helper no actualiza bases migradas.
- **Plataforma, workers y altas:** `using (tenantScope.Enter(tenantId)) { … }` después de autorizar, y antes de abrir el límite. En plataforma, además, con motivo obligatorio y el `SecurityEvent` registrado antes de `Enter` (`PlatformActionGuard`).
- **Un worker que escribe datos compartidos** (`engagement`) no usa `Enter`, que deja la clase en null y el guardado no sabe qué parte sellar: abre un scope nuevo y llama a `ITenantAccessInitializer.SetFromAccess(tenantId, TenantKind.Business)`, como `RolePermissionCache`. Solo lo hacen `TenantResolutionMiddleware`, `RolePermissionCache` y `WhatsAppInboundProcessor`; uno nuevo se suma a la lista de la guarda.
- **Seed de Development:** siembra los mismos datos globales que Production. No crea membresías, espacios personales ni organizaciones. El E2E prepara sus datos de prueba en una base separada de la base de Development.
- **Una persona (B2C) nunca crea una organización:** el alta es "Registrá tu empresa" (`BusinessSignupService`).
- **Caché y locks:** `CacheKeys.Tenant(…)`, `CacheKeys.PublicSite(…)`, `CacheKeys.User(…)` y `CacheKeys.Platform(…)` producen claves `t:`, `s:`, `u:` y `p:`; los identificadores son GUID en formato `N`. `AdvisoryLockKeys.For(tenantId, recurso, id)` produce `t:{tenantId:N}:lock:{recurso}:{id}` y se toma dentro del límite del caso de uso. En un recurso compartido, lock de fila.

## Prohibido
- Leer el tenant de un header, del body o de la query.
- Usar el subdominio para autorizar datos privados.
- Asignar a mano o cambiar `TenantId`, `BusinessTenantId` o `ConsumerTenantId`.
- Quitar los filtros `"Tenant"`, `"Public"` o `"Parties"` (con `IgnoreQueryFilters()` sin nombres o con alguno de esos nombres) fuera de la lista blanca de `QueryFilterBypassTests` (el namespace `Persistence.Readers.Platform`, hoy vacío, y sólo `"Tenant"` en `LoginInvitationRepository` para el alcance OTP exacto de arriba). `IgnoreQueryFilters(["SoftDelete"])`, para ver lo borrado, sí se permite ([persistencia-ef](persistencia-ef.md)).
- Que una empresa lea la cuenta o el espacio personal de una persona: solo ve lo copiado en el dato compartido.
- Una ruta sin `[Access]` ni `[PublicSite]`, salvo las anónimas del dominio principal de la lista de `AccessDeclarationTests`.
- `[PublicSite]` en una ruta del dominio principal (`DirectoryController` (E7) es `[AllowAnonymous]`).
- "tenant" en una ruta: el acceso lo declara `[Access]`, no la ruta ([api-http](api-http.md)).
- Responder 403 por un recurso ajeno: es 404.

## Copiá de
- Dato privado: `Domain/Authorization/Role.cs` (E4).
- Reserva privada de invitación: `Domain/Invitations/Invitation.cs`, `Domain/Tenancy/Member.cs` y `Infrastructure/Persistence/Configurations/Tenant/InvitationConfiguration.cs` (E3c).
- Datos público y compartido: `TestFeatures/Isolation/Poster.cs` y `Deal.cs` (E2). La plantilla no trae módulos de negocio, así que estos son la referencia.
- Resolución pública: `Api/Tenancy/PublicSiteResolutionMiddleware.cs`, `PublicSiteAttribute.cs` y `PublicSiteFilter.cs`; la consulta es `Infrastructure/Persistence/Readers/PublicSiteReader.cs`.

## Lo verifica
- `DataClassificationTests` (E2), `TenantScopeUsageTests` (E2; desde la E11 también limita `SetFromAccess`), `QueryFilterBypassTests` (E0), `IdentityAccessTests` (E0), `AccessDeclarationTests` (E0)

## Detalle
[multitenancy.md](../architecture/multitenancy.md)
