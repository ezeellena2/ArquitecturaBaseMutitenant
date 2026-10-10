# E8 B7 · Conflicto de destino al aprobar una recuperación

Cuando el destino se verifica en una cuenta después de registrar una recuperación, la aprobación devuelve 409 `Platform.AccountRecovery.MethodAlreadyOnAccount` si pertenece a la cuenta que se recupera, o `Platform.AccountRecovery.MethodInUse` si pertenece a otra. Phone y Email, incluido el contacto verificado de Google, usan la misma respuesta sin errores de campo. El pedido permanece Pending con su versión original y el rechazo no cambia las cuentas, sus métodos y principal, el outbox, la auditoría ni las marcas de aviso.

## Decisiones tomadas

- `AccountRecoveryGuard.CheckNewAsync` conserva primero la validación del destino y sus políticas de canal/país. Después distingue propiedad propia/ajena cuando recibe `recoveringUserId`; sin ese argumento conserva los 400 públicos y `errors.phone`/`errors.email` existentes. No cambia consultas de propiedad, locks ni límite transaccional.
- Se reutilizan los dos códigos y claves de recursos existentes. Español e inglés dicen ahora «método de ingreso» / “sign-in method”. El público Email mantiene 400 y `errors.email`, con esa precisión de redacción; Phone público conserva su texto y código propios. No se introducen códigos, campos, recursos nuevos ni un override de traducción.
- `PlatformRecoveryDecisionService` conserva su única UoW `OnSuccess`, su orden de locks y la comparación del `xmin` leído. No se rebasa la versión ni se modifica el pedido para permitir el rechazo. El fallo del guard sigue siendo anterior a retirar un pendiente, decidir, agregar método, cambiar principal, auditar o encolar avisos. No requiere migración.
- La nueva clase de integración usa una `ApiFactory` y base Docker propia por caso, Testing explícito, sin hosted workers y con transporte WhatsApp prohibido. Todas las cuentas y el operador se preparan antes del OTP Phone para que el pickup Email no despache un outbox Phone pendiente. La ocupación legítima del destino usa otra transacción y el linker existente.
- La instantánea se toma **después** de ocupar el destino. Se comparan completos el pedido y sus campos/versión/decisión, ambos usuarios, sus métodos y principal, todos los mensajes del outbox, eventos de seguridad y marcas de aviso; también la fila HTTP original. Se comprueban 409, `code`, `traceId`, ausencia de `errors`, cookie, destino y dueño en ProblemDetails. Cada caso se aprueba en es-AR y en-US con claves idempotentes distintas: doce respuestas HTTP entre seis casos.
- La carrera existente alta Phone × aprobación exige ahora el error de la operación que pierde: aprobación 409 `MethodInUse`, alta 400 `Auth.Signup.PhoneTaken`, con un único dueño. Las expectativas literales públicas de Email se ajustan a la misma redacción del recurso.

## Verificación focal

Pruebas HTTP y PostgreSQL Docker reales; no Meta externo. Los conteos de filas son disjuntos y las repeticiones se reemplazan, no se suman.

| Evidencia | Casos correctos / total | Exit | Duración MTP |
|---|---:|---:|---:|
| RED real: Phone propio tomado después de Submit | 0/1 | 2 | 33,852 s |
| Conflictos finales Phone/Email/Google × propio/ajeno, es/en | 6/6 | 0 | 88,515 s |
| `WhatsAppRecoveryTests`: públicos Phone 400, prueba/Submit, países, carreras y estados | 16/16 | 0 | 87,666 s |
| `PlatformRecoveryTests`: permisos, estados, aprobación/rechazo, versión N1 y decisiones simultáneas | 9/9 | 0 | 54,829 s |
| `Auth.AccountRecoveryTests`: públicos, Google, Submit y rollback de aviso N1 | 7/7 | 0 | 35,555 s |
| `EmailAccountStatesTests`: estados públicos y propiedad revalidada | 6/6 | 0 | 36,006 s |
| `ResourceParityTests`, DLL nueva | 7/7 | 0 | 2,543 s |
| `ErrorCodeTests`, DLL nueva | 8/8 | 0 | 1,857 s |

