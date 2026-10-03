# Calidad de diseño · Front · pantallas

> Informe de un revisor, solo lectura, sobre el commit `961f786 del front`. Los números de línea son de ese commit: buscá por símbolo. Antes de corregir un hallazgo, confirmalo en el código actual. El resumen, los bugs confirmados y el orden de trabajo están en [`../2026-10-03-calidad-de-diseno.md`](../2026-10-03-calidad-de-diseno.md).

## Revisión de diseño del código: front, pantallas (src/areas, sin Plataforma)

Raíz: `../ArquitecturaBaseMutitenantFront/`, commit 961f786 (leí la copia congelada). Todas las rutas de abajo cuelgan de esa raíz. En el back (``) solo miré `IdempotencyFilter.cs` y `RoleService.cs` para confirmar dos hallazgos. Fue solo lectura: no corrí npm ni tests.

### 1. Puntajes

| # | Criterio | Nota | Justificación |
|---|---|---|---|
| 1 | Responsabilidad única | 2 | Cuatro páginas B2B y dos de ingreso juntan en un archivo los datos, 2 a 5 diálogos, la validación y el armado de payloads. `CompanyDetailPage` tiene 13 useState, 5 diálogos y 2 pestañas. `personal/account` muestra la forma correcta. |
| 2 | Lógica y presentación | 2 | Las reglas y los payloads viven en handlers y en el JSX: validación dentro de `mutationFn`, «asignaciones preservadas» calculadas en `UserDetailPage`, el tipo de CUIT elegido en `BusinessSignupPage` y la regla `HasUsers` adelantada en `RolesPage:102`. Solo `public/auth` tiene `lib/` con lógica pura y tests. |
| 3 | Estructura de cada feature | 3 | `api/` + `columns.tsx` + `pages/` es uniforme en business. Pero nadie creó los `components/`, `tabs/`, `lib/` ni `errors.ts` que `arbol.md` ya definía. Hay claves de consulta sueltas y `invitationContract` vive en shared aunque lo usa una sola página. |
| 4 | Duplicación | 2 | Diálogo de empresa ×2, paso del código ×3, términos ×4, punto de estado ×6, caja de sección ×10, helper de error ×4 más 16 expresiones en línea, `toSearch` ×4, `fetchCompanyOptions` ×2. |
| 5 | Estado | 3 | Bien: el servidor en Query y los listados en la URL. Mal: banderas sueltas donde iría un reducer (Login, BusinessSignup), fetch y estado derivado en efectos, un `mutationError` para dos diálogos y `usePagination(undefined)` en las 5 listas. |
| 6 | Formularios | 2 | Hay dos patrones según el área: RHF + zod + `applyApiErrorToForm` en personal y public, y drafts a mano sin errores por campo en todo business. El guard está en 2 de 5 ediciones, el 409 se maneja distinto en cada pantalla y Roles usa una clave idempotente fija. |
| 7 | Sobreingeniería | 4 | Hay poca; el código peca de lo contrario. Restos: `roleColumns`/`roleColumnDefinitions` existen solo para el test, una columna de acciones de relleno, `secondsUntilResend`, `sentAtMs` y `CodeStep.kind` sin uso, y `VerifyLoginMethodDialog` es un alias de una línea. |
| 8 | Clean code | 2 | Los nombres son claros. Pero hay líneas de hasta 515 caracteres (CompanyDetailPage tiene 17 de más de 250), ternarios de 6 niveles, botoneras duplicadas, tres estilos de callback de mutación en una misma página, un componente definido dentro de otro y nombres engañosos (`zodEmailIsValid` es una regex). |
| 9 | Tests | 3 | Son recorridos por rol y nombre accesible, con MSW estricto, y verifican cuerpo, `version`, `Idempotency-Key` y URL. En contra: 29 aserciones atadas a clases o al DOM, la referencia sin axe ni vista a 390 px, ningún test de validación ni de error por campo en business, y fixtures que ya no reflejan el contrato. |
| 10 | Consistencia con Roles | 2 | El esqueleto del listado se copió. Pero Roles incumple sus propias fichas (formularios, idempotencia, `errors.ts`, tests) y cada feature divergió por su lado. Hoy `personal/account` es mejor referencia que Roles. |

