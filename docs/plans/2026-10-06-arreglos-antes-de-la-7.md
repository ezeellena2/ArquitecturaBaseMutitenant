# Arreglos antes de la Etapa 7

> **Para Codex:** cada arreglo va con su rojo, su verde y su commit, como en los demás planes, con commits que nombran solo tus rutas (`git commit -m "…" -- <rutas>`). Todo en `main` y sin push. Lo que se ve en pantalla sale de su tablero: si el tablero todavía no está, frená y preguntale al usuario.

**Tableros:** lienzo **v112, front `9c1c782`**, aprobado por el usuario el 2026-10-06.

| Arreglo | Tablero | Tweak y estado |
|---|---|---|
| A1 | `Ingreso` y `M-Ingreso` | «estado» = «Operador: la cuenta entra por Plataforma». Es la pantalla de «Sin empresa» con «Esta cuenta es de un operador: solo entra a Plataforma», «Ir a Plataforma» (principal) y «Salir». |
| A2 | `Ingreso` y `M-Ingreso` | «estado» = «Correo sin cuenta». Debajo del campo va «No hay una cuenta con ese correo.» y, abajo, «Crear mi cuenta» o «Registrá tu empresa», según el Tweak «puerta». No se manda código. |
| A2 | `Registro` y `M-Registro` | «estado» = «Correo con cuenta». Debajo del campo va «Ya tenés una cuenta con ese correo.» y, abajo, «Ingresar». |
| A2 | `Recuperar` y `M-Recuperar` | «paso» = «1. Sin cuenta con ese correo»: «No hay una cuenta con ese correo.». «paso» = «2. El correo nuevo ya es de otra cuenta»: «Ese correo ya es de otra cuenta.». |
| A2 | `Cuenta-Agregar` y `M-Cuenta-Agregar` | «Correo: ya es de otra cuenta»: «Ese correo ya está asociado a otra cuenta.». |
| A6 | `Cuenta` y `M-Cuenta` | En la compu, «Tus datos» va en una fila de cuatro columnas iguales: Nombre, Apellido, Idioma y región, Zona horaria. En el teléfono, uno debajo del otro. «Criterio · Formularios» dice «Nombre» en sus ejemplos. |

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
  - el correo de un operador en las puertas públicas se comporta como el de cualquier cuenta:
    - el ingreso y el registro de una empresa piden el código;
    - el registro de una persona responde «ya tenés cuenta»;
    - recién después del código aparece la pantalla privada de A1;
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

## A3 · La sesión de Plataforma va aparte (ADR 0044, decidido por el usuario el 2026-10-06)

**El problema:**
- Hay una sola sesión por navegador para persona, empresa y plataforma.
- Con la sesión del operador guardada, «Ingresar» y «Ingresá como empresa» no muestran el ingreso: `/connect/authorize` intenta entrar con el operador y falla con `Tenancy.Access.PlatformOnly` (`ConnectController.cs`, líneas 48 a 56).
- El usuario lo encontró al probar como un usuario común.

**La decisión:** la [ADR 0044](../decisions/0044-sesion-de-plataforma-aparte.md). Plataforma tiene su propia sesión, en su cookie; persona y empresa comparten la pública.

- [x] **Rojo** (integración): observado el 2026-10-06, **18 tests: 8 fallos y 10 verdes**, sin omitidos (EXIT 2, 53,9 s). Evidencia: `.local/pre7/a3/integration-red.log`.
  - con solo la sesión de Plataforma, el authorize de persona y el de empresa redirigen al ingreso de esa puerta;
  - con las dos sesiones, cada authorize usa la suya;
  - salir de Plataforma no cierra la pública, y salir de la pública no cierra Plataforma;
  - cerrar sesiones y la baja cierran las dos;
  - un operador nunca recibe la sesión pública.