Total verde: **44 HTTP únicos + 15 guardas = 59**, sin fallos ni omitidos. La primera pasada de los seis conflictos, antes de precisar los textos, fue 6/6 en 89,894 s; la pasada final localizada la reemplaza en el conteo. `AccountRecoveryTests` pasó antes de recompilar la redacción; su único caso literal afectado se repitió con la DLL nueva: **1/1**, exit 0, 28,033 s. Esos siete casos cuentan una sola vez. No se repitieron las regresiones Phone/Plataforma, cuyos asserts y conducta no cambió la precisión del texto.

El RED original llegó hasta la aprobación real de un pedido Pending, con la versión HTTP original y el destino ocupado legítimamente después de Submit: `Expected: Conflict`, `Actual: BadRequest`, y log `Failed ApproveAccountRecovery: Platform.AccountRecovery.PhoneMethodInUse`. No es un fallo de fixture ni de tooling.

Builds seriales con analyzers, `--no-restore -m:1 -nr:false -v:minimal`, **0 advertencias y 0 errores**. El build final de integración tras resources tomó 74,257 s; después de ajustar las expectativas públicas restantes, los builds de Application.UnitTests, ArchitectureTests y Api.IntegrationTests tomaron 8,027 s, 9,155 s y 18,321 s. Incluyen Application, Infrastructure y API. `git diff --check` verifica las rutas propias.

Comandos de conducta y build principales, conservados completos con logs, duración de comando, contadores y exits en el inventario local:

```powershell
dotnet build tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/ArquitecturaBaseMultitenant.Api.IntegrationTests.csproj --no-restore -m:1 -nr:false -v:minimal
dotnet test --project tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/ArquitecturaBaseMultitenant.Api.IntegrationTests.csproj --no-build -- --filter-class '*PlatformRecoveryDestinationConflictTests' --filter-method '*Approval_rejects_a_phone_acquired_by_the_recovering_account_after_submission'
dotnet test --project tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/ArquitecturaBaseMultitenant.Api.IntegrationTests.csproj --no-build -- --filter-class '*PlatformRecoveryDestinationConflictTests'
```

## Tooling y contrato

Se conservaron por separado: build inicial con una directiva redundante `IDE0005` (exit 1, corregida antes del RED); Docker sandbox sin acceso a su pipe (exit 1); las dos guardas sin pruebas ejecutadas por denegación del pipe IPC de MTP (exit -532462766, luego verdes escaladas); y el primer intento de restaurar el contrato con `execFileSync` por `ENOBUFS` (exit 1, sin escritura). Estos fallos no integran el RED de conducta ni los contadores verdes. Algunos builds sandbox registran una denegación no fatal del key ring de Data Protection durante la generación de OpenAPI; el compilador termina 0/0.

El rebuild con resources genera un delta de ejemplos HTTP, conservado sólo como evidencia local (`e8-recovery-conflict-generated-contract-delta.log` y `…generated-contract-intermediate.json`, SHA256 `469bd1ec819166a88a19fa6218d93b501cea605deaf0c6cadfca9cd5e872f6ad`). Después de todos los builds se repuso el blob exacto de la base `ed3a44db5c9148639719f88200362a6e11c098df` mediante `git show` como Buffer y `writeFileSync`, con buffer ampliado a 64 MiB; conserva LF y sin diff contractual.

`docs/contracts/openapi.json` conserva SHA256 `0ae765f28aab2dd6498b57e111b02ef8e2ba2fe5291065d7b74618eef28bdbf3`. El `Role.cs` ajeno conserva `3260a9378a8bb68317e1d92ce81e290435d29b7d030624d72dfed470a114039a`. M3 actualizará una sola vez los ejemplos y el inventario de códigos reales de aprobación: Phone ocupado 409 propio/ajeno, incluida `MethodAlreadyOnAccount`; no se publica un contrato intermedio en B7.

## Alcance y entrega

Este correctivo no cierra E8. Suites completas, E2E, galería, recorrido y revisiones finales pertenecen a la puerta de la etapa. No se modificó Front ni planes de coordinación, y no se inició Aspire. Root recibe las diez rutas propias con sus SHA en `.local/reviews/e8b-contract/e8-recovery-owner-conflict-files.json` para auditar/publicar; el implementador no ejecuta stage, commit ni push. Evidencia local: `e8-recovery-conflict-*`.

Para M2 queda registrada la referencia dura de `PlatformRecoveryDestinationConflictTests` a `Modules.WhatsApp.WhatsAppIdentityTests.CodeAsync`: separar los casos Phone y sus helpers de composición quitable, preservando las pruebas Email/Google del núcleo. No se abre ese refactor en B7.
