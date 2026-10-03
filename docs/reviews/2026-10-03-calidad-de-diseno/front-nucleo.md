# Calidad de diseño · Front · núcleo compartido

> Informe de un revisor, solo lectura, sobre el commit `961f786 del front`. Los números de línea son de ese commit: buscá por símbolo. Antes de corregir un hallazgo, confirmalo en el código actual. El resumen, los bugs confirmados y el orden de trabajo están en [`../2026-10-03-calidad-de-diseno.md`](../2026-10-03-calidad-de-diseno.md).

## Revisión de diseño · Front · núcleo compartido

Revisé la copia congelada del commit 961f786. Las rutas son relativas a `../ArquitecturaBaseMutitenantFront/`. Trabajé solo en lectura: no corrí npm ni tests.

### 1. Puntajes

| # | Criterio | Nota | Justificación |
|---|---|---|---|
| 1 | Responsabilidad única | 3 | dropdown-menu, select y dialog no mezclan responsabilidades: son envoltorios de shadcn, una función por primitiva. DataTable es cohesivo e icons.tsx es solo un catálogo. Los que sí mezclan son otros: `tenancy/AccessMenu.tsx` (menú escritorio y móvil, flujo de salir, mapeo de estados), `layouts/AppShell.tsx` (eventos de ventana, ruteo de errores de acceso, baja), `shared/api/httpClient.ts` (transporte más la política de pantalla del 403), `shared/format/useFormat.ts` y `shared/api/accountContract.ts` (rutas de API y de pantalla, códigos y un parser). |
| 2 | Lógica y presentación | 3 | shared/ui no llama a la API, y la lógica de URL, idempotencia y "salir sin guardar" vive en hooks. Pero el flujo de salir está copiado en 4 componentes, la visibilidad por permisos se calcula en el layout y en cada página, y el transporte HTTP decide qué pantalla se muestra. |
| 3 | Duplicación | 2 | Seis campos repiten la misma máquina de estados. Hay cuatro selectores y `searchable()` copiado 5 veces. El offset horario y el tokenizador de patrones están 3 veces cada uno, la cultura se resuelve en 5 lugares, el mapa acceso→ruta está en unos 8, el mini-store en 4 y la salida en 4. |
| 4 | Abstracciones justas | 2 | Hay piezas sin adopción: estados y enums con solo `TestStatus`, `usePagination(result)` que nadie puede alimentar, `useLanguagePreference` y `resolveEffectiveTimeZone` sin uso, icons.tsx, Sheet, StatusDot, Avatar, SearchInput, IconButton. Al mismo tiempo faltan las que las pantallas necesitan: permisos, mensaje de error de mutación, listado paginado y salir. |
| 5 | Estado y efectos | 3 | Está bien el uso de `useSyncExternalStore`, el ajuste durante el render (`shared/ui/SearchInput.tsx:23`) y los efectos solo para sincronizar con sistemas externos. Los problemas son varios. La cultura tiene 5 fuentes. El acceso se lee del claim y también de `/api/me`. Cada consumidor de `useReferenceData` guarda su propia copia de la cultura pedida. El panel Administración vive en `useState(false)` (`layouts/PrivateLayoutFrame.tsx:41`) en vez de derivarse de la ruta. Los campos con borrador no se resincronizan cuando cambia el valor de afuera. |
| 6 | Capa de datos | 3 | `httpClient` (renovación única, ETag/304, ApiError tipado) y `useIdempotentMutation` son muy buenos. Faltan un `MutationCache` y un mensaje de error común. El 403 global vive en el transporte. `api.get<T>` no ata la ruta al tipo de respuesta, aunque `ApiPaths` está generado. |
| 7 | Tipado | 3 | No hay `any` y casi no hay casts. Pero hay tipos escritos a mano que divergen del contrato (`PagedResult`, los zod de referencia, `ProblemDetails`, `AccessKind`, `TaxIdValue` tres veces, `StatusTone` dos veces). Los permisos y enums son `string` sueltos, y se ignora `effectivePermissions` del contrato. |
| 8 | Clean code | 3 | Los nombres son claros y los comentarios explican el porqué. En contra: ternarios anidados de 3 a 5 niveles (`AppShell.tsx:61-67`, `AccessMenu.tsx:110-114`), booleanos posicionales (`httpClient.ts:80-81`), 15 rutas lazy con el mismo boilerplate (`app/routes.tsx`), `&&` en JSX en 8 lugares contra la regla, y `useFormat.ts` armado con `createElement` para no ser `.tsx`. |
| 9 | Tests | 3 | Los tests de hooks y API son de conducta (URL, claves, renovación con MSW) y consultan por rol. Pero hay 117 `toHaveClass` en el núcleo, asserts sobre los `d` de un SVG, mocks parciales de `useFormat` en 12 tests y un test que recorre la estructura del árbol de rutas. |
| 10 | Consistencia | 2 | Hay dos sistemas de íconos y tres implementaciones de selector. `ForbiddenPage` y `ErrorPageFrame` se importan por dos rutas. Hay botones y avatares hechos a mano junto a `Button` y `Avatar`. Y el camino de estados del núcleo convive con puntos de color hechos a mano en las áreas. |

