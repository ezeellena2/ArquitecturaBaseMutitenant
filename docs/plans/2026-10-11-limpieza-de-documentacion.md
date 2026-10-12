# Limpieza de la documentación (tanda 2)

| Alcance / documento | Resultado | Palabras antes | Después |
|---|---|---:|---:|
| T1–T7 | Hechas en los dos repos; commits locales con pathspec, sin push | — | — |
| T8 | Historial retirado; pendientes los tres documentos protegidos de la 8b hasta el aviso del coordinador nuevo. E8 sigue activa. Tags locales historial-docs en ambos repos. | — | — |
| Builds back / front | Pasaron; back sin advertencias | — | — |
| Tests Node del arnés | 48/48; completa una vez y luego sólo fallos / detectores corregidos | — | — |
| Arquitectura back + HarnessTests | 253/256; HarnessTests pasó. Fallan NoManualFormattingTests, TenantScopeUsageTests y ReferenceDataHardcodeTests en Assistant WIP ajeno; sin cambiar esas reglas ni el producto. | — | — |
| Tests front + arnés explícito | 3010/3010 + 8/8; sólo los timeouts y el archivo no cargado se completaron aislados | — | — |
| Detector back / front | 0/0/0 en ambos; 0 tokens, sin hallazgos para revisar a mano | — | — |
| Galería genérica | Un caso nuevo de la 8b generado; 136 casos preparados y 2 pendientes en los datos | — | — |
| Back: arbol.md / arnes.md | Mapa único y explicación corta | 20429 / 2795 | 5679 / 1100 |
| Back: backend.md / multitenancy.md | Panorama y enlaces funcionales | 12410 / 6501 | 2588 / 4292 |
| Front: arbol.md / arnes.md | Carpetas, modelos y punteros | 11131 / 1165 | 2042 / 510 |
| Front: frontend.md | Receta de implementación | 9491 | 1399 |
| Front: architecture/formatos.md + rules/formatos.md | Una sola ficha, rules/formatos.md | 1980 + 1180 | 0 + 919 |
| Back: identidad.md / plataforma.md / whatsapp.md | Estado vigente; incluyen reglas exclusivas trasladadas desde panoramas y fichas | 6344 / 8653 / 1717 | 8939 / 9282 / 1808 |
| Back: plan-de-desarrollo.md / ADR 0048 | Hoja de ruta / decisiones D1–D26 | 10560 / 528 | 1114 / 1507 |
| Fichas modificadas back / front (totales) | Máximo 60 líneas; sólo guardas en Lo verifica | 22384 / 18488 | 15149 / 12876 |
| Punteros modificados back / front (totales) | Máximo 3 líneas; subcarpetas fijas front heredadas | 7279 / 7955 | 2460 / 777 |
| 88 Markdown históricos back retirados | Recuperables con git show historial-docs:ruta; además se retiraron 3 archivos auxiliares y 72 archivos del lienzo-v35 front | 306784 | 0 |
| Palabras de cada documento restante | [Desglose local por archivo](../../.local/doc-cleanup/palabras.md); no se versionan salidas | — | — |

**Cuándo:** apenas cierre la Etapa 8. T1 y T2, que son lo que traba cada commit, van antes de abrir los chats nuevos de la 8b; T3 a T7 son solo documentos y siguen en paralelo con la 8b. **Quién:** un chat nuevo de Codex, con los dos repos. **Por qué:** para programar una pantalla o un área hoy hay que leer unas 30.000 palabras por repo, y cada commit de código arrastra documentación que se mantiene a mano. Las revisiones del 2026-10-11 estimaron unas 45.000 palabras menos sin perder ninguna regla de código. La tanda 1 (back `aa5240be`, `1b0c4aef`; front `979c20db`, `88a8ae89`) ya corrigió las contradicciones; esta tanda cambia la estructura.

**Reglas:** `AGENTS.md`, «Velocidad y cierre liviano». No cambia la conducta del producto ni una regla de código: capas, guardado con `IUnitOfWork`, `Result`, aislamiento, permisos, tiempo, textos en resources, logs y tests de arquitectura. Las capturas siguen versionadas (decisión del usuario). Commits con pathspec, sin push.

## Tareas

