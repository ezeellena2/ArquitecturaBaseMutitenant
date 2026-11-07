# Traspaso 10: evaluación de ArquitecturaBase entero

**Estado:** insumo · del 2026-11-07, con el plan de §4 aprobado ese día y las nueve decisiones de §5 tomadas, todas las recomendadas: son la 160 a la 179 de la hoja de ruta, y las dos de forma de trabajo pasaron a `AGENTS.md`. Se retira cuando las specs estén al día y los tableros de §4.3, aprobados en el lienzo. El trabajo se reparte en chats en paralelo, a pedido del usuario.

Suma lo que encontró la revisión paralela de la sesión «Guia - Desarrollo», del mismo día, verificado acá antes de anotarlo.

Fuente: el [traspaso 10](../../../ArquitecturaBase/docs/plans/2026-11-07-traspaso-multitenant/README.md) (README y capítulos 01 a 04) y las fuentes canónicas de los dos repos de allá: `docs/features`, `docs/architecture/backend.md` y `docs/decisions`, y del front, `docs/design` y las capturas de `src/test/visual/screenshots`, en el `main` del 2026-11-07 (`78c9eca2` y `6f440628`). El lienzo provisorio de allá no se puede abrir desde esta cuenta: lo que se ve sale de su registro de diseño, su código y sus capturas.

Rige la condición de la [hoja de ruta](2026-10-12-formularios-dinamicos-hoja-de-ruta.md): nada de lo que viene de ArquitecturaBase pasa por encima de `AGENTS.md` ni de `docs/rules/`. Las pantallas y las reglas de producto se copian (126); el código, no (18). Acá, «maqueta» es el lienzo: la maqueta de la 124 sigue sin usarse.

## 1. En pocas palabras

**Qué hizo ArquitecturaBase el 6 y el 7/11:**
- verificó los renglones con Meta y decidió «Agregar bloque» (RG10-D) e «ítem» en lugar de «renglón» (RG12-A);
- programó el estado de la publicación dicho una vez (EP1-A), la vista oficial que se recorre (VO1-A), el nombre del Flow y el mensaje que nombra el formulario;
- decidió elegir primero por un campo (LD1-A a LD3-A), que se está programando;
- revisó el rendimiento contra 31 consejos, con los esqueletos de carga (ES1-A, ES2 y ES3) como regla de diseño.

**Qué encontró la revisión de todo ArquitecturaBase,** área por área (§3):
1. **Reglas de diseño de allá que faltan acá:**
   - que no parezca plantilla;
   - los esqueletos de carga;
   - el estado, una vez;
   - decidir a propósito;
   - ninguna elección se descarta en silencio;
   - mucho texto, con jerarquía;
   - el ancho de 320.
2. **Decisiones de acá que no llegaron a los tableros:**
   - sin cantidades en los filtros (143) y un solo «Filtros» (144), también en el núcleo programado;
   - la línea entre columnas (137), que no está en unas 79 tablas;
   - «Grupo de líneas», que la 146 reemplazó y sigue en 11 tableros;
   - «Mis cargas» (153) y el chat en Mi cuenta (159).
3. **Lo que allá se repensó y acá quedó viejo:**
   - la publicación;
   - el mensaje del envío;
   - la baja;
   - el bloque y la palabra «ítem»;
   - corregir y anular;
   - la carga;
   - el catálogo que propone el asistente.
4. **Lo técnico** (Meta, rendimiento, operación y ARCA): se anota en las specs, la E10 y la hoja de ARCA, sin preguntarte.

**Qué propongo** (§4): no redibujar todo de cero.
- Primero la documentación.
- Después, un pase de los generadores sobre las piezas compartidas del lienzo.
- Por último, cinco tandas de tableros, sumadas a los unos 70 que ya estaban pendientes de los traspasos 5 a 9:
  - rehacer unos 80 tableros;
  - sumar 8;
  - retirar 2.

Antes, nueve decisiones tuyas, de a una (§5).

## 2. Tus reglas (el 03)

Desde hoy trabajo con ellas. Las que no estaban escritas acá y no chocan quedan en la memoria de esta cuenta, y pasan a `AGENTS.md` con el plan:
- varias sesiones en el mismo checkout: cada una commitea solo lo suyo, sin stash, reset ni cambio de rama;
- si el checkout no compila por el trabajo de otra sesión, se prueba en una copia de `HEAD` dentro de una carpeta ignorada;
- con la app levantada, ninguna sesión edita el `src` del front;
- nada de computer use ni escritorio remoto, y ninguna clave real en manos de Claude;
- en las pruebas con vos, los pasos clic por clic y sin cargar datos por vos;
- para decidir, pocas opciones, de a una y con una captura o un tablero antes;
- las corridas largas, con la PC despierta.

