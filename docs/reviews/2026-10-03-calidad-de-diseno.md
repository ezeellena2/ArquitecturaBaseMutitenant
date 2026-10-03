# Revisión de calidad de diseño: back y front

- **Fecha:** 2026-10-03.
- **Código revisado:** back `1202433` y front `961f786`. Quedó afuera Plataforma, que está en curso.
- **Cómo:** cinco revisores en paralelo, solo lectura, con diez criterios:
  1. responsabilidad única;
  2. inyección de dependencias;
  3. capas;
  4. dominio;
  5. duplicación;
  6. sobreingeniería;
  7. clean code;
  8. errores;
  9. tests;
  10. consistencia con la referencia.
- **Qué está verificado:** los bugs marcados «Confirmado» los verifiqué en el código actual.
- **Detalle:** cada revisor dejó su informe con archivo y línea en [`2026-10-03-calidad-de-diseno/`](2026-10-03-calidad-de-diseno/).

## 1. Veredicto

- **Los cimientos están bien hechos y no hay que tocarlos.** Están al nivel de ArquitecturaBase y los cubren guardas con caso de control.
  - Back: guardado con `IUnitOfWork`, aislamiento con RLS, `Result` y errores, caché, value objects y `OperationLog`.
  - Front: `httpClient`, `useIdempotentMutation`, formatos, `DataTable` y el estado en la URL.
- **El problema está en cómo se arman los casos de uso.**
  - **Back: sobran clases.** `Application/Services` tiene 118 clases sin contar Plataforma. Solo identidad tiene 84 para 20 servicios de entrada, muchas creadas para no pasar el tope de 8 dependencias, y crear una organización recorre 10 clases.
  - **Front: faltan piezas comunes.** Cada pantalla resuelve a mano los errores, los permisos, el paginado y los formularios.
- **Hay reglas copiadas que ya no coinciden entre sí:** la búsqueda, el estado de un miembro, el alcance de un rol, el enmascarado de datos personales y el alta de una cuenta. De ahí salen varios bugs de la sección 4.
- **No hace falta reescribir.** Antes de la Etapa 7 hay que hacer tres cosas:
  - corregir una regla del arnés;
  - unificar unas diez piezas duplicadas;
  - arreglar las referencias que se copian.

## 2. Puntajes (1 a 5)

Escala: 4 está bien, 3 es aceptable con deuda y 2 hay que corregirlo antes de que se copie.

| Área | Promedio | Lo mejor | Lo más flojo |
|---|---|---|---|
| Back · núcleo y arquitectura | 3,4 | Capas, abstracciones y errores (4) | Tope de dependencias, composición de `AddApplication` y modo de host (3) |
| Back · identidad e ingreso | 2,8 | Inyección y errores (4) | Responsabilidad única, duplicación, sobreingeniería y consistencia (2) |
| Back · organización, roles y empresas | 2,8 | Inyección (4) | Responsabilidad única, duplicación y consistencia con Roles (2) |
| Front · núcleo compartido | 2,7 | HTTP, idempotencia y estado (3) | Duplicación, abstracciones y consistencia (2) |
| Front · pantallas | 2,5 | Casi sin sobreingeniería (4) | Responsabilidad única, formularios, duplicación y consistencia (2) |

## 3. Por qué pasó

No fue falta de instrucciones. Hubo tres causas.

1. **Una regla del arnés empuja en la dirección equivocada.** `ServiceDependencyCountTests` también cuenta el logger, el reloj, el validador y la unidad de trabajo, que las propias reglas obligan a inyectar. Hoy 46 de las 118 clases tienen 7 u 8 dependencias. Para no pasarse del tope pasaron tres cosas:
   - se crearon clases que solo reenvían;
   - la transacción bajó a los helpers;
   - `TransactionBoundaryTests` se aflojó con una lista blanca de 14 helpers, que contradice `guardado.md` («un helper no recibe `IUnitOfWork`»).
2. **Las referencias se desviaron.** Los `AGENTS.md` y las fichas mandan copiar archivos que ya no cumplen la regla:
   - la escritura de `OrganizationSettingsService`, que delega todo en otra clase;
   - `RoleEditorPage`, que no usa RHF ni `useIdempotentMutation`.

   Cada área nueva copió a la anterior.
3. **Al front le faltan piezas comunes.** `frontend.md` promete estas piezas, pero no existen, así que cada pantalla las resolvió a su manera:
   - `usePermissions`;
   - un mensaje de error común para las mutaciones;
   - el listado paginado completo;
   - la salida;
   - una sola fuente para los accesos y la cultura.

