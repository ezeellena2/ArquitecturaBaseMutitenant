# Etapa 1: núcleo transversal Implementation Plan

> **Para agentes:** al ejecutar este plan, usar la skill executing-plans o subagent-driven-development y seguir las casillas tarea por tarea. Este documento solo planifica; su aprobación precede a cualquier implementación.

**Goal:** Dejar probados Result, errores, cultura, tiempo, validación, logging, OpenAPI y la presentación unificada de datos en el backend y el front.

**Architecture:** El backend conserva Domain ← Application ← Infrastructure, con Api como composición. El front concentra acceso HTTP en shared/api y toda presentación en shared/format y shared/ui/format. Ambos consumen los mismos casos de formato versionados por el backend.

**Tech Stack:** .NET 10, ASP.NET Core MVC, xUnit v3, React, TypeScript, Vite, Vitest, TanStack Query y PostgreSQL/Aspire ya preparados en la Etapa 0.

---

## Decisiones recibidas para ejecutar la Etapa 1

No quedan preguntas técnicas pendientes. Según AGENTS.md, una duda nueva se resuelve primero con `../ArquitecturaBase` o `../ArquitecturaBaseFront`; si no existe allí, se elige la solución más simple compatible con los documentos y se registra en «Decisiones tomadas» del informe final. Solo se consulta si cambia el producto o contradice una regla escrita.

1. `format-cases.json` usa el esquema de ArquitecturaBase si existe; si no, `{ "now": "2026-09-27T15:00:00Z", "cases": [{ "id", "type", "culture", "timeZone", "input", "expected" }] }`. Incluye **todos** los tipos de formatos.md en `es-AR` y `en-US`. Cada caso debe pasar en ambos lados. El value object `TaxId` sigue en E6; el texto fiscal de los casos compartidos se produce en E1 a partir de `{ type, number }`.
2. `contracts:check` es obligatorio en local. En el CI del front, si falta el repo hermano, se salta con un aviso visible; cuando ambos repos estén en GitHub se agrega el checkout cruzado.
3. `GET /api/time-zones` lleva `[AllowAnonymous]` y entra en la lista explícita de `AccessDeclarationTests` al nacer esa guarda.
4. El catálogo horario copia ArquitecturaBase si existe. En su ausencia, expone identificador IANA, label por cultura desde resx y offset calculado con `TimeProvider` como `GMT−3`, para Buenos Aires, Córdoba, Mendoza, Tucumán, Montevideo, Santiago, Asunción y UTC.
5. En E1, `useFormat` usa proveedor con `es-AR`, Buenos Aires y ARS por defecto y lee la cultura de `localStorage`; en E3 se conecta a `/api/me`.
6. El teléfono se muestra en formato nacional si su país coincide con el de la cultura, e internacional en caso contrario. Los casos compartidos fijan el texto exacto si las librerías difieren.
7. Las versiones se toman de ArquitecturaBase cuando estén; para las dependencias faltantes se fija la última estable exacta en `Directory.Packages.props` o `package.json`. `Microsoft.Extensions.ApiDescription.Server` exporta `docs/contracts/openapi.json` durante el build.
8. El catálogo de moneda copia ArquitecturaBase si existe; si no, ARS, USD, EUR, BRL y UYU tienen dos decimales, CLP y PYG cero; cualquier otra se rechaza.
9. `ValidPermissions` nace en E4 con el catálogo. La validación de E1 no lo anticipa.
10. `UniqueConstraintViolationException` nace en E2 con `IUnitOfWork`; queda fuera de E1.
11. `NormalizedInputTests` hace POST a `TestFeatures/TestController`, que devuelve el cuerpo ya normalizado; no usa persistencia E2.
12. `TaxIdField` queda en E1: valida solo mediante `stdnum` y emite `{ type, number }`, con `number` sin separadores. El value object del back nace en E6.
13. E1 cubre falta de conexión, versión nueva y 429: `retryAfterSeconds` en `httpClient` y cuenta regresiva en el botón iniciador. El flujo de sesión vencida espera E3.
14. `usePagination(result?: { items; totalCount })` corrige internamente la página solo al recibir `items` vacíos con `totalCount > 0`, con `replace` y sin entrada nueva en el historial; si ArquitecturaBaseFront resuelve el mismo caso de otro modo, se copia ese patrón.
15. `/swagger` y `/openapi` se agregan a `BackendPrefixes` y al proxy de Vite solo en Development.
16. `statusTones` de E1 tiene `success`, `warning`, `danger`, `neutral` y `pending` según tema.md. Los estados de negocio llegan con cada feature; un enum de TestFeatures basta para probarlo.
17. E1 conecta en el orden de backend.md §16 el pipeline disponible. Se dejan comentadas las posiciones de `UseAuthentication`, `TenantResolutionMiddleware` y `UseAuthorization` (E3) y del bootstrap de base (E2); `PublicSiteResolutionMiddleware` y `LegalAcceptanceMiddleware` conservan sus etapas E7/E3. Las piezas no disponibles no se activan antes de su etapa.

## Método de ejecución y rutas

- **Orden obligatorio:** una tarea, su test rojo, implementación mínima, test verde, commit en main del repo indicado; sin push. Antes de cada tarea releer plan maestro “Reglas para todas las etapas” y “Etapa 1”, y las entradas [E1] de ambos arbol.md. Toda duda técnica se resuelve con las bases de solo lectura o con la opción más simple compatible, y se registra en «Decisiones tomadas»; solo frenan cambios de producto o contradicciones escritas.
- **Comandos de prueba:** D = dotnet test --project tests/ArquitecturaBaseMultitenant.Domain.UnitTests/ArquitecturaBaseMultitenant.Domain.UnitTests.csproj; A = dotnet test --project tests/ArquitecturaBaseMultitenant.Application.UnitTests/ArquitecturaBaseMultitenant.Application.UnitTests.csproj; I = dotnet test --project tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/ArquitecturaBaseMultitenant.Api.IntegrationTests.csproj; R = dotnet test --project tests/ArquitecturaBaseMultitenant.ArchitectureTests/ArquitecturaBaseMultitenant.ArchitectureTests.csproj. En cada tarea, “prueba X” significa ejecutar el comando correspondiente con -- --filter-class X, comprobar FAIL por la conducta aún ausente, implementar y repetir hasta PASS. Para front, ejecutar npm test -- ruta/del/test desde ../ArquitecturaBaseMutitenantFront; el primer pase debe fallar por el caso nuevo y el segundo pasar. Si una prueba exige compilación, preparar únicamente el armazón necesario para obtener el fallo de comportamiento, sin adelantar la implementación.
- **Rutas:** las rutas back son relativas a este repo; las front empiezan por ../ArquitecturaBaseMutitenantFront/. Cada flecha ← indica copia de archivo real del repo de solo lectura, seguida de la adaptación documentada. Los archivos AGENTS.md y CLAUDE.md se crean en la misma tarea que la carpeta del mapa (arnes.md back §3 / front §2); CLAUDE.md contiene @AGENTS.md. Los tests y la documentación de una regla se commitean con su código.
- **Verificación continua:** al terminar cada tarea, además de la prueba focal, compilar el proyecto afectado y revisar git diff --check. La puerta integral se ejecuta al final, antes de subir HarnessStage.

## Tareas backend

### Tarea 1. Result y errores de dominio