| # | Qué | Archivos | Listo cuando | Estado |
|---|---|---|---|---|
| T1 | **El árbol por carpetas.** `arbol.md` lista carpetas, y archivos solo donde uno es el modelo para copiar. El detector `arbol` marca solo carpetas nuevas o borradas. La regla del summary por archivo C# sale, porque no tiene test ni la consume nadie. | back y front `docs/architecture/arbol.md`; `scripts/arneses/realinear-docs/detectores/arbol.mjs` y su test; `docs/architecture/arnes.md` §3 se integra al árbol; `punteros.mjs`; `docs/architecture/backend.md` §4 y :119; `docs/guides/leer-backend.md`:40 | un archivo nuevo en una carpeta existente no genera hallazgos; tests del arnés en verde | Hecha |
| T2 | **Punteros mínimos (decisión del usuario, 2026-10-11).** Cada carpeta de capa, área, módulo, proyecto y shared/ (incluido src/areas del front) tiene AGENTS.md y CLAUDE.md (@AGENTS.md), hasta 3 líneas: responsabilidad, ficha y archivo modelo. Las subcarpetas fijas api/, pages/, components/ y tabs/ del front heredan el puntero de la feature, que las describe en una línea. | ambos src/**, tests/**, arbol.md y guardas del arnés | el árbol enlaza cada carpeta a su puntero y las guardas exigen exactamente ese mapa | Hecha |
| T3 | **Dos documentos cortos.** `backend.md` pasa a ser un panorama de unas 3.000 palabras: stack, capas, contexto de la petición, pipeline, OIDC y hosting; lo que repite fichas o features se reemplaza por un enlace. `frontend.md` pasa a ser una receta de 2.000 palabras como mucho: estructura con `modules/` y `shared/modules/`, ruta, manifiesto y permiso, API y alias, pantalla, textos, tests, teléfono y cierre. | back `docs/architecture/backend.md` y `multitenancy.md` (§3.2, §8 y §12 a enlaces); front `docs/architecture/frontend.md` y los dos `formatos.md` fusionados | ninguna regla queda solo en lo borrado | Hecha |
| T4 | **Fichas cortas.** Cada ficha tiene unas 60 líneas cortas como mucho. «Lo verifica» nombra solo las guardas de arquitectura, no los tests de conducta de un área. Lo que es de una sola pantalla o área va a su feature. La lista de rutas anónimas queda en un solo lugar. | `docs/rules/*.md` de los dos repos; ajustes de `HarnessTests` y `harness.test.ts` si exigen nombres que se van | las guardas siguen exigiendo cada ficha y su formato | Hecha |
| T5 | **Las features de la 11 como estado vigente.** `whatsapp.md`, `identidad.md` y `plataforma.md` describen cómo funciona hoy, sin enlaces a planes ni revisiones y sin etiquetas de tarea (3a, T01, M1). | back `docs/features/{whatsapp,identidad,plataforma}.md` | un agente nuevo entiende el área sin abrir el historial | Hecha |
| T6 | **Galería en un solo script.** `scripts/capturar-etapa.mjs` recibe la lista de casos de una etapa en un archivo de datos, sin tests propios. Las PNG siguen en `docs/design/capturas/etapa-<n>/`. Los scripts de etapas ya cerradas se borran. | front `scripts/` y `docs/rules/tests.md` | la galería de la 8b se arma con ese script | Hecha |
| T7 | **Detalles.** Los punteros de `Modules/Assistant` (Application y Api) pasan a una línea. `quitar-whatsapp.md` y `quitar-asistente.md` quedan sin conteos fijos. `shared/modules/` se suma al árbol del front. Se borran `useEffectiveTimeZone` y `resolveEffectiveTimeZone`, que solo usan sus tests. | los nombrados | build y tests del alcance en verde | Hecha |
| T8 | **El historial fuera del árbol** (decisión del usuario, 2026-10-11). Hoy `docs/plans` tiene 185.000 palabras y `docs/reviews` 174.000, contra 105.000 de documentación vigente. (1) El plan maestro pasa a ser una hoja de ruta corta: una línea por etapa cerrada con su ADR o feature, y lo que queda (11, 9 y 10) con sus decisiones. (2) Los documentos vigentes que enlazan planes o revisiones (24 en el back) pasan a apuntar a la ADR o a la feature. (3) La tabla D1–D26 de `propuesta-asistente-llm.md` pasa a la ADR 0048. (4) Se etiqueta el commit anterior al borrado (`historial-docs`, local) y se borran los planes de etapas cerradas, `docs/reviews/` entero y, en el front, `docs/design/lienzo-v35`. El plan viejo de la 8b, su revisión y la propuesta del asistente se borran recién cuando el coordinador nuevo de la 8b avise que su plan corto ya tiene los pendientes. | back `docs/plans/`, `docs/reviews/`, `docs/decisions/0048-asistente.md` y los enlaces; front `docs/design/lienzo-v35/` | `docs/` tiene solo lo vigente; `git show historial-docs:<ruta>` recupera cualquier archivo; detector en 0/0/0 | Pendiente sólo del aviso para retirar los tres documentos protegidos |

**Decisiones técnicas:** mapa único en arbol.md; por decisión del usuario (2026-10-11), punteros de capa/área/módulo/proyecto/shared de hasta tres líneas y subcarpetas fijas del front heredadas; reglas exclusivas trasladadas a sus fichas; formatos tiene una sola ficha; galería genérica con casos JSON de la 8b (136 preparados y 2 pendientes, sin acreditar API real); T8 conserva el plan de la 8 todavía en puerta y los tres documentos de la 8b hasta el aviso del coordinador; el detector respeta carpetas mapeadas aunque estén vacías y exclusiones de descendientes; cada puntero describe una responsabilidad concreta y copia un modelo real de su carpeta o área.

## Cómo se cierra

Una sola corrida al final: tests del arnés (`node --test "scripts/arneses/**/*.test.mjs"`), `HarnessTests` y los tests de arquitectura del back, `npx vitest run src/test/harness.test.ts` y `npm test` del front, builds de los dos repos y `realinear-docs --detectar` en 0/0/0 en los dos. Arriba de este plan queda solo una tabla corta con el resultado y las palabras antes y después de cada documento.

## Texto para el chat de Codex

```
Leé AGENTS.md del back y del front y docs/plans/2026-10-11-limpieza-de-documentacion.md (back). Hacé T1 a T7 en ese orden, en los dos repos, y T8 al final. No cambies la conducta del producto ni una regla de código: solo la estructura y el largo de la documentación, y los tests del arnés que la verifican. Si una regla queda solo en un texto que borrás, movela a su ficha. Las decisiones técnicas son tuyas: anotalas en una línea del plan. Commits con pathspec, sin push. Al final, la corrida de «Cómo se cierra» una sola vez y la tabla de resultados arriba del plan.
```
