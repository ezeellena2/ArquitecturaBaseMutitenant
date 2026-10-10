# E8b · T06a: fundamentos financieros

## Alcance

Cuotas diarias, reservas duraderas, consumo por llamada, espejo técnico global y reader interno real. No cierra T06 ni E8b. State, Uso, Send, Stop, SSE, workers/reaper, captura SDK, ejecución de títulos, correos/outbox y retención completa siguen pendientes; sus respuestas 501 se conservan.

Base de implementación: `96c555a5f0be15cf090fc37faf791bb8443345a3`, contrato CC04 publicado. Los avances documentales legitimados por Root hasta `01edb1fdf4a6bca4220577ac3b55f92117d738b2` son ajenos y se preservan: excepción financiera `cc83cd2b`, v116/guiones y OTP documental posteriores, sin ampliación de este corte. No hubo stage, commit, push, edición Front ni builds/tests de Root concurrentes. `Role.cs` ajeno queda excluido; hashes originales en el manifiesto. El inventario propietario y los snapshots de entrega se guardan en `.local/reviews/e8b-contract/t06a-*`.

## Decisiones tomadas

- `AssistantPlatformKeyDailyQuota` es global por persona/día UTC: comparte el límite configurado (50 por defecto) entre Consumer, todas sus organizaciones y Platform. `AssistantOwnKeyDailyQuota` es privada por organización/persona/día. Ambas separan reservado de consumido. La renovación es exactamente 00:00Z; remaining es null por encima de 10.
- Helpers internos reciben un binding compuesto por el servidor y trabajan dentro de la única UOW del caller. El repositorio exige ese scope original y locks PostgreSQL ordenados de persona, operación y organización. No conceden permisos de ejecución ni abren una UOW propia. El caller futuro compone Account/Legal/admisión antes de invocar el proveedor.
- Una identidad financiera opaca por operación, diferente de una conversación, conserva Reserved→Invoked→Completed o Reserved→Released. Replay liberado no admite otro efecto; BeginCall devuelve IsNew=false para una secuencia ya persistida. Complete es idempotente. La marca de invocación es técnica: T05c debe ubicarla en el límite real de intento al proveedor, después de validaciones previas; aquí no hay captura SDK ni prueba de proveedor real.
- El ledger global contiene solamente binding técnico, financiación, límites, tarifas/revisión históricas y hechos por llamada; no contiene texto, archivos ni ConversationId. La copia privada `AssistantUsageFact` se confirma junto con el espejo en la misma completion UOW. Platform agrega globalmente sin recorrer scopes de organizaciones. Después de una baja, se conserva contabilidad técnica pseudónima y no se recrea la copia privada eliminada; la prueba no sustituye la futura prueba de revocación del worker.
- Se reutilizan `AssistantProviderReference` y sus tarifas absolutas de cache read/write TTL5m/1h y tier largo. Input/Output personalizados son las tarifas base de entrada sin cache y salida; cache y tier largo conservan valores absolutos de la revisión técnica. No hay ratios inventados, catálogo paralelo, metadata HTTP nueva ni habilitación de cache en el request.
- Tarifas Money USD: scale6/numeric(18,6). Costos y reservas: AwayFromZero12/numeric(24,12), incrementalmente también en las dos columnas CostAmount existentes. Cien llamadas de USD0,00015 suman USD0,015 sin redondear a centavos. Los minorUnits de moneda siguen siendo de catálogo/presentación.
- Una llamada está acotada a 51.200 tokens inclusivos de entrada y 2.048 de salida, o 64 para título. La reserva cubre el peor importe aplicable de cache/tier, sin sumar dos veces esos tokens. Son cinco llamadas totales: cuatro normales más título opcional, o cinco normales. No se selecciona ni ejecuta título/proveedor aquí: la composición posterior aporta el snapshot del modelo más barato elegible con esa clave. El SDK reporta input inclusivo y reasoning como parte de output; los updates acumulativos reemplazan, no se suman. Conteos/categorías/TTL desconocidos conservan costo estimado, no costo cero.
- No se persiste un acumulador monetario ilimitado para Consumer/Platform. Hechos por llamada y reservas acotadas caben en numeric(24,12) incluso con una tarifa válida menor que 10¹²: la reserva máxima demostrada es USD266.239.999.999,999999733760. El presupuesto Business se verifica bajo lock contra costos completados más reservas vigentes, por financiación; el progreso lee sólo consumo registrado.
- Business se determina por el access/binding original, no por nullable Money: RuntimeConfiguration puede devolver platformMonthlyBudget para todos los actores PK. Consumer y operadores no reciben un presupuesto de organización. Las operaciones conservan el presupuesto usado al admitirse; el reader usa snapshots históricos del ledger y no Settings actuales.
- La excepción de zona fue aprobada explícitamente por el humano: «Sí, períodos consecutivos (recomendado)»; evidencia `financial-month-zone-human-authorization.json` y plan del coordinador `cc83cd2b`. El período abierto es inmutable; el siguiente inicia exactamente en su cierre y termina en el cierre civil del siguiente mes lógico en la zona vigente. Se materializan períodos vacíos consecutivos al saltar meses sin consumo. Esto es una excepción explícita al mes civil puro en la transición, sin huecos, superposición de admisión ni segundo presupuesto; no cambia la zona general ni el día diario UTC.
- El reader filtra por CreatedAtUtc de la admisión y devuelve Month/TimeZone/UTCbounds persistidos. Completar tarde no cambia el mes del costo. Un caller de State/Uso futuro debe recuperar esos límites antes de consultar, sin recomputar el inicio civil según Settings actuales: la hora de transición BA→NY pertenece al período guardado.

