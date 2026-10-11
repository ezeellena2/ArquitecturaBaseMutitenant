# E8b · CC05/CC06: contrato verificado para publicación

Autorización humana directa del 10/10: «Sí, publicar CC05 y CC06 y regenerar tipos», conservada en `.local/reviews/e8b-contract/cc05-cc06-and-durable-retention-human-authorization.json`. Entrega sobre HEAD vivo `35f4810616608befabd926a79242d68467a73f10`; se preservaron los commits de documentación posteriores a `91d652c1`. Root audita y publica las siete fuentes propias, sin push; la regeneración de los dos archivos de tipos corresponde al owner Front después de recibir el commit publicado. Este informe no afirma que esa publicación o regeneración ya haya ocurrido.

## Cambio verificado

- CC05: sólo InputTokens, OutputTokens, CacheWriteTokens y CacheReadTokens de `AssistantUsageRowResponse` pasan de int a long, integer/int32 a integer/int64. Conservan nombres, required y JSON numérico. Los cuatro roundtrips con JsonConfiguration real conservan exactamente `2457600000`; no strings, coerción, truncamiento ni saturación. Queries, PeopleCount, OrganizationCount, TotalQueries y AssistantTurnUsageResponse conservan int32.
- CC06: State incorpora al final `MonthlyBudgetProgress`, sin default, requerida y nullable del tipo existente `AssistantBudgetProgressResponse`. Consumer/Platform null; Business divide costo registrado confirmado o conservador por presupuesto snapshot de la misma financiación efectiva y mes civil. Excluye reservas, suma de financiaciones, repricing y recorte. Presupuesto no aplicable/cero/null produce null. Sin configurar conserva precedencia; consultas fijas siguen disponibles y State no garantiza admisión.
- Los tres success previos siguen configured=false, models=[], quickQueries=[], remainingMessages=1 y presupuestos/costos null; sólo se agregó monthlyBudgetProgress=null. Diez ejemplos nuevos Business, cinco pares es/en, usan presupuesto USD10 y costo 0/7,9/8/10/12 para progreso 0/0,79/0,8/1/1,2 y status WithinLimit/WithinLimit/NearLimit/LimitReached/LimitReached. Configured=true incluye una opción de modelo de ejemplo con ID contractual fijo y nombre localizado, sin proveedor factual ni consultas ficticias.

State y Usage reales siguen en 501 hasta T05c/T06b. Las pruebas DTO no acreditan HTTP200, cálculo runtime, permisos efectivos ni integración financiera. No se cambiaron rutas, permisos, requests, gates, SSE, runtime, base, migraciones, Front ni proveedor. No cierra E8/E8b ni sustituye la puerta de Coordinación.

## Verificación

| Evidencia local | Resultado real |
|---|---|
| 01-red-build | Tooling: exit1, 0 warnings/0 errores reportados, sin diagnóstico; no es RED. |
| 02-red-build-serial | Build Architecture antes de producto: exit0, 0 warnings/0 errores; export original idéntico. Log de entorno DataProtection UnauthorizedAccess preservado. |
| 03-red-contract | Sandbox: named pipe denegado, 0 tests; no es RED. Status nativo unsigned `3762504530` (signed `-532462766`); herramienta exterior salió1. |
| 04-red-contract-permissions | RED real tras escalación: 27 casos, 10 previos verdes y 17 nuevos rojos, 0 skips, exit2. Fallos por Int32 fuera de rango y State/required/ejemplos ausentes. |
| 05-green-api-build / 06-green-architecture-build | Builds seriales API/export real y Architecture: exit0, 0 warnings/0 errores. |
| 07-green-contract | GREEN: 27/27, incluidos los mismos 17 nuevos, 0 skips, exit0. |
| 09-green-contract-guards | Guardas Architecture pertinentes: 42/42, 0 skips, exit0. Disjuntas de los 27 anteriores: **69 casos distintos**; no sumar RED ni reintentos como casos nuevos. |
| 08-semantic-audit | 22 punteros semánticos exactos autorizados, 0 fuera, schemas existentes idénticos salvo State y cuatro formatos. |
| 10-process-audit | Lectura CIM verificada tras escalación: 0 procesos API/build/test activos. La lectura CIM inicial en sandbox falló y su array vacío no se usa como evidencia de procesos0. |
| 11-diff-check / 12-inventory-validation | Diff check e inventario: exit0. El primer intento del script local de inventario salió1 porque trim quitaba el espacio inicial de porcelain; corregido a trimEnd, sin cambios de producto. Se conserva esa falla en inventory-initial-tooling-failure.json; esa invocación inicial no cronometrada tiene UTC null explícito, sin inventar tiempos. |

