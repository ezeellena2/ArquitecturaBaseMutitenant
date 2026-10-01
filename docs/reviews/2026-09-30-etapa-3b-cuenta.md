# Etapa 3b · La cuenta — informe de ejecución

Estado: Etapa 3b completa. La puerta automática pasó, el E2E real final dio 8/8 y la comparación visual incluye las dos nuevas reautenticaciones. Las diferencias menores están documentadas con su motivo. La 3c no se inició y conserva su propia puerta.

## Tareas y commits

| Tarea | Commit back | Commit front | Evidencia |
|---|---|---|---|
| Plan previo | `c60cd18` | — | 30 tareas, TDD, alcance y matriz visual |
| T01 · recorridos reales rojos | — | `20e2e8b` | Los cinco fallaron antes de implementar /cuenta |
| T02 · reglas de métodos | `61ffe6c` | — | Rojo CS1501/CS1061; verde LoginMethodTests 6/6 |
| T03 · comprobante de reautenticación | `414cb47` | — | Rojo CS0103; verde ReauthTicketTests 4/4 |
| T04 · persistencia de cuenta | `5949ffb` | — | Rojo CS1061; Testcontainers LoginMethodsTests 3/3; modelo sin cambios pendientes; SQL revisado |
| T05 · avisos de cuenta | `5eebba1` | — | Rojo CS0234; Testcontainers AccountNoticeTests 2/2; contactos deduplicados, payload cifrado y aviso final principal |
| T06 · agregar y verificar correo | `0d6f226` | — | Rojo CS0246/CS0117; LoginMethodsTests 5/5 y plantillas: total 17/17; código Login rechazado, intento guardado, código propio consumido y reserva global |
| T07 · quitar y elegir principal | `ba150f2` | — | Rojo CS0246/CS1061; integración 8/8, inventario existente de identidad 3/3; contexto/replay/vencimiento, último método y principal/copias atómicos |
| T06 bis · aislar registro y correo pendiente | `2097c3b` | — | Rojo: 204 con cookie en lugar de 400; SignupTests 8/8; registro no confirma métodos pendientes agregados desde otra cuenta |
| T08 · vincular Google | `457f569` | — | Rojo CS0246; Google 13/13 + protocolo 1/1; sesión distinta/correo no verificado/conflicto rechazados; OAuth state protegido y antiforgery |
| T09 · HTTP de métodos y reautenticación | `38112e8` | — | Rojo: 404 en rutas inexistentes y código sin traducir bajo campo; HTTP + inventario 5/5; 401, 404 ajeno real, replay y verificación pickup |
| T10 · perfil versionado y aviso | `a533f5c` | — | Rojo: falta version y luego 204 en vez de 409; integración 19/19, unit 24/24; guardado viejo conserva datos e idioma; aviso usa disponibilidad |
| T11 · términos bloqueantes | `6d8be73` | — | Rojo: 200 en vez de 403; integración 8/8 y perfil unit 13/13; versión nueva entre lectura/aceptación devuelve 409, aceptación append-only y repetible |
| T12 · reglas de baja y gracia | `0fbce93` | — | Rojo CS1061/CS0103; AccountDeletionPolicyTests 6/6; operador, pendiente, suspensión, límite inclusivo y anonimización |
| T13 · participantes de eliminación | `e5bb79c` | — | Rojo CS0246/CS1061 y falta de grant 42501; runtime 1/1 + inventarios existentes 12/12; limpieza doble, auditoría única, mensajes ajenos intactos y DELETE B2B devuelve cero |
| T14 · pedir baja y revocar sesiones | `ebce448` | — | Rojo: 404; integración 7/7; operador/sin ticket rechazados, módulo conserva estado y ticket, tokens Consumer y Business revocados y cookie cerrada |
| T15 · ingreso de gracia | `42fb0c4` | — | Rojo: no .eml, metadata y cookie ausentes; integración 20/20. Ticket cinco minutos, puerta preservada, cookie HttpOnly Secure sin secreto en URL, expiración servidor |
| T16 · cancelar baja | `a487445` | — | Rojo 404; integración 13/13; returnUrl consumer/business, replay 403, vencimientos sin cambios y sesión previa 401 |
| T17 · eliminación final | `6bdd516` | — | Rojo CS0246; integración 3/3, ampliación 2/2; lease excluye segundo reclamo, fallo tras Personal cerrado se recupera, Pending/Suspended terminan Deleted, un evento y aviso final. Inventario bulk existente de T04 se ajusta nominalmente en T18 |
| T18 · contratos cruzados | `97e7469` | `0c315cc` | Rojo ENOENT; 2/2 verdes, schema regenerado, contracts:check y tsc verdes; inventarios existentes back 13/13 |
| T19 · clientes y recursos | — | `690aade` | Rojo clientes ausentes; API y paridad 6/6, tsc y lint verdes |
| T20 · perfil y menú | — | `303d767` | Rojo ruta 404, menú ausente y versión faltante; 20/20 funcionales y axe, build verde; 409 conserva draft |
| T21 · tabla y aviso | — | `013aeb2` | Rojo componente/aviso ausentes; 8/8 cuenta/home, build/lint verdes |
| T22 · agregar y verificar | — | `f58c52e` | Rojo import ausente; 10/10 verdes, OTP incorrecto conserva diálogo, código corregido confirma |
| T23 · cambiar método y Google | — | `8771cac` | Rojo import ausente; 11/11 verdes, prueba usa respaldo y DELETE ticket en cuerpo, tsc/lint verdes |
| T24 · aceptación bloqueante | — | `296c8d4` | Rojo pantalla/gate ausentes; casos de aceptación y httpClient 12/12 verdes, tsc/lint verdes |
| T24 bis · F5 conserva puerta | — | `f940874` | Rojo recuperaba consumer en /cuenta desde Business; 11/11 recuperación/auth/ruta verdes |
| T25 · pedir baja | — | `3209dfb` | Rojo diálogo ausente; 23/23 cuenta/home/shell verdes; motivo y código obligatorios, sesión cerrada y fecha |
| T26 · cancelar durante gracia | — | `053b4e6` | Rojo estado genérico; LoginPage 21/21 verdes por correo/Google sin sesión previa a cancelación |
| T23 bis · cooldown de prueba | — | `0778296`, luego `3c05b47` | Primera corrección superada: el reintento final es manual, según E1; test rojo → verde |
| T27 · recorridos reales | `fc66c58` | `a50b5b0`, `3c05b47` | Cinco recorridos inicialmente rojos, final real 6/6; reintento 429 explícito |
| T28 · comparación y correos | `165d927`, `8b054d1` | `59dc2cd`, `f81d2eb` | 118 pares revisados; validación incompleta 3 rojos → 4 verdes; preview 1 rojo → 4 verdes; Salir ocupado rojo → verde |
| T29 · documentación y recorrido | `d143c0f` | — | Identidad, manual operador/segunda Gmail, publicación legal local y limpieza por gracia |
| T30 · puerta e informe | `1b96685`, `5f95acd` | `f81d2eb` | Puerta inicial: back 1110/1110, front 655/655 y E2E 6/6. La puerta final ampliada está en el addendum de seguridad |
## E2E real inicial de T01–T30 (30/09)