- [x] **Implementación:** verde dirigido **18/18** y final de las clases cambiadas **58/58**, sin omitidos (EXIT 0). El alcance inicial ejecutó 286 tests: 284 verdes y dos fallos de preparación HTTPS de los tests nuevos de Google, corregidos y repetidos en el final de 58. Arquitectura **148/148**, Application afectada **28/28**, build de solución **sin advertencias ni errores**. Evidencias: `.local/pre7/a3/integration-green.log`, `integration-scope.log`, `integration-final.log`, `build-back.log` y sus `.exit`.
  - un esquema de cookie para Plataforma, con la misma configuración segura que la pública;
  - el ingreso con código guarda la sesión según el acceso del pedido;
  - `Authorize` lee solo la del acceso pedido;
  - la salida y el cierre de sesiones cubren las dos.

  Copiá cómo se arma hoy la cookie de Identity. No agregues capas: si hace falta una pieza nueva, que lleve un sufijo de la lista de `capas-y-flujo`.
- [x] **E2E real:** `MT_E2E_ONLY_CASE=A3`, `npm run test:e2e:real`, **2/2** (padre y recorrido), sin fallos, cancelados ni omitidos; EXIT 0, 109,7 s. Dos pestañas del mismo `BrowserContext`: operador en Plataforma, persona con otro correo, F5 y reapertura conservan al operador; salir de la pública conserva Plataforma y luego se sale de Plataforma. Front, API, PostgreSQL y pickup reales, sin mocks; se respetó el cooldown HTTP 429. Aspire apagado al finalizar. Evidencia FRONT: `.local/pre7/a3/real-a3.log` y `real-a3.exit`.
- [x] **Docs:** `identidad.md` y `plataforma.md` describen las dos cookies y su revocación por cuenta.

**Hecho cuando:** con Plataforma abierta, «Ingresar» y «Ingresá como empresa» muestran siempre el ingreso, y las dos sesiones conviven.

## A4 · «Registrarte con Google» no va a Google (encontrado por el usuario el 2026-10-06)

**El problema:** en el registro de una persona, con los términos aceptados, tocar «Registrarte con Google» no lleva a Google. Marca el campo de correo con «Ingresá un correo electrónico válido.». La Guía lo reprodujo en el navegador.

**La causa:**
- `GoogleButton` (modo registro) arma su formulario oculto con `createPortal` dentro de `document.body`, pero lo renderiza **adentro del formulario de correo** de `SignupPage`.
- En React, el evento `submit` de un portal sube por el árbol de React, no por el del DOM.
- Así, el envío del formulario de Google llega al `onSubmit` del formulario de correo. React Hook Form cancela el envío (`preventDefault`) y valida el correo vacío.
- El back anda bien: el POST de `/api/auth/external/google` responde 302 a Google.
- Pasa lo mismo en «Registrá tu empresa» («Seguir con Google»), que usa la misma pieza.

**Por qué no lo vimos:** el E2E no puede usar Google, y los tests del front revisan el formulario oculto pero no que el clic llegue a enviarlo.

- [x] **Rojo** (`SignupPage.test.tsx` y `BusinessSignupPage.test.tsx`): tocar el botón de Google con los términos aceptados manda el formulario de Google, no ejecuta la validación del correo y no muestra «Ingresá un correo electrónico válido.». Observado el 2026-10-06: **2 fallos y 16 verdes**, EXIT 1; ambos envíos llegaban con `defaultPrevented=true`. Evidencia FRONT: `.local/pre7/a4/unit-red.log`.
- [x] **Implementación:** **23/23** en Google y los registros, más **30/30** de Ingreso; build, lint dirigido, sintaxis del runner y diff EXIT 0. El lint dirigido conserva tres advertencias anteriores de los efectos de `BusinessSignupPage`. Evidencias FRONT: `.local/pre7/a4/unit-green.log`, `login-scope.log`, `build-front.log`, `lint-front.log` y sus `.exit`.
  - el botón de Google y su formulario quedan **fuera** del formulario de correo en el árbol de React, como hermanos, con la casilla de términos compartida desde la página;
  - sin portal, y sin cortar la propagación con `stopPropagation`.
- [x] **E2E real:** `MT_E2E_ONLY_CASE=A4`, `npm run test:e2e:real`, **2/2** (padre y recorrido), sin fallos, cancelados ni omitidos; EXIT 0, 65,7 s. «Registrarte con Google» y «Seguir con Google», con términos aceptados y correo vacío, envían POST real 302 y llegan a `accounts.google.com`, sin ingresar a Google ni imprimir URL/state. El primer intento en sandbox falló antes del recorrido al arrancar Aspire; se preservó en `real-a4-startup-failed.*` y la repetición autorizada pasó. Evidencia FRONT: `.local/pre7/a4/real-a4.log` y `.exit`; Aspire apagado (`ps=[]`, EXIT 0). Capturas de ambos registros a 1440 y 390 en FRONT `docs/design/capturas/consolidacion/pre7/registro/`.

