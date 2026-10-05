# Etapa 5: las pantallas de Plataforma — plan ejecutable

> **Para Codex, que programa estas pantallas:**
> - Antes de empezar, leé el `AGENTS.md` de la raíz de cada repo. Antes de tocar una carpeta, leé su `AGENTS.md` y las fichas que nombra.
> - Seguí las tareas en el orden de «Orden y dependencias». Cada tarea de lógica sigue el «Protocolo»: rojo, implementación, verde y commit.
> - **La fuente de cada pantalla es su tablero** (lienzo v111, front `e685df0`). «Lo que dicen los tableros» copia sus textos, columnas, filtros, menús, diálogos y estados; si algo de este plan no coincide con el tablero, manda el tablero y lo anotás.
> - **No inventes.** Si una pantalla necesita algo que el tablero no dibuja (un estado, un texto, una columna, una acción), frená y preguntale al usuario: lo dibuja Diseño. Lo que se sabía que faltaba está en «Para decidir», ya resuelto: cada punto dice qué hacer. Los que dibuja Diseño (1, 2, 6, 7 y 8) se programan con el lienzo v111.
> - **Las piezas comunes de `src/shared/` se usan tal cual.** No ajustes medidas a mano en las pantallas. Si una medida o una forma del tablero no coincide con la pieza común, no toques la pieza: anotalo en «Para decidir» del «Registro de resultados» y seguí con la pieza como está.
> - **El usuario ve cada pantalla antes y durante** (ver «La regla de pantallas del usuario»): antes de programar una pantalla, le mostrás su tablero y esperás su visto bueno; en cada avance, le mandás la captura lado a lado y el comando de la vista en vivo.
> - Marcá cada casilla con su evidencia (el comando y su resultado) y completá el «Registro de resultados».
> - Commiteá cada cambio coherente y verificado, en español, solo con tus rutas: `git commit -m "…" -- <rutas>`, que guarda solo esas rutas aunque otra conversación tenga algo en el índice. Nunca `git add .` ni `git commit -a`. Todo en `main`, sin push.
> - Si algo técnico no está en este plan, en las fichas ni en ArquitecturaBase, decidí lo más simple, anotalo en «Decisiones tomadas» y seguí. Si cambia lo que ve o puede hacer un usuario, frená y preguntale al usuario.

**Estado:** listo para Codex. El usuario resolvió «Para decidir» el 2026-10-05.

- **Lo que dibuja Diseño** (los puntos 1, 2, 6, 7 y 8): ya está en el lienzo **v111** (front `e685df0`), aprobado por el usuario el 2026-10-05. Ver «Lo que suma la v111».

**Objetivo:** las pantallas de Plataforma de la Etapa 5 (T19 a T25 del [plan de la Etapa 5](2026-10-03-etapa-5-plataforma.md), precedidas por T18c en el back), conectadas a la API real y copiadas de sus tableros, en la compu y en el teléfono:
- el marco de Plataforma: «Gestión» y «Administración» en la compu; la barra de abajo y «Más» en el teléfono;
- Organizaciones y su ficha; Cuentas (con los operadores) y su ficha; Recuperaciones; Documentos legales; Auditoría; Configuración;
- «Recuperar mi cuenta» (`/recuperar`), la pantalla pública, con su enlace desde Ingreso;
- el E2E real de la etapa y la puerta de cierre.

**Fuente visual:** lienzo **v111, front `e685df0`** (`docs/design/lienzo/` del front). Los tableros de Plataforma no tienen «· por refinar» ni «· para aprobar»: están validados.

**Lo que suma la v111** (Diseño, aprobada por el usuario). «Lo que dicen los tableros» se escribió con la v110: con estos tableros, releelos antes de programar su pantalla y manda el tablero.

**Tweaks nuevos:**
- **`Cuenta-Plat`, Tweak «cuenta»:**
  - Persona;
  - Operador;
  - Operador: acciones;
  - Operador: cambiar permisos;
  - Operador: quitar operador;
  - Invitación de operador pendiente;
  - Invitación de operador vencida.
- **`M-Cuenta-Plat`, Tweak «estado»:** las mismas variantes.
- **`Organizacion`:**
  - el Tweak «estado» suma «Falló el alta», con «Reintentar el alta» en la banda;
  - el Tweak «duenos»: Con su Dueño, Sin Dueño todavía, Con el Dueño suspendido.
- **`M-Organizacion`, Tweak «estado»:**
  - Historial;
  - Falló el alta, con la hoja del ⋮ y «Reintentar el alta»;
  - Dueños;
  - Dueños: sin Dueño todavía;
  - Dueños: Dueño suspendido.
- **`Recuperar` y `M-Recuperar`, Tweak «whatsapp»:** Sin WhatsApp (hasta la Etapa 8) o Con WhatsApp. **Se programa «Sin WhatsApp».**

**Textos nuevos:**
- quitar operador: «¿Quitar a {nombre} como operador?» y «Deja de entrar a la plataforma.»;
- «Recuperar mi cuenta» sin WhatsApp, paso 1: «Escribí el correo con el que entrabas.»;
- un Dueño suspendido: «Suspendido», tono warning;
- la versión legal que todavía no rige: «Versión {n} desde {fecha}», tono pending, en una tercera línea de la fila de `Legales`.

**Además:**
- los tonos de `M-Organizaciones`, `M-Cuentas` y `M-Recuperaciones` siguen §12;
- «Mi cuenta» está en tu menú de los ocho tableros de Plataforma de la compu.

---

## Fuentes, restricciones y punto de partida

### Fuentes

- [Plan de la Etapa 5](2026-10-03-etapa-5-plataforma.md): T19 a T25 y sus «Decisiones tomadas».
- [Ficha de Plataforma](../features/plataforma.md): la conducta del back de cada ruta.
- Contrato: `docs/contracts/openapi.json` del back y `src/shared/api/generated/schema.d.ts` del front.
- Tableros (front, `docs/design/lienzo/`; cada uno es `<Nombre>.dc.html`):

  | Pantalla | Compu | Teléfono |
  |---|---|---|
  | El marco | los 8 de Plataforma (menú lateral, panel y tu menú) | `M-Mas-Plat` y la barra de abajo de cada `M-*` |
  | Organizaciones | `Organizaciones` | `M-Organizaciones` |
  | Ficha de una organización | `Organizacion` (Tweaks: estado, pestaña, página, dominio) | `M-Organizacion` |
  | Cuentas | `Cuentas` | `M-Cuentas` |
  | Ficha de una cuenta | `Cuenta-Plat` (Tweaks: estado, pestaña) | `M-Cuenta-Plat` |
  | Recuperaciones | `Recuperaciones` (Tweak: caso) | `M-Recuperaciones` |
  | Documentos legales | `Legales` (Tweak: pantalla) | `M-Legales` |
  | Auditoría | `Auditoria-Plat` | `M-Auditoria-Plat` y `M-Auditoria-Plat-Detalle` |
  | Configuración | `Config-Plat` | `M-Config-Plat` |
  | Recuperar mi cuenta | `Recuperar` (Tweak: paso) y el enlace de `Ingreso` | `M-Recuperar` y el enlace de `M-Ingreso` |
  | Criterio | `Criterio-Marco`, `Criterio-Tablas`, `Criterio-Dialogos`, `Criterio-Formularios`, `Criterio-Menus`, `Criterio-Botones`, `Criterio-Avisos` y `Criterio-Decisiones` (filas de Plataforma) | |

  **Fuera de alcance:** `Plat-WA-*` y `M-Plat-WA-*` (Plataforma › WhatsApp, Etapa 11), el «WhatsApp» del menú lateral, la rama «WhatsApp» de Administración, «Consultas» de la barra de abajo y el grupo «WhatsApp» de `M-Mas-Plat`.
- Fichas del front (`docs/rules/`): `pantallas-y-ui`, `formularios`, `errores`, `paginado-y-listados`, `accesibilidad`, `responsive`, `accesos-y-permisos`, `datos-y-api`, `estructura-y-features`, `textos-y-traducciones`, `tests` y `formatos`; [`tema.md`](../../../ArquitecturaBaseMutitenantFront/docs/architecture/tema.md) y [`arbol.md`](../../../ArquitecturaBaseMutitenantFront/docs/architecture/arbol.md) del front.
- Modelo de forma: el [plan de la Etapa 7](2026-10-04-etapa-7-publico-y-personal.md).

### Punto de partida (2026-10-05)

- **Back** `5f4ed39`. El back de la Etapa 5 está hecho (T05 a T18, T16b y T18b, en `6bd4ff7` y `5f4ed39`): suspender, reactivar, cerrar sesiones y dar de baja una cuenta mandan solo `{ reason }`. Falta lo que este plan suma: T18c (la primera tarea), T20a y T21a.
- **Front** `2209be7`: lienzo v110 (`c604c0b`) y la consolidación cerrada hasta la tanda 6d-7, sin cambios pendientes. La Guía verificó el 2026-10-05:
  - back: build con 0 advertencias y `dotnet test` **1659/1659**;
  - front: `npm test` **1245/1245**, lint sin errores y build en verde.
- No existe nada de `src/areas/platform/`. `layouts/navigation/platform.ts` está vacío. `/api/me` no trae los permisos de Plataforma (`ProfileSnapshotBuilder` solo llena los de la organización).
- Existen: `src/test/platform-contract.test.ts` (solo el reintento del alta) y el paso «5: ingresar como operador y consultar Plataforma» de `scripts/test-e2e-real.test.mjs`, en rojo desde T02.
- `ClosedStages` es `{0,1,2,3,4,6}` en los dos repos (`tests/ArquitecturaBaseMultitenant.ArchitectureTests/HarnessStage.cs` y `src/test/HarnessStage.ts` del front).

### Restricciones

- **Repositorio:** `main`, sin push. Puede haber otra conversación trabajando en el mismo repo: antes de tocar un archivo **fuera de** `src/areas/platform/`, `src/shared/platform/` y `src/locales/*/platform*.json`, mirá `git status` del front; si ese archivo tiene cambios ajenos, frená y avisale al usuario. Nunca metas un archivo de otra conversación en tu commit.
- **Datos:** sin datos de ejemplo en Development. Tests, capturas y E2E usan sus propias bases. Nunca registres correos, teléfonos, códigos ni tokens.
- **Lienzo:** lo toca solo la conversación de Diseño. No se programa nada que el tablero no tenga.
- **Piezas comunes:** se usan como están. Si falta una pieza que dos features necesitan, va a `src/shared/` (la regla de `estructura-y-features`); T20d dice cuáles.
- **Arnés:** no nacen guardas nuevas. Se amplían las que existen (`AccessDeclarationTests`, `ExplicitRouteInventoryTests`, `FeatureGateTests`, `ModuleControllersTests`, `routes.test.tsx`, `visible.test.ts`, `columns-mobile.test.ts`, `submit-buttons.test.ts`, `parity.test.ts`, `structure.test.ts`, `theme-tokens.test.ts`). Cada test que nombre una ficha del front lleva la marca `(E5)`, porque `HarnessTests` del back lo exige.
- **Contrato:** `generated/` no se edita: `npm run contracts` y se commitea.

### Orden y dependencias

1. T00 (línea de base).
2. T18c (back): la primera tarea, antes de todo el front.
3. «Antes de T19»: los tableros de la v111 (`e685df0`), con el visto bueno del usuario.
4. T19 (contrato y «Recuperar mi cuenta»). No depende del marco.
5. T20 (el marco): T20a (back) → T20b, T20c y T20d.
6. T21 (Organizaciones): T21a (back) → T21b → T21c → T21d.
7. T22 (Cuentas): T22a → T22b.
8. T23 (Recuperaciones, Documentos legales, Auditoría y Configuración): T23a a T23d, en cualquier orden.
9. T24 (docs y capturas) y T25 (E2E y puerta), con los dos repos.

Las partes de back se commitean en el back y después se regenera el contrato del front (`npm run contracts`). Son:
- T18c, T20a y T21a;
- el comienzo de T22a, de T23c y de T23d, por los puntos 4 y 10 de «Para decidir».

---

## La regla de pantallas del usuario

Es pedido del usuario (`pantallas-y-ui.md` del front) y no se saltea.

**Antes de programar cada pantalla:**
1. Le mostrás su tablero tal como va a quedar, a 1440 y a 390, con cada estado (cada opción de cada Tweak):
   - con el recorrido del lienzo: `node scripts/recorrido-lienzo/recorrido.mjs` (desde el front; páginas «Plataforma» e «Ingreso y registro»; los `Plat-WA-*` se saltean), o
   - con las PNG de los tableros que genera `scripts/capturar-etapa-5.mjs` («Antes de T19»).
2. Le decís qué estado es cada uno y qué puntos de «Para decidir» toca esa pantalla.
3. Esperás su visto bueno. Si pide un cambio, frenás: lo dibuja Diseño y se lo volvés a mostrar.

**En cada avance** (antes del commit de cada parte de T19 a T23):
1. La captura lado a lado, tablero y app, a 1440 y a 390, de los estados de esa parte:
   ```text
   npx vite --host 127.0.0.1 --port 5191
   node scripts/capturar-relevamiento.mjs --app-url http://127.0.0.1:5191 --group <grupo> --out docs/design/capturas/etapa-5/lado-a-lado
   node scripts/capturar-relevamiento.mjs --verify --out docs/design/capturas/etapa-5/lado-a-lado
   ```
   Para que la tome, cada caso nuevo se suma a `scripts/capturar-etapa-5.mjs` (su manifiesto, `docs/design/capturas/etapa-5/manifest.json`) y a `scripts/capturar-relevamiento.casos.mjs` (con `pairCases(root, { stage: "etapa-5", … })`, como los de la etapa 6). Los grupos: `plataforma-marco`, `plataforma-organizaciones`, `plataforma-organizacion`, `plataforma-cuentas`, `plataforma-cuenta`, `plataforma-recuperaciones`, `plataforma-legales`, `plataforma-auditoria`, `plataforma-configuracion` y `recuperar`.
2. Le mandás las PNG `-lado-a-lado.png` y, en el mensaje, qué estado es cada una y qué diferencia ves.
3. Le pasás el comando de la vista en vivo, para que la mire mientras programás:
   ```text
   node scripts/recorrido-lienzo/vivo.mjs etapa-5/<id del caso>
   node scripts/recorrido-lienzo/vivo.mjs --tablero <Tablero> --ruta <ruta> --como visual-operator [--telefono]   (antes de que exista el caso)
   ```
   `visual-operator` (Martín Sosa, con los nueve permisos) nace en T20b; para `/recuperar` no hace falta `--como`.
4. Si pide un cambio, lo corregís antes de seguir. Las capturas no se autoaprueban.

---

## Las piezas que se copian

| Para | Copiá de (front) |
|---|---|
| Un listado con filtros y cantidades | `src/areas/business/users/pages/UsersPage.tsx` (`usePagedQuery`, conteos de `filter-counts`, `FilterBar`, `Surface` + `DataTable` + `Pagination`, `RowActions` con las de peligro al final, el alta en un diálogo que se monta al abrir, `toast.success` y `toastActionError`) y `users/columns.tsx` (segunda línea con `detail`, tonos, fila del teléfono) |
| La referencia de listas | `src/areas/business/roles/pages/RolesPage.tsx` y `roles/columns.tsx` |
| Una ficha con pestañas | `src/areas/business/companies/pages/CompanyDetailPage.tsx` (banda con estado y resumen, `actions` y `moreActions`, `tabs` con `count` y `hidden`, `useBreadcrumbLeaf`, `LoadErrorPage` y `NotFoundPage`), `companies/tabs/CompanyMembersTab.tsx` y `useCompanyMembers.ts` |
| El error de un diálogo y releer después de un 409 | `src/areas/business/companies/components/useDialogFailure.ts` (pasa a `shared/` en T20d) |
| Un alta en un diálogo | `src/areas/business/users/components/InviteUserDialog.tsx` y `companies/components/CompanyDialog.tsx` (React Hook Form + zod, el botón que no se apaga, el error en su campo) |
| Un editor en pantalla propia | `src/areas/business/roles/pages/RoleEditorPage.tsx` (`useEditPage`, «Cancelar» al crear, «¿Salir sin guardar?») |
| Un formulario de configuración | `src/areas/business/settings/pages/SettingsPage.tsx` (`useEditForm`, `useEditPage`, `Section` y `SectionFields`, `ConcurrencyBanner`, la versión de `VersionResponse`, el teléfono con los botones fijos abajo) |
| Una lista por cursor y su detalle | `src/areas/business/audit/pages/AuditPage.tsx` (`useCursorList`, `LoadMore`, `FilterBar phoneTools="filter"`, el detalle en grilla y `AuditPhoneDetail`) |
| Ingreso, código y pantalla de estado | `src/areas/public/auth/pages/LoginPage.tsx`, `components/AuthCodeStep.tsx`, `AuthStatus.tsx`, `AuthMessage.tsx`, `AuthLink.tsx`; los textos de `common:code` |
| El menú y los permisos | `layouts/navigation/{business,visible,administration,types}.ts`, `layouts/components/{Sidebar,AdminPanel,Breadcrumbs}.tsx`, `layouts/PrivateLayoutFrame.tsx`, `layouts/MorePage.tsx`, `tenancy/AccessMenu.tsx`, `app/RequirePermission.tsx`, `auth/usePermissions.ts` |
| Errores | `shared/api/errorMessage.ts` (`apiErrorMessage`, `actionErrorMessage`, `toastActionError`, `isNotFoundError`), `shared/api/formErrors.ts` (`applyApiErrorToForm`), `business/roles/errors.ts` (código → campo) |
| Estados | `shared/format/statusTones.ts` (`defineStatusTones`) y `shared/ui/format/StatusBadge.tsx` |
| Altas y envíos | `shared/api/useIdempotentMutation.ts` |
| Pantallas comunes | `Page`, `DataTable`, `FilterBar`, `Pagination`, `LoadMore`, `usePagedQuery`, `useCursorList`, `useEditForm`, `useEditPage`, `FormField`, `FormError`, `Banner`, `ConcurrencyBanner`, `ConfirmDialog`, `RowActions`, `Section`, `List`, `MultiSelect`, `Select`, `SegmentedControl`, `fields/{EmailField,PhoneField,DateField,NumberField,TimeZoneSelect,CultureSelect}`, `dialog` (`size="sm"` 420, normal 560), `Button` con `pending`, `LoadErrorPage`, `NotFoundPage`, `app/{AppBar,BottomNav,ListRow,PhoneBand,FixedActions,SettingsGroup}` |
| Capturas | `scripts/capturar-etapa-6.mjs` (datos del tablero como respuestas de la API) y `scripts/capturar-relevamiento.casos.mjs` |
| Contrato | `src/test/platform-contract.test.ts` y `src/test/organization-audit-contract.test.ts` |