### 2. Hallazgos (los más importantes primero)

**1. [alta] El transporte decide la pantalla de "sin permiso", para cualquier lectura** · M · ahora
- **Evidencia:**
  - `shared/api/httpClient.ts:114-119` publica en un store global todo 403 de un GET, salvo `/api/auth/`.
  - `layouts/AppShell.tsx:61-67` reemplaza la pantalla entera por `ForbiddenPage`.
  - `meta: { silent: true }` no lo evita, porque `shared/api/queryClient.ts:19-22` actúa después de la publicación.
- **Problema:** basta un 403 en una consulta secundaria (opciones de un filtro, un conteo) para tirar abajo la pantalla, y por eso cada query tiene que blindarse con `enabled: canX`. Hay un caso concreto: `areas/business/companies/pages/CompanyDetailPage.tsx:94` pide `/api/roles/paged` también con solo `company.members.read`, y el back exige `roles.read` (`RolesController.cs:22-23`). Una persona sin `roles.read` vería la ficha entera reemplazada.
- **Refactor:** que httpClient solo transporte y renueve. Publicar desde `QueryCache.onError` cuando la query lo declare (`meta.blocksView`, por defecto solo en la query principal de la página). `Legal.AcceptanceRequired` sigue su camino propio.
- **Contraste:** el comentario de `httpClient.ts:116` muestra la intención ("solo una lectura protegida bloquea la vista"), pero el criterio se aplica a toda lectura. Es una trampa real, no un gusto.

**2. [alta] Los errores genéricos de las mutaciones los resuelve cada pantalla** · M · ahora
- **Evidencia:**
  - `shared/api/queryClient.ts:16-57` tiene solo `QueryCache.onError`; no hay `MutationCache`.
  - El mismo `failureMessage`/`errorMessage` está copiado en `UsersPage.tsx:38`, `CompaniesPage.tsx:52`, `CompanyDetailPage.tsx:66` y `UserDetailPage.tsx:43`, además de 20 `onError` en áreas.
  - `auth/LegalAcceptanceGate.tsx:20` muestra `error?.message` crudo.
- **Problema:** `ApiError.network()` (`ApiError.ts:40-42`) no trae `detail`, así que un corte de red en una mutación muestra "Algo salió mal", sin "No pudimos conectarnos" ni «Reintentar». Un 5xx no ofrece el traceId. Esto contradice frontend.md ("los resuelve shared/api… nunca cada pantalla") y errores.md (prohíbe `error.message` crudo).
- **Refactor:** `shared/api/errorMessage.ts` con `apiErrorMessage(error, t)`, y `new MutationCache({ onError })` para red, 5xx y 429, salvo `meta.handlesError`. Después, borrar los 4 helpers.
- **Contraste:** lo exige una regla escrita y la divergencia se ve en el código.

**3. [alta] `usePagination(result)` no se puede alimentar: la corrección de página fuera de rango nunca corre** · M · ahora
- **Evidencia:**
  - `shared/hooks/usePagination.ts:22,45-56` corrige solo si recibe `result`.
  - Las 5 pantallas que lo usan pasan `undefined` (`UsersPage.tsx:52`, `CompaniesPage.tsx:64`, `CompanyDetailPage.tsx:84`, `RolesPage.tsx:45`, `AuditPage.tsx:30`). No tienen otra opción: la query necesita el estado del hook antes de existir.
  - Cada listado repite la misma cadena: useFilters → usePagination → useDebouncedValue → objeto query → useQuery → `<Pagination>` con 8 props copiadas de `data` (`UsersPage.tsx:195`, `CompaniesPage.tsx:201`, `CompanyDetailPage.tsx:232`).
