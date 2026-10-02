# Informe E4 · Autorización y Roles

Fecha: 2026-10-02. Trabajo en `main`, sin push.

**Estado:** Etapa 4 cerrada en `main`, sin push. El usuario aprobó las pantallas de Organización; quedaron implementadas `Roles`, `Rol`, `M-Roles`, `M-Rol` y el enlace «Roles y permisos». `HarnessStage=4` en ambos repositorios. Todas las puertas finales están verdes y no se crearon cuentas u organizaciones de ejemplo: el seed global conserva solo el operador y cada prueba/E2E usó su propia base.

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
| Pantallas Roles/Rol, navegación, contrato cruzado y diez pares visuales | front `1806055` |
| Diagnóstico por pasos del E2E de baja de método | front `4801190` |
| `HarnessStage=4` y aceptación de inventario de etapas previas | back `50ee187` |

## Puertas

- `dotnet build ArquitecturaBaseMultitenant.slnx`: correcto, **0 advertencias y 0 errores**.
- `dotnet test`: **1264 correctos, 0 errores, 0 omitidos** (4m 25s). La primera corrida recibió un 500 de Docker al inspeccionar un contenedor temporal; Docker Desktop se reinició, los dos tests del fixture pasaron aislados y la corrida completa posterior quedó verde. La configuración de producción no cambió.
- Front: `npm test` **143 archivos / 690 tests aprobados**; `npm run build` correcto; `npm run contracts:check` confirma el esquema generado; `npm run lint` termina con código 0. Persisten warnings históricos de `lienzo-v35` y uno documentado de `InvitationPage`; Roles no agrega warnings.
- `npm run generate` terminó correctamente y no dejó cambios de datos generados; `npm test` del generador: **30/30**.
- Comparación visual E4: `npm run test:capturas:etapa-4`, **2/2 tests**; dieciocho pares app/lienzo, diez de Roles/Rol, en 1440×900 y 390×844.
- Arnés: `HarnessStage=4` en los dos repositorios; los tests existentes de back y front pasan. Puerta documental A: se generó, verificó y eliminó el esqueleto temporal `Tags`. Puerta B: diez preguntas contestadas leyendo solo `AGENTS.md`; una respuesta ubicó `docs/guides/` sin decir el archivo, registrada en backlog #73.
- `aspire ps --format Json --non-interactive`: `[]` después del E2E integral final.

## Salida literal del E2E real final

Comando: `npm run test:e2e:real`. Se usaron navegador, Aspire, API, PostgreSQL y pickup aislados, sin mocks ni intercepts. Los dos recorridos de invitación leyeron enlaces de `.eml`; para la invitación sin cuenta, el test comprobó que no se creara espacio Personal.

```text
> arquitecturabase-multitenant-front@0.0.0 test:e2e:real
> node --test scripts/test-e2e-real.test.mjs

▶ registro real y puerta empresa usan un PostgreSQL aislado, front, Api y pickup sin mocks
  ✔ 4: administrar roles y bloquear la baja del único Dueño (115974.7525ms)
  ✔ 3c: aceptar invitación sin cuenta y sin Personal desde el .eml (13345.8219ms)
  ✔ 3c: aceptar invitación con cuenta previa desde el .eml (18982.0713ms)
  ✔ 3b seguridad: sesión abierta no suma correo ni Google y método nuevo no autoriza el anterior (77476.7094ms)
  ✔ 3b seguridad: registro con posesión recupera un correo pendiente de otra cuenta (151168.1103ms)
  ✔ 3b: sumar correo personal con código leído del .eml (78705.9185ms)
  ✔ 3b: quitar un método con código enviado a otro (130188.4413ms)
  ✔ 3b: pedir baja y cancelarla ingresando durante la gracia (130239.1821ms)
  ✔ 3b: cambiar el idioma a en-US desde /cuenta (13110.9429ms)
  ✔ 3b: aceptar versión nueva de términos que bloquea el ingreso (9979.2322ms)
✔ registro real y puerta empresa usan un PostgreSQL aislado, front, Api y pickup sin mocks (816564.8511ms)
ℹ tests 11
ℹ suites 0
ℹ pass 11
ℹ fail 0
ℹ cancelled 0
ℹ skipped 0
ℹ todo 0
ℹ duration_ms 818936.5327
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
- El panel Administración de la maqueta incluye Usuarios, Empresas, Configuración, Página pública, WhatsApp y Auditoría; la app muestra Roles y permisos porque las otras áreas esperan etapas posteriores.
- La creación ofrece «Toda la organización». Los alcances por empresa esperan el catálogo/gestión de E6; la API aún no tiene el nombre de una empresa para representar roles `SpecificCompany`, así que muestra «Una empresa específica».
- En móvil la segunda línea de la banda muestra «Vale en … · De sistema» y puede truncarse por el ancho; la maqueta prioriza «De sistema». Se mantiene toda la información y los controles disponibles.
- Las casillas deshabilitadas de Roles de sistema conservan el tono de marca con menor opacidad, mientras que la maqueta usa gris. Ambos estados indican solo lectura.

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
- Los códigos `TenantAdmin` y `CompanyAdmin` se conservan en la API y se presentan como «Dueño» y «Administrador» desde recursos es/en; el Dueño se determina únicamente por su asignación de rol.
- `roles.read` habilita la navegación, el listado y el catálogo; `roles.manage` permite alta, edición y baja de roles propios. Los roles de sistema quedan de solo lectura en el front.
- Hasta E6, el formulario solo permite crear roles para toda la organización. En la edición se conserva un `CompanyId` existente sin exponer un selector que todavía no tiene catálogo.
- El nombre del rol tiene límite de 60 caracteres, alineado con `TextLimits.ShortName` del back; el test de pantalla verifica que no se pueda exceder.
- Una corrida integral inicial topó un `Auth.LoginCode.ResendTooSoon` al quitar un método. El mismo recorrido real pasó aislado y en la corrida final completa; se agregaron marcas de etapa al E2E para precisar el paso si reaparece. No se alteraron los límites de producción.
- `columns.tsx` exporta el arreglo tipado de definiciones móviles y construye etiquetas traducidas a partir de ese arreglo, siguiendo la verificación compartida de columnas que ya existía.

T10 quedó aprobado por el usuario, implementado y verificado. El trabajo final queda en dos repositorios limpios, con commits locales y sin push.