`npm run test:e2e:real`, 30/09/2026. AppHost y PostgreSQL efímero exclusivos; datos del preparador y de /registro en esa base. Registro/puerta empresa/F5/cambio Personal/logout de 3a se recorrieron antes de los casos nuevos.

```text
✖ 3b: sumar correo personal con código leído del .eml (18744.9227ms)
✖ 3b: quitar un método con código enviado a otro (19940.3442ms)
✖ 3b: pedir baja y cancelarla ingresando durante la gracia (19921.7423ms)
✖ 3b: cambiar el idioma a en-US desde /cuenta (20112.2356ms)
✖ 3b: aceptar versión nueva de términos que bloquea el ingreso (19950.0975ms)
Error en cada caso: La cuenta 3b: /cuenta no muestra la pantalla Mi cuenta.
tests 6 · pass 0 · fail 6 · cancelled 0 · skipped 0 · duration_ms 140946.0782
```

La sexta prueba es el padre, que falla porque sus cinco subrecorridos fallan. Se mantienen activas: el resultado final debe ser verde en todos. El runner cerró su AppHost y eliminó el pickup propio al terminar.

## Puerta inicial de T01–T30

Comandos ejecutados en el repo dueño el 30/09/2026. Docker activo para integración y E2E; ninguna cuenta fixture se crea en Development.

