# Etapa 7: área B2C, sitio público e interacción — plan ejecutable

> **Para agentes:** plan ejecutable de la Etapa 7. Seguí el orden de cada repo, completá las casillas con evidencia real y commiteá cada cambio coherente y verificado con rutas explícitas (`git add -- <rutas>`, nunca `git add .`). Antes de tocar una carpeta, leé su `AGENTS.md` y las fichas que nombra.

**Estado:** borrador del 2026-10-04, revisado por la Guía. No se programa nada hasta que el usuario resuelva «Para decidir antes de empezar» y apruebe el plan (T00).

**Objetivo:**
- dejar el área personal lista para que cada producto le sume sus módulos B2C con la misma receta que los B2B (`docs/features/personal.md`);
- abrir el sitio público: la página de cada organización publicada en su subdominio, con «No disponible» e «Ingresá para continuar», y el directorio de empresas en el dominio principal (la sección «Empresas en ArquitecturaBase» de la portada y `/empresas`);
- probar de punta a punta la mecánica de interacción persona ↔ empresa con `TestFeatures` (el `Deal` y `PartyPolicy`), sin pantallas de negocio.

**Arquitectura:**
- Domain ← Application ← Infrastructure; Api compone. Las capas se copian de Roles; la página pública, de la E6.
- El subdominio resuelve **solo lo público**. El tenant de una sesión sale siempre del claim `tenant_id`, nunca del host.
- El SPA y la Api se sirven en el mismo origen en cada host, sin CORS. El issuer es fijo: el dominio principal.
- La plantilla no trae módulos de negocio: la mecánica se prueba con `TestFeatures`, que nunca va a `src/`.

**Tecnologías:** .NET 10, EF Core/Npgsql, PostgreSQL 18, OpenIddict 7, Aspire 13.5, React 19, TanStack Query, oidc-client-ts, i18next, Vitest y Playwright.

## Fuentes, restricciones y punto de partida

### Fuentes

