# E8b — contrato congelado y pase al paralelo

T00/T01 están revisados y commiteados en ambos repositorios, en `main`, sin push. El avance E8b fue autorizado expresamente el 09/10 mientras Integración completa la puerta Meta de E8. Este pase habilita Parte 2; no cierra E8 ni E8b.

| Entrega | Commit |
|---|---|
| T00 Back | `fe36e7ce85441a2dc2af2e56d1588a11a5c2ccb1` |
| T00 Front | `429fcfe359a4f5f9984f7a12704ddea8a40ca73b` |
| T01 Back | `250c498dea0e9b8d5ec770ad78b9c8399e2c2bf3` |
| T01 Front | `61f15c1ccf4575255c76f9e6cd62ada972d4a7a5` |

## Contrato y compatibilidad

OpenAPI SHA-256 normalizado LF: `fb765706ea855dfb8b11752f9243dc2672b5e91db9af6c84fd8ce1bc58e0530b`. Tipos Front generados SHA-256 LF: `e9c098388a8f87991af93146977b3cad3643c563936a66227f24b3758cb2c2c0`. Son formatos distintos; `contracts:check` confirma que los tipos corresponden al JSON.

Se agregaron 40 paths, 56 operaciones (45 de chat y 11 de gestión) y 87 schemas. Las 120 rutas previas E8 permanecen idénticas por comparación estructural, sin eliminaciones. De sus 184 schemas sólo MeResponse y ProblemDetails reciben las adiciones neutrales previstas; los demás permanecen idénticos. Las 411 respuestas de error declaradas nuevas tienen ejemplos ES/EN.

El contrato publica los seis eventos SSE cerrados, payload de wire separado del sobre OpenAPI, nueve proyecciones Quick tipadas, adjuntos multipart, exportación binaria, versiones, idempotencia privada y errores. En T01 los servicios nuevos devolvían 501 deliberadamente. T02 implementó persistencia, gates y lecturas privadas; T03–T09 deben completar la conducta restante antes de la entrega final de Parte 2.

## Verificación y revisión

SPEC y QUALITY aprobaron T01 después de corregir cinco hallazgos de formas/guardas, ejemplos semánticos y la fuente única de moneda. Cero observaciones pendientes. La raíz comprobó hashes de 32 logs y de los 234 archivos entregados (232 Back y dos Front).

| Verificación | Resultado |
|---|---|
| Architecture final | 193/193 |
| Validadores Assistant finales | 14/14 |
| Integración Docker focal de rutas, accesos, gates, errores y seed | 27/27 |
| Contratos Front, dos archivos | 5/5 |
| Último ajuste de ejemplos semánticos | 3/3 dirigidos |
| Último ajuste de fuente única de moneda | 12/12 dirigidos; JSON idéntico |
| Builds afectados | 0 advertencias, 0 errores |
| Front generación, contracts:check, TS/Vite y lint de tipos propios | exit 0 |

Los cuatro primeros grupos suman 239 tests finales; los dirigidos posteriores son subconjuntos y no se suman como cobertura distinta. Los 381 verdes iniciales se conservan sólo como evidencia histórica. Domain 16/16 y spy de modos idempotentes 6/6 pasaron previamente y sus implementaciones no cambiaron después. El runner MTP se ejecutó por DLL por una limitación de IPC del sandbox, sin omitir tests. Un primer intento Front encontró un cache temporal inexistente antes de cargar una suite; el retry focal pasó 5/5, sin cambios de fuente.

Evidencia local: `.local/reviews/e8b-contract/t01-{manifest.json,files.json,spec-review.md,quality-review.md,delivery.md}`. La verificación completa Back con Docker y la integración de Parte 2 quedan para su cierre técnico; estas pruebas focales no las reemplazan.

## Decisiones tomadas