## Migración y aislamiento

`20261010124555_AssistantFinance` agrega cuatro tablas técnicas globales y dos privadas. Sólo las privadas usan ITenantOwned, filtros y RLS tenant_scope forzado con trigger de inmutabilidad del tenant. Hay grants explícitos SELECT/INSERT/UPDATE/DELETE para mt_app en las seis. No se reescribe historial de migraciones; el snapshot refleja el modelo nuevo. SQL Up/Down original: evidence33/34. La prueba aislada Up→Down→Up preserva el costo previo de seis decimales, comprueba escala, clasificación y grants. Down elimina las seis tablas y devuelve CostAmount a scale6: no es un rollback sin pérdida de los nuevos hechos/fracciones12.

## Cambios al contrato

Necesidad concreta probada, sin delta HTTP en este corte: ocho personas ×50 preguntas ×30 días ×4 llamadas ×51.200 input =2.457.600.000 tokens de entrada; salida98.304.000, costo USD294,912 con presupuesto1000. El reader real conserva long y devuelve ese total; checked cast a int falla. Los cuatro contadores de tokens del DTO HTTP actual int32 requieren una decisión/coordinación de contrato antes de publicar Uso. Alternativas: int64 (con tipos/validación de precisión de JavaScript coordinados), cadena decimal exacta o un límite de producto explícito; truncar, saturar o dividir filas artificialmente no son soluciones. Root prepara el delta posterior; T06a mantiene CC04 intacto, SHA UTF8LF `c8ef280ef0f360adfb9af2d8b38657039de1d301137f0c86dd178c94d2b367d4`.

## Verificación inicial (anterior a la corrección SPEC)

Los logs originales, comandos, exits y SHA se congelan en el manifiesto; no se suman cohortes repetidas como tests distintos. Las ejecuciones fueron focales y secuenciales entre comandos. ApiFactory usa ValidateOnBuild y PostgreSQL Docker real con rol runtime mt_app.

