# Formularios: evaluación del traspaso 2 de ArquitecturaBase

Estado: **propuesta del 2026-10-24.** No cambia la [hoja de ruta de formularios](2026-10-12-formularios-dinamicos-hoja-de-ruta.md) ni las [specs](formularios/README.md) hasta que el usuario decida (§6).

Fuente: `../ArquitecturaBase/docs/plans/2026-10-23-traspaso-multitenant/` (README, capítulos 01 a 07 y anexos A a F), en el main `0e8189bd` del 2026-10-24.

Rige la condición de la hoja de ruta: nada de lo que viene de ArquitecturaBase pasa por encima de `AGENTS.md` ni de `docs/rules/`. Si algo choca, manda lo de acá.

## 1. En pocas palabras

**Qué pasó en ArquitecturaBase del 17 al 23/10:**
- cerró la Etapa 5 (el asistente): la espera bajó de 35 s a 6,6 s, y cada conversación quedó aislada;
- programó dar de baja, reactivar y eliminar formularios, y la pantalla «Formularios» con el detalle completo de una respuesta;
- programó los catálogos E1 y E2:
  - E1: tipo de valor, carga de una vez, «elegir varias» y hasta 10.000 opciones;
  - E2: campos extra, qué viaja a WhatsApp, datos de las personas y filtros por destinatario;
- decidió, sin programar, los Casos del asistente y los Documentos por WhatsApp, y estudió las integraciones.

**El aprendizaje más importante:** el usuario probó la app y concluyó que Codex la programó mal. Los reviews encontraron 142 problemas, 39 de ellos en el de catálogos E2. Tres cosas los causaron:
- la guía visual pedía que el botón «explicara el estado» y daba «Cambios sin guardar» como ejemplo;
- los contratos no tenían ejemplos reales;
- el front validaba por su cuenta y le tapaba la validación al servidor.

El 23/10 el usuario fijó reglas nuevas de interfaz y de idioma para los dos proyectos.

**Qué significa acá:**
1. **Antes de redibujar, las reglas del 23/10 entran en nuestras guías.** Hoy tenemos dos frases que inducen el mismo error (§2.1).
2. **Hay más etapas.** Los catálogos crecieron mucho, y se suman el ciclo de vida, la pantalla «Formularios», los Casos y los Documentos. Propongo pasar de 6 etapas a 11, más chicas, cada una con su review (§4).
3. **Hay mucho para dibujar:** 24 de los 60 tableros vigentes de formularios se redibujan, y hay 34 nuevos (§5).
4. **La puerta y las specs suman las lecciones** de los 142 problemas (§2.5 y §3).

## 2. Antes de cualquier etapa

### 2.1 Reglas de interfaz del 23/10 contra las nuestras

Valen para los dos proyectos (capítulo 04 del traspaso), así que se adoptan. §6 A pregunta solo su alcance y un caso que no cubren.

**Un botón está habilitado o deshabilitado, sin textos al lado ni debajo.** Si el motivo no es obvio, va en un tooltip.
- **Hoy acá:**
  - Criterio-Botones prohíbe «Deshabilitar un botón en vez de explicar qué falta»;
  - el README del lienzo dice «nada se deshabilita sin explicar por qué».

  **Es el equivalente de la frase que causó el error allá.**
- **Qué cambia:** las dos frases pasan a decir «Un botón está habilitado o deshabilitado, sin textos al lado ni debajo. Si el motivo no es obvio, va en el tooltip del botón o en el ⓘ de la acción».

**Sin «Cambios sin guardar» ni números de revisión a la vista.** Se conserva el aviso al salir.
- **Hoy acá:** `pantallas-y-ui.md` y Criterio-Marco lo ponen en la segunda línea de la banda, y aparece en **57 tableros** de todo el sistema.
- **Qué cambia:** sale de la banda, y las revisiones quedan solo en los historiales. El alcance sobre lo ya programado es la decisión A1.

**Las acciones van al borde derecho del encabezado.**
- **Hoy acá:** la banda ya lo hace, pero los encabezados de las cajas no se revisaron.
- **Qué cambia:** se escribe en `pantallas-y-ui.md` y se revisa `Section`.

**Se valida al intentar guardar, y después en vivo.** Nunca va en rojo una fila recién agregada.
- **Hoy acá:** no está escrito, y seis tableros apagan Guardar mientras hay un error.
- **Qué cambia:** se suma a `formularios.md` del front, y esos seis tableros se redibujan.