### 2. Hallazgos (los más importantes primero)

**1. Roles no sirve como referencia: incumple las fichas que la citan como «Copiá de».** Severidad alta · esfuerzo M · ahora.
- Evidencia:
  - `docs/rules/formularios.md` manda copiar RoleEditorPage, pero `src/areas/business/roles/pages/RoleEditorPage.tsx:90-106` usa draft y validación en `useState` (la regla pide RHF + zod) y `:228` tiene `maxLength={60}` escrito a mano.
  - `docs/rules/errores.md` da como ejemplo `roleErrorMessage` en `errors.ts`, y `docs/architecture/arbol.md:193-207` lista `errors.ts`, `lib/permissionPicker.ts`, `lib/systemRoles.ts`, `components/PermissionPicker.tsx` y `RoleScopeField.tsx`. No existe ninguno.
  - `docs/rules/tests.md` manda copiar `RolesPage.test.tsx` y exige axe. `RolesPage.test.tsx` y `RoleEditorPage.test.tsx` no corren axe ni prueban 390 px, y `RolesPage.tsx:158` no tiene estado «sin coincidencias».
  - `RolesPage.tsx:70-75` saca los conteos de 3 pedidos extra a `/api/roles/paged`.
  - `roles/pages/AGENTS.md:2-3` habla del lienzo v60 y de que «el selector de empresa espera E6», pero el selector ya existe (`RoleEditorPage.tsx:243-249`). El comentario de `:137` está obsoleto y en inglés.
- Problema: la plantilla funciona con «no inventes, copiá». Cada feature nueva va a copiar estas desviaciones.
- Refactor: llevar Roles a la forma de `arbol.md` (permission picker y alcance como componentes, `lib/` con tests, `errors.ts`, RHF + zod, tests con axe, 390 px, en-US y «sin coincidencias»). Mientras tanto, apuntar los «Copiá de» de formularios y diálogos a `personal/account/components/*`, y los de listados a `users/pages/UsersPage.tsx`. Pedir al back `GET /api/roles/filter-counts`.
- Refutación: cada punto es una regla escrita incumplida, no un gusto.

**2. Alta de rol: clave idempotente fija y validación dentro de `mutationFn`.** Severidad alta · esfuerzo S · ahora.
- Evidencia:
  - `RoleEditorPage.tsx:107` crea la clave con `useMemo(() => crypto.randomUUID(), [])` y `:114` usa `useMutation`.
  - `:116-131` hace `setValidation(...); throw new Error("validation")` y `:159` separa el error por `error.message === "validation"`.
  - En el back, `IdempotencyFilter.cs` guarda las respuestas 200 a 499 y responde 422 `IdempotencyKeyReused` si llega la misma clave con otro cuerpo. Existe `Roles.Role.AlreadyExists`.
- Problema: si el nombre ya existe, la persona lo corrige y vuelve a guardar con la misma clave y otro cuerpo, y recibe 422 hasta recargar la página. Además, la validación pone estado dentro de la mutación, usa un string como centinela y muestra un solo error por vez.
- Refactor: `useIdempotentMutation((req, key) => createRole(req, key))` para el alta (renueva la clave después de un 4xx), `useMutation` para la edición con el payload como variable, y validar con un schema de zod antes de `mutate`.
- Refutación: `datos-y-api.md` dice «useIdempotentMutation en lugar de useMutation». Verifiqué la conducta del back.

**3. Columnas compuestas por posición: la ficha de empresa pierde «Estado».** Severidad alta · esfuerzo S · ahora.
- Evidencia:
  - `companies/pages/CompanyDetailPage.tsx:178` hace `...createCompanyMemberColumns(t).slice(0, -1)`.
  - `companies/columns.tsx:27-38` devuelve nombre, roles y estado, sin columna de acciones, así que el slice se lleva «Estado». El tablero `docs/design/lienzo/Empresa.dc.html` muestra «Miembro · Roles en la empresa · Estado · Acciones», y el teléfono pierde la columna `mobile: "status"`.
  - `CompaniesPage.tsx:154` usa el mismo truco contra una columna de relleno (`columns.tsx:23`, `cell: () => <EmptyValue />`).
