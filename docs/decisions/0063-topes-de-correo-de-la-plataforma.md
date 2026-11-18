# ADR 0063 · Topes de correo de la plataforma

**Estado:** aceptada por el usuario el 2026-11-18 (pregunta 16 del traspaso 15). Toma la N7 de ArquitecturaBase (traspaso 14, `02-nucleo.md` §3) y suma un tope por organización. Todavía no está programada: la tarea está en el [backlog](../backlog.md#traspaso-15-el-núcleo-para-programar). Nada de ArquitecturaBase pasó todavía su prueba integral.

## Contexto

El cupo de correo es de la cuenta que manda, no de cada organización. El Gmail de producción da unos 500 correos por día, y **los comparte con ArquitecturaBase** ([configuración](../operations/configuracion.md), [runbook](../operations/runbook.md)). Si el Dueño de una organización manda 400 invitaciones en una mañana, ese día no les llegan los códigos de ingreso a los demás, ni en las otras organizaciones ni en ArquitecturaBase.

Hoy hay topes por destino y por dirección ([0043](0043-las-pantallas-dicen-si-hay-cuenta.md); `LoginCode:MaxRequestsPerWindow` y los perfiles `LoginCode` y `LoginVerify` de [cuotas](../rules/cuotas.md)), por ventanas cortas y no por día. No hay ninguno global ni ninguno de invitaciones: invitar cuenta solo en las cuotas generales de pedidos.

## Decisión

1. **Un tope global de la plataforma,** en 24 horas móviles, por debajo del cupo de la cuenta. Cuenta todo correo con un código o una invitación. Los códigos frenan antes, en el tope menos una reserva para invitaciones, así un abuso de los códigos no deja sin invitar. Al 80 % y al llegar queda un aviso en el log, para la alerta de operaciones.
2. **Mientras la cuenta sea compartida con ArquitecturaBase,** el tope global del multitenant es su parte del cupo: la suma de los topes de los dos queda por debajo del cupo, con lugar para los otros correos (avisos y resúmenes). Las dos apps no comparten base, así que esa es la forma de contar lo que manda la otra. Antes de producción conviene separar las cuentas, o pasar a un servicio transaccional ([runbook](../operations/runbook.md)).
3. **Uno diario de invitaciones por persona que invita,** en 24 horas móviles, sume las organizaciones que sume. Incluye a los operadores que invitan desde Plataforma.
4. **Uno diario de invitaciones por organización** (la adaptación al multitenant): sin él, una organización deja sin códigos a las demás.
5. **Siguen los de hoy** (por destino, `LoginCode:MaxRequestsPerWindow`, y por dirección, con los perfiles de cuotas), y se suman dos diarios, como en ArquitecturaBase: uno de códigos por dirección y otro por cuenta para los códigos de Mi cuenta. Una dirección IPv6 cuenta por su /64.
6. **Durables y compartidos entre copias:** los contadores van en una tabla global, como `identity.WhatsAppCodeQuotas`, nunca en un proceso. Son cuotas de negocio, como las del Asistente, y no suman un perfil a los doce de [cuotas](../rules/cuotas.md).
7. **Los valores** van en la configuración de la instalación (`Email:*`), validados al arrancar, porque dependen de la cuenta de correo. El rechazo es 429 con `retryAfter`, y la pantalla muestra una espera larga en horas y minutos.

Los valores de arranque son los de ArquitecturaBase: 400 de tope global con una reserva de 50 para invitaciones, 50 códigos por dirección, 10 por cuenta desde Mi cuenta y 100 invitaciones por persona. Por organización, 100 hasta que se mida otra cosa. Con la cuenta compartida, el tope global baja a la parte que le toque al multitenant.

## Consecuencias

- Un costo aceptado, como en ArquitecturaBase: un abuso desde muchas direcciones puede agotar el tope global y dejar a todos sin códigos ese día.
- Los avisos que no son códigos ni invitaciones no cuentan en el tope, pero sí gastan el cupo: el tope se fija con ese margen.

## Alternativas descartadas

- **Solo los topes de ArquitecturaBase,** sin el de cada organización: una sola organización agota el día de todas.
- **Solo uno por organización, sin global:** la suma de las organizaciones pasa el cupo de la cuenta.
