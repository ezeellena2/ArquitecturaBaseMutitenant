---
name: puerta
description: Corre la puerta de una etapa (suites del back y del front, contrato, E2E real, detector de documentación, galería lado a lado y recorrido de usuario) en worktrees aparte, con cada paso completo una sola vez y después solo lo que toca cada arreglo, y muestra un resumen corto de qué pasó y qué no. Usala cuando pidan verificar o cerrar una etapa, «correr la puerta» o confirmar que todo está en verde.
---

# /puerta <etapa> [corte del back] [corte del front]

La puerta se corre **una sola vez, al final de la etapa** (`AGENTS.md`, «Cierre liviano»). Esta skill la corre y cuenta el resultado; no arregla nada. Cada paso pasa completo una sola vez. Cada fallo vuelve a su dueño, y con el arreglo se corre solo lo que ese arreglo toca. **No se repite todo al final ni se arma un corte nuevo por cada arreglo.** La suite completa de un repo se repite solo si el arreglo tocó algo que usa todo el repo: autenticación, persistencia base o composición de la app.

## Qué hacer

1. **Elegí el punto de partida, una sola vez.** Usá los dos commits que te pasen o, si no te pasan ninguno, `HEAD` de `main` en cada repo. Los arreglos que lleguen después se traen a esos mismos worktrees, y se corre solo lo que tocan.
2. **Armá dos worktrees hermanos**, de ruta corta y con el nombre exacto de cada repo, porque el E2E, `contracts:check` y el detector buscan el otro repo al lado.
   - Por ejemplo, `C:\e2e\ArquitecturaBaseMutitenant` y `C:\e2e\ArquitecturaBaseMutitenantFront`, con `git worktree add <ruta> <corte>` desde cada repo.
   - En el front corré `npm ci`, porque un `node_modules` enlazado rompe la letra Inter en las capturas.
   - No uses la copia de trabajo: ahí trabajan otros chats.
3. **Corré cada paso en segundo plano y esperá a que termine, sin consultar el avance.** Tardan minutos. Guardá cada salida en `.local/puerta-<etapa>/` de la copia principal del back, que no se versiona.

   | Paso | Dónde | Comando | Pasa si |
   |---|---|---|---|
   | Build del back | back | `dotnet build ArquitecturaBaseMultitenant.slnx` | 0 errores y 0 advertencias |
   | Tests del back | back | `dotnet test --no-build`; necesita Docker | 0 fallidos |
   | Build del front | front | `npm run build` | sale 0 |
   | Lint | front | `npm run lint` | 0 errores |
   | Tests del front | front | `npm test`, **solo**, porque con la suite del back en paralelo los tests con axe pasan los 5 s | 0 fallidos |
   | Contrato | front | `npm run contracts:check` | sale 0 |
   | E2E real | front | `npm run test:e2e:real`, **solo**, porque levanta su propio AppHost | 0 fallidos |
   | Documentación | back | `node scripts/arneses/arnes.mjs realinear-docs --detectar` | auto, modelo y humano en 0, en los dos repos |
   | Galería | front | `node scripts/capturar-etapa-<etapa>.mjs` y, si existe, `node scripts/generar-galeria-etapa-<etapa>.mjs` | solo las pantallas nuevas o cambiadas en la etapa, cada una al lado de su tablero en `docs/design/capturas/etapa-<etapa>/`, y alguien la mira |
   | Recorrido de usuario | front | `node scripts/recorrido-usuario.mjs --no-build` (ver la skill `/recorrido`) | todas las comprobaciones en verde |
   | Quitar el módulo | copia descartable | solo en la etapa del módulo, una sola vez y con la base vacía, según su guía (por ejemplo `docs/guides/quitar-whatsapp.md`) | núcleo, build, tests e ingreso en verde |

4. **Leé solo los resúmenes:** totales, `passed` y `failed`, `# pass` y `# fail`, y los contadores del detector. Si algo falla, abrí solo las líneas del fallo.
5. **Antes de contar un fallo, descartá el entorno.** Si un test se pasó de tiempo, corré ese archivo solo. Si así pasa, el problema era el entorno y no cuenta como fallo.
6. **No registres cada corrida.** Las salidas quedan en `.local/`. En el plan va solo la tabla final de abajo.

## Qué mostrar

- **Una tabla** con una fila por paso: el resultado y el número (por ejemplo, «1989/1989»).
- **Por cada fallo:** `archivo:línea`, la causa en una línea y a qué dueño vuelve (Back o Front).
- **El corte medido:** los dos commits.
- **Qué falta para cerrar la etapa.** No digas «cerrada» si falta el E2E real, la galería o el recorrido de usuario.

## Nunca

- **Hacer push**, fusionar o cerrar la etapa si el usuario no lo pide.
- **Mostrar secretos:** de user-secrets se leen solo los nombres.
- **Matar procesos con un filtro amplio:** si hay que cortar algo, se corta por PID.
- **Dejar algo levantado:** al final, `aspire stop` y `git worktree remove` de los dos worktrees.