- AssistantMoney es un schema local cerrado para los 19 campos monetarios del módulo. Conserva Money/converter del núcleo y las formas E8. La moneda fija se obtiene de AssistantContractPolicy/BillingCurrency; T03 aún debe validarla contra el catálogo factual.
- Replay privado vive en puertos/modelos neutrales de Application, sin tipos HTTP. Los modos durables T01 sólo validan la key y evitan capturar cuerpos de stubs; binding, persistencia y reconstrucción autorizada se implementan en T05. BufferedJson conserva su conducta.
- El actor durable usa Models/Request e Interfaces/Integrations/Request. Su alcance/adaptador todavía se implementará en T05.
- Las categorías neutrales de rutas instaladas son TechnicalWebhook, PlatformChat y PlatformManagement, verificadas por atributos/permisos con controles negativos, sin exceptuar nombres de controllers. La categoría predeterminada conserva WhatsApp.
- La disponibilidad modular usa una policy por key: bool decide, null delega TenantFeatures, después de catálogo/scope/emergencia. T02 debe sustituir los gates provisionales por fuentes reales.
- T01 publica assistant.use para CompanyAdmin; T02 debe hacerlo efectivo desde asignaciones activas de empresa sin conceder configure ni otros permisos. Los operadores existentes conservan sus permisos individuales; el bootstrap legacy no los promueve a permisos nuevos.

## Cambios al contrato

Ninguna necesidad pendiente al congelar T01. Durante Parte 2 se registra aquí cualquier cambio necesario antes de coordinarlo por el usuario. Eliminar la conducta 501 no autoriza alterar unilateralmente el JSON congelado o los tipos Front. CC01/CC02/CC03 fueron autorizados expresamente y se publican juntos después del commit T02 verificado `fa74d44768187b40e6aba175aa1f1fe2a2babefb`; esta publicación fue revisada SPEC/QUALITY y commiteada en Back `ce1494d7f620e0e152a12fde2c8d2eeea5b791e6` y Front `3caae3b0841127a1f252b613247f78fb5173eb07`.

### CC01 — modelos y precios para configuración Business

**Estado: autorizada expresamente por el usuario el 10/10 UTC; publicada en fuentes, OpenAPI y tipos; SPEC/QUALITY aprobadas y commits acreditados.** El usuario respondió «Sí, publicar CC01 y regenerar tipos» a la propuesta concreta, registrado en `b0da6bbe`. Integración transmitió el hallazgo Front T12/D19; la raíz confirmó las firmas y permisos actuales.

`GET /api/organization/assistant/settings` exige assistant.configure y devuelve sólo modelId. `GET /api/organization/assistant` exige assistant.use, ofrece únicamente Id/Name y filtra el modo guardado. El catálogo Platform exige acceso Platform. Por eso Config-Org no puede mostrar precios ni opciones del modo que se está preparando antes de guardar, y una persona con configure sin use no tiene una lectura adecuada.

Propuesta concreta mínima: agregar a **OrganizationAssistantSettingsResponse**, conservando todos sus miembros actuales, `platformKeyModels: AssistantModelPriceResponse[]` y `ownKeyModels: AssistantModelPriceResponse[]`. Se reutiliza el DTO de catálogo ya publicado (Id, Name, Provider, ProviderModelId, InputPricePerMillion, OutputPricePerMillion, IsActive, Version); ambos precios conservan AssistantMoney/Money. Las listas son siempre arrays, vacíos cuando no hay elegibles, independientes del modo guardado:

- PlatformKey: sólo modelos activos, compatibles y permitidos por Plataforma.
- OwnKey: todos los modelos activos y compatibles si allowOwnKey; array vacío si la política lo impide.

La lectura conserva Business + módulo efectivo + assistant.configure; no exige assistant.use ni llama proveedor. No agrega ruta, permiso o secreto, ni cambia el modelo guardado al consultar opciones. Front puede filtrar el borrador de modo con estos datos reales sin inventar catálogo/precios o consultar Platform.

Publicar contrato/modelo/ejemplos y tipos B/F, con nuevos hashes y commits, después del cambio estable T02. Verificar configure-sin-use, 401/403, aislamiento/política/modelo inactivo o incompatible, precios y ausencia de claves; preservar las 120 rutas E8. Las guardas contractuales y de acceso acompañan la publicación; la elegibilidad/precios reales se verifican al implementar configuración T03. La forma CC01 queda publicada; la conducta y la compatibilidad integral E8b quedan pendientes de T03 y la puerta final.