---

## Lo que dicen los tableros

Se copian tal cual: estructura, textos, orden y estados. Los textos van entre «». `{x}` es un dato. Los íconos son los de `shared/ui/icons.tsx` o los de `lucide-react` que ya usan las pantallas de referencia para lo mismo (`Plus`, `RotateCw`, `Trash2`); los que faltan se suman en T20d.

### 0. Reglas que valen para todas

- **El criterio de v110** lo ponen las piezas comunes, no las pantallas: la banda de 64 y las migas con 4 entre sí (`Page`, `Breadcrumbs`); los diálogos con encabezado de 48, ✕ de 32 con ícono de 16, radio 12 y el texto con 20 arriba (`dialog`); las tablas y la caja de filtros con radio 12 y sin sombra (`Surface`, `FilterBar`); filas alternadas, encabezado arena y el estado como punto y palabra (`DataTable`, `StatusBadge`); diálogos de 560 y de 420. **Codex no ajusta medidas a mano:** si una medida del tablero no coincide con la pieza común, la anota y no toca la pieza.
- **Permisos:** lo que el operador no puede hacer no se muestra (`accesos-y-permisos.md`). Ver «El contrato» para el permiso de cada ruta.
- **Confirmaciones con motivo** (§11): toda acción sobre una organización o una cuenta pide «Motivo», salvo las que el tablero hace directas.
- **Avisos:** lo que salió bien, aviso verde que se va solo; una acción directa que falló, aviso rojo con ✕ que no se va solo (`toastActionError`).
- **Teléfono:** es una app (barra de estado, título en la barra, barra de abajo, listas de borde a borde, «+» flotante, diálogos como hojas con manija). Lo arman las piezas comunes con lo que declara cada pantalla.

### 1. El marco de Plataforma

**Compu** (los ocho tableros de escritorio):
- **Menú lateral:**
  - arriba, la marca; debajo, «Plataforma» (negrita) y «Administración del servicio» (gris);
  - «Gestión» (ícono de grilla de 2 × 2) abre el segundo panel «Gestión»;
  - al pie, con la línea de arriba, «Administración» (`ShieldCheckIcon`) abre el panel «Administración»;
  - abajo de todo, tu inicial, tu nombre y tu correo.
- **Panel «Gestión»:** encabezado «Gestión» y el botón «Cerrar Gestión»; «Organizaciones» (`BuildingIcon`), «Cuentas» (`UsersIcon`) y «Recuperaciones» (`KeyIcon`).
- **Panel «Administración»:** encabezado «Administración» y «Cerrar Administración»; «Auditoría» (`ClipboardIcon`), «Documentos legales» (ícono de documento) y «Configuración» (`SlidersHorizontalIcon`).
- Hay **un solo panel abierto**. Arranca abierto el de la pantalla actual (Organizaciones, Cuentas y Recuperaciones → «Gestión»; Auditoría, Documentos legales y Configuración → «Administración») y su opción va marcada.
- **Barra de arriba:** las migas «Plataforma / {sección}» y, en una ficha, «Plataforma / {sección} / {nombre}» («Plataforma» lleva a Organizaciones). A la derecha, tu menú: tu inicial, tu nombre y, debajo, «Plataforma». Abierto: tu inicial, tu nombre y tu correo; una línea; «Salir». Sin «Perfiles» (ver «Para decidir» 7 por «Mi cuenta»).

**Teléfono:**
- **Barra de abajo:** «Organizaciones», «Cuentas», «Recuperaciones» y «Más» (con tu inicial). «Consultas» es de la Etapa 11 y no va.
- **Más** (`M-Mas-Plat`): título «Más», debajo «Plataforma»; tu inicial, tu nombre y tu correo; el grupo «Administración» con «Auditoría», «Documentos legales» y «Configuración» (cada una con ›); al final, «Mi cuenta» y «Salir» (en rojo).
- Las listas de la barra de abajo llevan «Plataforma» debajo del título. Las pantallas que se abren desde «Más» llevan ‹ «Volver» y «Administración» debajo del título (ver «Para decidir» 13 por Auditoría).

### 2. Organizaciones · `/plataforma`

**Compu** (`Organizaciones`):
- **Banda:** ícono `BuildingIcon`; «Organizaciones»; debajo, «{n} organizaciones · {m} esperan aprobación» (con una: «1 espera aprobación»). A la derecha, «+ Crear organización» (principal).
- **Filtros** (caja «Filtrar organizaciones»):
  - buscador «Buscar por nombre o correo»;
  - «Estado: todas» (menú «Filtrar por estado»): «Todas», «Espera aprobación», «Activa», «Suspendida», «Falló el alta», «Cerrada», cada una con su cantidad;
  - «Origen: todos» (menú «Filtrar por origen»): «Todos», «Autoservicio», «Creada por la plataforma», con su cantidad;
  - «Limpiar», solo si hay algo elegido o escrito.
- **Tabla:**

  | Columna | Primera línea | Segunda línea (gris) |
  |---|---|---|
  | Organización | el nombre, enlace a la ficha | «Alta {dd/mm/aaaa} · {Autoservicio \| Plataforma}» |
  | Dueño | el nombre (sin nombre, «—») | el correo |
  | Empresas | el número, a la derecha | |
  | Miembros | el número, a la derecha | |
  | Estado | punto y palabra | |
  | (acciones) | ⋮ «Acciones de {nombre}» | |

- **⋮ por estado** (las de peligro, al final, después de la franja y en rojo):
  - siempre: «Ver ficha» (`EyeIcon`);
  - Espera aprobación: «Aprobar» (`CheckIcon`); franja; «Rechazar» (`CloseIcon`);
  - Falló el alta: «Reintentar el alta» (`RotateCw`), directa, sin diálogo ni motivo;
  - Activa: franja; «Suspender» (`PowerIcon`);
  - Suspendida: «Reactivar» (`PowerIcon`); franja; «Cerrar» (`TrashIcon`);
  - Cerrada: solo «Ver ficha».
- **Sin coincidencias:** «Ninguna organización coincide con los filtros» y «Limpiar filtros».
- **Pie:** «1–7 de 7», «10 por página» (10, 20, 50, 100), «Página 1 de 1» y las flechas «Página anterior» y «Página siguiente».
- **Diálogo «Crear organización»** (560, con ✕ «Cerrar»):
  - «Nombre de la organización*» y «Primera empresa*», en una fila;
  - «Correo del Dueño*», a lo ancho;
  - «Zona horaria» y «Idioma y región», desplegables; cada opción de idioma con su ejemplo debajo («27/09/2026 14:35 · 1.234,50»);
  - «Cancelar» y «Crear organización» («Creando…» mientras espera).
  - Errores debajo del campo: «Escribí el nombre de la organización.», «Escribí el nombre de la primera empresa.», «Escribí el correo.», «Revisá el correo: no parece válido.».
  - Al crear: «Creamos {nombre} e invitamos a {correo} como Dueño.».
- **Confirmaciones** (§11): Aprobar, Rechazar, Suspender, Reactivar y Cerrar.
- **Reintentar el alta:** aviso «Terminamos el alta de {nombre}.».

**Teléfono** (`M-Organizaciones`):
- Barra: «Organizaciones», debajo «Plataforma»; íconos «Buscar» y «Filtros» (el filtro abre una hoja con las opciones y su cantidad).
- Fila: 1.ª línea, el nombre y, a la derecha, la fecha de alta; 2.ª, «{Dueño o, sin nombre, su correo} · {n} empresa(s) · {n} miembro(s)» («sin empresas» con cero); 3.ª, el estado (punto y palabra) · el origen («Autoservicio» o «Plataforma»); ⋮ «Acciones de {nombre}», que abre la hoja.
- «+» flotante «Crear organización». El paginado es «Cargar más».

### 3. Ficha de una organización · `/plataforma/organizaciones/:id`

**Compu** (`Organizacion`; Tweaks: estado «Activa | Espera aprobación | Suspendida | Cerrada», pestaña «Historial | Dueños | Módulos | Dominio verificado | Página pública», página «Publicada | Borrador | Despublicada por la plataforma», dominio «Verificado | Pendiente»):
- **Banda:** «‹» «Volver a Organizaciones»; el nombre; debajo, el estado y:
  - Activa, Suspendida o Cerrada: «Alta el {fecha} por autoservicio · {n} empresas · {n} miembros»;
  - Espera aprobación: «Pidió el alta el {fecha} por autoservicio · 1 empresa · 1 miembro».
- **Acciones de la banda:**
  - Espera aprobación: «Rechazar» (rojo lleno) y «Aprobar» (principal);
  - Activa: «Suspender» (secundario);
  - Suspendida: «Reactivar» (secundario) y «Cerrar» (rojo lleno);
  - Cerrada: ninguna.
- **Cartel debajo de la banda:**
  - Suspendida (alerta): «**Suspendida el {fecha}.** Motivo: {motivo}»;
  - Cerrada (información): «**Cerrada el {fecha}. Solo lectura.** Motivo: {motivo}».
- **Pestañas** («Secciones de la organización»), con la cantidad en gris: «Historial», «Dueños», «Módulos», «Dominio verificado» y «Página pública» (ver «Para decidir» 3 por las cantidades).
- **Historial:** «Fecha y hora», «Qué pasó», «Quién», «Motivo» («—» sin motivo). Ejemplos: «Reactivó la organización», «Suspendió la organización», «Aprobó la organización», «Pidió crear la organización».
- **Dueños:** «Dueño» (nombre; correo debajo), «Último ingreso» («hace 55 minutos») y «Estado» («Activo», verde).
- **Módulos:** «Módulo», «Estado» («Prendido» verde, «Prueba hasta {fecha}» del tono pendiente, «Apagado» gris), «Origen» («Plan», «Prueba», «Manual») y ⋮ (si la organización no está cerrada):
  - «Prender» (`PowerIcon`), si no está prendido;
  - «Extender prueba» (ícono de calendario), si está en prueba, y la franja;
  - «Apagar» (`BanIcon`, rojo), si no está apagado.
- **Dominio verificado:** «Dominio», «Estado» («Verificado» verde, «Pendiente» ámbar), «Registro TXT» (pendiente: el valor, como `ab-verificacion=7f3c9a2e41b8d605`; verificado: «—») y ⋮ (si no está cerrada): pendiente: «Verificar» (`CircleCheckIcon`) y la franja; siempre «Quitar» (`TrashIcon`, rojo). Sin dominios: «Sin dominio de correo».
  - «Verificar» es directa, sin diálogo: «Verificamos {dominio}.».
- **Página pública:** «Dirección» (`{slug}.plataforma.com`), «Estado» («Publicada» verde, «Borrador» gris, «Despublicada por la plataforma» rojo), «Publicada el» y «Motivo» («—» sin dato) y ⋮ (si no está cerrada):
  - Publicada: «Ver página» (`EyeIcon`), franja y «Despublicar» (`BanIcon`, rojo);
  - Borrador: «Ver página», apagada;
  - Despublicada: «Permitir publicar» (`CheckIcon`).
- **Confirmaciones** (§11): Aprobar, Rechazar, Suspender, Reactivar, Cerrar, Prender, Extender prueba, Apagar, Quitar, Despublicar y Permitir publicar.

**Teléfono** (`M-Organizacion`):
- Barra: ‹ «Volver»; el nombre; debajo, «{estado} · {n} empresas · {n} miembros»; ⋮ «Acciones de {nombre}» con las acciones de la banda.
- Pestañas deslizables con la cantidad.
- Historial en filas: 1.ª, «qué pasó» y, a la derecha, la fecha corta («20/09 11:02»); 2.ª, «{quién} · {motivo}» (sin motivo, solo quién).
- Las demás pestañas no están dibujadas en el teléfono: van con la fila que arma `DataTable` desde los roles de cada columna (decisión 12 de este plan).

### 4. Cuentas · `/plataforma/cuentas`

**Compu** (`Cuentas`):
- **Banda:** ícono `UsersIcon`; «Cuentas»; debajo, «{n} cuentas · {m} suspendidas» («1 suspendida»). A la derecha, «+ Invitar operador».
- **Filtros** («Filtrar cuentas»):
  - «Buscar por nombre, correo o teléfono»;
  - «Tipo: todas» (menú «Filtrar por tipo»): «Todas», «Personas», «Operadores», con su cantidad;
  - «Estado: todos» (menú «Filtrar por estado»): «Todos», «Activas», «Invitación pendiente», «Invitación vencida», «Suspendidas», «Necesitan recuperación», «Baja pedida», «Eliminadas», con su cantidad;
  - «Limpiar».
- **Tabla:**

  | Columna | Primera línea | Segunda línea (gris) |
  |---|---|---|
  | Cuenta | el nombre, enlace a la ficha; sin nombre, el correo en gris (sin segunda línea) | el correo |
  | Perfiles | «Operador», «Personal», «Personal + 1 organización», «Personal + {n} organizaciones»; eliminada, «—» | |
  | Último ingreso | relativo («hace 55 minutos», «ayer», «18/09/2026»); invitación, «Nunca» | el método («Código por correo», «Google», «WhatsApp»); invitación, «Vence el {fecha}» |
  | Estado | punto y palabra | |
  | (acciones) | ⋮, solo si la fila tiene acciones | |

- **⋮ por estado:**
  - Invitación pendiente o vencida: «Reenviar invitación» (`RotateCw`); franja; «Revocar invitación» (`CloseIcon`, rojo);
  - Activa: «Cerrar sus sesiones» (`LogOutIcon`); franja; «Suspender» (`PowerIcon`, rojo) y «Dar de baja» (`TrashIcon`, rojo);
  - Suspendida: «Cerrar sus sesiones»; «Reactivar» (`PowerIcon`); franja; «Dar de baja»;
  - Necesita recuperación: «Cerrar sus sesiones»; franja; «Dar de baja» (ver «Para decidir» 9 por «Suspender»);
  - Baja pedida y Eliminada: sin ⋮.
- **Sin coincidencias:** «Ninguna cuenta coincide con los filtros» y «Limpiar filtros». Pie como Organizaciones.
- **Diálogo «Invitar operador»** (560, con ✕), en una columna:
  - «Correo*»;
  - «Permisos de plataforma»: desplegable con casillas; vacío dice «Ninguno»; con permisos, sus nombres separados por coma. Cada opción, con su explicación:

    | Permiso | Nombre | Explicación |
    |---|---|---|
    | `platform.tenants.read` | «Ver organizaciones» | «El listado y la ficha de cada organización.» |
    | `platform.tenants.manage` | «Administrar organizaciones» | «Aprobar, suspender, reactivar y cerrar; módulos y página pública.» |
    | `platform.accounts.read` | «Ver cuentas» | «Buscar cuentas y ver su ficha.» |
    | `platform.accounts.manage` | «Administrar cuentas» | «Suspender, cerrar sesiones y dar de baja.» |
    | `platform.recoveries.manage` | «Recuperaciones» | «Aprobar o rechazar pedidos de «Recuperar mi cuenta».» |
    | `platform.legal.manage` | «Documentos legales» | «Publicar una versión nueva de términos o privacidad.» |
    | `platform.operators.manage` | «Administrar operadores» | «Invitar operadores y cambiar sus permisos.» |
    | `platform.audit.read` | «Ver la auditoría» | «Los eventos de seguridad de todo el servicio.» |
    | `platform.settings.manage` | «Cambiar la configuración» | «Registro de personas y alta de organizaciones.» |

  - «Cancelar» e «Invitar» («Invitando…»).
  - Errores debajo del correo: «Escribí el correo.», «Revisá el correo: no parece válido.», «Ese correo ya tiene una invitación. Reenviala desde la lista.», «Ese correo ya tiene una cuenta. Un operador no puede tener perfiles de persona.».
  - Al invitar: «Invitamos a {correo} como operador.».
- **Reenviar invitación:** directa: «Reenviamos la invitación a {nombre o correo}.».
- **Confirmaciones** (§11): Revocar invitación (sin motivo), Cerrar sus sesiones, Suspender, Reactivar y Dar de baja.

**Teléfono** (`M-Cuentas`):
- Barra: «Cuentas», debajo «Plataforma»; «Buscar» y «Filtros».
- Fila: la inicial en un círculo (gris si no hay nombre); 1.ª línea, el nombre en negrita (o el correo) y, a la derecha, el último ingreso corto («hace 55 min», «ayer», «18/09», «hace 8 h»); 2.ª, el correo (si hay nombre) o «Operador» (invitación); 3.ª, el estado · los perfiles («Personal + 2 organizaciones», «Operador»; invitación: «Invitación pendiente · vence 04/10»); ⋮ «Acciones de {nombre}».
- «+» flotante «Invitar operador».

### 5. Ficha de una cuenta · `/plataforma/cuentas/:id`

