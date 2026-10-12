# Formularios por WhatsApp: specs por etapa

Cada etapa del bloque tiene acá su especificación detallada. La escribimos mientras se terminan las etapas anteriores, para que al programar no haya que pensar de nuevo. La fuente de alcance y decisiones es la [hoja de ruta](../2026-10-12-formularios-dinamicos-hoja-de-ruta.md).

**Cómo se usa.** Cuando le toca a una etapa, [`/plan-etapa`](../../../.agents/skills/plan-etapa/SKILL.md) toma su spec y la verifica contra el código real, sobre todo los puntos de contacto con las etapas anteriores. Con eso arma el plan corto: contrato, back y front en paralelo, e integración y puerta.

**Qué no va acá:**
- **Código para copiar:** cada spec nombra el archivo real de este repo que sirve de modelo. ArquitecturaBase es referencia, no fuente de código.
- **La ficha de `docs/features/` y los `AGENTS.md` de cada carpeta:** nacen al cerrar la etapa, del código real. El detector de documentación los controla y no puede haber punteros a carpetas que no existen.

Esta carpeta está fuera del detector (`docs/plans/**`).

| Etapa | Spec | Estado |
| --- | --- | --- |
| 12 · Formularios: crear, publicar, mandar y recibir | [e12-formularios.md](e12-formularios.md) | Spec escrita |
| 13 · Catálogos | — | Después de la E12, con la etapa 2 de ArquitecturaBase cerrada |
| 14 · Fotos y documentos | — | Cuando ArquitecturaBase programe su etapa 2B |
| 15 · Planilla y Excel | — | Cuando ArquitecturaBase programe su etapa 3 |
| 16 · Reglas, aprobación y PDF | — | Cuando ArquitecturaBase programe su etapa 4 |
| 17 · Biblioteca de plantillas | — | Primero tableros y decisiones de producto: no tiene fuente en ArquitecturaBase |
