# Formularios por WhatsApp: specs por etapa

Cada etapa del bloque tiene acá su spec, y todas van hacia el mismo [formato](plantilla.md): qué leer, pantallas, contrato, datos, pasos, qué verificar al arrancar y cómo se cierra. Las decisiones de producto están en la [§5 de la hoja de ruta](../2026-10-12-formularios-dinamicos-hoja-de-ruta.md#5-decisiones-de-producto), y el alcance de cada etapa, en su §3. Se escriben antes de programar, para que al programar no haya que pensar de nuevo.

**Cómo se sabe qué falta.** `node scripts/arneses/arnes.mjs alinear-etapas --detectar` no gasta tokens y dice qué etapa no está lista: secciones que le faltan, tableros que no coinciden con el lienzo, y permisos, decisiones, enlaces y estados que no cierran. No reemplaza leer las specs de punta a punta: una contradicción de sentido no la ve ningún script.

**Cómo se usa.** Cuando le toca a una etapa, [`/plan-etapa`](../../../.agents/skills/plan-etapa/SKILL.md) toma su spec y contrasta «Qué verificar al arrancar» con el código real. El contrato, los datos y los pasos ya están escritos: el plan corto anota solo lo que cambió, y suma el texto para cada chat.

**La regla del orden** (decisión 78, que controla `/plan-etapa`):
- cada etapa usa solo lo que dejaron las anteriores;
- lo que suma se ve en todas las pantallas que ya existen: un tipo de pregunta nuevo, en la planilla, el detalle, el Excel y el PDF, si ya están;
- nada nombra, prueba ni dibuja algo de una etapa posterior: eso pasa a esa etapa.

**Qué no va acá:**
- **Código para copiar:** cada spec nombra el archivo real de este repo que sirve de modelo. ArquitecturaBase es referencia, no fuente de código.
- **La ficha de `docs/features/` y los `AGENTS.md` de cada carpeta:** nacen al cerrar la etapa, del código real. El detector de documentación los controla y no puede haber punteros a carpetas que no existen.

Esta carpeta está fuera de `realinear-docs` (`docs/plans/**`); la controla `alinear-etapas`.

Las etapas van en el orden de la decisión 78, y **el número es el orden**. Antes de la 12 va [la base](base-de-la-12.md). Los tableros son los de la §5 de la [evaluación del traspaso 2](../2026-10-24-formularios-evaluacion-traspaso-2.md) y los que suman las decisiones 47 a 98, y los dibuja Diseño.

**En la columna «Tableros»** cada estado va con su etiqueta, y `alinear-etapas` la compara con el lienzo:
- **Aprobados:** listos y al día.
- **A revisar:** dibujados antes de las reglas del 25 y el 26/10. Están en el lienzo, y Diseño los revisa con su etapa.
- **Se redibujan:** están en el lienzo y una decisión posterior los cambia.
- **Por dibujar:** todavía no existen. Un código como `CE4-A` es un tablero de ArquitecturaBase que todavía no tiene nombre acá.

| Etapa | Spec | Tableros |
| --- | --- | --- |
| E12 · Catálogos | [12-catalogos.md](12-catalogos.md) | Aprobados: `FRM-Catalogos`, `FRM-Catalogo-Nuevo`, `FRM-Catalogo` (`CA1-A`), `FRM-Catalogo-Importar` (`CA2`), `FRM-Catalogo-Tipo` (`CA3`), `FRM-Catalogo-Filtros` y `FRM-Catalogo-Contactos`, con sus teléfonos, en el lienzo v141 y al día en la v145 |
| E13 · Campos de los catálogos | [13-campos.md](13-campos.md) | Aprobados: `FRM-Catalogo-13` y `M-FRM-Catalogo-13` (`CE1`, `CE2` y `CE3-B`, con «En WhatsApp» en la forma C), en el lienzo v145 |
| E14 · Formularios: crear, publicar, mandar y recibir | [14-formularios.md](14-formularios.md) | Aprobados: `FRM-Formularios`, `FRM-Publicar`, `FRM-Publicando`, `WA-Chats-Formulario`, `WA-Chats-Respuesta`, `WA-Chats-Formulario-Cliente`, `WA-Mensajes-14` y `FRM-Catalogos-14`, con sus teléfonos, en el lienzo v141 y al día en la v145. Se redibujan: `FRM-Editor` (`CA4`) y `M-FRM-Editor`, con «Agregar título» (88) |
| E15 · Respuestas: planilla, detalle, Excel y tablero | [15-respuestas.md](15-respuestas.md), [texto para Diseño](15-texto-diseno.md) | A revisar: `FRM-Formularios-15` (`MF1`), `FRM-Respuestas` (suma `CA5` y `CE5`), `FRM-Respuestas-Filtros`, `FRM-Respuesta`, `FRM-Respuestas-Exportar` y `FRM-Tablero` (`DOC7-A`), con sus teléfonos. `CA6` y `CE6` van como recorrido de la guía. Por dibujar: `FRM-Respuesta-Completa` y `M-FRM-Formularios-15` |
| E16 · Datos de las personas y filtros por destinatario | [16-datos-de-las-personas.md](16-datos-de-las-personas.md), [texto para Diseño](16-texto-diseno.md) | Por dibujar: `FRM-Datos-Personas`, `FRM-Usuario-Datos`, `FRM-Catalogo-16`, `FRM-Editor-16`, `WA-Chats-Formulario-16`, con sus teléfonos, `FRM-Publicar-16` y `FRM-Respuestas-16` |
| E17 · Ciclo de vida y copiar | [17-ciclo-de-vida.md](17-ciclo-de-vida.md), [texto para Diseño](17-texto-diseno.md) | A revisar: `FRM-Formularios-17`, `FRM-Editor-Baja`, `FRM-Dar-De-Baja`, `FRM-Reactivar-Eliminar` y `FRM-Copiar`, con sus teléfonos (`BA1-A` a `BA4`). `BA5` va como recorrido de la guía |
| E18 · Fotos y documentos | [18-fotos-y-documentos.md](18-fotos-y-documentos.md), [texto para Diseño](18-texto-diseno.md) | A revisar: `FRM-Editor-18`, `FRM-Publicar-18`, `WA-Chats-Respuesta-18` y `WA-Chats-Visor`, con sus teléfonos. Por dibujar: `FRM-Respuestas-18`, `FRM-Respuesta-18` y `WA-Chats-Adjuntos`, con sus teléfonos, y `FRM-Respuesta-Completa-18` |
| E19 · Condiciones, cálculos y líneas | [19-condiciones-calculos-y-lineas.md](19-condiciones-calculos-y-lineas.md), [texto para Diseño](19-texto-diseno.md) | Por dibujar: `FRM-Editor-Condiciones`, `FRM-Editor-Calculo`, `FRM-Editor-Lineas`, `FRM-Respuestas-19`, `FRM-Respuesta-19` y `WA-Chats-Respuesta-19`, con sus teléfonos, y `FRM-Tablero-19` |
| E20 · Cargar una respuesta desde la web | [20-cargar-desde-la-web.md](20-cargar-desde-la-web.md), [texto para Diseño](20-texto-diseno.md) | Por dibujar: `FRM-Formularios-20`, `FRM-Cargar-Respuesta`, `FRM-Cargar-Panel`, `FRM-Cargar-Lista`, `FRM-Cargar-Archivos`, `FRM-Cargar-Enviada`, `FRM-Respuestas-20`, `FRM-Respuesta-20`, `FRM-Respuestas-Propias`, `FRM-Editor-Listas` y `FRM-Publicar-20`, con sus teléfonos |
| E21 · Reglas, aprobación, PDF y avisos | [21-reglas-y-aprobacion.md](21-reglas-y-aprobacion.md), [texto para Diseño](21-texto-diseno.md) | A revisar: `FRM-Formularios-21`, `FRM-Respuesta-Aprobacion` y `WA-Chats-Avisos`, con sus teléfonos. Se redibujan: `FRM-Editor-Reglas`, `FRM-Pdf`, `M-FRM-Reglas` y `M-FRM-Respuesta-Aprobacion`, con «Mandar el PDF a un cliente» (47) y el logo, los datos y el color de la 82. Por dibujar: `FRM-Respuestas-21`, `FRM-Respuesta-Corregir` (91), `FRM-Respuesta-Anular` (92) y `FRM-Respuestas-Propias-21` (85), con sus teléfonos, y `FRM-Tablero-21` |
| E22 · Biblioteca de plantillas y formularios estándar | [22-biblioteca.md](22-biblioteca.md) | A revisar: `FRM-Formularios-22`, `FRM-Plantilla`, `FRM-Plantilla-Usar` y `FRM-Editor-Reglas-Plantilla`, con sus teléfonos. Se redibujan: `FRM-Biblioteca`, con `DOC1` sin la lectura, y 12 tableros de la organización y de Plataforma, con las reglas que llegan de una plantilla (la lista está en la spec) |
| E23 · El asistente, con Casos | [23-asistente.md](23-asistente.md), [texto para Diseño](23-texto-diseno.md) | A revisar: `FRM-Respuesta-Cadena`, `FRM-Faltan`, `WA-Chats-Asistente`, `WA-Chats-Sugerencia` y `WA-Chats-Audio`, con sus teléfonos. Se redibujan: `FRM-Editor-Asistente`, `FRM-Editor-Sugerencias` y `FRM-Catalogo-Propuesta`. Por dibujar: `WA-Asistente`, `WA-Chats-Casos`, `WA-Modo-Atencion` y `WA-Preguntas-Sin-Respuesta`, con sus teléfonos, `WA-Asistente-Casos`, `WA-Asistente-Probar` y `WA-Asistente-Limites` |
| E24 · Varios asistentes, uno por equipo | [24-varios-asistentes.md](24-varios-asistentes.md), [texto para Diseño](24-texto-diseno.md) | Por dibujar: `WA-Asistentes`, `WA-Equipo-Asistente` y `WA-Chats-Casos-24`, con sus teléfonos |
| E25 · Documentos por WhatsApp | [25-documentos.md](25-documentos.md), [texto para Diseño](25-texto-diseno.md) | Por dibujar: `WA-Chats-Documento`, `FRM-Documentos`, `FRM-Editor-Lectura` y `FRM-Armar-Con-IA`, con sus teléfonos, `M-WA-Flow-Prellenado`, `Plat-FRM-Plantilla-Lectura` y `FRM-Respuesta-25` |

## Equivalencia con los números de antes

Hasta el 2026-10-25 los números de las etapas eran nombres, no orden. Los documentos anteriores, como la [evaluación del traspaso 2](../2026-10-24-formularios-evaluacion-traspaso-2.md), usan los de antes.

| Antes | Ahora | Cambió el alcance |
| --- | --- | --- |
| E13 | E12 · Catálogos | Sin formularios; nace el módulo |
| E13b | E13 · Campos de los catálogos | Solo lo del catálogo |
| E12 | E14 · Formularios | Suma las preguntas de catálogo, la oferta, la presentación propia de una pregunta y el sellado |
| E15 | E15 · Respuestas | Sin las fotos, las líneas ni el canal «web», que suman sus etapas |
| E13c | E16 · Datos de las personas y filtros | Entera, con el papel «Filtro» de un campo |
| E12b | E17 · Ciclo de vida y copiar | — |
| E14 | E18 · Fotos y documentos | Suma los archivos a la planilla y el detalle |
| E13d | E19 · Condiciones, cálculos y líneas | Suma lo suyo a la planilla, el detalle y el Excel |
| E14b | E20 · Cargar desde la web | — |
| E16 | E21 · Reglas, aprobación, PDF y avisos | — |
| E17 | E22 · Biblioteca | Sin la lectura de documentos de una plantilla |
| E18 | E23 · Asistente | Con `BA6` y las sugerencias |
| E18b | E24 · Varios asistentes | — |
| E19 | E25 · Documentos | Con la lectura de documentos de una plantilla |
