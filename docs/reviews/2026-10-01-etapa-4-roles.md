# Informe E4 · Autorización y Roles

Fecha: 2026-10-02. Trabajo en `main`, sin push.

**Estado:** el back de E4, los siete Criterio compartidos, `/cuenta`, las guías y las puertas automáticas quedaron verificados. Las pantallas `Roles`, `Rol`, `M-Roles`, `M-Rol` y el enlace del AdminPanel siguen pendientes de aprobación; E4 no se declara cerrada y `HarnessStage` permanece en 3 en ambos repositorios. No se crearon cuentas ni organizaciones de ejemplo: el seed global conserva solo el operador y cada prueba usó datos propios.

## Relevamiento inicial

- Antes de iniciar el trabajo de E4, ambos repositorios estaban limpios; últimos commits de base: back `df14d4f`, front `ca48815`.
- Las Etapas 0, 1, 2, 3a, 3b y 3c estaban cerradas con sus puertas. No se detectó un bloqueo de flujo real ni de seguridad.
- La E4 resolvió lo relacionado con los Criterio v60 y `/cuenta`. La puerta ciega encontró una imprecisión documental menor, registrada como backlog #73.
- No hay cambios al plan maestro que alteren el orden 4 → 6 → 5 → 7. Este plan de etapa se actualizó para reflejar que las capturas E4 son de `/cuenta`; Criterio se cubre con pruebas de componentes.

## Tareas y commits

| Tarea | Commits |
|---|---|
| Plan detallado antes de programar y primer E2E rojo de Roles | back `613483e`; front `ca48815` |
| Criterio v60 y componentes compartidos | front `1425771` |
| `/cuenta` y diálogos según Perfil personal | front `bc332ce`, `7731cd1` |
| Dominio y persistencia de roles por organización | back `7766949`, `df14d4f` |
| RoleService, permisos, atributos, contratos HTTP, versión y 409 | back `4d02525`, `72ff78f`; contrato cruzado front `41313a5` |
| Guardar al último Dueño y proteger `Legal.AccountDeletion.LastAdmin` | back `f00646c`; recorrido real front `fc08692` |
| Límites de rate limit aislados para la suite TestServer | back `6f46417` |
| Guías, ficha de Roles y punteros/puerta del arnés | back `d964bd5`; front `41313a5` |
| Comparaciones visuales de `/cuenta`, ocho pares y prueba de puerta | front `315a64f` |

## Puertas

- `dotnet build ArquitecturaBaseMultitenant.slnx`: correcto, **0 advertencias y 0 errores**.
- `dotnet test ArquitecturaBaseMultitenant.slnx --no-build --no-restore`: **1264 correctos, 0 errores, 0 omitidos**. La primera corrida encontró cuatro 429 por compartir la IP del TestServer; `ApiFactory` eleva solo los límites del host de pruebas a 1000, mientras que los tests específicos de rate limit fijan sus límites. Producción no cambia.
- Front: `npm test` **141 archivos / 684 tests aprobados**; `npm run build` correcto; `npm run contracts:check` confirma el esquema generado; `npm run lint` termina con código 0. Permanecen los warnings ya documentados del archivo histórico `lienzo-v35` y `InvitationPage`.
- `npm run generate` terminó correctamente y no dejó cambios de datos generados; `npm test` del generador: **30/30**.
- Comparación visual E4: `npm run test:capturas:etapa-4`, **2/2 tests**; ocho pares app/lienzo en 1440×900 y 390×844.
- Arnés: los tests específicos existentes de back y front pasan. Puerta documental A: se generó, verificó y eliminó el esqueleto temporal `Tags`. Puerta B: diez preguntas contestadas leyendo solo `AGENTS.md`; una respuesta ubicó la carpeta `docs/guides/` sin decir el archivo, registrada en backlog #73.
- `aspire ps --format Json --non-interactive`: `[]` después de la prueba real.

## Salida literal del E2E real final

Comando: `npm run test:e2e:real`. Se usaron navegador, Aspire, API, PostgreSQL y pickup aislados, sin mocks ni intercepts. Los dos recorridos de invitación leyeron enlaces de `.eml`; para la invitación sin cuenta, el test comprobó que no se creara espacio Personal.

