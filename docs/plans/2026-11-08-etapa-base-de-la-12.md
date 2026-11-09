# Base de la 12 · plan ejecutable

**Estado:** en curso · Spec: `docs/plans/formularios/base-de-la-12.md` y `base-de-la-12-parte-pasos.md`. Tableros aprobados: lienzo v169. No hay decisión de producto pendiente.

| Parte | Tareas | Estado |
| --- | --- | --- |
| Contrato | K1 | Hecha (`8b23501c`, `534f4308`) |
| Back | B1–B10 | Hecha (`28652183` a `e1de70c9`, y `75fe6a93`) |
| Front | F1–F16 | Hecha (`8ace5c06` a `ac7de89b`, en el front) |
| Integración y puerta | I1–I4 | Pendiente |

## Forma de trabajo

Modo rápido: contrato primero; después Back y Front en paralelo; una sola puerta al final. Cada tarea tiene pruebas focales, build y tests rápidos completos de su repo, sin filtrar por módulo. No se repite una suite completa que ya pasó: un arreglo corre solo lo que toca.

Todo ocurre en `main`, sin ramas, worktrees, stash, reset ni push. Cada chat commitea solo sus rutas con pathspec. Si el front está levantado, nadie edita `src` hasta que se detenga Vite. La Guía revisa este plan y decide su commit; este plan no autoriza a tocar el lienzo.

## Qué se verificó al arrancar

| Fila de la spec | Qué se encontró | Qué cambia |
| --- | --- | --- |
| La F5 de la 10 | no está hecha | entra acá completa |
| Meta | no se verifica desde el repo | la vuelve a aprobar el usuario (I2) |
| `detail` en las tablas | 25 usos en 12 `columns.tsx`, no en 8 | F3 cubre los 12 |
| `hint` y `description` | 43 usos en 35 archivos | F10 los recorre todos, sin tocar las descripciones técnicas |
| Forma, escala y sombras | las seis piezas previstas, más `PublicPageCard.tsx` | F8 suma ese archivo |
| `tema.md` | raíz al 75 % en la compu y 44 × 44 en el teléfono | nada |
| `truncate` | 20 usos en 10 `columns.tsx` | nada |
| Degradés | 12 en 8 archivos, más `PublicPageCard.tsx` | F8 lo saca también |
| Lo que se elige y lo que se lee | `OptionDialog` no confirma antes de descartar; no hay lecturas plegadas en el núcleo | F16 |
| El enlace secreto | solo lo emite el bot | B6 lo retira entero |
| `InviteByPhoneAsync` | no recibe ni guarda el consentimiento | B7 |
| «Desvincular» sin WhatsApp | el teléfono se quita desde el núcleo | nada |
| `StatusBadge.test.tsx` | espera `--marca` y el componente usa `--punto-pendiente` | se arregla primero (F16) |
| `harness.test.ts:136` | dice «de 3 a 8 líneas» y controla de 1 a 3 | se corrige el mensaje (F16) |
| Una lectura por requisito (`docs/rules/permisos.md`) | una ruta de empresa con `organizationPermission` lee dos veces la proyección de la organización | B10, como pide la ficha |

## Contrato

### K1 · Publicar antes de programar las pantallas

No cambia una ruta existente salvo la importación de teléfonos, el cambio de acceso y el retiro del canje. El Back publica y genera `docs/contracts/openapi.json`. Los ejemplos de una corrida real nacen en la E12, con su guarda ([api-http](../rules/api-http.md)). El Front regenera `src/shared/api/generated/schema.d.ts` con `npm run contracts`; no escribe tipos a mano.

