# ADR 0043 · Las pantallas dicen si un correo tiene cuenta

Estado: aceptada el 2026-10-06 por el dueño del producto. Reemplaza la [0041](0041-no-revelar-cuentas.md).

## Contexto

La 0041 hacía que ninguna pantalla dijera si un correo o un teléfono tiene cuenta, para que nadie pudiera averiguar quién está registrado probando correos. Al probar la aplicación de punta a punta, el dueño del producto se encontró con esto:

- **Ingresar con un correo sin cuenta:** la pantalla dice «Te mandamos un código», pero no manda nada.
- **Ingresar o registrar una empresa con el correo del operador:** pide el código y después muestra un error genérico, o vuelve al inicio sin explicar nada.

Lo dijo así: «como iniciar sesión y no tener una cuenta no me debería enviar el código, me debería decir ir a registrarme… es lógica pura».

## Decisión

Las pantallas dicen claramente qué pasa con el correo, **antes** de mandar un código:

- **Ingreso, como persona o como empresa,** con un correo sin cuenta:
  - no se manda código;
  - dice «No hay una cuenta con ese correo.»;
  - ofrece crearla, con el correo ya escrito: «Crear mi cuenta» como persona, «Registrá tu empresa» como empresa.
- **Registro de una persona** con un correo que ya tiene cuenta: dice «Ya tenés una cuenta con ese correo.» y ofrece «Ingresar», con el correo ya escrito.
- **El correo de un operador** en las puertas públicas (el ingreso y el registro de una persona o de una empresa) se comporta como el de cualquier cuenta ([0042](0042-operadores-de-plataforma.md)):
  - el ingreso manda el código;
  - el registro de una persona dice «Ya tenés una cuenta con ese correo.»;
  - el registro de una empresa pide el código, como con cualquier cuenta que ya existe.

  Nada menciona al operador ni a Plataforma antes del código. Recién cuando prueba que el correo es suyo, con el código o con su sesión ya abierta, el operador ve una pantalla privada: le dice que esa cuenta entra por Plataforma, con «Ir a Plataforma» y «Salir» (A1 de los arreglos). Lo pidió el dueño del producto: «es algo íntimo mío… esa prueba solo yo tengo que saberla y solo yo puedo ingresar».
- **«Recuperar mi cuenta»:**
  - si el correo de antes no tiene cuenta, lo dice;
  - si el correo nuevo ya es de otra cuenta, lo dice y no manda código.
- **Mi cuenta,** al agregar un correo que ya es de otra cuenta: lo dice y no manda código. Vuelve «Ese correo ya está asociado a otra cuenta.».
- **Lo que no cambia:**
  - los topes por destino, por cuenta y por dirección, que frenan a quien prueba correos en masa;
  - el código, que sigue siendo de un solo uso, con vencimiento y tope de intentos;
  - los estados de una cuenta suspendida, con la baja pedida o bloqueada, que se muestran como hasta ahora.

## Alternativas descartadas

- **Mantener la 0041 y solo cambiar el texto** («Si tenés una cuenta, te llega un código»): sigue sin decirle a la persona qué hacer.
- **Decirlo solo en el ingreso y el registro:** si el ingreso lo dice, esconderlo en las demás pantallas ya no protege nada.

## Consecuencias

- **Back:** la petición del código responde un error propio cuando no hay cuenta, cuando ya la hay o cuando el correo es de un operador, con su código `Area.Entidad.Motivo` en es y en. Desaparecen el código interno que no se mandaba (la precisión de T14) y el aviso «Ya tenés una cuenta con este correo» para quien recupera o agrega un correo ajeno.
- **Tests:** los que exigían la misma respuesta con un correo libre y con uno ocupado pasan a exigir la respuesta propia de cada caso.
- **Lienzo:** Diseño dibuja los estados nuevos en Ingreso, Registro, Registro-Empresa, Recuperar y Cuenta-Agregar, con sus `M-*`.
- **Plan:** se programa como **A2** de los [arreglos antes de la Etapa 7](../plans/2026-10-06-arreglos-antes-de-la-7.md). El E2E real suma un recorrido por cada caso.
