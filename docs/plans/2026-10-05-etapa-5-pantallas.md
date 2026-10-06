# Etapa 5: las pantallas de Plataforma — plan ejecutable

> **Para Codex, que programa estas pantallas:**
> - Antes de empezar, leé el `AGENTS.md` de la raíz de cada repo. Antes de tocar una carpeta, leé su `AGENTS.md` y las fichas que nombra.
> - Seguí las tareas en el orden de «Orden y dependencias». Cada tarea de lógica sigue el «Protocolo»: rojo, implementación, verde y commit.
> - **La fuente de cada pantalla es su tablero** (lienzo v111, front `e685df0`). «Lo que dicen los tableros» copia sus textos, columnas, filtros, menús, diálogos y estados; si algo de este plan no coincide con el tablero, manda el tablero y lo anotás.
> - **No inventes.** Si una pantalla necesita algo que el tablero no dibuja (un estado, un texto, una columna, una acción), frená y preguntale al usuario: lo dibuja Diseño. Lo que se sabía que faltaba está en «Para decidir», ya resuelto: cada punto dice qué hacer. Los que dibuja Diseño (1, 2, 6, 7 y 8) se programan con el lienzo v111.
> - **Las piezas comunes de `src/shared/` se usan tal cual.** No ajustes medidas a mano en las pantallas. Si una medida o una forma del tablero no coincide con la pieza común, no toques la pieza: anotalo en «Para decidir» del «Registro de resultados» y seguí con la pieza como está.
> - **El usuario ve cada pantalla antes y durante** (ver «La regla de pantallas del usuario»): antes de programar una pantalla, le mostrás su tablero y esperás su visto bueno; en cada avance, le mandás la captura lado a lado y el comando de la vista en vivo.
>   **Visto bueno recibido (2026-10-05):** el usuario aprobó todos los tableros v111 capturados en front `a88a96a` y pidió programar T19 a T23 en orden, sin nuevas pausas antes de cada pantalla. Se mantienen las capturas tablero/app a 1440 y 390 y el comando de `vivo.mjs` en cada avance. Se frena solo si algo contradice el tablero o falta algo que el plan no resuelve.
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

**Aprobación vigente de esta ejecución:** el usuario dio visto bueno a todos los tableros v111 capturados en front `a88a96a`, ya aprobados con Diseño, y autorizó continuar T19–T23 en orden sin nuevas pausas por pantalla. La muestra previa y su visto bueno están cumplidos para esos tableros; se mantienen las capturas durante cada avance y se frena sólo ante una contradicción con la fuente o un punto de producto que el plan no resuelve.

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
  - Espera aprobación: «Rechazar» (rojo con borde, como el tablero v111 y `Button variant="danger"`) y «Aprobar» (principal);
  - Activa: «Suspender» (secundario);
  - Suspendida: «Reactivar» (secundario) y «Cerrar» (rojo con borde, como el tablero v111 y `Button variant="danger"`; relleno sólo en la confirmación irreversible);
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
- Dueños tiene las tres variantes dibujadas en la v111: nombre (sin nombre, «—»), último ingreso a la derecha, correo y estado debajo, sin avatar. Se declara la fila móvil con `ListRow`. Las pestañas que no tienen fila dibujada van con la que arma `DataTable` desde los roles de cada columna (decisión 12 de este plan).

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

**Compu** (`Recuperar`; Tweak: paso «1. El correo o teléfono de antes | 2. Un correo o WhatsApp nuevo | 3. Pedido recibido», whatsapp **«Sin WhatsApp (hasta la Etapa 8)»**, v111 releída), en el marco del ingreso (formulario y panel de la marca):
- **Paso 1:** «Recuperar mi cuenta»; «Escribí el correo con el que entrabas.»; «Correo electrónico»; «Seguir»; «Volver a ingresar». Sin selector de canal.
  - Error: «Ingresá un correo electrónico válido.».
- **Paso 2:** «‹ Atrás»; «¿Dónde te escribimos?»; «Decinos un correo nuevo donde podamos escribirte.»; «Correo electrónico nuevo»; «Enviar código»; «Volver a ingresar». Sin selector de canal.
  - Errores: «Ingresá un correo electrónico válido.» y «Tiene que ser un correo distinto del de antes.».
  - La variante «Con WhatsApp» queda para la Etapa 8 («Para decidir» 8).
- **Código:** «Usar otro correo»; «Revisá tu correo»; «Te mandamos un código a **{destino}**. Vence en 10 minutos.»; los seis casilleros; «Verificar» y «Reenviar código». Error: «Escribí los 6 números del código.». Reenviar: «Te mandamos un código nuevo.».
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
| `POST /api/platform/tenants/{id}/approve`, `/reject`, `/suspend`, `/reactivate`, `/close` | `tenants.manage` | `version`, `reason` | 200 `VersionResponse` (T18c): la versión nueva de la organización | 409 `General.ConcurrencyConflict`; 409 `Tenancy.Tenant.InvalidTransition` «La organización no puede pasar a ese estado.»; 404 `Tenancy.Tenant.NotFound` «No encontramos la organización.» |
| `POST /api/platform/tenants/{id}/retry-provisioning` | `tenants.manage` | `version` (sin motivo) | 200 `VersionResponse` (desde T18c) | 409 `Tenancy.Tenant.ProvisioningConflict` «La organización ya existe con otros datos de configuración.» u otro 409 del back: aviso rojo |

### Ficha de una organización

| Ruta | Permiso | Manda | Recibe | Errores |
|---|---|---|---|---|
| `GET /api/platform/tenants/{id}` (T21a) | `tenants.read` | — | detalle propio, separado de la fila: `owners[]` (`id`, `displayName`, `email`, `identityStatus`, `membershipStatus`, `lastSignInAtUtc`, `lastSignInMethod`) y `lastStateChange` (`atUtc`, `action`, `reason`; `null` sin evidencia), además de sus metadatos y versión | 404: «No encontramos esta página» |
| `GET /api/platform/tenants/{id}/history` (T21a) | `tenants.read` | `after`, `limit` | cursor sin total: `items`, `nextCursor`, `hasMore`; cada evento tiene `id`, `occurredAtUtc`, `action`, `actor`, `actorRole`, `reason`, `summary` técnico sin datos personales | 401, 403 y 404 de organización inexistente o Personal |
| `GET /api/platform/tenants/{id}/features` | `tenants.read` | — | `[]`: `key`, `enabled`, `source` (`Plan`/`Manual`/`Trial`), `expiresOn` (fecha civil, T21a), `version` | — |
| `PUT /api/platform/tenants/{id}/features/{featureKey}` | `tenants.manage` | `enabled`, `source`, `expiresOn` (`DateOnly`, T21a), `version` (0 sin fila), `reason` | 200 `VersionResponse` | 409; 400 `Features.Module.ExpiryRequired` «Escribí cuándo termina la prueba.» (debajo de «Hasta»), `Features.Module.InvalidSource`, `InvalidKey`, `InvalidScope`; 404 `Features.Module.NotFound` «No encontramos el módulo.» |
| `GET /api/platform/tenants/{id}/domains` | `tenants.manage` (registra la entrada a la organización) | — | `[]`: `id`, `domain`, `status`, `txtRecordName`, `txtRecordValue`, `verifiedAtUtc`, `version`, `managedEmailCount` | — |
| `POST /api/platform/tenants/{id}/domains/{domain}/verify` | `tenants.manage` | `version`, `reason` | 200 `VersionResponse` (desde T18c): la versión nueva del dominio | 409 `Settings.TenantDomain.TxtNotFound` «No encontramos el registro TXT de verificación en el DNS.»; 409 `Settings.TenantDomain.AlreadyVerified` «El dominio ya está verificado.»; 503 `Settings.TenantDomain.DnsUnavailable` (lo avisa el `MutationCache`) |
| `DELETE /api/platform/tenants/{id}/domains/{domain}` | `tenants.manage` | `version`, `reason` (en el cuerpo) | 204 | 409; 404 `Settings.TenantDomain.NotFound` |
| `GET /api/platform/tenants/{id}/public-site` | `tenants.manage` (registra la entrada) | — | `slug`, `name`, `isPublished`, `publishedAtUtc` (T21a), `publishBlockedAtUtc`, `publishBlockedReason`, `version` | — |
| `POST /api/platform/tenants/{id}/public-site/unpublish`, `/allow-publish` | `tenants.manage` | `version` (la de la página), `reason` | 200 `VersionResponse` (desde T18c): la versión nueva de la página | 409; 400 `PublicSite.PublicPage.BlockReasonRequired` «El bloqueo de moderación necesita un motivo.» (debajo de «Motivo») |

- «Verificar» manda motivo: el tablero no lo pide; va el texto fijo «Verificar el dominio» de los locales (decisión 17 de este plan).
- «Publicada el» usa `publishedAtUtc` desde T21a; sin evidencia anterior, `null` se muestra «—» (ver «Para decidir» 4).

### Cuentas y operadores

| Ruta | Permiso | Manda | Recibe | Errores |
|---|---|---|---|---|
| `GET /api/platform/accounts/paged` | `accounts.read` | `page`, `pageSize`, `sort`, `search`, `state` (`PlatformAccountState`), `kind` (`PlatformAccountKind`) | `items[]`: `id`, `displayName`, `primaryEmail`, `kind`, `state`, `hasPersonalSpace`, `organizationCount`, `lastSignInAtUtc`, `lastSignInMethod`, `invitationExpiresAtUtc` (sin `version` desde T18c) | 403 |
| `GET /api/platform/accounts/filter-counts` | `accounts.read` | lo mismo | `total`, `states[]`, `kinds[]` | — |
| `GET /api/platform/accounts/{id}` | `accounts.read` | — | `displayName`, `primaryEmail` (nullable, mismo principal completo del listado; null para eliminada, T22b), `kind`, `state`, `createdAtUtc`, `deletedAtUtc` (nullable, T22a), `lastStateChange` (`atUtc`, `action`, `reason`; null sin evidencia, T22a), `lastSignInAtUtc`, `lastSignInMethod`, `invitationExpiresAtUtc`, `hasPersonalSpace`, `deletion` (`requestedAtUtc`, `scheduledForUtc`, `requestedByPlatform`), `loginMethods[]` (`type`, `maskedValue`, `isPrimary`, `verifiedAtUtc`, `managedByOrganizationName`, `canSignIn`), `organizations[]` (`tenantId`, `name`, `roleName`, `joinedAtUtc`, `status`, `tenantStatus`, `endedAtUtc`), `soleOwnerOrganizations[]`, `operatorPermissions`, `operatorVersion` (la versión de la asignación del operador; `null` si no es operador). Sin `version` desde T18c | 404 `Users.User.NotFound`: «No encontramos esta página» |
| `GET /api/platform/audit?accountId={id}` | `audit.read` | `after`, `limit`, `period=all`, `accountId` | el cursor de Auditoría | — (sin `audit.read`, la pestaña no se muestra) |
| `POST /api/platform/accounts/{id}/suspend`, `/reactivate`, `/revoke-sessions`, `/deletion` | `accounts.manage` | solo `{ reason }` (T18b, `PlatformAccountReasonHttpRequest`) | 204 | nunca un 409 de concurrencia: dos acciones a la vez, una gana y la otra recibe el de estado, 409 `Users.User.InvalidTransition` «No se puede cambiar la cuenta a ese estado.» o, la baja, 409 `Legal.AccountDeletion.AlreadyPending`; además, 409 `Platform.Operator.LastManager` «Tiene que quedar al menos un operador activo que pueda administrar operadores.», 409 `Legal.AccountDeletion.LastAdmin` y 403 `Users.User.CannotModifySelf` «No podés aplicar este cambio sobre tu propia cuenta.» (ver «Para decidir» 11 por los textos de la baja) |
| `POST /api/platform/operators/invitations` | `operators.manage` | `email`, `permissions[]` | 201: `id`, `expiresAtUtc` | 409 `Platform.Operator.InvitationExists` y `Platform.Operator.AccountExists` (debajo del correo, con el texto de §4); 400 `Platform.Operator.PermissionsRequired` «Elegí al menos un permiso de plataforma.» (debajo de «Permisos de plataforma») |
| `POST /api/platform/operators/{id}/invitation/resend` | `operators.manage` | — | 200: `id`, `expiresAtUtc` | 409 `Platform.Operator.NoPendingInvitation` «Esa cuenta no tiene una invitación de operador pendiente.»: aviso rojo |
| `DELETE /api/platform/operators/{id}/invitation` | `operators.manage` | — | 204 | lo mismo, en la franja |
| `PUT /api/platform/accounts/{id}/operator` y `DELETE` | `operators.manage` | `version` (= `operatorVersion` de la ficha, la de la asignación, no la de la cuenta), `reason` y, el `PUT`, `permissions[]` | `PUT`: 200 `VersionResponse` (la nueva `operatorVersion`); `DELETE`: 204 | no se programan hasta resolver «Para decidir» 1 |

- El bloqueo del único Dueño sale de `soleOwnerOrganizations` de la ficha (decisión 9 de este plan).
- «Se elimina el {fecha}» sale de `deletion.scheduledForUtc` al releer la cuenta después de la baja (decisión 9).
- «Necesita recuperación»: la organización sale de `managedByOrganizationName` del método que ya no sirve y la fecha, de `endedAtUtc` de esa membresía.
- «Suspendida el {fecha}. Motivo: {motivo}» usa `lastStateChange` y «Eliminada el {fecha}» usa `deletedAtUtc` desde T22a; sin evidencia se muestra «—», sin estimar fechas ni motivos (ver «Para decidir» 4).

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
| `POST /api/account-recovery` | `email`, `nonce`, `previousEmail` o `previousPhone` (`country`, `number`) | 202 | `Platform.AccountRecovery.VerificationRequired` «Pedí un código nuevo y verificalo.» (vuelve al código); 400 `Validation.Failed`, campo `previousEmail`, mensaje de `ValidationTexts.RecoveryMethodRepeated`: «Tiene que ser un correo distinto del de antes.» (se corrige el correo nuevo) |

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

- [x] **Rojo:** **Evidencia (2026-10-05):** `VersionedContractTests` → 1 fallo de 4 al agregar POST; `PlatformTenantAdministrationTests` → 2 fallos de 6 (esperado 200, recibido 204); `platform-contract.test.ts` → 13 fallos de 14 (11 respuestas y 2 cuentas). Logs en `.local/t18c/{architecture-red,http-red}.log`; resumen en `.local/t18c/report.md`.
  - `VersionedContractTests.Versioned_saves_answer_the_new_version` pasa de «todo `PUT` con `version`» a «todo `PUT` o `POST` con `version`» (un `DELETE` sigue con 204), y el control de la guarda suma un `POST` con versión que responde 204: el test falla con las 11 acciones;
  - cada test de integración de esas acciones espera 200 con la `version` que quedó guardada (la misma que da una lectura posterior) y que la acción siguiente con esa versión no da 409;
  - los tests de Cuentas esperan el listado y la ficha sin `version`.
- [x] **Implementación:** cada servicio devuelve la entidad guardada desde su trabajo transaccional y arma `VersionResponse.Of(result)` después de `ExecuteInTransactionAsync`; las entradas a la organización (`ITenantScope`) y los eventos siguen como están. `PlatformAccountReader` deja de proyectar el `xmin` de la cuenta. **Evidencia:** las 11 acciones y los dos DTO revisados; revisiones independientes de especificación y calidad aprobadas sin hallazgos. `operatorVersion` conserva la versión de la asignación; los helpers compartidos conservan el 204 de Business.
- [x] **Docs:** la línea 9 de `docs/rules/concurrencia.md` dice «un `PUT` o `POST` con `version` responde 200 con `VersionResponse`»; `plataforma.md` dice qué devuelve cada acción. **Evidencia:** ambas fichas actualizadas; OpenAPI regenerado por el build del Api.
- [x] **Verde:** los focales, `VersionedContractTests`, la integración de Plataforma afectada, el build sin advertencias y el `openapi.json` regenerado. En el front: `npm run contracts`, `npm run contracts:check` y `platform-contract.test.ts` con las 11 respuestas y las cuentas sin `version`. **Evidencia:** arquitectura 148/148; Application 476/476; integración Plataforma 55/55, dominios Business 3/3 y contratos 21/21 (703 en total, 0 omitidos). Build Api → 0 advertencias y 0 errores. Front → contratos/check exit 0, focal 14/14, lint sin errores y build exit 0. `git diff --check` y arnés `realinear-docs --detectar` → exit 0, 0 hallazgos en ambos repos. Comandos y resultados completos en `.local/t18c/report.md`.

