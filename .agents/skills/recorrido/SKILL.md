---
name: recorrido
description: Corre el recorrido de usuario con Playwright (un solo navegador que conserva las sesiones, en un entorno aislado con su propia base y los correos en archivos) y muestra qué pasos anduvieron y cuáles no, con sus capturas. Usala en la prueba integral del cierre del bloque E12–E25, que es donde corre (desde el 2026-11-27 no corre en cada etapa), o cuando pidan recorrer la app como un usuario o probar el ingreso de punta a punta.
---

# /recorrido

El recorrido hace lo que haría una persona, en el mismo navegador y sin borrar las sesiones anteriores. Así encuentra lo que los tests no ven: de esta forma apareció el ingreso roto de la Etapa 5. Corre una vez, entero, en la prueba integral del cierre del bloque E12–E25 (skill `/puerta`), no en el cierre de cada etapa.

## Qué hacer

1. **Corrélo en el front.** En `../ArquitecturaBaseMutitenantFront`, o en la copia del front de la prueba integral, corré en segundo plano `node scripts/recorrido-usuario.mjs` y esperá a que termine.
   - Si el build ya está hecho, agregá `--no-build`.
   - El script levanta su propio entorno y lo apaga al final.
2. **Los pasos están en el script.** Los recorridos guiados del lienzo (`docs/design/recorridos/*.json` del front) dicen qué tiene que poder hacer una persona. Los pasos de cada etapa del bloque se escriben en la prueba integral, junto con su test (`scripts/recorrido-usuario.test.mjs`), a partir de su sección del backlog («Para después de la etapa <clave>», que trae lo de «Al cierre del bloque E12–E25» de su plan).
3. **Leé el resultado.** Queda en `docs/design/capturas/recorrido/<fecha>/index.html`, con cada paso, lo esperado, lo que pasó y sus capturas. Leé el resumen de comprobaciones y, de los pasos, solo los que fallaron.

## Qué mostrar

- **El total:** pasos y comprobaciones en verde.
- **Cada paso que falló:** qué se esperaba, qué pasó, su captura y la causa probable en una línea.
- **La ruta del `index.html`,** para abrirlo.
- **Lo que el script no automatiza,** como el ingreso con Google o un WhatsApp real: va como «para probar a mano», nunca como aprobado.

## Nunca

- **Usar el correo o el teléfono reales del usuario:** el recorrido tiene su propio entorno.
- **Dejar el entorno levantado si el script se cortó:** corré `aspire stop` y, si queda un proceso, cortalo por PID.