**Lo elegido antes que ya no se cambia va como dato de solo lectura** («Escritas a mano · Texto»).
- **Hoy acá:** el origen de un catálogo es un campo con candado que dice «No se cambia».
- **Qué cambia:** pasa a dato. La A11 (campos apagados) sigue para quien abre algo sin permiso de editarlo.

**Lo que la regla no cubre** es una opción de menú apagada. Hoy `pantallas-y-ui.md` le pone el motivo debajo, en gris (decisión A2).

### 2.2 Idioma (decisión B2)

La regla del 23/10, que el usuario confirmó acá el 24/10:
- lo que se crea se escribe en el idioma elegido en el menú del encabezado. Ese es su idioma original y el único obligatorio;
- no es obligatorio crearlo en los dos idiomas, y no se muestra cómo queda en los dos. No hay:
  - selector «Textos en»;
  - pares «Nombre» / «Nombre en inglés»;
  - vista previa doble;
  - avisos de lo que falta traducir;
- con el menú en otro idioma se ve lo escrito en ese idioma. Traducir es opcional, y se hace cambiando el menú;
- abarca formularios, catálogos, opciones, campos y datos de las personas;
- por WhatsApp, cada persona lo recibe en su idioma si existe y, si no, en el original, sin mezclar.

**Choca con el tercer punto de nuestra decisión 17** («el idioma de las pantallas del sistema no cambia»): para traducir, la persona cambia el menú, y con él cambian las pantallas. Los otros dos puntos siguen.

Hoy hay **18 tableros** con «Textos en», «En español: …» o pares en inglés, y tres avisos que exigen el inglés.

### 2.3 Producto genérico y soluciones simples

Ya son premisas de acá: la plantilla es genérica y, ante dos caminos, va el más simple. A las premisas de la hoja de ruta de formularios se suma una línea: ningún cliente, tipo de documento ni respuesta va escrito en el código, y lo particular es configuración de cada organización.

### 2.4 Contratos con ejemplos reales

La premisa 2 de la hoja de ruta ya los pide: «DTO, OpenAPI y ejemplos exportados». Pero ninguna ficha lo exige: `contracts:check` compara los tipos del front con el OpenAPI, y no hay ejemplos de una corrida real. Allá, el chat de Inicio estuvo roto desde el 09/10 porque el back mandaba `pending` y el front esperaba `Pending`.

Es técnico, así que se escribe sin preguntar, en `api-http.md` del back y en `datos-y-api.md` del front:
- toda respuesta que consume el front tiene ejemplos exportados de una corrida real, con sus estados y sus errores de negocio;
- el front valida esos ejemplos con zod estricto.

### 2.5 La puerta: lista de control

Pediste sumar a la puerta el capítulo 05 y la lista «no repetir» del anexo E. Esta lista los junta, sin repetir lo que ya pedimos (suites, E2E real, recorrido, una sola revisión de código al cerrar y la prueba a mano). Se escribe en la skill `/puerta` (`.agents` y `.claude`) y en `docs/rules/tests.md`:
1. **Números y fechas** con la cultura del pedido, rechazando lo ambiguo. Con tests de «1.500», «1,500», «12.345» y «3/4/2026», en es y en.
2. **El front no tapa la validación del servidor.** Solo el 409 de revisión pone el editor en revisión; otro 409 es un error que se corrige.
3. **Cambiar algo en uso** (tipo, fuente, papel, obligatorio, activo o eliminado):
   - se frena por el uso, o pide confirmación y limpia;
   - lo guardado que apunta a algo dado de baja sigue valiendo.
4. **Guardar sin cambios no escribe:** no cambian `ModifiedAtUtc`, la revisión ni la auditoría.
5. **Ninguna lectura trae todo para quedarse con poco.** Se prueba con el volumen máximo: 10.000 opciones o 10.000 respuestas.
6. **Nada nombra otro recurso sin permiso de leerlo:** ni un 409, ni un aviso, ni una vista previa.
7. **Nada exige un idioma fijo.** Se prueba guardando y releyendo con una traducción vacía, con `null` y con una referencia inactiva.
8. **Por cada exclusión o lock, un test** con dos conversaciones, canales o actores a la vez.
9. **Migraciones portables:**
   - el SQL va en una línea y sin `\r`;
   - el Down se niega antes de perder datos.
