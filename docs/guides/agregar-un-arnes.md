# Agregar un arnés ejecutable

> Receta para sumar una rutina como `realinear-docs`. El CLI, el worktree, los motores, la guardia, el informe y los parches **no se tocan**: un arnés nuevo es una carpeta con su manifiesto, sus detectores y sus instrucciones. Cómo funciona todo: [`scripts/arneses/README.md`](../../scripts/arneses/README.md) y el [ADR 0037](../decisions/0037-arneses-ejecutables.md).

## Pasos

1. **Copiá la plantilla:** `scripts/arneses/_plantilla/` a `scripts/arneses/<nombre>/` (las carpetas que empiezan con `_` no se listan).
2. **Completá `arnes.json`:**
   - `repos` (`back`, `front`) y `detectores` (nombres de los archivos de `detectores/`);
   - `excluir`: lo que nunca se mira (historia, planes, ADR, contratos);
   - `permisos`: `puedeCrear`, `puedeBorrar` y `nuncaToca` (código, tests, `.resx`, migraciones, contratos y secretos van **siempre** en `nuncaToca`);
   - `modelo`: motor, `maxHallazgos`, y por motor el nombre, el esfuerzo (`low`) y el tope (`presupuestoUsd` o `minutosMaximos`);
   - `verificacion` por repo y el mensaje de `commit`.
3. **Escribí los detectores** (`detectores/<nombre>.mjs`), cada uno con su test sobre `fixtures/`: exportan `nombre` y `detectar(ctx)`, solo leen y devuelven hallazgos hechos con `ctx.crear({ archivo, linea, tipo, clave, real, accion, ... })`. La `clave` hace estable el `id` entre corridas.
4. **Elegí la acción de cada hallazgo:**
   - `auto` cuando hay **una** respuesta inequívoca; trae un `parche` (`reemplazar`, `borrar-linea`, `insertar-linea`, `crear-archivo`, `escribir-archivo`, `borrar-archivo`);
   - `modelo` cuando hace falta criterio; trae `contexto` (±5 líneas) y los `candidatos`;
   - `humano` cuando es una decisión de producto o un cambio de código: no se toca y va al informe con la pregunta concreta.
5. **Escribí `instrucciones.md`** (las mismas para los dos motores) y, si el arnés usa el modelo, copiá `resultado.schema.json` de `realinear-docs`.
6. **Probalo con `--detectar`** sobre el repo real y ajustá los detectores hasta que casi no haya falsos positivos: cada falso positivo gasta tokens del modelo.
7. **Sumá la fila** al catálogo de `scripts/arneses/README.md`.

## Reglas que no cambian

- Sin dependencias npm: solo módulos de Node.
- Los tests no leen el repo real ni llaman a un modelo: usan `fixtures/` y los ejecutables falsos de `comun/fakes/`.
- El código manda en lo que existe; la documentación manda en la intención, y una regla que el código no cumple es `humano`.
- Lo planeado (`(E#)` de una etapa que no cerró) no es un error.
- Nunca se hace push.

## Ideas que esta estructura ya soporta

`revisar-textos` (claves `.resx` sin uso o sin traducción), `revisar-lienzo` (pantallas del lienzo contra rutas del front), `revisar-adr` (decisiones sin implementar).
