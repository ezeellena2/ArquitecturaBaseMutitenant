# Correcciones de la revisión E0–E1 Implementation Plan

> **Para agentes:** ejecutar con `subagent-driven-development` o `executing-plans`, tarea por tarea y en orden A → B → C. Las casillas registran TDD. Trabajar en `main`, con commits locales chicos y sin push.

**Goal:** Cerrar los hallazgos técnicos 1–34 de la revisión de E0–E1, conservar la puerta de E2 y aplicar la decisión posterior de contraste del punto 35.

**Architecture:** Corregir primero las fuentes y el generador de datos de referencia, después los contratos y las guardas, y finalmente los detalles de UX y mantenimiento. Cada arreglo tiene una regresión que falla antes del cambio; una ficha contradictoria se actualiza en el mismo commit. Los casos compartidos de formato se prueban con el mismo `docs/contracts/format-cases.json` en C# y TypeScript.

**Tech Stack:** .NET 10, xUnit v3, ASP.NET Core/OpenAPI, PostgreSQL 18/Testcontainers, Node 24, React/Vite, TypeScript/Vitest, CLDR 48.2.

---

## Alcance comprobado antes de programar

- Fuente: `docs/reviews/2026-09-28-revision-etapas-0-1.md` (copiada y commiteada en `e925ed2`). En este plan, `B:` es este repo y `F:` es `../ArquitecturaBaseMutitenantFront`; todas las rutas siguientes son relativas al repo indicado. Ambos `AGENTS.md` y la ficha de la carpeta se releen antes de editarla.
- **8 ya está corregido** por `25a6868`: `ReferenceDataService` proyecta todas las filas con su `IsEnabled`, y `docs/rules/datos-de-referencia.md` ya lo exige. Ejecutar sus pruebas de regresión, sin tocar código ni fabricar otro commit para ese punto.
- **34, subpunto `columns.tsx`:** hoy no existe ningún `src/**/columns.tsx` real en F. No crear una feature ficticia: registrar esta parte como pendiente de E4 y exigir inspección de los archivos reales en el test de columnas cuando nazca el primero. Los otros dos subpuntos de 34 sí se corrigen ahora.
- **35 fue aprobado por el usuario:** `--input: var(--t3)` y oscurecer `--peligro` lo mínimo necesario para contraste ≥ 4,5:1 sobre `--peligro-t`. Se añade una prueba de contraste de tokens y se corrige `tema.md`.
- `F:dist/` es salida de build: agregarlo a `F:.gitignore`; no versionar ni borrar los archivos generados. El contenedor de prueba `codex-mt-phase2-schema` ya se retiró después de comprobar que no tenía referencias en los repos ni pertenecía al Aspire/Testcontainers de E2; no volver a borrarlo ni incluir esta limpieza en un commit.

## Protocolo de cada tarea

1. Escribir una prueba focal que exprese la conducta concreta indicada abajo. Ejecutarla **antes** del arreglo y guardar el fallo esperado. Para artefactos declarativos, la prueba puede verificar la configuración o el resultado de generación; no sirve una aserción de mera existencia.
2. Hacer el cambio mínimo, ejecutar la prueba focal hasta verde y después las pruebas afectadas. Si se cambia una ficha o contrato, hacerlo en ese mismo commit. Revisar `git diff --check` y el status de ambos repos antes de commitear; no incluir `F:dist/`.
3. Comandos focales: `dotnet test --project tests/<Proyecto>/<Proyecto>.csproj -- --filter-class "<Clase>"` en B; `npm test -- <ruta.test.ts[x]>` en F; `npm test` dentro de `B:scripts/datos-de-referencia` para `generar.test.mjs`. Los tests de integración requieren Docker. No ejecutar builds concurrentes de .NET porque comparten artefactos.
4. Los mensajes de commit de las tareas de F se hacen en F; los de B, en B. Una tarea que afecta ambos repos lleva **dos commits** con el mismo punto y sufijo de capa. Una prueba que ya pasa por una corrección previa se registra como «ya resuelto», sin alterar su aserción para fabricar rojo.

## A. Prioridad alta

### 1. Zona predeterminada por país

**Archivos B:** `scripts/datos-de-referencia/{package.json,package-lock.json,sources.lock.json,generar.mjs,generar.test.mjs}`; salidas generadas `src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/Seed/ReferenceData/{countries,time-zones}.json`. **Regla:** `docs/architecture/datos-de-referencia.md` §8 y `docs/rules/datos-de-referencia.md`.

- [ ] Añadir pruebas de DE → `Europe/Berlin`, UA → `Europe/Kyiv`, AR → `America/Argentina/Buenos_Aires`; un país multizona sin principal ni override → `null`.
- [ ] Ejecutar `npm test` en `scripts/datos-de-referencia`; comprobar rojo por zona de la primera fila IANA.
- [ ] Fijar **ya en esta tarea** `cldr-bcp47@48.2.0` en package/lock/sources.lock y usar su `bcp47/timezone.json` para un helper reusable de alias histórico a ID IANA (p. ej. `Europe/Kiev` → `Europe/Kyiv`). Resolver zona única, luego `cldr-core/supplemental/primaryZones.json` normalizado por ese helper, luego override explícito; nunca elegir la primera fila para un multizona ni codificar países particulares.
- [ ] Ejecutar `npm run generate && npm test` allí y confirmar salidas reproducibles.
- [ ] Commit B: `fix: elegir zona principal de cada país desde CLDR`.

### 2. Ciudades de zona y alias CLDR

**Archivos B:** `scripts/datos-de-referencia/{generar.mjs,generar.test.mjs,ciudades.es.json}` y salida `src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/Seed/ReferenceData/time-zones.json`. **Regla:** `datos-de-referencia.md` (arquitectura y ficha), §generación y traducciones.

