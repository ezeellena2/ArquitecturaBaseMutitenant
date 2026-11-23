---
name: puerta
description: Corre la puerta de una etapa (suites del back y del front, contrato, E2E real, detector de documentación, galería lado a lado y recorrido de usuario) sobre una copia del corte, sin worktrees ni ramas, con cada paso completo una sola vez y después solo lo que toca cada arreglo, y muestra un resumen corto de qué pasó y qué no. Usala cuando pidan verificar o cerrar una etapa, «correr la puerta» o confirmar que todo está en verde.
---

# /puerta <etapa> [corte del back] [corte del front]

La puerta se corre **una sola vez, al final de la etapa** (`AGENTS.md`, «Cierre liviano»). Esta skill la corre y cuenta el resultado; no arregla nada. Cada paso pasa completo una sola vez. Cada fallo vuelve a su dueño, y con el arreglo se corre solo lo que ese arreglo toca. **No se repite todo al final ni se arma un corte nuevo por cada arreglo.** La suite completa de un repo se repite solo si el arreglo tocó algo que usa todo el repo: autenticación, persistencia base o composición de la app.

## Qué hacer

1. **Elegí el punto de partida, una sola vez.** Usá los dos commits que te pasen o, si no te pasan ninguno, `HEAD` de `main` en cada repo. Los arreglos que lleguen después se traen a esas mismas copias, y se corre solo lo que tocan.
2. **Armá dos copias hermanas del corte, sin worktrees ni ramas** (`AGENTS.md`, «Todo en `main`, también la puerta»), de ruta corta y con el nombre exacto de cada repo, porque el E2E, `contracts:check` y el detector buscan el otro repo al lado.
   - Por ejemplo, `C:\e2e\ArquitecturaBaseMutitenant` y `C:\e2e\ArquitecturaBaseMutitenantFront`, con `git clone --local <repo> <ruta>` y `git -C <ruta> checkout --detach <corte>` desde cada repo: la copia conserva la historia que leen los detectores y no suma ramas ni worktrees al repo de trabajo. Un arreglo se trae con `git -C <ruta> fetch` y otro `checkout --detach`.
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
   | Estado de los documentos | back | `node scripts/arneses/arnes.mjs estado-docs --detectar` | auto, modelo y humano en 0 |
   | Lo que no está en la spec | back | `node scripts/arneses/arnes.mjs conformidad --detectar --plan <plan de la etapa>` | humano en 0, en los dos repos. Cada hallazgo vuelve a su dueño: lo saca, o pasa a la spec por «Cambios al contrato» si es una decisión de producto del usuario; lo que no entra va a «Para después» |
   | Galería | front | `node scripts/capturar-etapa.mjs --casos scripts/capturas/etapa-<etapa>.json` y, si existe, `node scripts/generar-galeria-etapa-<etapa>.mjs` | solo las pantallas nuevas o cambiadas en la etapa, cada una al lado de su tablero en `docs/design/capturas/etapa-<etapa>/`, y alguien la mira |
   | Recorrido de usuario | front | `node scripts/recorrido-usuario.mjs --no-build` (ver la skill `/recorrido`) | todas las comprobaciones en verde |
   | Quitar el módulo | copia descartable | solo en la etapa del módulo, una sola vez y con la base vacía, según su guía (por ejemplo `docs/guides/quitar-whatsapp.md`) | núcleo, build, tests e ingreso en verde |
   | Lista de control | los dos | la revisión de código del cierre, contra la lista de abajo | cada punto que aplica a la etapa tiene su test, y pasa |
   | Commits que se pisan | los dos | contra el código del corte, que ningún commit de la etapa haya deshecho lo de otro (en ArquitecturaBase uno armado sobre una copia vieja deshizo 34 archivos) | lo de cada tarea sigue en el código |

4. **Leé solo los resúmenes:** totales, `passed` y `failed`, `# pass` y `# fail`, y los contadores del detector. Si algo falla, abrí solo las líneas del fallo.
5. **Antes de contar un fallo, descartá el entorno.** Si un test se pasó de tiempo, corré ese archivo solo. Si así pasa, el problema era el entorno y no cuenta como fallo.
6. **No registres cada corrida.** Las salidas quedan en `.local/`. En el plan va solo la tabla final de abajo.

## Qué mostrar

- **Una tabla** con una fila por paso: el resultado y el número (por ejemplo, «1989/1989»).
- **Por cada fallo:** `archivo:línea`, la causa en una línea y a qué dueño vuelve (Back o Front).
- **El corte medido:** los dos commits.
- **Qué falta para cerrar la etapa.** No digas «cerrada» si falta el E2E real, la galería o el recorrido de usuario. Lo «terminado» se comprueba contra el código, no contra los commits ni el reporte de un chat.
- **Al cerrar el bloque E12–E25** faltan además la revisión de arquitectura de Formularios (`docs/guides/revisar-arquitectura.md`) y la prueba integral con el usuario (`AGENTS.md`, «Forma de trabajo»).

## Lista de control

Sale de los 142 problemas que encontraron los reviews de ArquitecturaBase (capítulo 05 y anexo E del traspaso del 2026-10-23, pedido del usuario del 2026-10-24) y de las doce preguntas de su `validar-un-cambio.md` (traspaso 15). Cada punto que aplica a la etapa tiene un test escrito durante la etapa, o una comprobación contra el tablero o el código. Si falta uno, vuelve a su dueño como un fallo.

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

- **Hacer push**, fusionar o cerrar la etapa si el usuario no lo pide.
- **Mostrar secretos:** de user-secrets se leen solo los nombres.
- **Matar procesos con un filtro amplio:** si hay que cortar algo, se corta por PID.
- **Dejar algo levantado:** al final, `aspire stop` y se borran las dos copias.
