# Sitio público de las organizaciones

Una organización activa puede publicar su página en `<slug>.<host del issuer>`, con el esquema y el puerto de `Authentication:Issuer`. El host resuelve solo el sitio público: no concede acceso a la organización y nunca cambia el `tenant_id` del token. Las reglas generales están en [multitenancy](../rules/multitenancy.md) y [API HTTP](../rules/api-http.md); la edición privada está en [organizaciones](organizaciones.md#página-pública-y-auditoría).

## La página publicada

`GET /api/public-site` lleva `[PublicSite][AllowAnonymous]` y responde únicamente `slug`, `name`, `description`, `logoUrl`, `address`, `phone`, `email` y `website`. Los datos opcionales que la organización no cargó van en `null`. El teléfono viaja en E.164 y el correo en su forma legible, como en la vista previa privada. No se exponen identificadores, versión, estado de publicación ni datos de moderación.

`GET /api/public-site/logo` devuelve los bytes PNG, JPEG o WebP guardados por la organización. Si no hay logo, responde 404 `PublicSite.PublicPage.NotFound`. `logoUrl` es relativo al subdominio y lleva la versión de la página: `/api/public-site/logo?v=<version>`. El `ETag` usa esa misma versión; `If-None-Match` responde 304 sin cuerpo cuando el logo no cambió. La respuesta exige revalidación (`public, max-age=0, must-revalidate`), para que retirar una página o suspender la organización oculte también un logo que el navegador ya vio.

La consulta por host ocurre una vez cuando una ruta `[PublicSite]` la necesita. La lectura de contenido y del logo vuelve a exigir el identificador y el slug resueltos, `IsPublished`, ausencia de `PublishBlockedAtUtc` y organización Business en estado `Active`. Conserva los filtros EF y RLS; no usa `IgnoreQueryFilters`, no entra en un alcance privado y no carga el logo para responder el JSON. La segunda comprobación cubre un retiro o una suspensión después de resolver el host. No hay caché de servidor en esta etapa.

## No disponible

El dominio principal, un slug reservado o desconocido, una página inexistente, en borrador o bloqueada y una organización que no está activa responden el mismo 404 `PublicSite.PublicPage.NotFound`. El cuerpo de ProblemDetails es igual salvo `traceId`; no explica por qué no está disponible. Esto vale también para un miembro de la propia organización: el permiso para leer el borrador privado no lo publica. El front presenta «Esta página no está disponible» e «Ir a ArquitecturaBase».

Una sesión Business de B en el subdominio de A ve solo la publicación de A en estas rutas. Las rutas privadas siguen leyendo B desde el claim; el host nunca sustituye ese acceso. Los servicios de lectura usan `Result` y `OperationLog.RunAsync` sin registrar los datos de contacto.

## Carpetas

| Carpeta | Responsabilidad |
|---|---|
| `src/ArquitecturaBaseMultitenant.Domain/PublicSite` | contenido, publicación, logo y errores de dominio |
| `src/ArquitecturaBaseMultitenant.Application/Models/PublicSite` | borrador privado, proyecciones públicas y respuesta del sitio |
| `src/ArquitecturaBaseMultitenant.Application/Services/PublicSite` | edición privada y lectura del sitio resuelto |
| `src/ArquitecturaBaseMultitenant.Api/Contracts/PublicSite` | salida HTTP con correo legible y teléfono E.164 |
| `src/ArquitecturaBaseMultitenant.Api/Controllers/PublicSite` | página y logo del subdominio |
| `src/ArquitecturaBaseMultitenant.Api/Tenancy` | resolución del host, contexto y filtro `[PublicSite]` |
| `src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/Readers` | consultas EF explícitas de publicación y estado activo |
| `src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/Configurations/PublicSite` | contenido persistido en `public_site` y clasificación pública |
| `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/PublicSite` | contrato HTTP, logo, estados y aislamiento de la lectura pública |
| `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Tenancy` | host público e independencia del tenant privado |

## Lo verifica

- `PublicSiteTests` (E7): ocho campos públicos exactos, opcionales en null, PNG/JPEG/WebP, ETag y 304, versión nueva del logo, retiro de un logo cacheado, 404 uniforme, borrador del propio miembro y token de B en el sitio de A. También consulta directamente el reader público como dueño, para que el filtro privado nunca convierta el borrador en una publicación.
- `SubdomainTests` (E7): una sola etiqueta sobre el issuer, dominio y puerto correctos, slugs reservados, estados de la organización, resolución única por pedido e independencia del claim.
- `PublicPagesTests` (E6) y `PublicPageModerationTests` (E5): edición privada, publicación versionada y moderación.
- `ExplicitRouteInventoryTests`, `AccessDeclarationTests`, `ControllerOutputContractTests` y `OpenApiContractTests`: rutas, acceso y contratos.
