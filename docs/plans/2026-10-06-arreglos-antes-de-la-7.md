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

- [x] **Rojo:** observado el 2026-10-06, **49 tests: 10 fallos y 39 verdes**, 0 omitidos, EXIT 1, **25,02 s**. FRONT `.local/pre7/a1/unit-red.log` y `.exit`; incluye Callback, ambas puertas de ingreso, es/en, 390, salida fallida y el descarte exclusivo del usuario Platform en memoria antes del logout público.
  - `CallbackPage.test.tsx`: con `access_denied` y `Tenancy.Access.PlatformOnly`, se ve la pantalla del operador con sus dos botones, y no el error genérico;
  - «Ir a Plataforma» lleva a `/plataforma`;
  - «Salir» cierra la sesión y deja ingresar con otra cuenta;
  - axe y 390.
- [x] **El E2E real suma un recorrido:** focal `A1,A3`, segundo intento **3/3**, 0 fallos/cancelados/omitidos, EXIT 0, **258,090 s**. Con solo la cookie de Plataforma, ambas puertas muestran el formulario; después del código, el estado privado aparece a 1440 y 390. «Ir a Plataforma» usa la cookie vigente; «Salir» hace el logout público sin token Platform, permite entrar con otra persona y conserva Plataforma recargada. FRONT `.local/pre7/a1/real-a1-a3-second.log` y `.exit`. El primer intento (1 verde y 2 fallos de 3, EXIT 1) se conserva: el listener HTTP vencía mientras el botón esperaba el cooldown. El helper ahora espera la UI antes de escuchar HTTP; se repitió la misma prueba con los mismos topes y tiempos. Aspire final `[]`, EXIT 0; el `stop` adicional devolvió 7 porque el runner ya había apagado el AppHost.
- [x] **Verde:** pantalla y salida **55/55**, alcance auth/traducciones **16/16**, catálogo **11/11**; build front, lint dirigido y sintaxis EXIT 0. El primer build falló por tres tipos de preparación de tests y se repitió en verde. La primera comparación móvil heredaba `NotMember`: test dirigido del catálogo 1/1 rojo → 1/1 verde, cuatro comparaciones repetidas, revisadas y verificadas, EXIT 0. FRONT `a6782b0` (13 rutas), BACK ficha `d15b19b` (una ruta). Capturas en `docs/design/capturas/consolidacion/pre7/ingreso/`, con los cuatro comandos de vivo enviados al usuario.

**El otro caso:** la misma pantalla aparece cuando el operador escribe su correo en la puerta de persona o de empresa y pone el código. Con la [ADR 0043](../decisions/0043-las-pantallas-dicen-si-hay-cuenta.md), el correo del operador se comporta como el de cualquier cuenta antes del código. Esta pantalla es privada: solo la ve quien probó que el correo es suyo.

**Aplicación después de A3:** con solo la cookie de Plataforma, ambas puertas públicas muestran el formulario. El estado privado se muestra después de probar el correo y cuando el callback autorizado devuelve `Tenancy.Access.PlatformOnly`. «Salir» hace el logout público sin enviar un token Platform y conserva su cookie. El callback toma la puerta del estado OIDC autorizado; si no trae una puerta pública reconocida, usa Persona.

**Hecho cuando:** el operador nunca ve el error genérico y siempre tiene cómo salir. Antes del código, ninguna pantalla pública menciona al operador ni a Plataforma.

## A2 · Las pantallas dicen si un correo tiene cuenta (ADR 0043, decidido por el usuario el 2026-10-06)

**El problema:** con la [ADR 0041](../decisions/0041-no-revelar-cuentas.md), el ingreso con un correo sin cuenta decía «Te mandamos un código» y no mandaba nada. La persona no sabía que tenía que registrarse. El usuario lo encontró al probar: «es lógica pura».

**La decisión:** la [ADR 0043](../decisions/0043-las-pantallas-dicen-si-hay-cuenta.md), que reemplaza a la 0041. Lo que se ve sale de los tableros que dibuja Diseño: Ingreso, Registro, Registro-Empresa, Recuperar y Cuenta-Agregar, con sus `M-*`.

