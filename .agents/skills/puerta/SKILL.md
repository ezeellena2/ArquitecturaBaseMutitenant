---
name: puerta
description: Corre la verificación de una etapa con el código listo (el segundo hito, con el workflow verificar-etapa.js: integración y contrato, E2E real, galería lado a lado con las pantallas contra el tablero y recorrido de usuario) o la prueba integral del cierre del bloque E12–E25 (la pasada final con el usuario, con las suites y los detectores), sobre una copia del corte, sin worktrees ni ramas, con cada paso completo una sola vez y después solo lo que toca cada arreglo, y muestra un resumen corto de qué pasó y qué no. Usala cuando pidan verificar una etapa, la prueba integral del bloque o «correr la puerta».
---

# /puerta [clave de la etapa | bloque] [corte del back] [corte del front]

Desde el 2026-11-27 (pedido del usuario), cada etapa tiene dos hitos, que se avisan por separado ([conducir una etapa](../../../docs/guides/conducir-una-etapa.md)):
1. **«Código listo»:** lo deja el conductor (`etapa.js`), con la revisión en paralelo, los tests enteros, los arreglos y una última revisión.
2. **«Verificada»:** la integración, el E2E real, la galería contra los tableros y el recorrido de esa etapa, sobre una copia de su corte, mientras la siguiente se programa en `main`. Así se encadenan: E12 código → (verificación E12 ∥ código E13) → (verificación E13 ∥ código E14)…

Esta skill corre ese segundo hito, y también **la prueba integral del cierre del bloque E12–E25**, que sigue como pasada final con el usuario (`AGENTS.md`, «Forma de trabajo»). Cuenta el resultado; los arreglos los hacen sus dueños, en `main`. Cada paso pasa completo una sola vez. Cada fallo vuelve a su dueño, y con el arreglo se corre solo lo que ese arreglo toca. **No se repite todo al final ni se arma un corte nuevo por cada arreglo.** La suite completa de un repo se repite solo si el arreglo tocó algo que usa todo el repo: autenticación, persistencia base o composición de la app.

## Una etapa: el workflow

```js
Workflow({ scriptPath: '.agents/workflows/verificar-etapa.js', args: { clave: '12-catalogos' } })
```

