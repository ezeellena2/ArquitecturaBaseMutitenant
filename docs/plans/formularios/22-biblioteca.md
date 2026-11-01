# E22 · Biblioteca de plantillas de la plataforma (spec)

**Estado:** pendiente · spec de la etapa 22 del bloque (decisiones 34, 40, 78 y 79). **Es también la biblioteca de formularios estándar** (40). Se arma con las piezas de este repo y las de las etapas anteriores. `/plan-etapa` la convierte en plan cuando le toque, después de verificar los puntos marcados **[E14]**, **[E12]** y **[E21]** contra el código.

**Fuentes:**
- alcance y decisiones: [hoja de ruta](../2026-10-12-formularios-dinamicos-hoja-de-ruta.md), §E22 y decisiones 2, 13 a 16, 30 a 33, 35, 40, 45, 50, 59, 61, 62 y 64;
- reglas: [AGENTS.md](../../../AGENTS.md), las [fichas](../../rules/README.md) y la [ADR 0050](../../decisions/0050-auditoria-global-de-modulos.md) (auditoría de lo global);
- las specs de la [E14](14-formularios.md) y la [E12](12-catalogos.md).

**Partes.** Esta spec no entra en un solo archivo, así que sigue en otros, con los mismos números de sección. Lo que no está en la tabla está acá.

| Secciones | Archivo |
| --- | --- |
| §2, §3, §4, §5, §6 y §7 | [datos y contrato](22-parte-datos-y-contrato.md) |

## Qué leer

Antes de empezar:
1. **Lo anterior:** las specs de la [E12](12-catalogos.md), la [E14](14-formularios.md) y la [E21](21-reglas-y-aprobacion.md), sobre todo el borrador, los catálogos, las reglas y el diseño del PDF que se copian.
2. **Decisiones** de la hoja de ruta: 2, 13 a 16, 30 a 33, 35, 40, 45, 50, 59, 61, 62 y 64.
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
- plantillas de una organización para otra;
- la «Lectura de documentos» de una plantilla, que llega con los documentos (E25);
- saber qué organizaciones usaron una plantilla (decisión 16);
- borrar plantillas: se retiran.

## 8. Pantallas (tableros)

Valen los criterios de la E14 y la decisión 43. El texto para Diseño está en [22-texto-diseno.md](22-texto-diseno.md). Datos de ejemplo:
- **Categorías:** Ventas, Logística y Mantenimiento.
- **Plantillas:**
  - «Visita a cliente» (Ventas): publicada, con cambios guardados sin publicar, que la usaron 12 organizaciones;
  - «Control de vehículo» (Logística): borrador;
  - «Orden de trabajo» (Mantenimiento): retirada.

**Aprobados** en el lienzo v126, ajustados al editor de una sola pantalla en la v130, y con las reglas de ejemplo y el PDF en la v134:

| Tablero | Qué muestra | Estados |
| --- | --- | --- |
| `Plat-FRM-Biblioteca` y `M-Plat-FRM-Biblioteca` | listado: nombre, categoría, estado (Borrador, Publicada o Retirada) y «La usaron N»; filtro por estado y categoría; «Nueva plantilla»; «Categorías» como otra sección del área, no como pestaña | vacío, sin resultados |
| `FRM-Formularios-22` | la lista, sobre `FRM-Formularios-21`, con «Nuevo formulario ▾» (79) | — |
| `Plat-FRM-Plantilla-Nueva` | diálogo con el nombre y la categoría | — |
| `Plat-FRM-Plantilla` | ficha con pestañas **Plantilla**, **Catálogos** y **Publicación**. **Plantilla** es el editor de la E14: categoría, prefijo sugerido, descripción, para quién es (50), preguntas en el lugar y, debajo, las reglas de ejemplo y el diseño del PDF (33), con el teléfono al costado. Descartar, Guardar borrador y Publicar. **Catálogos:** una fila por catálogo, que se abre aparte. **Publicación:** estado, versiones con número, fecha, operador y motivo, «Retirar» en el ⋮ de la banda y «Volver a publicar» | cambios sin guardar, otra persona cambió, catálogos, un catálogo abierto, publicación (con su menú, borrador y retirada) |
| `Plat-FRM-Plantilla-Reglas` | «Cuando llega una respuesta» de la E21 en la pestaña «Plantilla», sin botón propio ni historial. En una acción que pide un rol: «Rol: Lo elige cada organización», con candado, y «Rol sugerido» | reglas, editar una regla, una regla con errores, sin reglas y borrador guardado |
| `Plat-FRM-Pdf` | el diseño del PDF de la E21, desde «Ver el diseño», con «Descartar» y «Guardar borrador». La vista previa muestra «Logo» y «Nombre de la organización» como marcadores | — |
| `Plat-FRM-Publicar` y `Plat-FRM-Retirar` | diálogos con el motivo. Publicar muestra además la comprobación: problemas por pregunta, catálogo o regla | sin problemas, con problemas |
| `Plat-FRM-Categorias` | lista con orden (⋮ con Editar, Subir, Bajar y Borrar), diálogo de alta y edición con motivo | una en uso: «Borrar» grisado, con el motivo en su tooltip |
| `FRM-Biblioteca` y `M-FRM-Biblioteca` | para la organización: tarjetas por categoría, con un buscador | sin plantillas |
| `FRM-Plantilla` y `M-FRM-Plantilla` | la vista de una plantilla: descripción, preguntas, catálogos que trae, «Reglas de ejemplo» en palabras (con «un rol que elegís al usarla» y el sugerido) y la vista del teléfono; «Usar esta plantilla» | sin permiso de catálogos |
| `FRM-Plantilla-Usar` | diálogo con el nombre y el prefijo propuesto; al confirmar, abre el formulario nuevo | prefijo tomado: se propone otro |
| `FRM-Editor-Reglas-Plantilla` y `M-FRM-Reglas-Plantilla` | el editor de la organización después de usar la plantilla: las reglas precargadas, con «De la plantilla» y «Sin guardar», «Elegí el rol» con «Rol sugerido» debajo, y una pregunta borrada marcada en la condición y en el texto que la nombra | — |
| `Cuenta-Plat` (ajuste) | los permisos del operador suman «Administrar la biblioteca de formularios» | — |

**Diseño redibuja** (evaluación, §5.1, y ajuste de la Guía del 2026-10-24), sin «Textos en», sin el inglés obligatorio, sin «Cambios sin guardar» ni «Cambios sin publicar», sin el número de versión en la lista ni en la banda, y sin textos junto a los botones (35 y 43):
- `Plat-FRM-Plantilla`, `Plat-FRM-Publicar`, `Plat-FRM-Retirar`, `Plat-FRM-Plantilla-Reglas` (con las acciones de la 61 y la 62, que quedan «Lo elige cada organización»), `Plat-FRM-Pdf` (con la base de la 59), `Plat-FRM-Categorias` y `M-FRM-Plantilla`;
- `Plat-FRM-Biblioteca`, `M-Plat-FRM-Biblioteca` y `Plat-FRM-Plantilla-Nueva`, por el estado de la plantilla;
- `FRM-Editor-Reglas-Plantilla` y `M-FRM-Reglas-Plantilla`: las reglas que llegan de una plantilla, en la pestaña de reglas de la E21.

**Diseño redibuja** `FRM-Biblioteca` (evaluación, §5.2): la biblioteca de formularios estándar (40), con en qué formularios propios se usó cada plantilla. La sección «Lectura de documentos» no va en esta etapa.

**Mandan sobre esta spec en:**
- **Biblioteca de Plataforma:** filtros Estado y Categoría, y los estados vacía, sin resultados, cargando y error. En el teléfono es solo para ver.
- **El estado de una plantilla** (ajuste de la Guía del 2026-10-24, con la decisión 43): la lista y la banda dicen solo «Borrador», «Publicada» o «Retirada». «Publicar» habilitado ya dice que hay cambios para publicar, y el número de versión va en el historial de versiones de «Publicación».
- **Publicación de una plantilla:**
  - el estado va en grilla, con las versiones (número, fecha, operador y motivo);
  - en borrador dice «Todavía no se publicó.»;
  - una retirada muestra quién la retiró, el motivo y «Volver a publicar».