Commit back `fix(plataforma): las acciones con versión responden la versión nueva`; front `chore(contratos): las acciones de Plataforma con VersionResponse`.

**Hecho cuando** no queda ninguna escritura con versión que responda 204 salvo los `DELETE`, y la cuenta no expone una versión que nadie usa.

### Antes de T19 · Los tableros, con el visto bueno del usuario

**Archivos:** `scripts/capturar-etapa-5.mjs` (copiá `capturar-etapa-6.mjs`), `docs/design/capturas/etapa-5/manifest.json` y `docs/design/capturas/etapa-5/tableros/`. `docs/design/capturas/etapa-5/propuesta.md` (archivada) no se toca.

- [x] El script nace con los casos de toda la etapa (la lista de T24) y, por ahora, captura solo los tableros, a 1440 × 900 y a 390 × 844, con cada opción de cada Tweak. Los datos de cada caso son los del tablero (los mismos nombres, fechas y cantidades), como hace la etapa 6. **Evidencia (2026-10-05):** `node scripts/capturar-etapa-5.mjs` y `node scripts/capturar-etapa-5.mjs --verify` → exit 0, 238 PNG / 119 pares / 10 grupos. Tweaks v111 visibles en su pestaña; Recuperar solo «Sin WhatsApp». 153 estados dibujados y 85 referencias base con su limitación explícita en `boardReference`: los M-* estáticos no se presentan como estados que no dibujan, ni se fabrican 50 filas o un 409. Esos casos quedan preparados para comparar la app al implementarla, según las piezas comunes y la decisión 12. TDD del catálogo: 6 fallos iniciales, luego 2 de visibilidad y 1 de ruta Más; verde 10/10. `node --check`, lint de los scripts y `git diff --cached --check` sobre las rutas propias → exit 0. Revisiones independientes de especificación y calidad aprobadas sin hallazgos. Arnés → auto/modelo/humano 0 tras agregar las dos líneas de los scripts en el árbol. Commit front `a88a96a`. Informe en `.local/capturas-e5/report.md` del front; fixtures y respuestas de app se agregan durante T19–T23.
- [x] Le mostrás al usuario las PNG de `tableros/` (o el recorrido del lienzo), con qué estado es cada una y los puntos de «Para decidir» que tocan cada pantalla. **Evidencia:** galería abierta en Codex con los 10 grupos, nombres de cada caso, compu/teléfono y referencias base; cada grupo indica los puntos de «Para decidir» aplicables. Recuperar se mostró además en 13 pares (26 PNG), con los tres pasos, código, errores, reenvío y enlaces desde ambas puertas de Ingreso. Comando presentado: `node scripts/recorrido-lienzo/vivo.mjs etapa-5/recuperar-paso-1-escritorio`. La escritura por etiqueta del tablero en vivo se corrigió con TDD (rojo 1 → verde; suite 10/10), front `97e5c6b`.
- [x] **Su visto bueno, antes de programar.** Si pide un cambio, frenás: lo dibuja Diseño. **Evidencia (2026-10-05):** el usuario respondió «Visto bueno a todos los tableros de la v111 que capturaste (a88a96a): ya los aprobé con Diseño, así que no frenes antes de cada pantalla». Autorizó programar T19 a T23 en orden y mantener las capturas comparadas y la vista en vivo en cada avance.

Commit `chore(capturas): los tableros de Plataforma, antes de programar`.

### T19 · Contrato y «Recuperar mi cuenta»

**Archivos:**
- `src/test/platform-contract.test.ts` (se amplía);
- `src/areas/public/auth/pages/RecoverAccountPage.tsx` y su test; `src/areas/public/auth/api/recovery.ts`; `src/areas/public/auth/errors.ts` (si traduce algo nuevo);
- `src/areas/public/auth/pages/LoginPage.tsx` y su test (el enlace);
- `src/app/routes.tsx` (`/recuperar`, pública, sin `AccessRoute`);
- `src/locales/{es,en}/auth.json` (`recovery.*`).

**Copiá de:** el tablero `Recuperar` y `M-Recuperar` (§10); `LoginPage.tsx`, `AuthCodeStep.tsx` (el paso del código: «Usar otro correo», casilleros, «Verificar», «Reenviar código» con su cuenta regresiva), `AuthStatus.tsx` (el paso 3), `AuthMessage.tsx` y `AuthLink.tsx`; `FormField variant="auth"`, `EmailField`, `PhoneField` + `CountrySelect`, `SegmentedControl`; `useIdempotentMutation` con `meta: { handlesError: true }`.

- [x] **Rojo:** **Evidencia T19 (2026-10-05):** `npm test -- src/areas/public/auth/pages/RecoverAccountPage.test.tsx src/areas/public/auth/pages/LoginPage.test.tsx` falló por pantalla y enlaces ausentes (22 anteriores verdes); `platform-contract.test.ts` falló por cliente recovery ausente, con 64 contratos existentes verdes. Focales adicionales: rechazo real de validación no ubicado en campo editable; un `AbortError` sin manejar al salir; revisión SPEC, 2 fallos por no mostrar el `detail` del back y por el fallback de 429. CALIDAD: 4 fallos al corregir datos o abandonar una operación tras perder su respuesta, más 3 del hook por no distinguir el cuerpo. Logs `.local/t19/{red,contract-red,validation-red,unmount-red,spec-detail-red,quality-idempotency-red,quality-hook-red}.txt` del front.
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
- [x] **Implementación:** **Evidencia T19:** `/recuperar` pública, tres POST tipados con `Idempotency-Key`, nonce de 32 bytes en memoria, RHF/EmailField y pasos `AuthCodeStep`/`AuthStatus`; dos enlaces de Ingreso. Variante Sin WhatsApp; prueba verificada y cancelación al salir, decisiones 20–23. `apiErrorMessage` conserva el texto del backend; los estados se deciden por `code`. `sameOperation` optativo distingue cuerpos corregidos y recorridos abandonados, sin afectar otros consumidores. Sin cambios en piezas visuales ni lienzo.
  - el nonce: 32 bytes de `crypto.getRandomValues` en base64url, uno por recorrido, solo en memoria;
  - los pasos viven en el estado de la pantalla; el código, con `AuthCodeStep` (la excepción de `formularios.md`);
  - el teléfono de antes, con `PhoneField`, se manda como `previousPhone` solo en la variante «Con WhatsApp»; en «Sin WhatsApp» v111 no se ofrece;
  - el número de WhatsApp nuevo no se ofrece mientras el back acepte solo correo (decisión 20 del plan de la E5).
- [x] **Verde:** los focales, lint, build y `contracts:check`. **Evidencia T19:** final `npm test -- src/areas/public/auth/pages/RecoverAccountPage.test.tsx src/areas/public/auth/pages/LoginPage.test.tsx src/test/platform-contract.test.ts src/locales/parity.test.ts src/app/routes.test.tsx src/shared/api/useIdempotentMutation.test.ts src/shared/api/contracts.test.ts src/shared/api/httpClient.test.ts src/shared/api/queryClient.test.ts` → 221/221 (`quality-final-green.txt`); build exit 0 (`quality-build.txt`), lint focal limpio (`quality-scoped-lint.txt`), `npm run contracts:check` exit 0 (`quality-contracts-check.txt`). `npm run lint` previo exit 0 con warnings preexistentes fuera del alcance. Capturadores: rojo 2 + 1 de acciones por rol → `node --test scripts/capturar-etapa-5.test.mjs scripts/capturar-relevamiento.test.mjs` 17/17; lanzamiento de Chromium protegido por cleanup. El test real bloquea solo Google Fonts externo para evitar un timeout ajeno a la conducta. Arnés `node scripts/arneses/arnes.mjs realinear-docs --detectar` → auto/modelo/humano 0, informe `.local/arneses/realinear-docs/20261005-1703.md` del back. Revisión SPEC final aprobada, con 49 tests independientes (Recovery 31 + hook 18); CALIDAD final aprobada después de repetir con Playwright la respuesta perdida y el correo corregido: nueva clave, mismo nonce, sin 422; P2 y P3 cerrados.
- [x] **Capturas** (grupo `recuperar`): los tres pasos y el código, a 1440 y a 390, y el enlace en Ingreso. Vista en vivo: `vivo.mjs etapa-5/recuperar-paso-1-escritorio`. **Evidencia T19:** `node scripts/capturar-relevamiento.mjs --app-url http://127.0.0.1:5191 --group recuperar --out docs/design/capturas/etapa-5/lado-a-lado` y el mismo comando con `--verify` → exit 0, 26 PNG / 13 pares. Se entregaron los pasos, código y galería con errores, reenvío y ambas puertas; `vivo.mjs etapa-5/recuperar-paso-1-escritorio --vite http://127.0.0.1:5191` abrió la vista en vivo. **Para decidir / diferencias comunes conservadas:** halo de foco del campo inválido; `AuthCodeStep` no marca los seis casilleros en rojo por código incompleto; toast común oscuro frente al claro del tablero; check de `AuthStatus` de 22 frente a SVG de 24. Ingreso omite el selector WhatsApp hasta E8. Se registran y se sigue con las piezas tal cual, según «Para Codex»; las capturas no se autoaprueban.

Commit `feat(recuperacion): conectar «Recuperar mi cuenta»`.

**Hecho cuando** `/recuperar` coincide con `Recuperar` y `M-Recuperar` en sus pasos, registra un pedido real y el ingreso tiene su enlace.

### T20 · El marco de Plataforma

#### T20a · Back · Los permisos de Plataforma en `/api/me`

**Archivos:** `Application/Models/Profile/EffectivePermissions.cs`, `Application/Services/Profile/ProfileSnapshotBuilder.cs` (y sus dependencias, sin pasar el tope de ADR 0039), sus tests, `docs/contracts/openapi.json`, `docs/features/plataforma.md` y `docs/features/identidad.md` si la nombran.

- [x] **Rojo:** «un operador con `platform.tenants.read` y `platform.audit.read` recibe exactamente esos dos en `effectivePermissions.platform`»; «una persona y una empresa reciben la lista vacía»; «un operador suspendido o sin asignación, vacía». **Evidencia T20a (2026-10-05):** `dotnet test --no-restore --project tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/ArquitecturaBaseMultitenant.Api.IntegrationTests.csproj -- --filter-class 'ArquitecturaBaseMultitenant.Api.IntegrationTests.Auth.MeTests'` → 2/5 fallan por campo ausente, 3 anteriores pasan (`.local/t20a/red-integration.log`). Front: `npm test -- src/test/platform-contract.test.ts -t 'permisos de Plataforma viajan'` → 1 fallo y 98 omitidos por falta de `platform` (`contracts-red.txt`). Los intentos previos por NuGet o la fixture HTTP no se cuentan como rojo.
- [x] **Implementación:** `EffectivePermissions` suma `Platform` (`IReadOnlyList<string>`), que se llena solo con `access=platform`, desde la asignación persistida (`PlatformPermissionService`), sin caché. `Permissions` (la lista plana) no cambia. **Evidencia T20a:** `ProfileEffectivePermissionsBuilder` compone las tres listas; `ProfileSnapshotBuilder` conserva ocho dependencias y la lista blanca del lector global. La misma sesión observa una asignación cambiada y luego eliminada. Consumer/Business no consultan permisos de Plataforma; suspendido, baja pendiente, invitación pendiente o sin asignación reciben lista vacía. Decisión 24; back `690cbca`, front `b8e0ce6`.
- [x] **Verde:** focal, integración de `/api/me`, build sin advertencias. Front: `npm run contracts` y `contracts:check`. **Evidencia T20a:** back, focales perfil 29/29, HTTP Me 5/5, arquitectura 24/24, permisos/DI 11/11 y contrato/host 4/4 (73 tests; comandos y logs en `.local/t20a/report.md`); `dotnet build src/ArquitecturaBaseMultitenant.Api/ArquitecturaBaseMultitenant.Api.csproj --no-restore` → 0 advertencias/errores y OpenAPI regenerado. Front: `npm run contracts`, `contracts:check`, build, lint de las nueve rutas escritas y diff-check → exit 0; focales de contrato/fixtures 142/142 más shared API 29/29, total 171 (`green-front.txt`, `green-shared-api.txt`, `build-front.txt`). Arnés central → auto/modelo/humano 0 (`20261005-1723.md`). SPEC y CALIDAD aprobadas sin hallazgos. Los diagnósticos del generador sobre keyring/EventLog del sandbox no impidieron la generación; el contrato se comprobó contra el runtime real. Sin cambios visuales ni Aspire.

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

- [x] **Rojo:**
  - `routes.test.tsx`: cada ruta de Plataforma pide su permiso; `/plataforma/legales/nueva` pide `legal.manage`;
  - `visible.test.ts`: cada enlace de los dos paneles es una ruta del manifiesto y se ve solo con su permiso; un panel sin enlaces no se muestra;
  - `RequirePermission.test.tsx`: sin el permiso de Plataforma, «No tenés permiso»;
  - `Sidebar.test.tsx` y `AdminPanel.test.tsx`: «Gestión» arriba y «Administración» al pie; un solo panel abierto; arranca abierto el de la pantalla y su opción marcada; «Cerrar Gestión» y «Cerrar Administración»; sin «WhatsApp»;
  - `AccessMenu.test.tsx`: en Plataforma, debajo del nombre «Plataforma»; sin «Perfiles»; «Salir» (y «Mi cuenta» según «Para decidir» 7);
  - `Breadcrumbs.test.tsx`: «Plataforma / Organizaciones» y «Plataforma / Organizaciones / {nombre}»;
  - el inicio de un operador sin `tenants.read` (según «Para decidir» 5);
  - axe.
  **Evidencia T20b (2026-10-05):** el comando focal de 10 suites en `.local/t20b/report.md` → 26 fallos de conducta ausente sobre 63 tests (`red.log`). Rojos dirigidos adicionales: índice real sin permisos (`red-real-home.log`), opción marcada al abrir el panel contrario (`red-active-panel.log`), flecha con Gestión filtrada (`red-reader-arrow.log`) y paridad del campo plano del fixture (`red-flat-permissions.log`, 1 fallo). Capturador: `node --test --test-name-pattern="el marco usa" scripts/capturar-etapa-5.test.mjs` → 1 fallo por arnés ausente (`capture-red.txt`).
- [x] **Implementación:**
  - el manifiesto (`frontend.md` §3): `/plataforma` (Organizaciones, el inicio, `tenants.read`), `/plataforma/organizaciones/:id` (`tenants.read`), `/plataforma/cuentas` y `/plataforma/cuentas/:id` (`accounts.read`), `/plataforma/recuperaciones` (`recoveries.manage`), `/plataforma/legales` y `/plataforma/legales/nueva` (`legal.manage`), `/plataforma/auditoria` (`audit.read`), `/plataforma/configuracion` (`settings.manage`);
  - «Gestión»: Organizaciones, Cuentas, Recuperaciones; «Administración»: Auditoría, Documentos legales, Configuración;
  - el contexto del menú lateral: «Plataforma» y «Administración del servicio»;
  - las pantallas que todavía no existen se suman a `platformPages` en su tarea; hasta entonces su ruta no se monta.
  **Evidencia T20b:** front `9776a02`. Nueve permisos individuales separados; nueve rutas y seis enlaces del mismo manifiesto; paneles exclusivos con la opción actual, migas y menú sin perfiles. `platformPages` parcial no monta features pendientes, pero conserva la guarda del índice real. Fixtures exclusivos y puros para Node, con `permissions: []`. Decisiones 25–26; se copiaron grilla y documento del tablero, anticipando esas piezas de T20d.
