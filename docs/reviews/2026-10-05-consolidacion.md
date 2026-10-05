# Informe de cierre · Consolidación previa a la Etapa 7

Fecha: 2026-10-05. Plan: [`2026-10-03-consolidacion.md`](../plans/2026-10-03-consolidacion.md).

La consolidación está terminada y su puerta pasó. El 2026-10-05, mientras corría la puerta, el usuario tomó las
decisiones que faltaban y otras tandas las están haciendo (sección 6e del plan): figuran como «en curso».

**Sobre qué commit se verificó.** Cada verificación corrió en una copia limpia de cada repo (un worktree), sin los
cambios sin commitear de otras sesiones:

| Verificación | Back | Front | Lienzo |
|---|---|---|---|
| Build y tests del back, `HarnessTests` | `3d8af5e` | — | — |
| Build, lint, tests y contratos del front, `harness.test.ts` | `3d8af5e` (los contratos) | `3cad8b6` | — |
| E2E real | `3d8af5e` | `3cad8b6`, con el E2E de `bbb1074` | — |
| Relevamiento visual | — | `3cad8b6` (la app), con los casos de `29b70ec` | v107, `422ef2c` |
| `realinear-docs --detectar` | `3d8af5e` | `f16c537` | — |

Lo que las otras tandas commitearon después (back `8920e3b`, `94c7dea` y `639ffcd`; front `a51a27c`, `1051136`, `3ebb939`,
`2c96ca7` y `b956feb`) lo verifica el coordinador con una pasada corta.

## Qué se hizo, bloque por bloque

### 0. Línea de base y documentación

- `realinear-docs --detectar` arrancó con 579 hallazgos en el back y 250 en el front. Quedó en 0 en los dos.
- `arbol.md` de los dos repos, llevado al árbol real: back `17686e9`, front `9c010f8`.
- Los docs viejos y las enmiendas de la revisión de decisiones: back `a5c4512`, `2acb1ee`, `29684b6`, `abe9f58`,
  `3ee854a`; front `e76b689`, `a113890`, `680d6c2`, `e0073ca`, `47a5af5`.
- Cinco descartes del detector, cada uno con su motivo en `ignorados.json` (`6606b5e`).

### 1 y 1b. Bugs y pendientes de los paquetes 1 y 3

- Los bugs B1, B2, B3, B5 y B9 (ver «Bugs que se arreglaron»).
- Configuración muestra cuántos correos administra cada dominio (1.2, `7cb8f75`).
- El teléfono de la página pública se guarda en E.164 (1.3, `fe74254`).
- «Recuperar mi cuenta» ya no delata por el tiempo: el aviso a la cuenta sale después, con o sin cuenta (`e433c2c`,
  `533215d`). Los avisos de la recuperación siguen el lienzo (`8d4e1e5`, `35fd948`, `f89686c`).
- D3: un rol de una empresa puede llamarse como el de otra (`2956276`).
- D4 y D4b: agregar un correo de otra cuenta responde igual que uno libre en cada paso (ADR 0041; `8cb4830`, `b05caca`,
  `6b50777`).
- D5: la única Dueña no se puede dar de baja mientras tenga una organización no cerrada (`3e66117`).
- D6: el tipo fiscal de una empresa sale del país de la organización (`c1c15cd`).
- D7: un ex miembro aparece en la auditoría solo con su nombre (`f24df16`).

### 1c. Decisiones del lienzo v99

- `/api/me` devuelve el correo del perfil activo (`668c564`).
- Invitar de nuevo a un operador con invitación pendiente pide reenviarla (`afb7dde`).
- El correo de un operador invitado por alguien sin nombre, como el tablero (`d419de1`, `9c54bd3`).
- La fecha corta en las listas del teléfono y el subtítulo «Administración» de Auditoría (front, R3a).

### 2. Arnés y referencias

- Los «Copiá de» apuntan a referencias sanas: back `5e08a6a`, front `7e60004`.
- Tests nuevos del arnés: un mensaje propio por regla de validación (`74ee2fc`), la salida de cada controller
  (`f47dcd0`), axe en toda página del front (`55f3036`, `64f6e45`) y la 3b contra el lienzo vigente (`f2ea27d`).

### 3. Back: la transacción vuelve al servicio

