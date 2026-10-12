# Correctivos de calidad antes de la Etapa 11

| Tarea | Resultado |
|---|---|
| C0 | Hecha: el usuario acepta `invitacion_organizacion/es` Marketing (ADR 0038); se conserva el nombre y la configuración. Meta confirmó el borrado de `invitacion_organizacion_literal` es/en, sin variantes restantes. |
| C1 | Hecha según lienzo v120 (`74141645`): grupos con título, área y total, hasta 10 filas y «Ver todos» filtrado; compu sin Área y teléfono sólo estado. Guarda SQL con 10/1000 pendientes verde; 6 unitarios, 24 de integración, 46 de arquitectura y 88 del front verdes. |
| C10 | Hecha: contrato y metadatos generados, descripción pública, CUIT, roles, cuenta eliminada y categorías. Build 0/0; unitarios 133 e integración 46 verdes; pruebas focales del front y contratos verdes. |
| C4 | Hecha: hooks comunes, blocker público, variables tipadas y guarda UNSAFE. Recorridos y barra final verdes; el primer pending también se verifica (22 casos afectados verdes). TypeScript y lint sin errores nuevos. |
| C11 | Hecha: reloj de servidor, validación Google, textos compuestos y duplicados. Back 325 y front 341 casos focales verdes; builds/TypeScript sin errores. |
| C7 | Hecha en núcleo y WhatsApp: diagnóstico privado y worker común. Rojo observado; 6 unitarios y 46 de integración verdes; build Api 0/0. Asistente se conserva en el alcance de 8b. |
| C2 | Hecha: opciones remotas de a 10, selección conservada y guardas. Recurso 101, «Todos», caché e invalidación verdes; áreas, TypeScript y lint sin errores nuevos. |
| C3 | Hecha: conteos agrupados y contratos aditivos. Back 16 de integración y 3 unitarios verdes; front 19 casos verdes, incluidas las dos guardas de un pedido por búsqueda; TypeScript sin errores. |
| C9 | Hecha: auditoría y dominios paginados, legacy con tope SQL y guarda general 3/3. Áreas de back/front e índices verdes; build 0/0, contratos, TypeScript y lint sin errores nuevos. |
| C5 | Hecha: filtro `onlyOwn` aditivo y paginado real con tipos generados; guarda contra respuestas manuales. 7 de integración y 42 del front verdes; build 0/0, TypeScript, lint y contratos sin errores nuevos. |
| C6 | Hecha: reglas de baja en Application y limpieza EF por puerto; guarda interna. 38 de arquitectura y 48 de integración verdes; builds 0/0. |
| C8 | Hecha: 27 literales y composición del origen centralizados; guarda fuente y avisos con URL completa. 301 casos verdes; builds 0/0. |
| Cierre | Corrida completa única terminada; pendiente un fixture de reloj de Assistant para la 8b. C1, autorizado después de esa corrida, está hecho y verificado en su alcance, conservando los verdes anteriores. |

| Verificación de cierre | Resultado |
|---|---|
| Back: build y suites, incluida arquitectura | Build 0 advertencias / 0 errores; 3072/3072 casos verdes agregados, sin omisiones. Sólo se repitieron la referencia documental corregida y el timeout aislado (verde). |
| Front: build, TypeScript y lint | Build verde; TypeScript verde después de los arreglos; lint sin errores, con 6 advertencias preexistentes. |
| Front: suite | Una corrida completa de 3042 casos; 3041 verdes agregados tras arreglos y repeticiones dirigidas. Pendiente únicamente `src/modules/assistant/components/AssistantRetryButton.test.tsx`. |
| Arnés y documentación | Tests verdes; detector 0 auto / 0 modelo / 0 humano, sin consumo de modelo. |
| Contratos | `contracts:check` verde; OpenAPI y tipos generados coinciden. |
| Revisión final | Completa; los hallazgos y el primer pending detectado por la suite tienen pruebas dirigidas verdes. |
| C1, alcance autorizado después del cierre | Back: 6 unitarios, 24 de integración y 46 de arquitectura verdes; front: 87 Vitest y 1 del generador verdes. Builds 0 errores, TypeScript y contratos verdes; lint conserva 6 advertencias previas. Detector 0/0/0. |

El fixture pendiente de la 8b activa timers falsos después de montar y reinicia `performance.now()`; al avanzar 60 s todavía muestra una espera. Debe conservar el origen monotónico o montar con el reloj falso desde el principio. Falló también en su única repetición aislada; no se modificó por la exclusión de Assistant. Los archivos propios están commiteados con pathspec y sin push.

**Decisiones técnicas:** C0 conserva `invitacion_organizacion` y su configuración según la decisión de producto del 12/10 (ADR 0038); se retira el intento literal de Meta en ambos idiomas.