10. **La interfaz cumple §2.1.**
11. **Cada arreglo busca el mismo defecto** en los caminos hermanos y en las piezas compartidas, y cada `fix` dice qué pasaba.
12. **Proveedores:**
    - sus respuestas se clasifican en definitiva, transitoria o desconocida, y cada estado tiene una salida;
    - cada variante de WhatsApp se abre en un celular: no alcanza con que Meta la acepte.

### 2.6 Ver todo antes de programar (decisión A3)

**El lienzo ya es navegable:**
- tiene Play entre tableros, el índice general y uno por área;
- Diseño está terminando el recorrido guiado adentro del lienzo.

**Allá el usuario pidió además una «maqueta viva»:** el front real con un back falso, personas, idiomas y un celular simulado.

### 2.7 Quién programa

Allá se pasó de Codex a un chat de Claude, el «Codificador». Acá ya programan los chats de Claude de cada etapa (Coordinador, Back y Front), y Codex escribe planes y corre pruebas. Se mantiene así.

## 3. Evaluación técnica, etapa por etapa

**Lo común a todas:**
- los datos de la organización van con `ITenantOwned` y RLS;
- los workers entran con `ITenantScope` (vía `ExecutionActor`);
- va el feature `forms`, y los permisos son de organización;
- WhatsApp sale por la WABA y el token de la organización (E11);
- los archivos van en `IFileStore` (E10);
- «Por aprobar» va en «Para resolver» (ADR 0040);
- la IA va dentro del tope mensual de la organización (ADR 0051).

### E12 · Formularios: crear, publicar, mandar y recibir
- **Idioma del menú** (B2):
  - el original fijo ya está en la spec (2e2a9a68);
  - cambian el editor y la publicación: no hay selector ni vista previa en los dos idiomas, y publicar no pide la traducción.
- **«Elegir varias»** (B1):
  - en WhatsApp es un `CheckboxGroup` de 1 a 20 opciones, con mínimo y máximo, en el perfil `flow-static-7.3-r9`;
  - la captura guarda una fila por opción, con su posición;
  - los ids que viajan siguen siendo locales a la oferta (C3).
- **Lección del piloto:** un formulario Manual sin prellenado no manda `prefill_FORM:{}`.

### E12b · Ciclo de vida (nueva; B1)
- **Dar de baja** conserva la versión activa y los envíos abiertos.
- **Reactivar** es publicar una versión nueva.
- **Eliminar** es lógico, y solo se puede sin respuestas ni envíos abiertos. El prefijo queda reservado dentro de la organización.
- **La deprecación de los Flows** es una operación durable por WABA de la organización, con su token. La corre un worker que entra con `ITenantScope` al vencer el último envío.
- **Lección del review de allá:** un rechazo de Meta al deprecar no puede repetirse para siempre. Su respuesta se clasifica y tiene una salida.

### E13 · Catálogos base (lo que allá es E1)
- **Tipo de valor:** texto, entero, decimal o fecha. Se elige al crear y se guarda canónico.
- **Carga de una vez:** se pega o se importa un XLSX o un CSV, con vista previa de altas, cambios, bajas y errores.
  - Se aplica con el SHA-256 del archivo, bajo el lock de la UoW.
  - Pide confirmación con más del 25 % de bajas o del 10 % de filas con error.
  - Tiene topes: 10 MiB, 64 columnas, 10.000 filas y un ZIP de 100 MiB.
  - El CSV va en UTF-8, y las fórmulas toman su valor guardado.
  - Usa ClosedXML, que ya está en el repo.
- **Exportar** y bajar la plantilla.
- **Hasta 10.000 opciones** por catálogo, contando las inactivas. Reemplaza el tope de 500 de nuestra spec.
- **Guardar todo junto (C6) manda solo lo que cambió.** Allá cada guardado reescribía las 10.000 opciones y su auditoría.
- **La fuente «Miembros»** (§4 de nuestra spec) se mantiene, con filtros por rol y por empresa.

### E13b · Campos extra y qué viaja a WhatsApp (nueva; la primera mitad de E2)
- **Campos por opción:**
  - tipos: texto, entero, decimal, fecha, sí/no y «elegir de otro catálogo» (a mano y de la misma organización);
  - reglas: obligatorio, único, mínimo y máximo;
  - papel: «Dato» o «Filtro».
- **Cómo se guardan:**
  - como JSON en el catálogo y en cada opción, en el esquema `tenant` y con RLS;
  - con un índice GIN `jsonb_path_ops`;
  - con 10.000 opciones por 20 campos, medido con EXPLAIN.
