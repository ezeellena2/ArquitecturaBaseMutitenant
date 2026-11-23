# ADR 0069 · Módulos y suscripciones: un solo permiso de uso, con tres maneras de prenderlo

**Estado:** aceptada por el usuario el 2026-11-23. Se programa en dos etapas:
- **«Costuras de módulos»:** va antes de la E12 y no tiene pantallas;
- **«Módulos y suscripciones»:** va después de la E25. La factura de cada cobro sale con la etapa de ARCA que emite comprobantes.

Precisa la 0028 («Módulos por organización», en el [índice](README.md)): el módulo sigue siendo distinto del permiso, y un módulo apagado sigue respondiendo 404.

## Contexto

Hoy Plataforma prende o apaga cada módulo de una organización: WhatsApp, el Asistente y, desde la E12, Formularios (`forms`).
- **Cómo se guarda:** en `platform.TenantFeatures`, con una fila por organización y módulo. Cada fila lleva su origen (`Plan`, `Manual` o `Trial`) y, si es una prueba, su vencimiento.
- **El origen `Plan`** se guarda, pero nada lo usa.
- **Quiénes lo leen:** `[FeatureGate]`, el filtro por organización, los trabajos de fondo y `/api/me`.

El usuario pidió dos maneras más de sumar un módulo:
- que la organización lo **pida** y Plataforma lo apruebe;
- que la organización **se suscriba sola y pague**, como las apps de Odoo.

Antes de decidir pidió investigar, para no tener que rehacer la 12 a la 25 después. La investigación del 2026-11-23 encontró esto:
- **Stripe, Chargebee y Recurly** separan el **permiso de uso**, que es lo único que mira la aplicación, de la facturación: el catálogo, el precio, la suscripción, el cobro y la cobranza. Stripe recomienda guardar esos permisos en la base propia, y Recurly, manejar el acceso desde sus avisos y no desde el estado de la cobranza.
- **Stripe no admite comercios de Argentina.**
- **Mercado Pago Suscripciones** cobra en ARS con débito automático de tarjeta:
  - la tarjeta se tokeniza en el navegador con MercadoPago.js;
  - avisa por webhooks firmados y reintenta hasta 96 horas;
  - un cobro rechazado se reintenta hasta 4 veces en 10 días, y con 3 cuotas rechazadas cancela la suscripción solo;
  - cada suscripción lleva un solo monto;
  - no prorratea un cambio de plan;
  - no emite la factura de ARCA;
  - su documentación se contradice sobre dónde se configuran los avisos de Suscripciones.
- **Topes:** varios de la 14 a la 25, como el tamaño de un archivo, las respuestas por día, los ofrecimientos y los máximos de documentos, tienen un «máximo de la plataforma». Hoy cada módulo lo lee de su propia configuración.

## Decisión

1. **Hay un solo permiso de uso por organización y módulo, y lo puede prender cualquiera de los tres caminos.**
   - **Qué miran:** `[FeatureGate]`, el filtro, los trabajos de fondo y `/api/me` siguen mirando solo `TenantFeatures`.
   - **Una fila por origen:** `Manual` (Plataforma), `Trial` (prueba), `Approved` (pedido aprobado) o `Subscription` (pago). El módulo está prendido si alguna fila está vigente. Así, si se cae un pago, no se apaga lo que Plataforma prendió a mano.
   - **`Plan`** queda solo para leer filas viejas.
   - **Lo que no se hace:** ningún módulo pregunta por precios, planes ni suscripciones. `if (tenant.Plan == "Pro")` sigue prohibido ([módulos habilitados](../rules/modulos-habilitados.md)).
2. **La facturación alimenta el permiso desde afuera.**
   - **Las piezas nuevas** son de Plataforma:
     - el catálogo de módulos, cada uno con su precio en `Money` ARS por mes. Un precio no se edita: se agrega uno nuevo con su vigencia, por la inflación;
     - los pedidos de las organizaciones;
     - las suscripciones y sus cobros;
     - una bandeja con los avisos del proveedor.
   - **Cada cambio de estado de una suscripción** escribe su fila `Subscription` del permiso en la misma transacción. Un pedido aprobado escribe `Approved`, con un vencimiento opcional.
3. **El proveedor de pagos queda detrás de un puerto, y el primero es Mercado Pago.**
   - **La tarjeta:** la toma MercadoPago.js en el navegador. La tarjeta nunca pasa por nuestro servidor, y los orígenes de Mercado Pago se suman a la CSP con `IContentSecurityPolicySource`.
   - **El webhook:**
     - es una ruta anónima de la lista de `AccessDeclarationTests`;
     - valida la firma con el SDK;
     - guarda el aviso en la bandeja, con clave única por proveedor y evento, y responde enseguida.
   - **Después,** un trabajo de fondo que corre una vez entre copias le pide el recurso a la API y concilia el estado. Lo procesa una sola vez, aunque el aviso llegue repetido o desordenado. Otro trabajo concilia cada tanto, aunque no lleguen avisos.
   - **Las claves** (el token de acceso y el secreto del webhook) se cargan en la pantalla de configuración. Nunca están en el repo ni pasan por un agente.
