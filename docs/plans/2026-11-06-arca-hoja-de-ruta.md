# ARCA: la primera integración

**Estado:** pendiente · después del bloque de formularios (decisión 76). Las siete etapas están anotadas con lo que ArquitecturaBase decidió y programó hasta el 2026-11-17, sin armar. Cada una se planifica con `/plan-etapa` cuando le toque, después del traspaso 16. Allá nada pasó todavía la prueba integral, la homologación con CUIT reales ni la corrida real del robot: lo que diga ARCA de verdad puede cambiar un detalle.

ARCA es la primera de las integraciones que la decisión 76 pone después del bloque. Esta hoja sale del [traspaso 6](../../../ArquitecturaBase/docs/plans/2026-11-06-traspaso-multitenant-arca-conexion.md) de ArquitecturaBase, del 2026-11-06. El 2026-11-18 sumó lo de sus traspasos 13 y 14, que llegaron con el 15 (`04-arca.md` de [2026-11-14](../../../ArquitecturaBase/docs/plans/2026-11-14-traspaso-multitenant/04-arca.md) y de [2026-11-16](../../../ArquitecturaBase/docs/plans/2026-11-16-traspaso-multitenant/04-arca.md)). Allá las seis etapas tienen back y front programados, y la regla vigente es la ficha [ARCA](../../../ArquitecturaBase/docs/features/arca.md). Como los traspasos anteriores, no es código para copiar (18) ni hace falta para dibujar (108): de allá se toman las pantallas y las reglas de producto.

## 1. Las etapas

Son siete, una más que allá, porque acá la cuenta ARCA de Plataforma es una etapa propia (243):

| Etapa | Qué suma | Depende de |
|---|---|---|
| 1 · La conexión de cada empresa | El certificado de la empresa, la prueba, los datos desde el padrón, el punto de venta y «Completar con ARCA», sin emitir | Nada del bloque |
| 2 · El motor para documentos | Acá no es una etapa de ARCA (abajo) | La E19, la E21, la E22, la E26 y la 191 |
| 3 · La factura C | La emisión segura, el PDF fiscal, las tablas de ARCA como catálogos, el cliente por su CUIT y la nota de crédito | Las etapas 1 y 2 |
| 4 · Las facturas A y B | El IVA por alícuota y la condición del receptor, en homologación | La etapa 3 |
| 5 · La cuenta ARCA de Plataforma | La CUIT, el certificado y el ticket de la plataforma, que comparten todas las organizaciones | La etapa 1 |
| 6 · La delegación guiada | La guía para que la empresa delegue en la CUIT de la plataforma | La etapa 5 |
| 7 · La delegación con robot | Un robot que hace los dos lados de la delegación; si se frena, vuelve a la guía | La etapa 6, con su ADR antes |

Después, como allá: la homologación con CUIT reales y la corrida real del robot.

### Lo que trae cada etapa

1. **La conexión de cada empresa** (traspaso 6 y los arreglos de su revisión):
   - la zona «Facturación · Datos del comprobante», con ingresos brutos e inicio de actividades, que van en el PDF y el padrón no trae (AC4);
   - la configuración de Facturación se guarda con revisión y 409, y se valida antes de abrir la transacción y de consultar a ARCA;
   - el estado sigue al certificado y a los servicios: un certificado vencido o un servicio sumado pasan a «Requiere atención» hasta una prueba en verde, y con el certificado vencido la conexión corta antes de llamar a ARCA (B4);
   - los catálogos de ARCA no piden un ticket que no sirve (H4), cortar el pedido de un ticket no lo pierde (H8) y los guardados confirman la conexión que se leyó, con 409 si cambió (H15);
   - cambiar el ambiente o el modo es un solo pedido que reemplaza la conexión entera (H30). La CUIT no cambia con ARCA conectado (172);
   - «Completar con ARCA» (el padrón de un tercero): 6 CUIT distintas por minuto y por persona y una caché de 10 minutos. Una constancia con errores no decide la condición frente al IVA (B3), y el padrón dice por qué falló: «No existe», «ARCA rechazó el pedido» o «ARCA no respondió» (H13 y H14). Consulta con la conexión de la empresa emisora (244).
