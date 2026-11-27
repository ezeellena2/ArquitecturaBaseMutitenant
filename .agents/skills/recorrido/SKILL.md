---
name: recorrido
description: Corre el recorrido de usuario con Playwright (un solo navegador que conserva las sesiones, en un entorno aislado con su propia base y los correos en archivos) y muestra qué pasos anduvieron y cuáles no, con sus capturas. Usala en la verificación de cada etapa (el segundo hito, que corre desde verificar-etapa.js), en la prueba integral del cierre del bloque E12–E25, o cuando pidan recorrer la app como un usuario o probar el ingreso de punta a punta.
---

# /recorrido

El recorrido hace lo que haría una persona, en el mismo navegador y sin borrar las sesiones anteriores. Así encuentra lo que los tests no ven: de esta forma apareció el ingreso roto de la Etapa 5. Corre una vez, entero y en los dos anchos, en la verificación de cada etapa, sobre una copia de su corte mientras se programa la siguiente, y otra vez en la prueba integral del cierre del bloque E12–E25, la pasada final (skill `/puerta`).

## Qué hacer

1. **Corrélo en el front.** En `../ArquitecturaBaseMutitenantFront`, o en la copia del front de la verificación o de la prueba integral, corré en segundo plano `node scripts/recorrido-usuario.mjs` y esperá a que termine. En una copia, va por la fila pesada, un ancho por llamada: `node scripts/arneses/carril.mjs fila --dir <copia del front> "node scripts/recorrido-usuario.mjs --no-build --width=1440"`, y después `--width=390`, desde el back de siempre.
   - Si el build del back ya está hecho, agregá `--no-build`.
   - El script levanta su propio entorno y lo apaga al final.
2. **Los pasos están en el script.** Los recorridos guiados del lienzo (`docs/design/recorridos/*.json` del front) dicen qué tiene que poder hacer una persona. Los pasos de cada etapa los escribe su verificación, junto con su test (`scripts/recorrido-usuario.test.mjs`), a partir de su sección «Verificación de la etapa» (la del plan o, si se retiró, la del backlog).
3. **Leé el resultado.** Queda en `docs/design/capturas/recorrido/<fecha>/index.html`, con cada paso, lo esperado, lo que pasó y sus capturas. Leé el resumen de comprobaciones y, de los pasos, solo los que fallaron.

## Qué mostrar

- **El total:** pasos y comprobaciones en verde.
- **Cada paso que falló:** qué se esperaba, qué pasó, su captura y la causa probable en una línea.
- **La ruta del `index.html`,** para abrirlo.
- **Lo que el script no automatiza,** como el ingreso con Google o un WhatsApp real: va como «para probar a mano», nunca como aprobado.

## Nunca

- **Usar el correo o el teléfono reales del usuario:** el recorrido tiene su propio entorno.
- **Dejar el entorno levantado si el script se cortó:** corré `aspire stop` y, si queda un proceso, cortalo por PID.
