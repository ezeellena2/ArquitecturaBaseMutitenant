# Limpieza de la documentación (tanda 2)

**Cuándo:** apenas cierre la Etapa 8, antes de abrir los chats nuevos de la 8b. **Quién:** un chat nuevo de Codex, con los dos repos. **Por qué:** para programar una pantalla o un área hoy hay que leer unas 30.000 palabras por repo, y cada commit de código arrastra documentación que se mantiene a mano. Las revisiones del 2026-10-11 estimaron unas 45.000 palabras menos sin perder ninguna regla de código. La tanda 1 (back `aa5240be`, `1b0c4aef`; front `979c20db`, `88a8ae89`) ya corrigió las contradicciones; esta tanda cambia la estructura.

**Reglas:** `AGENTS.md`, «Velocidad y cierre liviano». No cambia la conducta del producto ni una regla de código: capas, guardado con `IUnitOfWork`, `Result`, aislamiento, permisos, tiempo, textos en resources, logs y tests de arquitectura. Las capturas siguen versionadas (decisión del usuario). Commits con pathspec, sin push.

## Tareas

| # | Qué | Archivos | Listo cuando |
|---|---|---|---|
| T1 | **El árbol por carpetas.** `arbol.md` lista carpetas, y archivos solo donde uno es el modelo para copiar. El detector `arbol` marca solo carpetas nuevas o borradas. La regla del summary por archivo C# sale, porque no tiene test ni la consume nadie. | back y front `docs/architecture/arbol.md`; `scripts/arneses/realinear-docs/detectores/arbol.mjs` y su test; `docs/architecture/arnes.md` §3 se integra al árbol; `punteros.mjs`; `docs/architecture/backend.md` §4 y :119; `docs/guides/leer-backend.md`:40 | un archivo nuevo en una carpeta existente no genera hallazgos; tests del arnés en verde |
| T2 | **Punteros mínimos.** `AGENTS.md` y su `CLAUDE.md` solo en la raíz de cada proyecto, área, módulo y `shared/`, de 5 líneas como mucho: qué va ahí y a qué ficha o feature ir, sin reglas copiadas ni estado del plan. Los demás se borran. | back `src/**`, `tests/**` y `HarnessTests`; front `src/**`, `docs/architecture/arnes.md` §2 y `src/test/harness.test.ts` | los arneses exigen solo esos punteros y quedan en verde |
| T3 | **Dos documentos cortos.** `backend.md` pasa a ser un panorama de unas 3.000 palabras: stack, capas, contexto de la petición, pipeline, OIDC y hosting; lo que repite fichas o features se reemplaza por un enlace. `frontend.md` pasa a ser una receta de 2.000 palabras como mucho: estructura con `modules/` y `shared/modules/`, ruta, manifiesto y permiso, API y alias, pantalla, textos, tests, teléfono y cierre. | back `docs/architecture/backend.md` y `multitenancy.md` (§3.2, §8 y §12 a enlaces); front `docs/architecture/frontend.md` y los dos `formatos.md` fusionados | ninguna regla queda solo en lo borrado |
| T4 | **Fichas cortas.** Cada ficha tiene unas 60 líneas cortas como mucho. «Lo verifica» nombra solo las guardas de arquitectura, no los tests de conducta de un área. Lo que es de una sola pantalla o área va a su feature. La lista de rutas anónimas queda en un solo lugar. | `docs/rules/*.md` de los dos repos; ajustes de `HarnessTests` y `harness.test.ts` si exigen nombres que se van | las guardas siguen exigiendo cada ficha y su formato |
| T5 | **Las features de la 11 como estado vigente.** `whatsapp.md`, `identidad.md` y `plataforma.md` describen cómo funciona hoy, sin enlaces a planes ni revisiones y sin etiquetas de tarea (3a, T01, M1). | back `docs/features/{whatsapp,identidad,plataforma}.md` | un agente nuevo entiende el área sin abrir el historial |
| T6 | **Galería en un solo script.** `scripts/capturar-etapa.mjs` recibe la lista de casos de una etapa en un archivo de datos, sin tests propios. Las PNG siguen en `docs/design/capturas/etapa-<n>/`. Los scripts de etapas ya cerradas se borran. | front `scripts/` y `docs/rules/tests.md` | la galería de la 8b se arma con ese script |
| T7 | **Detalles.** Los punteros de `Modules/Assistant` (Application y Api) pasan a una línea. `quitar-whatsapp.md` y `quitar-asistente.md` quedan sin conteos fijos. `shared/modules/` se suma al árbol del front. Se borran `useEffectiveTimeZone` y `resolveEffectiveTimeZone`, que solo usan sus tests. | los nombrados | build y tests del alcance en verde |

## Cómo se cierra

Una sola corrida al final: tests del arnés (`node --test "scripts/arneses/**/*.test.mjs"`), `HarnessTests` y los tests de arquitectura del back, `npx vitest run src/test/harness.test.ts` y `npm test` del front, builds de los dos repos y `realinear-docs --detectar` en 0/0/0 en los dos. Arriba de este plan queda solo una tabla corta con el resultado y las palabras antes y después de cada documento.

## Texto para el chat de Codex

```
Leé AGENTS.md del back y del front y docs/plans/2026-10-11-limpieza-de-documentacion.md (back). Hacé T1 a T7 en ese orden, en los dos repos. No cambies la conducta del producto ni una regla de código: solo la estructura y el largo de la documentación, y los tests del arnés que la verifican. Si una regla queda solo en un texto que borrás, movela a su ficha. Las decisiones técnicas son tuyas: anotalas en una línea del plan. Commits con pathspec, sin push. Al final, la corrida de «Cómo se cierra» una sola vez y la tabla de resultados arriba del plan.
```
