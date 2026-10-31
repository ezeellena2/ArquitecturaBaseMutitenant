# Documentación

**Regla:** todo documento es de una de tres clases, y la carpeta dice cuál. **Vivo:** cuenta lo que existe hoy; si no coincide con el código, es un error. **Decisión:** cuenta por qué se eligió algo; no se reescribe, se reemplaza. **De paso:** sirve hasta que algo termina; declara su estado y, al terminar, lo que vale pasa a un vivo y el archivo se retira.

## Cómo se hace
- **Antes de crear un archivo,** buscá su lugar en esta tabla. Si ya hay un documento del tema, se suma ahí. No hay otras carpetas.

| Si vas a contar… | Clase | Va en | Forma |
|---|---|---|---|
| cómo se hace algo, siempre igual | vivo | `docs/rules/` | esta misma, en 60 líneas como mucho |
| qué hace un área hoy | vivo | `docs/features/` | como [Roles](../features/roles.md): termina en `Carpetas` y `Lo verifica` |
| cómo está armado el sistema | vivo | `docs/architecture/` | secciones numeradas |
| una receta paso a paso | vivo | `docs/guides/` | para qué sirve y los pasos |
| cómo configurar u operar | vivo | `docs/operations/` | una sección por tarea |
| por qué se eligió algo | decisión | `docs/decisions/` | `Estado`, `Contexto`, `Decisión` y `Consecuencias`; si entra en una línea, solo la fila del índice |
| qué se va a hacer en una etapa | de paso | `docs/plans/` | [etapa en paralelo](../guides/etapa-en-paralelo.md), 4.000 palabras como mucho |
| la spec de una etapa | de paso | `docs/plans/formularios/` | [plantilla](../plans/formularios/plantilla.md), 4.000 palabras como mucho |
| material para escribir otra cosa | de paso | `docs/plans/` | libre, con su condición de salida |

- **De paso:** debajo del título va `**Estado:** <palabra> · <detalle>`. Hay cuatro palabras: `pendiente`, `en curso`, `cerrado` e `insumo`, que además dice `se retira cuando …`.
- **Vivos:** no declaran estado: si están ahí, valen hoy. Cambian en el mismo commit que el código, y lo planeado lleva `(E#)`.
- **Revisado:** si el detector marca una ficha funcional y sigue siendo cierta, se le agrega `**Revisado:** AAAA-MM-DD` debajo del título.
- **Decisiones:** un cambio va como enmienda en su fila del índice o como decisión nueva que reemplaza a la anterior. Del archivo solo cambia el estado.
- **Al cerrar una etapa:** se suma a `ClosedStages` en los dos repos, lo que sirve de su plan pasa a sus vivos y el plan se retira con `git rm`. Queda en la historia: `git log --diff-filter=D -- <ruta>`.
- **Para saber qué está viejo:** `node scripts/arneses/arnes.mjs estado-docs --detectar`, que no gasta tokens. Lo que no aparece está al día hasta donde ve un script: una contradicción de sentido solo la ve quien lee.

## Prohibido
- Un archivo fuera de esta tabla, o una carpeta nueva para documentos.
- Repetir un dato en dos documentos: se enlaza el que manda.
- Guardar evidencia en el repo: actas, recibos, informes de corrida y logs van a `.local/`.
- Que un vivo anuncie algo de una etapa que ya cerró.
- Tomar como regla un `insumo` o algo `cerrado`.
- Un documento que nadie enlaza.

## Copiá de
- `docs/features/roles.md`, para una ficha funcional.
- `docs/decisions/0046-nombre-y-apellido.md`, para una decisión.
- `docs/plans/formularios/plantilla.md`, para una spec.

## Lo verifica
- `estado-docs.test.mjs` (E11): los detectores del arnés `estado-docs`.
- `HarnessTests` (E0): formato y referencias de las fichas, y `ClosedStages` igual en los dos repos.

## Detalle
[Arnés](../architecture/arnes.md) · [arneses ejecutables](../../scripts/arneses/README.md)