- [ ] Probar `Asia/Kolkata` → «Calcuta» en es, y alias históricos de Kyiv/Córdoba sin override manual.
- [ ] Ejecutar `npm test` del generador; rojo por ciudad sin traducir.
- [ ] Reutilizar el `cldr-bcp47@48.2.0` y helper de alias ya incorporados en 1; resolver `bcp47/timezone.json` hasta `exemplarCity`, mantener IANA como ID persistido y quitar el override de Córdoba.
- [ ] Regenerar los cinco JSON y repetir `npm test`; ninguna dependencia flotante.
- [ ] Commit B: `fix: resolver ciudades CLDR por alias IANA`.

### 3. Generador en CI y nombres de culturas reproducibles

**Archivos B:** `.github/workflows/ci.yml`, `scripts/datos-de-referencia/{generar.mjs,generar.test.mjs}`, salidas `src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/Seed/ReferenceData/cultures.json`. **Regla:** `docs/rules/datos-de-referencia.md` §Lo verifica; `docs/rules/tests.md` §CI.

- [ ] Probar generación de nombres a partir de `cldr-localenames-full` (`languages.json` y `territories.json`), independiente de `Intl.DisplayNames`; probar que el workflow ejecuta `npm ci && npm test` con `.node-version`.
- [ ] Ejecutar `npm test` en el generador y la prueba del workflow; observar rojo.
- [ ] Sustituir `Intl.DisplayNames` por datos CLDR y agregar `actions/setup-node` más instalación/prueba del generador al CI.
- [ ] `npm run generate && npm test`; verificar `git diff` de los cinco JSON y workflow.
- [ ] Commit B: `fix: probar generador y fijar nombres de culturas en CI`.

### 4. Cerrar bypass de formato manual

**Archivos B:** `tests/ArquitecturaBaseMultitenant.ArchitectureTests/NoManualFormattingTests.cs`; si se requiere permiso explícito, `docs/rules/numeros-y-moneda.md`. **Regla:** `backend.md` §18 y ficha de números.

- [ ] Añadir sondas negativas para interpolación con formato, `string.Format`, `StringBuilder.AppendFormat`, `ToString(IFormatProvider)` y `ToString()` numérico/fecha, además del `ToString(string)` ya cubierto.
- [ ] Ejecutar `NoManualFormattingTests`; rojo por cada forma hasta demostrar que se detecta.
- [ ] Inspeccionar IL/call sites con lista blanca pequeña para conversores wire y `DisplayFormatter`; la guarda debe mirar código productivo, no solo las sondas.
- [ ] Repetir la clase y `ArchitectureTests`; probar que un formato permitido no da falso positivo.
- [ ] Commit B: `test: cerrar escapes de formato manual`.

### 5. Harness descubre tests reales

**Archivos B:** `tests/ArquitecturaBaseMultitenant.ArchitectureTests/HarnessTests.cs`; **Archivos F:** ninguno salvo que su arnés necesite un puntero. **Regla:** `docs/architecture/arnes.md`, `docs/rules/tests.md` y `F:docs/architecture/arnes.md`.

- [ ] Agregar sondas `.test.mjs` y `.test.ts[x]` referenciadas por «Lo verifica», más clase C# `*Tests` sin `[Fact]`/`[Theory]`; el repo hermano se inspecciona si está disponible.
- [ ] Ejecutar `HarnessTests`; rojo al no detectar archivos o al aceptar clase vacía.
- [ ] Ampliar descubrimiento por extensión y verificar al menos un método de test efectivo por clase C#.
- [ ] Ejecutar `HarnessTests` y `ArchitectureTests`; sin falsos positivos por archivos de etapas futuras.
- [ ] Commit B: `test: comprobar pruebas Node TypeScript y clases vacías`.

### 6. Clave idempotente del formulario

**Archivos F:** `src/shared/api/{useIdempotentMutation.ts,useIdempotentMutation.test.ts}`, `docs/rules/datos-y-api.md`; **Archivos B:** `docs/rules/idempotencia.md`. **Regla:** ambas fichas y `F:docs/rules/errores.md`.

- [ ] Probar clave nueva después de 400/422, clave conservada ante red/5xx y `Request.InProgress`, máximo 30 reintentos y ausencia de nuevo intento tras desmontaje.
- [ ] `npm test -- src/shared/api/useIdempotentMutation.test.ts`; rojo por clave retenida/bucle ilimitado.
- [ ] Renovar en 4xx final salvo `Request.InProgress`; introducir contador, cancelación del temporizador y cleanup al desmontar. Corregir ambas fichas que decían «solo tras éxito».
- [ ] Repetir test focal, `npm run lint`; verificar que un 4xx corregido usa otra clave.
- [ ] Commits: F `fix: renovar clave idempotente tras errores definitivos`; B `docs: aclarar renovación de clave idempotente`.

### 7. Campos inválidos no dejan valor anterior

**Archivos F:** `src/shared/ui/fields/{MoneyField,NumberField,PercentField,DateField,DateTimeField,TimeField}.tsx` y sus `*.test.tsx`; `docs/rules/formularios.md`. **Regla:** ficha formularios; `PhoneField`/`TaxIdField` son el patrón.

- [ ] Parametrizar test por seis campos: texto inválido llama `onChange(null)` y `onValidityChange(false)`; el mensaje visible aparece solo tras blur. Reescribir `MoneyField.test.tsx` que fijaba el defecto.
- [ ] `npm test -- src/shared/ui/fields`; rojo por valor anterior y alerta prematura.
- [ ] Conservar draft, propagar invalidez y mostrar error en blur; restablecer validez al corregir; alinear ficha si describe otra conducta.
- [ ] Repetir tests de fields, lint y build.
- [ ] Commit F: `fix: invalidar valores al editar campos con error`.

