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

El contrato publica los seis eventos SSE cerrados, payload de wire separado del sobre OpenAPI, nueve proyecciones Quick tipadas, adjuntos multipart, exportación binaria, versiones, idempotencia privada y errores. Los servicios nuevos todavía devuelven 501 deliberadamente: Parte 2 debe implementar la conducta antes de su entrega final.

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

Ninguna necesidad pendiente al congelar T01. Durante Parte 2 se registra aquí cualquier cambio necesario antes de coordinarlo por el usuario. Eliminar la conducta 501 no autoriza alterar unilateralmente el JSON congelado o los tipos Front.

### CC01 — modelos y precios para configuración Business

**Estado: autorizada expresamente por el usuario el 10/10 UTC; publicación pendiente después de verificar T02.** El usuario respondió «Sí, publicar CC01 y regenerar tipos» a la propuesta concreta. Integración transmitió el hallazgo Front T12/D19; la raíz confirmó las firmas y permisos actuales.

`GET /api/organization/assistant/settings` exige assistant.configure y devuelve sólo modelId. `GET /api/organization/assistant` exige assistant.use, ofrece únicamente Id/Name y filtra el modo guardado. El catálogo Platform exige acceso Platform. Por eso Config-Org no puede mostrar precios ni opciones del modo que se está preparando antes de guardar, y una persona con configure sin use no tiene una lectura adecuada.

Propuesta concreta mínima: agregar a **OrganizationAssistantSettingsResponse**, conservando todos sus miembros actuales, `platformKeyModels: AssistantModelPriceResponse[]` y `ownKeyModels: AssistantModelPriceResponse[]`. Se reutiliza el DTO de catálogo ya publicado (Id, Name, Provider, ProviderModelId, InputPricePerMillion, OutputPricePerMillion, IsActive, Version); ambos precios conservan AssistantMoney/Money. Las listas son siempre arrays, vacíos cuando no hay elegibles, independientes del modo guardado:

- PlatformKey: sólo modelos activos, compatibles y permitidos por Plataforma.
- OwnKey: todos los modelos activos y compatibles si allowOwnKey; array vacío si la política lo impide.

La lectura conserva Business + módulo efectivo + assistant.configure; no exige assistant.use ni llama proveedor. No agrega ruta, permiso o secreto, ni cambia el modelo guardado al consultar opciones. Front puede filtrar el borrador de modo con estos datos reales sin inventar catálogo/precios o consultar Platform.

Publicar contrato/modelo/ejemplos y tipos B/F, con nuevos hashes y commits, después del cambio estable T02. Verificar configure-sin-use, 401/403, aislamiento/política/modelo inactivo o incompatible, precios y ausencia de claves; preservar las 120 rutas E8. Las guardas contractuales y de acceso acompañan la publicación; la elegibilidad/precios reales se verifican al implementar configuración T03. T02 y tareas independientes continúan. La compatibilidad integral E8b queda pendiente de resolver CC01.

### CC02 — turno activo al reabrir una conversación

**Estado: autorizado expresamente por el usuario el 10/10 UTC; publicación pendiente junto con CC01 después del commit verificado T02.** El usuario respondió «Sí, publicar CC02 y regenerar tipos» a la propuesta concreta. Integración transmitió el hallazgo de Front; la raíz cotejó los DTOs y las tres rutas `stop`. `AssistantConversationResponse` devuelve `conversation` y `messages`; ninguno contiene el identificador de turno. El identificador sí llega por SSE `start` y `TurnResult`, pero esos datos no están disponibles al reabrir el historial. La ruta de detener exige `conversationId` y `turnId`, por lo que el contrato actual no permite detener desde el detalle reabierto una respuesta que sigue en generación.

Delta mínimo propuesto: agregar **`activeTurnId: string(uuid) | null` al nivel superior de AssistantConversationResponse**, compartido por los GET de detalle Consumer, Business y Platform. La propiedad se devuelve siempre. Contiene sólo el identificador del turno propio no terminal de esa conversación, admitido y todavía cancelable; vale `null` cuando no hay turno activo o éste terminó Complete/Stopped/Failed. La consulta no cambia versiones ni estados. Summary, Message, SSE, requests, rutas y permisos conservan su forma.

Antes de leer el turno se revalidan propietario, lado y organización con los mismos controles del detalle; un chat ajeno o borrado conserva 404. No se agrega una lectura administrativa de turnos ni se deriva el identificador en Front. El índice de un solo turno vivo por conversación de T05 asegura que la referencia sea única. El front puede usar el valor durable para la ruta `stop` existente, con la misma autorización al detener.