- Refactor: que la factory reciba la columna de acciones como parámetro (`createCompanyMemberColumns(t, { actions })`), borrar la columna de relleno y mover las columnas de miembros a `memberColumns.tsx`, como pide `arbol.md`. Sumar un test de los encabezados de la tabla de miembros.
- Refutación: hoy falta una columna del tablero. El mismo idiom funciona en una pantalla y rompe en la otra.

**4. Las pantallas de edición resuelven a su manera el dirty, el guard, el aviso y el 409.** Severidad alta · esfuerzo M · ahora la pieza compartida; en Usuarios y Configuración, al tocarlos (coordinar con la otra conversación).
- Evidencia:
  - El guard está solo en `RoleEditorPage.tsx:103` y `personal/account/components/AccountProfileForm.tsx:42`. `settings/pages/SettingsPage.tsx:94`, `public-page/pages/PublicPagePage.tsx:122` y `users/pages/UserDetailPage.tsx:137` calculan el dirty sin guard.
  - El aviso «Cambios sin guardar» está solo en `UserDetailPage.tsx:210` y `AccountProfileForm.tsx:68-70`.
  - El 409 se maneja distinto: en PublicPage termina en un toast (`:110`), en el diálogo de `CompaniesPage.tsx:210` es un FormError, y `CompanyDetailPage.tsx:200` pasa `isDirty={false}`.
  - Hay tres formas de comparar: `sameDraft` (RoleEditor:40-44), `sameRoles` (UserDetail:51-53) y `JSON.stringify`.
  - La condición «FormError salvo `General.ConcurrencyConflict`» está copiada 4 veces (RoleEditor:219, UserDetail:233, Settings:112, AccountProfileForm:72).
  - El diálogo de salida usa textos propios en `RoleEditorPage.tsx:297-299` y los comunes en `AccountProfileForm.tsx:88`.
- Problema: en 3 pantallas se pierde lo escrito al navegar, y cada edición nueva vuelve a armar todo esto.
- Refactor: RHF con `useForm({ values: toDraft(data) })`, que da dirty, reset y resincronización. Sumar en shared un `useEditPage(isDirty)` que conecte guard, `UnsavedChangesDialog` y una prop `dirty` en `Page`, más un `SaveError` que ignore los 409 y acompañe al `ConcurrencyBanner`.
- Refutación: `formularios.md` exige `useUnsavedChangesGuard` y «Cambios sin guardar» en toda pantalla de edición, y cita «Configuración» como ejemplo.

**5. Las páginas absorben diálogos, pestañas y reglas; la descomposición ya estaba en `arbol.md`.** Severidad alta en Empresa, media en el resto · esfuerzo L · `CompanyDetailPage` ahora; Users, UserDetail y Settings al tocarlos.
- Evidencia:
  - `CompanyDetailPage.tsx` tiene 316 líneas, 13 useState (`:96-108`), 5 diálogos o confirmaciones (`:245`, `:271`, `:288`, `:293`, `:298`) y un solo `mutationError` (`:102`) para «Agregar miembro» y «Cambiar roles».
  - El diálogo de empresa está duplicado: `CompaniesPage.tsx:205-225` y `CompanyDetailPage.tsx:298-314`, con `draftFromCompany` repetido (`:48-50` y `:62-64`) y el mismo mapeo `taxIdType: draft.taxId?.type || null` tres veces (`CompaniesPage:108,148`, `CompanyDetail:139`). Ya divergieron en el manejo del 409.
  - `UsersPage.tsx:200-237` tiene el diálogo de invitación en línea.
  - En `UserDetailPage.tsx:284-303` el diálogo está en línea. `save` lleva flags de UI en las variables (`:107`, `{ request, message, keepDraft }`) y `remove` recibe el texto del toast (`:112`). Las acciones de ciclo de vida (reenviar, revocar, quitar) están repetidas entre lista y ficha.
  - `SettingsPage.tsx:145,157` tiene dos diálogos de dominio en línea.
  - Los 11 diálogos de business no usan `useRestoreFocusOnClose` ni se montan solo mientras están abiertos (así lo pide `formularios.md`), así que el foco cae en `<body>` al cerrar.
