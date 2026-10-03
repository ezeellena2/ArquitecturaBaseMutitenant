# Arneses ejecutables: realinear la documentación y el código

> **Estado:** propuesta para revisar. Nada de esto existe todavía.
> **Primer arnés:** `realinear-docs`. La estructura queda lista para los que vengan.

## 1. Qué es y qué no es

- **El arnés** ([arnes.md](../architecture/arnes.md)) es la guía estática: fichas, punteros por carpeta y `HarnessTests`. Hoy **frena** el build cuando un enlace, un puntero o un "Lo verifica" se rompe, pero no arregla nada y no mira el resto de la documentación (features, guías, `arbol.md`, índices, catálogos).
- **Un arnés ejecutable** es una rutina que se corre a mano o programada: **detecta** con un script (0 tokens), **arregla solo** lo mecánico (0 tokens), le pasa a un modelo barato **solo** lo que pide criterio y **deja anotado para vos** lo que es una decisión de producto o un cambio de código. Termina con un commit en una rama aparte.

## 2. Decisiones (van al ADR 0037)

| # | Decisión |
|---|---|
| D1 | **El código manda en lo que existe:** rutas, nombres de tipos, carpetas, permisos, códigos de error y rutas HTTP. Si el doc no coincide, se corrige el doc. |
| D2 | **La documentación manda en la intención:** una regla de una ficha que el código no cumple es un bug o una decisión pendiente. El arnés **no** cambia código para cumplirla: la anota como `humano`. |
| D3 | **Lo planeado no es error:** lo marcado `(E#)` o `[E#]` de una etapa que no cerró (lista `ClosedStages` de cada repo) se ignora. Un rango `E3–E5` cuenta como cerrado cuando cerraron todas. |
| D4 | **Puede borrar** solo en documentación y punteros: líneas, filas y menciones a cosas que ya no existen, y `AGENTS.md`/`CLAUDE.md` de carpetas que desaparecieron. **Puede crear** punteros, filas de índice y líneas de `arbol.md`. |
| D5 | **Nunca toca:** `*.cs`, `*.ts(x)`, tests, `*.resx`, migraciones, contratos generados (`docs/contracts/`) ni secretos. |
| D6 | **La historia no se reescribe:** `docs/plans/`, `docs/reviews/`, `docs/history/`, el cuerpo de los ADR (`docs/decisions/0*.md`), `docs/backlog.md` y `docs/design/` quedan fuera. Del ADR solo se mira el índice. |
| D7 | **Nunca inventa:** si el dato no está en el código ni en otro doc, el hallazgo pasa a `humano`. |
| D8 | **Trabaja aparte:** cada corrida crea un `git worktree` en una rama `arnes/<arnés>/<AAAAMMDD-HHmm>`, verifica, commitea ahí y **no** hace push. Fusionar es tuyo (o `--fusionar`, solo `--ff-only`). |
| D9 | **Tope de gasto:** cada arnés declara modelo, esfuerzo, presupuesto en USD y máximo de hallazgos por corrida. Sin hallazgos para el modelo, el modelo no se llama. |
| D10 | **Dos motores, Claude Code y Codex:** los mismos detectores, el mismo informe, las mismas instrucciones y la misma guardia. El motor se elige en `arnes.json` o con `--motor`. Lo que un motor no puede limitar por sí mismo lo controla el script después (§7). |

## 3. Árbol

### Archivos nuevos

