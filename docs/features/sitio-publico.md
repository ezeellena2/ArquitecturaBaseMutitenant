# Sitio público de las organizaciones

Una organización activa puede publicar su página en `<slug>.<host del issuer>`, con el esquema y el puerto de `Authentication:Issuer`. El host resuelve solo el sitio público: no concede acceso a la organización y nunca cambia el `tenant_id` del token. Las reglas generales están en [multitenancy](../rules/multitenancy.md) y [API HTTP](../rules/api-http.md); la edición privada está en [organizaciones](organizaciones.md#página-pública-y-auditoría).

## La página publicada

`GET /api/public-site` lleva `[PublicSite][AllowAnonymous]` y responde únicamente `slug`, `name`, `description`, `logoUrl`, `address`, `phone`, `email` y `website`. Los datos opcionales que la organización no cargó van en `null`. El teléfono viaja en E.164 y el correo en su forma legible, como en la vista previa privada. No se exponen identificadores, versión, estado de publicación ni datos de moderación.

`GET /api/public-site/logo` devuelve los bytes PNG, JPEG o WebP guardados por la organización. Si no hay logo, responde 404 `PublicSite.PublicPage.NotFound`. `logoUrl` es relativo al subdominio y lleva la versión de la página: `/api/public-site/logo?v=<version>`. El `ETag` usa esa misma versión; `If-None-Match` responde 304 sin cuerpo cuando el logo no cambió. La respuesta exige revalidación (`public, max-age=0, must-revalidate`), para que retirar una página o suspender la organización oculte también un logo que el navegador ya vio.

La consulta por host ocurre una vez cuando una ruta `[PublicSite]` la necesita. La lectura de contenido y del logo vuelve a exigir el identificador y el slug resueltos, `IsPublished`, ausencia de `PublishBlockedAtUtc` y organización Business en estado `Active`. Conserva los filtros EF y RLS; no usa `IgnoreQueryFilters`, no entra en un alcance privado y no carga el logo para responder el JSON. La segunda comprobación cubre un retiro o una suspensión después de resolver el host. No hay caché de servidor en esta etapa.

## No disponible

El dominio principal, un slug reservado o desconocido, una página inexistente, en borrador o bloqueada y una organización que no está activa responden el mismo 404 `PublicSite.PublicPage.NotFound`. El cuerpo de ProblemDetails es igual salvo `traceId`; no explica por qué no está disponible. Esto vale también para un miembro de la propia organización: el permiso para leer el borrador privado no lo publica. El front presenta «Esta página no está disponible» e «Ir a ArquitecturaBase».

Una sesión Business de B en el subdominio de A ve solo la publicación de A en estas rutas. Las rutas privadas siguen leyendo B desde el claim; el host nunca sustituye ese acceso. Los servicios de lectura usan `Result` y `OperationLog.RunAsync` sin registrar los datos de contacto.

## El directorio de empresas

`GET /api/directory` alimenta la portada y `/empresas` del dominio principal. `DirectoryController` lleva solo `[AllowAnonymous]` y está en la lista cerrada de `AccessDeclarationTests`. La ruta no requiere resolver un sitio por subdominio ni cambia el acceso de una sesión.

El pedido HTTP `DirectoryQuery` se mapea a `ListDirectoryRequest : PagedRequest`: `page`, `pageSize`, `sort` y `search`. La respuesta es `PagedResult<DirectoryEntryRow>`, con exactamente `slug`, `name`, `description` y `logoUrl` por tarjeta; los opcionales van en `null`. No incluye ids, contacto, versiones, estados ni datos de moderación.

El directorio exige página publicada y sin bloqueo, organización Business activa, slug presente y ausencia en `ReservedTenantSlugs`. Quedan afuera los borradores, las páginas inexistentes o bloqueadas y las organizaciones suspendidas, cerradas, en espera de aprobación o en provisioning. También queda afuera el borrador de la propia organización cuando una sesión Business permite verlo privadamente. El reader usa `AsNoTracking`, conserva el filtro `Public` y RLS y une `PublicPages` con `Tenants`; nunca entra a otro espacio ni usa `IgnoreQueryFilters`.

La búsqueda alcanza solo `PublicPages.Name`, con `ApplySearch`: no distingue acentos ni mayúsculas y trata `%` y `_` como texto. El único orden permitido es `name` o `-name`, por la collation ICU `es-AR`, con desempate por Id. El tamaño por defecto es 10 y admite también 20, 50 y 100. Una página fuera de rango vuelve vacía y conserva el total real. El validador hereda todos los mensajes propios de `PagedRequestValidator`; un orden o paginado inválido responde 400 `Validation.Failed`. El orden cuenta con el índice `(Name, Id)` de `public_site.PublicPages`, y la búsqueda con el GIN trigram sobre `public.f_unaccent(lower("Name"))` y `public.gin_trgm_ops`.

`logoUrl` se arma después de consultar y es relativo al dominio principal: `/api/directory/{slug}/logo?v=<version>`. `GET /api/directory/{slug}/logo` vuelve a exigir los mismos filtros del listado y la existencia de una imagen; responde 404 `PublicSite.PublicPage.NotFound` si no se puede listar la organización o no tiene logo. Devuelve PNG, JPEG o WebP con el mismo `ETag` versionado y revalidación de la página del subdominio. Un logo retirado o de una organización suspendida vuelve a dar 404 aun con `If-None-Match`. No hay caché de servidor en esta etapa.

## Carpetas

| Carpeta | Responsabilidad |
|---|---|
| `src/ArquitecturaBaseMultitenant.Domain/PublicSite` | contenido, publicación, logo y errores de dominio |
| `src/ArquitecturaBaseMultitenant.Application/Models/PublicSite` | borrador privado, proyecciones públicas, pedido paginado y tarjetas del directorio |
| `src/ArquitecturaBaseMultitenant.Application/Services/PublicSite` | edición privada y lectura del sitio resuelto y del directorio |
| `src/ArquitecturaBaseMultitenant.Application/Validation/PublicSite` | edición de la página y paginado del directorio |
| `src/ArquitecturaBaseMultitenant.Api/Contracts/PublicSite` | salida HTTP con correo legible y teléfono E.164 y query del directorio |
| `src/ArquitecturaBaseMultitenant.Api/Controllers/PublicSite` | página y logo del subdominio; directorio y logo por slug en el dominio principal |
| `src/ArquitecturaBaseMultitenant.Api/Tenancy` | resolución del host, contexto y filtro `[PublicSite]` |
| `src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/Readers` | consultas EF explícitas de publicación y estado activo |
| `src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/Configurations/PublicSite` | contenido persistido en `public_site` y clasificación pública |
| `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/PublicSite` | contrato HTTP, logo, estados y aislamiento de la lectura pública |
| `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Tenancy` | host público e independencia del tenant privado |

## Lo verifica

- `PublicSiteTests` (E7): ocho campos públicos exactos, opcionales en null, PNG/JPEG/WebP, ETag y 304, versión nueva del logo, retiro de un logo cacheado, 404 uniforme, borrador del propio miembro y token de B en el sitio de A. También consulta directamente el reader público como dueño, para que el filtro privado nunca convierta el borrador en una publicación.
- `DirectoryTests` (E7): estados excluidos y slugs reservados o ausentes, búsqueda por nombre con acentos y comodines literales, tamaños permitidos y página fuera de rango con 1 y 50 organizaciones, orden `es-AR` y desempate, cuatro campos exactos, logos y 304, retiro o suspensión con un logo cacheado y borrador de la propia sesión Business. Cada caso busca un prefijo único de nombre para que la base compartida con otras clases no altere sus totales.
- `SubdomainTests` (E7): una etiqueta sobre el issuer y su mismo esquema/puerto, slugs reservados, estados, resolución única e independencia del claim; además protocolo OIDC real, salida y recuperación silenciosa sin crear Personal.
- `SecurityHeadersTests` y `SpaHostingTests`: CSP limitada a la recuperación por issuer, mismo origen por host, prefijos técnicos y ausencia de CORS.
- `PublicPagesTests` (E6) y `PublicPageModerationTests` (E5): edición privada, publicación versionada y moderación.
- `ExplicitRouteInventoryTests`, `AccessDeclarationTests`, `ControllerOutputContractTests` y `OpenApiContractTests`: rutas, acceso y contratos.
- `SearchIndexTests` verifica el índice de búsqueda del nombre del directorio; `ValidationMessageTests` verifica los mensajes de cada regla del validador.