- Cada servicio de entrada abre la transacción en su método público. Los helpers trabajan adentro y no reciben
  `IUnitOfWork`.
- Commits: Usuarios `3739c24`, Empresas `a76545e`, Configuración y página pública `4d4f479`, alta de empresa `2d4b31d`,
  ingreso `fe6fe7d`, invitaciones `2f6d6ea`, Plataforma `a65e019` y las listas blancas `0f565d5`.
- Se borraron las clases que solo reenviaban: `OrganizationUserLifecycleCoordinator`, `OrganizationUserExitEffects`,
  `OrganizationSettingsUpdater`, `BusinessSignupTransactionRunner`, `BusinessSignupCodeFlow`, cuatro helpers más del
  alta, `SignupVerificationFlow`, `InvitationAcceptanceCredentials` y `PlatformTenantProvisioning`.
- `TransactionBoundaryTests` quedó sin helpers en su lista blanca.

### 4. Back: una sola pieza por regla

- Alcance de un rol (`e59149d`), estado de un miembro (`d55a19c`), búsqueda (`974b03c`), enmascarado (`c64b786`),
  auditoría de roles (`4812ef8`), índices únicos (`e2ecafc`), registro por área (`fa51bb7`), sufijos (`df62c6e`),
  personas asignadas a un rol (`e8d0d94`), repetir el alta de una empresa (`d1e2fb3`), invitaciones (`6536523`), la
  carrera de la clave foránea (`b5b0c9f`) y los índices de búsqueda (`fdca58f`).

### 4f y 4g. La auditoría de la organización, como su tablero

- Una entrada por acción, con «Quién» y el detalle del tablero: `d7ecb12` (el vencimiento de una invitación),
  `691c740`, `d64d50e` (la aprobación de Plataforma), `e5bae2b`, `a7ed218`, `d02bf34`, `9b6c02f`, `20f9fa5`, `d52f518`
  (Sistema y Plataforma en «Quién») y `616c2cc` (la baja de una cuenta).

### 5. Front: piezas comunes

- F1 `fcde757`, F2 `4d836d5`, F3 `28cd128`, F4 `1b5272a`, F5 `5ee936c` y `5fcf5c3`, F6 `f7f9737`, F7 `a6ced5c` a
  `bcfcc48`.
- B4 de punta a punta: back `2197699` y `f9ac0d8`; front `b88d5f8` y `2577a3b`; E2E real `88b2d71`.

### 5b. Front: alineación visual con el relevamiento

- El primer relevamiento (lienzo v99) coincidía en 35 de 222 capturas.
- Tandas: R1 (`1f028f0` a `eb41575`), R2a (`efe3437` a `5f5b912`), R2b (`48b6753` a `c23dd88`), R2c (`8e2c9c2` a
  `84409d4`), R3a (`2b4aac1` a `c86396b`), R3b (`1d8e0f1` a `4650372`), R3c (`f871389` a `13668d9`), R3d (`60f2507` a
  `3cc7f64`), R3e (`e019365` a `416a392`) y R3f (`a777eb0`, `08ca58f`, `054e4c8`).
- Diseño llevó el lienzo a la app donde la app tenía razón: v100 a v107 (front `706cd3c` … `422ef2c`).

### 6. Front: Roles como referencia

- 6a, el editor de roles (`f77f512` a `b42ab32`); 6b, la ficha de Empresa partida (`b02f57f`); los estados con
  `StatusBadge` (`e71daa4`); 6c, los formularios como Roles (`708b7e1` a `585c90e`).

### 6d. Pulido final

- 6d-1 (`64d6606`, `9f03359`, `e78169a`), el CUIT repetido en su campo (`44d2ad6`), 6d-2 (`4edda4f` a `e065fcf`), la
  miga sin enlace (`4dae58d`), Auditoría con `useCursorList` (`9dadbb4`), 6d-3a (`5aedb8e` a `7a975e8`) y 6d-3b
  (`ab6f3d3` a `29584a0`, con el back `4ea3d53` y `2fc0cb9`).

### 7. Puerta

- El E2E real suma los dos casos que faltaban: el rol con nombre repetido y su corrección (B2) y salir de Configuración
  sin guardar (B11). También espera el texto de auditoría de las tandas 4f y 4g (front `bbb1074`).
