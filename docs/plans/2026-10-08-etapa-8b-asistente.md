# Etapa 8b: el asistente — cerrada

| Tarea | Estado |
|---|---|
| T00–T01 · Decisiones y contrato | Hecha |
| T02–T04 · Instalación, configuración, catálogo y consultas fijas | Hecha |
| T05–T08 · Runtime, SSE y continuaciones | Hecha |
| T09 · Guardas y receta de quitar, incluidos los siete fallos | Hecha |
| T10–T13 · Front | Hecha |
| T14 · API real y FakeChatClient | Hecha |
| T15 · Puerta | Hecha |
| Anthropic real | Hecha con la clave existente de la Api |

No quedan tareas pendientes. Rigen D1–D26 de [ADR 0048](../decisions/0048-asistente.md) y [ADR 0049](../decisions/0049-precisiones-whatsapp-y-asistente.md). La implementación y los textos ejecutados para Back/Front quedan en git.

| Paso de la puerta | Resultado final acumulado |
|---|---|
| Build Back | 0 errores y 0 advertencias |
| Tests Back | 3033/3033, suite única y focos; 0 omisiones |
| Build y lint Front | 0 errores; 6 advertencias de lint preexistentes |
| Tests Front | 3015/3015, suite única y focos |
| Contrato | Pasó |
| E2E real, núcleo y WhatsApp incluidos | Pasó; Assistant: 42 hitos, 24 bloques técnicos y 8 cruces |
| Markdown y PDF | Abiertos y revisados en 1440 y 390; exportes Anthropic reales también revisados |
| Detector documental | 0/0/0 en ambos repos; cambios finales verificados sólo en su foco |
| Galería E8b lado a lado | 136/136 revisadas |
| Revisión única de código | Hecha |
| Recorrido con sesiones conservadas | 152/152; entorno apagado |
| Quitar Assistant con base vacía | Back 2577/2577, Front 2507/2507; modelos, migraciones e ingreso aprobados |
| Quitar ambos módulos con otra base vacía | Back 2317/2317, Front 2507/2507; modelos, migraciones e ingreso aprobados |
| Anthropic real | Herramienta, PNG/PDF, título, replay, financiación propia, stop, alertas y bloqueo sin llamada aprobados |
| Cierre | Entornos apagados; tres worktrees y dos copias propios quitados; commits con pathspec, sin push |

Verificado en los mismos worktrees hasta Back `86a8dd5f` y Front `da34f977`. Una corrida completa por paso y después sólo focos afectados, conservando los verdes.

Anthropic utilizó `Assistant:Platform:ApiKey` de user-secrets, sin exponerla: 6 llamadas de respuesta/título y 1 validación de clave. Ledger registrado **USD 0,0136789 estimados**, no factura del proveedor. Los saldos previos de las alertas 80/100 son sintéticos. El GET de consumo corregido se verificó con un fixture nuevo y cero llamadas al proveedor; el ledger real anterior quedó preservado por separado. El probe de Google del retiro sustituye sólo el proveedor externo; no es un ingreso real a Google.

Salidas privadas en `.local/puerta-8b/`: `galeria-8b/index.html`, `recorrido-8b/index.html` y `anthropic-real/anthropic-real.pdf`. Sin actas ni evidencia versionada.
