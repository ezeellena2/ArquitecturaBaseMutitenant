# Formularios por WhatsApp: specs por etapa

Cada etapa del bloque tiene acá su especificación detallada (la 12, la 14 y la 22) o un resumen de una página (las demás), con la forma de los de ArquitecturaBase. Las decisiones de producto están en la [§5 de la hoja de ruta](../2026-10-12-formularios-dinamicos-hoja-de-ruta.md#5-decisiones-de-producto), y el alcance de cada etapa, en su §3. Se escriben antes de programar, para que al programar no haya que pensar de nuevo.

**Cómo se usa.** Cuando le toca a una etapa, [`/plan-etapa`](../../../.agents/skills/plan-etapa/SKILL.md) toma su spec y la verifica contra el código real, sobre todo los puntos de contacto con las etapas anteriores. Con eso arma el plan corto: contrato, back y front en paralelo, e integración y puerta.

**La regla del orden** (decisión 78, que controla `/plan-etapa`):
- cada etapa usa solo lo que dejaron las anteriores;
- lo que suma se ve en todas las pantallas que ya existen: un tipo de pregunta nuevo, en la planilla, el detalle, el Excel y el PDF, si ya están;
- nada nombra, prueba ni dibuja algo de una etapa posterior: eso pasa a esa etapa.

**Qué no va acá:**
- **Código para copiar:** cada spec nombra el archivo real de este repo que sirve de modelo. ArquitecturaBase es referencia, no fuente de código.
- **La ficha de `docs/features/` y los `AGENTS.md` de cada carpeta:** nacen al cerrar la etapa, del código real. El detector de documentación los controla y no puede haber punteros a carpetas que no existen.

Esta carpeta está fuera del detector (`docs/plans/**`).

Las etapas van en el orden de la decisión 78, y **el número es el orden**. Antes de la 12 va la base del [plan de desarrollo](../2026-09-27-plan-de-desarrollo.md#etapas-12-a-25-formularios-por-whatsapp). Los tableros son los de la §5 de la [evaluación del traspaso 2](../2026-10-24-formularios-evaluacion-traspaso-2.md) y los que suman las decisiones 47 a 82, y los dibuja Diseño.

| Etapa | Spec | Tableros |
| --- | --- | --- |
| E12 · Catálogos | [12-catalogos.md](12-catalogos.md) | Aprobados en el lienzo v141 (front `93cb7d01`): `FRM-Catalogos`, `FRM-Catalogo-Nuevo`, `FRM-Catalogo` (`CA1-A`), `FRM-Catalogo-Importar` (`CA2`), `FRM-Catalogo-Tipo` (`CA3`), `FRM-Catalogo-Filtros` y `FRM-Catalogo-Contactos`, con sus teléfonos |
| E13 · Campos de los catálogos | [13-campos.md](13-campos.md) | Por dibujar: `CE1` y `CE2`, solo del catálogo. `CE3-B` es `FRM-Catalogo-13`, con «En WhatsApp» en la forma C, en el lienzo v145 |
| E14 · Formularios: crear, publicar, mandar y recibir | [14-formularios.md](14-formularios.md) | Aprobados en el lienzo v141 (front `93cb7d01`): `FRM-Formularios`, `FRM-Editor` (`CA4`), `FRM-Publicar`, `FRM-Publicando`, `WA-Chats-Formulario`, `WA-Chats-Respuesta`, `WA-Chats-Formulario-Cliente`, `WA-Mensajes-14` y `FRM-Catalogos-14`, con sus teléfonos. Se redibujan `FRM-Editor` y su teléfono, con «Agregar título» (88) |
| E15 · Respuestas: planilla, detalle, Excel y tablero | [15-respuestas.md](15-respuestas.md) | Aprobados en el lienzo v131. Por dibujar `MF1`, `DC0`, `CA5`, `CA6`, `CE5`, `CE6` y `DOC7-A` (el tablero, 57), y se ajusta `FRM-Respuesta` |
| E16 · Datos de las personas y filtros por destinatario | [16-datos-de-las-personas.md](16-datos-de-las-personas.md) | Por dibujar: `CE4-A`, el diálogo para definir un dato y el papel de un campo |
| E17 · Ciclo de vida y copiar | [17-ciclo-de-vida.md](17-ciclo-de-vida.md) | Por dibujar: `BA1-A` a `BA5` y `FRM-Copiar` |
| E18 · Fotos y documentos | [18-fotos-y-documentos.md](18-fotos-y-documentos.md) | Aprobados en el lienzo v130. Por dibujar `WA-Chats-Adjuntos`, para los archivos del chat (48), y los archivos en la planilla y el detalle |
| E19 · Condiciones, cálculos y líneas | [19-condiciones-calculos-y-lineas.md](19-condiciones-calculos-y-lineas.md) | Por dibujar: `FRM-Editor-Condiciones`, `FRM-Editor-Calculo`, `FRM-Editor-Lineas` y `FRM-Respuesta-Lineas` |
| E20 · Cargar una respuesta desde la web | [20-cargar-desde-la-web.md](20-cargar-desde-la-web.md) | Por dibujar: `FRM-Formularios-20`, `FRM-Cargar-Respuesta`, `FRM-Cargar-Panel`, `FRM-Cargar-Lista`, `FRM-Cargar-Archivos`, `FRM-Cargar-Enviada`, `FRM-Respuestas-20`, `FRM-Respuesta-20` y `FRM-Respuestas-Propias`, con sus teléfonos |
| E21 · Reglas, aprobación, PDF y avisos | [21-reglas-y-aprobacion.md](21-reglas-y-aprobacion.md) | Aprobados en el lienzo v132. Se redibujan 4, con «Mandar el PDF a un cliente» (47), y por dibujar el PDF con el logo, los datos y el color de la 82, `FRM-Respuesta-Corregir` (58) y `FRM-Respuestas-Propias-21` (85) |
| E22 · Biblioteca de plantillas y formularios estándar | [22-biblioteca.md](22-biblioteca.md) | Aprobados en el lienzo v126, v130 y v134. Se redibujan 12, con las reglas que llegan de una plantilla, y por dibujar `DOC1` sin la lectura |
| E23 · El asistente, con Casos | [23-asistente.md](23-asistente.md) | Aprobados en el lienzo v133. Se redibujan `FRM-Editor-Asistente`, `FRM-Editor-Sugerencias` y `FRM-Catalogo-Propuesta`, y por dibujar `AS1-A` a `AS6`, `BA6`, `WA-Modo-Atencion` y `WA-Preguntas-Sin-Respuesta` |
| E24 · Varios asistentes, uno por equipo | [24-varios-asistentes.md](24-varios-asistentes.md) | Por dibujar: `WA-Asistentes` y `WA-Equipo-Asistente` |
| E25 · Documentos por WhatsApp | [25-documentos.md](25-documentos.md) | Por dibujar: `DOC2` a `DOC6` y la sección de lectura de `DOC1` |

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