## 4. Bugs

Cada uno se arregla con un test que primero falla y después pasa.

| # | Qué pasa | Dónde | Estado | Esfuerzo |
|---|---|---|---|---|
| B1 | Buscar con texto entre los miembros de una empresa da 500 | `CompanyMembershipReader.cs:110`: `ESCAPE '\\'` dentro de un raw string son dos caracteres, y PostgreSQL exige uno. Falta el test de búsqueda | Confirmado | S |
| B2 | En el alta de un rol, si el nombre ya existe y la persona lo corrige, el segundo «Guardar» da 422 hasta recargar | `RoleEditorPage.tsx:107` crea la clave con `useMemo` y la usa en `:141`. El back guarda las respuestas 4xx por clave. Hay que usar `useIdempotentMutation` | Confirmado | S |
| B3 | A la tabla de miembros de la ficha de Empresa le falta la columna «Estado», que el tablero sí tiene | `CompanyDetailPage.tsx:178`: el `.slice(0, -1)` saca la última columna, que es Estado | Confirmado | S |
| B4 | Si alguien con `company.members.read` y sin `roles.read` abre la ficha de Empresa, la pantalla entera pasa a «Sin permiso» | `CompanyDetailPage.tsx:94` pide `/api/roles/paged`, que exige `roles.read`. `httpClient.ts:117` convierte cualquier 403 de un GET en pantalla bloqueada | Confirmado | S (el arreglo de fondo es F1) |
| B5 | En Página pública, los errores del slug y del logo dicen «La página debe estar entre {From} y {To}.», y el del nombre sale con el texto genérico de FluentValidation | `UpdatePublicPageRequestValidator.cs:15`, `:16` y `:27` | Confirmado | S |
| B6 | Si se borra el único ítem de la última página, la lista queda vacía en vez de volver a la página anterior | Las 5 listas llaman `usePagination(undefined)`, así que la corrección nunca corre | Confirmado | S (de fondo, F3) |
| B7 | Un rol con personas asignadas puede cambiar de alcance, y esas asignaciones dejan de valer en silencio | `Role.Update` (`Role.cs:78-90`) y `RoleService.UpdateAsync` no lo controlan. `RoleReader.cs:94-113` descarta las asignaciones que ya no coinciden | Confirmado; se resuelve con D2 | S |
| B8 | Un invitado con su membresía de empresa inactiva aparece en dos filtros y se cuenta dos veces | `CompanyMembershipReader.cs:135-154` frente a `CompanyMembershipService.cs:88-96` | A confirmar con el test | S |
| B9 | Cada guardado de Configuración deja dos entradas de auditoría, y el diff podría incluir campos sin cambios | `OrganizationSettingsUpdater.cs:37` más el interceptor. `MarkUpdated` marca la entidad entera | A confirmar con el test | S |
| B10 | Dos altas simultáneas de roles con el mismo nombre dan 500 | `RoleService` no traduce la violación del índice único | A confirmar con el test | S |
| B11 | Configuración, Página pública y la ficha de Usuario pierden lo escrito al salir, sin avisar | Esas tres pantallas no usan `useUnsavedChangesGuard` | A confirmar | S cada una |
| B12 | Si se corta la red al guardar, aparece «Algo salió mal», sin «No pudimos conectarnos» ni «Reintentar» | No hay `MutationCache` y cada pantalla arma su propio mensaje | A confirmar | (F2) |

**Bugs latentes, que van a aparecer en su etapa:**
- **Etapa 8:** quitar un teléfono o hacerlo principal lanza `NotSupportedException` en `EmailTemplateRenderer.cs:104-108`, dentro de la transacción.
- **Al sumar un documento legal nuevo:** las cinco altas de cuenta aceptan solo Términos y Privacidad, escritos a mano. El chequeo de aceptación exigiría también el documento nuevo, así que toda cuenta nueva nacería bloqueada.
- **En los tests:** `OrganizationUsersTests.InvitationEmailAsync` descifra todo el outbox y falla en la suite completa. Ya se lo pasé a Codex.

## 5. Decisiones (tomadas por el dueño del producto el 2026-10-03)

- **D1 · Tope de dependencias** ([ADR 0039](../decisions/0039-tope-de-dependencias-de-negocio.md)). El tope sigue en 8, pero no cuentan las cuatro dependencias que las reglas obligan a inyectar: `ILogger<T>`, `TimeProvider`, `IRequestValidator` e `IUnitOfWork`.
  - Ninguna clase actual queda por encima del tope: el máximo de dependencias de negocio es 8, en dos clases.
  - Se borran las clases que solo reenvían, y la transacción vuelve al método público del servicio, como en `RoleService`.
  - La lista blanca de `TransactionBoundaryTests` vuelve a tener solo los dos seeds.
  - Se aplica ya a lo que falta de la Etapa 5.
