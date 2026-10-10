# E8 M3 · HTTP documentado y export final

M3 corrige documentación del HTTP implementado después de B6/B7/B8 y M2; no cambia la ejecución de identidad, recuperación ni módulos. Base de trabajo `9ee0030299c40dacdb6aced25853cf08b4679f3e`; Root audita y publica los archivos propios, sin push. La autorización y las notas están en `.local/reviews/e8b-contract/e8-http-documentation-notes.md` y los dos resguardos humanos de correctivos. El plan E8 y el informe de coordinación tienen otro owner.

## Resultado y contrato

`docs/contracts/openapi.json` fue generado por la API durante el build, sin edición manual del JSON. Su SHA256 final es `46503255f38de831994887154187275f037a3682974976fc2aa920a95313f3d6`, idéntico al archivo generado en `Api/obj/openapi`. El original `0ae765f28aab2dd6498b57e111b02ef8e2ba2fe5291065d7b74618eef28bdbf3` y el export de mensajes B7 `469bd1ec819166a88a19fa6218d93b501cea605deaf0c6cadfca9cd5e872f6ad` quedan como evidencia, no se restauran al terminar.

La comparación completa tiene **633 diferencias JSON pointer en 26 operaciones**, exclusivamente catálogo, estados de error, descripciones, ejemplos y headers. Mantiene las **160 rutas y 274 esquemas**, `components` completos, verbos, solicitudes, parámetros, seguridad y demás metadata de las operaciones, incluidos permisos. No hay diferencias inesperadas ni ejemplos cuyo código/status quede fuera del catálogo. Los pointers completos y el resumen por operación están en `.local/reviews/e8b-contract/m3/contract-pointers.json` y `contract-comparison.json`.

| Operación | Corrección documental |
|---|---|
| `POST /api/auth/login-code/verify` | `Identity.Account.Suspended`403, `LockedOut`429, `PendingDeletion`403 y `Tenancy.Access.PlatformOnly`403. La cancelación sólo sigue a prueba válida dentro de la gracia; fuera de ella `Legal.AccountDeletion.GraceExpired`409 sin ticket. |
| `POST /api/auth/{signup,business-signup}/verify` | Estados de cuenta reales; baja pendiente sin ticket. Conserva diferencias Consumer/Business y PhoneTaken400 bajo phone. |
| `POST /api/me/login-methods/{methodId}/verify` | Suspended/PendingDeletion403 sin ticket, AlreadyVerified409 y NotFound404; no atribuye LockedOut ni PlatformOnly al verificador de métodos. |
| `POST /api/account-recovery/verify` | Retira los estados de cuenta del helper compartido: la prueba usa destino y nonce, sin conocer la cuenta anterior. Desaparece la respuesta403 inventada por esos estados. |
| `POST /api/auth/login-link/{preview,redeem}` | Invalid400 reúne inexistente/usado/vencido/invalidado/método retirado/operador; retira Expired/AlreadyUsed/TooManyRequests del bot. Ambos documentan Suspended403 y LockedOut429. Preview permite baja pendiente vigente sin ticket; redeem agrega PendingDeletion403 y GraceExpired409. |
| `POST /api/platform/recoveries/{id}/approve` | MethodAlreadyOnAccount409 para destino propio y MethodInUse409 para ajeno, sin errores de campo; retira PhoneMethodInUse400 falso. Conserva mensajes genéricos B7 es/en de método de ingreso. |
| `POST /api/platform/recoveries/{id}/reject` | Conserva NotFound, AlreadyResolved y concurrencia; retira errores exclusivos de aprobación: NoAccount, MethodInUse, PhoneMethodInUse y OperatorAccount. |

Las **15 acciones idempotentes** del contribuidor Identity pasan `Request.IdempotencyKeyReused` de409 a422 en catálogo, respuesta y ejemplos: approve/reject de recuperación; code/verify/submit de recuperación pública; code/verify/alta de BusinessSignup; request/verify de LoginCode; redeem de LoginLink; signup/verify; agregar/reenvío/verificar método propio. Required400 e InProgress409 permanecen. El filtro y sus semánticas no se modifican.

El controlador de enlaces explicita ProducesProblem403/429 para preview y429 para redeem, conservando firmas, cuerpos y acceso. Las descripciones distinguen preview con doble lectura coherente sin UoW/lock, de redeem con UoW y lock; preservan prueba, cookie y replay estricto.

El ejemplo LockedOut429 deja de fabricar `retryAfter=60`; el HTTP no emite metadata ni Retry-After en ese rechazo. Los límites de envío/cooldown conservan sus ejemplos actuales. Los **26 descriptores de Retry-After** del helper común pasan a declarar que el encabezado es opcional y depende de metadata entera. Su alcance incluye los tres POST de mensajes del Asistente (`organization`, `me`, `platform`) y el POST del webhook WhatsApp: en esos cuatro casos cambia sólo esa descripción. No se afirma igualdad byte a byte del Asistente completo; sus DTO, requests, schemas, seguridad, permisos, 501 pendientes y conducta permanecen. No incluye CC05/CC06, PriceDTO ni implementación de State/messages/Usage pendiente.