- `contracts:check` deja de fallar en una copia nueva de Windows (front `3cad8b6`).
- El catálogo del relevamiento simula el contrato de hoy y compara cada estado con el estado bueno del tablero (front
  `f00664f`, `68a74df` y `29b70ec`).
- El relevamiento final, con sus 292 capturas (front `f16c537`).

## Bugs que se arreglaron

| Bug | Qué pasaba | Arreglo | Test |
|---|---|---|---|
| B1 | Buscar con texto entre los miembros de una empresa daba 500. | `cfbf1bd` | `CompaniesTests` |
| B2 | Después de un nombre de rol repetido, el alta quedaba trabada con 422: el segundo intento salía con la misma clave. | front `1c2151f` | `RoleEditorPage.test` y el E2E real |
| B3 | A los miembros de Empresa les faltaba «Estado». Ya estaba arreglado; se sumó el test. | front `acc4613` | test de encabezados |
| B4 | Un Administrador de empresa no podía usar la ficha de su empresa. | back `2197699`, `f9ac0d8`; front `b88d5f8`, `2577a3b` | `CompaniesTests`, `HomeTests` y el E2E real |
| B5 | La Página pública mostraba mensajes de validación equivocados. | `100d713` | `PublicPageValidationTests` |
| B6 | Al borrar el único ítem de la última página, la lista quedaba vacía. | front `28cd128` | test de `usePagedQuery` |
| B7 + D2 | Un rol con personas asignadas podía cambiar de alcance. | `600405a`, `e8d0d94` | `RoleServiceWriteTests`, `RolesTests` |
| B8 | Un invitado con la membresía inactiva salía en dos estados y se contaba dos veces. | `d55a19c` | `CompaniesTests` |
| B9 | Configuración auditaba dos veces cada guardado. | `9b6cb5c` | `OrganizationSettingsTests` |
| B10 | Dos altas de rol con el mismo nombre a la vez: una daba 500. | `e2ecafc` | `UniqueKeyRaceTests` |
| B11 | Salir de Configuración con cambios no preguntaba. | front `a2da32b` | `SettingsPage.test` y el E2E real |
| B12 | Sin red al guardar, salía un error crudo. | front `4d836d5` | test del error común |

Además:
- Quitarle el rol a una Dueña miraba todas las organizaciones de la cuenta, no solo esta (`16dee4e`).
- La Página pública guardada con una versión vieja respondía otro 409, sin «Ver lo nuevo» (`709b9e2`). Guardada dos
  veces a la vez por primera vez, una daba 500 (`e2ecafc`).
- Asignar un rol mientras otra persona lo borraba daba 500 (`b5b0c9f`).
- Repetir el alta de una empresa sin la respuesta guardada respondía 409 en vez de la organización (`d1e2fb3`).
- Un 403 de una consulta secundaria tapaba la pantalla entera (F1, front `fcde757`).
- Avisos que salían dos veces: el de `/api/me` y el 409 de la ficha de Empresa (front `25ea5c3`, `5609326`).
- Las pantallas de edición mandaban la versión de la última relectura de fondo y podían pisar un cambio ajeno sin 409
  (front `ff050ae`). Después de guardar, la base es la versión que responde el guardado (back `4ea3d53`, front
  `d179d83`).
- «Tus datos» de Mi cuenta daba un 409 falso después de cualquier escritura en la cuenta. Ahora tiene su propia versión
  (back `4ea3d53`, front `545b311`).
- «Recuperar mi cuenta» tardaba más cuando la cuenta existía (`e433c2c`).
- El correo nuevo de una recuperación recibía dos avisos (`35fd948`).
- El correo de un operador invitado por alguien sin nombre no era el del tablero (`d419de1`, `9c54bd3`).
- Ninguna invitación vencía en la auditoría. Ahora queda «Venció la invitación a …», una sola vez (`d7ecb12`).
- `npm run contracts:check` fallaba en una copia nueva de Windows por los finales de línea (front `3cad8b6`, esta
  puerta).

## Piezas comunes: la única forma de hacer cada cosa

**Back**