```
scripts/arneses/
├── AGENTS.md                       puntero: qué va acá, qué leer, qué copiar (≤ 8 líneas)
├── CLAUDE.md                       @AGENTS.md
├── README.md                       qué es un arnés ejecutable, cómo se corre, catálogo de arneses, cómo se programa
├── arnes.mjs                       CLI único: lista, detecta, repara, llama al modelo, verifica y commitea
├── arnes.test.mjs                  tests del CLI con fixtures (sin red ni modelo)
├── programar.ps1                   alta, baja y consulta de la tarea programada de Windows
├── comun/                          piezas compartidas por todos los arneses
│   ├── repos.mjs                   ubica back y front; lee ClosedStages de cada uno
│   ├── archivos.mjs                índice del árbol de archivos (ignora bin, obj, node_modules, dist, .git, .local)
│   ├── markdown.mjs                enlaces, anclas, rutas entre backticks, tablas, bloques de árbol, marcas (E#)/[E#]
│   ├── simbolos.mjs                índice de tipos y miembros: C# por regex; TS por export
│   ├── hallazgos.mjs               forma del hallazgo, id estable, orden, filtro por ignorados.json
│   ├── parches.mjs                 aplica parches genéricos: reemplazar, borrar-línea, insertar-línea, crear-archivo
│   ├── worktree.mjs                crea y limpia el worktree y la rama de la corrida
│   ├── motores/                    misma interfaz: ejecutar({ instrucciones, tarea, modelo, esfuerzo, presupuesto, cwd })
│   │   ├── claude.mjs              `claude -p` (§7)
│   │   └── codex.mjs               `codex exec` (§7)
│   ├── guardia.mjs                 después del modelo: todo archivo tocado fuera de `permisos` se revierte y la corrida falla
│   ├── informe.mjs                 escribe .local/arneses/<arnés>/<fecha>.json y .md
│   └── comun.test.mjs
├── realinear-docs/                 el primer arnés
│   ├── arnes.json                  manifiesto (§4)
│   ├── ignorados.json              hallazgos descartados con motivo; versionado para que se revise en el diff
│   ├── instrucciones.md            instrucciones del modelo, iguales para los dos motores
│   ├── resultado.schema.json       forma de la respuesta final; Codex la exige con --output-schema y el script la valida en los dos
│   ├── detectores/
│   │   ├── referencias.mjs         enlaces, anclas y rutas entre backticks que no existen
│   │   ├── simbolos.mjs            tipos y miembros nombrados en los docs que ya no existen
│   │   ├── arbol.mjs               docs/architecture/arbol.md contra el árbol real
│   │   ├── punteros.mjs            AGENTS.md/CLAUDE.md contra el mapa de arnes.md §3 y las carpetas de área
│   │   ├── indices.mjs             rules/README, decisions/README y tabla "si vas a…" contra los archivos
│   │   └── catalogos.mjs           permisos, códigos de error y rutas HTTP de docs/features contra el código y openapi.json
│   ├── realinear-docs.test.mjs     un test por detector, con fixtures
│   └── fixtures/                   mini repos falsos (back y front) con cada tipo de desvío
└── _plantilla/                     se copia para crear un arnés nuevo; el CLI ignora carpetas que empiezan con _
    ├── arnes.json
    ├── ignorados.json
    ├── instrucciones.md
    ├── detectores/ejemplo.mjs
    └── ejemplo.test.mjs

.agents/skills/arnes/SKILL.md       skill /arnes <nombre> (versionada; Codex la lee de acá): corre el CLI y muestra el resumen
docs/guides/agregar-un-arnes.md     receta paso a paso para un arnés nuevo
```

**Claude Code:** `.claude/` está en `.gitignore`, así que la skill no puede versionarse ahí. `node scripts/arneses/arnes.mjs --instalar` copia `.agents/skills/arnes/` a `.claude/skills/arnes/` en cada máquina. Como en `find-skills`, la fuente está en `.agents/skills/`; acá va una copia y no un enlace, porque el repo tiene `core.symlinks=false`. Las instrucciones del modelo no viven en la skill: viven en `instrucciones.md`, escritas una sola vez.

### Archivos que cambian

| Archivo | Cambio |
|---|---|
| `AGENTS.md` (raíz) | fila "correr o crear un arnés ejecutable → `scripts/arneses/README.md`" y el comando en **Comandos** |
| `docs/architecture/arnes.md` | fila `scripts/arneses/` en el mapa (§3); en §6, "al cerrar cada etapa se corre `realinear-docs`" |
| `docs/architecture/arbol.md` | rama `scripts/arneses/`, `.agents/skills/arnes/` y la guía |
| `docs/decisions/README.md` | fila 0037 con D1 a D9 |
| `.github/workflows/ci.yml` | paso `node --test scripts/arneses` (0 tokens) y paso informativo `--detectar` con `continue-on-error` |

Los informes y el estado viven en `.local/arneses/` (ya está en `.gitignore`). Los worktrees, fuera del repo: `../.arneses/<repo>-<arnés>-<AAAAMMDD-HHmm>`, para que `HarnessTests` no recorra copias.

Sin dependencias npm: solo módulos de Node (`node:fs`, `node:path`, `node:child_process`, `node:test`), así no hace falta `npm ci`.

## 4. Contratos

### `arnes.json`