Chocan con lo de acá:

| Regla del 03 | Acá | Qué hago |
| --- | --- | --- |
| Todo en `main`, sin ramas ni worktrees, ni temporales («fundamental») | `/puerta` arma dos worktrees; `realinear-docs` trabaja en una rama aparte | Pregunta 6 |
| Build y tests rápidos enteros en cada corte, nunca filtrados | El test dirigido y el build del proyecto afectado | Pregunta 7 |
| Si la corrida completa falla, se arregla y se repite otra completa | Solo lo que toca el arreglo, salvo autenticación, persistencia base o composición | Pregunta 7 |
| Una revisión por tarea | Una sola revisión, al cerrar la etapa | Pregunta 7 |
| Idioma: el original y una traducción opcional | La 35: el contenido no tiene idioma | Queda la 35, que ya elegiste |
| «Por aprobar» en solapas | La 148: un segmentado, porque acá no hay pestañas para eso (79) | Queda la 148 |
| Cada pantalla, en 1440, 390 y 320 | 390, 768 y 1440 | Se suma 320 a las pruebas (técnico) |

## 3. Qué cambia, por área

Cada tabla tiene solo lo que cambia; lo igual va en su última fila.

### 3.1 Diseño de toda la app

| Tema | Allá | Acá | Qué cambia |
| --- | --- | --- | --- |
| Que no parezca plantilla | Sin el cuadrado del logo, el ícono de sección en su cuadradito, pastillas, avatares redondos ni sombras (salvo los menús); el activo del menú, con una barra | [Tema](../../../ArquitecturaBaseMutitenantFront/docs/architecture/tema.md): el logo cuadrado con degradé, el avatar redondo con degradé, el activo en pastilla blanca y las tarjetas con sombra. 369 de los 410 tableros vivos tienen alguna de esas formas, y todas salen de las piezas compartidas | Pregunta 1 |
| Esqueletos de carga (ES1-A, ES2 y ES3, 06/11) | Un listado que carga muestra su encabezado y el aro en el alto de ocho filas, sin filas esqueleto; la página y los ajustes tienen su espera propia | `Criterio-Tablas` y 57 tableros dibujan filas esqueleto; el front pone el aro suelto | Se adopta: no choca con ninguna decisión |
| El estado, una vez (EP1-A) | Lo que dice el encabezado no se repite debajo, la acción que destraba va en la banda y el porqué, en el motivo del botón apagado | `pantallas-y-ui.md` ya dice «una vez, arriba», pero `Criterio-Decisiones` manda los pasos a la ⓘ | La regla general se adopta; la publicación, pregunta 3 |
| Decidir a propósito | «¿Aprobar VIS-000003?», con el foco en Cancelar | La 114, solo en la E21 | Pasa a regla de toda la app |
| Ninguna elección se descarta en silencio | Un cambio de tipo no pierde lo escrito sin avisar | La 113, solo en las acciones de la E21 | Pasa a regla de toda la app |
| Mucho texto, con jerarquía | Todo a la vista, sin plegar ni cortar; las fechas a la derecha, una vez por momento; lo poco consultado, detrás de un botón | La 138 a la 140, para las lecturas; `formatos.md` corta el texto largo con «…» (72 tableros) | Lo general se adopta; cortar o no, pregunta 2 |
| Lo nuevo vale para lo que ya existe | Una regla nueva se aplica también a las pantallas viejas | La 136 dice «para que lo cumplan las pantallas nuevas», y la 137, la 143, la 144 y los esqueletos no llegaron a los tableros viejos | Se enmienda la 136 |
| Ajustes | Cada campo mide lo que lleva (PF1-A) | Columnas iguales por caja, elegidas el 02/10 | Queda lo de acá |
| Igual | Botones y esperas, sin leyendas, la (i), tablas, lecturas, lo elegido, filtros, el 75 %, el idioma solo cuando es el otro, los ajustes como Configuración y el menú sin saltos | La 43, la 109, la 110, la 115, la 116, la 122, la 137, la 138, de la 142 a la 144, y el traspaso 9 | — |

### 3.2 El núcleo ya programado