### CC02 — turno activo al reabrir una conversación

**Estado: autorizado expresamente por el usuario el 10/10 UTC; publicado junto con CC01/CC03, pendiente de revisión/commit.** El usuario respondió «Sí, publicar CC02 y regenerar tipos» a la propuesta concreta, registrado en `c026d6cb`. Integración transmitió el hallazgo de Front; la raíz cotejó los DTOs y las tres rutas `stop`. En T01, `AssistantConversationResponse` devolvía `conversation` y `messages`; ninguno contenía el identificador de turno. El identificador sí llega por SSE `start` y `TurnResult`, pero esos datos no están disponibles al reabrir el historial. La ruta de detener exige `conversationId` y `turnId`, por lo que T05 debe completar la lectura durable del campo publicado para detener desde el detalle reabierto una respuesta que sigue en generación.

Delta mínimo propuesto: agregar **`activeTurnId: string(uuid) | null` al nivel superior de AssistantConversationResponse**, compartido por los GET de detalle Consumer, Business y Platform. La propiedad se devuelve siempre. Contiene sólo el identificador del turno propio no terminal de esa conversación, admitido y todavía cancelable; vale `null` cuando no hay turno activo o éste terminó Complete/Stopped/Failed. La consulta no cambia versiones ni estados. Summary, Message, SSE, requests, rutas y permisos conservan su forma.

Antes de leer el turno se revalidan propietario, lado y organización con los mismos controles del detalle; un chat ajeno o borrado conserva 404. No se agrega una lectura administrativa de turnos ni se deriva el identificador en Front. El índice de un solo turno vivo por conversación de T05 asegura que la referencia sea única. El front puede usar el valor durable para la ruta `stop` existente, con la misma autorización al detener.

Publicación: schema requerido y nullable, ejemplos con turno y sin turno, tipos B/F y nuevos hashes; mantener las 120 rutas E8 intactas y dejar sólo los deltas CC01/CC02/CC03 aprobados. La conducta durable se verifica en T05 con reabrir pendiente/en ejecución, detener, terminales sin turno activo, dos propietarios/organizaciones/accesos y operadores completos sin lectura ajena. La publicación contractual no sustituye esa prueba de runtime ni habilita dar por resuelta la rama antes de comprobarla. Los tres deltas se publican juntos en un único pase B/F después del commit T02.

### CC03 — progreso del presupuesto y cuota diaria en Uso

**Estado: autorizado expresamente por el usuario el 10/10 UTC; publicado junto con CC01/CC02, pendiente de revisión/commit.** El usuario respondió «Sí, publicar CC03 y regenerar tipos» a la propuesta concreta, registrado en `64d71f69`. Integración transmitió el hallazgo T13; la raíz cotejó `AssistantUsagePageResponse`, `AssistantUsageRowResponse`, los lienzos v115 de Uso-Org y sus variantes móviles, y la regla de formatos. En T01, el contrato tenía costo y presupuesto mensual, pero no su fracción/estado; `queries` es mensual y `dailyLimit` no informa las consultas de hoy. El front tiene prohibido calcular montos o porcentajes.

Delta aditivo mínimo, conservando rutas, permisos, requests y todos los miembros actuales:

- `AssistantUsagePageResponse.budgetProgress`: objeto nullable `{fraction: decimal, status: "WithinLimit"|"NearLimit"|"LimitReached"}`. Fracción calculada en Back con costo registrado del mes dividido por su presupuesto, sin incluir meras reservas; no se recorta a 1 si supera el límite. Estado NearLimit desde 0,8 y LimitReached desde 1, conforme a los umbrales 80/100 de T06. Null si no aplica presupuesto. En un mes histórico se usa el presupuesto conservado para ese mes, sin recalcular costos o aplicar un tope actual ficticio. El resumen Platform agregado no tiene un presupuesto único: null.
- `AssistantUsageRowResponse.dailyUsage`: objeto nullable `{dayUtc: date, resetAtUtc: date-time, scope: "PlatformKey"|"OwnKey", queries: int, limit: int, limitReached: bool}`. Sólo filas Person del Uso Business en el mes civil actual de esa organización. `queries` son las consultas consumidas del día UTC, independiente del total mensual de la fila; PlatformKey lee la cuota compartida por UserId entre accesos y organizaciones, OwnKey la cuota del UserId de esa organización. `limitReached` usa consumo más reservas para indicar si se puede admitir otra consulta; no convierte una reserva en consulta consumida. Fecha y renovación UTC son explícitas. `dailyLimit` existente conserva su significado y coincide con `dailyUsage.limit` cuando hay proyección.

