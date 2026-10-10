# E8b M1 · permisos del Asistente para operadores anteriores

Base: `57e555c7fe7395b7541af41eb113f77e72db6d64` (par E8 etiquetado `puerta-8-1`). Correctivo autorizado por la decisión humana del 10/10: las asignaciones que ya conceden `platform.operators.manage` reciben `platform.assistant.use`, `platform.assistant.configure` y `platform.assistant.read` por una migración de datos. No se otorgan esos permisos en cada seed.

## Resultado

EF CLI generó `20261010214712_PlatformOperatorAssistantPermissions` y su Designer en la carpeta única de migraciones. El snapshot no cambió: no hay tablas, columnas, roles ni contratos nuevos. La migración conserva ID/UserId y el array previo, incluidos orden, otros permisos y duplicados ajenos; agrega sólo las claves faltantes, en orden use/configure/read. No crea asignaciones para indicadores sin fila ni modifica las asignaciones sin `platform.operators.manage`. No modifica identidades ni sus estados, incluidas cuentas suspendidas e invitaciones pendientes.

La CTE actualiza únicamente filas con faltantes e inserta un `PlatformOperatorPermissionsChanged` por cada fila realmente modificada, en la misma sentencia/transacción de la migración. El actor es System, ActorId vacío, TargetAccountId es el usuario destinatario, el motivo técnico es `Platform operator assistant permissions upgrade` y Changes contiene exclusivamente booleanos before=false/after=true de las claves agregadas. Tenant e IP quedan null. Si la inserción del evento falla, se revierten los grants y el historial EF. No se inventa una acción de un operador ni se borra evidencia.

## Decisiones tomadas