| Para | La pieza | La controla |
|---|---|---|
| Guardar | el servicio de entrada abre `IUnitOfWork.ExecuteInTransactionAsync`; los helpers no reciben `IUnitOfWork` | `TransactionBoundaryTests` |
| La última Dueña | `LastTenantAdminGuard` | `AccountDeletionTests`, `OrganizationUsersTests` |
| El alcance de un rol | `RoleAssignment.Check` y un solo guard para Usuarios y Empresas | `RoleAssignmentTests` |
| El estado de un miembro | `WithState`, una expresión SQL para filtrar, mostrar y contar | `MemberStateParityTests` |
| Buscar | `ApplySearch` (con `MemberSearch` para personas) e índices trigram | `SearchIndexTests` |
| Enmascarar | `PersonalDataMask`: correo, teléfono y método de ingreso | `PersonalDataMaskTests` |
| Índices únicos y claves foráneas | `UniqueKeys` y `ForeignKeys` | `UniqueKeysTests`, `UniqueKeyRaceTests`, `ForeignKeyRaceTests` |
| Registrar servicios | un `<Área>Registration.cs` por área | `DependencyInjectionTests` |
| Nombrar un helper | la lista cerrada de 22 sufijos de `capas-y-flujo.md` | `ApplicationServicesTests` |
| Auditar | el interceptor, con `AccessChangeTrail` y `RoleAssignmentTrail`; `OrganizationAuditEntryBuilder` arma la entrada en el idioma de quien mira | `OrganizationAuditTests` |
| Un permiso de la organización o de la empresa | `[HasCompanyPermission(permiso, organizationPermission: …)]` | `PermissionAuthorizationTests` |
| Responder un guardado con versión | `200 { version }` | los tests de cada área |

**Front**

| Para | La pieza |
|---|---|
| Mostrar un error | `shared/api/errorMessage.ts` y el `MutationCache` (red, 5xx con traceId y 429) |
| Listar | `usePagedQuery` (paginado) y `useCursorList` (cursor) |
| Saber el acceso y salir | `tenancy/access.ts` y `useSignOut` |
| Permisos y módulos | `usePermissions` (`can`, `canInCompany` y `hasFeature`) y el manifiesto de rutas |
| La cultura | `resolveCulture` y `useEffectiveCulture` |
| Editar | `useEditPage` y `useEditForm`, con React Hook Form y zod |
| Crear con un `POST` | `useIdempotentMutation` |
| Pantallas de estado | `LoadErrorPage`, `NotFoundPage` y `ForbiddenPage` |
| Avisos | `FormError`, `Banner`, `ConcurrencyBanner` y `PhoneBand` |
| Diálogos y hojas | `dialog.tsx`, con sus tamaños, y `DialogBody` |
| Estados | `StatusBadge` y `statusTones` |
| Campos | `FormField`, `Select`, `MultiSelect`, `OtpInput`, `checkbox` y `textarea` |
| Listados | `FilterBar`, `DataTable`, `ListRow`, `Pagination`, `LoadMore` y `Page` con pestañas |
| Botones | `Button`, con «Guardando» |
| Ingreso y registro | `AuthStatus`, `AuthCodeStep`, `AuthMessage` y `AuthLink` |
| La tarjeta de la página pública | `PublicPageCard`, que reusa la Etapa 7 |
| El tipo fiscal | `useCuitType`, en `shared/referenceData` |

## Las decisiones más importantes

El número es el de «Decisiones tomadas» del plan.

- **1.** El diff de una modificación lleva solo lo que cambió.
- **4.** El aviso de «Recuperar mi cuenta» lo manda un trabajo aparte: el pedido tarda lo mismo con cuenta o sin ella.
- **8 y 14.** Un correo de otra cuenta queda pendiente, y cada paso responde como con uno libre (ADR 0041).
- **9.** La regla de la última Dueña vive en una sola pieza, `LastTenantAdminGuard`.
- **10.** El país de la organización sale de su «Idioma y región».
- **13.** `/api/me` trae los permisos por empresa, y hay una ruta de roles de una empresa.
- **23.** Quitarle el rol a una Dueña mira solo esa organización.
- **24.** `OrganizationUserService` no se parte: con la ADR 0039 tiene 7 dependencias de negocio.
- **32.** Un solo `OperationLog` por pedido.
- **39 y 40.** El estado de un miembro sale de una sola expresión. Un invitado es «Invitado» aunque su membresía esté
  inactiva.
