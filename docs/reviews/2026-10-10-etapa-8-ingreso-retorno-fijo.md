# E8 · Ingreso OTP con retorno fijo

Corte Back A1 para Registro ocupado → Ingresar. BASE de implementación y comparación de contrato: `a9190b7a7e7cf75e769af005b70ed9493b5a6e29`. Los avances concurrentes `d33c707c`, `937aa920` y `c5c563d1` sólo publican el handoff de coordinación y precisiones del plan E8 (guion futuro de operador y teléfono principal enmascarado). No son cambios del corte. No cierra E8 ni E8b.

## Alcance y autorización

La respuesta humana original `01a125d2-b0b4-7522-9caa-962d09133ded`, en coordinación `01a118cd-33c8-7312-a159-299c0c51c8b3`, reply `call_DXKl9cA6nqHyUp5jEjJRqKbq`, aprueba únicamente `/` con `consumer` y `/org` con `business`, literales sin query, fragmento ni otras rutas. El motivo aprobado es **el teléfono no se guarda en el navegador para cruzar la redirección**. La fuente original y el acuerdo están conservados en la evidencia local del corte.

Back prueba el cuerpo HTTP y PostgreSQL reales, entrega contrato y conserva cookies existentes. Front regenerará sus tipos y completará el E2E real de Registro con número ocupado → Ingresar → código → OIDC → sesión/F5 por Persona y Empresa. El teléfono permanece sólo en navigation state; no se transporta en URL o storage. No se ejecutó navegador ni proveedor Meta en este corte.

## Cambios al contrato

`POST /api/auth/login-code/verify` agrega sólo `VerifyLoginCodeHttpRequest.access?: string | null`. No cambia ruta, respuesta, permisos, tenant ni cookies. `returnUrl` y `access` preservan los valores técnicos literales para validar antes de consumir OTP. El OpenAPI se exporta por el build normal; la descripción y dos ejemplos del endpoint documentan el delta.

| `returnUrl` | `access` | Resultado |
|---|---|---|
| `/` exacto | `consumer` exacto | 200 con `{ "returnUrl": "/" }` y cookie pública después del commit |
| `/org` exacto | `business` exacto | 200 con `{ "returnUrl": "/org" }` si la membresía y organización siguen activas |
| Dos fijos | Omitido/null, acceso inverso u otro | 400 `Validation.Failed` antes de consumir |
| OIDC permitido o `/invitacion` | Omitido/null | Conducta anterior |
| OIDC permitido o `/invitacion` | No null | 400 antes de consumir, sin doble fuente |
| Otro retorno o fijo alterado | Cualquiera | 400 antes de consumir |

Ejemplos del nuevo cuerpo: `{ "email": "persona@example.com", "code": "123456", "returnUrl": "/", "access": "consumer" }` y `{ "phone": { "country": "AR", "number": "11 3456-7890" }, "channel": "<phone-channel-key>", "code": "123456", "returnUrl": "/org", "access": "business" }`. Los valores son ejemplos públicos, no códigos o destinos de evidencia real.

La baja pendiente conserva 403 `Identity.Account.PendingDeletion` con `cancelTicket`, fechas y `returnUrl`, sin cookie. El ticket fija cuenta/retorno durante cinco minutos y sólo permite cancelar una vez. Cancelar conserva el cuerpo y respuesta existentes; los campos extra de un intento de cambiar cuenta/acceso/retorno no reemplazan el ticket. No añade columnas ni migración.

## Decisiones tomadas