- **Presentación:** qué campos van al título (30 caracteres) y a la descripción (300). Una pregunta puede tener la suya, y a Meta viaja solo lo elegido.
- **Sellado:** la respuesta guarda los campos de la opción tal como estaban.
- **Lecciones del review E2,** las 4 de gravedad alta, cada una con su test:
  - dar de baja una opción de la fuente no puede bloquear los catálogos que la usan;
  - un 409 de regla no es de revisión;
  - una traducción `""` no puede romper la lectura;
  - el front no tapa al servidor al cambiar el tipo.

### E13c · Datos de las personas y filtros por destinatario (nueva; la segunda mitad de E2)
- **Los datos de las personas** (como «Zona») son de la organización y del miembro:
  - van con `ITenantOwned` y clave por membresía, no por cuenta (C3);
  - una persona puede ser Centro en una organización y no existir en otra.
- **Dónde se cargan:** en una pantalla de definiciones y en «Datos», en la ficha del usuario.
- **Un permiso nuevo:** `formpersondata.manage`. Va en el catálogo, en `Permissions.resx` y su `.en`, y en el seed de los roles de sistema.
- **Los filtros por destinatario:** «el campo Filtro de la opción = el dato de quien recibe».
  - A quien no tiene el dato no se le manda, y se dice el motivo.
  - Al publicar hay dos avisos que no frenan: el máximo por persona y las personas sin el dato.
- **Baja de la cuenta:** el participante borra los datos del miembro. No van en «Exportar mis datos», porque son datos de la organización.
- **Fuera de esta etapa:** B2C, y que un dato dependa de la empresa.

### E14 · Fotos y documentos
Sin cambios:
- los archivos van en `IFileStore`, con claves opacas y sin prefijo por organización (E10);
- el antivirus está en la lista de lanzamiento.

### E15 · «Formularios», planilla, detalle y Excel
- **La pantalla «Formularios» (MF1):**
  - en el panel, reemplaza «Respuestas» y «Por aprobar» por una sola entrada, dentro de «Gestión de formularios». «Diseñar formularios», «Catálogos» y «Formularios que faltan» quedan como están;
  - es una tabla de formularios con «Respuestas», «Última respuesta» y «Por aprobar»;
  - tiene los filtros «Activos · Dados de baja» y «Con algo por aprobar», sin bandeja aparte;
  - quien solo aprueba ve solo lo que espera.

  La columna «Por aprobar» llega con la E16.
- **El detalle:**
  - FRM-Respuesta (D2 allá) queda como vista rápida, al costado;
  - «En pantalla completa» (DC0) abre la pantalla propia, con flechas que siguen el orden de la planilla y la vuelta a la misma fila con sus filtros.
- **La planilla:**
  - suma subcolumnas de los campos sellados: los «Dato», visibles; los «Filtro», en «Columnas»;
  - tiene el filtro «alguna de» para «elegir varias»;
  - en el Excel, «elegir varias» va unido con «; ».
- **La empresa** va como columna y como filtro (decisión 24).

### E16 · Reglas, aprobación, PDF y avisos
- **Reglas, sugerencias y PDF** se dibujan sin «Cambios sin guardar» ni revisiones junto a Guardar.
- **El PDF** muestra los campos «Dato».
- **«Por aprobar»** se suma a MF1, con la misma fuente que «Para resolver».

### E17 · Biblioteca de plantillas de la plataforma
- **Es la misma biblioteca de formularios estándar** que la de los Documentos (DOC1 allá; decisión B8).
- **La sección «Lectura de documentos»** de una plantilla («Cómo reconocerlo», los datos estándar y las validaciones fijas) se suma en la E19.
- **La decisión 14** (una plantilla trae solo catálogos a mano) es compatible con los campos extra.

### E18 · El asistente, con Casos (antes «asistente»; B6)
- **Por organización:** el perfil, el conocimiento, los casos y los límites.
- **Seis acciones:**
  1. formulario;
  2. texto exacto;
  3. respuesta con la información del negocio;
  4. hasta 3 botones;
  5. derivar;
  6. no responder.

  «Cualquier otra cosa» va al final, y «Probar» no manda nada.
