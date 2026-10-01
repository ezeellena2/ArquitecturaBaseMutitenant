# Etapa 3c · Invitaciones — informe de cierre

Estado: **cerrada**. `HarnessStage` queda en 3 en los dos repositorios. Se trabajó en `main`, sin push y con commits por rutas explícitas. La base Development no recibió cuentas ni organizaciones de prueba; las integraciones y el E2E usaron sus propias bases temporales.

## Tareas y commits

| Tarea | Resultado | Commit(s) |
|---|---|---|
| Plan previo | Plan TDD detallado, revisado y comprometido antes de programar | back `7934f42` |
| T01 · Dominio | Invitation y Member(Invited), expiración/consumo único; 13/13 | back `abbd59d` |
| T02 · Persistencia | Reserva sin identidad, EF/RLS, migración e índice de accesos; 10/10 integraciones | back `a332c2b` |
| T03 · Emisión y correo | Issuer dentro de la UoW del llamador, canal Email por outbox, renderer real es/en; 28/28 focales | back `1ce2d21`, `82c09c9` |
| T04 · E2E rojo previo | Preparador aislado y pruebas reales con invitación nueva/existente: ambas llegaron al correo y fallaron porque aún no había preview | back `d329ccd`; front `1a62898` |
| T05 · Vista previa | Ocho estados, guard de token, organización privada y continuación HttpOnly; 15 unitarios + 9 integraciones | back `5170d21` |
| T06 · Aceptación | Cuenta nueva sin Personal, cuenta previa y aceptación atómica; 42 integraciones focales | back `861463c` |
| T07 · Respaldo y baja | Metadata de origen, regla de respaldo y revocación de invitaciones recibidas en baja; pruebas de baja | back `9163854`; informe intermedio `badfcb1` |
| T08 · API y contrato cruzado | `POST /preview` y `/accept`, antiforgery, idempotencia, rate limit, retorno de ingreso `/invitacion`; el contrato lee ambos repos | back `81a01fd`, `9ad21fc`; front `3849299`, `ce8a08a` |
| T09 · Pantalla | `/invitacion`, sus estados y recursos; acceso Business persistente, sin crear Personal | front `6692111`, `94ce987` |
| T10 · E2E verde y ajustes | Aserciones del E2E y verificaciones técnicas de cierre; pruebas dirigidas pasan | front `c4ace06`; back `7853d2a` |
| T11 · Comparación visual | 30 pares app/lienzo, correos HTML del renderer y manifest verificado | front `ed1e7d4` |
| T12 · Cierre | Fichas, arquitectura, OpenAPI, feature, backlog, plan, informe y HarnessStage 3; puerta completa | front `073ac0b`; back `4b32337` |

## TDD y puerta completa

T04 dejó el rojo real pedido: antes de implementar preview y aceptación, los dos recorridos de invitación leyeron un enlace real del `.eml` y fallaron al abrir la vista previa; los siete recorridos de cuenta anteriores pasaron. La salida resumida fue:

```text
✖ 3c: aceptar invitación sin cuenta y sin Personal desde el .eml (20922.3212ms)
✖ 3c: aceptar invitación con cuenta previa desde el .eml (19869.5576ms)
tests 10 · pass 7 · fail 3 · cancelled 0 · skipped 0 · todo 0
duration_ms 685282.0408
```

El tercer fallo era el padre de esos dos casos. El mismo E2E completo termina ahora con 10/10 correctos, cero fallos y cero omitidos (salida final abajo).

Los tests focales finales de preview/emisión/aceptación pasan 31/31. Las guardas arquitectónicas existentes pasan 40/40. La suite completa `dotnet test` pasó **1.182/1.182**, con cero errores y cero omitidos; incluye `HarnessTests`. `dotnet build ArquitecturaBaseMultitenant.slnx --no-restore` terminó con **0 advertencias y 0 errores**. La primera invocación dentro del sandbox no pudo resolver el SDK de Aspire por falta de acceso a su caché/NuGet; después de restaurar, el build final fuera del sandbox pasó sin advertencias.

En el front, `npm test` pasó 669 pruebas en 136 archivos; `npm run build` y `npm run contracts:check` terminaron correctamente. `node --test scripts/datos-de-referencia/generar.test.mjs` pasó 30/30. `node scripts/capturar-etapa-3c.mjs --verify` informó `30 pares visuales 3c completos.`. `npm run lint` terminó con código 0 y 746 warnings: 745 pertenecen a `docs/design/lienzo-v35`; uno está en `InvitationPage` por sincronizar el fragmento a estado local después de retirarlo de la barra de direcciones. Se agregó al backlog para refactorizar sin permitir que el token se consulte mientras sigue en la URL; no bloquea el flujo ni la etapa.

Después del E2E, `aspire ps` respondió `No running AppHost found`; el runner limpió su AppHost y su pickup temporales.

### Salida literal sanitizada de `npm run test:e2e:real`

La salida no incluye correos, enlaces, tokens ni códigos de prueba.