- Resolver OTP separado de `IsLoginReturn`/`TryReadAccessSelection`: mantiene la lista anterior de Google, operador e invitación. Una selección validada se comparte entre servicio, verifier e invitaciones; la cookie usa ese mismo lado.
- `RawText` sólo en los dos campos técnicos de este cuerpo, junto al código que ya lo tenía. El pipeline JSON no puede transformar `/org `, invisibles o `consumer ` en valores aceptados. Email y Phone mantienen normalización.
- `IssueForLoginCode` es la admisión específica de cancelación de OTP fijo. El emisor genérico mantiene los destinos legacy de Google y enlaces. El ticket ya guarda el retorno, cuyo lado fijo es único.
- Revalidar Business dentro de la UoW del cancelador tras preparar la cuenta Active. `UserRepository.GetByIdAsync` lee esa instancia preparada; `GetActiveUserAsync` comprueba las membresías sin abrir otra UoW. `CommitPolicy.OnSuccess` revierte cuenta/ticket ante un acceso perdido, antes de participantes/avisos. Cookie sólo después del commit.
- Revalidar método disponible antes de emitir ticket de baja, también después de haber probado OTP. El método retirado o el canal apagado no se convierte en prueba de cancelación. Se conservan locks de identidad → cuenta → código y `CommitPolicy.OnAnyResult` en la verificación.
- Las pruebas de composición Phone quedan en `Modules/WhatsApp/WhatsAppIdentityTests`; Auth mantiene fixtures de Email neutrales y no incorpora referencias CLR al módulo. Son pruebas HTTP sintéticas y deterministas del pipeline, sin Meta real.

## Verificación

Se conserva RED real HTTP/PostgreSQL: ambos retornos fijos respondían 400 en vez de 200 (2 ejecutadas, 2 fallos, exit 1, 25.393 s). El primer intento del launcher `dotnet test` falló antes de ejecutar y otro no pudo abrir el named pipe Docker del sandbox; son diagnósticos separados de RED. La ejecución Docker autorizada permitió continuar.

Resultados focales finales, con comandos exactos, exits y stdout/stderr originales en `.local/reviews/e8b-contract/e8-otp-fixed-evidence/`:

| Cohorte | Ejecutadas | Fallos / omitidas | Exit | Tiempo |
|---|---:|---|---:|---:|
| HTTP OTP fijo + composición Phone | 83 | 0 / 0 | 0 | 107.369 s |
| Regresión Auth/Legal/Security/input/contrato (14 clases) | 142 | 0 / 0 | 0 | 131.375 s |
| Unidades de OTP, selección, cancelación, acceso y resources | 81 | 0 / 0 | 0 | 0.297 s |
| Guards de contratos, capas, UoW, validación, logging y módulos (17 clases) | 91 | 0 / 0 | 0 | 18.327 s |

Los builds focales seriales, con `--no-restore -m:1 -nr:false -p:UseSharedCompilation=false -v:minimal`, terminaron con 0 advertencias y 0 errores: integración final 14.89 s; Application.UnitTests 13.70 s; ArchitectureTests y API 17.56 s. El último export OpenAPI se produjo por ese build normal.

Comparación completa contra el contrato publicado de BASE: 160 rutas y 274 esquemas conservados; sólo cuatro punteros cambian: `VerifyLoginCodeHttpRequest.properties.access`, descripción de verify y ejemplos `personaFija`/`empresaFija`. `access` es string nullable y opcional. SHA-256 raw/LF del export: `fb006c3a326231e09676970a075419cd68969877372b1af4b4352f6ce66da077`; BASE: `c8ef280ef0f360adfb9af2d8b38657039de1d301137f0c86dd178c94d2b367d4`.

Los parciales de 33 y 82 ejecutadas se conservan como diagnóstico: dos continuaciones OIDC usaban BaseAddress HTTP; dos escenarios de suspensión intentaban una transición inválida del lifecycle y dos assertions pedían `userId` que la respuesta HTTP existente no expone. Se corrigieron las fixtures; no se cuentan esos parciales como verde final ni se suman a las cohortes finales.

El cambio humano al modo rápido limita la entrega a este corte verificado, sin freeze ni nuevas cohortes después de la comprobación de arquitectura, ya terminada. El inventario propio comprende 19 archivos; `Role.cs` ajeno permanece intacto. No se stageó, commiteó ni pusheó desde el implementador.

## Pendientes de integración

Front regenera tipos del contrato publicado y coordinación incorpora este corte a su plan. E2E real de Registro ocupado, galería lado a lado, suites completas, documentación en 0, retirada de módulos y Meta siguen en sus puertas correspondientes. Este corte no declara esos resultados ni publica push.