| Punto general | Resultado y evidencia |
|---|---|
| 1 · build y tests back | `dotnet build ArquitecturaBaseMultitenant.slnx`: 0 advertencias/0 errores. `dotnet test` final con Docker: 1110/1110, 0 errores/0 omitidos, 3m 12s 828ms. Plantillas después del ajuste final: 12/12 |
| 2 · front | `npm run lint`: exit 0; `npm test`: 134 archivos y 655 tests en verde; `npm run build`: exit 0. Después del último ajuste de presentación: 36/36 dirigidos, lint/build verdes |
| 3 · rutas | Inventarios existentes actualizados; cada ruta nueva ejercida en HTTP/integración. ArchitectureTests 153/153, incluidos inventarios de accesos, rutas y transacciones |
| 4 · aislamiento | Incluido en suite completa Docker; métodos ajenos 404, tickets ligados a cuenta/acción/objetivo, DELETE runtime limitado a Personal y datos de otro usuario intactos |
| 5 · formatos | Preferencias/cultura/zona desde catálogos; fechas de baja y avisos usan formateador central con cultura/zona explícitas. Paridad de formatos incluida en suites; ejemplo del selector usa los mismos formateadores |
| 6 · contratos | OpenAPI y schema TS regenerados. `npm run contracts:check`: `Generated TypeScript schema matches OpenAPI.` Dos tests cruzados leen ambos repos: rutas, claims, redirecciones y códigos |
| 7 · generador y arnés | Generador 30/30; JSON oficiales idénticos byte a byte. HarnessTests back 10/10; `harness.test.ts` incluido en 655. Arnés existente de capturas 20/20. HarnessStage permanece 2 hasta cerrar 3c |
| 8 · apagado | `aspire stop` y `aspire ps`: `No running AppHost found.` Servidor visual detenido al terminar. Main en ambos repos, commits locales con rutas explícitas, sin push |
| 9 · comparación visual | En la puerta inicial: 118 pares presentes, 236 PNG, 14 HTML y 10 hojas; diferencias y motivo documentados |
| 10 · E2E real | En la puerta inicial: 6/6; base efímera E2E, navegador/API/pickup reales, sin mocks. La ampliación final de seguridad está abajo |

Puerta propia 3b: agregar correo personal leyendo código `.eml`, quitar otro método con prueba en respaldo, aceptar versión legal nueva bloqueante, pedir baja/cancelar al ingresar durante gracia y guardar `en-US` en `/cuenta`: **todos verdes**. Se mantuvieron registro, puerta Empresa, F5, cambio a Personal y logout de 3a.

```text
> arquitecturabase-multitenant-front@0.0.0 test:e2e:real
> node --test scripts/test-e2e-real.test.mjs

▶ registro real y puerta empresa usan un PostgreSQL aislado, front, Api y pickup sin mocks
  ✔ 3b: sumar correo personal con código leído del .eml (16834.3814ms)
  ✔ 3b: quitar un método con código enviado a otro (71845.841ms)
  ✔ 3b: pedir baja y cancelarla ingresando durante la gracia (128263.1623ms)
  ✔ 3b: cambiar el idioma a en-US desde /cuenta (12456.6686ms)
  ✔ 3b: aceptar versión nueva de términos que bloquea el ingreso (10141.4868ms)
✔ registro real y puerta empresa usan un PostgreSQL aislado, front, Api y pickup sin mocks (294189.9573ms)
ℹ tests 6
ℹ suites 0
ℹ pass 6
ℹ fail 0
ℹ cancelled 0
ℹ skipped 0
ℹ todo 0
ℹ duration_ms 294732.1232
```

La suite front emite avisos conocidos de jsdom sobre canvas/pseudoelementos; no son fallos ni advertencias del build. No se agregó una dependencia o test del arnés para silenciarlos. Build/lint y todos los casos requeridos pasaron.

## Diferencias con el lienzo

Evidencia: [informe visual](../../../ArquitecturaBaseMutitenantFront/docs/design/capturas/etapa-3b/informe.md), [manifest](../../../ArquitecturaBaseMutitenantFront/docs/design/capturas/etapa-3b/manifest.json) y pares app/lienzo en esa carpeta.

- Ocultaciones autorizadas: WhatsApp (E8), exportación/sugerencia de exportar (E10) y bloqueo del único Dueño (E4). Reducen altura de lista/tarjeta/diálogo.
- Nombres/opciones de idiomas y zonas salen del catálogo traducido: en español aparece «Inglés (Estados Unidos)» frente a «English (United States)» del ejemplo del tablero. Los ejemplos de fecha/número sí se copian usando sus patrones.
- Versiones, fecha de eliminación, máscara del destinatario y disponibilidad de métodos vienen del contrato. La política de operador muestra el error resource y no emite OTP; el tablero no incluye esa variante.
- Mensajes solo trae referencia española de escritorio; se capturó además HTML real en inglés y móvil. «Método agregado» usa correo en 3b, mientras el ejemplo del tablero es WhatsApp de E8.
- Quedan diferencias menores de iconografía, interlineado, foco visible y posicionamiento de menús/toasts por los controles Radix/Sonner existentes. No se declara identidad de píxeles; no se agregaron campos/acciones ni se cambió el orden o estados. Las diferencias se revisaron y se documentan aquí con su motivo.