```json
{
  "nombre": "realinear-docs",
  "descripcion": "Alinea docs y punteros con el código: corrige, crea y borra lo que quedó viejo.",
  "repos": ["back", "front"],
  "detectores": ["referencias", "simbolos", "arbol", "punteros", "indices", "catalogos"],
  "excluir": ["docs/plans/**", "docs/reviews/**", "docs/history/**", "docs/decisions/0*.md",
              "docs/backlog.md", "docs/design/**", "docs/contracts/**", ".claude/**", ".agents/**"],
  "permisos": {
    "puedeCrear": ["**/AGENTS.md", "**/CLAUDE.md", "docs/**/*.md"],
    "puedeBorrar": ["**/AGENTS.md", "**/CLAUDE.md"],
    "nuncaToca": ["**/*.cs", "**/*.ts", "**/*.tsx", "**/*.resx", "**/Migrations/**", "tests/**", "docs/contracts/**"]
  },
  "modelo": {
    "motor": "claude",
    "maxHallazgos": 40,
    "claude": { "nombre": "sonnet", "esfuerzo": "low", "presupuestoUsd": 1.5 },
    "codex":  { "nombre": "gpt-6.1-sol", "esfuerzo": "low", "minutosMaximos": 10 }
  },
  "verificacion": {
    "back": ["node --test scripts/arneses",
             "dotnet test --project tests/ArquitecturaBaseMultitenant.ArchitectureTests/ArquitecturaBaseMultitenant.ArchitectureTests.csproj -- --filter-class \"ArquitecturaBaseMultitenant.ArchitectureTests.HarnessTests\""],
    "front": ["npx vitest run src/test/harness.test.ts"]
  },
  "commit": "docs: realinear documentación con el código"
}
```

### Hallazgo

```json
{
  "id": "referencias|back|docs/features/roles.md|ruta-inexistente|Services/Roles/RoleSrevice.cs",
  "detector": "referencias", "repo": "back",
  "archivo": "docs/features/roles.md", "linea": 42, "tipo": "ruta-inexistente",
  "real": "Services/Roles/RoleSrevice.cs no existe",
  "esperado": "src/ArquitecturaBaseMultitenant.Application/Services/Roles/RoleService.cs",
  "accion": "auto | modelo | humano",
  "parche": { "tipo": "reemplazar", "archivo": "docs/features/roles.md", "linea": 42, "de": "…", "a": "…" },
  "contexto": "líneas 37 a 47 del archivo, copiadas"
}
```

- `auto`: trae `parche` y lo aplica `comun/parches.mjs`. Solo cuando hay **un** candidato inequívoco.
- `modelo`: trae `contexto` (±5 líneas) y los candidatos; el modelo no explora por su cuenta.
- `humano`: no se toca; va al informe con la pregunta concreta.
- El `id` es estable entre corridas; `ignorados.json` guarda `{ id, motivo, fecha }` y esos ids no se vuelven a reportar.

### Detector

```js
export const nombre = 'referencias';
/** @returns {Promise<Hallazgo[]>} */
export async function detectar(ctx) { /* ctx: { repo, archivos, markdown, simbolos, etapasCerradas, manifiesto } */ }
```

Un detector solo lee. Nunca escribe ni llama al modelo.

### `instrucciones.md` (lo que recibe el modelo)

1. Recibís la ruta de un JSON con hallazgos `modelo`. Trabajás uno por uno.
2. Leés solo el `contexto` y, si hace falta, el archivo de código citado en `esperado`. No abrís docs enteros ni explorás el repo.
3. Para cada hallazgo hacés una de cuatro cosas: **corregir** (edición mínima, mismo estilo, voseo), **borrar** la mención vieja, **descartar** (agregás `{ id, motivo, fecha }` a `ignorados.json`) o **escalar** (`humano`, con la pregunta concreta).
4. Escalás si la corrección cambia qué ve o puede hacer un usuario, si el código contradice una regla escrita o si el dato no está en ningún lado (D2, D7).
5. Editás solo los archivos de los hallazgos y lo que permite `permisos`. No corrés builds, tests ni git: eso lo hace el script.
6. Terminás con un JSON `[{ id, resultado: corregido|borrado|creado|descartado|humano, nota }]`.

### `.agents/skills/arnes/SKILL.md` (y su copia en `.claude/skills/arnes/`)

Corre `node scripts/arneses/arnes.mjs <args>` en segundo plano, lee **solo** el `.md` del informe y muestra: rama, contadores por acción, costo o tokens y la lista `humano`. No abre los archivos tocados ni rehace el trabajo. Así la sesión grande no gasta: el trabajo ya lo hizo el modelo barato del arnés. Desde Codex, la sesión puede pasar `--motor codex`.

## 5. Detectores de `realinear-docs`