Va con `clave` o con `plan` (su ruta, aunque ya esté retirado), y con `corte: { back, front }` si no es el `HEAD` del momento. Hace los pasos de abajo en cinco fases:
- **a. Preparar:** lee la sección de verificación de la etapa (en su plan, «Verificación de la etapa»; si se retiró, en el backlog, como «Verificación de la etapa <clave>» o «Para después de la etapa <clave>», o en el plan que retiró git), escribe en `main` los casos del E2E real, los pasos del recorrido y el JSON de la galería (scripts del front, sin tocar su `src`) y arma las copias en `C:\e2e-<clave>\`, con esos casos encima.
- **b. Correr:** integración y contrato, E2E real, galería, recorrido a 1440 y a 390, y quitar el módulo si la sección lo pide, cada uno con su agente y de a uno por la fila pesada de `carril.mjs` (`fila --dir <copia>`), que se suelta entre paso y paso para que entren los carriles de la etapa siguiente. Las pantallas contra el tablero (puntos 10 y 13) las mira un revisor sobre la galería, con un verificador, a la vez que el recorrido.
- **c. Arreglos:** cada fallo y cada diferencia confirmada, en `main`, por su dueño y con su test dirigido.
- **d. Repetir:** trae los arreglos a las copias con `cherry-pick` (nunca el `HEAD` de `main`, que trae la etapa siguiente) y corre solo lo que tocan.
- **e. Cierre:** la hoja de ruta pasa de «Código listo» a «Verificada el <fecha>», la tabla corta va a la spec, la galería al front de `main`, la sección sale del backlog, lo de a mano queda en «Para después de la etapa <clave>» y se borran las copias. El resultado dice «verificada» o «verificación con fallos», con lo que falta.

## El bloque: a mano

0. **Escribí primero lo que falta para esta prueba:** lo que dejaron Acciones del asistente y Costuras de módulos en sus secciones «Para después de la etapa…» del backlog, y lo que cada verificación dejó «para probar a mano». Se commitea antes de armar las copias.
1. **Elegí el punto de partida, una sola vez.** Usá los dos commits que te pasen o, si no te pasan ninguno, `HEAD` de `main` en cada repo. Los arreglos que lleguen después se traen a esas mismas copias, y se corre solo lo que tocan.
2. **Armá dos copias hermanas del corte, sin worktrees ni ramas** (`AGENTS.md`, «Todo en `main`, también la puerta»), de ruta corta y con el nombre exacto de cada repo, porque el E2E, `contracts:check` y el detector buscan el otro repo al lado.
   - Por ejemplo, `C:\e2e\ArquitecturaBaseMutitenant` y `C:\e2e\ArquitecturaBaseMutitenantFront`, con `git clone --local <repo> <ruta>` y `git -C <ruta> checkout --detach <corte>` desde cada repo: la copia conserva la historia que leen los detectores y no suma ramas ni worktrees al repo de trabajo. Un arreglo se trae con `git -C <ruta> fetch` y `git -C <ruta> cherry-pick <commit>`.
   - En el front corré `npm ci`, porque un `node_modules` enlazado rompe la letra Inter en las capturas.
   - No uses la copia de trabajo: ahí trabajan otros chats.
3. **Corré cada paso en segundo plano y esperá a que termine, sin consultar el avance.** Tardan minutos. Los pesados van de a uno por la fila (`node scripts/arneses/carril.mjs fila --dir <copia> "<comando>"`, desde el back de siempre). Guardá cada salida en `.local/puerta-bloque/` de la copia principal del back, que no se versiona.

   La tabla dice qué corre en cada caso: la verificación de una etapa, las filas marcadas con «etapa»; la prueba del bloque, todas.

   | Paso | Dónde | Comando | Pasa si |
   |---|---|---|---|
   | Build del back | back | `dotnet build ArquitecturaBaseMultitenant.slnx` (etapa: lo hace la integración) | 0 errores y 0 advertencias |
   | Tests del back | back | `dotnet test --no-build`; necesita Docker | 0 fallidos |
   | Integración (etapa) | back | `node scripts/arneses/verificar.mjs --integracion`, que espera su turno en la fila | 0 fallidos |
   | Build del front | front | `npm run build` | sale 0 |
   | Lint | front | `npm run lint` | 0 errores |
   | Tests del front | front | `npm test`, **solo**, porque con la suite del back en paralelo los tests con axe pasan los 5 s | 0 fallidos |
   | Contrato (etapa) | front | `npm run contracts:check` | sale 0 |
   | E2E real (etapa) | front | `npm run test:e2e:real`, **solo**, porque levanta su propio AppHost | 0 fallidos |
   | Documentación | back | `node scripts/arneses/arnes.mjs realinear-docs --detectar` | auto, modelo y humano en 0, en los dos repos |
   | Estado de los documentos | back | `node scripts/arneses/arnes.mjs estado-docs --detectar` | auto, modelo y humano en 0 |
   | Galería (etapa) | front | `node scripts/capturar-etapa.mjs --casos scripts/capturas/etapa-<etapa>.json`, con la app en `http://localtest.me:5192` | solo las pantallas nuevas o cambiadas, cada una al lado de su tablero en `docs/design/capturas/etapa-<etapa>/`, comparadas contra él (puntos 10 y 13), y el usuario la mira |
   | Recorrido de usuario (etapa) | front | `node scripts/recorrido-usuario.mjs --no-build --width=1440` y `--width=390` (ver la skill `/recorrido`) | todas las comprobaciones en verde |
   | Quitar el módulo (etapa) | copia descartable | solo si la etapa trajo un módulo quitable nuevo, una sola vez y con la base vacía, según su guía (por ejemplo `docs/guides/quitar-formularios.md`) | núcleo, build, tests e ingreso en verde |
   | Commits que se pisan | los dos | contra el código del corte, que ningún commit del bloque haya deshecho lo de otro (en ArquitecturaBase uno armado sobre una copia vieja deshizo 34 archivos) | lo de cada tarea sigue en el código |

4. **Leé solo los resúmenes:** totales, `passed` y `failed`, `# pass` y `# fail`, y los contadores del detector. Si algo falla, abrí solo las líneas del fallo.
5. **Antes de contar un fallo, descartá el entorno.** Si un test se pasó de tiempo, corré ese archivo solo. Si así pasa, el problema era el entorno y no cuenta como fallo.
6. **No registres cada corrida.** Las salidas quedan en `.local/`. En la spec va solo la tabla final de abajo.

## Qué mostrar

- **El resultado, claro:** «verificada» o «verificación con fallos» para una etapa; para el bloque, si se puede cerrar.
- **Una tabla** con una fila por paso: el resultado y el número (por ejemplo, «1989/1989»).
- **Por cada fallo:** `archivo:línea`, la causa en una línea y a qué dueño vuelve (Back o Front).
- **El corte medido:** los dos commits.
- **Qué falta.** No digas «verificada» si falta el E2E real, la galería o el recorrido de la etapa, ni «cerrado» el bloque si falta la verificación de una etapa, la revisión de arquitectura de Formularios (`docs/guides/revisar-arquitectura.md`) o que el usuario haya mirado. Lo «terminado» se comprueba contra el código, no contra los commits ni el reporte de un chat.