- [x] **Back, rojo y verde:** BACK `962a71c`, 37 rutas propias; evidencia por alcance a continuación.
  - verde dirigido **6/6**, 0 fallos/omitidos, EXIT 0, **31,663 s**; `backend-green-second.log` y `.exit`. Integración ampliada final **91/91**, 0 fallos/omitidos, EXIT 0, **73,037 s**; `integration-fourth.log` y `.exit`. Los tres alcances anteriores fallaron por expectativas de la ADR 0041 y por dos preparaciones sin `Idempotency-Key`; se conservaron, se ajustaron las respuestas a la ADR 0043 sin retirar los controles de topes y bloqueo, y se repitió el alcance;
  - consulta inicial con límite por IP y revalidación al enviar: alcance adicional **20/20**, 0 omitidos, EXIT 0, **47,397 s**, `integration-preview-race-fourth.*`. La preparación de una recuperación histórica de Plataforma ya no usa el envío anónimo de un correo inexistente; conserva ese caso histórico en su fixture. El primer alcance (19 verdes y un fallo de 20) y los dos errores de compilación del preparador se conservaron;
  - unitarios y resources **48/48**, EXIT 0, **1,984 s** (`unit-second.*`); el primer intento dio 47 verdes y un fallo por el conteo anterior del catálogo de avisos, se conserva en `unit-first.*`. Arquitectura afectada **47/47**, EXIT 0, **3,919 s**; build AppHost **0 advertencias y 0 errores**, EXIT 0, **3,77 s** (`architecture-first.*`, `apphost-build.*`);
  - consulta de recuperación reforzada al cerrar: **6/6**, 0 fallos/omitidos, EXIT 0, **32,520 s**, `preview-readonly-final.*`. Para conocidos y desconocidos, códigos, outbox y solicitudes de recuperación quedan sin cambios; conocido responde 204 sin cookie y desconocido, 400 con código y traceId. Aspire final `[]`, EXIT 0 (`aspire-ps-final.*`);
  - rojo funcional verificado contra la versión anterior: **6/6 fallos**, 0 omitidos, EXIT 2, **31,094 s**; `backend-red-behavior.log` y `.exit`. Los cinco casos que emitían o aceptaban código responden 202 en vez de 400; la consulta de recuperación inexistente responde 404 en vez de 400. La preparación usa HTTPS sin redirecciones automáticas y los códigos consumidos se envejecen como en `AccountJourney`. Los cambios de producción A2 se restauraron por sus bytes antes del verde;
  - preparación inicial: `backend-red-second.log` dio 5 fallos de 5, EXIT 2, 27,267 s, pero **cuatro venían del preparador HTTP y sus redirecciones a 404**, antes de llegar a la conducta. No se cuenta como rojo dirigido completo. El primer intento tuvo un error de compilación por un `using` sin uso (EXIT 1); ambos se conservan. Se corrige el cliente de preparación (HTTPS y sin redirecciones automáticas) y se vuelve a observar contra la conducta anterior;
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
- [x] **Front, rojo y verde:** FRONT `0249b8a`, 19 rutas propias; doce comparaciones en `403472d`, 14 rutas propias.
  - verde dirigido **47/47** en tres archivos, EXIT 0, **16,04 s**; `front-green.log` y `.exit`. Catálogo de captura **13/13**, EXIT 0, **4,642 s**, después de su rojo de una preparación de botón desactualizada. Doce comparaciones repetidas, revisadas y verificadas; EXIT 0, enviadas al usuario con sus comandos de vivo. El fondo de Mi cuenta se repite al cerrar A6;
  - alcance de auth **106/106**, EXIT 0, **17,01 s** (`front-green-final.*`), seguido por dos casos nuevos del rechazo durante el envío de recuperación: rojo **2 fallos y 7 filtrados**, EXIT 1, **13,62 s**, y verde de Recuperar/Ingreso **40/40**, EXIT 0, **17,95 s**, `recovery-race-front-red/green.*`. Vuelve al campo rechazado y muestra su error;
  - build y lint final EXIT 0. Lint conserva advertencias de bundles históricos de Diseño y efectos existentes; no se declara sin advertencias. El primer build falló por una opción de tipos del test (`ByRoleOptions.exact`), corregida y repetida. El build del AppHost actualizó descripciones OpenAPI: el check desalineado (EXIT 1) se conserva y, después de regenerar, `contracts:check` volvió a EXIT 0;
  - el segundo E2E encontró un correo perdido al volver de OIDC. Rojo de producto observado en el ciclo completo Registro → Login → retorno autorizado con `StrictMode`: **1 fallo y 9 filtrados de 10**, EXIT 1, **7,34 s**, `oidc-strict-red.*`; verde dirigido **1/1**, 9 filtrados, EXIT 0, **6,95 s**, `oidc-strict-green.*`, conserva el correo y emite el POST del formulario. El inicializador leía y borraba el dato temporal durante render; ahora usa el patrón existente de lectura pura y descarte después del retorno confirmado. Alcance final **109/109** en ocho archivos, EXIT 0, **16,08 s**, `front-green-strict-final.*`; build y lint posteriores EXIT 0 (`front-build/lint-strict-final.*`). El cuarto E2E combinado verifica los cinco casos A2 y sus regresiones en verde, detallado abajo;
  - rojo observado: **7 tests, 5 fallos y 2 verdes**, 0 omitidos, EXIT 1, **14,43 s**; FRONT `.local/pre7/a2/front-red.log` y `.exit`;
  - la pantalla de cada caso, como su tablero, con el correo ya escrito en el destino: «Crear mi cuenta», «Registrá tu empresa» o «Ingresar»;
  - axe y 390;
  - la captura lado a lado.
