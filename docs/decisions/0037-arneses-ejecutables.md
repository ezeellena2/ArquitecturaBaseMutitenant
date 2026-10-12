# ADR 0037 · Arneses ejecutables

Estado: aceptada el 2026-10-02. La [fila 0037 del índice](README.md) resume esta decisión; el [plan](0037-arneses-ejecutables.md) la detalla y registra lo que se decidió al implementarla.

## Contexto

El [arnés](../architecture/arnes.md) (fichas, punteros por carpeta y `HarnessTests`) frena el build cuando un enlace, un puntero o un «Lo verifica» se rompe, pero no arregla nada y no mira el resto de la documentación (features, guías, `arbol.md`, índices, catálogos). Con el código cambiando todos los días, la documentación se desalinea más rápido de lo que alguien la corrige a mano, y pedirle a una sesión grande que la revise gasta tokens en trabajo mecánico.

## Decisión

Un **arnés ejecutable** (`scripts/arneses/<nombre>/`) es una rutina que se corre a mano o programada: detecta con un script (0 tokens), arregla solo lo mecánico, le pasa a un modelo barato solo lo que pide criterio y anota para la persona lo que es una decisión de producto o un cambio de código. El primero es `realinear-docs`. Reglas:

- **D1** El código manda en lo que existe (rutas, nombres de tipos, carpetas, permisos, códigos de error y rutas HTTP): si el documento no coincide, se corrige el documento.
- **D2** La documentación manda en la intención: una regla de una ficha que el código no cumple es un bug o una decisión pendiente; el arnés no cambia código para cumplirla, la anota como `humano`.
- **D3** Lo planeado no es un error: lo marcado `(E#)` o `[E#]` de una etapa que no cerró (`ClosedStages` de cada repo) se ignora. Un rango `E3–E5` cuenta como cerrado cuando cerraron todas.
- **D4** Puede borrar solo en documentación y punteros (líneas, filas y menciones a cosas que ya no existen, y `AGENTS.md`/`CLAUDE.md` de carpetas que desaparecieron). Puede crear punteros, filas de índice y líneas de `arbol.md`.
- **D5** Nunca toca `*.cs`, `*.ts(x)`, tests, `*.resx`, migraciones, contratos generados ni secretos.
- **D6** La historia no se reescribe: `docs/plans/`, `docs/reviews/`, `docs/history/`, el cuerpo de los ADR, `docs/backlog.md` y `docs/design/` quedan fuera. Del ADR solo se mira el índice.
- **D7** Nunca inventa: si el dato no está en el código ni en otro documento, el hallazgo pasa a `humano`.
- **D8** Trabaja aparte: cada corrida crea un `git worktree` en una rama `arnes/<arnés>/<AAAAMMDD-HHmm>`, verifica, commitea ahí y no hace push. Fusionar es de la persona (o `--fusionar`, solo `--ff-only`).
- **D9** Tope de gasto: cada arnés declara modelo, esfuerzo, presupuesto y máximo de hallazgos por corrida. Sin hallazgos para el modelo, el modelo no se llama.
- **D10** Dos motores, Claude Code y Codex: los mismos detectores, el mismo informe, las mismas instrucciones y la misma guardia. Lo que un motor no puede limitar por sí mismo lo controla el script después: la guardia revierte lo tocado fuera de `permisos`.

## Alternativas descartadas

- Pedirle la revisión a la sesión grande cada vez: gasta tokens caros en trabajo mecánico y no es repetible.
- Un solo motor: ataría el arnés a una suscripción y a un CLI.
- Que el modelo explore el repo: cuesta más y es menos confiable que darle el hallazgo con su contexto recortado.
- Dejar que el modelo edite `ignorados.json`: lo escribe el script a partir del resultado, así también funciona sobre un repo que no es el del arnés.
- Hacer push o fusionar automáticamente: una corrida programada no debe tocar `main` sin que nadie la mire.

## Consecuencias

La documentación se realinea con un costo acotado (solo los hallazgos `modelo`, con tope por corrida) y cada corrida deja una rama revisable. Los detectores son código que hay que mantener con sus fixtures; cada falso positivo se paga en tokens, así que se calibran sobre el repo real con `--detectar`. Un arnés nuevo es una carpeta ([receta](../guides/agregar-un-arnes.md)).
