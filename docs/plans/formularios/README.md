# Formularios por WhatsApp: specs por etapa

Cada etapa del bloque tiene acá su especificación detallada (la 12, la 13 y la 17) o un resumen de una página (las demás), con la forma de los de ArquitecturaBase. Las decisiones de producto están en la [§5 de la hoja de ruta](../2026-10-12-formularios-dinamicos-hoja-de-ruta.md#5-decisiones-de-producto), y el alcance de cada etapa, en su §3. Se escriben antes de programar, para que al programar no haya que pensar de nuevo.

**Cómo se usa.** Cuando le toca a una etapa, [`/plan-etapa`](../../../.agents/skills/plan-etapa/SKILL.md) toma su spec y la verifica contra el código real, sobre todo los puntos de contacto con las etapas anteriores. Con eso arma el plan corto: contrato, back y front en paralelo, e integración y puerta.

**Qué no va acá:**
- **Código para copiar:** cada spec nombra el archivo real de este repo que sirve de modelo. ArquitecturaBase es referencia, no fuente de código.
- **La ficha de `docs/features/` y los `AGENTS.md` de cada carpeta:** nacen al cerrar la etapa, del código real. El detector de documentación los controla y no puede haber punteros a carpetas que no existen.

Esta carpeta está fuera del detector (`docs/plans/**`).

Las etapas van en el orden de las decisiones 34 y 54. Los tableros son los de la §5 de la [evaluación del traspaso 2](../2026-10-24-formularios-evaluacion-traspaso-2.md), y los dibuja Diseño.

| Orden | Etapa | Spec | Tableros |
| --- | --- | --- | --- |
| 1 | E12 · Formularios: crear, publicar, mandar y recibir | [e12-formularios.md](e12-formularios.md) | Aprobados en el lienzo v128. Se redibujan 4: `FRM-Editor`, `M-FRM-Editor`, `FRM-Publicar` y `FRM-Publicando` |
| 2 | E12b · Ciclo de vida | [e12b-ciclo-de-vida.md](e12b-ciclo-de-vida.md) | Por dibujar: `BA1-A` a `BA6` |
| 3 | E13 · Catálogos base | [e13-catalogos.md](e13-catalogos.md) | Aprobados en el lienzo v129 y v130. Se redibujan 5, y por dibujar `CA1-A` a `CA6` |
| 4 | E13b · Campos extra y qué viaja a WhatsApp | [e13b-campos-extra.md](e13b-campos-extra.md) | Por dibujar: `CE1`, `CE2` y `CE3-B` |
| 5 | E13c · Datos de las personas y filtros por destinatario | [e13c-datos-de-las-personas.md](e13c-datos-de-las-personas.md) | Por dibujar: `CE4-A` y el diálogo para definir un dato |
| 6 | E13d · Condiciones, cálculos y líneas | [e13d-condiciones-calculos-y-lineas.md](e13d-condiciones-calculos-y-lineas.md) | Por dibujar: `FRM-Editor-Condiciones`, `FRM-Editor-Calculo`, `FRM-Editor-Lineas` y `FRM-Respuesta-Lineas` |
| 7 | E14 · Fotos y documentos | [e14-fotos-y-documentos.md](e14-fotos-y-documentos.md) | Aprobados en el lienzo v130. Por dibujar `WA-Chats-Adjuntos`, para los archivos del chat (48) |
| 8 | E15 · «Formularios», planilla, detalle y Excel | [e15-planilla.md](e15-planilla.md) | Aprobados en el lienzo v131. Por dibujar `MF1`, `DC0`, `CE5` y `CE6`, y se ajusta `FRM-Respuesta` |
| 9 | E16 · Reglas, aprobación, PDF y avisos | [e16-reglas-y-aprobacion.md](e16-reglas-y-aprobacion.md) | Aprobados en el lienzo v132. Se redibujan 7, con la acción «Mandar el PDF a un cliente» (47) |
| 10 | E17 · Biblioteca de plantillas y formularios estándar | [e17-biblioteca.md](e17-biblioteca.md) | Aprobados en el lienzo v126, v130 y v134. Se redibujan 7, y por dibujar `DOC1` |
| 11 | E18 · El asistente, con Casos | [e18-asistente.md](e18-asistente.md) | Aprobados en el lienzo v133. Se redibuja `FRM-Editor-Asistente`, y por dibujar `AS1-A` a `AS6` |
| 12 | E19 · Documentos por WhatsApp | [e19-documentos.md](e19-documentos.md) | Por dibujar: `DOC2` a `DOC7-A` |
