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

**Hecho cuando:** el operador con la sesión abierta nunca ve el error genérico y siempre tiene cómo salir.
