# Correcciones posteriores al cierre · Invitaciones

Fecha: 2026-10-02. Correcciones de los dos hallazgos confirmados en la revisión de Etapa 3, aplicadas después del cierre de E4. Trabajo en `main`, sin push, con datos de prueba aislados.

## Cambios

- El ingreso de una cuenta previa abre `/login?returnUrl=%2Finvitacion`. Código y Google emiten la cookie de identidad y vuelven a la invitación sin seleccionar Consumer ni crear Personal. El destino admitido es exactamente `/invitacion`: variantes con query, fragmento y otras rutas se rechazan. El registro conserva sus retornos anteriores.
- Los errores de Google y la cancelación de una baja pendiente conservan ese retorno. Siguen aplicándose estado de cuenta, bloqueo, vencimiento y uso único de códigos y comprobantes.
- Cada navegación a una invitación monta su propio flujo. La confirmación, la organización aceptada, el consentimiento, los errores y la clave de idempotencia no se arrastran desde la anterior; una aceptación tardía del flujo desmontado no altera la pantalla nueva.
- La ficha [Identidad e ingreso](../features/identidad.md) registra ambos comportamientos. El E2E inspecciona ahora también la cuenta previa sin Personal antes y después de aceptar.

## Commits

| Repo | Commit | Cambio |
|---|---|---|
| Front | `feb67ec` | Aislar estado y mutaciones de cada invitación; tres regresiones |
| Back | `51f604f` | Retorno exacto para código, Google y cancelación; regresiones HTTP y de validación |
| Front | `7c83835` | Ingreso sin seleccionar Consumer, contrato cruzado y E2E reforzado |

## Verificación del alcance

- TDD: las tres regresiones de estado fallaron antes del arreglo y pasaron después. El ingreso tuvo rojo en dos validaciones, tres pruebas del front y seis casos HTTP nuevos antes de admitir el retorno.
- Backend: 25/25 pruebas de validación y retorno; 49/49 integraciones de código, Google, baja pendiente y aceptación/HTTP de invitaciones, con Testcontainers y cero omitidos. Build de Api y sus dependencias: cero advertencias y cero errores.
- Front: 37/37 pruebas de ingreso, invitaciones y retorno; 2/2 del contrato de invitaciones y 5/5 del arnés. Build y `contracts:check` correctos. Lint focal correcto, con el warning histórico de `InvitationPage` ya documentado en backlog.
- E2E real focal: `MT_E2E_ONLY_CASE=3c:` y `npm run test:e2e:real`. Incluye el recorrido base de registro/accesos y las dos invitaciones, con navegador, Api, Aspire, PostgreSQL y pickup aislados, sin mocks ni intercepts. Las inspecciones comprueban cero Personal también para la cuenta previa al volver del ingreso y después de aceptar.
- La puerta completa de E4 permanece documentada en su [informe de cierre](2026-10-01-etapa-4-roles.md). Esta corrección verifica el alcance afectado.

Salida literal del E2E focal:

```text
▶ registro real y puerta empresa usan un PostgreSQL aislado, front, Api y pickup sin mocks
  ✔ 3c: aceptar invitación sin cuenta y sin Personal desde el .eml (8778.5873ms)
  ✔ 3c: aceptar invitación con cuenta previa sin Personal desde el .eml (19227.9509ms)
✔ registro real y puerta empresa usan un PostgreSQL aislado, front, Api y pickup sin mocks (83735.8222ms)
ℹ tests 3
ℹ suites 0
ℹ pass 3
ℹ fail 0
ℹ cancelled 0
ℹ skipped 0
ℹ todo 0
ℹ duration_ms 84283.7625
```

Al terminar, `aspire ps --format Json --non-interactive` respondió `[]`. La base Development no recibió datos de ejemplo.