**Compu** (`Cuenta-Plat`; Tweaks: estado «Activa | Suspendida | Necesita recuperación | Baja pedida | Baja iniciada por la plataforma | Eliminada», pestaña «Métodos de ingreso | Organizaciones | Historial de seguridad»):
- **Migas:** «Plataforma / Cuentas / {nombre}».
- **Banda:** «‹» «Volver a Cuentas»; el nombre; debajo, el estado y «Alta el {fecha} · Último ingreso {relativo}» (eliminada: «Alta el {fecha} · Eliminada el {fecha}»).
- **Acciones de la banda** (de izquierda a derecha):
  - ⋮ «Más acciones» con «Dar de baja» (rojo), si no tiene la baja pedida ni está eliminada;
  - «Cerrar sesiones» (secundario), si está Activa;
  - «Reactivar» (secundario), si está Suspendida;
  - «Suspender» (secundario), si está Activa o Necesita recuperación.
  - Baja pedida, baja iniciada por la plataforma y Eliminada: ninguna.
- **Carteles** (alerta), debajo de la banda:
  - Suspendida: «**Suspendida el {fecha}.** Motivo: {motivo}»;
  - Baja pedida: «**Pidió la baja el {fecha}.** Se elimina el {fecha}.»;
  - Baja iniciada por la plataforma: «**La plataforma inició la baja el {fecha}.** Se elimina el {fecha}.»;
  - Necesita recuperación: «**Necesita recuperación.** Su único método de ingreso era un correo administrado por {organización} y la membresía terminó el {fecha}.» y el enlace «Ver recuperaciones» (a `/plataforma/recuperaciones`).
- **Pestañas** («Secciones de la cuenta»): «Métodos de ingreso», «Organizaciones» e «Historial de seguridad», con su cantidad (ver «Para decidir» 3).
- **Métodos de ingreso:** caja de lista, sin encabezado de columnas. 1.ª línea, el valor tapado («lu•••@delta.ejemplo.com», «•••• 4521»); 2.ª, «{Correo | WhatsApp}» y, si aplica, «· Principal» y «· Administrado por {organización}»; a la derecha, solo si no sirve, «Ya no sirve para ingresar» (gris). Sin métodos: «No tiene métodos de ingreso.».
- **Organizaciones:** «Organización» (enlace a su ficha), «Rol» («Dueño», con la marca de rol de sistema, o el rol), «Desde» y «Estado de la membresía» («Activa» verde, «Terminada» gris). Vacía: «No está en ninguna organización.».
- **Historial de seguridad:** «Fecha y hora», «Qué pasó», «Quién», «Motivo» («—»). Ejemplos: «Ingresó con código por correo», «Agregó un método de ingreso: un correo», «Creó la cuenta», «Suspendió la cuenta».
- **Confirmaciones** (§11): Cerrar sesiones, Suspender, Reactivar y Dar de baja, con los textos de la ficha (ver «Para decidir» 9).

**Teléfono** (`M-Cuenta-Plat`):
- Barra: ‹ «Volver»; el nombre; debajo, «{estado} · último ingreso {relativo}»; ⋮ «Acciones de {nombre}».
- Pestañas deslizables con la cantidad.
- Métodos en filas: 1.ª, el valor y, a la derecha, el tipo («Correo», «WhatsApp»); 2.ª, «Administrado por {organización} · verificado el {fecha}», «Principal · verificado el {fecha}» o «Verificado el {fecha}»; 3.ª, «Sirve para ingresar» (verde) o «Ya no sirve para ingresar».

### 6. Recuperaciones · `/plataforma/recuperaciones`

**Compu** (`Recuperaciones`; Tweak: caso «Pedidos | Revisar un pedido | Sin pedidos»):
- **Banda:** ícono `KeyIcon`; «Recuperaciones»; debajo, «{n} pedidos · {m} pendientes» («1 pedido», «1 pendiente»); sin pedidos, «Sin pedidos». Sin acción.
- **Filtros** («Filtrar pedidos de recuperación»): «Buscar por cuenta, correo o teléfono»; «Estado: todos» (menú «Filtrar por estado»): «Todos», «Pendientes», «Aprobados», «Rechazados», con su cantidad; «Limpiar».
- **Tabla:** «Cuenta» (el nombre, enlace a la ficha de la cuenta; sin cuenta, «Sin cuenta»; debajo, la fecha y hora del pedido), «Método de antes», «Método nuevo» (tapados), «Estado» («Pendiente», «Aprobado», «Rechazado») y ⋮ con «Revisar» (pendiente) o «Ver detalle» (resuelto), con `EyeIcon`.
- **Vacíos:** con pedidos y sin coincidencias, «Ningún pedido coincide con los filtros» y «Limpiar filtros»; sin pedidos, «No hay pedidos de recuperación», sin pie.
- **Diálogo** (560, con ✕): «Revisar el pedido» (pendiente) o «Pedido de recuperación» (resuelto); «Pedido del {fecha y hora}» (ver «Para decidir» 12); grilla:
  - «Entraba con»: el método de antes;
  - «Método nuevo»: «{método nuevo} · verificado»;
  - «Cuenta encontrada»: el nombre o «Ninguna»;
  - resuelto: «Estado», «Resolvió» y «Motivo».
  - Pendiente sin cuenta: cartel ámbar «No hay ninguna cuenta con el método de antes. Solo se puede rechazar.».
  - Pendiente con cuenta: cartel de información «Si lo aprobás, el método nuevo pasa a ser el principal de la cuenta y le avisamos a la persona.».
  - Pendiente: «Motivo*»; «Cancelar», «Rechazar» (rojo) y «Aprobar» (principal; apagado sin cuenta, y el cartel dice por qué). Resuelto: «Cerrar».
  - Avisos: «Aprobamos el pedido. {método nuevo} ya es el método principal de {cuenta} y le avisamos.» y «Rechazamos el pedido y le avisamos a {método nuevo}.».

**Teléfono** (`M-Recuperaciones`): «Recuperaciones», debajo «Plataforma»; solo «Filtros». Fila: 1.ª, la cuenta o «Sin cuenta» y la fecha corta («27/09 10:20»); 2.ª, «{antes} → {nuevo}»; 3.ª, el estado; ⋮.

### 7. Documentos legales · `/plataforma/legales` y «Publicar versión nueva»

**Compu** (`Legales`; Tweak: pantalla «Documentos | Publicar versión nueva»):
- **Lista:**
  - banda: ícono de documento; «Documentos legales»; debajo, «{n} documentos · {m} versiones publicadas»; «+ Publicar versión nueva»;
  - caja de lista, sin encabezado: 1.ª línea, «Términos y condiciones» o «Política de privacidad»; 2.ª, «Versión {n} · vigente desde {fecha}»; ⋮ con «Publicar versión nueva» (`Plus`), que abre el editor con ese documento elegido.
- **Editor** «Publicar versión nueva»:
  - banda: «‹» «Volver a Documentos legales»; «Publicar versión nueva»; debajo, «Cambios sin guardar» (si hay) y «{documento} · versión {n}»; «Cancelar» y «Publicar»;
  - cartel ámbar: «Al publicarla, todas las cuentas tienen que aceptarla de nuevo para seguir usando el servicio. Una versión publicada no se edita.»;
  - «Documento*» (desplegable: «Términos y condiciones», «Política de privacidad») y «Vigente desde*» (`dd/mm/aaaa`);
  - encabezado «Contenido»; «En español*» y «En inglés*», textos largos a lo ancho;
  - errores: «Escribí la fecha de vigencia.», «Escribí la fecha como dd/mm/aaaa.», «Escribí el contenido en español.», «Escribí el contenido en inglés.»;
  - «Publicar» valida y abre la confirmación (§11, sin motivo); al publicar, vuelve a la lista con «Publicamos la versión {n} de {documento}.».
  - Ver «Para decidir» 12 por la forma de la caja.

**Teléfono** (`M-Legales`): ‹ «Volver»; «Documentos legales», debajo «Administración». Fila: 1.ª, el documento y, a la derecha, «Versión {n}»; 2.ª, «Vigente desde {fecha}»; 3.ª, «{n} versiones»; ⋮. «+» flotante «Publicar versión nueva».

### 8. Auditoría · `/plataforma/auditoria`

**Compu** (`Auditoria-Plat`):
- **Banda:** ícono `ClipboardIcon`; «Auditoría»; sin segunda línea ni acción (sin totales: decisión 24 del plan de la E5).
- **Filtros** («Filtrar la auditoría»), **sin cantidades**:
  - «Buscar en la auditoría»;
  - período (menú «Filtrar por período»): «Hoy», «Últimos 7 días», «Últimos 30 días» (por defecto) y «Todo»; el botón dice la opción elegida y, con «Todo», «Período: todo»;
  - «Tipo: todos» (menú «Filtrar por tipo de evento»): «Todos», «Ingresos», «Organizaciones», «Cuentas», «Operadores», «Configuración»;
  - «Organización: todas» (menú «Filtrar por organización»): «Todas» y las organizaciones (ver «Para decidir» 4);
  - «Limpiar», si algo es distinto de «Últimos 30 días» sin búsqueda ni filtros.
- **Tabla:** «Fecha y hora», «Qué pasó» (un botón que abre el detalle; nombre accesible «Ver el detalle: {qué pasó}»), «Sobre», «Quién» y «Motivo». «Cargar más» al pie. Sin coincidencias: «Nada coincide con los filtros» y «Limpiar filtros».
- **Detalle** (560, con ✕): título, «qué pasó»; grilla con «Fecha y hora», «Sobre», «Quién» («{nombre} · {rol}»), «Motivo» y «Dirección IP»; «Cerrar».

**Teléfono** (`M-Auditoria-Plat` y `M-Auditoria-Plat-Detalle`): ‹ «Volver»; «Auditoría», debajo «Últimos 30 días» (ver «Para decidir» 13); solo «Filtros». Fila: 1.ª, «qué pasó» y la fecha corta; 2.ª, «{sobre} · {quién}»; 3.ª, el motivo, si hay. «Cargar más». El detalle sube como hoja con manija: título, «Fecha y hora», «Sobre», «Quién», «Motivo», «Dirección IP» y «Cerrar».

### 9. Configuración · `/plataforma/configuracion`

**Compu** (`Config-Plat`):
- **Banda:** ícono `SlidersHorizontalIcon`; «Configuración»; debajo, «Cambios sin guardar» (si hay); «Descartar cambios» y «Guardar cambios» (apagado sin cambios).
- **Registro y altas:**
  - «Registro de personas»: «Abierto» («Cualquiera crea su cuenta con su correo o su WhatsApp.») o «Cerrado» («Solo entran las personas invitadas a una organización.»);
  - «Alta de organizaciones»: «Abierta» («La organización queda activa al crearla.»), «Requiere aprobación» («Queda esperando hasta que un operador la aprueba.») o «Cerrada» («Solo la plataforma crea organizaciones.»);
  - «Organizaciones propias por persona»: un número.
- **Contacto:** «Correo de ventas» y «Correo de soporte».
- **Errores:** «Usá un número de 1 a 50.» (ver «Para decidir» 10) y «Ingresá un correo válido.».
- **Guardar** valida y abre la confirmación (§11) «¿Guardar la configuración?»; al guardar, «Guardamos la configuración.».
- Ver «Para decidir» 12 por la forma de la caja.

**Teléfono** (`M-Config-Plat`): ‹ «Volver»; «Configuración», debajo «Administración»; los grupos «Registro y altas» y «Contacto» con cada campo y su valor; «Descartar cambios» y «Guardar cambios» fijos abajo, a lo ancho («Guardar cambios» apagado sin cambios).

### 10. Recuperar mi cuenta · `/recuperar`

**Ingreso** (`Ingreso` y `M-Ingreso`): al pie de las dos puertas, «¿No podés entrar? Recuperá tu cuenta», con el enlace a `/recuperar`.

**Compu** (`Recuperar`; Tweak: paso «1. El correo o teléfono de antes | 2. Un correo o WhatsApp nuevo | 3. Pedido recibido»), en el marco del ingreso (formulario y panel de la marca):
- **Paso 1:** «Recuperar mi cuenta»; «Escribí el correo o el teléfono con el que entrabas.»; selector «Entrabas con» «Correo | WhatsApp»; «Correo electrónico» o «Número de WhatsApp» (con el país); «Seguir»; «Volver a ingresar».
  - Errores: «Ingresá un correo electrónico válido.» o «Ingresá un número de celular válido.».
- **Paso 2:** «‹ Atrás»; «¿Dónde te escribimos?»; «Decinos un correo o un WhatsApp nuevo donde podamos escribirte.»; selector «Escribinos a» «Correo | WhatsApp»; «Correo electrónico nuevo» o «Número de WhatsApp nuevo»; «Enviar código»; «Volver a ingresar».
  - Errores: «Ingresá un correo electrónico válido.», «Ingresá un número de celular válido.», «Tiene que ser un correo distinto del de antes.» o «Tiene que ser un número distinto del de antes.».
  - Ver «Para decidir» 8 por WhatsApp.
- **Código:** «Usar otro correo» (o «Usar otro número»); «Revisá tu correo» (o «Revisá tu WhatsApp»); «Te mandamos un código a **{destino}**. Vence en 10 minutos.»; los seis casilleros; «Verificar» y «Reenviar código». Error: «Escribí los 6 números del código.». Reenviar: «Te mandamos un código nuevo.».
- **Paso 3:** círculo verde; «Recibimos tu pedido»; «Un operador lo va a revisar y te va a avisar a **{destino}**.»; «Volver a ingresar».
- Si el correo nuevo ya es de otra cuenta, la pantalla sigue igual (decisión 16 del plan de la E5).

**Teléfono** (`M-Recuperar`): lo mismo, en una columna y sin el panel de la marca. Es web, sin barra de abajo.

### 11. Las confirmaciones con motivo

Todas son el diálogo chico (420), **sin ✕**: título, texto, «Motivo*» (texto largo; vacío: «Escribí el motivo.»), «Cancelar» y la acción. La acción va roja si es de peligro y principal si no.

| Pantalla | Acción | Título | Texto | Botón | Aviso al terminar |
|---|---|---|---|---|---|
| Organizaciones y ficha | Aprobar | «¿Aprobar {org}?» | «Terminamos el alta y {Dueño} ya puede entrar como Dueño.» | «Aprobar» (principal) | «Aprobamos {org}. Le avisamos a {correo}.» (ver «Para decidir» 11) |
| | Rechazar | «¿Rechazar {org}?» | «La organización queda cerrada y le avisamos a {correo}. Su cuenta personal sigue funcionando.» | «Rechazar» | «Rechazamos {org}.» |
| | Suspender | «¿Suspender {org}?» | «Nadie entra a la organización mientras esté suspendida y se cierran las sesiones abiertas en ella. Los perfiles personales de sus miembros siguen funcionando.» | «Suspender» | «Suspendimos {org}.» |
| | Reactivar | «¿Reactivar {org}?» | «Sus miembros vuelven a entrar con los mismos roles.» | «Reactivar» (principal) | «Reactivamos {org}.» |
| | Cerrar | «¿Cerrar {org}?» | «No se puede deshacer. Sus datos se conservan hasta la purga y nadie vuelve a entrar.» | «Cerrar» | «Cerramos {org}.» |
| Ficha › Módulos | Prender | «¿Prender {módulo}?» | «{org} lo tiene desde ahora, sin fecha de fin.» | «Prender» (principal) | «Prendimos {módulo}.» |
| | Extender prueba | «¿Extender la prueba de {módulo}?» | «{org} lo sigue usando hasta la fecha que elijas.» + campo «Hasta*» (`dd/mm/aaaa`; «Escribí la fecha como dd/mm/aaaa.») | «Extender prueba» (principal) | «Extendimos la prueba de {módulo}.» |
| | Apagar | «¿Apagar {módulo}?» | «Sus pantallas dejan de existir para {org}. Los datos se conservan.» | «Apagar» | «Apagamos {módulo}.» |
| Ficha › Dominio | Quitar | «¿Quitar {dominio}?» | «Deja de contar como dominio de correo de {org}.» | «Quitar» | «Quitamos {dominio}.» |
| Ficha › Página | Despublicar | «¿Despublicar {dirección}?» | «La página deja de verse y {org} no puede volver a publicarla hasta que lo permitas. Le avisamos a la Dueña.» (ver «Para decidir» 11) | «Despublicar» | «Despublicamos {dirección}.» |
| | Permitir publicar | «¿Permitir publicar {dirección}?» | «{org} puede volver a publicar su página cuando quiera.» | «Permitir publicar» (principal) | «{org} ya puede publicar su página.» |
| Cuentas | Revocar invitación (sin motivo) | «¿Revocar la invitación a {nombre}?» | «El enlace deja de funcionar. Podés volver a invitarla cuando quieras.» | «Revocar» | «Revocamos la invitación a {nombre}.» |
| Cuentas (lista) | Cerrar sus sesiones | «¿Cerrar las sesiones de {nombre}?» | «Tiene que volver a ingresar en todos sus dispositivos. No le cambia nada más.» | «Cerrar sesiones» | «Cerramos las sesiones de {nombre}.» |
| | Suspender | «¿Suspender la cuenta de {nombre}?» | «No puede entrar con ningún perfil y se cierran todas sus sesiones. Sus organizaciones siguen funcionando para el resto de los miembros.» | «Suspender» | «Suspendimos la cuenta de {nombre}.» |
| | Reactivar | «¿Reactivar la cuenta de {nombre}?» | «Vuelve a entrar con sus perfiles de siempre.» | «Reactivar» (principal) | «Reactivamos la cuenta de {nombre}.» |
| Cuenta (ficha) | Cerrar sesiones | igual | «Tiene que volver a ingresar en todos sus dispositivos, como persona y en sus organizaciones. No le cambia nada más.» | «Cerrar sesiones» | igual |
| | Suspender | igual | «No puede entrar como persona ni en sus organizaciones, y se cierran sus sesiones en los dos lados. Sus organizaciones siguen funcionando para el resto de los miembros.» | «Suspender» | igual |
| | Reactivar | igual | «Vuelve a entrar como persona y en sus organizaciones, con los mismos roles.» | «Reactivar» (principal) | igual |
| Cuentas y ficha | Dar de baja | «¿Dar de baja la cuenta de {nombre}?» | «Se cierran sus sesiones y le avisamos por todos sus métodos de ingreso. Tiene 30 días para cancelar la baja ingresando; después se elimina la cuenta. La plataforma no puede cancelarla.» | «Dar de baja» | «Dimos de baja la cuenta de {nombre}. Se elimina el {fecha}.» |
| Recuperaciones | (en su diálogo, §6) | | | | |
| Documentos legales | Publicar (sin motivo) | «¿Publicar la versión {n} de {documento}?» | «Desde el {fecha}, todas las cuentas tienen que aceptarla para seguir usando el servicio. Después no se puede editar.» | «Publicar» (principal) | «Publicamos la versión {n} de {documento}.» |
| Configuración | Guardar | «¿Guardar la configuración?» | «Cambia cómo entran las personas y las organizaciones nuevas. Lo que ya existe no cambia.» | «Guardar» (principal) | «Guardamos la configuración.» |

