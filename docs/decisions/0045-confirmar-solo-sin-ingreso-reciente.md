# ADR 0045 · Agregar un correo o Google pide confirmación solo si el ingreso no es reciente

Estado: aceptada el 2026-10-06 por el dueño del producto. Modifica la [0033](0033-metodos-de-ingreso.md) para agregar un correo y vincular Google.

## Contexto

La 0033 pedía un `ReauthTicket` (un código a un método que ya tiene la cuenta, válido cinco minutos) antes de sumar un correo, vincular Google, cambiar el principal o quitar un método. Al probar, el dueño del producto agregó un correo recién ingresado y le llegaron dos códigos: uno a su correo de siempre, para confirmar que era él, y otro al correo nuevo. Lo dijo así: «no creo necesario tener que enviar el código al momento de querer agregar un correo».

## Decisión

- **Agregar un correo y vincular Google** no piden confirmación si la sesión empezó hace **menos de 15 minutos**. El correo nuevo sigue necesitando su propio código, y Google su ingreso con OAuth.
- **Pasados los 15 minutos,** se confirma primero, como hasta ahora.
- **Quitar un método y cambiar el principal** piden confirmación siempre, como dice la 0033.
- **La hora de inicio** de la sesión es la del ingreso con código o con Google, la que ya guarda la sesión (`BrowserSessionKeys.StartedAtUtc`). Los 15 minutos son configuración, no un número en el código.
- **El aviso a los otros métodos** («Se agregó un correo a tu cuenta») sigue saliendo como hasta ahora.

## Alternativas descartadas

- **Nunca confirmar para agregar:** cualquiera con la sesión abierta podría sumar su correo y quedarse con la cuenta.
- **Confirmar siempre** (la 0033 tal cual): son dos códigos para algo que se hace apenas se ingresa.

## Consecuencias

- **Back:** sumar un correo y vincular Google aceptan el pedido sin ticket si la sesión es reciente; si no, piden el ticket como hoy. Lleva tests de los dos lados de los 15 minutos, con `TimeProvider`.
- **Front:** Mi cuenta pide el correo nuevo directo cuando la sesión es reciente.
- **Plan:** A5 de los [arreglos antes de la Etapa 7](../plans/2026-10-06-arreglos-antes-de-la-7.md).
