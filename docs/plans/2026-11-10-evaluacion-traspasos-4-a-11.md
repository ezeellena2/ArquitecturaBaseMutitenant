# Evaluación de los traspasos 4 a 11 de ArquitecturaBase

**Estado:** insumo · se retira cuando sus decisiones pasen a la [hoja de ruta](2026-10-12-formularios-dinamicos-hoja-de-ruta.md), las [specs de formularios](formularios/README.md) y las reglas del front.

Fuente: `ArquitecturaBase` en `5cf993f` (2026-11-09) y `ArquitecturaBaseFront` en `acc37bd` (2026-11-08). Son los traspasos 4 a 11 y lo que cerró después sin traspaso propio. Se leyó contra este repo en `c339b58`, con la hoja de ruta hasta la decisión 108; los traspasos se escribieron contra ese mismo punto. Rige la condición de siempre: nada de ArquitecturaBase pasa por encima de `AGENTS.md` ni de `docs/rules/`, y lo que choca con una decisión o una regla de acá lo decide el usuario.

| Traspaso | Fecha | Qué trae | Toca |
| --- | --- | --- | --- |
| [4](../../../ArquitecturaBase/docs/plans/2026-11-02-traspaso-multitenant-ajustes-de-la-prueba.md) | 02/11 | Dos reglas de diseño para toda la app; catálogos, publicar y aprobar, después de la prueba completa | E12 a E16 y E21 |
| [5](../../../ArquitecturaBase/docs/plans/2026-11-03-traspaso-multitenant-lecturas-y-filtros.md) | 03/11 | Tablas, lecturas, lo elegido, filtros, el detalle, las reglas en pasos y el armado de la carga; diseña renglones, ARCA y carpetas | E12 a E23 |
| [6](../../../ArquitecturaBase/docs/plans/2026-11-06-traspaso-multitenant-arca-conexion.md) | 06/11 | ARCA, etapa 1: conexión con el certificado, padrón y punto de venta | Después del bloque (76) |
| [7](../../../ArquitecturaBase/docs/plans/2026-11-06-traspaso-multitenant-carga-etapas-2-a-5.md) | 06/11 | La carga desde la app, etapas 2 a 5 | E14, E15, E20 y E21 |
| [8](../../../ArquitecturaBase/docs/plans/2026-11-06-traspaso-multitenant-ia-arma-formularios.md) y [11](../../../ArquitecturaBase/docs/plans/2026-11-08-traspaso-multitenant-ia-pregunta-y-corrida.md) | 06/11 y 08/11 | La IA arma formularios y catálogos, etapa 1, con la corrida real | E23 y E25 |
| [9](../../../ArquitecturaBase/docs/plans/2026-11-06-traspaso-multitenant-ajustes-de-toda-la-app.md) | 06/11 | Idioma, reintentar un aviso, Mi perfil, el menú sin saltos y controles de la plantilla | Front, E16 y E21 |
| [10](../../../ArquitecturaBase/docs/plans/2026-11-07-traspaso-multitenant/README.md) | 07/11 | ArquitecturaBase entero, para rehacer maqueta y documentación: lo nuevo, lo pendiente, las reglas del usuario y dónde leer | Todo |

Después del 10 cerraron allá, sin traspaso, las cuatro etapas de los [renglones](../../../ArquitecturaBase/docs/plans/2026-11-03-renglones-en-formularios.md) y [elegir primero por un campo](../../../ArquitecturaBase/docs/plans/2026-11-07-elegir-primero-por-un-campo.md). Los dos están verificados con Meta; la prueba en el celular quedó para la prueba integral final de allá.

## 1. En pocas palabras

- **La primera pregunta es el alcance** (§2): el traspaso 10 pide rehacer maqueta y documentación desde ArquitecturaBase entero, y la 108 dice que los diseños son de acá.
- **Lo que no choca se adopta** (§3): reglas para toda la app, ajustes de cada etapa, la lista de «no repetir» y reglas de trabajo del usuario.
- **24 preguntas para el usuario** (§4), cada una con la recomendada primero. La recomendada es casi siempre lo que el usuario eligió en ArquitecturaBase, que es posterior a la decisión de acá. Donde el modelo multitenant pide otra cosa, está el porqué.
- **Los tableros que cambian** (§5) los redibuja Diseño cuando estén las respuestas.
- **Lo técnico** (§6) lo decide quien programa, y **lo que no aplica** está en el §7.