## B. Prioridad media

**Orden técnico dentro de B:** ejecutar 11 → 10 → 9 para que el resolver único preceda al perfil completo y la API tipada lo reutilice; luego 12–21. Los números siguen los del informe de revisión. Esto mantiene A antes de B y B antes de C.

### 8. Referencias deshabilitadas — ya resuelto

**Verificación B:** `tests/ArquitecturaBaseMultitenant.Application.UnitTests/ReferenceData/ReferenceDataServiceTests.cs` y `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/ReferenceData/ReferenceDataApiTests.cs` exigen filas habilitadas y deshabilitadas con `IsEnabled` real. `docs/rules/datos-de-referencia.md` ya dice lo mismo. **Commit existente:** `25a6868`; no modificar.

### 9. API tipada de formato

**Archivos B:** `src/ArquitecturaBaseMultitenant.Application/Common/Formatting/{DisplayFormatter,CultureProfiles}.cs`, nuevo `DisplayFormatContext.cs`; `tests/ArquitecturaBaseMultitenant.Application.UnitTests/Common/DisplayFormatterTests.cs`; `docs/rules/{numeros-y-moneda,telefonos}.md`. **Regla:** `backend.md` §18.

- [ ] Probar `CreateAsync(culture,timeZone)` una sola lectura del perfil y métodos tipados para `Money`, instante UTC, teléfono, decimal y porcentaje; el despachador JSON solo usa esos métodos para casos de contrato.
- [ ] Ejecutar `DisplayFormatterTests`; rojo por API inexistente.
- [ ] Crear contexto inmutable, métodos tipados y adaptar `FormatAsync`; no dejar `JsonElement` como entrada pública de mensajes de negocio. Corregir firmas de las fichas.
- [ ] Ejecutar `DisplayFormatterTests` y `FormatCasesContractTests`.
- [ ] Commit B: `refactor: exponer formato tipado con contexto cultural`.

### 10. Formato de fecha independiente del host

**Archivos B:** `scripts/datos-de-referencia/{cultures.source.json,generar.mjs,generar.test.mjs}` y salida `Infrastructure/Persistence/Seed/ReferenceData/cultures.json`; `src/ArquitecturaBaseMultitenant.Domain/ReferenceData/Culture.cs`; `src/ArquitecturaBaseMultitenant.Application/{Interfaces/ReferenceData/ICultureCatalog.cs,Models/ReferenceData/ReferenceDataResponse.cs,Common/Formatting/{CultureProfiles,DisplayFormatter}.cs,Services/ReferenceData/ReferenceDataService.cs}`; `src/ArquitecturaBaseMultitenant.Infrastructure/{ReferenceData/JsonReferenceDataCatalog.cs,Persistence/{Configurations/Platform/ReferenceData/CultureConfiguration.cs,Readers/ReferenceDataReader.cs,Seed/ReferenceDataSeeder.cs,Migrations/<EF-generated>_CultureDayPeriods.cs}}`; `src/ArquitecturaBaseMultitenant.Api/Contracts/ReferenceData/ReferenceDataHttpResponse.cs`; pruebas de generador, formatter, seed/reader, API y migración. **F:** tipos de `src/shared/referenceData/referenceData.ts`/schema generado y formato si la nueva propiedad se consume allí. **Regla:** `docs/rules/fechas-y-zonas.md`, `docs/architecture/datos-de-referencia.md` §Cultures.

- [ ] Casos de formato de fecha/hora con separadores, calendario y AM/PM del catálogo bajo distintas culturas de proceso; comprobar que el seed/migración preservan designadores y que la API los expone.
- [ ] Ejecutar prueba focal; rojo al heredar ajustes del ICU del host.
- [ ] Agregar `AmDesignator`/`PmDesignator` a la fuente versionada, JSON generado, entidad, catálogo, EF, migración, seed, reader y contrato API. Clonar `DateTimeFormat`, fijar `/`, `:`, `GregorianCalendar` y esos designadores; `NumberFormat.NegativeSign = "-"`. No derivarlos del ICU del host al ejecutar.
- [ ] Ejecutar generador, seed/migración con Testcontainers, formato back, `npm run contracts` y paridad de `format-cases.json` en F.
- [ ] Commits: B `fix: fijar fecha y signo desde perfil de cultura`; F `chore: actualizar contrato y perfil de cultura` cuando cambien schema, tipos o formato del front (la tarea no termina con cambios sin commitear).

### 11. Unificar resolución y fallback de cultura

**Archivos B:** `src/ArquitecturaBaseMultitenant.Application/{Common/Formatting/CultureProfiles.cs,Services/ReferenceData/ReferenceDataService.cs,Interfaces/ReferenceData/ICultureCatalog.cs,Resources/FormattingTexts.cs}`; tests `Application.UnitTests/{Common/DisplayFormatterTests.cs,ReferenceData/ReferenceDataServiceTests.cs}`. **Regla:** `datos-de-referencia.md` y `textos-y-traducciones.md`.

- [ ] Probar código con distinto casing, pedido deshabilitado, fallback deshabilitado y cadena de `FallbackCulture` que gobierna también `FormattingTexts`.
- [ ] Ejecutar ambas clases; rojo por resolver divergente.
- [ ] Extraer un solo resolver `CultureProfiles`, reutilizarlo en servicio y formato; quitar `DisplayName` duplicado y saltar fallbacks deshabilitados.
- [ ] Repetir pruebas de servicio/formato y contrato de referencias.
- [ ] Commit B: `refactor: resolver cultura y caída en un solo lugar`.

