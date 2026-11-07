# ARCA: la primera integración

**Estado:** pendiente · después del bloque de formularios (decisión 76); cada etapa se planifica con `/plan-etapa` cuando le toque.

ARCA es la primera de las integraciones que la decisión 76 pone después del bloque. Esta hoja sale del [traspaso 6](../../../ArquitecturaBase/docs/plans/2026-11-06-traspaso-multitenant-arca-conexion.md) de ArquitecturaBase, del 2026-11-06. Allá la etapa 1, la conexión, se programó del 3 al 5/11, y su regla vigente es la ficha [ARCA](../../../ArquitecturaBase/docs/features/arca.md). Como los traspasos anteriores, no es código para copiar (18) ni hace falta para dibujar (108): de allá se toman las pantallas y las reglas de producto.

## 1. Las etapas

Son las de la [hoja de ruta de ARCA](../../../ArquitecturaBase/docs/plans/2026-10-31-arca-formularios-estandar.md) de allá, en el mismo orden:

| Etapa | Qué suma | Depende de |
|---|---|---|
| 1 · La conexión | El certificado de la empresa, la prueba de la conexión, los datos de la empresa desde el padrón y el punto de venta, sin emitir. El modelo admite los tres modos de acceso y la pantalla muestra solo el primero | Nada del bloque |
| 2 · El motor para documentos | Renglones, cálculos, condiciones, el resultado externo en la respuesta, carpetas y la biblioteca de estándares | La E19 (146) y las carpetas |
| 3 · La factura C | La emisión segura, el PDF fiscal, las tablas de ARCA como catálogos que se actualizan solos, el autocompletar de la CUIT del cliente y la nota de crédito | La etapa 2 |
| 4 · Las facturas A y B | El IVA por alícuota, la condición frente al IVA obligatoria y un piloto con una CUIT real | La etapa 3 |
| 5 · La delegación guiada | El certificado de la CUIT de la plataforma y la guía para que el cliente delegue | La etapa 1 |
| 6 · La delegación con robot | Un worker que delega del lado del cliente y acepta del lado de la plataforma; si falla, vuelve a la guía | La etapa 5 |

## 2. Lo que se decidió

**Allá, adoptado porque no choca con este modelo** (§2 del traspaso):
- la factura primero: la C, después la A y la B, con nota de crédito;
- tres modos de acceso, programando solo el certificado de la empresa;
- un ambiente por vez, homologación o producción; pasar a producción es configurarla de nuevo;
- solo certificados nuevos, generados en la app: no se importa uno existente;
- la conexión activa por zonas, cada una con su acción en la banda (AC2-A);
- una carpeta por familia de servicios (la conexión, el padrón y la facturación), y ninguna usa a otra;
- cada empresa elige qué servicios de ARCA usa: solo esos se autorizan, se prueban y muestran su zona, y la constancia de inscripción es obligatoria;
- ARCA se puede quitar entero, como WhatsApp;
- las tablas de ARCA como catálogos y el autocompletar de la CUIT del cliente van en la etapa 3.

**Acá, elegidas por el usuario el 2026-11-06, las tres recomendadas:**

150. **La conexión es de cada empresa.** Cada empresa conecta su propia CUIT, que sale de su ficha y no se escribe, con su certificado y su punto de venta. Se configura en la ficha de la empresa, y una organización con dos razones sociales factura con cada una.
151. **ARCA es un módulo propio,** `[FeatureGate("arca")]`, del lado Business, como WhatsApp o el asistente: la plataforma lo prende o lo apaga por organización y se puede vender aparte. No va dentro de un módulo general de integraciones; las que vengan después (Mercado Libre, Mercado Pago, webhooks) se evalúan con su traspaso.
152. **El alta va por pasos plegados (AC1-A):** Datos, Pedido, Certificado, Prueba y Punto de venta. El paso en curso está abierto, los hechos plegados con su resumen en la banda y los siguientes apagados; el que se abre recibe el foco, y se puede salir y volver. Es una excepción más a «sin paneles plegables» del front.

## 3. La adaptación

