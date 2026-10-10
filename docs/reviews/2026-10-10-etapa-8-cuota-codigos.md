# E8 M1: cuota de códigos WhatsApp

## Alcance y autorización

Autorización humana: proteger 20 de los 100 envíos diarios para ingresos Phone ya verificados y utilizables. El modo rápido permite corrección focal RED → GREEN, build y publicación propia inmediata; las revisiones independientes y la puerta completa se hacen una vez al final. Este corte no ejecuta la puerta ni acredita Meta/E2E.

El total por número de plataforma y día UTC sigue siendo 100. Los nuevos tienen máximo global 80 y máximo 20 por IP/día. Los ingresos protegidos pueden usar los 100 si no hubo nuevos. Cambiar propósito o IP no crea capacidad global adicional. Se conservan los límites de pedidos por IP/destino y cooldown existentes.

## Decisiones tomadas

- El núcleo prepara un código separado del tracking; cada caller decide el envío y llama a la admisión neutral con propósito y dueño reales. La admisión exitosa antecede a invalidación/Add y enqueue en la única UoW del caller. Los ingresos legítimos sin entrega conservan su historial con SentAtUtc null y sin cuota; un rechazo de cuota no agrega ni invalida códigos, ni deja outbox o ledger vacío bajo OnAnyResult. Rollback gasta 0 y replay gasta 1; el fallo del transporte posterior al enqueue no devuelve capacidad.
- Sólo Login a un Phone realmente verificado y utilizable del dueño, releído bajo identidad → cuenta → destino, usa capacidad protegida. BusinessSignup con dueño verificado, VerifyDestination, recuperación y métodos pendientes son nuevos. La disponibilidad usa la política existente del núcleo; no hay dependencia del núcleo hacia módulos, ni país nuevo codificado a mano.
- La protección configurable se redondea hacia arriba (`ceil(total * fraction)`) y se resta del total. Fracción 0–1 y topes positivos con validación de opciones; total 1 con fracción 0,2 admite 0 nuevos y 1 ingreso protegido.
- El lock global número/día serializa total, nuevos e IP, después de los locks de identidad/cuenta/destino. El reloj se relee al adquirirlo; cruzar medianoche cambia el día del lock y de RetryAfter. El repositorio recarga entidades tracked sin cambios tras esperar el lock.
- El ledger IP guarda HMAC pseudónimo con separación de dominio/número/día, usando la clave OTP de 32 bytes mínimo ya configurada y compartida entre réplicas. RemoteIpAddress proviene de IRequestInfo después de ForwardedHeaders confiables; IPv4 y mapped IPv6 comparten bucket. Sin IP hay un bucket unknown estable. No guarda Phone/UserId/IP en claro ni los registra. Cambiar la clave durante un día activo no es una operación admitida para ampliar buckets.
- La migración nueva conserva ReservedCount y lo copia como NewReservedCount y LegacyReservedCount: el gasto antiguo carece de propósito/IP reconstruibles. Legacy también descuenta capacidad de cada IP en ese día; una fila antigua con 98 no regala 80 nuevos ni 20 por una IP desconocida. Down/up conserva total y reaplica esa política conservadora. No se modifican migraciones históricas.
- La retención instalada borra filas IP de días terminados, incluso con canal apagado, y conserva los contadores globales total/nuevos/legacy. Baja de cuenta, purga OTP o eliminación de IP vencida no reinician cuota. El log técnico de retención cuenta elementos vencidos, porque ya no cuenta sólo textos de 90 días.
- El perfil UI privado Testing fija total 1000, protección 0,2 y nuevos/IP 1000. Estimación conservadora de 38 grupos × 20 OTP = 760 nuevos frente a 800 disponibles. No cambia defaults productivos, no resetea ledger por escenario y no simula IP para eludir cuotas. Ese recorrido no prueba los defaults de cuota; las pruebas M1 sí usan 100/80/20.
- El test histórico AccountRecoveryNoticeMigrationTests fija su propia migración por nombre y restaura Latest antes del control de modelo, en vez de asumir que siempre es la última. Conserva assertions de recibos, grants, versiones y cascade.

## Evidencia focal