## Decisiones tomadas

- Ejecución directa en main, en este chat, según autorización expresa. Revisión del plan sin subagentes.
- Google conserva subject como clave y suma contacto Email verificado para visualización y avisos; la dirección no reemplaza el subject.
- La nueva versión legal del E2E la publica su preparador en la base propia por comando de archivo. No se agrega endpoint productivo ni se toca Development.
- La limpieza de una cuenta real respeta la gracia: pedir baja impide su ingreso normal y el worker anonimiza al vencer; no se borra Identity ni auditoría a mano.
- HarnessStage sigue en 2 hasta cerrar 3c; solo se actualizan inventarios existentes exigidos y se agrega el contrato cruzado funcional.
- T03 entrega entidad y puerto; los helpers de emisión/verificación se conectan en T07 después de la persistencia T04, para probar el flujo completo con sus códigos y locks reales.
- T04: índice parcial único de principal. Como PostgreSQL lo comprueba al emitir cada UPDATE, el repositorio retira la marca anterior dentro de la UoW antes de fijar la nueva; la fila no es auditable ni soft-deletable y rollback revierte todo. `xmin` usa la columna de sistema (el SQL de Npgsql no crea una columna manual).
- T05: el canal recibe CultureProfile ya resuelto y renderiza async dentro de la UoW llamadora. UserId nullable permite cancelar mensajes propios sin modificar mensajes anteriores o de otros destinos; cancelar borra el payload cifrado pendiente.
- T06: se reutiliza VerifyDestination ya definido en LoginCodePurpose, en lugar de crear un propósito equivalente. Los avisos de agregado/desvinculado adaptan el sustantivo del método en la plantilla aprobada; Google mantiene subject privado.
- T07: el código también conserva acción/respaldo/objetivo, antes del ticket; de otro modo, la misma prueba podría presentarse como una acción diferente al verificar. Los métodos administrados consultan un booleano de membresía/organización activa mediante un puerto específico, declarado en el inventario existente; no consultan Members fuera de RLS.
- T06 bis: un correo pendiente agregado desde Mi cuenta solo se confirma con VerifyDestination y su UserId. El registro anónimo no lo activa ni inicia una sesión de su propietario; devuelve el error existente de dirección asociada después de probar el código. Las invitaciones se implementan con su protocolo en 3c.
- T08: para iniciar OAuth desde una API bearer, el vínculo recibe JSON con redirectUrl generado por el middleware; la navegación Google sigue después. El callback existente compara el UserId del state protegido con la cookie Identity validada, sin usar query para cuenta o retorno. Locks externos antes del lock de cuenta. Contactos Google existentes sin dato se completan con el correo probado en su próximo ingreso.
- T09: el listado es lectura por puerto separado, devuelve acciones/respaldo calculados y días de gracia de PlatformSettings. El subject Google queda fuera del contrato. Los códigos de resources que Domain usa como errores de campo se traducen centralmente en ProblemDetailsMapper; los mensajes de validación ya traducidos siguen igual.

- T10: las preferencias y el nombre se guardan al confirmar la UoW con OriginalValue=xmin recibido. UserStore.Update vuelve a adjuntar y elimina la expectativa del cliente; no se usa para este guardado sin campos de Identity. El conflicto real devuelve 409 y conserva los primeros datos.

- T11: lector legal por UserId explícitamente autorizado en inventario existente. La prueba de publicaciones usa ApiFactory propia para conservar los documentos v1 de otras pruebas. El preparador E2E acepta v1 para su cuenta fixture; no cambia el seed ni registra aceptación ficticia del operador.

- T12: el estado y la fecha permanecen separados. Suspended con fecha solo vuelve a PendingDeletion al reactivar; nunca cancela suspendida ni al vencer. El cierre conserva Id/fechas para referencias y auditoría y limpia la razón de texto libre. Los cambios de estado quedan pendientes del único guardado UoW y rotan SecurityStamp al pedir baja.