- **Respuestas redactadas con límites:** piden un ADR de acá, equivalente al 0021 de allá.
- **Cómo trabaja:**
  - el historial es el de las últimas 24 h de la misma conversación;
  - las ráfagas esperan 4 s, con un tope de 15 s;
  - hace hasta 3 acciones por turno.
- **Desde el primer día:**
  - **la espera:** las señales al confirmar la UoW, un sondeo de 2 s solo con algo pendiente y el webhook que despierta la captura;
  - **el aislamiento por conversación:** `ChannelConversationId` en el contexto, con el índice `(OwnerUserId, ChannelId, ChannelConversationId)`;
  - **la medición:** de texto a envío y de audio a envío.
- **El costo** del modelo, de la transcripción (decisión 27) y de la lectura entra en el tope mensual.
- **Se mantienen** las decisiones 28 (configurar los Casos y pasar al modo Asistente usan «Administrar WhatsApp», sin permiso nuevo) y 29 (solo Internos; B7).

### E19 · Documentos por WhatsApp (nueva; B1)
- **Qué hace:** lee con IA lo que manda una cuenta vinculada y propone el formulario prellenado. Siempre confirma una persona.
  - Lee foto, PDF, `.docx`, `.xlsx`, ubicación y contacto.
  - No lee formatos viejos, ni archivos con macros o con contraseña.
- **Pantallas nuevas:**
  - la bandeja «Documentos recibidos»;
  - «Armar formulario con la IA»;
  - un tablero por formulario.
- **Las validaciones fijas** (CBU/CVU con la entidad del BCRA, CUIT/CUIL) van como datos de referencia, nunca como una lista en el código.
- **La retención** se decide en B9.
- **El mayor riesgo, sin verificar:** precargar un archivo en un Flow. Hay que probarlo con Meta antes de planificar.
- **Depende de:**
  - la E13c, por el cruce por CUIT;
  - la E17, por los estándares;
  - la E18, por las ráfagas, las acciones y derivar.

### Después del bloque
- **Integraciones:**
  - un módulo `Integrations`, con conexiones y credenciales por organización;
  - la API pública y el MCP necesitan una identidad por organización;
  - quedan abiertas las 13 decisiones de su estudio.
- **Catálogos, fases 3 a 6:** la búsqueda en vivo, el asistente que arma catálogos y los orígenes externos.
- **La conexión a la base de un cliente:** una VPN por organización es pesada. Las alternativas simples son:
  - un agente del lado del cliente que mande los datos;
  - una réplica de solo lectura.
- **Más adelante:** los tableros que cruzan formularios, y B2C.

## 4. Orden propuesto (decisión B1)

Los números son nombres, no orden, como con la 8b y con la 11 antes de la 9. Se suman letras para no renumerar.

| Orden | Etapa | Alcance | Depende de | Tableros (§5) |
|---|---|---|---|---|
| 1 | E12 | Formularios, con «elegir varias» y el idioma del menú | E10 | Redibujar 4 |
| 2 | E12b | Ciclo de vida | E12 | BA1-A a BA6 |
| 3 | E13 | Catálogos base | E12 | Redibujar 5; CA1-A a CA6 |
| 4 | E13b | Campos extra y qué viaja a WhatsApp | E13 | CE1, CE2 y CE3-B |
| 5 | E13c | Datos de las personas y filtros por destinatario | E13b | CE4-A y el diálogo para definir un dato |
| 6 | E14 | Fotos y documentos | E12 y E10 | Sin cambios |
| 7 | E15 | «Formularios», planilla, detalle y Excel | E13c y E14 | MF1, DC0, CE5 y CE6; ajustar FRM-Respuesta |
| 8 | E16 | Reglas, aprobación, PDF y avisos | E15 | Redibujar 7 |
| 9 | E17 | Biblioteca de plantillas | E16 | Redibujar 7; DOC1 |
| 10 | E18 | El asistente, con Casos | E12, después de la E17 (decisión 19) | Redibujar 1; AS1-A a AS6 |
| 11 | E19 | Documentos por WhatsApp | E13c, E17 y E18 | DOC2 a DOC7-A |

Si las fotos importan antes, la E14 puede ir después de la E12b: no depende de los catálogos.

## 5. Tableros

### 5.1 Redibujar (24 tableros, 374 apariciones)