En meses históricos y filas por día, organización, Personal/Operadores agregados, `dailyUsage` es null: no se presenta un total mensual como consumo de hoy ni se inventa una cuota individual del agregado. El período mensual civil y el día de cuota UTC siguen siendo reglas distintas. Ambas propiedades nuevas se devuelven siempre y son required/nullable en OpenAPI. Ninguna proyección contiene chat, prompts, claves ni IDs de conversaciones. No se agregan cálculos al Front ni lecturas de contenido privado desde Platform.

La publicación requiere schema/ejemplos/tipos B/F, pruebas independientes de CC03 y comparación estructural con el freeze T01, dejando únicamente CC01/CC02/CC03 autorizados. T06 debe probar cálculo real, 80/100, cero uso, mes histórico y snapshots, cambio de día/zona, cuota compartida PlatformKey versus OwnKey, reservas y aislamiento. Publicar el contrato no acredita esa conducta ni cierra T13/E8b. Sólo los dos archivos de tipos Front; UI y fixtures pertenecen a Front.

### CC04 — personas por día y financiación histórica en Uso

**Estado: propuesta pendiente de autorización y publicación coordinada; no aplicada a DTO, OpenAPI ni tipos Front.** Integración comunicó el 10/10 los datos ausentes en T13 v115. La raíz cotejó las proyecciones publicadas: Page ya tiene `peopleCount` del período, pero Row no informa personas del día ni financiación; Page no informa cuántas organizaciones tuvieron uso. No se obtienen estos datos de la página visible, del presupuesto, de los topes ni de la configuración actual.

Delta aditivo mínimo propuesto, conservando rutas, permisos, requests y miembros existentes:

- `AssistantUsageRowResponse.peopleCount: int | null`, propiedad requerida y nullable. Sólo la fila `day` de Uso Business devuelve la cantidad de UserId distintos con consumo de modelo contabilizado en ese día civil de la organización. Una persona que usó ambos modos cuenta una vez; los títulos no suman personas ni consultas adicionales. Sin consumo: cero si se incluye esa fila. Las filas `person`, `organization`, `personal` y `operator` devuelven null; `AssistantUsagePageResponse.peopleCount` conserva su significado de personas distintas del período completo.
- `AssistantUsageRowResponse.fundingModes: AssistantMode[]`, propiedad requerida, siempre array, reutilizando el enum cerrado `PlatformKey`/`OwnKey`. Contiene los modos distintos de la actividad contabilizada de esa fila y período, ordenados PlatformKey y OwnKey, sin duplicados. Una organización que cambió de financiación durante el mes conserva una sola fila agregada con ambos modos; no se divide la fila ni se modifica Queries/Cost. Las filas Business por día/persona también pueden tener ambos. Los agregados `personal` y `operator` contienen únicamente PlatformKey cuando tuvieron consumo. Array vacío significa que la fila no tuvo actividad contabilizada: no se presenta la selección actual como financiación histórica. La UI puede mostrar las dos claves cuando corresponda, sin inventar un modo único ni agregar `Mixed` al enum de configuración.
- `AssistantUsagePageResponse.organizationCount: int | null`, propiedad requerida y nullable. Sólo Uso Platform devuelve el número de organizaciones Business distintas con consumo de modelo contabilizado durante el mes solicitado, antes de paginar y excluyendo los agregados Personas/Operadores. Cero cuando no hay organizaciones con uso, aunque existan organizaciones habilitadas. Uso Business devuelve null. No equivale a `rows.totalCount`, ni al número de organizaciones configuradas o presentes en la página.