- T13: el puerto queda en Integrations/Legal, como el documento canónico. La proyección de eliminación es un lector específico de TenantId/Kind sobre el índice técnico existente, sin reutilizar el lector reservado de perfiles. La limpieza Personal borra sus filas y su índice en la misma transacción; un reintento omite el alcance ya limpiado. Membresías B2B quedan Removed con motivo AccountDeleted y evento de auditoría único.
- T13: revisión automática rechazó el grant general DELETE por ampliar el alcance de mt_app. La alternativa aprobada agrega una política AS RESTRICTIVE solo para Personal, combinada con tenant_scope y FORCE RLS; el test demuestra que DELETE en Business no borra nada incluso con el alcance correcto. No se alteró Development.

- T14: se reutiliza SignInService.RevokeSessions para revocar cookie por SecurityStamp y autorizaciones/tokens OpenIddict de ambos accesos en la UoW. La API cierra la cookie después del commit. Los mensajes anteriores se cancelan antes de encolar el aviso de baja; la razón permanece únicamente en la identidad y se limpia al anonimizar.

- T15: lock cuenta antes del destino y propiedad releída para evitar inversión con reauth. Google conserva returnUrl y comprobante dentro de una cookie Data Protection HttpOnly/Secure de cinco minutos, recuperada por POST no-store; el servidor también exige vigencia. Suspensión y bloqueo prevalecen antes de emitir el ticket.

- T16: el hash se proyecta a UserId sin tracking antes del lock; el ticket se relee bajo ese lock. La cookie nueva se emite después del commit; la sesión anterior permanece revocada. No se permite cambiar UserId ni returnUrl desde la petición anónima.

- T17: dos columnas técnicas de lease en la identidad evitan una tabla de trabajo adicional; CTE SKIP LOCKED y vigencia de quince minutos. Cada paso mantiene lock de fila hasta commit para impedir otro reclamo en medio de la limpieza. La purga usa RemoveRange tracked, sin ampliar excepciones bulk. SQL revisado: solo dos columnas nullable, sin grants nuevos. En Testing el worker se despacha explícitamente; en el runtime comienza al arrancar y continúa cada hora.

- T18: el mapa compartido de API se consume desde las features sin importarlas entre sí. El contrato lee ambos repos y verifica rutas/metadata/claims/retornos/errores y schema. La guardia bulk de E2 se actualiza en su prueba existente para declarar la única llamada técnica de principal de T04 (una llamada en el repositorio exacto); no se agregan guardas ni tests de arnés nuevos.

- T20: selectores compartidos ofrecen variante compacta del lienzo. El perfil congela versión con el draft, aplica cultura solo tras guardar y mantiene layout del acceso actual. Mi cuenta vive en el menú de identidad y en el lateral personal que ya muestra el lienzo.
- T21–23: el aviso vive en shared/ui para compartirlo con Inicio sin importar áreas. La tabla obedece flags del backend. El comprobante de reautenticación se conserva en memoria para reintentar si se pierde la respuesta de la mutación. Google enlaza antiforgery y errores de callback por códigos traducidos.

## Recorrido manual con cuentas reales

Preparación: Docker activo; Google y Gmail configurados según [configuración](../operations/configuracion.md). En la raíz del back ejecutá `aspire run` y abrí `https://localhost:5174`. Usá Gmail SMTP en Development para recibir correos reales. Si aparece una cuenta de ejemplo, no la crea el seed de esta etapa: Development solo siembra `Seed:PlatformOwner:*`. No ejecutes el preparador E2E contra esta base.