- **Problema:** si se borra el último ítem de la última página, la pantalla queda vacía en lugar de saltar a la anterior. La corrección que documenta paginado-y-listados.md solo corre en `usePagination.test.tsx`.
- **Refactor:** `usePagedQuery({ queryKey, queryFn, filterKeys, defaultSort })` que componga estado, debounce, query y corrección con el resultado. `Pagination` recibiría `result` y `pagination`.
- **Contraste:** es una regla escrita y el diseño del hook impide usarlo bien.

**4. [alta] Acceso, rutas y organización activa no tienen una fuente única** · M · ahora (antes de E7 y E9)
- **Evidencia:**
  - El mismo mapa acceso→inicio está dos veces: `auth/AccessRoute.tsx:6-10` y `tenancy/accessHome.ts:3-9`. Además `AccessKind` (`AccessRoute.tsx:4`) está escrito a mano aunque existe el generado `Access` (`schema.d.ts:7126`).
  - La puerta de ingreso se decide de 4 formas: `AuthProvider.tsx:20`, `ProtectedRoute.tsx:17`, `AccessMenu.tsx:182` y `accountContract.ts:20`.
  - `app/RouteViews.tsx:29,36` olvida `platform` y funciona solo por el rebote de AccessRoute. `shared/ui/ErrorPageFrame.tsx:5` usa `"/org"` por defecto. En src hay 35 literales `"/org"` o `"/login/empresa"`.
  - La organización activa se busca en 6 lugares (`PrivateLayoutFrame.tsx:44`, `BusinessLayout.tsx:15`, `AccessMenu.tsx:158`, `RouteViews.tsx:28`…).
  - Los estados "no disponible" están listados en 5 lugares (`BusinessLayout.tsx:7-11`, `AppShell.tsx:54-56`, `OrganizationUnavailablePage.tsx:12`, `AccessMenu.tsx:109-114`, `LoginPage.tsx:37`).
  - La secuencia de salir está copiada 4 veces (`AccessMenu.tsx:169-179`, `OrganizationUnavailablePage.tsx:48`, `AcceptTermsPage.tsx:48`, `InvitationPage.tsx:128`).
- **Refactor:** `tenancy/access.ts` con `type Access = ApiSchemas["Access"]`, `parseAccess`, `homeOf`, `loginPathOf`, `accessOfPath`, `activeOrganization(me)`, `isSelectable(org)` y `unavailableCodeOf(status)`. Sumar `auth/useSignOut()` y sacar el valor por defecto de ErrorPageFrame.
- **Contraste:** ninguna regla lo pide textualmente, pero ya hay deriva (el caso de platform), y E7 (storefront por host) y E9 (plataforma) agregan accesos. Cada uno tocaría unos 8 archivos.

**5. [alta] La cultura efectiva se resuelve en cinco lugares con precedencias distintas** · M · ahora
- **Evidencia:**
  - `shared/i18n/index.ts:71-74`: preferida → local → defecto.
  - `app/providers.tsx:22-26` y `shared/format/useFormat.ts:77-81`: la misma cadena cuenta → local → i18n → defecto, copiada dos veces.
  - `shared/referenceData/useReferenceData.ts:18-31`: estado propio por instancia (local, si no i18n) más un listener de `languageChanged` por cada consumidor. En `:49` cae a datos de otra cultura con `lastCachedData`.
  - `shared/format/useDateInTimeZone.ts:12` ignora la cultura de la cuenta y la local.
  - `Accept-Language` sale de `i18n.language` (`AuthProvider.tsx:15`) o de `effectiveCulture()` (`httpClient.ts:67`).
- **Problema:** si la cultura de la cuenta es distinta de la local, el catálogo se pide dos veces y mientras tanto se muestra en otro idioma que los formatos. Cambiar la precedencia (por ejemplo, la cultura de la organización en el storefront) obliga a tocar 5 lugares.
- **Refactor:** una función pura `resolveCulture({ account, stored, catalog })` y un único `useEffectiveCulture()` (un store alimentado por i18n) que consuman todos.
- **Contraste:** frontend.md («Idioma y cultura») define una sola precedencia, y las copias no coinciden entre sí.

