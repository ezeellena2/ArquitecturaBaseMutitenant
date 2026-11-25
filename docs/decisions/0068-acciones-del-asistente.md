# ADR 0068 · Las acciones del asistente: la IA prepara y la pantalla de siempre confirma

**Estado:** aceptada por el usuario el 2026-11-23. Ese día aprobó el diseño y eligió dónde programarlo: en la etapa «Acciones del asistente», después de la segunda parte de la base y antes de la E12.
- **Lo que toma:** el diseño técnico de la decisión 268 de la [hoja de ruta de formularios](../plans/2026-10-12-formularios-dinamicos-hoja-de-ruta.md#5-decisiones-de-producto), que es la v2 de la D8 de la [0048](0048-asistente.md). Toma la ADR 0034 de ArquitecturaBase («la IA prepara y la pantalla de siempre confirma») y la adapta a organizaciones, empresas y Plataforma. El «Nuevo usuario» de la 268 es, acá, «Invitar usuario».
- **Lo que deja igual:** la [0057](0057-la-ia-propone-y-una-persona-crea.md).
- **La enmienda del mismo día** (la revisión de alineación de la E12 a la E25): la E23 y la E24 dejaban afuera editar el agente y dar de baja, reactivar, editar o asignar un asistente. Las suma con dos cosas: el tipo `Items` (§4) y el puerto lector entre módulos (§5).
- **Las revisiones:** pasó por dos revisiones adversariales, cada una con un revisor nuevo. La primera fue sobre el diseño, antes de aprobarlo; la segunda, el mismo día, sobre este texto. Los arreglos de las dos ya están acá.

## Contexto

El usuario pidió, con sus palabras, que cuando la IA pueda crear, editar o eliminar:
- no toque lo de otras empresas;
- respete los permisos de cada persona;
- no agregue columnas, no admita SQL ni haga en la base algo que no tiene permitido;
- y que sumar una funcionalidad nueva, como «Proveedores», la deje disponible en el asistente con muy poco trabajo, con un patrón, reglas claras y documentación.

**Lo que había, medido el 2026-11-23 contra el código:**
- **Lo que ya cumple el asistente:**
  - lee con 19 herramientas declaradas (`IAssistantTool`) con el actor firmado;
  - usa los permisos efectivos de quien pregunta y los relee antes y después de cada lectura, con RLS y sin campos de tenant ni de usuario en los argumentos (`AssistantToolCatalogTests`, `AssistantToolsExecutionTests`, `AssistantAuthorizationTests`);
  - la [0053](README.md) prohíbe todo SQL que escriba una persona o un modelo;
  - la app corre con `mt_app`, que no crea tablas ni es dueño de ninguna (`RuntimeRoleTests`);
  - los catálogos de la E12 son filas con columnas fijas.
- **Lo que faltaba:**
  - **escribir:** no había diseño ni etapa;
  - **solo leer:** las herramientas reciben servicios enteros, que también escriben, y ningún test les impide escribir;
  - **las transacciones:** una herramienta corre en el mismo contexto de EF que la transacción que el asistente abre después (`AssistantTurnService.ExecuteToolAsync`), así que algo que dejara pendiente se guardaría sin confirmar;
  - **un error:** una herramienta que devuelve un error corta el turno (`AssistantTurnWorker`);
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
   - **cómo se confirma y en qué pantalla:** `Dialog`, `Page` o `RemoveConfirmation`, y la pantalla, de una lista cerrada del núcleo (`AssistantActionScreen`) que el módulo traduce a su `AssistantRouteKey`;
   - **su efecto fuera del sistema,** de una lista cerrada;
   - **sus campos;**
   - **para editar o eliminar,** qué lectura de la pantalla encuentra el registro;
   - **sus textos** `Action.<id>@<versión>.*`, en es y en.
3. **El requisito es el de la ruta.** `AssistantActionRequirement` copia el atributo de la ruta que guarda:
   - `Consumer`, sin permiso;
   - `Organization`, con su permiso;
   - `Company`, con su permiso y el permiso de organización que también alcanza;
   - `Platform`, con su permiso.

   **Una acción `Company` nombra su campo de empresa,** y el permiso se mira en esa empresa (`HasCompanyPermissionAsync`) al resolver, al proponer y al abrir. Nunca alcanza «en alguna empresa».

   **Lo que la ruta decide adentro de su servicio** (la 0061, el último Dueño, la membresía) lo sigue decidiendo ahí. La fuente usa la misma `Policy`, para no ofrecer lo que la ruta rechazaría.

   **La IA no tiene permisos propios:** cuentan los efectivos de quien pide, con lo incluido de la 260.
4. **Los campos son los de la pantalla, no los del pedido HTTP.**
   - **Los tipos forman una lista cerrada:**
     - `Text`, `LongText`, `Integer`, `Decimal`, `Money`, `Date`, `Boolean`, `Email`, `Phone` y `TaxId`;
     - `Choice` y `Choices`, de una definición (un rol, un formulario, un catálogo);
     - `Record`, un registro (una empresa, una persona, una opción de un catálogo);
     - `Consent`, que nunca se propone y llega sin tildar;
     - `Items`, una lista de filas con sus campos. Cada campo es de esta misma lista, pero nunca otro `Items`: tiene un solo nivel. Cada lista tiene un tope de filas y una clave por fila, por ejemplo los casos o las preguntas frecuentes del asistente.
   - **Lo que no existe:** no hay tipo para claves, conexiones, SQL, expresiones ni URL, y ningún campo se llama como un tenant, un usuario o un secreto.
   - **Un campo puede depender de otro** (`DependsOn`): por ejemplo, los roles de la empresa elegida.
   - **Editar son operaciones:** un campo simple se cambia, y una lista se edita con «sumar» y «quitar», nunca se reemplaza entera. «Agregale Vendedora a Kevin» no le saca los otros roles. Un `Items` suma, quita o cambia una fila por su clave.
   - **Lo que no se declara como acción:**
     - claves y conexiones;
     - métodos de ingreso;
     - la baja de una cuenta;
     - aceptar una invitación o un documento legal;
     - consentimientos;
     - lo que pide una prueba reciente o un segundo factor.
5. **La fuente de cada área solo lee.** Cada área implementa `IAssistantActionSource`, un puerto del núcleo, con el sufijo `ActionSource` ([capas-y-flujo](../rules/capas-y-flujo.md)). Sus declaraciones son estáticas, y hace tres cosas:
   - **lista** las opciones de un `Choice` o un `Choices`;
   - **resuelve** cada texto o referencia a un id permitido: único, con RLS, los permisos y lo que quien pide puede dar. Sin coincidencia vuelve al modelo, y con varias queda «Revisá». Sobre datos públicos o compartidos filtra además por la organización del claim, porque RLS deja ver lo publicado de todas;
   - **valida sin guardar:** arma el `*Request` de la pantalla y corre su validador y las `Policy` de la ruta.

   **Dos barreras aseguran que solo lea:**
   - **Una lista de lo permitido,** comprobada sobre todo el árbol de dependencias:
     - readers, validadores y `Policy`;
     - un `Resolver` solo si su propio árbol también la cumple;
     - `ICurrentUser`, las lecturas de permisos, `TimeProvider` y los catálogos de referencia;
     - lecturas por la caché: llenarla no es escribir un dato, pero nunca se invalida ni se avanza una generación;
     - un **puerto lector del núcleo** (`I<X>Reader`), cuando los datos viven en otro módulo que el de la ruta. El puerto solo devuelve datos, y su implementación en el módulo dueño cumple también esta lista. Un puerto que lee y escribe, como `IConversationAgentSettings`, se separa, y su parte que lee va en un lector propio. Con el módulo dueño quitado, el apagado del núcleo responde «no disponible», y la acción no se ofrece.

     La fuente vive siempre con la ruta que guarda, en su área o su módulo.

     Nada de `Interfaces/Integrations` (salvo el contexto de la petición), `IServiceProvider`, `IUnitOfWork`, repositorios, `Writer`, `Guard` ni servicios.
   - **Dónde corre:** en un alcance de DI propio. Ese alcance entra con el mismo actor revalidado (en el worker), con la organización del claim (en una petición) o, desde la E23, con la organización de un turno de canal, sin persona y sin acciones, nunca con `ITenantScope.Enter`. Abre una transacción de PostgreSQL `READ ONLY` y se descarta al terminar: nada que quede en memoria llega a otra transacción.

   **Las 19 lecturas de hoy** reciben servicios enteros, así que a ellas las cubre la segunda barrera: pasan a correr en ese mismo alcance.

   **La regla queda en un solo lugar.** Lo que decide un `Guard` de la ruta queda en una `Policy` pura que usan el `Guard` y la fuente; por ejemplo, qué roles puede dar quien pide ([0061](0061-nadie-da-lo-que-no-tiene.md)).
6. **El registro filtra y sella.** `AssistantActionCatalog`, en el módulo Assistant, junta las fuentes por DI y comprueba al arrancar que cada id con versión sea único.
   - **Filtra en cada llamada:** por acceso, módulo prendido para la organización, requisito, que la acción esté ofrecida y que el modelo del turno sea uno con el que pasó la calificación. Lee los permisos una sola vez por llamada.
   - **Sella en el turno** la lista que ofreció (`AssistantTurns.OfferedActions`; [0064](0064-reglas-de-toda-la-ia.md) §6). Solo se proponen esas acciones, y cada llamada vuelve a autorizar.
   - **No nombra a nadie:** el núcleo no nombra al asistente ni a ningún módulo.
7. **Tres piezas de IA, fijas para todas las áreas:**
   - **la lista precargada** `assistant_actions`: el id y el nombre corto de las acciones, como dato del turno. Con más de 40, la lista trae las áreas, y el modelo pide las de una;
   - **`read_action`:** las acciones de un área, o los campos de una acción con su tipo y sus límites. En un `Choice` o un `Choices` suma nombres de definiciones, 20 por página y con búsqueda;
   - **`propose_action`:** la acción, el destino y una lista de campo y valor. Pasa por el esquema, la resolución y la validación, y no guarda nada.

   **Lo rechazado vuelve al modelo** con la ruta de cada campo, sin listar registros, para que lo corrija en el mismo turno. Se devuelve como resultado tipado (`AssistantActionToolData`) y no como error, que cortaría el turno. También vuelve así la segunda propuesta del turno (`Assistant.Action.OnePerTurn`).

   **Al proveedor** van lo que dijo la persona, los tipos de acción, los campos y los nombres de las definiciones. Nunca van registros, personas ni opciones de un catálogo: un `Record` lo escribe el modelo como texto y lo resuelve el servidor. En la historia de la conversación, la propuesta deja la acción y lo que escribió el modelo, nunca lo resuelto. Así, la última propuesta queda en la historia (0064 §9) sin datos de otros registros.
8. **La propuesta.** Lo aceptado es **la propuesta del turno**, una por turno, y se guarda en el mensaje del asistente, privada y con RLS, igual que la de la 0057.
   - **Qué guarda:** la acción con su versión; los valores, con los ids resueltos; el destino y la versión que tenía al proponer (`baseVersion`); las dudas.
   - **Los sujetos:** las personas que nombra (como destino o como `Record`) quedan como sujetos del mensaje. Si una de esas cuentas se da de baja, la propuesta queda «ya no disponible».
   - **Una propuesta nueva reemplaza** a la anterior sin aplicar de esa conversación: la vieja queda «Reemplazada» y ya no se abre, como en la 0057.
   - **Lo que muestra la tarjeta** se resuelve en el servidor al dibujarla; nunca es texto del modelo.
9. **Confirma la pantalla de siempre.** La pantalla lee la propuesta por id (`GET /api/{lado}/assistant/proposals/{id}`). Esa lectura:
   - revalida el dueño, el acceso, la organización, que la acción siga ofrecida para esa persona y su versión;
   - vuelve a resolver con la fuente cada id guardado, el destino y los `Record`, con los permisos de ese momento y en su empresa. Uno que ya no resuelve deja la propuesta «ya no disponible».

   La dirección lleva solo el id, nunca valores. Cada pantalla presta un adaptador que dibuja **todos** los valores propuestos; si uno no se puede mostrar, la pantalla rechaza la propuesta. Los selectores muestran lo elegido que no cargaron con el nombre que trae la propuesta del servidor (`selectedOptions` de `Picker`).

   **Cómo se confirma, según la acción:**
   - **`Dialog`:** ese diálogo sobre el Inicio, con «Propuesta del asistente» y «Revisá» en lo dudoso;
   - **`Page`:** esa página, sin guardar;
   - **los dos avisan** antes de guardar si guardar sale del sistema («Se le manda la invitación por correo.»);
   - **`RemoveConfirmation`:** la confirmación de esa pantalla, de a una cosa;
   - **editar:** la pantalla lee lo guardado al abrir, aplica las operaciones y muestra en «Lo que cambia» también lo que se quita. Si el registro cambió desde la propuesta, lo avisa y cuenta los cambios sobre lo de ahora. A la ruta va la versión leída al abrir, y si otro guarda en el medio, vale el 409 de siempre.

   **Lo que nace en borrador** (un formulario, un catálogo) sigue con su propuesta propia y su tarjeta (0057); una acción genérica no crea desde el chat. Sin pantalla donde confirmar, el asistente contesta «todavía no».
10. **Guarda la ruta de siempre,** sin excepción: `[Access]`, el permiso, la validación, el servicio, `IUnitOfWork`, `Result`, RLS, el `Guard` de la 0061 y la auditoría, con la persona como actor.

    **Al crear, la propuesta da la clave:**
    - cada intento de guardar desde una propuesta usa una `Idempotency-Key` que entrega el servidor y que queda con la propuesta;
    - **la clave se repite** mientras ese intento no termine en 4xx, así que dos pestañas usan la misma;
    - **un 422 de clave repetida** no prueba que se guardó, porque la clave guarda también un 4xx: el front vuelve a pedir la clave a la propuesta, y si una ya terminó en 2xx, la respuesta es «ya se guardó», con el enlace a lo creado;
    - **«Aplicada»** la marca el servidor, cuando el almacén de idempotencia confirma que una clave de la propuesta terminó en 2xx para esa persona y esa ruta (el almacén suma esa lectura);
    - **si pasa la validez de la clave** (`Idempotency:ValidityHours`, 24 h de entrada) sin saberlo, la propuesta no se vuelve a abrir y queda «Sin confirmar», con el enlace al listado, como la [0066](0066-lo-que-no-se-sabe-si-salio.md). Ese estado se calcula al leer, con las claves de la propuesta.

    **Al editar o eliminar, la versión** impide aplicar dos veces; «Aplicada» se marca con la versión nueva.
11. **Los tres lados y WhatsApp:**
    - **Plataforma** solo propone sobre lo de Plataforma. Adentro de una organización contesta «todavía no», porque entrar pide `ITenantScope.Enter` y un `SecurityEvent`.
    - **Una persona** solo tiene acciones `Consumer`, y nunca crea empresas.
    - **Por WhatsApp** va un resumen con «Revisar en la app», solo para quien trabaja en la organización y con sus permisos (268). Se suma cuando el canal tenga lugar para estas piezas.
12. **Versiones, límites y calificación.**
    - **Versiones:** cambiar los campos de una acción es una versión nueva, que se lee un despliegue antes de escribirse ([0060](0060-dos-versiones-conviven.md)). El adaptador del front también conserva la versión anterior un despliegue más. Una propuesta de una versión que ya no existe se ve «ya no disponible».
    - **Límites:** una propuesta por turno, dentro de las 5 llamadas; 20 opciones por página, 40 campos y 16 KiB por propuesta; el cupo diario y el tope mensual de siempre.
    - **Calificación:** una acción se ofrece recién cuando sus casos pasan la calificación contra cada modelo con el que se ofrece (268), y su presupuesto es el costo medido ahí. Mientras tanto queda registrada y sin ofrecer.

## Modelo de amenazas

| Amenaza | Qué la frena |
|---|---|
| Instrucciones escondidas en un mensaje, un adjunto o un nombre | Son datos, y el prompt sale de resources. La IA solo propone una acción sellada para esa persona, de a una; la pantalla muestra todo lo que se guarda, con nombres del servidor |
| Valores que la pantalla no muestra | Los campos son los de la pantalla; el adaptador rechaza lo que no dibuja |
| Otra organización | La organización sale del claim, y ningún campo es de tenant ni de usuario. Lo privado se resuelve con RLS, y lo público o compartido, además, por la organización del claim: lo ajeno da 404 |
| Otra empresa | El permiso se mira en la empresa de la acción al resolver, al proponer y al abrir |
| Escalar permisos | La IA no tiene permisos propios. El requisito es el de la ruta, la resolución usa la `Policy` de la 0061, y decide la ruta al guardar |
| Escribir sin confirmar | Fuentes con una lista de dependencias permitidas, y fuentes y lecturas en un alcance aparte y `READ ONLY` |
| Efectos fuera del sistema | Se declaran, la pantalla avisa antes de guardar, y una acción sin efecto declarado no encola nada |
| Acciones delicadas | Claves, conexiones, ingreso, bajas de cuentas, aceptaciones, consentimientos y segundo factor no se declaran |
| Editar de más | Listas con «sumar» y «quitar», «Lo que cambia» con lo que se quita, el aviso si cambió y el 409 |
| SQL | Valores tipados que van a un `*Request` y a EF con parámetros; ningún tipo de campo es texto ejecutable; sin `FromSqlRaw`, `ExecuteSqlRaw` ni `SqlQueryRaw` |
| Cambiar el esquema | No hay camino: las acciones son rutas que existen, los catálogos son filas y `mt_app` no crea ni altera tablas |
| Duplicar | La clave de cada intento la da el servidor, ante un 422 se le vuelve a preguntar a la propuesta si ya se guardó, y pasada la validez de la clave queda «Sin confirmar» |
| Abrir la propuesta de otro | Solo la lee su dueño (0048, D5) |
| Datos de terceros al proveedor | Van tipos, campos y nombres de definiciones; nunca registros ni personas, tampoco en la historia (0064 §10) |
| Cantidades y costos | Los límites de la regla 12, y el costo de cada acción medido al calificarla |

**El build no puede probar cómo resiste el modelo una inyección.** Esa barrera la ponen la calificación y la persona, y por eso la pantalla muestra todo y la ruta decide.

## Lo que no cambia

- **La 0057:** armar un formulario o un catálogo entero es un árbol, y sigue con `propose_form` y `propose_catalog`, que crean en borrador desde su tarjeta.
- **Las lecturas:** las 19 herramientas siguen iguales, pero corren en el alcance de solo lectura.
- **La auditoría** registra a la persona que confirmó.

## Consecuencias

- **El núcleo** suma el puerto `IAssistantActionSource`, sus modelos y el sufijo `ActionSource`.
- **El módulo Assistant** suma el registro, las tres piezas, la propuesta en el mensaje, su lectura por id, las claves de cada intento y «Aplicada».
- **El almacén de idempotencia** suma la lectura del estado de una clave.
- **El front** suma la tarjeta genérica, la apertura por id y el adaptador de cada pantalla.
- **Sumar un área** son cuatro piezas, sin código de IA, herramientas ni prompts: su fuente con las declaraciones, sus textos, el adaptador de su pantalla y su escenario con sus casos. Lo armado como dato, como las opciones de un catálogo, se declara una vez por tipo y no pide nada más. La receta es [agregar-una-accion-al-asistente](../guides/agregar-una-accion-al-asistente.md).
- **Lo que necesita antes:** dar roles necesita la 0061 y la 260, que se programan en la segunda parte de la base. Ahí sus `Guard` nacen con su `Policy` pura (`OrganizationDelegationPolicy` y `CompanyDelegationPolicy`), que las fuentes reusan.
- **El primer ejemplo** es «Invitar usuario» (`users.invite@1`): el correo, los roles de la organización, la empresa y sus roles.
- **Desde la E12,** cada etapa que suma una pantalla que crea, edita o elimina declara sus acciones. Se ofrecen juntas, calificadas, antes de la prueba integral del bloque.
- **Revisión adversarial:** toda acción que da permisos o sale del sistema la pide, como manda [AGENTS.md](../../AGENTS.md).
- **Endurecimientos que entran con la etapa:**
  - `BannedSymbols.txt` prohíbe `FromSqlRaw`, `ExecuteSqlRaw` y `SqlQueryRaw`, también en los tests, que pasan a `ExecuteSqlAsync` interpolado;
  - `RuntimeRoleTests` mira todos los esquemas;
  - las lecturas de hoy corren en el alcance de solo lectura.
- **Quitar el asistente** deja las declaraciones de las áreas sin uso, y el núcleo no cambia.

## Alternativas descartadas

- **Una herramienta de proponer por pantalla:** cada área repite código de IA, tarjeta y casos, y las herramientas crecen con las pantallas.
- **Que la IA guarde, aunque sea con confirmación en el chat:** sería un segundo camino para escribir, y la persona no vería la pantalla entera, con lo que queda por defecto y lo que declara.
- **Que la tarjeta cree un borrador para cualquier acción:** necesitaría armar el pedido HTTP desde el chat. Lo que nace en borrador sigue con la 0057.
- **Un formulario genérico dibujado desde el esquema:** duplica la validación y el aspecto de cada pantalla, y lo mismo se crearía de dos maneras.
- **Tomar los campos del pedido HTTP:** el pedido puede llevar más de lo que la pantalla muestra, y la persona confirmaría algo que no ve.
- **Que la IA busque a quién se refiere:** le mandaría al proveedor datos de otros registros.
- **Varias acciones por turno, o un agente que encadena pasos:** cuesta confirmarlas de a una, y un error se arrastra.

## Verificación

- **De arquitectura, sin Docker** (`AssistantActionDeclarationTests`), sobre todas las acciones registradas:
  - **el id y la forma:**
    - el id con versión es único y tiene formato;
    - `Remove` se confirma con `RemoveConfirmation`;
    - una acción `Company` nombra su campo de empresa;
    - cada pantalla de `AssistantActionScreen` tiene su traducción en el módulo;
  - **la ruta:** el requisito es igual al atributo de su `SaveRoute`, que existe, escribe y, si crea, lleva `[Idempotent]`;
  - **los campos:** son de la lista cerrada, con límites y sin nombres prohibidos; un `Items` tiene un solo nivel, su tope de filas y su clave, y ninguna acción guarda por una ruta de la lista de lo que no se declara;
  - **los textos y los casos:** cada acción tiene sus textos en es y en y sus casos;
  - **sin escritura:** el árbol de dependencias de las fuentes cumple la lista de lo permitido, también a través de la implementación de cada puerto lector, y el ejecutor corre cada fuente y cada herramienta en el alcance de solo lectura;
  - **el contrato:** el build publica `docs/contracts/assistant-actions.json`, y el front comprueba un adaptador por acción.
- **La batería genérica, con Docker** (`AssistantActionContractTests`): una teoría por acción registrada, con un escenario en `TestFeatures` que da también el cuerpo HTTP de su caso válido. No lleva tests por acción. Prueba:
  - **cuándo aparece:** sin permiso, con el módulo apagado o desde otro acceso, la acción no aparece;
  - **el aislamiento:** lo de otra organización o de otra empresa da 404 o «ya no disponible»;
  - **sin escribir:** lo inválido vuelve con su ruta sin cortar el turno, y no queda ninguna fila nueva;
  - **la ruta:** lo válido no escribe, y la ruta lo guarda con la auditoría de la persona;
  - **los efectos:** una acción sin efecto declarado no encola nada en el outbox al guardar;
  - **la revocación:** si se quita el permiso en el medio, la ruta da 403;
  - **la fuente:** una fuente que intenta escribir falla y no deja nada;
  - **versiones y turnos:** una versión desconocida no se abre, y una segunda propuesta en el turno se rechaza;
  - **el guardado único:** dos guardados desde una propuesta crean uno, y el segundo dice «ya se guardó»; un 422 con otro cuerpo no se toma como guardado.
- **En el front:** el adaptador de cada acción dibuja todos los campos de una propuesta completa y manda lo que se ve, y el E2E corre «Invitar usuario» con una propuesta.
- **La calificación,** con el modelo real y el visto bueno del usuario, contra cada modelo con el que se ofrece. Comprueba tres cosas:
  - que se elija la acción correcta;
  - que pasen los casos de cada acción: inyección en adjuntos, en nombres de definiciones y en textos largos, y algo de otra organización o de otra empresa;
  - el costo medido.