1. Abrí `/login`, ingresá el correo configurado en `Seed:PlatformOwner:Email`, pulsá «Enviar código», copiá el código del correo de Gmail y pulsá «Verificar». Entrás como operador. Abrí el menú de tu cuenta y «Mi cuenta», o `/cuenta`.
2. En «Tus datos», editá el nombre, seleccioná «Inglés (Estados Unidos)» en «Idioma y región» y guardá. Verificá «My account» y que F5 conserve inglés. Volvé a Español (Argentina) y guardá para seguir este recorrido.
3. Pulsá «Agregar correo o teléfono». Primero aparece el diálogo de reautenticación y llega un código a un método que ya estaba verificado antes de iniciar esta sesión; ingresalo. Después ingresá otra dirección que controles (distinta del correo que vas a usar para registrar la segunda persona), «Enviar código» y verificá el código recibido en ese buzón. Aparece verificado y llega el aviso del cambio. Un método agregado en esta sesión todavía no puede autorizar otras acciones hasta que vuelvas a ingresar. WhatsApp todavía está oculto.
4. En ⋮ de ese correo, «Hacer principal» pide un código enviado al otro método disponible; ingresalo y confirmá. Verificá la marca Principal y los avisos. Volvé a hacer principal el correo original, usando el código que llega al nuevo.
5. En ⋮ del correo nuevo, «Quitar» pide otro código al principal original. Ingresalo y confirmá: desaparece y llega el aviso. Si aparece cuenta regresiva, esperá y pulsá el mismo botón otra vez para pedir el código; nunca se reenvía solo. En el único método propio Quitar queda deshabilitado.
6. Si Google está configurado, «Vincular Google» pide primero el código de un método que ya estaba verificado antes de iniciar la sesión; después abre el proveedor. Elegí una cuenta Google libre, autorizá y verificá el regreso a `/cuenta` con el aviso. Para desvincularla, ⋮ → «Desvincular», código en otro método → confirmar. Google nunca se vincula automáticamente por coincidir el correo.
7. Para probar términos nuevos con cuentas reales, abrí la conexión **propietaria de appdb de Development** desde tu cliente PostgreSQL y ejecutá **una sola vez** [etapa-3b-publicar-terminos.sql](etapa-3b-publicar-terminos.sql). El script solo agrega otra versión del texto legal vigente es/en; no cambia personas ni aceptaciones. La pantalla administrativa para publicarlos nace más adelante. Volvé a la aplicación y F5: aparece «Actualizamos los términos», con el número recién publicado. Abrí el enlace legal, volvé, marcá la casilla y «Aceptar y seguir». F5 ya no vuelve a bloquear. La publicación también exigirá aceptar a las demás cuentas activas.
8. El operador no puede darse de baja. Para probar la baja, cerrá su sesión y abrí `/registro`. Usá **un Gmail distinto del método del operador**, aceptá Términos/Privacidad, enviá el código y completá el registro con el correo recibido. Esto crea la segunda cuenta real y su espacio Personal; no crea ninguna empresa.
9. En esta segunda cuenta abrí `/cuenta` → «Dar de baja». Escribí el motivo; al abrir llega un código al principal. Si el registro fue reciente, esperá la cuenta regresiva y pulsá «Dar de baja mi cuenta» para pedirlo otra vez. Copiá los seis números y confirmá. Verificá «Cerramos tu sesión», la fecha de eliminación y el correo de baja.
10. Durante la gracia (30 días por defecto), abrí `/login` e ingresá **el mismo Gmail**. Enviá el código, verificá y comprobá «Tu cuenta tiene la baja pedida». Pulsá «Cancelar la baja y entrar»: recién entonces vuelve Personal y llega el aviso de cancelación. F5 debe conservar la sesión nueva. Con Google vinculado, el mismo estado aparece tras demostrar ese método en el proveedor.
11. Para dejar solo al operador **activo**, volvé a pedir la baja de esta segunda cuenta con motivo y código y dejala pendiente. No vuelvas a ingresar y cancelar. Al llegar la fecha, `AccountDeletionWorker` la procesa al arrancar la Api o en su siguiente ejecución horaria. Verificá el aviso final al principal y que el Gmail ya no permita ingresar. La cuenta se anonimiza y se borran sus métodos, su espacio Personal y credenciales; su Guid y la evidencia legal/auditoría quedan conservados. Esto cumple la baja definitiva; no se hace DELETE manual de Identity ni se acorta la gracia para limpiar una prueba real. Después entrá otra vez con el operador.
12. Terminá con `aspire stop` desde la raíz del back.





## Últimas tareas verificadas

| Tarea | Commit back | Commit front | Evidencia |
|---|---|---|---|
| T27 · recorridos reales completos | `fc66c58` | `a50b5b0` | Cinco recorridos + padre, 6/6 reales; publicación legal por archivo en base exclusiva |
| T27 bis · respetar 429 manual | — | `3c05b47` | Test rojo: el botón permanecía bloqueado/reintento automático; verde 3/3, runner pulsa otra vez tras RetryAfter |
| T30 correcciones de la puerta | `1b96685` | `f81d2eb` | Arquitectura 153/153 y afectados de integración 13/13; suite completa 1110/1110 |
| T28 correos reales | `165d927`, `8b054d1` | `f81d2eb` | HTML real de siete avisos en es/en sin conectar ninguna base; plantillas 12/12 |