```text
> arquitecturabase-multitenant-front@0.0.0 test:e2e:real
> node --test scripts/test-e2e-real.test.mjs

▶ registro real y puerta empresa usan un PostgreSQL aislado, front, Api y pickup sin mocks
  ✔ 3c: aceptar invitación sin cuenta y sin Personal desde el .eml (9544.4659ms)
  ✔ 3c: aceptar invitación con cuenta previa desde el .eml (19301.1667ms)
  ✔ 3b seguridad: sesión abierta no suma correo ni Google y método nuevo no autoriza el anterior (78637.1835ms)
  ✔ 3b seguridad: registro con posesión recupera un correo pendiente de otra cuenta (151081.8513ms)
  ✔ 3b: sumar correo personal con código leído del .eml (78840.9377ms)
  ✔ 3b: quitar un método con código enviado a otro (130095.1357ms)
  ✔ 3b: pedir baja y cancelarla ingresando durante la gracia (130232.2955ms)
  ✔ 3b: cambiar el idioma a en-US desde /cuenta (11538.3669ms)
  ✔ 3b: aceptar versión nueva de términos que bloquea el ingreso (10394.0953ms)
✔ registro real y puerta empresa usan un PostgreSQL aislado, front, Api y pickup sin mocks (671525.9524ms)
ℹ tests 10
ℹ suites 0
ℹ pass 10
ℹ fail 0
ℹ cancelled 0
ℹ skipped 0
ℹ todo 0
ℹ duration_ms 671978.4998
```

## Comparación con el lienzo

Se inspeccionaron Invitacion y M-Invitacion con un elemento a 1440×900 y 390×844 antes de programar. La composición era legible, sin elementos deformados ni desborde; no hizo falta pedir alternativas. Al finalizar se compararon los ocho estados estables, tres transitorios y cuatro variantes del correo en ambos tamaños: 30 pares completos, sin desborde horizontal a 390 px.

| Referencia | Diferencia observada | Motivo y resolución |
|---|---|---|
| Invitacion y M-Invitacion | Empresa, Roles y rol de quien invita no aparecen | Esos modelos/API nacen en E6/E4; no se inventan valores en 3c. |
| Aceptación | No aparece el enlace WhatsApp del lienzo | El único canal de 3c es Email. WhatsApp corresponde a E8. |
| Fecha de vencimiento | El lienzo dice “En 6 días”; la pantalla presenta fecha civil localizada | Se conserva el DisplayFormatter compartido, que muestra fecha absoluta futura para no insinuar vigencias distintas por redondeo. |
| Carga, envío y error | Los tableros no muestran estados transitorios | Se fotografió el estado real de la app y se comparó con la tarjeta estable correspondiente; no se agregó un estado ficticio al lienzo. |
| Mensajes · correo | El ejemplo usa “ArquitecturaBase”, “Delta Norte S.A.” y texto español fijo | El renderer real usa datos actuales escapados, resources es/en y la fecha/localización de la organización. El aviso de código aparece solo para un correo Email verificado y disponible; no se promete código para Google-only ni para una cuenta nueva. |

Los PNG, HTML del renderer, manifest y notas están en el [informe de capturas del front](../../../ArquitecturaBaseMutitenantFront/docs/design/capturas/etapa-3c/informe.md). Las capturas usan respuestas sintéticas del arnés visual y no reemplazan el E2E real.

## Decisiones tomadas

- La invitación nueva crea una identidad ya vinculada a la organización, guarda los consentimientos vigentes y no crea espacio Personal. La primera entrada Consumer crea Personal; una cuenta existente debe autenticarse como su dueña y el enlace nunca la ingresa automáticamente.
- `InvitationIssuer` es un helper interno dentro de la UoW del llamador, sin ruta propia. En 3c emite solo por Email/outbox; se dejan WhatsApp, empresa/roles y las acciones administrativas de Usuarios para E8/E4/E6 según el plan.
- El token va en el fragmento del enlace y se retira antes de consultar; preview y accept lo reciben en el cuerpo JSON. La base guarda solo su hash. Una continuación HttpOnly permite volver del ingreso y, para una identidad nueva, recuperar durante cinco minutos una aceptación cuya respuesta se perdió, únicamente desde el navegador original.
- Se mantiene la fecha futura absoluta localizada del formatter común aunque el lienzo use texto relativo. El correo no afirma que una cuenta nueva recibirá un código si el único medio de prueba fue el enlace.
- No se implementó parser telefónico en 3c: no hay flujo telefónico que lo consuma; la ficha sitúa parser y tests con el primer flujo telefónico de E8. La clasificación de la ficha se actualizó para no marcar como faltante de E3 un test fuera de alcance.
- La validación usa el límite de token central de `InvitationOptions`; la aceptación agrupa dependencias técnicas para respetar el límite existente de la capa. La política para «Salir y seguir» quedó dentro del servicio de logout, que conserva solo la continuación protegida y rota su nonce; logout común la borra.
- El barrido de organizaciones durante una baja sigue lineal porque las invitaciones pendientes aún no están vinculadas a una identidad y no deben exponerse mediante un índice global. Su optimización queda en backlog solo si una medición muestra impacto.

El recorrido manual de invitar desde Usuarios sigue expresamente en la puerta de E6. No se inició E4 ni E6.