- Aplicar el upgrade antes de aceptar tráfico, con `mt_owner` no superusuario y sin BYPASSRLS. Las asignaciones son globales; no se toca RLS ni el alcance de organización. Los grants del runtime y la FK de identidad se conservan.
- Usar `uuidv7()` nativo, disponible en [PostgreSQL 18](https://www.postgresql.org/docs/18/functions-uuid.html) y ejercitado en PostgreSQL 18.3 real. Mantiene UUID v7 sin extensión ni cambio de la factory runtime. CURRENT_TIMESTAMP pertenece a la transacción migratoria, no a un servicio de Application.
- Down no-op para permisos y eventos, como la aditiva de roles: no existe procedencia que permita distinguir un grant previo/manual de uno agregado por este upgrade. No agregar tablas de recibos ni adivinar qué revocar. La retirada del módulo tiene su migración y preflight propios; volver a un binario anterior requiere revisar compatibilidad y no se acredita aquí. Up repetido y Down→Up conservan grants/evidencia sin duplicarlos cuando no hubo una edición intermedia; volver a aplicar deliberadamente la migración evalúa las asignaciones de ese momento.
- Mantener `CannotModifySelf` y `LastManager`, la edición individual posterior, el catálogo completo para un primer operador nuevo y `LegacyBootstrap` para reparar un indicador sin fila. El seed conserva una asignación existente, incluso si se revocó un permiso del Asistente. El reader AsNoTracking consulta permisos sin caché revocable: no hace falta un namespace nuevo de caché.
- Mantener en la receta de retiro el aborto si quitar los tres códigos dejaría una asignación vacía; un administrador autorizado debe asignar primero un permiso núcleo elegido explícitamente. No borrar assignments ni conceder permisos automáticamente. Las recetas de ambos módulos apuntan a la nueva última migración y exigen confirmar el corte real. Esto no acredita la retirada física de módulos.

## Pruebas de conducta

El nuevo test de datos prepara una base con las migraciones reales y asignaciones anteriores: gestor LegacyBootstrap; no gestor; gestores con uno, dos y los tres permisos ya presentes; gestor suspendido con orden y duplicado ajeno deliberados; gestor con invitación pendiente e indicador sin asignación. Verifica catálogo exacto de nueve claves núcleo más tres del módulo, identidad/estado/asignación, sólo faltantes, un evento por fila, diff exacto, rol mt_owner sin bypass, FK/grants y ACL append-only de SecurityEvents. Ejerce Up, SQL directo de Up por segunda vez, Down→Up y modelo sin cambios pendientes. El test de fallo de auditoría instala un trigger sólo de test, rechaza el INSERT de evidencia y demuestra rollback de permisos, eventos e historial; retira ese trigger y completa el upgrade.

El HTTP focal muestra 403 antes del upgrade y acceso posterior a modelos/configuración sin clave. Uso del historial exige primero el aviso explícito: 400 `Assistant.Notice.AcceptanceRequired`, POST de aceptación y luego 200. PUT de configuración habilita personalEnabled sin clave. GET de Uso conserva 501: su implementación queda pendiente. La autoedición sigue devolviendo `Users.User.CannotModifySelf`; un segundo administrador revoca use/configure/read por HTTP con versión y motivo, y cada ruta vuelve a 403 inmediatamente. Repetir seed y construir otro host conserva las tres revocaciones.

## Resultados originales

Logs y comandos completos en `.local/reviews/e8b-operator-upgrade/`; SHA256, bytes, exits, tiempos y clasificación en `.local/reviews/e8b-contract/e8b-operator-upgrade-files.json`.

| Ejecución | Total / verdes / fallos / omitidos | Exit | Duración del runner |
|---|---:|---:|---:|
| RED real, Up vacío | 1 / 0 / 1 / 0 | 2 | 45,178 s |
| Primera ejecución de los tres nuevos | 3 / 1 / 2 / 0 | 2 | 58,066 s |
| Datos/rollback verdes, fixture HTTP aún incorrecta | 3 / 2 / 1 / 0 | 2 | 57,738 s |
| HTTP focal después de corregir el aviso | 1 / 1 / 0 / 0 | 0 | 38,075 s |
| PlatformOperatorTests | 7 / 7 / 0 / 0 | 0 | 50,350 s |
| LastManager y autosuspensión | 1 / 1 / 0 / 0 | 0 | 41,868 s |
| MigrationsTests | 4 / 4 / 0 / 0 | 0 | 11,616 s |
| DI instalado del Asistente | 1 / 1 / 0 / 0 | 0 | 25,412 s |
| PlatformPermissionServiceTests | 7 / 7 / 0 / 0 | 0 | 2,766 s |
| PermissionTextsTests es/en | 65 / 65 / 0 / 0 | 0 | 1,764 s |
| ResourceParityTests | 7 / 7 / 0 / 0 | 0 | 1,840 s |

**95 casos distintos finalmente verdes**: tres nuevos y 92 regresiones. Son 100 ejecuciones reales: 96 éxitos (incluye una repetición del rollback) y cuatro fallos históricos (un RED de conducta y tres expectativas de fixture corregidas). No se vuelve a contar un caso por reejecutarlo: los dos verdes de datos/rollback se retienen y sólo se repitió el HTTP tras su última corrección.

Los tres fallos de fixture fueron suponer un trigger DB histórico de SecurityEvents y, dos veces, interpretar el 400 del historial sin leer su código. Se corrigió el test a ACL SELECT/INSERT sin UPDATE/DELETE y al aviso explícito que exige AssistantConversationPolicy. No se cambió runtime para acomodar la fixture.

Build serial del alcance Infrastructure/Api/IntegrationTests: exit 0, cero warnings/errores (20,810 s en la última compilación, con generación OpenAPI omitida en las recompilaciones de tests). Api con generación OpenAPI también había pasado en la compilación inicial serial (63,720 s); el log contiene una limitación de DataProtection en el sandbox durante la generación, sin fallo ni cambio del export. Build de Application.UnitTests para regresiones: exit 0, cero warnings/errores (6,610 s). `has-pending-model-changes`: exit 0, modelo sin cambios. Script incremental idempotente de CLI: exit 0, revisado; actualiza sólo el array seleccionado e inserta eventos/historial dentro de la transacción, sin DDL ni cambios de grants/FK/RLS.

Se conservan también los intentos de tooling: migrations add con build y un build inicial devolvieron exit 1 sin diagnóstico; la ejecución test con build devolvió exit 1 sin tests; un build de mi test falló por EF1003 y dos xUnit2031, corregidos. Un `--no-build` en sandbox terminó con exit -532462766 por NamedPipeClient y **cero pruebas**. Ninguno se presenta como RED. Los tests reales se ejecutaron con la escalación autorizada para IPC/Docker; no hubo rechazo del auto-review.

## Límites y archivos protegidos

No se cambió runtime, seed, Domain, guards, DTO ni contrato HTTP. No se ejecutaron suites completas, detector documental global, Aspire, E2E, proveedores reales, Front ni retiro físico: la única puerta E8 coordinada corre aparte sobre su par fijo. No se stageó, commiteó ni pusheó; Root audita y publica este alcance.

`Role.cs` ajeno conserva rawSHA256 `3260a9378a8bb68317e1d92ce81e290435d29b7d030624d72dfed470a114039a`. El export OpenAPI conserva `46503255f38de831994887154187275f037a3682974976fc2aa920a95313f3d6`. Snapshot EF conserva exactamente el contenido del blob base al comparar LF, incluido su BOM UTF-8, con git diff vacío; el checkout tiene CRLF (264824 bytes) y el blob Git LF (258727 bytes), de modo que se registran ambos hashes sin confundirlos con igualdad raw. No se normalizó ni se incluye el snapshot para stage. No se tocaron planes ni documentos de coordinación, etiquetas o copias de la puerta.