## Decisiones tomadas durante la puerta

- Reducir dependencias de Google mediante `GoogleAccountGuard` y mantener la preparación de la vinculación en el servicio, sin cambiar state, callback ni claims.
- Actualizar inventarios existentes para las piezas que nacen en 3b; no añadir guardas de arquitectura ni tests del arnés.
- Simular el estado anterior a una migración con columnas de ese esquema histórico; el modelo EF actual ya contiene columnas posteriores. El worker de baja se prueba en una base propia porque adelantar su reloj en una fixture compartida vuelve vencidas cuentas de otros tests.
- Respetar el 429 con cuenta regresiva y reintento explícito en el botón ya dibujado. Se retiró el reintento automático de la primera corrección de cooldown por contradecir la regla escrita de la Etapa 1.
- Componer los correos visuales a través del renderer real, el catálogo JSON y DisplayFormatter; el modo de captura no abre conexiones ni envía correo. En el navegador solo se sirve el logo local de la plantilla.
- Los botones HTML incluyen `#007475`, equivalente sRGB del token de marca, antes de `oklch`, para que el fondo sea visible también en clientes de correo sin CSS moderno. Plantillas 12/12 y build final 0 advertencias/0 errores.
- T28 bis: los tres diálogos validan al confirmar un código incompleto, como el lienzo, sin enviar esa prueba al servidor. Tres tests existentes rojos y cuatro casos verdes; front `59dc2cd`. Durante cancelación, Salir queda deshabilitado hasta la respuesta (test rojo → verde).
- El selector compacto muestra el ejemplo fijo aprobado (27/09/2026 14:35 y 1234,50) por los patrones del catálogo, con UTC solo para ilustrar la cultura. La descripción queda fuera de ItemText para que el valor del selector muestre solo el nombre. Test rojo → verde 4/4; se conserva `en-US` al elegir.
- El chat paralelo de comentarios fue autorizado expresamente por el usuario. Los commits de backend se hicieron con patches funcionales revisados: la revisión automática rechazó stage de archivos completos por el riesgo de mezclar comentarios ajenos. No quedó bloqueada ninguna acción necesaria.

## Correcciones de seguridad solicitadas antes del cierre (01/10/2026)

| Tarea | Commit back | Commit front | Resultado |
|---|---|---|---|
| T31 · regresiones por toma de cuenta y método pendiente | `1b607b9` | `e5346e1` | Los tests quedaron rojos con el comportamiento previo; al final ambas rutas de ataque quedan bloqueadas y quien prueba posesión recupera el correo pendiente |
| T32 · reautenticación obligatoria para altas | `1b607b9` | — | El servidor sella el inicio de sesión; agregar correo/vincular Google requieren tickets específicos de un método ya verificado antes de la sesión. ADR 0033 y `datos-personales.md` se actualizaron en este mismo commit |
| T33 · diálogo existente en el front | — | `e5346e1` | Reautenticación previa al POST de correo o al inicio OAuth; tickets solo en memoria/cuerpo, recursos es/en y contrato cruzado actualizados |
| T34 · liberar y vencer métodos pendientes | `1b607b9` | — | Bajo el lock del destino, la prueba de posesión elimina el pendiente ajeno y continúa la operación; el pendiente vence junto con el código. Los métodos verificados de otra cuenta siguen reservados |
| T35 · E2E, capturas y puerta | `3976aeb` | `161931f` | Dos casos E2E de seguridad; límites de códigos ampliados únicamente con `MT_E2E_ISOLATED=1`; cuatro comparaciones app/lienzo nuevas. Este informe y el plan se cierran en el commit de documentación que contiene esta actualización |

Las nuevas pruebas se corrieron rojas antes del código: agregar correo o Google con solo la sesión, usar como reautenticación un método verificado después del inicio de sesión y registrar con un correo pendiente ajeno. Los casos finales prueban la reautenticación por el método anterior, el rechazo del método recién agregado como origen y la recuperación atómica del destino pendiente.

## Puerta final de 3b