| Tema | Allá | Acá | Qué cambia |
| --- | --- | --- | --- |
| Filtros sin cantidades | Un desplegable simple, sin cantidades (FO1-A) | La 143, pero los tableros Usuarios, Roles, Cuentas, Organizaciones, Empresas, Empresa, Recuperaciones, Campana y Campana-Plat, y la app, muestran la cantidad de cada opción | Se aplica: en los tableros y, con la base de la 12, en el front |
| El chat de WhatsApp en Mi cuenta (PF1-A) | Programado el 03/11; fuera de las 24 horas, los avisos quedan «Pausados» | La 159, con `Cuenta-21` por dibujar. Acá un aviso con una plantilla aprobada llega igual (61) | Se dibuja con «Llegan hasta el …» y, después, «Solo con plantilla», con su (i) |
| El ingreso por el bot y el consentimiento | Ningún mensaje lleva un enlace secreto: «Entrar a mi cuenta» lleva al ingreso, y el código se pide desde la web (ADR 0011). Invitar por WhatsApp pide marcar «La persona aceptó recibir mensajes por WhatsApp» | El bot de Plataforma manda un enlace de un solo uso para entrar (E8), y «Cargar teléfonos» invita sin preguntar (E11) | Pregunta 9 |
| Lo demás de WhatsApp | Los envíos sin confirmar no se repiten, una bandeja de solo lectura, el texto se borra a los 90 días; además, el historial de accesos, el estado de una invitación y desvincular un teléfono robado | La 80, la D3 y la D4 de la E11 lo resolvieron distinto; lo demás no existe acá | Quedan la 80, la D3 y la D4. Lo demás es nuevo: no lo recomiendo ahora |
| Altas | Dicen «Agregar …» | «Sumar empresa» en Usuario y en los datos de una persona | Se iguala |
| Distinto a propósito | La apariencia con colores propios, el tamaño de listado por instalación, la ubicación con Google Places, el chat simple y editar un usuario con sus medios de ingreso | El tema fijo Pradera, el tamaño en cada tabla, la dirección escrita, la bandeja completa de la E11 y la cuenta global | No se trae. «Último ingreso» en Mi cuenta es nuevo: no lo recomiendo ahora |

### 3.3 Catálogos y formularios (E12, E13, E14, E16 y E17)

| Tema | Allá | Acá | Qué cambia |
| --- | --- | --- | --- |
| El estado de la publicación (EP1-A, 06/11) | Programado. Sin zona ni pasos, y la etiqueta del encabezado dice el estado. «Consultar publicación» y «Repetir el mismo intento» aparecen solo cuando hacen falta. «Publicar en WhatsApp» apagado dice por qué | Spec 14 §9 y `FRM-Publicando`: los pasos y «Consultar ahora», detrás de la ⓘ | Pregunta 3 |
| La vista oficial (VO1-A, 06/11) | Programado: un diálogo de 480 × 840 que se recorre, con las primeras 20 opciones de cada catálogo | Spec 14 §6.5: al costado y sin tocar | Pregunta 4 |
| El mensaje que acompaña | Programado el 07/11: «Completá el formulario «Visita a cliente».», propuesto al elegir y sin pisar lo escrito | T10 y 8 tableros: «Completá este formulario, por favor.» | Se adopta (126) |
| La baja | «Lo dio de baja Ana Gómez el …»; al reactivar, la etiqueta dice el estado de la publicación | `FRM-Editor-Baja`: «Lo dio de baja» y «Cuándo», en pares | Se adopta (126) |
| El nombre del Flow en Meta | «Visita a cliente · v2 · es · ab63a1f9», hasta 80 caracteres | `form_<publicationId>_<wabaId>` | Técnico; acá, sin el idioma (35) |
| Lo probado con Meta | `TextSubheading`, `Dropdown` con datos y un mensaje de 34,7 KB con 331 opciones | Spec 14 §14 y §15: sin probar | Se anota, y acá se prueba igual |
| El aviso de opciones (157) | NI3-B | La spec está al día; `FRM-Publicar-16`, no | Se redibuja |
| Copiar y carpetas | Copiar, sin decidir; las carpetas (CP1-A a CP5-A), sin programar | Copiar es la 74 (E17); las carpetas, «después del bloque» | Copiar va adelante acá; las carpetas, se espera |
| Igual | Catálogos E1 y E2, K7, reintentos, deprecación, PU1-A, ZP1-A y títulos de sección | La 38, la 80, la 88, la 112, de la 117 a la 120, la 125, de la 129 a la 132, y la 145 | — |

### 3.4 Respuestas, carga y reglas (E15 y E18 a E21)