- [x] **El E2E real suma estos recorridos:** cuarto combinado **9/9**, 0 fallos/cancelados/omitidos, EXIT 0, **575,132 s**, `real-fourth.*`. A1 **139,905 s**, A3 **62,542 s**, reclamo **86,258 s**. Aspire después del cuarto y al cerrar `[]`, EXIT 0. Se conserva la incertidumbre del tercer fallo intermitente de A1, sin atribuirle un arreglo causal.
  - primer alcance combinado `A2:,A1:,A3:,registro con posesión`: **9 tests, 7 verdes, 1 fallo del padre y 1 cancelado (A3)**, 0 omitidos, EXIT 1, **847,830 s**; FRONT `.local/pre7/a2/real-first.log` y `.exit`. A2, A1 y el reclamo de un correo pendiente pasaron, pero A3 llegó al sexto pedido del mismo operador dentro de la cuota de cinco por quince minutos: HTTP 429 `Auth.LoginCode.TooManyRequests`, timeout de 240 s. Este intento no cuenta como verde. La próxima preparación de A2 usa el operador reducido real ya sembrado; A1 y A3 mantienen su cuenta y los topes siguen intactos. Después de terminar los demás casos, se cerró solo el proceso propio que conservaba el timer cancelado; el runner devolvió EXIT 1. Aspire después del primero `[]`, EXIT 0 (`aspire-ps-after-first.*` BACK);
  - segundo alcance combinado: **9 tests, 7 verdes y 2 fallos** (padre y persona A2 a 390), 0 cancelados/omitidos, EXIT 1, **544,810 s**, `real-second.*`. A1, A3 y el reclamo pasaron, pero el ingreso desde Registro con correo existente no emitió el POST: el campo quedó vacío al retornar de OIDC. El reporte parcial de todos los A2 en verde se corrigió al leer la salida completa. Se agregó el rojo de `StrictMode` y se corrige el transporte del correo; este intento tampoco cuenta como verde;
  - tercer alcance combinado: **9 tests, 7 verdes y 2 fallos** (padre y A1), 0 cancelados/omitidos, EXIT 1, **545,176 s**, `real-third.*`. Los cinco A2, A3 y el reclamo pasaron; A1 no recibió la respuesta esperada tras respetar 53 s de reenvío empresarial. El timeout de respuesta por sí solo no demuestra que el POST no haya salido. Los últimos eventos API/OIDC registrados pertenecían a la pestaña de Plataforma, porque el wrapper todavía no observaba la pestaña pública nueva; tampoco prueban una navegación de la pestaña pública. La causa se investiga con diagnóstico del formulario, pedidos y respuestas públicos antes de repetir; este intento tampoco cuenta como verde. Aspire después del tercero `[]`, EXIT 0 (`aspire-ps-after-third.*` BACK);
  - diagnóstico real de A1 aislado: **2/2**, 0 fallos/cancelados/omitidos, **206,344 s**, `a1-diagnostic-first.log`; formularios de `/login` y `/login/empresa` con correo válido y botón «Enviar código» al reenviar. Este verde aislado no explica ni reemplaza el fallo del tercer combinado; queda pendiente su causa y repetición;
  - dos diagnósticos mínimos en orden `A2: operador,A1:`: **3/3**, 0 fallos/cancelados/omitidos, EXIT 0, **266,567 s** y **267,112 s**, `a1-minimal-first.*` y `a1-minimal-second.*`. El segundo conserva la secuencia original del clic y observa pedidos y respuestas de la pestaña pública. Aspire `[]`, EXIT 0. No se reprodujo la causa; se continúa con los cinco casos de A2 antes de A1, sin cambiar los tiempos ni los topes;
  - extracción histórica segura del API del tercer intento: BACK `real-third-api-operation-evidence-second.*`, EXIT 0. Las 17 entradas `Handling` terminan en `Handled` o `Failed`; el 429 empresarial terminó a las 02:33:42.426Z y no hay otra operación `RequestLoginCode` hasta A3, a las 02:35:08.149Z. Descarta una operación de aplicación colgada en ese intervalo, pero el log no registra comienzo/final de pedidos HTTP y no demuestra su ausencia;
  - diagnóstico en el orden completo de los cinco A2 antes de A1: **7/7**, 0 fallos/cancelados/omitidos, EXIT 0, **436,899 s**, `a1-context-first.*`; A1 **150,416 s**. En empresa se respetaron 429 de 54 y 2 segundos, seguidos por 202 y el 403 privado tras el código. Cada pedido público tuvo respuesta; Aspire `[]`, EXIT 0. El fallo intermitente del tercer intento no se reprodujo en ninguno de estos cuatro diagnósticos ni en el cuarto combinado con la observación correcta y sin capturadores en paralelo; no se atribuye una corrección causal;
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

**Rojo inicial (2026-10-07):** backend `backend-red-second.*`, **12 tests: 11 fallos y 1 verde**, 0 omitidos, EXIT 2, 39,593 s. Cinco fallos corresponden a la prioridad de la semilla; los otros seis se detienen en la preparación HTTP por redirecciones a 404 y **no se cuentan como rojo funcional de `/api/me`**. Se corrige el cliente y se reobserva contra la conducta anterior. El primer filtro no ejecutó tests (EXIT 8) y tampoco se cuenta como rojo. Front `front-red.*`, **2/2 fallos**, EXIT 1, 13,70 s: faltan los campos «Nombre» y «Apellido» a 1440 y 390. Su primer verde dio 25 verdes y 2 fallos de 27 por una preparación de nombres distinta, y no se da por bueno. Evidencias locales `.local/pre7/a6/` de cada repo; los resultados de las repeticiones se detallan abajo.

**Rojo funcional verificado:** BACK `backend-red-valid-second.*`, **13 tests: 12 fallos y 1 verde**, 0 omitidos, EXIT 2, 43,350 s. Cinco casos de semilla y siete de perfil llegan a la conducta: faltan las partes en GET, PUT acepta partes demasiado largas y el combinado no se actualiza. El intento `backend-red-valid.*` también se conserva: sus siete casos de perfil se detenían por HTTPS requerido después de fijar solo las redirecciones; no se cuenta como rojo funcional completo. Se corrigieron ambas opciones del cliente antes de reobservar contra HEAD y restaurar las fuentes propias.

**Capturas:** Cuenta 2, fondos de A5 10 y Agregar correo de otra cuenta A2 2, a 1440 y 390. Catorce comparaciones inspeccionadas, enviadas al usuario con vivo y verificadas, EXIT 0; FRONT `.local/pre7/a6/root-capturas*.`, `pre7/cuenta/`. Los capturadores terminaron antes del E2E real.

**Verde dirigido:** BACK `backend-green-final.*`, **77/77**, 0 fallos/omitidos, EXIT 0, 143,262 s (149,622 s del comando), después del rojo funcional. Incluye perfil, semilla, Google, baja, persistencia de ingreso y Me. Los intentos anteriores se conservaron: 50/56 y 50/57 por la preparación HTTP, y 73/77 por una zona de concurrencia no habilitada en el fixture. Unitarios **59/59**, EXIT 0, 3,110 s (`unit-final.*`); arquitectura afectada **45/45**, EXIT 0, 14,101 s (`architecture.*`); build AppHost **0 advertencias y 0 errores**, EXIT 0, 4,19 s (`apphost-build.*`). El primer unitario falló por acceso NuGet del sandbox y el segundo por dos dobles que implementaban el contrato anterior; ambos se conservan y el alcance se repitió con los dobles actualizados. FRONT `front-green-final-second.*`, **34/34** en seis archivos, EXIT 0, 13,70 s; el primer verde 25/27 y la repetición 31/31 en cinco archivos se conservan. Build final, lint dirigido, contrato regenerado y `contracts:check` EXIT 0; catálogo de Cuenta **1/1**, EXIT 0. Aspire previo `[]`, EXIT 0. E2E verificado abajo, antes de los commits propios.

