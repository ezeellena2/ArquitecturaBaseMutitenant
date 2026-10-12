# Etapa 8b: el asistente — pendientes de cierre

| Tarea | Estado |
|---|---|
| T00–T01 · Decisiones y contrato | Hecha |
| T02–T04 · Instalación, configuración, catálogo y consultas fijas | Hecha |
| T05–T08 · Runtime, SSE y continuaciones | Hecha |
| T09 · Guardas y receta de quitar, incluidos los siete fallos | Hecha; prueba física pendiente en T15 |
| T10–T13 · Front | Hecha |
| T14 · API real y FakeChatClient | Hecha |
| T15 · Puerta determinística | Pendiente recorrido y retiros |
| Anthropic real | Clave disponible; pendiente prueba real al final |

Lo hecho queda en git. Este plan contiene sólo el cierre pendiente. Rigen D1–D26 de [ADR 0048](../decisions/0048-asistente.md), las precisiones de [ADR 0049](../decisions/0049-precisiones-whatsapp-y-asistente.md), [asistente](../features/asistente.md) y [OpenAPI](../contracts/openapi.json). No ampliar producto ni E11. Decisiones técnicas a cargo de quien programa; sólo producto se consulta. Commits convencionales en español, con pathspec; sin push ni cambios ajenos.

## T15 · Puerta pendiente

Aplicar [/puerta](../../.agents/skills/puerta/SKILL.md) y [/recorrido](../../.agents/skills/recorrido/SKILL.md). Mantener los worktrees originales de la puerta: los arreglos se incorporan ahí. Cada paso completo pasa una sola vez; después se ejecuta únicamente test, área o caso afectado. Sólo autenticación, persistencia base o composición justifican repetir la suite completa de un repo. Front, E2E y recorrido van solos. Una repetición aislada para un timeout. Sin revisores por arreglo, actas, evidencia versionada ni corridas de estabilidad.

| Paso | Resultado acumulado |
|---|---|
| Build Back | Pasó: 0 errores y 0 advertencias |
| Tests Back | 3026/3026, suite única y focos; 0 omisiones |
| Build y lint Front | Pasaron: 0 errores; 6 advertencias de lint preexistentes |
| Tests Front | 3015/3015 acumulados, suite única y focos |
| Contrato | Pasó |
| E2E real, núcleo y WhatsApp incluidos | Pasó; Assistant: 42 hitos, 24 bloques técnicos y 8 cruces |
| Markdown y PDF reales | Abiertos y revisados en 1440 y 390 |
| Detector documental | 0/0/0 en ambos repos |
| Galería E8b lado a lado | 136/136 revisadas; sólo pantallas E8b |
| Revisión única de código | Hecha |
| Recorrido con sesiones conservadas | Pendiente |
| Quitar Assistant con base vacía | Pendiente suite e ingreso; builds y contrato aprobados |
| Quitar ambos módulos con otra base vacía | Pendiente suite e ingreso; builds y contrato aprobados |
| Anthropic real | Clave disponible; prueba real pendiente al final |

Cortes medidos en los mismos worktrees: Back `a9f60b9c`, Front `3b6f0f08`. Los totales acumulados conservan los casos verdes de la única corrida completa y suman únicamente los focos resueltos y casos nuevos. Salidas locales en `.local/puerta-8b/`, sin historial de corridas en este documento.

### Recorrido de usuario

**Archivos Front:** `scripts/recorrido-usuario.mjs`, `recorrido-usuario-asistente.mjs`, `assistant-real-ui.mjs` y los seis guiones aprobados de `docs/design/recorridos/`. Backend: fixtures aisladas de `tools/RealE2ESetup/` y `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/TestFeatures/`, sólo si un fallo demuestra que requieren arreglo.

Ejecutar una vez `node scripts/recorrido-usuario.mjs --no-build`, ambos anchos, un navegador persistente y sin borrar sesiones. Mantener núcleo y E8; no recapturar su galería. Fake reemplaza sólo al proveedor del modelo. Si falla, conservar lo aprobado y continuar sólo lo fallado o pendiente, con preparación mínima explícita. Abrir únicamente resumen y fallo. Tests del arreglo: helper/recorrido pertinente o área funcional modificada; build del proyecto afectado. Preservar índice y capturas en `.local/puerta-8b/recorrido-8b/` antes de retirar worktrees.