| Ruta | Acceso y permiso | Entrada / salida | Resultado |
| --- | --- | --- | --- |
| `GET /api/notifications` | Consumer, Business y Platform; cada fuente aplica sus permisos | sin cuerpo → `NotificationListResponse { items: NotificationItem[≤50], unread }`; `NotificationItem { id, kind, params, occurredAtUtc, link, read }` en orden descendente | 200 |
| `POST /api/notifications/{id}/read` | los mismos tres accesos | sin cuerpo | 204; un id que no corresponde a un aviso visible se ignora |
| `POST /api/organization/whatsapp/phone-imports` | Business + `users.invite` + módulo WhatsApp | suma `consentConfirmed: true` a la importación; el preview no lo pide | 400 `WhatsApp.PhoneImport.ConsentRequired`, error de campo `consentConfirmed`, si falta o es `false`; conserva sus respuestas actuales |
| `POST /api/auth/login-link/preview` y `/redeem` | — | se retiran con sus contratos y ejemplos | 404 por ruta inexistente |
| Cambiar de acceso (`ConnectController`) | el de siempre | suma `returnTo`, que se valida con los destinos de `ReturnUrls.cs` y no amplía los retornos autorizados | un destino ajeno no se usa, como hoy |

`INotificationSource.ListAsync(NotificationContext, ct)` vive en `Application/Interfaces/Services/`, copia el alcance de `IPendingItemSource` y devuelve solo lo autorizable. `kind` queda abierto. El front arma el texto de cada aviso con su `kind` y sus `params`, en el idioma de quien mira: los del núcleo, con sus locales; los de un módulo, con `notifications` de su `registration.ts` (`{kind, describe}`, como `businessHome.pending`). `occurredAtUtc` es UTC. Las rutas cuentan en las cuotas generales. No hay permiso nuevo ni idempotencia para marcar leído.

**Texto para el chat de Contrato**

```text
Leé AGENTS.md, este plan y la spec de Base de la 12. Hacé K1 primero, sin adelantar formularios. Publicá exactamente las rutas, cuerpos, accesos y ProblemDetails de esta sección; generá openapi.json y avisá al coordinador para que el front corra npm run contracts. Todo en main, sin push, rama ni worktree; commiteá solo tus rutas con pathspec. Aplicá api-http, permisos, textos, fechas y tests. Al terminar, build y tests rápidos enteros del back, nunca filtrados por módulo.
```

## Back

Las tareas copian los moldes indicados en la spec. Aplican Result/ProblemDetails, `OperationLog.RunAsync`, `LoggerMessage`, `TimeProvider`, resources es/en, UoW única por escritura, RLS y auditoría. Los cambios de módulo permanecen en `Modules/WhatsApp`; el núcleo no lo referencia.

1. **B1 · Campana.** En `INotificationSource`, modelos, `NotificationService`, controller de Cuenta, entidad/configuración/repositorio y migración de `NotificationRead` global en `identity`; copiar `IPendingItemSource`, `HomeService`, `HomeController` y `MeController`. Junta fuentes aisladas y marca leído con UoW y clave `UserId + NotificationId`. Probar tres accesos, permiso, orden, 50, fuente fallida y aislamiento.
2. **B2 · Fuentes del núcleo.** En `Services/Notifications/`, sumar `PersonalLoginMethodNotificationSource`, `OrganizationPendingNotificationSource` y `RecoveryRequestedNotificationSource`, con `kind` `personalLoginMethod`, `organizationPending` y `recoveryRequested`. El sufijo `NotificationSource` entra en la lista cerrada de [capas-y-flujo](../rules/capas-y-flujo.md) y en `ApplicationServicesTests`, como `PendingItems`; copiar `OrganizationUserPendingItems`. Probar cada fuente con y sin el permiso.
3. **B3 · Baja y limpieza.** Sumar participante de baja y worker de 90 días; copiar `AccountDeletionWorker`. Probar eliminación con la cuenta y limpieza vencida.
4. **B4 · Textos.** En resources es/en, `Permissions`, plantillas de correo/WhatsApp y `docs/operations/whatsapp-plantillas.md`, cambiar el vocabulario de la spec; copiar los recursos actuales. Probar `ResourceParityTests` y una guarda sin las palabras retiradas.
5. **B5 · Otro acceso.** Sumar `returnTo` al cambio de acceso, solo desde `ReturnUrls.cs`; copiar ese archivo. Probar `FrontendReturnUrlTests`, incluidos destinos ajenos.
6. **B6 · Bot sin enlace.** Retirar controller, contratos, entidad, repositorio, opciones, servicios, replay, DI, migración y pruebas de `LoginLink`; borrar `LoginLinks` con un `Down` que se niega a perder datos. En los escritores y `WhatsAppCloudClient`, copiar el botón existente y responder `Entrar a mi cuenta` con `cta_url` a `/login`, aun sin consultas. Probar que no sale token/enlace y actualizar inventarios de rutas, seguridad, clasificación y documentación.
7. **B7 · Consentimiento por teléfono.** En contratos/validador/importador WhatsApp, `OrganizationUserInvitationWriter`, invitación y migración, agregar `consentConfirmed`, actor y fecha; copiar el importador actual. Probar 400 sin casilla/nada enviado, sellado con casilla y OpenAPI.
8. **B8 · Configuración y arnés corto.** En `appsettings.Development.json` y `HarnessTests`, llevar Debug a Warning y limitar a 30 KB cada cadena de `AGENTS.md`; copiar `Agent_files_are_short_and_their_links_work`. Probar configuración y límite.
9. **B9 · Arnés sin ramas.** En `scripts/arneses/comun/worktree.mjs`, `corrida.mjs`, `programar.ps1`, README e instrucciones/skills, reemplazar worktree/rama por copia local detached de `HEAD`, como `/puerta`; `--fusionar` solo hace `--ff-only`. Probar que no crea rama/worktree y que un `main` adelantado no fusiona.
10. **B10 · Una lectura por requisito y cierre.** Corregir `PermissionAuthorizationHandler` para que lea cada proyección una sola vez por requisito, como pide `docs/rules/permisos.md` para la base de la 12, con un contador de lecturas en `PermissionAuthorizationTests`. Actualizar `docs/features/whatsapp.md`, Identidad, `backend.md`, ADR 0038/0049 y plantillas. La decisión técnica es incluir esta corrección porque la ficha la fija para la base de la 12. Los ejemplos exportados y `PersistenceNamingTests` son de la E12.