| Tema | Allá | Acá | Qué cambia |
| --- | --- | --- | --- |
| Cómo se agrega el bloque (RG10-D, 06/11) | Decidido: «Agregar bloque» al lado de «Agregar pregunta»; arranca abierto y con «Bloque» de nombre; con uno armado, el botón se apaga, y «Tipo de respuesta» deja de ofrecerlo | La 146: un tipo de pregunta «Renglones» | Pregunta 5 |
| «Ítem» en lugar de «renglón» (RG12-A, 07/11) | Decidido para todo texto que ve el usuario: «Productos · ítem 2», «¿Agregás otro ítem?», «Agregar ítem» | La 146, cuatro specs y 31 tableros dicen «renglón» | Pregunta 5 |
| Un mínimo de más de 10 ítems (RG13-A, 07/11) | Con WhatsApp, publicar frena: «En WhatsApp se cargan hasta 10 ítems; el mínimo es 15.» | La 146 solo trata el máximo de más de 10, que avisa sin frenar | Pregunta 8 |
| Un catálogo dentro del bloque | Filtra por quien lo recibe | La spec 19 lo deja afuera; la 146 no lo excluye | Se adopta (126) |
| «Grupo de líneas» | Reemplazado por el bloque | La 146 lo reemplazó, pero 11 tableros aprobados lo siguen dibujando | Se redibuja |
| La prueba con Meta | Tres variantes aceptadas; hace falta el `routing_model` con las dos ramas; de lo no visitado no llega nada; hoy la respuesta cae en «no pudimos relacionar…» | Spec 19: todo por probar | Se anota; acá se prueba igual antes de planificar |
| Corregir y anular | Programado. «Corregir», con su lápiz, va en la fila del título, y el ⋮ tiene solo «Anular», en rojo. «Motivo de la corrección» va al final, y el botón dice «Guardar corrección» | `FRM-Respuesta-Corregir` y `-Anular`: el ⋮ con Corregir, el PDF y Anular, y la zona «La corrección» arriba | Se redibuja (126) |
| Quién corrige una rechazada | El código deja corregir solo una Recibida o Por aprobar, sin una decisión que lo cambie | La 93: quien la cargó, mientras no esté aprobada | Queda la 93 |
| La carga | Programado: la zona plegada dice «N de M respondidas»; «Enviada» tiene «Volver a Formularios»; dice «Elegí primero «Campo»», y en el chat, un formulario con una lista dependiente aparece apagado, con su motivo | La spec 20 no tiene ninguna de las cuatro | Se adopta (126 y 110) |
| Lo propio | El motivo del rechazo, en los momentos del detalle | Una columna «Motivo del rechazo» en `FRM-Respuestas-Propias-21` | Se redibuja (126) |
| Filtros y «Mis cargas» | — | «Más filtros» en 14 tableros, aunque la 144 lo reemplazó; `FRM-Formularios-21` sin «Mis cargas» (153) | Se redibuja |
| Igual | Planilla, «Filtros», detalle, Excel, fotos y documentos, RP1-A, AP1-A, MD1-A, RT1-A, títulos, AR1-A y AR2-A | La 133, de la 139 a la 149, la 153 y la 158 | — |

### 3.5 IA, asistente y documentos (E22 a E25)

| Tema | Allá | Acá | Qué cambia |
| --- | --- | --- | --- |
| El catálogo que propone el asistente (H4) | Repensado el 04/11 (IA3-A, ADR 0024): nunca se crea solo; «Crear catálogo» sale de la tarjeta del Inicio o del resumen de WhatsApp. Se edita como cualquier catálogo, con «Descartar propuesta» y «Aprobar y activar», y guardar no lo activa | E23: queda «Por aprobar» apenas la IA lo propone y no se edita; la E25 dice «Aprobar y activar». Choca con la 154 | Pasa a la E25, con la tarjeta y editable, como allá: es lo más simple que cumple la 154 (126) |
| La información del negocio | Varias franjas por día, «24 horas», productos y servicios, políticas y condiciones, y «Aplicar de lunes a viernes» | El conocimiento de la E23 (`WA-Asistente-Limites`) tiene una franja por día | Se adopta (126) |
| La oferta del asistente | «Te preparé el formulario «<nombre>» para que lo completes.» | Los tableros: «Te paso el formulario para pedir el service.» | Se adopta (126) |
| El prellenado | Deja afuera «elegir varias» y las preguntas del bloque | La E23 no nombra el bloque | Se anota |
| La tarjeta del Inicio (IA1-A) | «Nueva», «Catálogo creado», sin permiso y «ya existe» | La 154; `Asistente-Org-25`, por dibujar | Se suman al texto de la E25 |
| El micrófono (IA4) | En el Inicio | La 156; la ruta del audio de la E25 no lleva el acceso | Solo en el Inicio de la organización, porque el costo es de su tope (156); se corrige la ruta |
| Armar por WhatsApp y los Casos | Allá no hay Casos programados | La 155 y los Casos de la E23 | Un bloque fijo, antes de los casos: lo más simple. Va en la E25 |
| Documentos | DOC6 usa la misma propuesta y, al publicar, los documentos que originaron el formulario quedan propuestos | La E25, con rutas propias | Se anota en la E25 |
| Biblioteca | La biblioteca de estándares, sin programar | La E22, propia | No aplica al modelo |
| Igual | Casos, lectura de documentos, sugerencias, audio, la espera y el aislamiento, y el motor único | La 39, la 41, la 46, de la 51 a la 53, la 63, de la 65 a la 71 y la 99; ADR 0048 | — |