- [x] **Verde:** focales, `navigation.test.tsx`, lint y build. **Evidencia T20b:** comando de 14 suites del informe → 252/252 (`green-final-2.log`); incrementales rutas/textos 39/39 (`green-contract-texts.log`), Sidebar/PlatformLayout 13/13 (`green-reader-arrow.log`); cierre del Minor de fixtures 54/54 en cuatro suites (`green-flat-permissions.log`). `npm run build`, lint del alcance y diff-check → exit 0, sin warnings nuevos (`build-flat-permissions.log`, `lint-complete.log`, `lint-flat-permissions.log`). `node --test scripts/capturar-etapa-5.test.mjs scripts/capturar-relevamiento.test.mjs` → 18/18; lint de las cuatro rutas de arnés exit 0. Arnés central `realinear-docs --detectar` → auto/modelo/humano 0 (`20261005-1750.md`). SPEC y CALIDAD finales aprobadas; único Minor corregido con rojo y rechecks de ambas revisiones. No se repitieron suites globales ni se levantó Aspire.
- [x] **Capturas** (grupo `plataforma-marco`): el marco con cada panel abierto, a 1440. **Evidencia T20b:** `node scripts/capturar-relevamiento.mjs --app-url http://127.0.0.1:5191 --group plataforma-marco --out docs/design/capturas/etapa-5/lado-a-lado` y `--verify` → exit 0, 10 PNG / 5 pares a 1440 y 390. Se entregaron Gestión, Administración y tu menú con comandos `vivo.mjs etapa-5/marco-{gestion,administracion,tu-menu}-{escritorio,movil} --vite http://127.0.0.1:5191`, y galería actualizada. **Alcance parcial explícito:** contenido central pendiente T21–T23; a 390 la barra y Más se incorporan en T20c. **Diferencias comunes conservadas:** íconos de menú de 18 frente a SVG de 20; trazo de `KeyIcon`; `Avatar` y `DropdownMenu` existentes, incluido correo truncado. Se registran y se sigue tal cual, según «Para Codex»; las capturas no se autoaprueban. WhatsApp/Consultas se omiten por alcance E11.

Commit `feat(plataforma): el marco, la navegación y los permisos del operador`.

#### T20c · Front · El marco del teléfono

**Archivos:** `src/layouts/PrivateLayoutFrame.tsx` (la barra de abajo que declara el manifiesto), `src/layouts/MorePage.tsx` (Plataforma), `src/layouts/navigation/{types,platform}.ts` y sus tests.

**Copiá de:** la barra de abajo de los `M-*` de Plataforma y `M-Mas-Plat` (§1); `MorePage.tsx`; `BottomNav`, `AppBar`, `SettingsGroup`.

- [x] **Rojo:** **Evidencia T20c:** `npm test -- src/layouts/PrivateLayoutFrame.test.tsx src/layouts/MorePage.test.tsx src/auth/SessionRecovery.test.tsx` → 25 fallos de UI / 22 verdes; navegación focal → 1 fallo / 4 verdes (`.local/t20c/red.log`, `red-navigation.log`). La prueba de F5 ya pasó con `SessionRecovery` existente: verifica que recuperar la cookie manda `access=platform`, sin afirmar un E2E real antes de T25.
  - `PrivateLayoutFrame.test.tsx`: en Plataforma, la barra de abajo trae «Organizaciones», «Cuentas», «Recuperaciones» (cada una solo con su permiso) y «Más»; la sección actual va marcada; sin «Consultas»;
  - `MorePage.test.tsx`: «Más» de Plataforma: debajo del título «Plataforma»; quién sos; «Administración» con lo que tus permisos dejan ver; «Mi cuenta» y «Salir»; sin «Perfiles» ni «WhatsApp»;
  - F5 en `/plataforma/cuentas` conserva la sesión de Plataforma.
- [x] **Implementación:** el manifiesto declara las secciones de la barra de abajo; `PrivateLayoutFrame` las arma con `BottomNav` (las de Personal y Empresa siguen con «Inicio» y «Más»). Las pantallas abiertas desde «Más» llevan ‹ «Volver» y «Administración» debajo del título. **Evidencia T20c:** `bottomSections` referencia las mismas entradas de Gestión y se filtra con `canOpen`; los detalles conservan su sección y no reciben Volver a Más. `/plataforma/mas` real muestra identidad, Administración según permiso, Mi cuenta y Salir; sin Perfiles, WhatsApp ni Consultas. La ruta se monta desde `moreTo`, separada de las features pendientes. Los tres SVG inferiores de T20d se copiaron del tablero ahora porque la barra ya los necesita; las piezas comunes y los íconos existentes se conservan tal cual.
- [x] **Verde y capturas** (grupo `plataforma-marco`): la barra de abajo y «Más», a 390. **Evidencia T20c:** `npm test -- src/layouts/PrivateLayoutFrame.test.tsx src/layouts/MorePage.test.tsx src/layouts/PlatformLayout.test.tsx src/layouts/navigation src/layouts/components/Breadcrumbs.test.tsx src/app/routes.test.tsx src/auth/SessionRecovery.test.tsx src/auth/AccessRoute.test.tsx src/shared/ui/app/app.test.tsx src/shared/ui/icons.test.tsx` → **272/272 en 12 suites**, incluidas axe, permisos y accesos existentes; `npm run build`, oxlint de los 11 archivos de código/test modificados y 4 scripts, diff-check → exit 0. Capturador rojo dirigido 1 + 1 de rótulo móvil; `node --test scripts/capturar-etapa-5.test.mjs scripts/capturar-relevamiento.test.mjs` → **20/20**. Captura `--group plataforma-marco` → 10 PNG / 5 pares a 1440 y 390 (un caso repetido tras navegación durante la primera corrida); verify `--group recuperar,plataforma-marco` → **36 PNG**, exit 0. Entregados ambos tamaños de barra y Más, galería y `vivo.mjs etapa-5/marco-{barra-inferior,mas}-{escritorio,movil} --vite http://127.0.0.1:5191`. Más se captura con su router real; las listas centrales siguen pendientes T21–T23. **Diferencias comunes conservadas:** truncado del correo y ausencia de indicadores del sistema operativo y de inicio dibujados en los tableros; `AppBar`, `BottomNav`, `Avatar` y `SettingsGroup` se usan sin retoques.

**Lint completo T20c:** `npm run lint` → exit 0, 0 errores y 752 advertencias en rutas fuera del cambio (principalmente lienzo archivado y piezas previas); `.local/t20c/lint-global.log`. Ninguna advertencia pertenece a las 28 rutas del commit. El lint del alcance está limpio; el global no se presenta como libre de advertencias.

**Revisiones T20c:** SPEC y CALIDAD aprobadas sin hallazgos pendientes; `realinear-docs --detectar` → auto/modelo/humano 0 (`20261005-1820.md`). Commit front `b7ee780`, en `main`, sin push.

Commit `feat(plataforma): la barra de abajo y «Más» en el teléfono`.

#### T20d · Front · Las piezas de Plataforma

**Archivos:**
- `src/shared/platform/ReasonDialog.tsx`, su test, `AGENTS.md` y `CLAUDE.md` (como `shared/account/`);
- `src/shared/hooks/useDialogFailure.ts` y su test (sale de `areas/business/companies/components/`, con la raíz de la consulta como parámetro; Empresas pasa `companiesQueryRoot`). Además del 409 de concurrencia, acepta los códigos que dicen que lo que se ve ya cambió; Plataforma pasa `Tenancy.Tenant.InvalidTransition`, `Users.User.InvalidTransition`, `Legal.AccountDeletion.AlreadyPending` y `Platform.AccountRecovery.AlreadyResolved` (decisión 18 de este plan);
- `src/shared/ui/DetailGrid.tsx` y su test (sale de `areas/business/audit/pages/AuditPage.tsx`: la grilla del detalle en la compu y `AuditPhoneDetail` en el teléfono);
- `src/tenancy/publicSite.ts` y su test: `publicSiteHost(slug)` (sale `PUBLIC_DOMAIN` de `PublicPagePage.tsx`; la Etapa 7 la completa desde el issuer);
- `src/shared/ui/icons.tsx`: los íconos que faltan, copiando el trazo del tablero: grilla (Gestión, `Organizaciones.dc.html`), documento (Documentos legales), calendario («Extender prueba», `Organizacion.dc.html`) y los tres de la barra de abajo del teléfono (`M-Organizaciones.dc.html`: Organizaciones, Cuentas, Recuperaciones).

**Copiá de:** §11; `ConfirmDialog.tsx` y `dialog.tsx` (`size="sm"`), `FormField`, `textarea`, `DateField`, `FormError`, `Button` con `pending`; `useDialogFailure.ts`; `AuditPage.tsx`.

- [x] **Rojo** (`ReasonDialog.test.tsx`): **Evidencia T20d:** `npm test -- src/shared/platform/ReasonDialog.test.tsx` → 15 fallos de conducta antes de implementar; fecha específica/única → 2 fallos; verde final del diálogo 19/19. El consolidado focal de 17 suites → 271/271, incluido DateField por defecto, reintento global con respuesta void/versionada, campos rechazados, cinco códigos de 409, axe y en-US. `npm run build` y lint de 18 rutas → exit 0, sin advertencias del alcance. Lint global exit 0, 752 advertencias heredadas fuera del cambio. Logs en front `.local/t20d/`; decisiones 28–29.
  - «sin motivo, "Escribí el motivo." debajo del campo y no manda nada»;
  - «con fecha ("Extender prueba"), la fecha mal escrita dice "Escribí la fecha como dd/mm/aaaa."»;
  - «la acción de peligro es roja y la otra, principal; sin ✕; Escape y "Cancelar" cierran»;
  - «mientras espera, el botón dice lo que hace y no se toca»;
  - «un bloqueo conocido muestra la franja roja, sin motivo, con la acción apagada»;
  - «lo que el back rechaza va en la franja, arriba de los botones; un 409 de concurrencia o de estado relee la pantalla al cerrar»;
  - «la acción devuelve la versión que respondió el back, para que la pantalla la tome como base»;
  - «en el teléfono es una hoja con manija»;
  - axe.
- [x] `useDialogFailure.test.ts`, `DetailGrid.test.tsx` y `publicSite.test.ts`; `CompanyDetailPage.test.tsx`, `AuditPage.test.tsx` y `PublicPagePage.test.tsx` siguen verdes sin cambiar lo que se ve. **Evidencia T20d:** extracciones/CalendarIcon primero en rojo (3 módulos ausentes y 1 fallo de ícono); consolidado `focal-final.log` → 271/271, con consumidores Empresas, Auditoría y Página pública. Árbol, fichas y punteros actualizados; `npm test -- src/test/harness.test.ts` → 7/7. `node --test scripts/capturar-etapa-5.test.mjs scripts/capturar-relevamiento.test.mjs` → 22/22; lint de capturas y `git diff --check` → exit 0. Captura de los grupos organizaciones, organización y cuentas → 12 PNG / 6 pares a 1440 y 390; verify de los cinco grupos implementados → 48 PNG, exit 0. Galería y comandos vivo entregados. Se compara el diálogo real con fondo pendiente T21–T23; cuando el tablero móvil no dibuja el diálogo, su referencia base queda declarada. Se conservan foco, asterisco, hoja y medidas de las piezas comunes.
- [x] **Antes de tocar** `companies/`, `audit/`, `public-page/` o `icons.tsx`, mirá `git status`: si tienen cambios de otra conversación, frená y avisale al usuario. **Evidencia T20d:** `git status --short` antes de modificar esas rutas → sin cambios ajenos; bases front `b7ee780` y back `05ea9f3`, `main`. Sólo coexistieron rutas de captura del root de esta misma tarea.

Commit `feat(plataforma): las piezas comunes de las pantallas de Plataforma`.

**Hecho cuando** (T20) el operador entra a `/plataforma`, ve «Gestión» y «Administración» (o la barra de abajo y «Más») con lo que sus permisos le dejan, y las piezas de las pantallas existen.

### T21 · Organizaciones

#### T21a · Back · La ficha y su historial