### 12. Validación dot-atom y límites de Email

**Archivos B:** `src/ArquitecturaBaseMultitenant.Domain/ValueObjects/Email.cs`, `tests/ArquitecturaBaseMultitenant.Domain.UnitTests/ValueObjects/EmailTests.cs`, `docs/contracts/format-cases.json`, `tests/ArquitecturaBaseMultitenant.Application.UnitTests/Common/FormatCasesContractTests.cs`; **Archivo F:** `src/shared/format/formatters.test.ts` si requiere leer `error`. **Regla:** `docs/rules/emails.md`, `docs/architecture/formatos` del front.

- [ ] Casos inválidos de coma, `; < > ( ) [ ] " \\ :`, Cc/Cf, punto al borde y `..`; IDN válido y límites exactos por bytes UTF-8. Añadir `error` opcional al contrato para casos inválidos.
- [ ] Ejecutar `EmailTests` y pruebas contractuales; rojo por aceptación indebida.
- [ ] Validar local dot-atom y longitud en bytes, sin perder IDN; adaptar lectores del contrato en ambos lados para `error`.
- [ ] Repetir tests de email y casos de formato back/front.
- [ ] Commits: B `fix: validar correo dot-atom y límites UTF-8`; F `test: leer errores esperados del contrato de formato` si F cambia.

### 13. Tipos OpenAPI fieles al JSON

**Archivos B:** `src/ArquitecturaBaseMultitenant.Api/{OpenApi/OpenApiExtensions.cs,Json/JsonConfiguration.cs,Json/NormalizedStringJsonConverter.cs}`, `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Contracts/{OpenApiTests,OpenApiContractTests}.cs`, `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Api/NormalizedInputTests.cs`, `docs/contracts/openapi.json`. **Regla:** `docs/rules/api-http.md`, `docs/rules/validacion.md`.

- [ ] Probar nullable string no requerido, `items` en arrays y enteros sin `string` ni `null` extra; comparar esquema generado con serialización real. Añadir regresión de POST a `TestFeatures/TestController` que devuelve el cuerpo normalizado (sin persistencia), para demostrar que quitar el conversor global no elimina normalización de entrada.
- [ ] Ejecutar `OpenApiTests`; rojo por transformador global/conversor.
- [ ] Usar `JsonNumberHandling.Strict`; limitar normalización de strings al resolver de propiedades de entrada, no deformar tipos globales del esquema.
- [ ] Regenerar OpenAPI, ejecutar `OpenApiTests` y `NormalizedInputTests`, y `npm run contracts:check` en F; si cambia `schema.d.ts`, `npm run contracts` y commit F separado.
- [ ] Commit B: `fix: generar tipos OpenAPI acordes al contrato JSON`.

### 14. Esquema explícito de ProblemDetails

**Archivos B:** `src/ArquitecturaBaseMultitenant.Api/OpenApi/ProducesProblemAttribute.cs`, nuevo `ApiProblemDetails.cs` o transformador equivalente, `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Contracts/OpenApiContractTests.cs`, `docs/contracts/openapi.json`; **F:** `src/shared/api/generated/schema.d.ts` si cambia. **Regla:** `docs/rules/result-y-errores.md`, `docs/rules/api-http.md`.

- [ ] Probar que 400/409/429 declaran `code`, `traceId`, `errors` y `retryAfter` con su nulabilidad, y que payload real conserva `application/problem+json`.
- [ ] Ejecutar `OpenApiContractTests`; rojo por `ProblemDetails` genérico.
- [ ] Tipar la respuesta OpenAPI con record/transformador, mantener el mapper de runtime como fuente de valores.
- [ ] Regenerar contratos, correr pruebas back y `npm run contracts:check` F.
- [ ] Commit B: `fix: describir extensiones de ProblemDetails en OpenAPI` (F separado si cambia generado).

### 15. Arranque front si falla referencias

**Archivos F:** `src/app/providers.tsx`, `src/shared/format/useFormat.ts`, `src/shared/api/queryClient.ts`, `src/shared/i18n/index.ts`, `src/App.test.tsx`; nuevos tests del proveedor si conviene. **Regla:** `docs/rules/{datos-y-api,errores,textos-y-traducciones}.md`.

- [ ] Probar fallo inicial de `GET /api/reference-data`: textos empaquetados visibles, estado de error con «Reintentar» y Toaster operativo.
- [ ] `npm test -- src/App.test.tsx`; rojo por suspensión/pantalla vacía.
- [ ] Inicializar i18n con locales empaquetados antes del catálogo; mostrar estado `isError` con `refetch`, ubicar Toaster fuera del suspense.
- [ ] Repetir tests de arranque, build y lint.
- [ ] Commit F: `fix: mostrar reintento si falla catálogo al iniciar`.

### 16. Nueva versión sin romper la app

**Archivos F:** `src/layouts/{AppShell.tsx,AppShell.test.tsx}`, `src/app/{router.tsx,routes.tsx}`, nuevo `src/layouts/AppErrorBoundary.tsx` o `errorElement`, `src/shared/phone/{CountryFlag.tsx,countries.test.ts}`, locales es/en. **Regla:** `docs/rules/errores.md`, `docs/rules/accesibilidad.md`.

- [ ] Cambiar test que esperaba `preventDefault`; probar error de precarga, pantalla traducida de error y bandera cuyo import falla sin derribar app.
- [ ] Ejecutar `AppShell.test.tsx` y `countries.test.ts`; rojo.
- [ ] No cancelar el evento de Vite; usar ErrorBoundary/`errorElement` traducido y una acción explícita «Actualizar». Nunca recargar automáticamente: `frontend.md` exige que la persona decida para no perder un formulario. Dar a la bandera un fallback con rol/etiqueta accesible.
- [ ] Repetir pruebas, paridad de locales, build y lint.
- [ ] Commit F: `fix: contener errores de precarga y banderas`.

