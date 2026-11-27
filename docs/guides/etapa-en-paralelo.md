# Una etapa con back y front en paralelo

Desde la Etapa 8, cada etapa con back y front se programa con **dos agentes a la vez**, uno por repo, y se integra al final. Lo pidió el usuario el 2026-10-07. Rige junto con la forma de trabajo rápida: programar directo, tests donde importan y un solo cierre al final ([plan de la Etapa 7](../features/sitio-publico.md), «Forma de trabajo»).

Los dos repos son separados, así que los dos agentes no se pisan. Lo único que comparten es **el contrato**.

## Las tres partes

| Parte | Quién | Qué hace | Termina cuando |
|---|---|---|---|
| **1. Contrato** | Un solo agente, en el back | Escribe las rutas nuevas de la etapa en `docs/contracts/openapi.json`: ruta, permiso, lo que recibe, lo que devuelve y cada código de error con su texto. Pueden ir sin implementación o devolviendo `501`. Las guardas de rutas tienen que seguir en verde: el barrido de `RouteIsolationMatrixTests` no cuenta como caída el `501` con `General.Contract.NotImplemented`, y sí cualquier otro 5xx. En el front, `npm run contracts`. | El contrato está commiteado en los dos repos y el plan de la etapa lo nombra. |
| **2a. Back** | Agente del back | Programa las rutas del contrato: dominio, servicios, persistencia, aislamiento y permisos, con sus tests. **No cambia el contrato.** | El build y los tests del área en verde, y `openapi.json` igual al de la parte 1. La suite completa corre una sola vez, en el cierre. |
| **2b. Front** | Agente del front | Programa las pantallas contra el contrato, copiando su tablero, con las respuestas de la API simuladas en los tests, como hoy. **No cambia el contrato.** | Build del alcance afectado, lint de los archivos cambiados, tests focales del área (Vitest con paths o filtro) y `contracts:check` en verde. `npm run test` ejecuta la suite completa: se reserva para el cierre. |
| **3. Revisión y cierre** | Los agentes del cierre, con los dos repos | Revisan todo a la vez, con los tests enteros, arreglan lo que encuentran, hacen una última revisión y cierran el plan. | El plan queda cerrado (abajo). |

Las partes 2a y 2b corren al mismo tiempo. El [conductor](conducir-una-etapa.md) corre las tres solo, con un agente por tarea.

## Reglas

- **El contrato manda.** Si en la parte 2 hace falta cambiarlo, lo decide quien programa y lo anota en «Cambios al contrato» del plan. El coordinador le avisa al otro chat antes de publicarlo, y el cambio se hace en los dos repos. Al usuario se le pregunta solo si el cambio cambia lo que una persona ve o puede hacer.
- **Cada agente toca solo su repo.** El del front no edita el back, y al revés. Commits con `git commit -m "…" -- <rutas>`, en `main` y sin push.
- **Las pantallas salen del tablero vigente.** Si a un tablero le falta un estado, se le pide a Diseño y se sigue con el resto. Una pantalla nueva entera necesita su tablero aprobado antes de programarse.
- **Los textos van en es y en,** en cada repo: los errores en los resources del back y las pantallas en los `locales` del front.
- **Nada se da por terminado** hasta el cierre de la parte 3.

## El cierre (parte 3)

Desde el 2026-11-27 la etapa no tiene puerta: la cierra el conductor en cuatro fases, una sola vez aunque salgan varias etapas juntas ([conducir una etapa](conducir-una-etapa.md)):
1. **Revisión, todo a la vez:** un revisor por cada ficha de `docs/rules/` que tocan los archivos de la etapa, los transversales de arquitectura y de lo que no está en la spec, uno adversarial por cada tema de riesgo tocado, y los tests enteros: `dotnet build` sin advertencias, `dotnet test` con Docker, `npm run build`, `lint`, `test` y `contracts:check`, y los detectores de documentación (`realinear-docs`, `estado-docs` y `conformidad`).
2. **Arreglos:** un agente por repo comprueba cada hallazgo y arregla lo real con su test dirigido.
3. **Una última revisión** del diff entero, que arregla lo chico y manda lo demás al backlog.
4. **El cierre y su commit:** la spec cerrada, el plan retirado, la hoja de ruta y el backlog. La etapa queda con el código listo, el primer hito.

El segundo hito, «Verificada», lo da `verificar-etapa.js` sobre una copia del corte, mientras la etapa siguiente se programa en main: escribe los casos a partir de la sección «Verificación de la etapa» (la del plan, o la que el cierre pasó al backlog) y corre una vez, enteros, el contrato, el E2E real, la galería de capturas y el recorrido de usuario (skills [`/puerta`](../../.agents/skills/puerta/SKILL.md) y [`/recorrido`](../../.agents/skills/recorrido/SKILL.md)). La prueba integral del cierre del bloque E12–E25 sigue como pasada final. Lo que falla va a los arreglos, que corren solo su test dirigido y el build. No se repite todo al final. El resultado es una tabla corta, y no un registro de cada corrida (`AGENTS.md`, «Velocidad y cierre liviano»).

## Cómo se arma el plan de una etapa

El plan parte de la spec de la etapa, con el formato de [`docs/plans/formularios/plantilla.md`](../plans/formularios/plantilla.md): toma su contrato, sus datos y sus pasos, los contrasta con el código real y anota solo lo que cambió. Antes de planificar, `node scripts/arneses/arnes.mjs alinear-etapas --detectar` no puede dar hallazgos de esa etapa. En el bloque de formularios, los planes de la base a la E25 se escriben todos antes de programar: lo que deja una etapa anterior va a la tarea K0 del contrato, que lo confirma al arrancar ([`/plan-etapa`](../../.agents/skills/plan-etapa/SKILL.md)).

El plan ejecutable de cada etapa trae tres secciones, una por parte:
- «Contrato», con la tabla de rutas;
- «Back» y «Front», con sus tareas;
- «Revisión y cierre», con los riesgos que suma a la lista de control y la documentación que escribe el cierre;
- «Verificación de la etapa», con los casos del E2E, los pasos del recorrido y los tableros de la galería que va a recorrer su verificación.

Además trae el texto con que la Guía abre cada chat, y la sección «Cambios al contrato», vacía al empezar. Tiene unas 4.000 palabras como mucho y no lleva historial: el estado es una tabla de tareas.