**Archivos:** crear src/ArquitecturaBaseMultitenant.Domain/Results/{Error,ErrorType,Result,ValidationError}.cs ← ../ArquitecturaBase/src/ArquitecturaBase.Domain/Results/{Error,ErrorType,Result,ValidationError}.cs; tests/ArquitecturaBaseMultitenant.Domain.UnitTests/Results/ResultTests.cs ← ../ArquitecturaBase/tests/ArquitecturaBase.Domain.UnitTests/Results/ResultTests.cs y ValidationErrorTests.cs (nuevo); modificar tests/ArquitecturaBaseMultitenant.Domain.UnitTests/ArquitecturaBaseMultitenant.Domain.UnitTests.csproj para quitar --ignore-exit-code 8.
**Respaldo:** plan maestro §Etapa 1, Back 1; backend.md §6 “Tipos” y “Catálogos”; arbol.md back “Domain/Results” y “Domain.UnitTests/Results”; rules/result-y-errores.md “Cómo se hace”.
- [ ] Escribir/ajustar ResultTests y ValidationErrorTests para éxitos, error y errores por campo; ejecutar D con cada clase y comprobar rojo.
- [ ] Copiar/adaptar los cuatro tipos, ejecutar D con ambas clases y comprobar verde.
- [ ] Commit back: feat: establecer Result y errores de dominio

### Tarea 2. Bases de entidad y value object

**Archivos:** crear src/ArquitecturaBaseMultitenant.Domain/Common/{Entity,ValueObject}.cs ← ../ArquitecturaBase/src/ArquitecturaBase.Domain/Common/{Entity,ValueObject}.cs; src/ArquitecturaBaseMultitenant.Domain/Common/{AGENTS,CLAUDE}.md; test nuevo tests/ArquitecturaBaseMultitenant.Domain.UnitTests/Common/EntityAndValueObjectTests.cs.
**Respaldo:** plan maestro §Etapa 1, Back 1; backend.md §4.1; arbol.md back “Domain/Common”; arnes.md back §3.
- [ ] Probar Guid v7, constructor EF protegido e igualdad por valor; ejecutar D con EntityAndValueObjectTests y comprobar rojo.
- [ ] Copiar/adaptar bases y punteros; repetir la prueba hasta verde.
- [ ] Commit back: feat: establecer bases de dominio

### Tarea 3. Dinero, moneda y cultura

**Archivos:** crear src/ArquitecturaBaseMultitenant.Domain/ValueObjects/{Money,CurrencyCode,CultureCode}.cs y ValueObjects/{AGENTS,CLAUDE}.md; tests/ArquitecturaBaseMultitenant.Domain.UnitTests/ValueObjects/{Money,CurrencyCode,CultureCode}Tests.cs (nuevos).
**Respaldo:** plan maestro §Etapa 1, Back 1; backend.md §18 “Contrato en la API”; arbol.md back “Domain/ValueObjects”; rules/numeros-y-moneda.md “Cómo se hace”; arnes.md back §3; decisión 8 de este plan.
- [ ] Escribir tests de suma solo con igual moneda, redondeo AwayFromZero, decimales y culturas admitidas; D por clase debe fallar.
- [ ] Copiar el catálogo de ArquitecturaBase si existe; si no, admitir ARS/USD/EUR/BRL/UYU (2), CLP/PYG (0) y rechazar las demás. D por clase en verde.
- [ ] Commit back: feat: agregar dinero moneda y cultura

### Tarea 4. Normalización y límites de texto

**Archivos:** crear src/ArquitecturaBaseMultitenant.Domain/Common/TextLimits.cs; src/ArquitecturaBaseMultitenant.Application/Common/Text/TextNormalizer.cs; tests/ArquitecturaBaseMultitenant.Application.UnitTests/Common/TextNormalizerTests.cs y tests/ArquitecturaBaseMultitenant.ArchitectureTests/TextLimitsTests.cs (nuevos); modificar tests/ArquitecturaBaseMultitenant.Application.UnitTests/ArquitecturaBaseMultitenant.Application.UnitTests.csproj para quitar --ignore-exit-code 8.
**Respaldo:** plan maestro §Etapa 1, Back 11; backend.md §20; arbol.md back “Piezas de los estándares P4”; rules/textos-libres.md “Cómo se hace” y “Lo verifica”.
- [ ] Probar trim, NFC, invisibles, vacío→null y largos declarados; A/TextNormalizerTests y R/TextLimitsTests en rojo.
- [ ] Implementar limpiador y constantes; repetir ambas pruebas en verde.
- [ ] Commit back: feat: normalizar textos y centralizar límites

### Tarea 5. Contrato compartido de formatos

**Archivos:** crear docs/contracts/format-cases.json y tests/ArquitecturaBaseMultitenant.Application.UnitTests/Common/FormatCasesContractTests.cs (nuevos); el EmailTests de la tarea siguiente leerá los casos de correo de este contrato.
**Respaldo:** plan maestro §Etapa 1, Back 5 y puerta; backend.md §18; arbol.md back “Raíz/docs/contracts”; formatos.md front §§2–3; decisiones 1 y 6 de este plan.
- [ ] Probar `now` fijo, `id/type/culture/timeZone/input/expected` por caso, todos los tipos de formatos.md y ambas culturas; A/FormatCasesContractTests en rojo.
- [ ] Copiar el esquema de ArquitecturaBase si existe; si no, versionar `{ "now": "2026-09-27T15:00:00Z", "cases": [...] }` y casos exactos de las dos culturas; repetir en verde.
- [ ] Commit back: test: fijar casos de formato compartidos

### Tarea 6. Correo y teléfono de dominio

**Archivos:** crear src/ArquitecturaBaseMultitenant.Domain/ValueObjects/Email.cs ← ../ArquitecturaBase/src/ArquitecturaBase.Domain/ValueObjects/Email.cs (adaptar NFC/IDN); PhoneNumber.cs ← archivo homónimo base; src/ArquitecturaBaseMultitenant.Domain/Users/{EmailErrors,PhoneErrors}.cs (nuevos); tests/ArquitecturaBaseMultitenant.Domain.UnitTests/ValueObjects/{EmailTests,PhoneNumberTests}.cs ← homónimos de ../ArquitecturaBase/tests/ArquitecturaBase.Domain.UnitTests/ValueObjects/; tests/ArquitecturaBaseMultitenant.ArchitectureTests/EmailPropertyTests.cs (nuevo).
**Respaldo:** plan maestro §Etapa 1, Back 11; arbol.md back “Domain/ValueObjects”, “Domain/Users” y “Piezas P3”; rules/emails.md, rules/telefonos.md.
- [ ] Tests de correo normalizado/IDN, con casos de correo del format-cases.json, error Users.Email.Invalid, E.164 y propiedades sin correo suelto; D/EmailTests, D/PhoneNumberTests y R/EmailPropertyTests en rojo.
- [ ] Copiar/adaptar tipos y errores; repetir en verde.
- [ ] Commit back: feat: definir correo y teléfono normalizados

### Tarea 7. Culturas, resources y paridad

**Archivos:** crear src/ArquitecturaBaseMultitenant.Application/Common/Formatting/SupportedCultures.cs; Resources/{Errors,Errors.en,Validation,Validation.en}.resx ← archivos homónimos de ../ArquitecturaBase/src/ArquitecturaBase.Application/Resources/; Resources/ErrorTexts.cs ← ErrorMessages.cs y ValidationTexts.cs ← ValidationMessages.cs de esa carpeta; Resources/{AGENTS,CLAUDE}.md; src/ArquitecturaBaseMultitenant.Api/Localization/LocalizationExtensions.cs ← ../ArquitecturaBase/src/ArquitecturaBase.Api/Localization/LocalizationExtensions.cs; tests/ArquitecturaBaseMultitenant.Application.UnitTests/Resources/{ResourceParityTests,ErrorTextsTests}.cs ← ResourceParityTests.cs y ErrorMessagesTests.cs de la base; tests/ArquitecturaBaseMultitenant.ArchitectureTests/ErrorCodeTests.cs ← homónimo base. La prueba HTTP de localización se agrega al conectar Program.
**Respaldo:** plan maestro §Etapa 1, Back 3; backend.md §6 “Catálogos” y §12; arbol.md back “Application/Resources” y tests; rules/textos-y-traducciones.md.
- [ ] Probar paridad de claves/placeholders, códigos reservados y fallback; A/ResourceParityTests, A/ErrorTextsTests y R/ErrorCodeTests en rojo.
- [ ] Copiar/adaptar resources y envoltorios, agregar culturas y localización; repetir en verde.
- [ ] Commit back: feat: localizar errores y validaciones