| Punto | Resultado final |
|---|---|
| Build y tests back | `dotnet build ArquitecturaBaseMultitenant.slnx --no-restore`: 0 advertencias/0 errores. `dotnet test ArquitecturaBaseMultitenant.slnx --no-restore` con Docker/Testcontainers: 1117/1117 |
| Front | `npm run lint`, `npm test` (658/658) y `npm run build`: verdes |
| Contratos | `npm run contracts:check`: verde (`Generated TypeScript schema matches OpenAPI.`); los tests funcionales existentes leen ambos repositorios para rutas, acciones, cuerpos, claims, redirecciones y errores |
| Generador y arnés | Generador 30/30; HarnessTests back 10/10; harness visual front 20/20; comparación `--verify`: 122/122 pares completos |
| Datos y aislamiento | Development sigue sembrando solo `Seed:PlatformOwner:*`. Los tests usan Testcontainers y la base/pickup efímeros propios del E2E; no se crean personas ni métodos de ejemplo en Aspire |
| Aspire | `aspire stop`; `aspire ps` sin recursos activos |
| Comparación visual | 122 pares app/lienzo (244 PNG), 14 HTML de correo y 10 hojas existentes; los cuatro pares nuevos se revisaron individualmente en escritorio/móvil. Diferencias y motivo en el [informe visual](../../../ArquitecturaBaseMutitenantFront/docs/design/capturas/etapa-3b/informe.md) |
| E2E real | 8/8, sin mocks, con PostgreSQL y pickup propios. Salida literal abajo |

```text
▶ registro real y puerta empresa usan un PostgreSQL aislado, front, Api y pickup sin mocks
  ✔ 3b seguridad: sesión abierta no suma correo ni Google y método nuevo no autoriza el anterior (75735.2501ms)
  ✔ 3b seguridad: registro con posesión recupera un correo pendiente de otra cuenta (150969.1989ms)
  ✔ 3b: sumar correo personal con código leído del .eml (78887.2397ms)
  ✔ 3b: quitar un método con código enviado a otro (130054.3449ms)
  ✔ 3b: pedir baja y cancelarla ingresando durante la gracia (130547.7017ms)
  ✔ 3b: cambiar el idioma a en-US desde /cuenta (11716.3898ms)
  ✔ 3b: aceptar versión nueva de términos que bloquea el ingreso (10266.1648ms)
✔ registro real y puerta empresa usan un PostgreSQL aislado, front, Api y pickup sin mocks (651897.7627ms)
ℹ tests 8
ℹ suites 0
ℹ pass 8
ℹ fail 0
ℹ cancelled 0
ℹ skipped 0
ℹ todo 0
ℹ duration_ms 653626.4638
```

El ajuste de cupo evita agotar el límite de códigos por IP al correr toda la batería de seguridad dentro del mismo E2E aislado. Solo eleva a 100 ese límite en modo aislado; producción y Development mantienen su configuración normal y los códigos/cooldowns siguen activos.

## Diferencias visuales y motivo final

- Ocultaciones autorizadas: WhatsApp (nace en E8), exportación (E10) y bloqueo del único Dueño (E4).
- Las nuevas reautenticaciones de agregar correo y vincular Google no están dibujadas en Cuenta/M-Cuenta. La app reutiliza el diálogo existente, con seis casillas OTP, orden y tokens. Para la comparación se usó el estado del lienzo de confirmación por código para cambiar el método principal; el título, explicación y botón nombran la acción real. Es una diferencia de seguridad y contenido requerida antes de abrir el formulario o redirigir a Google.
- Se mantienen las diferencias ya registradas por datos de referencia traducidos, valores/timestamps contractuales, plantilla de correo para el caso de correo, y detalles menores Radix/Sonner de foco, iconografía, interlineado y alineación. No cambian las pantallas dibujadas, campos, orden ni acciones.

## Decisiones tomadas al cerrar la seguridad

- El backend toma el inicio de sesión de la cookie protegida por Data Protection, lo emite como claim y lo conserva en refresh/cambio de acceso; el cliente no puede moverlo.
- Cada alta tiene un ticket de un solo uso ligado a la acción. Para obtenerlo solo sirven métodos verificados antes de esa sesión.
- Quien prueba posesión de un correo pendiente pasa a ser su dueño; el borrado del pendiente ajeno y la nueva escritura comparten lock/transacción. La expiración es la misma del código.
- El E2E usa su configuración de código más amplia únicamente bajo `MT_E2E_ISOLATED=1`, para evitar que los siete recorridos y el padre compartan un límite de IP demasiado bajo.
- No se agregaron guardas de arquitectura ni tests nuevos del arnés. El estado de reautenticación se capturó con el lienzo existente más cercano, sin inventar una pantalla en el tablero.

La etapa 3b queda completa. La etapa 3c no se inició y conserva su propia puerta.