## Lista de control

Sale de los 142 problemas que encontraron los reviews de ArquitecturaBase (capítulo 05 y anexo E del traspaso del 2026-10-23, pedido del usuario del 2026-10-24) y de las doce preguntas de su `validar-un-cambio.md` (traspaso 15). Cada punto que aplica a una etapa tiene un test escrito durante la etapa, o una comprobación contra el tablero o el código. Los revisores del código listo de cada etapa la usan; los puntos 10 y 13, contra el tablero, se miran en la galería de su verificación.

1. **Números y fechas** se leen con la cultura del pedido, y lo ambiguo se rechaza. Se prueba con «1.500», «1,500», «12.345» y «3/4/2026», en es y en.
2. **El front no tapa la validación del servidor.** Solo el 409 de versión pone el editor en conflicto; otro 409 es un error que se corrige.
3. **Cambiar algo en uso** (tipo, fuente, papel, obligatorio, activo o eliminado):
   - se frena por el uso, o pide confirmación y limpia;
   - lo guardado que apunta a algo dado de baja sigue valiendo.
4. **Guardar sin cambios no reescribe nada.** Escribe solo la fila de la entidad (`ModifiedAtUtc`), así EF compara la versión ([concurrencia](../../../docs/rules/concurrencia.md)), y no toca ni audita lo que cuelga de ella (opciones, preguntas, valores).
5. **Ninguna lectura trae todo para quedarse con poco.** Se prueba con el volumen máximo de la etapa.
6. **Nada nombra otro recurso sin permiso de leerlo** (ni un 409, ni un aviso, ni una vista previa), **y nada mezcla organizaciones:** un id de otra devuelve 404, los trabajos de fondo entran con su organización, y la caché y las cuotas llevan su alcance.
7. **Nada exige un idioma fijo.** Se guarda y se relee con una traducción vacía, con `null` y con una referencia inactiva.
8. **Cada exclusión o lock tiene un test** con dos conversaciones, canales o actores a la vez.
9. **Las migraciones son portables y conviven con la versión anterior:**
   - el SQL va en una línea y sin `\r`;
   - el Down se niega antes de perder datos;
   - se agrega antes de borrar, y un valor nuevo de un enum se escribe un despliegue después del que lo lee (`docs/guides/migracion.md`, «Convivencia de versiones»).
10. **Botones y avisos** según `pantallas-y-ui.md` del front: sin textos al lado de un botón y sin «Cambios sin guardar». Toda acción que tarda muestra que trabaja, con el aro y su gerundio. Sin leyendas: la ayuda va en la (i), y lo apagado dice por qué, también en el teléfono.
11. **Cada arreglo busca el mismo defecto** en los caminos hermanos y en las piezas compartidas, y cada `fix` dice qué pasaba.
12. **Cada respuesta de un proveedor** se clasifica como definitiva, transitoria o desconocida, y cada estado tiene una salida, con su test. Cada variante de WhatsApp se abre en un celular.
13. **Cada pantalla se compara con su tablero,** y sus textos son ciertos en todos los estados: cargando, vacío, con error, sin permiso y con la organización suspendida.
14. **Cada error en pantalla tiene un solo dueño:** no lo muestran a la vez el campo, el cartel y el aviso.
15. **Lo que cambia un dato invalida todo lo que lo muestra:** la lista, la ficha, los conteos y la campana.
16. **Lo que la pantalla ofrece sigue la misma condición que el servidor:** un botón a la vista no termina en un 403 o un 409 seguro.
17. **Cada tope sale de una sola constante,** y lo que cuesta (IA, mensajes) se mide antes de pagar.
18. **Uno malo no frena al resto:** en un lote, un trabajo de fondo o una importación, la fila que falla se registra y las demás siguen.
19. **Anda con los datos que ya existen:** se prueba con filas guardadas antes del cambio, no solo con las nuevas.
20. **El test falla sin el arreglo,** y el contrato exportado es el que el front acepta tal cual (`npm run contracts:check`).

## Nunca

- **Hacer push**, fusionar o dar el bloque por cerrado si el usuario no lo pide.
- **Traer el `HEAD` de `main` a la copia de una etapa:** trae lo de la etapa siguiente; los arreglos se traen con `cherry-pick`.
- **Mostrar secretos:** de user-secrets se leen solo los nombres.
- **Matar procesos con un filtro amplio:** si hay que cortar algo, se corta por PID.
- **Dejar algo levantado:** al final, `aspire stop` y se borran las dos copias.