**E2E real verificado:** selector `A6:,A5:,3b: cambiar el idioma`, **6/6**, 0 fallos/cancelados/omitidos, EXIT 0, **168,077 s** (169,036 s del comando), FRONT `.local/pre7/a6/real.*`. En los dos tamaños, cuatro combinaciones de las partes se guardan sin mandar el combinado, persisten tras F5 y se muestran como `DisplayName` o el contacto de respaldo en el menú. Pasan también el correo con su único código, principal/quitar/baja/cancelación de A5, vínculo Google por OAuth a 390 y cambio de idioma. Aspire después `[]`, EXIT 0, BACK `aspire-after.*`; el runner cerró su AppHost.

- [x] **Back:** BACK `de85e34`, 27 rutas propias; rojo funcional 12 fallos y 1 verde de 13, luego integración 77/77, unitarios 59/59 y arquitectura 45/45, EXIT 0. Build AppHost sin advertencias ni errores. Evidencias `.local/pre7/a6/` detalladas arriba.
  - nombre y apellido por separado en la identidad, con su migración (la primera palabra al nombre y el resto al apellido); **base cubierta por A7, `b3ffa40`**, porque la importación de Google necesita esos campos antes del editor de A6;
  - el nombre para mostrar se arma en un solo lugar. **Conservá `DisplayName` como ese nombre armado**, así no cambian las lecturas que hoy lo muestran: son unos 77 archivos del back y 42 del front. Solo cambian la identidad, la edición de `/api/me`, la migración, la semilla, Google y el formulario de Mi cuenta;
  - los contratos de `/api/me` y de las lecturas con nombres;
  - la semilla del operador;
  - rojo y verde de cada parte.
- [x] **Front:** FRONT `77a4039`, 16 rutas propias; rojo 2/2 fallos y verde final 34/34, EXIT 0. Build, lint dirigido, contrato regenerado/comprobado y catálogo de Cuenta 1/1 EXIT 0. Catorce comparaciones en `91ef0d5`, 16 rutas propias, verificadas y enviadas con vivo.
  - Mi cuenta con «Nombre» y «Apellido», como el tablero;
  - los tipos regenerados;
  - las pantallas que muestran el nombre siguen usando el nombre armado, sin juntarlo por su cuenta.
- [x] **E2E real:** guardar nombre y apellido en Mi cuenta y verlos en el menú. **6/6**, 0 fallos/cancelados/omitidos, EXIT 0, 168,077 s; `real.*` FRONT. Dos tamaños y regresiones A5/idioma; Aspire final `[]`, EXIT 0. Detalle arriba.

## A7 · Google trae el nombre y el apellido (ADR 0046, pedido por el usuario el 2026-10-06)

- [x] **Rojo** (integración, con el Google de prueba de los tests): observado el 2026-10-06, `GoogleNamesTests` **21 tests: 16 fallos y 5 verdes**, sin omitidos; EXIT 2, 73,753 s. Recorre callback HTTP con claims `GivenName`/`Surname` del proveedor de integración y un nombre combinado distinto. Reglas puras `GoogleNameRulesTests`: **4/4 fallan**, EXIT 2, 2,129 s. La migración antes de incorporar el backfill: **1/1 falla**, EXIT 2, 24,840 s. Evidencias BACK `.local/pre7/a7/integration-red.log`, `unit-red.log`, `migration-red.log` y sus `.exit`.
  - registrarse, ingresar o vincular Google completa el nombre y el apellido desde `given_name` y `family_name` si están vacíos;
  - si ya tienen algo, no los toca.
- [x] **Implementación:** donde se resuelve la cuenta de Google, sin pisar lo que escribió la persona. Verde dirigido **22/22** (Google y migración), EXIT 0, 79,211 s; reglas puras, perfil y consumidores **58/58**, EXIT 0, 2,588 s; integración y seguridad **83/83**, EXIT 0, 159,903 s; arquitectura **148/148**, EXIT 0, 18,529 s. Sin omitidos. Build de API **0 advertencias y 0 errores**, EXIT 0, 2,48 s. SQL diferencial, SQL idempotente, comprobación de modelo y `npm run contracts:check` EXIT 0. Evidencias `.local/pre7/a7/` de ambos repos. El primer alcance de arquitectura detectó una etiqueta E3 de la documentación desactualizada; se corrigió la ficha y se repitió completo en verde. Docs: `identidad.md`, reglas y arquitectura al día.
- [x] **E2E real de regresión:** `MT_E2E_ONLY_CASE=A4,3b`, `npm run test:e2e:real`, **9/9**, 0 fallos/cancelados/omitidos, EXIT 0, **347,685 s**. OAuth de ambos registros y recorridos de Mi cuenta con la nueva identidad/migración, front/API/PostgreSQL/pickup reales; se respetó el cooldown del destino. FRONT `.local/pre7/a7/real-a7.log` y `.exit`. Aspire final `[]`, EXIT 0. La importación de `given_name`/`family_name` se verifica en el callback HTTP con el proveedor de integración que autoriza A7; este E2E no inicia sesión en una cuenta real de Google.

