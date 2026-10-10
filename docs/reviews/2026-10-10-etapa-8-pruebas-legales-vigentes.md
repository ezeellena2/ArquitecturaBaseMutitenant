# E8 · Pruebas de documentos legales vigentes

## Resultado

Se corrigieron las expectativas de dos pruebas del núcleo y se aisló la prueba del Asistente que publica una revisión legal, sin cambiar el producto ni las semillas. La lectura HTTP de términos y privacidad ahora exige exactamente el documento vigente persistido: identidad, versión, cultura y texto. La lectura legal desde el servicio exige los mismos valores. Ambas mantienen los prefijos de demostración y las culturas solicitadas.

Base de trabajo: `0c9f0afb3afe265b5dc92ab723cebbf25540d457`. El rojo dirigido reprodujo dos fallos por `Expected: 1 / Actual: 2`; la revisión legal v2 del Asistente ya estaba presente en la base. El diagnóstico conjunto anterior al arreglo ejecutó Me y aceptación legal: 12 casos, 10 correctos y los mismos dos fallos.

## Decisiones tomadas

- Leer las expectativas directamente de `ApplicationDbContext`, antes de la operación probada, filtrando documentos efectivos al instante UTC del `TimeProvider` y seleccionando el vigente por fecha y versión. El texto se lee por identidad del documento y cultura. No se usa el servicio probado para producir su propia expectativa.
- Aislar las dos lecturas de documentos sembrados mediante una `ApiFactory` propia. El diagnóstico encontró que `AssistantDurableAuthorizationTests` publicaba revisiones en la fixture compartida; esas revisiones podían cambiar el texto vigente según el orden de la suite. El aislamiento conserva la comprobación estricta del prefijo del seed.
- A pedido de coordinación, ampliar el mismo corte para aislar también `Consumer_durable_replay_requires_live_gate_legal_and_account`: cada caso gate/legal/account usa su propia `ApiFactory` para preparar datos, mutar y ejecutar replay. Conserva la publicación Terms con versión máxima + 1, ambos textos originales, suspensión y todos los asserts de creación/replay (404 para gate; 403 para legal y cuenta). Así la escritura tampoco altera la colección compartida y cumple la prohibición de tests que dependen del orden de [tests.md](../rules/tests.md).
- Retirar los literales de revisión 1 y 2 de esas pruebas del núcleo. No se reemplazan por límites amplios: se exige igualdad exacta con la revisión persistida. La comprobación específica de v2, conservación de v1, reinicio y revisiones propias/futuras sigue en `AssistantLegalSeedTests`, intacta.

## Verificación

Evidencia original en `.local/reviews/e8b-contract/`: cada corrida conserva `.command.txt`, `.stdout.log` y `.exit.txt` con el mismo prefijo. Los conteos se informan por corrida; no se suman casos repetidos.

| Evidencia | Resultado | Salida |
|---|---|---:|
| `legal-current-red-build` · build IT con API, serial, sin restore | 0 advertencias, 0 errores | 0 |
| `legal-current-red` · método legal HTTP exacto, antes del cambio | 2 casos, 2 fallos, 0 correctos, 0 omitidos | 2 |
| `legal-current-baseline-classes` · Me y aceptación legal, antes del cambio | 12 casos, 2 fallos, 10 correctos, 0 omitidos | 2 |
| `legal-current-green-build` · build IT con API, serial, analizadores | 0 advertencias, 0 errores | 0 |
| `legal-current-green` · clases exactas Me, aceptación legal y seed Asistente, Docker, antes de ampliar el writer | 15 casos, 15 correctos, 0 fallos, 0 omitidos | 0 |
| `legal-current-writer-build` · build final IT con API, serial, analizadores | 0 advertencias, 0 errores | 0 |
| `legal-current-final-writer-green` · writer exacto gate/legal/account + Me + aceptación legal + seed Asistente, Docker | 18 casos, 18 correctos, 0 fallos, 0 omitidos | 0 |

El build inicial dentro del sandbox registró una denegación ambiental de DataProtection durante la generación de OpenAPI y terminó con salida 0. Los builds posteriores autorizados fuera del sandbox terminaron limpios. La corrida final de 18 casos usa PostgreSQL aislado de Docker y contiene los tres casos del writer: gate, legal y cuenta. El caso de publicación legal conserva el flujo 3→4, aceptación exacta y rechazo 409 de la versión anterior; también pasa la comprobación de aceptación de ambos documentos durante el registro.

## Alcance y límites

Solo cambian `Auth/MeTests.cs`, `Legal/LegalAcceptanceTests.cs`, `Modules/Assistant/AssistantDurableAuthorizationTests.cs` y este informe. Sin fuentes productivas, seed, permisos, migraciones, nuevas dependencias al módulo ni cambios de contrato. La evidencia histórica N3, incluidos sus fallos, y la corrida anterior de 15 casos se conservan por separado.

OpenAPI conserva SHA-256 de bytes `0ae765f28aab2dd6498b57e111b02ef8e2ba2fe5291065d7b74618eef28bdbf3`. El archivo ajeno `Role.cs` conserva SHA-256 `3260a9378a8bb68317e1d92ce81e290435d29b7d030624d72dfed470a114039a`.

Este arreglo verifica el alcance focal indicado. No demuestra remoción física del módulo, E2E, Meta ni cierre de E8; la puerta completa corresponde al corte final de integración. El implementador entrega sin stage, commit ni push para publicación inmediata de Root.
