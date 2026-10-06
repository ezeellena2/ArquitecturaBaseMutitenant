# ADR 0045 · Mi cuenta no pide un código de confirmación

Estado: aceptada el 2026-10-06 por el dueño del producto. Modifica la [0033](0033-metodos-de-ingreso.md): saca el `ReauthTicket`.

## Contexto

La 0033 pedía un `ReauthTicket` antes de varias acciones de Mi cuenta: sumar un correo, vincular Google, cambiar el principal, quitar un método y dar de baja la cuenta. El ticket es un código a un método que ya tiene la cuenta, de un solo uso, para una sola acción y válido cinco minutos. Al probar, el dueño del producto se encontró con esto:

- recién ingresado, agregar un correo le mandó dos códigos;
- quitar un correo le pidió otro;
- pedir dos seguidos lo frenó con «Esperá un momento antes de pedir otro código».

Lo dijo así: «es un montón que para cada cosa que quiera hacer, como quitar un correo o vincularlo, me envíe un código, eso no tiene sentido».

Se le ofreció confirmar una vez y quedar habilitado 15 minutos. Eligió que no se confirme nunca.

## Decisión

- **Ninguna acción de Mi cuenta pide un código de confirmación:** sumar un correo, vincular Google, cambiar el principal, quitar un método ni dar de baja la cuenta. Alcanza con la sesión iniciada.
- **El correo nuevo sigue necesitando su propio código** para quedar verificado, y Google su ingreso con OAuth. Es la prueba de que el método nuevo es de quien lo suma, no una confirmación de la cuenta.
- **Lo que protege la cuenta a partir de ahora:**
  - el aviso a los demás métodos verificados cada vez que se suma o se quita un método, o se cambia el principal;
  - la baja con 30 días de gracia y su aviso, que se cancela ingresando;
  - «Recuperar mi cuenta»;
  - las reglas que ya existían: no se quita el último método y no se queda sin principal.

## Alternativas descartadas

- **Confirmar siempre** (la 0033 tal cual): un código por acción.
- **Confirmar solo si el ingreso no es reciente,** o una vez cada 15 minutos: el dueño del producto prefirió no confirmar nunca.

## Consecuencias

- **Back:**
  - salen `ReauthTicket`, `POST /api/me/reauth` y `/reauth/verify`, y lo que solo los usaba;
  - las acciones de Mi cuenta dejan de pedir el ticket;
  - los avisos siguen;
  - los tests de reautenticación pasan a probar que la acción anda sin código y que sale el aviso.
- **Front:** Mi cuenta deja de abrir el paso del código antes de cada acción. Los diálogos de confirmación («¿Quitar este correo?») siguen, sin código.
- **E2E real:** el recorrido «quitar un método con código enviado a otro» pasa a quitarlo sin código. «Sesión abierta no suma correo» se reemplaza por «sumar un correo manda solo el código del correo nuevo».
- **Plan:** A5 de los [arreglos antes de la Etapa 7](../plans/2026-10-06-arreglos-antes-de-la-7.md).
