# ADR 0047 · El ingreso con código acepta una invitación pendiente

Estado: aceptada el 2026-10-07 por el dueño del producto. Completa la [0043](0043-las-pantallas-dicen-si-hay-cuenta.md).

## Contexto

Una organización creada desde Plataforma invita a su Dueño por correo, sin crearle una cuenta: la cuenta nace recién cuando acepta la invitación desde el enlace. Pasa lo mismo con quien invita Usuarios. El dueño del producto creó TheFlock, quiso ingresar como empresa con el correo invitado y la pantalla le dijo «No hay una cuenta con ese correo». Para quien está invitado, tener una invitación ya es tener una cuenta.

## Decisión

- **El ingreso, como persona o como empresa,** con un correo que tiene una invitación pendiente y vigente se comporta como el de una cuenta: manda el código.
- **El código prueba que el correo es suyo,** igual que el enlace. Al ponerlo:
  - se crea la cuenta, si no existía;
  - se acepta la invitación;
  - la persona entra.
- **Si hay términos por aceptar,** se aceptan antes de entrar, con la pantalla que ya existe para eso.
- **Por la puerta de empresa,** entra a la organización de la invitación. Si tiene más de una, rige la regla de siempre: la última usada o la más antigua.
- **Por la puerta de persona,** entra a su espacio personal, y la invitación queda aceptada igual.
- **Una invitación vencida o revocada** no cuenta: la pantalla dice lo de un correo sin cuenta.
- **Lo que no cambia:**
  - el enlace de la invitación sigue andando como antes;
  - una invitación de operador sigue el camino de Plataforma ([0042](0042-operadores-de-plataforma.md)), no este.

## Consecuencias

- **Back:** el pedido y la verificación del código reconocen la invitación vigente y la aceptan en la misma transacción. Lleva tests de integración: con invitación vigente, vencida y revocada, y por las dos puertas.
- **E2E real:** la Plataforma crea una organización y el Dueño invitado entra por «Ingresá como empresa» con el código, sin tocar el enlace.
- **Plan:** A9 de los [arreglos antes de la Etapa 7](../plans/2026-10-06-arreglos-antes-de-la-7.md).
