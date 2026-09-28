# Agregar un prefijo del backend

La Api y el SPA comparten el origen del dominio principal y de cada subdominio. `SpaExtensions.UseSpaFallback` devuelve `index.html` solo para navegaciones que pertenecen al front. Si una ruta nueva del backend queda fuera de `/api`, hay que reservar su prefijo para que una ruta inexistente devuelva 404 con ProblemDetails y no HTML con 200.

## Pasos

1. Definí la ruta HTTP en el controller que corresponda y agregala al inventario de `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Contracts/ExplicitRouteInventoryTests.cs`, con sus pruebas de éxito y error. Para una ruta fuera de `/api`, usá un prefijo estable y específico.
2. Sumá ese prefijo a `BackendPrefixes` en `src/ArquitecturaBaseMultitenant.Api/Hosting/SpaExtensions.cs`. Un prefijo cubre también sus subrutas mediante `PathString.StartsWithSegments`; no reserva rutas parecidas (`/account` no cubre `/accounting`). `/swagger` y `/openapi` siguen reservados para no servir el SPA, pero sus endpoints solo se mapean en Development.
3. Sumá un caso de ruta inexistente bajo ese prefijo a `Backend_prefixes_return_problem_details_instead_of_spa` en `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Hosting/SpaHostingTests.cs`. Comprobá 404, `application/problem+json` y el código `Http.NotFound` en el host principal y en un subdominio.
4. Sumá el mismo prefijo a `backendProxy` en `../ArquitecturaBaseMutitenantFront/vite.config.ts` y a `../ArquitecturaBaseMutitenantFront/src/test/proxy-prefixes.test.ts`. Conservá `changeOrigin: false`: la Api recibe el `Host` original para las rutas públicas. El proxy de `/swagger` y `/openapi` existe solo en modo development.
5. Corré `dotnet test --project tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/ArquitecturaBaseMultitenant.Api.IntegrationTests.csproj -- --filter-class ArquitecturaBaseMultitenant.Api.IntegrationTests.Hosting.SpaHostingTests`, `npm test -- src/test/proxy-prefixes.test.ts` en el front y el build de ambos repos. Si cambió un contrato de `/api`, regenerá también OpenAPI y los tipos del front según `docs/rules/api-http.md`.

`UseSpaFallback` va después de `UseStaticFiles` y de `MapControllers`. No se reemplaza por un endpoint catch-all: eso alteraría los rechazos 405 y 415 de la Api. Un archivo faltante (`/assets/*.js`) tampoco debe devolver `index.html`.