### 3.6 Arquitectura, operación y ARCA

| Tema | Allá | Acá | Qué cambia |
| --- | --- | --- | --- |
| Decisiones sin ADR acá | 0019 (SQL acotado), 0021 (respuestas redactadas), 0022 (lectura de documentos), 0024 (la IA arma), y 0023 y 0025 (ARCA) | La 39, la 70, la 71, de la 154 a la 156, y de la 150 a la 152 | Cada ADR propio se escribe al planificar su etapa (E23, E25 y ARCA 1); la 0019, una fila del índice |
| Convenciones de allá sin par acá | 0008 (`Lock…`, escrituras con transacción, `Find` sin colecciones, `Count` que devuelve un entero), 0018 (robustez proporcional) y 0012 (la Api entrega el archivo con el permiso del recurso, sin URL firmadas) | `persistencia-ef.md` y `guardado.md` no las nombran; `estandares.md` (P11) propone una URL firmada | Se anotan en esas fichas, y P11 se alinea con la 0012 |
| Rendimiento | Brotli o gzip, `/assets` en caché por un año, una lectura de permisos por requisito, índices de registros y probes obligatorios | `SpaExtensions` solo pone `no-cache` en `index.html`, y el despliegue no exige los probes. Otra sesión está aplicando acá mejoras de rendimiento (los commits `perf` del 07/11) | A la E10 (B4), a la ficha de permisos y a la E14 y la E15; se coordina con esa sesión |
| Probar en el celular | Una guía nueva | Sin guía, y con subdominios el reenvío USB no sirve tal cual | Se adapta en la E20 |
| Advertencias del 03 | Cadenas de `AGENTS.md` por debajo de 30 KB, `core.autocrlf` en migraciones, `--artifacts-path` y `Logging:Debug` | La cadena más larga mide 16,6 KB; `core.autocrlf` está prendido; sin nota | Se anotan; el test de la cadena va con la próxima etapa |
| WhatsApp Flows | `routing_model`, `update_data`, `required` atado a `visible`, `label` de 30, el doble escape y `frame-src` | Nada escrito; la política de la E9 ya permite `*.facebook.com` | Al «no repetir» de la E14, la E19 y la E20 |
| ARCA | La etapa 1 programada, sin homologar | Hoja al día (de la 150 a la 152) y sin tableros | La hoja suma: el ticket va en PostgreSQL y no en Redis, los clientes SOAP propios, un ADR antes del robot de la etapa 6, y que la CUIT de una empresa no cambia con ARCA conectado (antes se desconecta); los tableros, cuando le toque |

## 4. El plan

### 4.1 Cómo

- **No se redibuja de cero.** El lienzo sigue el armado de allá desde la v148 (126), y casi todo lo que difiere sale de las piezas compartidas o de pocas decisiones. Se rehace lo que difiere, se suma lo que falta y se retira lo que sobra.
- **Primero la documentación.** Los tableros se dibujan desde ella.
- **Por tandas.** Cada una se dibuja y la aprobás en el lienzo antes de la siguiente.
- **La guía del lienzo se arma una sola vez,** al final.
- **No se programa nada.** Lo que cambia en el front ya programado queda en la spec de la base de la 12.

### 4.2 En qué orden

