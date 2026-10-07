# API HTTP: controllers, contratos, status y OpenAPI

**Regla:** los controllers son finos: contrato → modelo de Application → servicio → `ToActionResult`. Toda entrada es un contrato de `Api/Contracts/<Área>`, y toda respuesta de error es ProblemDetails.

## Cómo se hace
- `public sealed class XController(IXService service) : ControllerBase`, con `[ApiController]`, `[Route("api/<recurso>")]` y `[Tags]`.
- **Entrada:**
  - `[FromBody] XHttpRequest` o `[FromQuery] XQuery` (records sealed con props nullable), mapeados a mano;
  - excepción de protocolo: `POST /api/auth/external/google` recibe `[FromForm] ExternalLoginQuery` porque el navegador debe navegar al desafío Google; valida antiforgery antes de iniciarlo. `GET .../google/antiforgery` emite el token para Registro;
  - si lleva datos personales, códigos o tokens, sobrescribe `ToString()`.
  - los enums viajan por su nombre en JSON; un valor numérico se rechaza con 400. Los instantes usan `DateTime` UTC con sufijo `Utc`; `DateTimeOffset` no va en propiedades ni parámetros de contratos HTTP.
- **Salida:**
  - `POST` que crea: `ToCreatedResult(this, nameof(Get), id => new { id })` → 201 con `Location`;
  - `PUT` con `version` (un guardado) → 200 con la versión nueva, `VersionResponse` ([concurrencia](concurrencia.md)); los demás `PUT` y `DELETE` → 204;
  - `GET` → 200;
  - un trabajo aceptado → `ToAcceptedResult`;
  - lo que devuelve es un modelo de Application (`*Response`) o, si la respuesta lleva value objects o es de protocolo, un `*HttpResponse` de `Api/Contracts` (ADR 0002, enmienda del 2026-10-03), nunca una entidad ni un tipo de Infrastructure.
- Un `POST` que crea o envía lleva `[Idempotent]` ([idempotencia](idempotencia.md)). Una acción de un módulo lleva `[FeatureGate]` ([modulos-habilitados](modulos-habilitados.md)). Un `PUT` o `DELETE` de una entidad `IVersioned` recibe `version` ([concurrencia](concurrencia.md)).
- Cada acción declara su acceso ([multitenancy](multitenancy.md), [permisos](permisos.md)): `[Access(Business)]` + `[HasPermission]` o `[HasCompanyPermission]`; `[Access(Platform)]` + `[HasPlatformPermission]`; `[Access(Consumer)]` sin permiso (la persona tiene implícitos los `personal.*`); `[PublicSite][AllowAnonymous]` solo si responde en el subdominio de una organización publicada (`PublicSiteController` (E7)); o solo `[AllowAnonymous]` si es anónima del dominio principal (ingreso, registro, "Registrá tu empresa", invitación, enlace, `GET` de documentos legales, `GET /api/reference-data` y sus rutas por catálogo, directorio, pedido de "Recuperar mi cuenta", cancelar la baja, webhooks), y entonces su controller va en la lista explícita de `AccessDeclarationTests` (E3). Además lleva `[ProducesResponseType<T>(status)]` y los errores extra con `[ProducesProblem(status)]`.
- Rutas en inglés, plural y kebab-case (`api/companies/{companyId}/members`). Sin versionado (ADR 0005).
- `[PublicSite]` también admite `[Access(Consumer)]` para iniciar una interacción desde un subdominio publicado. El espacio personal sale del claim y lo sella el interceptor; la organización contraparte sale de `IPublicSiteContext`, nunca del cuerpo. No admite Business ni Platform. Las transiciones compartidas declaran ambos accesos para llegar a `PartyPolicy`, que exige la parte correspondiente y devuelve 403 `Tenancy.Party.NotAllowed`; lo ajeno sigue siendo 404.
- **Sin prefijo de acceso:** las rutas de la organización son `api/home`, `api/roles`, `api/permissions`, `api/users`, `api/users/invitations` y `api/companies/{companyId}` (con sus `members` y `roles`); lo que es de la organización entera va bajo `api/organization` (`settings`, `settings/domains`, `public-page` y `audit`), que nombra el recurso y no el acceso. El acceso lo declara `[Access]`, no la ruta. Las únicas excepciones son `/api/platform/...`, `/api/me/...`, `/api/auth/...` y `/api/invitations/...` (anónimo). "tenant" nunca aparece en una ruta.
- **Prefijo nuevo** (fuera de `/api`): se suma en `BackendPrefixes`, en `SpaHostingTests` y en el proxy de `vite.config.ts` ([guía](../guides/prefijo-de-backend.md), E1). `/swagger` y `/openapi` están en la lista y en el proxy solo en Development.
- **Protocolo OpenIddict:** `ConnectController` expone `/connect/*` con `[AllowAnonymous]` para delegar la autenticación y los errores del protocolo a OpenIddict; lleva `[OwnProtocol]` y `[ApiExplorerSettings(IgnoreApi = true)]`. Sus verbos y rutas permanecen en `ExplicitRouteInventoryTests` y el controller en la lista cerrada de `AccessDeclarationTests`; no se mezclan con el contrato JSON de negocio de OpenAPI.
- Los controllers de protocolo siguen inyectando un servicio de Application. La excepción técnica cerrada del constructor permite además `OpenIdPrincipalFactory` solo en `ConnectController` e `IAuthenticationSchemeProvider` solo en `ExternalLoginController`; `ControllerServiceRepositoryTests` rechaza cualquier otra dependencia directa.
- **Después de cambiar un contrato:** regenerar `docs/contracts/openapi.json` (lo hace el build) y avisar al front (`npm run contracts`).
- El esquema de `application/problem+json` declara `code` y `traceId` obligatorios, y `errors` (campo → mensajes) y `retryAfter` (segundos) opcionales; es la misma forma que devuelve el mapper HTTP.
- Un rechazo del rate limiter responde 429 con ProblemDetails (`Http.TooManyRequests`, `traceId`, `retryAfter`) y el header `Retry-After` con los mismos segundos enteros.
- **Datos de referencia:** `GET /api/reference-data` y sus rutas por catálogo son `[AllowAnonymous]`, devuelven todas las filas traducidas a `Accept-Language` con `isEnabled` y llevan `ETag` para caché del navegador ([datos-de-referencia](datos-de-referencia.md)). Los selectores filtran las habilitadas; las filas deshabilitadas siguen disponibles para mostrar valores guardados. Sustituyen `GET /api/time-zones`.

