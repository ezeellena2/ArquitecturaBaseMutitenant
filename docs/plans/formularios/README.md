# Formularios por WhatsApp: specs por etapa

Cada etapa del bloque tiene acá su especificación detallada (la 12, la 13 y la 17) o un resumen de una página (la 14, la 15, la 16 y el asistente), con la forma de los de ArquitecturaBase. Las decisiones de producto están en la [§5 de la hoja de ruta](../2026-10-12-formularios-dinamicos-hoja-de-ruta.md#5-decisiones-de-producto). La escribimos mientras se terminan las etapas anteriores, para que al programar no haya que pensar de nuevo. La fuente de alcance y decisiones es la [hoja de ruta](../2026-10-12-formularios-dinamicos-hoja-de-ruta.md).

**Cómo se usa.** Cuando le toca a una etapa, [`/plan-etapa`](../../../.agents/skills/plan-etapa/SKILL.md) toma su spec y la verifica contra el código real, sobre todo los puntos de contacto con las etapas anteriores. Con eso arma el plan corto: contrato, back y front en paralelo, e integración y puerta.

**Qué no va acá:**
- **Código para copiar:** cada spec nombra el archivo real de este repo que sirve de modelo. ArquitecturaBase es referencia, no fuente de código.
- **La ficha de `docs/features/` y los `AGENTS.md` de cada carpeta:** nacen al cerrar la etapa, del código real. El detector de documentación los controla y no puede haber punteros a carpetas que no existen.

Esta carpeta está fuera del detector (`docs/plans/**`).

| Etapa | Spec | Estado |
| --- | --- | --- |
| 12 · Formularios: crear, publicar, mandar y recibir | [e12-formularios.md](e12-formularios.md) | Spec escrita; tableros de la v122, que Diseño rehace con la disposición de ArquitecturaBase (decisión 18) |
| 13 · Catálogos | [e13-catalogos.md](e13-catalogos.md) | Spec escrita; tableros de la v124, que Diseño rehace con la disposición de ArquitecturaBase (decisión 18) |
| 14 · Fotos y documentos | [e14-fotos-y-documentos.md](e14-fotos-y-documentos.md) | Resumen de una página (2026-10-17), desde el traspaso |
| 15 · Planilla y Excel | [e15-planilla.md](e15-planilla.md) | Resumen de una página (2026-10-17), con el detalle D2 |
| 16 · Reglas, aprobación y PDF | [e16-reglas-y-aprobacion.md](e16-reglas-y-aprobacion.md) | Resumen de una página (2026-10-17) |
| 17 · Biblioteca de plantillas | [e17-biblioteca.md](e17-biblioteca.md) | Spec escrita; tableros en el lienzo v126; las reglas de ejemplo esperan la spec de la E16 |
| El asistente en WhatsApp | [asistente.md](asistente.md) | Resumen de una página (2026-10-17). Se diseña después de los tableros de la 16 y se programa después del bloque |