0. **Tus respuestas a §5**, de a una. La 1 va primero, porque cambia todos los tableros.
1. **La documentación**, un commit por tema:
   1. la hoja de ruta: las decisiones de la 160 en adelante (lo adoptado en §3 y tus respuestas), y la §3 de la E19, la E23 y la E25;
   2. las specs: la base de la 12, la 14, la 16, la 17, la 19, la 20, la 21, la 23 y la 25, según §3;
   3. las fichas del front: `pantallas-y-ui`, `datos-y-api`, `paginado-y-listados`, `formularios`, `formatos`, `responsive` y `tests`, y `tema.md`;
   4. el `AGENTS.md` de los dos repos: tus reglas del 03 y lo que respondas en la 6 y la 7;
   5. lo técnico: la E10, la hoja de ARCA, `migracion.md`, `tests.md` y el índice de decisiones;
   6. lo que no cierra entre los documentos de acá:
      - en la spec 25, las rutas de las propuestas van bajo `/api/organization/…`, y suma `forms.authoring.decline`, la propuesta en el `Message` del SSE de la E8b, la vida del audio, los topes del turno de la E8b y el error de un nombre o un prefijo en uso;
      - la spec 16 dibuja dos «Agregar filtro…», y la 134 pide uno con ▾;
      - la base de la 12 todavía dice «Más filtros» (144);
      - `features/whatsapp.md` conserva la D2, que la 159 supera;
      - el README del lienzo dice «un cartel debajo de la banda», `Criterio-Marco` dice «campos de su medida» y `Criterio-Menus` nombra la pestaña «Inglés» (35);
      - «Ver la vigente» pasa a «Ver la actual»;
   7. retirar los insumos que ya cumplieron: el [traspaso del 17/10](2026-10-17-formularios-traspaso/README.md), que esperaba las specs de la 15 a la 25, y la [evaluación del traspaso 2](2026-10-24-formularios-evaluacion-traspaso-2.md), que esperaba sus tableros. Antes, lo que sirva pasa a la hoja de ruta.
2. **El lienzo**, en cinco tandas (§4.3).
3. **Al final:** los índices y los recorridos del lienzo, las dos guías y la publicación en tandas. Te aviso para que allá se retiren los traspasos 2 a 10.

### 4.3 Qué tableros

«Pendientes» son los que ya estaban por redibujar o por dibujar desde los traspasos 5 a 9; con cada teléfono va su tablero `M-`.

| Tanda | Rehacer | Sumar | Retirar |
| --- | --- | --- | --- |
| **a · Piezas y criterio** | El pase de los generadores sobre los 410 tableros vivos: las formas de la pregunta 1, la línea entre columnas (137), los filtros sin cantidades (143 y 144) y la primera carga (ES1-A). Los ocho `Criterio-*` | — | — |
| **b · Núcleo** | `Usuarios`, `Roles`, `Cuentas`, `Organizaciones`, `Empresas`, `Empresa`, `Recuperaciones`, `Usuario`, `Campana` y `Campana-Plat`, con sus teléfonos, y `M-Usuario-Empresa` | `Cuenta-21`, pendiente | — |
| **c · E12 a E18** | E14: `FRM-Publicando` y `M-FRM-Editor` (preguntas 3 y 4); `WA-Chats-Formulario`, `WA-Chats-Formulario-Cliente`, `WA-Chats-Respuesta` y `WA-Mensajes-14` (el mensaje). E15: `FRM-Respuestas-Exportar`. E16: `FRM-Publicar-16`, pendiente; `WA-Chats-Formulario-16`, `FRM-Usuario-Datos` y `FRM-Editor-16`, al que le falta «Agregar título» (88). E17: `FRM-Editor-Baja`, con «Agregar título» también en sus versiones de la 21 y la 23. E18: `WA-Chats-Respuesta-18`, `WA-Chats-Visor` y `FRM-Editor-18` («Agregar título») | E14: `FRM-Vista-Oficial`, según la pregunta 4 | — |
| **d · E19 a E21** | E19: `FRM-Editor-Calculo` y `FRM-Editor-Condiciones`, y los pendientes `FRM-Editor-Lineas`, `FRM-Publicar-19`, `FRM-Respuestas-19`, `FRM-Respuesta-19` y `WA-Chats-Respuesta-19`. E20: `FRM-Editor-Listas`, `FRM-Publicar-20`, `FRM-Cargar-Lista` y `FRM-Cargar-Enviada`, y los pendientes `FRM-Cargar-Respuesta`, `FRM-Cargar-Panel`, `FRM-Respuestas-20`, `FRM-Respuestas-Propias`, `FRM-Respuesta-20` y `FRM-Formularios-20`. E21: `FRM-Editor-21`, `FRM-Respuesta-Corregir`, `FRM-Respuesta-Anular`, `FRM-Formularios-21` y `WA-Chats-Avisos`, y los pendientes `FRM-Editor-Reglas`, `FRM-Respuestas-21`, `FRM-Respuesta-Aprobacion`, `FRM-Respuestas-Propias-21` y `FRM-Cargar-Respuesta-21` | E21: `FRM-Editor-Reglas-Bloque`, las reglas con «algún ítem de…» (146), y `WA-Chats-Respuesta-21`, la tarjeta del chat con «Después se corrigió…» o «Después se anuló…», que la spec 21 pide | — |
| **e · E22 a E25** | E22: `Plat-FRM-Publicar` y `Plat-FRM-Plantilla`, sin el renglón «Estado» que repite la banda (EP1-A) y con el bloque según la pregunta 5, y los pendientes `FRM-Editor-Reglas-Plantilla` y `Plat-FRM-Plantilla-Reglas`. E23: `WA-Asistente-Limites` (la información del negocio) y los siete pendientes, con la oferta nueva | E25: los 21 pendientes, más `FRM-Catalogo-Propuesta-25` y `FRM-Plantilla-25` | E23: `FRM-Catalogo-Propuesta` y `FRM-Catalogos-23`, que pasan a la E25 |

