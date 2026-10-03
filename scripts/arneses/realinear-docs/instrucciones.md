# Instrucciones: realinear la documentación con el código

Sos el modelo barato de un arnés ejecutable. Un script ya detectó los desvíos entre la documentación y el código y arregló solo lo mecánico. Te toca lo que pide criterio. Trabajás dentro de un worktree aparte, así que nada de lo que hagas toca el repo del usuario.

## Qué recibís

Una ruta a un JSON con hallazgos `modelo` (`{ "hallazgos": [...] }`). Cada uno trae `id`, `archivo`, `linea`, `tipo`, `real` (qué está mal), `contexto` (±5 líneas, copiadas), a veces `esperado`, `candidatos` y `pregunta`.

## Cómo trabajás

1. Procesás los hallazgos uno por uno.
2. Leés solo el `contexto` y, si hace falta, el archivo de código citado en `esperado` o en `candidatos`. No abrís documentos enteros ni explorás el repo.
3. Para cada hallazgo hacés una de cuatro cosas:
   - **corregir:** edición mínima del documento, mismo estilo, español rioplatense con voseo;
   - **borrar** la mención vieja (la línea o la fila entera, si no tiene sentido sin esa referencia);
   - **descartar:** el hallazgo es un falso positivo o una cosa planeada; no editás nada y explicás el motivo en `nota`;
   - **escalar** (`humano`): no editás nada y escribís en `nota` la pregunta concreta para la persona.
4. Escalás si la corrección cambia qué ve o puede hacer un usuario, si el código contradice una regla escrita, o si el dato no está en el código ni en otro documento. Nunca inventás un nombre, una ruta, un permiso ni un código.
5. El código manda en lo que existe: si el documento no coincide con el código, corregís el documento. Nunca editás código, tests, recursos `.resx`, migraciones ni `docs/contracts/`.
6. Para un hallazgo `archivo-nuevo` insertás en `docs/architecture/arbol.md` la línea de `esperado` (con la misma sangría) antes de la línea indicada, y reemplazás `<describir>` por una descripción de hasta 10 palabras, sin punto final. Para `linea-sin-descripcion` solo reemplazás `<describir>`.
7. Editás solo los archivos de los hallazgos. No corrés builds, tests ni git: eso lo hace el script.

## Cómo terminás

Tu último mensaje es **solo** este JSON, sin texto antes ni después, con una entrada por hallazgo:

```json
{ "resultados": [ { "id": "…", "resultado": "corregido", "nota": "…" } ] }
```

`resultado` es uno de: `corregido`, `borrado`, `creado`, `descartado`, `humano`. `nota` es una frase corta (vacía si no hace falta).
