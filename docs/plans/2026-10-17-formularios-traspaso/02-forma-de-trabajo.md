# 02 · Forma de trabajo

Así se trabajó cada etapa en ArquitecturaBase, después de corregir lo que al principio hacía todo lento. Acá mandan las guías propias de este proyecto (`docs/guides/etapa-en-paralelo.md`, skill `/plan-etapa`, `docs/rules/`). Esto sirve para conservar el mismo ritmo y las mismas garantías.

## Roles

| Quién | Qué hace |
| --- | --- |
| **El usuario** | Elige los tableros, toma las decisiones de producto y hace los pilotos reales con su celular. Nunca se da un tablero por elegido sin que lo diga. |
| **Chat coordinador (Claude)** | Guía y propone. Escribe los resúmenes de etapa, lanza los code reviews, arma los mensajes listos para pegar y mantiene la documentación liviana. No programa. |
| **Chat de diseño (Claude)** | Dibuja los tableros y el recorrido guiado en el lienzo, con piezas comunes. Anota las elecciones en el resumen de la etapa y en el registro de tableros del front. |
| **Un chat de Codex por etapa** | Programa backend y front juntos, de punta a punta, y hace el piloto con el usuario. Un chat nuevo por etapa, para que lea solo el resumen y no arrastre contexto viejo. |

## El ciclo de una etapa

1. **Tablero antes que código.** Cada pantalla nueva o cambiada se dibuja en el lienzo con su funcionalidad completa:
   - estados de carga, vacío y error;
   - qué pasa sin permiso;
   - el recorrido de diálogos;
   - qué vive en la URL;
   - cómo se ve en el celular.

   Se programa después de que el usuario elige. Programar primero y dibujar después no cuenta. Los ajustes chicos de un tablero ya elegido también se dibujan antes de programar esa parte; el chat de Codex la deja para el final y se le avisa cuando está elegida.
2. **Resumen de una página por etapa** (`docs/plans/formularios/etapa-N-*.md`), con este esquema:
   - **Estado** (una línea con fecha);
   - **Qué se construye** (por pieza: backend, front y tablero);
   - **Decisiones tomadas — no se vuelven a preguntar**;
   - **Fuera de esta etapa**;
   - **Cómo se cierra**;
   - **Requisitos previos**;
   - **Ajustes ya decididos** (lo que se decidió después, con fecha).

   El chat de Codex lee solo ese resumen y el `AGENTS.md`. Lo que el resumen no fija, lo resuelve con la opción más simple, sin preguntar.
3. **Un solo mensaje de arranque al chat de Codex** (ver «Mensajes tipo» abajo).
4. **Desarrollo con TDD donde hay lógica:** primero el test en rojo. En cada corte, solo el build y los tests rápidos del área: unitarios y de arquitectura en el backend, y en el front `tsc`, `lint` y los tests de la feature. Commits chicos, en español, con conventional commits, en `main`, sin push.
5. **Corrida pesada, una sola vez al final:**
   - la integración con PostgreSQL, Redis y Azurite (Docker);
   - `dotnet test` completo y el build sin advertencias;
   - en el front, `npm run build`, `lint`, `test` y `test:visual` con revisión de capturas;
   - el recorrido en el navegador;
   - `aspire stop`.
6. **Si la corrida final falla,** se corrige y se repite solo lo que falló, y después una corrida completa más. Vale la última en verde. Las intermedias no se registran, no se nombran ni se discuten. Un test que falla solo cuando corre junto con los demás es un problema de aislamiento de datos del test, no de la app.
7. **Code review por etapa, de solo lectura:** dos agentes en paralelo, uno de backend y uno de front.
   - **Qué revisan:** seguridad y permisos, bugs con escenario concreto, robustez frente al ADR 0018, «hacer de más», diferencias con los tableros, reglas del front y tests que faltan.
   - **Cómo se entrega:** un archivo `docs/reviews/AAAA-MM-DD-code-review-etapa-N.md` con «Arreglar ahora» (numerado, con archivo:línea, escenario y arreglo) y «Con la etapa siguiente o al tocar esos archivos».
   - **Cuándo se corrige:** lo hace el chat de la etapa siguiente al empezar, o el mismo chat entre dos cortes.