**Hecho cuando:** los dos botones de Google del registro llevan a Google y nunca validan el correo.

## A5 · Mi cuenta sin código de confirmación (ADR 0045, decidido por el usuario el 2026-10-06)

**El problema:** cada acción de Mi cuenta pedía un código a un método que ya tenía la cuenta, de un solo uso y para esa sola acción:
- agregar un correo pedía dos códigos;
- quitar un correo pedía otro;
- pedir dos seguidos frenaba con «Esperá un momento antes de pedir otro código».

**La decisión:** la [ADR 0045](../decisions/0045-mi-cuenta-sin-codigo-de-confirmacion.md). Ninguna acción de Mi cuenta pide confirmación: sumar un correo, vincular Google, cambiar el principal, quitar un método ni dar de baja la cuenta. El correo nuevo sigue con su propio código.

- [x] **Rojo** (integración): observado el 2026-10-06, **5 tests: 4 fallos y 1 verde**, sin omitidos; EXIT 2, 32,369 s. En el front, `AccountSessionActions.test.tsx`: **5 fallos de 5**, EXIT 1. Evidencias BACK `.local/pre7/a5/integration-red.log` y FRONT `.local/pre7/a5/unit-red.log`, con sus `.exit`.
  - sumar un correo sin ticket manda solo el código del correo nuevo y avisa a los demás métodos;
  - vincular Google, cambiar el principal y quitar un método andan sin ticket y avisan;
  - la baja anda sin ticket, con su gracia y su aviso;
  - siguen el último método y el principal obligatorio.
- [x] **Implementación:** integración dirigida **73/73**, seguridad y persistencia afectadas **34/34**, arquitectura **148/148** y dominio **12/12**, sin omitidos; EXIT 0. Front **39/39** en siete archivos, más API **3/3**; build de API sin advertencias ni errores, build front, lint dirigido, contrato generado y comprobado, sintaxis del runner y catálogo de capturas **10/10**, EXIT 0. Evidencias `.local/pre7/a5/` de ambos repos. El primer verde de integración falló 17/71 por faltar el grant `mt_app` de la nueva tabla de cancelación: la migración se corrigió y el alcance se repitió en verde.
  - salen `ReauthTicket`, `POST /api/me/reauth` y `/reauth/verify`, y todo lo que solo los usaba, en el back, el front, el contrato y los textos; la repetición final de `AccountSessionMigrationTests` confirma **1/1**, EXIT 0, 24,044 s, con el propósito histórico `AddEmail`;
  - los diálogos de confirmación de Mi cuenta siguen, sin el paso del código.
- [x] **Docs:** `identidad.md`, `datos-personales`, arquitectura, árboles y reglas describen acciones con sesión vigente y la prueba específica de cancelación de baja; el contrato y los textos ya no ofrecen reautenticación.
- [x] **E2E real:** `MT_E2E_ONLY_CASE=A5`, `npm run test:e2e:real`, **3/3** (padre y dos recorridos), sin fallos, cancelados ni omitidos; EXIT 0, **137,947 s**. Correo nuevo con su único código, principal y quitar sin código, baja con gracia de 30 días y cancelación al probar posesión; Google inicia OAuth real a 390 sin código adicional. FRONT `.local/pre7/a5/real-a5-third.log` y `.exit`; BACK `aspire-final.json` = `[]`, EXIT 0. El primer intento falló por el helper que aún buscaba términos dentro del formulario después de A4. En el segundo pasó la cadena principal y falló la preparación móvil por un helper de perfil de escritorio. Se corrigió cada preparación y se repitió el alcance; ambos logs fallidos se conservan, sin dar esos intentos por buenos.
  - «quitar un método» pasa a quitarlo sin código;
  - «sesión abierta no suma correo» se reemplaza por «sumar un correo manda solo el código del correo nuevo».