Actividad contabilizada significa una invocación del modelo registrada conforme a T06, también cuando termina detenida o fallida y corresponde conservar consumo/costo; excluye consultas fijas, reservas sin invocación y configuraciones sin uso. Las llamadas de título aportan costo al modo de su turno, conservando la deduplicación de personas/organizaciones. No se cambia el período mensual civil, el día civil de la fila Business ni la cuota UTC de CC03.

**Fuentes de datos e implementación posterior:** T06 conserva la financiación derivada del servidor al admitir/completar el turno, junto con sus snapshots de costo y período. Los agregados privados Business se leen dentro del tenant con RLS y deduplican UserId entre modos. El espejo administrativo global, sin texto, debe conservar organización Business, usuario/ámbito, período y modo de financiación para calcular filas y contadores completos antes de paginar. No se entra a todos los tenants desde Uso Platform. Las entidades diarias actuales de T02 aún no tienen este desglose ni completado real; la propuesta no las presenta como una fuente suficiente ni rellena historia ausente con la configuración actual. La precisión de ADR0051 y los snapshots de precios siguen vigentes.

**Verificación al publicar:** schemas required/nullable o array, ejemplos de cero uso y dos financiaciones, serialización real, tipos B/F regenerados y comparación estructural que deje sólo CC04 sobre el freeze CC01–03. **Verificación de conducta en T06:** dos personas repetidas durante un día y varios días; una misma persona con ambos modos; cambio PlatformKey→OwnKey→PlatformKey sin recalcular historia; reservas/Quick excluidos, título sin aumentar consultas/personas; consumo contabilizado detenido/fallido; varias organizaciones con página pequeña y Personas/Operadores excluidos del contador; mes histórico, zona civil/UTC, espejo y consumo en rollback, aislamiento y ausencia de contenido privado. Los costos se suman en Back con Money/decimal; el Front no calcula porcentajes, montos ni conteos globales desde filas paginadas.

**Alcance:** dos DTO de Uso y sus schemas/ejemplos, pruebas contractuales y los dos archivos de tipos previstos; ningún endpoint, permiso, request, SSE ni financiación automática nueva. La conducta T06 y la UI T13 se componen después de la aprobación conjunta. T04a continúa independiente. No se publica CC04 ni se cierra T13/E8b con esta propuesta.

La discrepancia separada de T12b sobre revocar OwnKey no es un cambio CC04: el plan T03 exige dejarla inutilizable y permitir un guardado explícito PlatformKey, sin usar o facturar otra clave por sorpresa. ADR0048/0049 no establecen un fallback automático que reemplace esa regla. El ajuste del mensaje visible del lienzo queda a cargo de la coordinación con el usuario; Back conserva la conducta T03.

## Publicación CC01/CC02/CC03

OpenAPI SHA-256 LF: `ac450a7ca6ccfda23e5bd16f2f2fec0c20e3bec9176cfd785be9960f3919c9f4`. Schema Front generado SHA-256 LF: `523dcdbf361e37a624e90746d396d886574425d3df8c3e88c0aadbdc310175e4`. `types.ts` conserva sus aliases y no requiere modificación; SHA-256 LF `67f6aa7c2593e5317f23ae34f653e1ff7a72fbeb9e603d14fb19488b4a0818f7`.

La comparación exacta contra el freeze T01 deja sólo cuatro DTO modificados (Settings Business, detalle de conversación, UsagePage y UsageRow), tres schemas nuevos (BudgetProgress, BudgetStatus y DailyUsage), y ejemplos de seis GET. Conserva las 160 rutas, sus permisos, requests y SSE; las 120 rutas E8 siguen idénticas y sus 184 schemas mantienen la forma congelada T01. `budgetProgress`, `dailyUsage` y `activeTurnId` son required/nullable; ambas listas CC01 son required y no nullable. Los ejemplos muestran listas permitidas/vacías, turno/null y cuotas PlatformKey/OwnKey separadas del total mensual, agotamiento por reservas, umbrales 80/100 sin recorte, histórico y agregados sin cuota diaria.