**6. [media] Permisos sin hook ni guarda de ruta** · M · ahora
- **Evidencia:**
  - frontend.md §2 promete `usePermissions` y `Can`, y §3 dice que el permiso de cada ruta se declara en `routes.tsx`. Nada de eso existe: `routes.tsx:71-112` no declara permisos.
  - Las páginas leen `currentUser?.permissions?.includes("…") === true` 28 veces y repiten `<ForbiddenPage homePath="/org" />` 9 veces.
  - `PrivateLayoutFrame.tsx:26-38` filtra el menú por su cuenta, y `layouts/navigation/business.ts:10-19` repite los mismos strings.
  - El front lee el `permissions?: string[]` opcional y no usa `effectivePermissions` (organización y por empresa) ni `features` (`schema.d.ts:7515,7519,7524`).
- **Refactor:** `auth/usePermissions()` (`has`, `hasInCompany`, un tipo `Permission` cerrado) y un manifiesto de rutas único (path, permission, feature, label, icon) del que salgan `routes.tsx` (con `<RequirePermission>` y un helper `lazyPage` que elimina el boilerplate) y la navegación.
- **Contraste:** lo exige frontend.md, y un permiso declarado dos veces puede divergir.

**7. [media] La abstracción de estados y enums del núcleo no se puede adoptar** · S en el núcleo, M al migrar · ahora y al tocarlo
- **Evidencia:**
  - `shared/format/statusTones.ts:4-11` solo mapea `TestStatus`, que es un fixture de test en producción, y `locales/es/enums.json` solo trae `TestStatus`.
  - Ninguna área usa `type: "status"`, `StatusBadge` ni `EnumText`: dibujan el punto y eligen el color a mano (`areas/business/companies/columns.tsx:19-21,34-36`).
  - `shared/ui/StatusDot.tsx:4,11-17` no se usa, redefine `StatusTone` y dibuja la pastilla que la regla prohíbe.
- **Refactor:** un registro de tonos abierto y tipado por los enums generados, que cada feature declare. Mover TestStatus a los fixtures, borrar StatusDot y migrar las columnas a `type: "status"`.
- **Contraste:** formatos.md manda `StatusBadge` más `statusTones`, y la falta de adopción se comprueba en el código.

**8. [media] Tipos escritos a mano que duplican el contrato y ya divergen** · S · ahora
- **Evidencia:**
  - `shared/api/pagedResult.ts:2-10` declara obligatorios `totalPages`, `hasPrevious` y `hasNext`, que en los `PagedResultOf*` generados son opcionales (`schema.d.ts:7657-7726`). `Pagination.tsx:59,70` deshabilita botones según esos campos.
  - Los zod de `shared/referenceData/referenceData.ts:5-77` no están atados a `ReferenceDataHttpResponse` (`:86-90` descarta el tipo generado), y ya difieren: `sortOrder`, `minorUnits` y `callingCode` son opcionales en el contrato y obligatorios-nulables en zod. Si el back omitiera los nulos, la app no arrancaría.
  - `problemDetails.ts` es paralelo al generado, y el alias `ApiProblemDetails` (`types.ts:6`) no se usa.
  - `TaxIdValue` está tres veces (`formatters.ts:17`, `TaxIdText.tsx:4`, `DataTable.tsx:55`).
- **Refactor:** derivar `PagedResult<T>` del tipo generado, tipar el schema como `z.ZodType<RawReferenceData>` (o agregar un test de tipos), y definir `type Access = ApiSchemas["Access"]`.
- **Contraste:** datos-y-api.md prohíbe interfaces a mano para lo que ya está en `generated/`.

**9. [media] El núcleo importa de las áreas y forma ciclos** · S · ahora
- **Evidencia:**
  - `auth/useLanguagePreference.ts:5` importa `@/areas/personal/account/api/account`, lo que arma un ciclo auth ↔ áreas. Además el hook solo lo usa su test: `AccountProfileForm` resuelve lo mismo por su cuenta.
  - `layouts/AppShell.tsx:4-5,12` y `BusinessLayout.tsx:2` importan páginas de áreas, mientras las áreas importan `@/layouts/AuthLayout`.
  - `areas/public/errors/pages/ForbiddenPage.tsx:1` y `ErrorPageFrame.tsx:1` reexportan `shared/ui`.
  - `test/structure.test.ts:27-29` solo controla archivos dentro de `areas/`, así que nada de esto se detecta.
- **Refactor:** borrar el hook, mover los estados del armazón a `layouts/` (o dejar esa dependencia en una lista blanca explícita), quitar las reexportaciones y extender el test de estructura a shared, auth, tenancy y layouts.
- **Contraste:** frontend.md ubica los errores en `areas/public/errors`, pero el ciclo y el hook muerto son reales.