```text
> arquitecturabase-multitenant-front@0.0.0 test:e2e:real
> node --test scripts/test-e2e-real.test.mjs

▶ registro real y puerta empresa usan un PostgreSQL aislado, front, Api y pickup sin mocks
  ✔ 4: administrar roles y bloquear la baja del único Dueño (116269.8336ms)
  ✔ 3c: aceptar invitación sin cuenta y sin Personal desde el .eml (12335.0638ms)
  ✔ 3c: aceptar invitación con cuenta previa desde el .eml (19003.624ms)
  ✔ 3b seguridad: sesión abierta no suma correo ni Google y método nuevo no autoriza el anterior (78754.2064ms)
  ✔ 3b seguridad: registro con posesión recupera un correo pendiente de otra cuenta (150690.3582ms)
  ✔ 3b: sumar correo personal con código leído del .eml (79150.8639ms)
  ✔ 3b: quitar un método con código enviado a otro (129809.4455ms)
  ✔ 3b: pedir baja y cancelarla ingresando durante la gracia (130463.7447ms)
  ✔ 3b: cambiar el idioma a en-US desde /cuenta (11736.412ms)
  ✔ 3b: aceptar versión nueva de términos que bloquea el ingreso (9986.2928ms)
✔ registro real y puerta empresa usan un PostgreSQL aislado, front, Api y pickup sin mocks (791657.67ms)
ℹ tests 11
ℹ suites 0
ℹ pass 11
ℹ fail 0
ℹ cancelled 0
ℹ skipped 0
ℹ todo 0
ℹ duration_ms 792137.2177
```

## Diferencias con el lienzo y motivo

- No hay tableros móviles separados para los diálogos. En las capturas a 390×844, `M-Cuenta` sirve como referencia del marco, no como equivalencia del estado del diálogo.
- Radix presenta los diálogos móviles como panel inferior con fondo desenfocado; el lienzo usa oscurecimiento sólido. Se conserva el comportamiento responsivo compartido.
- Agregar un correo pide reautenticación antes del formulario; el tablero no la muestra, pero la API la exige para cambiar métodos de acceso.
- No se precargan correos ni códigos de la app: son datos personales y secretos; los textos del lienzo son ilustrativos.
- La cuenta muestra Email y Google. WhatsApp y la gestión real de métodos Phone esperan E8, cuando exista el canal para emitir y verificar códigos.
- «Exportar mis datos» espera E10 y su endpoint; no se dibuja como una acción inerte.
- `M-Cuenta` no dibuja el bloque Privacidad. La app mantiene la baja disponible en teléfono para no ocultar un flujo real; el lienzo no define el bloque en móvil.
- La baja usa botón delineado rojo en la fila y acción roja llena en el diálogo. Se oculta ✕ en la confirmación irreversible según Criterio de diálogos; el texto del botón coincide con «Dar de baja».
- El marco conserva los controles de navegación y usuario de la app; se verifican contra `Criterio-Marco` y cambian según viewport.

Las diferencias de E8 y E10 ya tienen etapa en el plan maestro y no agregan deuda al backlog.

## Decisiones tomadas

- `Role` y `RoleAssignment` son privados por organización, con RLS y versión. `TenantAdmin`/`CompanyAdmin` son plantillas de sistema provisionadas por organización; no hay tenant ficticio en el seed global.
- `TenantAdmin` en `RoleAssignment` es la fuente de verdad de Dueño. No se agrega flag a `Member`; la proyección de perfil se mantiene desde la asignación dentro de la transacción.
- `CompanyId` queda como GUID opcional sin FK hasta E6, cuando exista `Company`; E4 no asigna roles de empresa.
- La entidad `Role` se define localmente: ArquitecturaBase tiene catálogo y permisos, pero no una entidad `Role` de dominio para reutilizar.
- La unicidad de nombres se basa en `NormalizedName` con NFC y mayúsculas invariantes; la concurrencia se cierra en base. La versión usa `xmin`.
- `LastTenantAdminGuard` reutiliza la lectura de Dueños activos y serializa el conteo con los cambios de asignación. Solo cuentan identidad, Member y organización activos; el bloqueo ocurre al solicitar la baja y no consume el comprobante.
- El test de baja del E2E espera el `retryAfter` real antes de leer el `.eml`; `ResendTooSoon` era el cooldown por correo tras el login, no un rechazo de la regla de Dueño.
- El límite de 1000 en `ApiFactory` existe solo para evitar que los tests normales agoten la IP compartida de TestServer. Los límites de producción y las pruebas específicas permanecen intactos.

## Pendiente de aprobación

No se creó `areas/business/roles`, las páginas Roles/Rol ni el enlace «Roles y permisos» del AdminPanel. Sus tableros de Organización siguen «· para aprobar». Una vez aprobados, queda T10: implementar esos estados y compararlos en escritorio/móvil; recién entonces correr el cierre completo de E4 y subir `HarnessStage` a 4 en ambos repositorios.