### Retiro físico: Assistant y ambos módulos

**Archivos:** [quitar-asistente](../guides/quitar-asistente.md), [quitar-whatsapp](../guides/quitar-whatsapp.md), contribuciones runtime/DesignTime/bootstrap, migración Drop generada desde el modelo instalado y tests neutrales afectados. En Front, composición, contribuciones, dependencias, contrato regenerado y tests del núcleo de la copia.

Las dos copias descartables ya están preparadas. Ejecutar una única suite Back por copia sobre su base vacía y una única suite Front por copia, secuencialmente. Verificar migraciones históricas y Drop, modelos runtime/design/bootstrap, ausencia física de módulo/rutas/paquetes/tablas, RLS, legales, permisos e ingreso del núcleo. Assistant retirado conserva WhatsApp; ambos retirados conservan el núcleo. No repetir WhatsApp solo, ya probado en E8, ni agregar variantes de retiro.

**Tests:** `dotnet test --no-build` de cada copia Back, incluidos los probes de modelo, composición e ingreso Email/Google; `npm test -- --maxWorkers=1` de cada copia Front. El probe de Google sustituye únicamente el proveedor externo; no cuenta como un ingreso real a Google. Ante un fallo, volver a su dueño y ejecutar sólo el test y área afectados. No repetir builds ya aprobados sin cambio. Conservar puertos neutrales; no borrar roles/asignaciones ni conceder permisos automáticamente. El preflight aborta si un operador queda sin permisos.

### Texto para el chat Back

```text
Etapa 8b: T05–T09 y E2E Assistant están hechos. Atendé sólo fallos Back del recorrido o del retiro físico según este plan y las recetas. Preservá los 3026 casos acumulados y el E2E aprobado; ningún rerun completo salvo cambio en autenticación, persistencia base o composición. Retiro Assistant y ambos: una copia/base vacía por escenario, suite e ingreso una vez, sin repetir WhatsApp solo. Técnico decidilo y avisalo; producto al coordinador. Commits propios en español con pathspec, sin push ni WIP ajeno. No agregar revisores ni actas.
```

### Texto para el chat Front

```text
Etapa 8b: T14 y E2E ambos anchos están hechos. Completar único recorrido con sesiones preservadas y las suites de las dos copias retiradas, en ventanas sin pruebas Back ni otro AppHost. Conservar los 3015 casos, la galería de 136 vistas y los verdes del E2E; repetir sólo lo afectado por un arreglo. No cambiar producto/E11. Técnico decidilo y avisalo; producto al coordinador. Commits propios en español con pathspec, sin push ni evidencias versionadas. Al terminar guardar índice/capturas en .local y apagar entorno propio; no tocar procesos ajenos.
```

## Proveedor externo y cierre

La clave ya está cargada en user-secrets de la Api como `Assistant:Platform:ApiKey`. No pedirla de nuevo ni mostrarla. Al final de las verificaciones determinísticas, probar Anthropic sin Fake según [configuración](../operations/configuracion.md): pregunta/herramienta, imagen/PDF, stop, título, consumo, financiación propia, presupuesto y alertas. Archivos afectados sólo si aparece un fallo: `src/ArquitecturaBaseMultitenant.Infrastructure/Modules/Assistant/AnthropicChatClientFactory.cs`, servicios del módulo y su área de tests; Front `src/modules/assistant/` y helpers del recorrido. No registrar secretos ni contenido privado. Conservar la comprobación externa de WhatsApp ya aprobada.

Al finalizar la puerta determinística, actualizar sólo esta tabla final, apagar Aspire propio y retirar los worktrees/copias propios. La prueba real de Anthropic debe completarse antes de declarar la etapa cerrada. No hay nuevos cambios de contrato HTTP durante este cierre.
