# E8b · A2 catálogo técnico editable y M5 vinculado

## Alcance autorizado

Correctivo autorizado Full technical catalog: `.local/reviews/e8b-contract/review-priority-catalog-human-authorization.json` y propuesta `.local/reviews/2026-10-10-e8b-catalogo-editable-propuesta.md`. Base de fuentes `b8412d2206d2af529604c7cb5f724bb6b3c2a680`, export base SHA256 `78c5120454f240a1a42ccdd24fb42c1d838c325e32ad953b1615759df16bb3f6`. Sin cambios Front, permisos nuevos ni rutas nuevas. Root publica; este dueño entrega fuentes sin stage/commit.

## Resultado y decisiones tomadas

El catálogo completo vive en `platform.AssistantModelPrices`, bajo el mismo xmin, UOW y auditoría global. Crear/editar exige `technical` completo: capacidades de texto/herramientas/imágenes/PDF/adaptive, esfuerzos únicos low/medium/high, límites positivos coherentes, tres tarifas de caché Money USD6 y tramo largo opcional con umbral y cinco tarifas completas. Se rechazan tarifas técnicas cero, negativas, moneda distinta, más de seis decimales y valores fuera de numeric(18,6). Los precios base conservan la regla no negativa existente. Las filas heredadas desconocidas mantienen `technical=null`/`technicalRevision=null`, sin inventar capacidades ni borrarlas; una edición completa las repara.

Settings OwnKey/PlatformKey y ConfigurationPolicy comparten la proyección actual de base y la misma elegibilidad: modelo activo Anthropic, texto, herramientas, adaptive, capacidad al menos 51200/2048 y esfuerzo elegido admitido. Imágenes/PDF no impiden chat de texto; su validación por adjunto pertenece al corte correspondiente. Un modelo menor coherente se administra pero no se ofrece como elegible. No se reducen dinámicamente los topes del módulo: entrada51200, salida2048, título64, cinco llamadas incluyendo título. State conserva la forma `{id,name}` y su implementación pendiente queda fuera de este corte.

`technicalRevision` es SHA256 (`db-v1-…`) calculado por servidor sobre identificación, capacidades, esfuerzos, límites y tarifas efectivas. Decimales canónicos G29 y esfuerzos ordenados evitan cambios por escala textual u orden; nombre, estado activo y xmin no modifican la referencia. El snapshot financiero copia esos datos al admitir y no se recalcula al editar catálogo. Tarifas6 y reserva/costo12 se conservan, sin redondeo de tarifa a unidades menores.

JSON embebido queda sólo para los presets del bootstrap inicial; se retiró ProviderReferenceFile y cualquier lectura de archivo externo runtime. Si ya hay configuración, seed es no-op, incluso si se renombró el provider ID de un preset o USD está deshabilitada. M5 usa marcadores iniciales de default/allowed y valida claves con el default actual elegible low o el compatible activo de menor suma de tarifas base, con desempate por ID. No requiere un literal Haiku/Sonnet ni consulta Models API. Transporte B5 se mantiene fuera.

## Persistencia y migración

Migración única nueva `20261010223305_AssistantEditableModelCatalog`, generada por EF CLI junto con Designer y snapshot. Agrega columnas opcionales y ocho FK monetarias al catálogo factual. Up completa sólo los cuatro IDs publicados conocidos, conserva IDs/nombres/precios base/estado/settings y deja un SecurityEvent técnico por fila completada en la misma transacción. Unknown legacy queda null. No usa BYPASSRLS ni cambia grants existentes.

Down elimina metadata y cualquier edición técnica posterior; conserva precios base y eventos. Up posterior vuelve a completar presets conocidos y no puede recuperar ediciones eliminadas. Es una pérdida explícita, no una reversión semántica de lo que editó el operador.

Al borrar el tramo largo, las monedas shadow que sostienen las FK no escriben la columna compartida (`BeforeSaveBehavior/AfterSaveBehavior.Ignore`); el owned Money es su único escritor. Así se evita el conflicto EF de Currency entre el espejo shadow y Money eliminado sin quitar integridad referencial ni hacer SQL fuera del UOW.

## Fuentes oficiales verificadas · 2026-10-10