- **42 y 48.** Las personas asignadas a un rol son todas sus asignaciones: activas, invitadas y desactivadas.
- **43 y 44.** Los índices únicos que se traducen van en `UniqueKeys`, y sus carreras se prueban.
- **53 a 55.** Un solo formato de enmascarado: «lu•••@dominio» y «•••• 4521».
- **58.** La lista cerrada de 22 sufijos de helpers.
- **59.** El correo del perfil activo va en el mismo campo `email` de `/api/me`.
- **62 y 63.** La entrada de auditoría se arma al guardar, y el detalle se muestra en el idioma de quien mira.
- **66.** El vencimiento de una invitación se registra una sola vez, con su fecha.
- **76 y 77.** «Quién» ofrece «Sistema» y «Plataforma».
- **84.** Cada «Copiá de» apunta a un archivo de este repo que sigue las reglas de hoy.
- **86 a 89.** La fila por empresa de Inicio-Miembro: quién la ve y adónde lleva «Reenviar».
- **90.** Quedaba abierto: reenviar desde la ficha de Empresa (lo resolvió la decisión del usuario del 2026-10-05, en
  curso en la sección 6e) y el caso mixto (sigue abierto).
- **91 a 95.** Los de la puerta: `contracts:check` sin mirar los finales de línea, el relevamiento con las clases de hoy,
  los dos casos nuevos del E2E, el recorrido de Plataforma rojo hasta T20 y el detector de docs en un worktree.

## Lo descartado

- **B3:** ya estaba arreglado. Solo se sumó el test.
- **La ADR 0039 en `ServiceDependencyCountTests`:** ya estaba aplicada.
- **Partir `OrganizationUserService`:** la revisión lo proponía con el conteo viejo. Con la ADR 0039 tiene 7
  dependencias (decisión 24).
- **Renombrar cinco salidas de Application** (`LegalDocumentRow`, `PendingDeletionState` y los tres
  `Platform*FilterCounts`): cambiaría esquemas que ya usa el front (decisión 12).
- **M-Usuario y M-Pagina-Org (v99):** la app ya era así.
- **`useFeature`, `<Feature>` y `FeatureRoute`:** nunca existieron. `hasFeature` vive en `usePermissions` (F5).
- **«Antes de deshabilitarte» (R2a):** no llega, porque el back responde antes `CannotDisableSelf`.
- **Pedir el catálogo una sola vez (F6):** obligaría a esperar `/api/me`. Queda como está.
- **Cinco hallazgos del detector de docs:** descartados con su motivo en `ignorados.json` (decisión 82).
- **`arbol.md`:** salieron 189 líneas con nombres que nunca existieron (decisión 79).
- **De la puerta:** las «migas del Administrador de empresa» del relevamiento parecían un error, pero «Empresas» ya va
  sin enlace para quien no abre Empresas (`4dae58d`); y la «bandera vacía» de una captura de la Página pública es de la
  captura (la imagen no llegó a cargar), no de la app.

## Números de la puerta

| Qué | Resultado | Cómo |
|---|---|---|
| Back: build | 0 advertencias y 0 errores | `dotnet build ArquitecturaBaseMultitenant.slnx` |
| Back: tests | **1652/1652**, 0 fallas y 0 omitidos (7 m 53 s), con Docker | `dotnet test` |
| Back: `HarnessTests` | 10/10 | `dotnet test --project tests/ArquitecturaBaseMultitenant.ArchitectureTests/… -- --filter-class "*HarnessTests"` |
| Front: build | correcto | `npm run build` |
| Front: lint | 0 errores; 7 avisos en `src` y 745 en la copia congelada `docs/design/lienzo-v35` | `npm run lint` |
| Front: tests | **1161/1161** en 193 archivos (2 m 12 s) | `npm test` |
| Front: `harness.test.ts` | 7/7 | `npx vitest run src/test/harness.test.ts` |
| Front: contratos | coinciden con OpenAPI | `npm run contracts:check` |
| E2E real | **13 de 14 recorridos** en verde (14 m 26 s); falla el de Plataforma, que es de otra etapa | `npm run test:e2e:real` |
| Capturas | **292** (146 estados a 1440 y a 390), todas verificadas | `capturar-relevamiento.mjs --verify` |
| Relevamiento | 18 coinciden; 274 difieren: 268 con algo nuevo, 112 con algo de otra etapa y 5 en curso | `relevamiento.md` |
| Docs | `realinear-docs --detectar`: 0 en los dos repos (automáticos 0, de modelo 0 y para vos 0) | `node scripts/arneses/arnes.mjs realinear-docs --detectar` |

