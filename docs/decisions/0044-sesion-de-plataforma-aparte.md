# ADR 0044 · La sesión de Plataforma va aparte

Estado: aceptada el 2026-10-06 por el dueño del producto. Completa la [0042](0042-operadores-de-plataforma.md).

## Contexto

El servidor de ingreso guarda una sola sesión por navegador, en la cookie de Identity, para los tres accesos: persona, empresa y plataforma. `/connect/authorize` toma esa sesión y, si existe, intenta entrar directo con esa cuenta (`ConnectController`, líneas 48 a 56). Así, una persona que ya ingresó pasa a su empresa sin pedir otro código. Está bien.

Pero la cuenta de un operador solo entra a Plataforma (0042). Con la sesión del operador guardada, tocar «Ingresar» o «Ingresá como empresa» en el inicio no muestra el ingreso: falla con `Tenancy.Access.PlatformOnly` y cae en «No pudimos iniciar tu sesión». El dueño del producto lo encontró al probar la aplicación como un usuario común.

## Decisión

Plataforma tiene su propia sesión, como un backoffice:

- **Dos sesiones por navegador, cada una en su cookie:**
  - la **pública**, para persona y empresa;
  - la de **Plataforma**, solo para `access=platform`.
- **El ingreso con código** guarda la sesión de la puerta por la que entró: la de Plataforma si el pedido es de `access=platform`, y la pública en los demás casos.
- **`/connect/authorize`** lee solo la sesión del acceso pedido. La sesión del operador nunca participa del ingreso de una persona o de una empresa: con solo esa sesión, «Ingresar» muestra el ingreso.
- **Se pueden tener las dos a la vez:** Plataforma abierta como operador y, en el mismo navegador, una persona o una empresa con otra cuenta.
- **Salir** cierra solo la sesión de ese lado.
- **Cerrar sesiones y la baja** cierran las dos.
- **La cuenta de un operador nunca recibe la sesión pública.** Si pone su correo y su código en una puerta pública, ve la pantalla privada que lo manda a Plataforma (A1 de los arreglos, [0043](0043-las-pantallas-dicen-si-hay-cuenta.md)).

## Alternativas descartadas

- **Una sola sesión que se reemplaza:** si la cuenta guardada no puede usar esa puerta, se muestra el ingreso y la nueva sesión pisa a la anterior. Es más chico, pero el operador queda afuera de Plataforma cada vez que prueba como usuario.
- **Arreglarlo solo en la pantalla de vuelta** (`CallbackPage`): el usuario seguiría sin ver el ingreso.

## Consecuencias

- **Back:** un esquema de cookie más para Plataforma, con la misma configuración segura que la pública. Cambian el ingreso con código, `ConnectController.Authorize`, la salida, el cierre de sesiones y la baja.
- **Tests:**
  - con la sesión de Plataforma abierta, el authorize de persona y el de empresa redirigen al ingreso;
  - las dos sesiones conviven;
  - salir de un lado no cierra el otro;
  - cerrar sesiones cierra las dos.
- **E2E real:** un recorrido nuevo. El operador entra a Plataforma y, en el mismo navegador, una persona ingresa con otro correo. Los dos lados siguen andando.
- **Plan:** A3 de los [arreglos antes de la Etapa 7](../plans/2026-10-06-arreglos-antes-de-la-7.md).