| Etapa | Tableros | Qué tienen |
|---|---|---|
| E12 | FRM-Editor, M-FRM-Editor, FRM-Publicar y FRM-Publicando | «Textos en», «En español: …», «Cambios sin guardar», revisiones junto a Guardar y textos junto a Publicar |
| E13 | FRM-Catalogo, M-FRM-Catalogo, FRM-Catalogo-Filtros, M-FRM-Catalogo-Filtros y FRM-Catalogo-Propuesta | El origen como campo con «No se cambia», «Textos en» y «Cambios sin guardar» |
| E16 | FRM-Editor-Reglas, M-FRM-Reglas, FRM-Editor-Reglas-Plantilla, M-FRM-Reglas-Plantilla, FRM-Editor-Sugerencias, FRM-Pdf y M-FRM-Respuesta-Aprobacion | «Cambios sin guardar», «Revisión N» fuera del historial, «Textos en», pares en inglés en el PDF y textos junto a botones |
| E17 | Plat-FRM-Plantilla, Plat-FRM-Publicar, Plat-FRM-Retirar, Plat-FRM-Plantilla-Reglas, Plat-FRM-Pdf, Plat-FRM-Categorias y M-FRM-Plantilla | El inglés obligatorio, «Textos en», «Cambios sin guardar» y un texto junto a un botón |
| E18 | FRM-Editor-Asistente | «Textos en» y «Cambios sin guardar» |

**Para revisar,** porque no está claro que choquen:
- «Publicada · v2» y «Cambios sin publicar» en Plat-FRM-Biblioteca, M-Plat-FRM-Biblioteca, Plat-FRM-Plantilla-Nueva y las plantillas de arriba. Son el estado de la plantilla, pero «v2» es un número de versión a la vista.
- «Reglas de la revisión 3» en FRM-Respuesta-Aprobacion.

**Además:**
- los tres criterios del lienzo cambian con §2.1: Marco, Botones y Decisiones;
- con A1, «Cambios sin guardar» sale de los 57 tableros del sistema;
- con MF1, cambia el panel de todos los FRM de la compu, y FRM-Por-Aprobar y M-FRM-Por-Aprobar pasan al Archivo.

### 5.2 Dibujar nuevos (34)

Son los elegidos allá, con nuestras piezas, adaptados a organizaciones y con las reglas de §2.1. El anexo D marca que varios violan esas reglas allá: BA2, BA4, BA6, AS2, AS3, CA1-A, CA2, CA3, CE1, CE2 y CE3-B. Esos se dibujan corregidos. Los de idioma de allá (I1 a I3) no se dibujan: con §2.2 no hace falta mostrar los dos idiomas.

| Etapa | Tableros |
|---|---|
| E12b | BA1-A a BA6; BA5 es el circuito |
| E13 | CA1-A a CA6; CA6 es el circuito |
| E13b | CE1, CE2 y CE3-B |
| E13c | CE4-A, y el diálogo para definir un dato, que allá falta |
| E15 | MF1, DC0 con el ajuste de DC1, CE5 y CE6, el circuito de los catálogos |
| E17 | DOC1 (la biblioteca), junto con las plantillas |
| E18 | AS1-A a AS6 |
| E19 | DOC2 a DOC7-A |

## 6. Decisiones para el usuario

En todas va primero la recomendada.

**No se vuelve a preguntar:**
- lo que fijaste el 23/10 para los dos proyectos (§2.1 y §2.3);
- lo que pediste sumar: la lista de la puerta (§2.5) y lo que el capítulo 07 manda incorporar:
  - los catálogos E1 y E2;
  - MF1 con su detalle;
  - el ciclo de vida;
  - los arreglos del asistente;
  - los Casos y los Documentos;
  - las integraciones;
- lo técnico, que se decide al programar (§2.4 y la tabla de C).

Se pregunta solo dónde va cada cosa, cuándo, y lo que choca con decisiones nuestras.

### A. Interfaz

**A1. Las pantallas ya programadas.**
- **Recomendado:** sacar «Cambios sin guardar» y los textos junto a botones de todo el sistema, de una vez, en la E12. Es sobre todo `Page` y las pantallas que le pasan ese texto, y un sistema con dos formas confunde. Respeta «lo cerrado no se reabre»: se arregla en la etapa en curso.
- **Alternativa:** solo en lo nuevo. Las pantallas de las etapas cerradas quedan como están.
- **Consecuencia:** Diseño saca la frase de los 57 tableros, y el front de la E12 suma una tarea chica.