- **Una regla que bloquea, conocida antes de confirmar** (Dar de baja del único Dueño, tablero Cuentas y Cuenta-Plat): el diálogo muestra, en una franja roja arriba, «Es el único Dueño de {organización}. Primero la organización tiene que sumar otro Dueño, o la plataforma tiene que cerrarla.»; no muestra «Motivo» y «Dar de baja» va apagado (la franja dice por qué).
- **Lo que el back rechaza al confirmar** (un 409 u otro error de negocio): en la franja del diálogo, arriba de los botones, con el texto del back (`formularios.md`). Después de un 409 de concurrencia o de estado («No se puede cambiar la cuenta a ese estado.», «La organización no puede pasar a ese estado.», «Ese pedido ya se resolvió.»), al cerrar el diálogo se relee la pantalla.
- **Al terminar bien,** la versión que respondió la acción es la base de la siguiente (las acciones sobre una cuenta no llevan versión).
- **Mientras espera,** el botón dice lo que hace (decisión 10 de este plan).

### 12. Los tonos de los estados

Cada feature los registra con `defineStatusTones` y los pinta `StatusBadge`. Son los de la compu (ver «Para decidir» 6 por los del teléfono):

| Enum | Valor → texto · tono |
|---|---|
| `TenantStatus` | `PendingApproval` «Espera aprobación» · pending; `Provisioning` «Falló el alta» · danger; `Active` «Activa» · success; `Suspended` «Suspendida» · warning; `Closed` «Cerrada» · neutral |
| `PlatformAccountState` | `Active` «Activa» · success; `Suspended` «Suspendida» · warning; `NeedsRecovery` «Necesita recuperación» · danger; `PendingDeletion` «Baja pedida» · warning; `Deleted` «Eliminada» · neutral; `Invited` «Invitación pendiente» · pending; `InvitationExpired` «Invitación vencida» · warning |
| `AccountRecoveryStatus` | `Pending` «Pendiente» · pending; `Approved` «Aprobado» · success; `Rejected` «Rechazado» · danger |
| Módulo (derivado) | «Prendido» · success; «Prueba hasta {fecha}» · pending; «Apagado» · neutral |
| `TenantDomainStatus` | `Pending` «Pendiente» · warning; `Verified` «Verificado» · success |
| Página pública (derivado) | «Publicada» · success; «Borrador» · neutral; «Despublicada por la plataforma» · danger |
| Dueño (derivado) | «Activo» · success |
| `MemberStatus` (membresía de una cuenta) | `Active` «Activa» · success; `Removed` «Terminada» · neutral |

Los textos de los enums van en `enums.json` (es y en): `TenantStatus`, `TenantRegistrationSource` (`SelfService` «Autoservicio», `Platform` «Plataforma»), `PlatformAccountState`, `PlatformAccountKind` (`Person` «Persona», `Operator` «Operador»), `AccountRecoveryStatus`, `LoginAuditMethod` (`Code` «Código por correo», `Google` «Google», `WhatsAppCode` y `WhatsAppLink` «WhatsApp»), `TenantFeatureSource` (`Plan` «Plan», `Manual` «Manual», `Trial` «Prueba»), `TenantDomainStatus`, `LegalDocumentKind` (`Terms` «Términos y condiciones», `Privacy` «Política de privacidad»), `ConsumerSignupMode` y `BusinessSignupMode` (los textos de §9). Las opciones de los filtros van en plural en el namespace de cada feature («Activas», «Personas», «Pendientes»…).

---

## El contrato, pantalla por pantalla

### Reglas comunes

- **Rutas y tipos:** los del contrato generado (`shared/api/generated/schema.d.ts`), con alias en `shared/api/types.ts`. Cada feature tiene su `api/<recurso>.ts` con sus claves de consulta.
- **Versiones:** la `version` de la fila o de la ficha viaja en cada acción que la pide (`formularios.md`, «Ediciones simultáneas»). Un 409 `General.ConcurrencyConflict` («Otra persona cambió esto. Revisá la versión más reciente antes de guardar.») va en la franja del diálogo, o en el `ConcurrencyBanner` de Configuración.
- **La versión que responde:** desde T18c, toda escritura con `version` (los `PUT` y los `POST` de acciones) responde 200 con `VersionResponse` (`{ version }`); solo los `DELETE` responden 204. **Después de cada acción con versión, la base pasa a ser la `version` de la respuesta,** como hace `useEditForm` (`wrote`): la próxima acción sobre lo mismo la manda aunque la relectura de fondo todavía no llegó, y una relectura con otra versión no la pisa (si otra persona cambió algo, el back responde 409).
- **Las acciones sobre una cuenta no llevan versión** (T18b): suspender, reactivar, cerrar sesiones y dar de baja mandan solo `{ reason }`. Desde T18c, la lista y la ficha de cuentas tampoco traen `version`. Cambiar o quitar los permisos de un operador usan la versión de su asignación, `operatorVersion` de la ficha.
- **Idempotencia:** las rutas marcadas `[Idempotent]` exigen `Idempotency-Key`: se mandan con `useIdempotentMutation` (la clave nace al abrir el diálogo). Son todos los `POST` de Plataforma y de `/api/account-recovery`, y `PUT`/`DELETE /api/platform/accounts/{id}/operator`. Las demás (`PUT /settings`, `PUT /features/{key}`, `DELETE /domains/{domain}`, `DELETE /operators/{id}/invitation`) van con `useMutation`.
- **Errores:** se decide por `error.code`; el texto es el `detail` del back (`apiErrorMessage`, `actionErrorMessage`). Un 403 de la consulta principal muestra «No tenés permiso»; un 404 de una ficha, «No encontramos esta página»; si la ficha no carga, «No pudimos abrir esta pantalla» con «Reintentar». Red, 5xx y 429 los avisa el `MutationCache`.
- **Validación en el front:** zod, con los textos del tablero. Lo que rechaza el back de un campo va debajo de ese campo (`applyApiErrorToForm`); lo demás, en la franja.

### Marco · `GET /api/me`

- `effectivePermissions.platform` (nuevo en T20a): los permisos de Plataforma del operador (las nueve claves de `PlatformPermissions`). `usePermissions` suma `canPlatform(permission)`.

### Organizaciones

| Ruta | Permiso | Manda | Recibe | Errores que muestra |
|---|---|---|---|---|
| `GET /api/platform/tenants/paged` | `tenants.read` | `page`, `pageSize`, `sort`, `search`, `status` (`TenantStatus`), `source` (`TenantRegistrationSource`) | `items[]`: `id`, `name`, `slug`, `status`, `source`, `createdAtUtc`, `companyCount`, `memberCount`, `ownerName`, `ownerEmail`, `version`; `totalCount` | 403: «No tenés permiso» |
| `GET /api/platform/tenants/filter-counts` | `tenants.read` | lo mismo | `total`, `statuses[]` (`status`, `count`), `sources[]` (`source`, `count`) | — |
| `POST /api/platform/tenants` | `tenants.manage` | `name`, `firstCompanyName`, `ownerEmail`, `culture`, `timeZoneId` | 201: `id`, `name`, `slug`, `firstCompanyId`, `invitationId` | 400 en su campo (`Tenancy.Tenant.NameRequired` «Ingresá el nombre de la organización.», `Tenancy.Tenant.NameTooLong` «El nombre de la organización es demasiado largo.», «Este campo es obligatorio.», «Ingresá un correo válido.»); lo demás, en la franja |
| `POST /api/platform/tenants/{id}/approve`, `/reject`, `/suspend`, `/reactivate`, `/close` | `tenants.manage` | `version`, `reason` | 200 `VersionResponse` (desde T18c; hoy 204): la versión nueva de la organización | 409 `General.ConcurrencyConflict`; 409 `Tenancy.Tenant.InvalidTransition` «La organización no puede pasar a ese estado.»; 404 `Tenancy.Tenant.NotFound` «No encontramos la organización.» |
| `POST /api/platform/tenants/{id}/retry-provisioning` | `tenants.manage` | `version` (sin motivo) | 200 `VersionResponse` (desde T18c) | 409 `Tenancy.Tenant.ProvisioningConflict` «La organización ya existe con otros datos de configuración.» u otro 409 del back: aviso rojo |

### Ficha de una organización

| Ruta | Permiso | Manda | Recibe | Errores |
|---|---|---|---|---|
| `GET /api/platform/tenants/{id}` (T21a lo amplía) | `tenants.read` | — | lo de la fila y, desde T21a, `owners[]` (`id`, `displayName`, `email`, estados de identidad y membresía) y `lastStateChange` (`atUtc`, `action`, `reason`; `null` sin evidencia) | 404: «No encontramos esta página» |
| `GET /api/platform/tenants/{id}/history` (nueva en T21a) | `tenants.read` | `after`, `limit` | cursor de eventos: fecha, acción traducida, quién y motivo | — |
| `GET /api/platform/tenants/{id}/features` | `tenants.read` | — | `[]`: `key`, `enabled`, `source` (`Plan`/`Manual`/`Trial`), `expiresAtUtc`, `version` | — |
| `PUT /api/platform/tenants/{id}/features/{featureKey}` | `tenants.manage` | `enabled`, `source`, `expiresAtUtc`, `version` (0 sin fila), `reason` | 200 `VersionResponse` | 409; 400 `Features.Module.ExpiryRequired` «Escribí cuándo termina la prueba.» (debajo de «Hasta»), `Features.Module.InvalidSource`, `InvalidKey`, `InvalidScope`; 404 `Features.Module.NotFound` «No encontramos el módulo.» |
| `GET /api/platform/tenants/{id}/domains` | `tenants.manage` (registra la entrada a la organización) | — | `[]`: `id`, `domain`, `status`, `txtRecordName`, `txtRecordValue`, `verifiedAtUtc`, `version`, `managedEmailCount` | — |
| `POST /api/platform/tenants/{id}/domains/{domain}/verify` | `tenants.manage` | `version`, `reason` | 200 `VersionResponse` (desde T18c): la versión nueva del dominio | 409 `Settings.TenantDomain.TxtNotFound` «No encontramos el registro TXT de verificación en el DNS.»; 409 `Settings.TenantDomain.AlreadyVerified` «El dominio ya está verificado.»; 503 `Settings.TenantDomain.DnsUnavailable` (lo avisa el `MutationCache`) |
| `DELETE /api/platform/tenants/{id}/domains/{domain}` | `tenants.manage` | `version`, `reason` (en el cuerpo) | 204 | 409; 404 `Settings.TenantDomain.NotFound` |
| `GET /api/platform/tenants/{id}/public-site` | `tenants.manage` (registra la entrada) | — | `slug`, `name`, `isPublished`, `publishBlockedAtUtc`, `publishBlockedReason`, `version` | — |
| `POST /api/platform/tenants/{id}/public-site/unpublish`, `/allow-publish` | `tenants.manage` | `version` (la de la página), `reason` | 200 `VersionResponse` (desde T18c): la versión nueva de la página | 409; 400 `PublicSite.PublicPage.BlockReasonRequired` «El bloqueo de moderación necesita un motivo.» (debajo de «Motivo») |

- «Verificar» manda motivo: el tablero no lo pide; va el texto fijo «Verificar el dominio» de los locales (decisión 17 de este plan).
- «Publicada el» no tiene dato en el contrato (ver «Para decidir» 4).

### Cuentas y operadores

| Ruta | Permiso | Manda | Recibe | Errores |
|---|---|---|---|---|
| `GET /api/platform/accounts/paged` | `accounts.read` | `page`, `pageSize`, `sort`, `search`, `state` (`PlatformAccountState`), `kind` (`PlatformAccountKind`) | `items[]`: `id`, `displayName`, `primaryEmail`, `kind`, `state`, `hasPersonalSpace`, `organizationCount`, `lastSignInAtUtc`, `lastSignInMethod`, `invitationExpiresAtUtc` (sin `version` desde T18c) | 403 |
| `GET /api/platform/accounts/filter-counts` | `accounts.read` | lo mismo | `total`, `states[]`, `kinds[]` | — |
| `GET /api/platform/accounts/{id}` | `accounts.read` | — | `displayName`, `kind`, `state`, `createdAtUtc`, `lastSignInAtUtc`, `lastSignInMethod`, `invitationExpiresAtUtc`, `hasPersonalSpace`, `deletion` (`requestedAtUtc`, `scheduledForUtc`, `requestedByPlatform`), `loginMethods[]` (`type`, `maskedValue`, `isPrimary`, `verifiedAtUtc`, `managedByOrganizationName`, `canSignIn`), `organizations[]` (`tenantId`, `name`, `roleName`, `joinedAtUtc`, `status`, `tenantStatus`, `endedAtUtc`), `soleOwnerOrganizations[]`, `operatorPermissions`, `operatorVersion` (la versión de la asignación del operador; `null` si no es operador). Sin `version` desde T18c | 404 `Users.User.NotFound`: «No encontramos esta página» |
| `GET /api/platform/audit?accountId={id}` | `audit.read` | `after`, `limit`, `period=all`, `accountId` | el cursor de Auditoría | — (sin `audit.read`, la pestaña no se muestra) |
| `POST /api/platform/accounts/{id}/suspend`, `/reactivate`, `/revoke-sessions`, `/deletion` | `accounts.manage` | solo `{ reason }` (T18b, `PlatformAccountReasonHttpRequest`) | 204 | nunca un 409 de concurrencia: dos acciones a la vez, una gana y la otra recibe el de estado, 409 `Users.User.InvalidTransition` «No se puede cambiar la cuenta a ese estado.» o, la baja, 409 `Legal.AccountDeletion.AlreadyPending`; además, 409 `Platform.Operator.LastManager` «Tiene que quedar al menos un operador activo que pueda administrar operadores.», 409 `Legal.AccountDeletion.LastAdmin` y 403 `Users.User.CannotModifySelf` «No podés aplicar este cambio sobre tu propia cuenta.» (ver «Para decidir» 11 por los textos de la baja) |
| `POST /api/platform/operators/invitations` | `operators.manage` | `email`, `permissions[]` | 201: `id`, `expiresAtUtc` | 409 `Platform.Operator.InvitationExists` y `Platform.Operator.AccountExists` (debajo del correo, con el texto de §4); 400 `Platform.Operator.PermissionsRequired` «Elegí al menos un permiso de plataforma.» (debajo de «Permisos de plataforma») |
| `POST /api/platform/operators/{id}/invitation/resend` | `operators.manage` | — | 200: `id`, `expiresAtUtc` | 409 `Platform.Operator.NoPendingInvitation` «Esa cuenta no tiene una invitación de operador pendiente.»: aviso rojo |
| `DELETE /api/platform/operators/{id}/invitation` | `operators.manage` | — | 204 | lo mismo, en la franja |
| `PUT /api/platform/accounts/{id}/operator` y `DELETE` | `operators.manage` | `version` (= `operatorVersion` de la ficha, la de la asignación, no la de la cuenta), `reason` y, el `PUT`, `permissions[]` | `PUT`: 200 `VersionResponse` (la nueva `operatorVersion`); `DELETE`: 204 | no se programan hasta resolver «Para decidir» 1 |

- El bloqueo del único Dueño sale de `soleOwnerOrganizations` de la ficha (decisión 9 de este plan).
- «Se elimina el {fecha}» sale de `deletion.scheduledForUtc` al releer la cuenta después de la baja (decisión 9).
- «Necesita recuperación»: la organización sale de `managedByOrganizationName` del método que ya no sirve y la fecha, de `endedAtUtc` de esa membresía.
- «Suspendida el {fecha}. Motivo: {motivo}» y «Eliminada el {fecha}» no tienen dato en el contrato (ver «Para decidir» 4).

### Recuperaciones

| Ruta | Permiso | Manda | Recibe | Errores |
|---|---|---|---|---|
| `GET /api/platform/recoveries/paged` | `recoveries.manage` | `page`, `pageSize`, `sort`, `search`, `status` (`AccountRecoveryStatus`) | `items[]`: `id`, `createdAtUtc`, `accountId`, `accountName`, `previousMethodType`, `previousMethod`, `newMethodType`, `newMethod` (tapados), `status`, `decidedAtUtc`, `decidedBy`, `reason`, `version` | 403 |
| `GET /api/platform/recoveries/filter-counts` | `recoveries.manage` | lo mismo | `total`, `statuses[]` | — |
| `POST /api/platform/recoveries/{id}/approve`, `/reject` | `recoveries.manage` | `version` (la del pedido), `reason` | 200 `VersionResponse` (desde T18c): la versión nueva del pedido | en la franja del diálogo: `Platform.AccountRecovery.NoAccount`, `MethodInUse` «Ese correo ya es de otra cuenta.», `MethodAlreadyOnAccount` «Ese correo ya es un método de esa cuenta.», `OperatorAccount` «La cuenta es de un operador de la plataforma: no se recupera desde acá.», `AlreadyResolved` «Ese pedido ya se resolvió.», `NotFound` «No encontramos el pedido.», 409 de concurrencia |

### Documentos legales

