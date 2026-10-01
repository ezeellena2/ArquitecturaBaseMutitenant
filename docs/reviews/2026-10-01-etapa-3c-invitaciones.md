# Etapa 3c · Invitaciones — informe de trabajo

Estado: en curso. No se declara cerrada la etapa ni se cambia `HarnessStage` hasta completar la puerta.

| Tarea | Resultado | Commit |
|---|---|---|
| Plan previo | Revisado y guardado antes de programar | `7934f42` |
| T01 · Dominio | Reglas de vigencia/uso único/bootstrap y Member sin identidad; 13/13 | `abbd59d` |
| T02 · Persistencia | RLS, reservas sin identidad e índice de accesos; 10/10 integraciones | `a332c2b` |
| T03 · Emisor y correo | Emisión nueva/Email/Google sin crear cuentas, rollback y canal real; 28/28 focales | `1ce2d21` |
| T04 · E2E rojo | Dos invitaciones reales llegan al navegador y fallan en preview ausente; siete recorridos previos verdes | back `d329ccd`, front `1a62898` |
| T05 · Preview | Estados actuales, sesión explícita y continuación HttpOnly; 15 unitarios y 9 integraciones | commit que incorpora esta fila |

## TDD y verificaciones

- T01 rojo: `InvitationTests.cs` no compiló porque no existían el namespace `Domain.Invitations` ni `Invitation` (`CS0234`, `CS0246`). El intento inicial con red restringida falló en NuGet (`NU1900`); no se contó como rojo de conducta. Restauración con acceso a NuGet y repetición dieron el rojo esperado.
- T01 verde: `dotnet test --project tests/ArquitecturaBaseMultitenant.Domain.UnitTests/ArquitecturaBaseMultitenant.Domain.UnitTests.csproj --no-restore -- --filter-class '*InvitationTests' --filter-class '*MemberTests'`: total 13, correcto 13, error 0, omitido 0.
- Build Domain: 0 advertencias, 0 errores.
- T02 rojo: tras compilar adaptadores y ajustar las proyecciones de Guid nullable, los tests reales fallaron en `PendingModelChangesWarning` por la migración aún ausente. El intento sin acceso al pipe de Docker no se contó como rojo de conducta.
- T02 verde: `InvitationsTests`, `UserTenantAccessIndexTests`, `RlsPolicyInventoryTests` y `MigrationsTests`: total 10, correcto 10, error 0, omitido 0. PostgreSQL aislado creado y eliminado por Testcontainers.
- Build Infrastructure: 0 advertencias, 0 errores. `has-pending-model-changes` indicó que no hay cambios; SQL idempotente revisado con claves/FKs, índices parciales, RLS forzado, grants SELECT/INSERT/UPDATE y trigger actualizado.
- T05 rojo: tests de servicio no compilaron por los modelos, servicio y puerto de continuación ausentes (`CS0246`). Verde: 15 unitarios, 9 integraciones de invitación; sin omitidos. API build 0 advertencias/0 errores. La integración usa fila privada real, comprueba Pending después de dos previews y cookie protegida HttpOnly/Secure/SameSite Lax; no emite cookie Identity. El token adulterado ni siquiera entra al scope.

## E2E real

T03 rojo: la prueba de emisión no compiló por la ausencia de `Application.Services.Invitations` (`CS0234`). La variante Google detectó después que el aviso de código era incorrecto: esperaba ausencia y encontraba el texto. Se ajustó al método real. Dos expectativas de texto/fecha en inglés se corrigieron para respetar resources y catálogo (`MM/dd/yyyy`), sin cambiar el producto.

T03 verde: `InvitationsTests`, `EmailTemplateTests`, `LoginCodeEnumerationTests`, `LoginCodeConcurrencyTests`, `MigrationsTests`: total 28, correcto 28, error 0, omitido 0. Build Infrastructure (incluye Application): 0 advertencias/0 errores; modelo EF sin pendientes. La migración `InvitationHashStorage` amplía hashes de 60 a 500 siguiendo LoginCode/ReauthTicket: el generador existente produce SHA-256 hexadecimal de 64 caracteres. Up/Down revisados; Down vuelve al límite anterior y PostgreSQL rechaza valores largos sin truncarlos silenciosamente.

T04 ejecutó `npm run test:e2e:real` completo antes de implementar aceptación HTTP/front. Cada caso emitió con el issuer real en la base exclusiva y leyó el enlace del `.eml`. Ambos fallaron en «abre /invitacion y ve la vista previa»: `locator.waitFor: Timeout 15000ms exceeded`. Se mantuvieron los siete casos de cuenta anteriores y todos pasaron. Sin mocks, skip ni datos de ejemplo en Development. El `finally` detuvo el AppHost propio y eliminó el pickup temporal.

```text
✖ 3c: aceptar invitación sin cuenta y sin Personal desde el .eml (20922.3212ms)
✖ 3c: aceptar invitación con cuenta previa desde el .eml (19869.5576ms)
tests 10 · pass 7 · fail 3 · cancelled 0 · skipped 0 · todo 0
duration_ms 685282.0408
```

El tercer fallo es el test padre por sus dos hijos rojos. T10/T12 exigen el verde completo. La inspección del preparador verificará Member activo, invitación aceptada y cero espacios Personal antes de cualquier selección Consumer.

## Comparación con el lienzo

Se revisaron los tableros Invitacion y M-Invitacion a 1440×900 y 390×844 con una invitación. No mostraron desborde horizontal ni campos deformados. T09 repetirá con datos del contrato real y T11 guardará las comparaciones en `docs/design/capturas/etapa-3c/` del front. Empresa, Roles y rol del invitador esperan una decisión del usuario porque esos modelos nacen en E6/E4.

## Decisiones tomadas

Las decisiones vigentes están en el [plan](../plans/2026-10-01-etapa-3c-invitaciones.md#decisiones-técnicas-tomadas): identidad diferida, token protegido y RLS, continuación HttpOnly, ingreso sin selección de acceso antes del accept, reconocimiento de Google-only, nonce de bootstrap para recuperar una respuesta perdida, barrido por organización durante baja y locks compatibles. Una reserva de Member retirada puede seguir sin identidad; nunca se activa ni se vincula después de retirarse.

El aviso «te vamos a mandar un código» del correo aparece solo para un correo Email ya verificado, nunca para Google-only ni cuenta nueva. El pie conserva «Si no esperabas este correo, podés ignorarlo» y omite la promesa «sin el código nadie puede entrar»: la invitación nueva prueba posesión mediante el enlace. Se registrará también en la comparación visual. No se crean datos de Empresa ni Roles para completar el lienzo.

El usuario confirmó Aspire apagado antes de compilar. Se trabaja en main, sin push y con rutas explícitas. El recorrido manual de invitar desde Usuarios pertenece a la puerta de E6.