| Evidencia | Resultado y alcance |
|---|---|
| 01–04 | Fallos/diagnóstico de entorno de dotnet test/build default; no se cuentan como RED de lógica. El serial posterior funciona;04 acabó build, no ejecutó tests. |
| 06 | RED real1/1: CostAmount anterior redondeaba USD0,000000123456 a cero. |
| 10 | RED real1 de2: cache inclusive/TTL devolvía USD0,00015 en vez de0,0000945. |
| 07–09,14,18 | Correcciones de compilación/analyzers y builds intermedios; no RED de conducta. |
| 16–17 | Build0warnings; costos unitarios7/7. |
| 20–21 | Build API/Infra/IT0warnings; Docker7/7: precisión,50/51 y40/10, doshosts quota/budget, OwnKey independiente, UTC rollover, rollback/RLS, DST y baja/reconcile técnico. |
| 22–25 | Capacidad:23 falló preparando seed por timeout;25 cargó facts y pasó reader global, pero timeout del reader privado con estadísticas recién creadas. No son GREEN completos. |
| 26–28 | Build0warnings; Docker3/3: capacidad de ambos readers, cinco llamadas/cumulative12 y UpDownUp real. Sólo seed48k usa180s scoped y ANALYZE de fixture; readers conservan30s. |
| 27 | Fixture de zona usaba alias no presente en catálogo: FK falló antes del repro; no RED de producto. |
| 29–30 | Build0warnings; clone Money owned elimina advertencias de instancia compartida; título/cumulative pasa. RED real zona:03:30Z estaba fuera del book BA terminado03:00Z. |
| 31–32 | Build0warnings;52/52 guards afectados de UOW/capas/módulos/clasificación/precisión/readers. |
| 33–35 | SQL Up/Down exit0; EF no pending model changes. |
| 36–37 | Build0warnings; costo+DI12/12. |
| 38–39 | Build0warnings; Docker3/3 períodos consecutivos: BA→NY e inverso, borde exacto, cambios sucesivos, DST, meses vacíos, late completion y ambos readers. |
| 40–42 | Build API/Infra/IT0warnings;41 seleccionó0 por intersección class+method (no GREEN).42 filtro válido: Docker24/24/169,709s, con14 finanzas,6 persistencia Assistant,2 inventario RLS y2 DI/ValidateOnBuild. Contenedores eliminados. |
| 43–45 | Modelo final1/1 sin drift; build guards0warnings;52/52 guards finales. |

La cohorte financiera final incluye snapshots v1/v2 persistidos sin recalcular pasado. Los comandos completos de las cohortes finales quedan en el manifiesto; los primeros intentos de entorno no imprimieron el comando en stdout y se distinguen como procedencia parcial. No hay suma de17/21/28/39/42 ni suites completas.

## Integraciones pendientes

- T05c: admisión compuesta única UOW, actor/access/tenant real revalidado, binding durable original, marca real de invocación y updates SDK inclusivos por call/replay. Revalidar antes de efectos tras revocación; completar accounting conocido/conservador con el binding original aunque ya no pueda leer chat. Ninguna prueba aquí autoriza leer chats arbitrarios mediante System.
- T06b/State/Uso: consumo del reader interno y cuota, períodos persistidos para rangos, financiación/snapshots coherentes, progreso sin reservas y delta de capacidad coordinado. Los helpers no son una implementación de endpoints501.
- Título: selección del activo más barato elegible con la misma clave, llamada sin herramientas hasta64 tokens dentro de las cinco, compare-and-set contra renombrado y fallback determinista. Sólo hay presupuesto/ledger compatible aquí.
- Alertas: futura unicidad Org+mes+umbral, sin funding en la clave; outbox neutral cifrado en la misma completion UOW y sólo por costo registrado. No se implementa correo/outbox ni umbrales HTTP aquí.
- Proveedor/archivos/retención: manual real pendiente del usuario sin solicitar otra clave; optional app boot, Fake sólo Testing/E2EIsolated y ningún fallback OwnKey→PlatformKey. No se declara prueba Anthropic real, worker ni retención completa.

## Corrección SPEC r1: referencias de moneda

