---
name: plan-etapa
description: Arma el plan ejecutable de una etapa en tres partes (contrato; back y front en paralelo; integración y puerta) con el texto listo para cada chat que programa, y resuelve con el usuario lo que queda abierto en tandas de preguntas, cada una con opciones y una recomendada. Usala cuando pidan planificar una etapa o una funcionalidad nueva, o pasarle una etapa a Codex.
---

# /plan-etapa <etapa o tema>

El plan tiene que poder ejecutarse sin adivinar. Las decisiones de producto las toma el usuario. Las técnicas las toma quien planifica, y quedan anotadas en el plan.

## Qué hacer

1. **Leé las fuentes:**
   - la sección de la etapa en `docs/plans/2026-09-27-plan-de-desarrollo.md`;
   - las ADR de `docs/decisions/`, con su índice;
   - el lienzo vigente (`docs/design/lienzo/` del front) y sus recorridos guiados (`docs/design/recorridos/`);
   - la guía `docs/guides/etapa-en-paralelo.md`;
   - como modelo de las tres partes y del texto para cada chat, los planes de la 8 y la 8b (`docs/plans/2026-10-07-etapa-8-whatsapp.md` y `docs/plans/2026-10-08-etapa-8b-asistente.md`), pero no de su largo: tienen 20.700 y 15.800 palabras, y un plan nuevo, unas 4.000 como mucho.
2. **Si falta un tablero, pedilo antes de escribir el plan.** Lo dibuja «Conversacion - Diseño», la única que toca el lienzo. Si no le podés escribir, dale el texto al usuario.
3. **Escribí el plan en `docs/plans/<fecha>-etapa-<n>-<tema>.md`, corto (unas 4.000 palabras como mucho, sin historial; `AGENTS.md`, «Velocidad y cierre liviano») y con estas secciones:**
   - **Forma de trabajo:** modo rápido y una sola puerta al final.
   - **Contrato:** rutas, cuerpos, códigos de error y permisos. Va primero y completo.
   - **Back** y **Front:** tareas con sus archivos, el archivo real del que se copia y sus tests. Cada parte lleva el texto listo para pegar en su chat.
   - **Integración y puerta:** lo que corre la skill `/puerta`.
   - **Relación con otras etapas.**
   - **Cambios al contrato:** vacía; la usan los chats si algo cambia.
   - **Para decidir:** cada punto con opciones y una recomendada.

   Si planifica la Guía (Claude), el plan lo escribe un chat de Codex, así no se gasta el uso de Claude. La Guía le pasa este punto como texto y después lo revisa.
4. **Revisá el plan antes de dárselo al usuario.** Tiene que aplicar las ADR nuevas, el contrato tiene que estar completo y nada puede quedar condicional.
5. **Resolvé «Para decidir» con el usuario:**
   - en tandas de cuatro preguntas como mucho, con opciones y la recomendada primero;
   - al usuario se le preguntan solo las decisiones de producto: lo que ve o puede hacer una persona. Las técnicas las decide quien planifica y las anota en una línea del plan. Si una de producto tiene una parte técnica, primero explicá qué es, para qué sirve y un ejemplo real del repo;
   - anotá cada respuesta en el plan, y también en una ADR si cambia una regla;
   - commiteá solo esos archivos.
6. **Entregá el orden de los chats y sus textos:** primero el Contrato; después Back y Front a la vez; al final, Integración y puerta. Si el usuario trabaja con un chat coordinador de Codex, todos los textos van a ese coordinador.

## Nunca

- **Programar** desde el chat que planifica.
- **Decidir por el usuario** algo que cambia lo que ve o puede hacer una persona.
- **Dejar condicionales** del estilo «si el usuario quiere…»: el plan sale cerrado.
