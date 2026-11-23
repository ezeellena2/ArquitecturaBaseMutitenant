# ADR 0068 · Las acciones del asistente: la IA prepara y la pantalla de siempre confirma

**Estado:** aceptada por el usuario el 2026-11-23. Ese día aprobó el diseño y eligió dónde programarlo: en la etapa «Acciones del asistente», después de la segunda parte de la base y antes de la E12.
- **Lo que toma:** el diseño técnico de la decisión 268 de la [hoja de ruta de formularios](../plans/2026-10-12-formularios-dinamicos-hoja-de-ruta.md#5-decisiones-de-producto), que es la v2 de la D8 de la [0048](0048-asistente.md). Toma la ADR 0034 de ArquitecturaBase («la IA prepara y la pantalla de siempre confirma») y la adapta a organizaciones, empresas y Plataforma.
- **Lo que deja igual:** la [0057](0057-la-ia-propone-y-una-persona-crea.md).
- **La revisión:** antes de aprobarse, el diseño pasó por una revisión adversarial, y sus arreglos ya están acá.

## Contexto

El usuario pidió, con sus palabras, que cuando la IA pueda crear, editar o eliminar:
- no toque lo de otras empresas;
- respete los permisos de cada persona;
- no agregue columnas, no admita SQL ni haga en la base algo que no tiene permitido;
- y que sumar una funcionalidad nueva, como «Proveedores», la deje disponible en el asistente con muy poco trabajo, con un patrón, reglas claras y documentación.

**Lo que había, medido el 2026-11-23 contra el código:**
- **Lo que ya cumple el asistente:** lee con 19 herramientas declaradas (`IAssistantTool`) con el actor firmado. Usa los permisos efectivos de quien pregunta y los relee antes y después de cada lectura, con RLS y sin campos de tenant ni de usuario en los argumentos (`AssistantToolCatalogTests`, `AssistantToolsExecutionTests`, `AssistantAuthorizationTests`). La [0053](README.md) prohíbe todo SQL que escriba una persona o un modelo. La app corre con `mt_app`, que no crea tablas ni es dueño de ninguna (`RuntimeRoleTests`). Los catálogos de la E12 son filas con columnas fijas.
- **Lo que faltaba:**
  - **escribir:** no había diseño ni etapa;
  - **solo leer:** las herramientas reciben servicios enteros, que también escriben, y ningún test les impide escribir;
  - **las transacciones:** una herramienta corre en el mismo contexto de EF que la transacción que el asistente abre después (`AssistantTurnService.ExecuteToolAsync`), así que algo que dejara pendiente se guardaría sin confirmar;
  - **sumar una lectura:** pide una clase dentro del módulo, y un área que vive en un módulo pide además un puerto del núcleo (`ModuleIsolationTests`).

**El problema de la escala:** si cada pantalla trajera su herramienta de proponer, las herramientas crecerían con las pantallas, igual que el costo de cada turno y el trabajo de IA de cada área.

## Decisión

1. **La regla.** La IA prepara, la persona confirma en la pantalla de siempre y guarda la ruta de siempre.
   - El asistente no tiene ningún camino propio para escribir.
   - Nunca puede hacer algo que esa persona no pueda hacer a mano en esa misma pantalla, y nunca lo hace sola.
   - La persona ve todo lo que se va a guardar.

   El patrón es un **registro de comandos declarados** (Command + Registry, abierto a extensión y cerrado a cambios), con **confirmación humana en dos fases**: preparar y confirmar. El comando es un dato: su ejecución es la ruta que ya existe.
2. **Una acción declarada.** Cada área declara sus acciones en un solo lugar: `AssistantActionDefinition`, en Application del núcleo, sin tipos de IA. Cada acción lleva:
   - **su id con versión,** sellado (`users.invite@1`);
   - **su clase:** crear, editar, eliminar u otra, como dar de baja o reactivar;
   - **su requisito y su ruta:** el requisito, el módulo que la prende (si es de un módulo) y la ruta que guarda (`SaveRoute`);
   - **cómo se confirma y en qué pantalla:** `Dialog`, `Page`, `DraftCard` o `RemoveConfirmation`, y la pantalla, de la lista cerrada de `AssistantLink.routeKey`;
   - **su efecto fuera del sistema,** de una lista cerrada;
   - **sus campos, y para editar o eliminar, qué lectura de la pantalla encuentra el registro;**
   - **sus textos** `Action.<id>@<versión>.*`, en es y en.
3. **El requisito es el de la ruta.** `AssistantActionRequirement` copia el atributo de la ruta que guarda:
   - `Consumer`, sin permiso;
   - `Organization` con su permiso;
   - `Company` con su permiso y el permiso de organización que también alcanza;
   - `Platform` con su permiso.

   La IA no tiene permisos propios. Cuentan los efectivos, con lo incluido de la 260.
4. **Los campos son los de la pantalla, no los del pedido HTTP,** y sus tipos forman una lista cerrada: `Text`, `LongText`, `Integer`, `Decimal`, `Money`, `Date`, `Boolean`, `Email`, `Phone`, `TaxId`, `Choice` y `Choices` (de una definición: un rol, un formulario, un catálogo), `Record` (un registro: una empresa, una persona, una opción de un catálogo) y `Consent`.
   - **Lo que no existe:** no hay tipo para claves, conexiones, SQL, expresiones ni URL, y ningún campo se llama como un tenant, un usuario o un secreto.
   - **Los consentimientos** son `Consent`: nunca se proponen y llegan sin tildar.
   - **Editar son operaciones.** Un campo simple se cambia, y una lista se edita con «sumar» y «quitar», nunca se reemplaza entera: «agregale Vendedora a Kevin» no le saca los otros roles.
5. **La fuente de cada área solo lee.** Cada área implementa `IAssistantActionSource`, un puerto del núcleo, con el sufijo `ActionSource` ([capas-y-flujo](../rules/capas-y-flujo.md)). Sus declaraciones son estáticas, y hace tres cosas:
   - **lista** las opciones de un `Choice`;
   - **resuelve** cada texto o referencia a un id permitido: único, con RLS, los permisos y lo que quien pide puede dar. Sin coincidencia vuelve al modelo, y con varias queda «Revisá»;
   - **valida sin guardar:** arma el `*Request` de la pantalla y corre su validador y las `Policy` de la ruta.

   **Dos barreras aseguran que solo lea:**
   - **sus dependencias:** solo readers, validadores, `Policy`, `Resolver`, `ICurrentUser` y las lecturas de permisos; nada de `IUnitOfWork`, repositorios, `Writer`, `Guard` ni servicios;
   - **dónde corre:** en un alcance de DI propio, con una transacción de PostgreSQL `READ ONLY`, y el alcance se descarta al terminar. Una escritura falla con 25006, y lo que quedó en memoria nunca llega a otra transacción.

   Las 19 lecturas de hoy reciben servicios enteros, así que para ellas vale la segunda barrera: pasan a correr en ese mismo alcance de solo lectura. Lo que decide un `Guard` de la ruta, como qué roles puede dar quien pide ([0061](0061-nadie-da-lo-que-no-tiene.md)), queda en una `Policy` pura que usan el `Guard` y la fuente: la regla está en un solo lugar.
6. **El registro filtra y sella.** `AssistantActionCatalog`, en el módulo Assistant, junta las fuentes por DI y comprueba al arrancar que cada id con versión sea único.
   - **Filtra en cada turno** por acceso, módulo prendido para la organización, requisito y que la acción esté ofrecida. Lee los permisos una sola vez por turno.
   - **Sella en el turno** la lista que ofreció ([0064](0064-reglas-de-toda-la-ia.md) §6), y solo se proponen esas acciones.
   - El núcleo no nombra al asistente ni a ningún módulo.
7. **Tres piezas de IA, fijas para todas las áreas:**
   - **la lista precargada** `assistant_actions`: el id y el nombre corto de hasta 40 acciones, como dato del turno;
   - **`read_action`:** los campos de una acción con su tipo y sus límites y, solo en un `Choice`, nombres de definiciones, 20 por página y con búsqueda;
   - **`propose_action`:** la acción, el destino y una lista de campo y valor. Pasa por el esquema, la resolución y la validación, y no guarda nada. Lo rechazado vuelve al modelo con la ruta de cada campo, sin listar registros.

   **Al proveedor** van lo que dijo la persona, los tipos de acción, los campos y los nombres de las definiciones. Nunca van registros, personas ni opciones de un catálogo: un `Record` lo escribe el modelo como texto y lo resuelve el servidor. El destino resuelto no vuelve al modelo.
8. **La propuesta.** Lo aceptado es **la propuesta del turno**: una por turno, y una segunda da `Assistant.Action.OnePerTurn`.
   - **Dónde se guarda:** en el mensaje del asistente, privada y con RLS, igual que la de la 0057.
   - **Qué guarda:**
     - la acción con su versión;
     - los valores, con los ids resueltos;
     - el destino y la versión que tenía al proponer (`baseVersion`);
     - las dudas.
   - **Lo que muestra la tarjeta** se resuelve en el servidor al dibujarla; nunca es texto del modelo.
9. **Confirma la pantalla de siempre.** La pantalla lee la propuesta por id (`GET /api/{lado}/assistant/proposals/{id}`). Esa lectura revalida:
   - el dueño, el acceso y la organización;
   - que la acción siga ofrecida para esa persona;
   - su versión.

   La dirección lleva solo el id, nunca valores. Cada pantalla presta un adaptador que dibuja **todos** los valores propuestos; si uno no se puede mostrar, rechaza la propuesta, y los selectores buscan por id lo elegido que no cargaron.

   **Cómo se confirma, según la acción:**
   - **`Dialog`:** ese diálogo sobre el Inicio, con «Propuesta del asistente» y «Revisá» en lo dudoso;
   - **`Page`:** esa página, sin guardar; si guardar sale del sistema, lo dice antes de «Guardar»;
   - **`RemoveConfirmation`:** la confirmación de esa pantalla, de a una cosa;
   - **`DraftCard`:** la tarjeta muestra todos los campos con valor y crea un borrador o un inactivo con la ruta de alta del área. Nada queda vigente hasta que alguien lo activa en su pantalla;
   - **editar:** se lee lo guardado con su versión, se aplican las operaciones y «Lo que cambia» muestra también lo que se quita. Si el registro cambió desde la propuesta, lo avisa, y vale el 409 de siempre.

   Sin pantalla donde confirmar, el asistente contesta «todavía no».
10. **Guarda la ruta de siempre,** sin excepción y con todo lo de siempre: `[Access]`, el permiso, la validación, el servicio, `IUnitOfWork`, `Result`, RLS, el `Guard` de la 0061 y la auditoría, con la persona como actor.
    - **La primera `Idempotency-Key` sale de la propuesta** (`UUIDv5` de su id). Dos pestañas, o la tarjeta abierta otra vez, repiten el resultado en lugar de duplicarlo.
    - **«Aplicada» la marca el servidor,** después de comprobar que esa clave terminó en 2xx, para esa persona y esa ruta. Una propuesta aplicada ya no abre la pantalla con valores: lleva a lo creado.
11. **Los tres lados y WhatsApp:**
    - **Plataforma** solo propone sobre lo de Plataforma. Adentro de una organización contesta «todavía no», porque entrar pide `ITenantScope.Enter` y un `SecurityEvent`.
    - **Una persona** solo tiene acciones `Consumer` y nunca crea empresas.
    - **Por WhatsApp** va un resumen con «Revisar en la app», solo para quien trabaja en la organización y con sus permisos (268). Se suma cuando el canal tenga lugar para estas piezas.
12. **Versiones, límites y calificación.**
    - **Versiones:** cambiar los campos de una acción es una versión nueva, que se lee un despliegue antes de escribirse ([0060](0060-dos-versiones-conviven.md)). Una propuesta de una versión que ya no existe se ve «ya no disponible».
    - **Límites:** una propuesta por turno, dentro de las 5 llamadas; hasta 40 acciones en la lista, 20 opciones por página, 40 campos y 16 KiB por propuesta; el cupo diario y el tope mensual de siempre.
    - **Calificación:** una acción se ofrece recién cuando sus casos pasan la calificación con el modelo real (268) y su costo está medido. Mientras tanto queda registrada y sin ofrecer.

## Modelo de amenazas

| Amenaza | Qué la frena |
|---|---|
| Instrucciones escondidas en un mensaje, un adjunto o un nombre | Son datos, y el prompt sale de resources. La IA solo propone una acción sellada para esa persona, de a una; la pantalla muestra todo lo que se guarda, con nombres del servidor |
| Valores que la pantalla no muestra | Los campos son los de la pantalla; el adaptador rechaza lo que no dibuja |
| Otra organización o empresa | La organización sale del claim, y ningún campo es de tenant ni de usuario. Destino, registros y opciones se resuelven con RLS: lo ajeno da 404 |
| Escalar permisos | La IA no tiene permisos propios. El requisito es el de la ruta, las opciones y la resolución usan la `Policy` de la 0061, y decide la ruta al guardar |
| Escribir sin confirmar | Fuentes sin dependencias de escritura; fuentes y lecturas, en un alcance aparte y `READ ONLY` |
| Editar de más | Listas con «sumar» y «quitar», «Lo que cambia» con lo que se quita, `baseVersion` y el 409 |
| SQL | Valores tipados que van a un `*Request` y a EF con parámetros; ningún tipo de campo es texto ejecutable; sin `FromSqlRaw`, `ExecuteSqlRaw` ni `SqlQueryRaw` |
| Cambiar el esquema | No hay camino: las acciones son rutas que existen, los catálogos son filas y `mt_app` no crea ni altera tablas |
| Duplicar | La clave sale de la propuesta, y «Aplicada» la comprueba el servidor |
| Abrir la propuesta de otro | Solo la lee su dueño (0048, D5) |
| Datos de terceros al proveedor | Van tipos, campos y nombres de definiciones; nunca registros ni personas (0064 §10) |
| Cantidades y costos | Los límites de la regla 12, y el costo de cada acción medido al calificarla |

**El build no puede probar cómo resiste el modelo una inyección.** Esa barrera la ponen la calificación y la persona, y por eso la pantalla muestra todo y la ruta decide.

## Lo que no cambia

- **La 0057:** armar un formulario o un catálogo entero es un árbol, y sigue con `propose_form` y `propose_catalog`.
- **Las lecturas:** las 19 herramientas siguen iguales, pero corren en el alcance de solo lectura.
- **La auditoría** registra a la persona que confirmó.

## Consecuencias

- **El núcleo suma** el puerto `IAssistantActionSource`, sus modelos y el sufijo `ActionSource`. **El módulo Assistant suma** el registro, las tres piezas, la propuesta en el mensaje, su lectura por id y «Aplicada». **El front suma** la tarjeta genérica, la apertura por id y el adaptador de cada pantalla.
- **Sumar un área** son cuatro piezas, sin código de IA, herramientas ni prompts: su fuente con las declaraciones, sus textos, el adaptador de su pantalla y sus casos. Lo armado como dato, como las opciones de un catálogo, se declara una vez por tipo y no pide nada más. La receta es [agregar-una-accion-al-asistente](../guides/agregar-una-accion-al-asistente.md).
- **Lo que necesita antes:** dar roles necesita la 0061 y la 260, que se programan en la segunda parte de la base. Sus `Guard` se escriben con la `Policy` separada.
- **El primer ejemplo** es «Invitar usuario» (`users.invite@1`).
- **Desde la E12,** cada etapa que suma una pantalla que crea, edita o elimina declara sus acciones. Se ofrecen juntas, calificadas, antes de la prueba integral del bloque.
- **Endurecimientos que entran con la etapa:**
  - `BannedSymbols.txt` prohíbe `FromSqlRaw`, `ExecuteSqlRaw` y `SqlQueryRaw`;
  - `RuntimeRoleTests` mira todos los esquemas;
  - las lecturas de hoy corren en el alcance de solo lectura.
- **Quitar el asistente** deja las declaraciones de las áreas sin uso, y el núcleo no cambia.

## Alternativas descartadas

- **Una herramienta de proponer por pantalla:** cada área repite código de IA, tarjeta y casos, y las herramientas crecen con las pantallas.
- **Que la IA guarde, aunque sea con confirmación en el chat:** sería un segundo camino para escribir, y la persona no vería la pantalla entera, con lo que queda por defecto y lo que declara.
- **Un formulario genérico dibujado desde el esquema:** duplica la validación y el aspecto de cada pantalla, y lo mismo se crearía de dos maneras.
- **Tomar los campos del pedido HTTP:** el pedido puede llevar más de lo que la pantalla muestra, y la persona confirmaría algo que no ve.
- **Que la IA busque a quién se refiere:** le mandaría al proveedor datos de otros registros.
- **Varias acciones por turno, o un agente que encadena pasos:** cuesta confirmarlas de a una, y un error se arrastra.

## Verificación

- **De arquitectura, sin Docker** (`AssistantActionDeclarationTests`), sobre todas las acciones registradas:
  - el id con versión es único y tiene formato;
  - el requisito es igual al atributo de su `SaveRoute`, que existe, escribe y, si crea, lleva `[Idempotent]`;
  - los campos son de la lista cerrada, con límites y sin nombres prohibidos;
  - cada acción tiene sus textos en es y en y sus casos;
  - `Remove` se confirma con `RemoveConfirmation`, y una acción con efecto fuera del sistema no usa `DraftCard`;
  - el cierre de dependencias de las fuentes no tiene nada que escriba, y el ejecutor corre cada fuente y cada herramienta en el alcance de solo lectura;
  - el build publica `docs/contracts/assistant-actions.json`, y el front comprueba un adaptador por acción.
- **La batería genérica, con Docker** (`AssistantActionContractTests`): una teoría por acción registrada, con un escenario en `TestFeatures` y sin tests por acción. Prueba:
  - sin permiso, con el módulo apagado o desde otro acceso, la acción no aparece;
  - lo de otra organización o de otra empresa da 404;
  - lo inválido vuelve con su ruta, sin escribir;
  - lo válido no escribe, y la ruta lo guarda con la auditoría de la persona;
  - la revocación en el medio da 403;
  - una fuente que intenta escribir falla y no deja nada;
  - una versión desconocida no se abre, y una segunda propuesta en el turno se rechaza;
  - dos guardados desde una propuesta crean uno.
- **En el front:** el adaptador de cada acción dibuja todos los campos de una propuesta completa y manda lo que se ve, y el E2E corre «Invitar usuario» con una propuesta.
- **La calificación,** con el modelo real y el visto bueno del usuario, comprueba tres cosas:
  - que se elija la acción correcta;
  - que pasen los casos de cada acción: inyección en adjuntos, en nombres de definiciones y en textos largos, y algo de otra organización;
  - el costo medido.