### Tarea 8. Contratos de paginado y orden estable

**Archivos:** crear src/ArquitecturaBaseMultitenant.Application/Common/Pagination/{PagedRequest,PagedResult,SortDescriptor}.cs ← homónimos de ../ArquitecturaBase/src/ArquitecturaBase.Application/Common/Pagination/ (tamaño 10); CursorRequest.cs y CursorResult.cs (nuevos); src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/Extensions/SortMap.cs (nuevo) y QueryableExtensions.cs ← archivo homónimo de ../ArquitecturaBase/src/ArquitecturaBase.Infrastructure/Persistence/Extensions/ (solo ApplySort en E1; búsqueda y cursor en E2); tests/ArquitecturaBaseMultitenant.Application.UnitTests/Common/PaginationContractsTests.cs y tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Contracts/SortMapTests.cs (nuevos).
**Respaldo:** plan maestro §Etapa 1, Back 6; backend.md §9 “Paginado, orden y búsqueda”; arbol.md back “Application/Common/Pagination” e “Infrastructure/Persistence/Extensions”; rules/paginado-y-busqueda.md.
- [ ] Probar página/tamaño por defecto, cursor sin total, sort permitido y desempate Id; A/PaginationContractsTests e I/SortMapTests en rojo.
- [ ] Crear contratos, SortMap y ApplySort; repetir en verde.
- [ ] Commit back: feat: definir paginado y orden estable

### Tarea 9. Logging de operaciones

**Archivos:** crear src/ArquitecturaBaseMultitenant.Application/Common/Logging/OperationLog.cs; tests/ArquitecturaBaseMultitenant.Application.UnitTests/Common/OperationLogTests.cs (nuevo).
**Respaldo:** plan maestro §Etapa 1, Back 7; backend.md §17; arbol.md back “Application/Common/Logging”; rules/logs.md.
- [ ] Probar inicio/fin/fallo mediante FakeLogger sin datos sensibles; A/OperationLogTests en rojo.
- [ ] Implementar RunAsync con LoggerMessage y TimeProvider; repetir en verde.
- [ ] Commit back: feat: registrar operaciones sin datos sensibles

### Tarea 10. Servicio horario

**Archivos:** crear src/ArquitecturaBaseMultitenant.Application/Interfaces/Integrations/{AGENTS,CLAUDE}.md y Time/ITimeZoneService.cs; Interfaces/Services/{AGENTS,CLAUDE}.md y ITimeZoneCatalogService.cs; Models/{AGENTS,CLAUDE}.md y Time/{TimeZoneResponse,DayRangeUtc}.cs; Services/{AGENTS,CLAUDE}.md y Time/TimeZoneCatalogService.cs; Resources/{TimeZones,TimeZones.en}.resx; src/ArquitecturaBaseMultitenant.Infrastructure/Time/TimeZoneService.cs y DependencyInjection.cs ← ../ArquitecturaBase/src/ArquitecturaBase.Infrastructure/DependencyInjection.cs; src/ArquitecturaBaseMultitenant.Application/DependencyInjection.cs ← ../ArquitecturaBase/src/ArquitecturaBase.Application/DependencyInjection.cs; tests/ArquitecturaBaseMultitenant.Application.UnitTests/Time/TimeZoneCatalogServiceTests.cs y tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Time/TimeZoneServiceTests.cs (nuevos).
**Respaldo:** plan maestro §Etapa 1, Back 4; backend.md §§7, 11; arbol.md back “Interfaces/Integrations/Time”, “Models/Time”, “Services/Time” e “Infrastructure/Time”; rules/fechas-y-zonas.md; arnes.md back §3; decisión 4 de este plan.
- [ ] Probar IANA, límites UTC con DST, reloj falso, ocho zonas fijadas, labels por cultura y offset `GMT−3` con menos tipográfico; A/TimeZoneCatalogServiceTests e I/TimeZoneServiceTests en rojo.
- [ ] Crear puertos, servicio, modelos, resources y registros explícitos; repetir en verde.
- [ ] Commit back: feat: agregar servicio y catálogo de zonas horarias

### Tarea 11. Validación de requests

**Archivos:** crear src/ArquitecturaBaseMultitenant.Application/Common/Validation/{IRequestValidator,RequestValidator,ValidationRules,FieldErrors,PagedRequestValidator,CursorRequestValidator}.cs; copiar/adaptar ServiceRequestValidator.cs, ValidationRules.cs, FieldErrors.cs y PagedRequestValidator.cs desde ../ArquitecturaBase/src/ArquitecturaBase.Application/Common/Validation/; crear src/ArquitecturaBaseMultitenant.Application/Validation/{AGENTS,CLAUDE}.md; tests/ArquitecturaBaseMultitenant.Application.UnitTests/Common/{RequestValidatorTests,PagedRequestValidatorTests,CursorRequestValidatorTests}.cs (PagedRequestValidatorTests ← homónimo base); tests/ArquitecturaBaseMultitenant.Application.UnitTests/TestDoubles/ServiceFixture.cs (nuevo, con FakeTimeProvider, FakeLogger y RequestValidator real).
**Respaldo:** plan maestro §Etapa 1, Back 6; backend.md §6 “Validación”; arbol.md back “Application/Common/Validation”; rules/validacion.md; decisión 9 de este plan. `ValidPermissions` se incorpora en E4 con el catálogo.
- [ ] Probar validador único, FieldErrors camelCase, reglas de textos/preferencias, página y cursor; A por las tres clases en rojo.
- [ ] Adaptar reglas y registro de E1 sin `ValidPermissions`; A por las tres clases en verde.
- [ ] Commit back: feat: centralizar validación de pedidos

### Tarea 12. DisplayFormatter

**Archivos:** crear src/ArquitecturaBaseMultitenant.Application/Common/Formatting/{DisplayFormatter,CultureProfiles}.cs y {AGENTS,CLAUDE}.md; tests/ArquitecturaBaseMultitenant.Application.UnitTests/Common/DisplayFormatterTests.cs (nuevo).
**Respaldo:** plan maestro §Etapa 1, Back 5; backend.md §18 “Formato del lado del backend”; arbol.md back “Application/Common/Formatting”; rules/numeros-y-moneda.md y rules/fechas-y-zonas.md; arnes.md back §3; decisiones 1, 6 y 8 de este plan.
- [ ] Hacer que DisplayFormatterTests recorra **todos** los casos de format-cases.json, incluidos relativa, rangos, cantidad, compacto, tamaño, duración, zona, cultura, enum, booleano y texto fiscal, y falle por texto distinto.
- [ ] Implementar perfiles explícitos y formato con cultura/zona explícitas, sin crear el value object `TaxId` de E6; A/DisplayFormatterTests en verde.
- [ ] Commit back: feat: unificar formato de datos en el backend

### Tarea 13. Mapeo de errores a ProblemDetails