**Texto para el chat del Back**

```text
Leé AGENTS.md, este plan, la spec Base de la 12, api-http, permisos, guardado, persistencia-ef, multitenancy, auditoría, textos, fechas, validación, idempotencia y tests. Después de K1, hacé B1 a B10 en orden y contra ese contrato; no programes formularios ni toques el front. Copiá los moldes nombrados, mantené WhatsApp quitable y anotá cualquier cambio imprescindible en «Cambios al contrato». Todo en main, sin push, ramas, worktrees, stash ni reset; commiteá solo lo propio con pathspec. Por cada tarea: pruebas focales, build y tests rápidos enteros del back sin Docker y sin filtrar por módulo; al terminar cambios de rutas, persistencia, autenticación o aislamiento, sus integración y seguridad. No edites src del front si Vite está levantado.
```

## Front

Las tareas siguen los tableros v169 y las fichas de UI, formatos, formularios, listados, accesibilidad y tema. Todas usan i18n, tipos generados, formatos compartidos y pruebas axe donde cambia una pantalla.

1. **F1 · Botones y esperas.** En `button`, `ConfirmDialog`, `DataTable`, `Table` y `useEditPage`, aro/gerundio sin atenuar, confirmación hasta respuesta y refresco con línea. Copiar las piezas actuales; probar éxito, fallo y campo requerido.
2. **F2 · Campana.** Crear `layouts/notifications/`, `NotificationsPage` y API contra K1, con navegación de tres accesos, teléfono y versión nueva. `ModuleRegistration` suma `notifications` (`ModuleNotificationKind {kind, describe}`), para que cada módulo arme el texto de sus avisos. Copiar los tableros; probar no leído, marcar, filtros sin conteos y axe.
3. **F3 · Tablas.** En `DataTable`, `FilterBar`, siete pantallas núcleo, WhatsApp y `ChatsWorkspace`, quitar detail de la compu en los 12 `columns.tsx`, no solo ocho. En `FilterBar`, el buscador y hasta tres filtros en una fila, y lo demás en un solo «Filtros», nunca «Más filtros», que aplica cada cambio, con lo aplicado en chips y «Limpiar todo»; un filtro de opciones es un desplegable simple, sin cantidades y con la elegida tildada, y un segmentado conserva su número (143, 144 y 164). Retirar los 20 `truncate` de 10 archivos, `title` y el corte compartido, salvo preview de Chats. Probar texto largo, CUIT, zonas, ▲/▼, líneas y alto de ocho filas.
4. **F4 · Fechas.** En `shared/format` y sus usos, reemplazar `relative`/`formatCalendar` por fecha y hora completas; copiar `formatos.md`. Probar la guarda que prohíbe ambos.
5. **F5 · Carteles y estado.** En las siete pantallas listadas por la spec, llevar carteles a banda/campana y una sola etiqueta con ⓘ; sacar renglón Estado del teléfono. Copiar tableros; probar cada pantalla.
6. **F6 · Palabras.** En locales/enums, `UserDetailPage` y `UserCompanyDialog`, aplicar el vocabulario y `Agregar empresa`; probar una guarda de textos.
7. **F7 · Otro acceso.** En `OtherAccessPage`, rutas y `returnTo`, copiar el tablero y probar ambos accesos/destino ajeno.
8. **F8 · Forma y escala.** En `index.css`, `BrandMark`, `Avatar`, `Page`, 38 usos de `icon`, `menuRow`, `FilterBar`, `button`, `Surface` y los cinco paneles publicados, aplicar seis piezas, sombras, panel liso y escala. Retirar también el degradé adicional de `PublicPageCard.tsx`. Probar tokens, 44×44 y sombras. Al programarla, el brillo de fondo de la página pública y de la portada (`--fondo-brillo`) se quedó: lo dibujan los tableros y lo pide `tema.md`. No es un panel verde ni una de las seis piezas, y ninguna decisión lo saca.
9. **F9 · Miembros de empresa.** En `CompanyDetailPage`, ocultar la pestaña sin permiso; probarla con y sin permiso.
10. **F10 · Ayuda y motivos.** En `InfoTip`, `FormField`, `Section`, `Page`, `CheckboxField` y controles apagados, mover solo ayuda útil a (i). Inventariar 43 `hint`/`description` en 35 archivos, sin tocar descripciones técnicas. Copiar `InfoButton`, `Sheet` y `RowAction.disabledReason`; probar teclado, teléfono y axe.
11. **F11 · Lo elegido.** En `SegmentedControl`, `RadioGroupField`, filtros, `DataTable` y chats, aplicar tilde, borde/barra y contraste; copiar Criterio Menús. Probar 3:1, 4,5:1 y 44×44.
12. **F12 · Carga de página/ajuste.** Crear `PageLoading` y `FormSkeleton`; integrar `routes`, `RequirePermission`, `LegalAcceptanceGate`, `ModuleRegistry` y tres ajustes. Copiar `Page`; probar banda, zonas y navegación.
13. **F13 · Decidir a propósito.** En `ReasonDialog`, `ReviewRecoveryDialog` y `ConfirmDialog`, nombrar decisión y enfocar Cancelar; copiar `ConfirmDialog`. Probar foco y título.
14. **F14 · WhatsApp de cuenta.** En `LoadPhonesPage`, API y locales, sumar casilla; retirar página/ruta/API/estado login-link y actualizar E2E/recorrido. Probar sin/con casilla y ausencia de `/login/enlace`.
15. **F15 · Capturas.** En scripts y manifiestos de galería, sumar 320 además de 1440/390; copiar captura de 390. Probar sin scroll horizontal.
16. **F16 · Elección y lectura existente.** En `OptionDialog` y locales del bot, confirmar antes de perder texto/equipo y conservar al cancelar; revisar los demás hallazgos, sin plegar lecturas. Copiar `ConfirmDialog`; probar los cuatro casos. Arreglar primero `StatusBadge.test.tsx` a `--punto-pendiente` y el mensaje de `harness.test.ts` a «1 a 3 líneas».