- C1: `pendingGroups` aditivo publica total y `maxItems` como fuente única del tope; `IPendingGroupSource` limita en SQL y revalida permisos en lote y política de empresa tras hidratar, mientras `GetAsync` conserva el agregado interno completo; «Ver todos» usa los filtros aditivos `Expired` y `Pending` sin cambiar estados ni facets existentes.
- C10: FluentValidation publica límites/rangos; `contracts` genera tipos y `validation.ts`; la baja persiste `DisplayName=null` y se traduce según estado al leer.
- C4: `useResourceAction` reconoce sólo variables ejecutadas por su dueño y observa cache más registro del dueño para reflejar el primer pending; incluye reintentos sin observer y excluye borradores abandonados. `useEditPage` conserva el único blocker público y no compara rutas literales.
- C11: HTTP `Date` y avance monotónico fijan tiempos/reenvíos sin atrasarse ante respuestas demoradas; los tests conservan el origen monotónico al simular una espera. Application valida el inicio Google y contiene `WebClientOptions`/`AuthServerDefaults` para respetar capas.
- C2: opciones por recurso bajo su raíz de caché, páginas de 10 y búsqueda remota; caché/detalle y roles ya asignados conservan etiquetas. «Todos» carga páginas sólo al pulsarlo y su dueño se remonta al cambiar de empresa.
- C9: rutas /paged aditivas; arrays anteriores deprecados y acotados en SQL a una página, con `maxItems` y sucesora verificables. La guarda sólo lista catálogos; también detectó Dominios sin tope, que recibe el mismo paginado e índices de búsqueda/orden.
- C3: conteos agrupados en SQL; Roles devuelve búsqueda y totales juntos, Usuarios mantiene el facet de estado sin estado y el de empresa con estado; campos nuevos aditivos.
- C5: `onlyOwn` conserva el listado asignable por defecto; pestaña y contador comparten la primera página y caché de 30 s, invalidada por Roles. Respuestas concretas usan alias generados; helpers derivan sólo la forma común.
- C6: tres participantes en Application/AddLegal; `IAccountDeletionCleanupRepository` conserva consultas y limpieza EF, transacciones y scopes, con la regla de última Dueña internal.
- C7: `ExceptionLog` publica sólo tipos/stacks, también internos; `PeriodicWorker` conserva intervalos/señal y continúa tras fallar. HTTP completa ProblemDetails aun con Accept no admitido para que el framework no registre la excepción cruda.
- C8: `ReturnUrls` contiene rutas, raíz y callbacks; `IPublicOrigin.Absolute` conserva issuer, encoding y fallbacks. El arnés omite salidas locales de build/correo/navegador; las carpetas vacías no se documentan.

**Cuándo y quién:** un chat nuevo de Codex, con los dos repos, en paralelo con la 8b y antes de la 11 (decisión del usuario, 2026-10-12). La 11 copia los moldes del núcleo (Roles, las listas para elegir, «Para resolver»): estos arreglos van primero para que no copie sus problemas. **Por qué:** la revisión de código del 2026-10-12 encontró que las reglas se cumplen, pero hay cosas que andan con pocos datos y se rompen con muchos, valores fijos y código copiado en vez de compartido.

**Reglas:** `AGENTS.md`, «Velocidad y cierre liviano». No hay funcionalidad nueva: cada tarea hace cumplir una regla ya escrita (paginado, valores fijos, capas, no duplicar). No se tocan `Modules/Assistant` ni `src/modules/assistant` (son de la 8b) ni `Program.cs`. Los contratos nuevos son aditivos y los coordina este mismo chat en los dos repos. Commits con pathspec, sin push.

## Precisiones de la revalidación (2026-10-12)

Once agentes revalidaron cada caso contra el código. Los 11 son reales; C5, C6 y partes de C10 están algo exagerados. El detalle está en `.local/revision-correctivos/revision-11-correctivos.md`, que no se versiona.

- **Orden de trabajo:**
  1. C0;
  2. lo que hoy ve un usuario: C10 (descripción de la página pública, que el front deja en 1000 y el back en 500, y «Cuenta eliminada»), C4 (dirección con «/» al final) y C11 (reloj de la compu);
  3. C7;
  4. C1, recién cuando esté publicado el tablero de Diseño (decisión del usuario del 12/10), y C2, C3 y C9;
  5. al final, C5, C6 y C8.
- **Cada arreglo suma su guarda, para que no vuelva:**
  - C1: un test que cuente las consultas con muchos pendientes;
  - C2: una guarda que prohíba `pageSize=100` en `src/areas/**/api`;
  - C3: un solo pedido de conteos por búsqueda;
  - C4: una guarda contra `UNSAFE_`;
  - C7: un worker que falla registra y sigue, y el texto de un error nunca llega al log;
  - C8: ninguna ruta del front fuera de `ReturnUrls`;
  - C9: un test de arquitectura que rechace `IReadOnlyList` en rutas cuyos datos crecen, con una lista blanca de catálogos;
  - C10: largos y rangos publicados en el OpenAPI;
  - C11: `Date.now` y `Date.parse` dentro de la guarda de formatos.