| Ruta | Permiso | Manda | Recibe | Errores |
|---|---|---|---|---|
| `GET /api/platform/legal-documents` | `legal.manage` | — | `[]` por tipo: `kind`, `currentVersion`, `currentEffectiveAtUtc`, `versions[]` (`id`, `version`, `effectiveAtUtc`, `isCurrent`) | 403 |
| `POST /api/platform/legal-documents` | `legal.manage` | `kind`, `effectiveOn` (fecha civil), `contents[]` (`culture`, `text`), uno por cultura habilitada | 201: `id`, `version` | 400 en su campo (la fecha anterior a hoy, el texto vacío o de más de `TextLimits.LegalDocument`); lo demás, en la franja |

- La lista muestra la versión vigente y su fecha. El resumen suma `versions.length` de los dos tipos.
- Publicar una versión que rige hoy bloquea también al operador hasta aceptarla: lo resuelve `LegalAcceptanceGate`, que ya existe.

### Auditoría

| Ruta | Permiso | Manda | Recibe | Errores |
|---|---|---|---|---|
| `GET /api/platform/audit` | `audit.read` | `after`, `limit` (25), `period` (`0`, `7`, `30`, `all`), `type` (`signins`, `organizations`, `accounts`, `operators`, `settings`), `tenantId`, `search` | `items[]`: `id`, `occurredAtUtc`, `type`, `action` (código técnico), `summary` («qué pasó», traducido), `target` («sobre»), `targetTenantId`, `targetAccountId`, `actor`, `actorRole`, `reason`, `ipAddress` (tapada; `null` en los ingresos); `nextCursor`, `hasMore` | 403 |
| `GET /api/platform/audit/{id}` | `audit.read` | — | la misma fila | 404 `Platform.Audit.NotFound` |

### Configuración

| Ruta | Permiso | Manda | Recibe | Errores |
|---|---|---|---|---|
| `GET /api/platform/settings` | `settings.manage` | — | `consumerSignup` (`Open`/`Closed`), `businessSignup` (`Open`/`RequiresApproval`/`Closed`), `maxOwnedOrganizations`, `salesEmail`, `supportEmail`, `version` | 403 |
| `PUT /api/platform/settings` | `settings.manage` | lo mismo, `version` y `reason` | 200 `VersionResponse` | 400 en su campo («Permití al menos una organización por persona.», «Ingresá un correo válido.», «Este campo es obligatorio.»); 409 en el `ConcurrencyBanner` |

### Recuperar mi cuenta (sin sesión)

| Ruta | Manda | Recibe | Errores |
|---|---|---|---|
| `POST /api/account-recovery/code` | `email` (el nuevo), `nonce` (43 caracteres base64url que genera el navegador una vez por recorrido) | 202: `resendAfterSeconds` | 429 con cuenta regresiva (`Auth.LoginCode.ResendTooSoon`, `TooManyRequests`), como el ingreso |
| `POST /api/account-recovery/verify` | `email`, `nonce`, `code` | 204 | debajo de los casilleros: `Auth.LoginCode.Invalid` «El código no es válido.», `Expired` «El código venció. Pedí uno nuevo.», `AlreadyUsed`, `TooManyAttempts` |
| `POST /api/account-recovery` | `email`, `nonce`, `previousEmail` o `previousPhone` (`country`, `number`) | 202 | `Platform.AccountRecovery.VerificationRequired` «Pedí un código nuevo y verificalo.» (vuelve al código); 400 `RecoveryMethodRepeated` «Tiene que ser un correo distinto del de antes.» |

- Las tres llevan `Idempotency-Key` y `meta: { handlesError: true }`, como el ingreso. La respuesta nunca dice si la cuenta existe.

---

## Protocolo de cada tarea de lógica

1. Escribir los tests de conducta y correr el focal: el rojo es por la conducta ausente (un error de entorno no cuenta).
2. Implementar lo mínimo, con las piezas de «Las piezas que se copian», textos en es y en, y `oxlint` limpio.
3. Repetir el focal en verde; `npm run lint` y `npm run build`; si cambió el contrato, `npm run contracts:check`. En el back: el focal, el build sin advertencias y, si hay HTTP o persistencia, la integración con PostgreSQL.
4. La captura lado a lado y el comando de la vista en vivo al usuario («La regla de pantallas del usuario»).
5. Los docs afectados en el mismo commit. `git diff --check` y un conventional commit en español solo con tus rutas: `git commit -m "…" -- <rutas>` (los archivos nuevos, antes con `git add -- <ruta>`).

Comandos focales:

```text
npm test -- <ruta.test.tsx>
npm run lint && npm run build
npm run contracts && npm run contracts:check
npm run test:e2e:real
dotnet test --project tests/ArquitecturaBaseMultitenant.Application.UnitTests/ArquitecturaBaseMultitenant.Application.UnitTests.csproj -- --filter-class "<namespace>.<Clase>"
dotnet test --project tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/ArquitecturaBaseMultitenant.Api.IntegrationTests.csproj -- --filter-class "<namespace>.<Clase>"
dotnet build src/ArquitecturaBaseMultitenant.Api/ArquitecturaBaseMultitenant.Api.csproj
node scripts/arneses/arnes.mjs realinear-docs --detectar
```

Cada test de pantalla prueba, cuando el estado existe: carga, vacío, sin coincidencias, error con «Reintentar» y sin permiso; el recorrido de cada diálogo; cada `code` que traduce la feature; los formatos en es-AR y en-US; axe sin violaciones; 390 con `asPhone()`; el 409 en cada edición; la misma `Idempotency-Key` al reintentar un alta; y, en los listados, 1 y 50 filas («1 de 1» y «1–10 de 50», «Página 1 de 5»).

---

## Seguimiento y tareas verificables

### T00 · Línea de base

- [x] **Los dos repos limpios en `main`,** sin trabajo de otra conversación a medio commitear; Aspire apagado. El último commit de cada uno, en el «Registro de resultados». **Evidencia (2026-10-05, retomada):** `git branch --show-current` → `main` en ambos; `git status --short` → ambos limpios al retomar, back `97c28f9` y front `e685df0` (Diseño cerró v111). `aspire ps` → ningún AppHost. El bloqueo anotado en `2ed8f13` quedó resuelto.
- [x] **El contrato del front al día con el back:** T18b ya está en el back (`6bd4ff7` y `5f4ed39`); el front regenera sus tipos desde `docs/contracts/openapi.json` de HEAD (`npm run contracts`, `npm run contracts:check`). **Evidencia (2026-10-05, retomada):** `npm run contracts` y `npm run contracts:check` → exit 0; `schema.d.ts` coincide con OpenAPI. Commit front `d73f362`; `contracts.test.ts`, `httpClient.test.ts` y `queryClient.test.ts` → 29/29; lint exit 0 sin errores (advertencias preexistentes), build exit 0.
- [x] **La consolidación del front cerrada** hasta la tanda 6d-7 (`2209be7`), sin cambios pendientes en `shared/ui`, `layouts` ni `SettingsPage`. **Evidencia (2026-10-05):** `git log -1 --format='%h %s'` → `2209be7`; `git merge-base --is-ancestor 2209be7 HEAD` → exit 0; `git status --short` → cambios solo en `docs/design/lienzo/`.
- [x] **El lienzo vigente es v110 (`c604c0b`) o posterior.** Antes de «Antes de T19» tiene que ser **v111**, con los dibujos de «Para decidir» 1, 2, 6, 7 y 8. Con cada versión nueva, releé los tableros que cambiaron y corregí «Lo que dicen los tableros» antes de seguir. **Evidencia (2026-10-05, retomada):** `docs/design/lienzo/README.md` declara «versión 111», front `e685df0`; se releyeron «Para Codex», «Fuente visual» y «Lo que suma la v111» de `97c28f9`. Recuperación se programa «Sin WhatsApp»; cada tablero cambiado se relee antes de su pantalla.
- [x] `realinear-docs --detectar` en 0 en los dos repos; `ClosedStages` en `{0,1,2,3,4,6}`. **Evidencia (2026-10-05):** desde el back, `node scripts/arneses/arnes.mjs realinear-docs --detectar` → exit 0, auto/modelo/humano 0 (revisa ambos repos); `node scripts/arneses/arnes.mjs realinear-docs --detectar --repo front` → exit 0, auto/modelo/humano 0. `rg -n 'ClosedStages'` en ambos `HarnessStage` → `[0, 1, 2, 3, 4, 6]`.
- [x] **«Para decidir», resuelto por el usuario y la Guía** (2026-10-05). Cada punto dice qué hacer; los que suman back dicen en qué tarea.

### T18c · Back · Toda acción con versión responde la versión nueva (la primera tarea)

Es el segundo ítem abierto de T18b, y lo decidió la Guía el 2026-10-05. El criterio es uno solo: hoy estas 11 son las únicas escrituras con versión que responden 204 sin ser un `DELETE`.

**Archivos:**
- `Api/Controllers/Platform/{PlatformTenantsController,PlatformPublicSiteController,PlatformDomainsController,PlatformRecoveriesController}.cs` (`[ProducesResponseType<VersionResponse>(StatusCodes.Status200OK)]` en lugar del 204, como `PlatformSettingsController.Update`);
- los servicios de esas acciones en `Application/Services/Platform/` (devuelven `Result<VersionResponse>`) y sus puertos en `Application/Interfaces/Services/`;
- `Application/Models/Platform/PlatformAccountModels.cs` y `Infrastructure/Persistence/Readers/PlatformAccountReader.cs` (sin `version` en la cuenta);
- `tests/ArquitecturaBaseMultitenant.ArchitectureTests/VersionedContractTests.cs`;
- los tests de integración de cada acción (`tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Platform/`);
- `docs/rules/concurrencia.md` (línea 9), `docs/features/plataforma.md`, `docs/contracts/openapi.json`;
- front: `src/shared/api/generated/schema.d.ts` (regenerado) y `src/test/platform-contract.test.ts`.

**Copiá de:** `Application/Services/Roles/RoleService.cs`, `UpdateAsync`: el trabajo de la transacción devuelve la entidad guardada y el servicio arma la respuesta después de `ExecuteInTransactionAsync` con `VersionResponse.Of(result)`, **sin releer**.

1. **Las 11 acciones `POST` con `version` responden 200 con `VersionResponse`,** como los `PUT` y como publicar y despublicar la Página pública:
   - `POST /api/platform/tenants/{id}/approve`, `/reject`, `/suspend`, `/reactivate`, `/close` y `/retry-provisioning`: la versión de la organización;
   - `POST /api/platform/tenants/{id}/public-site/allow-publish` y `/unpublish`: la versión de la página;
   - `POST /api/platform/tenants/{id}/domains/{domain}/verify`: la versión del dominio;
   - `POST /api/platform/recoveries/{id}/approve` y `/reject`: la versión del pedido.
2. **`PlatformAccountResponse` y `PlatformAccountDetailResponse` sin `version`:** ya ninguna acción sobre una cuenta la usa (T18b). Los permisos de un operador (`PUT` y `DELETE /api/platform/accounts/{id}/operator`) siguen con la versión de su propia asignación, que la ficha ya expone aparte en `operatorVersion`; no cambia.

- [ ] **Rojo:**
  - `VersionedContractTests.Versioned_saves_answer_the_new_version` pasa de «todo `PUT` con `version`» a «todo `PUT` o `POST` con `version`» (un `DELETE` sigue con 204), y el control de la guarda suma un `POST` con versión que responde 204: el test falla con las 11 acciones;
  - cada test de integración de esas acciones espera 200 con la `version` que quedó guardada (la misma que da una lectura posterior) y que la acción siguiente con esa versión no da 409;
  - los tests de Cuentas esperan el listado y la ficha sin `version`.
- [ ] **Implementación:** cada servicio devuelve la entidad guardada desde su trabajo transaccional y arma `VersionResponse.Of(result)` después de `ExecuteInTransactionAsync`; las entradas a la organización (`ITenantScope`) y los eventos siguen como están. `PlatformAccountReader` deja de proyectar el `xmin` de la cuenta.
- [ ] **Docs:** la línea 9 de `docs/rules/concurrencia.md` dice «un `PUT` o `POST` con `version` responde 200 con `VersionResponse`»; `plataforma.md` dice qué devuelve cada acción.
- [ ] **Verde:** los focales, `VersionedContractTests`, la integración de Plataforma afectada, el build sin advertencias y el `openapi.json` regenerado. En el front: `npm run contracts`, `npm run contracts:check` y `platform-contract.test.ts` con las 11 respuestas y las cuentas sin `version`.

Commit back `fix(plataforma): las acciones con versión responden la versión nueva`; front `chore(contratos): las acciones de Plataforma con VersionResponse`.

**Hecho cuando** no queda ninguna escritura con versión que responda 204 salvo los `DELETE`, y la cuenta no expone una versión que nadie usa.

### Antes de T19 · Los tableros, con el visto bueno del usuario

**Archivos:** `scripts/capturar-etapa-5.mjs` (copiá `capturar-etapa-6.mjs`), `docs/design/capturas/etapa-5/manifest.json` y `docs/design/capturas/etapa-5/tableros/`. `docs/design/capturas/etapa-5/propuesta.md` (archivada) no se toca.

- [ ] El script nace con los casos de toda la etapa (la lista de T24) y, por ahora, captura solo los tableros, a 1440 × 900 y a 390 × 844, con cada opción de cada Tweak. Los datos de cada caso son los del tablero (los mismos nombres, fechas y cantidades), como hace la etapa 6.
- [ ] Le mostrás al usuario las PNG de `tableros/` (o el recorrido del lienzo), con qué estado es cada una y los puntos de «Para decidir» que tocan cada pantalla.
- [ ] **Su visto bueno, antes de programar.** Si pide un cambio, frenás: lo dibuja Diseño.

Commit `chore(capturas): los tableros de Plataforma, antes de programar`.

### T19 · Contrato y «Recuperar mi cuenta»

**Archivos:**
- `src/test/platform-contract.test.ts` (se amplía);
- `src/areas/public/auth/pages/RecoverAccountPage.tsx` y su test; `src/areas/public/auth/api/recovery.ts`; `src/areas/public/auth/errors.ts` (si traduce algo nuevo);
- `src/areas/public/auth/pages/LoginPage.tsx` y su test (el enlace);
- `src/app/routes.tsx` (`/recuperar`, pública, sin `AccessRoute`);
- `src/locales/{es,en}/auth.json` (`recovery.*`).

**Copiá de:** el tablero `Recuperar` y `M-Recuperar` (§10); `LoginPage.tsx`, `AuthCodeStep.tsx` (el paso del código: «Usar otro correo», casilleros, «Verificar», «Reenviar código» con su cuenta regresiva), `AuthStatus.tsx` (el paso 3), `AuthMessage.tsx` y `AuthLink.tsx`; `FormField variant="auth"`, `EmailField`, `PhoneField` + `CountrySelect`, `SegmentedControl`; `useIdempotentMutation` con `meta: { handlesError: true }`.

- [ ] **Rojo:**
  - `platform-contract.test.ts`: cada ruta, método, cuerpo y respuesta de «El contrato» existe en `openapi.json` y en `schema.d.ts` (como `organization-audit-contract.test.ts`); las acciones sobre una cuenta mandan solo `reason` (T18b) y la cuenta no trae `version` (T18c); el reintento del alta, solo `version`; las 11 acciones de T18c responden `VersionResponse`. Cada tarea siguiente le suma su `api/*.ts`: que llama a esas rutas con esos cuerpos;
  - `RecoverAccountPage.test.tsx`:
    - «el paso 1 pide el correo de antes; con un correo mal escrito dice "Ingresá un correo electrónico válido." y no avanza»;
    - «el paso 2 no acepta el mismo correo que el de antes»;
    - «Enviar código manda el correo nuevo y un nonce; Verificar manda el código con el mismo nonce; después se registra el pedido con el método de antes y el mismo nonce»;
    - «las tres llamadas llevan Idempotency-Key y un reintento por red repite la misma»;
    - «el código incorrecto va debajo de los casilleros, con el texto del back, y un dígito nuevo lo saca»;
    - «"Reenviar código" espera la cuenta regresiva»;
    - «el paso 3 dice "Recibimos tu pedido" y "Volver a ingresar" lleva a /login»;
    - «la respuesta es la misma exista o no la cuenta» (sin ramas por la respuesta);
    - «sin el canal de WhatsApp, no hay selector "Correo | WhatsApp"» (según «Para decidir» 8);
    - axe y 390.
  - `LoginPage.test.tsx`: «las dos puertas muestran "¿No podés entrar? Recuperá tu cuenta", que lleva a /recuperar».
- [ ] **Implementación:**
  - el nonce: 32 bytes de `crypto.getRandomValues` en base64url, uno por recorrido, solo en memoria;
  - los pasos viven en el estado de la pantalla; el código, con `AuthCodeStep` (la excepción de `formularios.md`);
  - el teléfono de antes, con `PhoneField`, se manda como `previousPhone`;
  - el número de WhatsApp nuevo no se ofrece mientras el back acepte solo correo (decisión 20 del plan de la E5).
- [ ] **Verde:** los focales, lint, build y `contracts:check`.
- [ ] **Capturas** (grupo `recuperar`): los tres pasos y el código, a 1440 y a 390, y el enlace en Ingreso. Vista en vivo: `vivo.mjs etapa-5/recuperar-paso-1-escritorio`.

Commit `feat(recuperacion): conectar «Recuperar mi cuenta»`.

**Hecho cuando** `/recuperar` coincide con `Recuperar` y `M-Recuperar` en sus pasos, registra un pedido real y el ingreso tiene su enlace.

### T20 · El marco de Plataforma

#### T20a · Back · Los permisos de Plataforma en `/api/me`

**Archivos:** `Application/Models/Profile/EffectivePermissions.cs`, `Application/Services/Profile/ProfileSnapshotBuilder.cs` (y sus dependencias, sin pasar el tope de ADR 0039), sus tests, `docs/contracts/openapi.json`, `docs/features/plataforma.md` y `docs/features/identidad.md` si la nombran.