- Refactor: aplicar lo que `arbol.md:208-246` ya define (`CompanyDialog` compartido, `AddMemberDialog`, `MemberRolesDialog`, `tabs/CompanyMembersTab`, `tabs/CompanyRolesTab`, `InviteUserDialog`, `UserCompaniesField`, `EmailDomainSection`). Cada pieza es dueña de su draft, su mutación y su error, lo que respeta «useMutation directo». Mover la lógica pura a `lib/` con tests: `users/lib/companyAssignments.ts` (`UserDetail:139-146`) y `companies/lib/companyDraft.ts`. Subir `Section` (la caja con encabezado aparece ×10 en 6 archivos) y `ReadOnlyField` (UserDetail:74-79 más 4 en RoleEditor) a `shared/ui`.
- Regla práctica:
  - La página hace carga, banda y composición: 150 líneas y 8 hooks como máximo.
  - Cada diálogo, pestaña o caja con estado propio es un componente.
  - Los hooks de feature quedan solo para flujos multi-paso reutilizados, como `useAccountReauth` o un `useUserLifecycle` para lista y ficha.
  - No conviene un `useXxxActions` que solo envuelva mutaciones: contradice la convención y agrega indirección.
- Refutación: `arbol.md` es canónico, y `formularios.md` exige el montaje condicional y la restauración del foco.

**6. Dos patrones de formulario según el área; en B2B ningún error del servidor llega a su campo.** Severidad media · esfuerzo L, progresivo · al tocarlo, empezando por BusinessSignupPage.
- Evidencia:
  - RHF + zod aparece solo en 5 archivos (`EmailCodeForm`, `SignupPage`, `AccountProfileForm`, `AddLoginMethodDialog`, `AccountDeletionDialog`). `applyApiErrorToForm` no se usa en ninguna feature de business.
  - El error del cliente se envuelve en `new Error(t(...))` y se guarda en `failure: unknown` (`CompaniesPage.tsx:144,210`, `CompanyDetailPage.tsx:162`).
  - `BusinessSignupPage.tsx` tiene siete estados de error sueltos (`:41`, `:46`, `:52-56`) y una regex propia en `:322-324`.
  - El payload se arma dentro de `mutationFn` leyendo el estado del componente (`CompaniesPage:108`, `CompanyDetail:123,139`, `RoleEditor:133-140`).
  - Se usa `Input type="email"` crudo en lugar de `EmailField` (`UsersPage.tsx:206`, `PublicPagePage.tsx:216`), y el CUIT es un input crudo con su propia lógica (`BusinessSignupPage.tsx:63-67,288-291`) en lugar de `TaxIdField`.
  - `SignupPage.tsx:44` tiene `false as true`, un `as` que esconde el tipo.
- Refactor: RHF + zod en todo formulario con reglas, con el schema y un `toRequest(values)` en el `lib/` de la feature. En el error, `applyApiErrorToForm` y, si no encaja, `errors.ts` que lleva el código de dominio al campo. Los diálogos simples (dominio, roles de un miembro) pueden seguir con useState, como permite la regla.
- Refutación: `formularios.md` pide RHF + zod «cuando hay reglas», el error del servidor «en su campo», `EmailField` y `TaxIdField`.

