# Consolidación previa a la Etapa 7 — plan ejecutable

> Plan de implementación. Seguir el orden, completar las casillas con evidencia y commitear cada cambio coherente y verificado con rutas explícitas.

**Estado:** aprobado por el usuario el 2026-10-03.

**Objetivo:** que la Etapa 7 copie referencias sanas. Para eso:
- se corrigen los bugs de la revisión de calidad;
- se aplica el ADR 0039 a lo que ya existe;
- se unifica cada regla duplicada;
- se crean las piezas comunes del front.

No hay pantallas nuevas. Solo se suman los teléfonos que faltaban de pantallas ya programadas (lienzo v78).

**Fuentes:**
- La [revisión de calidad de diseño](../reviews/2026-10-03-calidad-de-diseno.md) y sus anexos, con archivo y línea de cada hallazgo. Las líneas son del commit `1202433` del back y `961f786` del front, así que hay que buscar por símbolo.
- El [ADR 0039](../decisions/0039-tope-de-dependencias-de-negocio.md) y la decisión D2 de la revisión.
- El lienzo v78 (`../../../ArquitecturaBaseMutitenantFront/docs/design/lienzo/`), para lo que se ve: la columna Estado de Empresa, los mensajes y el formato del enmascarado.
- AGENTS.md, `backend.md`, `frontend.md`, `arbol.md` de los dos repos y las fichas de `docs/rules/`.

**Cuándo empieza:** cuando estén commiteados la tanda de alineación (paquetes 1 y 3 e Inicios) y el back pendiente de la Etapa 5 (T10 a T18), con `git status` limpio en los dos repos. El front de la Etapa 5 (T19 a T25) va después de esta consolidación, para usar sus piezas comunes.

**Quién:** Claude programa y verifica. Usa dos agentes en paralelo solo donde los archivos no se cruzan: el back en los bloques 3 y 4, y el front en los bloques 5 y 6. Cada entrega se revisa antes del commit.

**Restricciones:**
- **Repositorio:** se trabaja en `main`, sin push. Commits chicos en español, con rutas explícitas.
- **Conducta:** es un refactor y la conducta no cambia, salvo en los bugs y en D2. Si un test se rompe solo por estructura (clases, rutas de archivo), se cambia para que pruebe conducta.
- **Sin sobreingeniería:**
  - ninguna abstracción nueva sin dos usos reales;
  - ningún detector ni lector de IL nuevo;
  - antes borrar que envolver.
- **Hallazgos:** antes de corregir uno, se confirma en el código actual. Si no se sostiene, se anota en el informe y se sigue.
- **Tests:**
  - dirigidos mientras se trabaja;
  - la suite completa del back al cerrar los bloques 3 y 4;
  - la del front al cerrar los bloques 5 y 6;
  - las dos en la puerta.
- **Lienzo:** no se toca. Lo que cambie lo que ve o puede hacer un usuario se pregunta.

**Fuera de alcance:** lo de «Primer paso de la Etapa 8», «Al tocarlo» y «Backlog» de la revisión.

## Seguimiento

### 0. Línea de base y documentación

- [ ] `main` limpio y último commit en los dos repos; Aspire apagado.
- [ ] El ADR 0039 está aplicado en `ServiceDependencyCountTests` (se le pidió a la Etapa 5). Si no lo está, aplicarlo primero:
  - el test con su caso de control;
  - `capas-y-flujo.md`, `guardado.md` y `backend.md` §4.
- [ ] Línea de base: `realinear-docs --detectar` en los dos repos. El 2026-10-03, el back dio 372 hallazgos automáticos, 193 de modelo y 217 preguntas.
- [ ] Aplicar lo automático con `realinear-docs --sin-modelo` en los dos repos.
- [ ] Resolver los hallazgos de modelo y las preguntas, comparando con el código:
  - `arbol.md` de los dos repos, llevado al árbol real;
  - los códigos de error, permisos y rutas, cada uno en su ficha de `docs/features`;
  - la fila de `capas-y-flujo.md` en AGENTS.md.

  Al usuario va solo lo que cambia el producto.
- [x] Sumar la consolidación al plan maestro como paso previo de la Etapa 7. Se hizo al aprobar el plan.
- [ ] **Hecho cuando** `--detectar` sale 0 en los dos repos.

### 1. Bugs que se arreglan por su cuenta (test rojo → arreglo → verde)

- [ ] **B1 · Buscar con texto entre los miembros de una empresa da 500.**
  - Test: integración de `GET /api/companies/{id}/members/paged?search=`, con y sin invitaciones pendientes.
  - Arreglo: un solo carácter en el `ESCAPE` de `CompanyMembershipReader`. El bloque 4 después lo reemplaza por `ApplySearch`.