La revisión SPEC inicial pidió un P2: cada columna Currency de una tabla nueva necesita FK al catálogo; las FK de BillingCurrency no protegen los Money independientes. Se agregaron 34 relaciones `ON DELETE RESTRICT` a `platform.Currencies`, para 37 columnas Currency totales: 1 en AssistantUsageFacts, 12 en AssistantFinancialCalls y 24 en AssistantFinancialOperations. Incluyen costo, reserva, presupuesto opcional, tarifas de entrada/salida/cache/tier largo y título opcional. No se amplían las dos tablas Daily históricas ni el contrato HTTP.

Money.Currency conserva el value object CurrencyCode y su conversión. Una propiedad string shadow en el propietario EF comparte la misma columna del owned y establece la FK compatible con Currency.Code string. EF propaga el valor almacenado entre ambas propiedades; las pruebas reales verifican persistencia, materialización y opcionales presentes/ausentes. No se cambia Money, CurrencyCode ni el catálogo del núcleo, ni se reemplaza la FK por una restricción USD.

La migración aún no publicada conserva `20261010124555_AssistantFinance`; Designer y ModelSnapshot se regeneraron y el scaffold temporal se archivó como evidencia, sin dejar otro ID en el proyecto. Las columnas nuevas adoptan `character(3)` del catálogo. SQL Up/Down r1 revisado: sólo seis tablas nuevas, 37 referencias Currency, dos precisiones CostAmount existentes, clasificación RLS y grants originales. La prueba Up→Down→Up vuelve a pasar. Los avances documentales ajenos hasta HEAD `92a47b868e1f8031555736b7e26f4f10b2cd85fe` se preservan y se capturan como referencias; la base de implementación sigue siendo 96c555a5.

La evidencia r1 está en `.local/reviews/e8b-contract/t06a-r1-evidence/`, con comandos y exits exactos en el manifiesto nuevo. La cohorte inicial 42 de 24 casos es histórica y no acredita esta corrección. No se suman repeticiones ni cohortes solapadas.

| Evidencia r1 | Resultado y alcance |
|---|---|
| 01–02 | Build0warnings; primer intento5/5 fallan: cuatro reproducciones de FK faltante y un error de fixture al llamar Platform reader dentro de scope tenant. Este último se corrigió en el test. |
| 03–04 | Build0warnings; RED dirigido5 ejecutados/4 fallos: metadata y las tres tablas aceptaban alpha3 ZZZ fuera del catálogo. Lectura histórica con USD deshabilitada ya pasa. |
| 05–07 | Builds API/Infra/IT0warnings. Scaffold temporal, integrado en la migración pendiente conservando ID; advertencia estándar de revisión por cambio de tipo de columna, no fallo de ejecución. |
| 08 | GREEN5/5 PostgreSQL real: metadata FK Restrict, UPDATE de cada una de37 columnas rechaza ZZZ con23503, incluyendo Money opcionales poblados, y USD deshabilitada conserva ambos readers e historia. |
| 09 | Cohorte final corregida Docker29/29/173,963s:19 casos financieros/FK/precisión/migración,6 persistencia Assistant,2 RLS y2 DI. Incluye48.000 hechos y capacidad exacta de ambos readers, cuotas/concurrencia, períodos y rollback. Contenedor eliminado. |
| 10–13 | Modelo1/1 sin drift; build Infrastructure y unit0warnings; costo+DI12/12 sobre esta versión. |
| 14–15 | Build guards0warnings;52/52 guardas focales de UOW/capas/módulos/identidad/clasificación/precisión. |
| 16–18 | SQL Up/Down exit0,37FK Currency revisadas, RLS privado/grants de seis tablas intactos; EF no pending model changes, exit0. |

El primer freeze `t06a-{files,manifest,delivery}.json`, sus snapshots, informe y logs originales permanecen inmutables. La revisión fresca debe usar `t06a-r1-{files,manifest,delivery}.json` y `t06a-r1-freeze/`. Son43 fuentes del mismo alcance propietario; no hubo stage/commit/push, build/test simultáneo de Root, suites completas ni ejecución real del proveedor. Persisten únicamente las integraciones futuras y la preocupación de capacidad HTTP ya documentadas.