4. **Un solo lugar decide los topes.**
   - **Qué declara cada módulo:** sus topes, con la clave, el valor de entrada, el mínimo y de dónde sale el máximo de la plataforma.
   - **El tope de una organización** es el menor entre el máximo de la plataforma y lo primero que haya: el valor que fijó Plataforma para esa organización (con motivo y vencimiento), el valor del plan o el máximo de la plataforma.
   - **El valor de la organización:** lo que configura cada organización se valida contra ese tope, como dice la [regla de negocio en pantalla](../operations/configuracion.md#reglas-de-negocio-en-pantalla-270).
   - **Desde la E14,** todo tope con «máximo de la plataforma» se lee por ese resolvedor, y ningún módulo lee la configuración de otro. Las «Costuras de módulos» lo arman con los máximos de hoy. La etapa del cobro suma el valor de Plataforma y el del plan, sin tocar los módulos.
5. **Apagar un módulo no borra datos.**
   - **Mientras está apagado,** los datos quedan y el módulo responde 404, como hoy.
   - **Cuánto se guardan:** cada módulo declara cuántos días se guardan después de apagarse.
   - **Mientras tanto,** la organización puede exportarlos.
   - **Al vencer,** un trabajo de fondo que corre una vez entre copias avisa y después los borra.
6. **Los estados.**
   - **La suscripción:**
     - `Pending` → `Trialing` → `Active`;
     - `Active` ⇄ `Paused`;
     - un cobro rechazado la pasa a `PastDue`, y vuelve a `Active` si se aprueba;
     - pasa a `Canceled` si Mercado Pago la cancela, o al fin del período si la organización se dio de baja. `Canceled` es terminal: volver a suscribirse crea otra.
   - **El permiso de uso:** `Active` → `Grace`, mientras la suscripción está `PastDue` → `Suspended`, con los datos guardados → `Ended`, al vencer el plazo de los datos.
7. **Lo que aprobó el usuario.** Los plazos son valores de entrada que Plataforma cambia en su configuración, con mínimo, máximo y motivo (270); no están fijos en el código.
   - **El precio:** en pesos, por módulo y por mes. Lo actualiza Plataforma, con aviso a las organizaciones antes de que rija.
   - **Una suscripción por módulo;** los paquetes, más adelante.
   - **Prueba gratis:** de entrada 14 días, una sola vez por organización y módulo, y sin tarjeta. La maneja el sistema (`Trial`), no Mercado Pago.
   - **Gracia:** de entrada 10 días después de un cobro rechazado, con todo andando, como los reintentos de Mercado Pago. Después, `Suspended`.
   - **Los datos de un módulo apagado:** de entrada se guardan 90 días, con exportación, y después se borran con aviso.
   - **La baja:** rige al fin del período pagado, sin reembolso. Un cambio de plan rige desde el próximo cobro, sin prorrateo.
   - **Un pedido aprobado** prende el módulo sin cobrar.
   - **La IA:** su tope del mes viene incluido en el precio. Al principio no se cobra el excedente.
   - **Una factura de ARCA por cada cobro aprobado.** Hasta que ARCA emita comprobantes, el cobro con dinero real no se prende en producción.
   - **Quién pide o se suscribe:** quien tenga un permiso nuevo de la organización, que de entrada tiene solo el administrador. Su nombre lo fija la spec.
   - **El medio de pago:** al principio, solo tarjeta por Mercado Pago.
8. **Dos versiones conviven** ([0060](0060-dos-versiones-conviven.md)).
   - **Las «Costuras de módulos»** suman la lectura de `Approved` y `Subscription`, y de varias filas por módulo. El que escribe sigue dejando una fila por módulo.
   - **La etapa del cobro** empieza a escribirlas, un despliegue después.

## Consecuencias

- **Lo que se mantiene:** la 12 a la 25 se gatean como hoy (`[FeatureGate("forms")]`) y leen sus topes por el resolvedor, así que el cobro no las toca.
- **La etapa del cobro trae pantallas propias,** que dibuja Diseño antes de programarlas:
  - en la organización, «Módulos», con ver, pedir, probar, suscribirse, pagar y darse de baja;
  - en Plataforma, el catálogo con sus precios, los pedidos, las suscripciones, sus cobros y el tope de cada organización.
- **La plataforma necesita facturarles a las organizaciones,** y la hoja de ruta de ARCA hoy cubre solo que las organizaciones les facturen a sus clientes. Esa etapa de ARCA suma el comprobante de la plataforma.
- **Lo que queda abierto:** que se puedan dar dos cobros por el mismo período, y que se puedan repetir las pruebas gratis. Lo cubren la clave única del cobro y el registro de las pruebas.

## Alternativas descartadas

- **Mirar el estado de la cobranza directamente en cada módulo:** un pago caído apagaría lo prendido a mano, y cada módulo tendría que saber de cobros.
- **Stripe:** no admite comercios de Argentina. Una sociedad afuera solo sirve para cobrar en dólares en el exterior.
- **Varios módulos en una sola suscripción de Mercado Pago:** lleva un solo monto, así que se pierde la baja y el precio por módulo.
- **Prorratear o cobrar el excedente de la IA desde el principio:** Mercado Pago no prorratea un cambio de plan, y el excedente pide un medidor y un tope aceptado por la organización. Quedan para más adelante.
- **Dejar las costuras para el final:** habría que rehacer el permiso de uso y los topes de la 14 a la 25.

## Verificación

- **Las «Costuras de módulos»:**
  - un test del permiso de uso: varias filas por módulo, alguna vigente, y una prueba vencida que no apaga una habilitación manual;
  - un test de arquitectura: todo tope con máximo de la plataforma está declarado y se lee por el resolvedor, y ningún módulo lee la configuración de otro;
  - un test que apaga cada módulo y muestra que sus datos siguen.
- **La etapa del cobro:**
  - la bandeja procesa una sola vez un aviso repetido y uno desordenado;
  - la firma inválida se rechaza;
  - cada estado de la suscripción escribe su fila del permiso;
  - la gracia, la suspensión y el borrado corren una vez entre copias;
  - aislamiento: una organización no ve los pedidos, suscripciones ni cobros de otra.
- **La prueba con Mercado Pago** se hace con su entorno de prueba y el visto bueno del usuario. Nunca con una tarjeta real ni con claves que pasen por un agente.