## Prohibido
- Recibir modelos de Application como body.
- Lógica, EF o `try/catch` de negocio en un controller.
- `Ok()` o `BadRequest()` a mano con un `Result`.
- Minimal APIs de negocio.
- Mapear la misma combinación verbo + ruta dos veces.
- `[PublicSite]` en una ruta del dominio principal (el directorio, por ejemplo, es `[AllowAnonymous]`).
- "tenant" o un prefijo de acceso (`api/business/...`) en la ruta.

## Copiá de
- `Api/Controllers/Organization/RolesController.cs` y `Api/Contracts/Organization/*Role*` (E4)

## Lo verifica
- `ControllerInputContractTests` (E1), `ControllerServiceRepositoryTests` (E1): reconoce todo tipo derivado de `ControllerBase`, incluso si no termina en `Controller`, y revisa constructor, parámetros de acción y dependencias; prohíbe `DateTimeOffset` también en propiedades anidadas de contratos; `MinimalApiRoutesTests` (E0).
- `ControllerOutputContractTests` (E7): lo que declara cada acción en `ProducesResponseType` es un modelo de Application o un `*HttpResponse` de `Api/Contracts`.
- `EnumAndOffsetJsonTests` (E1): nombres de enum aceptados y números rechazados con 400 ProblemDetails.
- `AccessDeclarationTests` (E3): toda ruta declara `[Access]` o `[PublicSite]`; una con solo `[AllowAnonymous]` pasa únicamente si su controller está en la lista explícita del test.
- `ExplicitRouteInventoryTests` (E1): inventaría cada endpoint de producción salvo health, exige verbo HTTP y que su ruta tenga un prefijo declarado en `BackendPrefixes`.
- `OpenApiContractTests` (E1): openapi.json al día. `OpenApiTests` (E1): esquema de éxito y de errores.
- `ReferenceDataApiTests` (E1): catálogos, traducción, ETag y rutas anónimas.

## Detalle
[backend.md §5 y §17](../architecture/backend.md#controller) · ADR 0002