2. **El motor para documentos:** acá no es una etapa de ARCA. Lo dan la E19 (bloque, ítems y cálculos), la E21 (acciones y resultado externo), la E22 (biblioteca, con los estándares de un módulo, 220: conviven con las plantillas de la plataforma, con las preguntas atadas con candado, sus casillas, sus valores iniciales y sus reglas activas; se ven solo con el módulo prendido y la plataforma no los edita), las carpetas (E26, 231) y elegir primero por un campo (191). Falta lo que el bloque no trae: catálogos que mantiene un módulo y completar con un padrón.
3. **La factura C** (ADR 0028 y 0031 de allá):
   - **la cola:** una por secuencia (ambiente, CUIT, punto de venta y tipo), con un candado, y nadie toma otro número mientras haya uno pendiente. El número y la huella se guardan antes de pedir el CAE, y ningún pedido a ARCA va dentro de una transacción. Ante un corte, se concilia con `FECompConsultar`. Espera creciente y sin tope (30 s, 1, 2, 5, 10 y 30 minutos, después cada hora). Estados: en cola, enviando, autorizado, rechazado, incierto, para revisar y cancelado;
   - **lo que se emite:** el pedido se fija al encolar, con versión y huella, y el worker solo pone el número; una huella distinta va a revisión y conserva el número (H2). El total es la suma de los renglones (B1). Los importes tienen tope, 9.999.999.999.999,99 (H9). Hay una sola nota de crédito activa por factura (H27 y H34);
   - **lo que va a revisión:** un 10016 por la fecha libera el número, y un 10016 solo no lo suelta (H1). «Emitir con la fecha de hoy» corre también un vencimiento ya pasado, y nunca se corrige la fecha sola (B2). Lo que ARCA tiene y la base no conoce va a revisión factura por factura (H3, 251);
   - **las esperas:** dicen de quién son, ARCA, la conexión u otra versión de la app (H7). Con facturación, la Api no arranca sin almacén. Una factura autorizada sin PDF se informa emitida, y un PDF que no se arma no frena a los demás (H17);
   - **las pantallas:** la zona «Comprobante» arriba del detalle de la respuesta (EM1-A); la respuesta queda anulada cuando ARCA autoriza la nota de crédito (NC1-A); por WhatsApp, el «¿La emito?» llega después del Flow, y de él decide solo el botón que se espera (EM2-A); «Otro: escribir la CUIT»; la fecha fuera de la ventana frena con «Emitir con la fecha de hoy» o «No emitir»;
   - **el resto:** el PDF fiscal de diseño fijo con QR, el estándar «Factura» (FAC) con «Clientes» y su regla, y anular con una nota de crédito total. El cliente escrito por su CUIT entra a «Clientes» recién emitida la factura, sin domicilio si es una persona (H31). Desde el 01/12/2026, la condición del receptor siempre (RG 5616). El total es un importe con dos decimales, tipo `amount` (H29);
   - **las decisiones de acá:** se elige la empresa al agregar «Factura» (244), poner a emitir pide dos permisos (252), el cliente por «Inicial» por WhatsApp (253), una respuesta con factura no se corrige (241) y los nombres fiscales en español (242).