**El E2E real** corre con el AppHost, un PostgreSQL aislado, el front y la Api reales y el correo leído del pickup, sin
mocks. Cubre lo que pide la puerta:
- **el ingreso:** por correo y código, por la puerta de persona y de empresa, y en el teléfono;
- **el alta de un rol con nombre repetido y su corrección:** 409 `Roles.Role.AlreadyExists` debajo del nombre y, corregido,
  201 con otra `Idempotency-Key`;
- **un Administrador de empresa en la ficha de su empresa:** su «Para resolver» con la empresa de la fila, «Reenviar»
  que abre la ficha, la búsqueda y el filtro de estado de los miembros, con la orden `expire` del preparador (B4);
- **salir de Configuración sin guardar:** el enlace del menú pregunta, «Seguir editando» conserva lo escrito y «Salir sin
  guardar» se va sin mandar nada.

El recorrido de Plataforma (`5: ingresar como operador y consultar Plataforma`) está rojo a propósito desde la Etapa 5
(«rojo por pantalla E5 ausente»; verde en T21 a T23). Hoy falla antes: al entrar por `/plataforma`, el back responde
`Tenancy.Access.PlatformOnly` y la app se queda en `/auth/callback` con «No pudimos iniciar tu sesión». El ingreso de
Plataforma (sus rutas y sus puertas) es T20.

## Las diferencias visuales que quedan

El detalle, estado por estado y con su imagen, está en el
[relevamiento final](../../../ArquitecturaBaseMutitenantFront/docs/design/capturas/consolidacion/final/relevamiento.md).

**De otra etapa:**
- WhatsApp: el menú, Administración, las pestañas del teléfono, los atajos, los permisos y el cambio «Correo» /
  «WhatsApp».
- «Recuperá tu cuenta» (Etapa 5, T19).
- Plataforma: el correo de ventas de «Llegó al límite» y «Alta cerrada». Sale de `GET /api/contact`, que ya hizo el back
  de la Etapa 5 y el front todavía no usa.
- «Exportar mis datos».
- La portada: en `/` la app muestra «Iniciando sesión…».

**En curso** (las decisiones del usuario del 2026-10-05, sección 6e del plan): «Agregar miembro» y reenviar en la ficha
de Empresa para el Administrador de empresa, la ficha con un solo grupo, «Guardar cambios» parejo y los textos de espera.

**Nuevas.** Casi todas son de medidas y vienen de unas pocas piezas comunes:
- los íconos de lucide del marco y del título, 2 a 4 px más grandes (N1);
- la marca «ArquitecturaBase» más apretada, por el `tracking-tight` de `AuthLayout`: sola, hace que no coincidan 54
  capturas del ingreso, el registro, la invitación y los términos (N2);
- en el teléfono, la bajada de los encabezados 4 a 6 px más lejos y los campos 2 px más separados por hueco (N3 y N4);
- las sombras de diálogos y desplegables más cortas (N5), y el encabezado y el cuerpo de los diálogos (N6).

Las que conviene mirar primero, porque no son solo de medidas:
- **«Último cambio» cortado** en la tarjeta Auditoría del Inicio de la organización: con una fecha vieja, la hora no
  entra y se pierde (N17). Parece un error de la app.
- **Mi cuenta sin el módulo de WhatsApp:** dice «Agregar correo o teléfono» con un control de un solo segmento; el
  tablero dice «Agregar correo», sin control (N19). Parece un error de la app.
- **El correo sin tapar** en «Te mandamos un código a…» de Mi cuenta; el tablero lo tapa (N19).
- **«Verificado» y Google** en los métodos de Mi cuenta: la app suma el estado y escribe «correo / Verificado» (N19).
- **«Quitar dominio» sin el número de correos** (N23) y **la tarjeta de la vista previa en el teléfono** (N24): ya están
  en la tanda 6d-4b (sección 6e del plan).
- **La despublicada en el teléfono:** el ⓘ corta la dirección (N24).

