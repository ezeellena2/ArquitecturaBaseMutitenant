# E8 — cooldown real del recorrido combinado E7 + E8

Fecha: 2026-10-10. Corte inicial: `2a7116e7d0cbe8615b1a317cb23e8f3e4866f826`. Correctivo focal del puente `38febf92702426ef030141b51d14cd7785d47cea`, entregado a Root para publicar su propio scope. Sin stage, commit ni push desde el implementador.

## Problema y resultado

El host combinado `RecorridoApiHost` fijaba `Authentication:LoginCode:ResendCooldownSeconds=0`. El guion E7 pide un código Email de Empresa en el paso 22, registra un OTP incorrecto y espera 400 `Auth.LoginCode.Invalid` con `attemptsLeft: 4`; el paso 23 solicita otro inmediatamente y exige 429 `Auth.LoginCode.ResendTooSoon`. Con el perfil 0 el segundo pedido devolvía 202 y reemplazaba el código.

El host combinado ahora fija **60 segundos reales**. La fixture WhatsApp independiente y su fixture de comandos privados conservan 0. El default productivo, el contrato HTTP y el perfil restante se mantienen: total diario 1000, fracción protegida 0.2, nuevos/IP 1000, ventana de pedidos 100 y rate limits 1000. No se reinician ledgers, destinos ni IP.

## Decisiones tomadas

- Preparar una cuenta Email verificada preexistente mediante `IUserRepository`, `LoginMethod.CreateEmail`/`Verify`/`MakePrimary` y `IUnitOfWork`, siguiendo `E7Preparation`. La prueba afirma ausencia de OTP previo para ese destino; no registra un código previo ni manipula `CreatedAtUtc`, xmin o SQL.
- Consumir la configuración real de `RecorridoApiHost`, sin forzar 60 desde la fixture. El assert de la opción efectiva queda después del reenvío, para que el RED acredite 202 contra 429.
- Usar el campo HTTP vigente `resendAfterSeconds`, declarado por `RequestLoginCodeResponse`. El handoff decía `resendCooldownSeconds`; ajustar el assert conserva el contrato congelado.
- Ejercer sólo la cadena focal con la API real Testing hospedada por WAF externo sobre la misma PostgreSQL real y el origen `https://localtest.me:5174/`. No se necesitó proxy ni otra sonda Aspire para esta conducta de API.

## Validación focal

Todos los logs originales están en `.local/reviews/e8b-contract/`; las repeticiones no se suman como casos únicos.

| Comprobación | Resultado | Log original |
|---|---:|---|
| Build integración para RED, serial | exit 0; 0 advertencias/errores | `e8-cooldown-red-build.log` |
| Primer intento RED bloqueado por named pipe del sandbox | 0 casos; exit -532462766; incidencia de infraestructura, excluida del RED | `e8-cooldown-red-sandbox.log` |
| RED real con Docker y perfil 0 | 1 ejecutado, 1 fallo, 0 correctos/omitidos; exit 2; esperado 429, actual 202 | `e8-cooldown-red-real.log` |
| Build integración tras perfil 60, serial | exit 0; 0 advertencias/errores | `e8-cooldown-green-it-build.log` |
| Primera regresión con perfil 60 | 5 ejecutados, 4 correctos, 1 fallo por nombre JSON del assert, 0 omitidos; exit 2 | `e8-cooldown-green-real.log` |
| Build integración final, serial | exit 0; 0 advertencias/errores | `e8-cooldown-final-it-build.log` |
| `RecorridoUnifiedHostTests` final | **5/5**, 0 fallos/omitidos; exit 0 | `e8-cooldown-final-real.log` |
| Build herramienta final, serial | exit 0; 0 advertencias/errores | `e8-cooldown-final-tool-build.log` |

Los builds usan `-m:1 -nr:false -p:UseSharedCompilation=false`. AppHost no cambió y no se recompiló para este correctivo.

El nuevo caso comprueba POST real 202 con `resendAfterSeconds: 60`, lectura controlada de Email pickup sin imprimir OTP, verificación incorrecta 400 `Auth.LoginCode.Invalid`/`attemptsLeft: 4` sin cookie y otro POST con nueva clave de idempotencia que responde 429 `Auth.LoginCode.ResendTooSoon`. `retryAfter` está entre 1 y 60 y coincide con `Retry-After`. La opción efectiva del host es 60, la conexión es la misma PG externa y el issuer conserva el origen 5174.

Tras el rechazo conserva una sola fila con ID, hash y fechas originales, un intento fallido, sin consumo ni invalidación. El conjunto de IDs del outbox Email queda igual y la bomba Email no produce otro `.eml`. Los cuatro casos existentes pasan: desafíos Google consumer/business, signup/Email/legacy-cookie/bot firmado y aviso explícito repetible con versión original y aprobación HTTP.

## Integridad y alcance

La guía distingue el perfil combinado 60 del standalone 0. El informe anterior sólo recibe una nota histórica: los logs y resultados del puente `38febf92` siguen describiendo el perfil 0 y su sonda no acredita E7 22→23.

Contrato SHA256: `0ae765f28aab2dd6498b57e111b02ef8e2ba2fe5291065d7b74618eef28bdbf3`. Role.cs ajeno SHA256: `3260a9378a8bb68317e1d92ce81e290435d29b7d030624d72dfed470a114039a`. Ambos intactos. No se modificaron Front, defaults productivos, cuotas, B6/B7 ni planes de Coordinación.

Inventario exacto de cinco fuentes, SHA256, comandos, exits, conteos e incidencias: `.local/reviews/e8b-contract/e8-unified-walk-cooldown-files.json`. Fuentes inmóviles para Root y comandos propios terminados. No se levantó Aspire. Esta prueba focal no cierra E8: el recorrido completo, puerta, galería y Meta conservan su validación separada.