**Archivos:** crear src/ArquitecturaBaseMultitenant.Api/ErrorHandling/{ApiErrorCodes,ProblemDetailsMapper,ControllerResultExtensions}.cs ← homónimos de ../ArquitecturaBase/src/ArquitecturaBase.Api/ErrorHandling/; crear src/ArquitecturaBaseMultitenant.Api/DependencyInjection.cs ← ../ArquitecturaBase/src/ArquitecturaBase.Api/DependencyInjection.cs; actualizar src/ArquitecturaBaseMultitenant.Api/Program.cs ← ../ArquitecturaBase/src/ArquitecturaBase.Api/Program.cs (solo piezas E1); tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/TestFeatures/TestController.cs ← ../ArquitecturaBase/tests/ArquitecturaBase.Api.IntegrationTests/TestFeatures/TestController.cs (rehacer sin BD), TestControllerApplicationPart.cs ← ../ArquitecturaBase/tests/ArquitecturaBase.Api.IntegrationTests/Support/TestControllerApplicationPart.cs; tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/ErrorHandling/{ErrorHandlingTests,ValidationProblemTests}.cs (ErrorHandlingTests ← ../ArquitecturaBase/tests/ArquitecturaBase.Api.IntegrationTests/ErrorHandlingTests.cs; ValidationProblemTests nuevo, con referencia a ProblemDetailsMapperTests.cs de la base).
**Respaldo:** plan maestro §Etapa 1, Back 2 y puerta; backend.md §6 “Mapeo HTTP”; arbol.md back “Api/ErrorHandling”; rules/result-y-errores.md.
- [ ] Probar los siete ErrorType, status, detail es-AR/en-US, code, traceId, errors y retryAfter; I/ErrorHandlingTests e I/ValidationProblemTests en rojo.
- [ ] Copiar/adaptar mapper y extensiones; repetir en verde.
- [ ] Commit back: feat: mapear resultados a ProblemDetails

### Tarea 14. Errores del framework y localización HTTP

**Archivos:** crear src/ArquitecturaBaseMultitenant.Api/ErrorHandling/{GlobalExceptionHandler,MvcInvalidModelStateResponseFactory,EmptyJsonBodyContentTypeFilter}.cs ← homónimos de ../ArquitecturaBase/src/ArquitecturaBase.Api/ErrorHandling/; actualizar src/ArquitecturaBaseMultitenant.Api/{DependencyInjection,Program}.cs; crear tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/ErrorHandling/FrameworkErrorsTests.cs ← ../ArquitecturaBase/tests/ArquitecturaBase.Api.IntegrationTests/FrameworkErrorsTests.cs y Localization/LocalizationTests.cs ← ../ArquitecturaBase/tests/ArquitecturaBase.Api.IntegrationTests/LocalizationTests.cs.
**Respaldo:** plan maestro §Etapa 1, Back 2 y 10; backend.md §§6, 7, 16; arbol.md back “Api/ErrorHandling” y “TestFeatures”; rules/api-http.md y rules/result-y-errores.md; decisión 17 de este plan.
- [ ] Probar cuerpo ilegible, tipo incorrecto, 401/403/404/405/429, excepción 500 sin mensaje interno y Accept-Language; I/FrameworkErrorsTests e I/LocalizationTests en rojo.
- [ ] Conectar errores MVC y localización en Program; repetir ambas clases en verde.
- [ ] Commit back: feat: unificar errores HTTP del framework

### Tarea 15. JSON de fechas civiles e instantes

**Archivos:** crear src/ArquitecturaBaseMultitenant.Api/Json/{JsonConfiguration,DateOnlyConverter,TimeOnlyConverter}.cs y {AGENTS,CLAUDE}.md; UtcDateTimeConverter.cs ← ../ArquitecturaBase/src/ArquitecturaBase.Api/Json/UtcDateTimeConverter.cs; ampliar tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/TestFeatures/TestController.cs con rutas de prueba de fecha/hora; tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Json/UtcDateTimeTests.cs ← ../ArquitecturaBase/tests/ArquitecturaBase.Api.IntegrationTests/Json/UtcDateTimeConverterTests.cs, y DateOnlyTimeOnlyTests.cs (nuevo).
**Respaldo:** plan maestro §Etapa 1, Back 4 y 10; backend.md §§11, 18; arbol.md back “Api/Json” y tests; rules/fechas-y-zonas.md; arnes.md back §3.
- [ ] Probar ISO Z, rechazo sin offset, yyyy-MM-dd y HH:mm:ss; I/UtcDateTimeTests e I/DateOnlyTimeOnlyTests en rojo.
- [ ] Registrar los conversores en un único ConfigureJson; repetir en verde.
- [ ] Commit back: feat: serializar tiempo con contratos UTC

### Tarea 16. JSON de dinero y texto de entrada

**Archivos:** crear src/ArquitecturaBaseMultitenant.Api/Json/{MoneyJsonConverter,NormalizedStringJsonConverter,RawTextAttribute}.cs; actualizar src/ArquitecturaBaseMultitenant.Api/Json/JsonConfiguration.cs; crear src/ArquitecturaBaseMultitenant.Api/Contracts/{AGENTS,CLAUDE}.md y Common/PhoneInputHttpRequest.cs ← ../ArquitecturaBase/src/ArquitecturaBase.Api/Contracts/Users/PhoneNumberHttpRequest.cs (adaptado); ampliar tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/TestFeatures/TestController.cs con rutas de prueba de Money y un POST de texto; tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Json/MoneyJsonTests.cs y Api/NormalizedInputTests.cs (nuevos).
**Respaldo:** plan maestro §Etapa 1, Back 5, 10–11; backend.md §§18, 20; arbol.md back “Api/Json” y “Piezas P4”; rules/textos-libres.md y rules/numeros-y-moneda.md; arnes.md back §3; decisión 11 de este plan.
- [ ] Probar objeto {amount,currency}, moneda inválida→400, limpieza global y excepción RawText; el POST de TestController devuelve el cuerpo normalizado sin persistir. I/MoneyJsonTests e I/NormalizedInputTests en rojo.
- [ ] Implementar conversores y contrato telefónico según alcance resuelto; repetir en verde.
- [ ] Commit back: feat: serializar dinero y limpiar entradas

### Tarea 17. Ruta de zonas horarias e inventario

**Archivos:** crear src/ArquitecturaBaseMultitenant.Api/Controllers/{AGENTS,CLAUDE}.md y Account/TimeZonesController.cs; tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Contracts/ExplicitRouteInventoryTests.cs y Time/TimeZonesEndpointTests.cs (nuevos).
**Respaldo:** plan maestro §Etapa 1, Back 4 y puerta general 3; backend.md §§5, 11; arbol.md back “Api/Controllers/Account” y “Api.IntegrationTests/Contracts”; rules/api-http.md; arnes.md back §3; decisiones 3 y 4 de este plan.
- [ ] Probar verbo/ruta explícitos, `[AllowAnonymous]`, ocho zonas, label traducido y offset del reloj inyectado; I/ExplicitRouteInventoryTests e I/TimeZonesEndpointTests en rojo. Anotar el controller en la lista explícita de `AccessDeclarationTests` cuando esa guarda nazca en E3.
- [ ] Crear controller fino y actualizar inventario de todas las rutas existentes; repetir en verde.
- [ ] Commit back: feat: exponer catálogo de zonas horarias

### Tarea 18. OpenAPI versionado

**Archivos:** crear src/ArquitecturaBaseMultitenant.Api/OpenApi/{OpenApiExtensions,ProblemResponsesConvention,ProducesProblemAttribute}.cs ← homónimos de ../ArquitecturaBase/src/ArquitecturaBase.Api/OpenApi/; actualizar src/ArquitecturaBaseMultitenant.Api/ArquitecturaBaseMultitenant.Api.csproj, Directory.Packages.props, Program.cs y .github/workflows/ci.yml; generar docs/contracts/openapi.json; crear tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Contracts/{OpenApiTests,OpenApiContractTests}.cs (OpenApiTests ← ../ArquitecturaBase/tests/ArquitecturaBase.Api.IntegrationTests/OpenApiTests.cs).
**Respaldo:** plan maestro §Etapa 1, Back 8 y puerta general 6; backend.md §17; arbol.md back “Api/OpenApi”, “Raíz/docs/contracts” y “Api.IntegrationTests/Contracts”; rules/api-http.md; decisión 7 de este plan.
- [ ] Probar esquema 2xx, ProblemDetails de errores, Swagger solo Development y artefacto al día; I/OpenApiTests e I/OpenApiContractTests en rojo.
- [ ] Integrar `Microsoft.Extensions.ApiDescription.Server`, fijar su versión exacta en `Directory.Packages.props`, exportar durante el build a `docs/contracts/openapi.json` y comprobarlo en CI; repetir en verde.
- [ ] Commit back: feat: versionar contrato OpenAPI