## 2. El alcance

El traspaso 10 dice que el usuario quiere que este repo vuelva a revisar ArquitecturaBase entero y rehaga su maqueta y su documentación. La 108, del 2026-11-01, fija otra forma: las pantallas se dibujan con las specs, las decisiones y las piezas de acá, y el lienzo de ArquitecturaBase es solo un ejemplo.

**Recomendada: incorporar decisión por decisión, sin rehacer desde cero.** Se adopta lo que no choca (§3), el usuario decide los choques (§4) y Diseño redibuja solo los tableros del §5. Lo aprobado en el lienzo (v147) y en las specs se conserva donde no cambia. Casi todo lo de ArquitecturaBase ya está en la hoja de ruta (decisiones 20 a 108). Rehacer todo obliga a redibujar más de cien tableros y reescribir catorce specs para llegar casi al mismo lugar.

La otra opción: rehacer el lienzo y las specs tomando ArquitecturaBase entero como base, y enmendar la 108.

## 3. Se adopta sin preguntar

Nada de esto choca con una decisión ni con una regla de acá.

**Reglas para toda la app** (en el front: `pantallas-y-ui.md`, `formularios.md`, `tema.md` y los tableros «Criterio»):
- sin leyendas que expliquen: la ayuda que sirve va en la ⓘ (su forma es la P3) y lo demás se saca. Lo apagado dice por qué en su tooltip;
- tablas: una línea fina entre columnas, la tabla cierra abajo y un código no se parte en sus guiones. Los números a la derecha ya estaban;
- lecturas:
  - lo que pasa en orden va en pasos, con su ícono;
  - los datos de un registro van en dos columnas;
  - dos versiones se comparan lado a lado, solo con lo distinto;
  - lo elegido en otra página va primero en la lista;
- lo que carga no mueve lo que ya se ve, y un bloque de carga se distingue de su superficie;
- el estado se dice una vez, en el encabezado (EP1-A);
- una pantalla de ajustes se arma como Configuración, y un dato con dos usos dice el estado de cada uno;
- quitar va en rojo, y las altas dicen «Agregar …»;
- un texto del sistema nombra un idioma solo cuando no es el del menú, como «Le llega en …» (NI1-A);
- ninguna elección se descarta en silencio: cambiar un tipo conserva lo escrito;
- una acción que no se deshace y dispara avisos pide confirmar con el número, como «Aprobar» (AP1-A).

**Por etapa:**

| Etapa | Qué suma |
| --- | --- |
| E12, E13 y E16 | Un solo combo con búsqueda para elegir un catálogo, en campos, datos de las personas y preguntas, con los dados de baja apagados y su motivo. Importar en un diálogo, con «Origen» (archivo o pegar) y la revisión debajo; lo pegado, en una grilla con las columnas del catálogo (IM1-A a IM3-A). |
| E14 | Publicar por zonas (PU1-A): «Problemas», «Avisos» y «Qué se publica», con el límite en concreto («Hasta 20 caracteres; tiene 26.»). La vista oficial en modo interactivo, con las primeras 20 opciones de cada catálogo (VO1-A). El nombre del Flow en Meta, y el mensaje que nombra el formulario cuando el operador no escribe nada. Títulos de sección: hasta 40, nunca entre las preguntas de un bloque, y sin títulos el Flow sale igual; Meta ya los aceptó. |
| E15 | El detalle (TL1-A y TL3-A): el valor primero, los datos del catálogo debajo y los momentos con su fecha a la derecha. La actividad va en cuatro columnas fijas, con cada persona de un aviso en su renglón. Las acciones del encabezado van como íconos junto a ↑ ↓ ×. El historial, detrás de un botón (HI1-A). |
| E20 | Dos anchos (AR1-A). Otra clave de idempotencia después de cada envío. Listas dependientes: el asistente no ofrece el formulario, «Enviar formulario» lo muestra apagado con su motivo, la lista no lleva los límites de WhatsApp y se revisa al enviar. |
| E21 | «Adjuntar el PDF» (RP1-A): dice al lado qué falta, lo arregla con un botón y no se desmarca solo; con la 106, también vale al recibir. Reglas en pasos (LR1-A). Corregir y anular: los avisos salen otra vez, la tarea abierta de la anterior se cierra sin decisión, las listas son las de quien la cargó, el conteo es de las vigentes y el `Down` se niega a perder datos. Se conserva lo de acá, que es más estricto: quien corrigió no aprueba. Quien ve respuestas sin el rol de la tarea ve la aprobación de solo lectura. |
| E23 y E25 | Un pedido ambiguo pregunta antes de proponer. Un prefijo pedido que no cumple el formato se cambia por el válido más parecido, y se muestra. Las aclaraciones y los «no se puede» van sin cifras, y cada regla que valida el servidor va también en lo que lee el modelo. «Armar con la IA» va junto a «Nuevo formulario», «Nuevo catálogo» y «Formularios que faltan». |