**7. Ingreso y registros repiten el paso del código, los términos y los paneles de resultado; `StatusPanel` está anidado.** Severidad media · esfuerzo M · ahora BusinessSignupPage; el resto al tocarlo.
- Evidencia:
  - El paso del código está ×3 (`LoginPage.tsx:211-225`, `SignupPage.tsx:138-151`, `BusinessSignupPage.tsx:244-260`) y `resendCode` también ×3 (`:151-163`, `:90-102`, `:157-169`).
  - El separador «o» está ×3 y la casilla de términos ×4 (Signup:121-125, BusinessSignup:228-233, Invitation:211-217, `public/legal/pages/AcceptTermsPage.tsx:58`).
  - `LoginPage.tsx:174-188` reescribe `components/BusinessAccessResult.tsx`, que solo usa CallbackPage. El mapa de código a ícono y tono está ×2 (`LoginPage:36-40` y `public/errors/pages/OrganizationUnavailablePage.tsx:81-86`). Al menos 8 enlaces se pintan como botón a mano en lugar de usar `Button asChild`.
  - `StatusPanel` está declarado dentro de `BusinessSignupPage` (`:299-312`), lo que prohíben `estructura-y-features.md` y `frontend.md`. La ficha dice que lo verifica oxlint, pero `.oxlintrc.json` solo activa `jsx-no-literals`, `rules-of-hooks` y `only-export-components`.
- Refactor: un `CodeStep` y un `useCodeStep` (código, error, intentos, reenvío, Retry-After) para las tres pantallas; `TermsConsent`, `OrDivider` y un `ResultPanel` configurable (BusinessAccessResult, StatusPanel, cerrado y no disponible pasan a ser configuraciones); `Button asChild` para los enlaces; y un chequeo de componentes anidados.
- Refutación: `StatusPanel` viola una regla escrita. El resto son unas 150 líneas duplicadas que se pueden verificar.

**8. LoginPage y BusinessSignupPage arman máquinas de estado con banderas sueltas, y ponen fetch y estado derivado en efectos.** Severidad media · esfuerzo M · al tocarlo, junto con el 7.
- Evidencia:
  - `LoginPage.tsx:49-68` tiene 9 useState y 2 refs, más 3 efectos. En `:72-76`, `getPendingDeletion().then(setPending)` corre dentro de un `useEffect` con un ref de guarda.
  - `backToEmail` y `cancelDeletion` resetean 4 o 5 estados a mano (`:107`, `:121-127`). La vista es un ternario de 6 ramas (`:174-225`).
  - `BusinessSignupPage.tsx:96-114` deriva `step` en efectos y la vista es un ternario de 6 ramas (`:217-296`).
- Problema: se pueden representar combinaciones inválidas y las transiciones quedan dispersas.
- Refactor: `useReducer` con una unión de vistas (`email | code | result | pendingDeletion`). Llevar el mapeo de `ApiError.code` a transición a `lib/loginOutcome.ts` con tests, como `lib/returnUrl.ts`. Reemplazar el efecto por `useQuery({ queryFn: getPendingDeletion, enabled, retry: false })`, calcular el paso inicial en el render y armar un componente por vista.
- Refutación: la regla 3 de AGENTS.md dice «Nada de useEffect con fetch» y `frontend.md` dice «el estado derivado se calcula en el render».

**9. Errores: ninguna feature de business tiene `errors.ts`.** Severidad media · esfuerzo S · ahora.
- Evidencia:
  - La misma función está en 4 páginas (`UsersPage:38`, `UserDetailPage:43`, `CompaniesPage:52`, `CompanyDetailPage:66`), más `displayError` en `InvitationPage:43`, y la expresión `instanceof ApiError ? … .detail ?? …` en otros 16 lugares.
  - Los códigos se deciden dentro del JSX (`CompanyDetailPage.tsx:279`, `Companies.Membership.LastCompanyAdmin`).
  - Roles adelanta la regla del back (`RolesPage.tsx:102-106`) en lugar de mapear `Roles.Role.HasUsers`, que es justo el ejemplo de la ficha.
- Refactor: un `shared/api/errorMessage.ts` (`apiErrorMessage(error, t)`) y un `errors.ts` por feature con `switch` por código.
- Refutación: `errores.md` lo pide textualmente.