Sin credenciales ni llamadas reales a Anthropic. Los cuatro IDs y sus especificaciones se verificaron en [modelos](https://platform.claude.com/docs/en/models/overview) y las fichas [Fable5.1](https://platform.claude.com/docs/en/models/fable-5-1/overview), [Opus5.5](https://platform.claude.com/docs/en/models/opus-5-5/overview), [Sonnet5.5](https://platform.claude.com/docs/en/models/sonnet-5-5/overview), [Haiku5.5](https://platform.claude.com/docs/en/models/haiku-5-5/overview). Bootstrap: entrada1.000.000 y salida128.000. La definición de `max_input_tokens` como ventana máxima de entrada está en el [tipo oficial SDK](https://raw.githubusercontent.com/anthropics/anthropic-sdk-python/main/src/anthropic/types/model_info.py) y se contrastó con [context windows](https://platform.claude.com/docs/en/build-with-claude/context-windows). No se usaron los ceros de ejemplo de retrieve como metadatos reales.

[Precios oficiales](https://platform.claude.com/docs/en/about-claude/pricing), USD por millón: Fable10/50/cache12,5/20/0,25; Opus4/20/cache5/8/0,2; Sonnet2/10/cache2,5/4/0,1; Haiku0,1/0,5/cache0,125/0,2/0,01. Haiku largo desde más de100.000:0,5/2,5/cache0,625/1/0,05. Los valores explícitos se preservan; no se calculan con ratios ni se inventan modelos. [Effort](https://platform.claude.com/docs/en/build-with-claude/effort) y [PDF](https://platform.claude.com/docs/en/build-with-claude/pdf-support) corroboran capacidades; la aplicación conserva exclusivamente low/medium/high aprobados.

## Contrato y coordinación

La forma cambia en Create/UpdateAssistantModel, AssistantModelPriceResponse y sus arrays de configuración; se agregan cuatro componentes technical/longContext request/response. technical es obligatorio no-null al escribir y obligatorio nullable al leer. technicalRevision es de respuesta, obligatorio nullable, calculado por servidor. Ejemplos request.es/request.en, validaciones de campo bilingües y legacyIncomplete acompañan el export. No se cambian rutas, permisos, State.models, Usage ni CC05/CC06; los últimos están autorizados para otra frontera de Root.

Diff semántico exacto y hashes en `.local/reviews/e8b-contract/a2-openapi-pointer-diff.json`. Coord conserva la sección Cambios al contrato y Front regenera sus tipos desde el export que publique Root.

## Verificación

RED observado: Create201 persistía un ID nuevo pero OwnKeyModels lo excluía; 1/1 fallo real en39,258s (`a2-red.log`). GREEN inicial1/1 en39,753s (`a2-green.log`). No se cuentan compilaciones fallidas ni errores de IPC como RED.

Resultado final: **214 tests distintos correctos, 0 fallos y 0 omitidos** en los filtros ejecutados. El GREEN inicial se solapa con el grupo nuevo y no se suma dos veces.

| Verificación | Resultado | Tiempo | Evidencia local |
|---|---:|---:|---|
| HTTP nuevo ID, legacy/permisos y migración Up/Down/Up | 3/3 | 54,203 s | `a2-editable-integration-4.log` |
| Application: validación, settings, costos y paridad de recursos | 52/52 | 2,923 s | `a2-application-tests.log` |
| Arquitectura: Assistant, contrato, capas, UOW, precisión y referencia | 89/89 | 33,734 s | `a2-architecture-tests.log` |
| IT: catálogo, settings, startup, adaptador, persistencia, Finance, precisión, migración, gates y contrato | 70/70 | 5 min 02,069 s | `a2-integration-regression.log` |

Los tres casos nuevos acreditan uso inmediato OwnKey/PlatformKey y resolución de policy desde DB, cambios de ID y metadata, retiro del tramo largo, capacidad insuficiente excluida, revisión efectiva y snapshots Finance históricos sin repricing (tarifas USD6 y reserva USD12). También prueban legacy nullable sin Use ni key, doce variantes de metadata inválida con errores es/en, stale409 sin escritura parcial de auditoría, seed idempotente y migración conservando IDs/settings/precios base, grants, mt_owner NOBYPASSRLS y ocho FK de moneda.

La regresión excluye expresamente `Default_chat_is_hidden_and_platform_management_can_configure_without_a_key_with_individual_permissions`: su fallo conocido del fixture compartido queda reservado al correctivo independiente de Root. La exclusión figura literalmente en el comando; no representa una suite global aprobada.

Builds seriales de API (61,69 s), Application.UnitTests (7,35 s), ArchitectureTests (8,63 s) y último Api.IntegrationTests (20,14 s): exit0, cero advertencias y errores. `has-pending-model-changes` devuelve cero cambios; SQL incremental y `git diff --check` exit0. El generador de OpenAPI dejó un diagnóstico de acceso al key ring del sandbox durante el build API; la compilación y export terminaron correctamente, sin cambios de secretos.

El export generado por la API y su copia versionada son idénticos byte a byte: 2.205.156 bytes, SHA256 `86428a7254f5e7f6f412fb06c63e05287655e03f81e17c49ce4a8c88f81928a0`. Lo acreditan `a2-api-build.log` (GenerateOpenApiDocuments) y `a2-export-verify.log` (comparación raw). El delta contiene **41 pointers semánticos** sobre el baseline 78c512, con componentes, ejemplos es/en y validaciones limitadas al catálogo editable.

Inventario final de fuentes, SHA256 raw, bytes, blob base y evidencia con comandos/salidas/tiempos: `.local/reviews/e8b-contract/e8b-editable-catalog-files.json`. Role.cs ajeno protegido SHA256 `3260a9378a8bb68317e1d92ce81e290435d29b7d030624d72dfed470a114039a`. Sin suite global, arnés global, Aspire, Meta ni proveedor real; pruebas SDK usan transporte simulado explícito y no acreditan prueba humana final.