### 17. Porcentajes sin pérdida por multiplicación

**Archivos F:** `src/shared/format/{parsers.ts,parsers.test.ts,formatters.ts,formatters.test.ts}`; **B:** `docs/contracts/format-cases.json`, `Application.UnitTests/Common/FormatCasesContractTests.cs`. **Regla:** `F:docs/architecture/formatos.md` §porcentaje.

- [ ] Añadir al contrato un caso `type=percent,input=0.00115` y otro `input=0.082,expected="8,2 %",parseInput="8,2",parsedExpected=0.082`. El lector front verifica además `parseInput`; el back verifica el texto de salida. Así el contrato conserva su forma de salida común y prueba el parseo donde existe.
- [ ] Ejecutar tests de parsers/formatters; rojo por residuo binario.
- [ ] Desplazar exponente decimal al convertir porcentaje (`Number(`${n}e-2`)` y operación inversa), preservar validación de rango.
- [ ] Ejecutar casos contractuales B/F y tests focales.
- [ ] Commits: B `test: fijar casos de porcentaje de precisión fina`; F `fix: evitar residuo binario al convertir porcentajes`.

### 18. Paginación y URL canónica

**Archivos F:** `src/shared/hooks/{usePagination.ts,usePagination.test.tsx}`; `docs/rules/paginado-y-listados.md`. **Regla:** ficha de paginado y `B:docs/rules/paginado-y-busqueda.md`.

- [ ] Probar sort inicial `-createdAtUtc` alternando a ascendente; `page`/`pageSize` inválidos, fraccionarios o fuera de lista vuelven a 1/10 con `replace` sin nueva entrada de historial.
- [ ] Ejecutar `usePagination.test.tsx`; rojo.
- [ ] Normalizar al leer URL; alternar `-field → field → -field`; mantener corrección de página vacía ya existente.
- [ ] Repetir pruebas de hook y componente `Pagination`.
- [ ] Commit F: `fix: normalizar paginado y alternar orden descendente`.

### 19. Importe sin pérdida silenciosa de precisión

**Archivos F:** `src/shared/format/{parsers.ts,parsers.test.ts}`, `src/shared/ui/fields/MoneyField.test.tsx`; `docs/rules/formatos.md`. **Regla:** `B:docs/rules/numeros-y-moneda.md`.

- [ ] Probar >15 dígitos significativos y decimal cuyo `Number` no recompone el canónico; debe quedar inválido, no enviar monto redondeado.
- [ ] Ejecutar `parsers.test.ts`; rojo por aceptación actual.
- [ ] Comparar decimal canónico y conversión reversible con límite de 15 dígitos antes de devolver `MoneyValue`.
- [ ] Repetir parsers y `MoneyField.test.tsx`.
- [ ] Commit F: `fix: rechazar montos con precisión no representable`.

### 20. Selectores con opción elegida visible

**Archivos F:** `src/shared/ui/fields/{CountrySelect,CurrencySelect,TimeZoneSelect,CultureSelect}.tsx` y sus `.test.tsx`, `src/locales/{es,en}/common.json`; `docs/rules/telefonos.md` si se cambia buscador. **Regla:** `F:docs/rules/formularios.md` y ficha teléfonos.

- [ ] Probar filtro que excluye la opción elegida: el trigger conserva su texto; cada búsqueda tiene nombre propio accesible, p. ej. «Buscar país».
- [ ] Ejecutar tests de los cuatro selectores; rojo por `selected` buscado solo entre opciones filtradas.
- [ ] Buscar valor elegido en catálogo completo, filtrar únicamente la lista del popup; dar etiquetas i18n específicas al buscador.
- [ ] Repetir tests, paridad de traducciones y accesibilidad.
- [ ] Commit F: `fix: conservar selección al buscar en catálogos`.

### 21a. Guardas de catálogo y literales

**Archivos B:** `tests/ArquitecturaBaseMultitenant.Application.UnitTests/ReferenceData/ReferenceDataServiceTests.cs`, `tests/ArquitecturaBaseMultitenant.ArchitectureTests/ReferenceDataHardcodeTests.cs`. **Regla:** `docs/rules/datos-de-referencia.md` §Lo verifica.

- [ ] Usar fixture de zona compartida habilitada y sonda de literal en enum/reflexión, `CallSites.Literals` y `src/**/*.json` fuera de salidas autorizadas.
- [ ] Ejecutar las dos clases; rojo al introducir cada sonda.
- [ ] Corregir guardas y fixture; retirar sondas que representan código prohibido luego de verificar que rompen.
- [ ] Repetir tests y arquitectura completos.
- [ ] Commit B: `test: vigilar referencias y literales en todo el código`.

### 21b. Guardas de controller, rutas e idempotencia

**Archivos B:** `tests/ArquitecturaBaseMultitenant.ArchitectureTests/{ControllerServiceRepositoryTests,IdempotentActionsTests}.cs`, `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Contracts/ExplicitRouteInventoryTests.cs`; `docs/rules/{api-http,idempotencia}.md`. **Regla:** `AGENTS.md` capas, `api-http.md` y `idempotencia.md`.