- [ ] **Rojo:** «un operador con `platform.tenants.read` y `platform.audit.read` recibe exactamente esos dos en `effectivePermissions.platform`»; «una persona y una empresa reciben la lista vacía»; «un operador suspendido o sin asignación, vacía».
- [ ] **Implementación:** `EffectivePermissions` suma `Platform` (`IReadOnlyList<string>`), que se llena solo con `access=platform`, desde la asignación persistida (`PlatformPermissionService`), sin caché. `Permissions` (la lista plana) no cambia.
- [ ] **Verde:** focal, integración de `/api/me`, build sin advertencias. Front: `npm run contracts` y `contracts:check`.

Commit back `feat(plataforma): los permisos del operador en /api/me`; front `chore(contratos): los permisos de Plataforma en /api/me`.

#### T20b · Front · Rutas, permisos y el marco de la compu

**Archivos:**
- `src/auth/usePermissions.ts` y su test: `platformPermissions` (las nueve claves) y `PlatformPermission`; `canPlatform(permission)`;
- `src/layouts/navigation/types.ts`: `AppRoute.platformPermission`; un panel de arriba («Gestión») además de `administration`;
- `src/layouts/navigation/platform.ts`: `platformRoutes` y `platformNavigation` (sale el comentario «El backoffice comienza en E9»);
- `src/layouts/navigation/{visible,administration}.ts` y sus tests: los dos paneles;
- `src/app/RequirePermission.tsx`, `src/app/routes.tsx` (`platformPages`, como `businessPages`) y sus tests;
- `src/layouts/components/{Sidebar,AdminPanel,Breadcrumbs}.tsx`, `src/layouts/PrivateLayoutFrame.tsx` (`scope="platform"`), `src/layouts/PlatformLayout.tsx` y sus tests;
- `src/tenancy/AccessMenu.tsx` y su test (Plataforma);
- `src/test/mocks/currentUsers.ts`: `platformOperator` con los nueve permisos y `platform-operator-reader` (solo `tenants.read` y `accounts.read`); `visual-operator` (Martín Sosa, `martin.sosa@arquitecturabase.ejemplo.com`, los nueve permisos);
- `src/locales/{es,en}/common.json` (`navigation.*`, `layout.*`).

**Copiá de:** el marco de los ocho tableros de escritorio (§1); `navigation/business.ts`, `visible.ts`, `Sidebar.tsx`, `AdminPanel.tsx`, `AccessMenu.tsx`.

- [ ] **Rojo:**
  - `routes.test.tsx`: cada ruta de Plataforma pide su permiso; `/plataforma/legales/nueva` pide `legal.manage`;
  - `visible.test.ts`: cada enlace de los dos paneles es una ruta del manifiesto y se ve solo con su permiso; un panel sin enlaces no se muestra;
  - `RequirePermission.test.tsx`: sin el permiso de Plataforma, «No tenés permiso»;
  - `Sidebar.test.tsx` y `AdminPanel.test.tsx`: «Gestión» arriba y «Administración» al pie; un solo panel abierto; arranca abierto el de la pantalla y su opción marcada; «Cerrar Gestión» y «Cerrar Administración»; sin «WhatsApp»;
  - `AccessMenu.test.tsx`: en Plataforma, debajo del nombre «Plataforma»; sin «Perfiles»; «Salir» (y «Mi cuenta» según «Para decidir» 7);
  - `Breadcrumbs.test.tsx`: «Plataforma / Organizaciones» y «Plataforma / Organizaciones / {nombre}»;
  - el inicio de un operador sin `tenants.read` (según «Para decidir» 5);
  - axe.
- [ ] **Implementación:**
  - el manifiesto (`frontend.md` §3): `/plataforma` (Organizaciones, el inicio, `tenants.read`), `/plataforma/organizaciones/:id` (`tenants.read`), `/plataforma/cuentas` y `/plataforma/cuentas/:id` (`accounts.read`), `/plataforma/recuperaciones` (`recoveries.manage`), `/plataforma/legales` y `/plataforma/legales/nueva` (`legal.manage`), `/plataforma/auditoria` (`audit.read`), `/plataforma/configuracion` (`settings.manage`);
  - «Gestión»: Organizaciones, Cuentas, Recuperaciones; «Administración»: Auditoría, Documentos legales, Configuración;
  - el contexto del menú lateral: «Plataforma» y «Administración del servicio»;
  - las pantallas que todavía no existen se suman a `platformPages` en su tarea; hasta entonces su ruta no se monta.
- [ ] **Verde:** focales, `navigation.test.tsx`, lint y build.
- [ ] **Capturas** (grupo `plataforma-marco`): el marco con cada panel abierto, a 1440.

Commit `feat(plataforma): el marco, la navegación y los permisos del operador`.

#### T20c · Front · El marco del teléfono

**Archivos:** `src/layouts/PrivateLayoutFrame.tsx` (la barra de abajo que declara el manifiesto), `src/layouts/MorePage.tsx` (Plataforma), `src/layouts/navigation/{types,platform}.ts` y sus tests.

**Copiá de:** la barra de abajo de los `M-*` de Plataforma y `M-Mas-Plat` (§1); `MorePage.tsx`; `BottomNav`, `AppBar`, `SettingsGroup`.

- [ ] **Rojo:**
  - `PrivateLayoutFrame.test.tsx`: en Plataforma, la barra de abajo trae «Organizaciones», «Cuentas», «Recuperaciones» (cada una solo con su permiso) y «Más»; la sección actual va marcada; sin «Consultas»;
  - `MorePage.test.tsx`: «Más» de Plataforma: debajo del título «Plataforma»; quién sos; «Administración» con lo que tus permisos dejan ver; «Mi cuenta» y «Salir»; sin «Perfiles» ni «WhatsApp»;
  - F5 en `/plataforma/cuentas` conserva la sesión de Plataforma.
- [ ] **Implementación:** el manifiesto declara las secciones de la barra de abajo; `PrivateLayoutFrame` las arma con `BottomNav` (las de Personal y Empresa siguen con «Inicio» y «Más»). Las pantallas abiertas desde «Más» llevan ‹ «Volver» y «Administración» debajo del título.
- [ ] **Verde y capturas** (grupo `plataforma-marco`): la barra de abajo y «Más», a 390.

Commit `feat(plataforma): la barra de abajo y «Más» en el teléfono`.

#### T20d · Front · Las piezas de Plataforma

**Archivos:**
- `src/shared/platform/ReasonDialog.tsx`, su test, `AGENTS.md` y `CLAUDE.md` (como `shared/account/`);
- `src/shared/hooks/useDialogFailure.ts` y su test (sale de `areas/business/companies/components/`, con la raíz de la consulta como parámetro; Empresas pasa `companiesQueryRoot`). Además del 409 de concurrencia, acepta los códigos que dicen que lo que se ve ya cambió; Plataforma pasa `Tenancy.Tenant.InvalidTransition`, `Users.User.InvalidTransition`, `Legal.AccountDeletion.AlreadyPending` y `Platform.AccountRecovery.AlreadyResolved` (decisión 18 de este plan);
- `src/shared/ui/DetailGrid.tsx` y su test (sale de `areas/business/audit/pages/AuditPage.tsx`: la grilla del detalle en la compu y `AuditPhoneDetail` en el teléfono);
- `src/tenancy/publicSite.ts` y su test: `publicSiteHost(slug)` (sale `PUBLIC_DOMAIN` de `PublicPagePage.tsx`; la Etapa 7 la completa desde el issuer);
- `src/shared/ui/icons.tsx`: los íconos que faltan, copiando el trazo del tablero: grilla (Gestión, `Organizaciones.dc.html`), documento (Documentos legales), calendario («Extender prueba», `Organizacion.dc.html`) y los tres de la barra de abajo del teléfono (`M-Organizaciones.dc.html`: Organizaciones, Cuentas, Recuperaciones).

**Copiá de:** §11; `ConfirmDialog.tsx` y `dialog.tsx` (`size="sm"`), `FormField`, `textarea`, `DateField`, `FormError`, `Button` con `pending`; `useDialogFailure.ts`; `AuditPage.tsx`.

- [ ] **Rojo** (`ReasonDialog.test.tsx`):
  - «sin motivo, "Escribí el motivo." debajo del campo y no manda nada»;
  - «con fecha ("Extender prueba"), la fecha mal escrita dice "Escribí la fecha como dd/mm/aaaa."»;
  - «la acción de peligro es roja y la otra, principal; sin ✕; Escape y "Cancelar" cierran»;
  - «mientras espera, el botón dice lo que hace y no se toca»;
  - «un bloqueo conocido muestra la franja roja, sin motivo, con la acción apagada»;
  - «lo que el back rechaza va en la franja, arriba de los botones; un 409 de concurrencia o de estado relee la pantalla al cerrar»;
  - «la acción devuelve la versión que respondió el back, para que la pantalla la tome como base»;
  - «en el teléfono es una hoja con manija»;
  - axe.
- [ ] `useDialogFailure.test.ts`, `DetailGrid.test.tsx` y `publicSite.test.ts`; `CompanyDetailPage.test.tsx`, `AuditPage.test.tsx` y `PublicPagePage.test.tsx` siguen verdes sin cambiar lo que se ve.
- [ ] **Antes de tocar** `companies/`, `audit/`, `public-page/` o `icons.tsx`, mirá `git status`: si tienen cambios de otra conversación, frená y avisale al usuario.

Commit `feat(plataforma): las piezas comunes de las pantallas de Plataforma`.

**Hecho cuando** (T20) el operador entra a `/plataforma`, ve «Gestión» y «Administración» (o la barra de abajo y «Más») con lo que sus permisos le dejan, y las piezas de las pantallas existen.

### T21 · Organizaciones

#### T21a · Back · La ficha y su historial