### Tarea 19. Hosting y guía de prefijos

**Archivos:** crear src/ArquitecturaBaseMultitenant.Api/Hosting/{ForwardedHeadersExtensions,SecurityHeadersExtensions,SpaExtensions}.cs ← homónimos de ../ArquitecturaBase/src/ArquitecturaBase.Api/Hosting/; actualizar src/ArquitecturaBaseMultitenant.Api/Program.cs; crear docs/guides/prefijo-de-backend.md; tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Hosting/SpaHostingTests.cs ← ../ArquitecturaBase/tests/ArquitecturaBase.Api.IntegrationTests/Hosting/SpaHostingTests.cs y SecurityHeadersTests.cs (nuevo, tomando ForwardedHeadersTests.cs de base como referencia).
**Respaldo:** plan maestro §Etapa 1, Back 9; backend.md §§16, 19; arbol.md back “Api/Hosting” y “Documentación en capas”; rules/api-http.md; decisiones 15 y 17 de este plan. El proxy del front se actualiza en la tarea 24.
- [ ] Probar prefijos backend, `/swagger` y `/openapi` solo en Development, fallback SPA en host principal/subdominio y headers; I/SpaHostingTests e I/SecurityHeadersTests en rojo.
- [ ] Copiar/adaptar hosting y guía; conectar las piezas operativas de §16 en orden y dejar comentados los lugares de autenticación, resolución tenant, autorización, bootstrap y middleware de etapas posteriores; repetir en verde.
- [ ] Commit back: feat: completar hosting y documentar prefijos

### Tarea 20. Marca de idempotencia

**Archivos:** crear src/ArquitecturaBaseMultitenant.Api/Idempotency/{IdempotentAttribute.cs,AGENTS.md,CLAUDE.md}; tests/ArquitecturaBaseMultitenant.ArchitectureTests/IdempotentActionsTests.cs (nuevo).
**Respaldo:** plan maestro §Etapa 1, Back 12; arbol.md back “Piezas P6”; rules/idempotencia.md; arnes.md back §3.
- [ ] Probar que todo POST con respuesta 201/202 declara la marca; R/IdempotentActionsTests en rojo con caso de control.
- [ ] Crear solo el atributo, sin filtro/tabla/worker E2; repetir en verde.
- [ ] Commit back: feat: marcar acciones idempotentes

### Tarea 21. Guardas de arquitectura del núcleo

**Archivos:** crear tests/ArquitecturaBaseMultitenant.ArchitectureTests/{ApplicationPublicApiTests,ControllerInputContractTests,ControllerServiceRepositoryTests,ApplicationServicesTests}.cs ← homónimos de ../ArquitecturaBase/tests/ArquitecturaBase.ArchitectureTests/; ServiceDependencyCountTests.cs, DecimalPrecisionTests.cs y NoManualFormattingTests.cs (nuevos).
**Respaldo:** plan maestro §Etapa 1, Back 10 y “Reglas para todas las etapas”; backend.md §§3, 5, 18; arbol.md back “ArchitectureTests”; rules/capas-y-flujo.md, rules/numeros-y-moneda.md y rules/tests.md.
- [ ] Escribir/adaptar cada guarda y demostrar rojo con una violación de prueba local, retirada antes del commit; R con cada una de las siete clases.
- [ ] Ajustar únicamente código E1 que incumpla la guarda y repetir R completo en verde.
- [ ] Commit back: test: proteger arquitectura del núcleo transversal

## Tareas frontend

### Tarea 22. i18n, namespaces y paridad

**Archivos:** crear ../ArquitecturaBaseMutitenantFront/src/shared/i18n/{index.ts,i18n.test.tsx} ← homónimos de ../ArquitecturaBaseFront/src/shared/i18n/ (adaptar es-AR/en-US y clave local); ../ArquitecturaBaseMutitenantFront/src/locales/es/{common,errors,enums}.json y en/{common,errors,enums}.json (common ← homónimo de ../ArquitecturaBaseFront/src/locales/es/ y en/; errors/enums nuevos); ../ArquitecturaBaseMutitenantFront/src/locales/parity.test.ts ← homónimo base; ../ArquitecturaBaseMutitenantFront/src/locales/{AGENTS,CLAUDE}.md; modificar ../ArquitecturaBaseMutitenantFront/src/app/providers.tsx.
**Respaldo:** plan maestro §Etapa 1, Front 2; frontend.md §4 “Idioma y cultura”; arbol.md front “src/locales” y “src/shared/i18n”; rules/textos-y-traducciones.md; arnes.md front §2.
- [ ] Probar idioma efectivo, fallback y mismas claves/placeholders; npm test -- src/shared/i18n/i18n.test.tsx src/locales/parity.test.ts en rojo.
- [ ] Copiar/adaptar inicialización y textos del tablero aprobados; repetir en verde.
- [ ] Commit front: feat: incorporar traducciones y paridad

### Tarea 23. Soporte de tests HTTP y componentes

**Archivos:** crear ../ArquitecturaBaseMutitenantFront/src/test/mocks/{server,handlers}.ts ← homónimos de ../ArquitecturaBaseFront/src/test/mocks/ (sin /api/me ni auth E3); ../ArquitecturaBaseMutitenantFront/src/test/utils/renderWithProviders.tsx ← homónimo base (sin access ni host); modificar ../ArquitecturaBaseMutitenantFront/src/test/setup.ts; crear ../ArquitecturaBaseMutitenantFront/src/test/mocks/mocks.test.ts (nuevo).
**Respaldo:** plan maestro §Etapa 1, Front 1–2; arbol.md front “src/test”; rules/tests.md “Cómo se hace”.
- [ ] Probar MSW con onUnhandledRequest:error y render con i18n/Query, sin proveedor Auth; npm test -- src/test/mocks/mocks.test.ts en rojo.
- [ ] Copiar/adaptar soporte y setup; repetir en verde.
- [ ] Commit front: test: preparar MSW y render con proveedores

### Tarea 24. Contratos generados del backend

**Archivos:** crear ../ArquitecturaBaseMutitenantFront/scripts/{generate-contracts,check-contracts}.mjs y ../ArquitecturaBaseMutitenantFront/src/shared/api/generated/schema.d.ts (solo salida generada); crear ../ArquitecturaBaseMutitenantFront/src/shared/api/types.ts; modificar ../ArquitecturaBaseMutitenantFront/{package.json,package-lock.json,.github/workflows/ci.yml,vite.config.ts}; crear ../ArquitecturaBaseMutitenantFront/src/shared/api/{AGENTS,CLAUDE}.md y ../ArquitecturaBaseMutitenantFront/src/shared/api/contracts.test.ts, ../ArquitecturaBaseMutitenantFront/src/test/proxy-prefixes.test.ts (nuevos).
**Respaldo:** plan maestro §Etapa 1, Front 6 y puerta general 6; frontend.md §4 “Datos y API”; backend.md §19; arbol.md front “Raíz/scripts” y “src/shared/api”; rules/datos-y-api.md; arnes.md front §2. Depende de Q2, Q15 y de la tarea OpenAPI backend.
- [ ] Probar que contracts:check detecta un schema desactualizado, generated contiene solo schema.d.ts y el proxy coincide con BackendPrefixes acordados; npm test -- src/shared/api/contracts.test.ts src/test/proxy-prefixes.test.ts en rojo.
- [ ] Crear scripts, alias tipados, scripts npm, CI y ajustar el proxy solo según Q15; ejecutar npm run contracts && npm run contracts:check y pruebas focales hasta verde.
- [ ] Commit front: feat: generar tipos desde OpenAPI