**Del tablero, para Diseño:** el pie «Administración» viejo en varios tableros, el botón ‹ de Inicio-Org, Inicio-Miembro y
Error-Org, el sombreado de «Para resolver», el rojo tapado de M-Cuenta-Baja, el doble espacio de Registro-Empresa, los
dos menús de perfiles distintos entre sí, el encabezado de 52 px de algunos diálogos y el CUIT repetido en la franja.

## Lo pendiente de verdad

- **En curso** (sección 6e del plan):
  - el Administrador de empresa: el back está hecho (`94c7dea`, con la ruta de candidatos y reenviar o revocar con
    `company.members.manage`) y falta el front (tanda 6d-4b);
  - «Guardar cambios» con un solo criterio (`a51a27c`), los textos de espera (`1051136`) y la ficha con un solo grupo
    (`2c96ca7`): hechos después de los commits de esta puerta;
  - lo demás de v108, tanda 6d-4b: «Quitar dominio» para 10, 1 y 0 correos, el detalle de Auditoría en el teléfono, las
    casillas fijas de un rol de sistema en gris en el teléfono y la hoja de la vista previa.
- **T18b de la Etapa 5:** las acciones de Plataforma sobre una cuenta sin 409 falsos.
- **El front de la Etapa 5** (T19 a T25): el ingreso de Plataforma y sus pantallas, que ponen en verde el recorrido E2E de
  Plataforma; «Recuperá tu cuenta»; y el correo de ventas de `GET /api/contact` en el registro de empresa.
- **Lo que encontró la puerta y queda para decidir o arreglar:**
  - «Último cambio» cortado en el Inicio (N17) y el texto de Mi cuenta sin el módulo de WhatsApp (N19);
  - las demás diferencias nuevas del relevamiento, con Diseño;
  - B7 + D2 en el front: el 409 `Roles.Role.ScopeChangeHasUsers` al guardar sale en la franja con el texto del back, como
    cualquier error de guardado, pero no tiene un test propio y su casilla del plan sigue abierta;
  - el arnés de capturas no puede mostrar las variantes de un tablero que se aplican solo al montarse («Diálogo: Sumar
    empresa: otra persona cambió esto» de Usuario y «Crear empresa: ya hay una con ese CUIT» de Empresas);
  - los 7 avisos del lint en `src`: `set-state-in-effect` en `BusinessSignupPage` (3) e `InvitationPage`, y
    `only-export-components` en `TaxIdField`, `AppNotices` y `AppChrome`; y el lint revisa también la copia congelada
    `docs/design/lienzo-v35` (745 avisos);
  - el caso mixto de la decisión 90 sigue anotado como abierto.
- **Casillas del plan sin marcar con el trabajo ya hecho** (confirmado en el código): 1.2 («10 correos administrados»
  en Configuración), D4 («Ese correo ya lo usa otra cuenta» no está en la app), D6 (el tipo fiscal sale del país) y la
  fecha corta de 1c. Las marca quien lleva el plan.

## Lo que pasó en la puerta

- **`contracts:check` fallaba en una copia nueva de Windows:** comparaba bytes y el `schema.d.ts` sale con CRLF.
  Arreglado en `3cad8b6`, con su test.
- **El catálogo del relevamiento** simulaba el guardado de Configuración con 204, y desde 6d-3b el guardado responde
  `{ version }`: el estado «Guardamos la configuración.» no se podía capturar (`f00664f`). Además, 16 capturas se
  comparaban con otro estado del tablero (`68a74df` y `29b70ec`). Las dos cosas, con su test.
- **El E2E esperaba un texto de auditoría viejo** («Cambió los roles de …»; desde 4f y 4g, invitar es «Invitó a …»).
  Arreglado en `bbb1074`, que lee el texto de la API.
- **Un commit de más:** `29b70ec` se llevó también el `b6d4aCases` que la tanda 6d-4a tenía preparado en el índice de la
  copia compartida (`capturar-relevamiento.casos.mjs`). No se reescribe la historia: los tests del catálogo pasan en ese
  commit, y la tanda 6d-4a commiteó después sus capturas (`b956feb`).
- **El detector de docs en un worktree** da un hallazgo falso si falta el repo hermano `ArquitecturaBase`
  (`backend.md:31`); en la copia principal, o con el hermano al lado, da 0.
