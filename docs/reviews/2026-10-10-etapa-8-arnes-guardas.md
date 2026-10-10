# E8: correctivos del inventario de pruebas y fichas del arnés

Base: `79a9661b4d2ba7c47a5c30e0241714cb37e64148`. Entrega del implementador sin stage, commit ni push; Root audita y publica las nueve rutas propias. El índice está vacío.

## Resultado y alcance

El scanner acepta una declaración `partial` sin pruebas locales cuando otra parte de la misma clase contiene un método directo `[Fact]` o `[Theory]`. La identidad incluye proyecto, namespace, tipos contenedores, clase y aridad. No rescatan una clase vacía los homónimos de otro proyecto, namespace, contenedor, aridad o clase no partial. Los tipos `file partial` sólo comparten pruebas dentro de su archivo. Conserva también el inventario de `record class` explícito, que el scanner original detectaba por la palabra `class`: rechaza el vacío y une sus partes verdaderas con prueba directa.

`TestNames` conserva nombres simples para las referencias documentales. `MaskNonCode`, `ClosingBrace`, `HasTestMethod` y `StageRangeForReference` permanecen íntegros; no se quitaron asserts ni se agregaron Skip o listas de excepciones. Un Fact anidado y los señuelos en comentarios o strings no cuentan para el padre.

Las fichas `capas-y-flujo` y `datos-personales` vuelven a tener los cinco encabezados requeridos, en orden. Los bloques completos «Actor neutral de background» y «Ledger técnico de cuota WhatsApp» se trasladaron a `Detalle` como subsecciones, sin cambiar sus párrafos ni anchors. Las referencias a los cinco tests de actores y los tres Durable conservan E8b T05a/T05b y agregan la marca canónica E8. En `idempotencia.md` sólo cambia esa marca: el vencimiento de 24 h sigue sin habilitar repetir un efecto confirmado.

Se agregaron punteros factuales AGENTS/CLAUDE en `Application/Modules/Assistant/Tools/Personal` y `ArchitectureTests/Modules/WhatsApp`. Cada CLAUDE contiene únicamente `@AGENTS.md`.

## Decisiones tomadas

- Se conserva un scanner léxico local, sin agregar paquetes. Resuelve namespaces de archivo y de bloque, incluidos bloques anidados, y nombres/aridad de contenedores class, struct, interface y record. No evalúa preprocesador ni reemplaza la compilación de C#.
- El proyecto se identifica por el `.csproj` más cercano dentro del árbol `tests`; las fixtures sin proyecto usan la raíz `tests` examinada. Si una carpeta contiene varios proyectos, se aísla por archivo de forma conservadora. No interpreta inclusiones MSBuild ni fuentes enlazadas entre proyectos.
- Un contenedor no partial incluye su declaración en la identidad. Un tipo file-local incluye su archivo. Así no se habilita la unión por un nombre simple compartido.
- La clasificación de `record class` explícito ocurre antes de normalizar el shorthand `record` para identidad del contenedor. El shorthand conserva el alcance previo: no se agrega como clase de tests. `record struct` e `interface` siguen siendo contenedores.
- No se ejecutaron Links ni ModuleIsolation adicionales: el cambio es el inventario léxico y sus fichas/punteros; los guards de Harness verifican directamente formato, enlaces, mirrors y referencias.

## Verificación

Todos los comandos literales, inicio/fin UTC, exit code y hashes de logs están en `.local/reviews/e8b-contract/e8-harness-correctives-files.json`. Los logs originales se encuentran en `.local/reviews/e8b-contract/e8-harness-correctives-evidence/`.

| Evidencia | Inicio UTC del 2026-10-10 | Exit | Resultado |
|---|---|---:|---|
| 01-red-build | 23:32:06 | 0 | ArchitectureTests serial, 0 advertencias y 0 errores. |
| 02-partial-red | 23:33:03 | 1 | 19 casos: 7 fallos esperados por partes válidas; 12 controles negativos pasan. |
| 03-green-build | 23:34:00 | 0 | ArchitectureTests serial, 0 advertencias y 0 errores. |
| 04-partial-green | 23:34:31 | 0 | Los mismos 19 casos pasan. |
| 05-harness | 23:35:02 | 0 | HarnessTests completa: 29/29 antes del ajuste final file-local. |
| 06-file-local-red-build | 23:35:46 | 0 | ArchitectureTests serial, 0 advertencias y 0 errores. |
| 07-file-local-red | 23:36:16 | 1 | 1 fallo esperado: las dos partes file-local del mismo archivo deben unirse. |
| 08-final-build | 23:36:28 | 0 | Build de la primera entrega, 0 advertencias y 0 errores. |
| 09-final-focal | 23:37:16 | 0 | 25/25 antes del correctivo de revisión: 20 casos nuevos, caso previo de clase vacía y 4 guards de fichas/punteros/referencias. |
| 10-record-class-red-build | 23:42:54 | 0 | ArchitectureTests serial, 0 advertencias y 0 errores. |
| 11-record-class-red | 23:43:16 | 1 | 2 fallos esperados: record class vacío omitido y partial con Fact ausente de TestNames. |
| 12-record-class-final-build | 23:43:33 | 0 | Build final ArchitectureTests serial, 0 advertencias y 0 errores. |
| 13-record-class-final-focal | 23:43:58 | 0 | 27/27 sobre fuentes finales: los 25 casos previos y los 2 casos nuevos de record class. |

Todas las corridas tienen 0 errores de runner, 0 skipped y 0 not run. Se observaron 32 casos distintos entre las revisiones: 122 ejecuciones de casos, 112 exitosas y 10 fallos rojos esperados. El alcance final ejecutado es 27; el resultado completo 29 corresponde a la revisión anterior a los ajustes file-local y record class. La revisión de Root detectó la omisión de record class antes de publicar; el rojo y el focal final verifican su corrección.

Los builds usaron `--no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:OpenApiGenerateDocuments=false -v:minimal`. No regeneraron OpenAPI. `content-checks.json` verifica los dos bloques íntegros, el cambio exclusivamente documental de idempotencia, las cuatro funciones preservadas, ausencia de Skip/listas nuevas/asserts retirados, índice vacío y las tres rutas documentales ajenas sin cambios.

## Frontera de entrega

Nueve fuentes propias: HarnessTests.cs; las tres fichas; las dos parejas AGENTS/CLAUDE; este informe. El inventario registra para cada una hash SHA-256 y bytes raw, blob Git de base, SHA-256 de sus bytes Git y longitud de base; los archivos nuevos tienen base nula.

`Role.cs` ajeno conserva SHA-256 `3260a9378a8bb68317e1d92ce81e290435d29b7d030624d72dfed470a114039a`. El contrato versionado y `Api/obj/openapi/ArquitecturaBaseMultitenant.Api.json` conservan SHA-256 `86428a7254f5e7f6f412fb06c63e05287655e03f81e17c49ce4a8c88f81928a0`.

No se tocaron `docs/architecture/arbol.md` ni AGENTS/CLAUDE de `Domain.UnitTests/Assistant`, reservados a Coordinación. El paso de Harness no afirma su fusión ni el resultado del detector documental. No se ejecutaron realinear-docs, suites globales, Front, Docker, Aspire ni transportes externos. No hay procesos de build o tests activos al entregar. Esta frontera no cierra E8/E8b ni certifica retiro físico de módulos.
