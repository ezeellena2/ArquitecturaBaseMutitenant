# Formularios por WhatsApp: specs por etapa

Cada etapa del bloque tiene acá su spec, y todas van hacia el mismo [formato](plantilla.md): qué leer, pantallas, contrato, datos, pasos, qué verificar al arrancar y cómo se cierra. Las decisiones de producto están en la [§5 de la hoja de ruta](../2026-10-12-formularios-dinamicos-hoja-de-ruta.md#5-decisiones-de-producto), y el alcance de cada etapa, en su §3. Se escriben antes de programar, para que al programar no haya que pensar de nuevo.

**Cómo se sabe qué falta.** `node scripts/arneses/arnes.mjs alinear-etapas --detectar` no gasta tokens y dice qué etapa no está lista: secciones que le faltan, tableros que no coinciden con el lienzo, y permisos, decisiones, enlaces y estados que no cierran. No reemplaza leer las specs de punta a punta: una contradicción de sentido no la ve ningún script.

**Cómo se usa.** Cuando le toca a una etapa, [`/plan-etapa`](../../../.agents/skills/plan-etapa/SKILL.md) toma su spec y contrasta «Qué verificar al arrancar» con el código real. El contrato, los datos y los pasos ya están escritos: el plan corto anota solo lo que cambió, y suma el texto para cada chat.

**La regla del orden** (decisión 78, que controla `/plan-etapa`):
- cada etapa usa solo lo que dejaron las anteriores;
- lo que suma se ve en todas las pantallas que ya existen: un tipo de pregunta nuevo, en la planilla, el detalle, el Excel y el PDF, si ya están;
- nada nombra, prueba ni dibuja algo de una etapa posterior: eso pasa a esa etapa.

**Antes de dibujar o especificar una pantalla:** mirar cómo está en `../ArquitecturaBaseFront` (126). Sus escenas de `src/test/visual` muestran la pantalla real.

**Sin la E10 y sin la maqueta** (123 y 124):
- las etapas se programan y se cierran sin la E10. Lo que pide Meta con un número conectado, o la nube, queda pendiente en el plan de cada etapa, y se prueba junto cuando esté la I1;
- las pruebas con Meta que van antes de programar la 19, la 21 y la 25 no esperan la I1: se hacen con la cuenta de desarrollo, la de la prueba de la 11 (`WhatsApp:BusinessAccountId` y su número, en [configuración](../../operations/configuracion.md));
- lo que se ve sale del lienzo y cómo funciona, de las specs. Ninguna etapa copia de la maqueta.

**Qué no va acá:**
- **Código para copiar:** cada spec nombra el archivo real de este repo que sirve de modelo. ArquitecturaBase es referencia, no fuente de código.
- **La ficha de `docs/features/` y los `AGENTS.md` de cada carpeta:** nacen al cerrar la etapa, del código real. El detector de documentación los controla y no puede haber punteros a carpetas que no existen.

Esta carpeta está fuera de `realinear-docs` (`docs/plans/**`); la controla `alinear-etapas`.

Las etapas van en el orden de la decisión 78, y **el número es el orden**. Antes de la 12 va [la base](base-de-la-12.md), con su [parte de pasos](base-de-la-12-parte-pasos.md): redibuja los tableros del núcleo (`Usuarios`, `Roles`, `Cuentas`, `Organizaciones`, `Empresas`, `Empresa`, `Recuperaciones`, `Usuario`, `Campana` y `Campana-Plat`, con sus teléfonos, y `M-Usuario-Empresa`), los del bot (`Plat-WA-Bot`, `Plat-WA-Bot-Recorrido` y `M-Plat-WA-Bot`), `WA-Telefonos` con su teléfono y los ocho `Criterio-*`, y `Enlace` y `M-Enlace` pasan al Archivo (179). Los tableros son los de la §5 de la [evaluación del traspaso 2](../2026-10-24-formularios-evaluacion-traspaso-2.md) y los que suman las decisiones 47 a 135, y los dibuja Diseño.

**En la columna «Tableros»** cada estado va con su etiqueta, y `alinear-etapas` la compara con el lienzo:
- **Aprobados:** listos y al día.
- **A revisar:** dibujados antes de las reglas del 25 y el 26/10. Están en el lienzo, y Diseño los revisa con su etapa.
- **Se redibujan:** están en el lienzo y una decisión posterior los cambia.
- **Por dibujar:** todavía no existen.

| Etapa | Spec | Tableros |
| --- | --- | --- |
| E12 · Catálogos | [12-catalogos.md](12-catalogos.md), con sus partes de [datos y contrato](12-parte-datos-y-contrato.md) y de [archivos, tests y referencia](12-parte-archivos-tests-y-referencia.md) | Aprobados: `FRM-Catalogos`, `FRM-Catalogo`, `FRM-Catalogo-Opciones`, `FRM-Catalogo-Filtros` y `FRM-Catalogo-Contactos`, con sus teléfonos, y `FRM-Catalogo-Nuevo` y `FRM-Catalogo-Tipo`, en el lienzo v150, y `FRM-Catalogo-Importar`, redibujado con el traspaso 5 en el lienzo v162 |
| E13 · Campos de los catálogos | [13-campos.md](13-campos.md) | Aprobados: `FRM-Catalogo-13`, `FRM-Catalogo-Campos` y `FRM-Catalogo-Opciones-13`, con sus teléfonos, en el lienzo v150 |
| E14 · Formularios: crear, publicar, mandar y recibir | [14-formularios.md](14-formularios.md), con sus partes de [decisiones y flujos](14-parte-decisiones-y-flujos.md), [datos y contrato](14-parte-datos-y-contrato.md) y [archivos, tests y Meta](14-parte-archivos-tests-y-meta.md), y el [texto para Diseño](14-texto-diseno.md) | Aprobados: `FRM-Formularios`, `FRM-Catalogo-14`, `FRM-Catalogos-14` y `Campana-14`, con sus teléfonos, y `FRM-Publicar`, en el lienzo v150, y `FRM-Editor`, redibujado con el traspaso 5 en el lienzo v162. Se redibujan: `FRM-Publicando` y `M-FRM-Editor`, con el estado una vez y sin la vista oficial al costado (175 y 176); `WA-Chats-Formulario` y `WA-Chats-Formulario-Cliente`, con sus teléfonos, y `WA-Chats-Respuesta`, `M-WA-Conversacion-Respuesta`, `WA-Mensajes-14` y `M-WA-Mensaje-14`, con el mensaje que nombra el formulario (165). Por dibujar: `FRM-Vista-Oficial`, con su teléfono, la vista oficial en un diálogo (176) |
| E15 · Respuestas: planilla, detalle, Excel y tablero | [15-respuestas.md](15-respuestas.md), con su [parte de datos y contrato](15-parte-datos-y-contrato.md) | Aprobados: `FRM-Formularios-15` y `FRM-Tablero`, con sus teléfonos, y `FRM-Respuestas-Exportar`, en el lienzo v150, y `FRM-Respuestas` y `FRM-Respuesta`, con sus teléfonos, y `FRM-Respuestas-Filtros` y `FRM-Respuesta-Completa`, redibujados con el traspaso 5 en el lienzo v160 |
| E16 · Datos de las personas y filtros por destinatario | [16-datos-de-las-personas.md](16-datos-de-las-personas.md), con su [parte de datos y contrato](16-parte-datos-y-contrato.md) | Aprobados: `FRM-Datos-Personas` y `FRM-Catalogo-16`, con sus teléfonos, en el lienzo v150, y `FRM-Respuestas-16`, con su teléfono, redibujado con el traspaso 5 en el lienzo v162. Se redibujan: `FRM-Publicar-16`, con el aviso de opciones solo cuando no entra o da cero (157); `FRM-Editor-16`, con su teléfono, con «Agregar título» (88) y un solo «Agregar filtro ▾» (134); `FRM-Usuario-Datos`, con su teléfono, con «Agregar empresa»; y `WA-Chats-Formulario-16`, con su teléfono, con el mensaje que nombra el formulario (165) |
| E17 · Ciclo de vida y copiar | [17-ciclo-de-vida.md](17-ciclo-de-vida.md) | Aprobados: `FRM-Formularios-17`, con su teléfono, y `FRM-Dar-De-Baja`, `FRM-Reactivar-Eliminar` y `FRM-Copiar`, en el lienzo v151. Se redibujan: `FRM-Editor-Baja`, con su teléfono, con la baja en una frase y la etiqueta al reactivar (175) y «Agregar título» (88) |
| E18 · Fotos y documentos | [18-fotos-y-documentos.md](18-fotos-y-documentos.md) | Aprobados: `FRM-Editor-18`, `WA-Chats-Respuesta-18`, `WA-Chats-Visor` y `WA-Chats-Adjuntos`, con sus teléfonos, y `FRM-Publicar-18` y `FRM-Dar-De-Baja-18`, en el lienzo v152, y `FRM-Respuestas-18` y `FRM-Respuesta-18`, con sus teléfonos, y `FRM-Respuesta-Completa-18`, redibujados con el traspaso 5 en el lienzo v163 |
| E19 · Condiciones, cálculos y líneas | [19-condiciones-calculos-y-lineas.md](19-condiciones-calculos-y-lineas.md), con su [parte de contrato, datos y pasos](19-parte-contrato-datos-y-pasos.md) | Aprobados: `FRM-Tablero-19`, con su teléfono, en el lienzo v153. Se redibujan: `FRM-Editor-Condiciones` y `FRM-Editor-Calculo`, con sus teléfonos, sin el «Grupo de líneas» (177); `FRM-Editor-Lineas`, `FRM-Respuestas-19`, `FRM-Respuesta-19` y `WA-Chats-Respuesta-19`, con sus teléfonos, y `FRM-Publicar-19`, con lo del traspaso 5, «Agregar bloque», «ítem», el catálogo del bloque con sus filtros, «Pasan al bloque», el mínimo de más de 10 que frena y la columna del bloque entera (168, 177, 178 y 185) |
| E20 · Cargar una respuesta desde la web | [20-cargar-desde-la-web.md](20-cargar-desde-la-web.md), con su [parte de contrato, datos y pasos](20-parte-contrato-datos-y-pasos.md) y el [texto para Diseño](20-texto-diseno.md) | Aprobados: `FRM-Cargar-Archivos`, con su teléfono, en el lienzo v154. Se redibujan: `FRM-Cargar-Respuesta`, `FRM-Respuestas-20`, `FRM-Respuestas-Propias` y `FRM-Respuesta-20`, con sus teléfonos, y `FRM-Cargar-Panel`, con lo del traspaso 5; `FRM-Formularios-20`, con su teléfono, con «Mis cargas» y «Mi última carga» (153); y, con el traspaso 10, el bloque con «Agregar ítem» y «Ítem 2» en la banda (177 y 183), la zona plegada con «3 de 5 respondidas», «Elegí primero «Zona»» en `FRM-Cargar-Lista`, «Volver a Formularios» en `FRM-Cargar-Enviada`, `FRM-Editor-Listas` sin el «Grupo de líneas» y `FRM-Publicar-20` con el mínimo de más de 10 solo para la app (166, 177 y 178), con sus teléfonos. Por dibujar: `WA-Chats-Formulario-20`, con su teléfono, con los formularios solo para la app apagados y su motivo en «Enviar formulario» (166 y 184) |
| E21 · Reglas, aprobación, PDF y avisos | [21-reglas-y-aprobacion.md](21-reglas-y-aprobacion.md), con sus partes de [datos y contrato](21-parte-datos-y-contrato.md), [corregir y anular](21-parte-corregir-y-anular.md) y [pasos y verificar](21-parte-pasos-y-verificar.md), y el [texto para Diseño](21-texto-diseno.md) | Aprobados: `FRM-Tablero-21` y `Campana-21`, con sus teléfonos, y `FRM-Pdf` y `FRM-Dar-De-Baja-21`, en el lienzo v155. Se redibujan: `FRM-Editor-Reglas`, `FRM-Respuestas-21`, `FRM-Respuesta-Aprobacion`, `FRM-Respuestas-Propias-21` y `FRM-Cargar-Respuesta-21`, con sus teléfonos, con lo de los traspasos 5 y 9; y, con el traspaso 10, `FRM-Respuesta-Corregir`, `FRM-Respuesta-Anular` y `FRM-Respuestas-Propias-21` (166), `FRM-Formularios-21` (153), `FRM-Editor-21` (sin el «Grupo de líneas», 177), `WA-Chats-Avisos` (un aviso con el bloque, 177) y `FRM-Respuesta-Aprobacion` («¿Aprobar VIS-000003?», 161), con sus teléfonos, y `FRM-Editor-Baja-21`, con lo de la E17 (88 y 175). Por dibujar: `Cuenta-21`, con su teléfono (159 y 170), y `FRM-Editor-Reglas-Bloque` y `WA-Chats-Respuesta-21`, con sus teléfonos (177 y 166) |
| E22 · Biblioteca de plantillas y formularios estándar | [22-biblioteca.md](22-biblioteca.md), con su [parte de datos y contrato](22-parte-datos-y-contrato.md) | Aprobados: `FRM-Formularios-22`, `FRM-Biblioteca`, `FRM-Plantilla` y `FRM-Plantilla-Usar`, con sus teléfonos, y `Plat-FRM-Biblioteca` (con su teléfono), `Plat-FRM-Plantilla-Nueva`, `Plat-FRM-Pdf`, `Plat-FRM-Retirar` y `Plat-FRM-Categorias`, en el lienzo v156. Se redibujan: `FRM-Editor-Reglas-Plantilla`, con su teléfono, y `Plat-FRM-Plantilla-Reglas`, con las reglas en pasos y el historial detrás de su botón (traspaso 5), y `Plat-FRM-Plantilla` y `Plat-FRM-Publicar`, sin el renglón «Estado» (175) y con «Agregar bloque» (177) |
| E23 · El asistente, con Casos | [23-asistente.md](23-asistente.md), con su [parte de datos y contrato](23-parte-datos-y-contrato.md) | Aprobados: `WA-Asistente`, `WA-Asistente-Casos`, `WA-Modo-Atencion` y `WA-Preguntas-Sin-Respuesta`, con sus teléfonos; `WA-Chats-Sugerencia`, `FRM-Editor-Asistente`, `FRM-Faltan`, `FRM-Dar-De-Baja-23`, `FRM-Editor-Baja-23`, `Campana-23` y `Plat-Asistente-Config-23`, en el lienzo v157. Se redibujan: `WA-Asistente-Probar`, `WA-Chats-Casos`, `WA-Chats-Asistente`, `WA-Chats-Audio`, `FRM-Editor-Sugerencias`, `FRM-Editor-Reglas-23` y `FRM-Respuesta-Cadena`, con sus teléfonos, con lo prellenado en dos columnas, las reglas y las sugerencias en pasos y el detalle alineado (traspaso 5), y la oferta que nombra el formulario (165); y `WA-Asistente-Limites`, con su teléfono, con la información del negocio de allá (169). `FRM-Catalogos-23` y `FRM-Catalogo-Propuesta` pasan al Archivo: el catálogo propuesto es de la E25 (167) |
| E24 · Varios asistentes, uno por equipo | [24-varios-asistentes.md](24-varios-asistentes.md) | Aprobados: `WA-Asistentes`, `WA-Equipo-Asistente` y `WA-Chats-Casos-24`, con sus teléfonos, en el lienzo v158 |
| E25 · Documentos por WhatsApp | [25-documentos.md](25-documentos.md), con sus partes de [datos y contrato](25-parte-datos-y-contrato.md) y de [pasos](25-parte-pasos.md), y el [texto para Diseño](25-texto-diseno.md) | Por dibujar: `WA-Chats-Documento`, `FRM-Documentos`, `FRM-Editor-Lectura`, `FRM-Armar-Con-IA`, `FRM-Respuesta-25`, `Campana-25` y `Asistente-Org-25`, con sus teléfonos, `M-WA-Flow-Prellenado`, `M-WA-Armar-Con-IA`, `Plat-FRM-Plantilla-Lectura`, `WA-Asistente-Casos-25`, y `FRM-Formularios-25`, `FRM-Catalogos-25` y `FRM-Faltan-25` (154 a 156), y `FRM-Catalogo-Propuesta-25` (167) y `FRM-Plantilla-25`, con su teléfono (traspaso 10) |

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
| E18 | E23 · Asistente | Con las sugerencias |
| E18b | E24 · Varios asistentes | — |
| E19 | E25 · Documentos | Con la lectura de documentos de una plantilla |