**Base preparada para A6:** `FirstName` y `LastName` opcionales, máximo 100 por parte; `DisplayName` sigue persistido y se arma en la identidad, con capacidad para el combinado de 201. Migración `20261007003838_SplitUserNames`, tres archivos: divide datos existentes y conserva vacíos los campos personales de las cuentas eliminadas. El guardado legado del mismo combinado preserva los nombres compuestos de Google; una edición legado con partición inválida devuelve su error propio. Google avanza `ProfileVersion` solo cuando completa algún campo. A6 completó después los contratos/editor de `/api/me`, semilla y formulario, reutilizando esta base. BACK de A7 `b3ffa40`, 31 rutas propias; FRONT sin cambios en A7.

## A8 · «Ingresar» sigue fallando con una sesión vieja del operador (encontrado por el usuario el 2026-10-07)

**El problema:** después de A3, tocar «Ingresar» en el inicio sigue dando `access_denied` con `Tenancy.Access.PlatformOnly` y «No pudimos iniciar tu sesión».

**La causa:**
- El navegador del usuario guarda una sesión del operador **de antes de A3**, cuando había una sola cookie. Quedó en la cookie que ahora es la pública.
- `ConnectController.Authorize` (líneas 51 a 55) sigue devolviendo `Forbid` cuando la sesión guardada no puede usar la puerta pedida.
- Las pruebas de A3 arrancan con el navegador limpio y nunca tienen esa sesión vieja.

**La regla (vale para cualquier caso, no solo el operador):** una sesión guardada que no puede usar la puerta pedida nunca termina en un error sin salida.
- **Si el caso tiene su pantalla diseñada, se muestra:**
  - «Sin empresa», para `Tenancy.Access.NotMember`;
  - la organización inactiva, para `Tenancy.Member.Inactive`.
- **Si la sesión no debería existir,** como un operador en la cookie pública (ADR 0044), se descarta: `SignOutAsync` del esquema de esa puerta. Después se muestra el ingreso de esa puerta, como si no hubiera sesión.
- **«Volver a ingresar»** del error genérico tiene que llevar al ingreso de verdad, nunca de vuelta al mismo error.

- [x] **Rojo** (integración): `LegacyBrowserSessionTests`, dos fallos esperados y un verde de tres, EXIT 2, 33,197 s de MTP (67,165 s del comando); `.local/pre7/a8/red.log` y `.meta.json`. La cookie firmada del operador anterior a A3 llevaba a `access_denied` en ambas puertas, mientras «Sin empresa» ya funcionaba.
  - con una cookie pública que tiene a un operador (armada en el test como la dejaba la versión anterior), `GET /connect/authorize` de persona y de empresa redirige a `/login` y a `/login/empresa`, y borra la cookie pública;
  - después, el ingreso con otro correo anda;
  - con una persona sin empresa en la puerta de empresa, se sigue viendo «Sin empresa».
- [x] **Implementación:** en `Authorize`, cuando `SelectAccessAsync` falla con `PlatformOnly` en una puerta pública, se descarta la sesión de esa puerta y se sigue al redireccionamiento del ingreso. Lo demás queda igual. Verde dirigido 3/3, Auth 160/160 y arquitectura/seguridad 33/33, todos EXIT 0. Builds API, RealE2ESetup y AppHost sin advertencias ni errores; evidencias `.local/pre7/a8/`. Front: rojo 5 fallos y 44 verdes de 49, EXIT 1; verde 52/52, build y lint dirigido EXIT 0. El reintento genérico usa `prompt=login` y sólo el retorno del estado OIDC validado; conserva los textos y el diseño de Sesión.
- [x] **E2E real:** `MT_E2E_ONLY_CASE=A8`, `npm run test:e2e:real`, segundo intento **2/2**, 0 fallos/cancelados/omitidos, EXIT 0, **317,748 s** de Node; FRONT `.local/pre7/a8/real-second.*`. Antes de cada clic público conserva el ticket antiguo firmado con las claves reales de la base aislada: ambas puertas a 1440 y 390 muestran el formulario, entra otra cuenta y Plataforma sigue abierta. La portada se espera antes de conservar el ticket, para que la recuperación silenciosa no lo descarte antes de probar Empresa. Primer intento `A8,A1,A3`: A1 y A3 verdes, A8 cancelado al presupuesto inicial de 240 s durante el cuarto cooldown; se conserva en `real-first.*` y no se cuenta como aceptación. El nuevo caso largo usa 600 s, sin cambiar cuotas ni esperas. Sintaxis, lint dirigido y verificaciones focales conformes.
- [ ] **El recorrido de usuario de la puerta se hace en un navegador con sesiones de antes,** no solo en uno limpio.

**Hecho cuando:** en el navegador del usuario, tal como está hoy, «Ingresar» e «Ingresá como empresa» muestran el ingreso.

## A9 · El invitado entra con el código y acepta la invitación (ADR 0047, decidido por el usuario el 2026-10-07)

**El problema:** el Dueño invitado de una organización creada desde Plataforma ingresaba con su correo y veía «No hay una cuenta con ese correo».

**La decisión:** la [ADR 0047](../decisions/0047-el-ingreso-acepta-la-invitacion.md). Con una invitación vigente, el ingreso manda el código y, al verificarlo, crea la cuenta, acepta la invitación y la persona entra.

- [ ] **Rojo** (integración):
  - por la puerta de empresa, con invitación vigente: el código crea la cuenta, acepta y entra a esa organización;
  - por la puerta de persona: entra a Personal y la invitación queda aceptada;
  - vencida o revocada: «No hay una cuenta»;
  - con términos pendientes: los pide antes de entrar.