**10. El punto de estado está hecho a mano en 6 lugares y con colores distintos.** Severidad media · esfuerzo S · ahora.
- Evidencia:
  - En `users/columns.tsx:38` el estado `Expired` es `--marca-tx`, y en `UserDetailPage.tsx:35-41` es `--alerta`.
  - Hay más en `companies/columns.tsx:20,35`, `CompanyDetailPage.tsx:204` y `PublicPagePage.tsx:153,161`.
  - `shared/ui/format/StatusBadge.tsx`, `shared/format/statusTones.ts` (con el comentario «Cada feature incorporará sus estados de negocio») y el `type: "status"` de DataTable existen y ningún área los usa.
- Refactor: registrar los enums de cada feature en `statusTones`, usar columnas `type: "status"` y `StatusBadge` en las bandas.
- Refutación: la divergencia de color se ve en pantalla.

**11. Datos compartidos entre features, duplicados con claves distintas; opciones truncadas a 100 y filtradas en el cliente.** Severidad media · esfuerzo M · al tocarlo; los endpoints del back, al backlog.
- Evidencia:
  - `fetchCompanyOptions` es idéntica en `roles/api/roles.ts:41-43` y `users/api/users.ts:76-78`, con claves `["role-company-options"]` (escrita en línea en `RolesPage:65` y `RoleEditor:95`) y `["organization-user-company-options"]`. Ninguna cuelga de `["companies"]`, así que crear o renombrar una empresa no las invalida (staleTime de 30 s).
  - `toSearch` está ×4 (roles:20, users:29, companies:37, audit:15) y el pedido del token antiforgery ×3 (`auth/api/googleSignup.ts:6`, `shared/api/invitationContract.ts:13`, `account/api/loginMethods.ts:20`).
  - Hay claves en línea en `CompanyDetailPage:94,109,111`.
  - `companies/api/companies.ts:76-82,101-109` recorre todas las páginas de miembros y filtra roles en el cliente.
- Refactor: un `shared/api/options.ts` con claves bajo la raíz de cada recurso, un `shared/api/queryString.ts`, la API de invitación en `public/auth/api/`, y pedir al back filtros como `notInCompanyId`.
- Refutación: `estructura-y-features.md` dice que lo que comparten dos features sube a shared, y `datos-y-api.md` que `api/` exporta las claves.

**12. Permisos: 26 comprobaciones `currentUser?.permissions?.includes("…") === true` con códigos literales.** Severidad media · esfuerzo S-M · backlog.
- Evidencia: `CompanyDetailPage.tsx:76-81` tiene seis. No existen `usePermissions`, `Can` ni `CanInCompany`, que `frontend.md` §2 y §3 describen. `MeResponse.effectivePermissions.companies` no se usa: los permisos `company.members.*` se evalúan contra la lista plana.
- Refactor: un `auth/usePermissions` con `can` y `canInCompany`, y una unión tipada de códigos.
- Refutación: afecta solo la experiencia, no la seguridad (decide el back), por eso es media.

**13. El andamiaje de listado se repite, y `usePagination(undefined)` está en las 5 listas.** Severidad media · esfuerzo S-M · backlog.
- Evidencia:
  - Las 5 llamadas (`RolesPage:45`, `UsersPage:52`, `CompaniesPage:64`, `CompanyDetailPage:84`, `AuditPage:30`) pasan `undefined`, así que el salto a la última página que pide `paginado-y-listados.md` nunca corre: el hook necesita el resultado antes de que exista la consulta.
  - En cada página se repiten el debounce, el objeto de consulta y `<Pagination>` con 8 props (×4).
  - `AuditPage.tsx:37` reimplementa `useCursorList`, que hoy nadie usa.
- Refactor: separar el estado del hook de la corrección (`usePageCorrection(result)`), mover el debounce adentro, crear un `<PagedTable result pagination …>` y dejar que `useCursorList` acepte opciones.
- Refutación: la ficha lo exige y hoy no corre en ninguna pantalla.