Es la parte de back de T21 del [plan de la E5](2026-10-03-etapa-5-plataforma.md#t21--organizaciones-listado-y-ficha-real), sin cambios: `GET /api/platform/tenants/{id}` con `owners` y `lastStateChange`, y `GET /api/platform/tenants/{id}/history` por cursor, con sus tests (dos Dueños y registrante retirado, identidad eliminada o sin nombre, historial de otra organización excluido, último estado no reemplazado por cambios de módulos ni por la entrada técnica, `null` sin evidencia), el inventario de rutas y el contrato.

Suma lo que se decidió en «Para decidir»:
- **4:** el último ingreso de cada Dueño en `owners` y `publishedAtUtc` de la página pública. Se guarda al publicar, con su migración; si no hay dato, `null`.
- **11:** aprobar una organización avisa por correo a sus Dueños activos. Copiá el aviso de despublicar: el correo en es y en, por el outbox, y su test de integración.
- **14:** el `PUT` de un módulo recibe `expiresOn` (`DateOnly`) en lugar de `expiresAtUtc`. Vale hasta el final de ese día en la zona de la organización, como «Vigente desde» de los legales, y la lectura devuelve esa fecha.

- [ ] Rojo, implementación y verde como dice ese plan, más un test por cada punto de arriba. Front: `npm run contracts` y `contracts:check`.

Commit back `feat(plataforma): la ficha de una organización con Dueños, último estado e historial`.

#### T21b · Organizaciones: la lista y «Crear organización»

**Archivos:** `src/areas/platform/tenants/{api/tenants.ts, columns.tsx, errors.ts, components/CreateOrganizationDialog.tsx, pages/OrganizationsPage.tsx}` y sus tests; `AGENTS.md` y `CLAUDE.md` de `tenants/`; `src/locales/{es,en}/platformTenants.json`; `enums.json`.

**Copiá de:** §2 y §11; `UsersPage.tsx`, `users/columns.tsx`, `InviteUserDialog.tsx`; `ReasonDialog`.

- [ ] **Rojo** (`OrganizationsPage.test.tsx`):
  - «la banda: "{n} organizaciones · {m} esperan aprobación" y "+ Crear organización"»;
  - «los filtros Estado y Origen con las cantidades de filter-counts; la búsqueda en la URL, con 300 ms»;
  - «las columnas con su segunda línea; el Dueño sin nombre dice "—" y el correo debajo»;
  - «el ⋮ de cada estado, con las de peligro al final»;
  - «Aprobar, Rechazar, Suspender, Reactivar y Cerrar piden el motivo y mandan versión y motivo; el aviso al terminar»;
  - «la versión que responde una acción es la base de la siguiente sobre esa organización, aunque la relectura de la lista no haya llegado»;
  - «Reintentar el alta manda solo la versión, sin diálogo»;
  - «un 409 en la franja del diálogo; al cerrarlo, la lista se relee»;
  - «Crear organización: los cinco campos, los errores del tablero, la misma Idempotency-Key al reintentar y el aviso»;
  - «sin tenants.manage, sin "Crear organización" ni acciones»;
  - «1 y 50 filas»; «sin coincidencias»; «error con Reintentar»; es-AR y en-US; axe; 390 (filas, «Buscar», «Filtros», «+», «Cargar más»).
- [ ] **Implementación:** `usePagedQuery` (orden por defecto `name`), conteos de `filter-counts`, la zona y el idioma por defecto según la decisión 14 de este plan.
- [ ] **Verde** y **capturas** (grupo `plataforma-organizaciones`): la lista con 7 filas y con 50, sin coincidencias, un menú de cada estado, «Crear organización» vacío y con errores, una confirmación con motivo y su 409, y el teléfono.

Commit `feat(plataforma): la lista de organizaciones`.

#### T21c · Ficha de una organización: banda, Historial y Dueños

**Archivos:** `src/areas/platform/tenants/{pages/OrganizationPage.tsx, tabs/OrganizationHistoryTab.tsx, tabs/OrganizationOwnersTab.tsx, tabs/useOrganizationHistory.ts}` y sus tests.

**Copiá de:** §3; `CompanyDetailPage.tsx`, `CompanyMembersTab.tsx`; `useCursorList` y `LoadMore` (el historial).

- [ ] **Rojo** (`OrganizationPage.test.tsx`):
  - «la banda con el estado, el resumen y las acciones de cada estado (Espera aprobación, Activa, Suspendida, Cerrada)»;
  - «el cartel de Suspendida y de Cerrada con la fecha y el motivo de lastStateChange»;
  - «después de Suspender desde la banda, Reactivar manda la versión que respondió la suspensión, aunque la relectura de la ficha no haya llegado; una relectura con otra versión no la pisa»;
  - «Historial por cursor, con "Cargar más"»; «Dueños con su correo, último ingreso y estado»;
  - «las pestañas con la cantidad que tiene dato» (según «Para decidir» 3);
  - «404: "No encontramos esta página"»; «si no carga: "No pudimos abrir esta pantalla" y "Reintentar"»;
  - «en el teléfono: la barra con el estado y el resumen, el ⋮ con las acciones, las pestañas deslizables y el historial en filas»; axe.
- [ ] **Verde** y **capturas** (grupo `plataforma-organizacion`): cada estado del Tweak «estado» con Historial y Dueños, y el teléfono.

Commit `feat(plataforma): la ficha de una organización`.

#### T21d · Ficha de una organización: Módulos, Dominio verificado y Página pública

**Archivos:** `src/areas/platform/tenants/tabs/{OrganizationModulesTab,OrganizationDomainTab,OrganizationPublicPageTab}.tsx` y sus tests.

**Copiá de:** §3 y §11; `CompanyMembersTab.tsx`; `ReasonDialog`; `publicSiteHost`.

- [ ] **Rojo:**
  - Módulos: «estado y origen de cada módulo; Prender, Extender prueba (con "Hasta") y Apagar mandan lo suyo con motivo y versión (0 sin fila)»; «sin módulos, según "Para decidir" 2»; los gates con un catálogo de prueba (`TestFeatures` en MSW), nunca datos productivos;
  - Dominio verificado: «Pendiente con su registro TXT, "Verificar" directa y "Quitar" con motivo»; «"No encontramos el registro TXT…" en un aviso rojo»; «sin dominios, "Sin dominio de correo"»;
  - Página pública: «la dirección, el estado y el motivo; Despublicar y Permitir publicar con motivo»; «"Ver página" no aparece hasta la Etapa 7» (decisión 8 de este plan);
  - «la versión que responde Prender, Extender, Apagar, Verificar, Despublicar o Permitir publicar es la base de la acción siguiente sobre ese módulo, dominio o página»;
  - «Dominio verificado y Página pública se piden recién al abrir su pestaña y no se muestran sin tenants.manage» (decisión 6);
  - «una organización cerrada no tiene ⋮ en ninguna pestaña»; axe; 390.
- [ ] **Verde** y **capturas** (grupo `plataforma-organizacion`): los Tweaks «página» y «dominio», Módulos, y cada confirmación.

Commit `feat(plataforma): módulos, dominio y página pública de una organización`.

**Hecho cuando** (T21) la lista y la ficha coinciden con `Organizaciones`, `Organizacion`, `M-Organizaciones` y `M-Organizacion` en todos sus estados, con datos reales.

### T22 · Cuentas y operadores

#### T22a · Cuentas: la lista e «Invitar operador»

**Archivos:** `src/areas/platform/accounts/{api/accounts.ts, api/operators.ts, columns.tsx, errors.ts, components/InviteOperatorDialog.tsx, pages/AccountsPage.tsx}` y sus tests; `AGENTS.md` y `CLAUDE.md` de `accounts/`; `src/locales/{es,en}/platformAccounts.json`.

**Copiá de:** §4 y §11; `UsersPage.tsx` (reenviar y revocar una invitación), `users/columns.tsx`, `InviteUserDialog.tsx`; `MultiSelect` con la explicación de cada opción; `DateText kind="relative"`.

- [ ] **Primero, el back** («Para decidir» 4). La ficha de la cuenta (`GET /api/platform/accounts/{id}`) trae:
  - `lastStateChange`, con la fecha y el motivo, como R03 de las organizaciones;
  - `deletedAtUtc`, que la identidad ya guarda.

  Lleva rojo y verde de integración, contrato y `npm run contracts`. Es un commit back propio: `feat(plataforma): la ficha de una cuenta con su último estado y su baja`.
- [ ] **Rojo** (`AccountsPage.test.tsx`):
  - «la banda, los filtros Tipo y Estado con sus cantidades y la búsqueda»;
  - «la fila de una persona, de un operador, de una invitación ("Nunca", "Vence el …"), de una cuenta sin nombre y de una eliminada ("—", sin ⋮)»;
  - «el ⋮ de cada estado» (según «Para decidir» 9 por las diferencias con la ficha); «sin ⋮ en tu propia cuenta» (según «Para decidir» 5);
  - «Invitar operador: correo y permisos; los dos 409 del back debajo del correo; "Elegí al menos un permiso de plataforma." debajo de los permisos; la misma Idempotency-Key al reintentar; el aviso»;
  - «Reenviar invitación, directa, y Revocar invitación, con confirmación y sin motivo»;
  - «Suspender, Reactivar y Cerrar sus sesiones mandan solo el motivo, sin versión»; «si otro operador ya la cambió, "No se puede cambiar la cuenta a ese estado." va en la franja del diálogo y, al cerrarlo, la lista se relee»;
  - «Dar de baja lee la cuenta al abrirse: con soleOwnerOrganizations, la franja del único Dueño y el botón apagado; si no, el motivo; el aviso con la fecha de la relectura»;
  - «LastManager en la franja del diálogo»;
  - «sin operators.manage, sin "Invitar operador" ni acciones de invitación; sin accounts.manage, sin las demás»;
  - «1 y 50 filas»; es-AR y en-US; axe; 390.
- [ ] **Verde** y **capturas** (grupo `plataforma-cuentas`): la lista, cada menú, «Invitar operador» vacío, con permisos y con cada error, «Dar de baja» con y sin bloqueo, y el teléfono.

Commit `feat(plataforma): cuentas y la invitación de operadores`.

#### T22b · Ficha de una cuenta

**Archivos:** `src/areas/platform/accounts/{pages/AccountPage.tsx, tabs/AccountMethodsTab.tsx, tabs/AccountOrganizationsTab.tsx, tabs/AccountHistoryTab.tsx}` y sus tests.

**Copiá de:** §5 y §11; `CompanyDetailPage.tsx`; `List` (métodos); `useCursorList` (historial).

- [ ] **Rojo** (`AccountPage.test.tsx`):
  - «la banda y las acciones de cada estado del Tweak (Activa, Suspendida, Necesita recuperación, Baja pedida, Baja iniciada por la plataforma, Eliminada)»;
  - «los carteles de cada estado» (según «Para decidir» 4 por la suspensión y la eliminación); «"Ver recuperaciones" lleva a /plataforma/recuperaciones»;
  - «Métodos: valor tapado, tipo, Principal, Administrado por y "Ya no sirve para ingresar"; vacío»;
  - «Organizaciones con rol, desde y estado de la membresía; enlace a la ficha; vacía»;
  - «Historial de seguridad por cursor; no se muestra sin audit.read»;
  - «404 y error de carga»; axe; 390 (barra, ⋮, pestañas y filas de Métodos como `M-Cuenta-Plat`).
- [ ] **La variante de un operador,** como la dibuja la v111 («Para decidir» 1):
  - la pestaña «Permisos»;
  - «Cambiar permisos»: `PUT .../operator`, con `operatorVersion`, el motivo y la versión de la respuesta como base;
  - «Quitar operador»: `DELETE .../operator`, con el 409 `LastManager` en la franja;
  - la ficha de una invitación de operador.

  Sus tests van en `AccountPage.test.tsx`.
- [ ] **Verde** y **capturas** (grupo `plataforma-cuenta`): cada estado y cada pestaña, y el teléfono.

Commit `feat(plataforma): la ficha de una cuenta`.

**Hecho cuando** (T22) Cuentas y su ficha coinciden con `Cuentas`, `Cuenta-Plat`, `M-Cuentas` y `M-Cuenta-Plat`, con datos reales, y un operador puede invitar a otro.

### T23 · Recuperaciones, Documentos legales, Auditoría y Configuración

Cada parte va en su commit.

#### T23a · Recuperaciones

**Archivos:** `src/areas/platform/recoveries/{api/recoveries.ts, columns.tsx, errors.ts, components/ReviewRecoveryDialog.tsx, pages/RecoveriesPage.tsx}` y sus tests; `AGENTS.md` y `CLAUDE.md`; `src/locales/{es,en}/platformRecoveries.json`.

**Copiá de:** §6; `UsersPage.tsx`; `DetailGrid`; `useDialogFailure`.

- [ ] **Rojo:** «la banda con "{n} pedidos · {m} pendientes" y "Sin pedidos"»; «el filtro Estado con cantidades y la búsqueda»; «la fila con y sin cuenta»; «Revisar: la grilla, el cartel según haya cuenta, el motivo obligatorio; Aprobar apagado sin cuenta; Rechazar y Aprobar mandan versión y motivo con Idempotency-Key, y la versión que responden es la base»; «cada 409 del back en la franja del diálogo»; «Ver detalle de un resuelto, con "Cerrar"»; «sin pedidos: "No hay pedidos de recuperación", sin pie»; «1 y 50 filas»; axe; 390.
- [ ] **Verde** y **capturas** (grupo `plataforma-recuperaciones`): los tres casos del Tweak, un pedido resuelto, los errores y el teléfono.

Commit `feat(plataforma): recuperaciones`.

#### T23b · Documentos legales

**Archivos:** `src/areas/platform/legal/{api/legalDocuments.ts, errors.ts, pages/LegalDocumentsPage.tsx, pages/PublishLegalVersionPage.tsx}` y sus tests; `AGENTS.md` y `CLAUDE.md`; `src/locales/{es,en}/platformLegal.json`.

**Copiá de:** §7; `RoleEditorPage.tsx` (pantalla propia, «Cancelar», «¿Salir sin guardar?»); `List`; `DateField`; `Select`; `useIdempotentMutation`.

- [ ] **Rojo:** «la lista con la versión vigente y su fecha; el resumen»; «el ⋮ abre el editor con ese documento»; «el editor: el cartel, los cuatro campos, un campo de texto por cultura habilitada del catálogo, los errores del tablero, "Cambios sin guardar" y "¿Salir sin guardar?"»; «Publicar confirma y manda kind, effectiveOn y contents con Idempotency-Key; vuelve a la lista con el aviso»; «la fecha anterior a hoy, con el texto del back debajo de "Vigente desde"»; axe; 390 (lista como `M-Legales` y «+»).
- [ ] **Verde** y **capturas** (grupo `plataforma-legales`): los dos estados del Tweak, los errores, la confirmación y el teléfono.

Commit `feat(plataforma): documentos legales`.

#### T23c · Auditoría

**Archivos:** `src/areas/platform/audit/{api/securityEvents.ts, pages/SecurityAuditPage.tsx}` y sus tests; `AGENTS.md` y `CLAUDE.md`; `src/locales/{es,en}/platformAudit.json`.

**Copiá de:** §8; `AuditPage.tsx`; `DetailGrid`.

- [ ] **Rojo:** «los filtros sin cantidades; "Últimos 30 días" por defecto; "Limpiar" solo si algo cambió»; «las columnas; "Qué pasó" abre el detalle»; «"Cargar más" pide la tanda siguiente con el cursor y los mismos filtros»; «el detalle con su grilla y "Cerrar"; en el teléfono, la hoja»; «sin coincidencias»; «403: "No tenés permiso"»; es-AR y en-US (fechas); axe; 390.
- [ ] **Primero, el back** («Para decidir» 4): `GET /api/platform/audit/organizations`, con las organizaciones que aparecen en la auditoría de seguridad, para el filtro «Organización».
  - Copiá `/api/organization/audit/actors`.
  - Lleva `[HasPlatformPermission]` de auditoría, el inventario de rutas, rojo y verde de integración, y el contrato.
  - Es un commit back propio.
- [ ] **Verde** y **capturas** (grupo `plataforma-auditoria`): la lista, un filtro, el detalle y el teléfono con su hoja.

Commit `feat(plataforma): la auditoría de seguridad`.

#### T23d · Configuración

**Archivos:** `src/areas/platform/settings/{api/platformSettings.ts, pages/PlatformSettingsPage.tsx}` y sus tests; `AGENTS.md` y `CLAUDE.md`; `src/locales/{es,en}/platformSettings.json`.

**Copiá de:** §9; `SettingsPage.tsx` (`useEditForm`, `useEditPage`, `Section`, el teléfono con `FixedActions`); `Select` con la explicación de cada opción; `NumberField`; `EmailField`; `ReasonDialog`.

- [ ] **Primero, el back** («Para decidir» 10): «Organizaciones propias por persona» acepta de 1 a 50.
  - El validador va con el texto del tablero, «Usá un número de 1 a 50.», en es y en.
  - Lleva rojo y verde.
  - Es un commit back propio.
- [ ] **Rojo:** «los campos con lo guardado; "Guardar cambios" apagado sin cambios; "Descartar cambios" vuelve a lo guardado»; «los errores del tablero, sin mandar nada»; «Guardar pide el motivo y manda los cinco campos, la versión y el motivo; toma la versión de la respuesta»; «el 409 en el ConcurrencyBanner, con "Ver lo nuevo" y "Seguir editando"»; «"¿Salir sin guardar?"»; axe; 390 (grupos y botones fijos).
- [ ] **Verde** y **capturas** (grupo `plataforma-configuracion`): con y sin cambios, con errores, la confirmación, el 409 y el teléfono.

Commit `feat(plataforma): la configuración de la plataforma`.

**Hecho cuando** (T23) las cuatro pantallas coinciden con sus tableros en la compu y en el teléfono, con datos reales.

### T24 · Documentación y capturas comparadas

**Archivos:**
- back: `docs/features/plataforma.md` (las pantallas, rutas, permisos y estados), `docs/features/identidad.md` (la recuperación pública y `/api/me`), el [plan de la E5](2026-10-03-etapa-5-plataforma.md) (casillas de T19 a T25 con la referencia a este plan) y `docs/reviews/2026-10-05-etapa-5-pantallas.md` (diferencias visuales y su aceptación);
- front: `docs/architecture/arbol.md` (las carpetas reales de `areas/platform/`, `shared/platform/`, `RecoverAccountPage.tsx` y lo que salió de su lugar en T20d; la línea de `PlatformSettingsPage` deja de decir «días de gracia»), las fichas cuyo «Lo verifica» o «Copiá de» cambian (con la marca `(E5)`), los `AGENTS.md`/`CLAUDE.md` nuevos y `docs/design/capturas/etapa-5/`.

- [ ] **Las capturas finales** (`capturar-etapa-5.mjs` y `capturar-relevamiento.mjs --out docs/design/capturas/etapa-5/lado-a-lado`), a 1440 × 900 y 390 × 844, de cada caso:

  | Grupo | Casos |
  |---|---|
  | `plataforma-marco` | Gestión abierta; Administración abierta; tu menú; barra de abajo; Más |
  | `plataforma-organizaciones` | lista (7 y 50 filas); sin coincidencias; ⋮ de cada estado; Crear organización (vacío y con errores); una confirmación con motivo; su 409 |
  | `plataforma-organizacion` | estado × {Activa, Espera aprobación, Suspendida, Cerrada}; pestaña × las cinco; página × las tres; dominio × los dos; cada confirmación |
  | `plataforma-cuentas` | lista; cada ⋮; Invitar operador (vacío, con permisos, con cada error); Dar de baja (con y sin bloqueo) |
  | `plataforma-cuenta` | estado × los seis; pestaña × las tres |
  | `plataforma-recuperaciones` | caso × los tres; resuelto; errores |
  | `plataforma-legales` | pantalla × las dos; errores; confirmación |
  | `plataforma-auditoria` | lista; filtro; detalle (compu y hoja) |
  | `plataforma-configuracion` | sin cambios; con cambios; errores; confirmación; 409 |
  | `recuperar` | paso × los tres; código; enlace en Ingreso |

- [ ] Cada diferencia con el tablero se corrige o queda en la revisión con su motivo y la aceptación del usuario. Los datos, de una base aislada, nunca de Development.
- [ ] `realinear-docs --detectar` en 0.

Commit back `docs(plataforma): las pantallas de la Etapa 5`; front `docs(plataforma): árbol, fichas y capturas de la Etapa 5`.

**Hecho cuando** los docs dicen lo que hace el código y el usuario aceptó las capturas.

### T25 · E2E completo y puerta de cierre

**Archivos:** front `scripts/test-e2e-real.test.mjs` (el paso 5 crece); back `tools/ArquitecturaBaseMultitenant.RealE2ESetup/{Program,PlatformCommand}.cs` (el segundo operador); `HarnessStage.cs` y `src/test/HarnessStage.ts`.

- [ ] **El recorrido real de la E5,** solo con pasos de pantalla (sin GET de API como reemplazo), como pide T25 del plan de la E5 y R06:
  - el operador entra con código, F5 conserva Plataforma y navega «Gestión» y «Administración»;
  - un segundo operador con permisos reducidos ve solo lo suyo y no tiene las acciones que no le tocan;
  - crea una organización con un Dueño sin cuenta, que acepta sin Personal;
  - aprueba una pendiente y reintenta un alta sin duplicados;
  - suspende una organización y su Dueño sigue entrando como persona; la reactiva;
  - los módulos (con el catálogo de prueba del preparador, si existe; si no, queda anotado);
  - despublica una página y la organización no puede publicarla; permite publicar;
  - quita un dominio y los métodos vinculados se conservan;
  - suspende, reactiva y cierra las sesiones de una cuenta; da de baja otra;
  - un pedido de «Recuperar mi cuenta» con un correo nuevo, aprobado por el operador;
  - publica una versión legal y la acepta;
  - mira la auditoría y guarda la configuración.
- [ ] Siguen verdes los recorridos E3a, E3b, E3c, E4 y E6. Sin mocks ni intercepción; PostgreSQL y pickup propios. Lo que no pueda correr localmente queda anotado, sin inventar verde.
- [ ] **La puerta:** `dotnet build ArquitecturaBaseMultitenant.slnx` sin advertencias; `dotnet test` completo con Docker; front `npm test`, `npm run lint`, `npm run build`, `npm run contracts:check` y `npm run test:e2e:real`; `HarnessTests`, el arnés del front, `FeatureGateTests` y `ModuleControllersTests`.
- [ ] **El cierre:** con todo en verde, `5` entra en `ClosedStages` de los dos repos. Informe con commits, salidas, decisiones, diferencias visuales y pendientes reales. `aspire stop` si se levantó; los dos repos limpios en `main`, sin push.

Commit `test(e2e): el recorrido real de Plataforma` y `chore(arnes): cerrar la Etapa 5`.

**Hecho cuando** la puerta está verde y la Etapa 5 cerrada.

---

## Fuera de alcance

- Plataforma › WhatsApp (`Plat-WA-*`, «Consultas», la rama «WhatsApp»): Etapa 11.
- El segundo factor del operador («Ingresó con el autenticador», los códigos de recuperación de `Ingreso`): no existe en la E5 (decisión 24 del plan de la E5).
- El sitio público del subdominio y «Ver página»: Etapa 7.
- El número de WhatsApp como método nuevo de «Recuperar mi cuenta»: Etapa 8.

---

## Decisiones tomadas

### Del plan de la Etapa 5 (las que tocan estas pantallas)

- **1.** El catálogo productivo de módulos está vacío: los gates y la pestaña Módulos se prueban con `TestFeatures` y un catálogo de test, nunca con datos productivos.
- **2, 17, 23 y ADR 0042.** Los operadores tienen permisos individuales (las nueve claves), sin roles. Se invitan por correo, sin Personal; la invitación queda pendiente hasta el primer ingreso y vence a los 7 días («Reenviar» y «Revocar»). Invitar no pide motivo. Un correo que ya es de una cuenta se rechaza.
- **5.** Los inicios de Organización y WhatsApp no se rediseñan en la E5.
- **6.** Configuración exige motivo y versión. La gracia de la baja (30 días) no se edita.
- **8.** «Crear organización» tiene los cinco campos del tablero; la dirección se genera del nombre; sin campo de motivo.
- **9 y R05.** «Reintentar el alta» manda solo la versión, sin motivo escrito; sin responsable verificable, el back responde 409.
- **13.** Permitir publicar retira el bloqueo; publicar lo decide la organización.
- **14.** `{domain}` va normalizado; quitar un dominio manda motivo y versión en el cuerpo.
- **16 y 20.** «Recuperar mi cuenta» no revela si un correo tiene cuenta. El nonce lo genera el navegador. El método de antes puede ser correo o teléfono; el nuevo, solo correo hasta la Etapa 8.
- **18 y 21.** La baja por Plataforma la bloquea ser el único Dueño de una organización no cerrada, aunque la cuenta esté suspendida.
- **19.** Suspender, reactivar y cerrar sesiones no avisan a la persona; la baja sí.
- **22.** Recuperaciones usa `/paged` y `/filter-counts`; aprobar suma el correo como principal; al operador sí se le dice que el correo es de otra cuenta.
- **24.** Auditoría: los filtros del tablero, «Hoy» en la zona del operador, sin totales ni cantidades, correos tapados, los métodos reales (sin «autenticador»), el detalle sin el diff técnico y los roles «Operador», «Automático» y «Persona».
- **25.** Publicar una versión legal no pide motivo; «Vigente desde» es una fecha civil desde hoy; hasta 100.000 caracteres por cultura; publicar una que rige hoy bloquea también al operador hasta aceptarla.
- **26.** Una cuenta de operador no se recupera desde Recuperaciones.
- **T18b** (`6bd4ff7` y `5f4ed39`). Suspender, reactivar, cerrar sesiones y dar de baja una cuenta mandan solo `{ reason }` y responden 204. Las protege el estado: sus 409 son `Users.User.InvalidTransition` y `Legal.AccountDeletion.AlreadyPending`, nunca `General.ConcurrencyConflict`. Recuperaciones y el retiro de un operador conservan su versión, que es la de su propia fila.
- **T20 de la E5.** Cuentas incluye a los operadores; WhatsApp no aparece.

### De la Guía (2026-10-05)

El 2026-10-05 la Guía decidió los puntos 1 y 2 de T18c:
1. **Las 11 acciones `POST` con `version` responden 200 con `VersionResponse`,** como los `PUT` y como publicar y despublicar la Página pública: aprobar, rechazar, suspender, reactivar, cerrar y reintentar el alta de una organización; permitir publicar y despublicar su página; verificar un dominio; aprobar y rechazar un pedido de recuperación. Un `DELETE` sigue con 204. `VersionedContractTests` lo exige para todo `PUT` o `POST` con `version` y `concurrencia.md` lo dice.
2. **`PlatformAccountResponse` y `PlatformAccountDetailResponse` no traen `version`:** ninguna acción sobre una cuenta la usa. Los permisos de un operador siguen con la versión de su asignación, `operatorVersion` de la ficha.

Y en las pantallas (T21 a T23): después de cada acción con versión, la base pasa a ser la `version` que devolvió la respuesta, como en `useEditForm`. Las acciones sobre una cuenta no llevan versión.

### De este plan (técnicas; el usuario las revisa con el plan)

1. **Fuente:** lienzo v111, front `e685df0`. Parte de la v110 (`c604c0b`) y suma lo de «Para decidir». Ningún tablero de Plataforma está «· por refinar».
2. **Permisos en `/api/me`:** `effectivePermissions.platform` (T20a). Sin eso, el front no puede mostrar ni ocultar nada según los permisos del operador. Sigue `accesos-y-permisos.md` (los permisos salen de `effectivePermissions`).
3. **El editor legal es su propia ruta,** `/plataforma/legales/nueva` (con `?documento=Terms|Privacy` desde el ⋮), como `/org/roles/nuevo`.
4. **Namespaces:** `platformTenants`, `platformAccounts`, `platformRecoveries`, `platformLegal`, `platformAudit` y `platformSettings`; los enums en `enums.json`; «Recuperar mi cuenta» en `auth.json`; el diálogo con motivo en `common.json`.
5. **Piezas que pasan a `shared/`** porque las usan dos features (`estructura-y-features.md`): `ReasonDialog` (`shared/platform/`), `useDialogFailure` (`shared/hooks/`), `DetailGrid` (`shared/ui/`) y `publicSiteHost` (`tenancy/publicSite.ts`). Se crean componiendo piezas comunes, sin medidas propias.
6. **Pestañas según el permiso,** como Empresa oculta «Roles»: «Dominio verificado» y «Página pública» piden `tenants.manage` y se piden recién al abrir la pestaña (leerlas registra una entrada a la organización); «Historial de seguridad» pide `audit.read`.
7. **Íconos:** los del tablero. Los que faltan (grilla, documento, calendario y los tres de la barra de abajo) se suman a `icons.tsx` con el trazo del tablero.
8. **«Ver página»** no aparece hasta la Etapa 7, como los controles que llevan a rutas de etapas posteriores (plan de la 3a): todavía no hay sitio público.
9. **«Dar de baja»** lee la cuenta al abrirse para mostrar el bloqueo del único Dueño antes de confirmar, como el tablero; el aviso toma «Se elimina el {fecha}» de la relectura (el front no calcula la gracia).
10. **Botones que esperan** dicen lo que hacen («Criterio · Botones»): «Creando…», «Aprobando…», «Rechazando…», «Suspendiendo…», «Reactivando…», «Cerrando…», «Prendiendo…», «Extendiendo…», «Apagando…», «Quitando…», «Despublicando…», «Permitiendo…», «Cerrando sesiones…», «Dando de baja…», «Revocando…», «Invitando…», «Publicando…» y «Guardando…».
11. **La fila nueva resaltada** de los tableros no se programa, como en Usuarios, Empresas y Roles.
12. **Las filas del teléfono que no están dibujadas** (las demás pestañas de las fichas) las arma `DataTable` con el rol de cada columna (`responsive.md`); se muestran en las capturas para que el usuario las revise.
13. **El nombre de un módulo** sale de `platformTenants:modules.<clave>`; sin texto, la clave. Cada módulo que nazca suma el suyo.
14. **«Crear organización»** arranca con la zona y el idioma del operador (`/api/me`).
15. **El diálogo con motivo no tiene ✕,** como en el tablero, también cuando no es de peligro.
16. **Idempotencia:** `useIdempotentMutation` en las rutas `[Idempotent]` (ver «El contrato»).
17. **«Verificar» un dominio** manda el motivo fijo «Verificar el dominio» (el back lo exige y el tablero no lo pide), como el reintento del alta.
18. **Un 409 de estado también relee la pantalla** al cerrar el diálogo, como el de concurrencia: sin versión en las cuentas (T18b), «No se puede cambiar la cuenta a ese estado.» es lo que avisa que otro operador cambió la cuenta. Lo resuelve `useDialogFailure` en `shared/` con los códigos que le pasa cada pantalla.

---

## Para decidir (resuelto el 2026-10-05)

Lo que el lienzo o los docs no resolvían. Cada punto empieza con lo **decidido**; lo que sigue es el porqué. Si la decisión y la recomendación no coinciden, manda lo decidido. Los puntos 1, 2, 6, 7 y 8 los dibuja Diseño en la v111 y se programan con esa versión.

1. **La ficha de un operador.**
   - **Decidido (el usuario):** Diseño la dibuja ahora, en la v111, en `Cuenta-Plat` y `M-Cuenta-Plat`:
     - la pestaña «Permisos»;
     - «Cambiar permisos»;
     - «Quitar operador»;
     - la ficha de una invitación de operador.
   - T22b la programa con la v111.

   `Cuenta-Plat` dibuja solo personas. El back ya tiene `operatorPermissions`, `PUT` y `DELETE /api/platform/accounts/{id}/operator` y el 409 `LastManager`, y Cuentas lista operadores con enlace a su ficha (también las invitaciones). **Recomiendo** que Diseño dibuje la variante de operador: la pestaña «Permisos», «Cambiar permisos» (el desplegable de «Invitar operador» y el motivo) y «Quitar operador» en el ⋮ (rojo, con motivo y el 409 en la franja), y la ficha de una invitación. Hasta entonces, T22b no programa cambiar ni quitar permisos.
2. **Estados que la pantalla necesita y el tablero no dibuja:**
   - **Decidido (el usuario):** Diseño los dibuja ahora, en la v111:
     - «Falló el alta» en `Organizacion`;
     - Dueños vacío y Dueños con un Dueño suspendido;
     - la versión legal que todavía no rige, en `Legales`;
     - y sus `M-*`.
   - **Decidido (la Guía):** la pestaña Módulos sin módulos se oculta, con `hidden` de `Page`.

   Lo que se había analizado:
   - la ficha de una organización «Falló el alta» (`Provisioning`): **recomiendo** el estado en la banda y «Reintentar el alta» como acción de la banda, como en el ⋮ de la lista;
   - Dueños sin nadie todavía (una organización creada desde Plataforma no tiene Dueño hasta que acepta) o con un Dueño suspendido: **recomiendo** que Diseño dibuje el vacío, con el correo invitado que ya muestra la lista;
   - Módulos sin módulos (en la E5 el catálogo productivo está vacío, así que en producción siempre): **recomiendo** ocultar la pestaña cuando no hay módulos;
   - Documentos legales con una versión publicada que todavía no rige: **recomiendo** que Diseño dibuje la fila.
3. **Las cantidades de las pestañas.**
   - **Decidido (la Guía):** como se recomienda abajo, con «Cargar más» al pie de los historiales, como Auditoría.

   El tablero las muestra en todas, pero «Historial» (de la organización y de la cuenta) es por cursor, sin total, y «Dominio verificado» y «Página pública» registran una entrada a la organización cada vez que se leen. **Recomiendo** sin cantidad en esas tres; con cantidad en Dueños, Módulos, Métodos de ingreso y Organizaciones. Y «Cargar más» al pie de los historiales, que el tablero no dibuja.
4. **Datos que el tablero muestra y el contrato no trae:**
   - **Decidido (la Guía):** el back suma lo que el tablero muestra, **también «Publicada el»**: la columna no se saca. Va así:
     - **en T21a:**
       - `publishedAtUtc` de la página pública, que se guarda al publicar (con su migración; una página publicada antes muestra «—»);
       - el último ingreso de cada Dueño, en `owners`;
     - **en una parte de back al empezar T22a:** `lastStateChange` (fecha y motivo) y `deletedAtUtc` en la ficha de la cuenta;
     - **en una parte de back al empezar T23c:** `GET /api/platform/audit/organizations`, como `/api/organization/audit/actors`.

   Lo que se había analizado:
   - «Publicada el» de la página pública (la página no guarda esa fecha): **recomiendo** sacar la columna;
   - «Último ingreso» de cada Dueño: **recomiendo** sumarlo a `owners` en T21a, con la misma fuente que Cuentas;
   - «Suspendida el {fecha}. Motivo: {motivo}» de una cuenta: **recomiendo** `lastStateChange` en la ficha de la cuenta, como R03 de las organizaciones (una parte de back al empezar T22, como T21a);
   - «Eliminada el {fecha}»: **recomiendo** `deletedAtUtc` en la ficha (la identidad ya lo guarda), en esa misma parte de back;
   - las organizaciones del filtro «Organización» de Auditoría: **recomiendo** `GET /api/platform/audit/organizations`, como `/api/organization/audit/actors` de la auditoría de una organización (con `tenants/paged` haría falta `tenants.read` y traería todas).
5. **Lo que el operador no puede hacer:**
   - **Decidido (la Guía):** las dos cosas, como se recomienda abajo.
   - el inicio de un operador sin `tenants.read` (`/plataforma` es Organizaciones): **recomiendo** llevarlo a la primera pantalla del menú que puede abrir; sin ninguna, «No tenés permiso»;
   - su propia cuenta (el back responde 403 `Users.User.CannotModifySelf`, y el tablero muestra el ⋮ en la fila de Martín Sosa): **recomiendo** sin ⋮ ni acciones sobre la propia cuenta, en la lista y en la ficha.
6. **Los tonos del teléfono**
   - **Decidido (la Guía):** un tono por estado, el de la compu (§12). Diseño alinea los `M-*` en la v111.

   No coinciden con los de la compu: «Espera aprobación» y «Pendiente» van ámbar en el teléfono y del tono pendiente (petróleo) en la compu; «Suspendida» va roja en el teléfono y ámbar en la compu; «Necesita recuperación», al revés. `StatusBadge` usa un tono por estado. **Recomiendo** los de la compu (§12) y que Diseño alinee los `M-*`.
7. **«Mi cuenta» en Plataforma.**
   - **Decidido (la Guía):** va en la compu y en el teléfono, como dice «Criterio · Decisiones».
   - Diseño lo suma al menú de la compu en la v111.

   Tu menú de la compu tiene solo «Salir»; `M-Mas-Plat` y «Criterio · Decisiones» tienen «Mi cuenta». El back acepta `/api/me/*` con `access=platform`. **Recomiendo** «Mi cuenta» en los dos y que Diseño lo sume al menú de la compu.
8. **WhatsApp en «Recuperar mi cuenta».**
   - **Decidido (la Guía), como se recomienda abajo.** Diseño dibuja la variante sin WhatsApp en `Recuperar` y `M-Recuperar` de la v111.

   El paso 1 ofrece «Correo | WhatsApp» y el paso 2 también, pero el back acepta el método nuevo solo como correo hasta la Etapa 8 (decisión 20) y el ingreso todavía no ofrece WhatsApp. **Recomiendo:** en el paso 1, el selector solo con el canal de WhatsApp prendido (`useHasLoginChannel("whatsapp")`), como el ingreso; en el paso 2, solo el correo, y que Diseño dibuje esa variante con la bajada «Decinos un correo nuevo donde podamos escribirte.».
9. **La lista y la ficha de Cuentas no dicen lo mismo:**
   - **Decidido (la Guía):** las dos cosas, como se recomienda abajo.
   - los textos de Suspender, Reactivar y Cerrar sesiones (§11): **recomiendo** los de la ficha en los dos lugares;
   - en la lista, «Suspender» solo con la cuenta Activa y «Cerrar sus sesiones» también con Suspendida o Necesita recuperación; en la ficha, «Suspender» también con Necesita recuperación y «Cerrar sesiones» solo con Activa: **recomiendo** la misma regla en los dos (Suspender: Activa o Necesita recuperación, que el back permite; Cerrar sesiones: toda cuenta que no tiene la baja pedida, no está eliminada ni es una invitación).
10. **El tope de «Organizaciones propias por persona».**
    - **Decidido (la Guía):** el back suma el tope de 50, con el texto del tablero, en una parte de back al empezar T23d.

    El tablero dice «Usá un número de 1 a 50.»; el back acepta cualquier número desde 1 («Permití al menos una organización por persona.»). **Recomiendo** que el back sume el tope de 50 con el texto del tablero.
11. **Textos del tablero que no coinciden con lo que hace el back:**
    - **Decidido (el usuario): aprobar avisa por correo a los Dueños activos.** No se saca el aviso, como se recomendaba abajo.
      - Es una parte de back en T21a: copiá el aviso de despublicar, con el correo en es y en, por el outbox.
      - El aviso de la pantalla queda como el tablero: «Aprobamos {org}. Le avisamos a {correo}.». El correo va tapado y es el del Dueño que nombra la confirmación.
      - Con más de un Dueño: «Aprobamos {org}. Les avisamos a sus Dueños.».
    - **Decidido (la Guía):** los demás puntos, como se recomienda abajo.

    Lo que se había analizado:
   - «Aprobamos {org}. Le avisamos a {correo}.»: aprobar no avisa (ficha de Plataforma); **recomiendo** «Aprobamos {org}.»;
   - «Le avisamos a la Dueña.» al despublicar: el back avisa a todos los Dueños activos; **recomiendo** «Les avisamos a sus Dueños.»;
   - el resumen de la ficha de una organización creada por la plataforma (el tablero solo escribe «por autoservicio»): **recomiendo** «Alta el {fecha} por la plataforma»;
   - «Perfiles» de una cuenta sin espacio personal (un Dueño invitado desde Plataforma acepta sin Personal): **recomiendo** «1 organización» y «{n} organizaciones»;
   - «Último ingreso» de una cuenta que nunca ingresó, en la ficha: **recomiendo** «Último ingreso: nunca»;
   - los textos del back para la baja están escritos para la persona: `Legal.AccountDeletion.AlreadyPending` dice «Ya pediste la baja de tu cuenta.» y `Legal.AccountDeletion.LastAdmin`, «Antes de pedir la baja, cada Organización activa debe tener otra persona con el rol Dueño.». **Recomiendo** que `accounts/errors.ts` los traduzca en Plataforma: el único Dueño, con el texto del tablero (y la organización de `soleOwnerOrganizations`, releyendo la cuenta); la baja ya pedida, «Esa cuenta ya tiene la baja pedida.».
12. **Formas del tablero que no coinciden con las piezas comunes** (las piezas no se tocan):
    - **Decidido (la Guía):** como se recomienda abajo. Manda el criterio (decisión 103 de la consolidación).
   - Configuración dibuja una sola caja con dos encabezados («Registro y altas» y «Contacto»); Publicar versión nueva, una caja con el cartel adentro, un primer grupo sin título y «Contenido». La pieza es una `Section` por tema, con título. **Recomiendo** dos `Section` en Configuración; en Legales, el cartel como `Banner` debajo de la banda y dos `Section`, «Versión» y «Contenido».
   - «Revisar el pedido» pone «Pedido del {fecha}» debajo del título; `Dialog` no lleva bajada en el encabezado («Criterio · Diálogos»: lo que aclara el título va en el cuerpo). **Recomiendo** la fecha como primera línea del cuerpo.
   - «Último ingreso» dice «ayer» y la fecha desde el día siguiente; `DateText kind="relative"` dice «hace 1 día» y la fecha desde los 7 días. **Recomiendo** la pieza como está.
13. **El subtítulo de Auditoría en el teléfono.**
    - **Decidido (la Guía):** «Administración».

    `M-Auditoria-Plat` dice «Últimos 30 días»; las demás pantallas que se abren desde «Más» (`M-Legales`, `M-Config-Plat`) y `M-Auditoria-Org` dicen «Administración». **Recomiendo** «Administración».
14. **«Hasta» de «Extender prueba».**
    - **Decidido (la Guía):** como se recomienda abajo, en T21a. Lo pide `fechas-y-zonas`: una fecha civil es un `DateOnly`.

    Es una fecha civil y el contrato pide `expiresAtUtc` (un instante). **Recomiendo** que el `PUT` reciba `expiresOn` y el back la tome en la zona de la organización hasta el final de ese día, como «Vigente desde» de los legales (decisión 25), y que la lectura devuelva esa fecha para «Prueba hasta {fecha}».

---

## Registro de resultados

| Tarea | Commit back | Commit front | Evidencia (comandos y resultados) | Capturas enviadas | Notas |
|---|---|---|---|---|---|
| T00 | `2ed8f13` (evidencia inicial; completada al retomar) | `d73f362` | Ambos repos limpios en `main` al retomar; `aspire ps` → sin AppHost; consolidación `2209be7` presente; arnés centralizado → 0 hallazgos en ambos; `ClosedStages` → `{0,1,2,3,4,6}`; `npm run contracts` y `contracts:check` → exit 0; API front → 29/29; lint sin errores y build exit 0 | — | HEAD al retomar: back `97c28f9`, front `e685df0`; lienzo v111 aprobado. Resueltos el trabajo pendiente de Diseño y el contrato desactualizado anotados en la comprobación inicial. |
| T18c | | | | — | |
| Antes de T19 | | | | | |
| T19 | | | | | |
| T20a–T20d | | | | | |
| T21a–T21d | | | | | |
| T22a–T22b | | | | | |
| T23a–T23d | | | | | |
| T24 | | | | | |
| T25 | | | | | |

**Para decidir que aparezca al programar** (medidas o formas del tablero que no coinciden con una pieza común, y estados que falten): se anota acá, con el tablero, el caso y la pieza, y se le muestra al usuario.