- [ ] Sondas: dependencia `.Interfaces.ReferenceData` o parámetro de acción indebidos, clase que hereda `ControllerBase`, ruta sin verbo, prefijo ausente en `BackendPrefixes` y POST que usa `ToCreatedResult`/`ToAcceptedResult` sin idempotencia.
- [ ] Ejecutar clases focales; rojo por cada escape.
- [ ] Inspeccionar parámetros y herencia, inventariar endpoint salvo health y analizar IL de los resultados de creación; actualizar fichas si el inventario cambia.
- [ ] Repetir guardas e integración de rutas.
- [ ] Commit B: `test: cerrar guardas de controllers rutas e idempotencia`.

### 21c. Guardas de límites, precisión, logs y validadores

**Archivos B:** `tests/ArquitecturaBaseMultitenant.ArchitectureTests/{TextLimitsTests,DecimalPrecisionTests}.cs`; crear `SensitiveToStringLoggingTests.cs` y `InjectedValidatorTests.cs` allí; `docs/rules/{logs,validacion}.md` si se ajusta el enunciado. **Regla:** ambas fichas y `persistencia-ef.md`.

- [ ] Sondas de `MaximumLength|Length|StringLength`, límite exacto, precisión en modelo EF real con owned/complex, `ToString` sensible en logs y validadores creados con `new` en servicios.
- [ ] Ejecutar clases focales; rojo mientras el control solo revisa sintaxis parcial.
- [ ] Inspeccionar metadata de EF y fuentes/IL de llamadas; proveer tests reales a ambos «Lo verifica».
- [ ] Repetir arquitectura completa y construir sin advertencias.
- [ ] Commit B: `test: cubrir límites precisión logs y validadores`.

## C. Prioridad baja

### 22. Offset para un instante explícito

**Archivos B:** `src/ArquitecturaBaseMultitenant.Application/Interfaces/Integrations/Time/ITimeZoneService.cs`, `src/ArquitecturaBaseMultitenant.Infrastructure/Time/TimeZoneService.cs`, `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Time/TimeZoneServiceTests.cs`; consumidores de `DisplayFormatter.cs`. **Regla:** `docs/rules/fechas-y-zonas.md`.

- [ ] Probar offsets de la misma zona antes/después de DST mediante `GetUtcOffset(id, instantUtc)`.
- [ ] Ejecutar `TimeZoneServiceTests`; rojo por método ausente.
- [ ] Agregar sobrecarga explícita y delegar la de «ahora» al `TimeProvider` si sigue necesaria.
- [ ] Repetir pruebas de zona y formato.
- [ ] Commit B: `feat: calcular offset para un instante explícito`.

### 23. Redondeo antes de signo y unidad

**Archivos B:** `src/ArquitecturaBaseMultitenant.Application/Common/Formatting/DisplayFormatter.cs`, `tests/ArquitecturaBaseMultitenant.Application.UnitTests/Common/DisplayFormatterTests.cs`, `docs/contracts/format-cases.json`; **F:** `src/shared/format/{formatters.ts,formatters.test.ts}`. **Regla:** `backend.md` §18, `F:formatos.md`.

- [ ] Casos en el límite de redondeo: monto negativo que redondea a cero; compacto 999,95K → unidad siguiente; tamaño 999,95KB → unidad siguiente.
- [ ] Ejecutar casos en B/F; rojo por signo/unidad decididos antes de redondear.
- [ ] Elegir magnitud y signo del valor mostrado tras redondear, iterando unidad si cruza umbral; sincronizar los dos formateadores.
- [ ] Repetir todos los casos contractuales en B/F con texto idéntico.
- [ ] Commits: B `fix: fijar signo y unidad tras redondear formatos`; F `fix: igualar redondeo de signos y unidades`.

### 24. Preservar ZWJ/ZWNJ en texto libre

**Archivos B:** `src/ArquitecturaBaseMultitenant.Application/Common/Text/TextNormalizer.cs`, `tests/ArquitecturaBaseMultitenant.Application.UnitTests/Common/TextNormalizerTests.cs`; `docs/rules/textos-libres.md`. **Regla:** ficha de textos.

- [ ] Probar secuencias legítimas con ZWJ/ZWNJ conservadas y una lista cerrada de invisibles peligrosos eliminados.
- [ ] Ejecutar `TextNormalizerTests`; rojo por pérdida de joins.
- [ ] Sustituir descarte general `UnicodeCategory.Format` por lista concreta, sin abrir controles Cc peligrosos.
- [ ] Repetir normalizador y tests de entrada HTTP normalizada.
- [ ] Commit B: `fix: conservar uniones Unicode en texto libre`.

### 25. Tamaños de página en una sola fuente

**Archivos B:** `src/ArquitecturaBaseMultitenant.Application/Common/{Pagination/PagedRequest.cs,Validation/PagedRequestValidator.cs}`, `src/ArquitecturaBaseMultitenant.Api/OpenApi/OpenApiExtensions.cs`, `tests/ArquitecturaBaseMultitenant.Application.UnitTests/Common/PagedRequestValidatorTests.cs`, `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Contracts/OpenApiTests.cs`, `docs/contracts/openapi.json`; **F:** `src/shared/api/generated/schema.d.ts` si cambia. **Regla:** `docs/rules/paginado-y-busqueda.md`.

- [ ] Probar que 10/20/50/100 se extraen de `PagedRequest.AllowedPageSizes` y OpenAPI muestra exactamente ese enum.
- [ ] Ejecutar validator/OpenAPI; rojo por duplicación y ausencia de enum.
- [ ] Centralizar colección inmutable y usarla en validator y transformador de esquema.
- [ ] Regenerar OpenAPI, correr pruebas back y `npm run contracts` más `npm run contracts:check` en F.
- [ ] Commits: B `refactor: publicar tamaños de página desde un solo catálogo`; F `chore: actualizar contrato de tamaños de página` si cambia el generado.

### 26. Cancelación no es error de operación

