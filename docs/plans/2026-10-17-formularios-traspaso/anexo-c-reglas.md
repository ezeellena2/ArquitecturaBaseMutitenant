> Anexo del traspaso. Las rutas y siglas de este anexo son relativas al repo `ArquitecturaBase` (backend) salvo las que dicen `Front:`; los documentos retirados se recuperan allá con el `git show` indicado.

# Formularios por WhatsApp: reglas de negocio, decisiones, premisas de proceso y lecciones

Traspaso para que otro equipo replique el desarrollo. Corte: **2026-10-17**. Backend `ArquitecturaBase` en `main`, HEAD `b43edf8f`; front `ArquitecturaBaseFront` en `main`, HEAD `f8212b2`. Los dos árboles tienen cambios sin commitear de las correcciones en curso del review de la Etapa 4; acá no se evalúan.

## Cómo leer este documento

Cada bloque cita su fuente. Las rutas son relativas al repo del backend salvo que digan `Front:`. Las fuentes retiradas se recuperan con el `git show` indicado.

| Sigla | Fuente |
| --- | --- |
| AG-B | `AGENTS.md` (raíz del backend) |
| AG-F | `Front: AGENTS.md` |
| VAL | `docs/guides/validar-un-cambio.md` |
| AUD | `docs/guides/auditar-documentacion.md` |
| PRE | `docs/architecture/premisas-de-la-plantilla.md` |
| BE | `docs/architecture/backend.md` |
| FORM | `docs/features/formularios.md` |
| WA | `docs/features/whatsapp.md` |
| IA | `docs/features/inteligencia-artificial.md` |
| IDX | `docs/plans/formularios/README.md` (índice de etapas) |
| E2 | `docs/plans/formularios/etapa-2-catalogos.md` |
| E2B | retirado: `git show ff1c507f^:docs/plans/formularios/etapa-2b-fotos-y-documentos.md` |
| E3 | retirado: `git show bef6d615^:docs/plans/formularios/etapa-3-planilla.md` |
| E4 | retirado: `git show a56ebb2c^:docs/plans/formularios/etapa-4-reglas-aprobacion-pdf.md` |
| E5 | `docs/plans/formularios/etapa-5-asistente.md` |
| PLAN | retirado: `git show 4b151eb6^:docs/plans/2026-10-07-formularios-dinamicos-y-whatsapp-flows.md` (plan principal, 183 KB) |
| ACU | retirado: `git show 4b151eb6^:docs/plans/formularios-modelo-demo/acuerdos-2026-10-08.md` |
| ARR | retirado: `git show 4b151eb6^:docs/plans/formularios-etapa-1-arranque.md` (coordinación de la Etapa 1) |
| R1009 | retirado: `git show 93178685^:docs/reviews/2026-10-09-formularios-incremento-0.md` |
| R1008 | retirados: `git show 4b151eb6^:docs/reviews/2026-10-08-formularios-whatsapp-plan.md` y `...-acuerdos.md` |
| R1012 | `docs/reviews/2026-10-12-code-review-etapa-2.md` |
| R1014 | `docs/reviews/2026-10-14-code-review-etapa-2-k6-k7.md` |
| R1015 | `docs/reviews/2026-10-15-code-review-etapa-2b.md` |
| R1016 | retirado: `git show a56ebb2c^:docs/reviews/2026-10-16-code-review-etapa-3.md` |
| R1017 | `docs/reviews/2026-10-17-code-review-etapa-4.md` |
| HIST | `docs/history/README.md` |
| VER | `docs/plans/verificacion-y-produccion.md` |
| PLANS | `docs/plans/README.md` |
| DIS-F | `Front: docs/design/formularios-whatsapp.md` |
| ADR NNNN | `docs/decisions/NNNN-*.md` |

Aclaraciones de procedencia:

- **No hubo un resumen propio de la Etapa 1** en `docs/plans/formularios/`. La Etapa 1 se planificó en PLAN, ACU y ARR, que se retiraron el 2026-10-11 (commit `4b151eb6`). Su cierre vive en FORM, «Verificación de cierre — 2026-10-11», y el índice IDX enlaza ahí.
- **Reviews:** los del 12, 14, 15 y 17 de octubre **siguen versionados** en `docs/reviews/`. Solo el del 16 (Etapa 3) se retiró, en `a56ebb2c`.
- **«Un solo chat de Codex».** Los documentos dicen «un solo chat, backend y front juntos» sin nombrar la herramienta. Que fuera Codex sale del pedido de este traspaso, no de los documentos.

### Vocabulario

- **Definición:** el formulario editable. **Borrador:** su contenido mutable con `DraftRevision`. **Versión:** la copia inmutable de las preguntas que se publica.
- **Publicación:** la operación durable que crea la versión, la lleva a Meta como Flow y la activa.
- **Sesión:** un envío concreto a un contacto. Fija versión, idioma, cuenta y oferta de catálogo.
- **Respuesta o registro:** lo que contestó el empleado, numerado `PREFIJO-NNNNNN`.
- **Catálogo:** una lista compartida de opciones, Manual o Filters.
- **Tablero:** el dibujo de una pantalla en el lienzo (Artifact «Sistema visual — ArquitecturaBase»). **Versión del lienzo:** la numeración de esas entregas de diseño.
- **Corte:** un avance chico dentro de un paso. **Paso o etapa:** la unidad que se cierra con la suite completa. **Piloto:** la prueba con proveedores reales.
- **Perfil r3…r7:** las revisiones del contrato técnico del Flow JSON que genera el compilador.

---

## 1. Premisas de proceso

### 1.1 El tablero se dibuja y se elige antes de programar

- **Regla.** «Antes de programar una pantalla nueva, dibujala como tablero en el Artifact del sistema visual […] con su funcionalidad completa: estados de carga, vacío y error, qué pasa sin permiso, el recorrido de diálogos y qué vive en la URL. Se programa después de una elección explícita del usuario; programar primero y dibujar después no cuenta.» El tablero arranca de una plantilla del lienzo y se arma con las piezas de la biblioteca ArquitecturaBase UI. (AG-F, «Reglas adicionales»; AG-B, «Otras reglas fijas»: «Una pantalla nueva se dibuja antes de programarse».)
- **No copiar a ciegas.** Contratos funcionales, permisos, accesibilidad, i18n y manejo de errores tienen precedencia sobre el dibujo. Las decisiones aprobadas terminan en tokens de `src/index.css`, componentes de `src/shared/ui` y tests (AG-F).
- **Congelar lo elegido.** El Artifact es privado y puede cambiar fuera de Git. Por eso DIS-F y sus capturas en `Front: docs/design/evidence/formularios-whatsapp/` congelan la elección. El usuario pidió implementarlo «tal cual» y empezar de a poco. Las notas naranjas numeradas son notas, no UI. Los datos (Juan Gómez, VIS-000024) son ficticios. Cada parte se arma con su pieza de `shared/ui`, no copiando el dibujo (DIS-F, encabezado y «Cómo leer los tableros»).
- **Secuencia de elecciones del lienzo** (PLAN §11 y §14, E3, E4, E5, DIS-F):

  | Versión | Fecha | Qué se eligió |
  | --- | --- | --- |
  | 54 | 2026-10-10 | Etapa 1, incremento 1: A listado, B alta, C prefijo, D conflicto |
  | 55 | 2026-10-10 | Incremento 2: P1 publicar, P2 publicando y resultado, P3 vista |
  | 56 | 2026-10-10 | E idiomas (incremento 1) y C1–C5, selección y envío desde el chat (incremento 3) |
  | 57 | 2026-10-10 | R1–R3: llega la respuesta, quién la ve y cómo se muestran los valores (incremento 4) |
  | 61 | 2026-10-10 | K1–K7 de catálogos, como propuesta |
  | 65 | 2026-10-10/11 | Diseño de las cinco etapas cerrado: K1–K7, T1–T4, G1–G6, H1–H5 y s1–s4 |
  | 66 | 2026-10-11 | F1–F5 (fotos y documentos) y T5 (exportar), elegidos ese día |
  | 71 | 2026-10-16 | T1 y T3 con fotos y documentos |
  | 75 | 2026-10-16 | Ajustes de G2 («Incluir las fotos») y G5 («Reintentar») |
  | 78 | 2026-10-17 | Recorrido guiado de punta a punta, en ocho capítulos |
  | 79 | 2026-10-17 | D2: detalle de una respuesta en panel lateral (reemplaza T3, G4 y G5) |

- **Las maquetas sueltas no se promueven.** Las del 7 y 8 de octubre (editor por pestañas, laboratorio, dossier) quedaron como «procedencia sustituida»: no se siguen mejorando ni son referencia de UI (PLAN, encabezado y §14; DIS-F).
- **Estados que faltan.** Hay una excepción local autorizada «para estados realmente ausentes con componentes reales», que se presentan antes de montarse. No reemplaza el diseño (PLAN §11).
- **Conflictos entre el tablero y las reglas.** «No se modifica el diseño elegido para resolver contradicciones silenciosamente.» PLAN §11 los lista en una tabla con una propuesta cada uno, y se decidían explícitamente. Por ejemplo, el caso 6 del tablero K7 muestra el valor rechazado, pero vale la regla de nombrar la pregunta y no el valor (E2, «Decisiones tomadas»).

### 1.2 Un resumen de una página por etapa, con decisiones que no se reabren

- **Regla.** «Para trabajar en formularios se lee solo el resumen de la etapa en curso: tiene el alcance y las decisiones ya tomadas, que no se vuelven a preguntar» (IDX; AG-B y AG-F, «Forma de trabajo»).
- **Origen.** El 2026-10-11 el plan principal de 183 KB y sus contratos se condensaron en un resumen por etapa, por pedido humano, «para que los agentes no lean más de 300 KB antes de cada tarea» (HIST, «Planes de formularios retirados el 2026-10-11»).
- **Estructura usada en E2, E2B, E3, E4 y E5:**
  1. **Estado**, con fecha.
  2. **Qué se construye:** por tablero, separando backend y front.
  3. **Decisiones tomadas**, con la frase literal «No se vuelven a preguntar.».
  4. **Fuera de esta etapa.**
  5. **Cómo se cierra:** checks, TDD, casos con Docker y piloto real.
  6. **Requisitos previos:** permisos, dependencias y decisiones técnicas.
  7. **Ajustes ya decididos**, fechados.
  8. **Pendientes que se arrastran.**
  9. Cierre de cada corte o paso en un párrafo: qué quedó, cómo se probó y qué falta.
- **Lo que el plan no fija** «se resuelve con la opción más simple que cumpla el plan y estas reglas, sin preguntar ni agregar mecanismos por las dudas» (AG-B, AG-F).
- **Autorización para aplicar propuestas.** «La instrucción del usuario del 2026-10-10 autoriza aplicar las propuestas del plan y acuerdos y enumerarlas al cerrar el paso. Las decisiones funcionales ya resueltas no se reabren; sólo las contradicciones con reglas del repositorio/Meta y los cambios visibles se presentan juntos» (PLAN §11). PLAN §12: «Las propuestas técnicas existentes se aplican […] y se enumeran al cerrar cada paso; sólo contradicciones o cambios visibles requieren decisión del usuario, agrupados». La memoria del usuario dice lo mismo: con un plan acordado, aplicar y revisar en bloque, sin preguntar de a una.
- **Precedencia:** AGENTS.md, backend.md y ADR vigentes, después los acuerdos del 2026-10-08 y después el plan. El diseño del front manda en pantallas, navegación, textos y orden de entrega (PLAN, encabezado; DIS-F).

### 1.3 Un chat, dos repositorios

- **Etapa 1.** Tres chats: «El chat original coordina el plan y revisa; backend y front conservan sus repositorios, tests y commits separados. El usuario autorizó la coordinación entre ambos chats» (ARR).
- **Desde la Etapa 2, un solo chat:**
  - R1014 y R1015: «Se corrige en un solo chat, backend y front juntos».
  - E2 (2026-10-13): «Después se inicia 2B en este mismo hilo».
  - R1016: «Se corrige en el chat de la Etapa 4, al cerrar el corte en curso, backend y front juntos».
  - R1017: «Se corrige al empezar el chat de la Etapa 5, antes de programarla, backend y front juntos».
- **Entre chats no viajan artefactos.** «El script de exportación los copia al front; no se pasan rutas, hashes ni manifiestos entre chats» (AG-F). Las corridas intermedias «no se discuten entre chats» (VAL).
- **Git.** Se trabaja en `main`, sin ramas ni push salvo pedido explícito. Commits chicos, en español, con conventional commits (AG-B, AG-F).

### 1.4 TDD, pruebas rápidas por corte y la corrida pesada una vez al final

- **TDD donde hay lógica:** «primero un test que falle por el comportamiento faltante, después la implementación. Elegí el nivel que observe la garantía, sin reproducir el algoritmo» (VAL; AG-B).
  - Ejemplo del incremento 0 (R1009): el scaffolding que no compila «no se cuenta como rojo de comportamiento». El rojo real fue 12 casos con 8 correctos y 4 fallidos; el verde, 12/12.
- **En cada corte:**
  - Backend: build y tests unitarios y de arquitectura del área con `--filter-class`, sin los de integración con Docker.
  - Front: `tsc`, `lint` y los tests de la feature, sin `test:visual`.
  - Fuentes: VAL, «Verificación y cierre»; AG-F; ADR 0018 §2.
- **Al terminar la tarea o cerrar un paso:**
  - Backend: build sin advertencias, `dotnet test` completo con integración PostgreSQL/Redis (y Azurite desde la 2B) y `aspire stop` si se levantó la app.
  - Front: `npm run build`, `npm run lint`, `npm run test` y, con cambios visuales, navegador, `npm run test:visual` y revisión de capturas.
  - «Sin Docker no se da por cerrado» (AG-B).
- **Comandos** (AG-B):
  - Compilar: `dotnet build ArquitecturaBase.slnx`.
  - Una clase: `dotnet test --project tests/<Proyecto>/<Proyecto>.csproj -- --filter-class "<Namespace.Clase>"`.
  - Todo: `dotnet test`.
- **Por qué.** La Etapa 1 corría 2.605 tests con Docker, unos 22 minutos por corrida, en cada corte (ADR 0018, «Contexto»).
- **Si la corrida final falla** (VAL; AG-B; AG-F):
  - Se corrige y se repite solo lo que falló, con `--filter-class` o el test del front.
  - Después, una corrida completa más. **Vale la última completa en verde.**
  - Las intermedias no se registran, no se nombran («adicional», «única») ni se discuten entre chats.
  - «Un test que falla solo junto con los demás es un problema de aislamiento de datos del test, no de la app.»
- **Nada de verde maquillado** (VAL): no silenciar consolas, no quitar handlers estrictos, no aumentar tolerancias ni actualizar capturas solo para obtener verde. Un aviso del arnés se investiga; si no se resuelve, «queda una línea en la deuda del plan». Ejemplos:
  - La visual final de la Etapa 1 terminó 292/294 y se registró como roja, «no se presenta esta corrida como verde» (FORM, «Verificación de cierre»).
  - El aviso NO_COLOR/FORCE_COLOR del runner se conservó «sin silenciarlo» (E2, 2026-10-13).
- **Qué acredita cada prueba.** Una suite verde no acredita un diseño coherente ni un proveedor real. Los tests visuales con HTTP simulado no acreditan persistencia ni proveedores (VAL, «Revisión del cambio»). Cada cierre de FORM separa explícitamente el navegador con transporte simulado, la API con PostgreSQL y Redis reales, y el piloto físico.
- **La entrega se informa en tres partes** (VAL): **Cambio y encaje** (comportamiento, responsabilidad, patrón reutilizado y motivo de lo nuevo), **Pruebas** (qué se corrió con su resultado final y qué se probó a mano o con servicios reales) y **Pendientes** (alcance sin verificar, con fuente y criterio de cierre).
- **Clasificar un hallazgo** (VAL): bug funcional, desvío de arquitectura, problema del arnés o entorno, u organización del desarrollo. «La clase del hallazgo no indica por sí sola su gravedad ni el esfuerzo.»

### 1.5 El contrato es el código

- «Entre backend y front, el contrato son los DTO, el OpenAPI y los ejemplos de respuesta que exporta el backend para el front; no se escriben contratos en Markdown antes del código.» Planes y revisiones se actualizan al cerrar cada paso, con un resumen corto (ADR 0018 §3; AG-B).
- El front valida sus datos de prueba contra esos ejemplos exportados, que copia el script de exportación (AG-F). En la Etapa 4, los consumidores verificaron 67 capturas en seis cohortes por bytes, tamaño y SHA256 (FORM, «Verificación de la Etapa 4»).
- **Contratos no publicados, sin compatibilidad.** «Un contrato todavía no publicado (creado en el desarrollo en curso) se cambia revisándolo con el front, sin capas de compatibilidad; el ADR 0005 aplica a consumidores existentes» (AG-B; AG-F; E2; R1015).
  - Caso D5, 2026-10-10: `name` pasó de texto a mapa es/en directamente y se regeneró la migración inicial `AddBusinessFormDrafts`, «sin Asp.Versioning/ADR ni convivencia legada ficticia» (FORM, encabezado; PLAN §3).