Los errores OTP Invalid/Expired/AlreadyUsed/TooManyAttempts conservan400 y pierden `errors.code` inventado: sus fábricas no son ValidationError. Invalid sin prueba activa también pierde el `attemptsLeft=4` artificial; la descripción aclara que los intentos se informan sólo para prueba activa. Los errores reales de destino y sus400 por campo permanecen, aunque su fábrica base sea Conflict o NotFound.

## Decisiones tomadas

- Separar prueba de OTP y estados de cuenta propios de cada operación, en lugar de agregar todos los estados al helper compartido. No modificar Domain, servicios, autenticación, persistencia ni idempotencia para adaptar el runtime a la documentación.
- Conservar la composición de contribuidores del helper. Auditar todos los pointers y todos los ejemplos de problemas del documento final para detectar residuos de los merges, además de comparar todos los paths productivos con el endpoint real.
- Documentar las condiciones de cancelación sin crear un plazo nuevo ni prometer tickets de cancelación en signup/verificación de métodos.
- A pedido adicional de Root, precisar únicamente la receta `docs/guides/quitar-asistente.md`: si quitar los tres permisos Assistant deja vacía una asignación de Plataforma, el preflight aborta sin cambios hasta que un administrador autorizado elija explícitamente un permiso núcleo por el flujo existente. El Drop de la copia elimina sólo esos tres códigos y conserva IDs/usuarios/estados/otros grants/LastManager/CannotModifySelf. No elimina filas ni otorga permisos automáticamente: el seed recrearía LegacyBootstrap para un operador sin fila. No se implementó ni ejecutó ese retiro físico en M3.

## Verificación dirigida

Los comandos, salidas originales y tiempos medidos están en `.local/reviews/e8b-contract/m3`; el inventario conserva los resultados sin presentar fallos de herramientas como RED de conducta.

| Ejecución | Resultado original | Tiempo medido |
|---|---|---|
| Build IT/API para RED, sandbox | Exit1, falla silenciosa con0 diagnósticos; no cuenta como RED | Ver `build-red.json` |
| Mismo build con IPC autorizado | Exit0,0 advertencias/errores | Ver `build-red-escalated.json` |
| IdentityHttpDocumentationTests RED | **3 casos,3 fallos**, exit2: estado Suspended ausente y HTTP422 frente a catálogo409 | 38.186s |
| Build IT/API para GREEN | Exit0,0 advertencias/errores | Ver `build-green.json` |
| Cohorte focal Docker PostgreSQL | **86/86**,0 fallos/omitidos, exit0 | 108.437s |
| Build final tras precisión de preview | Exit0,0 advertencias/errores | 7.303s |
| Primera selección del método final | 0 casos, exit8; filtro sin wildcard, fallo de selección | Ver `contract-final.json` |
| Selección final corregida del método | **1/1**,0 fallos/omitidos, exit0 | 28.944s |
| Comparación estructural y de pointers | Exit0, unexpected0/stale0; SHA generado/versionado idéntico | Ver `contract-comparison.json` |

La cohorte86 contiene **13 casos nuevos significativos y73 regresiones seleccionadas**:5 de IdentityHttpDocumentationTests y8 de LoginLinkTests.HttpDocumentation. Los nuevos comparan HTTP es/en con ejemplos/status/code/detail de422, Invalid sin prueba activa y suspensión/bloqueo de preview/redeem sin cookie ni delay. La prueba de catálogo verifica estados por operación y consistencia bilingüe. Las regresiones son Etapa8ContractTests, OpenApiTests, OpenApiContractTests, LoginLinkTests, LoginCodeTests, IdempotencyTests, IdempotencyModeContractTests y PlatformRecoveryDestinationConflictTests, incluido destino propio/ajeno y es/en. El último1/1 repite sólo igualdad del contrato después del cambio textual final; no se repitió la cohorte86.

## Entrega y límites

Root recibe `.local/reviews/e8b-contract/e8-http-documentation-files.json` para auditar y commitear únicamente las fuentes inventariadas. Luego pasa SHA/delta a Coordinación para registrar Cambios al contrato y a Front para regenerar sus tipos por su chat. No se editaron Front, Role.cs ajeno, planes ni informe de coordinación. Role.cs conserva SHA256 `3260a9378a8bb68317e1d92ce81e290435d29b7d030624d72dfed470a114039a`.

Los comandos propios terminaron. M3 no ejecuta ni acredita solución completa, puerta final, detector documental global, E2E real, galería, recorrido ni retirada física: corresponden a la única puerta posterior sobre el par fijo. No cambia contratos aprobados CC01–04 ni declara terminado el runtime del Asistente.
