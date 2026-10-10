---
name: recorrido
description: Corre el recorrido de usuario con Playwright (un solo navegador que conserva las sesiones, en un entorno aislado con su propia base y los correos en archivos) y muestra qué pasos anduvieron y cuáles no, con sus capturas. Usala cuando pidan recorrer la app como un usuario, probar el ingreso de punta a punta o verificar una etapa antes de cerrarla.
---

# /recorrido

El recorrido hace lo que haría una persona, en el mismo navegador y sin borrar las sesiones anteriores. Así encuentra lo que los tests no ven: de esta forma apareció el ingreso roto de la Etapa 5.

## Qué hacer

1. **Corrélo en el front.** En `../ArquitecturaBaseMutitenantFront`, o en el worktree de la puerta, corré en segundo plano `node scripts/recorrido-usuario.mjs` y esperá a que termine.
   - Si el build ya está hecho, agregá `--no-build`.
   - El script levanta su propio entorno y lo apaga al final.
2. **Los pasos están en el script.** Los recorridos guiados del lienzo (`docs/design/recorridos/*.json` del front) dicen qué tiene que poder hacer una persona. Si la etapa suma un recorrido, se agrega al script junto con su test (`scripts/recorrido-usuario.test.mjs`).
3. **Leé el resultado.** Queda en `docs/design/capturas/recorrido/<fecha>/index.html`, con cada paso, lo esperado, lo que pasó y sus capturas. Leé el resumen de comprobaciones y, de los pasos, solo los que fallaron.

## Qué mostrar

- **El total:** pasos y comprobaciones en verde.
- **Cada paso que falló:** qué se esperaba, qué pasó, su captura y la causa probable en una línea.
- **La ruta del `index.html`,** para abrirlo.
- **Lo que el script no automatiza,** como el ingreso con Google o un WhatsApp real: va como «para probar a mano», nunca como aprobado.

## Nunca

- **Usar el correo o el teléfono reales del usuario:** el recorrido tiene su propio entorno.
- **Dejar el entorno levantado si el script se cortó:** corré `aspire stop` y, si queda un proceso, cortalo por PID.