- [ ] **B2 · El alta de un rol se traba con 422 después de un error.**
  - Test de `RoleEditorPage`: el nombre está repetido (409), se corrige y el segundo «Guardar» sale con otra clave y crea el rol.
  - Arreglo: `useIdempotentMutation`.
- [ ] **B3 · A los miembros de Empresa les falta la columna «Estado».**
  - Test de encabezados: Miembro, Roles en la empresa, Estado y Acciones, como el tablero Empresa.
  - Arreglo: la factory de columnas recibe la de acciones, y se borra la columna de relleno de Empresas.
- [ ] **B5 · Página pública muestra mensajes de validación equivocados.**
  - Test del validador: cada regla tiene su texto; el slug y el logo ya no dicen «La página debe estar entre…», y el nombre usa `ValidationRules.OrganizationName()`.
  - Arreglo: textos propios en `Validation.resx` y `.en.resx`.
- [ ] **B7 + D2 · Un rol con personas asignadas no puede cambiar de alcance.**
  - Tests de dominio o servicio, y de integración: si el rol tiene asignaciones, cambiar el alcance da un error propio con el conteo; si no tiene, el alcance cambia.
  - Front: el error aparece al guardar, igual que al borrar.
  - Textos en es y en; actualizar `docs/features/roles.md`.
- [ ] **B9 · Configuración audita dos veces cada guardado.**
  - Test: un guardado deja una sola entrada, y si se edita un campo, el diff tiene solo ese campo.
  - Arreglo: sacar la entrada manual que sobra y hacer que el interceptor omita las propiedades que no cambiaron.

B4, B6, B8, B10, B11 y B12 se arreglan con su pieza, en los bloques 4 y 5.

### 2. Arnés y referencias

- [ ] Los «Copiá de» apuntan a referencias sanas:
  - **back:** `RoleService`, `RoleReader` y `CompanyReader`. Va en los `AGENTS.md` de `Services/PublicSite`, `Services/OrganizationSettings` y `Persistence/Readers`, y en `paginado-y-busqueda.md`;
  - **front:** `personal/account` para formularios y diálogos, y `UsersPage` para listados, hasta que el bloque 6 deje Roles como referencia.
- [ ] Crear el `AGENTS.md` de `Services/OrganizationUsers`.
- [ ] Agregar un test que exija un mensaje propio en cada regla de validación: recorre `CreateDescriptor()` de cada validador del ensamblado.
- [ ] Borrar `Stage3aInventoryTests`.
- [ ] `OrganizationUsersTests.InvitationEmailAsync`: guardar los ids del outbox antes de invitar, como hace `InvitationsTests`, en lugar del try/catch que saltea los mensajes que no se pueden descifrar.

### 3. Back: la transacción vuelve al servicio, sin clases que solo reenvían

Cada servicio de entrada abre la transacción en su método público, y los helpers trabajan adentro sin recibir `IUnitOfWork`. Si un servicio pasa de 8 dependencias de negocio, se parte por caso de uso; por ejemplo, en Usuarios, las invitaciones aparte de los miembros.

- [ ] **Usuarios:**
  - `OrganizationUserEditor`, `OrganizationUserStatusEditor`, `OrganizationUserRemover` y `OrganizationUserInvitationManager` dejan de recibir `IUnitOfWork`;
  - se borran `OrganizationUserLifecycleCoordinator` y `OrganizationUserExitEffects`.
- [ ] **Empresas:**
  - `CompanyCreator` y `CompanyMembershipWriter` dejan de recibir `IUnitOfWork`;
  - `CompanyMembershipRoleManager` pierde los métodos que solo reenvían;
  - el control «la empresa existe y está activa» queda una sola vez, adentro de la transacción.
- [ ] **Configuración, dominios y página pública:**
  - `OrganizationSettingsService` valida y abre la transacción; `OrganizationSettingsUpdater` se absorbe o deja de recibir `IUnitOfWork`;
  - `OrganizationDomainRegistrar`, `OrganizationDomainRemover`, `OrganizationDomainVerifier` y `PublicPageEditor` dejan de recibir `IUnitOfWork`;
  - el verificador consulta el DNS fuera de la transacción, y toma el lock y recarga con versión adentro.
