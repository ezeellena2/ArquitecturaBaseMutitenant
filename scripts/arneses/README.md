# Arneses ejecutables

Un **arnés ejecutable** es una rutina que se corre a mano o programada: **detecta** con un script (0 tokens), **arregla solo** lo mecánico (0 tokens), le pasa a un modelo barato **solo** lo que pide criterio y **deja anotado para vos** lo que es una decisión de producto o un cambio de código. Termina con un commit en una rama aparte (`arnes/<arnés>/<AAAAMMDD-HHmm>`), sin push. Las decisiones están en el [ADR 0037](../../docs/decisions/0037-arneses-ejecutables.md); el plan, en [docs/plans/2026-10-02-arneses-ejecutables.md](../../docs/decisions/0037-arneses-ejecutables.md).

No es lo mismo que [el arnés estático](../../docs/architecture/arnes.md) (fichas, punteros y `HarnessTests`), que **frena** el build cuando algo se rompe pero no arregla nada.

## Catálogo

| Arnés | Qué hace | Repos |
|---|---|---|
| `realinear-docs` | alinea docs y punteros con el código: corrige rutas, símbolos, `arbol.md`, punteros e índices; crea y borra lo que quedó viejo | back y front |

## Cómo se corre

```bash
node scripts/arneses/arnes.mjs                                  # lista los arneses
node scripts/arneses/arnes.mjs realinear-docs --detectar        # 0 tokens: detecta y escribe el informe (sale 0 si no hay nada, 2 si hay, 1 si falló)
node scripts/arneses/arnes.mjs realinear-docs                   # corrida completa con el motor del manifiesto
node scripts/arneses/arnes.mjs realinear-docs --motor codex     # con Codex
node scripts/arneses/arnes.mjs realinear-docs --repo front --sin-modelo
node scripts/arneses/arnes.mjs --instalar                       # copia las skills de .agents/skills/ a .claude/skills/
```

| Opción | Qué hace |
|---|---|
| `--detectar` | solo detecta; sin worktree, sin modelo |
| `--sin-modelo` | arregla lo mecánico y no llama al modelo |
| `--motor claude\|codex` · `--modelo <nombre>` · `--presupuesto <USD>` | pisan lo que dice `arnes.json` |
| `--repo back\|front` | un solo repo |
| `--fusionar` | si todo verificó, avanza la rama actual con `git merge --ff-only` |
| `--sin-aislar` | no evita la config del usuario (para medir cuántos tokens cuesta cargarla) |

Los tests (`node --test "scripts/arneses/**/*.test.mjs"`) usan solo `realinear-docs/fixtures/` y ejecutables falsos de `claude` y `codex`: no leen el repo real ni llaman a un modelo.

### Una corrida

1. Crea un worktree y la rama, fuera del repo: `../.arneses/<repo>-<arnés>-<fecha>`.
2. Detecta **dentro** del worktree (lo commiteado, no lo que tenés a medio hacer).
3. Aplica los hallazgos `auto` (parches con verificación del texto y de los permisos).
4. Si quedan hallazgos `modelo` (hasta `maxHallazgos`), llama al motor con la tarea en `.local/arneses/<arnés>/<fecha>.modelo.json`.
5. **Guardia:** todo archivo tocado fuera de `permisos` se revierte y la corrida falla.
6. Vuelve a detectar: no puede quedar ningún `auto`.
7. Corre `verificacion` del repo. Si algo falla, no commitea y deja el worktree para mirar.
8. Commitea con el mensaje del manifiesto y escribe el informe en `.local/arneses/<arnés>/<fecha>-<repo>.md`.

## Motores

| | Claude Code | Codex |
|---|---|---|
| Llamada | `claude -p` con `--append-system-prompt-file`, solo `Read Edit Write Glob Grep`, `--max-budget-usd` | `codex exec` con las instrucciones por stdin, `--output-schema`, `-o` |
| Config del usuario | `--setting-sources project --strict-mcp-config` | `--ignore-user-config` y, en Windows, `-c windows.sandbox="elevated"` |
| Tope | presupuesto en USD | `minutosMaximos` |
| Consumo | tokens y costo | tokens (costo n/d) |

Para apuntar a otro ejecutable: `ARNES_CLAUDE_BIN` o `ARNES_CODEX_BIN` (una ruta). El `codex` de npm puede ser más viejo que el de la app de Codex y no conocer el modelo del manifiesto.

## Cuándo corre

| Momento | Cómo |
|---|---|
| cuando quieras | `node scripts/arneses/arnes.mjs <arnés>`, o `/arnes <arnés>` en Claude Code o en Codex |
| al cerrar cada etapa | paso de la puerta ([arnes.md §6](../../docs/architecture/arnes.md#6-mantenimiento)) |
| programado | `pwsh scripts/arneses/programar.ps1 -Arnes realinear-docs -Motor claude -Hora 03:00 -Dias Lun,Jue` (con `-Consulta` o `-Baja` para ver o quitar la tarea) |
| en cada push | CI: `node --test` y `--detectar` informativo |

## Un arnés nuevo

Copiá `_plantilla/` y seguí [docs/guides/agregar-un-arnes.md](../../docs/guides/agregar-un-arnes.md).
