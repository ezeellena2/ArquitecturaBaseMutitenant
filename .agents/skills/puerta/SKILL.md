---
name: puerta
description: Corre la puerta completa de una etapa sobre un corte fijo (suites del back y del front, contrato, E2E real, detector de documentación, galería lado a lado y recorrido de usuario) en worktrees aparte y muestra un resumen corto de qué pasó y qué no. Usala cuando pidan verificar o cerrar una etapa, «correr la puerta» o confirmar que todo está en verde.
---

# /puerta <etapa> [corte del back] [corte del front]

La puerta se corre **una sola vez, al final de la etapa** (modo rápido, `AGENTS.md`). Esta skill la corre y cuenta el resultado; no arregla nada. Cada fallo vuelve a su dueño y, después del arreglo, se repite lo que ese arreglo invalida.

## Qué hacer

1. **Fijá el corte.** Usá los dos commits o etiquetas que te pasen. Si no te pasan ninguno, tomá `HEAD` de `main` en cada repo y etiquetalo (`git tag puerta-<etapa>-<n>`). Así no medís código que se sigue moviendo.
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
   | Galería | front | `node scripts/capturar-etapa-<etapa>.mjs` y, si existe, `node scripts/generar-galeria-etapa-<etapa>.mjs` | cada pantalla al lado de su tablero en `docs/design/capturas/etapa-<etapa>/`, y alguien la mira |
   | Recorrido de usuario | front | `node scripts/recorrido-usuario.mjs --no-build` (ver la skill `/recorrido`) | todas las comprobaciones en verde |
   | Quitar el módulo | copia descartable | solo en las etapas de un módulo quitable, según su guía (por ejemplo `docs/guides/quitar-whatsapp.md`) | núcleo, build, tests e ingreso en verde |

4. **Leé solo los resúmenes:** totales, `passed` y `failed`, `# pass` y `# fail`, y los contadores del detector. Si algo falla, abrí solo las líneas del fallo.
5. **Antes de contar un fallo, descartá el entorno.** Si un test se pasó de tiempo, corré ese archivo solo. Si así pasa, el problema era el entorno y no cuenta como fallo.

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