Verificación: tres pruebas RED por las formas ausentes; Architecture focal final 25/25, incluyendo JSON real con las proyecciones null/pobladas; integración Docker focal 8/8, incluyendo 401/403 y configure sin use, aislamiento y errores. Builds Back afectados: cero advertencias/errores de compilador. El exportador offline generó OpenAPI sin conexión ni proveedor; el sandbox emitió un diagnóstico DataProtection por falta de acceso al key ring local, sin impedir exportación (exit 0). Generación y `contracts:check` Front exit 0; TS focal de los aliases/generated exit 0 y suites existentes contracts/httpClient/queryClient 36/36. El build completo Front llegó a TS y falló exit 2 por `src/modules/assistant/runtime/useAssistantRuntime.test.tsx(21,101)`, TS2741: la fixture anterior carece del nuevo `activeTurnId` requerido; Vite no se ejecutó. La fuente de esa fixture pertenece a Front y no se modificó en este pase. Dos intentos focales Vitest en sandbox encontraron caches temporales ENOENT; se excluyeron worktrees `.local` y la ejecución elevada pasó las tres suites, sin cambios de fuente. Docker requirió acceso elevado al named pipe. Los intentos y exits reales se conservan en el manifiesto.

T02 fue revisado SPEC/QUALITY y commiteado en `fa74d44768187b40e6aba175aa1f1fe2a2babefb`, con verificación dirigida final 19/58/33 y baseline único T02 entre proyectos 213 (Architecture 86); esos grupos pertenecen a T02 y no se suman a esta publicación. T03 todavía implementa opciones/precios/elegibilidad reales y sus gates; configuración Business conserva 501. T05 implementa admisión y referencia durable autorizada del turno no terminal/índice/reabrir/stop: el detalle T02 agrega temporalmente `activeTurnId=null`, lo que no resuelve esa conducta. T06 todavía implementa Uso real, costos registrados/snapshots, reservas, umbrales, cuota UTC y aislamiento; Uso conserva 501. Las muestras contractuales no sustituyen pruebas productivas ni cierran T13, Parte 2 o E8b.

Evidencia local: `.local/reviews/e8b-contract/cc123-{manifest.json,files.json,delivery.md,structural-diff.json}` con comandos/exits/logs y hashes LF de cada fuente propia; revisión SPEC → QUALITY aprobada y commits Back/Front acreditados arriba, sin push. QUALITY dejó una mejora menor no bloqueante: reforzar las aserciones positivas de opciones OwnKey del ejemplo permitido; la implementación contractual actual es correcta. Las suites completas y la puerta real quedan en T09.

Corrección SPEC CC01: `noEligibleModels` ahora muestra OwnKey guardado con política posteriormente revocada (`allowOwnKey=false`), ambas listas vacías y modelo guardado conservado. Su descripción deja explícito que la configuración sigue visible/configurable y que la admisión no hace fallback; es una muestra del contrato, sin nueva regla o validación de GET. La guarda comprueba coherencia del modelo en todos los ejemplos seleccionados PlatformKey y el estado OwnKey denegado. Rojo dirigido: 4 pruebas, 1 fallo por la lista vacía; verde posterior: 4/4. Build afectado sin advertencias/errores de compilador y exportación offline exit 0. Regeneración y `contracts:check` Front exit 0, con tipos idénticos al pase anterior: el cambio afecta exclusivamente el modo y la descripción de ese ejemplo. Estas cuatro pruebas son subconjunto de la verificación previa de 69 casos, no cobertura adicional; no se repitieron las suites 25/8/36 ni el build completo Front. Evidencia adicional `cc123-specfix-*` incluida en el manifiesto.

## Pase a Back, Front e Integración

Back puede continuar T02–T09 automáticamente según la autorización posterior registrada. Front verifica los commits y este informe antes de implementar su Parte 2b; cada chat toca sólo su repositorio desde el congelamiento. No se enviaron mensajes a otros chats.

Integración recibe al finalizar Back las migraciones, registros/contribuidores de módulos, configuración sin secretos, resultados completos y comprobación de contrato inmutable. La puerta final conserva Anthropic real, E2E Front/API/BD, documentación, galería, sesiones anteriores y pruebas de quitar cada módulo. E8 conserva su recorrido Meta real independiente.