## Corrección QUALITY r2: presupuesto del mismo intervalo

QUALITY r1 encontró un P2 distinto del monetario ya resuelto: Business filtraba costos y consultas por el rango solicitado, pero elegía el presupuesto entre todas las operaciones completadas del mes. Platform ya lo elegía dentro del rango. La corrección mínima reutiliza `scopedOperations` al seleccionar MonthlyBudget Business, manteniendo el mes, financiación y orden CreatedAtUtc/Id. No cambia esquema, contrato, permisos ni reglas del producto.

El repro PostgreSQL usa la misma organización/financiación/mes: A se admite el10oct12Z con presupuesto1000 y costo conocido USD0,00015; B el11oct12Z con presupuesto2000 y el mismo costo. Para [10oct00Z,11oct00Z), antes del fix ambos readers devolvían una consulta/costoA, pero Platform presupuesto1000 y Business2000. El RED lo demuestra con dos casos: PlatformKey y OwnKey, sin error de fixture. Completar ocurre después de cada admisión. Otra organización con presupuesto9000 y consumo en el mismo día verifica aislamiento.

Después del fix, el rango A conserva presupuesto1000/query1/costo USD0,00015 en ambos readers; el mes completo conserva presupuesto2000/query2/costo USD0,0003. También coinciden tokens y límites del período histórico.

| Evidencia r2 | Resultado y alcance |
|---|---|
| 01–02 | Build API/Infra/IT0warnings; RED2/2 por discrepancia concreta Expected1000/Actual2000, en ambas financiaciones. |
| 03–04 | Build API/Infra/IT0warnings; GREEN2/2 PostgreSQL real, rango A y mes completo coherentes, otra organización aislada. |
| 05 | Cohorte final corregida Docker31/31/181,616s:21 casos financieros/precisión/FK/migración/rangos,6 persistencia Assistant,2 RLS y2 DI. Incluye48.000 hechos/readerslong, períodos, snapshots, moneda deshabilitada, concurrencia y rollback. Contenedor eliminado. |
| 06–07 | Build guards0warnings;24/24 guardas focales de UOW/readers/filtros/clasificación/identidad/módulos. |

Los resultados r1 de costo+DI12/12, guardas52/52, SQL Up/Down y drift son históricos; no se vuelven a atribuir a una ejecución r2. `t06a-r2-unchanged-evidence.json` demuestra40 fuentes byteidénticas a r1, incluidos servicios de costo/DI, mapeos, migración, Designer y ModelSnapshot. No se regeneró SQL ni se repitió el comando EF de drift: el modelo permanece idéntico y la cohorte actual vuelve a verificar Up→Down→Up y ausencia de pending model changes dentro de esa prueba. Las nuevas guardas24 y DI/RLS de la cohorte actual sí se ejecutaron sobre la versión corregida. No se suman cohortes solapadas.

La entrega vigente para revisión es `t06a-r2-{files,manifest,delivery}.json` y `t06a-r2-freeze/`, con comandos/exits originales en `t06a-r2-evidence/`. Se preservan completos el primer freeze, r1, ambos informes de revisión y sus originales. R2 modifica exactamente reader/test/informe respecto de r1; las nueve modificaciones r1 se registran por separado. HEAD sigue92a47b86, base96c555a5,43 fuentes owner, Role ajeno intacto e index vacío. OpenAPI publicado y export normal conservan UTF8LF SHA `c8ef280ef0f360adfb9af2d8b38657039de1d301137f0c86dd178c94d2b367d4`.

No hubo stage/commit/push, builds/tests de Root concurrentes, edición Front ni suites completas. Se mantienen las integraciones pendientes ya delimitadas; este fix no implementa Uso/State ni otros endpoints501, runtime, proveedor real o un delta HTTP.