Las guardas efectivamente ejecutadas cubren AssistantContract, AssistantEditableCatalogContract, ControllerInputContract, ControllerOutputContract, VersionedContract, ModuleControllers y AccessDeclaration. El filtro adicional PermissionAuthorizationTests no aportó casos: esa clase vive en Api.IntegrationTests y no se ejecutó. No se acreditan sus tests ni se añade Docker por un cambio de forma contractual.

Comandos exactos, inicio/fin UTC, exit code, bytes y SHA de logs: `.local/reviews/e8b-contract/cc05-cc06-publication-files.json` y `.local/reviews/e8b-contract/cc05-cc06-publication/*.json`. No hay tests omitidos ni suite global/detector/recorrido/E2E propios. Los fallos de tooling se conservan separados de RED/GREEN. Cada build/test terminó antes del siguiente.

## Delta y hashes

Base OpenAPI raw y Git bytes: SHA-256 `86428a7254f5e7f6f412fb06c63e05287655e03f81e17c49ce4a8c88f81928a0`, 2.205.156 bytes. Export real docs y obj: SHA-256 `96e0f10b7aefdf085ea8ceca0cd2bf57e83261e9eb452ba4f5ad379522575a45`, 2.223.830 bytes, igualdad byte a byte. A2, OTP78, CC01–04, Money, restantes ejemplos y metadata permanecen semánticamente idénticos.

Los 22 punteros son cuatro formatos token; State.properties.monthlyBudgetProgress y required[11]; tres descripciones GET State; tres success.value.monthlyBudgetProgress; diez ejemplos Business nuevos completos. La lista exacta y diff completo están en `.local/reviews/e8b-contract/cc05-cc06-publication/semantic-delta.json`. No se agregó schema ni enum. Las bases raw CRLF/LF y blobs Git se inventarían por separado; git show tiene maxBuffer40MiB y todo error lanza, sin interpretar ENOBUFS como archivo nuevo.

Role.cs ajeno conserva SHA-256 raw `3260a9378a8bb68317e1d92ce81e290435d29b7d030624d72dfed470a114039a`. Índice vacío al entregar. No se toca el helper local de retiro ni archivos de Coordinación. Los hashes binarios/raw y las siete fuentes exactas están en el inventario.

## Decisiones tomadas

Se aplicaron las decisiones CC05/CC06 autorizadas, sin decisiones nuevas de producto. Se mantuvo el guard CC04 y se cambió sólo su expectativa de los cuatro formatos a int64. Los ejemplos configured=true incluyen una opción de modelo coherente; los success previos se conservaron completos. Para verificar, se usó el serializador real API y el export generado por MSBuild; no reflexión general ni endpoint ficticio. Los accesos denegados se resolvieron mediante escalación del mismo scope, sin cambiar configuración del repo.

## Pendientes

Root audita/publica el delta y comunica commit/hash/punteros; Front regenera sus dos archivos contra ese export y verifica contracts:check. Coordinación registra la recepción y conserva su corte E8 separado. T05c/T06b deben implementar y verificar State/Uso HTTP reales con el ledger, financiación/mes/snapshot/reservas y capacidad numérica; esta entrega no lo reemplaza. La ventana exclusiva CC se devuelve con procesos0, antes del siguiente trabajo o E2E de Coordinación.
