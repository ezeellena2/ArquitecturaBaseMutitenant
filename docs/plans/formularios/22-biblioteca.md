# E22 · Biblioteca de plantillas de la plataforma (spec)

**Estado:** pendiente · spec de la etapa 22 del bloque (decisiones 34, 40, 78, 79, 175, 177 y 220). **Es también la biblioteca de formularios estándar** (40). Se arma con las piezas de este repo y las de las etapas anteriores. `/plan-etapa` la convierte en plan cuando le toque, después de verificar los puntos marcados **[E14]**, **[E12]** y **[E21]** contra el código.

**Fuentes:**
- alcance y decisiones: [hoja de ruta](../2026-10-12-formularios-dinamicos-hoja-de-ruta.md), §E22 y decisiones 2, 13 a 16, 30 a 33, 35, 40, 45, 50, 59, 61, 62, 64, 138, 149, 175 y 177;
- reglas: [AGENTS.md](../../../AGENTS.md), las [fichas](../../rules/README.md) y la [ADR 0050](../../decisions/0050-auditoria-global-de-modulos.md) (auditoría de lo global);
- las specs de la [E12](12-catalogos.md), la [E14](14-formularios.md), la [E17](17-ciclo-de-vida.md), la [E19](19-condiciones-calculos-y-lineas.md), la [E20](20-cargar-desde-la-web.md) y la [E21](21-reglas-y-aprobacion.md).

**Partes.** Esta spec no entra en un solo archivo, así que sigue en otros, con los mismos números de sección. Lo que no está en la tabla está acá.

| Secciones | Archivo |
| --- | --- |
| §2, §3, §4, §5, §6 y §7 | [datos y contrato](22-parte-datos-y-contrato.md) |

## Qué leer

Antes de empezar:
1. **Lo anterior:** las specs de la [E12](12-catalogos.md), la [E14](14-formularios.md) y la [E21](21-reglas-y-aprobacion.md), sobre todo el borrador, los catálogos, las reglas y el diseño del PDF que se copian; la [E17](17-ciclo-de-vida.md), copiar; la [E19](19-condiciones-calculos-y-lineas.md), las fórmulas y el bloque, y la [E20](20-cargar-desde-la-web.md), lo que deja un formulario solo para la app.
2. **Decisiones** de la hoja de ruta: 2, 13 a 16, 30 a 33, 35, 40, 45, 50, 59, 61, 62, 64, 138, 149, 175 y 177.
3. **Fichas:** [persistencia-ef](../../rules/persistencia-ef.md), [multitenancy](../../rules/multitenancy.md), [auditoria](../../rules/auditoria.md), [guardado](../../rules/guardado.md), [concurrencia](../../rules/concurrencia.md), [idempotencia](../../rules/idempotencia.md), [api-http](../../rules/api-http.md), [permisos](../../rules/permisos.md), [paginado-y-busqueda](../../rules/paginado-y-busqueda.md), [result-y-errores](../../rules/result-y-errores.md), [validacion](../../rules/validacion.md), [textos-libres](../../rules/textos-libres.md), [textos-y-traducciones](../../rules/textos-y-traducciones.md), [logs](../../rules/logs.md), [modulos](../../rules/modulos.md) y [tests](../../rules/tests.md). Además, la [ADR 0050](../../decisions/0050-auditoria-global-de-modulos.md).
4. **Código para copiar:** pieza por pieza, en la §7.
5. **Pantallas:** los tableros de la §8.

## 1. Alcance

