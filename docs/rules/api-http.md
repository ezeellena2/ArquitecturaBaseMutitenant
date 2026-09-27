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
- Cada acción lleva `[Access]` + permiso (o `[PublicSite][AllowAnonymous]` si es de una página pública), `[ProducesResponseType<T>(status)]` y los errores extra con `[ProducesProblem(status)]`.
- Rutas en inglés, plural y kebab-case (`api/companies/{companyId}/members`). Sin versionado (ADR 0005).
- **Prefijo nuevo** (fuera de `/api`): se suma en `BackendPrefixes`, en `SpaHostingTests` y en el proxy de `vite.config.ts` ([guía](../guides/prefijo-de-backend.md), E1).
- **Después de cambiar un contrato:** regenerar `docs/contracts/openapi.json` (lo hace el build) y avisar al front (`npm run contracts`).

## Prohibido
- Recibir modelos de Application como body.
- Lógica, EF o `try/catch` de negocio en un controller.
- `Ok()` o `BadRequest()` a mano con un `Result`.
- Minimal APIs de negocio.
- Mapear la misma combinación verbo + ruta dos veces.

## Copiá de
- `Api/Controllers/Organization/RolesController.cs` y `Api/Contracts/Organization/*Role*` (E4)

## Lo verifica
- `ControllerInputContractTests`, `ControllerServiceRepositoryTests`, `MinimalApiRoutesTests`.
- `ExplicitRouteInventoryTests`: cada ruta con su test.
- `OpenApiContractTests`: openapi.json al día. `OpenApiTests`: esquema de éxito y de errores.

## Detalle
[backend.md §5 y §17](../architecture/backend.md#controller) · ADR 0002