4. **Las facturas A y B:** la alícuota en cada ítem, con el IVA discriminado una vez en el total (IV1-A), y la casilla «El precio incluye IVA» en «Precio unitario» (IV2-A). A o B según el emisor y el receptor; la A exige CUIT. Las leyendas de la RG 5003 y la RG 5614. Todo en homologación: se saca el piloto con una CUIT real. Quedan afuera la A con leyenda, MiPyME, tributos, otras monedas, exportación, notas de débito y la nota de crédito parcial.
5. **La cuenta ARCA de Plataforma** (243): nueva de acá, porque allá la CUIT de la plataforma es configuración de la instalación y el certificado vive en la conexión.
6. **La delegación guiada** (ADR 0027 de allá): «Cómo se conecta» en la zona «Datos» (DG1-A), y la guía en dos listas, «Lo hace la empresa» y «Lo hace la plataforma» (DG2-A). La constancia de inscripción la consulta la plataforma con su CUIT, sin delegar. Detectar es probar: «Sin delegar» o «Sin asignar al alias». Hay un alias para todas las organizaciones. La delegación se revoca en ARCA (250).
7. **La delegación con robot** (ADR 0026 de allá; su ADR de acá, antes de programar):
   - el consentimiento y el ingreso son una zona del alta (RB1-A); el avance va en dos columnas, «Con tu clave» y «Con la clave de {plataforma}» (RB2-A); si se frena, la guía abre en ese paso (RB3-A); el robot crea también el certificado de la plataforma (RB5-A);
   - el lado de la empresa vive solo en memoria, en la copia que recibió la clave, y el de la plataforma es durable, con la clave fiscal de la plataforma como secreto de la instalación. Sin captchas ni segundo factor: en esos casos, la guía. Solo en producción;
   - los arreglos: la clave rechazada de la plataforma no se vuelve a probar sola (H5, 247 y 248); «ARCA no responde» se separa de una pantalla cambiada (H18); avisa cuando se rinde, y la pantalla promete el aviso solo si salió (H19); un latido renueva la reserva del paso (H20); la clave fiscal vive lo menos posible (H21); el driver caído vuelve en la pasada siguiente (H22); el robot da de alta un punto de venta propio (O10);
   - corre dentro de la Api, apagado (249). Los avisos de RB4-A, uno por motivo y paso por día, van a los operadores de Plataforma, al correo de soporte, como los de la clave de IA de la plataforma ([ADR 0049](../decisions/0049-precisiones-whatsapp-y-asistente.md)). El interruptor es de Plataforma, y el uso, de cada organización con el módulo `arca`.

## 2. Lo que se decidió

**Allá, adoptado porque no choca con este modelo** (§2 del traspaso 6):
- la factura primero: la C, después la A y la B, con nota de crédito;
- tres modos de acceso, programando solo el certificado de la empresa;
- un ambiente por vez, homologación o producción; pasar a producción es configurarla de nuevo;
- solo certificados nuevos, generados en la app: no se importa uno existente;
- la conexión activa por zonas, cada una con su acción en la banda (AC2-A);
- una carpeta por familia de servicios (la conexión, el padrón y la facturación), y ninguna usa a otra;
- cada empresa elige qué servicios de ARCA usa: solo esos se autorizan, se prueban y muestran su zona, y la constancia de inscripción es obligatoria;
- ARCA se puede quitar entero, como WhatsApp;
- las tablas de ARCA como catálogos y el autocompletar de la CUIT del cliente van en la etapa 3.

Lo de los traspasos 13 y 14 que está en §1 también se adopta así, salvo lo que cambian las decisiones de abajo.

**Acá, elegidas por el usuario el 2026-11-06, las tres recomendadas:**

150. **La conexión es de cada empresa.** Cada empresa conecta su propia CUIT, que sale de su ficha y no se escribe, con su certificado y su punto de venta. Se configura en la ficha de la empresa, y una organización con dos razones sociales factura con cada una.
151. **ARCA es un módulo propio,** `[FeatureGate("arca")]`, del lado Business, como WhatsApp o el asistente: la plataforma lo prende o lo apaga por organización y se puede vender aparte. No va dentro de un módulo general de integraciones; las que vengan después (Mercado Libre, Mercado Pago, webhooks) se evalúan con su traspaso.
152. **El alta va por pasos plegados (AC1-A):** Datos, Pedido, Certificado, Prueba y Punto de venta. El paso en curso está abierto, los hechos plegados con su resumen en la banda y los siguientes apagados; el que se abre recibe el foco, y se puede salir y volver. Es una excepción más a «sin paneles plegables» del front.