**Lo que no hay que repetir:** las listas de cada traspaso (4 §6, 5 §12, 6 §4, 7 §4, 8 §4 y 11 §5) se enlazan desde la §4 de la hoja de ruta, sin copiarlas.

**Reglas de trabajo del usuario** (traspaso 10, `03`). Se suman a `AGENTS.md` de los dos repos porque no chocan:
- varias sesiones trabajan en el mismo checkout: cada una commitea lo suyo, nunca stash, reset ni cambio de rama;
- con la app levantada, ninguna sesión edita el `src` del front;
- en las pruebas de punta a punta, Claude no carga datos por el usuario: le da los pasos, clic por clic;
- Claude nunca guarda, copia ni imprime claves reales, y no maneja la PC del usuario;
- «Algo salió mal» sin código casi siempre es el esquema del front que rechazó un valor nuevo: se reproduce con un test.

## 4. Lo que decide el usuario

| | Tema | Recomendada | La otra | Toca |
| --- | --- | --- | --- | --- |
| P1 | Alcance (§2) | Incorporar decisión por decisión, con la 108 | Rehacer maqueta y specs desde ArquitecturaBase entero | Todo |
| P2 | El botón que trabaja | El aro y su gerundio («Guardando…»), sin atenuarse ni aceptar otro clic; una confirmación queda abierta hasta la respuesta (LC1-A) | Grisado con su mismo texto (43 y base de la 12) | 43, base de la 12 y todas las etapas |
| P3 | La ⓘ | Un tooltip, que es parte de la descripción accesible del control | Un aviso (`ImportantNotice`), como hoy | Front |
| P4 | Lo elegido | Algo más que un tinte (OE1-A): el segmentado relleno, la tarjeta con borde de 2 px, la tilde en un desplegable y la conversación con barra | El segmentado blanco con sombra, como hoy | `tema.md` y Criterio |
| P5 | Filtros de un listado | Sin la cantidad por opción ni opciones apagadas (los segmentados conservan su número). Cada cambio se aplica al instante, lo aplicado va en chips con «Limpiar todo», sin contador, y el resto va en un solo «Filtros» (FO1-A y PL1-A) | La cantidad por opción, y «Más filtros» con borrador hasta aplicar | Front y todos los listados |
| P6 | «Por aprobar» | Dos solapas en la planilla: «Por aprobar (N)» y «Todas» (PL1-A) | Una opción del filtro «Estado», con la campana y «Para resolver» (79) | 79, E15 y E21 |
| P7 | Zonas plegables | Dos excepciones escritas más: el editor de una pregunta de catálogo (ZP1-A) y el alta de ARCA (AC1-A) | Solo los permisos y las excepciones de hoy | Front, E14 y ARCA |
| P8 | Opciones y campos de un catálogo | Cada uno en un modal de lista y detalle (CO1-B y «Campos de…») | La tabla editable en el lugar, como hoy | E12 y E13 |
| P9 | Exportar opciones | Un Excel que se lee como en pantalla, con números y fechas de Excel, y que se reimporta sin cambios | El valor canónico como texto (C10) | E12 |
| P10 | Filtrar una pregunta con opciones | Un combo de una opción, con «Todas» y búsqueda, sin filtro de presencia; fotos y documentos salen de «Filtros». El contrato no cambia (PL2-A) | «Alguna de» y «con o sin respuesta», como hoy | Hoja de ruta §3, E15, E18 y E19 |
| P11 | El mensaje de una regla | «Agregar dato» pone etiquetas, «Así llega» muestra el ejemplo y se guarda la identidad de la pregunta (MD1-A) | `{pregunta}` escrita a mano | E21 y E22 |
| P12 | «Mantener las respuestas» | También en la página de la carga (AR2-A) | Solo en el panel (84) | 84 y E20 |
| P13 | Quien solo carga, en «Formularios» | «Mis cargas» y «Mi última carga» en cada formulario | Sin conteos (83) | 83 y E20 |
| P14 | Reintentar un aviso a un rol | Por persona, en su renglón, solo con la falla confirmada (RT1-A) | Por ejecución, como hoy | E21 |
| P15 | El aviso de opciones por persona | Solo cuando no entran en WhatsApp o nadie ve ninguna (NI3-B) | Siempre, con el máximo por persona | 37 y E16 |
| P16 | Renglones | El bloque de ArquitecturaBase (RG1-A a RG13-A): «Agregar bloque», «ítem», mínimo y máximo hasta 100, y publicar frena un mínimo mayor que 10. En WhatsApp, una pantalla por ítem hasta 10, con 64 KiB por respuesta. Reglas y filtros con «algún ítem con…» | El «grupo de líneas» de la E19 (54), con hasta 6 columnas y sin reglas ni filtros sobre los renglones | 54, E19, E21 y E25 |
| P17 | Elegir primero por un campo | Sumarlo (LD1-A a LD3-A): la pregunta pide primero un campo del catálogo («Rubro») y lista sus opciones, también en WhatsApp. Va en la E16, y LD3-A con los renglones | No por ahora | E16 y E19 |
| P18 | Quién crea lo que arma la IA | Una persona, con un botón en la tarjeta de la propuesta: la IA propone y nunca crea (IA1-A) | El chat lo crea al pedirlo (75) | 75 y E25 |
| P19 | Armar por WhatsApp | Sí, con un resumen y botones, y «pregunta y espera» ante un pedido ambiguo (IA3-A) | Solo en la web, como hoy | E25 |
| P20 | Micrófono en el chat del Inicio | Sí, en cada organización cuya transcripción acepte lo que graba el navegador | Sin audio, como hoy | E25 y `assistant` |
| P21 | El estado del chat de WhatsApp | En «Mensajes», por organización, con hasta cuándo llegan los avisos | En «Mi cuenta» (PF1-A) | Personal |
| P22 | ARCA: de quién es la conexión | De cada empresa, con la CUIT de su ficha | De la organización | Después del bloque |
| P23 | ARCA: el módulo | Un módulo propio, `Arca`, con su `[FeatureGate]`, quitable y con una carpeta por familia | Dentro de `Integrations` | Después del bloque |
| P24 | Pruebas | En cada corte, los tests rápidos enteros, nunca filtrados por carpeta; una pantalla nueva también se prueba a 320 px | Los tests de la carpeta y 1440 y 390, como dice `AGENTS.md` | `AGENTS.md` y tests |