**Archivos B:** `src/ArquitecturaBaseMultitenant.Application/Common/Logging/OperationLog.cs`, `tests/ArquitecturaBaseMultitenant.Application.UnitTests/Common/OperationLogTests.cs`; `docs/rules/logs.md`. **Regla:** ficha de logs.

- [ ] Probar `OperationCanceledException`: se propaga y no emite evento Error 103.
- [ ] Ejecutar `OperationLogTests`; rojo por log de error.
- [ ] Capturar cancelación antes de excepción general y omitir `LogException`.
- [ ] Repetir tests y verificar que excepciones reales siguen en Error.
- [ ] Commit B: `fix: no registrar cancelaciones como fallas`.

### 27. Orden cultural de catálogos

**Archivos B:** `src/ArquitecturaBaseMultitenant.Application/Services/ReferenceData/ReferenceDataService.cs`, `tests/ArquitecturaBaseMultitenant.Application.UnitTests/ReferenceData/ReferenceDataServiceTests.cs`; `docs/rules/datos-de-referencia.md` si no describe orden. **Regla:** datos de referencia.

- [ ] Probar nombres con tildes/casos y mismo `SortOrder` bajo cultura solicitada.
- [ ] Ejecutar `ReferenceDataServiceTests`; rojo por comparación ordinal.
- [ ] Usar comparador cultural del perfil resuelto, sin cambiar el orden primario explícito.
- [ ] Repetir service/API tests de referencias.
- [ ] Commit B: `fix: ordenar nombres de referencia con cultura efectiva`.

### 28. Respuesta 429 del rate limiter

**Archivos B:** `src/ArquitecturaBaseMultitenant.Api/DependencyInjection.cs`, `src/ArquitecturaBaseMultitenant.Api/ErrorHandling/ProblemDetailsMapper.cs` si se necesita, `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/ErrorHandling/RateLimiterTests.cs`; `docs/rules/api-http.md`. **Regla:** `result-y-errores.md`, `api-http.md`.

- [ ] Probar rechazo real: HTTP 429, `application/problem+json`, `code=Http.TooManyRequests`, `traceId`, `retryAfter` y header `Retry-After` concordantes.
- [ ] Ejecutar `RateLimiterTests`; rojo por configuración vacía.
- [ ] Fijar `RejectionStatusCode=429` y `OnRejected` usando el mapper y metadata de retry.
- [ ] Repetir integración y contrato OpenAPI si la respuesta declarada cambia.
- [ ] Commit B: `fix: devolver ProblemDetails y Retry-After al limitar`.

### 29. CI y SDK reproducibles

**Archivos B:** `global.json`, `Directory.Build.props`, `.github/workflows/ci.yml`; **F:** `.github/workflows/ci.yml`; tests nuevos de configuración bajo `tests/ArquitecturaBaseMultitenant.ArchitectureTests/BuildConfigurationTests.cs` o `scripts/datos-de-referencia/generar.test.mjs` si conviene. **Regla:** `AGENTS.md` §Build, `docs/rules/tests.md`.

- [ ] Probar `rollForward=latestPatch`, `AnalysisLevel` explícito, `dotnet test --solution ArquitecturaBaseMultitenant.slnx` y actions fijadas por SHA en ambos CI.
- [ ] Ejecutar prueba de configuración; rojo por valores actuales.
- [ ] Cambiar configuración y fijar SHAs oficiales de las versiones usadas; mantener semántica de ambos workflows.
- [ ] Ejecutar build/tests locales y validador de workflow.
- [ ] Commits: B `chore: fijar SDK análisis y acciones de CI`; F `chore: fijar acciones del CI frontend`.

### 30. JSON estricto para enum y fecha desplazada

**Archivos B:** `src/ArquitecturaBaseMultitenant.Api/Json/JsonConfiguration.cs`, tests nuevos `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Json/EnumAndOffsetJsonTests.cs`; `docs/rules/api-http.md`. **Regla:** `backend.md` §18, `fechas-y-zonas.md`.

- [ ] Probar que enum numérico da 400 y que `DateTimeOffset` en contrato HTTP se rechaza por guarda de arquitectura (o se serializa UTC explícito si existe uso legítimo).
- [ ] Ejecutar clase focal; rojo por `JsonStringEnumConverter` permisivo.
- [ ] Configurar `allowIntegerValues:false`; prohibir `DateTimeOffset` en contratos con test de inventario, sin duplicar conversión de `DateTime` UTC.
- [ ] Repetir JSON/OpenAPI y arquitectura.
- [ ] Commit B: `fix: rechazar enums numéricos y contratos de hora ambiguos`.

### 31. Errores declarados en campos estáticos

**Archivos B:** `tests/ArquitecturaBaseMultitenant.ArchitectureTests/ErrorCodeTests.cs`; `docs/rules/result-y-errores.md` si hace falta. **Regla:** `AGENTS.md` §Result.

- [ ] Sonda de campo estático `Error` con código duplicado o fuera de `Area.Entidad.Motivo`.
- [ ] Ejecutar `ErrorCodeTests`; rojo al comprobar que la sonda se ignora.
- [ ] Recorrer también campos estáticos de tipo `Error` en clases `*Errors` conocidas y leer `Error.Code`; `GetValue` inicializa la clase, así que limitar reflexión a esas declaraciones puras y no inspeccionar tipos arbitrarios con inicializadores de efectos. También se puede extraer el literal del código desde fuente/IL si alguna clase deja de ser pura.
- [ ] Repetir test y `ArchitectureTests`.
- [ ] Commit B: `test: revisar también errores declarados como campos`.

### 32. Offset independiente de la cultura del usuario

**Archivos F:** `src/shared/format/{formatters.ts,formatters.test.ts}`; `docs/rules/formatos.md`. **Regla:** `F:docs/architecture/formatos.md` §zona horaria.

