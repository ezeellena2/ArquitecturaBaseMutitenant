# ADR 0071 · El conductor en carriles: el back en copias, agentes cortos y la integración una vez por carril

**Estado:** aceptada el 2026-11-24, por pedido del usuario («Sí, todo»), después de medir la base 2. Cambia la [0067](README.md) (el conductor de etapas) en tres puntos: el back pasa de un escritor a varios carriles, los agentes paran antes y los tests de integración corren por carril. Guía: [conducir una etapa](../guides/conducir-una-etapa.md). **Enmienda 2026-11-26 (código primero, pedido del usuario):** el punto 3 queda sin efecto. Cada tarea hace su código, su test dirigido y el build, y commitea; los tests rápidos enteros, la integración y la seguridad corren una vez, en la puerta. La revisión adversarial deja de ir en cada tarea: va una vez, en el cierre, como una premisa por tema de riesgo. En el reparto, cada tarea pendiente pesa uno.

**Enmienda 2026-11-27 (las cinco fases, pedido del usuario):** el conductor programa y cierra en cinco fases: programar; revisar todo a la vez (un revisor por cada ficha de `docs/rules/` que tocan los archivos de la etapa, los transversales de arquitectura y de lo que no está en la spec, uno adversarial por cada tema de riesgo tocado y los tests enteros con los detectores, en copias de `HEAD` dentro de `.local/`); arreglar, con un agente por repo que comprueba cada hallazgo; una última revisión del diff entero; y cerrar el plan con su commit. Se van la puerta, los verificadores, la integración aparte y la premisa de pantallas por etapa: el E2E real, la galería y el recorrido corren en la prueba integral del cierre del bloque E12–E25. Con varias etapas, de la revisión al cierre corren una sola vez. Los carriles, `carril.mjs` y la fila pesada siguen.

## Contexto

La corrida de la base 2 del 2026-11-24 se midió en sus primeras 2 h 40 min. Los números son de 15 agentes:
- **El back era lo que más tardaba.** Sus tareas iban de a una, porque dos agentes en el mismo checkout se pisan el build. Una tarea con riesgo tardaba de 60 a 95 minutos.
- **La espera de los tests de integración.** Era el 40 % del tiempo del back. En la B12 corrieron enteros dos veces, al programar y al corregir: 23 minutos cada vez.
- **El cupo semanal manda.** Con unos 1,2 puntos por tarea, alcanza para unas 3 etapas por semana. La ventana de 5 horas se llenaba en 3.
- **El gasto en tokens no era leer las reglas.** Lo grande era que cada paso relee todo lo que el agente lleva:
  - una tarea larga hacía de 100 a 200 pasos y terminaba con 300 a 470 mil tokens de contexto;
  - el 74 % de lo que llenaba el contexto era leer y buscar código;
  - el 12 % era la salida de git y el 10 %, la de los tests.

El usuario teme que cada etapa tarde una semana.

## Decisión

1. **Los carriles del back.**
   - **Cada carril programa en su propia copia de main,** en `.local/copias/<clave>-back-<n>`: un clon con su propio build. Así dos carriles no se pisan, y no hay ramas ni worktrees.
   - **El script es `scripts/arneses/carril.mjs`:**
     - `preparar` crea la copia o la pone al día;
     - `entregar` rebasa lo propio sobre main, verifica la copia si entró algo de otro carril y hace avanzar main con `--ff-only`, de a un carril por vez;
     - `limpiar` borra las copias que ya entregaron todo.
   - **Lo que falla en la entrega no se fuerza:** un choque se resuelve en la copia, y si main tiene cambios ajenos en esos archivos, no se entrega.
2. **El reparto.**
   - **El lector del plan da tres datos por tarea:** su `area`, si trae `migracion` y su mapa de archivos.
   - **Van juntas en un carril** las de la misma área (tocan los mismos archivos) y todas las que traen migración (comparten el snapshot).
   - **Cada grupo va al carril con menos trabajo.** Una tarea con revisión adversarial cuenta como tres.
   - **Adentro de cada carril se respeta el orden del plan.** Así, una espera entre carriles nunca deja a uno trabado. Si el reparto formara un ciclo, el back vuelve a un solo carril.
   - **`carrilesBack` va de 1 a 4,** y por defecto es 2. Con 1, el back trabaja en main, como antes.
3. **La integración, una vez por carril.**
   - **Lo que corre cada tarea:** el test dirigido, que puede ser de integración (filtrado por su clase), el build y los tests rápidos. Quien corrige un hallazgo corre solo su prueba.
   - **Al cerrar cada carril,** un agente corre los tests de integración y de seguridad de las áreas que tocaron sus tareas, y arregla lo que falle.
   - **Una sola integración por vez.** Esos tests esperan su turno en la fila pesada de `carril.mjs`, para no cargar la PC. Así se sigue cumpliendo «una corrida pesada por vez» (traspaso 18).
4. **Agentes más baratos.**
   - **Agentes cortos:** paran cerca de los 80 pasos (antes, 150) y siguen con otro, hasta seis.
   - **El mapa de la tarea:** cada agente lo recibe, así no recorre el repo buscando.
   - **Lectura por partes:** se lee con `grep -n` y por rangos, y de un diff, primero `--stat`.
   - **Verificación con la salida mínima:** se verifica con `scripts/arneses/verificar.mjs` en el back y `scripts/verificar.mjs` en el front. Muestran solo lo que falla y dejan el log entero en `.local/verificar/`.
5. **`modelos`.**
   - **Elige otro modelo por rol** (`front`, `back`, `verifica`). Por defecto, todos usan el de la sesión.
   - **La documentación oficial no dice que Sonnet gaste menos cupo del plan que Opus** (https://code.claude.com/docs/en/costs.md). Por eso se mide con una tarea antes de usarlo en una etapa.

## Consecuencias

- **Una etapa típica pasa de un día a medio día,** si el cupo alcanza. Lo más largo pasa a ser el carril más cargado.
- **El cupo baja menos que el tiempo.** Los carriles gastan lo mismo en menos horas, así que llenan antes la ventana de 5 horas. Lo que baja el cupo son los agentes cortos, el mapa y la consola callada: entre 30 y 50 % menos tokens por etapa, a confirmar con la próxima.
- **Una falla de integración aparece al cerrar el carril y no en la tarea misma.** Los commits son chicos y llevan su `Tarea:`, así que se ubica rápido.
- **En una copia, los tests que cruzan al front se saltean,** porque el front no está al lado. Corren en los tests enteros de la revisión, que arma las dos copias hermanas.

## Verificación

- **`carril.test.mjs`** prueba, en repos temporales:
  - la entrega de dos carriles uno encima del otro;
  - la verificación con lo que entró de main;
  - el choque en el rebase;
  - que no pise los cambios de otra sesión;
  - la fila;
  - la limpieza.
- **`verificar.test.mjs`,** en los dos repos, prueba la salida mínima con salidas reales.
- **`conductor.test.mjs`** prueba:
  - el reparto por área y las migraciones juntas;
  - las copias en los prompts;
  - la espera entre carriles;
  - las cinco fases, con una sola revisión y un solo cierre para varias etapas;
  - los modelos y los agentes cortos.