### Tarea 25. Cliente HTTP y contratos de error

**Archivos:** crear ../ArquitecturaBaseMutitenantFront/src/shared/api/{httpClient.ts,httpClient.test.ts,ApiError.ts,problemDetails.ts,pagedResult.ts} ← homónimos de ../ArquitecturaBaseFront/src/shared/api/ (adaptar cultura, 401 y ProblemDetails).
**Respaldo:** plan maestro §Etapa 1, Front 1; frontend.md §4 “Datos y API” y “Errores”; arbol.md front “src/shared/api”; rules/datos-y-api.md y rules/errores.md. La conexión de Auth depende de Q13.
- [ ] Probar Bearer, Accept-Language efectivo, un solo intento de renovación, error tipado y PagedResult; npm test -- src/shared/api/httpClient.test.ts en rojo.
- [ ] Copiar/adaptar cliente y tipos sin incorporar Auth E3 antes de Q13; repetir en verde.
- [ ] Commit front: feat: centralizar cliente HTTP y errores

### Tarea 26. Errores globales y formularios

**Archivos:** crear ../ArquitecturaBaseMutitenantFront/src/shared/api/{queryClient.ts,formErrors.ts} ← homónimos de ../ArquitecturaBaseFront/src/shared/api/; queryClient.test.ts ← ../ArquitecturaBaseFront/src/shared/api/queryClient.test.tsx (adaptar la prueba sin JSX al nombre del árbol), formErrors.test.ts ← homónimo base; modificar ../ArquitecturaBaseMutitenantFront/src/app/providers.tsx.
**Respaldo:** plan maestro §Etapa 1, Front 1 y 9; frontend.md §4 “Errores”; arbol.md front “src/shared/api”; rules/errores.md y rules/formularios.md. Depende de Q13.
- [ ] Probar silencio de 401/403/meta.silent, toast de red/5xx con traceId, 429 sin reintento y campos por code; npm test -- src/shared/api/queryClient.test.ts src/shared/api/formErrors.test.ts en rojo.
- [ ] Adaptar cliente Query y mapeo de campos; repetir en verde.
- [ ] Commit front: feat: resolver errores globales y de formulario

### Tarea 27. Mutación idempotente

**Archivos:** crear ../ArquitecturaBaseMutitenantFront/src/shared/api/{useIdempotentMutation.ts,useIdempotentMutation.test.ts} (nuevos).
**Respaldo:** plan maestro §Etapa 1, Front 7; arbol.md front “src/shared/api”; rules/datos-y-api.md y back rules/idempotencia.md.
- [ ] Probar clave estable ante reintento, clave nueva tras éxito y espera de Request.InProgress; npm test -- src/shared/api/useIdempotentMutation.test.ts en rojo.
- [ ] Implementar hook sobre TanStack Query y httpClient; repetir en verde.
- [ ] Commit front: feat: agregar mutación idempotente

### Tarea 28. Perfiles y formateadores del front

**Archivos:** crear ../ArquitecturaBaseMutitenantFront/src/shared/format/{cultureProfiles,formatters,statusTones}.ts y {formatters.test,format-usage.test,statusTones.test}.ts; ../ArquitecturaBaseMutitenantFront/src/shared/format/{AGENTS,CLAUDE}.md.
**Respaldo:** plan maestro §Etapa 1, Front 3; formatos.md §§2–5; arbol.md front “src/shared/format”; rules/formatos.md; tema.md §Colores; arnes.md front §2. Depende de Q1, Q5, Q6 y Q16.
- [ ] Probar cada caso compartido, estado→tono y prohibiciones de formato fuera de shared/format; npm test -- src/shared/format/formatters.test.ts src/shared/format/statusTones.test.ts src/shared/format/format-usage.test.ts en rojo.
- [ ] Implementar solo los tipos y tonos respaldados por las respuestas, sin usar defaults del navegador; repetir en verde.
- [ ] Commit front: feat: unificar formatos y tonos

### Tarea 29. Parsers y contexto de formato

**Archivos:** crear ../ArquitecturaBaseMutitenantFront/src/shared/format/{parsers.ts,parsers.test.ts,useFormat.ts,useFormat.test.tsx} (nuevos).
**Respaldo:** plan maestro §Etapa 1, Front 3; formatos.md §§1, 4–5; frontend.md §4 “Presentación de datos”; rules/formatos.md. Depende de Q5.
- [ ] Probar parseDecimal, parseMoney, parsePercent, parseDate y cultura/zona/moneda ya resueltas; npm test -- src/shared/format/parsers.test.ts src/shared/format/useFormat.test.tsx en rojo.
- [ ] Implementar parsers y hook según fuente aprobada de preferencias; repetir en verde.
- [ ] Commit front: feat: interpretar entradas por cultura

### Tarea 30. Países, banderas y selector telefónico

**Archivos:** crear ../ArquitecturaBaseMutitenantFront/src/shared/phone/{countries.ts,priorityCountries.ts,CountryFlag.tsx,CountrySelect.tsx,countries.test.ts,CountrySelect.test.tsx,AGENTS.md,CLAUDE.md}; modificar ../ArquitecturaBaseMutitenantFront/{package.json,package-lock.json}.
**Respaldo:** plan maestro §Etapa 1, Front 7; formatos.md §3; arbol.md front “src/shared/phone”; rules/telefonos.md “PhoneField”; arnes.md front §2. Depende de Q7.
- [ ] Probar lista completa de libphonenumber-js, prioridades, búsqueda nombre/ISO/prefijo y SVG diferido; npm test -- src/shared/phone/countries.test.ts src/shared/phone/CountrySelect.test.tsx en rojo.
- [ ] Crear piezas con versión aprobada de country-flag-icons; repetir en verde.
- [ ] Commit front: feat: ofrecer países y banderas para teléfonos

### Tarea 31. Catálogo horario del front

**Archivos:** crear ../ArquitecturaBaseMutitenantFront/src/shared/time/{timeZones.ts,TimeZoneSelect.tsx,timeZones.test.ts,TimeZoneSelect.test.tsx}.
**Respaldo:** plan maestro §Etapa 1, Front 5; formatos.md §§3, 5; arbol.md front “src/shared/time”; rules/formatos.md y rules/datos-y-api.md. Depende de Q3–Q4 y de la tarea de la ruta horaria backend.
- [ ] Probar consumo de GET /api/time-zones, ciudad traducida y grupos por país; npm test -- src/shared/time/timeZones.test.ts src/shared/time/TimeZoneSelect.test.tsx en rojo.
- [ ] Implementar contra schema generado, sin lista IANA escrita a mano; repetir en verde.
- [ ] Commit front: feat: mostrar catálogo de zonas horarias

### Tarea 32. Componentes de fecha y números

**Archivos:** crear ../ArquitecturaBaseMutitenantFront/src/shared/ui/format/{DateText,DateRangeText,NumberText,MoneyText,PercentText}.tsx y {DateText,NumberText,MoneyText,PercentText}.test.tsx; {AGENTS,CLAUDE}.md.
**Respaldo:** plan maestro §Etapa 1, Front 5; formatos.md §§3–5; arbol.md front “src/shared/ui/format”; rules/formatos.md; arnes.md front §2. Depende de perfiles, formateadores, parsers y useFormat.
- [ ] Probar time dateTime ISO, vacío, tooltips, alineación numérica y aria-label de moneda; npm test -- src/shared/ui/format/DateText.test.tsx src/shared/ui/format/NumberText.test.tsx src/shared/ui/format/MoneyText.test.tsx src/shared/ui/format/PercentText.test.tsx en rojo.
- [ ] Crear componentes que solo llaman shared/format; repetir en verde.
- [ ] Commit front: feat: mostrar fechas números y dinero