**Texto para el chat del Front**

```text
Leé AGENTS.md del front, este plan y la spec Base de la 12. Esperá K1, corré npm run contracts y hacé F1 a F16 contra los tipos generados, copiando los tableros v169. No toques lienzo, specs ni back, ni src si Vite está levantado. Usá i18n, shared/ui/format, TanStack Query, accesibilidad y las fichas de UI; no calcules dinero ni escribas tipos generados. Todo en main, sin push, rama, worktree, stash ni reset; commiteá solo tus rutas con pathspec. Por tarea corré pruebas focales, npx tsc -b y npm test completos, sin filtrar por módulo; al cierre de la parte, build, lint y contracts:check completos.
```

## Integración y puerta

1. **I1 · Integrar.** Front consume K1 real; cualquier diferencia se devuelve a su dueño y se anota abajo. Regenerar los tipos.
2. **I2 · Meta.** El usuario vuelve a aprobar en Meta las plantillas de cuenta con el mismo nombre y textos nuevos. No se carga ningún secreto ni se maneja su cuenta. La prueba manual usa el número de Plataforma: enviar un mensaje, verificar el botón `Entrar a mi cuenta` y que abre `/login` en el celular.
3. **I3 · Puerta.** Correr `/puerta` una vez sobre copias del corte: build y `dotnet test --no-build`; front build, lint, test y `contracts:check`; E2E real; `realinear-docs --detectar` y `estado-docs --detectar`; galería solo de pantallas cambiadas a 1440/390/320; `/recorrido`; y revisión de la lista de control. La retirada del módulo no corresponde: ya se verificó en la E11 y esta base no cambia su composición.
4. **I4 · Recorrido.** Campana de los tres accesos; una tabla, fecha y botón por área; otro acceso; baja/reactivación y borrar cuenta; cargar teléfonos sin/con casilla; y una pantalla de cada área a 320. El resultado queda en la tabla breve de esta sección.