- **D2 · Un rol con personas asignadas no puede cambiar de alcance.** Es el mismo criterio que para borrar un rol con usuarios (`RoleErrors.HasUsers`): el error aparece al guardar. Hay que actualizar `docs/features/roles.md`.

## 6. Plan

### Ahora, en la Etapa 5 (sin frenar su trabajo)
Por D1 (ADR 0039):
- Cambiar cómo cuenta `ServiceDependencyCountTests`.
- Actualizar `guardado.md` y `backend.md`.
- En lo que falta de Plataforma, no agregar helpers que reciban `IUnitOfWork` ni clases que solo reenvían.

### Consolidación: al cerrar la Etapa 5, antes de la 7
Va en este orden. Cada punto es un commit con su test.

1. **Bugs:** B1 a B12.
2. **Arnés y referencias** (S):
   - Los «Copiá de» de los `AGENTS.md` y de las fichas tienen que apuntar a referencias sanas, hasta que se corrija Roles del front:
     - back: `RoleService`, `RoleReader` y `CompanyReader`;
     - front: `personal/account` para formularios y diálogos, `UsersPage` para listados.
   - Crear el `AGENTS.md` de `Services/OrganizationUsers`.
   - Llevar `backend.md` §4 al árbol real.
   - Cerrar la lista de sufijos y verificarla con un test.
   - Agregar un test que exija un mensaje propio en cada regla de validación.
   - Borrar `Stage3aInventoryTests`.
3. **Back: la transacción vuelve al servicio y se borran los reenvíos** (M). Al terminar, se vacía la lista blanca.
   - Usuarios: `OrganizationUserLifecycleCoordinator` y `OrganizationUserExitEffects`.
   - Empresas: `CompanyCreator`, `CompanyMembershipWriter` y la mitad de `CompanyMembershipRoleManager`.
   - Configuración: `OrganizationSettingsUpdater`.
   - Alta de empresa: pasa de 10 clases a 3 o 4.
   - `SignupVerificationFlow`.
4. **Back: una sola pieza para cada regla duplicada** (M):
   - `RoleAssignment.Check` en Domain y un solo guard para Usuarios y Empresas, con un solo código de error.
   - El estado de un miembro en una sola expresión SQL, que sirva para filtrar, mostrar y contar.
   - La búsqueda de miembros con `ApplySearch` y `ToPagedResultAsync`, sin acentos como el resto.
   - Un único `PersonalDataMask` en `Application/Common`; hoy hay cinco versiones distintas.
   - `UniqueKeys`: los nombres de índice como constantes, usadas en `HasDatabaseName` y en el `catch`.
   - `AddApplication` partido en un `*Registration.cs` por área. Borrar el registro duplicado de `LastTenantAdminGuard` y registrar las opciones de Application en `AddApplication`.
5. **Front: piezas comunes** (M). Son las que va a necesitar la Etapa 7.
   - **F1.** El 403 bloquea la vista solo cuando viene de la consulta principal de la página (`QueryCache` con `meta`), no desde el transporte.
   - **F2.** `apiErrorMessage(error, t)` y un `MutationCache` para red, 5xx y 429. Después se borran los 4 `failureMessage`.
   - **F3.** `usePagedQuery`, que compone filtros, debounce, consulta, corrección de página y `Pagination`.
   - **F4.** `tenancy/access.ts` (inicio y login de cada acceso, organización activa, estados no disponibles) y `useSignOut`.
   - **F5.** `usePermissions` (`can` y `canInCompany`) con códigos tipados, y un manifiesto de rutas único para `routes.tsx` y el menú.
   - **F6.** Una sola `resolveCulture` y `useEffectiveCulture`.
   - **F7.** `useEditPage` (guard, «Cambios sin guardar» y 409) y un `errors.ts` por feature.
6. **Front: Roles como referencia de verdad** (M):
   - RHF con zod, `useIdempotentMutation`, `errors.ts`, `lib/` con tests, axe y vista a 390 px.
   - Partir `CompanyDetailPage` (316 líneas, 13 `useState`, 5 diálogos) en los componentes que ya define `arbol.md`.
   - Mostrar los estados con `StatusBadge` y `statusTones`.