| Detector | Compara | Manda | `auto` | `modelo` | `humano` |
|---|---|---|---|---|---|
| referencias | enlaces `[x](ruta#ancla)` y rutas entre backticks (con la convención de capa de arnes.md §3: `Application/X` = `src/ArquitecturaBaseMultitenant.Application/X`) | código | un único archivo con ese nombre en otra ruta, o un `git log --follow` que muestra el renombre | varios candidatos, o borrado sin reemplazo | — |
| simbolos | identificadores entre backticks con forma del repo (`*Service`, `*Repository`, `*Reader`, `*Controller`, `*Errors`, `*Tests`, `*Policy`, `*Request`, `*Row`, `I*`…) | código | renombre único por `git log -S` | candidatos parecidos | — |
| arbol | secciones de `arbol.md` cuyo título es una ruta, contra los archivos | código | borrar líneas de archivos eliminados (`git log --diff-filter=D`); insertar la línea de un archivo nuevo con la sangría correcta | escribir la descripción corta (≤ 10 palabras) de cada línea nueva | archivo de etapa cerrada que nunca existió |
| punteros | carpetas del mapa §3 y carpetas de área con `docs/features/<área>.md` | código para existir, docs para el texto | crear `AGENTS.md` desde la fila del mapa y `CLAUDE.md` = `@AGENTS.md`; borrar punteros de carpetas que ya no existen | `AGENTS.md` en una carpeta que no está en el mapa (¿falta la fila?) | — |
| indices | `docs/rules/README.md`, `docs/decisions/README.md`, tabla "si vas a…" del `AGENTS.md` raíz | archivos | borrar filas de archivos que no existen | escribir la fila de una ficha nueva | ficha sin fila en "si vas a…" (puede ser a propósito) |
| catalogos (back) | permisos de `Domain/Authorization/*Permissions.cs`, códigos `Area.Entidad.Motivo` de `*Errors.cs` y rutas de `openapi.json`, contra `docs/features/*.md` | código | — | corregir un permiso, código o ruta mencionado que cambió; sumar una fila a una tabla de rutas que ya existe | algo del código que ninguna feature documenta |

Lo que ya frena `HarnessTests` (enlaces de fichas y punteros, secciones de ficha, "Lo verifica") también se detecta acá, pero para **arreglarlo**; el test sigue siendo la red final.

## 6. Una corrida

```
node scripts/arneses/arnes.mjs realinear-docs [--repo back|front] [--detectar] [--sin-modelo]
                                             [--motor claude|codex] [--modelo <nombre>] [--presupuesto 1.5] [--fusionar]
node scripts/arneses/arnes.mjs --instalar      copia la skill a .claude/skills/
```

1. Sin argumentos: lista los arneses (lee `*/arnes.json`).
2. `--detectar`: corre los detectores en la carpeta actual, escribe el informe y termina. Sale con 0 si no hay nada, 2 si hay hallazgos, 1 si falló. **0 tokens**, sin worktree.
3. Corrida completa:
   1. crea el worktree y la rama;
   2. detecta **dentro** del worktree (lo commiteado, no lo que tenés a medio hacer);
   3. aplica los `auto`;
   4. si hay `modelo` (hasta `maxHallazgos`), llama al motor (§7); si no, sigue sin modelo;
   5. `guardia.mjs`: `git diff --name-only` en el worktree; lo que esté fuera de `permisos` o en `nuncaToca` se revierte y la corrida falla;
   6. vuelve a detectar: no puede quedar ningún `auto` ni `modelo` de los que se mandaron;
   7. corre `verificacion` del repo;
   8. si todo pasó, commitea con el mensaje del manifiesto y el resumen en el cuerpo; si falló, no commitea y deja el worktree para mirar;
   9. escribe el informe: rama, motor, contadores, tokens, costo cuando el motor lo da y la lista `humano`.
4. `--fusionar`: si verificó, `git merge --ff-only` de la rama en la rama actual del repo y borra el worktree.

## 7. Modelo y tokens

**Sí, se eligen el motor y el modelo por arnés** en `arnes.json` → `modelo`, y se pueden pisar con `--motor` y `--modelo`. Recomendación:

| Trabajo | Modelo |
|---|---|
| detectar, arreglos mecánicos, guardia, verificar, commitear | ninguno: script |
| hallazgos `modelo` con Claude | `sonnet`, esfuerzo `low` (`haiku` si alcanza: probalo con el mismo informe) |
| hallazgos `modelo` con Codex | el modelo de tu config (`gpt-6.1-sol`) pero con esfuerzo `low`, no el `xhigh` que tenés por defecto; si tu cuenta tiene uno más barato, va en `arnes.json` |
| construir el arnés (una vez) | `sonnet` o Codex con esfuerzo `medium`; subí solo si se traba en el parser de `arbol.md` |

### Claude Code (`comun/motores/claude.mjs`)

```
claude -p "<tarea: procesá .local/arneses/realinear-docs/<fecha>.modelo.json>"
  --model sonnet --effort low --max-budget-usd 1.5
  --append-system-prompt-file scripts/arneses/realinear-docs/instrucciones.md
  --allowedTools "Read Edit Write Glob Grep" --disallowedTools "Bash"
  --permission-mode acceptEdits
  --setting-sources project --strict-mcp-config
  --no-session-persistence --output-format json
```

### Codex (`comun/motores/codex.mjs`)

```
codex exec -C <worktree> -m gpt-6.1-sol -c model_reasoning_effort="low"
  -s workspace-write --ignore-user-config --ephemeral
  --output-schema scripts/arneses/realinear-docs/resultado.schema.json
  -o .local/arneses/realinear-docs/<fecha>.resultado.json --json
  -                      ← por stdin: instrucciones.md + la tarea
```

