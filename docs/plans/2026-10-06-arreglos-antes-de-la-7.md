# Arreglos antes de la Etapa 7

> **Para Codex:** cada arreglo va con su rojo, su verde y su commit, como en los demás planes, con commits que nombran solo tus rutas (`git commit -m "…" -- <rutas>`). Todo en `main` y sin push. Lo que se ve en pantalla sale de su tablero: si el tablero todavía no está, frená y preguntale al usuario.

Junta lo que apareció después de cerrar la Etapa 5: lo que el usuario encuentra al probar y lo que el usuario elija del informe de la [revisión de código](2026-10-06-revision-de-codigo.md). La Etapa 7 arranca con esta lista resuelta.

## A1 · El operador con su sesión abierta toca «Ingresar» (encontrado por el usuario el 2026-10-06)

**El problema:**
- La cuenta de un operador solo entra a Plataforma (ADR 0042).
- Con su sesión abierta en el navegador, el operador toca «Ingresar» o «Ingresá como empresa» en el inicio, y `/connect/authorize` rechaza el pedido con `access_denied` y `Tenancy.Access.PlatformOnly`. El rechazo es correcto.
- `CallbackPage` traduce solo `Tenancy.Access.NotMember` y `Tenancy.Member.Inactive`. Este código cae en el genérico «No pudimos iniciar tu sesión».
- «Volver a ingresar» vuelve a fallar igual, porque la sesión del operador sigue abierta: el usuario queda encerrado.

**Por qué no lo vimos:**
- Ningún tablero dibujaba ese caso.
- El E2E arranca cada recorrido con un navegador limpio y nunca prueba la puerta de persona o de empresa con la sesión del operador abierta.

**La decisión (2026-10-06):** una pantalla propia, como «Sin empresa» de `BusinessAccessResult`. El usuario no tuvo preferencia y la Guía eligió esta opción. Lleva:
- el texto del back: «Esta cuenta es de un operador: solo entra a Plataforma.»;
- «Ir a Plataforma» (principal);
- «Salir», que cierra la sesión para entrar con otra cuenta.

Diseño la dibuja como estado de `Ingreso` y `M-Ingreso`.

- [ ] **Rojo:**
  - `CallbackPage.test.tsx`: con `access_denied` y `Tenancy.Access.PlatformOnly`, se ve la pantalla del operador con sus dos botones, y no el error genérico;
  - «Ir a Plataforma» lleva a `/plataforma`;
  - «Salir» cierra la sesión y deja ingresar con otra cuenta;
  - axe y 390.
- [ ] **El E2E real suma un recorrido:** el operador ingresa a Plataforma y, en el mismo navegador, toca «Ingresar» en el inicio. Ve la pantalla del operador, toca «Salir» e ingresa como persona con otro correo.
- [ ] **Verde,** con la captura lado a lado contra el tablero, y `docs/features/identidad.md` al día.

**El otro caso:** la misma pantalla aparece cuando el operador escribe su correo en la puerta de persona o de empresa y pone el código. Con la [ADR 0043](../decisions/0043-las-pantallas-dicen-si-hay-cuenta.md), el correo del operador se comporta como el de cualquier cuenta antes del código. Esta pantalla es privada: solo la ve quien probó que el correo es suyo.

**Hecho cuando:** el operador nunca ve el error genérico y siempre tiene cómo salir. Antes del código, ninguna pantalla pública menciona al operador ni a Plataforma.

## A2 · Las pantallas dicen si un correo tiene cuenta (ADR 0043, decidido por el usuario el 2026-10-06)

**El problema:** con la [ADR 0041](../decisions/0041-no-revelar-cuentas.md), el ingreso con un correo sin cuenta decía «Te mandamos un código» y no mandaba nada. La persona no sabía que tenía que registrarse. El usuario lo encontró al probar: «es lógica pura».

**La decisión:** la [ADR 0043](../decisions/0043-las-pantallas-dicen-si-hay-cuenta.md), que reemplaza a la 0041. Lo que se ve sale de los tableros que dibuja Diseño: Ingreso, Registro, Registro-Empresa, Recuperar y Cuenta-Agregar, con sus `M-*`.

- [ ] **Back, rojo y verde:**
  - pedir el código de ingreso con un correo sin cuenta responde un error propio y no manda código;
  - el registro de una persona con un correo que ya tiene cuenta responde un error propio;
  - el correo de un operador en las puertas públicas se comporta como el de cualquier cuenta: ingreso con código, y registro con «ya tenés cuenta»;
  - «Recuperar mi cuenta» y agregar un correo en Mi cuenta dicen si el correo ya es de otra cuenta;
  - cada error lleva su código `Area.Entidad.Motivo`, su texto en es y en, y la ficha `identidad.md` al día;
  - siguen los topes por destino, por cuenta y por dirección;
  - los tests de la 0041 que pedían la misma respuesta pasan a pedir la de cada caso;
  - se quitan el código interno que no se mandaba y el aviso «Ya tenés una cuenta con este correo».
- [ ] **Front, rojo y verde:**
  - la pantalla de cada caso, como su tablero, con el correo ya escrito en el destino: «Crear mi cuenta», «Registrá tu empresa» o «Ingresar»;
  - axe y 390;
  - la captura lado a lado.
- [ ] **El E2E real suma estos recorridos:**
  - ingresar con un correo sin cuenta, ir a «Crear mi cuenta» y terminar el registro;
  - registrarse con un correo que ya tiene cuenta e ir a «Ingresar»;
  - registrar una empresa con un correo nuevo y entrar como empresa.

**Hecho cuando:** ninguna pantalla dice «Te mandamos un código» si el código no sale, y desde cada caso la persona sabe qué hacer.