ARCA suma sus cuatro tableros (el alta por pasos, la conexión activa, los estados y la empresa sin CUIT) cuando le toque, después del bloque, y rehace la pestaña de `Empresa`, ARCA entre los módulos de `Organizacion` y sus permisos en `Rol`.

## 5. Decisiones para vos

Te las pregunto de a una, después de tu visto bueno al plan, con la captura o el tablero de cada una.

1. **¿Dejamos de «parecer plantilla»?** Hoy el lienzo y la app tienen:
   - el logo en un cuadrado con degradé y el avatar redondo con degradé;
   - el ícono de cada sección en su cuadradito;
   - el activo del menú en una pastilla blanca;
   - pastillas en los filtros y los contadores;
   - sombras en los botones y las tarjetas.

   Allá no queda ninguna (compará `users-1440.png` de allá con `capturas/plan/etapa-12/catalogos-lista.png` de acá).
   - **A (recomendada):** sacarlas y conservar la paleta Pradera y la densidad:
     - el nombre, sin cuadrado, y la banda, sin ícono;
     - el avatar, un cuadrado chico con la inicial;
     - el activo del menú, con una barra;
     - los filtros y las etiquetas, con el radio de un control;
     - sombras solo en menús, desplegables y tooltips.

     Es tu regla más reciente, la 136 la lleva a toda la app y alcanza con un pase sobre las piezas del lienzo y seis piezas del front.
   - **B:** solo el marco (el logo, el avatar, el ícono y el activo); quedan las pastillas y las sombras.
   - **C:** dejarlo como está. Igual cambia el activo del menú, que no llega al contraste que pide la 142.

   **Elegida: A**, el 2026-11-07.
2. **¿Un texto largo se ve entero o se corta con «…»?** Por ejemplo, «Observaciones» en la planilla: allá la celda baja de renglón; acá se corta con «…» y su tooltip.
   - **A (recomendada):** entero, bajando de renglón, en las celdas y en las lecturas; «un dato por celda» sigue valiendo. Es tu regla «sin plegar ni cortar», y es una sola pieza.
   - **B:** «…» en las tablas, y entero en las lecturas.

   **Elegida: A**, el 2026-11-07.
3. **¿Cómo se ve el estado de una publicación?** Por ejemplo, «Visita a cliente» quedó sin confirmar.
   - Hoy: «● Sin confirmar», con una ⓘ que abre los pasos y «Consultar ahora».
   - Allá: la etiqueta, «Consultar publicación» a la vista y el porqué en el motivo de «Publicar en WhatsApp».

   Las opciones:
   - **A (recomendada):** como allá, con nuestras reglas. Sin pasos, y en la banda, solo cuando hacen falta:
     - «Consultar publicación»;
     - «Repetir el mismo intento»;
     - en «Necesita revisión», «Salió» y «No salió».

     Lo rechazado sigue marcado en su pregunta, sin la zona «Problemas» de allá, porque acá lo que falla se marca en su lugar.
   - **B:** como allá, con la zona «Problemas».
   - **C:** dejar la ⓘ con los pasos.

   **Elegida: A**, el 2026-11-07.
4. **¿Dónde va la vista oficial de WhatsApp?** Por ejemplo, la versión 4 de «Visita a cliente»: hoy va al costado, achicada y sin tocar; allá, «Ver vista oficial» abre un diálogo donde se recorre, con las primeras 20 opciones de «Motivo».
   - **A (recomendada):** el diálogo, como allá; al costado queda «Así lo ve quien lo recibe». La vista que se recorre pide 480 × 840 y al costado no entra.
   - **B:** al costado, achicada.

   **Elegida: A**, el 2026-11-07 (176).
5. **¿El bloque se arma como allá?** Por ejemplo, en «Pedido de materiales», «Productos» se suma con «Agregar bloque», al lado de «Agregar pregunta». En el celular se ve «Productos · ítem 2» y «¿Agregás otro ítem?». Hoy la 146 lo hace un tipo de pregunta «Renglones» y dice «renglón».
   - **A (recomendada):** los dos cambios, RG10-D y RG12-A. Allá salieron de probarlo en el celular: en el desplegable nadie encontraba el tipo, e «ítem» se lee bien con cualquier nombre de bloque. Cambian la 146, cuatro specs y 31 tableros.
   - **B:** solo «Agregar bloque», y sigue «renglón».
   - **C:** dejar la 146 como está.

   **Elegida: A**, el 2026-11-07 (177).