| Tema | Acá |
|---|---|
| Datos | `ITenantOwned` en el esquema `tenant`, con su helper RLS y su test de aislamiento por ruta. Las cinco tablas de allá cuelgan de la empresa: el índice único de la conexión es por empresa. Desconectar borra de verdad la conexión, los certificados, los tickets y los datos de cada familia, porque hay secretos: sin `ISoftDeletable` y en cascada desde la conexión |
| CUIT | La de la ficha de la empresa (`TaxId`, [identificacion-fiscal](../rules/identificacion-fiscal.md)), con su validador. Una empresa sin CUIT no se puede conectar, y la pantalla dice por qué. Con ARCA conectado, la CUIT no cambia: primero se desconecta (172). El núcleo lo pregunta por un puerto neutral que el módulo implementa, como `IAccountDeletionParticipant` |
| Secretos | La clave privada y el ticket de acceso, cifrados con un anillo propio del módulo; el AAD suma la organización y la empresa a la clave, el propósito, la conexión y el certificado. La clave privada nunca sale del servidor ni llega a un lector. El ticket va en PostgreSQL, no en Redis: es una credencial y no un dato de la [caché](../rules/cache.md), y si se perdiera ARCA no daría otro por unos minutos (ADR 0023 de ArquitecturaBase). Las columnas cifradas llevan `[NotAudited]` ([auditoria](../rules/auditoria.md)), así desconectar no deja copia |
| Clientes SOAP | Propios: `HttpClient`, `System.Xml.Linq` y la firma con `SignedCms`, sin bibliotecas ni intermediarios que reciban la clave. Solo HTTPS, sin seguir redirecciones, con tope de tamaño de respuesta y sin los loggers del `HttpClient`. Las direcciones de cada ambiente son configuración |
| Logs | Con `[LoggerMessage]`, sin la clave, el token, la firma, el pedido ni el certificado; la CUIT va enmascarada, como un teléfono, con `PersonalDataMask` ([logs](../rules/logs.md)) |
| Permisos | `arca.read` y `arca.manage`, de alcance de empresa (`[HasCompanyPermission]`), en el catálogo, en `Permissions.resx` y `.en.resx`, y en el seed del Dueño y del Administrador de la empresa |
| Módulo | Apagado, las rutas responden 404 y `/api/me` no lo lista; el núcleo no nombra al módulo ([modulos](../rules/modulos.md)) |
| Límites | Los perfiles cerrados de [cuotas](../rules/cuotas.md): lectura, escritura y lo que llama a ARCA, cada uno con el suyo |
| Pantalla | Una pestaña «ARCA» en la ficha de la empresa, con los botones de la decisión 43 |
| Delegación (etapas 5 y 6) | El certificado y los tickets de la CUIT de la plataforma son globales, no de una organización: el bloqueo «ya posee un TA válido» afectaría a todas a la vez, y la reserva compartida del ticket importa más. Se decide al planificar la etapa 5. El robot de la etapa 6, con la clave fiscal que maneja solo en memoria, pide su ADR antes de programarse |

## 4. No repetir

Lo que allá se encontró al programar la conexión (§4 del traspaso):
- **Ningún pedido a ARCA dentro de una transacción.** El ticket se reserva y se guarda en dos transacciones cortas, con el pedido al WSAA en el medio.
- **Sin reintentos automáticos** en los clientes de ARCA, que no llevan la resiliencia común que `ServiceDefaults` le pone a todo `HttpClient`: un pedido repetido a ciegas puede emitir dos veces o pedir un segundo ticket que ARCA rechaza y bloquea por minutos.
- **Un puerto con una implementación por familia** se registra como uno por familia, no como un servicio único.
- **El resultado de una prueba lenta no pisa una renovación:** se guarda solo si el certificado probado sigue siendo el activo.
- **Los detalles confirmados con los WSDL de homologación:** el error 602 de la facturación es una lista vacía, no una falla; el padrón va sin calificar y con el `SOAPAction` vacío; el pedido de ticket lleva el huso explícito y un `uniqueId` al azar; `cms.bad` no es un reloj desfasado.
- **El estado de cada servicio vive en la visita:** el servidor guarda el resultado general y su hora, y «Sin probar» aparece solo si nunca hubo una prueba.
- **«Activar la conexión» aparece desde «Sin verificar»,** no desde que se pide el certificado.
- **Los tests de la conexión usan una familia genérica,** para que quitar una familia no rompa tests ajenos.

## 5. Tableros a dibujar

En la ficha de la empresa, con sus teléfonos, tomando de ejemplo AC1-A, AC2-A y AC3 del lienzo de ArquitecturaBase (filas «ARCA · conexión»):
- **El alta por pasos** (152), con los cinco pasos y la elección de los servicios.
- **La conexión activa:** Empresa, Servicios (con «Lo usa esta empresa» y «Probar»), Certificado (con la renovación adentro) y Punto de venta, con el aviso a treinta días del vencimiento.
- **Los estados:** sin conexión, solo lectura, cargando, no se pudo leer, probando, un servicio sin autorizar, un certificado que no corresponde, sin puntos de venta, otra persona guardó (con «Revisar») y desconectar con su confirmación.
- **La empresa sin CUIT** en su ficha, nuevo de acá: la pestaña dice que falta y lleva a cargarla.

Se dibujan cuando le toque, después del bloque. Con ellos se rehacen `Empresa`, con la pestaña «ARCA» y la CUIT que no se edita mientras está conectado (172); ARCA entre los módulos de `Organizacion`, y sus permisos en `Rol`.

## 6. Pendiente

- La homologación real con una CUIT del usuario, que allá también falta.
- Las etapas 2 a 6, sin diseño de detalle.
- El plan de la etapa 1, con `/plan-etapa`, cuando le toque.