- [ ] **Alta de empresa:**
  - `BusinessSignupService` abre la transacción;
  - `BusinessOrganizationCreator`, `BusinessSignupTransactionRunner` y `BusinessSignupCodeFlow` dejan de recibir `IUnitOfWork`;
  - pasa de 10 clases a 3 o 4, con un solo `OperationLog` por pedido;
  - `TenantProvisioner` se conserva, porque lo usa Plataforma.
- [ ] **Ingreso:** se borra `SignupVerificationFlow`, y quien lo usaba inyecta directo.
- [ ] **Plataforma:** los helpers de la Etapa 5 que reciben `IUnitOfWork` hacen el mismo cambio.
- [ ] **Listas blancas:** en `TransactionBoundaryTests` quedan solo los dos seeds. Con el mismo criterio se revisa la lista de `TenantScopeUsageTests`.
- [ ] **Hecho cuando** la suite completa del back pasa y la lista blanca no tiene helpers.

### 4. Back: una sola pieza por regla duplicada

- [ ] **Alcance de un rol:**
  - `RoleAssignment.Check` en Domain: una función pura que devuelve el error o nada;
  - un solo guard para Usuarios y Empresas, con un solo código de error;
  - `Assign` recibe un valor simple, no la entidad.

  Tests: la matriz de dominio y la de permisos efectivos (`ListRoleIdsForUserAsync`).
- [ ] **Estado de un miembro:** una sola expresión SQL para filtrar, mostrar y contar (con un `GroupBy`), en Usuarios y en Miembros de empresa. La política de C# queda como especificación, con un test de paridad.
  - Esto arregla **B8**. Test: un invitado con su membresía inactiva aparece en un solo estado y se cuenta una vez.
- [ ] **Búsqueda:** miembros e invitaciones usan `ApplySearch` y `ToPagedResultAsync`, sin SQL crudo ni `EscapeLike` propio. Tests de búsqueda con acentos en `/api/users/paged` y `/members/paged`.
- [ ] **Enmascarado:** `PersonalDataMask` en `Application/Common`, para correo y teléfono, con el formato que muestra el lienzo.
  - Lo usan el interceptor de auditoría, la cuenta, los avisos y el reader de auditoría.
  - Se borra `IPhoneNumberParser.Mask`.
- [ ] **Índices únicos:** `UniqueKeys` con sus nombres, usados en `HasDatabaseName` y en cada `catch`. Test: cada clave existe en el modelo.
  - `RoleService` traduce la clave del nombre, y eso arregla **B10**. Test: dos altas simultáneas con el mismo nombre dan una creada y un 409.
- [ ] **Registro:**
  - `AddApplication` se parte en un `*Registration.cs` por área;
  - se borra el registro duplicado de `LastTenantAdminGuard`;
  - las opciones de Application (`LoginCodeOptions`, `InvitationOptions` y `OutboxOptions`) pasan a `AddApplication`.

  Tests: cada clase de `Services` se registra una sola vez, y `ApiFactory` corre con `ValidateOnBuild`.
- [ ] **Sufijos:** la lista cerrada de sufijos de helpers, con los que quedaron después del bloque 3, va en `capas-y-flujo.md` y la verifica `ApplicationServicesTests`.
- [ ] **Hecho cuando** la suite completa del back pasa.

### 5. Front: piezas comunes

- [ ] **F1 · El 403 bloquea la vista solo si viene de la consulta principal.**
  - `httpClient` solo transporta, y `QueryCache.onError` publica cuando la consulta lo declara en `meta`.
  - `Legal.AcceptanceRequired` sigue su camino.
  - Test: un 403 en una consulta secundaria no reemplaza la pantalla.
- [ ] **F2 · Un mensaje de error común:** `shared/api/errorMessage.ts` y un `MutationCache` para red, 5xx (con el traceId) y 429. Se borran los cuatro `failureMessage` y los `error.message` crudos.
  - Arregla **B12**. Test: si se corta la red al guardar, aparece «No pudimos conectarnos» con «Reintentar».
- [ ] **F3 · `usePagedQuery`** en las cinco listas: filtros, debounce, consulta, corrección de página y las props de `Pagination`.
  - Arregla **B6**. Test: al borrar el único ítem de la última página, vuelve a la anterior.
- [ ] **F4 · `tenancy/access.ts`** concentra el tipo `Access` del contrato, el inicio y el ingreso de cada acceso, la organización activa y los estados no disponibles. Se suma `useSignOut`.
  - Fuera de ahí no queda ningún literal `"/org"` ni `"/login/empresa"`.
  - Tests de las funciones puras.