- [Plan maestro, Etapa 7](2026-09-27-plan-de-desarrollo.md#etapa-7-área-b2c-sitio-público-e-interacción), con el ☰ ya corregido (`34e0ead`).
- [`backend.md`](../architecture/backend.md) §4, §8, §9 («Paginado, orden y búsqueda»), §13, §16 y §19.
- [`multitenancy.md`](../architecture/multitenancy.md) §3.2, §4, §5, §7, §8, §10 y §13.
- [`arbol.md`](../architecture/arbol.md): las entradas [E7].
- ADR 0021, 0031 y 0032 (en el [índice](../decisions/README.md)) y [0035](../decisions/0035-baja-de-cuenta.md).
- Front: `docs/architecture/frontend.md` §2, §3 y §4 («Auth» y «Teléfono»), `docs/architecture/arbol.md` y las fichas `accesos-y-permisos`, `paginado-y-listados`, `responsive` y `tests`.
- Lienzo vigente (front `docs/design/lienzo/`, v93, `b99384a`): Landing, Landing-Movil, Directorio, M-Directorio, Pagina-Publica y M-Pagina-Publica, con todos sus estados; Criterio-Marco y Criterio-Decisiones (P92 a P97).
- [Revisión de decisiones del 2026-10-03](../reviews/2026-10-03-revision-de-decisiones.md):
  - P92: la portada;
  - P97 y R14: el ☰, ya resuelto en el lienzo v93 y en el plan maestro;
  - la fila de la ADR 0031 de su anexo [`adr-0021-0041.md`](../reviews/2026-10-03-revision-de-decisiones/adr-0021-0041.md), que pide otro nombre para el controller público.

### Lo que dicen los tableros

Se copian tal cual: la estructura, los textos, el orden y los estados.

**El ☰ en el teléfono.** Sin sesión, la portada y el directorio se ven como **web móvil con ☰** (R14 de Criterio-Marco). La página pública de una organización, en su subdominio, es la **misma tarjeta, sin ☰** (P97). Ninguna de las tres lleva la barra de abajo de la app.

**Portada · `/`** (Landing; en el teléfono, Landing-Movil):
- **Encabezado:** la marca «ArquitecturaBase»; el menú «Personas», «Cómo empezar» y «Empresas», en ese orden; a la derecha, «Ingresá como empresa» e «Ingresar».
- **Encabezado en el teléfono:** la marca, «Ingresar» y ☰ («Abrir el menú»), que abre Personas, Cómo empezar, Empresas e «Ingresá como empresa».
- **Héroe, sin etiqueta:** «Para vos / y para tu empresa.», su texto, «Crear mi cuenta» y «Registrá tu empresa».
- **Secciones, en este orden:**
  1. «Dos lados separados» (`#personas`);
  2. «Empresas en ArquitecturaBase» (`#empresas`): el buscador «Buscar una empresa», tres tarjetas (logo, nombre, descripción y dirección, como «delta.plataforma.com») y «Ver todas las empresas», que lleva a `/empresas`;
  3. «Tu empresa, en tres pasos» (`#empezar`);
  4. «Probalo hoy».
- «Empresas», arriba, baja a `#empresas`.
- **Pie:** «© 2026 ArquitecturaBase», «Términos», «Privacidad» y «Ayuda».

**Directorio · `/empresas`** (Directorio; en el teléfono, M-Directorio):
- **Encabezado:** el de la portada, con «Empresas» marcada (en el teléfono, dentro del ☰).
- **Contenido:** el título «Empresas», el buscador «Buscar por nombre» y las tarjetas (logo, nombre, descripción y dirección). Cada tarjeta lleva a la página de la organización en su subdominio.
- **Paginado:** «1–3 de 3», «10 por página», «Página 1 de 1» y las flechas «Página anterior» y «Página siguiente». En el teléfono va igual, pero sin «10 por página».
- **Estados:**
  - «Con empresas»;
  - «Sin resultados»: «Ninguna empresa coincide con la búsqueda» y «Limpiar búsqueda»;
  - «Todavía no hay empresas»: «Todavía no hay empresas publicadas».
- **Pie:** «© 2026 ArquitecturaBase», «Términos» y «Privacidad».

**Página pública · `<slug>.plataforma.com`** (Pagina-Publica; en el teléfono, M-Pagina-Publica):
- **Encabezado:** solo la marca «ArquitecturaBase», que lleva a la portada.
- **Tarjeta centrada:** el logo, el nombre, la descripción y los cuatro datos de contacto con su ícono: «Dirección», «Teléfono» (enlace `tel:`), «Correo» (`mailto:`) y «Sitio web».
- **Estados:**
  - «Publicada, sin sesión»: abajo de la tarjeta, «Ingresá para continuar»;
  - «Publicada, con sesión»: sin el botón y con tu menú arriba;
  - «No disponible»: en la misma tarjeta, «Esta página no está disponible» e «Ir a ArquitecturaBase», sin decir por qué.
- **Tu menú en la compu:** el botón con tu inicial, tu nombre y «Personal». Abre tu nombre y tu correo, «Perfiles» (Personal ✓, «Tu perfil personal»), «Mi cuenta» y «Salir».
- **Tu menú en el teléfono:** tu inicial («Tu cuenta») abre lo mismo en una hoja con manija.
- **Pie:**
  - publicada: «© 2026 <nombre de la organización> · en ArquitecturaBase» (en el tablero, «Grupo Delta»);
  - no disponible: «© 2026 ArquitecturaBase»;
  - los dos llevan «Términos» y «Privacidad».

### Punto de partida (2026-10-04, back `34e0ead`, front `f7f9737`)

**Ya existe en el back:**
- `PublicPage` (E6): nombre, descripción, logo (bytes PNG, JPEG o WebP de hasta 512 KiB), correo, teléfono (`PhoneNumber`), dirección y sitio web, en borrador o publicada.
  - Se edita en `api/organization/public-page` (`Controllers/Organization/PublicPageController.cs`).
  - El bloqueo de la plataforma es de la E5 (`PlatformPublicSiteController`).
- El slug vive en `platform.Tenants` (`TenantSlug`):
  - los reservados son un catálogo guardado (`ReservedTenantSlug`: www, app, api, auth, admin, empresas y plataforma);
  - `GET /api/public-site/slug-availability` consulta si está libre.
- Las tres clases de datos, con sus filtros y su RLS:
  - `Party` y `PartyPolicy` (hoy devuelve `bool`);
  - `TenantStampInterceptor` sella la parte activa;
  - `AuditTrailInterceptor` audita lo compartido en las dos partes.
- `TestFeatures/Isolation` tiene `Widget`, `Poster` y `Deal`, pero solo se usan con un contexto aparte: el host de la Api de los tests no los conoce.
- Cinco participantes de la baja, en `Infrastructure/Identity/Deletion/`.
- **Ingreso y hosting:**
  - el issuer es fijo (`Authentication:Issuer`; en desarrollo, `https://localhost:5174/`);
  - el SPA y la Api ya se sirven en el mismo origen en cualquier host (`SpaHostingTests` prueba `empresa.localtest.me`);
  - la CSP admite solo `frame-src 'self'` y `frame-ancestors 'self'`.

**No existe en el back:**
- `IPublicSiteContext` ni `PublicSiteResolutionMiddleware` (en `Program.cs` hay solo un comentario);
- `[PublicSite]` ni `SubdomainRedirectUriValidator`;
- `DirectoryController`;
- `IRetainedOnConsumerDeletion` ni el participante de los datos compartidos;
- `SubdomainTests`.

**Ya existe en el front:**
- `SiteLayout` (la portada y los legales) y `LandingPage`;
- `authConfig.ts`, con `metadataUrl` y `metadataSeed` en el origen propio;
- `navigation/personal.ts`, con Inicio y Mi cuenta;
- de la consolidación:
  - `usePagedQuery`, `apiErrorMessage`, `tenancy/access.ts` y `useSignOut`;
  - `usePermissions`, con `can`, `canInCompany` y `hasFeature` (F5);
  - el manifiesto de rutas con `RequirePermission`: una ruta de un módulo apagado da 404 (F5);
  - `visibleNavigation`, que filtra el menú lateral y «Más» (F5);
  - una sola cultura efectiva (F6);
- Vite acepta `*.localtest.me` y no cambia el `Host`.

**No existe en el front:**
- el árbol por host, `areas/storefront`, `usePublicSite` ni `/empresas`;
- `routes-by-host.test.ts` ni la opción `host` de `renderRouteWithProviders`;
- de la consolidación: `useEditPage` (F7). `useFeature` no va a existir: lo reemplaza `hasFeature` de `usePermissions`.

**Desvíos que corrige esta etapa:**
- la portada tiene la etiqueta del héroe y el menú en otro orden (P92);
- en el teléfono, la portada no tiene ☰ y muestra «Empresas» como atajo a la puerta de empresas;
- `PublicPagePage` arma la dirección pública con un `publicUrl` propio y `.plataforma.com` fijo, igual que el sufijo del slug en «Registrá tu empresa».

### Restricciones

- **Repositorio:** se trabaja en `main`, sin push. Un worktree corto, solo si otra conversación toca los mismos archivos.
- **Datos:** sin datos de ejemplo en Development: no se siembra ninguna organización ni página publicada. Los tests, las capturas y el E2E usan sus propias bases.
- **Lienzo:**
  - lo toca solo la conversación de Diseño;
  - no se programa nada que el tablero no tenga: si falta algo, se dibuja primero;
  - las capturas se le mandan al usuario antes del cierre y no se autoaprueban.
- **Arnés:**
  - no nacen guardas de arquitectura nuevas: se amplían las que existen (`AccessDeclarationTests`, `ExplicitRouteInventoryTests`, `SpaHostingTests`, `AccountDeletionParticipantsTests`);
  - nacen solo los tests que nombran las fichas (`SubdomainTests`, `routes-by-host.test.ts`) y los de conducta de cada tarea.
- **Piezas comunes del front (de la consolidación):**
  - se usan `usePagedQuery` (el directorio), `apiErrorMessage` y `actionErrorMessage` (los errores de carga) y `tenancy/access.ts` con `useSignOut` (las puertas, el inicio y «Salir»);
  - `usePermissions` casi no se usa: el sitio público y el área personal no piden permisos. La navegación personal usa su `hasFeature`, a través de `visibleNavigation`;
  - `useEditPage` (F7) no hace falta, porque la etapa no tiene pantallas de edición; se nombra para que nadie arme otro guard;
  - si una pieza no existe al llegar a su tarea, no se crea una igual en la E7: se avisa a la Guía.
- **Datos personales:** nunca se registran correos, teléfonos, códigos ni tokens. La copia de los datos de una persona en un dato compartido no va a la auditoría.
- **Decisiones técnicas menores:** primero, cómo lo hace ArquitecturaBase; si no está ahí, la más simple. Se anotan en «Decisiones tomadas». Solo se pregunta lo que cambia el producto o contradice una regla escrita.

**Quién:**
- Back y front van en paralelo, un agente por repo, con archivos que no se cruzan. Dentro de cada repo, las tareas van en orden.
- El front genera sus tipos cuando el back commitea T02 y T03. Hasta entonces avanza con lo que no usa esos tipos: el árbol por host y la dirección pública (T10, salvo `usePublicSite`) y el marco (T11).
- Si las pantallas de la Etapa 5 (T19 a T25) avanzan al mismo tiempo, comparten `routes.tsx`, `layouts/` y `locales/`. Por eso: commits chicos, rebase antes de cada commit y nunca un archivo de la otra conversación en el commit propio.
- La puerta (T18 a T20) la cierra un solo agente, con los dos repos.

## Protocolo de cada tarea de lógica

1. Escribir los tests de conducta en el proyecto y la carpeta que les corresponden. Correr el focal y registrar el rojo por la conducta ausente; un error de entorno no cuenta como rojo.
2. Implementar lo mínimo:
   - se valida afuera;
   - adentro van locks → lecturas → reglas → escrituras;
   - la caché se invalida después del commit;
   - cada método público de un servicio va en `OperationLog.RunAsync`;
   - los textos van en es y en.
3. Repetir el focal en verde y compilar lo afectado sin advertencias.
   - Si hay HTTP, ingreso, persistencia o aislamiento: integración con PostgreSQL y los controles 401, 403 y 404 que apliquen.
   - En el front: lint, build y, si cambia el contrato, `npm run contracts:check`.
4. Los docs afectados cambian en el mismo commit. Antes de commitear: `git diff --check`, `git add -- <rutas de la tarea>` y un conventional commit en español.

Comandos focales:

```text
dotnet test --project tests/ArquitecturaBaseMultitenant.Domain.UnitTests/ArquitecturaBaseMultitenant.Domain.UnitTests.csproj -- --filter-class "<namespace>.<Clase>"
dotnet test --project tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/ArquitecturaBaseMultitenant.Api.IntegrationTests.csproj -- --filter-class "<namespace>.<Clase>"
dotnet test --project tests/ArquitecturaBaseMultitenant.ArchitectureTests/ArquitecturaBaseMultitenant.ArchitectureTests.csproj -- --filter-class "<namespace>.<Clase>"
dotnet build src/ArquitecturaBaseMultitenant.Api/ArquitecturaBaseMultitenant.Api.csproj
npm test -- <ruta.test.tsx>
npm run lint && npm run build
npm run contracts && npm run contracts:check
npm run test:e2e:real
node scripts/arneses/arnes.mjs realinear-docs --detectar
```

Si Visual Studio bloquea `bin` u `obj`, usá `-p:ArtifactsPath=<directorio temporal único>`. Testcontainers necesita Docker fuera del sandbox.

## Contrato que comparten back y front

Se fija acá para que los dos repos avancen en paralelo. El back lo publica en `docs/contracts/openapi.json` (T02 y T03) y el front regenera sus tipos.

**Hosts:**
- El dominio principal es el host del issuer: `Authentication:Issuer` en el back y `VITE_AUTH_ISSUER` en el front.
- Una organización vive en `<slug>.<host del issuer>`, con el mismo esquema y el mismo puerto.
- Cualquier otro host no es un sitio público.

**La página del subdominio** (`PublicSiteController`, `[PublicSite][AllowAnonymous]`):
- `GET /api/public-site` responde 200 con `slug`, `name`, `description`, `logoUrl`, `address`, `phone` (E.164), `email` y `website`; los datos que la organización no cargó van en `null`. No lleva versión, ni estado, ni ids.
- `GET /api/public-site/logo` devuelve la imagen (PNG, JPEG o WebP) con su `ETag`, o 404 si no hay logo. `logoUrl` lleva la versión de la página, para que un logo nuevo no quede en la caché del navegador.
- **«No disponible» es siempre el mismo 404** `PublicSite.PublicPage.NotFound`, con el mismo cuerpo, en todos estos casos:
  - un slug desconocido o reservado;
  - una página en borrador o bloqueada por la plataforma;
  - una organización que no está `Active`;
  - el dominio principal.

**El directorio** (`DirectoryController`, `[AllowAnonymous]`, en el dominio principal):
- `GET /api/directory?page=&pageSize=&search=&sort=` responde `PagedResult<DirectoryEntryRow>`, con `slug`, `name`, `description` y `logoUrl`.
  - Trae solo las páginas publicadas de organizaciones activas.
  - El orden por defecto es `name`; `pageSize`, 10, 20, 50 o 100; la búsqueda mira el nombre.
- `GET /api/directory/{slug}/logo` devuelve la imagen o 404.
- La portada pide `page=1&pageSize=10` y muestra las tres primeras, porque `PagedRequest` no tiene un tamaño 3.
- **Nombres de las rutas.** Los comentarios de los tableros dicen `/api/public/site` y `/api/public/businesses`. Se usan `api/public-site` (como el `slug-availability` que ya existe) y `api/directory`, porque `api-http.md` prohíbe un prefijo de acceso en la ruta.

**Ingresar desde un subdominio** (OIDC, cliente `web`):
- `authorize` y `logout` navegan al issuer fijo, con:
  - `access=consumer`;
  - `redirect_uri=https://<slug>.<principal>/auth/callback` (o `/silent-renew.html`);
  - `post_logout_redirect_uri=https://<slug>.<principal>/`.
- El canje del código, la renovación, `userinfo` y `revoke` van a `/connect/*` del propio subdominio. `authConfig.ts` ya lo arma con `metadataUrl` y `metadataSeed`.
- Esas URIs se aceptan solo para los slugs publicados de organizaciones activas (`SubdomainRedirectUriValidator`).

## Seguimiento y tareas verificables

### T00 · Línea de base y plan

- [ ] **La consolidación cerrada:**
  - su puerta (bloque 7) en verde y su informe `docs/reviews/<fecha>-consolidacion.md` commiteado;
  - existe `useEditPage` (F7). `usePermissions`, el manifiesto de rutas y `visibleNavigation` ya están (F5).
- [ ] **`main` limpio en los dos repos:**
  - sin trabajo de otra conversación a medio commitear (al escribir este plan había cambios sin commitear en los dos repos);
  - Aspire apagado (`aspire ps` vacío);
  - el último commit de cada repo, anotado en «Registro de resultados».
- [ ] **La línea de base del arnés:** `realinear-docs --detectar` sale 0 en los dos repos y `ClosedStages` sigue en `{0,1,2,3,4,6}` (más el 5, si ya cerró).
- [ ] **Los tableros del alcance validados en el lienzo vigente** (v93 o posterior):
  - Landing, Landing-Movil, Directorio, M-Directorio, Pagina-Publica y M-Pagina-Publica, sin «· por refinar» ni «· para aprobar»;
  - Criterio-Marco con la excepción del ☰ para el sitio público sin sesión (R14);
  - Criterio-Decisiones con P92 a P97.
  - Si el README del lienzo trae una versión posterior, se releen los estados de esos seis tableros y se corrige «Lo que dicen los tableros».
- [ ] **«Para decidir antes de empezar» resuelto por el usuario.** Cada respuesta se pasa al texto de las tareas que toca y la sección se borra.
- [ ] **El plan aprobado por el usuario,** con sus respuestas pasadas a las tareas. Commit solo del plan: `docs(plan): la Etapa 7 aprobada`.

## Back (T01 a T09)

### T01 · Back · El subdominio resuelve la organización publicada

**Archivos nuevos:**
- `Application/Interfaces/Integrations/Request/IPublicSiteContext.cs`;
- `Application/Interfaces/Persistence/IPublicSiteReader.cs` e `Infrastructure/Persistence/Readers/PublicSiteReader.cs`;
- `Api/Tenancy/{PublicSiteResolutionMiddleware,PublicSiteAttribute,PublicSiteContext}.cs`;
- una ruta de prueba `[PublicSite]` en `TestFeatures/Access/`, que devuelve qué organización se resolvió;
- `tests/…Api.IntegrationTests/Tenancy/SubdomainTests.cs`.

**Archivos que cambian:** `Api/Program.cs` (la línea `// E7: PublicSiteResolutionMiddleware.`), el registro DI de cada capa, `backend.md` §8 y §16, `multitenancy.md` §8 y `docs/rules/multitenancy.md`.

**Copiá de:** `Api/Tenancy/TenantResolutionMiddleware.cs` para el middleware y el contexto; `Readers/TenantSlugAvailabilityReader.cs` para el slug y los reservados.

- [ ] **Rojo** (`SubdomainTests`):
  - `Only_a_single_label_over_the_main_domain_is_an_organization_host`:
    - sí: `<slug>.<host del issuer>`, con mayúsculas o sin ellas y con el mismo puerto;
    - no: el dominio principal, dos etiquetas (`a.b.<principal>`), otro dominio, una IP y un slug reservado (`www`, `api`, `empresas`…);
  - `A_published_organization_subdomain_resolves_its_site`;
  - `Main_domain_reserved_and_unknown_subdomains_resolve_no_site`: la ruta `[PublicSite]` responde 404 `PublicSite.PublicPage.NotFound`;
  - `The_host_never_sets_the_tenant_of_a_session`: con el token de la Empresa B en el subdominio de la Empresa A, una ruta privada sigue con el tenant del claim (B).
- [ ] **Implementación:**
  - el dominio principal es el host de `Authentication:Issuer` (`IPublicOrigin`);
  - un host de organización es exactamente `<slug>.<ese host>`. El slug se valida con `TenantSlug.Create` y se descartan los de `ReservedTenantSlug`;
  - el middleware va donde está el comentario de `Program.cs`, después de `TenantResolutionMiddleware`. Solo lee el host: la base se consulta una vez por pedido y solo cuando una ruta `[PublicSite]` lo necesita;
  - `IPublicSiteReader` resuelve el slug a la organización solo si su página está publicada, no está bloqueada y la organización está `Active`;
  - sin caché en esta etapa: la caché la define la ADR de la Etapa 9. Se corrige la nota «caché con prefijo del sitio público» de `arbol.md`;
  - `[PublicSite]` responde 404 cuando no hay sitio y nunca toca `ITenantContext`.
- [ ] **Verde:**
  - `SubdomainTests`, `SuspensionTests` y `AccessDeclarationTests` focales;
  - build de la Api sin advertencias;
  - los punteros `AGENTS.md` + `CLAUDE.md` de las carpetas nuevas.

  Commit `feat(sitio-publico): el subdominio resuelve la organización publicada`.

**Hecho cuando** una ruta `[PublicSite]` responde solo en el subdominio de una organización publicada y activa, y el host nunca cambia el tenant de una sesión.

### T02 · Back · La página del subdominio y «No disponible»

**Archivos nuevos:**
- `Api/Controllers/PublicSite/PublicSiteController.cs` (`[PublicSite][AllowAnonymous]`) y `Api/Contracts/PublicSite/` (la respuesta, si lleva value objects);
- `Application/Interfaces/Services/IPublicSiteService.cs` y `Application/Services/PublicSite/PublicSiteService.cs`;
- `Application/Models/PublicSite/PublicSiteResponse.cs`;
- `tests/…Api.IntegrationTests/PublicSite/PublicSiteTests.cs`;
- `docs/features/sitio-publico.md`.

**Archivos que cambian:** `IPublicPageReader` y `PublicPageReader` (la lectura pública), `SubdomainTests`, `ExplicitRouteInventoryTests`, `docs/contracts/openapi.json` y `docs/features/organizaciones.md` (un enlace).

**El nombre del controller:**
- es `PublicSiteController` y no `PublicPageController`, como recomendó la revisión de decisiones: ya existe `Controllers/Organization/PublicPageController.cs`, de la E6;
- los docs que nombran `PublicPageController` para el `[PublicSite]` se corrigen en el mismo commit.

**Copiá de:** `Organization/PublicPageController.cs` y `Services/PublicSite/PublicPageService.cs` (E6); `Organization/RolesController.cs` para los atributos de OpenAPI.

- [ ] **Rojo:**
  - `PublicSiteTests.Published_page_shows_only_its_public_fields`: nombre, descripción, `logoUrl`, dirección, teléfono E.164, correo, sitio web y slug; ni versión, ni bloqueo, ni ids;
  - `PublicSiteTests.Logo_is_served_with_its_type_and_answers_304_when_unchanged`, y 404 si no hay logo;
  - `SubdomainTests.Suspended_organization_or_blocked_page_is_not_available`: los estados se siembran directo (`TenantStatus.Suspended` y `PublishBlockedAtUtc`), sin los servicios de la E5, como pide el plan maestro;
  - `SubdomainTests.Draft_page_is_not_available_even_for_its_own_members`: con el token de un miembro, el subdominio tampoco muestra el borrador;
  - `SubdomainTests.Not_available_answers_the_same_for_every_reason`: una organización desconocida, reservada, con la página en borrador o bloqueada, suspendida, cerrada o en espera de aprobación da el mismo 404, con el mismo `code` y el mismo cuerpo salvo el `traceId`;
  - `SubdomainTests.Subdomain_resolves_public_data_only` (multitenancy.md §13): con el token de la Empresa B, el subdominio de la Empresa A muestra solo lo publicado de A;
  - `ExplicitRouteInventoryTests`, con las dos rutas nuevas.
- [ ] **Implementación:**
  - las dos rutas del contrato;
  - la lectura exige `IsPublished` y la organización `Active` de forma explícita: el filtro `"Public"` solo le mostraría el borrador a su propia organización;
  - `OperationLog.RunAsync`, `[ProducesResponseType]` y `[ProducesProblem(404)]`; la salida cumple `ControllerOutputContractTests`;
  - sin textos nuevos: el 404 usa `PublicSite.PublicPage.NotFound` y la pantalla la pinta el front;
  - `docs/features/sitio-publico.md` nace con la página, «No disponible», «Carpetas» y «Lo verifica». Cada carpeta de su tabla «Carpetas» lleva su `AGENTS.md` y su `CLAUDE.md`, y los punteros de las carpetas `PublicSite/` mandan a leer esa ficha.
- [ ] **Verde:**
  - `PublicSiteTests`, `SubdomainTests`, el inventario de rutas y `OpenApiContractTests`;
  - build sin advertencias y `openapi.json` regenerado.

  Commit `feat(sitio-publico): la página de la organización en su subdominio`.

**Hecho cuando** el subdominio de una organización publicada devuelve su página y cualquier otro caso responde el mismo 404.

### T03 · Back · El directorio de empresas publicadas

**Archivos nuevos:**
- `Api/Controllers/PublicSite/DirectoryController.cs` (`[AllowAnonymous]`, sin `[PublicSite]`) y `Api/Contracts/PublicSite/DirectoryQuery.cs`;
- `Application/Models/PublicSite/ListDirectoryRequest.cs` y `ReadModels/DirectoryEntryRow.cs`;
- `Application/Validation/PublicSite/ListDirectoryRequestValidator.cs`;
- `Application/Interfaces/Services/IDirectoryService.cs` y `Application/Services/PublicSite/DirectoryService.cs`;
- `Application/Interfaces/Persistence/IDirectoryReader.cs` e `Infrastructure/Persistence/Readers/DirectoryReader.cs`;
- una migración: el índice GIN trigram sobre `f_unaccent(lower("Name"))` de `public_site.PublicPages` y el índice del orden por nombre;
- `tests/…Api.IntegrationTests/PublicSite/DirectoryTests.cs`.

**Archivos que cambian:** `AccessDeclarationTests` (la lista cerrada), `ExplicitRouteInventoryTests`, `openapi.json` y `docs/features/sitio-publico.md`.

**Copiá de:** `Readers/RoleReader.cs` (`SortMap`, `ApplySearch`, `ApplySort` y `ToPagedResultAsync`) y `Organization/RolesController.cs`.

- [ ] **Rojo** (`DirectoryTests`, salvo el último):
  - `Directory_lists_only_published_pages_of_active_organizations`: quedan afuera las páginas en borrador o bloqueadas y las organizaciones suspendidas, cerradas, en espera de aprobación o en provisioning;
  - `Directory_search_ignores_accents_and_case_and_escapes_wildcards`: «perez» encuentra «Pérez», y `%` y `_` se buscan como texto;
  - `Directory_pages_by_ten_and_an_out_of_range_page_is_empty`, con 1 y con 50 organizaciones:
    - 10 por página por defecto, y también 20, 50 y 100;
    - la página 8 de 5 vuelve vacía, con el total real;
  - `Directory_orders_by_name_and_rejects_other_sorts`: el orden de es-AR; un `sort` desconocido da 400 `Validation.Failed`;
  - `Directory_rows_carry_only_public_fields`;
  - `Directory_logo_is_served_only_for_listed_organizations`;
  - `AccessDeclarationTests.Anonymous_controllers_are_an_explicit_closed_list`, en rojo hasta que `DirectoryController` entra en la lista.
- [ ] **Implementación:**
  - las dos rutas del contrato;
  - el reader lee `PublicPages` (filtro `"Public"` y su RLS) y lo une con `platform.Tenants` por el slug y el estado, sin `IgnoreQueryFilters`;
  - sin caché en esta etapa;
  - cada regla del validador tiene su propio mensaje (`ValidationMessageTests`).
- [ ] **Verde:**
  - `DirectoryTests`, `AccessDeclarationTests`, el inventario de rutas y OpenAPI;
  - build sin advertencias y el SQL de la migración revisado.

  Commit `feat(sitio-publico): el directorio de empresas publicadas`.

**Hecho cuando** el directorio pagina, ordena y busca como cualquier listado y nunca muestra una organización que no está publicada y activa.

### T04 · Back · Ingresar desde un subdominio

**Archivos nuevos:** `Infrastructure/Identity/OpenIddict/SubdomainRedirectUriValidator.cs`.

**Archivos que cambian:**
- `OpenIddictRegistration.cs` (registra el validador);
- `Api/Hosting/SecurityHeadersExtensions.cs` y el armado del ingreso (`ConnectService`), solo con la opción A de «Para decidir» 1;
- `SubdomainTests` y `Hosting/SecurityHeadersTests.cs`;
- `Support/AccountJourney.cs` (`AuthorizeAsync`), si hace falta un `redirect_uri` de subdominio;
- `multitenancy.md` §5, `backend.md` §13 y `docs/features/identidad.md`.

- [ ] **Rojo** (`SubdomainTests`, salvo lo de la CSP):
  - `Redirect_uri_only_for_published_slugs` (multitenancy.md §13):
    - siguen al ingreso `https://<slug>.<principal>/auth/callback` y `/silent-renew.html` de una organización publicada y activa;
    - se rechazan sin redirigir a esa URI: un slug en borrador, bloqueado, suspendido, desconocido o reservado, dos etiquetas, otro puerto, `http` y otra ruta;
  - `Code_issued_for_a_subdomain_is_exchanged_on_its_own_origin_with_the_fixed_issuer`: `authorize` en el dominio principal y el canje en `/connect/token` del subdominio; el token trae el `iss` fijo, `access=consumer` y el `tenant_id` del espacio personal;
  - `Userinfo_and_revocation_answer_on_the_subdomain_origin`;
  - `Logout_returns_only_to_a_published_subdomain`;
  - con la opción A: `Silent_sign_in_never_creates_the_personal_space`: con `prompt=none` y `access=consumer`, una cuenta sin espacio personal recibe `login_required` y no se le crea;
  - con la opción A: `SecurityHeadersTests.Only_organization_subdomains_can_frame_the_authorize_endpoint` y `SecurityHeadersTests.Subdomain_documents_can_frame_the_main_domain_authorize`.
- [ ] **Implementación:**
  - el validador amplía la validación de OpenIddict del cliente `web`:
    - la lista fija de `Authentication:Clients:Web` queda igual;
    - un host `<slug>.<principal>`, con el esquema y el puerto del issuer, se acepta solo en esas rutas exactas y solo si `IPublicSiteReader` dice que el slug está publicado y la organización, activa;
    - nunca un comodín;
  - lo mismo vale para `post_logout_redirect_uri` (`https://<slug>.<principal>/`);
  - el issuer sigue fijo y `/connect/*` ya atiende cualquier host;
  - con la opción A, la CSP se abre lo justo:
    - `/connect/authorize` admite que lo enmarquen los subdominios (`frame-ancestors` con `<esquema>://*.<principal>[:puerto]`);
    - el SPA de un subdominio admite enmarcar el issuer (`frame-src`);
    - nada más cambia;
  - con la opción A, el ingreso en silencio (`prompt=none`) nunca crea el espacio personal: si la cuenta no lo tiene, responde `login_required`. Lo crea solo «Ingresá para continuar», que la persona toca.
- [ ] **Verde:**
  - `SubdomainTests`, `ConnectTests` y `SecurityHeadersTests`;
  - las regresiones de ingreso (`LoginCodeTests` y `SignupTests`);
  - build sin advertencias.

  Commit `feat(ingreso): ingresar desde el subdominio de una organización publicada`.

**Hecho cuando** una persona puede ingresar desde el subdominio de una organización publicada, y ningún otro host recibe un código.

### T05 · Back · El mismo origen en cada host y el dominio de desarrollo

**Archivos que cambian:**
- `tests/…Api.IntegrationTests/Hosting/SpaHostingTests.cs`;
- con la opción A de «Para decidir» 1:
  - `Api/appsettings.Development.json` (`Authentication:Issuer`, `RedirectUris` y `PostLogoutRedirectUris`);
  - `AppHost/AppHost.cs` (el mismo issuer llega al front como `VITE_AUTH_ISSUER`);
  - el test de configuración de arranque;
  - `docs/operations/configuracion.md`.

- [ ] **Rojo:**
  - `SpaHostingTests.Subdomain_api_answers_on_its_own_origin_without_cors_headers`: un pedido con el `Origin` de otro host no recibe `Access-Control-Allow-*`, y un preflight `OPTIONS` tampoco;
  - con la opción A: `StartupConfigurationTests.Development_uses_localtest_me_for_the_main_domain_and_its_subdomains`.
- [ ] **Implementación:**
  - no se agregan CORS ni un prefijo nuevo: `/api/public-site` y `/api/directory` viven bajo `/api`, que ya está en `BackendPrefixes`;
  - con la opción A, el dominio principal de desarrollo pasa a `https://localtest.me:5174`:
    - Vite ya acepta `*.localtest.me` y no cambia el `Host`;
    - `configuracion.md` dice qué URI sumar en la consola de Google para ingresar con Google en desarrollo;
    - también dice que el certificado de desarrollo no cubre `localtest.me`.
- [ ] **Verde:** `SpaHostingTests` y el test de configuración; build sin advertencias. Commit `chore(hosting): el sitio público en el mismo origen en cada host, también en desarrollo`.

**Hecho cuando** cada host sirve su SPA y su Api sin CORS, y el desarrollo usa el dominio que se eligió.

### T06 · Back · La mecánica de interacción con `TestFeatures`

**Archivos:** todo va en el proyecto de tests, salvo `PartyPolicy`.
- `Domain/Common/PartyPolicy.cs`: devuelve un `Result` con un error común (`Tenancy.Party.NotAllowed`, 403) en lugar de un `bool`, con sus textos en `Errors.resx` y `.en.resx`;
- `tests/…Domain.UnitTests/Tenancy/PartyPolicyTests.cs`;
- `TestFeatures/Isolation/{Deal,DealsController,PostersController,IsolationSchema,IsolationModelCustomizer}.cs` y un servicio de prueba para los `Deal`;
- la fábrica de estos tests, que suma `IsolationModelCustomizer` al `ApplicationDbContext` del host. Conviene una fábrica propia, para no cambiar el modelo de las demás pruebas;
- `tests/…Api.IntegrationTests/Tenancy/EngagementTests.cs` (nuevo).

**El `Deal`, como un módulo de verdad:**
- `Status`: `Requested`, `Confirmed`, `Rejected` o `Cancelled`;
- `Summary`: lo que se pidió;
- `ConsumerName`: la copia del nombre de la persona, marcada `[NotAudited]`;
- `IAuditable` e `IVersioned`;
- su módulo de prueba, `test-deals`, de los dos lados, registrado en el catálogo de prueba como los de `FeatureProbeController`.

**La persona:**
- `POST /test/deals` (`[Access(Consumer)]` + `[PublicSite]` y `[Idempotent]`), según «Para decidir» 2:
  - la organización sale del subdominio (`IPublicSiteContext`), nunca del cuerpo;
  - el espacio personal lo sella el interceptor;
  - el nombre se copia de la cuenta;
  - el pedido mira los módulos de la organización del subdominio: si `test-deals` está apagado ahí, responde como cualquier módulo apagado (decisión tomada 2);
- `POST /test/deals/{id}/cancel`;
- `GET /test/deals`: lo que pidió, en todas las organizaciones.

**La empresa:**
- `GET /test/deals`: su bandeja;
- `POST /test/deals/{id}/confirm` y `/reject`.

**Cada cambio de estado** lleva `version` y lo decide `PartyPolicy`. Una fila que no se ve da 404. La bandeja no pide un permiso de prueba: el catálogo productivo no suma permisos para tests, y `PermissionAuthorizationTests` ya prueba los atributos. Un comentario dice qué permiso pide la bandeja de un módulo real, y `personal.md` lo repite.

- [ ] **Rojo** (`EngagementTests`, salvo el último):
  - `A_person_requests_from_the_public_page_and_the_business_answers_from_its_inbox`;
  - `Each_transition_belongs_to_one_party`: Kevin como persona no confirma y Kevin como Empresa A no cancela; los dos dan 403 `Tenancy.Party.NotAllowed`;
  - `Only_the_two_parties_reach_a_deal`: Carla y la Empresa B reciben 404 al leer y al cambiar;
  - `Business_sees_only_what_was_shared`: la bandeja trae la copia, ni el correo ni la cuenta de la persona;
  - `A_request_needs_a_published_subdomain`:
    - el dominio principal, una organización suspendida y una página en borrador dan 404;
    - sin sesión, 401;
    - con el acceso de empresa, 403 `Tenancy.Access.Wrong`;
  - `A_request_needs_the_module_on_in_that_organization`: con `test-deals` apagado en la Empresa A, pedir en su subdominio responde como un módulo apagado; en la Empresa B, con el módulo prendido, el pedido entra;
  - `Repeating_a_request_with_the_same_key_creates_one_deal`;
  - `A_stale_version_does_not_change_the_state`: 409;
  - `Each_change_is_audited_for_both_parties_without_the_copied_name`;
  - `Published_posters_reach_the_public_page_and_drafts_do_not`: lo que publica un módulo, por HTTP;
  - `PartyPolicyTests` del dominio, con el error común.
- [ ] **Implementación:** la forma de un módulo real (contrato → servicio → datos), con comentarios que dicen qué copia un producto. Las tablas de prueba suman sus columnas en `IsolationSchema`, sin migraciones productivas.
- [ ] **Verde:**
  - `EngagementTests`, `PublicAndSharedRowsTests` y `PartyPolicyTests`;
  - las regresiones de `TestFeatures` (`IsolationSchemaTests`, `ConcurrencyTests`, `ModelFiltersTests` y `RlsBarrierTests`);
  - build sin advertencias.

  Commit `feat(interaccion): pedir desde la página pública y responder desde la bandeja`.

**Hecho cuando** la persona pide desde la página de la organización, la empresa responde desde su bandeja, cada parte hace solo lo suyo y nadie más ve el dato.

### T07 · Back · La baja: la copia en lo compartido y la retención legal

**Archivos nuevos:**
- `Application/Interfaces/Integrations/Legal/IRetainedOnConsumerDeletion.cs`;
- `Infrastructure/Identity/Deletion/EngagementDeletionParticipant.cs`, junto a los otros cinco participantes.

**Archivos que cambian:**
- `Domain/Common/IConsumerBusinessShared.cs`: el contrato para reemplazar la copia (decisión tomada 1);
- la retención: la tabla global, su migración y el paso en `AccountDeletionWorker`;
- `TestFeatures/Isolation/Deal.cs`;
- `Legal/AccountDeletionTests.cs` y `Legal/AccountDeletionParticipantsTests.cs`;
- la ADR 0035 («Consecuencias») y `multitenancy.md` §3.2;
- `arbol.md` y `backend.md` §4.2, que ubican los participantes en `Services/Legal/Participants/`: se llevan al código.

- [ ] **Rojo:**
  - `AccountDeletionTests.Deleting_the_account_replaces_the_shared_copy_and_the_business_keeps_its_row`: la empresa sigue viendo el `Deal`, con el estado y el resumen, y «Cuenta eliminada» en lugar del nombre;
  - `AccountDeletionTests.A_retained_shared_copy_is_replaced_only_when_its_period_ends`, con el reloj de prueba;
  - `AccountDeletionTests.Deleting_one_account_leaves_the_shared_data_of_others_intact`;
  - `AccountDeletionParticipantsTests.Current_participants_clean_only_the_account_data_and_can_run_twice`: ahora son seis, y repetir el de los datos compartidos no cambia nada;
  - ninguna entrada de auditoría guarda el nombre que se reemplazó.
- [ ] **Implementación:**
  - el participante corre en el paso del espacio personal, dentro de su `ITenantScope`: la política `Parties` le deja ver solo las filas donde la persona es parte;
  - recorre las entidades compartidas del modelo y reemplaza la copia de cada fila, salvo en los tipos que un módulo declaró retenidos;
  - lo retenido queda en la tabla global con su vencimiento, y el `AccountDeletionWorker` reemplaza la copia cuando vence;
  - «Cuenta eliminada» no se guarda: sale traducido al leer, de `Legal.AccountDeletion.DeletedDisplayName`.
- [ ] **Verde:**
  - `AccountDeletionTests`, `AccountDeletionParticipantsTests` y `AccountDeletionProcessingTests`;
  - las regresiones de la baja y build sin advertencias.

  Commit `feat(baja): la copia en los datos compartidos y la retención legal`.

**Hecho cuando** eliminar una cuenta deja a cada empresa su registro sin los datos de la persona, y lo retenido por ley se borra al vencer.

### T08 · Back · La receta B2C y el aislamiento entre accesos

**Archivos nuevos:**
- `docs/features/personal.md`, con «Carpetas» y «Lo verifica»;
- `TestFeatures/Isolation/PersonalWidgetsController.cs` y su servicio de prueba;
- `tests/…Api.IntegrationTests/Tenancy/PersonalIsolationTests.cs`;
- los punteros de `Application/Services/Personal/` y `Api/Controllers/Personal/`, donde van los módulos B2C del producto (`backend.md` §4.2 y §4.4).

**Archivos que cambian:** `docs/guides/agregar-un-area.md`, con un enlace a la receta. La tabla del `AGENTS.md` raíz no cambia: su fila «agregar un área completa» ya lleva a esa guía, y sus filas solo apuntan a fichas o guías (`arnes.md` §4).

- [ ] **Rojo** (`PersonalIsolationTests`):
  - `People_never_reach_each_other_personal_data`: Kevin y Carla, al listar, leer, cambiar y borrar; lo ajeno da 404;
  - `The_consumer_and_business_access_of_one_person_do_not_mix`: Kevin como persona no ve lo privado de la Empresa A, y Kevin como Empresa A no ve su espacio personal; el acceso equivocado da 403 `Tenancy.Access.Wrong`;
  - `Consumer_routes_need_no_permission`.
- [ ] **Implementación:** `personal.md` es la receta de un módulo B2C:
  - **el dato privado de la persona:** `ITenantOwned` en su espacio personal;
  - **las rutas:** `[Access(Consumer)]` sin permisos, porque la persona tiene los `personal.*` implícitos;
  - **un módulo opcional:** su clave con alcance `Consumer` o `Both` en `Features.cs` y `[FeatureGate]`;
  - **un módulo que interactúa con empresas:**
    - lo publicado va en `public_site` y se lee con `[PublicSite]`;
    - lo compartido va en `engagement` (`IConsumerBusinessShared`), con la copia de lo que la otra parte necesita ver, marcada `[NotAudited]` si es un dato personal;
    - los estados los cambia `PartyPolicy`;
  - **la baja:** el contrato de la copia y, si la ley lo pide, `IRetainedOnConsumerDeletion`;
  - **los tests:** copiá `PersonalIsolationTests` y `EngagementTests`;
  - **el front:** `areas/personal/<módulo>/`, el enlace en `navigation/personal.ts` y lo público en `areas/storefront/<módulo>/`;
  - **las diez preguntas de la puerta de documentación a ciegas** (T20), escritas antes, sobre el sitio público y el área personal.
- [ ] **Verde:** `PersonalIsolationTests` y `HarnessTests`: `personal.md` y sus carpetas, con sus punteros. Commit `docs(personal): la receta de un módulo B2C, probada con TestFeatures`.

**Hecho cuando** alguien que solo lee `personal.md` sabe dónde va cada pieza de un módulo B2C, y los tests prueban el aislamiento entre personas y entre los dos accesos de la misma persona.

### T09 · Back · Los docs y el arnés del back

- [ ] **`arbol.md`:** las entradas [E7] con los nombres reales:
  - `PublicSiteController` y `DirectoryController`;
  - los readers en `Persistence/Readers/`, sin subcarpeta;
  - los participantes en `Infrastructure/Identity/Deletion/`;
  - «No disponible» en `SubdomainTests`, como dice el plan maestro (hoy `arbol.md` lo pone en `SuspensionTests`).
- [ ] **Arquitectura y fichas:**
  - `backend.md` §4, §8, §16 y §19;
  - `multitenancy.md` §5, §8 y §13 (la tabla de tests, con los de HTTP);
  - `docs/rules/{multitenancy,api-http,modulos-habilitados}.md`: «Lo verifica» y los nombres; `modulos-habilitados.md` todavía nombra `PublicPageAdminController`.
- [ ] **ADR y fichas funcionales:**
  - la enmienda de la ADR 0021 en el índice, si se aprueba «Para decidir» 2;
  - `docs/features/{sitio-publico,personal,identidad,organizaciones}.md`.
- [ ] **Verde:** `HarnessTests` en verde y `realinear-docs --detectar` en 0 en el back. Commit `docs(etapa-7): fichas, árbol y decisiones del sitio público y del área personal`.

**Hecho cuando** los docs del back describen lo que hay, sin [E7] pendientes.

## Front (T10 a T17)

### T10 · Front · Rutas por host y una sola dirección pública

**Archivos nuevos:**
- `src/tenancy/publicSite.ts` y su test: `siteOfHost`, `publicSiteUrl(slug)`, `publicSiteHost(slug)` y `mainSiteUrl(path)`, todos a partir de `VITE_AUTH_ISSUER`;
- `src/tenancy/usePublicSite.ts` y su test;
- `src/app/routes-by-host.test.ts`, que nombra `accesos-y-permisos.md`;
- `src/areas/storefront/AGENTS.md` y `CLAUDE.md`.

**Archivos que cambian:**
- `src/app/{routes,router}.tsx`: el árbol por host;
- `src/test/utils/renderWithProviders.tsx`: la opción `host`;
- `src/areas/business/public-page/pages/PublicPagePage.tsx` («Ver página») y `src/areas/public/auth/pages/BusinessSignupPage.tsx` (el sufijo del slug): usan la pieza nueva en lugar de `publicUrl` y `.plataforma.com` fijos;
- con la opción A de «Para decidir» 1: el issuer por defecto de `authConfig.ts`.

- [ ] **Rojo:**
  - `routes-by-host.test.ts`:
    - «en el subdominio de una organización se montan solo la página pública y /auth/callback: /org, /cuenta, /plataforma, /login y /empresas no existen»;
    - «en el dominio principal sigue todo y se suma /empresas»;
  - `publicSite.test.ts`: «el dominio principal es el host del issuer y un subdominio de una etiqueta es una organización», con el puerto, mayúsculas, dos etiquetas, otro dominio y una IP;
  - `usePublicSite.test.tsx`: «trae la página del subdominio; un 404 es "No disponible", no un error»;
  - `PublicPagePage.test.tsx`: «Ver página abre la dirección real del subdominio».
- [ ] **Implementación:**
  - `routes.tsx` elige el árbol por el host;
  - el subdominio lleva el `AppShell` (por los avisos genéricos y `Legal.AcceptanceRequired`), `/` con la página pública y `/auth/callback`;
  - con la opción A, también `SessionRecovery`.
- [ ] **Verde:** los tests focales, lint y build. Commit `feat(sitio-publico): rutas por host y una sola dirección pública por organización`.

**Hecho cuando** el mismo SPA arma el sitio de la plataforma o el de una organización según el host, y la dirección pública sale de un solo lugar.

### T11 · Front · El marco del sitio público

**Archivos:** `src/layouts/SiteLayout.tsx` y su test, `src/locales/{es,en}/site.json` y `src/locales/{es,en}/storefront.json` (nuevo).

**Copiá de:** los tableros de «Lo que dicen los tableros»; `shared/ui/Sheet.tsx` para la hoja del teléfono; `tenancy/AccessMenu.tsx` y `useProfiles.tsx` para tu menú.

- [ ] **Rojo** (`SiteLayout.test.tsx`):
  - «el menú de arriba sigue el tablero: Personas, Cómo empezar y Empresas»;
  - «en el teléfono, la portada y el directorio llevan "Ingresar" y ☰, con Personas, Cómo empezar, Empresas e "Ingresá como empresa"; en el directorio, "Empresas" va marcada»;
  - «la página de una organización no lleva ☰ ni botones de ingreso; con sesión, lleva tu menú»;
  - «cada pie, como su tablero»;
  - axe.
- [ ] **Implementación:**
  - **las variantes:**
    - la portada y el directorio: el encabezado con el menú y los dos botones;
    - la página pública: la marca, que lleva al dominio principal, y tu menú si hay sesión;
    - los legales, sin cambios;
  - **en el teléfono:**
    - la portada y el directorio son web móvil con ☰ (R14);
    - la página pública es la misma tarjeta, sin ☰ (P97);
    - ninguna lleva la barra de abajo;
  - los enlaces del pie de la página pública van a `/terminos` y `/privacidad` del dominio principal.
- [ ] **Verde:** el test focal, lint y build. Commit `feat(sitio-publico): el marco del sitio público como los tableros`.

**Hecho cuando** el marco de la portada, el del directorio y el de la página pública coinciden con sus tableros, en la compu y en el teléfono.

### T12 · Front · La portada (P92) y «Empresas en ArquitecturaBase»

**Archivos:**
- `src/areas/public/site/pages/LandingPage.tsx` y su test;
- `src/areas/public/site/api/directory.ts`;
- `src/areas/public/site/components/CompanyCard.tsx`, compartida con el directorio;
- `src/locales/{es,en}/site.json`.

- [ ] **Rojo** (`LandingPage.test.tsx`):
  - «sin la etiqueta del héroe» (P92);
  - «después de "Dos lados separados" va "Empresas en ArquitecturaBase", con el buscador, tres empresas y "Ver todas las empresas", y después "Tu empresa, en tres pasos"»;
  - «cada tarjeta muestra el logo o las iniciales, el nombre, la descripción y la dirección del subdominio, y lleva a su página»;
  - «"Empresas", arriba, baja a esa sección»;
  - el buscador y la portada sin empresas, según «Para decidir» 3;
  - es-AR y en-US, axe y 390.
- [ ] **Implementación:**
  - se saca `landing.hero.kicker`;
  - el ancla «Empresas» pasa de `#organizaciones` a `#empresas`;
  - la sección pide `page=1&pageSize=10` y muestra las tres primeras;
  - sin logo, la tarjeta muestra las iniciales, como el tablero;
  - un error al cargar usa `apiErrorMessage`.
- [ ] **Verde:** el test focal, lint, build y `contracts:check`. Commit `feat(sitio-publico): la portada con las empresas publicadas (P92)`.

**Hecho cuando** la portada coincide con Landing y Landing-Movil y su sección de empresas muestra datos reales.

### T13 · Front · El directorio en `/empresas`

**Archivos:**
- `src/areas/public/site/pages/DirectoryPage.tsx` y su test;
- `api/directory.ts` y `components/CompanyCard.tsx`;
- `src/shared/ui/Pagination.tsx`: la forma web del teléfono, como M-Directorio;
- `src/app/routes.tsx`.

**Copiá de:** `areas/business/users/pages/UsersPage.tsx` y `roles/pages/RolesPage.tsx` (`usePagedQuery`).

- [ ] **Rojo** (`DirectoryPage.test.tsx`):
  - «con empresas, sin resultados ("Ninguna empresa coincide con la búsqueda" y "Limpiar búsqueda") y todavía no hay ("Todavía no hay empresas publicadas")»;
  - «la búsqueda vive en la URL, espera 300 ms y vuelve a la página 1»;
  - «10 por página, con 1 y con 50 empresas: "1–10 de 50" y "Página 1 de 5"»;
  - «en el teléfono: el rango, la página y las flechas, sin el selector de tamaño ni "Cargar más"»;
  - «si falla la carga, el texto común con "Reintentar"»;
  - axe y 390.
- [ ] **Implementación:**
  - `usePagedQuery` con `filterKeys` vacío;
  - `Pagination` suma la forma web del teléfono: el sitio público no es la app, así que no usa «Cargar más»;
  - cada tarjeta navega a `publicSiteUrl(slug)`.
- [ ] **Verde:** el test focal, `usePagedQuery.test.tsx`, `Pagination.test.tsx`, lint y build. Commit `feat(sitio-publico): el directorio de empresas en /empresas`.

**Hecho cuando** `/empresas` coincide con Directorio y M-Directorio en sus tres estados.

### T14 · Front · La página pública del subdominio

**Archivos:**
- `src/areas/storefront/pages/PublicPage.tsx` y su test;
- `src/areas/storefront/api/publicSite.ts`;
- `src/locales/{es,en}/storefront.json`, que suma los textos de la tarjeta.

**Copiá de:** `areas/business/public-page/pages/PublicPagePage.tsx` para leer la página; `shared/ui/format` (`PhoneText`) para el teléfono.

- [ ] **Rojo** (`PublicPage.test.tsx`):
  - «publicada y sin sesión: el logo o las iniciales, el nombre, la descripción, Dirección, Teléfono, Correo y Sitio web, e "Ingresá para continuar"»;
  - «solo muestra los datos de contacto que la organización cargó»;
  - «el teléfono, con el formato de cada cultura (es-AR y en-US) y el enlace `tel:` en E.164»;
  - «No disponible: "Esta página no está disponible" e "Ir a ArquitecturaBase", sin decir por qué»;
  - «en el teléfono, la misma tarjeta, sin ☰»;
  - axe y 390.
- [ ] **Implementación:**
  - `usePublicSite` decide entre «Publicada» y «No disponible»;
  - «Ir a ArquitecturaBase» va a la portada del dominio principal;
  - el teléfono va con `PhoneText`, sin tipos de formato nuevos.
- [ ] **Verde:** el test focal, `parity.test.ts`, lint y build. Commit `feat(sitio-publico): la página de cada organización en su subdominio`.

**Hecho cuando** el subdominio muestra los estados «Publicada, sin sesión» y «No disponible» como sus tableros.

### T15 · Front · «Ingresá para continuar» y la sesión en el subdominio

**Archivos:**
- `src/areas/storefront/pages/PublicPage.tsx`;
- `src/auth/{authConfig.ts,SessionRecovery.tsx}` y sus tests;
- `src/tenancy/{AccessMenu,useProfiles}.tsx`: los enlaces al dominio principal;
- `src/areas/public/auth/pages/{LoginPage,SignupPage}.tsx`, si se aprueba «Para decidir» 5.

- [ ] **Rojo:**
  - «"Ingresá para continuar" entra como persona (`access=consumer`) y vuelve a la misma página»;
  - «con sesión: sin el botón y con tu menú: Perfiles con Personal ✓, "Mi cuenta" en el dominio principal y "Salir"»;
  - «en el teléfono, "Tu cuenta" abre tu menú en una hoja con manija»;
  - «"Salir" vuelve a la página sin sesión»;
  - `authConfig.test.ts`: «en un subdominio, `redirect_uri`, `silent_redirect_uri` y `post_logout_redirect_uri` son del origen propio, y `authority` es el issuer»;
  - con la opción A de «Para decidir» 1: «al abrir o recargar la página, recupera la sesión sin pedir el código» y «una cuenta sin espacio personal sigue sin sesión»;
  - lo que se decida en «Para decidir» 4 (tu menú) y 5 (crear la cuenta).
- [ ] **Implementación:**
  - `signinRedirect` con `returnTo` = la página actual y `extraQueryParams: { access: "consumer" }`;
  - «Salir» usa `useSignOut`;
  - «Mi cuenta» y los perfiles abren el dominio principal (`mainSiteUrl`).
- [ ] **Verde:** los tests focales, lint y build. Commit `feat(sitio-publico): ingresar como persona desde la página pública y volver`.

**Hecho cuando** «Publicada, con sesión» coincide con su tablero y la persona vuelve a la misma página después de ingresar.

### T16 · Front · `navigation/personal.ts`, listo para sumar módulos

**Archivos:** `src/layouts/navigation/{personal,types}.ts`, `navigation.test.tsx` y, si hace falta, el armado de la barra de abajo en `PrivateLayoutFrame`.

- [ ] **Rojo** (`navigation.test.tsx`):
  - «un módulo B2C se suma con una línea, con su enlace y su módulo; apagado, no aparece ni en el menú ni en la barra de abajo»;
  - «Inicio y Mi cuenta siguen igual».
- [ ] **Implementación:**
  - el enlace declara su módulo (`feature`) y entra en el menú lateral y en la barra de abajo del teléfono (hasta cuatro y «Más»);
  - lo filtra `visibleNavigation` con `hasFeature` de `usePermissions` (F5), igual que el menú de la empresa. No se crea otro filtro.
- [ ] **Verde:** el test focal, lint y build. Commit `feat(personal): la navegación personal lista para sumar módulos`.

**Hecho cuando** sumar un módulo B2C al menú es una línea en `navigation/personal.ts`.

### T17 · Front · Los docs y el arnés del front

- [ ] **Arquitectura:**
  - `frontend.md` §3: las rutas por host, `/empresas`, la página del subdominio y el ingreso desde ahí;
  - `arbol.md`: las entradas [E7].
- [ ] **Fichas y punteros:**
  - `docs/rules/{accesos-y-permisos,tests,paginado-y-listados,responsive}.md`;
  - `areas/public/site/AGENTS.md`: nombra «M-Landing», que no existe; son Landing-Movil, Directorio y M-Directorio;
  - los punteros de las carpetas nuevas.
- [ ] **Verde:** `harness.test.ts` en verde y `realinear-docs --detectar` en 0 en el front. Commit `docs(etapa-7): rutas por host, sitio público y área personal en el front`.

**Hecho cuando** los docs del front describen lo que hay, sin [E7] pendientes.

## Puerta (T18 a T20)

### T18 · Las capturas contra el lienzo

**Archivos:** `scripts/capturar-etapa-7.mjs` (copiá `capturar-etapa-6.mjs`) y `docs/design/capturas/etapa-7/` (`manifest.json`, los PNG y `comparacion.md`).

- [ ] **Qué se captura:** cada estado, de la pantalla real y de su tablero, a 1440 × 900 y a 390 × 844, con los mismos datos del tablero (Grupo Delta, Omega S.A. y Sigma S.R.L.):
  - **la portada:** en el teléfono, también con el ☰ abierto;
  - **el directorio:** «Con empresas», «Sin resultados» y «Todavía no hay empresas»; en el teléfono, también con el ☰ abierto;
  - **la página pública:**
    - «Publicada, sin sesión»;
    - «Publicada, con sesión», con tu menú abierto (el menú en la compu y la hoja en el teléfono);
    - «No disponible».
- [ ] **Diferencias:** cada una se corrige o se anota en `comparacion.md` con su motivo, para que el usuario la apruebe.
- [ ] **Revisión:** las capturas se le mandan al usuario antes del cierre.

### T19 · El E2E real

- [ ] **Al empezar el front**, el caso de la etapa se escribe en `scripts/test-e2e-real.test.mjs` y queda en rojo, porque faltan las pantallas.
- [ ] **El recorrido.** Va sin mocks ni intercepción, con PostgreSQL y pickup propios del E2E. La organización del E2E tiene la página publicada, desde la pantalla como en la E6 o desde el preparador; nunca en Development.
  1. un visitante abre la página de la organización en su subdominio y ve «Publicada, sin sesión»;
  2. toca «Ingresá para continuar», ingresa como persona con el código del `.eml` y vuelve a la misma página, con su menú;
  3. con la opción A de «Para decidir» 1, recargar la página conserva la sesión;
  4. «Salir» vuelve a la página sin sesión;
  5. en el dominio principal, `/empresas` busca la organización por una parte del nombre sin acentos, la tarjeta aparece y lleva al subdominio;
  6. el subdominio de una organización suspendida muestra «No disponible». Si no hay una pantalla de la E5 para suspenderla, la deja así el preparador;
  7. en el teléfono (390), la portada y el directorio tienen ☰ y la página pública no.
- [ ] **Los recorridos de las etapas cerradas** (E3a, E3b, E3c, E4 y E6) siguen en verde. Si la E5 sigue abierta y su caso todavía está en rojo porque le faltan pantallas (R06 de su plan), se anota en el informe y se consulta con la Guía antes de cerrar.

### T20 · La puerta general y el cierre

- [ ] **El back:** `dotnet build ArquitecturaBaseMultitenant.slnx` sin advertencias y `dotnet test` completo con Docker.
- [ ] **El front:** `npm run build`, `npm run lint`, `npm test` y `npm run contracts:check`, completos y limpios.
- [ ] **Las rutas:** `ExplicitRouteInventoryTests` con cada ruta nueva y su test.
- [ ] **El aislamiento:** los tests de `Tenancy/` en verde, ampliados a lo nuevo:
  - entre personas;
  - entre el acceso B2C y el B2B de la misma persona;
  - por subdominio;
  - en los datos compartidos.
- [ ] **Las decisiones registradas:** cada decisión nueva queda en su ADR o en «Decisiones tomadas», en su ficha y, si es una conducta, con su test. Una ADR que cambia se enmienda en el mismo commit.
- [ ] **El arnés:** `realinear-docs --detectar` sale 0 en los dos repos, y `HarnessTests` y `harness.test.ts` están en verde.
- [ ] **La puerta de documentación a ciegas:** un agente que lee solo los `AGENTS.md` contesta bien las diez preguntas que se escribieron en T08.
- [ ] **Probar la receta:** un agente sin contexto sigue `personal.md` y suma un módulo B2C de prueba sin preguntar nada que la receta no responda. Se corrige la receta y se descarta el módulo.
- [ ] **Las capturas (T18) y el E2E real (T19):** en verde y revisados por el usuario.
- [ ] **El cierre:**
  - el 7 se suma a `ClosedStages` en los dos repos, solo con todo lo anterior en verde;
  - el informe va en `docs/reviews/<fecha>-etapa-7-publico-y-personal.md`, con los commits, las salidas, las decisiones, las diferencias visuales y los pendientes reales;
  - `aspire stop`, si se levantó;
  - los dos repos quedan en `main`, limpios y sin push.

## Fuera de alcance

- **Los módulos de negocio, B2B y B2C:** cada producto los suma con su plan, copiando Roles y `personal.md`, con `[FeatureGate]`.
- **El dominio propio de una organización** (`www.empresa-a.com.ar`): queda para más adelante (ADR 0031; multitenancy.md §5, «Después, opcional»).
- **WhatsApp:** Etapas 8 y 11.
- **Los avisos de un módulo a las dos partes** (multitenancy.md §10, paso 5): los define cada módulo, con el outbox.
- **El rate limit más estricto en las páginas públicas, la caché en Redis y el fuzz de aislamiento por subdominio:** Etapa 9.
- **«Exportar mis datos»:** Etapa 10.
- **El menú de la persona para un módulo compartido** (qué organizaciones lo prenden): llega con el primer módulo compartido real, porque necesita un dibujo. `personal.md` lo anota y `modulos-habilitados.md` lo marca `Pendiente:` (decisión tomada 2).

## Decisiones tomadas

La Guía tomó las dos primeras al revisar el plan. Las que se tomen al programar siguen la numeración, con su motivo; si cambian una conducta, también van en su ficha y con su test.

1. **La baja en los datos compartidos (T07).**
   - `IConsumerBusinessShared` suma un método para reemplazar la copia: así ningún dato compartido nace sin decir qué borra. Cambia una marca de `Domain/Common`, así que se anota en la ADR 0035.
   - `IRetainedOnConsumerDeletion` declara, por tipo, el motivo y el plazo.
   - Lo retenido se anota en una tabla global con su vencimiento. El `AccountDeletionWorker`, que ya existe, reemplaza la copia al vencer, entrando al espacio personal cerrado.
   - *Motivo:* la ADR 0035 y `multitenancy.md` §3.2 piden las dos cosas, y es técnico: no cambia ninguna pantalla.
2. **Los módulos en un pedido desde el subdominio (T06).**
   - `modulos-habilitados.md` dice que un módulo compartido está prendido para una persona si lo está para la organización con la que interactúa.
   - El `Deal` lo cumple donde se interactúa: un pedido en el subdominio mira los módulos de esa organización.
   - El menú de la persona para un módulo compartido queda para el primer módulo real, porque necesita un dibujo.
   - *Motivo:* es una regla escrita y el `Deal` es la guía que copia un producto.

## Registro de resultados

| Tarea | Estado/evidencia | Commit back | Commit front |
|---|---|---|---|
| T00 | pendiente | — | — |
| T01 | pendiente | | — |
| T02 | pendiente | | — |
| T03 | pendiente | | — |
| T04 | pendiente | | — |
| T05 | pendiente | | — |
| T06 | pendiente | | — |
| T07 | pendiente | | — |
| T08 | pendiente | | — |
| T09 | pendiente | | — |
| T10 | pendiente | — | |
| T11 | pendiente | — | |
| T12 | pendiente | — | |
| T13 | pendiente | — | |
| T14 | pendiente | — | |
| T15 | pendiente | — | |
| T16 | pendiente | — | |
| T17 | pendiente | — | |
| T18 | pendiente | — | |
| T19 | pendiente | | |
| T20 | pendiente | | |

No se declara el cierre a partir de esta tabla sin completar T20.

## Para decidir antes de empezar

Son puntos ambiguos o contradictorios entre el plan maestro, los docs y el lienzo. Cada uno trae una recomendación. Al decidirlos, el texto de las tareas se ajusta y esta sección se borra (T00).

### 1. La sesión en el subdominio y el dominio de desarrollo

**El problema.** `multitenancy.md` §5 dice que, si la persona ya ingresó, no tiene que volver a hacerlo en otro subdominio (`prompt=none`). Lo mismo hace falta para que la sesión siga después de recargar la página. Las dos cosas necesitan un iframe silencioso del subdominio al dominio principal, el mismo mecanismo que ya usa el dominio principal al recargar (`SessionRecovery`). Hoy lo impiden dos cosas:
- la CSP (`frame-src 'self'` y `frame-ancestors 'self'`);
- en desarrollo, el dominio principal es `localhost:5174` y los subdominios, `*.localtest.me`: son sitios distintos, así que la cookie de `/connect` no llega al iframe.

**A (recomendada):** la sesión se recupera al abrir y al recargar, como dice §5.
- La CSP se abre lo justo: los subdominios pueden enmarcar `/connect/authorize`, y el subdominio puede enmarcar el dominio principal.
- En silencio nunca se crea el espacio personal: una cuenta que no lo tiene sigue sin sesión en el subdominio hasta que toca «Ingresá para continuar».
- En desarrollo, el dominio principal pasa a `https://localtest.me:5174`. Cambian `appsettings.Development.json`, el issuer por defecto del front y la URL del E2E.
- Para ingresar con Google en desarrollo, hay que sumar `https://localtest.me:5174/signin-google` en la consola de Google.
- El certificado de desarrollo no cubre `localtest.me`: el navegador avisa una vez por dirección, como ya pasa hoy con los subdominios. El E2E ya ignora ese aviso.

**B:** sin sesión silenciosa en el subdominio.
- La sesión dura lo que la pestaña después de «Ingresá para continuar».
- Al recargar o al abrir otra organización se vuelve a ver el botón. Tocarlo entra sin pedir el código si la cookie sigue viva.
- No cambian la CSP ni el dominio de desarrollo, y se corrige §5.

### 2. Pedir algo desde la página pública

**El problema.** Hoy `[PublicSite]` va solo con `[AllowAnonymous]`: lo dicen `AccessDeclarationTests` («public site requires anonymous without access») y `multitenancy.md` §8. Pero en §10 la persona pide «desde la página de la organización», y esa ruta necesita dos cosas:
- la sesión de la persona (`[Access(Consumer)]`);
- la organización del subdominio (`[PublicSite]`).

**Recomendado:** admitir `[PublicSite]` + `[Access(Consumer)]`, nunca con Business ni Platform.
- El tenant activo sale del claim, y la otra parte, del subdominio, nunca del cuerpo.
- Se enmiendan la ADR 0021 (en el índice), `multitenancy.md` §8, `api-http.md`, `docs/rules/multitenancy.md` y `AccessDeclarationTests`.

**Alternativa:** `[Access(Consumer)]` con el slug de la organización en la ruta (`/api/<módulo>/{slug}/…`), resuelto contra lo publicado. Sirve también desde el dominio principal, pero suma otra forma de nombrar a la otra parte.

### 3. La sección de empresas de la portada

**El problema.** El tablero dibuja el buscador y tres empresas, pero no dice qué hace el buscador ni qué se ve sin empresas publicadas.

**Recomendado:** escribir y apretar Enter abre `/empresas?search=<texto>`, el directorio, que ya tiene «Sin resultados». Sin empresas publicadas, la sección muestra «Todavía no hay empresas publicadas», el texto del directorio. Diseño suma ese estado a Landing y Landing-Movil antes de programarlo.

**Alternativa:** el buscador filtra las tres tarjetas en el lugar y la sección se oculta sin empresas. Las dos cosas se dibujan antes.

### 4. Tu menú en la página pública

**El problema.** Pagina-Publica dibuja a una persona que solo tiene Personal. No dice qué pasa si también es de una organización, ni adónde va «Mi cuenta», que vive en el dominio principal.

**Recomendado:** el mismo menú de siempre, porque `multitenancy.md` §3 dice que «la lista es la misma en los dos lados». Elegir una organización abre `/org` del dominio principal en esa organización, y «Mi cuenta» abre `/cuenta`.

**Alternativa:** en la página pública, tu menú muestra solo Personal, Mi cuenta y Salir, como el tablero.

### 5. Crear la cuenta desde «Ingresá para continuar»

**El problema.** Una persona sin cuenta llega a Ingreso y toca «Creá una». Hoy ese enlace pierde el regreso: al terminar el registro, la persona queda en su inicio personal y no en la página de la organización.

**Recomendado:** el registro conserva el regreso y, al terminar, vuelve a la misma página pública. No hacen falta pantallas nuevas.

**Alternativa:** queda como está, y la persona vuelve a abrir la página.

### 6. `/empresas` con sesión

**El problema.** El tablero Directorio dibuja solo el encabezado sin sesión, con «Ingresá como empresa» e «Ingresar».

**Recomendado:** en la E7, el mismo encabezado con sesión o sin ella, como el tablero. Si querés tu menú ahí, Diseño lo dibuja primero.

**Alternativa:** tu menú en lugar de los botones, como en la página pública con sesión. Necesita el dibujo.