**A2. Una opción de menú apagada.**
- **Recomendado:** se mantiene el motivo debajo, en gris, como dice hoy `pantallas-y-ui.md`. Es un menú y no un botón, y en el celular no hay tooltip.
- **Alternativa:** se pasa a tooltip, como en los botones.

**A3. Cómo ves todo antes de programar.**
- **Recomendado:** el lienzo navegable, con Play, los índices y un recorrido guiado por etapa.
- **Alternativa:** una maqueta viva como la de allá, con el front real, un back falso y un celular simulado. Se recorre como la app real, pero es otro repo que hay que mantener en cada etapa.

### B. La hoja de ruta

**B1. Cómo se parte el bloque.**
- **Recomendado:** las 11 etapas de §4:
  - el ciclo de vida como E12b;
  - «elegir varias» en la E12;
  - los catálogos en tres etapas (E13, E13b y E13c);
  - los Documentos como E19.
- **Alternativa:** 6 etapas más grandes. La E13 junta E1 y E2, y el ciclo de vida y los Documentos quedan para después del bloque.
- **Consecuencia:** las etapas chicas tienen cada una su review y su puerta. Eso es lo que allá faltó con E2, que juntó 39 problemas en una sola entrega.

**B2. La decisión 17: el idioma.** Lo confirmaste el 24/10 (§2.2): sale el tercer punto de la decisión 17 y se redibujan los 18 tableros. Queda una sola pregunta. Con el menú en inglés, ¿qué pasa con un formulario que se creó solo en español?
- **Recomendado:** aparece igual, con sus textos en español, y quien quiera lo traduce ahí. Nadie de la organización pierde de vista un formulario.
- **Alternativa:** no aparece, porque cada idioma tiene sus formularios. Un compañero con el menú en inglés no ve los que creaste en español.

**B3. La decisión 20: un catálogo en otro idioma.**
- **Recomendado:** se mantiene: solo sirven los catálogos con el mismo idioma original que el formulario. Así no se mezclan idiomas en un mismo envío, y con B2 lo natural es escribir todo en un idioma.
- **Alternativa:** como allá: sirve cualquier catálogo, y cada opción sin traducción sale en su original.

**B4. Las decisiones 11 y 21: nombres de más de 30 caracteres.**
- **Recomendado:** se mantiene el recorte con «…», con el nombre completo en el detalle. Se suma la presentación de la E13b, que elige qué campos van al título y cuáles a la descripción.
- **Alternativa:** como allá: no se recorta, y el envío se frena si el título pasa los 30.

**B5. Las decisiones 12 y 22: catálogos de más de 200 opciones.**
- **Recomendado:** se mantienen: se guarda con aviso, y el bloqueo es al mandar. Se suman:
  - los avisos de la E13c al publicar: el máximo por persona y las personas sin el dato;
  - el tope de 20 en «elegir varias».

**B6. El asistente con Casos (E18).**
- **Recomendado:** sí. El asistente contesta lo que su organización configure (perfil, conocimiento, casos y límites), redacta dentro de esos límites y deriva lo demás. Reemplaza «lo que no es un formulario no se contesta».
- **Alternativa:** como hoy, el asistente solo reconoce formularios.
- **Consecuencia:** hace falta un ADR de respuestas redactadas, equivalente al 0021 de allá.

**B7. Casos en la bandeja Clientes.**
- **Recomendado:** no en la primera versión: solo Internos, como dice la decisión 29. Los clientes siguen con el bot del menú de la 11, y se evalúa después del piloto.
- **Alternativa:** también en Clientes.

**B8. La biblioteca de la E17.**
- **Recomendado:** es la misma que la de formularios estándar. Una plantilla de la plataforma puede traer su «Lectura de documentos», que se suma en la E19.
- **Alternativa:** dos bibliotecas.

**B9. Cuánto se guardan los documentos.**
- **Recomendado:**
  - los usados siguen a su respuesta, que dura lo que la organización (decisión 23);
  - los descartados o sin clasificar se borran a los 90 días, como fijaste allá.
- **Alternativa:** como la D4 del chat (E11): se guardan mientras exista el contacto.

### C. Lo que allá es global y acá puede ser por organización (anexo C §19)

**Para decidir:**

**C1. Idiomas disponibles** (tema 1).
- **Recomendado:** español e inglés para todas las organizaciones, sin configurar.
- **Alternativa:** que cada organización elija los suyos.

**C2. El idioma de quien recibe** (tema 2).
- **Recomendado:** el de su cuenta, uno solo para todas sus organizaciones.
- **Alternativa:** uno por organización.