### Primer paso de la Etapa 8 (antes de sumar teléfonos)
- `AccountRegistrar` con `LegalConsentRecorder`. Hoy hay 5 altas y 5 consentimientos copiados.
- `LoginCodeAttemptVerifier`, para el bloqueo por intentos. Hoy está 3 veces.
- `LoginMethodLinker`, para sumar un método verificado. Hoy está 8 veces.
- `AccountLifecycle` en `Domain/Users`. Hoy vive en el `ApplicationUser` de Infrastructure y se chequea en 7 lugares.
- `enum LoginMethodChange` en lugar de los textos «Added», «Removed» y «Primary».

### Al tocarlo
- `DisplayFormatter` con API tipada.
- `TenantResolutionMiddleware` delegando en un servicio, como `LegalAcceptanceMiddleware`.
- `AuditTrailInterceptor` con el diff y el enmascarado por atributo.
- El formato de los cambios auditados como value object, y el reader de auditoría sin decidir qué mostrar.
- `MembershipDeletionParticipant` a Application.
- `UserManager` sin guardados propios.
- El ticket de la baja fuera de la metadata del Error.
- Los campos del front con `useParsedInput`.
- Los selectores del front con una base común.
- El formateo del front sin piezas triplicadas.
- Los valores mágicos.

### Backlog
- La velocidad de la suite de integración: corre en una sola colección en serie y levanta 19 contenedores extra.
- Los tests del front atados a clases de Tailwind.
- Los íconos: quedarse con lucide y borrar las piezas del kit que nadie usa.
- Los stores de módulo copiados.
- Un solo lector de IL en el arnés.

## 7. Lo que hay que conservar como patrón

**Back**
- `UnitOfWork`: único límite, rollback seguro y traducción de 23505 y de la concurrencia. `FakeUnitOfWork` cumple el mismo contrato.
- `TenantContext`, los filtros por clase de dato y `TenantIsolationModelValidator`.
- `HybridCacheExtensions` con `CacheKeys`.
- `QueryableExtensions`: orden con desempate por Id, búsqueda con unaccent y cursor por fila.
- `Result`, `Error`, `ValidationError`, `ProblemDetailsMapper` y `OperationLog`.
- Value objects con una fábrica que devuelve `Result` (`Email`, `PhoneNumber`, `Money`, `TaxId` y `TenantSlug`).
- `RoleService`, `RoleReader` y `CompanyReader` como referencia de servicio y de listado.
- `OrganizationDomainVerifier`: la llamada externa va fuera de la transacción; después toma el lock y recarga con la versión.
- Repositorios con versión esperada y locks tomados en orden.
- `PermissionService` y `LegalAcceptanceMiddleware`, que delega en un servicio.
- `LoginCodeIssuer`, `LoginCodeVerifier` y `AccessSwitchPolicy`.
- Los puertos de módulo y el outbox cifrado.
- Las guardas de arquitectura con caso de control.

**Front**
- `httpClient`: renovación compartida, ETag y `ApiError` tipado.
- `useIdempotentMutation`.
- `DataTable` con columnas tipadas.
- Filtros y página en la URL (`useFilters` y `useQueryUpdate`).
- `createFormatters`: puro, con los mismos casos que el back.
- `useUnsavedChangesGuard`, `useRestoreFocusOnClose` y `useRetryAfterCountdown`.
- `FormField`, `ConfirmDialog` y `Banner`.
- Como moldes:
  - `personal/account`: un diálogo por archivo y RHF con zod y `applyApiErrorToForm`;
  - `public/auth`: `lib/` pura con tests y `errors.ts`.
- Las guardas mecánicas: `structure.test`, `format-usage.test`, `parity.test` y `contracts:check`.
- TypeScript estricto, sin `any`.

## 8. ¿Hay sobreingeniería?

- **Sí, en cómo se arman los casos de uso del back:**
  - clases que solo reenvían;
  - un caso de uso repartido en 10 clases;
  - tres `OperationLog` anidados por pedido;
  - un `Result` que nunca falla.
- **Sí, en piezas del front que nadie usa:**
  - `StatusDot` y `Sheet`;
  - `icons.tsx`, con 29 SVG propios, mientras lucide se usa en 44 archivos;
  - `useLanguagePreference`;
  - `usePagination(result)`.
- **No, en los cimientos:** UoW, RLS, idempotencia, caché y `Result` son lo justo para un multitenant B2B + B2C.
- **En el front, más que sobrar, falta:** las piezas comunes del punto 5 del plan reducen código en lugar de sumarlo.