### Tarea 33. Demás componentes de presentación

**Archivos:** crear ../ArquitecturaBaseMutitenantFront/src/shared/ui/format/{FileSizeText,DurationText,PhoneText,TaxIdText,TimeZoneText,CultureText,EnumText,StatusBadge,BooleanText,EmptyValue}.tsx y {PhoneText,TaxIdText,TimeZoneText,EnumText,StatusBadge,EmptyValue}.test.tsx.
**Respaldo:** plan maestro §Etapa 1, Front 5; formatos.md §§3–5; arbol.md front “src/shared/ui/format”; rules/formatos.md y rules/telefonos.md; tema.md §Colores. Depende de Q1, Q6, Q12 y Q16.
- [ ] Probar E.164 oculto, sin ID IANA crudo, enum y badge traducidos con tono correcto, booleano, vacío accesible y formato fiscal acordado; npm test -- src/shared/ui/format/PhoneText.test.tsx src/shared/ui/format/TaxIdText.test.tsx src/shared/ui/format/TimeZoneText.test.tsx src/shared/ui/format/EnumText.test.tsx src/shared/ui/format/StatusBadge.test.tsx src/shared/ui/format/EmptyValue.test.tsx en rojo.
- [ ] Implementar componentes por delegación a shared/format y traducciones; repetir en verde.
- [ ] Commit front: feat: completar componentes de presentación

### Tarea 34. Campos de fecha y números

**Archivos:** crear ../ArquitecturaBaseMutitenantFront/src/shared/ui/fields/{DateField,DateTimeField,TimeField,NumberField,MoneyField,PercentField}.tsx y {DateField,DateTimeField,MoneyField,PercentField}.test.tsx; {AGENTS,CLAUDE}.md.
**Respaldo:** plan maestro §Etapa 1, Front 5; formatos.md §§4–5; arbol.md front “src/shared/ui/fields”; rules/formularios.md y rules/formatos.md; arnes.md front §2.
- [ ] Probar DateOnly sin zona, instante convertido desde zona efectiva, decimal cultural, Money con moneda y porcentaje fraccional; npm test -- src/shared/ui/fields/DateField.test.tsx src/shared/ui/fields/DateTimeField.test.tsx src/shared/ui/fields/MoneyField.test.tsx src/shared/ui/fields/PercentField.test.tsx en rojo.
- [ ] Crear campos que usan parsers y emiten contratos HTTP; repetir en verde.
- [ ] Commit front: feat: cargar fechas números y montos

### Tarea 35. Campos de correo, teléfono e identificación

**Archivos:** crear ../ArquitecturaBaseMutitenantFront/src/shared/ui/fields/{EmailField,PhoneField,TaxIdField}.tsx y {EmailField,PhoneField,TaxIdField}.test.tsx; PhoneField toma como referencia, sin copiar lista fija, ../ArquitecturaBaseFront/src/shared/ui/PhoneField.tsx y su test homónimo; modificar ../ArquitecturaBaseMutitenantFront/{package.json,package-lock.json} por stdnum según Q7.
**Respaldo:** plan maestro §Etapa 1, Front 7; formatos.md §§3–5; arbol.md front “src/shared/ui/fields”; rules/telefonos.md, rules/formularios.md. Depende de Q7 y Q12.
- [ ] Probar correo normalizado, país al pegar número, AsYouType, contrato {country,number} y TaxId acordado; npm test -- src/shared/ui/fields/EmailField.test.tsx src/shared/ui/fields/PhoneField.test.tsx src/shared/ui/fields/TaxIdField.test.tsx en rojo.
- [ ] Crear campos manteniendo accesibilidad del PhoneField base; repetir en verde.
- [ ] Commit front: feat: cargar correo teléfono e identificación

### Tarea 36. Hooks de paginación, cursor y búsqueda

**Archivos:** modificar ../ArquitecturaBaseMutitenantFront/src/shared/hooks/{usePagination.ts,usePagination.test.tsx}; crear {useCursorList.ts,useCursorList.test.tsx,useDebouncedValue.ts,useDebouncedValue.test.tsx}.
**Respaldo:** plan maestro §Etapa 1, Front 4 y 9; frontend.md §4 “Paginado” y “Errores”; arbol.md front “src/shared/hooks”; rules/paginado-y-listados.md y rules/formularios.md. Corrección interna depende de Q14.
- [ ] Probar URL y reset, página fuera de rango sin correctPage público, cursor sin total y debounce 300 ms; npm test -- src/shared/hooks/usePagination.test.tsx src/shared/hooks/useCursorList.test.tsx src/shared/hooks/useDebouncedValue.test.tsx en rojo.
- [ ] Adaptar hook existente y agregar los otros; repetir en verde.
- [ ] Commit front: feat: completar hooks de listados y cambios pendientes

### Tarea 37. Controles de paginación

**Archivos:** crear ../ArquitecturaBaseMutitenantFront/src/shared/ui/{Pagination.tsx,Pagination.test.tsx} ← homónimos de ../ArquitecturaBaseFront/src/shared/ui/; crear LoadMore.tsx y LoadMore.test.tsx (nuevos).
**Respaldo:** plan maestro §Etapa 1, Front 4; frontend.md §4 “Paginado”; arbol.md front “src/shared/ui”; rules/paginado-y-listados.md y rules/pantallas-y-ui.md.
- [ ] Probar 10/20/50/100, “1–10 de 1.234”, página y Cargar más sin total; npm test -- src/shared/ui/Pagination.test.tsx src/shared/ui/LoadMore.test.tsx en rojo.
- [ ] Adaptar Pagination al formato y tokens; crear LoadMore; repetir en verde.
- [ ] Commit front: feat: mostrar paginado y carga por cursor

### Tarea 38. Piezas genéricas de formulario y aviso

**Archivos:** crear ../ArquitecturaBaseMutitenantFront/src/shared/ui/{Banner,CheckboxField,ConfirmDialog,EmptyState,FormField,IconButton,MultiSelect,RadioGroupField}.tsx ← homónimos de ../ArquitecturaBaseFront/src/shared/ui/; crear/adaptar tests homónimos .test.tsx desde la misma carpeta base; crear ../ArquitecturaBaseMutitenantFront/src/shared/hooks/{useUnsavedChangesGuard.ts,useUnsavedChangesGuard.test.tsx} ← homónimos de ../ArquitecturaBaseFront/src/shared/hooks/.
**Respaldo:** plan maestro §Etapa 1, Front 8–9; frontend.md §4 “UI y pantallas”; arbol.md front “src/shared/ui”; rules/formularios.md, rules/pantallas-y-ui.md, rules/accesibilidad.md; tema.md §§Colores, Forma.
- [ ] Adaptar tests de textos traducidos, foco, labels, tokens y bloqueo de salida; npm test -- src/shared/ui/Banner.test.tsx src/shared/ui/ConfirmDialog.test.tsx src/shared/ui/FormField.test.tsx src/shared/hooks/useUnsavedChangesGuard.test.tsx en rojo.
- [ ] Copiar/adaptar ocho componentes y el guard con ConfirmDialog, con todos los --color-* sustituidos; repetir las pruebas en verde.
- [ ] Commit front: feat: agregar controles genéricos traducidos

### Tarea 39. Página, acciones e iconos