**Por qué, donde hace falta:**
- **P1:** se conserva la 108, que el usuario decidió hace nueve días, y se llega al mismo producto con mucho menos trabajo.
- **P8:** la regla del front ya dice «editar o agregar abre diálogo» (`formularios.md`). La tabla editable de hoy es la excepción.
- **P10:** el contrato conserva las cantidades y la presencia, que siguen sirviendo a reglas y sugerencias; cambia solo lo que muestra el front.
- **P13:** la 83 vino de ArquitecturaBase, y allá, al programarla, se sumó «Mis cargas».
- **P21:** no es lo de ArquitecturaBase. Acá cada organización tiene su número, así que el estado del chat es por organización, y «Mi cuenta» es de toda la cuenta.
- **P22:** una organización puede tener varias empresas, cada una con su CUIT en la ficha, y ARCA trabaja por CUIT.
- **P24:** la regla de allá es posterior a la de `AGENTS.md`: un filtro por carpeta dejó pasar un test que rompió `main`.

## 5. Los tableros que cambian

Diseño los redibuja después de las respuestas, con las piezas de acá. Los de ArquitecturaBase son solo un ejemplo (108), en las filas que nombra cada traspaso.

| Dónde | Tableros | Por |
| --- | --- | --- |
| Criterio | `Criterio-Botones`, `Criterio-Avisos`, `Criterio-Tablas`, `Criterio-Menus`, `Criterio-Decisiones` y `Criterio-Dialogos` | §3 y P2 a P7 |
| E12 y E13 | `FRM-Catalogo`, `FRM-Catalogo-Importar` y `FRM-Catalogo-13`, con sus teléfonos | §3, P8 y P9 |
| E14 | `FRM-Editor`, `FRM-Publicar`, `FRM-Publicando`, `FRM-Catalogo-14` y `WA-Chats-Formulario` | §3 y P7 |
| E15 | `FRM-Formularios-15`, `FRM-Respuestas`, `FRM-Respuestas-Filtros` y `FRM-Respuesta` | §3, P5, P6 y P10 |
| E16 | `FRM-Editor-16` y `FRM-Publicar-16` | P15 y P17 |
| E19 | `FRM-Editor-Lineas`, `FRM-Respuestas-19`, `FRM-Respuesta-19`, `WA-Chats-Respuesta-19` y `FRM-Publicar-19` | P16 |
| E20 | `FRM-Formularios-20`, `FRM-Cargar-Respuesta`, `FRM-Cargar-Panel` y `FRM-Cargar-Enviada` | §3, P12 y P13 |
| E21 | `FRM-Editor-Reglas`, `FRM-Respuesta-Aprobacion` y `Campana-21` | §3, P11 y P14 |
| E25 | `FRM-Armar-Con-IA` y `Asistente-Org-25`, y el resumen por WhatsApp | P18 a P20 |
| Personal | «Mensajes» | P21 |
| ARCA | El alta, la conexión activa y sus estados, por dibujar | P22 y P23, cuando ARCA entre en la hoja de ruta |