- [ ] **E2E real:** Plataforma crea una organización y el Dueño entra por «Ingresá como empresa» con el código, sin tocar el enlace.
- [ ] **Docs:** `identidad.md` e `invitaciones` en la ficha que corresponda.

## A10 · El mensaje del CUIT (encontrado por el usuario el 2026-10-07)

**El problema:** con `33333333333`, «Editar empresa» y el registro de una empresa («CUIT de la empresa») dicen «Revisá el CUIT: son 11 números.», aunque tiene 11. El error real es el dígito verificador: para `33-33333333-?` tendría que ser 9. `33333333339` es válido.

- [ ] Hay un mensaje por cada causa: el largo («son 11 números») y el verificador («Ese CUIT no es válido: revisá los números.»). Va en es y en, con su test.
- [ ] Va junto con R03 del [informe de la revisión](../reviews/2026-10-06-revision-de-codigo.md), que saca el CUIT argentino fijo del front: el mensaje sale del tipo fiscal, no de un texto fijo.

## A11 · Solo lectura con los campos grisados (decidido por el usuario el 2026-10-07)

**El pedido:** «Datos del rol» de un rol de sistema muestra rótulos y texto suelto. El usuario quiere los mismos campos que al editar, pero grisados.

**La decisión:** el mismo criterio en todo el sistema. Toda pantalla que muestra datos sin poder editarlos usa los campos del formulario deshabilitados, no texto suelto.
- Diseño lo suma a «Criterio · Formularios» y a los tableros de solo lectura, y se lo muestra al usuario.
- Se programa con esos tableros.

- [ ] **Front:**
  - una sola pieza o variante de `FormField` para solo lectura, en `shared/`;
  - aplicada en cada pantalla de solo lectura (el rol de sistema, y las demás que liste Diseño);
  - axe y 390.

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

## Verificación de cierre

Se ejecuta después del commit verificado de A6, con Aspire apagado para compilar y probar. Los capturadores visuales y el recorrido real se ejecutan por separado. Logs, EXIT y tiempo de cada comando: `.local/pre7/cierre/` del repo correspondiente.

| Repo | Comando | Resultado |
|---|---|---|
| Back | `dotnet build ArquitecturaBaseMultitenant.slnx` | Repetido después del fixture del recorrido real: EXIT 0, 0 advertencias y 0 errores; 4,99 s del build, 5,534 s del comando. `back-build.*`. Primer verde de 14,62 s conservado en `back-build-first.*`. |
| Back | `dotnet test` | Tercera ejecución completa con Docker y fixture actualizado: **1779/1779**, 0 fallos/omitidos, EXIT 0, **806,981 s** de MTP y 818,006 s del comando; `back-test.*`. Segunda verde de 1779/1779 conservada en `back-test-second.*` (826,153 s). Primer intento 1778/1779, 1 fallo de etiquetas de etapa de la ficha, EXIT 2, 732,825 s; ficha corregida y HarnessTests 10/10, EXIT 0, 4,791 s. `back-test-first.*`, `harness-green.*`. |
| Front | `npm run build` | EXIT 0, 22,876 s del comando; Vite 1,80 s. `front-build.*`. Repetido tras las dos correcciones de tests: EXIT 0, 23,371 s del comando, Vite 1,72 s; `.local/pre7/cierre-submit-buttons/build-navigation.*`. |
| Front | `npm run lint` | Repetido tras el runner: EXIT 0, 0 errores y 752 advertencias existentes de bundles históricos de Diseño y efectos; 1,203 s. `front-lint.*`; verdes anteriores en `front-lint-first.*` y `front-lint-second.*`. No se modifica el lienzo. |
| Front | `npm test` | Quinto intento, en exclusiva: **2056/2056**, 235 archivos, EXIT 0, **121,45 s** de Vitest y 123,017 s del comando; `front-test.*`. Tercer verde conservado en `front-test-third.*` (123,99 s). Cuarto intento **2049 verdes y 7 fallos**, EXIT 1, 144,00 s; `front-test-fourth.*`: seis timeouts de 5000 ms (cinco casos incluyen axe) y Quitar sin menú. MSW recibió un POST Verificar del caso anterior durante Quitar, después de su timeout. Los cinco archivos afectados repitieron sin cambios **141/141**, EXIT 0, 31,23 s (`affected-exclusive.*`); luego pasó el full, sin ampliar límites. La carga concurrente del back no quedó aislada como causa; los gates pesados se ejecutan por separado. Primer full 2039/2040, EXIT 1: checker rechazaba `sendRetry.isRunning` legítimo por 429/Retry-After; corregido por origen léxico del hook, sin excepción por pantalla y rechazando alias falsos/sombreado (rojo 11 fallos/9 verdes, verde 23/23). Segundo full 2055/2056, EXIT 1: el test encontraba el input de Ingreso antes de montar Registro; ahora espera su título distintivo, conserva las comprobaciones y no agrega sleeps ni timeout. Focal conjunto 33/33, build/lint EXIT 0. Evidencias `.local/pre7/cierre-submit-buttons/`, `front-test-first.*`, `front-test-second.*`. |
| Front | `npm run contracts:check` | EXIT 0; tipos TypeScript coinciden con OpenAPI. `front-contracts.*`. |
| Front | `npm run test:e2e:real` | Segundo completo sin selector: **30/30**, 0 fallos/cancelados/omitidos, EXIT 0, **1295,875 s** de Node y 1296,762 s del comando; `real-full.*`. Front, API, PostgreSQL aislado y pickup reales, sin mocks. A5 cadena 65,276 s y Google 390 13,754 s; 5b 109,408 s; A1 140,569 s y A3 63,204 s. Aspire final `[]`, EXIT 0 (`aspire-after-real-second.*` BACK). Primer completo **27 verdes, 2 fallos (padre y A5), 1 cancelado (5b) de 30**, EXIT 1, 1847,806 s; `real-full-first.*`. A5 comprobaba la desaparición global del correo antes de terminar las relecturas de métodos/usuario; el log no distingue fila de menú lateral. 5b agotó 600 s y seguía ejecutando después de cancelar. El operador tenía cinco emisiones previas frente al límite de cinco por quince minutos: compatible con el timeout, sin un 429 de 5b registrado. Se corrigieron espera DOM y principal original, operadores propios por recorrido, progreso del reenvío y cancelación con limpieza antes del siguiente caso. Se conservan cuotas/timeouts y ambos intentos; rojo/verde del ciclo de vida abajo. |
| Back | `node scripts/arneses/arnes.mjs realinear-docs --detectar --repo back` | Repetido con los archivos del runner documentados: EXIT 0, auto 0/modelo 0/humano 0, 1,862 s; 0 tokens, «Para vos»: nada. `docs-back.*`, `docs-back-resumen.md`. |
| Front, desde el CLI del back | `node scripts/arneses/arnes.mjs realinear-docs --detectar --repo front` | Repetido con los archivos del runner documentados: EXIT 0, auto 0/modelo 0/humano 0, 2,133 s; 0 tokens, «Para vos»: nada. `docs-front.*`, `docs-front-resumen.md`. |