**10. [media] Seis campos con la misma máquina de estados** · S · ahora (todavía no tienen consumidores)
- **Evidencia:**
  - `shared/ui/fields/DateField.tsx:20-57`, `DateTimeField.tsx` y `TimeField.tsx` difieren en 4 líneas: formateador, parser y clave de error.
  - `NumberField.tsx` y `PercentField.tsx` difieren en 5 líneas, y `MoneyField.tsx:23-77` repite el mismo bloque.
  - Los seis usan `&&` en JSX contra la regla.
- **Problema latente en los seis:** el texto escrito no se resincroniza si el padre cambia el valor. Por ejemplo, después de «Descartar cambios» el campo sigue mostrando el texto inválido y su error. `SearchInput.tsx:18-26` sí lo resuelve.
- **Refactor:** un hook `useParsedInput({ value, format, parse, errorKey, onChange, onValidityChange })` que devuelva las props del input y el error, con el ajuste durante el render. Cada campo queda en unas 10 líneas.

**11. [media] Selectores de datos de referencia sin base común** · M · al tocarlo
- **Evidencia:**
  - `searchable()` está 5 veces: `CultureSelect.tsx:20`, `TimeZoneSelect.tsx:20`, `CurrencySelect.tsx:17`, `CountrySelect.tsx:20` y `shared/phone/countries.ts:4`.
  - El mismo pipeline habilitadas → filtro → orden se repite con dos centinelas distintos y con `localeCompare` con o sin cultura.
  - La misma clase del `<select>` nativo está en 4 archivos, incluido `TaxIdField.tsx:61`.
  - Hay tres implementaciones del control: nativo, nativo más Radix en la variante "compact", y buscador más Radix.
- **Refactor:** `useReferenceOptions(rows, { search, keys, compare })` y un `ReferenceSelect` con `renderLabel`.
- **Contraste:** formularios.md exige la misma búsqueda en los cuatro, y cada uno la resuelve distinto.

**12. [media] Formateo y parseo con piezas triplicadas** · M · al tocarlo
- **Evidencia:**
  - El offset horario se calcula tres veces con el mismo `Intl` y la misma regex: `formatters.ts:231-239`, `parsers.ts:174-183` y `useFormat.ts:160-171`.
  - El patrón de fecha se tokeniza tres veces (`formatters.ts:89`, `parsers.ts:70-87`, `parsers.ts:110-139`), y ya divergen: `dateParts` no admite literales entre comillas y los otros dos sí.
  - La validación de fecha civil está tres veces (`formatters.ts:26-29`, `parsers.ts:97-102,157-162`).
  - `formatCompact` y `formatFileSize` comparten la escalera de unidades (`formatters.ts:180-209`).
  - `useDateInTimeZone.ts:8-14` arma un segundo juego de formateadores fuera del FormatProvider.
- **Refactor:** extraer `shared/time/offset.ts`, un único tokenizador y `civilDate`, y exponer `useFormat().inTimeZone(tz)`.

**13. [media] Tests acoplados a clases y a la estructura** · M · backlog
- **Evidencia:**
  - `DataTable.test.tsx:123-127` asserta clases (`bg-[var(--cab)]`, `h-10`), y `:76-77` verifica llamadas a formateadores mockeados.
  - `AccessMenu.test.tsx:63-80` asserta clases y los `d` del SVG de Salir.
  - 12 tests mockean `useFormat` con objetos parciales (por ejemplo `MoneyText.test.tsx:5-7`).
  - `app/routes.test.tsx:12-23` recorre la forma del árbol de rutas.
- **Problema:** un refactor sin cambio visual rompe decenas de tests, y la comparación con el lienzo ya cubre lo visual.
- **Refactor:** dejar en los tests roles, ARIA y texto; mover las medidas al arnés de capturas; usar un `renderWithFormat()` con FormatProvider real y el catálogo de MSW.