## 6. Lo técnico

Lo decide quien programa, y va a estos lugares:

| Tema | Dónde va |
| --- | --- |
| Cuatro controles de arquitectura: las claves huérfanas de `Errors.resx`; un `ExecuteInTransactionAsync` por método público, contado en el IL; `ToString()` en todo contrato con datos privados, también en los módulos y con `ApiKey`, `DisplayName` y `Number`; y lo que devuelve un lector (`Row`, `Response` o `Counts`) | [Backlog](../backlog.md) 4 y 56, más un ítem para los otros dos |
| Quitar formularios: la vuelta atrás de la migración de retiro, y los tests de otros módulos que dan por instalado el módulo | E14, fila «Retiro» |
| Meta: `routing_model` declarado con un `If`; `update_data` desde Flow JSON 6.0; `required` atado a `visible`; el `label` de 30 caracteres; `flow_action_payload`, que se decodifica dos veces; y el marco de `business.facebook.com` en la CSP | `14-parte-archivos-tests-y-meta.md` |
| El agente de IA de Meta: con él como primer respondedor, los mensajes llegan por `standby` y la app no los ve | La guía de operación de WhatsApp |
| Los logs con el depurador de Visual Studio: el proveedor Debug baja la Api de unos 700 a unos 11 pedidos por segundo | `appsettings.Development.json` de la Api, después de medir |
| Índices de la planilla: las respuestas vigentes por formulario y número, y las de cada autor | E15 y E20, en su parte de datos |

Del rendimiento (traspaso 10, `01` §4), lo demás ya está:
- los permisos salen de la caché, sin una consulta por alternativa;
- el índice del último ingreso existe;
- los probes, la caché y la compresión de los estáticos están en el B4 de la E10.

Hay dos diferencias a propósito:
- acá el JSON de la API no se comprime, porque algunas respuestas llevan secretos por HTTPS;
- la primera carga de una tabla sigue los tableros de acá.

## 7. Lo que no aplica

- NI2-C, el renglón «Idiomas» al publicar: acá el contenido no tiene idioma (35).
- La escala de escritorio al 75 % y lo visual propio de ArquitecturaBase (colores, bandas y avatares): acá mandan el lienzo y `tema.md`.
- La clave visible de un campo: acá es fija y oculta, y la regla de la clave coincide.
- Las rutas de lo propio: acá son rutas propias y allá un guard, con el mismo efecto.
- Lo que sigue sin programar allá entra con su traspaso, cuando cierre:
  - las carpetas (CP1-A a CP5-A);
  - las etapas 2 y 3 de la IA;
  - las etapas 2 a 6 de ARCA;
  - los documentos y los casos del asistente.
- El arnés de calificación de ArquitecturaBase y lo propio de Windows.

## 8. Después de las respuestas

1. Las elegidas pasan a la §5 de la hoja de ruta como decisiones nuevas, desde la 109, y su §7 vuelve a «Ninguna».
2. Cada spec y su texto para Diseño suman lo del §3 y lo elegido. La base de la 12 cambia si la P2 cambia la 43.
3. Las reglas del front y `AGENTS.md` suman lo del §3.
4. Lo técnico del §6 va a sus lugares.
5. Diseño redibuja los tableros del §5.
6. Esta evaluación se retira, y en ArquitecturaBase se pueden retirar los traspasos 4 a 11.