**Correcciones del cierre:** ciclo de vida del runner, rojo funcional rápido **7 fallos y 2 verdes de 9**, EXIT 1, 130,207 ms; prueba adicional de cierre pendiente **1 fallo y 9 verdes de 10**, EXIT 1, 134,487 ms. Verde final **10/10**, 0 fallos/cancelados/omitidos, EXIT 0, 133,309 ms. El ámbito asíncrono cancela las esperas y cierra solo los contextos de su caso; espera también un cierre que ya emitió su evento y drena el trabajo antes del siguiente recorrido. Logs `runner-fix/node-red-fast.*`, `node-close-red.*`, `node-final.*` FRONT. Sintaxis de las tres rutas MJS, lint dirigido y diff-check EXIT 0; build de RealE2ESetup 0 advertencias/errores, EXIT 0, 6,41 s (`runner-fix/build-tool.*` BACK). Commits posteriores al E2E completo verde: BACK `630185e`, una ruta propia del fixture; FRONT `71aa16e`, tres rutas propias del runner; FRONT `b89a517`, los dos tests del checker y del montaje de Registro; FRONT `fec2a86`, su árbol actualizado. Las fichas, árbol y este registro del back se cierran juntos por sus tres rutas propias. Índices vacíos después de cada commit, `main`, sin push.

**Recorrido manual real:** pendiente, **0/16 verificaciones** (ocho pasos en cada tamaño, en el mismo navegador y sin limpiar sesiones). La revisión automática rechazó abrir `https://127.0.0.1:5174/alive` en el navegador integrado por considerarlo una posible evasión del bloqueo previo de `localhost`. Se pidió autorización humana específica y no se intentó otro acceso para evitar ese rechazo. Este bloqueo no reemplaza ni da por bueno el recorrido manual; los demás arreglos y verificaciones continúan.

## Registro de resultados

Se completa al verificar cada arreglo. Los logs locales conservan la salida y el código de cada comando; las capturas comparan el tablero v112 (`9c1c782`) con la app a 1440 y 390. Se trabaja en `main`, sin push, sin tocar `docs/design/lienzo/` y sin iniciar la revisión de código ni la Etapa 7.