**14. [baja] Íconos y piezas del kit sin uso conviven con reimplementaciones** · S · al tocarlo
- **Evidencia:**
  - `shared/ui/icons.tsx` tiene 29 SVG propios, con el comentario "sin depender de un paquete" (`:7-9`). Lo usan solo 3 archivos, mientras lucide se importa en 44. `AccessMenu.tsx:23-29` agrega un tercer ícono de salir.
  - `Avatar` no se usa, y `Sidebar.tsx:66` y `AccessMenu.tsx:90,161` dibujan avatares a mano.
  - `SearchInput` no se usa, y `FilterBar.tsx:66` tiene su propio input.
  - `IconButton` no se usa, y `RowActions.tsx:36`, `Topbar.tsx:18` y `Page.tsx:53,83` arman botones de ícono a mano.
  - `Sheet.tsx` es un alias de Dialog sin uso.
- **Refactor:** quedarse con lucide (es lo que declara `components.json`), usar las piezas existentes y borrar Sheet y StatusDot.

**15. [baja] Stores de módulo copiados y singletons que esquivan la inyección de tests** · S · backlog
- **Evidencia:**
  - El mismo mini-store está cuatro veces: `shared/api/accessErrorStore.ts`, `auth/signOutStatus.ts`, `auth/deletionRequestStatus.ts` y `shared/hooks/useBreadcrumbLeaf.ts`.
  - `auth/AuthProvider.tsx:4,10,24` limpia el `queryClient` singleton y guarda la sesión en una variable de módulo.
  - `app/providers.tsx:52-57` inyecta el `authContext` de prueba por debajo de `HttpClientBridge`. Por eso, en `renderWithProviders` el puente HTTP ve el OIDC real y ningún test integrado ejercita el token ni la sesión vencida.
- **Refactor:** un `createStore<T>()` en `shared/lib`, que el puente tome el cliente con `useQueryClient()`, e inyectar el auth reemplazando el provider en lugar de sobrescribir el contexto debajo.

**Descartado después de refutarlo:**
- El largo de dropdown-menu, select y dialog: es código generado por shadcn y la regla pide editarlo lo mínimo.
- `cn` importado desde dos módulos: está documentado en `shared/lib/utils.ts`.
- El kit todavía sin consumidores (MoneyField, PercentField, PhoneField, SegmentedControl): es parte deliberada de la plantilla.
- El efecto que canoniza la URL en `usePagination`: lo pide la regla.
- Los formateadores que lanzan durante el render: la capa pura tiene que rechazar entradas inválidas.
- El orden de zonas con `Intl` dentro del comparador: con 13 zonas habilitadas el costo es despreciable.

### 3. Lo que está bien hecho y conviene conservar como patrón
- **`useIdempotentMutation`:** la clave es por formulario; se conserva ante red o 5xx y se renueva tras un éxito o un 4xx. Ante `Request.InProgress` espera con un `AbortSignal` atado al montaje.
- **`httpClient`:** la renovación se comparte entre pedidos (`renewalInFlight`, `lastRenewal`). `ApiError` tiene getters tipados y `ApiError.network()`, y `getWithMetadata` maneja ETag y 304.
- **`DataTable`:** las columnas son una unión discriminada por `type`, que resuelve formato, alineación y vacío. La guarda `columns-mobile.test` lo respalda.
- **La URL como fuente de verdad:** `useQueryUpdate` (una escritura por tick, `replace`), `useFilters`, y `pageSizes` con `satisfies` contra el enum generado.
- **`createFormatters`:** es puro, tiene `now` inyectable y comparte los casos de prueba con el back. Los parsers rechazan pérdida de precisión.
- **Hooks de experiencia bien documentados:** `useUnsavedChangesGuard`, `useRestoreFocusOnClose`, `useRetryAfterCountdown`.
- **Piezas accesibles:** `FormField`, `ConfirmDialog`, `Banner`, `CheckboxField`, `MultiSelect`.
- **Guardas mecánicas del arnés:** `structure.test`, `format-usage.test`, `theme-tokens.test`, `parity.test`, `contracts:check`.
- **TS estricto:** cero `any` en el núcleo y `import type` en todos lados.

### 4. Veredicto
El núcleo es técnicamente fino donde la plantilla puso el esfuerzo (HTTP, idempotencia, formatos, tabla tipada, estado en la URL) y sus tests de conducta son buenos.
La deuda está en las costuras: faltan las abstracciones de caso de uso que necesita cada pantalla (permisos, error de mutación, listado paginado, salir, rutas por acceso, cultura única) y sobran otras que nadie adoptó.
Conviene resolver los hallazgos 1 a 6 antes de E7–E9, porque cada área nueva va a copiar la duplicación actual; el resto se puede hacer al tocar cada pieza.