**Acá, del traspaso 15, elegidas por el usuario el 2026-11-18, todas las recomendadas.** Se suman la 241 y la 242 de la [hoja de ruta de formularios](2026-10-12-formularios-dinamicos-hoja-de-ruta.md#5-decisiones-de-producto), que son excepciones a la 93 y a la 42:

243. **Una cuenta ARCA de Plataforma** (pregunta 61; lo que el ADR 0027 de allá deja para acá): una pantalla en Plataforma con la CUIT y el nombre de la plataforma, su certificado y el ticket de cada servicio, que comparten todas las organizaciones que delegan, con la reserva del ticket compartida. Vive en el esquema `platform`, con `[Access(Platform)]` y un permiso de Plataforma nuevo, y cada cambio pide el motivo y deja su `SecurityEvent` (`PlatformActionGuard`). Ninguna organización la ve ni la toca.
244. **La factura sale de la empresa elegida al agregar «Factura»** (N2): al agregar el estándar de la biblioteca se elige la empresa que emite, y queda una copia por empresa, como «Factura · Sur SA» y «Factura · Sur Servicios». `arca.issue` se pide en esa empresa, y «Completar con ARCA» consulta con su conexión. Descartadas: una pregunta «Empresa» en el formulario, y la empresa de quien carga, que es ambigua si la persona está en varias.
245. **Una CUIT tiene una sola conexión vigente con ARCA, por ambiente, en toda la plataforma** (N3): ARCA lleva una sola numeración por punto de venta. La segunda organización recibe un error que no dice dónde está conectada ([ADR 0041](../decisions/0041-no-revelar-cuentas.md)); para pasarla, la primera desconecta. El índice es global, como el de los números de Meta en `identity.WhatsAppChannels`. Así la secuencia de una CUIT vive en una sola organización, y su candado y el control de lo que ARCA tiene y la base no conoce (251) se evalúan dentro de ella.
246. **Desconectar borra solo los secretos** (cambia la fila «Datos» de abajo): se borran la conexión, el certificado y los tickets. Los comprobantes, su pedido, su PDF y la numeración se conservan durante el plazo fiscal, que fija el negocio con su contador. Tampoco los borran quitar el módulo ni un pedido de borrado de datos personales ([datos personales](../rules/datos-personales.md)). La purga al vencer el plazo queda para después.
247. **Si ARCA rechaza la clave fiscal de la plataforma, se frena solo el lado de la plataforma** (pregunta 62): para todas las organizaciones, y se avisa a los operadores al correo de soporte. Cada empresa ve que su aceptación quedó esperando y puede seguir con la guía; su lado termina lo suyo. Descartadas: pausar el robot entero, y que cada organización siga intentando, que puede bloquear más la clave.
248. **La clave de la plataforma la vuelve a probar un operador de Plataforma** (pregunta 63): con un permiso de Plataforma, `[Access(Platform)]` y el motivo, desde la cuenta de la 243. Nunca `arca.manage`, que acá es de cada empresa.
249. **El robot corre dentro de la Api, apagado por defecto** (pregunta 64): con Chromium solo en la imagen donde se prende, así la clave fiscal no viaja por la red interna. Acá se suma un tope de corridas a la vez por copia, y se mide memoria y CPU (H22) antes de prenderlo para todas.
250. **La delegación se revoca en ARCA, no desde la app** (pregunta 65): la próxima prueba deja la conexión en «Requiere atención». «Desconectar» una empresa delegada borra su conexión, nunca el certificado de la plataforma, que es de todas, y le dice que revoque en ARCA.
251. **Lo que ARCA tiene y la base no conoce va a revisión, factura por factura** (pregunta 66; H3): la decide una persona de esa empresa con `arca.issue`. La guía y el robot le dan a la app un punto de venta propio (O10). Cubre una restauración y otro sistema en el mismo punto de venta; la excepción es una base que no conoce ningún número de esa secuencia.
252. **Poner a emitir pide dos permisos** (pregunta 67; H6): `formrules.manage`, de la organización, y `arca.issue`, «Emitir comprobantes en ARCA», de la empresa que emite. Vale al agregar «Factura» y al guardar cualquier regla que deje una acción «Emitir»; sin los dos, un 403 propio, que el editor no confunde con perder el permiso de reglas. Cargar la «Factura» no pide `arca.issue`, y sacar la regla que emite tampoco.
253. **Por WhatsApp, el cliente de la factura se elige primero por su «Inicial»** (pregunta 68; B5): un campo nuevo de «Clientes», con un tope de 200 clientes por inicial, en la versión 2 del estándar. Depende de elegir primero por un campo (191): la factura C va después, o arranca con más de 200 clientes solo desde la app. Descartada: escribir la CUIT por WhatsApp, que pide algo nuevo en los Flows.

## 3. La adaptación

| Tema | Acá |
|---|---|
| Datos | `ITenantOwned` en el esquema `tenant`, con su helper RLS y su test de aislamiento por ruta. Las tablas de allá cuelgan de la empresa: el índice único de la conexión es por empresa, más el global de la 245. Desconectar borra de verdad la conexión, los certificados y los tickets, porque hay secretos: sin `ISoftDeletable` y en cascada desde la conexión. Los comprobantes, su pedido, su PDF y las secuencias no cuelgan de la conexión y se conservan (246) |
| CUIT | La de la ficha de la empresa (`TaxId`, [identificacion-fiscal](../rules/identificacion-fiscal.md)), con su validador, que además rechaza once ceros, como la pieza común de allá. Una empresa sin CUIT no se puede conectar, y la pantalla dice por qué. Con ARCA conectado, la CUIT no cambia: primero se desconecta (172). El núcleo lo pregunta por un puerto neutral que el módulo implementa, como `IAccountDeletionParticipant` |
| Secuencias | Por CUIT emisora, punto de venta y tipo: acá, de la empresa. Con la 245, cada una vive en una sola organización, y la cola, la huella, el pedido fijado y las revisiones quedan aisladas por RLS con la empresa |
| Secretos | La clave privada y el ticket de acceso, cifrados con un anillo propio del módulo; el AAD suma la organización y la empresa a la clave, el propósito, la conexión y el certificado. La clave privada nunca sale del servidor ni llega a un lector. El ticket va en PostgreSQL, no en Redis: es una credencial y no un dato de la [caché](../rules/cache.md), y si se perdiera ARCA no daría otro por unos minutos (ADR 0023 de ArquitecturaBase). Las columnas cifradas llevan `[NotAudited]` ([auditoria](../rules/auditoria.md)), así desconectar no deja copia. Lo de la plataforma (243) lleva su propio propósito |
| Clientes SOAP | Propios: `HttpClient`, `System.Xml.Linq` y la firma con `SignedCms`, sin bibliotecas ni intermediarios que reciban la clave. Solo HTTPS, sin seguir redirecciones, con tope de tamaño de respuesta y sin los loggers del `HttpClient`. Las direcciones de cada ambiente son configuración |
| Logs | Con `[LoggerMessage]`, sin la clave, el token, la firma, el pedido ni el certificado; la CUIT va enmascarada, como un teléfono, con `PersonalDataMask` ([logs](../rules/logs.md)). Los de la cola llevan el id del comprobante, su clave de pedido y el código de la falla; el certificado y la configuración de Facturación son auditables, y desconectar deja quién lo hizo (H26). La métrica de la cola (pendientes, en revisión y la antigüedad del más viejo) va sin la empresa ni la organización: el detalle está en la pantalla de cada empresa |
| Permisos | `arca.read`, `arca.manage` y `arca.issue`, de alcance de empresa (`[HasCompanyPermission]`), en el catálogo, en `Permissions.resx` y `.en.resx`, y en el seed del Dueño y del Administrador de la empresa. `arca.issue` resuelve una revisión de la emisión y anula una respuesta con factura (252). La cuenta de Plataforma (243) y el rearme de su clave (248), con un permiso de Plataforma nuevo |
| Módulo | Apagado, las rutas responden 404 y `/api/me` no lo lista; el núcleo no nombra al módulo ([modulos](../rules/modulos.md)). Formularios se conecta por puertos neutrales: emitir, los catálogos que mantiene un módulo, completar con un padrón, la biblioteca y las acciones |
| Límites | Los perfiles cerrados de [cuotas](../rules/cuotas.md): lectura, escritura y lo que llama a ARCA, cada uno con el suyo. El cupo de «Completar con ARCA» es durable y compartido entre copias, como las cuotas del Asistente; allá era de cada copia. La caché del padrón lleva la organización en la clave ([multitenancy](../rules/multitenancy.md)) |
| Archivos | El PDF fiscal se guarda una vez, en una categoría que la búsqueda de huérfanos del almacén nunca borra (la E10 la hace por categoría y fecha). La guía de restauración suma ARCA: levantar con la cola apagada, comparar el último autorizado de cada punto de venta y recuperar lo que falte; «Quitar ARCA» pide copiar los PDF antes |
| Pantalla | Una pestaña «ARCA» en la ficha de la empresa, con los botones de la decisión 43, y «Desconectar» en el ⋮, en rojo (209). En Plataforma, la cuenta ARCA de la plataforma (243) |
| Delegación (etapas 6 y 7) | Con la cuenta de la 243: el bloqueo «ya posee un TA válido» afectaría a todas a la vez, y por eso la reserva del ticket es compartida. «Desconectar la clave de la plataforma», que allá borra el certificado al desconectar, no vale acá: es de todas (250). El robot pide su ADR antes de programarse, con la 247 a la 249 |

## 4. No repetir

Lo que allá se encontró al programar la conexión (§4 del traspaso 6):
- **Ningún pedido a ARCA dentro de una transacción.** El ticket se reserva y se guarda en dos transacciones cortas, con el pedido al WSAA en el medio.
- **Sin reintentos automáticos** en los clientes de ARCA, que no llevan la resiliencia común que `ServiceDefaults` le pone a todo `HttpClient`: un pedido repetido a ciegas puede emitir dos veces o pedir un segundo ticket que ARCA rechaza y bloquea por minutos.
- **Un puerto con una implementación por familia** se registra como uno por familia, no como un servicio único.
- **El resultado de una prueba lenta no pisa una renovación:** se guarda solo si el certificado probado sigue siendo el activo.
- **Los detalles confirmados con los WSDL de homologación:** el error 602 de la facturación es una lista vacía, no una falla; el padrón va sin calificar y con el `SOAPAction` vacío; el pedido de ticket lleva el huso explícito y un `uniqueId` al azar; `cms.bad` no es un reloj desfasado.
- **El estado de cada servicio vive en la visita:** el servidor guarda el resultado general y su hora, y «Sin probar» aparece solo si nunca hubo una prueba.
- **«Activar la conexión» aparece desde «Sin verificar»,** no desde que se pide el certificado.
- **Los tests de la conexión usan una familia genérica,** para que quitar una familia no rompa tests ajenos.

De la revisión de allá (traspaso 14), lo que vale para cualquier etapa:
- **Un 10016 solo no suelta el número:** puede ser el nuestro autorizado tarde, y soltarlo factura dos veces (H1).
- **Un 403 de un permiso que no es el de la pantalla lleva su propio código,** o el front lo toma por «perdiste el permiso» y cierra sin lo editado (H6).
- **Los límites y los cupos de allá son de cada copia;** acá, compartidos entre copias desde el principio.

## 5. Tableros a dibujar

En la ficha de la empresa, con sus teléfonos, tomando de ejemplo AC1-A a AC4 del lienzo de ArquitecturaBase (filas «ARCA · conexión»):
- **El alta por pasos** (152), con los cinco pasos y la elección de los servicios.
- **La conexión activa:** Empresa, Servicios (con «Lo usa esta empresa» y «Probar»), Certificado (con la renovación adentro), Punto de venta y «Facturación · Datos del comprobante» (AC4). El aviso a treinta días del vencimiento y el rojo con el certificado vencido.
- **Los estados:** sin conexión, solo lectura, cargando, no se pudo leer, probando, «Requiere atención» (un certificado vencido o un servicio sumado), un servicio sin autorizar, un certificado que no corresponde, sin puntos de venta, otra persona guardó (con «Revisar») y desconectar, en el ⋮ y con su confirmación.
- **La empresa sin CUIT** en su ficha, nuevo de acá: la pestaña dice que falta y lleva a cargarla.
- **La CUIT conectada en otra organización** (245), nuevo de acá: el error, sin decir dónde.

Se dibujan cuando le toque, después del bloque. Con ellos se rehacen `Empresa`, con la pestaña «ARCA» y la CUIT que no se edita mientras está conectado (172); ARCA entre los módulos de `Organizacion`, y sus permisos en `Rol`. Las demás etapas toman de allá EM1-A, EM2-A y NC1-A (la factura C), IV1-A e IV2-A (la A y la B), DG1-A y DG2-A (la delegación guiada) y RB1-A a RB5-A (el robot). Suman lo nuevo de acá: la cuenta ARCA de Plataforma (243), la empresa que emite al agregar «Factura» (244) y el aviso de la plataforma frenada en el avance del robot (247).

## 6. Las integraciones que siguen

Solo anotadas, sin etapas. Se evalúan con su traspaso, como dice la 151.
- **Tickets en Jira desde formularios:** decidido en ArquitecturaBase el 12/11 y sin programar allá (JT1 a JT7). El formulario es el ticket, con cuatro disparadores y comentarios de ida y vuelta, en un módulo propio, `Modules/Jira`, como ARCA. Para cuando llegue:
  - la conexión es de cada organización, con `[FeatureGate("jira")]`;
  - el aviso que manda Jira es global y resuelve la organización por la conexión, como WhatsApp por el número;
  - el límite de allá, que «fuera de las 24 horas falla», es menor acá, porque cada organización tiene sus plantillas;
  - «Se carga sola» crea una respuesta a nombre de quien cargó la primera, y cargar a nombre de otra persona está después del bloque;
  - una falla con origen «Sistema» puede ser de la plataforma o de una organización.
- **Lo que allá quedó sin decidir:** conectar bases externas (allá se recomendó no construir el conector), copiar formularios, los tableros que cruzan formularios y la retención de las respuestas. La conexión a la base de un cliente y los tableros que cruzan ya están en «Después del bloque» de la hoja de ruta de formularios; la retención de las respuestas está en el [backlog](../backlog.md#traspaso-15-whatsapp-y-arca-para-programar), para decidir antes de producción.

## 7. Pendiente

- La prueba integral, la homologación real con una CUIT del usuario y la corrida real del robot, con la medición de memoria y CPU. Allá también faltan.
- El traspaso 16, antes de armar cualquier etapa.
- El plan de la etapa 1, con `/plan-etapa`, cuando le toque.
- Lo que pide código fuera de ARCA y ya se puede hacer, como que la CUIT rechace once ceros, está en el [backlog](../backlog.md#traspaso-15-whatsapp-y-arca-para-programar).