- **No protegerse contra lo que no existe.** «No se protege contra clientes, datos o casos que no existen» (AG-B). En D5: «No se simula convivencia con datos desplegados inexistentes» (PLAN §3).

### 1.6 Robustez proporcional al efecto (ADR 0018, 2026-10-11)

- **Decisión.** Comprobantes recuperables por `requestId`, estado incierto, leases y fences, reconciliación y pruebas de varios hosts se usan solo para operaciones con efecto fuera del sistema o que cuestan al repetirse. La configuración interna usa la forma común: un límite por método, control de revisión y 409 (ADR 0018; BE, «Robustez proporcional al efecto»).

  | Tipo de operación | Ejemplos en formularios | Tratamiento |
  | --- | --- | --- |
  | Configuración interna | catálogos, reglas, diseño del PDF, sugerencias, ajustes | Un límite por método, revisión y 409; sin comprobantes recuperables, sin vencimientos de 30 días ni pruebas de varios hosts (FORM, encabezado) |
  | Efecto externo o hacia personas | publicar en Meta, mandar formularios y mensajes, armar y subir el PDF, ejecutar acciones de reglas, LLM | Recuperación durable: reserva, lease/fence, «incierto» que se consulta antes de repetir |
  | Lectura idempotente | bajar un medio de Meta; subir a Azure con clave determinística | Reintento simple; reintentar sobrescribe lo mismo (E2B) |
  | Solo lectura | planilla, filtros, detalle, Excel | Sin recibos, leases ni pruebas de varios hosts (E3, «Cómo se cierra») |

- **Lo hecho se conserva.** La durabilidad del borrador ya implementada en la Etapa 1 se mantiene: no se retira trabajo para cumplir el ADR.
- **Más robustez se justifica.** Quien quiera más robustez para algo interno la justifica con el riesgo concreto (qué se pierde o duplica, y para quién); sin eso, la revisión la objeta.
- **Los reviews la objetan.** Cada review tiene su lista de lo hecho de más:
  - R1012: «un lock sobre un Id recién creado y un token de concurrencia redundante con la revisión».
  - R1014: el lock global durante el SQL de Filters y el flag `forSending`.
  - R1015, «Backend de más», y R1017, «Hacer de más».

### 1.7 La opción más simple

Ejemplos de esta regla aplicada:

- La vista sobre las tablas de Identity se reemplazó por un SELECT fijo del lector, «lo más simple» (R1012 #4).
- `isActive` va en el PUT del catálogo, «sin ruta adicional» (E2, 2026-10-13).
- Los archivos no tienen staging, versiones ni protocolo durable para lecturas y subidas idempotentes (E2B; BE, «Archivos privados»).
- No se agregó un tope de bytes a las opciones sin medirlo antes con Meta (E2, piloto).
- El Excel no usa cola ni trabajos de fondo (E3).
- No hay caché de formularios ni conexión Meta por usuario (FORM, «Verificación de cierre»).

### 1.8 Un code review por etapa, con una lista de «Arreglar ahora»

- **Formato.** «Revisión de lectura de backend `X..Y` y front `A..B`», con estas secciones:
  - Una lista bloqueante: «Antes de K7» (R1012), «Antes del piloto con Meta» (R1014), «Antes de cerrar la 2B» (R1015) o **«Arreglar ahora»** (R1016, R1017).
  - «En el piloto»: qué comprobar con el proveedor real.
  - «Con la <etapa siguiente> o al tocar esos archivos»: deuda menor.
  - «Lo que está bien»: patrones que se conservan.
  - «Avance de las correcciones» o «Cierre»: el resultado de cada punto.
- **Reglas de corrección:**
  - Cada arreglo lleva su test y commits chicos, con backend y front juntos.
  - Las decisiones de la etapa no cambian («tampoco el “nunca se trunca”», R1014).
  - La lista «Con la X» solo se atiende si un cambio toca esos archivos: «Si un cambio de esta etapa toca un archivo nombrado en “Con la 2B o al tocar esos archivos” […] se corrige ese punto en el mismo corte. El resto de esa lista no entra en la 2B» (E2B, «Pendientes que se arrastran»).
- **Cuándo se hicieron:**

  | Review | Qué revisó | Cuándo se corrigió |
  | --- | --- | --- |
  | R1012 | K1–K4 y salud de la Etapa 1 | Antes de K7 |
  | R1014 | K6–K7 | Antes del piloto con Meta |
  | R1015 | 2B | Antes del piloto con Meta y Azure |
  | R1016 | Etapa 3 | En el chat de la Etapa 4, antes de seguir con G1 |
  | R1017 | Etapa 4 | Al empezar la Etapa 5, antes de programarla |

- **Gravedad.** «No tienen igual gravedad: la exposición de opciones y el rechazo prematuro son los riesgos principales» (R1014, cierre). R1015 separa controles de seguridad, disponibilidad y uso o accesibilidad.
- **Lo que encuentra el piloto se suma al review.** Ejemplo: R1017 #17, el falso error por `origin` encontrado en el piloto de la Etapa 4.

### 1.9 Cada etapa cierra con un piloto real

- **Regla.** «No se cierra con el SQL de la demo ni solo con la vista previa web» (E2). «Los dobles no prueban la decisión» (E5). Azurite no acredita Azure real (PRE; ADR 0012). La preview orientativa no acredita el Flow publicado.
- **Pilotos realizados:**

  | Etapa | Fecha | Proveedores | Resultado |
  | --- | --- | --- | --- |
  | 1 | 2026-10-11 | Meta real | PRB1010-000001 con los seis tipos |
  | 2 | 2026-10-15 | Meta real | PILK7-000001…000003 y PIL200-000001 |
  | 2B | 2026-10-15 | Meta y Azure privado reales | FOTO2B-000001 |
  | 3 | 2026-10-16 | Sin Meta ni IA | Respuestas reales en la planilla y el XLSX abierto en Excel |
  | 4 | 2026-10-17 | Meta y Azure reales, solo en español | VISITA-000001; quedan pendientes inglés, respaldo íntegro al original y apertura web |
  | 5 | — | Proveedor de IA y Meta, con un audio transcripto | Pendiente |

- **Higiene del piloto:**
  - Al terminar: `aspire stop`, apagar el túnel (`DevTunnel` «expone toda la Api a internet», WA «Configuración»), eliminar credenciales temporales y volver a Azurite como predeterminado.
  - El registro guarda fecha, versiones, perfil e IDs no sensibles, «sin tokens ni teléfonos completos» (PLAN §12.1; VER; FORM).
  - No se mandan mensajes de prueba a terceros sin autorización (VER).
- **El usuario puede cerrar con deuda declarada.** La Etapa 1 se cerró por instrucción del usuario con el piloto completo y la deuda visual expresa (FORM, «Verificación de cierre»).

### 1.10 Consolidar la documentación y retirar planes

- **Recorrido** (AUD): preparar, inventariar, contrastar con código y tests (leer el cuerpo del test, no solo su nombre), consolidar en la fuente canónica, rescatar pendientes, retirar el plan con su procedencia en HIST, verificar enlaces y cerrar con los checks.
- **Destinos:**

  | Qué es | Dónde va |
  | --- | --- |
  | Arquitectura técnica | BE |
  | Regla funcional | `docs/features/` |
  | Decisión estructural | ADR |
  | Procedimiento | `docs/guides/` |
  | Resumen para agentes | AGENTS.md y su mapa |
  | Trabajo abierto | `docs/plans/` |

- **Estados de un plan** (AUD): una propuesta sin implementar no se promueve a premisa. Una implementación parcial registra lo hecho y conserva lo pendiente. Lo verificado se consolida y el plan se retira.
- **No inventar historia.** «Un archivo sin versionar y sin otra copia no se elimina por asumir que está terminado.» Las notas del 9 al 11 de octubre que nunca se commitearon se conservaron en un ZIP local con manifiesto SHA256, fuera de Git. «No se atribuye a Git el contenido nunca commiteado» (FORM; HIST).
- **Retiros de formularios** (HIST):

  | Fecha | Qué se retiró | Commit para recuperar |
  | --- | --- | --- |
  | 2026-10-11 | Plan principal, acuerdos, contratos de la Etapa 1 y del incremento 2, coordinación de la Etapa 1 y reviews del 7 y 8 | `f4d2a9f` (retiro en `4b151eb6`) |
  | 2026-10-11 | Notas por corte del 9 al 11 | `bb8d01e` (incremento 0) |
  | 2026-10-15 | Resumen de la 2B | `ed53fb8` |
  | 2026-10-16 | Resumen de la Etapa 3 | `577d1c3` |
  | 2026-10-16 | Plan de la Etapa 4 y review de la Etapa 3 | `98fa775c` |

- **Al cerrar un desarrollo,** sus decisiones van a las fuentes de las premisas y el plan terminado se retira (AG-B). Las premisas de formularios están resumidas en PRE, en la tabla «Decisiones que se conservan» y en los párrafos de «Mantenimiento».

### 1.11 Otras reglas de la plantilla que condicionaron todo

Fuentes: AG-B y BE.

- **Recorrido HTTP:** `Controller → interfaz de servicio → servicio → repositorio, lector o integración`. Sin handlers, repositorio genérico ni Minimal APIs de negocio. Mapeos a mano.
- **Una sola forma de guardar:** `IUnitOfWork.ExecuteInTransactionAsync(trabajo, CommitPolicy, ct)`, una vez por método público que escribe.
- **Errores:** `Result` en vez de excepciones. Los errores se declaran en Domain con códigos `Area.Entidad.Motivo` traducidos en `.resx`.
- **Módulos opcionales:** BusinessForms, WhatsApp e IA viven en `Modules/<M>` y el núcleo no los nombra. Entre módulos hay puertos neutrales, commits separados e IDs opacos, sin FK ni joins (BE, «Formularios estructurados»).
- **Permisos, nunca roles:** `[HasPermission]`.
- **Fechas:** UTC con `TimeProvider`; `DateOnly` sin hora.
- **Logs:** `[LoggerMessage]`, sin códigos, tokens, teléfonos ni valores.
- **Idioma de la UI:** textos de usuario en `.resx` es/en con voseo.
- **Lugar de cada pieza:** cada responsabilidad nueva actualiza su AGENTS.md y el mapa en el mismo desarrollo.

---

## 2. Reglas y decisiones por etapa

### Etapa 1 · Crear, publicar, mandar y recibir (cerrada el 2026-10-11)

Fuentes: PLAN §1–§14, ACU, ARR, FORM (todas las secciones hasta «Verificación de cierre»), WA («Perfil local», «Publicación durable», «Envío durable», «Recepción»), DIS-F y R1009.

#### Objetivo y alcance

- Implementar los tableros A–E, P1–P3, C1–C5, R1–R3 y e1-4 con seis tipos de pregunta: texto corto, texto largo, número decimal, fecha, sí o no y elegir de una lista manual (PLAN §12.1).
- El recorrido: crear, guardar y descartar; ordenar y editar preguntas; vista previa; publicar una versión; mandar desde el chat Manual existente; guardar la respuesta numerada y mostrarla en el chat.
- Se entregó en incrementos: 0 (modelo puro), 1 (listado, editor e idiomas), 2a (chequeo local), 2b (aceptación durable, worker, Meta y preview), «mínimo 2» (recarga y cultura de preview), «mínimo 3» (presentación sellada y captura) y 3 (envío durable). FORM cierra «los pasos 1–4».
- **Fuera** (PLAN §12.1): IA, planilla y panel de respuestas, carga web, correcciones, aprobación, PDF y archivos, avisos automáticos, catálogos, reglas y cálculos entre registros, grupos, remitos y stock, intercambio de datos y plantillas para escribir fuera de las 24 h. «No se crean rutas, entidades o esqueletos de esas capacidades.»

#### Decisiones tomadas, con fecha

**2026-10-08**, acuerdos con el usuario (ACU §1):

- Todo el sistema es de **una sola empresa**: sin TenantId, conexiones por operador ni Embedded Signup. El administrador crea y publica; los empleados completan, consultan o aprueban (PLAN §1).
- **Reglas** («Cuando llega una respuesta») **y sugerencias** («Qué sugerir después») se guardan aparte, con su propio Guardar e historial, sin republicar en WhatsApp. Publicar fija solo las preguntas.
- **Numeración por prefijo** (VIS-000024), en vez de `F000001-000001`: se propone a partir del nombre, es editable hasta la primera publicación, único y no se recicla.
- Aprobación de un nivel; catálogos Manual y Filters; formularios sin circuitos rígidos, conectados por sugerencias; modo Asistente que manda solo.
- Orden de etapas propuesto (ACU §7).

**2026-10-09**, confirmaciones del usuario (PLAN §3, §11, §12.1):

- El servidor asigna `fieldId` y `choiceId` **al guardar el borrador** y devuelve el mapa de las identidades provisionales del navegador. Las filas de `BusinessFormFields` se crean **al publicar**, así un borrador descartado no deja huérfanos.
- El prefijo se bloquea en el commit que acepta la primera publicación durable y sigue bloqueado aunque Meta falle.
- El botón que abre el formulario dice siempre **«Abrir formulario»** y es distinto del botón final de cada formulario (decisión 10 de DIS-F).
- Ver los valores de una respuesta en el chat exige **`formrecords.read`** además de `whatsapp.read`.
- Los permisos usan dos segmentos: `formtemplates.read/manage/publish` y `formrecords.create/read`. Los de tres segmentos (`forms.templates.*`) chocaban con la guía de permisos.
- «Guardar borrador» permanece en el editor; «Volver» regresa al listado.
- La revisión técnica aprobó `BusinessFormDraftChanges` con recuperación de 30 días.

**2026-10-10:**

- **D4:** publicar compila solo lo guardado. Con cambios sin guardar, el diálogo pide guardar primero (ACU §1).
- **D5:** todo texto que ve el empleado va en español e inglés. Se guarda incompleto; para publicar hacen falta los dos idiomas completos. No hay fallback ni traducción automática. Cada empleado recibe en el idioma de su cuenta (`UserCultures.Of`) (ACU §1; DIS-F, decisión 11).
- **Contrato directo:** BusinessForms todavía no estaba commiteado ni desplegado, así que `name` pasó a mapa es/en y se regeneró `AddBusinessFormDrafts`, sin ADR de versionado (PLAN §3; FORM, encabezado).
- **Controles del Flow:** Sí/No sin selección inicial; número que conserva decimales exactos, validado en el servidor; calendario que respeta la obligatoriedad (PLAN §11).
- Diseño del incremento 1 elegido en la versión 54 (PLAN §11, §14).
- Orden de etapas confirmado: 1 editor y chat → 2 catálogos → 3 planilla → 4 reglas, aprobación, PDF y avisos → 5 asistente (PLAN §12).
- Se autorizó la 2b con las propuestas técnicas existentes, sumando `WhatsApp:WabaId` a la conexión actual con verificación de pertenencia (FORM, «Publicación y preview — 2b»).
- Para etapas posteriores: los tres criterios de catálogos; la revisión de reglas fijada al recibir; «Pedir aprobación» como última acción; nadie aprueba lo propio; el asistente no contesta lo que no es un formulario; tope de 2 ofrecimientos diarios (ACU §1; PLAN §11).

**2026-10-11:**

- ADR 0018 (robustez proporcional) y ADR 0019 (sin SQL configurable).
- Se suma la Etapa 2B; exportar a Excel en la Etapa 3; importar después; motor único de IA (ADR 0020 propuesto); audios; formularios que faltan (ACU §1).
- **Decisión humana del cierre:** la ayuda del campo decimal indica el punto como separador **solo en publicaciones futuras** (perfil r4). El receptor acepta punto y coma, y los assets ya sellados se conservan (FORM, «Verificación de cierre»).
- Etapa 1 cerrada por instrucción del usuario.

#### Reglas de negocio implementadas

**Modelo**

- Una definición tiene tres responsabilidades separadas: qué se pregunta, qué se respondió y qué hacer después. El editor es uno solo, sin pestañas, pero los guardados son distintos (PLAN §3).
- Sin CREATE TABLE por formulario ni ALTER TABLE por pregunta: modelo híbrido con JSON canónico más una proyección tipada reconstruible (PLAN §5).
- Nombre, cultura y botón son metadata de la definición, fuera de `SchemaJson`. El esquema tiene `format=1` y solo las preguntas. El ledger de identidades retiradas va aparte. No hay tablas de traducciones (FORM, «Persistencia y responsabilidad»).
- `NameJson` es la autoridad del nombre. `Name` se deriva del idioma original en el mismo commit. `DisplayNameEs` y `DisplayNameEn` son columnas calculadas que usan el original si falta la traducción; no son fuente ni caché.

**Identidad**

- Solo el servidor genera `FieldId` y `ChoiceId` (Guid v7). La key es `field_` más el FieldId en formato N, nunca la etiqueta (FORM, «Identidad»).
- Renombrar, traducir y reordenar conservan los IDs. Cambiar el tipo exige `replacesFieldId` y crea otra identidad. Omitir una pregunta la retira, y el ledger impide resucitarla. Las opciones no cambian de pregunta.
- `Group` está reservado y se rechaza al guardar; habilitarlo no reescribirá esquemas `format=1`. `source.catalog` quedó reservado desde la Etapa 1 y se habilitó en K6.

**Idiomas (D5, Etapa 1)**

- El idioma original (`draft.culture`) es inmutable y sus textos no pueden ser vacíos.
- Las traducciones pueden faltar o venir como `""`. El espacio en blanco se convierte en `""`. Mandar `""` retira una traducción a propósito.
- Omitir una traducción no vacía que ya existía falla con `TranslationsWouldBeLost`. Así se distingue la intención de la omisión accidental.
- El botón omitido al crear toma Enviar/Submit solo en el original.
- La ayuda es nullable por idioma y no se inventa. Si existe en un idioma, para publicar se exige en el otro.
- Las opciones duplicadas se rechazan solo en el original, comparando con NFC + Trim + OrdinalIgnoreCase y señalando la segunda fila. Se rechazan claves ajenas a es/en, `null` en nombre, etiquetas, opciones o botón, y configuraciones desconocidas.

**Presupuestos de autoría**, propios y revisables, independientes de Meta. No se trunca (FORM):

| Elemento | Límite |
| --- | --- |
| Nombre | 120 UTF-16 por idioma |
| Prefijo | ASCII normalizado, 1 a 12 caracteres, único |
| Etiquetas, opciones y botón | 160 elementos Unicode |
| Ayuda | 1000 |
| Preguntas por formulario | 40 |
| Opciones por pregunta | 50 |
| Cuerpo del pedido | 512 KiB |
| Resultado protegido | 1 MiB |
| `maxLength` de texto | 80 corto / 600 largo por defecto; rango 1 a 10.000 |

**Listado**

- `name` viaja como mapa completo. El SPA muestra el idioma de la UI y, si falta, el original con indicación de traducción pendiente.
- Búsqueda, orden y facetas usan **la misma proyección en SQL**, sin paginar en memoria ni N+1.
- Esa referencia de autoría **no permite fallback** en el Flow publicado (FORM, «Listado»).

**Guardar el borrador**

- Un único límite `OnSuccess` con estos locks, en orden: actor y UUID, intento previo, lock global de definiciones y prefijos.
- El mismo actor, UUID y cuerpo canónico devuelven el resultado original (201 o 200) sin avanzar la revisión. Otro cuerpo con el mismo UUID da 409 `RequestConflict`.
- El resultado se recupera durante 30 días; después, 410. Se purgan 100 por guardado y cada 15 minutos.
- Los tombstones conservan actor, UUID y revisiones para siempre, contra la reutilización del UUID.
- «404 en vuelo no demuestra rollback; conservar UUID/cuerpo.»
- El payload se protege con Data Protection con un propósito propio. Ningún hash ni cuerpo aparece en columnas o logs en claro (FORM, «Un guardado y recuperación exacta»).

**Rutas y permisos de autoría**

- Seis rutas MVC bajo `/api/forms`: GET definitions, facets y detail; POST definitions; PUT `definitions/{id}/draft`; GET `draft-changes/{requestId}`.
- Lectura con `formtemplates.read` siempre; escribir suma `formtemplates.manage`. Recuperar el propio intento pide solo Read y ser el mismo actor (FORM, «HTTP, permisos y composición»).

**Chequeo de publicación**

- `POST definitions/{id}/publication-checks` con Read + Publish, sin Manage, 30 por minuto por actor, sin escritura ni Meta.
- Compara el contenido enviado con el borrador guardado: revisión vieja da 409 `RevisionConflict`; contenido distinto, 409 `DraftMustBeSaved` (D4).
- `FormPublicationPolicy` exige es/en completos. Cada faltante sale como `Forms.Publication.MissingTranslation` con su ruta exacta (`name.en`, `draft.fields[i].label.en`, etc.) y su FieldId o ChoiceId. Si algo falta, el resultado es `Blocked`, sin compilación parcial (FORM, «Chequeo local»).

**Aceptación durable**

- El chequeo devuelve un `checkToken` válido 10 minutos, solo si el canal admite publicar y no hay otra operación abierta.
- `POST definitions/{id}/publications` corre en un solo `OnSuccess`. El replay por actor, UUID y payload se resuelve antes de revisar vencimiento, revisión o perfil, y nunca crea otra versión.
- El commit registra las identidades en `BusinessFormFields`, la versión inmutable (`SchemaJson` jsonb con `ContentHash` solo del esquema) y la operación con su manifiesto protegido. Fija `PrefixLocked` y conserva el borrador.
- Responde 202 sin Location. Se consulta con `GET .../publications/{requestId}` (Read + Publish, 120 por minuto; 404 si es ajena) (FORM, «Aceptación durable»).

**Estados de la publicación**

- `Accepted → Preparing → Publishing → Uncertain o ActivationPending → Published o Failed`.
- `ActivationPending` significa que Meta ya confirmó PUBLISHED y falta el commit local. `Failed` conserva snapshot, prefijo y la versión activa anterior.
- Una sola operación abierta por definición: 409 `Forms.Publication.InProgress`.
- La UI muestra una sola etiqueta: «Borrador», «Publicando…», «Publicado · versión N», «No se pudo publicar» o «Sin confirmar» (FORM; DIS-F, decisión 4).

**Activación**

- Exige pertenencia, fence vigente, permisos actuales y `BaseActiveVersionId`. No exige que siga vigente la revisión del borrador: un borrador posterior se conserva.
- Un conflicto de base deja la operación en `Uncertain/ActivationConflict` sin pisar otra versión.
- La revocación frena nuevos efectos. El fallo no desbloquea el prefijo ni reemplaza la versión activa.
- `CanRetry=false`: no hay acción HTTP que repita efectos inciertos.
- Si el canal devuelve error al aceptar, BusinessForms deja la operación en Uncertain y reintenta con los mismos IDs (FORM, «Publicación y preview — 2b»). Corregido el 2026-10-13: `UnsupportedConfiguration` y `PayloadMismatch` terminan en Failed (WA, «Rechazos y reintentos»).

**Canal Meta** (WA, «Publicación durable de Flows»)

- Antes de aceptar y de cada efecto se verifica, con una consulta paginada de `phone_numbers`, que el `PhoneNumberId` pertenece al `WabaId` de la misma conexión.
- Cambiar de conexión obliga a revisar: nunca se publica en silencio en otra cuenta.
- Pasos: Create → Upload → Validate → Publish → Verify, bajo el lock `whatsapp-flow`, con lease/fence y sin transacciones durante el HTTP.
- El estado InFlight se confirma justo antes de llamar. Un rechazo seguro libera el paso; un timeout deja incertidumbre para consultar, **nunca un POST repetido a ciegas**.
- Un Create sin ID confirmado nunca se repite ni se adopta por nombre: queda en revisión. Un Publish incierto no se repite.
- Verify comprueba ID, nombre, WABA, JSON 7.3 sin errores, salud AVAILABLE del Flow y del número, y el SHA256 del asset. El asset se baja solo por HTTPS 443 desde `*.fbcdn.net` o `mmg.whatsapp.net`, sin bearer ni redirects, hasta 1 MiB.
- **Cada versión crea un Flow físico nuevo**, con un recurso por versión y cultura (`WhatsAppFlowBindings`). No se reutilizan Flows entre definiciones.
- El cliente de Meta no reintenta POST ni usa los logs de `HttpClient` (WA, «Reglas»).

**Perfil del Flow** (WA, «Perfil local de formularios»)

- Flow JSON 7.3, Graph v25.0, codec 1, sin endpoint, una pantalla terminal con Form y Footer.

  | Tipo | Control y límites |
  | --- | --- |
  | Texto corto / largo | TextInput / TextArea; etiqueta 20, helper 80 |
  | Decimal | TextInput con patrón y hasta 60 caracteres; codec exacto (96 bits, escala ≤ 28) que rechaza redondeo, exponente y separador de miles |
  | Fecha | CalendarPicker single; `yyyy-MM-dd` a `DateOnly`, sin UTC; etiqueta 40 |
  | Sí o no | RadioButtonsGroup sin valor inicial; una ausencia opcional no es `false`; etiqueta 30, descripción 300 |
  | Elegir de una lista | Dropdown con ChoiceId; hasta 50 opciones manuales; etiqueta 20, título 30 |
  | Footer | `submitLabel` hasta 35; entrega `field_{Guid:N}`, nunca índices ni etiquetas |

- Por pantalla: nombre de 80, 40 preguntas, 50 componentes y 1 MiB de JSON. Los textos se miden en UTF-16.
- Cada texto sale de su idioma exacto, sin fallback. Si falta un texto requerido en cualquier idioma, no se produce ningún Flow.
- Un cambio de perfil **no recompila** proyecciones ya selladas ni toca versiones o sesiones. Una revisión de perfil versiona el contrato técnico.
- Historia: Meta rechazó r2; r3 emite los máximos como números JSON y mapea por identidad los diagnósticos de Meta, sin su texto (PRE).

**Preview**

- `GET .../versions/{versionId}/preview` exige Read vigente, versión publicada y `?culture=es` o `?culture=en`. Ausente u otro valor da 400 `Forms.Preview.CultureInvalid`.
- Responde siempre `no-store`, con URL oficial HTTPS y `ExpiresAtUtc` exacto del proveedor. No cambia el idioma de la UI ni de la cuenta.
- Pedirla de nuevo es una consulta explícita, nunca una renovación automática. La URL se redacta en `ToString` (FORM, «Recarga de publicación y cultura de preview»).
- En el detalle, «última operación» y «versión activa» son proyecciones distintas: el filtro Published depende de la activa, aunque la nueva operación esté pendiente o fallida.

**Presentación sellada**

- `BusinessFormVersion` conserva `NameJson`, `SubmitLabelJson`, `Culture` y `QuestionCount` inmutables. Editar el borrador no la cambia.
- Los selectores usan esa proyección, nunca el nombre del borrador (FORM, «Presentación sellada de versión»).

**Envío desde el chat** (WA, «Envío durable de formularios»; FORM, «Envío durable — incremento 3»)

- `GET /api/whatsapp/chats/{id}/forms` exige `whatsapp.read` y `whatsapp.send`. Ofrece versiones activas solo si la cuenta vinculada al contacto está activa, tiene idioma es/en y tiene su propio `formrecords.create`.
- `recipientContextRevision` es opaca: cambia con el vínculo, el estado, el idioma o los roles del destinatario, y no autoriza nada.
- `POST .../form-messages` crea la sesión y la salida en un solo commit, en el idioma de la cuenta.
  - Mensaje `interactive.type=flow`, `flow_message_version="3"`, `flow_action=navigate` y `flow_token` aleatorio.
  - El texto acompañante es obligatorio, de 1 a 1024 caracteres, con validador propio (no el de 4096 del texto común).
- La sesión fija contacto, cuenta, operador, definición, versión, idioma, binding, vencimiento y huella del destinatario. Token y payload se cifran con Data Protection; la correlación usa un hash comparado en tiempo constante.
- El worker vuelve a verificar WABA y PhoneNumberId y a autorizar antes de enviar. Nunca publica desde el chat ni reenvía un despacho incierto.
- Vigencia de la sesión: `FlowSessionLifetimeHours` = 24 (1 a 168), desde que entra en cola. Vence según la fecha firmada por Meta, no la del worker.
- Mismo `requestId` y cuerpo devuelven la misma salida; otro cuerpo, 409. Pending, Sending y Uncertain bloquean otro envío.
- Una sesión ya aceptada mantiene su versión aunque después se publique otra.

**Recepción y captura** (FORM, «Recepción y captura — mínimo 3»; WA, «Recepción de respuestas de Flow»)

- El webhook reconoce `interactive.nfm_reply` y guarda la respuesta cifrada, fuera del texto del chat. Una de más de 16 KiB se marca rechazada; el compilador ya lo impide al publicar.
- Un worker independiente la procesa aunque el procesamiento de texto entrante esté apagado.
- Deben coincidir contacto, cuenta activa, vencimiento, versión, binding, cultura, proyección y `LinkRevision`.
- El mapping sellado del canal, no el JSON de Meta, define claves, tipos, obligatoriedad, largos y opciones.
- Una respuesta desconocida, malformada o con NUL o UTF-16 incompleto no llega al sink ni consume número.
- `BusinessFormRecordRecorder` vuelve a autorizar `formrecords.create` antes y dentro de su límite, y graba recibo, registro y valores tipados juntos.
- Un recibo confirmado se consulta antes que los permisos actuales: el mismo contenido recupera su número aunque después se desvincule, se revoque el permiso o venza la sesión. Reutilizar la sesión con otro contenido se rechaza sin crear registro ni consumir número.

**Numeración e inmutabilidad**

- El número es `PREFIJO-NNNNNN`, con seis dígitos. Se asigna dentro del commit de captura, bajo el lock global de definiciones, con índice único (definición, número).
- No se reutiliza ni se renumera. Un replay devuelve el número original y un rechazo no lo consume.
- **Una respuesta confirmada no se modifica:** no hay ruta para editarla. Una corrección futura será una respuesta sucesora (FORM, «Numeración e inmutabilidad»).

**Lectura en el chat**

- `formrecords.read` gobierna los valores. Sin él, el chat muestra estado y número pero no las respuestas. Al revocarlo, los valores salen de la pantalla y de la caché.
- La vista previa del listado de chats es el texto fijo «Respuesta del formulario».
- Las etiquetas salen de la versión fijada; lo escrito por el empleado queda intacto. Distinto de `false`, cero y ausencia; decimal exacto; `DateOnly` (WA; PLAN §12.1).

**Sin respuesta automática.** En la Etapa 1 el operador manda la cortesía a mano. Cerrar el formulario sin enviar no genera respuesta (PLAN §12.1; DIS-F).

**Retención**

- El texto de los mensajes de WhatsApp se borra a los 90 días.
- `ProtectedFlowResponse` se vacía solo en mensajes ya procesados.
- Registros, valores, recibos y versiones hoy se conservan sin plazo: es una decisión pendiente (WA; PLANS).

#### Cómo se cerró y qué probó el piloto

Fuente: FORM, «Verificación de cierre — 2026-10-11».

- **Piloto real**, versión 2 de PRB1010 con perfil r3.
  - Ambos Flows publicados y disponibles. El número quedó disponible después de corregir el pago (el día anterior se había observado el bloqueo 141006).
  - Envío desde el chat «Entregado» a las 15:24 (Argentina); el usuario confirmó a las 15:25.
  - Se creó **PRB1010-000001** con los seis tipos y se conservó al recargar. No apareció el 141006.
- **Límites de lo probado.** La cantidad respondida fue 100, así que el piloto no califica decimales, ausencia opcional, sesión de una versión anterior ni máximos de texto. La preview oficial aceptó `12.50` y rechazó `12,50`.
- **Backend `c4c08b1`:** suite completa 2685/2685 con Docker, cero omitidos. `ed1e50c` prueba que un Flow r3 sigue enviable y captura su sesión abierta después de publicar r4.
- **Front:** 1455/1455, lint 48/48 y build sin advertencias. La visual final quedó **292/294 en rojo** (dos esperas iniciales de Nombre a 390 y 320 px, causa no probada), registrada como deuda del arnés.
- **Cierre por instrucción del usuario,** «sin hallazgos altos». Tres pendientes menores pasan a la Etapa 2: presentación R3, prueba física de decimales y test intermitente de Roles.

---

### Etapa 2 · Catálogos Manual y Filters (cerrada el 2026-10-15)

Fuentes: E2, FORM («El corte Manual K1–K3» en adelante, hasta «Preparación de catálogos»), WA («Catálogos de formularios — K7», «Rechazos y reintentos»), ADR 0019, R1012 y R1014.

#### Objetivo y alcance

- **K1 Lista:** paginada, con búsqueda y filtros por origen y por estado (`Active`, `Inactive` o `Unavailable`). Los conteos por origen vienen del backend.
- **K2 Alta:** Manual o Filters; Query no se ofrece.
- **K3 Manual:** ítems con `expectedRevision`, delta y movimientos.
- **K4 Filters:** origen Usuarios.
- **K6:** pregunta «Elegir de un catálogo».
- **K7:** enviar y responder con catálogos.

#### Decisiones tomadas, con fecha

**2026-10-10**, confirmadas por el usuario ante la duda (PLAN §5.2; ACU §1):

1. Los ítems pueden no tener inglés: se muestra el español. Es una excepción a D5, porque casi siempre son nombres.
2. Cambiar el catálogo de una pregunta publicada la convierte en una pregunta nueva; versiones y sesiones anteriores quedan intactas.
3. Si la opción elegida se dio de baja antes de responder, se rechaza **toda** la respuesta: sin registro ni número, con un aviso sin valores y reenvío manual explícito.

**2026-10-11:** el alcance es Manual y Filters (K1–K4, K6, K7). Query/K5 queda para después ([ADR 0019](#adr-0019-sin-sql-configurable)): K2 muestra solo Manual y Filters.

**Decisiones del resumen**, con la frase «No se vuelven a preguntar» (E2):

- El origen no cambia: otro origen es otro catálogo.
- Filters lo arma el sistema desde columnas permitidas de un origen fijo. Primero, usuarios (id, nombre, roles, activo), **nunca correo, contraseña ni códigos**. Los orígenes los agrega desarrollo, no la pantalla ni la IA.
- **2026-10-12** (code review): el origen Usuarios es un **SELECT fijo del lector del módulo, sin vista en la base**, porque una vista sobre las tablas de Identity frenaría migraciones futuras del núcleo. El filtro por rol guarda el Id del rol y muestra su nombre actual. Se excluyen las opciones sin texto.
- Una fila por persona. «Activo» es la cuenta activa. El rol se filtra como «tiene el rol».
- Los valores van siempre como parámetros, por la conexión normal, sin credencial restringida aparte.
- Las condiciones usan la gramática común: columna permitida, operador según tipo y valor.
- El único parámetro de contexto es el usuario destinatario, que pone el servidor y nunca el mensaje. La zona no existe.
- La vista previa usa una persona de ejemplo elegida explícitamente, sin suplantar su identidad ni sus permisos, con tope de tiempo y de filas.
- Un catálogo inválido después de una migración no frena el despliegue. Queda «No se puede usar» con motivo seguro, no se elige en K6 y no se puede mandar.
- Guardar un catálogo es configuración interna: un límite, revisión y 409, sin comprobantes (ADR 0018).
- Nombre y descripción van en es y en. Cada ítem tiene texto, detalle y código, con español obligatorio e inglés opcional. El código no se muestra en WhatsApp.
- Dar de baja un ítem no lo borra y se puede reactivar; las respuestas viejas lo siguen mostrando.
- Las posiciones son únicas por catálogo. El servidor ubica las nuevas al final, y reordenar es mover por `itemId` (arriba o abajo) dentro del mismo Guardar.
- La versión guarda solo `source.catalog`: cambiar ítems no obliga a republicar.
- Las opciones se resuelven al enviar, para cada destinatario, y viajan dentro del mensaje. No hay endpoint de intercambio.
- **Más de 200 opciones (tope fijo), o ninguna en una pregunta obligatoria: no se puede mandar. Nunca se trunca.**
- Al responder se valida que la opción esté en la oferta de la sesión. Se guarda catálogo, revisión, id y el texto que vio el empleado: renombrar después no cambia la respuesta.
- «Dada de baja» aplica solo a opciones Manual. En Filters alcanza con que la opción estuviera en la oferta: la captura no vuelve a consultar el origen.
- Un catálogo se da de baja y se reactiva desde su pantalla, con su revisión; no se borra. Dado de baja, no se elige en preguntas nuevas.
- Permisos `formcatalogs.read` y `formcatalogs.manage`. Ítems y movimientos se guardan en el PUT del catálogo.

**2026-10-13**, decisiones aplicadas en las correcciones (E2):

- Cinco fallas por mensaje antes de rechazar.
- `isActive` se guarda junto con el contenido y su revisión, sin ruta aparte.
- La acción de la pantalla prepara el cambio y Guardar lo aplica.
- Dar de baja un Filters no necesita vista previa; reactivarlo sí.

#### Reglas de negocio implementadas

**Manual, K1–K3** (FORM)

- El cliente no manda `Position`. Al crear, el servidor numera en el orden del pedido; al editar, agrega las nuevas al final.
- El PUT admite una lista ordenada `moves` de `{ itemId, direction: "up" | "down" }`. Se aplica completa dentro del mismo lock y UoW, resuelve el orden global por Position más Id, incluso entre páginas, y normaliza a posiciones consecutivas.
- PostgreSQL impone la unicidad con una **clave diferida**, para admitir el intercambio dentro de la transacción. Va en una migración SQL porque EF trata las claves alternas como identificadoras.

**Filters, K4** (FORM)

- SELECT fijo sobre `AspNetUsers`, `AspNetUserRoles` y `AspNetRoles` por la conexión normal.
- Devuelve una fila por persona con nombre, roles y actividad. Excluye cuentas borradas y nombres vacíos.
- Los roles sin miembros aparecen en los filtros.
- La vista previa pide elegir explícitamente una persona activa de ejemplo y no guarda nada. El servidor pagina y busca esa muestra.
- Los conteos de origen se calculan sobre el conjunto filtrado antes de paginar.
- `Unavailable` queda reservado para una definición guardada inválida después de una migración. Dar de baja un rol no invalida el catálogo.

**Pregunta de catálogo, K6** (FORM)

- Una pregunta `choice` usa opciones manuales **o** una única referencia `source.catalog`. Cambiar el origen exige reemplazar la identidad.
- El chequeo de publicación bloquea referencias inexistentes o inactivas en `draft.fields[i].source.catalog`.
- `UsageCount` cuenta definiciones distintas cuyo borrador actual usa el catálogo. Sin permiso de lectura de formularios viaja como `null`.
- El Flow declara un Dropdown dinámico `data-source: ${data.<key>_options}` (perfil r5). La vista previa web muestra la pregunta sin opciones.

**Enviar y responder, K7** (FORM; WA)

- `IStructuredFormSource.PrepareAsync` resuelve Manual y Filters para la cuenta vinculada, en su idioma.
- Al aceptar el envío se revalidan vínculo, permisos y versión, y la **oferta se fija dentro del payload protegido de la sesión**, en el mismo límite que la salida durable.
- El despacho la manda en `flow_action_payload.data`, sin volver a consultar el catálogo.
- Al capturar solo se admiten IDs ofrecidos. En Manual se comprueba la actividad bajo el mismo lock que su edición, y una baja rechaza toda la respuesta antes de asignar número.
- `BusinessFormRecordReferences` guarda catálogo, revisión, ID, texto, detalle y nombre históricos, en la misma transacción que registro, valores y recibo. Una falla al insertar la referencia revierte también la numeración.
- El chat devuelve las referencias solo con `formrecords.read`. El rechazo nombra la pregunta, nunca el valor elegido, y no muestra registro ni número.
- «Mandar de nuevo» abre el compositor; el operador elige y confirma otro envío. No hay reenvío automático.
- La preparación (`GET .../preparation`) usa el contacto y la cuenta vinculada que conoce el servidor, nunca un destinatario mandado por el navegador.

**Correcciones del 2026-10-13** (FORM; E2)

- Los PUT de Manual y Filters admiten `isActive` junto con `expectedRevision`. Omitirlo conserva el estado.
- El editor conserva el borrador ante error de red o 5xx y permite reintentar con la misma revisión. **Solo un 409 exige revisar.**
- La búsqueda de personas espera 300 ms y conserva el editor y el foco.

**Cierre del review K6–K7, 2026-10-15** (FORM)

- El operador recibe solo campo, etiqueta y cantidad, más problemas seguros. Las opciones completas quedan en la sesión.
- Manual valida, al guardar, **30 caracteres de texto y 300 de detalle por idioma**. K4 conserva cada texto completo y marca el que no entra.
- El bloqueo al enviar informa catálogo y cantidad incompatible, sin valores ni truncamiento.
- En Filters, el texto de la opción es siempre el nombre.
- Una pregunta de catálogo sin selección manda `null` y recibe la validación en su campo.
- La confirmación de baja advierte que tampoco se podrán mandar los formularios publicados que usan el catálogo.
- El Flow omite `description` si no hay detalle.
- Las fallas técnicas de captura se reintentan hasta **cinco intentos y 15 minutos desde la primera falla persistida**. Solo cumplidos ambos límites quedan `Rejected` y visibles en el chat (WA, «Rechazos y reintentos»).

#### Fuera de esta etapa (E2)

- Query/K5: editor, parser, permiso propio y credencial restringida.
- El endpoint de intercambio cifrado: búsqueda en vivo, listas dependientes y listas largas.
- Catálogos propuestos por la IA (Etapa 5, H4).
- Maestros que todavía no existen: clientes, productos, depósitos.
- Importar ítems desde Excel.

#### ADR 0019: sin SQL configurable

Aceptado el 2026-10-11. **Por ahora no se admite SQL configurable.** Query/K5 se retoma solo si una lista necesaria no se puede expresar con Filters. Al retomarlo, estos requisitos quedan fijos:

- Consultas limitadas a `Catalogs.Query`, aprobadas por una persona, sobre vistas inventariadas del esquema `catalogos`, con una credencial separada y restringida.
- Único parámetro `:usuario_actual`.
- Catálogo inválido: «No se puede usar», sin frenar el despliegue.
- Vista previa con persona de ejemplo, sin suplantación.
- Permiso propio.
- Validación con parser y AST con listas permitidas. No alcanza una expresión regular ni comprobar que empiece con `SELECT`.
- Timeout, topes de filas y bytes, y revalidación después de cada migración.
- Un LLM podrá proponer configuración, pero nunca ejecutarla ni aprobarla.

#### Cómo se cerró y qué probó el piloto

- **Validación local final, 2026-10-15:** backend **2772/2772** con PostgreSQL/Redis, sin omitidos; front **1643/1643**, lint/arquitectura 48/48, visual **422/422** (FORM).
- **Piloto real con Meta, 2026-10-15** (E2):
  - Dos formularios publicados en es/en; Meta confirmó `PUBLISHED` y `AVAILABLE`, sin errores ni 141006.
  - Manual con y sin detalle, y Usuarios vacío y con opciones: **PILK7-000001** y **000002**, con texto, detalle y catálogo históricos tras recargar.
  - Baja después del envío: rechazo `Forms.Record.CatalogItemInactive` sin registro ni número. «Mandar de nuevo» tras recargar generó **PILK7-000003**, sin salto de numeración.
  - **200 opciones con 136 caracteres de detalle cada una:** Meta las aceptó y quedó **PIL200-000001** con el detalle completo. No se truncó ni se agregó un tope de bytes sin medir.
- **Heredados de la Etapa 1** (R3, decimales físicos, Roles intermitente y las dos visuales de Nombre): E2 los declaró para cerrarse en esta etapa, pero el cierre dice que «mantienen su alcance». **No hay registro de que se hayan cerrado.**

---

### Etapa 2B · Fotos y documentos (cerrada el 2026-10-15)

Fuentes: E2B, FORM («Fotos y documentos — implementación local 2B», «Cierre de la Etapa 2B»), WA («Fotos y documentos en Flows»), BE («Archivos privados»), ADR 0012 y R1015.

#### Objetivo y alcance

- Archivos privados en Azure Blob (ADR 0012), con Azurite para pruebas repetibles.
- **F1:** preguntas Foto y Documento en el editor.
- **F2:** en el celular, PhotoPicker y DocumentPicker.
- **F3:** la respuesta llega con archivos.
- **F4:** ver y descargar.
- **F5:** límites y casos.

#### Decisiones tomadas, con fecha

**2026-10-11**, por el usuario (PLAN §12.2b; ACU §1; E2B). Va después de catálogos y antes de la planilla por tres motivos: es otra clase de pregunta que toca las mismas piezas, la planilla nace mostrando archivos y la Etapa 4 necesita Azure ya probado para el PDF. Se confirmaron tres propuestas: miniaturas livianas en el chat con el original solo al abrir, nada guardado a medias y exportar hasta 10.000. Decisiones del resumen, con «No se vuelven a preguntar» (E2B):

- **Foto:** «Cámara o galería» (predeterminado) o «Solo cámara». **Documento:** PDF, Excel (xls y xlsx), Word, JPEG y PNG. Sin CSV (Meta no lo admite), video ni comprimidos.
- Mínimo y máximo por pregunta; obligatoria es mínimo 1.
- **Hasta 10 archivos y 100 MiB por respuesta** (límite de Meta): no se publica si la suma de los máximos pasa de 10.
- **Tamaño por archivo:** un solo tope de instalación, hasta 25 MiB, sin tope por pregunta.
- Una pregunta de archivo por pantalla, ubicada por el compilador.
- **Nada guardado a medias:** si un archivo no baja tras reintentos acotados, o no coinciden hash, tamaño o tipo, la respuesta queda rechazada y visible con su motivo, y el operador puede reenviar.
- Bajar de Meta es una lectura idempotente con reintento simple. Meta guarda los archivos hasta 20 días.
- Subir a Azure usa una clave determinística por respuesta y archivo: reintentar sobrescribe lo mismo.
- Los archivos no referenciados por una respuesta confirmada se limpian.
- Contenedor privado. Se ven y descargan solo por la API con `formrecords.read`: nunca URL públicas ni acceso del navegador a Azure. Sin permiso nuevo.
- JPEG y PNG en línea con `nosniff`; documentos siempre como descarga.
- Miniaturas armadas al guardar; el original de la foto se baja solo al abrir el visor.
- Nombres y contenidos nunca van a logs, lista de chats ni avisos.
- **Los archivos siguen a la respuesta**, no a los 90 días del texto de WhatsApp.
- El tope se configura en MiB y se muestra como MB. La biblioteca de imágenes se justifica en Infrastructure.
- **Antivirus** (por ejemplo, Defender for Storage): requisito antes de producción, a decidir con el despliegue.
- Por decisión del usuario, el desarrollo usa Azurite hasta que exista la cuenta de Azure; la prueba con Azure real va al cierre (E2B, Estado; E2, 2026-10-13).
- **Piloto, agregado por el review:** dejar vacía una pregunta de archivo opcional y comprobar qué manda Meta, sin asumir `null`, `""` ni array vacío (E2B, «Cómo se cierra»; R1015).

#### Reglas de negocio implementadas

**Editor y publicación** (FORM)

- Foto y Documento conservan `files` (mínimo, máximo y origen de foto); un mínimo mayor que cero las vuelve obligatorias.
- Guardar admite presupuestos de más de 10 y traducciones pendientes. Publicar exige es/en, almacenamiento disponible y suma de máximos hasta 10.
- `BusinessForms:MaxFileSizeMiB` (1 a 25, 25 por defecto) **se sella en el manifiesto**, fuera del esquema.
- La capacidad `form-files` habilita los tipos en el editor. Sin almacenamiento, los tipos se ven pero no se eligen.

**Persistencia**

- `StoredFiles` es del núcleo: Entity e IAuditable, dueño, nombre, tipo, tamaño, SHA-256 y claves privadas del original y la miniatura.
- `BusinessFormRecordFiles` es de BusinessForms: registro, pregunta, archivo y orden. Sin FK entre módulos.
- `BusinessFormFileRecorder` verifica y sube fuera de la UoW con claves determinísticas. Después confirma juntos registro, valores, archivos, referencias y recibo.
- Un rechazo no guarda una respuesta parcial ni consume número.

**Verificación del contenido**

- Se combinan SHA-256, tamaño, MIME y contenido real: JPEG y PNG reales, PDF, Word y Excel antiguos (CFB) y OOXML.
- SkiaSharp genera miniaturas orientadas sin tocar el original. OpenMcdf verifica los streams de documentos antiguos. ZIP y XML usan la BCL sin ejecutar ni renderizar.
- **Antes de decodificar una foto:** ancho y alto hasta 16.384 píxeles, área hasta 32 Mi píxeles.
- Las imágenes mandadas como documento se inspeccionan sin rasterizar.
- Un documento dañado o cifrado se rechaza como tipo inválido.
- **Nombre de descarga:** se normaliza, se quitan los caracteres de formato Unicode (categoría Cf) y se usa la extensión del MIME verificado, sin truncar.

**Acceso**

- `GET /api/forms/records/{recordId}/files/{fileId}/thumbnail` y `.../content?download=true` piden `formrecords.read` y pertenencia, y comprueban el permiso antes y después de abrir el stream.
- Responden `no-store` y `nosniff`; fotos en línea y documentos como adjunto.
- Si falta el blob, 404 aunque exista su metadata.
- Cupo propio de **600 lecturas por minuto por actor**, separado de los listados.
- Perder el permiso cierra el visor y retira nombres y valores.

**Limpieza**

- Un worker corre cada hora y borra blobs de más de un día sin metadata confirmada.
- Consulta en lotes de 128 claves por el índice del original y deriva la clave de la miniatura.
- Respeta las fuentes de retención de efectos durables pendientes. La API no crea contenedores (BE).

**Canal** (WA, «Fotos y documentos en Flows»)

- Perfil `flow-static-7.3-r7`: cada pregunta de archivo abre su propia pantalla; navegación intermedia sin payload y `complete` final con referencias, sin init-values.
- El webhook conserva `id`, `file_name`, `mime_type` y `sha256`. El worker consulta la API de medios de la cuenta prevista y descarga solo por HTTPS desde hosts de Meta, sin redirects.
- **Cada captura tiene 90 segundos en total.** Las fallidas se saltean hasta su próximo intervalo: 30, 90, 210, 450 y 930 segundos desde la primera falla.
- Corrupción, tipo o límites inválidos rechazan sin valores ni número.
- El chat muestra «bajando» o «reintentando» con cantidad y fecha. Los rechazos nombran la pregunta.

#### Cómo se cerró y qué probó el piloto

- **Local:** backend 2857/2857 con PostgreSQL, Redis y Azurite; front 1681/1681 y visual 434/434 (FORM).
- **Piloto real con Meta y Azure privado, 2026-10-15** (FORM, «Cierre de la Etapa 2B»; E2B; WA):
  - Versión 2 bilingüe publicada, activada y mandada desde el chat.
  - Cámara más PDF, con la foto opcional vacía, guardados como **FOTO2B-000001**.
  - **Meta omite la propiedad de la pregunta opcional vacía**: no manda `null`, `""` ni array vacío.
  - La tarjeta pasó de «Recibiendo» a guardada dentro del minuto, sin recargar.
  - Descargas íntegras: JPEG de 88.229 bytes y PDF de 41.412 bytes, con tamaño y SHA-256 iguales a la metadata.
  - **Dos incompatibilidades corregidas**, cada una con test rojo primero: (a) Meta exige IDs de pantalla de solo letras y guiones bajos (perfil r7, solo para publicaciones futuras); (b) el ID de medio puede llegar como número JSON entero, y se conserva su lexema como texto, sin conversión numérica. El primer intento quedó rechazado sin registro ni número; el reenvío manual fue el que se guardó.
- **Cierre:** backend 2865/2865 y front 1681/1681. Al final se restauró Azurite como predeterminado.
- **Advertencia operativa:** los objetos del piloto quedaron en Azure. Para abrirlos otra vez hay que activar ese mismo almacenamiento, porque cambiar la configuración no copia blobs.

---

### Etapa 3 · Planilla, detalle y exportar a Excel (cerrada el 2026-10-16)

Fuentes: E3, FORM («Planilla, detalle y Excel — Etapa 3»), R1016 y DIS-F («Implementación de T1–T5»).

#### Objetivo y alcance

- **T1:** panel «Respuestas» y planilla.
- **T2:** filtros.
- **T3:** detalle.
- **T4:** estados.
- **T5:** exportar a Excel.
- Esta etapa **solo lee**: no crea rutas ni servicios de captura.

#### Decisiones tomadas, con fecha

**2026-10-10 y 2026-10-11** (E3; ACU §1):

- Etapa 3 es planilla, filtros, detalle y exportación. La carga web va después de la Etapa 4.
- Las respuestas de las Etapas 1 a 2B aparecen sin migrar ni volver a capturar.
- **Exportar:** `.xlsx` con los filtros aplicados, permiso propio `formrecords.export`, hasta 10.000 respuestas por vez (con más, el diálogo pide acotar), sin trabajos de fondo.
- De las preguntas de archivo se exportan cantidad y nombres, no los archivos.
- Ausencia, cero y `false` se distinguen en pantalla y en el Excel.
- Filtrar por una opción retirada conserva la historia.
- Un 403 o un permiso revocado no exponen datos ni conteos.
- La columna «Estado» llega con la aprobación, solo si el formulario la pide.
- La elección de «Columnas» vive en la URL y no se guarda en el servidor.

**2026-10-16**, T1 y T3 con archivos, lienzo versión 71 (E3):

- Una columna por pregunta de archivo, con la primera miniatura y la cantidad; abre el visor F4.
- Los documentos muestran tipo y cantidad.
- Con muchas preguntas, la planilla se desplaza a lo ancho.
- El detalle va a todo el ancho, con galería y documentos con nombre, tamaño y descarga.

#### Reglas de negocio implementadas (FORM)

**Panel y planilla**

- El panel lista los publicados desde `GET /api/forms/navigation`, paginado y ordenado en SQL. Nada queda fijo en `navigation.ts`.
- La planilla `/formularios/{id}/respuestas` tiene columnas fijas (número, recibida y persona/canal) y la **unión de preguntas por FieldId** de las versiones publicadas o con respuestas.
- Primero va el orden de la versión activa y después las preguntas retiradas. Cambiar el tipo conserva identidades distintas. Los encabezados indican desde qué versión existe cada pregunta.
- **«—» significa que la pregunta no existía; «Sin respuesta», que se dejó vacía.** Cero y `false` se conservan.

**Detalle**

- Usa las preguntas y opciones de su propia versión y las referencias capturadas. Muestra origen e historial.
- Anterior y siguiente respetan el filtro y orden actuales.
- El enlace al chat requiere además `whatsapp.read` y no manda mensajes al abrir.

**Permisos.** `formrecords.read` autoriza planilla, valores, archivos y conteos sin pedir lectura de diseños ni catálogos. Todas las lecturas revalidan el permiso antes de responder.

**Filtros**

- AND de condiciones por FieldId:

  | Tipo | Operadores |
  | --- | --- |
  | Texto | contains, equals |
  | Número y fecha | equals, gte, lte |
  | Elección y sí/no | in |
  | Todos | answered, unanswered, notAsked |

- Se suman persona, versión y rango de recepción UTC, con final exclusivo.
- La búsqueda mira solo los textos largos.
- **Texto, personas y condiciones viajan por POST en el cuerpo, nunca en la URL, el storage ni los logs.** Página, tamaño, orden, versiones y columnas pueden ir en la URL.

**Ejecución en SQL**

- Se filtra y ordena antes de contar y paginar, limitado a las versiones del snapshot de columnas para no mezclar una publicación concurrente.
- El orden por una elección manual usa un único CASE sobre su ChoiceId con etiquetas parametrizadas de la última versión. El de catálogo usa el texto capturado.
- Cada faceta ignora solo su propia dimensión y conserva opciones históricas con cero coincidencias, con la última etiqueta y las versiones en orden numérico.
- Los esquemas se deserializan **una vez por versión** y se comparten entre validación, filtros, listado y Excel.

**Excel**

- `POST /api/forms/definitions/{id}/records/export` exige además `formrecords.export`. Una exportación concurrente por usuario, sin cola; otra da 429. Las lecturas tienen su propio cupo.
- Hasta 10.000 respuestas: una consulta de IDs ordenados e hidratación en lotes de 100. Definición y columnas se leen una vez, el número máximo se fija al empezar y hay un **presupuesto de 30 segundos**.
- ClosedXML (MIT) vive solo en Infrastructure. Mantiene números, fechas y booleanos como tipos de Excel y los textos literales. Encabezados en el idioma de quien exporta.
- Opcional vacía es celda vacía; pregunta inexistente, «—».
- No se agregaron tablas, migraciones, Redis ni mecanismos durables para estas lecturas.

#### Fuera de esta etapa (E3)

- Carga web e importación desde Excel; correcciones.
- Estado, aprobación, PDF y avisos (Etapa 4); lo que hizo el asistente y la cadena de origen (Etapa 5).
- Exportar los archivos o exportar en segundo plano.
- Permisos por formulario: siguen siendo globales.

#### Cómo se cerró

- **Recorrido real, sin Meta ni IA** (FORM):
  - FOTO2B-000001 con miniatura, visor del original privado de Azure, PDF, opcional vacía y recarga.
  - Facetas históricas en PILK7: 1 entrega a domicilio y 2 retiros. El filtro y anterior/siguiente entre 000003 y 000001 se conservan al recargar.
  - **El usuario abrió `FOTO2B-20261016.xlsx` en Excel sin reparación.**
  - EXPLAIN sobre 10.001 respuestas y 40.004 valores: usa índices existentes y pagina después de filtrar. Es una medición local, no una garantía de producción.
- **Cerrada el 2026-10-16:** backend **2896/2896**, front **1703/1703**, visual **451/451**. Los contratos del front verifican los bytes HTTP y XLSX exportados.
- Los 14 puntos del review R1016 se corrigieron dentro de la Etapa 4.

---

### Etapa 4 · Reglas, aprobación, PDF y avisos (implementada y verificada localmente; piloto real parcial)

Fuentes: E4, FORM (desde «Reglas con revisiones propias — Etapa 4, G1» hasta «Piloto de aprobación y PDF — 2026-10-17»), WA («Avisos de reglas de formularios — Etapa 4, G6»), VER y R1017.

#### Objetivo y alcance

- **G1:** reglas «Cuando llega una respuesta».
- **G5:** motor único de acciones.
- **G3 y G4:** aprobación de un nivel.
- **G2:** diseño y copia fija del PDF.
- **G6:** avisos por WhatsApp.
- Orden de implementación: G1 → G5 → G3/G4 → G2 → G6 (E4).

#### Decisiones tomadas, con fecha

**2026-10-10** (ACU §1; PLAN §11):

- «Pedir aprobación» es la última acción de su regla; guardar una regla con acciones posteriores se rechaza. Lo siguiente va en «se aprueba» o «se rechaza». **Nunca hay PDF de algo sin aprobar.**
- La respuesta usa la revisión de reglas con que llegó, aunque las reglas cambien mientras espera.
- **Nadie aprueba lo que cargó.** En WhatsApp el autor es la cuenta que confirmó la respuesta, no el operador ni la IA. Se valida en el servidor, dentro del límite de la decisión.

**Decisiones del resumen**, «No se vuelven a preguntar» (E4):

- Reglas con guardado e historial propios: no crean versión ni republican.
- Aprobación de un nivel. Varios niveles o aprobadores, después.
- El rol asignado delimita quién decide, pero nunca reemplaza al permiso.
- PDF de diseño simple, en el idioma de quien lo recibe, como copia fija. Cambiar reglas, catálogos o diseño no altera lo emitido.
- **Un solo motor:** sin `ActionOperations`, `ApprovalDecisions` ni `documents.issue`, sin PDF a mano por fuera ni segunda cola de salida.

**2026-10-11**, ajustes (E4):

- G1–G6 elegidos.
- «Reintentar» no repite lo que ya salió confirmado. Lo que quedó «Sin confirmar» se consulta primero.
- «No aplica» es una acción cuya condición no se cumplió: se muestra, pero no es un error.
- El editor dice «con el diseño vigente», no «de esta versión».

**Requisitos previos decididos** (E4):

- Permisos `formrules.manage` (reglas y diseño del PDF) y `formapprovals.decide`. Los PDF se ven con `formrecords.read`.
- La clave de deduplicación de un aviso es el Id de su ejecución. Un envío del sistema «Sin confirmar» lo revisa quien tiene `formrules.manage`: primero consulta, después reintenta.
- **Una condición sobre una pregunta que no existe en la versión de la respuesta cuenta como «sin respuesta».**
- Una sola tarea de aprobación abierta por respuesta.
- El diseño del PDF tiene su propia tabla con revisiones, fuera de la versión de preguntas.
- Los PDF emitidos duran lo que dura la respuesta.

**2026-10-16:**

- **Idioma:** en lo nuevo de esta etapa (reglas, mensajes y diseño del PDF) solo es obligatorio el idioma original del formulario; el otro es opcional. Si el idioma de quien recibe no está completo, PDF y mensajes salen **enteros en el original, sin mezclar**. Las Etapas 1 a 3 siguen exigiendo los dos idiomas hasta el cambio posterior a la Etapa 5 (E4).
- G2 y G5 ajustados en la versión 75 (E4):
  - **«Incluir las fotos»:** marcada, las fotos van chicas debajo de su pregunta y quedan en la copia fija. Sin marcar, el PDF dice cuántas hay. Los documentos solo se nombran.
  - **«Reintentar»:** retoma solo lo que no salió; lo «Sin confirmar» se consulta primero.

#### Reglas de negocio implementadas

**G1, reglas** (FORM)

- `formrules.manage` consulta y guarda reglas, revisiones y opciones de configuración.
- Cada guardado crea una revisión inmutable en `BusinessFormRuleSets` y mueve `ActiveRuleSetId` bajo el lock de definiciones. `expectedRevision` parte de cero; un guardado atrasado da 409.
- Guardar no toca borrador, preguntas, versión ni WhatsApp. Guardar una lista vacía deshabilita las reglas con una revisión nueva.
- Cada regla tiene:
  - Identidades estables de regla y de acciones.
  - Un evento: Received, Approved o Rejected.
  - **Hasta diez condiciones AND** con la gramática de la planilla.
  - Acciones ordenadas.
- Pedir aprobación solo al recibir y como última acción. Generar PDF solo al aprobar. Adjuntar el PDF al aviso de quien cargó exige una acción anterior de generación en la misma regla.
- El esquema de preguntas se fija junto con las reglas, para resolver los placeholders (`{número}`, `{pregunta}`) por identidad aunque cambien las etiquetas.
- Los textos nuevos exigen solo el idioma original.
- La sección del editor tiene guardado e historial propios y comparte el aviso de salida por cambios pendientes. Releer una revisión no pisa el trabajo local: el usuario la adopta explícitamente.

**G5, motor de acciones** (FORM)

- Cada respuesta conserva la revisión de reglas con que llegó.
- Los runs de un evento fijan condiciones, datos, textos, destinatarios y diseño del PDF. La clave respuesta + evento + regla + acción impide duplicados.
- Cada acción espera solo a la anterior de su propia regla; las reglas no se frenan entre sí. Una condición falsa queda «No aplica».
- Se reserva con lease y fence, el efecto ocurre fuera de la transacción y se confirma bajo el fence vigente.
- **Un resultado incierto se consulta antes de repetir; uno confirmado nunca vuelve a enviarse.**
- La captura fija la revisión y crea sus runs antes de confirmar recibo y número, en el mismo commit que valores, referencias y archivos.

**G3 y G4, aprobación** (FORM)

- Un nivel, asignado a un rol. Solo decide una cuenta activa con `formapprovals.decide` y pertenencia vigente al rol, **y nunca quien confirmó la respuesta**.
- La revisión esperada evita dos decisiones. Cerrar la tarea, cambiar el estado y crear las acciones del evento es un único commit; si otro decidió antes, 409.
- **Rechazar exige un motivo de hasta 500 caracteres.**
- La bandeja «Por aprobar» ordena de la más vieja a la más nueva, dentro de los roles actuales. «Para mí» excluye las respuestas propias; «Todas» las muestra sin habilitar la decisión.
- `formapprovals.decide` permite leer la metadata de la tarea. Los valores y el PDF siguen pidiendo `formrecords.read`.
- La planilla muestra «Estado» si las reglas actuales o alguna revisión usada por sus respuestas piden aprobación.

**Idioma de los avisos y del PDF.** Al preparar cada evento, el idioma de quien recibe debe estar completo en todos los avisos de esa revisión de reglas y en el diseño del PDF vigente, si la revisión lo genera. Si falta cualquier texto, mensajes y copias usan íntegramente el original, y esa elección queda fijada en los runs (FORM).

**G2, diseño y copia fija del PDF** (FORM)

- Revisiones inmutables en `BusinessFormPdfDesigns` con el puntero `ActivePdfDesignId`. Guardar exige `formrules.manage`, revisión y el lock de definiciones; uno atrasado da 409.
- **Nombre original obligatorio (hasta 160 caracteres), traducción opcional, pie opcional (hasta 1000).**
- La selección conserva identidades: una pregunta nueva no se suma sola a un diseño guardado; una retirada que ya estaba seleccionada se dibuja solo si existe en la versión capturada.
- El diseño inicial sin guardar (revisión 0) incluye las preguntas, la aprobación y las fotos.
- La vista previa usa ejemplos y no guarda nada. Responde `application/pdf`, `no-store` y `Content-Language` con el idioma completo usado.
- Al aprobar, la acción fija diseño, nombre del sistema, número, versión, datos, etiquetas y decisión.
  - SkiaSharp genera A4 con Public Sans (OFL) embebida, texto ajustado y paginado.
  - **Fechas en UTC.** La aprobación muestra quién decidió y cuándo.
- **La clave privada depende de run y cultura.** Antes de generar se lee la copia existente. La creación condicional conserva el primer archivo y confirma tamaño y SHA-256 de esos bytes.
- Una recuperación solo consulta y nunca genera hasta confirmar la ausencia y que se pida reintentar.
- Una fuente neutral de retención protege los PDF subidos y todavía no confirmados, mientras exista su run.
- La copia se abre o descarga solo por la API, con `formrecords.read`, pertenencia y revalidación después de abrir el stream. Nunca hay claves ni enlaces a Azure.

**G6, avisos por WhatsApp** (WA)

- `WhatsAppStructuredRecordActionChannel` implementa el puerto neutral `IStructuredRecordActionChannel` y encola texto o documento en la **salida durable existente** (sin segunda cola).
- La clave run/destinatario conserva la misma operación y rechaza un contenido distinto.
- La cuenta debe seguir activa, con teléfono confirmado y el mismo vínculo. **Destino y ventana de 24 horas se comprueban al aceptar y al despachar.**
- No exige Atención manual ni cambia el modo. Fuera de la ventana queda un fallo visible: **esta etapa no agrega plantillas de Meta**.
- Los documentos se leen del almacenamiento privado, se verifican por tamaño y SHA-256 y se suben a Meta antes del envío.
- La aceptación de Meta queda como `Accepted`; entrega y lectura dependen de los receipts. Una reserva vencida queda `Uncertain` y no se reenvía sola.
- Un reintento explícito solo repite un fallo confirmado, sobre la misma operación. Las acciones inciertas no bloquean la atención manual.
- El historial muestra `Origin=FormAction`, autor automático y operador nulo. Consultar y Reintentar están en el historial de acciones de la respuesta, no en el chat.
- Sin `formrecords.read`, la API oculta cuerpo y caption, y el front los retira antes de guardar en caché. La bandeja muestra un resumen genérico.

#### Fuera de esta etapa (E4)

- Plantillas de Meta para escribir fuera de las 24 h.
- Varios niveles o aprobadores.
- Acciones de otros módulos (stock) y compensaciones.
- Carga web, importación y correcciones.
- Asistente y sugerencias.

#### Cómo se cerró y qué probó el piloto

- **Verificación local, 2026-10-16** (FORM):
  - Incluye los 14 arreglos del review de la Etapa 3.
  - Backend **3096/3096** con PostgreSQL, Redis y Azurite, build sin errores ni advertencias. Corrida definitiva sobre `98fa775c`; el exportador copió 67 capturas en seis cohortes.
  - Front **1793/1793**, consumidores 13/13, visual **493/493**.
  - Recorrido local contra la app: bandeja vacía, reglas con traducción vacía, diseño inicial y preview con respaldo completo al original, descarte sin persistir, filtro decimal `12,5`, conteo y detalle histórico.
  - El visor PDF nativo no se pudo calificar en headless; los bytes, las fotos en color y las cuatro páginas se verificaron por render técnico separado.
- **Piloto real, 2026-10-17** (FORM, «Piloto de aprobación y PDF»; VER), con backend `d79f2a3`, front `5aa6318`, túnel, Meta y Azure privado reales, en español:
  - **Registrar visita**, versión 1: **VISITA-000001**, reglas y diseño en revisión 1, con foto obligatoria incluida en el PDF.
  - Llegó «Recibimos…». La visita apareció en Por aprobar y otra cuenta activa del rol Admin, con `formapprovals.decide`, la aprobó.
  - El usuario abrió el PDF con la foto en el celular. Las cuatro acciones quedaron en «Hecho» y antes de aprobar no había PDF.
  - **Incidencias sin corregir en el piloto:**
    1. La web rechazó `origin` en el recibo del envío, aunque el POST dio 202 y la consulta 200. Se conservó el borrador y no se repitió el envío. Ahora es R1017 #17.
    2. Chrome bloqueó el visor inline del PDF. Ahora es R1017 #12.
  - **Siguen pendientes:** apertura web de la copia, inglés completo y respaldo íntegro al original.

---

### Etapa 5 · Asistente, sugerencias y audios (diseño elegido, sin empezar)

Fuentes: E5, ADR 0020, ADR 0017, IA, ACU §1, §4 y §6, y PLAN §4 y §12.5.

#### Qué se construye (E5)

- **Motor único.** El motor del asistente de Inicio se generaliza con canales: conversación en modo Asistente, sugerencia al operador en Atención manual y chat administrativo.
  - BusinessForms y WhatsApp aportan capacidades tipadas: id con versión, permiso, lectura o propuesta, esquemas y presupuesto.
  - Reemplazan a los puertos `IStructuredIntentInference`, `IStructuredSuggestionSource` e `IStructuredCatalogProposalSource`.
  - Casos de evaluación con datos ficticios para cada capacidad que decide sobre lenguaje.
- **Modo Asistente (H1, H3).** «Bot / automatización sin configurar» pasa a ser Asistente.
  - Lee, reconoce el formulario y lo manda solo, prellenado, por la cola existente (`Origin` Assistant, sin operador). Mandar no guarda nada.
  - Pasar a Asistente pide «Configurar el asistente». Los chats en Bot pasan a Atención manual: nadie queda en Asistente sin elegirlo. Sin conexión de IA o sin el módulo, la opción no se ofrece.
  - «Tomar atención manual» corta al asistente; solo termina el envío ya iniciado.
- **Atención manual, solo sugiere (H2).** La sugerencia va arriba del compositor, con un diálogo de prellenados que se pueden destildar. Manda el operador.
- **«Cuándo lo ofrece el asistente» (tablero 03).** Es un texto en «El formulario». Sin ese texto, el asistente no ofrece el formulario.
- **Límites (H5).** Si no está seguro, pregunta con botones de respuesta rápida. Solo a cuentas vinculadas, activas y con permiso, y dentro de las 24 h. La respuesta registra que la mandó el asistente, por qué y qué propuso.
- **Sugerencias entre formularios (s1–s4).** `BusinessFormSuggestions` guarda origen, destino publicado, «cuándo», condición opcional, modo Assistant o Always, mapeo entre preguntas del mismo tipo, activa y revisión.
  - Las respuestas guardan `OriginRecordId`, `OriginSuggestionId` y qué se aceptó y mapeó.
  - Assistant: decide la IA. Always: se ofrece al cumplirse la condición, sin IA; en Atención manual confirma el operador.
- **Catálogo propuesto (H4).** Se pide desde el chat administrativo («los supervisores activos»). BusinessForms valida y arma la vista previa. El catálogo queda inactivo hasta que una persona con `formcatalogs.manage` lo aprueba en la web.
- **Audios (H6).** Se bajan como los archivos de la 2B y se transcriben con un perfil calificado. La transcripción entra al asistente como texto, y el operador la ve en el chat, también en Atención manual.
- **Formularios que faltan (H7).** Si la persona quiere registrar algo y ningún formulario corresponde, el asistente le dice que no hay uno y registra el pedido. Quien administra formularios los ve agrupados por tema (cuántos pedidos, de cuántas personas y cuándo, sin nombres ni mensajes) y crea el formulario o descarta el pedido.

#### Decisiones tomadas, con fecha («No se vuelven a preguntar», E5)

- **ADR 0020 aceptado el 2026-10-16:**
  - Todo uso de un modelo pasa por el motor único del módulo de IA.
  - Las capacidades solo leen o proponen; lo propuesto lo valida y ejecuta el módulo dueño, con su servicio, una UoW y su cola durable si hay efecto externo.
  - Una capacidad que escriba necesita un ADR nuevo.
  - Todo llega al motor como texto: el audio, transcripto.
  - Cada resultado conserva canal, capacidad, versión, perfil de modelo, versión de las instrucciones y evidencia.
  - Los casos de evaluación se corren antes de habilitar un modelo, perfil o instrucciones nuevas.
- **En Asistente manda el formulario prellenado; en Atención manual solo sugiere. La IA nunca guarda ni confirma una respuesta:** la confirma la persona en el formulario.
- **No responde lo que no es un formulario:** la conversación queda sin leer para un operador.
- **Hasta 2 ofrecimientos por día por conversación.** No repite uno rechazado; no abrirlo no es rechazarlo.
  - Cuenta los formularios que el asistente ofrece o manda, por día calendario en la zona horaria del negocio.
  - Las sugerencias al operador en Atención manual no consumen cupo (E5, «Requisitos previos»).
- **Un solo botón, «Abrir formulario»**, también en las sugerencias. WhatsApp no admite otro botón en el mismo mensaje; una sugerencia no deseada simplemente no se abre (DIS-F, decisión 10).
- Sugerencias Assistant y Always, con guardado e historial propios, sin republicar. **Sin circuitos, pasos ni estados de proceso.**
- **El catálogo del asistente es solo Filters, nunca SQL** (ADR 0019), y siempre lo aprueba una persona.
- **Permiso «Configurar el asistente»:** código `whatsapp.configureassistant`, Admin por seed. El `whatsapp.automation.manage` de los acuerdos tenía tres segmentos y no pasa la regla área.acción (PLAN §11 y §12.5).
- **Proveedor elegido el 2026-10-16:** OpenAI para el asistente y para transcribir, con una sola cuenta. Modelo y perfil de transcripción se califican al implementar.
  - Audio y transcripción siguen la retención de los mensajes de WhatsApp.
  - Sin transcripción, el audio queda para el operador y el asistente no lo interpreta.

**2026-10-11**, ajustes ya decididos (E5):

- H1–H5 y s1–s4 elegidos.
- Un pedido sin formulario se avisa y se registra; cualquier otro mensaje (consultas o charla) no se contesta y queda sin leer.
- **Un formulario que la persona no puede completar se trata como inexistente:** no se revela que existe y se registra el pedido (H5 «Sin permiso»).

**Elecciones posteriores:**

- **2026-10-16:** H6 (audios) y H7 (formularios que faltan) elegidos. Idioma: nada de esta etapa exige traducciones; «cuándo lo ofrece», los pedidos y los temas se escriben en un solo idioma, y el asistente responde en el idioma de la conversación.
- **2026-10-17, recorrido guiado** (lienzo v78): una visita en ocho capítulos de las Etapas 1 a 5. Resume tableros ya elegidos y no suma decisiones.
- **2026-10-17, D2** (lienzo v79), reemplaza el detalle de T3, G4 y G5:
  - La respuesta se abre en un panel al costado de la planilla o de Por aprobar, con ↑ ↓ para la anterior o la siguiente. La URL la mantiene abierta; en el celular ocupa la pantalla.
  - El estado se arma con las reglas que tenía el formulario al llegar la respuesta y muestra momentos, no acciones sueltas: Recibida con su origen (operador o asistente), Por aprobar → Aprobada o Rechazada, y Al recibirla, Al aprobarla o Al rechazarla con el resumen de sus acciones.
  - Lo que no aplica no se muestra. Mientras espera, Aprobar y Rechazar van en el renglón «Por aprobar».
  - Pestañas Respuesta, Fotos, Actividad y PDF (esta última solo con la copia fija). «Descargar todas las fotos juntas» queda para después.

#### Requisitos previos y permisos (E5)

- Etapas 2, 2B, 3 y 4 cerradas; ADR 0020 aceptado; motor de Inicio generalizado con canales, conservando sus pruebas.
- La clave de OpenAI está cargada desde el 2026-10-16 (modelo `gpt-6.1-sol`, Disponible, prueba correcta). Falta calificar el perfil: hasta declarar `ArtificialIntelligence:Assistant:ModelProfiles`, Inicio responde «modelo no habilitado», como corresponde.
- «Cuándo lo ofrece el asistente» rige al guardarlo, sin publicar de nuevo, como las reglas.
- Permisos nuevos: `formsuggestions.manage` y `whatsapp.configureassistant`. Los pedidos de formularios que faltan los guarda BusinessForms y se ven con `formtemplates.manage`.
- «Tomar atención manual» pide solo `whatsapp.send`: siempre se puede frenar al asistente. Pasar a Asistente pide `whatsapp.configureassistant`.
- Antes de programar se corrige el review R1017.

#### Fuera de esta etapa (E5)

- Borrador de formulario armado por la IA a partir de los pedidos: después, siempre revisado y publicado por una persona.
- Responder consultas generales; SQL configurable en catálogos.
- Plantillas de Meta para escribir fuera de las 24 h.
- Capacidades que escriben (piden un ADR nuevo) e imágenes como entrada.

#### Cómo se cierra (E5)

- Checks por corte y al cierre. Modelo, transcripción y envíos son efectos externos: usan la operación durable del motor y la cola de WhatsApp.
- **Pruebas de arquitectura del ADR 0020:** nadie llama al transporte del modelo fuera del motor, y las capacidades no reciben `IUnitOfWork` ni servicios de escritura.
- Casos de evaluación corridos antes de habilitar un modelo, perfil o instrucciones.
- **Docker:**
  - Tope diario con tiempo inyectado y replay.
  - Respuesta tardía después de tomar Manual.
  - Propuesta con destino o valores no permitidos.
  - Permiso revocado.
  - Ofertas repetidas y bucles de sugerencias.
  - IA no disponible.
- **Prueba real con el proveedor de IA y Meta, incluido un audio transcripto.**

#### Lo heredado del motor de Inicio (ADR 0017; IA)

- Operación con UUID. PostgreSQL guarda conversación, turno, recibo, reserva y resultado.
- Un GET nunca genera. Fencing, revisión de conexión, permisos actuales, cuotas distribuidas y recibos impiden reenvíos.
- Un Running con posible envío perdido pasa a Uncertain y exige una decisión explícita.
- Contenido cifrado con propósitos propios. Nunca hay una transacción abierta durante el HTTP del proveedor.
- El modelo no elige SQL, tablas, filtros ni URL. La salida final es una herramienta tipada validada (estructura y procedencia, no semántica).
- Antes de llamar al proveedor se suman los permisos de todas las fuentes ofrecidas.
- No hay perfiles de modelo habilitados por defecto.

---

### Regla transversal: el idioma, de D5 a la regla del 2026-10-16

| Fecha | Regla | Fuente |
| --- | --- | --- |
| 2026-10-10 (D5) | Todo texto que ve el empleado va en es y en. Se guarda incompleto; publicar exige los dos completos. Sin fallback ni traducción automática. El destinatario recibe en el idioma de su cuenta. Los textos del sistema van en resx/i18next es/en y `ResourceParityTests` no valida el contenido del usuario. | ACU §1; PLAN §3; FORM |
| 2026-10-10 | Excepción de catálogos: el inglés de los ítems es opcional y se muestra el español. Nombre y descripción del catálogo, en es y en. | ACU §1; E2 |
| 2026-10-16 | En lo nuevo de la Etapa 4 (reglas, mensajes y diseño del PDF) solo el original es obligatorio. Si el idioma de quien recibe está incompleto en cualquier aviso de la revisión o en el diseño del PDF, todo el evento sale en el original. La elección se fija en los runs. | E4; FORM |
| 2026-10-16 | Etapa 5: nada exige traducciones. El asistente responde en el idioma de la conversación. | E5 |
| **2026-10-16, para después de la Etapa 5 (primera prioridad)** | Solo el idioma original es obligatorio para **guardar y publicar**; la traducción es opcional. Si no está completa, formulario, PDF, mensajes y opciones de catálogo salen en el original, **sin mezclar**. Cambia D5 (publicar exige es/en), la publicación en Meta (solo los idiomas completos), el envío y los catálogos. Antes se ajustan los tableros del editor y de publicar. El idioma del sistema no cambia. Varios países y más idiomas, después. | IDX, «Después de la Etapa 5» |

Lección: el catálogo Manual guardaba un solo texto, en el idioma de quien respondía. Con un formulario en español y una cuenta en inglés, el PDF en español decía «North branch». Se corrigió guardando el texto de cada ítem por idioma al capturar (R1017 #3, cerrado). La regla «sin mezclar» exige guardar **todas** las variantes de idioma de lo capturado.

### Tabla de permisos

| Código | Qué habilita | Desde | Fuente |
| --- | --- | --- | --- |
| `formtemplates.read` | Leer definiciones, borradores y publicación; preview | Etapa 1 | FORM; código `BusinessFormsPermissions.cs` |
| `formtemplates.manage` | Crear y guardar borradores; ver pedidos de formularios que faltan (Etapa 5) | Etapa 1 | FORM; E5 |
| `formtemplates.publish` | Chequear, publicar y consultar la operación propia | Etapa 1 | FORM |
| `formrecords.create` | Completar formularios. Se verifica sobre la **cuenta del destinatario**, nunca la del operador | Etapa 1 | FORM; WA |
| `formrecords.read` | Ver valores en el chat, planilla, detalle, archivos y PDF | Etapa 1 (2026-10-09) | FORM |
| `formrecords.export` | Exportar a Excel, además de read | Etapa 3 | FORM |
| `formcatalogs.read` / `.manage` | Catálogos | Etapa 2 | E2 |
| `formrules.manage` | Reglas, diseño del PDF y revisión de avisos inciertos | Etapa 4 | E4; FORM |
| `formapprovals.decide` | Aprobar o rechazar, con pertenencia vigente al rol y sin ser el autor | Etapa 4 | FORM |
| `formsuggestions.manage` | Sugerencias (pendiente) | Etapa 5 | E5 |
| `whatsapp.configureassistant` | Pasar a modo Asistente (pendiente, Admin por seed) | Etapa 5 | E5 |
| `whatsapp.read` y `whatsapp.send` | Listar y preparar formularios en el chat, y enviarlos | WhatsApp existente | WA |

Reglas comunes:

- Permisos globales asignados a roles, sin grants por formulario. «No diferenciar formularios por nombre de rol» (PLAN §11).
- `formtemplates.read` es necesario también para editar; no se asume que un permiso implique otro (PLAN §11).
- Permiso nuevo siempre con código `área.acción` de dos segmentos, según `docs/guides/permiso-nuevo.md`.

### Números y límites

| Concepto | Valor | Fuente |
| --- | --- | --- |
| Número de respuesta | `PREFIJO-NNNNNN`; prefijo ASCII de 1 a 12, único y no reciclable | FORM |
| Preguntas / opciones manuales | 40 / 50 | FORM; WA |
| Opciones de catálogo por envío | 200; nunca se trunca | E2; FORM |
| Texto / detalle de opción de catálogo | 30 / 300 caracteres por idioma | FORM (2026-10-15) |
| Archivos por respuesta | 10 (suma de máximos); 100 MiB en total | E2B; FORM |
| Tamaño por archivo | `MaxFileSizeMiB` de 1 a 25, 25 por defecto, sellado al publicar | FORM |
| Dimensiones de foto | 16.384 px por lado; 32 Mi px de área | FORM |
| Respuesta `response_json` | 16 KiB | WA |
| Texto que acompaña el formulario | 1 a 1024 caracteres | WA |
| Vigencia de la sesión | 24 h (1 a 168) desde la cola, según la hora firmada por Meta | WA; FORM |
| Ventana de mensajes | 24 h, verificada al aceptar y al despachar | WA |
| Reintentos de captura | 5 fallas y al menos 15 min desde la primera; plazo de 90 s por captura; intervalos 30/90/210/450/930 s | WA |
| Check token de publicación | 10 minutos | FORM |
| Comprobante del borrador | 30 días, después 410; tombstones sin plazo | FORM |
| Excel | 10.000 respuestas, lotes de 100, 30 s, una exportación concurrente por usuario | FORM |
| Condiciones por regla | 10, en AND | FORM |
| Motivo de rechazo | 500 caracteres | FORM |
| PDF: nombre / pie | 160 / 1000 | FORM |
| Cupos por actor | escritura de formularios 30/min; operación de publicación 120/min; lecturas de formularios 120/min; archivos 600/min | FORM; R1015 |
| Ofrecimientos del asistente | 2 por día y conversación, en la zona del negocio | E5 |
| Retención del texto de WhatsApp | 90 días; los registros de formularios no tienen plazo (decisión pendiente) | WA; PLANS |

---

## 3. Lecciones de los code reviews: errores a no repetir

Cada punto dice qué pasó, por qué importaba y cómo se corrigió. Entre paréntesis va el review y su número de punto.

### 3.1 Contratos estrictos entre backend y front

- **Un campo nuevo en la respuesta rompe al front estricto.** El recibo del envío trajo `origin` y el esquema estricto del front lo rechazó: la web mostró un error aunque el POST dio 202 y la consulta 200. Lo encontró el piloto. Arreglo: sumar `origin` al contrato del front, con su fixture exportada y un test (R1017 #17; FORM, piloto del 2026-10-17). Cada cambio de DTO se exporta y el front lo consume en el mismo corte.
- **Las rutas de error tienen que tener la misma forma en los dos lados.** El backend devolvía `rules[0].actions[0].roleId` y el front buscaba `rules.0.actions.0.roleId`, así que los errores de campo de las reglas nunca se marcaban. Además, la fixture `role-unavailable-es.json` estaba exportada y sin uso. Arreglo: normalizar las rutas, abrir la regla del primer error y testearlo con esa fixture (R1017 #10).
- **`""` no es `null`.**
  - Una pregunta de catálogo nueva mandaba `catalog: ""`: el backend respondía un 400 genérico y no marcaba la pregunta. Arreglo: mandar `null` y testear el alta completa, tipo → catálogo → guardar (R1014 #4).
  - Borrar el texto en inglés dejaba `""` en vez de `null` (R1012, front).
  - El Flow emitía `description: ""` en opciones sin detalle. Arreglo: omitir la propiedad (R1014 #9).
- **Un helper HTTP no debe pisar el `Accept`.** `api.blob` forzaba `Accept: application/octet-stream` y los errores llegaban como texto plano: el 429 sin `retryAfter` y el 500 sin `traceId`. Arreglo: no sobrescribirlo y probar con 404 y 429 (R1015 #7).
- **Una entrada inválida del cliente nunca es 500:**
  - `sortFieldId` ajeno (R1016 #3) y `sort=field%20` sin `sortFieldId` (R1017 #7) daban 500.
  - Intercambiar los códigos de dos ítems daba 500.
  - El timeout de la vista previa salía como 500.
  - Arreglo: 400 con su test, y validadores reales (R1012).
- **Un campo omitido no puede apagar algo.** Omitir `isActive` en un ítem lo daba de baja: se volvió obligatorio (R1012).
- **El OpenAPI declara lo que pasa de verdad.** No declaraba el 409 de Filters y sí uno que el alta manual nunca devuelve (R1012).
- **El 429 lleva `retryAfter`** (R1017 #7).
- **Un error tiene que nombrar lo correcto.** La vista previa respondía «catálogo no encontrado» cuando la persona de ejemplo no existía (R1012).

### 3.2 Estados inciertos y reintentos que nunca terminan

- **Un error definitivo no es incierto.** `UnsupportedConfiguration` y `PayloadMismatch` del Accept se trataban como Uncertain. Con el perfil r5, una publicación aceptada bajo r4 reintentaba cada 30 segundos para siempre y el formulario no se podía volver a publicar. Arreglo: los dos terminan en Failed visible (R1012 #1; WA, «Rechazos y reintentos»).
- **Toda cola necesita un final.** Una respuesta que fallaba siempre quedaba pendiente para siempre, ocupaba el lote de 50 (con 50 así se frena la cola) y nunca se purgaba. Arreglo: reintento acotado y después `Rejected` visible (R1012 #2).
- **Contar intentos no alcanza: hace falta una antigüedad mínima.** Cinco fallas eran unos 2,5 minutos, así que una caída de Redis de 3 minutos dejaba respuestas válidas rechazadas. Arreglo: además de cinco intentos, 15 minutos desde la primera falla persistida, con un test de caída (R1014 #5).
- **Un elemento trabado no puede frenar a los demás.** Las capturas se procesaban en serie, 30 segundos por archivo, y la trabada iba primera en cada lote. Arreglo: plazo total por respuesta (90 segundos) y saltear en la vuelta las que fallaron hace poco, usando el contador y la fecha de primera falla que ya existían (R1015 #4).
- **Unas pocas acciones inciertas frenaban el motor de reglas.** `FormActionBackgroundService` procesaba una ejecución cada 2 segundos y las Uncertain volvían a la fila cada 5 segundos, con el lock global. Tres avisos inciertos bloqueaban confirmaciones, tareas y PDF. Arreglo: drenar el lote completo aislando fallas por candidato, ordenar por `NextCheckAtUtc` y no volver a consultar una Uncertain sin pedido. Agregar el test del bucle, que faltaba porque el worker está apagado en los tests (R1017 #1, cerrado).
- **Todo estado necesita una salida.** El detalle ofrecía Consultar y Reintentar sobre un aviso «Sin confirmar», pero `RetryAsync` solo reintentaba Failed. Arreglo: revisión humana atribuida y durable («salió» o «no salió»), con revisión esperada y consulta antes de repetir. «No se envió» habilita reintentar y consultar nunca reenvía (R1017 #2, cerrado).
- **Un error que no es transitorio no se reintenta.** Un .docx dañado o cifrado quedaba 15 minutos reintentando, bajando todo cada 30 segundos, y terminaba como falla técnica. Arreglo: capturar `FormatException` y `InvalidDataException` y rechazar enseguida con `Files.Content.InvalidType`, con tests de tamaño real (R1015 #1).
- **Validar los datos de configuración al reservar.** Pedir aprobación a un rol borrado dejaba la respuesta pendiente para siempre. Arreglo: verificar el rol al reservar y fallar visible con `Forms.Rule.RoleUnavailable` (R1017 #5).
- **Un destinatario malo no puede tumbar a todos.** «Avisar a un rol» fallaba entero si un miembro no tenía WhatsApp, y frenaba las acciones siguientes. Arreglo: mandar a los que se puede, mostrar a quién no y no bloquear; un rol vacío no frena lo siguiente (R1017 #4, en curso).
- **Lo sellado manda.** La captura usaba el tope de tamaño vigente y no el sellado al publicar, y el cliente de medios fijaba 25 MiB por su cuenta: hay que alinearlos (R1015, «Con la Etapa 3»).
- **Antecedentes de la Etapa 1.** Se agregaron aislamiento y reintento por entrada, fallas definitivas antes del envío, recuperación del comprobante tras revocar, desvincular o vencer, y un presupuesto de respuesta de 16 KiB que bloquea publicar sin truncar (PLAN, «Pendientes rescatados al retirar las revisiones»).

### 3.3 Avisos duplicados: un solo dueño por error

- **Dos dueños, dos avisos.** Al preparar un envío salía un toast global y además el aviso del diálogo, cada uno con su Reintentar, y un 409 se mostraba como falla de carga. Arreglo: dueño local y el 409 como formulario bloqueado (R1014 #6).
- **Un toast por archivo.** La query de archivos no tenía `errorOwner`, y en el visor el error salía como toast y como `role="status"`. Arreglo: dueño local y un único aviso por respuesta (R1015 #6).
- **El mismo error en varias pantallas.** Detalle y planilla avisaban la misma falla por separado; la navegación mostraba un toast en cualquier pantalla; Reintentar aparecía aunque no fuera un error de red. Arreglo: un dueño por pantalla, navegación sin toast global y Reintentar solo para red (R1016 #9).
- **Errores como párrafos, a veces repetidos.** Las pantallas nuevas de la Etapa 4 mostraban así los errores generales. Arreglo: `notify` con un id por operación; junto al campo queda solo lo vigente (409, espera, «tu propia respuesta») (R1017 #9).
- **Un pedido por tecla.** Los conteos del panel de filtros mandaban el borrador en cada tecla: «12,», «-» o espacios llegaban crudos, el backend respondía 400 y cada tecla dejaba un toast con un Reintentar que volvía a fallar. Arreglo: contar solo con el borrador normalizado y válido, con espera; convertir la coma y recortar espacios (R1016 #1). R1017 #15 encontró la corrección incompleta: un error del conteo deshabilitaba Aplicar y Reintentar aparecía ante un 400.
- **Avisos del front en R1012:** el éxito al crear se borraba al navegar; «Más de 200 opciones» salía solo como un aviso de 8 segundos; un 403 al guardar reemplazaba el editor por «Sin permiso» en vez de dejarlo en solo lectura; se mostraba «resultado incierto» sin haberse escrito nada.
- **Los textos dicen la verdad:**
  - Dar de baja un catálogo frena el envío de los formularios que lo usan, y la confirmación no lo decía (R1014 #8).
  - Después de un error de red el aviso pedía «revisar el estado», cuando correspondía volver a guardar (R1014 #8).
  - El texto de espera decía «antes de consultar» al guardar, decidir y descargar; desde la bandeja, volver decía «Volver a respuestas» (R1017 #16).
  - Plurales `_one`/`_other`, KB en vez de «0 MB», y «Podés mandar el formulario de nuevo» sin permiso de envío (R1015 #11).

### 3.4 Cachés y pedidos de red

- **Miniaturas pedidas una y otra vez.**
  - Con `staleTime: 0` y refetch encendido, el visor y cada vuelta a la pestaña volvían a pedir todo y agotaban el cupo de 120 por minuto. Arreglo: `staleTime: Infinity`, sin refetch, `gcTime: 0` y un test que cuente pedidos; en el backend, una política propia para archivos (600 por minuto) (R1015 #5).
  - Después, con `gcTime: 0` se volvían a bajar en cada visita (cientos de pedidos con 100 filas). Arreglo: retenerlas mientras dure el ámbito de respuestas, con purga por dueño y permisos, y cargar solo las visibles (R1016 #11).
- **No reemplazar el editor mientras carga.** En la búsqueda de persona de ejemplo, cada tecla cambiaba la clave de la query y un Spinner reemplazaba al editor; si fallaba, el editor desaparecía. Arreglo: conservar los datos anteriores, Spinner solo en la primera carga, y espera de 300 ms (R1012 #9).
- **Un reloj de 1 segundo redibujaba toda la conversación** (R1014 #7).
- **Una dependencia en la dirección equivocada.** `layouts` importaba `app`: armaba un ciclo, sumaba respuestas al bundle inicial y pedía la navegación en todas las pantallas. Arreglo: componer el shell desde `app` y que `layouts` use solo un contexto neutral (R1016 #13).
- **Lo privado no vive en cachés persistentes.** Borradores, claves y valores privados quedan fuera de la URL y del storage. TanStack Query guarda solo datos confirmados, segmentados por usuario, cultura y permisos, y se limpia al perderlos (PLAN §6; AG-F). R1012 y R1014 lo marcan como algo que está bien.

### 3.5 Rendimiento de planillas y exportación

- **El Excel repetía toda la lectura en cada página.** Cada página de 100 rehacía permisos, definición, esquemas, COUNT y ORDER BY con OFFSET de hasta 9.900; con unas 9.500 filas podía vencer el plazo, y solo se había probado con 3. Arreglo: definición y columnas una vez, una sola consulta de IDs ordenados (hasta 10.001) e hidratación por lotes de 100, más una prueba de 250 filas y una unitaria del borde de 10.000 (R1016 #2).
- **El esquema JSON se deserializaba fila por fila.** Arreglo: un diccionario por versión deserializado una vez (R1016 #4). R1017 #7 encontró que `SchemaJson` y `NameJson` todavía viajaban por fila en el listado y el export. Antes, R1014 había visto que `HasCatalogs` deserializaba por fila y que el formulario se buscaba tres veces.
- **Un SQL distinto por cada combinación.** Ordenar por una elección armaba un CASE por versión y opción con literales. Arreglo: una subconsulta con el ChoiceId y un CASE parametrizado (R1016 #5).
- **Una exportación sin límite propio** compartía el cupo de lectura y tenía todo en memoria. Arreglo: política propia, una exportación a la vez por usuario, sin cola (R1016 #6). Queda pendiente que ese límite es por proceso, no global (R1017).
- **Limpieza cuadrática.** La limpieza de huérfanos hacía una consulta por blob, con un OR sobre `ThumbnailKey` sin índice. Arreglo: consultar por página con `= ANY(@keys)` y deducir la clave de la miniatura (R1015 #8).
- **Copias de más.** Cada ejecución del motor copiaba la respuesta entera, y la del PDF dos veces. Arreglo: fijar solo lo que cambia y leer el registro por Id (R1017, cerrado dentro del #3).
- **Un snapshot de columnas con una publicación concurrente.** El número máximo se fija antes de cargar los esquemas y las consultas se limitan a las versiones del snapshot, para no mezclar esquemas (R1016, «Avance»).
- **EXPLAIN sobre el SQL real.** El EXPLAIN tiene que correr sobre el SQL que genera EF, no sobre uno escrito a mano (R1016 #14).

### 3.6 Seguridad de archivos

- **El nombre de descarga lo elegía quien mandaba el archivo.** Un «acta.pdf.html», con contenido que es PDF y HTML a la vez, se descargaba como HTML; tampoco se filtraba U+202E, que invierte el texto. Arreglo: raíz saneada más la extensión del tipo verificado, quitando los caracteres de formato (categoría Cf), con el nombre en el DTO y usado por el front (R1015 #2).
- **Imágenes enormes.** El tope de 32 MP se medía sobre la imagen ya escalada: un JPEG progresivo que declaraba 40.000 × 40.000 podía pedir gigas de memoria. Arreglo: rechazar por ancho, alto y área del encabezado antes de decodificar. Las imágenes que llegan como documento se inspeccionan sin rasterizar (R1015 #3).
- **No confiar en el tipo de dato del proveedor.** Un `file_size` que no era número se revisa ahora con `ValueKind` (R1015 #1). El ID de medio de Meta puede llegar como número JSON entero y se conserva como texto (piloto 2B).
- **Un blob faltante respondía 500.** Ahora responde 404 aunque exista la metadata (R1015 #13).
- **Lo que está bien** (R1015): claves determinísticas sin el nombre del archivo; todo verificado antes de subir; subida antes del commit; pertenencia entre respuesta y archivo con 404; `nosniff`; sin URL públicas ni SAS; el token de Meta solo hacia hosts permitidos, sin redirects y con lectura en streaming con tope; Azure, SkiaSharp y OpenMcdf confinados a `Infrastructure/Files`.
- **Pendiente:** antivirus antes de producción (E2B; FORM).

### 3.7 Privacidad y exposición de datos

- **La preparación del envío exponía a todas las personas.** Un operador con `whatsapp.send`, sin `users.read` ni `formcatalogs.read`, recibía nombres y detalles de hasta 200 personas, y el diálogo solo usaba la cantidad. Arreglo: un DTO de WhatsApp con `{fieldId, label, count}` y los problemas. El contrato no estaba publicado, así que se cambió junto con el front (R1014 #1).
- **`ToString` redactado** en `StructuredFormPreparationIssue`, `FormRecordReferenceRow` y `StructuredRecordChoiceValue`, y en todo modelo sensible (R1014; PLAN, «Bajas rescatadas»).
- **Retención de lo sellado.** La oferta sellada en `WhatsAppFlowSessions.ProtectedPayload` tiene que entrar en la retención (R1014).
- **Valores fuera de toda superficie pública.** Sin `formrecords.read`, nada de valores en previews, avisos, aria-labels ni caché tras revocar (PLAN §11; WA). Los avisos de reglas se retiran del front antes de la caché (WA, G6).

### 3.8 Modelo de datos e integración con el núcleo

- **Una vista sobre las tablas de otro dueño frena sus migraciones.** La vista sobre `AspNetUsers`, `AspNetUserRoles` y `AspNetRoles` habría trabado cualquier migración del núcleo y cortado el despliegue. Arreglo, lo más simple: SELECT fijo en el lector del módulo y una migración que quita la vista (R1012 #4).
- **Guardar referencias por Id, no por nombre.** El filtro por rol guardado por nombre dejaba el catálogo con 0 opciones si se renombraba el rol, y un rol sin miembros no se podía elegir. Arreglo: guardar el RoleId y mostrar el nombre actual (R1012 #5).
- **Las opciones vacías no viajan.** Los usuarios sin nombre generaban opciones vacías. Arreglo: excluirlas (R1012 #6). Además, `TextColumn` admitía `role` y `active`, y un usuario sin roles daba `""`: ahora solo admite `name` (R1014 #3).
- **Validar contra los límites del canal al guardar, no al mandar.** Una opción que no entraba en WhatsApp bloqueaba todos los envíos sin decir cuál: Manual aceptaba 200 y 500 caracteres, y Filters usaba `DisplayName` de hasta 100, así que una persona con nombre largo bloqueaba el catálogo para todos. Arreglo: 30/300 al guardar en backend y front, la vista previa marca a quien no entra, y el bloqueo dice cuántas opciones y de qué catálogo (R1014 #2).
- **La misma regla en SQL y en memoria.** «Sin respuesta» sobre una pregunta inexistente daba falso, contra lo decidido. Arreglo: aplicarlo igual en reglas y SQL, manteniendo la paridad; la planilla sigue mostrando «—» (R1017 #8).
- **Un lector devuelve filas y decide el servicio.** El lector devolvía `*Response` y decidía el estado; además buscaba por `Key` (un GUID que duplicaba el Id sin uso). La gramática estaba definida en tres lugares y faltaba `OperationLog.RunAsync` (R1012).
- **Lo decidido tiene que existir.** Dar de baja y reactivar estaba decidido pero `SetActive` no tenía ruta, así que el estado «Dado de baja» no se podía producir. Arreglo: `isActive` en el PUT con revisión y 409 (R1012 #7).
- **Tras un error de red, reintentar con la misma revisión.** Guardar después de un error de red o 5xx exigía revisión y la única salida era descartar todo. Solo un 409 pide revisar (R1012 #8).

### 3.9 La interfaz frente a los tableros

- **Dejar el foco en `<body>`** es un error repetido: al perder el permiso con el visor abierto (R1015 #9), después de Listo, quitar o mover en reglas (R1017 #14) y al decidir en el detalle (R1017 #11). Arreglo: llevar el foco a la tarjeta, al aviso o a la decisión, con su test.
- **Navegar no desmonta la página.** Anterior y Siguiente perdían el foco y el título parpadeaba. Arreglo: conservar el detalle anterior mientras carga, con `aria-busy` (R1016 #10).
- **El panel de decisión desaparecía al decidir**, y un 409 se veía solo un instante (R1017 #11).
- **Un iframe con sandbox no abre el visor PDF** de Chrome ni de Edge. Arreglo: `<object>` o un iframe sin sandbox, con descarga como alternativa, verificado en un navegador real. Ya se había visto en headless y en el piloto (R1017 #12).
- **Un 403 de exportar ocultaba toda la planilla.** Arreglo: cerrar el diálogo y avisar, sin quitar la lectura (R1016 #7). El total del diálogo podía estar desactualizado y la espera de un 429 se perdía al cerrar (R1016 #8).
- **Diferencias con el tablero en cada review:**
  - K3: faltaban «Así lo ve el empleado» y el segmentado (R1012).
  - T2: faltaba el pie fijo (R1016 #12).
  - G4 y G5: el historial no estaba en orden cronológico y el estado decía «Pendiente» en vez de «Por aprobar» (R1017 #13).
  - G1: faltaba el «si» en palabras y el motivo de «Guardar reglas» deshabilitado (R1017 #14).
- **Un cambio de corte sin tablero.** La bandeja de WhatsApp pasó de `md` a `xl` sin dibujarlo: se vuelve atrás, o se dibuja y se elige (R1014).
- **Una acción que puede no hacer nada.** «Mandar de nuevo» no miraba la cuenta ni los formularios elegibles. Arreglo: `useFormComposer` dice si se puede abrir (R1014 #7).
- **Inputs numéricos.** `Number()` convertía el campo vacío en 0 y aceptaba 2.5 o -1. Arreglo: enteros acotados (R1015 #12).
- **Botones sin cambios.** Descartar pedía confirmación sin cambios y Guardar sin cambios subía la revisión (R1012).

### 3.10 Tests y documentación

- **Tests que dependen del orden** causaban las corridas completas fallidas:
  - Una vista y una clave diferida que solo creaba un test, porque `EnsureCreated` no las crea.
  - `Assert.Single` sobre toda la base.
  - Más de 200 usuarios de otros tests disparaban `ExceedsLimit`.
  - Arreglo: crearlas en el fixture del módulo y acotar cada aserción a los datos propios del test (R1012 #3). En el cierre de la Etapa 1 pasó lo mismo con un recibo, y se aisló (FORM, «Verificación de cierre»). Regla: «Un test que falla solo junto con los demás es un problema de aislamiento de datos del test» (VAL).
- **Un arnés de navegador no es un test.** `BusinessFormsManualAppPreviewTests` era un arnés de navegador puesto como test (R1012).
- **Probar los recorridos de punta a punta:** captura → tarea → decisión → PDF → aviso, en inglés y con caída al original; catálogo en otro idioma; presupuesto vencido; liberación del límite del export (R1017 #18). Tamaños reales en archivos (R1015 #14). La gramática negativa y el 403 en cada ruta (R1012).
- **Un worker apagado en los tests deja bugs sin ver.** El bucle del motor no tenía test porque el worker está apagado en los tests (R1017 #1).
- **La documentación se actualiza con el cambio.** AGENTS.md y el diseño del front seguían diciendo que K6 y K7 estaban pendientes (R1014 #11).
- **Fixtures exportadas y sin uso.** Una fixture que existe y nadie consume delata un test faltante (R1017 #10). Se borran fixtures de cohortes anteriores y código muerto (R1012; R1014).

### 3.11 Lo que aprendieron los pilotos con Meta

- Un Flow publicado es irreversible y sus assets son inmutables, así que **cada versión crea un Flow físico nuevo** (WA).
- Meta rechazó el perfil r2; r3 emite los máximos como números JSON (PRE).
- El bloqueo 141006 desapareció al corregir el pago del número (FORM).
- La preview oficial acepta `12.50` y rechaza `12,50`. La ayuda de las publicaciones futuras indica punto, y el receptor acepta ambos (FORM).
- Meta exige **IDs de pantalla de solo letras y guiones bajos** (r7) (WA).
- El **ID de medio** puede llegar como número JSON entero: se conserva su lexema como texto (WA).
- **Una pregunta de archivo opcional vacía llega con la propiedad omitida** (WA; FORM).
- **200 opciones con 136 caracteres de detalle cada una** entran en los datos iniciales (E2).
- El número de prueba de Meta rechaza celulares argentinos con el 9 (131030). `SendArgentineMobilesWithoutNine` existe solo para él y en producción va apagado (WA).
- Una invitación es una plantilla Marketing y puede no llegar (131049) (WA).
- Graph v25.0 rechaza `health_status` sobre `phone_number` (WA).
- **Ningún test con dobles detectó estas incompatibilidades:** solo el piloto. Corregirlas con test rojo primero y aplicarlas solo a publicaciones futuras, sin recompilar lo sellado.

### 3.12 Lecciones de proceso (ADR 0018 y la Etapa 1)

- **La Etapa 1 llevó cuatro días de los cinco previstos para las cinco etapas.** No fue por el código (unas 6.000 líneas de backend y otras tantas de tests): fue por robustez de efecto externo en guardados internos, por la suite completa en cada corte y por contratos y bitácoras en Markdown (un contrato de 1.381 líneas antes del código, revisiones por corte y un arranque que registraba cada commit) (ADR 0018, «Contexto»).
- **Los planes no deben pesar más de lo que un agente puede leer:** de ahí la depuración a menos de 300 KB (HIST).
- **Lo que un test de arquitectura exige se resuelve con composición útil, no con trucos.** En el incremento 0, `ModuleBoundaryTests` exigía que `Program.cs` nombrara el módulo. No se agregó un `using` nominal ni se relajó la prueba: se esperó a la capa con composición real (R1009).
- **Un build que termina exit 1 sin diagnóstico no se informa como exitoso**, y un rojo de compilación no cuenta como rojo de TDD (R1009).
- **Una suite verde no acredita el diseño:** «La revisión también cubre lo que los tests no demuestran» (VAL).

### 3.13 Patrones que los reviews confirmaron como correctos

Fuentes: R1012, R1014, R1015 y R1017, «Lo que está bien».

- **SQL:** desde lista blanca y con parámetros, sin correo ni teléfono.
- **Guardado:** permisos en controller y servicio; una transacción por escritura con 409 por revisión.
- **Módulos:** BusinessForms no nombra WhatsApp ni IA, y las features del front no se importan entre sí.
- **Oferta sellada por sesión,** validada en el canal y en el sink. El rechazo por baja ocurre antes de numerar.
- **Perfiles anteriores:** r3, r4 y r5 siguen mandando y capturando, con huella compatible.
- **Aprobación:** dentro del lock e idempotencia por ejecución.
- **Envíos:** ventana de 24 h verificada y logs sin valores.
- **Front:** piezas comunes, textos es/en con voseo, borrador fuera de la URL y de la caché, 409 que conserva el borrador y errores junto al campo.

---

## 4. Pendientes y decisiones para después de la Etapa 5

### 4.1 Decidido, en orden

1. **Idioma de los formularios** (decidido el 2026-10-16, primera prioridad después de la Etapa 5). Solo el idioma original es obligatorio para guardar y publicar; la traducción es opcional. Si no está completa, formulario, PDF, mensajes y opciones de catálogo salen en el original, sin mezclar. Cambia D5, la publicación en Meta (solo idiomas completos), el envío y los catálogos. **Antes se ajustan los tableros del editor y de publicar.** El idioma del sistema no cambia. Varios países y más idiomas, después (IDX).

### 4.2 Sin orden fijo; cada uno necesita su tablero y su decisión antes de programarse

Fuente: IDX, «Después de la Etapa 5», salvo indicación.

- **Catálogo con una consulta (Query/K5),** solo si una lista no puede armarse con filtros, con todos los requisitos del ADR 0019.
- **Intercambio cifrado con Meta:** búsqueda en vivo, listas dependientes, listas largas y validación previa. Hace falta calificar claves, protocolo y límites con Meta antes de activarlo (PLAN §5.2).
- **Grupos y remitos con líneas.** El tipo `group` ya está reservado en `format=1`. Las condiciones de una línea deben cumplirse en la misma fila (RowId), sin «la primera fila» implícita (PLAN §4).
- **Corregir una respuesta:** una respuesta nueva que reemplaza a la anterior, sin ciclos ni dos sucesoras vigentes y sin alterar el PDF ni los efectos anteriores (PLAN §3).
- **Borrador de formulario armado por la IA** a partir de los pedidos de formularios que faltan. Es una capacidad de propuesta del motor único (ADR 0020); la revisa y publica una persona y nunca se publica sola (PLAN §12.6).
- **Copiar, eliminar y deprecar** (PLANS):
  - Copiar asigna identidades y prefijo nuevos, sin numeración ni vínculos con Meta.
  - Eliminar solo definiciones nunca publicadas, sin respuestas ni operaciones, con borrado lógico que deja reservado el prefijo.
  - Deprecar en Meta los Flows de versiones reemplazadas cuando no queden sesiones vigentes.
- **Cargar respuestas desde la web e importar desde Excel** (respuestas o ítems de catálogo), juntos y con su tablero (IDX; ACU §1).
- **Plantillas de Meta para escribir fuera de las 24 h** (E4; E5).
- **Varios niveles o varios aprobadores** (E4).
- **Acciones de otros módulos, como stock, y compensaciones,** solo con un proveedor real idempotente (E4; PLAN §8 C).
- **«Descargar todas las fotos juntas»** (E5, D2).
- **Reutilizar un Flow físico entre definiciones** quedó para después (PLAN §12).

### 4.3 Pendientes abiertos de producto y operación

- **Retención de respuestas.** Hoy registros, valores, recibos y versiones se conservan sin plazo. Hay que decidir por negocio la retención o anonimización antes de producción; no heredan los 90 días de WhatsApp (PLANS; PLAN §7).
- **Antivirus** para archivos, antes de producción, a decidir con el despliegue (E2B; FORM).
- **Guía `docs/guides/quitar-formularios.md`,** como las de WhatsApp e IA (PLANS).
- **WhatsApp en producción** (VER, «Despliegue y operación»):
  - Número de producción y su PIN.
  - Plantillas en su cuenta.
  - HTTPS y webhook propios.
  - Secreto de la app.
  - Monitoreo del token y del costo.
  - Control de cambio de identidad (un número reciclado llega con otro BSUID).
- **IA antes de datos reales** (VER, «Inteligencia artificial»):
  - Calificar un modelo exacto por proveedor antes de declarar `ModelProfiles`.
  - Revisar egress y retención de las cuentas (`store:false` no es retención cero).
  - Restaurar la base, el anillo y el `InstallationId` en un ambiente aislado.
  - Probar dos réplicas y el apagado con turnos en curso.
- **Preview oficial embebida** (iframe y CSP) y otras variantes físicas, sin calificar (FORM, «Publicación y preview — 2b»).

### 4.4 Verificaciones pendientes

- **Piloto de la Etapa 4** (VER, «Formularios: piloto de aprobación y PDF»). Está verificado el recorrido en español con foto. Faltan:
  - El recorrido **en inglés con traducción completa**.
  - El **respaldo íntegro al idioma original** cuando falta la alternativa.
  - La **apertura web de la copia del PDF**, que Chrome bloqueó.
  - Registrar versiones, idiomas, resultado y límites, sin mensajes privados, teléfonos ni credenciales.
- **Heredados de la Etapa 1, sin cierre registrado** (E2, «Pendientes que se arrastran»; PLAN, «Pendientes rescatados»):
  - **Presentación R3:** versión e idioma de la respuesta y una señal explícita de purga del mensaje, sin inferirla de `Body=null`.
  - **Prueba física en el teléfono:** decimales (12,50 y 12.50), ausencia opcional, máximos de texto y sesión de una versión anterior.
  - **Test intermitente de Roles** en el front: diagnosticar sin relajar assertions.
  - **Dos pruebas visuales del editor** (es, 390 y 320 px) que fallan en la espera inicial de Nombre: comprobar la preparación de Idiomas con un control negativo antes de tocar los tests.

### 4.5 Correcciones de código pendientes

- **R1017, «Arreglar ahora»** (se corrige al empezar la Etapa 5, antes de programarla). Cerrados 1, 2 y 3. Al corte siguen abiertos (el árbol tiene cambios sin commitear que no se evaluaron):
  - 4: avisar a un rol sin bloquear.
  - 5: rol borrado → `Forms.Rule.RoleUnavailable`.
  - 6: vista previa del PDF con fotos de ejemplo y sin 500 por falta de nombre.
  - 7: arreglos incompletos de la Etapa 3: `SchemaJson` y `NameJson` por fila, `sort=field%20`, `retryAfter`.
  - 8: «Sin respuesta» sobre pregunta inexistente, igual en reglas y SQL.
  - 9 a 16: errores generales con `notify`, rutas de errores de campo, panel de decisión y foco, visor PDF sin sandbox, detalle e historial frente a G4 y G5, reglas frente a G1, conteo de filtros y textos.
  - 17: `origin` en el contrato del front.
  - 18: tests de punta a punta.
- **R1017, «Con la Etapa 5 o al tocar esos archivos»:**
  - Los avisos a un rol traban el chat manual en la cola única: medirlo y documentarlo.
  - El límite de exportación es por proceso.
  - Los emojis salen como cuadrados en el PDF.
  - Lo hecho de más: el control de duplicados inalcanzable en `FormPdfDocumentRecorder`, el `AppName` por defecto, el puerto de retención privada, tres limpiezas privadas casi iguales en el front y `NativeSelect multiple`.
  - Front frente a los tableros: filtro de Estado y PDF en filas aprobadas, resumen de cambios en el historial de reglas, Entregado y Leído en el historial, migas del diseño del PDF, y «Adjuntar el PDF» que queda marcado al cambiar de evento.
- **Listas «Con la X o al tocar esos archivos» heredadas** (no se hicieron, se atienden al tocar los archivos): R1012, «Con K7»; R1014, «Con la 2B»; R1015, «Con la Etapa 3»; R1016, «Con la Etapa 4».
- **Deuda menor de la Etapa 1** (PLANS):
  - Backend: lock global dentro del envío, default inalcanzable, OperationLog duplicado o por lote, interfaz pública de un helper, códigos de error, payload duplicado, costo del polling, retry por kind, contador de números en la definición auditable, recibos rechazados innecesarios, modelos sensibles sin `ToString` redactado y estados entre módulos como strings.
  - Front: 409 repetido, claves de caché y remount, texto de expandir y fuentes desactualizadas.
- **Deuda del front y del arnés** (PLANS):
  - Avisos React `act(...)` y pedidos MSW sin handler en la suite completa.
  - Cierre de Vite en Windows sin confirmar.
  - Cooldowns que descuentan por tick.
  - **El chat de WhatsApp guarda conversación, búsqueda, «Sin leer» y página en estado local en vez de la URL.**