- [ ] **F5 · `usePermissions`,** con `can`, `canInCompany` y códigos tipados, y un manifiesto de rutas único para `routes.tsx` y el menú. Con tests.
- [ ] **B4 · Permisos por empresa de punta a punta.** Hoy un Administrador de empresa no puede usar las pantallas de su empresa (confirmado leyendo el código):
  - `ProfileSnapshotBuilder` manda vacío `effectivePermissions.companies` en `/api/me`;
  - `permissions` trae solo los permisos de la organización;
  - la ficha de Empresa pide `/api/roles/paged`, que exige `roles.read` de organización, y el 403 bloquea toda la pantalla.

  Arreglo:
  - llenar el mapa por empresa en `/api/me`;
  - una ruta de roles asignables de la empresa, bajo `[HasCompanyPermission]`;
  - `canInCompany` en el front.

  Tests:
  - integración con un usuario que solo tiene `CompanyAdmin`: entra a la ficha de su empresa y ve miembros, roles y filtros;
  - E2E real del mismo recorrido.

  Si los Inicios ya llenaron el mapa, solo se verifica.
- [ ] **F6 · Una sola cultura efectiva:** `resolveCulture` y `useEffectiveCulture`, que usan i18n, formatos, datos de referencia y `Accept-Language`. Se borra `useLanguagePreference`. Tests de precedencia.
- [ ] **F7 · `useEditPage`** (guard, «Cambios sin guardar» y 409) y un `errors.ts` por feature, en Configuración, Página pública y la ficha de Usuario.
  - Arregla **B11**. Test: si salís con cambios sin guardar, te pide confirmar.
- [ ] **Teléfonos que faltaban** (lienzo v78, front `f2eff7d`, `generadores/faltan_1004.py`):
  - M-Sesion: 5 estados, como el arranque de la app;
  - M-Perfil-Suspendido: 3 estados;
  - M-Error-Org: 2 estados, con la barra de abajo del miembro (Inicio y Más);
  - M-Avisos: 9 estados, sobre M-Configuracion. Es la forma en el teléfono de F2 y F7:
    - lo pasajero va en una franja oscura abajo;
    - lo que sigue va debajo de la barra;
    - «¿Salir sin guardar?» va en una hoja sin ✕;
    - «Reintentá en 0:39» va en el botón fijo.

  Capturas a 390 px contra cada tablero.
- [ ] **Hecho cuando:**
  - `npm run build` y `npm test` completos pasan;
  - las capturas no cambian, salvo lo que arreglan B3 y B4 y los teléfonos de v78.

### 6. Front: Roles como referencia de verdad

- [ ] **`RoleEditorPage`:**
  - RHF con zod y `errors.ts`;
  - `lib/` con tests, para el selector de permisos y los roles de sistema;
  - los componentes que pide `arbol.md`: `PermissionPicker` y `RoleScopeField`;
  - tests con axe y a 390 px, y «sin coincidencias» en `RolesPage`.
- [ ] **`CompanyDetailPage`,** partida según `arbol.md`:
  - `CompanyDialog` (compartido con Empresas), `AddMemberDialog`, `MemberRolesDialog`, `tabs/CompanyMembersTab` y `tabs/CompanyRolesTab`;
  - cada pieza es dueña de su borrador, su mutación y su error;
  - los diálogos se montan solo cuando están abiertos y devuelven el foco al cerrar.
- [ ] **Estados** con `StatusBadge` y `statusTones` en Usuarios, Empresas y Miembros. Cada feature registra sus tonos, tipados por los enums del contrato, con el color del lienzo para cada estado.
- [ ] Los «Copiá de» del front vuelven a apuntar a Roles.
- [ ] **Hecho cuando:**
  - `npm test` completo pasa;
  - las capturas de Roles, Rol, Empresas y Empresa coinciden con sus tableros.

### 7. Puerta

- [ ] `dotnet build` sin advertencias y `dotnet test` completo; `npm run build` y `npm test` completos.
- [ ] E2E real de Playwright, con front y API reales y sin mocks:
  - ingreso;
  - alta de un rol con nombre repetido y su corrección;
  - un Administrador de empresa en la ficha de su empresa, con búsqueda y estado de los miembros;
  - salir de Configuración sin guardar.
- [ ] Capturas de las pantallas tocadas contra sus tableros, en la compu y en el teléfono, revisadas por el usuario.
- [ ] `realinear-docs --detectar` sale 0 en los dos repos.
- [ ] Informe en `docs/reviews/<fecha>-consolidacion.md`:
  - lo hecho;
  - lo descartado, con los hallazgos que no se sostuvieron;
  - las «Decisiones tomadas».

## Decisiones tomadas

Se completa durante el trabajo.