- **Lo que hay que sumar a cada tarea:**
  - C2: no hace falta un endpoint nuevo, porque las rutas ya aceptan `search`.
  - C5: hace falta un filtro nuevo en el back, «solo los roles propios de esta empresa».
  - C6: no hay un bug, es diseño, y por eso va al final.
  - C8: son 27 literales. Hay que sumar `PlatformOperatorInvitationNotifier`, `OrganizationMembershipEndNoticeBuilder` e `InvitationDeliveryIssuer`, y el patrón de origen está copiado 7 veces.
  - C10: el CUIT argentino del registro de empresa está acordado (`identificacion-fiscal.md`); solo falta usar `TaxIdNumberField`. Las categorías escritas como texto incluyen también `PublicPage` y `TenantDomain`, y el front las repite en `AuditPage.tsx`.

## Tareas

| # | Qué | Dónde | Listo cuando |
|---|---|---|---|
| C0 | **Conservar la invitación española como Marketing** (decisión del usuario, 12/10/2026; ADR 0038). Se mantiene `invitacion_organizacion` y se borra en Meta `invitacion_organizacion_literal` en es/en, sin cambiar configuración. | Meta; back `docs/operations/whatsapp-plantillas.md` | la consulta a Meta no devuelve variantes literales; la original conserva es APPROVED / MARKETING y en APPROVED / UTILITY |
| C1 | **«Para resolver» con tope.** Conteo más los primeros ítems de cada área y «Ver todos» hacia el listado filtrado. Los permisos se revisan una sola vez por pedido, no ítem por ítem. | back `Services/OrganizationUsers/OrganizationUserPendingItems.cs`, `Services/Home/HomeService.cs`; front `areas/platform/home/api/pending.ts`, `PlatformToResolve.tsx` | con miles de pendientes, el Inicio hace una cantidad fija de consultas |
| C2 | **Listas para elegir con búsqueda en el servidor.** Un selector con búsqueda remota en `shared`, que reemplaza las listas cortadas en 100 de roles, personas y empresas («Agregar miembro», filtros, asignaciones). Una sola consulta de opciones por recurso, con su clave. | front `roles/api/roles.ts`, `users/api/users.ts`, `companies/api/companies.ts`, `AddMemberDialog.tsx`; back, un endpoint de opciones paginado si hace falta | se puede elegir la empresa, el rol o la persona número 101 |
| C3 | **Conteos sin un pedido por empresa.** El back devuelve los conteos por empresa de una vez (`/api/roles/filter-counts` y el conteo por empresa en `users/filter-counts`). El front borra los `useQueries`. | back Roles y OrganizationUsers; front `RolesPage.tsx`, `UsersPage.tsx` | una búsqueda hace un solo pedido de conteos |
| C4 | **Concurrencia de Plataforma en `shared`.** Hooks comunes (`useMountedRef`, `useResourceAction` tipado, `useAbandonOnLeave` sobre `useBlocker`) en lugar del código copiado en 9 pantallas. Sin `UNSAFE_DataRouterContext` ni comparar `pathname`. La ficha datos-y-api pasa a decir «usá el hook». | front `areas/platform/**`, `shared/`, `docs/rules/datos-y-api.md` | ninguna pantalla repite la coordinación; la guarda de formularios no encuentra `UNSAFE_` |
| C5 | **Paginado en el servidor y tipos del contrato.** `CompanyRolesTab` pide la lista paginada con `usePagedQuery`. `PagedResult<T>` escrito a mano se reemplaza por los alias `PagedResultOf…` generados. | front `CompanyRolesTab.tsx`, `useCompanyRoles.ts`, `shared/api/pagedResult.ts` y sus usos | no queda paginado en el cliente ni un `PagedResult` propio |
| C6 | **Las reglas de la baja, en Application.** Los participantes de Membership, PersonalSpace y Recovery pasan a `Services/Legal`, como hizo WhatsApp. En Infrastructure queda solo lo genérico de EF (`EngagementCopyDeletion`). | back `Infrastructure/Identity/Deletion/*` | `ILastTenantAdminGuard` deja de ser público; tests de la baja en verde |
| C7 | **Una sola política de errores en logs y procesos de fondo.** Se registra el tipo y el stack, sin el mensaje (o con un redactor), igual en HTTP y en los workers. Una base `PeriodicWorker` reemplaza el bucle copiado. Ningún worker frena el servidor por una excepción inesperada. | back `GlobalExceptionHandler.cs`, `OperationLog.cs`, los 8 `BackgroundService` | un worker que falla registra y sigue |
| C8 | **Las rutas del front, en un solo lugar del back.** Un catálogo en `ReturnUrls` más un helper `IPublicOrigin.Absolute(path)`, en lugar de los ~20 literales (`/cuenta`, `/login/empresa`…). | back `ExternalLoginController`, `ConnectController`, `AccountDeletionNotifier`, `LoginMethodNotifier`, `PlatformRecoveryNotifier`, `IdentityRegistration` | no queda una ruta del front escrita fuera del catálogo |
| C9 | **Auditoría paginada.** Las organizaciones con eventos y los actores de la auditoría de la organización pasan a una búsqueda con tope (`PagedRequest`). | back `PlatformAuditController`, `SecurityEventReader`, `OrganizationAuditReader`; front sus filtros | ningún listado de auditoría devuelve todo |
| C10 | **Valores y textos con una sola fuente.** El CUIT del registro de empresa usa `TaxIdNumberField` con el tipo del catálogo. Los largos máximos y los rangos los publica el back en el OpenAPI y el front los genera con `npm run contracts`. Los nombres de los roles de sistema salen solo del back, y se alinean los mocks. «Cuenta eliminada» pasa a ser una etiqueta de `FormattingTexts`, sin guardarla traducida. Las categorías de auditoría van en una clase como `PlatformAuditCategories`. | front `BusinessSignupPage.tsx`, `useCuitType.ts`, formularios con `maxLength`, `roles/lib/systemRoles.ts`; back `ErrorTexts`, `AccountDeletionProcessor`, `PlatformOperatorProvisioner`, `OrganizationAuditReader` | ningún límite, CUIT ni nombre de rol vive en dos lugares |
| C11 | **Detalles.** Validación a mano y literales de error en `ExternalLoginController`, pasados a request más validador y constantes de `<X>Errors`. Frases armadas con fragmentos, pasadas a `<Trans components>`. `Date.now` y `Date.parse` fuera de `shared/time`, sumados a la guarda de formatos. Duplicados (`initials`, `toSearch`) y código muerto (`secondsUntilResend`, `personal.json`), afuera. Carpetas vacías del back, borradas. El worker de retención de WhatsApp se renombra a `…Worker`. | los nombrados | build, lint y tests del alcance en verde |