### Diferencias que absorbe el script

| | Claude Code | Codex | Qué hace el script |
|---|---|---|---|
| instrucciones | `--append-system-prompt-file` | no tiene un flag equivalente | Codex las recibe al principio del prompt |
| config del usuario (plugins, hooks, MCP) | `--setting-sources project --strict-mcp-config` | `--ignore-user-config` (la autenticación sigue andando) | — |
| herramientas | solo `Read Edit Write Glob Grep`, sin `Bash` | siempre tiene shell; `workspace-write` limita la escritura al worktree y deja la red apagada | `guardia.mjs` revierte lo que quede fuera de `permisos`, en los dos motores |
| tope | `--max-budget-usd` | no tiene | corta el proceso a los `minutosMaximos`; `maxHallazgos` vale para los dos |
| respuesta final | `--output-format json` | `--output-schema` + `-o` | valida contra `resultado.schema.json` en los dos |
| consumo en el informe | tokens y `total_cost_usd` | tokens, de los eventos de `--json` | anota lo que haya; costo `null` en Codex |
| `AGENTS.md` | lo carga por `CLAUDE.md` = `@AGENTS.md` | lo carga solo | — |

**A verificar en la fase 4, no lo doy por hecho:** que en Windows `workspace-write` funcione con `--ignore-user-config`, y qué campo de los eventos `--json` trae el uso de tokens. Si el sandbox necesita algo de tu config, se usa un perfil mínimo propio (`-p arneses`) y se anota en «Decisiones tomadas».

Por qué gasta poco, con cualquiera de los dos:
- el modelo ve **solo** los hallazgos que necesitan criterio, con su contexto recortado; nunca el repo entero;
- sin hallazgos `modelo`, no hay llamada;
- no se carga tu config de usuario: en esta máquina son los plugins de Claude (Vercel, engram, superpowers…) y los MCP de Codex (engram, meta, whatsapp, node_repl). **Medilo**: corré una vez con y otra sin esos flags y compará los tokens de entrada;
- las instrucciones prohíben builds, tests, git y exploración; con Claude además no hay `Bash`;
- `ignorados.json` evita pagar dos veces por el mismo hallazgo;
- `--max-budget-usd` (Claude) y `minutosMaximos` (Codex) cortan la corrida si se pasa.

## 8. Cuándo corre

| Momento | Cómo | Tokens |
|---|---|---|
| cuando quieras | `node scripts/arneses/arnes.mjs realinear-docs [--motor codex]`, o `/arnes realinear-docs` en Claude Code o en Codex | solo los `modelo` |
| al cerrar cada etapa | paso de la puerta (arnes.md §6) | solo los `modelo` |
| programado | `pwsh scripts/arneses/programar.ps1 -Arnes realinear-docs -Motor claude -Hora 03:00 -Dias Lun,Jue` (Programador de tareas de Windows; corre con tu usuario y deja la rama) | solo los `modelo` |
| en cada push | CI: `node --test scripts/arneses` y `--detectar` informativo | 0 |

## 9. Un arnés nuevo

Copiás `_plantilla/` a `scripts/arneses/<nombre>/`, escribís los detectores con sus tests, completás `arnes.json` e `instrucciones.md` y sumás la fila al catálogo de `scripts/arneses/README.md`. El CLI, el worktree, el motor, el informe y los parches no se tocan. Detalle en `docs/guides/agregar-un-arnes.md`.

Ideas que esta estructura ya soporta: `revisar-textos` (claves resx sin uso o sin traducción), `revisar-lienzo` (pantallas del lienzo contra rutas del front), `revisar-adr` (decisiones sin implementar).

## 10. Fases y aceptación

Cada fase: TDD con `node --test` (rojo → verde), un commit, y `node --test scripts/arneses` en verde.

| Fase | Alcance | Se acepta cuando |
|---|---|---|
| 1. Núcleo | `comun/*`, `arnes.mjs` (lista y `--detectar`), `_plantilla/`, fixtures | `--detectar` sobre los fixtures devuelve exactamente los hallazgos esperados |
| 2. Detectores simples | referencias, punteros, indices + `parches.mjs` | sobre el repo real, `--detectar` corre en < 10 s; cada `auto` del fixture queda arreglado |
| 3. Detectores difíciles | arbol, simbolos, catalogos | el parser de `arbol.md` reconstruye todas las rutas de las secciones con ruta, sin falsos positivos en los fixtures |
| 4. Modelo y corrida | `worktree.mjs`, `motores/claude.mjs`, `motores/codex.mjs`, `guardia.mjs`, `instrucciones.md`, `resultado.schema.json`, verificación, commit, `--fusionar` | sobre el **mismo** informe, una corrida con Claude y otra con Codex terminan con commit en su rama, `HarnessTests` en verde y el consumo anotado; un test de `guardia.mjs` revierte un archivo tocado fuera de `permisos` |
| 5. Entradas y docs | skill en `.agents/skills/arnes/`, `--instalar`, `programar.ps1`, guía, README, ADR 0037, filas en `AGENTS.md`, `arnes.md` y `arbol.md`, CI | `HarnessTests` en verde con la carpeta nueva; `/arnes realinear-docs` muestra el resumen en Claude Code y en Codex; la tarea programada se da de alta y de baja |
| 6. Front | `--repo front` con referencias, simbolos (TS), arbol, punteros e indices | una corrida sobre el front termina con commit y `harness.test.ts` en verde |