| Puerta | Resultado |
| --- | --- |
| Back / Front / Contrato / E2E / Documentación / Galería / Recorrido | Pendiente |

**Texto para el chat de Integración**

```text
Leé AGENTS.md de ambos repos, este plan y /puerta. Integrá contra K1, resolvé solo diferencias reales con su dueño y corré la puerta una sola vez sobre copias del corte. No hagas push ni modifiques el lienzo. Para Meta pedile al usuario los pasos manuales; nunca manejes su PC ni pegues secretos. Dejá solo la tabla final corta en este plan.
```

## Relación con otras etapas

La F5 de la E10 entra completa acá; E10 sigue después del bloque. E12–E25 agregan sus propias fuentes a la campana, sin cambiar esta base. No se implementa ningún catálogo, formulario, aviso de formularios ni funcionalidad posterior. E14 prueba la CSP de la vista oficial; esta etapa no cambia `frame-src`.

**Deja para las siguientes:**
- en el back, `INotificationSource` y `GET /api/notifications`: la E14, la E21, la E23 y la E25 suman sus fuentes con el sufijo `NotificationSource`, y en el front, sus textos con `notifications` de su `registration.ts`;
- en el front:
  - `layouts/notifications/`;
  - los botones con su espera y `ConfirmDialog` con `pending` (F1);
  - `FilterBar` con un solo «Filtros», chips y «Limpiar todo», sin cantidades (F3);
  - las fechas completas (F4);
  - la escala y las piezas de forma (F8);
  - `InfoTip` (F10) y lo elegido (F11);
  - `PageLoading` y `FormSkeleton` (F12);
  - la galería a 320 (F15).

## Cambios al contrato

Del front (2026-11-09): el contrato de la API no cambió; los tipos salen de `npm run contracts` sobre K1.
- `docs/contracts/frontend-routes.json`: sin `/login/enlace` y con `/avisos` y `/plataforma/avisos` (B6, `75fe6a93`). «Otro acceso» (F7) no suma rutas: se muestra en la ruta del link.
- La campana lee `params.name` en `organizationPending` y `recoveryRequested`; sin él, el aviso va sin nombre. `personalLoginMethod` no lleva `params`.
- La ficha de una cuenta de Plataforma no trae el motivo de la baja que pidió la plataforma (`deletion` no tiene `reason`): la ⓘ de su estado usa `lastStateChange.reason` si llega y, si no, «—».

## Para decidir

No queda nada para decidir: los tableros, decisiones 43, 109–116, 122, 137, 142–144, 160–164, 173–175 y 179–190, y el tratamiento de las plantillas de Meta ya resuelven el producto.