**Va a la Etapa 9, no acá:**
- los permisos con una consulta por empresa en cada pedido (`PermissionService`), junto con la caché;
- los tiempos de seguridad fijos (tokens, cookie, idempotencia, enlace de ingreso, lote de la baja) y el límite del webhook de WhatsApp, pasados a opciones con `ValidateOnStart`;
- un formateador en el CI, que reformatearía todo el repo y se pisaría con la 8b.

## Asistente: lo hace la 8b, antes de su puerta

- Una base para los tres controllers idénticos (`Api/Modules/Assistant/Controllers/{Business,Consumer,Platform}AssistantController.cs`): las derivadas declaran solo ruta, acceso, permiso y gate.
- Una base para las entidades gemelas (`AssistantConversation` y `PlatformAssistantConversation`, y lo mismo con Message, Turn y DurableOperation).
- Los números repetidos (10 MiB, 5 adjuntos, 0,8, 20 filas) en `AssistantTechnicalLimits` u opciones, y el contributor del contrato que los interpole.
- `RepositoryHelpArticleReader` y sus recursos, dentro del módulo, con las culturas del catálogo.
- `AssistantHome.tsx` partido en hooks y paneles. El módulo, autocontenido: sus tipos, textos y fixtures dentro de `src/modules/assistant`, aportados por `registration.ts`.
- Los 3 tests de arquitectura en rojo (formatos a mano, uso del tenant, datos fijos).

## Cómo se cierra

Cada tarea, con los tests de su área. Al final, una sola vez: builds, suites de los dos repos, tests de arquitectura y del arnés, `contracts:check` y el detector en 0/0/0. Arriba de este plan queda una tabla con el resultado de cada tarea.

## Texto para el chat de Codex

```
Leé AGENTS.md del back y del front y docs/plans/2026-10-12-correctivos-de-calidad.md (back). Hacé C0 a C11 en el orden de «Precisiones de la revalidación», en los dos repos; C1, recién cuando el usuario te avise que Diseño publicó su tablero. Cada arreglo suma la guarda que dice esa sección. Es en paralelo con la 8b: no toques Modules/Assistant, src/modules/assistant ni Program.cs, y commiteá solo tus archivos con pathspec. Los contratos nuevos son aditivos: publicalos y regenerá los tipos del front en el mismo paso. No agregues funcionalidad: cada tarea hace cumplir una regla ya escrita. Las decisiones técnicas son tuyas: anotalas en una línea del plan. Sin push. Al final, la corrida de «Cómo se cierra» una sola vez y la tabla arriba del plan.
```