Publicación: schema requerido y nullable, ejemplos con turno y sin turno, tipos B/F y nuevos hashes; mantener las 120 rutas E8 intactas y dejar sólo este delta adicional al CC01 aprobado. La conducta durable se verifica en T05 con reabrir pendiente/en ejecución, detener, terminales sin turno activo, dos propietarios/organizaciones/accesos y operadores completos sin lectura ajena. La publicación contractual no sustituye esa prueba de runtime ni habilita dar por resuelta la rama antes de comprobarla. CC01 y CC02 se publicarán juntos en un único pase B/F; T02 puede continuar de manera independiente.

### CC03 — progreso del presupuesto y cuota diaria en Uso

**Estado: autorizado expresamente por el usuario el 10/10 UTC; publicación pendiente junto con CC01/CC02.** El usuario respondió «Sí, publicar CC03 y regenerar tipos» a la propuesta concreta. Integración transmitió el hallazgo T13; la raíz cotejó `AssistantUsagePageResponse`, `AssistantUsageRowResponse`, los lienzos v115 de Uso-Org y sus variantes móviles, y la regla de formatos. El contrato tiene costo y presupuesto mensual, pero no su fracción/estado; `queries` es mensual y `dailyLimit` no informa las consultas de hoy. El front tiene prohibido calcular montos o porcentajes.

Delta aditivo mínimo, conservando rutas, permisos, requests y todos los miembros actuales:

- `AssistantUsagePageResponse.budgetProgress`: objeto nullable `{fraction: decimal, status: "WithinLimit"|"NearLimit"|"LimitReached"}`. Fracción calculada en Back con costo registrado del mes dividido por su presupuesto, sin incluir meras reservas; no se recorta a 1 si supera el límite. Estado NearLimit desde 0,8 y LimitReached desde 1, conforme a los umbrales 80/100 de T06. Null si no aplica presupuesto. En un mes histórico se usa el presupuesto conservado para ese mes, sin recalcular costos o aplicar un tope actual ficticio. El resumen Platform agregado no tiene un presupuesto único: null.
- `AssistantUsageRowResponse.dailyUsage`: objeto nullable `{dayUtc: date, resetAtUtc: date-time, scope: "PlatformKey"|"OwnKey", queries: int, limit: int, limitReached: bool}`. Sólo filas Person del Uso Business en el mes civil actual de esa organización. `queries` son las consultas consumidas del día UTC, independiente del total mensual de la fila; PlatformKey lee la cuota compartida por UserId entre accesos y organizaciones, OwnKey la cuota del UserId de esa organización. `limitReached` usa consumo más reservas para indicar si se puede admitir otra consulta; no convierte una reserva en consulta consumida. Fecha y renovación UTC son explícitas. `dailyLimit` existente conserva su significado y coincide con `dailyUsage.limit` cuando hay proyección.

En meses históricos y filas por día, organización, Personal/Operadores agregados, `dailyUsage` es null: no se presenta un total mensual como consumo de hoy ni se inventa una cuota individual del agregado. El período mensual civil y el día de cuota UTC siguen siendo reglas distintas. Ambas propiedades nuevas se devuelven siempre y son required/nullable en OpenAPI. Ninguna proyección contiene chat, prompts, claves ni IDs de conversaciones. No se agregan cálculos al Front ni lecturas de contenido privado desde Platform.

La publicación requiere schema/ejemplos/tipos B/F, pruebas independientes de CC03 y comparación estructural con el freeze T01, dejando únicamente CC01/CC02/CC03 autorizados. T06 debe probar cálculo real, 80/100, cero uso, mes histórico y snapshots, cambio de día/zona, cuota compartida PlatformKey versus OwnKey, reservas y aislamiento. Publicar el contrato no acredita esa conducta ni cierra T13/E8b. Sólo los dos archivos de tipos Front; UI y fixtures pertenecen a Front.

## Pase a Back, Front e Integración

Back puede continuar T02–T09 automáticamente según la autorización posterior registrada. Front verifica los commits y este informe antes de implementar su Parte 2b; cada chat toca sólo su repositorio desde el congelamiento. No se enviaron mensajes a otros chats.

Integración recibe al finalizar Back las migraciones, registros/contribuidores de módulos, configuración sin secretos, resultados completos y comprobación de contrato inmutable. La puerta final conserva Anthropic real, E2E Front/API/BD, documentación, galería, sesiones anteriores y pruebas de quitar cada módulo. E8 conserva su recorrido Meta real independiente.