**Aclaraciones de implementación:**
- `ReauthTicket` también transportaba la prueba para cancelar una baja después del ingreso con código o Google. Esa autorización se conserva en una pieza específica de cancelación, con cuenta, retorno permitido, uso único y cinco minutos; la migración conserva solo esas pruebas y elimina la reautenticación de Mi cuenta.
- `Cuenta-Baja` y `Cuenta-Metodo` de la v112 todavía dibujan el código. El mandato explícito de A5 y la ADR 0045 retiran ese bloque y su instrucción; se conservan los diálogos y los demás textos. La diferencia se registra en las comparaciones, sin modificar el lienzo de Diseño. La fila de nombre y apellido del fondo corresponde a A6 y se verifica al llegar a ese arreglo.

## A6 · Nombre y apellido en dos campos (ADR 0046, pedido por el usuario el 2026-10-06)

**La decisión:** la [ADR 0046](../decisions/0046-nombre-y-apellido.md). Se programa con el tablero de Cuenta y M-Cuenta que dibuja Diseño.

- [ ] **Back:**
  - nombre y apellido por separado en la identidad, con su migración (la primera palabra al nombre y el resto al apellido);
  - el nombre para mostrar se arma en un solo lugar. **Conservá `DisplayName` como ese nombre armado**, así no cambian las lecturas que hoy lo muestran: son unos 77 archivos del back y 42 del front. Solo cambian la identidad, la edición de `/api/me`, la migración, la semilla, Google y el formulario de Mi cuenta;
  - los contratos de `/api/me` y de las lecturas con nombres;
  - la semilla del operador;
  - rojo y verde de cada parte.
- [ ] **Front:**
  - Mi cuenta con «Nombre» y «Apellido», como el tablero;
  - los tipos regenerados;
  - las pantallas que muestran el nombre siguen usando el nombre armado, sin juntarlo por su cuenta.
- [ ] **E2E real:** guardar nombre y apellido en Mi cuenta y verlos en el menú.

## A7 · Google trae el nombre y el apellido (ADR 0046, pedido por el usuario el 2026-10-06)

- [ ] **Rojo** (integración, con el Google de prueba de los tests):
  - registrarse, ingresar o vincular Google completa el nombre y el apellido desde `given_name` y `family_name` si están vacíos;
  - si ya tienen algo, no los toca.
- [ ] **Implementación:** donde se resuelve la cuenta de Google, sin pisar lo que escribió la persona. Docs: `identidad.md`.

## Puerta: el recorrido de un usuario nuevo

Los tests y el E2E pasaron en verde con todo esto roto, porque probaban las reglas escritas, no lo que hace una persona. Antes de dar por terminados los arreglos, se recorre la app a mano como un usuario nuevo:
- **en el mismo navegador,** sin limpiar sesiones entre un paso y otro, como lo hizo el usuario;
- **a 1440 y a 390,** en la compu y en el teléfono;
- **con correos de prueba propios,** nunca los del usuario.

Lo hace Codex al cerrar y lo repite la Guía, con una captura de cada paso. Cada paso que se pueda automatizar, sin Google, queda también en el E2E real.

1. **Persona sin cuenta:**
   - en el inicio, tocar «Ingresar» y poner un correo sin cuenta: tiene que decir que no hay cuenta y ofrecer «Crear mi cuenta»;
   - desde ahí, registrarse con el correo ya escrito, poner el código y llegar a su inicio.
2. **Persona con cuenta:**
   - «Crear mi cuenta» con el mismo correo: tiene que ofrecer «Ingresar»;
   - ingresar con código;
   - salir y volver a ingresar.
3. **Google:** «Registrarte con Google» y «Seguir con Google» llevan a Google, en el registro de persona y en el de empresa, y nunca validan el correo. Este paso no va al E2E.
4. **Empresa:**
   - «Registrá tu empresa» con un correo nuevo y entrar como empresa;
   - «Ingresá como empresa» con una persona sin empresa: muestra «Sin empresa»;
   - con una con empresa: entra.
5. **Operador y usuario en el mismo navegador:**
   - el operador entra a Plataforma;
   - en otra pestaña, «Ingresar» muestra el ingreso, una persona entra con otro correo, y Plataforma sigue abierta;
   - el correo del operador en la puerta de persona pide el código como cualquier cuenta y, después del código, muestra la pantalla privada.