| Arreglo | Rojo | Verde y alcance afectado | E2E real | Commits y capturas |
|---|---|---|---|---|
| A3 | 18 tests: 8 fallos, 10 verdes, 0 omitidos; EXIT 2; `.local/pre7/a3/integration-red.log`. | Dirigido 18/18; alcance inicial 284/286 con dos preparaciones HTTPS corregidas; final cambiado 58/58. Arquitectura 148/148; Application 28/28; build back 0 advertencias/errores; build front, lint dirigido y sintaxis del runner EXIT 0. | Focal `A3`: 2/2, 0 fallos/cancelados/omitidos, EXIT 0, 109,7 s. Dos pestañas y ambas salidas; FRONT `.local/pre7/a3/real-a3.log`. | BACK `30aea96`; FRONT `ac6a177`. No cambia una pantalla: convivencia verificada con las pantallas reales. |
| A4 | 2 fallos y 16 verdes; EXIT 1; `.local/pre7/a4/unit-red.log` FRONT. | Google y registros 23/23; ingreso 30/30; build y lint dirigido EXIT 0 (3 advertencias previas en efectos de BusinessSignupPage). | Focal `A4`: 2/2, 0 fallos/cancelados/omitidos, EXIT 0, 65,7 s. Ambos registros llegan a Google sin correo escrito. Primer arranque fallido en sandbox preservado; repetición autorizada verde. | FRONT implementación `1865921`; capturas y manifiesto `d905057`: `docs/design/capturas/consolidacion/pre7/registro/`, 1440 y 390. |
| A5 | Back 4 fallos y 1 verde, EXIT 2; front 5/5 fallan, EXIT 1; `.local/pre7/a5/*red.log`. | Integración 73/73; seguridad/persistencia 34/34; arquitectura 148/148; dominio 12/12; migración final 1/1. Front 39/39 más API 3/3; catálogo 10/10. API build 0 advertencias/errores; front build, lint dirigido, contrato y sintaxis EXIT 0. Primer fallo de grants corregido en la migración y repetido. | Focal `A5`: tercer intento **3/3**, 0 fallos/cancelados/omitidos, EXIT 0, 137,947 s; `real-a5-third.log`. Dos preparaciones fallidas corregidas y preservadas; cadena de Mi cuenta y OAuth Google a 390. Aspire `ps=[]`, EXIT 0. | BACK `6b07c3e`; FRONT `544bfe6`; referencia actual de tests de la ficha Formularios corregida en `bee993e`. Diez comparaciones `ea434bc` en `docs/design/capturas/consolidacion/pre7/cuenta/`, a 1440 y 390. El OTP antiguo del tablero se conserva como referencia y la diferencia explícita A5/ADR0045 se documenta. |
| A7 | Google HTTP 16 fallos y 5 verdes de 21; puras 4/4 fallan; migración 1/1 falla; todos EXIT 2; BACK `.local/pre7/a7/*red.log`. | Dirigido 22/22; puras/perfil/consumidores 58/58; integración/seguridad 83/83; arquitectura 148/148; build API 0 advertencias/errores. SQL diferencial/idempotente, modelo y contrato EXIT 0. Primer fallo de etiqueta documental corregido y repetido. | Focal `A4,3b`: 9/9, 0 fallos/cancelados/omitidos, EXIT 0, 347,685 s; `real-a7.log`. Claims auténticas verificadas por proveedor de integración HTTP, sin simular ingreso real a Google. Aspire final `[]`, EXIT 0. | BACK `b3ffa40`, 31 rutas; FRONT sin cambios. Base de identidad y migración de A6 cubierta; su editor y capturas siguen en A6. |
| A1 | Front 49: 10 fallos, 39 verdes, 0 omitidos, EXIT 1; `unit-red.log`. Catálogo dirigido 1/1 falla antes de corregir la variante móvil. | Pantalla/salida 55/55, auth/traducciones 16/16, catálogo 11/11; build, lint y sintaxis EXIT 0. Cuatro comparaciones revisadas y verificadas. Primer build con tres errores de tipos de tests corregido. | Focal `A1,A3`, segundo intento 3/3, 0 fallos/cancelados/omitidos, EXIT 0, 258,090 s; `real-a1-a3-second.log`. Primer intento fallido conservado y carrera del helper corregida. Aspire `[]`, EXIT 0; stop adicional 7 sin AppHost. | FRONT `a6782b0` (13 rutas), BACK `d15b19b` (ficha). `pre7/ingreso/`: Persona y Empresa, 1440 y 390; primeras capturas móviles incorrectas preservadas en `.local/pre7/a1/`. |
| A2 | Backend funcional 6/6 fallos, EXIT 2, 31,094 s; frontend 5 fallos y 2 verdes de 7, EXIT 1, 14,43 s. StrictMode 1 fallo y 9 filtrados, EXIT 1, 7,34 s. Los rojos de preparación no se cuentan como conducta. | Back 6/6, integración 91/91 más 20/20, unitarios 48/48, arquitectura afectada 47/47, consulta sin escrituras final 6/6. Build AppHost 0 advertencias/errores. Front final 109/109, catálogo 13/13, build/lint/contrato EXIT 0. Doce comparaciones revisadas y verificadas. | Cuarto original A2,A1,A3,reclamo: 9/9, 0 fallos/cancelados/omitidos, EXIT 0, 575,132 s; real-fourth.*. Tres intentos fallidos preservados: cuota de preparación y StrictMode corregidos; fallo intermitente A1 del tercero no reproducido en cuatro diagnósticos ni en el cuarto, sin atribuir corrección causal. Aspire final [], EXIT 0. | BACK 962a71c (37 rutas), FRONT 0249b8a (19); capturas 403472d (14). Seis estados por 1440/390 en pre7/ingreso, registro, recuperar y cuenta. Fondo de Cuenta repetido tras A6 en 91ef0d5. |
| A6 | Back funcional 12 fallos y 1 verde de 13, EXIT 2, 43,350 s; front 2/2 fallos, EXIT 1, 13,70 s. Preparaciones HTTP 404/HTTPS preservadas y excluidas del rojo funcional. | Integración 77/77, unidad 59/59, arquitectura afectada 45/45; AppHost sin advertencias/errores. Front final 34/34, catálogo 1/1, build/lint/contrato EXIT 0. Primeros verdes fallidos corregidos en preparación HTTP, zona de concurrencia y dobles de interfaz. | Focal A6,A5,idioma: 6/6, 0 fallos/cancelados/omitidos, EXIT 0, 168,077 s; real.*. Nombres opcionales/compuestos a ambos anchos, F5 y menú; acciones de cuenta e idioma. Aspire final [], EXIT 0. | BACK de85e34 (27 rutas), FRONT 77a4039 (16); capturas 91ef0d5 (16). Cuenta dos imágenes y repetición de diez A5 más dos A2, todas inspeccionadas/enviadas/verificadas. Migración y Google siguen la base A7 b3ffa40. |

| A8 | Back 2 fallos/1 verde de 3, EXIT 2; front 5 fallos/44 verdes de 49, EXIT 1. Logs de ambos repos .local/pre7/a8/. | Back dirigido 3/3, Auth 160/160, arquitectura/seguridad 33/33; API, fixture y AppHost 0 advertencias/errores. Front 52/52, build/lint/sintaxis EXIT 0; ciclo de vida del runner 10/10. | Segundo focal A8 2/2, EXIT 0, 317,748 s; ticket histórico firmado antes de cada clic, ambas puertas y tamaños, ingreso posterior y Plataforma conservada. Primero cancelado en cooldown cuarto a240s, A1/A3 verdes; preservado sin darlo por bueno. | Commits propios de implementación y evidencia al cerrar A8; seis comparaciones en FRONT docs/design/capturas/consolidacion/pre7-a8/. Prueba final en navegador persistente pendiente hasta cerrar A9/A10. |