6. **¿Todo en `main`, también la puerta?** Allá marcaste como fundamental trabajar sin ramas ni worktrees, ni siquiera temporales. Acá la puerta arma dos worktrees, y el arnés que realinea los documentos trabaja en una rama aparte.
   - **A (recomendada):** como allá: la puerta y el arnés trabajan sobre una copia de `HEAD` en una carpeta ignorada, sin worktrees ni ramas.
   - **B:** la puerta sigue como está, como única excepción.

   **Elegida: A**, el 2026-11-07 (`AGENTS.md`).
7. **¿Cómo se verifica cada cambio?**
   - Allá: el build y los tests rápidos enteros en cada corte, otra corrida completa después de un arreglo y una revisión por tarea.
   - Acá, por tu pedido del 11/10: el test dirigido, solo lo que toca el arreglo y una sola revisión al cerrar la etapa.

   Las opciones:
   - **A (recomendada):** lo de acá, más los tests rápidos enteros en cada corte. Cuestan poco, y allá un filtro dejó pasar un test que rompió `main`.
   - **B:** lo de acá, sin cambios.
   - **C:** lo de allá, entero.

   **Elegida: A**, el 2026-11-07 (`AGENTS.md`).
8. **Con WhatsApp, ¿qué pasa al publicar un bloque con un mínimo de más de 10 ítems?** Por ejemplo, «Inventario de equipos» pide al menos 15, y WhatsApp carga 10 como mucho. Allá publicar frena (RG13-A).
   - **A (recomendada):** en la E19 frena, como allá. Desde la E20, si el formulario es para empleados o para los dos, se publica solo para la app, con su aviso, como un catálogo demasiado grande (96 y 104); si es solo para clientes, frena.
   - **B:** frena siempre, como allá.

   **Elegida: A**, el 2026-11-07 (178).
9. **WhatsApp de la cuenta: ¿dos cosas como allá?** Están programadas acá desde la E8 y la E11:
   - el bot de Plataforma le manda a una cuenta un enlace de un solo uso para entrar; allá ningún mensaje lleva un enlace secreto: «Entrar a mi cuenta» lleva al ingreso, y el código se pide desde la web;
   - «Cargar teléfonos» invita por WhatsApp sin preguntar; allá hay que marcar «La persona aceptó recibir mensajes por WhatsApp».

   Las opciones:
   - **A (recomendada):** las dos, como allá, con la base de la 12. Un enlace reenviado o abierto por otra persona le da la cuenta, y Meta pide el consentimiento para escribirle primero a alguien.
   - **B:** solo el consentimiento.
   - **C:** dejar las dos como están.

   **Elegida: A**, el 2026-11-07 (179).

## 6. En curso allá

Se tiene en cuenta, pero no se arma nada con esto hasta que llegue su traspaso:
- **Renglones, etapas 2 a 4:** la carga y las lecturas con los ítems, y «algún ítem con…». Cuando cierre la etapa 3, la spec 19 suma lo de allá:
  - el Excel, con la columna «Ítem» y una hoja con el nombre del bloque, de hasta 31 caracteres;
  - «En algún ítem de…», con todas las condiciones en el mismo ítem;
  - el PDF, con el bloque entero.
- **Elegir primero por un campo** (LD1-A a LD3-A), en programación. LD3-A choca con la E20, que hoy no admite una lista dependiente dentro del bloque.
- **La IA arma, etapas 2 y 3:**
  - las planillas con «Importar opciones»;
  - los documentos;
  - qué hace WhatsApp ante un pedido ambiguo.
- **Carpetas** (CP1-A a CP5-A), sin programar; acá van después del bloque, con dos listados y no tres (79).
- **Documentos por WhatsApp y Casos,** decididos y sin empezar allá; acá ya están en la E23 y la E25.
- **Rendimiento por elegir y para producción,** la homologación de ARCA, Azure y lo que falta decidir allá:
  - el teclado de Roles;
  - «Desvincular» sin el módulo de WhatsApp. Acá conviene revisar lo mismo al quitar WhatsApp.
- **Sin probar a mano allá:**
  - EP1-A y los esqueletos;
  - importar y exportar opciones;
  - las listas dependientes;
  - el menú sin saltos.

  Acá nada de eso se toma como probado.
