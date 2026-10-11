---
name: arnes
description: Corre un arnés ejecutable del repo (por ejemplo `realinear-docs`) y muestra solo el resumen del informe: rama, contadores, consumo y la lista para revisar a mano. Usalo cuando pidan realinear la documentación con el código o correr, programar o crear un arnés.
---

# /arnes <nombre> [opciones]

Un arnés ejecutable detecta con un script (0 tokens), arregla solo lo mecánico y le pasa a un modelo barato solo lo que pide criterio. El trabajo lo hace el arnés, no esta sesión: acá solo se lo corre y se cuenta el resultado.

## Qué hacer

1. Sin nombre, listá los arneses: `node scripts/arneses/arnes.mjs`.
2. Corré `node scripts/arneses/arnes.mjs <nombre> <opciones>` **en segundo plano** (puede tardar minutos) y esperá a que termine, sin consultar el avance.
3. Leé **solo** el `.md` del informe que imprime el comando (`.local/arneses/<nombre>/<fecha>-<repo>.md`).
4. Mostrá: estado, rama, contadores (auto, modelo y humano, al empezar y al terminar), consumo (tokens y costo cuando el motor lo informa) y la lista «Para vos».
5. No rehagas lo que el arnés ya hizo: no vuelvas a revisar ni a corregir sus cambios. Lo que queda en «Para vos» o lo que hace fallar la verificación posterior se resuelve así:
   - **Si es mecánico**, resolvelo vos sin preguntar, en la misma rama del arnés, y volvé a correr el detector y los tests que fallaban. Por ejemplo: una ruta o un símbolo que cambió de lugar, un enlace o un puntero roto, una fila del árbol o una línea de más en un `AGENTS.md`.
   - **Si es una decisión**, mostrásela a la persona y esperá su respuesta. Por ejemplo: qué dice una regla, una conducta o algo que ve el usuario.
6. Si el estado es `falló`, decí qué falló y dónde quedó el worktree.

## Opciones

| Opción | Qué hace |
|---|---|
| `--detectar` | solo detecta y escribe el informe: 0 tokens, sin worktree |
| `--sin-modelo` | arregla lo mecánico y no llama al modelo |
| `--motor claude\|codex` | elige el motor; desde Codex pasá `--motor codex` |
| `--modelo <nombre>` · `--presupuesto <USD>` | pisan el modelo y el tope de gasto del manifiesto |
| `--repo back\|front` | procesa un solo repo |
| `--fusionar` | si todo verificó, avanza la rama actual con `--ff-only` |

Nunca hagas push ni fusiones sin que la persona lo pida.
