# E8 · Contrato HTTP de rechazos Business al verificar OTP

El contrato de `POST /api/auth/login-code/verify` omite seis rechazos 403 que el flujo Business fijo ya devuelve después de probar el código. Este correctivo documenta esos resultados y sus ejemplos es/en. La conducta productiva permanece intacta.

Base: `3774272d821c0534ce101ca1a307df11bebcd49b`. Hallazgo original: `.local/puerta-8/20261010-1/revision-spec.md`; alcance cotejado con `.local/reviews/e8b-contract/otp-business-http-notes.md`.

## Cambios al contrato

`IdentityContractContributor` agrega únicamente a la operación indicada el catálogo, la explicación y doce ejemplos localizados:

| Código | HTTP | Metadata de organización |
|---|---|---|
| `Tenancy.Access.NotMember` | 403 | Ausente |
| `Tenancy.Member.Inactive` | 403 | `organizationName`, `tenantId` |
| `Tenancy.Tenant.PendingApproval` | 403 | `organizationName`, `tenantId` |
| `Tenancy.Tenant.Provisioning` | 403 | `organizationName`, `tenantId` |
| `Tenancy.Tenant.Suspended` | 403 | `organizationName`, `tenantId` |
| `Tenancy.Tenant.Closed` | 403 | `organizationName`, `tenantId` |

Las cinco respuestas con metadata llevan nombre sintético y GUID de ejemplo. Estos rechazos carecen de cookie, errores por campo, `retryAfter`, encabezado `Retry-After`, `cancelTicket` y `returnUrl`.

El export se regeneró con el proyecto API real, mediante `GenerateOpenApiDocuments`. El generado y `docs/contracts/openapi.json` tienen SHA256 `78c5120454f240a1a42ccdd24fb42c1d838c325e32ad953b1615759df16bb3f6`. La base tiene SHA256 `46503255f38de831994887154187275f037a3682974976fc2aa920a95313f3d6`.

La comparación semántica contra la base permite sólo la descripción de esta operación, su `x-error-codes`, la descripción 403 y sus ejemplos. Conserva exactamente los 16 errores y seis ejemplos 403 anteriores; suma seis errores y doce ejemplos. Hay **cero cambios fuera de esa lista**, incluidos esquemas, DTO, rutas, accesos, permisos, solicitudes y demás respuestas. El helper documental global permanece intacto.

## Decisiones tomadas

- Se amplió el hallazgo original de tres errores a los seis alcanzables desde `LoginVerifier → ConnectService.ResolveAsync → AccessSwitchPolicy` con los cinco estados normales de `TenantStatus`.
- `InvalidTransition` protege un valor de enum inválido; no representa un estado normal y no se fabricó una fixture corrupta para documentarlo. `UserNotFound` tampoco se agregó: la cuenta se relee bajo lock antes de llamar a Connect y sus estados existentes ya están documentados.
- La metadata específica se agrega localmente a los ejemplos de esta operación. No se amplía el esquema compartido ni se altera la forma global de otros errores.
- La prueba vive en `LoginCodeFixedReturnTests` y reutiliza sus fixtures. Los estados se preparan con `CreateBusiness`, `Activate`, `Suspend`, `Close`, `TenantSpaceProvisioner.Stage` y UoW legítimos. No usa SQL para corromper estados ni modifica producción para acomodar pruebas.

## Evidencia dirigida

La matriz nueva comprende seis rechazos × dos culturas (`es`, `en`). Solicita un OTP real, lo lee del pickup por destinatario y verifica contra PostgreSQL migrado. Comprueba status, código, detalle traducido, valores reales y forma de metadata, ausencia de cookie y campos ajenos; compara catálogo y ejemplos del OpenAPI servido por la API.

| Ejecución | Resultado | Exit | Duración informada |
|---|---|---|---|
| Prueba inicial en sandbox | Fallo de compilación, cero casos; no es RED | 1 | Tool wall 6,466 s |
| Build diagnóstico serial en sandbox | Cancelado sin diagnóstico, cero casos | 1 | Sin duración de build final |
| Build diagnóstico escalado API + integración | 0 advertencias, 0 errores | 0 | 16,68 s |
| RED nuevo focal, contributor original | 12/12 fallan por catálogo ausente, después de comprobar HTTP real | 2 | 1 m 07 s 553 ms |
| Build GREEN API + integración | 0 advertencias, 0 errores | 0 | 24,04 s |
| `LoginCodeFixedReturnTests` | 68/68, incluidos 12 nuevos y 56 existentes | 0 | 1 m 20 s 768 ms |
| `IdentityHttpDocumentationTests` | 5/5 | 0 | 40 s 986 ms |
| `AccessSwitchPolicyTests` | 12/12 | 0 | 4 s 692 ms |
| Build API y export real | 0 advertencias, 0 errores | 0 | 5,75 s |
| Comparación semántica y hashes | Lista permitida exacta; generado = versionado | 0 | 296 ms wall |

Conteos: **85 casos distintos verdes** (68 + 5 + 12); **97 ejecuciones de casos** al sumar los doce RED repetidos luego en GREEN. No se repitió ningún GREEN. Los fallos de tooling se conservan por separado y no se cuentan como casos. La clase de retorno fijo conserva las guardas A1 de validación literal, cookie después del commit, ausencia de cookie al rechazar, cancelación/replay y operador exclusivo.

Comandos, exits, tiempos, hashes y tamaños de fuentes/evidencias: `.local/reviews/e8b-contract/otp-business-http-files.json`; registros: `.local/reviews/e8b-contract/otp-business-evidence/`. `git diff --check` pasó. Todas las sesiones de ejecución terminaron y la comprobación final encontró cero procesos propios activos.

## Alcance de la entrega

Fuentes entregadas: contributor, prueba de retorno fijo, export y este informe. `Role.cs` ajeno conserva SHA256 `3260a9378a8bb68317e1d92ce81e290435d29b7d030624d72dfed470a114039a`. Root audita y publica los archivos propios; este implementador no hizo stage, commit ni push.

Front recibe el export después de esa publicación. Este correctivo no certifica la puerta completa, E2E, galería, recorrido, documentación global en cero, Meta ni retiro físico del módulo. La coordinación conserva su corte fijo separado.