Los logs originales están en `.local/reviews/e8b-contract/e8-quota-*`. Las ejecuciones fallidas o sin casos no se presentan como aprobadas; las corridas que se solapan no se suman.

| Verificación | Resultado |
|---|---|
| RED preparación sin enqueue (`e8-quota-red-prepare-actual.log`) | 6 casos, 1 fallo esperado: el código todavía mutaba/reservaba durante preparación |
| Unit final: LoginCodeFlow, cuota, intentos y validación | 54/54, 0 fallos/omitidos/no ejecutados |
| Architecture focal: aislamiento modular, UoW, dependencias, servicios, capas y modelos | 40/40, 0 fallos/omitidos/no ejecutados |
| Builds API/IT/unit/architecture | Entrega IT (incluye API): 19,49 s; unit: 31,83 s; architecture: 8,99 s. Todos 0 warnings/errors; comandos/logs en inventario |
| EF has-pending-model-changes | Sin cambios pendientes |
| SQL migración nueva | Backfill conservador, FK/índice IP y GRANT runtime revisados |
| Integración PostgreSQL/Docker ampliada (`e8-quota-it-green-candidate.log`) | 110 casos, 109 aprobados y 1 fallo de fixture HTTPS; 0 omitidos/no ejecutados, 270,208 s. No se presenta esta corrida como GREEN |
| Reparación focal + regresiones Email/alta/métodos (`e8-quota-it-repair-and-regression.log`) | 21/21, 0 fallos/omitidos/no ejecutados, 52,104 s. Incluye el único fallo anterior reparado, mixed alternado y Signup/BusinessSignup/AccountMethodsHttp. No se suman los solapamientos |
| Detector documentación global | auto 23 / modelo 11 / humano 0; ninguno referido al ledger/admisión/cuota. Pendiente de realineación general en puerta final |

Fallos de preparación registrados y corregidos: Docker pipe negado dentro del sandbox (reintento con escalación aprobada), payload técnico fuera del formato WhatsApp, 120 conexiones simultáneas sobre PostgreSQL con límite 100, requests de replay/422 agotando la ventana HTTP de la prueba diaria, fixture sin número de plataforma, y cliente de sesión con redirección automática/HTTP. Se conservan todos los logs y no se cambian las expectativas válidas del producto para acomodar fixtures.

La corrida ampliada cubre HTTP nuevos 80 + ingresos 20, ingresos 100 sin nuevos, IP 20/21 y mapped IP, propósitos/rotación global, replay y body cambiado 422, operador legítimo, país retirado, cap 1, estados sin entrega y Suspended/PendingDeletion con entrega, rollback/purga/retención, fallo real del dispatcher después de enqueue, upgrade/down/up/grants/modelo, locks de identidad, recuperación, retención y regresiones del núcleo por correo. La reparación mixed usa dos conexiones y propósitos alternados desde el inicio, con destinos nuevos distintos; verifica total 100, nuevos entre 60 y 80 y cada IP hasta 20. El RateLimitingPermitLimit=100 es sólo su perfil técnico de pruebas diarias; RealLoginRateLimitTests conserva el límite productivo de 20/15 minutos.

Self-review: todos los callers de emisión están inventariados y admiten dentro de la UoW existente; rechazo y early-noqueue se distinguen sin borrar historial válido; el núcleo no importa módulos; no hay secretos ni datos crudos en ledger/logs; sólo migración nueva y snapshot generado; Role ajeno conserva su hash. No quedan procesos de build/test levantados por este corte.

## Cambios al contrato

Ninguno. El rechazo usa el 429 `WhatsApp.LoginCode.DailyLimit` canónico con RetryAfter UTC. OpenAPI conserva SHA-256 `0ae765f28aab2dd6498b57e111b02ef8e2ba2fe5291065d7b74618eef28bdbf3`.

## Límites de esta entrega

Pruebas HTTP/servicios usan PostgreSQL real y outbox cifrado real, con transporte sintético; no prueban envío Meta ni posesión cuando el caso prepara entidades técnicas. La puerta completa, suites totales, dos revisiones independientes y recorrido real siguen a cargo de Integración sobre un corte fijo. No se implementa runtime AI/E9/E11 ni se modifica Front, Role ajeno o planes de coordinación.