6. **Mi cuenta:**
   - poner nombre y apellido y guardar;
   - agregar un correo con un solo código, el del correo nuevo;
   - quitar un correo sin código;
   - cambiar el principal sin código;
   - con un correo que ya es de otra cuenta: lo dice.
7. **«Recuperar mi cuenta»:** con un correo sin cuenta lo dice, y con un correo nuevo que ya es de otra cuenta, también.
8. **Errores comunes:** un correo mal escrito, un código equivocado, un código vencido y pedir otro código antes de tiempo. Cada uno tiene que decir qué pasó y qué hacer, nunca «No pudimos iniciar tu sesión» sin salida.

**Hecho cuando** los ocho pasos andan en la compu y en el teléfono. Las capturas van al usuario antes de decirle que está listo.

## Orden

1. **Sin dibujos:** A3, A4, A5 y A7, que no esperan nada.
2. **Con los tableros de la v112** (`9c1c782`, ya publicada): A1, A2 y A6.
3. **Después:** la [revisión de código](2026-10-06-revision-de-codigo.md).

Cada arreglo pasa su E2E real antes del siguiente commit de cierre.

## Registro de resultados

Se completa al verificar cada arreglo. Los logs locales conservan la salida y el código de cada comando; las capturas comparan el tablero v112 (`9c1c782`) con la app a 1440 y 390. Se trabaja en `main`, sin push, sin tocar `docs/design/lienzo/` y sin iniciar la revisión de código ni la Etapa 7.

| Arreglo | Rojo | Verde y alcance afectado | E2E real | Commits y capturas |
|---|---|---|---|---|
| A3 | 18 tests: 8 fallos, 10 verdes, 0 omitidos; EXIT 2; `.local/pre7/a3/integration-red.log`. | Dirigido 18/18; alcance inicial 284/286 con dos preparaciones HTTPS corregidas; final cambiado 58/58. Arquitectura 148/148; Application 28/28; build back 0 advertencias/errores; build front, lint dirigido y sintaxis del runner EXIT 0. | Focal `A3`: 2/2, 0 fallos/cancelados/omitidos, EXIT 0, 109,7 s. Dos pestañas y ambas salidas; FRONT `.local/pre7/a3/real-a3.log`. | BACK `30aea96`; FRONT `ac6a177`. No cambia una pantalla: convivencia verificada con las pantallas reales. |
| A4 | 2 fallos y 16 verdes; EXIT 1; `.local/pre7/a4/unit-red.log` FRONT. | Google y registros 23/23; ingreso 30/30; build y lint dirigido EXIT 0 (3 advertencias previas en efectos de BusinessSignupPage). | Focal `A4`: 2/2, 0 fallos/cancelados/omitidos, EXIT 0, 65,7 s. Ambos registros llegan a Google sin correo escrito. Primer arranque fallido en sandbox preservado; repetición autorizada verde. | FRONT implementación `1865921`; capturas y manifiesto `d905057`: `docs/design/capturas/consolidacion/pre7/registro/`, 1440 y 390. |
| A5 | Back 4 fallos y 1 verde, EXIT 2; front 5/5 fallan, EXIT 1; `.local/pre7/a5/*red.log`. | Integración 73/73; seguridad/persistencia 34/34; arquitectura 148/148; dominio 12/12; migración final 1/1. Front 39/39 más API 3/3; catálogo 10/10. API build 0 advertencias/errores; front build, lint dirigido, contrato y sintaxis EXIT 0. Primer fallo de grants corregido en la migración y repetido. | Focal `A5`: tercer intento **3/3**, 0 fallos/cancelados/omitidos, EXIT 0, 137,947 s; `real-a5-third.log`. Dos preparaciones fallidas corregidas y preservadas; cadena de Mi cuenta y OAuth Google a 390. Aspire `ps=[]`, EXIT 0. | BACK `6b07c3e`; FRONT `544bfe6`. Diez comparaciones en `docs/design/capturas/consolidacion/pre7/cuenta/`, a 1440 y 390. El OTP antiguo del tablero se conserva como referencia y la diferencia explícita A5/ADR0045 se documenta. |