- [ ] Probar `formatTimeZone` con perfiles `fr-FR` y `sv-SE` ficticios del test, donde `Intl` puede traducir «GMT»; offset esperado con signo tipográfico.
- [ ] Ejecutar `formatters.test.ts`; rojo por parseo dependiente de cultura.
- [ ] Obtener offset con locale fija `en` o cálculo ya centralizado en `shared/time`.
- [ ] Repetir formatos y contrato cruzado.
- [ ] Commit F: `fix: calcular offset sin depender del idioma visible`.

### 33. `localStorage` puede fallar en el arranque

**Archivos F:** nuevo `src/shared/hooks/safeStorage.ts` y `safeStorage.test.ts`; adaptar `src/app/providers.tsx`, `src/shared/i18n/index.ts`, `src/shared/referenceData/useReferenceData.ts`, `src/shared/format/useFormat.ts`; `docs/rules/datos-y-api.md`. **Regla:** `AGENTS.md` §tokens/almacenamiento.

- [ ] Mockear `getItem`/`setItem` que lanzan: app inicia con cultura por defecto y sigue pudiendo cambiar en memoria.
- [ ] Ejecutar tests focales de i18n, formato, referencias y almacenamiento; rojo por excepción en render.
- [ ] Centralizar acceso en helper con try/catch; reutilizarlo también al guardar cultura, sin capturar errores ajenos.
- [ ] Repetir tests de arranque, build y lint.
- [ ] Commit F: `fix: tolerar almacenamiento local bloqueado`.

### 34. Pruebas front sin falsos verdes

**Archivos F:** `src/test/utils/renderWithProviders.tsx`, test de aislamiento nuevo `src/test/utils/renderWithProviders.test.tsx`, `src/locales/parity.test.ts`, `src/shared/ui/columns-mobile.test.ts`; **dato B:** `src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/Seed/ReferenceData/cultures.json`. **Regla:** `F:docs/rules/tests.md`.

- [ ] Probar dos renders sucesivos sin compartir `queryClient`; comprobar paridad de idiomas de culturas habilitadas del catálogo, no solo carpetas disponibles. En local con repo hermano, la lectura del JSON B es obligatoria; en CI aislado de F, saltar **solo** este chequeo cruzado con un aviso explícito en el log, manteniendo el resto de `parity.test.ts`.
- [ ] Ejecutar tests focales; rojo por cliente global y cultura habilitada omitida.
- [ ] Inyectar un único QueryClient nuevo por render a `AppProviders` y derivar idiomas del JSON de culturas. No inventar `columns.tsx`: dejar guardia dinámica preparada y registrar ese subpunto para E4.
- [ ] Repetir tests de utilidades, locales y tabla móvil; confirmar que no hay `columns.tsx` real ahora.
- [ ] Commit F: `test: aislar providers y exigir paridad de culturas`.

## 35. Contraste y limpieza aprobados después de la revisión

**Archivos F:** `src/index.css`, `src/theme-tokens.test.ts`, `docs/architecture/tema.md`, `.gitignore`. **Regla:** decisión expresa del usuario y `tema.md` §Colores/Lo verifica.

- [ ] Añadir prueba de contraste calculado: borde `--input` respecto de fondo ≥3:1 y `--peligro` sobre `--peligro-t` ≥4,5:1; probar que `dist/` es ignorado.
- [ ] `npm test -- src/theme-tokens.test.ts`; rojo por 1,29:1 y 4,47:1.
- [ ] Cambiar `--input` a `var(--t3)`; bajar solo la luminosidad de `--peligro` hasta alcanzar el umbral. Reflejar valor preciso en `tema.md`; agregar `/dist/` a `.gitignore`.
- [ ] Repetir prueba, build/lint/test del front; revisar que `git status` ya no muestre `dist/`.
- [ ] Commit F: `fix: asegurar contraste de controles y peligro`.

## Puerta de salida

1. **Generador B:** `cd scripts/datos-de-referencia; npm ci; npm run generate; npm test`; salidas byte a byte estables y sin diferencias inesperadas. `sources.lock.json`/`package-lock.json` fijan versiones y hashes.
2. **Back:** `dotnet build ArquitecturaBaseMultitenant.slnx` sin advertencias; `dotnet test` completo con Docker/Testcontainers, sin omitidos. Mantener los dos tests nominales de aislamiento `Rls_blocks_cross_tenant_even_with_filters_ignored` y `Runtime_role_is_not_privileged`, seed idempotente, cinco esquemas e ICU `es-AR`.
3. **Contratos:** regenerar `docs/contracts/openapi.json` por el build; `F:npm run contracts` y `F:npm run contracts:check` sin diff. Back y front producen exactamente el mismo texto en todos los casos de `docs/contracts/format-cases.json`, incluidos los nuevos de error y redondeo.
4. **Front:** `npm run build`, `npm run lint`, `npm test` limpios; `src/theme-tokens.test.ts` prueba contrastes aprobados; ningún `dist/` versionado.
5. **Aspire:** `aspire run` levanta PostgreSQL migrado, API y front; `/alive` responde 200; cerrar con `aspire stop`. `HarnessStage` permanece en 2 en ambos repos. Revisar ramas `main`, commits locales sin push y `git status` limpio salvo artefactos ignorados. Confirmar que `codex-mt-phase2-schema`, ya retirado, no reapareció.
6. **Informe final:** tabla de puntos 1–35 con commit(s), «no aplicados» (8 ya resuelto; 34/columnas diferido hasta E4 si siguen inexistentes), «Decisiones tomadas» y resultados concretos de la puerta. No incluir valores de secretos.
