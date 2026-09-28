# API HTTP: controllers, contratos, status y OpenAPI

**Regla:** los controllers son finos: contrato → modelo de Application → servicio → `ToActionResult`. Toda entrada es un contrato de `Api/Contracts/<Área>`, y toda respuesta de error es ProblemDetails.

## Cómo se hace
- `public sealed class XController(IXService service) : ControllerBase`, con `[ApiController]`, `[Route("api/<recurso>")]` y `[Tags]`.
- **Entrada:**
  - `[FromBody] XHttpRequest` o `[FromQuery] XQuery` (records sealed con props nullable), mapeados a mano;
  - si lleva datos personales, códigos o tokens, sobrescribe `ToString()`.
- **Salida:**
  - `POST` que crea: `ToCreatedResult(this, nameof(Get), id => new { id })` → 201 con `Location`;
  - `PUT` y `DELETE` → 204;
  - `GET` → 200;
  - un trabajo aceptado → `ToAcceptedResult`.
- Un `POST` que crea o envía lleva `[Idempotent]` ([idempotencia](idempotencia.md)). Una acción de un módulo lleva `[FeatureGate]` ([modulos-habilitados](modulos-habilitados.md)). Un `PUT` o `DELETE` de una entidad `IVersioned` recibe `version` ([concurrencia](concurrencia.md)).
- Cada acción declara su acceso ([multitenancy](multitenancy.md), [permisos](permisos.md)): `[Access(Business)]` + `[HasPermission]` o `[HasCompanyPermission]`; `[Access(Platform)]` + `[HasPlatformPermission]`; `[Access(Consumer)]` sin permiso (la persona tiene implícitos los `personal.*`); `[PublicSite][AllowAnonymous]` solo si responde en el subdominio de una organización publicada (`PublicPageController`); o solo `[AllowAnonymous]` si es anónima del dominio principal (ingreso, registro, "Registrá tu empresa", invitación, enlace, `GET` de documentos legales, directorio, pedido de "Recuperar mi cuenta", cancelar la baja, webhooks), y entonces su controller va en la lista explícita de `AccessDeclarationTests`. Además lleva `[ProducesResponseType<T>(status)]` y los errores extra con `[ProducesProblem(status)]`.
- Rutas en inglés, plural y kebab-case (`api/companies/{companyId}/members`). Sin versionado (ADR 0005).
- **Sin prefijo de acceso:** las rutas de la organización son `api/roles`, `api/permissions`, `api/users`, `api/users/invitations`, `api/companies/{companyId}(/members)`, `api/settings` y `api/public-site`; el acceso lo declara `[Access]`, no la ruta. Las únicas excepciones son `/api/platform/...`, `/api/me/...`, `/api/auth/...` y `/api/invitations/...` (anónimo). "tenant" nunca aparece en una ruta.
- **Prefijo nuevo** (fuera de `/api`): se suma en `BackendPrefixes`, en `SpaHostingTests` y en el proxy de `vite.config.ts` ([guía](../guides/prefijo-de-backend.md), E1).
- **Después de cambiar un contrato:** regenerar `docs/contracts/openapi.json` (lo hace el build) y avisar al front (`npm run contracts`).

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
- `ControllerInputContractTests`, `ControllerServiceRepositoryTests`, `MinimalApiRoutesTests`.
- `AccessDeclarationTests`: toda ruta declara `[Access]` o `[PublicSite]`; una con solo `[AllowAnonymous]` pasa únicamente si su controller está en la lista explícita del test.
- `ExplicitRouteInventoryTests`: cada ruta con su test.
- `OpenApiContractTests`: openapi.json al día. `OpenApiTests`: esquema de éxito y de errores.

## Detalle
[backend.md §5 y §17](../architecture/backend.md#controller) · ADR 0002