**Entra:**
- **Plataforma:**
  - **Plantillas:** cada una trae un formulario (preguntas, botón y prefijo sugerido), sus catálogos (a mano, con su tipo de valor y sus campos; y de los miembros o de los contactos, sin filtros, 64), sus reglas de ejemplo y el diseño del PDF (ver [«Reglas de ejemplo»](22-parte-datos-y-contrato.md#reglas-de-ejemplo-y-diseño-del-pdf)). Tiene categoría y descripción. Cada texto es uno solo, sin idioma (35).
  - **Edición:** borrador y publicar, con versiones. Publicar y retirar piden motivo.
  - **Categorías:** las arma la plataforma.
  - **Uso:** solo la cantidad de organizaciones que usaron cada plantilla.
- **Organización:**
  - con la feature `forms`, ve la biblioteca y una vista de cada plantilla;
  - con «Usar esta plantilla», obtiene una **copia independiente**: un formulario en borrador con sus catálogos nuevos, el diseño del PDF y su prefijo, y las reglas de ejemplo precargadas hasta que guarde las suyas. Nunca se publica sola.
- **Permiso de plataforma:** `platform.formtemplates.manage`.

**No entra:**
- avisar «hay una versión nueva» o actualizar una copia: queda para después, si se pide;
- filtros en los catálogos de una plantilla: los de los miembros y de los contactos llegan sin filtros, y la organización los ajusta (64). Tampoco los filtros por destinatario de la E16, que dependen de los datos de las personas de cada organización;
- lo que en la E20 deja un formulario solo para la app: con WhatsApp instalado, publicar la plantilla frena una lista dependiente, «elegir varias» de más de 20 opciones o un bloque con un mínimo de más de 10 ítems (B8);
- plantillas de una organización para otra;
- la «Lectura de documentos» de una plantilla, que llega con los documentos (E25);
- los estándares que aporta un módulo, como la «Factura» de ARCA, con sus preguntas atadas con candado y sus reglas activas (220): conviven con estas plantillas, los trae el módulo ARCA en su segunda parte, y esta etapa no cambia por eso;
- saber qué organizaciones usaron una plantilla (decisión 16);
- borrar plantillas: se retiran.

## 8. Pantallas (tableros)

Valen los criterios de la E14 y la decisión 43. Datos de ejemplo:
- **Categorías:** Ventas, Logística y Mantenimiento, más Calidad, sin plantillas.
- **Plantillas:**
  - «Visita a cliente» (Ventas, para empleados): publicada, con cambios guardados sin publicar, que la usaron 12 organizaciones. Tiene 8 preguntas, dos catálogos (Clientes, de los contactos, y Motivos de visita, a mano) y cuatro reglas de ejemplo;
  - «Control de vehículo» (Logística): borrador;
  - «Orden de trabajo» (Mantenimiento): retirada;
  - «Comprobante de transferencia» (Ventas, para clientes): publicada.

Con el traspaso 5 se redibujaron `FRM-Editor-Reglas-Plantilla`, `M-FRM-Reglas-Plantilla` y `Plat-FRM-Plantilla-Reglas`: las reglas se leen en pasos (138) y el mensaje se arma con «Agregar dato» (149); en la organización, el historial va detrás de su botón (140), y en Plataforma no hay, porque las reglas se guardan con el borrador. Con el traspaso 10 se redibujaron `Plat-FRM-Plantilla` y `Plat-FRM-Publicar`: la pestaña «Publicación» pierde el renglón «Estado», que repite la banda (175), y las preguntas de la plantilla suman «Agregar bloque», como en el editor de la 19 (177). Todos están aprobados y publicados en el lienzo v168.

**Valen las de toda la app** (160 a 164, 173 a 175 y 180): lo que carga no salta, decidir a propósito, ninguna elección se descarta en silencio, mucho texto con jerarquía, sin parecer plantilla y con la escala de ArquitecturaBase, el texto largo entero y el estado dicho una vez.

**Aprobados y publicados** en el lienzo v156, con las piezas de la 14 y la 21 ya alineadas a ArquitecturaBase, que no tiene biblioteca, y los recorridos guiados `armar-una-plantilla` y `usar-una-plantilla`:

| Tablero | Qué muestra | Estados |
| --- | --- | --- |
| `Plat-FRM-Biblioteca` y `M-Plat-FRM-Biblioteca` | listado: nombre, categoría, estado (Borrador, Publicada o Retirada), «La usaron» y la última publicación; la caja de filtros de las listas, con Estado y Categoría; «Nueva plantilla». En el teléfono es solo para ver | menú de un filtro, borradores, sin resultados, cargando, vacía y error |
| `FRM-Formularios-22` y `M-FRM-Formularios-22` | la lista de la 21, con «Cargar», y «Nuevo formulario ▾» con «Desde cero» y «Desde una plantilla» (79); en el teléfono, el «+» pregunta lo mismo | los de la 21 |
| `Plat-FRM-Plantilla-Nueva` | diálogo con el nombre y la categoría | vacío, faltan los datos (se marcan al tocar «Crear plantilla») y creando |
| `Plat-FRM-Plantilla` | ficha con pestañas **Plantilla**, **Catálogos** y **Publicación**. **Plantilla** es el editor de la 14: nombre, categoría, prefijo sugerido, para quién es (50), texto del botón y descripción; las preguntas con ↑ ↓ ✎ 🗑, que se abren en el lugar, con «Agregar pregunta» y «Agregar bloque» (177), y debajo las reglas de ejemplo (33), con el teléfono al costado. En la banda, Descartar, Guardar borrador, Publicar y el ⋮ con «Retirar», en rojo. **Catálogos:** una fila por catálogo, con su origen (64), y cada uno se abre aparte. **Publicación:** en grilla, «La usaron» y la última publicación, o quién la retiró y el motivo, sin el renglón «Estado», que ya dice la banda (175); debajo, las versiones, con número, fecha, operador y motivo | con cambios, para quién es, guardando, borrador guardado, otra persona guardó, descartar, catálogos, un catálogo a mano, uno de los contactos, publicación (con su menú, borrador y retirada) |
| `Plat-FRM-Plantilla-Reglas` | «Cuando llega una respuesta» de la 21 debajo de las preguntas, sin «Guardar reglas» ni historial: se guarda con el borrador. Lo de cada organización va con candado, «Lo elige cada organización»: el rol, con «Rol sugerido» como texto, la persona, la dirección y la plantilla de WhatsApp (61 y 62) | editar una regla, mandar por correo, avisar a un rol y a una persona, una regla con errores, guardando, borrador guardado y sin reglas |
| `Plat-FRM-Pdf` | el diseño del PDF de la 21, desde «Ver el diseño», con «Descartar» y «Guardar borrador». El color va con candado, y el encabezado y la vista previa muestran «Logo» y «Nombre de la organización» como marcadores (82) | estilo moderno, armando la vista previa, guardando, borrador guardado y salir sin guardar |
| `Plat-FRM-Publicar` y `Plat-FRM-Retirar` | diálogos con el motivo. Publicar suma la comprobación de la 14: «Problemas» por pregunta, catálogo o regla, con «Publicar versión 3» grisado, y «Qué se publica». Detrás, la pestaña «Publicación», sin el renglón «Estado» (175) | sin problemas, con problemas, falta el motivo, primera publicación, volver a publicar, comprobando, publicando, otra persona guardó; retirando |
| `Plat-FRM-Categorias` | lista con orden y ↑ ↓ ✎ 🗑 a la vista (127); el alta, la edición, subir y borrar piden motivo | una en uso: «Borrar» grisado, con el motivo en su tooltip; nueva, faltan los datos, creando, editar, subir, borrar y vacía |
| `FRM-Biblioteca` y `M-FRM-Biblioteca` | la biblioteca de formularios estándar (40): tarjetas por categoría, con un buscador, y en qué formulario propio se usó cada plantilla: «La usaste en «Visita a cliente» y 1 más» (198) | sin resultados, sin plantillas, cargando y error |
| `FRM-Plantilla` y `M-FRM-Plantilla` | la vista de una plantilla: la descripción y sus datos, las preguntas, los catálogos que trae, las reglas de ejemplo en pasos, como en la E21, con los datos como etiquetas (138 y 149), «el rol que elegís al usarla» y el sugerido, y el teléfono; «Usar esta plantilla» | sin permiso de catálogos, cargando y ya no está |
| `FRM-Plantilla-Usar` | diálogo con el nombre y el prefijo propuesto; al confirmar, abre el formulario nuevo | prefijo tomado: se propone VIS2, con el porqué en la (i); falta el nombre, creando y se retiró |
| `FRM-Editor-Reglas-Plantilla` y `M-FRM-Reglas-Plantilla` | el formulario nuevo, en borrador, y en su pestaña «Reglas» las reglas precargadas, sin guardar: «Elegí…» en el rol, la persona, la dirección y la plantilla de WhatsApp, con «Rol sugerido» debajo. «Historial» queda apagado, con «Todavía no se guardaron: vienen de la plantilla.» en su tooltip (140) | elegir el rol, guardar sin elegir (se marca lo que falta, 43), una pregunta borrada (marcada en la condición, y en el mensaje como «Pregunta no disponible», 149), guardando y reglas guardadas |
| `Cuenta-Plat` (ajuste) | los permisos del operador suman «Administrar la biblioteca de formularios» | — |

**Corregidos en el lienzo v169,** porque contradecían una decisión: las reglas de ejemplo de `FRM-Plantilla` y su teléfono, en pasos y con los datos como etiquetas (138 y 149); el orden de «Agregar bloque» y «Agregar pregunta» en `Plat-FRM-Plantilla`; los filtros sin cantidades de `M-FRM-Formularios-22` (144), y `Cuenta-Plat` y su teléfono, sin «Administrar la biblioteca de formularios» (78). Esa fila la suma esta etapa en la cuenta del operador, sin tablero propio.

Sin «Textos en», sin el inglés obligatorio, sin «Cambios sin guardar» ni «Cambios sin publicar», sin el número de versión en la lista ni en la banda, y sin textos junto a los botones (35 y 43). La lectura de documentos no va en esta etapa.

**Mandan sobre esta spec en:**
- **El estado de una plantilla** (ajuste de la Guía del 2026-10-24, con la decisión 43): la lista y la banda dicen solo «Borrador», «Publicada» o «Retirada». «Publicar» habilitado ya dice que hay cambios para publicar, y el número de versión va en el historial de versiones de «Publicación».
- **Publicación de una plantilla:**
  - la grilla dice cuántas organizaciones la usaron y la última publicación, sin repetir el estado, que ya dice la banda (175), y debajo van las versiones (número, fecha, operador y motivo);
  - en borrador dice «Todavía no se publicó.»;
  - una retirada muestra quién la retiró, el motivo y «Volver a publicar».
- **Publicar:** el motivo y la comprobación. Muestra qué cambia, o los problemas por pregunta, catálogo o regla, con «Publicar» grisado. La primera publicación es la versión 1, y volver a publicar sin cambios repite la misma.
- **Las reglas y el diseño del PDF en Plataforma** se guardan con el borrador: van en `FormTemplateDraft` (`rules[]` y `pdfDesign`), con el mismo `PUT …/draft`, su versión y el 409. «Guardar borrador» de `Plat-FRM-Pdf` es ese mismo guardado. Los problemas de las reglas al publicar son `Forms.Template.Invalid` con el campo `rules[i]…` (B8).
- **Las reglas precargadas en la organización:** «Guardar reglas» queda habilitado; al tocarlo sin elegir lo que pide cada acción, se marcan (43). El servidor valida con la regla de la E21. Se ven y se guardan con `formrules.manage`; sin ese permiso, la sección muestra su estado «sin permiso». Una que cita respuestas, adjunta el PDF o manda una copia pide además `formrecords.read` al guardarla, con la regla de la E21 (F5 de ArquitecturaBase): «Usar esta plantilla» no guarda reglas (30), así que el control cae en «Guardar reglas».
- **«Logo» y «Nombre de la organización»** van como marcadores, porque la plataforma no ve datos de ninguna organización.
- **Usar una plantilla:**
  - se pide el nombre y se propone el prefijo; si VIS está tomado, se propone VIS2, y la (i) del prefijo dice por qué;
  - si la plantilla se retiró en el medio, avisa y vuelve a la biblioteca;
  - sin permiso de catálogos, «Usar esta plantilla» queda grisado, con el motivo en su tooltip (43).

**Decisiones del usuario** sobre los tableros:
- **Plataforma:** «Formularios» es un grupo de Administración, con Biblioteca y Categorías.
- **Pestaña Catálogos de la plantilla:** una fila por catálogo, con su origen y la pregunta que lo usa. Cada catálogo se abre aparte.
- **Retirar:** va en el ⋮ de la banda, en rojo. El diálogo pide motivo y dice que las copias ya hechas no cambian.
- **Categorías:** la lista con orden y ↑ ↓ ✎ 🗑 a la vista, como las preguntas (127); crear, editar, mover y borrar piden motivo.
- **Organización:** «Nuevo formulario» de la lista «Formularios» pasa a «Nuevo formulario ▾», con «Desde cero» y «Desde una plantilla», que abre la biblioteca (79); en el teléfono, el «+» pregunta lo mismo.

## 9. Tests

| Dónde | Qué |
| --- | --- |
| `Domain.UnitTests/Forms/` | estados de la plantilla; publicar sin cambios; volver a publicar sin versión nueva; copia que conserva los ids de preguntas, opciones y campos de catálogo, con catálogos nuevos (tipo, campos y valores) y `catalogRef` traducidos; una fórmula con un campo del catálogo y la presentación propia de una pregunta siguen andando en la copia |
| `Api.IntegrationTests/Modules/Forms/` | rutas de Plataforma: 403 sin el permiso, motivo obligatorio, evento de auditoría en la misma transacción, 409; rutas de organización: solo publicadas, retirada = 404, usar sin `formcatalogs.manage` = 403, el prefijo tomado se reemplaza, idempotencia; conteo: dos usos de la misma organización suman 1, y dos organizaciones suman 2; reglas de ejemplo: se leen de la versión aunque la plantilla se retire, y dejan de venir al guardar la primera revisión, también vacía; el diseño del PDF llega como primera revisión |
| Aislamiento | la plataforma no tiene ninguna ruta que lea formularios o respuestas de una organización; usar una plantilla escribe solo en la organización activa y en la fila de uso |
| Guardas | `ExplicitRouteInventoryTests`, `PermissionTextsTests` (de plataforma), `AuditTrail` de lo global (ADR 0050) |
| Front | biblioteca, vista, uso y editor de plataforma con MSW y axe |
| E2E y recorrido | el operador crea, completa y publica una plantilla; una organización la usa, completa el borrador y lo publica (con el canal de prueba) |

## 10. Puntos de contacto

| Marca | Qué hay que confirmar | Qué asume esta spec |
| --- | --- | --- |
| [E14] | El formato del borrador y el editor del front, para reusarlo en Plataforma. | El mismo formato, con `catalogRef`. |
| [E12] | El modelo de catálogos a mano, sus campos y sus límites. | Una plantilla trae catálogos con los mismos límites, salvo las opciones, hasta 200 (§3), con su tipo de valor y sus campos. |
| [E19] | Que la fórmula y la presentación propia de una pregunta apunten a los campos del catálogo por su id. | La copia conserva el id de cada campo (B11). |
| [E21] | El formato de las reglas y del diseño del PDF que deje la E21. | El de su spec, con las decisiones 30 a 33: los datos del mensaje por `{FieldId}` (149) y el bloque como un dato (177). Lo que cambia en una plantilla está en «Reglas de ejemplo y diseño del PDF», en la §3. |
| ADR 0050 | El tipo de evento para lo global del módulo. | `PlatformSettingsChanged` con destino global. |

## 11. Cómo se cierra

La puerta de la etapa, con la lista de control de `/puerta`, más una **prueba real con Meta**:
1. **En Plataforma:** un operador crea una plantilla con un catálogo a mano con un campo extra y reglas de ejemplo, y la publica con motivo.
2. **En una organización de prueba:** alguien la usa. El formulario queda en borrador, con su catálogo propio y el prefijo propuesto; se publica en Meta con el número de la organización, se manda desde un chat de Internos y se responde en el teléfono.
3. **El retiro:** la plataforma retira la plantilla, y la copia de la organización sigue igual.
4. **El conteo:** un segundo uso en la misma organización no suma; un uso en otra organización, sí.