**C3. Los datos de las personas** (tema 12).
- **Recomendado:** por organización y por miembro. En la primera versión, no dependen de la empresa ni hay para B2C.
- **Alternativa:** que también dependan de la empresa.

**C4. Los topes de los catálogos** (tema 11): 10.000 opciones, 20 campos y 20 datos de las personas.
- **Recomendado:** fijos, iguales para todas.
- **Alternativa:** según el plan de cada organización.

**C5. La lectura de documentos** (tema 9).
- **Recomendado:** usa el proveedor de la plataforma y entra en el tope mensual de IA de la organización, con un tope por persona y por día.
- **Alternativa:** solo el tope mensual, sin tope por persona.

**Lo técnico, sin preguntar:**

| Tema | Cómo queda |
|---|---|
| 3. Zona horaria | La de cada organización |
| 4. Cupos y costo de IA | Las cuotas por actor, dentro del tope mensual de la organización |
| 5. Conexión y calificación de modelos | Las hace Plataforma |
| 6 y 7. El asistente y sus respuestas redactadas | Por organización, con un máximo de la plataforma |
| 8. Información del negocio | Una por organización |
| 10. Biblioteca estándar | Una sola, de la plataforma (B8) |
| 13. Filtros por destinatario | Dentro de la organización |
| 14. Fuentes de valores | Dentro de la organización |
| 17. Plantillas de Meta | Por WABA. Fuera de las 24 horas, el aviso queda «No se pudo» (decisión 26) |
| 18. Deprecación de Flows | Por WABA, con el token de la organización |
| 19. Prefijo | Único por organización |
| 20. Retención de respuestas | Sin plazo mientras exista la organización (decisión 23) |
| 21. Tamaño de archivos | Una opción global |
| 22. Límites por proceso | Las cuotas de la E9, por organización |
| 23. Cola de salida | En orden, por número |
| 24. Permisos nuevos | Solo `formpersondata.manage`, de organización. El asistente sigue con «Administrar WhatsApp» (decisión 28) |
| 25. Reglas de interfaz | §2.1 |

**Para su estudio, después del bloque:**
- **15 y 16, orígenes externos e integraciones fiscales:** sus credenciales son de cada organización.
- **26, el piloto:** una organización y un número de prueba para cada grupo de etapas.

## 7. Cambios cuando decidas

**Hoja de ruta:**
- en §3, las etapas y el orden de §4;
- en §5, las decisiones nuevas, desde la 34;
- la decisión 17, según B2;
- las decisiones 11, 12, 20, 21 y 22, según B3 a B5;
- en las premisas, la línea de §2.3, y en la premisa 2, la exigencia de §2.4;
- en §4, los «Criterios» de cada etapa, con las lecciones de §3.

**Specs** (`formularios/`):
- **`e12-formularios.md`:** suma «elegir varias», el idioma del menú y el prellenado vacío.
- **`e13-catalogos.md`:** pasa al alcance de E1, con el tope de 10.000.
- **Resúmenes de una página nuevos:** `e12b-ciclo-de-vida.md`, `e13b-campos-extra.md` y `e13c-datos-de-las-personas.md`.
- **`e15-planilla.md`:** suma MF1, DC0, las subcolumnas, «alguna de» y el Excel de «elegir varias».
- **`e16-reglas-y-aprobacion.md`:** suma «Por aprobar» en MF1 y el PDF con los «Dato».
- **`e17-biblioteca.md`:** suma que es la biblioteca de estándares.
- **`asistente.md`:** pasa a ser `e18-asistente.md`, con los Casos.
- **Nuevo:** `e19-documentos.md`.
- **Los textos para Diseño** (`e12-`, `e13-` y `e17-texto-diseno.md`): siguen a sus specs.

**Guías:**
- §2.1 (y A2) en `pantallas-y-ui.md` y `formularios.md` del front;
- §2.4 en `api-http.md` del back y `datos-y-api.md` del front;
- §2.5 en `docs/rules/tests.md` y en la skill `/puerta`;
- §2.1 en los criterios del lienzo (Marco, Botones y Decisiones), que cambia Diseño.

Al tocar una ficha del front se corre `HarnessTests` del back.

**Quién hace cada cosa:**
- Formularios, las specs;
- Diseño, los tableros de §5: de a una etapa, empezando por los criterios;
- la Guía, las guías y la puerta.