**14. Tests atados a la implementación, y la referencia incompleta.** Severidad media · esfuerzo S-M · al tocarlo.
- Evidencia:
  - Hay 20 `toHaveClass` sobre clases de Tailwind y 9 `querySelector` o `parentElement` (por ejemplo `CompanyDetailPage.test.tsx:41-44`, `LoginPage.test.tsx:121,198,271`, `LandingPage.test.tsx:31-33`). Van a romperse con las extracciones propuestas sin que cambie la conducta.
  - Roles y Companies no corren axe ni prueban 390 px.
  - `BusinessSignupPage.test.tsx:27-35` arma sus propios providers, usa `fireEvent` y finge `innerWidth`, que no cambia el layout en jsdom.
  - El fixture `name: "TenantAdmin"` (`RolesPage.test.tsx:13`) ya no es lo que manda el back, que traduce los nombres (`RoleService.cs:140`, `RoleTexts.Name`). El front decide por el nombre visible (`roles/columns.tsx:24-36`, `CompanyDetailPage.tsx:95`).
- Refactor: dejar lo visual al arnés de capturas, sumar axe, 390 px y en-US, usar `renderRouteWithProviders` con userEvent, y pedir al back una clave estable para los roles de sistema.
- Refutación: `tests.md` exige axe. Las aserciones de clase no están prohibidas, pero frenan los refactors.

**15. Valores escritos a mano.** Severidad baja · esfuerzo M (depende del back) · backlog.
- Evidencia:
  - Hay 18 `maxLength` literales; el nombre con 120 aparece en 5 lugares, por ejemplo `CompaniesPage.tsx:211` y `BusinessSignupPage.tsx:269`. `formularios.md` dice que salen del contrato, pero `schema.d.ts` no publica ningún `maxLength`, así que la regla hoy no se puede cumplir.
  - El dominio `.plataforma.com` está escrito en `PublicPagePage.tsx:40` y como texto de traducción en `locales/{es,en}/auth.json:101`.
  - El límite del logo está en `PublicPagePage.tsx:130`.
- Refactor: que el back publique los `maxLength` en OpenAPI y que el dominio salga de la configuración.

### 3. Lo que está bien hecho y conviene conservar como patrón
- **`api/` por feature:** funciones tipadas desde `generated/`, `encodeURIComponent`, y `Idempotency-Key` y `version` visibles en la firma. Las claves tienen raíz y las mutaciones invalidan esa raíz (users, companies).
- **Listados:** filtros, página, orden y búsqueda viven en la URL (`useFilters`, `useQueryUpdate`), con `keepPreviousData`. Los conteos salen de `/filter-counts` en Users y Companies. `columns-mobile.test` y `structure.test` se cumplen.
- **`personal/account` como molde:** un diálogo por archivo montado solo mientras está abierto, `useRestoreFocusOnClose`, RHF + zod + `applyApiErrorToForm`, `useAccountReauth` reutilizado por tres diálogos y `AccountProfileForm` con guard, aviso y 409.
- **`public/auth`:** `lib/` pura con tests, `errors.ts` como mapa de código a clave, `EmailCodeForm` y `GoogleButton` encapsulados.
- **Otros patrones:** la tabla `confirmations` de `UserDetailPage`; `InvitationPage` reinicia el flujo con `key={location.key}` y está probado; las decisiones se toman por `code`; todo pasa por i18n; `meta: { silent: true }` con error y reintento en pantalla.
- **Tests:** son recorridos con nombres del tablero, consultas accesibles y MSW estricto, sin `getByTestId` ni snapshots.

### 4. Veredicto
Los cimientos son buenos y parejos: Query, estado en la URL, API tipada, `shared/ui` y tests por recorrido. El problema está en la capa de página: las pantallas B2B juntaron en archivos de 250 a 320 líneas la descomposición que `arbol.md` ya había diseñado, y cada una resuelve formularios, diálogos y edición a mano y de forma distinta.
Roles, la feature de referencia, incumple sus propias fichas y es el origen de varias desviaciones; hoy `personal/account` es el mejor modelo.
Prioridad: corregir Roles (hallazgos 1 y 2) y el slice de columnas (3), crear la pieza compartida de edición (4) y partir `CompanyDetailPage` y `BusinessSignupPage`; Usuarios y Configuración, al tocarlos.