- **Publicar:** el motivo y la comprobación. Muestra qué cambia, o los problemas por pregunta, catálogo o regla, con «Publicar» grisado. La primera publicación es la versión 1, y volver a publicar sin cambios repite la misma.
- **Las reglas y el diseño del PDF en Plataforma** se guardan con el borrador: van en `FormTemplateDraft` (`rules[]` y `pdfDesign`), con el mismo `PUT …/draft`, su versión y el 409. «Guardar borrador» de `Plat-FRM-Pdf` es ese mismo guardado. Los problemas de las reglas al publicar son `Forms.Template.Invalid` con el campo `rules[i]…` (B8).
- **Las reglas precargadas en la organización:** «Guardar reglas» queda habilitado; al tocarlo sin elegir los roles, se marcan las acciones que lo piden (43). El servidor valida con la regla de la E21, que pide el rol en esas acciones. Se ven y se guardan con `formrules.manage`; sin ese permiso, la sección muestra su estado «sin permiso».
- **«Logo» y «Nombre de la organización»** van como marcadores, porque la plataforma no ve datos de ninguna organización.
- **Usar una plantilla:**
  - se pide el nombre y se propone el prefijo; si VIS está tomado, se propone VIS2, con ⓘ;
  - si la plantilla se retiró en el medio, avisa y vuelve a la biblioteca;
  - sin permiso de catálogos, «Usar esta plantilla» queda grisado, con el motivo en su tooltip (43).

**Decisiones del usuario** sobre los tableros:
- **Plataforma:** «Formularios» es un grupo de Administración, con Biblioteca y Categorías.
- **Pestaña Catálogos de la plantilla:** una fila por catálogo, con sus opciones y la pregunta que lo usa. Cada catálogo se abre aparte.
- **Retirar:** va en el ⋮ de la banda, en rojo. El diálogo pide motivo y dice que las copias ya hechas no cambian.
- **Categorías:** la lista con orden y ⋮ (Editar, Subir, Bajar y Borrar); crear, editar, mover y borrar piden motivo.
- **Organización:** «Nuevo formulario» de la lista «Formularios» pasa a «Nuevo formulario ▾», con «Desde cero» y «Desde una plantilla», que abre la biblioteca (79); en el teléfono, el «+» pregunta lo mismo.

## 9. Tests

| Dónde | Qué |
| --- | --- |
| `Domain.UnitTests/Forms/` | estados de la plantilla; publicar sin cambios; volver a publicar sin versión nueva; copia que conserva los ids de preguntas y opciones, con catálogos nuevos (tipo, campos y valores) y `catalogRef` traducidos |
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
| [E21] | El formato de las reglas y del diseño del PDF que deje la E21. | **Resuelto el 2026-10-18** con su resumen, sus tableros (v132) y las decisiones 30 a 33: ver «Reglas de ejemplo y diseño del PDF» en la §3. |
| ADR 0050 | El tipo de evento para lo global del módulo. | `PlatformSettingsChanged` con destino global. |

## 11. Cómo se cierra

La puerta de la etapa, con la lista de control de `/puerta`, más una **prueba real con Meta**:
1. **En Plataforma:** un operador crea una plantilla con un catálogo a mano con un campo extra y reglas de ejemplo, y la publica con motivo.
2. **En una organización de prueba:** alguien la usa. El formulario queda en borrador, con su catálogo propio y el prefijo propuesto; se publica en Meta con el número de la organización, se manda desde un chat de Internos y se responde en el teléfono.
3. **El retiro:** la plataforma retira la plantilla, y la copia de la organización sigue igual.
4. **El conteo:** un segundo uso en la misma organización no suma; un uso en otra organización, sí.