8. **Piloto real para cerrar:** con Meta (el número real y el celular del usuario), Azure real y la IA real cuando corresponde.
   - **Antes:** el usuario manda «hola» desde el celular para abrir las 24 horas de WhatsApp.
   - **Durante:** el chat de Codex levanta la app con el túnel y guía paso a paso.
   - **Al terminar:** registra el resultado en pocas líneas y apaga la app.
9. **Cierre documental:** las decisiones van a `docs/features/` y a sus fuentes; el resumen y el review se retiran, con procedencia en `docs/history/README.md`; el índice de etapas se actualiza.

## Lo que hizo lento al principio, y cómo se arregló

- **Documentación inflada:** cada chat leía más de 300 KB. Se depuró en dos pasadas: resúmenes de una página, un `AGENTS.md` como índice y nada de relatos de corridas intermedias.
- **Tres chats relevándose** (backend, front y coordinador): se pasó a un solo chat de Codex por etapa, con backend y front juntos.
- **Pruebas pesadas en cada corte:** se movieron al final.
- **Burocracia de tests:** hashes, manifiestos y corridas «adicionales» pasados entre chats. El backend exporta los cuerpos HTTP reales con un script, el front los consume, y no se pasa nada a mano.
- **Preguntas que ya estaban decididas:** cada resumen tiene «Decisiones tomadas — no se vuelven a preguntar», y la regla «lo más simple, sin preguntar» está en el `AGENTS.md`.
- **Chats viejos con hábitos viejos:** un chat nuevo por etapa.

Con esto, la Etapa 2 llevó menos de dos días y las Etapas 3 y 4 unas 3 o 4 horas de programación cada una, más los arreglos.

## Premisas de criterio

- **La opción más simple** que cumpla el plan y las reglas, sin mecanismos por las dudas.
- **Sin capas de compatibilidad** para un contrato todavía no publicado (creado en el desarrollo en curso): se cambia junto con el front.
- **No se protege contra clientes, datos o casos que no existen.**
- **Robustez proporcional al efecto (ADR 0018):** recuperación durable, estados inciertos y pruebas de varios hosts solo para efectos fuera del sistema (Meta, correo, archivos, LLM). La configuración interna se guarda con un límite, control de revisión y 409.
- **El contrato con el front** son los DTO, el OpenAPI y los cuerpos exportados.
- **Las decisiones se registran donde las lee quien programa:** el resumen de la etapa y `docs/features/`.

## Mensajes tipo

**Arranque de etapa** (chat nuevo de Codex):

```
Primero corregí el code review de la Etapa N-1: docs/reviews/<archivo>.md, los puntos de «Arreglar ahora», en orden, backend y front juntos, cada uno con su test y commits chicos. Después arrancá la Etapa N. Leé AGENTS.md y docs/plans/formularios/etapa-N-*.md: ahí están el alcance y las decisiones tomadas, que no se vuelven a preguntar; fijate en «Ajustes ya decididos». Hacé backend y front en este mismo chat, en el orden del resumen. TDD donde hay lógica. En cada corte, solo el build y los tests rápidos del área; la integración con Docker, test:visual y el recorrido en el navegador se corren una sola vez, al final de todo. Lo que el resumen no fija, resolvelo con la opción más simple, sin preguntarme. Commits chicos en main, sin push. Al terminar, avisame con un resumen corto para la prueba real.
```

**Piloto** (mismo chat de Codex):

```
Arranquemos la prueba real con Meta de la Etapa N. Ya mandé «hola» desde mi celular. Prepará todo (aspire run con el túnel) y guiame con lo mínimo: qué toco en la web y qué hago en el celular. Comprobemos: <lista>. Al terminar, registrá el resultado en el resumen en pocas líneas y hacé aspire stop.
```

**Tablero elegido a mitad de etapa:**

```
Ya está elegido <tablero> (lienzo, versión N); está en «Ajustes ya decididos» del resumen. <qué define, en dos líneas>. Cuando llegues a esa parte, programala así.
```