## 11. Decisiones tomadas

> Anotadas durante la implementación (2026-10-02). Ninguna contradice D1 a D10.

1. **`node --test scripts/arneses` no recorre carpetas en Node 24:** desde Node 22 los argumentos de `--test` son globs y una carpeta se toma como archivo. El comando que corre en CI y localmente es `node --test "scripts/arneses/**/*.test.mjs"` (entre comillas, para que lo expanda Node y no el shell). `arnes.json` → `verificacion` usa esa forma.
2. **Anclas como GitHub y como `HarnessTests`:** cada espacio es un guion (sin colapsar), se conservan `_` y `-`, y no se quitan los `<...>`, porque dentro de un título van en código (`## 2. Formato de una ficha (\`docs/rules/<tema>.md\`)` da `2-formato-de-una-ficha-docsrulestemamd`).
3. **Convención de capa generalizada:** `Application/X` ya valía; ahora todo proyecto de `src/` y `tests/` se abrevia por lo que sigue al primer punto (`Api.IntegrationTests/X`, `AppHost/X`).
4. **Repos hermanos:** una ruta que no está en este repo se busca por sufijo en el otro del par (back/front). Una ruta que empieza con otro repo hermano (`ArquitecturaBase/...`, `../ArquitecturaBase/...`) no se juzga: no es código de este par.
5. **Valores que no se tratan como ruta:** extensiones sueltas (`.resx`, `.test.ts`), imports (`@AGENTS.md`), rutas con `\` o `%`, `wwwroot`, `bin`, `obj`, `.local`, y plantillas (`NNNN`, `AAAA`, `IXService`).
6. **`auto` solo con un candidato que no sea un nombre genérico** (`README.md`, `AGENTS.md`, `CLAUDE.md`, `index.*`, `package.json`): el nombre único no prueba el renombre. No se usa `git log --follow` (alcanza con el nombre; si hace falta, va a `modelo`).
7. **Una sola respuesta por línea:** si un detector borra una fila (`borrar-linea`, auto), los hallazgos de otros detectores sobre esa línea se descartan (el auto de `indices` gana al `modelo` de `referencias`).
8. **Carpetas de área:** un `AGENTS.md` en una carpeta hija de una del mapa (`Services/Auth`) es una carpeta de área (arnes.md §3) y no se pregunta si falta su fila. `Domain/<Módulo>/` no se expande: lo resuelve `HarnessTests` con la lista de módulos.
9. **Tipo de parche extra `escribir-archivo`** (reescribe un archivo existente, p. ej. un `CLAUDE.md` que no es `@AGENTS.md`); se suma a los cuatro del plan. Los parches preservan CRLF/LF.
10. **Fixtures sin repo real:** los tests de los detectores usan solo `realinear-docs/fixtures/` (el índice de archivos ignora cualquier carpeta llamada `fixtures`, así el repo real no los cuenta).

11. **`arbol.md`:** solo se leen las secciones cuyo título es una ruta (`Raíz`, `src/<Proyecto>`, `tests/`); las "Piezas…" con rutas planas quedan afuera. Una carpeta con nombre de plantilla (`NNNN-<titulo>.md`) lista un patrón y no se le agregan archivos; los de `excluir` tampoco.
12. **Archivos nuevos en `arbol.md`:** la línea se inserta (auto) solo si la descripción sale sin modelo: el primer `<summary>` del `.cs` (primera oración, hasta 10 palabras) o "tests de X". Si no hay, el hallazgo es `modelo` y trae la línea esperada con `<describir>`. Una línea que ya trae `<describir>` es un `modelo` aparte (`linea-sin-descripcion`). La línea se inserta antes de la última hermana, así no hay que reindentar el resto.
13. **`simbolos`:** `auto` solo con un candidato único a distancia ≤ 2, de la misma clase (interfaz o no) y con el mismo sufijo. Los miembros se verifican solo en tipos con forma del repo. Un nombre de archivo (`Email.cs`) o de proyecto (`ArchitectureTests`) no es un tipo ni un miembro.
14. **`catalogos`:** lo que el código define y ninguna feature documenta se pregunta por grupo (un permiso, un código o una ruta por grupo), no por elemento. El historial se lee con una sola llamada a `git log --diff-filter=D`.

15. **La respuesta del modelo es un objeto** `{ "resultados": [{ id, resultado, nota }] }` y no un arreglo suelto: Codex exige un objeto en la raíz con `--output-schema`. `resultado.schema.json` y su validador mínimo (`comun/resultado.mjs`) valen para los dos motores.
16. **`ignorados.json` lo escribe el script**, no el modelo: un `descartado` del resultado se anota con su motivo. Así también funciona sobre el front, cuyo worktree no contiene la carpeta del arnés; en ese caso queda modificado el `ignorados.json` del repo del arnés, sin commitear.
17. **Lo que el modelo no resolvió** (hallazgos enviados que siguen detectándose) no hace fallar la corrida: pasa a la lista `humano` con la nota «el modelo no lo resolvió». Falla la corrida si queda un `auto` (es un bug del script), si la guardia revirtió algo, si el motor falló o si la respuesta no cumple el schema.
18. **Los repos hermanos se buscan desde el repo real, no desde el worktree** (`raicesReales`): el worktree vive en `../.arneses/` y `../ArquitecturaBase` no está al lado.
19. **`.local/` del worktree se ignora con `info/exclude`**, porque el `.gitignore` del front no lo tiene; la tarea del modelo se escribe ahí.
20. **Front en el worktree:** para verificar con `npx vitest` se enlaza el `node_modules` del repo (una *junction*, que `quitarWorktree` suelta sin tocar el destino).
21. **`archivo-nuevo` sin descripción** lo resuelve el modelo (inserta la línea de `esperado` con ≤ 10 palabras); `instrucciones.md` lo explica.
22. **Windows y los finales de línea:** con `core.autocrlf`, un checkout nuevo trae CRLF. Los tests no comparan finales de línea, y los parches los preservan.
23. **Claude real no se pudo probar en esta máquina:** `claude auth status` da `loggedIn: false` (la sesión de la app de escritorio no la hereda un `claude -p`). Los flags del plan §7 los aceptó el CLI real (devolvió «Not logged in», no un error de opción); el flujo completo está probado con un `claude` falso. Para correrlo en serio: `claude /login` y `node scripts/arneses/arnes.mjs realinear-docs --motor claude --presupuesto 1`.
24. **Codex:** el `codex` de npm (0.151.0) rechaza `gpt-6.1-sol` con una cuenta de ChatGPT («model is not supported»); el de la app de Codex (0.159.0-alpha) lo acepta. `ARNES_CODEX_BIN` (y `ARNES_CLAUDE_BIN`) apuntan al ejecutable.
25. **Sandbox de Codex en Windows con `--ignore-user-config`:** no funciona tal cual: sin el `[windows] sandbox = "elevated"` de la config del usuario, `workspace-write` rechaza todo comando («blocked by policy»). El perfil mínimo propio es un solo override: `-c windows.sandbox="elevated"` (con `unelevated` falla con «CreateProcessAsUserW … Acceso denegado»). No hizo falta un `-p`.
26. **Tope de gasto:** Claude lo limita con `--max-budget-usd`; Codex no tiene tope de costo, así que el script corta el proceso a los `minutosMaximos`.

## 12. Contadores de las corridas reales (`--detectar`, sin modelo)

**Fase 2** (back, rama `main`, 2026-10-02; solo se cuentan hallazgos, no se listan). Corre en 0,6 s.

| Detector | auto | modelo | humano | Falsos positivos |
|---|---:|---:|---:|---|
| referencias | 1 | 18 | 0 | 3 (`Modules/`, `Disabled/` y una carpeta futura sin marca: planeado sin `(E#)`) |
| punteros | 2 | 24 | 0 | 0 (los 24 son carpetas con puntero que no están en el mapa de arnes.md §3) |
| indices | 0 | 0 | 1 | 0 |

Antes de ajustar, el primer pasaje dio 28 auto / 125 modelo (`referencias` 99, `punteros` 53): los ajustes de las decisiones 3 a 8 sacaron los falsos positivos.

**Fase 3** (back, `main`, 2026-10-02; corre en 1,8 s; con el front, 2,7 s).

| Detector | auto | modelo | humano | Falsos positivos |
|---|---:|---:|---:|---|
| referencias | 0 | 18 | 0 | 3 (los de la fase 2) |
| simbolos | 0 | 26 (24 tipos, 2 miembros) | 0 | ~6: negaciones del doc (`IQuery`) y nombres del framework que faltan en la lista |
| arbol | 200 | 58 | 54 (45 archivos y 9 carpetas que figuran y nunca existieron) | 0: son desvíos reales |
| punteros | 2 | 24 | 0 | 0 |
| indices | 0 | 0 | 1 | 0 |
| catalogos | 0 | 0 | 25 (11 grupos de códigos, 8 de permisos y 6 de rutas sin feature) | 0 |

Los 200 `auto` de `arbol` son archivos nuevos con descripción sacada del `<summary>` (o "tests de X"); los 58 `modelo` no tienen `<summary>`.

Ajustes que salieron de esta corrida (decisiones 11 a 14): primero `simbolos` dio 63 tipos y 126 miembros inexistentes, y 47 `auto` equivocados (plurales de `DbSet`, nombres de archivo como `Email.cs`, tipos del framework).

**Fase 6** (front, `--detectar`, misma fecha): auto 139 · modelo 54 · humano 23. `arbol` 126 auto, 41 modelo y 23 humano (20 archivos y 3 carpetas que figuran y nunca existieron); `punteros` 12 auto (carpetas del mapa sin puntero) y 1 modelo; `referencias` 1 auto y 8 modelo; `simbolos` 4 modelo. Los hooks de React y de las librerías (`useState`, `useQuery`…) estaban dando 13 falsos positivos: ahora están en la lista de nombres del framework.

## 13. Primera corrida real (Codex, `gpt-6.1-sol`, esfuerzo `low`)

Las dos corridas partieron de `main` en `3e53648` (back) y `b386ed1` (front); cada una dejó **una rama** y **un commit**, sin push: `arnes/realinear-docs/20261002-2248` (back) y `arnes/realinear-docs/20261002-2252` (front).

| | Back | Front |
|---|---|---|
| Hallazgos al empezar (auto · modelo · humano) | 204 · 134 · 80 | 145 · 52 · 25 |
| Aplicados sin modelo | 204 | 145 |
| Enviados al modelo (tope 40) | 40: 38 líneas nuevas de `arbol.md` y 2 descartes (`ignorados.json`) | 40 |
| Al terminar (auto · modelo · humano) | 0 · 94 · 80 (los 94 son los que superaron el tope) | 0 · 12 · 25 |
| Tokens de entrada (de caché) · salida | 226 158 (180 480) · 3 302 | 256 513 (200 576) · 2 965 |
| Costo | n/d (Codex no lo informa) | n/d |
| Verificación | `node --test` y `HarnessTests`: verdes | `npx vitest run src/test/harness.test.ts`: verde |
| Commit | 6 archivos, +256 −3 (`arbol.md`, 2 filas de docs, `ignorados.json` y los 2 punteros de `Configurations/PublicSite`) | 20 archivos, +203 −1 (punteros de las carpetas nuevas de `areas/business` y `arbol.md`) |

**Alcanzó:** con el modelo barato (esfuerzo `low`) y 40 hallazgos por corrida, los dos repos terminaron con la verificación en verde. Quedan 94 + 12 hallazgos `modelo` para las próximas corridas (cada una procesa hasta `maxHallazgos`).

**Intentos anteriores** (cada uno encontró un defecto del arnés, ya corregido): (1) el test de la corrida leía el `ignorados.json` real, que el propio arnés acababa de modificar; (2) un test comparaba finales de línea con LF y en el worktree venía CRLF; (3) `HarnessTests` mira el repo hermano en `../ArquitecturaBaseMutitenantFront` (el worktree vive en `../.arneses/`) y el build había regenerado `docs/contracts/openapi.json`, que no se debe commitear: ahora se enlaza el hermano mientras dura la verificación y la guardia revierte, después de verificar, lo que el build dejó fuera de `permisos`.

**Claude Code: no se pudo correr** (`claude auth status` → `loggedIn: false`; ver decisión 23). Quedan sin medir la corrida con `--motor claude --presupuesto 1` y la de `--modelo haiku` sobre el mismo informe. Con `claude /login` hecho:

```bash
node scripts/arneses/arnes.mjs realinear-docs --repo back --motor claude --presupuesto 1
node scripts/arneses/arnes.mjs realinear-docs --repo back --motor claude --modelo haiku
```

### Cuánto cuesta cargar la config del usuario (medido)

Misma tarea mínima (un hallazgo), Codex `gpt-6.1-sol` esfuerzo `low`, en esta máquina (plugins y MCP del usuario cargados):

| | Entrada | De caché | Salida |
|---|---:|---:|---:|
| `--ignore-user-config` (lo que hace el arnés) | 53 416 | 47 744 | 206 |
| sin aislar (`--sin-aislar`) | 90 942 | 66 176 | 372 |

Aislar evita 37 526 tokens de entrada por llamada (−41 %; sin contar la caché, 5 672 contra 24 766). **Claude Code:** no se pudo medir por la falta de sesión; el flag equivalente es `--sin-aislar`, que quita `--setting-sources project --strict-mcp-config`.
