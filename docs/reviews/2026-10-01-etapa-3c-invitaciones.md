# Etapa 3c · Invitaciones — informe de trabajo

Estado: en curso. No se declara cerrada la etapa ni se cambia `HarnessStage` hasta completar la puerta.

| Tarea | Resultado | Commit |
|---|---|---|
| Plan previo | Revisado y guardado antes de programar | `7934f42` |
| T01 · Dominio | Reglas de vigencia/uso único/bootstrap y Member sin identidad; 13/13 | commit que incorpora esta fila |

## TDD y verificaciones

- T01 rojo: `InvitationTests.cs` no compiló porque no existían el namespace `Domain.Invitations` ni `Invitation` (`CS0234`, `CS0246`). El intento inicial con red restringida falló en NuGet (`NU1900`); no se contó como rojo de conducta. Restauración con acceso a NuGet y repetición dieron el rojo esperado.
- T01 verde: `dotnet test --project tests/ArquitecturaBaseMultitenant.Domain.UnitTests/ArquitecturaBaseMultitenant.Domain.UnitTests.csproj --no-restore -- --filter-class '*InvitationTests' --filter-class '*MemberTests'`: total 13, correcto 13, error 0, omitido 0.
- Build Domain: 0 advertencias, 0 errores.

## E2E real

Pendiente: T04 observa rojo del flujo real ausente y T10/T12 exigen verde completo, con la base y pickup aislados. No se crearon datos de ejemplo en Development.

## Comparación con el lienzo

Se revisaron los tableros Invitacion y M-Invitacion a 1440×900 y 390×844 con una invitación. No mostraron desborde horizontal ni campos deformados. T09 repetirá con datos del contrato real y T11 guardará las comparaciones en `docs/design/capturas/etapa-3c/` del front. Empresa, Roles y rol del invitador esperan una decisión del usuario porque esos modelos nacen en E6/E4.

## Decisiones tomadas

Las decisiones vigentes están en el [plan](../plans/2026-10-01-etapa-3c-invitaciones.md#decisiones-técnicas-tomadas): identidad diferida, token protegido y RLS, continuación HttpOnly, ingreso sin selección de acceso antes del accept, reconocimiento de Google-only, nonce de bootstrap para recuperar una respuesta perdida, barrido por organización durante baja y locks compatibles. Una reserva de Member retirada puede seguir sin identidad; nunca se activa ni se vincula después de retirarse.

El usuario confirmó Aspire apagado antes de compilar. Se trabaja en main, sin push y con rutas explícitas. El recorrido manual de invitar desde Usuarios pertenece a la puerta de E6.
