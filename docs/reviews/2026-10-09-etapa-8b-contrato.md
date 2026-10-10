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

## Pase a Back, Front e Integración

Back puede continuar T02–T09 automáticamente según la autorización posterior registrada. Front verifica los commits y este informe antes de implementar su Parte 2b; cada chat toca sólo su repositorio desde el congelamiento. No se enviaron mensajes a otros chats.

Integración recibe al finalizar Back las migraciones, registros/contribuidores de módulos, configuración sin secretos, resultados completos y comprobación de contrato inmutable. La puerta final conserva Anthropic real, E2E Front/API/BD, documentación, galería, sesiones anteriores y pruebas de quitar cada módulo. E8 conserva su recorrido Meta real independiente.