Es la parte de back de T21 del [plan de la E5](2026-10-03-etapa-5-plataforma.md#t21--organizaciones-listado-y-ficha-real), sin cambios: `GET /api/platform/tenants/{id}` con `owners` y `lastStateChange`, y `GET /api/platform/tenants/{id}/history` por cursor, con sus tests (dos Dueños y registrante retirado, identidad eliminada o sin nombre, historial de otra organización excluido, último estado no reemplazado por cambios de módulos ni por la entrada técnica, `null` sin evidencia), el inventario de rutas y el contrato.

Suma lo que se decidió en «Para decidir»:
- **4:** el último ingreso de cada Dueño en `owners` y `publishedAtUtc` de la página pública. Se guarda al publicar, con su migración; si no hay dato, `null`.
- **11:** aprobar una organización avisa por correo a sus Dueños activos. Copiá el aviso de despublicar: el correo en es y en, por el outbox, y su test de integración.
- **14:** el `PUT` de un módulo recibe `expiresOn` (`DateOnly`) en lugar de `expiresAtUtc`. Vale hasta el final de ese día en la zona de la organización, como «Vigente desde» de los legales, y la lectura devuelve esa fecha.

- [x] Rojo, implementación y verde como dice ese plan, más un test por cada punto de arriba. Front: `npm run contracts` y `contracts:check`.
  **Evidencia T21a (2026-10-05):** back `0425176`, front `9712d33`. Rojo HTTP real 5/13 por las conductas ausentes; fecha nullable sin tipo 1/1 y fecha máxima con HTTP 500 1/1. Verde: comandos focales del informe back `.local/t21a/report.md` → integración PostgreSQL 45/45, seguridad 53/53, regresión Auditoría 3/3, arquitectura 56/56, Application 22/22 y Domain 9/9; build API 0 advertencias/errores. SPEC detectó un correo secundario propio omitido: rojo 2/2 → aviso corregido sólo en aprobación, verde 40/40 de avisos/alta/templates y build final 0 advertencias/errores. SPEC, ReSPEC y CALIDAD finales aprobadas. Migración: dos columnas nullable, sin backfill ni cambios de RLS; decisión 30. Front: rojo 6/101 y unknown de fecha 1/101; `npm test -- src/test/platform-contract.test.ts src/shared/api/contracts.test.ts src/shared/api/httpClient.test.ts src/shared/api/queryClient.test.ts` → 130/130 en cuatro suites. `npm run contracts`, `contracts:check` (también después del delta), build, lint de las dos rutas y diff-check → exit 0. `npm run lint` global → exit 0, 0 errores y 752 advertencias heredadas fuera del alcance. Detector `realinear-docs --detectar` → auto/modelo/humano 0 (`20261005-1940`). Sin cambios visuales ni captura; la conexión de las pantallas sigue en T21b–T21d. Los focales se superponen y no se suman como una suite global.

Commit back `feat(plataforma): la ficha de una organización con Dueños, último estado e historial`.

#### T21b · Organizaciones: la lista y «Crear organización»

**Archivos:** `src/areas/platform/tenants/{api/tenants.ts, columns.tsx, errors.ts, components/CreateOrganizationDialog.tsx, pages/OrganizationsPage.tsx}` y sus tests; `AGENTS.md` y `CLAUDE.md` de `tenants/`; `src/locales/{es,en}/platformTenants.json`; `enums.json`.

**Copiá de:** §2 y §11; `UsersPage.tsx`, `users/columns.tsx`, `InviteUserDialog.tsx`; `ReasonDialog`.

- [x] **Rojo** (`OrganizationsPage.test.tsx`): **Evidencia T21b:** pantalla ausente → 2 fallos de banda/acción; rojo dirigido del botón literal, clave al reabrir, reintento de diálogo cancelado y respuesta tardía. GET retenido: rojo 1/1 al esperar la invalidación antes de cerrar → verde al usar la versión respondida y releer en segundo plano. Logs e informe front `.local/t21b/report.md`.
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
- [x] **Implementación:** `usePagedQuery` (orden por defecto `name`), conteos de `filter-counts`, la zona y el idioma por defecto según la decisión 14 de este plan. **Evidencia T21b:** lista real en `/plataforma`; banda global y conteos cruzados; alta de cinco campos; API y aliases derivados del contrato; versiones propias, motivos y claves por recorrido del formulario. Las decisiones 31–33 precisan la coordinación sin alterar el producto.
- [x] **Verde** y **capturas** (grupo `plataforma-organizaciones`): la lista con 7 filas y con 50, sin coincidencias, un menú de cada estado, «Crear organización» vacío y con errores, una confirmación con motivo y su 409, y el teléfono. **Evidencia T21b:** `npm test -- src/areas/platform/tenants/api/tenants.test.ts src/areas/platform/tenants/components/CreateOrganizationDialog.test.tsx src/areas/platform/tenants/pages/OrganizationsPage.test.tsx src/app/routes.test.tsx src/test/platform-contract.test.ts` → 194/194 (52 focales de la feature); build, contracts:check, lint del alcance y diff-check → exit 0. `npm run lint` → exit 0, 752 advertencias heredadas. `node --test scripts/capturar-etapa-5.test.mjs scripts/capturar-relevamiento.test.mjs` → 24/24; `node --test scripts/recorrido-lienzo/vivo.test.mjs` → 3/3, después del rojo dirigido 1/1 de la espera. `node scripts/capturar-relevamiento.mjs --app-url http://127.0.0.1:5191 --group plataforma-organizaciones --out docs/design/capturas/etapa-5/lado-a-lado` → exit 0, 28 PNG / 14 pares. Verify acumulado de los cinco grupos implementados → exit 0, 70 PNG / 35 pares. Entregados lista, menú, alta con errores, 409 y 50 filas, ambos tamaños y comandos `vivo.mjs etapa-5/organizaciones-{caso}-{escritorio,movil} --vite http://127.0.0.1:5191`. **Para decidir / piezas comunes conservadas:** truncado de segundas líneas, anchos de tabla, radio/sombra de filtros y caja, SVG del menú y halo del campo inválido. El teléfono omite indicadores del SO y Consultas (E11). Las referencias base de los estados que no dibuja el teléfono y de 50 filas están declaradas; las capturas no se autoaprueban. Se continúa con shared tal cual, según «Para Codex».

Commit `feat(plataforma): la lista de organizaciones`.

#### T21c · Ficha de una organización: banda, Historial y Dueños

**Archivos:** `src/areas/platform/tenants/{pages/OrganizationPage.tsx, tabs/OrganizationHistoryTab.tsx, tabs/OrganizationOwnersTab.tsx, tabs/useOrganizationHistory.ts}` y sus tests.

**Copiá de:** §3; `CompanyDetailPage.tsx`, `CompanyMembersTab.tsx`; `useCursorList` y `LoadMore` (el historial).

- [x] **Rojo** (`OrganizationPage.test.tsx`):
  - «la banda con el estado, el resumen y las acciones de cada estado (Espera aprobación, Activa, Suspendida, Cerrada)»;
  - «el cartel de Suspendida y de Cerrada con la fecha y el motivo de lastStateChange»;
  - «después de Suspender desde la banda, Reactivar manda la versión que respondió la suspensión, aunque la relectura de la ficha no haya llegado; una relectura con otra versión no la pisa»;
  - «Historial por cursor, con "Cargar más"»; «Dueños con su correo, último ingreso y estado»;
  - «las pestañas con la cantidad que tiene dato» (según «Para decidir» 3);
  - «404: "No encontramos esta página"»; «si no carga: "No pudimos abrir esta pantalla" y "Reintentar"»;
  - «en el teléfono: la barra con el estado y el resumen, el ⋮ con las acciones, las pestañas deslizables y el historial en filas»; axe.
  **Evidencia T21c (2026-10-05):** `npm test -- src/areas/platform/tenants/pages/OrganizationPage.test.tsx --maxWorkers=1` → rojo 31/31 por ruta ausente; verde inicial 31/31. Pruebas dirigidas: `-t 'Dueños a 390'` → rojo 1/1, verde 1/1 al declarar `ListRow`; `-t 'Cerrar en la banda'` → rojo 1/1 y `-t 'en banda usa peligro'` → verde 2/2. La guarda de rutas esperaba sólo el listado: rojo 1/153, ajustada a las dos páginas lazy reales. Capturador: escenarios por par en vez de props del tablero móvil base, rojo 1/1 → verde; coincidencia exacta de «Cerrar» frente a «Cerrar Gestión», rojo HTTP real 1/1 → verde. Informe front `.local/t21c/report.md`.
- [x] **Verde** y **capturas** (grupo `plataforma-organizacion`): cada estado del Tweak «estado» con Historial y Dueños, y el teléfono. **Evidencia T21c:** cierre de diez suites del informe → 155/155; focal pantalla/API/rutas 88/88 (45 + 8 + 35). `npm run contracts:check`, `npm run build`, lint del alcance y `git diff --check` → exit 0, sin errores ni advertencias nuevas. `node --test scripts/capturar-etapa-5.test.mjs scripts/capturar-relevamiento.test.mjs` → 30/30; lint de scripts/fixture exit 0. Captura del grupo: dos fallos detectados y corregidos (selector «Cerrar» y navegación durante recarga); delta de seis casos → 6/6, exit 0 (`.local/t21c/capture-fixes.log`). Tras corregir SPEC, captura completa con el fixture fiel al contrato → 34/34, exit 0 (`capture-real-final.log`); caso móvil de Falló el alta con el menú abierto → exit 0 (`capture-retry-menu-final.log`). Resultado único: 32 PNG / 16 pares de ficha real, más dos PNG del diálogo aislado de módulos T20d; `node scripts/capturar-relevamiento.mjs --verify --group recuperar,plataforma-marco,plataforma-organizaciones,plataforma-organizacion,plataforma-cuentas --out docs/design/capturas/etapa-5/lado-a-lado` → 100 PNG / 50 pares acumulados, exit 0 (`capture-verify.log`). Activa, Dueños pendiente y Suspendida se entregaron a 1440/390 con los comandos `vivo.mjs etapa-5/organizacion-{estado-activa,duenos-sin-dueno-todavia,estado-suspendida}-{escritorio,movil} --vite http://127.0.0.1:5191`; galería con todos los estados y confirmaciones. **Diferencias comunes conservadas:** `Page.moreActions` abre el menú común en teléfono; `Banner`, `Surface`, fechas relativas, altura de pestañas e íconos mantienen sus piezas. Historial sin cantidad por decisión 3; las tres pestañas siguientes quedan para T21d. Las referencias móviles base se identifican en el manifiesto y la galería; no se inventa un tablero. Decisiones 34–35. SPEC detectó dos desvíos: Historial debía mostrar `action` traducida (no `summary` técnico) y Reintentar debía ser principal. Rojo dirigido 4/4 → verde 4/4 y ficha 45/45; build y lint focal exit 0 después del delta (`spec-fix-{red,green-focal,green-page,build}.log`). Fixture técnico: rojo 1/1 → verde, sin cambios `{}` según DTO/Domain. Menú de Falló el alta móvil: rojo por no abrirlo y por esperar el título del fondo oculto → verde 30/30. ReSPEC PASS. CALIDAD detectó que navegar a otra ficha ya cacheada podía conservar la confirmación anterior y cruzar su callback de versión/estado; rojo de navegación 3/3 → verde 3/3; cierre de ficha y rutas 83/83 (48 + 35), build/lint/diff-check exit 0 (`navigation-{red,green-focal,green-final,build}.log`). El contenido local se desmonta por id. Activa 1440/390 recapturada sin cambios visuales (`capture-navigation-final.log`), verify 100/50 vigente. ReSPEC y ReCALIDAD finales PASS, sin hallazgos abiertos. Front `ee0a665`, main, sin push. Detector central `realinear-docs --detectar` → auto 0 / modelo 1 / humano 0, exit 1 (`20261005-2113.md`, «Para vos: Nada»); revisión del detector pendiente para la puerta de etapa, no se declara cierre.

Commit `feat(plataforma): la ficha de una organización`.

#### T21d · Ficha de una organización: Módulos, Dominio verificado y Página pública

**Archivos:** `src/areas/platform/tenants/tabs/{OrganizationModulesTab,OrganizationDomainTab,OrganizationPublicPageTab}.tsx` y sus tests.

**Copiá de:** §3 y §11; `CompanyMembersTab.tsx`; `ReasonDialog`; `publicSiteHost`.

- [x] **Rojo:**
  - Módulos: «estado y origen de cada módulo; Prender, Extender prueba (con "Hasta") y Apagar mandan lo suyo con motivo y versión (0 sin fila)»; «sin módulos, según "Para decidir" 2»; los gates con un catálogo de prueba (`TestFeatures` en MSW), nunca datos productivos;
  - Dominio verificado: «Pendiente con su registro TXT, "Verificar" directa y "Quitar" con motivo»; «"No encontramos el registro TXT…" en un aviso rojo»; «sin dominios, "Sin dominio de correo"»;
  - Página pública: «la dirección, el estado y el motivo; Despublicar y Permitir publicar con motivo»; «"Ver página" no aparece hasta la Etapa 7» (decisión 8 de este plan);
  - «la versión que responde Prender, Extender, Apagar, Verificar, Despublicar o Permitir publicar es la base de la acción siguiente sobre ese módulo, dominio o página»;
  - «Dominio verificado y Página pública se piden recién al abrir su pestaña y no se muestran sin tenants.manage» (decisión 6);
  - «una organización cerrada no tiene ⋮ en ninguna pestaña»; axe; 390.
  **Evidencia T21d (2026-10-05):** `npm test -- src/areas/platform/tenants/tabs/OrganizationModulesTab.test.tsx src/areas/platform/tenants/tabs/OrganizationDomainTab.test.tsx src/areas/platform/tenants/tabs/OrganizationPublicPageTab.test.tsx --maxWorkers=1` → rojo inicial 9/9 por pestañas ausentes (`.local/t21d/red.log`), verde inicial 9/9. ExpiryRequired bajo Hasta: rojo 1/1 (`red-expiry.log`) → verde en cierre de feature. Captura detectó la falta de filas móviles y counts privados: rojo dirigido 3/3 a390 y 1/1 de cantidades → verde 4/4 (`red-mobile.log`, `red-private-counts.log`, `green-mobile-final.log`). Cancelación PUT/DELETE: rojo 3/3 por callback/retry viejo → verde 3/3 (`red-lifetime.log`, `green-lifetime.log`). Capturador: rojo 2/2 por rutas/fixtures ausentes, delta rojo 1/1 por catálogo visual vacío y 1/1 de expectativa heredada T20d → cierre 35/35, incluidos tres tests de vivo (`capture-final-green.log`).
- [x] **Verde** y **capturas** (grupo `plataforma-organizacion`): los Tweaks «página» y «dominio», Módulos, y cada confirmación. **Evidencia T21d:** cierre de ocho suites de feature, diálogos y hooks → 179/179 (`.local/t21d/green-feature-final.log`); contratos/build/lint focal exit 0 (`contracts.log`, `build-final.log`, `lint.log`); scripts/fixtures lint exit 0 (`lint-capture-final.log`). Captura inicial parcial exit 1 por TXT/motivo ocultos a390, corregida con ListRow común; recaptura dirigida 6/6 y grupo completo 62/62, exit 0 (`capture-mobile-fix.log`, `capture-final.log`): 32 PNG T21c más 30 PNG T21d, 31 pares reales, sin diálogo aislado de módulos. `node scripts/capturar-relevamiento.mjs --verify --group recuperar,plataforma-marco,plataforma-organizaciones,plataforma-organizacion,plataforma-cuentas --out docs/design/capturas/etapa-5/lado-a-lado` → 128 PNG / 64 pares acumulados, exit 0 (`capture-verify.log`); galería y comandos vivo entregados para Dominio pendiente, Página despublicada y Extender prueba a1440/390. **Diferencias comunes conservadas:** filas ListRow derivadas de columnas para pestañas móviles no dibujadas; cinco rótulos de Tabs superpuestos y dirección pública truncada ante estado largo; foco del primer campo y tamaños de ReasonDialog tal cual. Dominio/Página pública sin cantidades por decisión 3, catálogo sólo de prueba, Ver página pendiente E7. Decisiones 36–37. SPEC detectó motivo de otro GET mezclado con la versión propia: rojo focal 1 falló / 1 pasó → verde 2/2; pestaña Pública 19/19 y build/lint/diff-check exit 0 (`spec-reason-{red,green-focal,green-page,build,lint}.log`). La compatibilidad usa la versión original de Query, sin sobreponerla antes de comparar. SPEC y ReSPEC del motivo PASS. CALIDAD detectó retry de Verificar entre dominios con pérdida de clave y callback: rojo A→B y mismo A → verde focal 11/11, pestaña Dominio 27/27; build/lint/diff-check exit 0. Verificación directa aislada por dominio; los efectos no dependen de callbacks per-call, y el estado pendiente incluye toda la mutación. ReSPEC detectó el 409 terminal sin aviso al agotar las esperas: rojo 2/2 → verde focal 6/6, Dominio final 31/31; build/lint/diff-check exit 0 (`spec-domain-terminal-{red,green-focal,green-suite,build,lint}.log`). Una suscripción local a la caché nativa avisa sólo en el error final: las 30 esperas internas permanecen silenciosas y el agotamiento avisa una vez tras 31 pedidos, aun sin observer. SPEC, ReSPEC, CALIDAD y ReCALIDAD finales PASS, sin hallazgos abiertos. Front `a55ee7d`, main, sin push. Los focales no se suman a los 179 anteriores. Detector central `realinear-docs --detectar` → auto 0 / modelo 1 / humano 0, exit 2 (`20261005-2202.md`, «Para vos: Nada»); revisión pendiente para la puerta de etapa, no se declara cierre.

Commit `feat(plataforma): módulos, dominio y página pública de una organización`.

**Hecho cuando** (T21) la lista y la ficha coinciden con `Organizaciones`, `Organizacion`, `M-Organizaciones` y `M-Organizacion` en todos sus estados, con datos reales.

### T22 · Cuentas y operadores

#### T22a · Cuentas: la lista e «Invitar operador»

**Archivos:** `src/areas/platform/accounts/{api/accounts.ts, api/operators.ts, columns.tsx, errors.ts, components/InviteOperatorDialog.tsx, pages/AccountsPage.tsx}` y sus tests; `AGENTS.md` y `CLAUDE.md` de `accounts/`; `src/locales/{es,en}/platformAccounts.json`.

**Copiá de:** §4 y §11; `UsersPage.tsx` (reenviar y revocar una invitación), `users/columns.tsx`, `InviteUserDialog.tsx`; `MultiSelect` con la explicación de cada opción; `DateText kind="relative"`.

- [x] **Primero, el back** («Para decidir» 4). La ficha de la cuenta (`GET /api/platform/accounts/{id}`) trae:
  - `lastStateChange`, con la fecha y el motivo, como R03 de las organizaciones;
  - `deletedAtUtc`, que la identidad ya guarda.

  Lleva rojo y verde de integración, contrato y `npm run contracts`. Es un commit back propio: `feat(plataforma): la ficha de una cuenta con su último estado y su baja`.
  **Evidencia T22a back:** `dotnet test --project tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/ArquitecturaBaseMultitenant.Api.IntegrationTests.csproj -- --filter-class '*PlatformAccountAdministrationTests'` → rojo real 3/5 por campos ausentes (el primer intento NU1301 del sandbox no cuenta como rojo de conducta). `dotnet test --no-restore --project tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/ArquitecturaBaseMultitenant.Api.IntegrationTests.csproj -- --filter-class 'ArquitecturaBaseMultitenant.Api.IntegrationTests.Platform.PlatformAccount*'` → 13/13; `--no-build --no-restore` con `--filter-class '*OpenApiContractTests'` → 3/3. API build `--no-restore` → exit 0, 0 advertencias/errores; el generador informó permisos de keyring/EventLog del sandbox, sin cambios de configuración. Contrato front rojo 2/108 → 137/137 en las cuatro suites de contrato/cliente/query; `npm run contracts`, `contracts:check`, build, lint focal y diff-check → exit 0. SPEC y CALIDAD PASS. Back `5b22eee`, front `94a5726`; decisión 38, datos históricos nullable sin estimación, estado y operatorVersion intactos. Listado y ficha visual siguen en las casillas siguientes.
- [x] **Rojo** (`AccountsPage.test.tsx`):
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
- [x] **Verde** y **capturas** (grupo `plataforma-cuentas`): la lista, cada menú, «Invitar operador» vacío, con permisos y con cada error, «Dar de baja» con y sin bloqueo, y el teléfono.

  **Evidencia T22a front (`875df83`; reparación T21 `ed2a3c5`):** rojo de banda 1/19 antes de implementar; rojos dirigidos de evidencia fresca/voz/abandono (5), estado y permiso (2), reenvío que revivía (1), e invitación corregida (3). SPEC detectó el desmontaje del formulario de baja durante una relectura: dos rojos reales → 4/4 focales; ReSPEC PASS. CALIDAD detectó avisos de errores tardíos tras abandonar cuatro peticiones: diez rojos reales, controles activos 3/3 y verde focal 20/20; ReCALIDAD PASS. Los logs intermedios con fallos se conservan y no se declaran verdes. `npm run test -- src/areas/platform/accounts` → 70/70 (63 página + 7 HTTP, `.local/t22a/front-late-rejection-feature-green.log`); rutas 35/35 y patrones/i18n/formato/estructura verdes en la tanda que aún fallaba el puntero T21; reparación de éste con `npm run test -- src/test/harness.test.ts` → 7/7. Registro de dominio único y sus dos consumidores → 55/55. Build final, `contracts:check`, lint focal sin advertencias y diff-check → exit 0. `node --test scripts/capturar-etapa-5.test.mjs scripts/capturar-relevamiento.test.mjs` → 36/36; `vivo.test.mjs` → 3/3; fixture lint → exit 0. Captura inicial parcial 8/10, exit 1 por espera de heading oculto al abrir MultiSelect, corregida con espera del menú accesible y datos móviles literales; segundo parcial 10/10, grupo completo 36/36 PNG / 18 pares, exit 0 (`captures-full.log`). Verify de los grupos implementados → 162 PNG / 81 pares acumulados, exit 0 (`capture-verify.log`); conserva los 238 casos fuente y reemplaza el diálogo aislado T20d de baja por la lista real. Capturas y vivo entregados a1440/390 para lista, permisos, menú, ambas bajas y 409. Decisiones 39–42; diferencias comunes y límite de axe registrados abajo.

Commit `feat(plataforma): cuentas y la invitación de operadores`.

#### T22b · Ficha de una cuenta

**Dato resuelto por el usuario (2026-10-06):** antes de la ficha se agrega `primaryEmail` nullable a `PlatformAccountDetailResponse`, con el mismo dato y permiso `accounts.read` que la fila del listado. Una cuenta eliminada devuelve null; los métodos de ingreso siguen tapados. Lleva test de integración, contrato regenerado y actualización de `plataforma.md`.

- [x] **Back y contrato — correo principal del detalle:** back `78e5377`, front contrato `a8c07cc`. `dotnet test --no-restore --project tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/ArquitecturaBaseMultitenant.Api.IntegrationTests.csproj -- --filter-class '*PlatformAccountAdministrationTests'`: tres rojos reales por campo ausente → **8/8** HTTP/PG; persona e invitación con solo `accounts.read`, sin principal y eliminada histórica con copia persistida, Email/Google tapados. `*OpenApiContractTests`: **3/3**. `npm test -- src/test/platform-contract.test.ts src/shared/api/contracts.test.ts src/shared/api/httpClient.test.ts src/shared/api/queryClient.test.ts`: dos rojos de contrato → **138/138**. Build API (0 advertencias/errores), `npm run contracts`, `contracts:check`, build front, lint de las dos rutas y diff-check, **EXIT 0**. SPEC y CALIDAD frescos PASS; evidencia `.local/t22b-back/` en ambos repos. `plataforma.md` refleja el mismo dato/permiso y null al eliminar. No cambia reader, ruta, permiso, persistencia ni versiones; la UI se verifica aparte.

**Archivos:** `src/areas/platform/accounts/{pages/AccountPage.tsx, tabs/AccountMethodsTab.tsx, tabs/AccountOrganizationsTab.tsx, tabs/AccountHistoryTab.tsx}` y sus tests.

**Copiá de:** §5 y §11; `CompanyDetailPage.tsx`; `List` (métodos); `useCursorList` (historial).

- [x] **Rojo** (`AccountPage.test.tsx`): módulos y cliente ausentes (`red.log`), banda ausente con stub lazy (`red-page.log`) y selección equivalente que dejaba Guardar habilitado (`red-permission-order.log`). Los errores de entorno no se contaron como rojo; logs y EXIT preservados en `.local/t22b/` del front.
  - «la banda y las acciones de cada estado del Tweak (Activa, Suspendida, Necesita recuperación, Baja pedida, Baja iniciada por la plataforma, Eliminada)»;
  - «los carteles de cada estado» (según «Para decidir» 4 por la suspensión y la eliminación); «"Ver recuperaciones" lleva a /plataforma/recuperaciones»;
  - «Métodos: valor tapado, tipo, Principal, Administrado por y "Ya no sirve para ingresar"; vacío»;
  - «Organizaciones con rol, desde y estado de la membresía; enlace a la ficha; vacía»;
  - «Historial de seguridad por cursor; no se muestra sin audit.read»;
  - «404 y error de carga»; axe; 390 (barra, ⋮, pestañas y filas de Métodos como `M-Cuenta-Plat`).
- [x] **La variante de un operador,** como la dibuja la v111 («Para decidir» 1): `AccountPage.test.tsx` y API → **49/49**, con PUT/DELETE reales, versión propia, misma clave al reintentar, nueve permisos e invitaciones. Guardas de rutas/contratos/i18n/estructura/punteros → **170/170**. El indicador copia el preset de List de16 frente al SVG18 del tablero, sin medida puntual ni cambios de shared.
  - la pestaña «Permisos»;
  - «Cambiar permisos»: `PUT .../operator`, con `operatorVersion`, el motivo y la versión de la respuesta como base;
  - «Quitar operador»: `DELETE .../operator`, con el 409 `LastManager` en la franja;
  - la ficha de una invitación de operador.

  Sus tests van en `AccountPage.test.tsx`.
- [x] **Verde** y **capturas** (grupo `plataforma-cuenta`): cada estado y cada pestaña, y el teléfono. **Evidencia T22b:** Page/API final **52/52** (`quality-phone-green-final.log`), regresión del listado **63/63**, guardas **170/170**; contratos, build, lint focal y diff-check EXIT0. Lint global EXIT0 con advertencias heredadas fuera del alcance. SPEC final PASS tras retirar literal18 a preset16. CALIDAD encontró asociación ambigua por nombre (dos rojos y control único → verde) y fixtures que no copiaban producción: cinco rojos de conducta → capturadores **44/44**; Page390 rojo por tres disponibles → dos disponibles y uno no disponible, verde52/52. El build intermedio2 por UTC nullable se conserva como fallido; reparación con constantes UTC compartidas, build final0. Captura final completa **32/32 PNG / 16 pares** a1440/390, verify acumulado **194 PNG / 97 pares**, EXIT0; conserva238casos fuente/lienzo intacto. Galería y vivo entregados para persona, invitación, métodos/historial y operador/permisos/quitar. Evidencia `.local/t22b/` del front; diferencias comunes/datos en decisión44 y «Para decidir». Detector de docs inicial0auto/2modelo/0humano: corregida línea de fixture en árbol; final0auto/1modelo/0humano, EXIT2 y consumo0tokens (`20261006-0930.md`). El símbolo restante sí existe como función local en OrganizationsPage38; falso positivo heredado documentado, sin cambiar arnés ni ignorados. Relectura final de CALIDAD PASS, ambos P2 cerrados; commit front `d4d30c0`, main sin push.

Commit `feat(plataforma): la ficha de una cuenta`.

**Hecho cuando** (T22) Cuentas y su ficha coinciden con `Cuentas`, `Cuenta-Plat`, `M-Cuentas` y `M-Cuenta-Plat`, con datos reales, y un operador puede invitar a otro.

### T23 · Recuperaciones, Documentos legales, Auditoría y Configuración

Cada parte va en su commit.

#### T23a · Recuperaciones

**Archivos:** `src/areas/platform/recoveries/{api/recoveries.ts, columns.tsx, errors.ts, components/ReviewRecoveryDialog.tsx, pages/RecoveriesPage.tsx}` y sus tests; `AGENTS.md` y `CLAUDE.md`; `src/locales/{es,en}/platformRecoveries.json`.

**Copiá de:** §6; `UsersPage.tsx`; `DetailGrid`; `useDialogFailure`.

- [x] **Rojo:** «la banda con "{n} pedidos · {m} pendientes" y "Sin pedidos"»; «el filtro Estado con cantidades y la búsqueda»; «la fila con y sin cuenta»; «Revisar: la grilla, el cartel según haya cuenta, el motivo obligatorio; Aprobar apagado sin cuenta; Rechazar y Aprobar mandan versión y motivo con Idempotency-Key, y la versión que responden es la base»; «cada 409 del back en la franja del diálogo»; «Ver detalle de un resuelto, con "Cerrar"»; «sin pedidos: "No hay pedidos de recuperación", sin pie»; «1 y 50 filas»; axe; 390. **Evidencia:** `npm test -- src/areas/platform/recoveries/api/recoveries.test.ts src/areas/platform/recoveries/pages/RecoveriesPage.test.tsx`, módulos ausentes EXIT1 inicial; luego rojos reales de vida, borrador, elección y clave nativa conservados en `.local/t23a/red-*.log`. La captura encontró Pagination indebido aunque el primer verde pasaba: prueba corregida contra combobox real y «0 de 0», rojo1 → verde4/4 del vacío, sin coincidencias y 1/50. No se cuentan errores de entorno como rojo ni cortes incompletos como verde.
- [x] **Verde** y **capturas** (grupo `plataforma-recuperaciones`): los tres casos del Tweak, un pedido resuelto, los errores y el teléfono. **Evidencia:** comando Page/API anterior **46/46** (`green-complete.log`), antes del último delta de footer; `npm test -- src/areas/platform/recoveries/pages/RecoveriesPage.test.tsx -t 'sin pedidos: banda|sin coincidencias|filas conserva pie'`, **4/4** del delta, EXIT0 (`green-empty-footer.log`; focales superpuestos, sin suma). Guardas de rutas/contrato/i18n/estructura/formatos **179/179**, httpClient **9/9** y hook nativo **18/18**; `npm run contracts:check`, `npm run build`, lint de rutas propias y `git diff --check`, EXIT0. Lint global EXIT0 sólo con advertencias heredadas externas. `node --test scripts/capturar-etapa-5.test.mjs scripts/capturar-relevamiento.test.mjs`: **47/47** después de tres rojos del adaptador. `node scripts/capturar-etapa-5.mjs --manifest-only`:238 casos fuente; `node scripts/capturar-relevamiento.mjs --app-url http://127.0.0.1:5191 --group plataforma-recuperaciones --out docs/design/capturas/etapa-5/lado-a-lado`: **16PNG/8pares**, EXIT0, más recaptura final de ambos vacíos. `--verify` de grupos acumulados: **210PNG/105pares**, EXIT0. Pares1440/390, galería y comandos vivo entregados para lista/revisión/sin cuenta/resueltos/errores/vacío. SPEC y CALIDAD frescas PASS, sin hallazgos abiertos; commit front `76cde18`, main sin push. Evidencia `.local/t23a/`; decisión45 y diferencias comunes en «Para decidir». Detector `realinear-docs --detectar`:0auto/1modelo/0humano, EXIT2, consumo0tokens (`20261006-1008.md`), falso positivo heredado de función local OrganizationStatusDialog38 verificado; no se cambia el arnés para forzar verde.

Commit `feat(plataforma): recuperaciones`.

#### T23b · Documentos legales

**Archivos:** `src/areas/platform/legal/{api/legalDocuments.ts, errors.ts, pages/LegalDocumentsPage.tsx, pages/PublishLegalVersionPage.tsx}` y sus tests; `AGENTS.md` y `CLAUDE.md`; `src/locales/{es,en}/platformLegal.json`.

**Copiá de:** §7; `RoleEditorPage.tsx` (pantalla propia, «Cancelar», «¿Salir sin guardar?»); `List`; `DateField`; `Select`; `useIdempotentMutation`.

- [x] **Rojo:** «la lista con la versión vigente y su fecha; el resumen»; «el ⋮ abre el editor con ese documento»; «el editor: el cartel, los cuatro campos, un campo de texto por cultura habilitada del catálogo, los errores del tablero, "Cambios sin guardar" y "¿Salir sin guardar?"»; «Publicar confirma y manda kind, effectiveOn y contents con Idempotency-Key; vuelve a la lista con el aviso»; «la fecha anterior a hoy, con el texto del back debajo de "Vigente desde"»; axe; 390 (lista como `M-Legales` y «+»). **Evidencia T23b:** `npm test -- src/areas/platform/legal/pages/LegalDocumentsPage.test.tsx src/areas/platform/legal/pages/PublishLegalVersionPage.test.tsx` → seis rojos dirigidos EXIT1 antes de implementar (`.local/t23b/red-pages.log`). Rojo de validación tardía y rojo de Contenido apilado → delta2/2 verde; rojo del router lazy. Vida nativa detectó el bloqueo por una publicación abandonada: primer corte9/15, arreglo dirigido15/15, más cuatro casos pendientes de URL/permisos en el final. Se conservan los cortes fallidos y errores de patrón de prueba, sin declararlos verdes ni sumarlos.
- [x] **Verde** y **capturas** (grupo `plataforma-legales`): los dos estados del Tweak, los errores, la confirmación y el teléfono. **Evidencia T23b:** `npm test -- src/areas/platform/legal` → **54/54**, seis archivos, incluidas19 pruebas de vida nativa (`feature-final.log`, EXIT0); guardas de rutas/contrato/estructura/i18n/formatos/gate **161/161**, EXIT0. `npm run build`, `npm run contracts:check`, lint focal y `git diff --check` → EXIT0; lint global final EXIT0, sin advertencias propias y con las heredadas externas conservadas. Capturador: tres rojos →3/3; `node --test scripts/capturar-etapa-5.test.mjs scripts/capturar-relevamiento.test.mjs` →50/50 antes del delta Link navegable; rojo del rol →delta3/3. `node scripts/capturar-etapa-5.mjs --manifest-only` →238 casos. `node scripts/capturar-relevamiento.mjs --app-url http://127.0.0.1:5191 --group plataforma-legales --out docs/design/capturas/etapa-5/lado-a-lado` → **10PNG/5pares**, EXIT0 después de reparar columnas y DialogBody. Verify acumulado → **220PNG/110pares**, EXIT0; galería y pares1440/390 entregados con `vivo.mjs etapa-5/legales-{caso}-{escritorio,movil} --vite http://127.0.0.1:5191`. Los primeros intentos fallidos de captura se conservan; el final completo es `captures-full.log`. SPEC detectó P2 de relectura fallida con datos cacheados: rojo4/4 → `npm test -- src/areas/platform/legal/pages/LegalRefetch.test.tsx src/areas/platform/legal/pages/LegalRoutes.test.tsx` **12/12** del delta, EXIT0; build/lint/diff del delta0. El integral54/54 es anterior: no se declara un integral58 ni se suman los solapados. Lista, fecha, textos y operación pendiente permanecen. Segundo P2 SPEC: Atrás con confirmación superponía diálogos; rojo7/7 y Retry viejo rojo1 → `npm test -- src/areas/platform/legal/pages/LegalBack.test.tsx src/areas/platform/legal/pages/PublishLegalVersionPage.test.tsx src/areas/platform/legal/pages/PublishLegalVersionPage.lifetime.test.tsx src/areas/platform/legal/pages/LegalRefetch.test.tsx` **49/49**, EXIT0 (8 nuevos y 41 previos), build/lint final vacío/diff0. El primer lint con dos advertencias propias se conserva como tal; se eliminaron sin disables. Tampoco se declara un integral66: los deltas se solapan. SPEC y CALIDAD frescas finales PASS, sin hallazgos abiertos; commit front `5817985`, main sin push. Decisiones46–47 y diferencias comunes abajo; evidencia `.local/t23b/`. Detector `realinear-docs --detectar`:0auto/1modelo/0humano, EXIT2, consumo0tokens (`20261006-1115.md`); mismo falso positivo heredado de OrganizationStatusDialog38 verificado, sin cambiar el arnés.

Commit `feat(plataforma): documentos legales`.

#### T23c · Auditoría

**Archivos:** `src/areas/platform/audit/{api/securityEvents.ts, pages/SecurityAuditPage.tsx}` y sus tests; `AGENTS.md` y `CLAUDE.md`; `src/locales/{es,en}/platformAudit.json`.

**Copiá de:** §8; `AuditPage.tsx`; `DetailGrid`.

- [x] **Rojo:** «los filtros sin cantidades; "Últimos 30 días" por defecto; "Limpiar" solo si algo cambió»; «las columnas; "Qué pasó" abre el detalle»; «"Cargar más" pide la tanda siguiente con el cursor y los mismos filtros»; «el detalle con su grilla y "Cerrar"; en el teléfono, la hoja»; «sin coincidencias»; «403: "No tenés permiso"»; es-AR y en-US (fechas); axe; 390. **Evidencia T23c:** `npm test -- src/app/routes.test.tsx -t T23c` → rojo1 por ruta ausente; Page `-t 'sólo audit.read'` → rojo de montaje y, con skeleton, rojo de conducta (cada corrida EXIT1). Logs `.local/t23c/red-{routes,page,behavior}.log`. Los fallos intermedios de transform, fixture y expectativas se conservan como fallidos, sin declararlos rojos funcionales ni verdes.
- [x] **Primero, el back** («Para decidir» 4): `GET /api/platform/audit/organizations`, con las organizaciones que aparecen en la auditoría de seguridad, para el filtro «Organización». **Evidencia:** back `d6f107f`, diez rutas propias; rojo HTTP/PG: diez fallos404 y uno de setup previo al GET (EXIT2), conservado en `.local/t23c-back/red-route.log`. Opciones11/11 verdes dentro del focal41/42 (EXIT2, única expectativa de casing OpenAPI corregida); retest OpenAPI1/1 EXIT0, sin declarar integral42/42. Arquitectura66/66 y buildApi0advertencias/0errores, EXIT0; SPEC y CALIDAD frescas PASS. Contrato front `dfe15d5`: tres rojos por schema anterior → `npm run contracts` → `npm test -- src/test/platform-contract.test.ts`112/112; build, contracts:check y lint focal EXIT0. DTO `{ id, name }`, filtros y fuente global documentados en decisión48.
  - Copiá `/api/organization/audit/actors`.
  - Lleva `[HasPlatformPermission]` de auditoría, el inventario de rutas, rojo y verde de integración, y el contrato.
  - Es un commit back propio.
- [x] **Verde** y **capturas** (grupo `plataforma-auditoria`): la lista, un filtro, el detalle y el teléfono con su hoja. **Evidencia T23c:** `npm test -- src/areas/platform/audit/pages/SecurityAuditPage.test.tsx src/areas/platform/audit/api/securityEvents.test.ts` → **15/15**, EXIT0 (`green-feature-final.log`); rutas **35/35** en la corrida anterior al último delta sólo de tests, guardas de i18n/estructura/accesos/contrato/cursor/grilla **125/125**, EXIT0. Build, contracts:check, lint focal y diff-check0. Capturador rojo por harness/respuestas, luego radio móvil que cierra automáticamente: delta3/3. Colisión global de ids con Auditoría de organización: corrida52/53 fallida y rojo focal → `node --test scripts/capturar-etapa-5.test.mjs scripts/capturar-relevamiento.test.mjs` **54/54**, EXIT0, sin cambiar ids de E5/vivo ni lienzo. `node scripts/capturar-etapa-5.mjs --manifest-only` →238 casos fuente. `node scripts/capturar-relevamiento.mjs --app-url http://127.0.0.1:5191 --group plataforma-auditoria --out docs/design/capturas/etapa-5/lado-a-lado` → **6PNG/3pares**, EXIT0 (`captures-final.log`); verify acumulado **226PNG/113pares**, EXIT0. Listado, filtro y detalle entregados a1440/390 con vivo; referencia móvil filtrada base declarada. SPEC y CALIDAD frescas PASS, sin hallazgos abiertos; commit front `e91ba8c`, main sin push. Decisiones48–49 y diferencias comunes abajo; evidencia `.local/t23c/`. Detector `realinear-docs --detectar`:0auto/1modelo/0humano, EXIT2 y0tokens (`20261006-1238.md`), falso positivo heredado de OrganizationStatusDialog38 verificado. MarcoT20 conserva su limitación hastaT24; no se cierra E5.

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
12. **Las filas del teléfono que no están dibujadas** (las demás pestañas de las fichas) se declaran en `DataTable.mobileRow` con `ListRow` común, a partir del rol y los datos de cada columna (`responsive.md`, como `CompanyMembersTab`); se muestran en las capturas para que el usuario las revise. `DataTable` sin esa declaración sólo oculta columnas, no genera filas de lista.
13. **El nombre de un módulo** sale de `platformTenants:modules.<clave>`; sin texto, la clave. Cada módulo que nazca suma el suyo.
14. **«Crear organización»** arranca con la zona y el idioma del operador (`/api/me`).
15. **El diálogo con motivo no tiene ✕,** como en el tablero, también cuando no es de peligro.
16. **Idempotencia:** `useIdempotentMutation` en las rutas `[Idempotent]` (ver «El contrato»).
17. **«Verificar» un dominio** manda el motivo fijo «Verificar el dominio» (el back lo exige y el tablero no lo pide), como el reintento del alta.
18. **Un 409 de estado también relee la pantalla** al cerrar el diálogo, como el de concurrencia: sin versión en las cuentas (T18b), «No se puede cambiar la cuenta a ese estado.» es lo que avisa que otro operador cambió la cuenta. Lo resuelve `useDialogFailure` en `shared/` con los códigos que le pasa cada pantalla.
19. **Verificación de versiones en acciones terminales (T18c):** rechazar o cerrar una organización y resolver una recuperación no habilitan otra transición. El test compara la versión respondida con la persistida y comprueba que la siguiente acción inválida conserva el error de estado, sin `General.ConcurrencyConflict`. En las secuencias permitidas, la acción siguiente con la versión respondida termina sin 409.
20. **Reintento del pedido de recuperación (T19):** después de verificar el código, un error de red al registrar el pedido conserva la prueba en memoria y reintenta solo ese pedido, con su misma `Idempotency-Key`. No vuelve a verificar el código. Las otras dos mutaciones conservan también su clave en sus propios reintentos de red, según el hook común.
21. **Correo repetido en el contrato real (T19):** `AccountRecoveryRequestValidators` devuelve `Validation.Failed` con `errors.previousEmail`, usando `ValidationTexts.RecoveryMethodRepeated`; no existe un código de dominio `Platform.AccountRecovery.RecoveryMethodRepeated`. El front vuelve al paso del correo nuevo y asigna ese error a su campo `email`, sin comparar mensajes. La tabla de «El contrato» se corrigió con esa evidencia.
22. **Estado de los datos de captura de Recuperar (T19):** sus tableros dibujan «Reenviar código» disponible; el arnés usa `resendAfterSeconds: 0` para comparar ese mismo estado. La pantalla respeta la espera que devuelve la API y sus tests prueban la cuenta regresiva de 60 segundos. El capturador aparta el puntero antes de fotografiar para que un clic del paso anterior no introduzca un hover accidental en el siguiente.
23. **Clave de idempotencia al corregir un envío (T19):** la revisión de calidad reprodujo un `Request.IdempotencyKeyReused` al cambiar el correo después de perder la respuesta. `useIdempotentMutation` suma la opción tipada `sameOperation(previous, next)`: compara explícitamente todos los datos y el recorrido para conservar la clave solo en el reintento de esa misma operación. Recuperar pasa el cuerpo completo de sus tres pedidos, incluido el código; al volver o pedir otro código, cambia el identificador del paso, sin cambiar el nonce. Los consumidores que no usan la opción conservan su conducta. No hay un patrón equivalente en ArquitecturaBaseFront. La convención queda en `datos-y-api.md`, con tests de conducta del hook y de Recuperar; las piezas visuales y sus medidas se usan tal cual.
24. **Composición de permisos de `/api/me` (T20a):** `ProfileEffectivePermissionsBuilder` reúne los permisos de organización, empresa y Plataforma según el acceso, con `IPermissionService` e `IPlatformPermissionService`. Esa responsabilidad sale de `ProfileSnapshotBuilder`, que conserva ocho dependencias de negocio; el lector global de accesos sigue en la misma clase y no se amplía su lista blanca. La asignación de Plataforma se lee sin caché. El nuevo campo requerido se incorpora como lista vacía en los fixtures existentes de Consumer y Business al regenerar el contrato del front; los permisos concretos del operador se suman en T20b.
25. **Captura del marco antes de las páginas (T20b):** el modo `frame=platform` de `src/test/visualApp.tsx` dibuja `PlatformLayout` real con el contenido vacío, bajo la ruta del manifiesto. Permite comprobar ambos paneles y las migas antes de que T21–T23 monten las pantallas; no agrega placeholders a la app. Las capturas dicen «contenido pendiente T21–T23». La vista de 390 de T20b muestra el estado parcial: la barra y Más se completan en T20c. Los íconos de grilla y documento previstos en T20d se copian ahora del tablero porque los enlaces ya los necesitan; el ícono común de llave se conserva tal cual.
26. **Índice protegido con páginas pendientes (T20b):** el constructor acepta `platformPages` parcial y monta siempre la guarda del índice, aunque no haya páginas. Un operador sin permiso de ninguna sección recibe `ForbiddenPage` en la ruta real; uno autorizado conserva el marco con `Outlet` vacío hasta que se incorpora la página. Las demás rutas se montan solo cuando tienen página. `PlatformHomeRoute` vive junto a `HomeRoute` en `RouteViews.tsx`; el constructor queda en `routes.tsx`, sin desactivar la regla de Fast Refresh. Los fixtures que leen también Node y `vivo.mjs` conservan imports solo de tipos y datos de prueba explícitos.
27. **Secciones móviles y Más canónico (T20c):** `bottomSections` referencia los enlaces del manifiesto y declara sus SVG móviles; `visibleNavigation` aplica `canOpen` a esas mismas referencias. El marco obtiene la sección actual del panel de Gestión, también en las fichas. `moreTo` monta `/plataforma/mas` bajo el acceso Plataforma, separada de las nueve páginas con permiso de T20b; `/mas` del operador en teléfono redirige con `replace` a la ruta canónica y en escritorio vuelve al inicio. Más no necesita permiso de una sección: incluso un operador sin Administración puede abrir Mi cuenta y Salir. Personal y Empresa conservan Inicio y Más. La convención queda en las fichas de permisos y responsive del front. El capturador usa el router real para Más móvil y su propio rótulo de tamaño, sin heredar el estado parcial de escritorio.

---

28. **Un único error de fecha en el diálogo (T20d):** `DateField.invalidMessage` es opcional: omitido conserva su mensaje común, un string permite el texto exacto del tablero y `null` cede la alerta a `FormField`. ReasonDialog evita duplicar el error tras blur, click o Enter y conserva validez y descripción accesible. Es un ajuste de lógica para cumplir §11, sin cambiar medidas, clases ni conducta por defecto de otros consumidores. Queda documentado y probado en la ficha de formularios. El callback genérico de éxito del diálogo recibe la respuesta original, también `void`, antes de cerrar; el reintento global ejecuta el mismo callback.
29. **Captura aislada de piezas y selección del catálogo (T20d):** `PlatformReasonCapture` monta ReasonDialog real sobre el marco de Plataforma para seis estados existentes, con fondo explícitamente pendiente T21–T23. No agrega pantallas de producto. El catálogo incorpora sólo adaptadores implementados y compara grupo/id al excluir pendientes, porque hay ids compartidos con etapas anteriores. Los fixtures siguen siendo datos puros para Node. Al implementar cada pantalla se reemplaza este adaptador por la ruta real; la fuente móvil que no dibuja una confirmación conserva la nota de referencia base.

30. **Fecha civil de un módulo sin ampliar RLS (T21a):** `TenantFeature` conserva `expiresOn` junto al UTC técnico. La lectura global con `tenants.read` devuelve la fecha guardada y no consulta `TenantSettings` privado. La escritura con `tenants.manage` entra al alcance después de autorizar y registrar el evento, antes de abrir su UoW; dentro de ella, después de los locks, lee la zona y usa `ITimeZoneService.GetDayRangeUtc`: el final exclusivo de ese día mantiene el módulo disponible hasta terminarlo. Gates y auditoría conservan UTC. La columna civil de filas anteriores queda `null`, sin inventar qué fecha se escribió y sin alterar su vencimiento UTC; el catálogo productivo sigue vacío. Es una decisión técnica para cumplir el punto 14 sin agregar una excepción al aislamiento.

31. **Banda y consulta de Organizaciones (T21b):** la banda toma los conteos globales, independientes de búsqueda y filtros; Estado y Origen muestran los conteos cruzados de la consulta. Sin `tenants.manage`, «Ver ficha» conserva el acceso de consulta: el tablero §2 dice «siempre», por lo que «sin acciones» de la casilla se aplica a las que escriben.

32. **Respuesta antes de la relectura (T21b):** TanStack Query sigue siendo dueño de la lista. Un estado mínimo por id conserva sólo `{version, status}` de una escritura propia; la respuesta manda como base de la acción siguiente y otra lectura no la reemplaza. El éxito actualiza esa coordinación y la caché, muestra el aviso e invalida en segundo plano, para que cerrar el diálogo no espere el GET. Cerrar un conflicto descarta la coordinación de esa fila y relee.

33. **Vida del formulario y vista en vivo (T21b):** la confirmación monta un adaptador idempotente propio, con la misma vida que su formulario, como el alta y `useIdempotentMutation`. Reintentar mientras sigue montado conserva la clave del cuerpo completo; corregirlo o abrir otro diálogo inicia otra operación. Cancelar aborta el recorrido: un aviso viejo no vuelve a enviar ni acepta una respuesta ni cierra otro formulario. No hay mapas de claves ni formularios ocultos. La vista en vivo respeta `action.waitFor` y `app.now`, igual que las capturas: el segundo «Cargar más» espera la primera tanda y las fechas relativas usan la hora del caso.

34. **Dueños y último estado en la ficha (T21c):** `owners` sigue siendo la lista de asignaciones actuales del backend. Si está vacía y existe `ownerEmail` de un alta pendiente, el front representa ese contacto en una fila sin nombre, con Nunca e Invitación pendiente, sin inventar identidad ni membresía. El conteo corresponde a las filas disponibles. Después de una acción se conserva sólo su versión/estado propios y se descarta el evento anterior; hasta recibir la evidencia del nuevo estado se muestra sin dato en fecha/motivo. La vida del contenido local se delimita por id de organización: al cambiar de ficha se desmontan sus consultas, coordinación y confirmaciones, y se abandona la operación anterior.

35. **Referencia y acciones de captura (T21c):** los props del tablero conservan la fuente visual; el estado, la pestaña y la variante de la app salen del par del caso, porque un tablero móvil base puede representar otro estado. Las confirmaciones usan `action.exact` para esperar «Cerrar» sin activar «Cerrar Gestión» durante la carga. El resto de los recorridos conserva su coincidencia anterior.

36. **Coordinación por recurso en las pestañas (T21d):** las versiones y los campos mínimos de los cambios propios de módulos, dominios y página pública pertenecen al contenido local de la organización, delimitado por id. Se conservan al salir y volver a la pestaña; un GET todavía pendiente o con otra versión no reemplaza esa base. El motivo de bloqueo público sin dato en VersionResponse se muestra sólo al recibir una lectura del mismo estado y la misma versión propia; una lectura ajena no mezcla su motivo con esa base. Cada formulario POST montado posee su recorrido idempotente con comparación del cuerpo completo; Verificar directo tiene un componente local estable por dominio, con clave y ciclo propios, también ante retries de MutationCache sin observer. Los efectos de éxito comprueban la vida local, el estado pendiente usa la caché nativa de mutaciones y el feedback de negocio corresponde al error final, sin duplicar el polling ni modificar shared; PUT y DELETE usan la mutación común sin clave. La vida local de esos dos formularios impide reenvíos desde un aviso viejo y descarta callbacks tardíos tras cancelar, cambiar de pestaña o cambiar de organización, sin modificar shared. Cerrar un conflicto descarta la coordinación de ese recurso y relee.

37. **Catálogo y filas de captura administrativas (T21d):** el contrato de módulos sólo trae una clave opaca; el nombre usa `platformTenants:modules.<clave>` con fallback a la clave (decisión 13), sin inventar un DTO ni catálogo productivo. Las fichas visuales comparten dos TestFeatures con los nombres del tablero, mientras el producto conserva el catálogo vacío y oculta Módulos. El TXT pendiente usa el dominio como nombre del registro y la página sólo muestra fecha cuando está publicada, según el productor real y el tablero. Las tres filas móviles se declaran con ListRow común, a partir de los datos de sus columnas; no se fabrican tableros móviles ausentes. Las pestañas comunes con cinco rótulos largos se superponen a 390 y la dirección pública se trunca ante el estado largo: se registra la diferencia y se conserva la pieza según Para Codex.

38. **Evidencia de estado y eliminación de una cuenta (T22a):** `lastStateChange` copia R03: evento de la cuenta, tipo de estado permitido, diferencia efectiva de `Changes.status` y orden UTC/Id descendente; fecha, acción traducida y motivo nullable. Una baja personal, cancelación o eliminación cuyo evento histórico no trae diferencia no inventa evidencia ni altera productores: se conserva el último cambio efectivo o null. `deletedAtUtc` procede de la columna existente de identidad, sin migración ni backfill. Una suspendida con baja programada conserva Suspended, según la regla y los tests actuales; reactivarla devuelve PendingDeletion. No se reintroduce versión de cuenta; operatorVersion sigue perteneciendo a la asignación. La ficha front de idempotencia se precisó en `a627f63` con el patrón probado de T21d, porque un envío nuevo puede retirar los callbacks per-call de otro; no cambia producto ni shared.

39. **Invariantes transversales detectadas durante T22a:** `TenantDomainStatus` estaba registrado tanto en Dominio de Plataforma (T21d) como en Configuración de Empresa. Se extrae un registro único en `shared/format/statusTones.ts`, copiando `publicPageStateTones`, y ambos consumidores lo importan; conserva los mismos tonos, sin cambios de medidas ni de producto. Los tres punteros de Organizaciones de T21 que excedían 8 líneas se abrevian preservando sus reglas. Los tests existentes de registro único y del arnés dieron rojo; consumidores/tonos 55/55 y punteros 7/7 verifican la reparación. SPEC y CALIDAD PASS; commit front separado `ed2a3c5`, sólo las seis rutas propias.
40. **Vida de las operaciones de Cuentas (T22a):** una mutación nativa conserva su recurso y recorrido, y sus efectos comprueban la vida antes y después del envío. Reenviar recibe el éxito en el adaptador y el error final de la caché, aun si otro envío retiró el observer. Invitar también compara las variables originales con `form.getValues()` normalizado: cambiar correo o permisos abandona la operación anterior, incluso sin cancelar el diálogo. Tres tests rojos confirmaron que un Retry viejo enviaba el correo anterior y que una respuesta tardía afectaba el nuevo borrador; el delta cerró con feature 55/55, build y lint sin advertencias. No hay claves manuales, mapas de operaciones ni cambios de la pieza idempotente común.
41. **Apertura y relectura del diálogo de baja (T22a):** el primer GET exitoso después del montaje verifica la apertura; mientras no se obtiene, los datos cacheados no habilitan el formulario. Después de esa verificación, las relecturas conservan la instancia del formulario, el motivo y la franja. SPEC encontró que usar `isFetching` para ambas fases desmontaba el formulario y descartaba el rechazo de LastAdmin. Dos tests rojos → 4/4 focales → feature 57/57; ReSPEC PASS. Si la relectura trae organizaciones, muestra sus nombres y oculta el motivo; si trae cero, conserva el rechazo original sin inventar nombres. Cancelar o cambiar cuenta, acción o recorrido sigue retirando la instancia anterior.
42. **Errores tardíos de las acciones de Cuentas (T22a):** los adaptadores comprueban la vida también cuando una petición rechaza, como DomainActions. Revocar, Reenviar y las dos relecturas de baja normalizan a AbortError un error de red/5xx recibido después de cancelar, navegar o perder elegibilidad. Una operación activa conserva el error original y su aviso/reintento común; no se silencian los errores globales con meta ni se cambia shared. CALIDAD encontró esta brecha en los guards posteriores al await exitoso: diez rojos reales, controles activos 3/3, focal 20/20 y feature 70/70; build/lint/diff-check limpios. ReCALIDAD PASS.
43. **Correo principal del detalle de cuenta (T22b, usuario 2026-10-06):** `primaryEmail` nullable copia el mismo dato del listado bajo `accounts.read`; para una cuenta eliminada devuelve null. Resuelve la miga y la banda con correo completo de una invitación, cuyo nombre es null. Los métodos de ingreso conservan el enmascarado del tablero. El reader ya proyecta el principal en la fila común del listado y detalle: se reutiliza, sin otra fuente, permiso ni reconstrucción. Se verifica por integración y contrato antes de programar la ficha.
44. **Datos y piezas comunes de la ficha de cuenta (T22b):** organizaciones usa la clave `TenantAdmin` traducida como Dueño; null conserva el vacío común y no reconstruye roles privados de empresa. El override final de v111 da a `.rol` y `.rol.sis` el mismo aspecto: no se agrega una marca inexistente. Historial muestra el `summary` humano de Auditoría, sin total por cursor ni eventos que su productor no registra. El método administrado trae sólo el nombre de organización, que no es único: la fecha de membresía sólo se presenta con una coincidencia inequívoca, terminada; nombres homónimos conservan el vacío común. La vida por id/estado/tipo/permisos abandona borradores, confirmaciones y reintentos anteriores; PUT operador conserva inmediatamente su versión propia, aunque un GET posterior traiga otra. Los diálogos de cuenta se comparten localmente con el listado. La marca de permiso copia el preset16 de List, frente18 del dibujo; SPEC detectó y retiró la medida literal. Page, Tabs, MultiSelect y ReasonDialog conservan sus formas; las diferencias visuales se registran abajo.

45. **Revisión de pedidos y vida de la decisión (T23a):** `accountId` decide la existencia, no el nombre nullable; la UI nunca reconstruye los métodos tapados. La banda usa conteos globales y los filtros conservan los conteos cruzados. El sort permitido es `createdAt`; no se agregan órdenes por nombre o estado. Un POST guarda inmediatamente la versión, estado y motivo propios; una lectura vieja Pending no revive Revisar, y una lectura resuelta confirmada aporta la evidencia real sin inventar operador ni fecha. El formulario conserva motivo/foco mientras su dueño nativo local se remonta por borrador/elección: el wrapper compartido compara antes de la guarda del adaptador, así que un Retry abandonado debe conservar su dueño antiguo para no rotar la clave actual. Las claves las administra sólo el hook nativo, sin generar UUID ni mantener mapas de claves o polling en la feature. Cambiar evidencia de cuenta/métodos, permiso, URL o estado abandona el diálogo; guardas de éxito y error impiden efectos tardíos. El progreso pertenece sólo a la decisión enviada. La comparación visual detectó el pie indebido de Sin pedidos aunque el primer verde pasaba: rojo dirigido del selector real y verde del vacío, sin coincidencias y 1/50; se oculta el pie con total0. r2 usa correo nuevo conforme decisión8, sin adelantar el canal de E8 que ejemplifica el tablero. Las diferencias comunes se registran abajo.

46. **Vigencia, publicación y comparación de Legales (T23b):** `currentVersion`/`currentEffectiveAtUtc` son la evidencia vigente; una versión con `isCurrent=false` puede ser pasada o futura. La próxima se elige entre fechas posteriores al reloj, por fecha, y el número siguiente sale del máximo publicado más uno. Para comparar instantes se copia `Date.parse`/`Date.now` de Invitaciones y Empresas; todo formato sigue en `useFormat`, sin exportar otro reloj ni tocar shared. El capturador fija el reloj del navegador. La fecha del editor sigue civil en la zona del operador y los textos se generan por culturas habilitadas del catálogo, con el límite100000 del productor real. La confirmación no pide motivo y el aviso toma el número devuelto por el POST. El dueño nativo queda fuera del formulario, por vida de borrador y confirmación; cambiarlo abandona los reintentos anteriores. La captura detectó Contenido apilado en escritorio: se corrigió con rojo/verde y la grilla de RoleEditor/Versión, porque decisión12 no cambia esas columnas. Textarea sí conserva la altura común, frente a440 del dibujo. Banner/Sections siguen decisión12; diferencias comunes abajo.

47. **Una sola decisión al volver desde Legales (T23b):** la revisión SPEC detectó que Atrás del navegador con Publicar abierto podía mantener la confirmación y abrir además «¿Salir sin guardar?». `useEditPage` se conserva tal cual, como exige la ficha de formularios. La pantalla observa el bloqueo del mismo router, sin registrar otro blocker ni leer props internas del diálogo. Al bloquear la salida se abandona la publicación y se muestra sólo la pregunta de salida. «Seguir editando» conserva fecha/textos; una confirmación nueva tiene un dueño nativo nuevo y las respuestas/reintentos antiguos quedan inertes. «Salir» abandona y navega. Es una coordinación técnica local, sin nuevas medidas, cambios de shared ni cambios de producto.

48. **Opciones de organizaciones de Auditoría (T23c):** el endpoint devuelve Id y nombre requerido de las organizaciones que tienen eventos de seguridad, distintos, con el nombre actual global y orden por nombre e Id usando la collation ICU existente. Copia actores de auditoría: valida la consulta completa pero ignora su propio filtro y la paginación; conserva período, tipo, búsqueda y cuenta. Listado y opciones comparten los filtros y la definición de períodos, evitando dos interpretaciones. Usa sólo `audit.read`, sin tablas privadas, alcances ni cambios de permisos o persistencia. Un cursor de forma válida se ignora sin decodificarlo, como las opciones del patrón copiado.

49. **Ejemplos de datos de Auditoría (T23c):** la UI presenta `summary`, nombres y máscaras que recibe, conforme a la decisión24 de la E5; no reconstruye el mensaje desde `action` ni inventa eventos. Las capturas usan el ingreso por código de correo, error de código, reintento de alta y cambio de configuración que producen los servicios actuales, en lugar de los ejemplos de autenticador, bloqueo, fallo de alta y diff específico del dibujo. Los ingresos no traen IP; la IP de los eventos conserva tres octetos y la máscara del backend. Para que «Cargar más» sea realizable con `limit=25`, la primera tanda tiene25 filas y cursor real, frente a las seis ilustrativas del tablero. A390, la pieza común de filtros ofrece opciones sin buscador; el caso filtrado usa Organización, y conserva la referencia móvil base que no simula filtros. Ninguna de estas diferencias agrega conducta ni datos productivos.

    **Piezas comunes:** se conserva `Todo` en el botón de FilterBar frente a «Período: todo» de la fuente, sin alterar la opción ni su orden; DataTable reparte/trunca sus columnas como el patrón de Auditoría business (fecha162 copiada literalmente), frente al grid del tablero. El detalle usa DetailGrid y Dialog/hoja intactos, con fecha en el cuerpo y Administración en teléfono (decisiones12–13). El error de la tabla sustituye temporalmente filas con Reintentar, conserva caché y recupera filas al reintentar. Radio, padding, SVG y menú comunes quedan tal cual, según Para Codex8.

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
   - La ficha de Organización tiene cinco pestañas; la pieza común reparte las columnas a 390 y sus rótulos largos se superponen. En la fila de Página pública, el estado largo también reduce el título y trunca la dirección. **Aplicado en T21d:** piezas tal cual, sin medidas propias; capturas y decisión 37 registran la diferencia para revisión.
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
| T00 | `2ed8f13`, `ebcf3b7` | `d73f362` | Ambos repos limpios en `main` al retomar; `aspire ps` → sin AppHost; consolidación `2209be7` presente; arnés centralizado → 0 hallazgos en ambos; `ClosedStages` → `{0,1,2,3,4,6}`; `npm run contracts` y `contracts:check` → exit 0; API front → 29/29; lint sin errores y build exit 0 | — | HEAD al retomar: back `97c28f9`, front `e685df0`; lienzo v111 aprobado. Resueltos el trabajo pendiente de Diseño y el contrato desactualizado anotados en la comprobación inicial. |
| T18c | `f54bb4b` | `5ee8475` | Rojo: arquitectura 1/4, HTTP 2/6, front 13/14. Verde: arquitectura 148/148, Application 476/476, integración Plataforma 55/55, dominios 3/3 y contratos 21/21; build Api 0 advertencias/errores. Front 14/14, contracts/check, lint y build exit 0. Arnés 0 en ambos. Revisiones de especificación y calidad aprobadas. | — | Las 11 respuestas dan la versión persistida; DTO de cuenta sin version; operatorVersion preservada. Evidencia detallada en `.local/t18c/report.md`; contratos regenerados y comprobados otra vez tras el commit del back. |
| Antes de T19 | — (registro en el plan) | `a88a96a`; vivo `97e5c6b` | Catálogo rojo 6 + 2 de visibilidad + 1 de Más; verde 10/10. Captura y verify exit 0: 238 PNG / 119 pares / 10 grupos, a 1440 y 390. Sintaxis, lint de scripts y diff del índice exit 0. Spec y calidad aprobadas; arnés auto/modelo/humano 0. | Galería v111 abierta: 10 grupos; Recuperar 13 pares con estados y enlaces de Ingreso. El usuario aprobó todos los tableros capturados en `a88a96a` el 2026-10-05. | 153 estados dibujados y 85 referencias base documentadas; sin tocar lienzo ni propuesta. T19–T23 autorizadas en orden sin nuevas pausas de aprobación; fixtures de app se agregan al implementar cada pantalla. |
| T19 | — (documentación en este plan) | `23d7648` | Rojo: pantalla/enlaces/contrato ausentes; validación y cancelación; SPEC 2 y CALIDAD 4 + 3. Verde: 221/221 focales, 17/17 capturadores, build, lint del alcance, contracts:check y diff-check exit 0; arnés auto/modelo/humano 0. SPEC y CALIDAD finales aprobadas. | 26 PNG / 13 pares tablero-app a 1440 y 390, pasos, código, errores, reenvío y enlaces de ambos ingresos; galería entregada y vivo abierto. | Sin WhatsApp, nonce solo en memoria, tres POST y prueba conservada en reintentos. sameOperation diferencia cuerpos y recorridos; decisiones 20–23. Diferencias de piezas comunes registradas en la casilla Capturas, sin retocar UI ni lienzo. |
| T20a | `690cbca` | `b8e0ce6` | Rojo HTTP 2/5 por campo ausente y front 1/99. Verde back 73/73, front 171/171, API build 0 advertencias/errores; contratos regenerados/comprobados, build/lint del front y diff-check exit 0; arnés 0. SPEC y CALIDAD aprobadas. | — (contrato, sin cambios visuales) | Asignación persistida sin caché solo en Platform; Permissions planos intactos. Builder por responsabilidad, snapshot con ocho dependencias. Ocho archivos de fixtures adaptados al nuevo campo requerido; permisos concretos del operador en T20b. |
| T20b | — (marco del front; documentación en este plan) | `9776a02` | Rojo 26/63 y cuatro bordes dirigidos. Verde 252/252, incrementales 39/39 y 13/13; cierre del Minor 54/54. Capturador 18/18; build, lint y diff-check exit 0. Arnés 0. SPEC y CALIDAD finales aprobadas. | 10 PNG / 5 pares tablero-app a 1440 y 390: ambos paneles y menú; galería y comandos vivo entregados. Contenido T21–T23 y teléfono T20c pendientes explícitos. | Nueve permisos y manifiesto único, índice protegido aun sin páginas, panel activo específico. Decisiones 25–26. Diferencias comunes registradas en la casilla Capturas, sin retocar piezas ni lienzo. |
| T20c | — (marco del front; documentación en este plan) | `b7ee780` | Rojo 25 fallos UI y 1 de navegación; F5 existente verde. Verde 272/272 en 12 suites; capturador 20/20, build, lint del alcance y diff-check exit 0. Lint global exit 0, 0 errores, advertencias previas fuera del cambio. Arnés 0. SPEC y CALIDAD aprobadas. | Marco actualizado: 10 PNG / 5 pares a 1440 y 390, barra inferior y Más real; galería y comandos vivo entregados. Verify de grupos implementados: 36 PNG. | Secciones por manifiesto y permisos; detalles conservan sección, Administración vuelve a Más. Decisión 27. Indicadores del SO y correo truncado conservados como diferencias de las piezas comunes. El E2E real de recarga queda para T25. |
| T20d | — (piezas del front; documentación en este plan) | `cc9d95c` | Rojo diálogo 15 y fecha 2; extracciones 3 módulos ausentes/1 ícono, guarda 1. Verde 271/271 en 17 suites; diálogo final 19/19; capturadores 22/22; build, lint focal y diff-check exit 0. CALIDAD detectó Hasta sin obligatoriedad accesible: rojo 1 → verde 22/22, build y lint del delta exit 0. SPEC, ReSPEC y CALIDAD aprobadas; arnés 0. Lint global exit 0 con advertencias heredadas fuera del diff. | 12 PNG / 6 pares tablero-app a 1440 y 390: motivo vacío/escrito, aprobación, Hasta, baja bloqueada y 409. Galería y vivo entregados; verify 48 PNG de grupos implementados. | ReasonDialog, extracciones, host y calendario; decisiones 28–29. Fondo de las capturas pendiente T21–T23 y referencias base móviles declaradas. Foco/medidas de piezas comunes conservados; el delta required no cambia la imagen. |
| T21a | `0425176` | `9712d33` | Rojo HTTP 5/13, fecha nullable 1/1 y fecha máxima 1/1; verdes 45/45 integración, 53/53 seguridad, 3/3 auditoría, 56/56 arquitectura, 22/22 Application, 9/9 Domain. SPEC correo secundario: rojo 2/2 → verde 40/40. API build 0 advertencias/errores; front 130/130, contracts/check/build/lint focal/diff-check 0. SPEC, ReSPEC y CALIDAD PASS; arnés 0. | — (backend y contratos, sin cambios visuales) | Detalle separado de fila; fuentes globales y cursor sin LoginAudits; publicación UTC; aprobación por correo; fecha civil de módulos, decisión 30. Migration nullable sin inventar datos históricos. Lint global exit 0 con 752 advertencias previas fuera del diff. Conexión visual pendiente T21b–T21d. |
| T21b | — (documentación en este plan) | `2f37c00` | Rojo pantalla/acción, botón literal, clave al reabrir, respuesta tardía y GET retenido. Verde 194/194 (52 focales); capturadores 24/24; vivo rojo 1/1 → 3/3. Build, contracts:check, lint focal y diff-check exit 0; lint global exit 0 con 752 advertencias heredadas. SPEC y CALIDAD PASS. | 28 PNG / 14 pares a 1440 y 390: 7 y 50 filas, sin coincidencias, menús por estado, alta vacía/errores/correo inválido, motivo/validación/409; galería y vivo entregados. Verify acumulado 70 PNG / 35 pares. | Lista real; franja global, filtros cruzados, defaults del operador y cinco transiciones con versiones propias. Decisiones 31–33; cancelar termina el recorrido idempotente. Lectura sola conserva Ver ficha según fuente. Diferencias comunes registradas en Capturas; no se cambió lienzo ni UI compartida. Ficha pendiente T21c–d. |
| T21c | — (documentación en este plan) | `ee0a665` | Rojo inicial 31/31; SPEC rojo 4/4 → verde 4/4 y ficha 45/45; CALIDAD navegación rojo 3/3 → verde 3/3, ficha/rutas 83/83 (48 + 35). Base afectada 155/155 en diez suites; contratos/build/lint focal/diff-check exit 0. Capturadores 30/30. SPEC/ReSPEC y CALIDAD/ReCALIDAD finales PASS. | 32 PNG / 16 pares reales a 1440/390: cinco estados, Historial, Dueños en tres variantes y cinco confirmaciones/error. Grupo 34 PNG con dos aisladas T20d; recaptura completa exit 0, menú Falló390 y Activa finales exit 0. Verify acumulado 100 PNG / 50 pares; galería y vivo entregados. | Ficha lazy; cursor sin total, Dueños actuales/contacto pendiente y estado/versiones propios. Decisiones 34–35; vida local por id abandona operaciones viejas. Action traducida visible, Summary técnico excluido. Diferencias comunes registradas; lienzo intacto. Módulos/Dominio/Página pública pendientes T21d. Detector 0 auto / 1 modelo / 0 humano, sin Para vos; revisión pendiente para puerta de etapa. |
| T21d | — (sin cambio de API) | `a55ee7d`, `a627f63` (ficha del patrón) | Rojo inicial 9/9; móviles/counts 4/4, lifetime 3/3; verde feature 179/179, scripts 35/35. SPEC motivo: rojo 1/2 → verde 2/2, Pública 19/19. CALIDAD retry por dominio: rojos A→B/mismo A/polling → focal 11/11, Dominio 27/27. ReSPEC terminal: rojo 2/2 → focal 6/6, Dominio final 31/31. Contracts/build/lint/diff-check exit 0; revisiones finales PASS. Focales superpuestos, sin suma artificial. Detector 0 auto / 1 modelo / 0 humano, exit 2, pendiente para puerta. | Grupo Organización 62 PNG / 31 pares (32 T21c + 30 T21d); verify acumulado 128 PNG / 64 pares. Dominio pendiente, Pública despublicada, Extender y Módulos a 1440/390 con vivo; galería completa. | Decisiones 36–37: vida/versiones por recurso, motivo compatible, verificación nativa por dominio/ciclo, TestFeatures sólo de prueba; diferencias comunes registradas. Fuente v111 y shared/UI intactos. |
| T22a · back y contrato | `5b22eee` | `94a5726` | Rojo HTTP 3/5 y front 2/108; verde back 13/13 y OpenAPI 3/3, front 137/137. API build 0 advertencias/errores, contratos regenerados/comprobados, build/lint focal/diff-check 0; SPEC/CALIDAD PASS. | — (sin cambios visuales) | Decisión 38: evidencia efectiva por cuenta y fecha de identidad real; null histórico, sin nuevos productores, migración ni versión de cuenta. Se registró diagnóstico runtime keyring/EventLog del generador sin modificar configuración. |
| T22a · front | — (back y contrato en la fila anterior) | `875df83`; reparación `ed2a3c5` | Feature 70/70; rutas 35/35; consumidores/tonos 55/55; arnés 7/7. Build/contratos/lint sin advertencias/diff-check 0; scripts 36/36 y vivo 3/3. SPEC/ReSPEC y CALIDAD/ReCALIDAD finales PASS. | 36 PNG / 18 pares, 1440 y 390; verify acumulado 162 PNG / 81 pares. Galería y vivo entregados. | Decisiones 39–42; común preservado, referencias móviles base y alcance de axe explícitos. Detector `realinear-docs --detectar` en main: 0 auto / 1 modelo / 0 humano, exit 2, sin llamada al modelo ni Para vos; revisión pendiente para puerta de etapa (`20261006-0020.md`). |
| T22b (back previo) | `78e5377` | `a8c07cc` | Rojo HTTP 3/8 y contrato 2/109 → HTTP 8/8, runtime OpenAPI 3/3 y front 138/138; builds, generación, check, lint y diff EXIT 0; SPEC/CALIDAD PASS | Sin UI en este paso | Correo principal completo nullable, misma fuente/permiso del listado; eliminada null y métodos tapados. Autorización del usuario 2026-10-06, decisión 43; evidencia `.local/t22b-back/`. |
| T22b (front) | — (docs en este plan) | `d4d30c0` | Page/API52/52, lista63/63, guardas170/170; build/contratos/lint focal/diff0, lint global0 con advertencias heredadas. SPEC PASS; CALIDAD inicial dosP2, reparaciones rojo→verde y finalPASS. Capturadores44/44; detector final0auto/1modelo/0humano, EXIT2, falso positivo local verificado | 32PNG/16pares a1440/390; verify acumulado194/97, galería y vivo entregados | Decisión44, fixtures coherentes con producción, homónimos sin fecha inferida; piezas comunes conservadas y límites de datos/Phone registrados. |
| T23a | — (API existente) | `76cde18` | Page/API46/46 anterior al delta footer4/4, guardas179/179, transporte9/9, nativo18/18, pipeline47/47; contratos/build/lint propios/diff0; lint global0 con advertencias heredadas. SPEC y CALIDAD frescasPASS | 16PNG/8pares1440/390, vacío recapturado; verify acumulado210/105, galería y vivo entregados | Decisión45: ID define existencia, métodos tapados, versión/estado propios sin revivir Pending, dueño nativo por borrador/elección; diferencias comunes registradas. Detector0auto/1modelo/0humano EXIT2, falso positivo heredado verificado. |
| T23b | — (API existente) | `5817985` | Feature54/54 previo, vida19 incluida; deltas refetch12/12 y Back49/49 solapados, guardas161/161, contratos/build/lint focal/diff0; lint global0 sin advertencias propias. SPEC y CALIDAD frescas finales PASS | 10PNG/5pares1440/390; verify acumulado220/110, galería y vivo entregados | Decisión46: vigente DTO/futura por fecha, culturas habilitadas y fecha civil; publicación nativa por borrador vigente, cancelación/Retry viejos inertes. Diferencias comunes conservadas; columnas propias reparadas con TDD. |
| T23c back y contrato | `d6f107f` | `dfe15d5` | Rojo10/11por404+1setup; HTTP41/42 y retestOpenAPI1/1, sin corrida integral42/42; arquitectura66/66, buildAPI0advertencias. Contrato3rojos→112/112, build/check/lint0; SPEC/CALIDAD backPASS. | Opciones `{ id, name }` distintas y completas con filtros cruzados, sólo audit.read. | UI y6PNG/3pares completados en la fila T23c; decisión48 y ejemplos49. |
| T23c | — (back y contrato en la fila anterior) | `e91ba8c` | Rojo de ruta y conducta → feature15/15; rutas35/35 previo, guardas125/125; build/contratos/lint/diff0. Capturador54/54 después del rojo de colisión. SPEC y CALIDAD frescas PASS. Detector0auto/1modelo/0humano EXIT2 heredado. | 6PNG/3pares1440/390: lista, filtro y detalle/hoja; verify226/113; galería y vivo entregados. | Decisiones48–49; datos compatibles, 25filas/cursor real, shared intacto. MarcoT20 parcial hastaT24; no cierreE5. |
| T23d | | | | | |
| T24 | | | | | |
| T25 | | | | | |

**Para decidir que aparezca al programar** (medidas o formas del tablero que no coinciden con una pieza común, y estados que falten): se anota acá, con el tablero, el caso y la pieza, y se le muestra al usuario.

- **T23b · Legales (mostradas a1440/390):** Banner bajo la banda y dos Section tituladas «Versión»/«Contenido», según decisión12. Contenido conserva dos columnas en escritorio con grilla copiada, apiladas en móvil; la apilación inicial en escritorio era un bug propio reparado. Textarea conserva altura común frente al literal440 del dibujo; List/Surface/ListRow conservan medidas/sombra y StatusDot el tono pending de la futura. Confirmación Dialog/Body/Footer común sin X ni motivo, foco y geometría comunes intactos. M-Legales sólo dibuja la lista: editor/errores/confirmación usan referencias base explícitas; Page/FixedActions del patrón Roles, sin dibujar una pantalla nueva ni tocar shared/lienzo. Indicadores SO y Consultas E11 fuera del alcance escrito.

- **T23a · Recuperaciones (mostradas a 1440 y 390):** fecha «Pedido del» en el cuerpo según decisión12; `DetailGrid` conserva su caja, borde, relleno y grilla, frente a la grilla inline del dibujo. `Banner` warning conserva su ícono y `Dialog` el foco común del campo. `FilterBar`, `DataTable`, `Surface` y `ListRow` mantienen medidas, sombra y truncado; el vacío móvil conserva `EmptyBox`. `M-Recuperaciones` sólo dibuja la lista: diálogo, resultados, errores y vacío usan referencias base explícitas. Sin indicadores del SO ni Consultas (E11). El teléfono nuevo de r2 es un ejemplo de E8; los datos actuales usan correo nuevo conforme decisión8 y la ficha del back, con la misma presentación tapada. El pie indebido del vacío era un bug propio y se corrigió; no se lo registra como diferencia común. Shared y lienzo siguen intactos.

- **T22a · Cuentas e Invitar operador (avance mostrado a 1440 y 390):** `DateText kind="relative"` conserva «hace 1 día», minutos/horas completos y fechas con año, como ya resolvió el punto 12. `FilterBar` reparte el ancho de búsqueda y `Table` ajusta columnas/trunca correos; `MultiSelect` coloca el menú donde entra y muestra sus nueve opciones con la altura común. Las capturas muestran estas diferencias frente a `Cuentas`; las piezas de `src/shared/` permanecen tal cual. Los estados móviles de invitación usan la referencia base declarada de `M-Cuentas`, sin inventar un tablero de diálogo.
- **T22a · menú de invitación y baja bloqueada:** `RowActions` común pone el correo largo en el título de la hoja móvil y éste se corta; `ReasonDialog` común muestra la franja de bloqueo antes del cuerpo, mientras el tablero de escritorio la ubica después. Se conserva la composición común sin ajustes de medidas. La captura de baja bloqueada a1440/390 se mostró con sus comandos vivo; el menú usa la referencia móvil base declarada.
- **T22a · alcance de axe con MultiSelect:** la página/formulario con selector cerrado y el menú abierto como widget pasan axe. `axe(document.body)` con el portal del menú abierto reporta nueve avisos `region` de la pieza común. Se registra ese límite; no se presenta como chequeo global aprobado, no se desactiva la regla y no se altera MultiSelect.
- **T22b · correo principal de la ficha de invitación, resuelto:** `Cuenta-Plat` v111 pide el correo completo en la miga y la banda. El 2026-10-06 el usuario autorizó agregar `primaryEmail` nullable al detalle bajo el permiso existente `accounts.read`, con la misma fuente del listado; cuenta eliminada, null. Los métodos siguen tapados. Se agrega con integración y contrato regenerado antes de continuar con T22b y T23 en orden; no se reconstruye el correo desde valores tapados.
- **T22b · grupos y acciones comunes:** Page oculta la barra cuando sólo queda Permisos y usa DropdownMenu también a390 para Más acciones; el tablero dibuja una barra aislada y una hoja de acciones. Tabs reparte el ancho a390 y superpone rótulos largos. ReasonDialog conserva la confirmación sin cierre y la franja de rechazo encima de los botones. Se usan las piezas tal cual; capturas y comandos vivo entregados.
- **T22b · selector largo de permisos a390:** MultiSelect conserva el texto truncado y DialogContent su composición; la selección larga desborda el diálogo Cambiar permisos. SPEC confirmó que el formulario copia exactamente InviteOperatorDialog, InviteUserDialog, MemberRolesDialog y AddMemberDialog; no es una medida ni un wrapper omitido de la feature. Se registra el límite común, sin parchearlo. Su menú de escritorio conserva la altura común con los nueve permisos. La marca asignada usa preset16 de List, sin el ajuste literal18 del primer intento.
- **T22b · ejemplos frente a datos disponibles:** Beta muestra «—» en Rol porque el índice global devuelve null, sin leer sus roles privados. Historial muestra tres eventos reales: el productor no registra «Creó la cuenta» ni distingue un método agregado por tipo en su resumen. Usa los textos humanos del backend y no el código técnico de action. Los casos móviles sin ese estado dibujado conservan la referencia base explícita de M-Cuenta-Plat; no se presentan como una comparación exacta de un estado inexistente.
- **T22b · disponibilidad de WhatsApp y coherencia de datos:** el ejemplo de Métodos dibuja el teléfono disponible; `LoginMethodAvailabilityPolicy` actual sólo habilita Email/Google, con WhatsApp pendiente de E8. La fixture conserva teléfono tapado/verificado y `canSignIn=false`; la pantalla aplica el estado ya dibujado «Ya no sirve para ingresar», sin inventar canal ni texto. Mostrado a1440/390 y con vivo. CALIDAD corrigió además métodos principales verificados de operadores/invitaciones, sólo membresías activas en únicos Dueños, rol Automático y destino de organización, cronología de invitación/alta/ingreso/suspensión y la concordancia del último LoginAudit. Las capturas reproducen las respuestas actuales; no se fuerza el backend para igualar ejemplos.