**Archivos:** crear ../ArquitecturaBaseMutitenantFront/src/shared/ui/{Page,RowActions,SearchInput,SegmentedControl,Spinner,VerificationBadge,icons}.tsx ← homónimos de ../ArquitecturaBaseFront/src/shared/ui/; crear/adaptar {Page,RowActions,SegmentedControl,VerificationBadge,icons}.test.tsx desde sus homónimos base; crear SearchInput.test.tsx y Spinner.test.tsx (nuevos).
**Respaldo:** plan maestro §Etapa 1, Front 8; frontend.md §4 “UI y pantallas”; arbol.md front “src/shared/ui”; rules/pantallas-y-ui.md y rules/accesibilidad.md; tema.md §§Forma, Colores.
- [ ] Probar banda Page con resumen/acciones, menú ⋮ con destructivas al final, búsqueda accesible y trazo SVG; npm test -- src/shared/ui/Page.test.tsx src/shared/ui/RowActions.test.tsx src/shared/ui/SearchInput.test.tsx src/shared/ui/icons.test.tsx en rojo.
- [ ] Copiar/adaptar las siete piezas y tokens; repetir pruebas de la carpeta en verde.
- [ ] Commit front: feat: agregar página acciones e iconos

### Tarea 40. Tabla tipada y adaptable

**Archivos:** crear ../ArquitecturaBaseMutitenantFront/src/shared/ui/{DataTable.tsx,DataTable.test.tsx} ← homónimos de ../ArquitecturaBaseFront/src/shared/ui/ (reescribir columnas type/mobile); crear ../ArquitecturaBaseMutitenantFront/src/shared/ui/columns-mobile.test.ts.
**Respaldo:** plan maestro §Etapa 1, Front 5 y 8; formatos.md §4; frontend.md §4 “Paginado” y “UI y pantallas”; arbol.md front “src/shared/ui”; rules/responsive.md y rules/paginado-y-listados.md.
- [ ] Probar type→ui/format, carga/vacío/error, aria-sort, 390 px solo primary/status/⋮, 768 sin low y exactamente una primary; npm test -- src/shared/ui/DataTable.test.tsx src/shared/ui/columns-mobile.test.ts en rojo.
- [ ] Adaptar DataTable sin formateo manual ni dos datos en una celda; repetir en verde.
- [ ] Commit front: feat: agregar tabla tipada y adaptable

### Tarea 41. Filtros, diálogo y hoja móvil

**Archivos:** modificar ../ArquitecturaBaseMutitenantFront/src/shared/ui/{FilterBar.tsx,dialog.tsx}; crear ../ArquitecturaBaseMutitenantFront/src/shared/ui/{Sheet.tsx,Sheet.test.tsx,FilterBar.test.tsx,dialog.mobile.test.tsx}.
**Respaldo:** plan maestro §Etapa 1, Front 8; frontend.md §4 “UI y pantallas”; arbol.md front “src/shared/ui”; rules/responsive.md; tema.md §Forma y tableros aprobados “Botones”/“Avisos”.
- [ ] Probar filtros bajo buscador a 390, diálogo como hoja desde abajo y 44×44; npm test -- src/shared/ui/FilterBar.test.tsx src/shared/ui/dialog.mobile.test.tsx src/shared/ui/Sheet.test.tsx en rojo.
- [ ] Adaptar primitivas E0 y crear Sheet; repetir en verde.
- [ ] Commit front: feat: adaptar filtros y diálogos al teléfono

### Tarea 42. Avisos transversales y AppShell

**Archivos:** crear ../ArquitecturaBaseMutitenantFront/src/layouts/{AppShell.tsx,AppShell.test.tsx,AGENTS.md,CLAUDE.md}; ../ArquitecturaBaseMutitenantFront/src/layouts/components/{OfflineBanner,NewVersionBanner}.tsx; ../ArquitecturaBaseMutitenantFront/src/shared/ui/{ConcurrencyBanner.tsx,ConcurrencyBanner.test.tsx}; modificar ../ArquitecturaBaseMutitenantFront/src/app/{App.tsx,providers.tsx}; crear ../ArquitecturaBaseMutitenantFront/src/layouts/components/{OfflineBanner,NewVersionBanner}.test.tsx.
**Respaldo:** plan maestro §Etapa 1, Front 9; frontend.md §4 “Errores”; arbol.md front “src/layouts” y “src/shared/ui”; rules/errores.md, rules/formularios.md; arnes.md front §2; tablero aprobado “Avisos”. Depende de Q13.
- [ ] Probar online/offline, fallo de chunk→franja sin recarga sola, 409 con dos acciones sin perder formulario, 429 y sesión caducada al alcance E1 aprobado; npm test -- src/layouts/AppShell.test.tsx src/layouts/components/OfflineBanner.test.tsx src/layouts/components/NewVersionBanner.test.tsx src/shared/ui/ConcurrencyBanner.test.tsx en rojo.
- [ ] Crear avisos y composición, conectar solo las partes E1 acordadas; repetir en verde.
- [ ] Commit front: feat: mostrar avisos transversales

### Tarea 43. Cierre del arnés backend

**Archivos:** modificar tests/ArquitecturaBaseMultitenant.ArchitectureTests/HarnessStage.cs; ajustar solo punteros/fichas back de carpetas E1 creadas en las tareas anteriores si un nombre definitivo cambió. No crear funcionalidad nueva.
**Respaldo:** plan maestro “Reglas para todas las etapas” 1–8; arnes.md back §5–6; arbol.md back, entradas [E1].
- [ ] Cambiar HarnessStage.Closed a 1 y ejecutar R/HarnessTests: rojo si queda una referencia E1 incumplida.
- [ ] Completar enlaces y tests faltantes; ejecutar R/HarnessTests en verde y puerta back.
- [ ] Commit back: chore: cerrar arnés de Etapa 1

### Tarea 44. Cierre del arnés frontend

**Archivos:** modificar ../ArquitecturaBaseMutitenantFront/src/test/HarnessStage.ts; ajustar solo punteros/fichas front de carpetas E1 creadas en tareas anteriores si cambió un nombre definitivo. No crear funcionalidad nueva.
**Respaldo:** plan maestro “Reglas para todas las etapas” 1–8 y “Etapa 1, Puerta”; arnes.md front §4; arbol.md front, entradas [E1].
- [ ] Cambiar HarnessStage a 1 y ejecutar npm test -- src/test/harness.test.ts: rojo si queda una referencia E1 incumplida.
- [ ] Completar enlaces y tests faltantes; ejecutar npm test -- src/test/harness.test.ts en verde y la puerta completa de abajo.
- [ ] Commit front: chore: cerrar arnés de Etapa 1

## Puerta completa de Etapa 1

1. Back: dotnet build ArquitecturaBaseMultitenant.slnx sin advertencias; dotnet test verde, incluidas pruebas de Result, validación, resources, localización, cada ErrorType con status y detail correctos en es-AR/en-US, JSON UTC/Money, tiempo, textos, OpenAPI, hosting, rutas explícitas y guardas de arquitectura.
2. Front: npm run build, npm run lint y npm test limpios; pruebas de i18n/paridad, formatos, campos, paginado, tabla a 390/768/1440 y estados del tablero Avisos.
3. Contratos: docs/contracts/openapi.json generado y commiteado; npm run contracts y npm run contracts:check reproducibles, schema.d.ts generado y commiteado; chequeo CI de ambos repos conforme Q2. ExplicitRouteInventoryTests incluye GET /api/time-zones y cada ruta nueva.
4. Formatos: **para cada caso** de docs/contracts/format-cases.json, DisplayFormatterTests en back y formatters.test.ts en front producen **exactamente el mismo texto esperado** con la misma cultura y zona; ningún tipo nuevo presentado queda sin caso aplicable. Los dos tests leen el mismo archivo del back.
5. Arnés: todas las carpetas E1 del mapa tienen AGENTS.md + CLAUDE.md, las fichas enlazan archivos y tests reales, HarnessTests y harness.test.ts verdes; HarnessStage.Closed del back y HarnessStage del front pasan a **1** solo al cerrar la etapa.
6. Si se levantó Aspire para una comprobación, ejecutar aspire stop. Los repos quedan en main, con commits locales pequeños, sin push.
