# Una etapa con back y front en paralelo

Desde la Etapa 8, cada etapa con back y front se programa con **dos agentes a la vez**, uno por repo, y se integra al final. Lo pidió el usuario el 2026-10-07. Rige junto con la forma de trabajo rápida: programar directo, tests donde importan y una sola puerta al final ([plan de la Etapa 7](../plans/2026-10-04-etapa-7-publico-y-personal.md), «Forma de trabajo»).

Los dos repos son separados, así que los dos agentes no se pisan. Lo único que comparten es **el contrato**.

## Las tres partes

| Parte | Quién | Qué hace | Termina cuando |
|---|---|---|---|
| **1. Contrato** | Un solo agente, en el back | Escribe las rutas nuevas de la etapa en `docs/contracts/openapi.json`: ruta, permiso, lo que recibe, lo que devuelve y cada código de error con su texto. Pueden ir sin implementación o devolviendo `501`. Las guardas de rutas tienen que seguir en verde. En el front, `npm run contracts`. | El contrato está commiteado en los dos repos y el plan de la etapa lo nombra. |
| **2a. Back** | Agente del back | Programa las rutas del contrato: dominio, servicios, persistencia, aislamiento y permisos, con sus tests. **No cambia el contrato.** | `dotnet build` y `dotnet test` en verde, y `openapi.json` igual al de la parte 1. |
| **2b. Front** | Agente del front | Programa las pantallas contra el contrato, copiando su tablero, con las respuestas de la API simuladas en los tests, como hoy. **No cambia el contrato.** | `npm run build`, `lint`, `test` y `contracts:check` en verde. |
| **3. Integración y puerta** | Un solo agente, con los dos repos | Conecta el front a la API real y corrige lo que no encaja. | Pasa la puerta (abajo). |

Las partes 2a y 2b corren al mismo tiempo.

## Reglas

- **El contrato manda.** Si en la parte 2 un agente necesita cambiarlo, no lo cambia solo: lo anota en «Cambios al contrato» del plan, y el coordinador se lo pasa al otro agente. Recién después se cambia, en los dos repos. Al usuario se le pregunta solo si el cambio cambia lo que una persona ve o puede hacer.
- **Cada agente toca solo su repo.** El del front no edita el back, y al revés. Commits con `git commit -m "…" -- <rutas>`, en `main` y sin push.
- **Las pantallas salen del tablero vigente.** Si falta un estado, se frena y lo dibuja Diseño.
- **Los textos van en es y en,** en cada repo: los errores en los resources del back y las pantallas en los `locales` del front.
- **Nada se da por terminado** hasta pasar la puerta de la parte 3.

## La puerta (parte 3)

1. `dotnet build` sin advertencias y `dotnet test` completo con Docker.
2. `npm run build`, `lint`, `test` y `contracts:check`.
3. El E2E real con los recorridos nuevos de la etapa, con front y API reales, sin mocks.
4. `node scripts/arneses/arnes.mjs realinear-docs --detectar` en 0. Si no da 0, se corre el arnés.
5. La galería de capturas lado a lado de las pantallas nuevas.
6. El recorrido de usuario a mano, en un navegador con sesiones de antes, según la puerta del [plan de arreglos](../plans/2026-10-06-arreglos-antes-de-la-7.md#puerta-el-recorrido-de-un-usuario-nuevo) y los recorridos de la etapa.

Cada paso necesita una corrida completa en verde con el código de la etapa. Lo que falla vuelve a su dueño, y con el arreglo se corre solo lo que ese arreglo toca. No se repite todo al final ni se arma un corte nuevo por cada arreglo. El resultado es una tabla corta, con una fila por paso, y no un registro de cada corrida (`AGENTS.md`, «Velocidad y cierre liviano»).

## Cómo se arma el plan de una etapa

El plan ejecutable de cada etapa trae tres secciones, una por parte:
- «Contrato», con la tabla de rutas;
- «Back» y «Front», con sus tareas;
- «Integración y puerta».

Además trae un texto listo para pegar en cada chat de Codex, y la sección «Cambios al contrato», vacía al empezar. Tiene unas 4.000 palabras como mucho y no lleva historial: el estado es una tabla de tareas.
