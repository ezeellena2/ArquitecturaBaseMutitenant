# 07 · Pendientes y decisiones posteriores

Estado al 2026-10-17 en ArquitecturaBase.

## Estado de las etapas

| Etapa | Estado |
| --- | --- |
| 1 · Crear, publicar, mandar y recibir | Cerrada el 2026-10-11, con piloto real con Meta. |
| 2 · Catálogos a mano y con filtros | Cerrada el 2026-10-15, con piloto real con Meta (incluido un catálogo de 200 opciones con detalle). |
| 2B · Fotos y documentos | Cerrada el 2026-10-15, con Meta y Azure privado reales. |
| 3 · Planilla, detalle y Excel | Cerrada el 2026-10-16, con respuestas reales y el XLSX abierto en Excel. |
| 4 · Reglas, aprobación, PDF y avisos | Implementada. Piloto real en español el 2026-10-17: aviso al recibir, aprobación con otra cuenta y PDF con foto en el celular. Faltan el inglés y la caída al idioma original. |
| 5 · Asistente, sugerencias y audios | En curso: primero los arreglos del review de la Etapa 4, después el detalle D2 y después el asistente. Tableros H1–H7 y s1–s4 elegidos, ADR 0020 aceptado, OpenAI elegido y la clave cargada. Falta calificar el perfil del modelo. |

## Decidido para después de la Etapa 5

1. **El idioma de los formularios (decidido el 2026-10-16), primero de todo.**
   - **Qué cambia:** solo el idioma original es obligatorio para guardar y publicar, y la traducción es opcional. Si no está completa, el formulario, el PDF, los mensajes y las opciones de catálogo salen enteros en el idioma original, sin mezclar.
   - **Qué hay que tocar:** la regla de la Etapa 1 (publicar exigía es/en completos), la publicación en Meta (solo los idiomas completos), el envío y los catálogos. Antes se ajustan los tableros del editor y de publicar.
   - **Lo que no cambia:** el idioma de las pantallas del sistema. Varios países y más idiomas quedan para después.
   - **Para acá:** la hoja de ruta todavía dice «textos en español e inglés»; conviene empezar directamente con esta regla.
2. **Detalle de una respuesta D2 (elegido el 2026-10-17).** Reemplaza el detalle de las Etapas 3 y 4 por un panel al costado de la planilla. Se programa dentro de la Etapa 5, antes del asistente. Disposición en [05 · Pantallas](05-pantallas.md).

## Ideas y pendientes, sin orden fijo

Cada uno necesita su tablero y su decisión antes de programarse.

- **Catálogo con una consulta SQL (K5):** solo si una lista no se puede armar con filtros (ADR 0019).
- **Intercambio cifrado con Meta:** búsqueda en vivo, listas dependientes y listas de más de 200 opciones.
- **Grupos y remitos con líneas:** preguntas que se repiten.
- **Corregir una respuesta:** una respuesta nueva que reemplaza a la anterior.
- **Cargar desde la web e importar desde Excel:** después de la Etapa 4, con su tablero.
- **Borrador de formulario armado por la IA** a partir de los pedidos de formularios que faltan, siempre revisado y publicado por una persona.
- **Copiar, eliminar y deprecar:** copiar y eliminar definiciones (solo las nunca publicadas), y deprecar en Meta los Flows de versiones reemplazadas cuando no queden sesiones vigentes.
- **Retención de respuestas:** hoy las respuestas, los valores, los recibos y las versiones se conservan sin plazo. Hay que decidirla por negocio antes de producción; no hereda los 90 días de WhatsApp.
- **Plantillas de Meta** para escribir fuera de las 24 horas. Hoy un aviso fuera del plazo queda «No se pudo» y el PDF sigue guardado.
- **Antivirus del almacenamiento,** por ejemplo Defender for Storage: requisito antes de producción.
- **Descargar todas las fotos juntas:** idea anotada, no existe.
- **Guía para quitar el módulo de formularios:** falta.
- **Deuda menor de la Etapa 1,** que se arregla al tocar esos archivos (listada en `docs/plans/README.md` de ArquitecturaBase).

## Lo que este proyecto suma y ArquitecturaBase no tiene

- **Biblioteca de plantillas de la plataforma (F3).** La organización la toma como copia independiente.
- **Formularios por organización,** con la empresa de quien respondió en cada respuesta y la planilla filtrable por empresa. Formularios y permisos por empresa, después.
- **B2C** después de F7: personas que completan formularios de una organización y datos compartidos.
- **Feature `forms` por organización y costos de IA dentro del tope mensual** de cada organización.
