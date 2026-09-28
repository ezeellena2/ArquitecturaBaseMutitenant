# Etapa 1: núcleo transversal Implementation Plan

> **Para agentes:** al ejecutar este plan, usar la skill executing-plans o subagent-driven-development y seguir las casillas tarea por tarea. Las tareas 1 y 2 ya cerraron en `c5b5180` y `53056f8`; commitear primero esta revisión documental, luego reanudar código desde la tarea 3, con un commit por tarea y en orden.

**Goal:** Dejar probados Result, errores, catálogos de referencia oficiales, cultura, tiempo, validación, logging, OpenAPI y la presentación unificada de datos en el backend y el front.

**Architecture:** El backend conserva Domain ← Application ← Infrastructure, con Api como composición. En E1, Infrastructure lee JSON de referencia versionados, Application expone puertos de catálogo y Domain recibe `MinorUnits` sin depender de catálogos. El front consume `GET /api/reference-data` desde `shared/referenceData` y concentra la presentación en `shared/format` y `shared/ui/format`. Ambos consumen los mismos casos de formato versionados por el backend.

**Tech Stack:** .NET 10, ASP.NET Core MVC, xUnit v3, React, TypeScript, Vite, Vitest, TanStack Query y PostgreSQL/Aspire ya preparados en la Etapa 0.

---

## Decisiones recibidas para ejecutar la Etapa 1

No quedan preguntas técnicas pendientes. Según AGENTS.md, una duda nueva se resuelve primero con `../ArquitecturaBase` o `../ArquitecturaBaseFront`; si no existe allí, se elige la solución más simple compatible con los documentos y se registra en «Decisiones tomadas» del informe final. Solo se consulta si cambia el producto o contradice una regla escrita.

1. `format-cases.json` usa el esquema de ArquitecturaBase si existe; si no, `{ "now": "2026-09-27T15:00:00Z", "cases": [{ "id", "type", "culture", "timeZone", "input", "expected" }] }`. Incluye **todos** los tipos de formatos.md en `es-AR` y `en-US`. Cada caso debe pasar en ambos lados. El value object `TaxId` sigue en E6; el texto fiscal de los casos compartidos se produce en E1 a partir de `{ type, number }`.
2. `contracts:check` y la prueba de paridad de `format-cases.json` son obligatorios en local. En el CI del front, si falta el repo hermano, se saltan con un aviso visible; cuando ambos repos estén en GitHub se agrega el checkout cruzado.
3. ADR 0036 reemplaza la ruta horaria aislada por `GET /api/reference-data` y cinco rutas por catálogo. Son `[AllowAnonymous]` y entran en la lista explícita de `AccessDeclarationTests` al nacer esa guarda en E3.
4. ADR 0036 reemplaza las ocho zonas fijas: se generan las zonas canónicas IANA y las ciudades traducidas desde CLDR/overrides. El offset se calcula al mostrar con `TimeProvider` como `GMT−3`, con signo menos tipográfico; nunca se almacena.
5. En E1, `useFormat` expone carga hasta recibir el catálogo; después usa sus valores por defecto `es-AR`, Buenos Aires y ARS y lee la cultura de `localStorage` si está habilitada. No hay defaults literales en TS; en E3 se conecta a `/api/me`.
6. El teléfono se muestra en formato nacional si su país coincide con el de la cultura, e internacional en caso contrario. Los casos compartidos fijan el texto exacto si las librerías difieren.
7. Las versiones se toman de ArquitecturaBase cuando estén; para las dependencias faltantes se fija la última estable exacta en `Directory.Packages.props` o `package.json`. `Microsoft.Extensions.ApiDescription.Server` exporta `docs/contracts/openapi.json` durante el build.
8. ADR 0036 reemplaza la lista cerrada de monedas: ISO 4217 versionado en JSON contiene los códigos y unidades vigentes, incluidas monedas con 0, 2 y 3 decimales. Application rechaza códigos ausentes o deshabilitados para datos nuevos; Domain valida únicamente forma y recibe `MinorUnits` al redondear.
9. `ValidPermissions` nace en E4 con el catálogo. La validación de E1 no lo anticipa.
10. `UniqueConstraintViolationException` nace en E2 con `IUnitOfWork`; queda fuera de E1.
11. `NormalizedInputTests` hace POST a `TestFeatures/TestController`, que devuelve el cuerpo ya normalizado; no usa persistencia E2.
12. `TaxIdField` queda en E1: sus tipos salen del catálogo (`type` es el código completo, por ejemplo `AR-CUIT`); valida solo mediante `stdnum` y emite `{ type, number }`, con `number` sin separadores. El value object del back nace en E6 y el contrato HTTP de esa feature incluye `{ country, type, number }` según backend.md §18.
13. E1 cubre falta de conexión, versión nueva y 429: `retryAfterSeconds` en `httpClient` y cuenta regresiva en el botón iniciador. El flujo de sesión vencida espera E3.
14. `usePagination(result?: { items; totalCount })` corrige internamente la página solo al recibir `items` vacíos con `totalCount > 0`, con `replace` y sin entrada nueva en el historial; si ArquitecturaBaseFront resuelve el mismo caso de otro modo, se copia ese patrón.
15. `/swagger` y `/openapi` se agregan a `BackendPrefixes` y al proxy de Vite solo en Development.
16. `statusTones` de E1 tiene `success`, `warning`, `danger`, `neutral` y `pending` según tema.md. Los estados de negocio llegan con cada feature; un enum de TestFeatures basta para probarlo.
17. E1 conecta en el orden de backend.md §16 el pipeline disponible. Se dejan comentadas las posiciones de `UseAuthentication`, `TenantResolutionMiddleware` y `UseAuthorization` (E3) y del bootstrap de base (E2); `PublicSiteResolutionMiddleware` y `LegalAcceptanceMiddleware` conservan sus etapas E7/E3. Las piezas no disponibles no se activan antes de su etapa.

### Decisiones técnicas para generar los catálogos, conforme ADR 0036

- Fijar `cldr-core` 48.2 para `codeMappings.json` (alfa-2/alfa-3/numérico), nombres y datos monetarios, y versionar aparte `sources/cldr-region-validity.xml` de CLDR `release-48-2` porque el paquete JSON no trae esa clasificación. Para ISO 3166-1, expandir los rangos `regular` del XML y cruzarlos con `codeMappings` que tengan alfa-3 y numérico de tres dígitos menor que 900: son 249 países en esta versión; `AN`, `AA` y `XK` no entran. Para monedas, usar la lista vigente ISO 4217 de SIX. El script registra versión y URL de origen y falla si una referencia falta. Respaldo: datos-de-referencia.md §§1, 3 y 6. No escribir listas manuales de países ni monedas.
- Generar `CurrencyTranslations.DisplaySymbol` por cultura desde CLDR junto con el nombre/plural de cada moneda. `Currencies.Symbol` global no reemplaza ese símbolo visible; `DisplayFormatter` y el front combinan `DisplaySymbol` con el patrón de `Cultures`. Respaldo: datos-de-referencia.md §§2–4 y formatos.md front §3.
- Obtener `CallingCode` de los metadatos versionados de libphonenumber; derivar `DefaultCurrencyCode` de CLDR `currencyData` vigente y `DefaultTimeZoneId` de una zona canónica IANA cuyo `CountryCodes[]` contenga ese país, con orden estable si hay varias. Para un territorio ISO sin dato oficial, dejar el campo correspondiente en `null` y `IsEnabled=false`; validar FK solo si no es `null`. Nunca inventar moneda, prefijo ni zona. Respaldo: datos-de-referencia.md §§1–4.
- El XML oficial de SIX se procesa con un parser XML mantenido y fijado a versión exacta en `scripts/datos-de-referencia/package.json`/`package-lock.json`; usar `npm ci --prefix scripts/datos-de-referencia` para regenerar y probar, sin parsear XML con expresiones regulares. Respaldo: datos-de-referencia.md §§1 y 3.
- SIX e IANA publican URLs cuyo contenido cambia; la validez XML de CLDR tampoco viene en `cldr-core`. Versionar los tres snapshots bajo `scripts/datos-de-referencia/sources/` y sus hashes/URL/versión en `sources.lock.json`; la generación normal usa esos archivos, `--refresh` descarga y actualiza snapshot, lock y JSON. Los demás datos de CLDR y libphonenumber vienen de dependencias exactas fijadas en `package-lock.json`. Fijar también la versión de Node en `.node-version` porque `Intl.DisplayNames` depende de ICU. Los tests usan fixtures locales pequeños y no requieren red. Respaldo: datos-de-referencia.md §§1, 3 y 8.
- Obtener zonas canónicas y **todos** sus países desde la columna múltiple de IANA tzdb, conservarlos como `CountryCodes[]` en `time-zones.json` y agregar UTC desde tzdb con `CountryCodes=[]`. Una zona se habilita inicialmente si alguno de sus países está habilitado, además de UTC; los tipos fiscales se habilitan para países habilitados. `habilitados.json` aporta overrides y `SortOrder`, sin listas en código. En E2 esa relación pasa a `TimeZoneCountries`, sin `TimeZones.CountryCode` singular. Los nombres salen de CLDR/`Intl.DisplayNames` y `ciudades.<idioma>.json`; `cultures.source.json` (`es-AR`, `en-US`) y `tax-id-types.source.json` contienen patrones/tipos fiscales. El generador emite los cinco JSON de Infrastructure, que nunca se editan a mano. Respaldo: datos-de-referencia.md §§2–4 y 8.
- En E1 no hay tablas, seed de base, EF ni HybridCache para estos catálogos: los cinco puertos de Application usan `JsonReferenceDataCatalog`. Las tablas, `ReferenceDataSeeder` y `ReferenceDataReader` corresponden a E2. Respaldo: datos-de-referencia.md §§4 y 8.

**Fuentes para el generador:** [SIX ISO 4217 List One](https://www.six-group.com/dam/download/financial-information/data-center/iso-currrency/lists/list-one.xml), [Unicode CLDR JSON 48.2 codeMappings](https://github.com/unicode-org/cldr-json/blob/48.2.0/cldr-json/cldr-core/supplemental/codeMappings.json) (incluye AR → ARG/032), [Unicode CLDR 48.2 region.xml](https://github.com/unicode-org/cldr/blob/release-48-2/common/validity/region.xml) para la clasificación de vigencia, [Unicode CLDR JSON](https://github.com/unicode-org/cldr-json) para nombres y moneda de territorio, e [IANA tzdb](https://data.iana.org/time-zones/tzdb/zone1970.tab) para zonas y país. Se fijan versiones/URL en la generación; los metadatos de prefijos vienen de la versión fijada de libphonenumber. Respaldo: datos-de-referencia.md §§1–3.

## Método de ejecución y rutas

- **Orden obligatorio:** una tarea, su test rojo, implementación mínima, test verde, commit en main del repo indicado; sin push. Antes de cada tarea releer plan maestro “Reglas para todas las etapas” y “Etapa 1”, y las entradas [E1] de ambos arbol.md. Toda duda técnica se resuelve con las bases de solo lectura o con la opción más simple compatible, y se registra en «Decisiones tomadas»; solo frenan cambios de producto o contradicciones escritas.
- **Comandos de prueba:** D = dotnet test --project tests/ArquitecturaBaseMultitenant.Domain.UnitTests/ArquitecturaBaseMultitenant.Domain.UnitTests.csproj; A = dotnet test --project tests/ArquitecturaBaseMultitenant.Application.UnitTests/ArquitecturaBaseMultitenant.Application.UnitTests.csproj; I = dotnet test --project tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/ArquitecturaBaseMultitenant.Api.IntegrationTests.csproj; R = dotnet test --project tests/ArquitecturaBaseMultitenant.ArchitectureTests/ArquitecturaBaseMultitenant.ArchitectureTests.csproj. En cada tarea, “prueba X” significa ejecutar el comando correspondiente con -- --filter-class X, comprobar FAIL por la conducta aún ausente, implementar y repetir hasta PASS. Para front, ejecutar npm test -- ruta/del/test desde ../ArquitecturaBaseMutitenantFront; el primer pase debe fallar por el caso nuevo y el segundo pasar. Si una prueba exige compilación, preparar únicamente el armazón necesario para obtener el fallo de comportamiento, sin adelantar la implementación.
- **Rutas:** las rutas back son relativas a este repo; las front empiezan por ../ArquitecturaBaseMutitenantFront/. Cada flecha ← indica copia de archivo real del repo de solo lectura, seguida de la adaptación documentada. Los archivos AGENTS.md y CLAUDE.md se crean en la misma tarea que la carpeta del mapa (arnes.md back §3 / front §2); CLAUDE.md contiene @AGENTS.md. Los tests y la documentación de una regla se commitean con su código.
- **Verificación continua:** al terminar cada tarea, además de la prueba focal, compilar el proyecto afectado y revisar git diff --check. La puerta integral se ejecuta al final, antes de subir HarnessStage.

## Tareas backend

### Tarea 1. Result y errores de dominio

**Estado:** cerrada en `c5b5180`.

**Archivos:** crear src/ArquitecturaBaseMultitenant.Domain/Results/{Error,ErrorType,Result,ValidationError}.cs ← ../ArquitecturaBase/src/ArquitecturaBase.Domain/Results/{Error,ErrorType,Result,ValidationError}.cs; tests/ArquitecturaBaseMultitenant.Domain.UnitTests/Results/ResultTests.cs ← ../ArquitecturaBase/tests/ArquitecturaBase.Domain.UnitTests/Results/ResultTests.cs y ValidationErrorTests.cs (nuevo); modificar tests/ArquitecturaBaseMultitenant.Domain.UnitTests/ArquitecturaBaseMultitenant.Domain.UnitTests.csproj para quitar --ignore-exit-code 8.
**Respaldo:** plan maestro §Etapa 1, Back 1; backend.md §6 “Tipos” y “Catálogos”; arbol.md back “Domain/Results” y “Domain.UnitTests/Results”; rules/result-y-errores.md “Cómo se hace”.
- [x] Escribir/ajustar ResultTests y ValidationErrorTests para éxitos, error y errores por campo; ejecutar D con cada clase y comprobar rojo.
- [x] Copiar/adaptar los cuatro tipos, ejecutar D con ambas clases y comprobar verde.
- [x] Commit back: feat: establecer Result y errores de dominio

### Tarea 2. Bases de entidad y value object

**Estado:** cerrada en `53056f8`.

**Archivos:** crear src/ArquitecturaBaseMultitenant.Domain/Common/{Entity,ValueObject}.cs ← ../ArquitecturaBase/src/ArquitecturaBase.Domain/Common/{Entity,ValueObject}.cs; src/ArquitecturaBaseMultitenant.Domain/Common/{AGENTS,CLAUDE}.md; test nuevo tests/ArquitecturaBaseMultitenant.Domain.UnitTests/Common/EntityAndValueObjectTests.cs.
**Respaldo:** plan maestro §Etapa 1, Back 1; backend.md §4.1; arbol.md back “Domain/Common”; arnes.md back §3.
- [x] Probar Guid v7, constructor EF protegido e igualdad por valor; ejecutar D con EntityAndValueObjectTests y comprobar rojo.
- [x] Copiar/adaptar bases y punteros; repetir la prueba hasta verde.
- [x] Commit back: feat: establecer bases de dominio

### Tarea 3. Generador oficial de monedas y países

**Estado:** cerrada en el commit de esta tarea.

**Archivos:** crear `.gitattributes` (snapshots sin conversión de bytes; LF fijo para JSON generados), `scripts/datos-de-referencia/{generar.mjs,generar.test.mjs,package.json,package-lock.json,.node-version,sources.lock.json,habilitados.json,AGENTS.md,CLAUDE.md}`, `scripts/datos-de-referencia/sources/{iso4217-list-one.xml,cldr-region-validity.xml}` (snapshots de SIX y CLDR `release-48-2` descargados por `--refresh`), y `src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/Seed/ReferenceData/{currencies.json,countries.json,AGENTS.md,CLAUDE.md}`. `package.json` fija parser XML, CLDR y libphonenumber; `habilitados.json` contiene default, `SortOrder` y overrides editables, no un catálogo manual. No hay archivo equivalente en `../ArquitecturaBase`.
**Secuencia:** `Countries.DefaultTimeZoneId` queda `null` provisionalmente en esta tarea porque el snapshot IANA nace en la tarea 4. Esa tarea regenera `countries.json`, deriva la zona por pertenencia a `CountryCodes[]` y cierra la FK país→zona; no se asigna una zona supuesta en la tarea 3.
**Respaldo:** datos-de-referencia.md §§1–3, 6 y 8; rules/datos-de-referencia.md “Cómo se hace” y “Lo verifica”; arbol.md back “Infrastructure/Persistence/Seed/ReferenceData”; plan maestro §Etapa 1, Back 1 y formatos. Fuente: ISO 4217 vigente de SIX, ISO 3166 mediante CLDR `codeMappings` y validez versionados, CLDR `currencyData`, metadatos libphonenumber.
- [x] Tras `npm ci --prefix scripts/datos-de-referencia`, probar primero origen ausente, hash incorrecto de cualquiera de los dos snapshots, expansión de rangos `regular` de CLDR, 249 países y rechazo de `AN` (histórico), `AA` (reservado) y `XK` (regular en CLDR pero numérico privado), país ISO sin moneda/prefijo propio (`null`, deshabilitado), FK no nula rota, unidades menores ausentes, `IsEnabled`/`SortOrder` derivados solo de `habilitados.json` y `CurrencyTranslations.DisplaySymbol` de CLDR por cultura (`ARS`: `$` en es-AR, `ARS` en en-US; USD según CLDR); `node --test scripts/datos-de-referencia/generar.test.mjs` en rojo con fixtures locales.
- [x] Probar un fondo vigente de SIX sin traducción CLDR: conserva nombre oficial y símbolo código, queda deshabilitado y un override no puede habilitarlo hasta que tenga traducciones. Una fila con unidades `N.A.` no recibe decimales supuestos.
- [x] Generar los dos JSON con versión/URL de las fuentes, traducciones y símbolos monetarios por cultura, `IsEnabled`/`SortOrder`; demostrar monedas de 0, 2 y 3 decimales y regeneración reproducible desde snapshot verificado; repetir la prueba en verde.
- [x] Commit back: feat: generar monedas y países de fuentes oficiales

### Tarea 4. Generador de zonas, culturas y tipos fiscales

**Archivos:** ampliar `scripts/datos-de-referencia/{generar.mjs,generar.test.mjs,sources.lock.json,habilitados.json}`; crear `scripts/datos-de-referencia/sources/iana-zone1970.tab` (snapshot descargado por `--refresh`), `scripts/datos-de-referencia/{ciudades.es.json,ciudades.en.json,cultures.source.json,tax-id-types.source.json}` y `src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/Seed/ReferenceData/{time-zones,cultures,tax-id-types}.json`. No hay archivo equivalente en ArquitecturaBase. Patrones y tipos fiscales se editan solo en los archivos `.source.json`.
**Respaldo:** datos-de-referencia.md §§2–3, 5, 7–8; rules/datos-de-referencia.md “Lo verifica”; arbol.md back “Infrastructure/Persistence/Seed/ReferenceData”; rules/fechas-y-zonas.md. Fuentes: IANA tzdb fijado y CLDR fijado; `Intl.DisplayNames` y overrides de ciudad.
- [x] Probar primero zonas IANA canónicas con `CountryCodes[]` completo (fixture multipaís como `AE,OM`), UTC con `CountryCodes=[]`, nombres de cultura traducidos para `CultureSelect`, una sola cultura por defecto, patrones `es-AR`/`en-US` y tipos fiscales de los `.source.json`. Verificar habilitación si **algún** país de la zona está habilitado, más UTC; tipos fiscales de país habilitado; overrides/`SortOrder` de `habilitados.json`; FK no nulas país→moneda/zona; `Countries.DefaultTimeZoneId` pertenece a una zona que incluye ese país; y traducciones de toda cultura habilitada. Un territorio ISO sin zona propia conserva `null` y queda deshabilitado. `node --test scripts/datos-de-referencia/generar.test.mjs` en rojo.
- [x] Completar el generador y los tres JSON; regenerar dos veces y comparar bytes; repetir la prueba en verde.
- [x] Commit back: feat: generar zonas culturas e identificación fiscal

### Tarea 5. Puertos y adaptador JSON de datos de referencia

**Archivos:** crear `src/ArquitecturaBaseMultitenant.Application/Interfaces/ReferenceData/{ICurrencyCatalog,ICountryCatalog,ITimeZoneCatalog,ICultureCatalog,ITaxIdTypeCatalog}.cs` y `{AGENTS,CLAUDE}.md`; `src/ArquitecturaBaseMultitenant.Infrastructure/ReferenceData/JsonReferenceDataCatalog.cs` y `{AGENTS,CLAUDE}.md`; actualizar `src/ArquitecturaBaseMultitenant.Infrastructure/{ArquitecturaBaseMultitenant.Infrastructure.csproj,DependencyInjection.cs}` para incluir los cinco JSON como `EmbeddedResource` y cargar siempre desde el ensamblado; actualizar el `.csproj` de Application.UnitTests para referenciar Infrastructure solo desde tests; crear `tests/ArquitecturaBaseMultitenant.Application.UnitTests/ReferenceData/ReferenceDataCatalogTests.cs` (JSON reales embebidos). No hay homónimos en `../ArquitecturaBase`.
**Respaldo:** datos-de-referencia.md §§2, 4 y 8; rules/datos-de-referencia.md “Cómo se hace” y “Lo verifica”; backend.md §§3 y 18; arbol.md back “Application/Interfaces/ReferenceData”, “Infrastructure/ReferenceData”.
- [ ] Probar lectura de cinco catálogos desde recursos embebidos, aun sin checkout en el directorio de trabajo, búsqueda, vigencia, `IsEnabled`, nombres traducidos de culturas con fallback, `CountryCodes[]` completo en zonas multipaís/UTC, FK internas no nulas y `MinorUnits` 0/2/3; A/ReferenceDataCatalogTests en rojo.
- [ ] Implementar los puertos en Application y el único adaptador JSON en Infrastructure, con registros DI explícitos; repetir en verde.
- [ ] Commit back: feat: leer catálogos de referencia desde JSON

### Tarea 6. Dinero, moneda y cultura

**Archivos:** crear src/ArquitecturaBaseMultitenant.Domain/ValueObjects/{Money,CurrencyCode,CultureCode}.cs y ValueObjects/{AGENTS,CLAUDE}.md; tests/ArquitecturaBaseMultitenant.Domain.UnitTests/ValueObjects/{Money,CurrencyCode,CultureCode}Tests.cs (nuevos).
**Respaldo:** plan maestro §Etapa 1, Back 1; backend.md §18 “Contrato en la API”; datos-de-referencia.md §§2, 4 y 8; arbol.md back “Domain/ValueObjects”; rules/numeros-y-moneda.md y rules/datos-de-referencia.md; arnes.md back §3; decisión 8 de este plan.
- [ ] Escribir tests de suma solo con igual moneda, `Money.Round(minorUnits)` con 0/2/3 y AwayFromZero, `CurrencyCode` alfa-3 y `CultureCode` con forma BCP 47; D por clase debe fallar.
- [ ] Implementar VOs sin catálogo ni lista de monedas/culturas en Domain; la existencia y habilitación se validan en Application. D por clase en verde.
- [ ] Commit back: feat: agregar dinero moneda y cultura

### Tarea 7. Normalización y límites de texto

**Archivos:** crear src/ArquitecturaBaseMultitenant.Domain/Common/TextLimits.cs; src/ArquitecturaBaseMultitenant.Application/Common/Text/TextNormalizer.cs; tests/ArquitecturaBaseMultitenant.Application.UnitTests/Common/TextNormalizerTests.cs y tests/ArquitecturaBaseMultitenant.ArchitectureTests/TextLimitsTests.cs (nuevos); modificar tests/ArquitecturaBaseMultitenant.Application.UnitTests/ArquitecturaBaseMultitenant.Application.UnitTests.csproj para quitar --ignore-exit-code 8.
**Respaldo:** plan maestro §Etapa 1, Back 11; backend.md §20; arbol.md back “Piezas de los estándares P4”; rules/textos-libres.md “Cómo se hace” y “Lo verifica”.
- [ ] Probar `Clean` global con trim, NFC, invisibles y vacío→null, conservando espacios y saltos internos; probar por separado el helper tipado que colapsa solo nombres, y los largos declarados. A/TextNormalizerTests y R/TextLimitsTests en rojo.
- [ ] Implementar `Clean`, el helper tipado de nombres y las constantes; repetir ambas pruebas en verde. La conexión del conversor JSON global llega en T20.
- [ ] Commit back: feat: normalizar textos y centralizar límites

### Tarea 8. Contrato compartido de formatos

**Archivos:** crear docs/contracts/format-cases.json y tests/ArquitecturaBaseMultitenant.Application.UnitTests/Common/FormatCasesContractTests.cs (nuevos); el EmailTests de la tarea siguiente leerá los casos de correo de este contrato.
**Respaldo:** plan maestro §Etapa 1, Back 5 y puerta; backend.md §18; datos-de-referencia.md §7; arbol.md back “Raíz/docs/contracts”; formatos.md front §§2–3; decisiones 1 y 6 de este plan.
- [ ] Probar `now` fijo, `id/type/culture/timeZone/input/expected` por caso, todos los tipos de formatos.md y ambas culturas habilitadas; ARS y USD con sus símbolos visibles distintos por cultura, monedas de 0/2/3 decimales y teléfonos nacionales/internacionales; A/FormatCasesContractTests en rojo.
- [ ] Versionar `{ "now": "2026-09-27T15:00:00Z", "cases": [...] }` conforme ADR 0036 §7, con textos esperados exactos; repetir en verde.
- [ ] Commit back: test: fijar casos de formato compartidos

### Tarea 9. Correo y teléfono de dominio

**Archivos:** crear src/ArquitecturaBaseMultitenant.Domain/ValueObjects/Email.cs ← ../ArquitecturaBase/src/ArquitecturaBase.Domain/ValueObjects/Email.cs (adaptar NFC/IDN); PhoneNumber.cs ← archivo homónimo base; src/ArquitecturaBaseMultitenant.Domain/Users/{EmailErrors,PhoneErrors}.cs (nuevos); tests/ArquitecturaBaseMultitenant.Domain.UnitTests/ValueObjects/{EmailTests,PhoneNumberTests}.cs ← homónimos de ../ArquitecturaBase/tests/ArquitecturaBase.Domain.UnitTests/ValueObjects/; tests/ArquitecturaBaseMultitenant.ArchitectureTests/EmailPropertyTests.cs (nuevo).
**Respaldo:** plan maestro §Etapa 1, Back 11; arbol.md back “Domain/ValueObjects”, “Domain/Users” y “Piezas P3”; rules/emails.md, rules/telefonos.md.
- [ ] Tests de correo normalizado/IDN, con casos de correo del format-cases.json, error Users.Email.Invalid, E.164 y propiedades sin correo suelto; D/EmailTests, D/PhoneNumberTests y R/EmailPropertyTests en rojo.
- [ ] Copiar/adaptar tipos y errores; repetir en verde.
- [ ] Commit back: feat: definir correo y teléfono normalizados

### Tarea 10. Culturas, resources y paridad

**Archivos:** crear src/ArquitecturaBaseMultitenant.Application/Common/Formatting/SupportedCultures.cs (consulta `ICultureCatalog`, sin lista fija); Resources/{Errors,Errors.en,Validation,Validation.en}.resx ← archivos homónimos de ../ArquitecturaBase/src/ArquitecturaBase.Application/Resources/; Resources/ErrorTexts.cs ← ErrorMessages.cs y ValidationTexts.cs ← ValidationMessages.cs de esa carpeta; Resources/{AGENTS,CLAUDE}.md; src/ArquitecturaBaseMultitenant.Api/Localization/LocalizationExtensions.cs ← ../ArquitecturaBase/src/ArquitecturaBase.Api/Localization/LocalizationExtensions.cs; tests/ArquitecturaBaseMultitenant.Application.UnitTests/Resources/{ResourceParityTests,ErrorTextsTests}.cs ← ResourceParityTests.cs y ErrorMessagesTests.cs de la base; tests/ArquitecturaBaseMultitenant.ArchitectureTests/ErrorCodeTests.cs ← homónimo base. La prueba HTTP de localización se agrega al conectar Program.
**Respaldo:** plan maestro §Etapa 1, Back 3; backend.md §6 “Catálogos” y §12; datos-de-referencia.md §§4–5; arbol.md back “Application/Resources” y tests; rules/textos-y-traducciones.md.
- [ ] Probar paridad de claves/placeholders, códigos reservados y fallback; A/ResourceParityTests, A/ErrorTextsTests y R/ErrorCodeTests en rojo.
- [ ] Copiar/adaptar resources y envoltorios, agregar culturas y localización; repetir en verde.
- [ ] Commit back: feat: localizar errores y validaciones

### Tarea 11. Contratos de paginado y orden estable

**Archivos:** crear src/ArquitecturaBaseMultitenant.Application/Common/Pagination/{PagedRequest,PagedResult,SortDescriptor}.cs ← homónimos de ../ArquitecturaBase/src/ArquitecturaBase.Application/Common/Pagination/ (tamaño 10); CursorRequest.cs y CursorResult.cs (nuevos); src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/Extensions/SortMap.cs (nuevo) y QueryableExtensions.cs ← archivo homónimo de ../ArquitecturaBase/src/ArquitecturaBase.Infrastructure/Persistence/Extensions/ (solo ApplySort en E1; búsqueda y cursor en E2); tests/ArquitecturaBaseMultitenant.Application.UnitTests/Common/PaginationContractsTests.cs y tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Contracts/SortMapTests.cs (nuevos).
**Respaldo:** plan maestro §Etapa 1, Back 6; backend.md §9 “Paginado, orden y búsqueda”; arbol.md back “Application/Common/Pagination” e “Infrastructure/Persistence/Extensions”; rules/paginado-y-busqueda.md.
- [ ] Probar página/tamaño por defecto, cursor sin total, sort permitido y desempate Id; A/PaginationContractsTests e I/SortMapTests en rojo.
- [ ] Crear contratos, SortMap y ApplySort; repetir en verde.
- [ ] Commit back: feat: definir paginado y orden estable

### Tarea 12. Logging de operaciones

**Archivos:** crear src/ArquitecturaBaseMultitenant.Application/Common/Logging/OperationLog.cs; tests/ArquitecturaBaseMultitenant.Application.UnitTests/Common/OperationLogTests.cs (nuevo).
**Respaldo:** plan maestro §Etapa 1, Back 7; backend.md §17; arbol.md back “Application/Common/Logging”; rules/logs.md.
- [ ] Probar inicio/fin/fallo mediante FakeLogger sin datos sensibles; A/OperationLogTests en rojo.
- [ ] Implementar RunAsync con LoggerMessage y TimeProvider; repetir en verde.
- [ ] Commit back: feat: registrar operaciones sin datos sensibles

### Tarea 13. Servicio de datos de referencia

**Archivos:** crear `src/ArquitecturaBaseMultitenant.Application/Interfaces/Services/{IReferenceDataService.cs,AGENTS.md,CLAUDE.md}`, `src/ArquitecturaBaseMultitenant.Application/Models/{AGENTS,CLAUDE}.md`, `src/ArquitecturaBaseMultitenant.Application/Models/ReferenceData/{ReferenceDataResponse.cs,AGENTS.md,CLAUDE.md}`, `src/ArquitecturaBaseMultitenant.Application/Services/{AGENTS,CLAUDE}.md`, `src/ArquitecturaBaseMultitenant.Application/Services/ReferenceData/{ReferenceDataService.cs,AGENTS.md,CLAUDE.md}` y `tests/ArquitecturaBaseMultitenant.Application.UnitTests/ReferenceData/ReferenceDataServiceTests.cs` (nuevos; sin equivalentes en ArquitecturaBase); actualizar `src/ArquitecturaBaseMultitenant.Application/DependencyInjection.cs`.
**Respaldo:** datos-de-referencia.md §§4–5 y 8; backend.md §§3, 5 y 17; arbol.md back “Application/Interfaces/Services/IReferenceDataService”, “Models/ReferenceData” y “Services/ReferenceData”; rules/datos-de-referencia.md y rules/logs.md.
- [ ] Probar agregado de cinco catálogos habilitados, traducción/fallback, filtros de búsqueda y `OperationLog.RunAsync`; A/ReferenceDataServiceTests en rojo.
- [ ] Implementar servicio tras los cinco puertos con `OperationLog.RunAsync` y DI explícita; repetir en verde.
- [ ] Commit back: feat: reunir catálogos de referencia en un servicio

### Tarea 14. Aritmética de zonas horarias

**Archivos:** crear src/ArquitecturaBaseMultitenant.Application/Interfaces/Integrations/{AGENTS,CLAUDE}.md y Time/ITimeZoneService.cs; Models/Time/DayRangeUtc.cs; src/ArquitecturaBaseMultitenant.Infrastructure/Time/TimeZoneService.cs y actualizar DependencyInjection.cs ← ../ArquitecturaBase/src/ArquitecturaBase.Infrastructure/DependencyInjection.cs; src/ArquitecturaBaseMultitenant.Application/DependencyInjection.cs ← ../ArquitecturaBase/src/ArquitecturaBase.Application/DependencyInjection.cs; tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Time/TimeZoneServiceTests.cs (nuevo). El catálogo ya pertenece a `ITimeZoneCatalog` y `JsonReferenceDataCatalog`.
**Respaldo:** plan maestro §Etapa 1, Back 4; backend.md §§7, 11; datos-de-referencia.md §§1, 4 y 8; arbol.md back “Interfaces/Integrations/Time”, “Models/Time” e “Infrastructure/Time”; rules/fechas-y-zonas.md; arnes.md back §3.
- [ ] Probar ID IANA del catálogo, límites UTC con DST, reloj falso y offset numérico calculado desde `TimeProvider`; I/TimeZoneServiceTests en rojo. El texto `GMT−3` lo prueba DisplayFormatter.
- [ ] Crear puerto, servicio, modelo y registros explícitos; repetir en verde.
- [ ] Commit back: feat: calcular días y offsets por zona horaria

### Tarea 15. Validación de requests

**Archivos:** crear src/ArquitecturaBaseMultitenant.Application/Common/Validation/{IRequestValidator,RequestValidator,ValidationRules,FieldErrors,PagedRequestValidator,CursorRequestValidator}.cs; copiar/adaptar ServiceRequestValidator.cs, ValidationRules.cs, FieldErrors.cs y PagedRequestValidator.cs desde ../ArquitecturaBase/src/ArquitecturaBase.Application/Common/Validation/; crear src/ArquitecturaBaseMultitenant.Application/Validation/{AGENTS,CLAUDE}.md; tests/ArquitecturaBaseMultitenant.Application.UnitTests/Common/{RequestValidatorTests,PagedRequestValidatorTests,CursorRequestValidatorTests}.cs (PagedRequestValidatorTests ← homónimo base); tests/ArquitecturaBaseMultitenant.Application.UnitTests/ReferenceData/ReferenceDataValidationTests.cs (nuevo); tests/ArquitecturaBaseMultitenant.Application.UnitTests/TestDoubles/ServiceFixture.cs (nuevo, con FakeTimeProvider, FakeLogger y RequestValidator real).
**Respaldo:** plan maestro §Etapa 1, Back 6; backend.md §6 “Validación”; datos-de-referencia.md §§4 y 8; arbol.md back “Application/Common/Validation”; rules/validacion.md y rules/datos-de-referencia.md; decisión 9 de este plan. `ValidPermissions` se incorpora en E4 con el catálogo.
- [ ] Probar validador único, FieldErrors camelCase, reglas de textos/preferencias, página/cursor y `ValidCurrency/ValidCountry/ValidTimeZone/ValidCulture/ValidTaxIdType`: código inexistente o deshabilitado se rechaza para dato nuevo, uno guardado sigue legible; A por las cuatro clases en rojo.
- [ ] Adaptar reglas y registro de E1 contra los cinco puertos, sin `ValidPermissions`; A por las cuatro clases en verde.
- [ ] Commit back: feat: centralizar validación de pedidos

### Tarea 16. DisplayFormatter

**Archivos:** crear src/ArquitecturaBaseMultitenant.Application/Common/Formatting/{DisplayFormatter,CultureProfiles}.cs y {AGENTS,CLAUDE}.md; tests/ArquitecturaBaseMultitenant.Application.UnitTests/Common/DisplayFormatterTests.cs (nuevo). `CultureProfiles.cs` adapta patrones de `ICultureCatalog`/`cultures.json`; no contiene una lista fija.
**Respaldo:** plan maestro §Etapa 1, Back 5; backend.md §18 “Formato del lado del backend”; datos-de-referencia.md §§2, 4 y 7; arbol.md back “Application/Common/Formatting”; rules/numeros-y-moneda.md, rules/fechas-y-zonas.md y rules/datos-de-referencia.md; arnes.md back §3.
- [ ] Hacer que DisplayFormatterTests recorra **todos** los casos de format-cases.json, incluidos relativa, rangos, cantidad, compacto, tamaño, duración, zona (`GMT−3` con menos tipográfico), cultura, enum, booleano, texto fiscal y ARS/USD con símbolo distinto por cultura; fallar por cualquier texto distinto.
- [ ] Formatear con perfiles de `ICultureCatalog`, `CurrencyTranslations.DisplaySymbol` de `ICurrencyCatalog` combinado con el patrón cultural, cultura/zona explícitas y tipos fiscales del catálogo, sin crear el value object `TaxId` de E6; A/DisplayFormatterTests en verde.
- [ ] Commit back: feat: unificar formato de datos en el backend

### Tarea 17. Mapeo de errores a ProblemDetails

**Archivos:** crear src/ArquitecturaBaseMultitenant.Api/ErrorHandling/{ApiErrorCodes,ProblemDetailsMapper,ControllerResultExtensions}.cs ← homónimos de ../ArquitecturaBase/src/ArquitecturaBase.Api/ErrorHandling/; crear src/ArquitecturaBaseMultitenant.Api/DependencyInjection.cs ← ../ArquitecturaBase/src/ArquitecturaBase.Api/DependencyInjection.cs; actualizar src/ArquitecturaBaseMultitenant.Api/Program.cs ← ../ArquitecturaBase/src/ArquitecturaBase.Api/Program.cs (solo piezas E1); tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/TestFeatures/TestController.cs ← ../ArquitecturaBase/tests/ArquitecturaBase.Api.IntegrationTests/TestFeatures/TestController.cs (rehacer sin BD), TestControllerApplicationPart.cs ← ../ArquitecturaBase/tests/ArquitecturaBase.Api.IntegrationTests/Support/TestControllerApplicationPart.cs; tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/ErrorHandling/{ErrorHandlingTests,ValidationProblemTests}.cs (ErrorHandlingTests ← ../ArquitecturaBase/tests/ArquitecturaBase.Api.IntegrationTests/ErrorHandlingTests.cs; ValidationProblemTests nuevo, con referencia a ProblemDetailsMapperTests.cs de la base).
**Respaldo:** plan maestro §Etapa 1, Back 2 y puerta; backend.md §6 “Mapeo HTTP”; arbol.md back “Api/ErrorHandling”; rules/result-y-errores.md.
- [ ] Probar los siete ErrorType, status, detail es-AR/en-US, code, traceId, errors y retryAfter; I/ErrorHandlingTests e I/ValidationProblemTests en rojo.
- [ ] Copiar/adaptar mapper y extensiones; repetir en verde.
- [ ] Commit back: feat: mapear resultados a ProblemDetails

### Tarea 18. Errores del framework y localización HTTP

**Archivos:** crear src/ArquitecturaBaseMultitenant.Api/ErrorHandling/{GlobalExceptionHandler,MvcInvalidModelStateResponseFactory,EmptyJsonBodyContentTypeFilter}.cs ← homónimos de ../ArquitecturaBase/src/ArquitecturaBase.Api/ErrorHandling/; actualizar src/ArquitecturaBaseMultitenant.Api/{DependencyInjection,Program}.cs; crear tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/ErrorHandling/FrameworkErrorsTests.cs ← ../ArquitecturaBase/tests/ArquitecturaBase.Api.IntegrationTests/FrameworkErrorsTests.cs y Localization/LocalizationTests.cs ← ../ArquitecturaBase/tests/ArquitecturaBase.Api.IntegrationTests/LocalizationTests.cs.
**Respaldo:** plan maestro §Etapa 1, Back 2 y 10; backend.md §§6, 7, 16; arbol.md back “Api/ErrorHandling” y “TestFeatures”; rules/api-http.md y rules/result-y-errores.md; decisión 17 de este plan.
- [ ] Probar cuerpo ilegible, tipo incorrecto, 401/403/404/405/429, excepción 500 sin mensaje interno y Accept-Language; I/FrameworkErrorsTests e I/LocalizationTests en rojo.
- [ ] Conectar errores MVC y localización en Program; repetir ambas clases en verde.
- [ ] Commit back: feat: unificar errores HTTP del framework

### Tarea 19. JSON de fechas civiles e instantes

**Archivos:** crear src/ArquitecturaBaseMultitenant.Api/Json/{JsonConfiguration,DateOnlyConverter,TimeOnlyConverter}.cs y {AGENTS,CLAUDE}.md; UtcDateTimeConverter.cs ← ../ArquitecturaBase/src/ArquitecturaBase.Api/Json/UtcDateTimeConverter.cs; ampliar tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/TestFeatures/TestController.cs con rutas de prueba de fecha/hora; tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Json/UtcDateTimeTests.cs ← ../ArquitecturaBase/tests/ArquitecturaBase.Api.IntegrationTests/Json/UtcDateTimeConverterTests.cs, y DateOnlyTimeOnlyTests.cs (nuevo).
**Respaldo:** plan maestro §Etapa 1, Back 4 y 10; backend.md §§11, 18; arbol.md back “Api/Json” y tests; rules/fechas-y-zonas.md; arnes.md back §3.
- [ ] Probar ISO Z, rechazo sin offset, yyyy-MM-dd y HH:mm:ss; I/UtcDateTimeTests e I/DateOnlyTimeOnlyTests en rojo.
- [ ] Registrar los conversores en un único ConfigureJson; repetir en verde.
- [ ] Commit back: feat: serializar tiempo con contratos UTC

### Tarea 20. JSON de dinero y texto de entrada

**Archivos:** crear src/ArquitecturaBaseMultitenant.Api/Json/{MoneyJsonConverter,NormalizedStringJsonConverter,RawTextAttribute}.cs; actualizar src/ArquitecturaBaseMultitenant.Api/Json/JsonConfiguration.cs; crear src/ArquitecturaBaseMultitenant.Api/Contracts/{AGENTS,CLAUDE}.md y Common/PhoneInputHttpRequest.cs ← ../ArquitecturaBase/src/ArquitecturaBase.Api/Contracts/Users/PhoneNumberHttpRequest.cs (adaptado); ampliar tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/TestFeatures/TestController.cs con rutas de prueba de Money y un POST de texto; tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Json/MoneyJsonTests.cs y Api/NormalizedInputTests.cs (nuevos).
**Respaldo:** plan maestro §Etapa 1, Back 5, 10–11; backend.md §§18, 20; arbol.md back “Api/Json” y “Piezas P4”; rules/textos-libres.md y rules/numeros-y-moneda.md; arnes.md back §3; decisión 11 de este plan.
- [ ] Probar objeto {amount,currency}, sintaxis monetaria inválida→400, limpieza global y excepción RawText; la validación de existencia/habilitación queda en Application. El POST de TestController devuelve el cuerpo normalizado sin persistir. I/MoneyJsonTests e I/NormalizedInputTests en rojo.
- [ ] Implementar conversores y contrato telefónico según alcance resuelto; repetir en verde.
- [ ] Commit back: feat: serializar dinero y limpiar entradas

### Tarea 21. API anónima de datos de referencia e inventario

**Archivos:** crear src/ArquitecturaBaseMultitenant.Api/Controllers/{AGENTS,CLAUDE}.md y ReferenceData/{ReferenceDataController.cs,AGENTS.md,CLAUDE.md}; src/ArquitecturaBaseMultitenant.Api/Contracts/ReferenceData/{ReferenceDataHttpResponse.cs,AGENTS.md,CLAUDE.md}; tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Contracts/ExplicitRouteInventoryTests.cs y ReferenceData/ReferenceDataApiTests.cs (nuevos). No hay controller equivalente en ArquitecturaBase.
**Respaldo:** datos-de-referencia.md §§4–5 y 8; plan maestro §Etapa 1, Back 4 y puerta general 3; backend.md §§5, 11; arbol.md back “Api/Controllers/ReferenceData” y “Api.IntegrationTests/Contracts”; rules/api-http.md y rules/datos-de-referencia.md; arnes.md back §3; decisión 3 de este plan.
- [ ] Probar `GET /api/reference-data` y `/api/reference-data/{currencies,countries,time-zones,cultures,tax-id-types}` con búsqueda, `[AllowAnonymous]`, solo habilitados, `CountryCodes[]` completo de una zona multipaís, traducción/fallback por cultura, ETag y caché HTTP; I/ExplicitRouteInventoryTests e I/ReferenceDataApiTests en rojo. Incluir las rutas en la lista explícita de `AccessDeclarationTests` cuando nazca en E3.
- [ ] Crear controller fino, contratos HTTP y caché; actualizar inventario de todas las rutas existentes; repetir en verde.
- [ ] Commit back: feat: exponer datos de referencia por API

### Tarea 22. OpenAPI versionado

**Archivos:** crear src/ArquitecturaBaseMultitenant.Api/OpenApi/{OpenApiExtensions,ProblemResponsesConvention,ProducesProblemAttribute}.cs ← homónimos de ../ArquitecturaBase/src/ArquitecturaBase.Api/OpenApi/; actualizar src/ArquitecturaBaseMultitenant.Api/ArquitecturaBaseMultitenant.Api.csproj, Directory.Packages.props, Program.cs y .github/workflows/ci.yml; generar docs/contracts/openapi.json; crear tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Contracts/{OpenApiTests,OpenApiContractTests}.cs (OpenApiTests ← ../ArquitecturaBase/tests/ArquitecturaBase.Api.IntegrationTests/OpenApiTests.cs).
**Respaldo:** plan maestro §Etapa 1, Back 8 y puerta general 6; backend.md §17; datos-de-referencia.md §4; arbol.md back “Api/OpenApi”, “Raíz/docs/contracts” y “Api.IntegrationTests/Contracts”; rules/api-http.md; decisión 7 de este plan.
- [ ] Probar esquemas 2xx, ProblemDetails, las seis rutas de referencia, Swagger solo Development y artefacto al día; I/OpenApiTests e I/OpenApiContractTests en rojo.
- [ ] Integrar `Microsoft.Extensions.ApiDescription.Server`, fijar su versión exacta en `Directory.Packages.props`, exportar durante el build a `docs/contracts/openapi.json` y comprobarlo en CI; repetir en verde.
- [ ] Commit back: feat: versionar contrato OpenAPI

### Tarea 23. Hosting y guía de prefijos

**Archivos:** crear src/ArquitecturaBaseMultitenant.Api/Hosting/{ForwardedHeadersExtensions,SecurityHeadersExtensions,SpaExtensions}.cs ← homónimos de ../ArquitecturaBase/src/ArquitecturaBase.Api/Hosting/; actualizar src/ArquitecturaBaseMultitenant.Api/Program.cs; crear docs/guides/prefijo-de-backend.md; tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Hosting/SpaHostingTests.cs ← ../ArquitecturaBase/tests/ArquitecturaBase.Api.IntegrationTests/Hosting/SpaHostingTests.cs y SecurityHeadersTests.cs (nuevo, tomando ForwardedHeadersTests.cs de base como referencia).
**Respaldo:** plan maestro §Etapa 1, Back 9; backend.md §§16, 19; arbol.md back “Api/Hosting” y “Documentación en capas”; rules/api-http.md; decisiones 15 y 17 de este plan. El proxy del front se actualiza en la tarea de contratos generados.
- [ ] Probar prefijos backend, `/swagger` y `/openapi` solo en Development, fallback SPA en host principal/subdominio y headers; I/SpaHostingTests e I/SecurityHeadersTests en rojo.
- [ ] Copiar/adaptar hosting y guía; conectar las piezas operativas de §16 en orden y dejar comentados los lugares de autenticación, resolución tenant, autorización, bootstrap y middleware de etapas posteriores; repetir en verde.
- [ ] Commit back: feat: completar hosting y documentar prefijos

### Tarea 24. Marca de idempotencia

**Archivos:** crear src/ArquitecturaBaseMultitenant.Api/Idempotency/{IdempotentAttribute.cs,AGENTS.md,CLAUDE.md}; tests/ArquitecturaBaseMultitenant.ArchitectureTests/IdempotentActionsTests.cs (nuevo).
**Respaldo:** plan maestro §Etapa 1, Back 12; arbol.md back “Piezas P6”; rules/idempotencia.md; arnes.md back §3.
- [ ] Probar que todo POST con respuesta 201/202 declara la marca; R/IdempotentActionsTests en rojo con caso de control.
- [ ] Crear solo el atributo, sin filtro/tabla/worker E2; repetir en verde.
- [ ] Commit back: feat: marcar acciones idempotentes

### Tarea 25. Guardas de arquitectura del núcleo

**Archivos:** crear tests/ArquitecturaBaseMultitenant.ArchitectureTests/{ApplicationPublicApiTests,ControllerInputContractTests,ControllerServiceRepositoryTests,ApplicationServicesTests}.cs ← homónimos de ../ArquitecturaBase/tests/ArquitecturaBase.ArchitectureTests/; ServiceDependencyCountTests.cs, DecimalPrecisionTests.cs y NoManualFormattingTests.cs (nuevos).
**Respaldo:** plan maestro §Etapa 1, Back 10 y “Reglas para todas las etapas”; backend.md §§3, 5, 18; arbol.md back “ArchitectureTests”; rules/capas-y-flujo.md, rules/numeros-y-moneda.md y rules/tests.md.
- [ ] Escribir/adaptar cada guarda y demostrar rojo con una violación de prueba local, retirada antes del commit; R con cada una de las siete clases.
- [ ] Ajustar únicamente código E1 que incumpla la guarda y repetir R completo en verde.
- [ ] Commit back: test: proteger arquitectura del núcleo transversal

### Tarea 26. Guarda de catálogos sin literales

**Archivos:** crear `tests/ArquitecturaBaseMultitenant.ArchitectureTests/ReferenceDataHardcodeTests.cs` (nuevo); ajustar solo código E1 que la prueba descubra. No hay archivo equivalente en ArquitecturaBase.
**Respaldo:** datos-de-referencia.md §§1, 3–4 y 8; rules/datos-de-referencia.md “Prohibido” y “Lo verifica”; arbol.md back “ArchitectureTests”; plan maestro “Reglas para todas las etapas”.
- [ ] Escribir la guarda para detectar listas, `enum`, `switch` y comparaciones de códigos ISO/IANA/culturas fuera de los JSON/tests y demostrar rojo con una violación temporal, retirada antes del commit.
- [ ] Quitar hardcodes de E1 y ejecutar R/ReferenceDataHardcodeTests en verde; conservar los defaults como datos del catálogo.
- [ ] Commit back: test: impedir catálogos escritos en código

## Tareas frontend

### Tarea 27. i18n, namespaces y paridad

**Archivos:** crear ../ArquitecturaBaseMutitenantFront/src/shared/i18n/{index.ts,i18n.test.tsx} ← homónimos de ../ArquitecturaBaseFront/src/shared/i18n/ (adaptar es-AR/en-US y clave local); ../ArquitecturaBaseMutitenantFront/src/locales/es/{common,errors,enums}.json y en/{common,errors,enums}.json (common ← homónimo de ../ArquitecturaBaseFront/src/locales/es/ y en/; errors/enums nuevos); ../ArquitecturaBaseMutitenantFront/src/locales/parity.test.ts ← homónimo base; ../ArquitecturaBaseMutitenantFront/src/locales/{AGENTS,CLAUDE}.md; modificar ../ArquitecturaBaseMutitenantFront/src/app/providers.tsx.
**Respaldo:** plan maestro §Etapa 1, Front 2; frontend.md §4 “Idioma y cultura”; arbol.md front “src/locales” y “src/shared/i18n”; rules/textos-y-traducciones.md; arnes.md front §2.
- [ ] Probar idioma efectivo, fallback y mismas claves/placeholders; npm test -- src/shared/i18n/i18n.test.tsx src/locales/parity.test.ts en rojo.
- [ ] Copiar/adaptar inicialización y textos del tablero aprobados; repetir en verde.
- [ ] Commit front: feat: incorporar traducciones y paridad

### Tarea 28. Soporte de tests HTTP y componentes

**Archivos:** crear ../ArquitecturaBaseMutitenantFront/src/test/mocks/{server,handlers}.ts ← homónimos de ../ArquitecturaBaseFront/src/test/mocks/ (sin /api/me ni auth E3); ../ArquitecturaBaseMutitenantFront/src/test/utils/renderWithProviders.tsx ← homónimo base (sin access ni host); modificar ../ArquitecturaBaseMutitenantFront/src/test/setup.ts; crear ../ArquitecturaBaseMutitenantFront/src/test/mocks/mocks.test.ts (nuevo).
**Respaldo:** plan maestro §Etapa 1, Front 1–2; arbol.md front “src/test”; rules/tests.md “Cómo se hace”.
- [ ] Probar MSW con onUnhandledRequest:error y render con i18n/Query, sin proveedor Auth; npm test -- src/test/mocks/mocks.test.ts en rojo.
- [ ] Copiar/adaptar soporte y setup; repetir en verde.
- [ ] Commit front: test: preparar MSW y render con proveedores

### Tarea 29. Contratos generados del backend

**Archivos:** crear ../ArquitecturaBaseMutitenantFront/scripts/{generate-contracts,check-contracts}.mjs y ../ArquitecturaBaseMutitenantFront/src/shared/api/generated/schema.d.ts (solo salida generada); crear ../ArquitecturaBaseMutitenantFront/src/shared/api/types.ts; modificar ../ArquitecturaBaseMutitenantFront/{package.json,package-lock.json,.github/workflows/ci.yml,vite.config.ts}; crear ../ArquitecturaBaseMutitenantFront/src/shared/api/{AGENTS,CLAUDE}.md y ../ArquitecturaBaseMutitenantFront/src/shared/api/contracts.test.ts, ../ArquitecturaBaseMutitenantFront/src/test/proxy-prefixes.test.ts (nuevos).
**Respaldo:** plan maestro §Etapa 1, Front 6 y puerta general 6; frontend.md §4 “Datos y API”; backend.md §19; arbol.md front “Raíz/scripts” y “src/shared/api”; rules/datos-y-api.md; arnes.md front §2; decisiones 2 y 15 de este plan. Depende de OpenAPI backend.
- [ ] Probar que `contracts:check` detecta schema desactualizado, es obligatorio en local y deja aviso y sale bien en CI solo si falta el repo hermano; generated contiene solo schema.d.ts y el proxy añade `/swagger` y `/openapi` solo en Development. npm test -- src/shared/api/contracts.test.ts src/test/proxy-prefixes.test.ts en rojo.
- [ ] Crear scripts, alias tipados, scripts npm y CI; agregar prefijos de Development al proxy; ejecutar npm run contracts && npm run contracts:check en local y pruebas focales hasta verde.
- [ ] Commit front: feat: generar tipos desde OpenAPI

### Tarea 30. Cliente HTTP y contratos de error

**Archivos:** crear ../ArquitecturaBaseMutitenantFront/src/shared/api/{httpClient.ts,httpClient.test.ts,ApiError.ts,problemDetails.ts,pagedResult.ts} ← homónimos de ../ArquitecturaBaseFront/src/shared/api/ (adaptar cultura, 401 y ProblemDetails).
**Respaldo:** plan maestro §Etapa 1, Front 1; frontend.md §4 “Datos y API” y “Errores”; arbol.md front “src/shared/api”; rules/datos-y-api.md y rules/errores.md; decisión 13 de este plan. La conexión real de Auth llega en E3.
- [ ] Probar Bearer opcional, Accept-Language efectivo, error tipado con `retryAfterSeconds` y PagedResult; npm test -- src/shared/api/httpClient.test.ts en rojo.
- [ ] Copiar/adaptar cliente y tipos sin flujo de sesión vencida E3; repetir en verde.
- [ ] Commit front: feat: centralizar cliente HTTP y errores

### Tarea 31. Errores globales y formularios

**Archivos:** crear ../ArquitecturaBaseMutitenantFront/src/shared/api/{queryClient.ts,formErrors.ts} ← homónimos de ../ArquitecturaBaseFront/src/shared/api/; queryClient.test.ts ← ../ArquitecturaBaseFront/src/shared/api/queryClient.test.tsx (adaptar la prueba sin JSX al nombre del árbol), formErrors.test.ts ← homónimo base; modificar ../ArquitecturaBaseMutitenantFront/src/app/providers.tsx.
**Respaldo:** plan maestro §Etapa 1, Front 1 y 9; frontend.md §4 “Errores”; arbol.md front “src/shared/api”; rules/errores.md y rules/formularios.md; decisión 13 de este plan. La sesión vencida se resuelve en E3.
- [ ] Probar silencio de errores 401/403/meta.silent sin flujo Auth, toast de red/5xx con traceId, 429 sin reintento y con `retryAfterSeconds`, y campos por code; npm test -- src/shared/api/queryClient.test.ts src/shared/api/formErrors.test.ts en rojo.
- [ ] Adaptar cliente Query y mapeo de campos; repetir en verde.
- [ ] Commit front: feat: resolver errores globales y de formulario

### Tarea 32. Mutación idempotente

**Archivos:** crear ../ArquitecturaBaseMutitenantFront/src/shared/api/{useIdempotentMutation.ts,useIdempotentMutation.test.ts} (nuevos).
**Respaldo:** plan maestro §Etapa 1, Front 7; arbol.md front “src/shared/api”; rules/datos-y-api.md y back rules/idempotencia.md.
- [ ] Probar clave estable ante reintento, clave nueva tras éxito y espera de Request.InProgress; npm test -- src/shared/api/useIdempotentMutation.test.ts en rojo.
- [ ] Implementar hook sobre TanStack Query y httpClient; repetir en verde.
- [ ] Commit front: feat: agregar mutación idempotente

### Tarea 33. Cliente y carga única de referencia en el front

**Archivos:** crear `../ArquitecturaBaseMutitenantFront/src/shared/referenceData/{referenceData.ts,useReferenceData.ts,referenceData.test.ts,AGENTS.md,CLAUDE.md}`; modificar `../ArquitecturaBaseMutitenantFront/src/app/providers.tsx` para cargar al arrancar; ampliar `../ArquitecturaBaseMutitenantFront/src/test/mocks/handlers.ts` con los contratos de referencia; no hay equivalentes en `../ArquitecturaBaseFront`.
**Respaldo:** datos-de-referencia.md §§4–5 y 8; front arbol.md “src/shared/referenceData”; front rules/datos-y-api.md y back `docs/rules/datos-de-referencia.md`; frontend.md §4 “Datos y API”; plan maestro §Etapa 1, Front 1, 3 y 5.
- [ ] Probar parseo tipado de cinco catálogos de `GET /api/reference-data`, ETag, cultura efectiva, `staleTime: Infinity`, una sola carga desde el proveedor, nombres de cultura traducidos/fallback y estado de carga; `npm test -- src/shared/referenceData/referenceData.test.ts` en rojo.
- [ ] Implementar cliente y hook con TanStack Query; ninguna lista de códigos en TS; repetir en verde.
- [ ] Commit front: feat: cargar datos de referencia una vez

### Tarea 34. Perfiles y formateadores del front

**Archivos:** crear ../ArquitecturaBaseMutitenantFront/src/shared/format/{cultureProfiles,formatters,statusTones}.ts y {formatters.test,format-usage.test,statusTones.test}.ts; ../ArquitecturaBaseMutitenantFront/src/shared/format/{AGENTS,CLAUDE}.md. `cultureProfiles.ts` adapta los perfiles recibidos desde `shared/referenceData`; no declara perfiles fijos.
**Respaldo:** plan maestro §Etapa 1, Front 3; formatos.md §§2–5; datos-de-referencia.md §§2, 4 y 7; arbol.md front “src/shared/format”; front rules/formatos.md y back `docs/rules/datos-de-referencia.md`; tema.md §Colores; arnes.md front §2; decisiones 1, 5, 6 y 16 de este plan.
- [ ] Probar **cada** caso compartido, incluidas monedas con 0/2/3 decimales y teléfonos nacionales/internacionales, estado→tono y prohibiciones de formato o catálogos fijos fuera de `shared/format`/JSON; `npm test -- src/shared/format/formatters.test.ts src/shared/format/statusTones.test.ts src/shared/format/format-usage.test.ts` en rojo. `formatters.test.ts` avisa y omite solo la paridad en CI sin repo hermano; en local su ausencia falla.
- [ ] Implementar todos los tipos de formatos.md con perfiles del catálogo, los tonos `success`, `warning`, `danger`, `neutral`, `pending`, sin usar defaults del navegador; ajustar el texto telefónico a los casos compartidos y repetir en verde.
- [ ] Commit front: feat: unificar formatos y tonos

### Tarea 35. Parsers y contexto de formato

**Archivos:** crear ../ArquitecturaBaseMutitenantFront/src/shared/format/{parsers.ts,parsers.test.ts,useFormat.ts,useFormat.test.tsx} (nuevos).
**Respaldo:** plan maestro §Etapa 1, Front 3; formatos.md §§1, 4–5; frontend.md §4 “Presentación de datos”; rules/formatos.md; decisión 5 de este plan.
- [ ] Probar parseDecimal, parseMoney, parsePercent, parseDate, estado de carga sin defaults literales, y luego `es-AR`/Buenos Aires/ARS tomados del catálogo más cultura habilitada de localStorage; npm test -- src/shared/format/parsers.test.ts src/shared/format/useFormat.test.tsx en rojo.
- [ ] Implementar parsers y proveedor E1 con `useReferenceData`; documentar punto de conexión a `/api/me` para E3; repetir en verde.
- [ ] Commit front: feat: interpretar entradas por cultura

### Tarea 36. Banderas e interpretación telefónica

**Archivos:** crear ../ArquitecturaBaseMutitenantFront/src/shared/phone/{countries.ts,CountryFlag.tsx,countries.test.ts,AGENTS.md,CLAUDE.md}; modificar ../ArquitecturaBaseMutitenantFront/{package.json,package-lock.json}. `countries.ts` ordena/filtra `Countries` del catálogo, no contiene lista fija ni `priorityCountries.ts`.
**Respaldo:** plan maestro §Etapa 1, Front 7; formatos.md §3; datos-de-referencia.md §§1 y 4; arbol.md front “src/shared/phone”; front rules/telefonos.md y back `docs/rules/datos-de-referencia.md`; arnes.md front §2.
- [ ] Probar que los países y CallingCode provienen de `shared/referenceData`, orden por `SortOrder`, filtrado y bandera SVG diferida; npm test -- src/shared/phone/countries.test.ts en rojo.
- [ ] Implementar con versiones exactas de `libphonenumber-js` y `country-flag-icons`, sin códigos de país en TS; repetir en verde.
- [ ] Commit front: feat: interpretar países y mostrar banderas

### Tarea 37. Selectores de datos de referencia

**Archivos:** crear ../ArquitecturaBaseMutitenantFront/src/shared/ui/fields/{CurrencySelect,CountrySelect,TimeZoneSelect,CultureSelect}.tsx y {CurrencySelect,CountrySelect,TimeZoneSelect,CultureSelect}.test.tsx; ampliar ../ArquitecturaBaseMutitenantFront/src/shared/ui/fields/{AGENTS,CLAUDE}.md. No hay selectores equivalentes basados en catálogos en ArquitecturaBaseFront.
**Respaldo:** plan maestro §Etapa 1, Front 5 y 7; formatos.md §§3–5; datos-de-referencia.md §§4 y 8; arbol.md front “src/shared/referenceData” y “src/shared/ui/fields”; front rules/formatos.md, front rules/datos-y-api.md y back `docs/rules/datos-de-referencia.md`; decisiones 3 y 4 de este plan.
- [ ] Probar opciones habilitadas/traducidas, búsqueda, `SortOrder`, carga, país con bandera/CallingCode y zona multipaís que conserve todos los `CountryCodes[]` junto con ciudad/offset actual `GMT−3`; npm test -- src/shared/ui/fields/CurrencySelect.test.tsx src/shared/ui/fields/CountrySelect.test.tsx src/shared/ui/fields/TimeZoneSelect.test.tsx src/shared/ui/fields/CultureSelect.test.tsx en rojo.
- [ ] Implementar los cuatro selectores desde `shared/referenceData`, sin listas fijas ni almacenamiento de offset; repetir en verde.
- [ ] Commit front: feat: ofrecer selectores de referencia

### Tarea 38. Componentes de fecha y números

**Archivos:** crear ../ArquitecturaBaseMutitenantFront/src/shared/ui/format/{DateText,DateRangeText,NumberText,MoneyText,PercentText}.tsx y {DateText,NumberText,MoneyText,PercentText}.test.tsx; {AGENTS,CLAUDE}.md.
**Respaldo:** plan maestro §Etapa 1, Front 5; formatos.md §§3–5; arbol.md front “src/shared/ui/format”; rules/formatos.md; arnes.md front §2. Depende de perfiles, formateadores, parsers y useFormat.
- [ ] Probar time dateTime ISO, vacío, tooltips, alineación numérica y aria-label de moneda; npm test -- src/shared/ui/format/DateText.test.tsx src/shared/ui/format/NumberText.test.tsx src/shared/ui/format/MoneyText.test.tsx src/shared/ui/format/PercentText.test.tsx en rojo.
- [ ] Crear componentes que solo llaman shared/format; repetir en verde.
- [ ] Commit front: feat: mostrar fechas números y dinero

### Tarea 39. Demás componentes de presentación

**Archivos:** crear ../ArquitecturaBaseMutitenantFront/src/shared/ui/format/{FileSizeText,DurationText,PhoneText,TaxIdText,TimeZoneText,CultureText,EnumText,StatusBadge,BooleanText,EmptyValue}.tsx y {PhoneText,TaxIdText,TimeZoneText,EnumText,StatusBadge,EmptyValue}.test.tsx.
**Respaldo:** plan maestro §Etapa 1, Front 5; formatos.md §§3–5; arbol.md front “src/shared/ui/format”; rules/formatos.md y rules/telefonos.md; tema.md §Colores; decisiones 1, 6, 12 y 16 de este plan.
- [ ] Probar E.164 oculto, sin ID IANA crudo, enum y badge traducidos con tono correcto, booleano, vacío accesible y formato fiscal acordado; npm test -- src/shared/ui/format/PhoneText.test.tsx src/shared/ui/format/TaxIdText.test.tsx src/shared/ui/format/TimeZoneText.test.tsx src/shared/ui/format/EnumText.test.tsx src/shared/ui/format/StatusBadge.test.tsx src/shared/ui/format/EmptyValue.test.tsx en rojo.
- [ ] Implementar componentes por delegación a shared/format y traducciones; repetir en verde.
- [ ] Commit front: feat: completar componentes de presentación

### Tarea 40. Campos de fecha y números

**Archivos:** crear ../ArquitecturaBaseMutitenantFront/src/shared/ui/fields/{DateField,DateTimeField,TimeField,NumberField,MoneyField,PercentField}.tsx y {DateField,DateTimeField,MoneyField,PercentField}.test.tsx; {AGENTS,CLAUDE}.md.
**Respaldo:** plan maestro §Etapa 1, Front 5; formatos.md §§4–5; arbol.md front “src/shared/ui/fields”; rules/formularios.md y rules/formatos.md; arnes.md front §2.
- [ ] Probar DateOnly sin zona, instante convertido desde zona efectiva, decimal cultural, Money con moneda y porcentaje fraccional; npm test -- src/shared/ui/fields/DateField.test.tsx src/shared/ui/fields/DateTimeField.test.tsx src/shared/ui/fields/MoneyField.test.tsx src/shared/ui/fields/PercentField.test.tsx en rojo.
- [ ] Crear campos que usan parsers y emiten contratos HTTP; repetir en verde.
- [ ] Commit front: feat: cargar fechas números y montos

### Tarea 41. Campos de correo, teléfono e identificación

**Archivos:** crear ../ArquitecturaBaseMutitenantFront/src/shared/ui/fields/{EmailField,PhoneField,TaxIdField}.tsx y {EmailField,PhoneField,TaxIdField}.test.tsx; PhoneField toma como referencia, sin copiar lista fija, ../ArquitecturaBaseFront/src/shared/ui/PhoneField.tsx y su test homónimo; modificar ../ArquitecturaBaseMutitenantFront/{package.json,package-lock.json} por `stdnum` con versión estable exacta.
**Respaldo:** plan maestro §Etapa 1, Front 7; formatos.md §§3–5; arbol.md front “src/shared/ui/fields”; rules/telefonos.md, rules/formularios.md; decisiones 7 y 12 de este plan.
- [ ] Probar correo normalizado, país al pegar número, AsYouType, contrato telefónico `{country,number}` y contrato fiscal `{type,number}` sin separadores (`type` = código del catálogo, como `AR-CUIT`), tipos habilitados del catálogo y validación numérica solo con `stdnum`; npm test -- src/shared/ui/fields/EmailField.test.tsx src/shared/ui/fields/PhoneField.test.tsx src/shared/ui/fields/TaxIdField.test.tsx en rojo.
- [ ] Crear campos manteniendo accesibilidad del PhoneField base; repetir en verde.
- [ ] Commit front: feat: cargar correo teléfono e identificación

### Tarea 42. Hooks de paginación, cursor y búsqueda

**Archivos:** modificar ../ArquitecturaBaseMutitenantFront/src/shared/hooks/{usePagination.ts,usePagination.test.tsx}; crear {useCursorList.ts,useCursorList.test.tsx,useDebouncedValue.ts,useDebouncedValue.test.tsx}.
**Respaldo:** plan maestro §Etapa 1, Front 4 y 9; frontend.md §4 “Paginado” y “Errores”; arbol.md front “src/shared/hooks”; rules/paginado-y-listados.md y rules/formularios.md; decisión 14 de este plan.
- [ ] Probar URL y reset, `usePagination(result?: { items; totalCount })`, salto interno con `replace` cuando llegan `items` vacíos y `totalCount > 0`, sin `correctPage` público ni historial nuevo; cursor sin total y debounce 300 ms. npm test -- src/shared/hooks/usePagination.test.tsx src/shared/hooks/useCursorList.test.tsx src/shared/hooks/useDebouncedValue.test.tsx en rojo.
- [ ] Adaptar hook existente (o copiar la solución equivalente de ArquitecturaBaseFront) y agregar los otros; repetir en verde.
- [ ] Commit front: feat: completar hooks de listados y cambios pendientes

### Tarea 43. Controles de paginación

**Archivos:** crear ../ArquitecturaBaseMutitenantFront/src/shared/ui/{Pagination.tsx,Pagination.test.tsx} ← homónimos de ../ArquitecturaBaseFront/src/shared/ui/; crear LoadMore.tsx y LoadMore.test.tsx (nuevos).
**Respaldo:** plan maestro §Etapa 1, Front 4; frontend.md §4 “Paginado”; arbol.md front “src/shared/ui”; rules/paginado-y-listados.md y rules/pantallas-y-ui.md.
- [ ] Probar 10/20/50/100, “1–10 de 1.234”, página y Cargar más sin total; npm test -- src/shared/ui/Pagination.test.tsx src/shared/ui/LoadMore.test.tsx en rojo.
- [ ] Adaptar Pagination al formato y tokens; crear LoadMore; repetir en verde.
- [ ] Commit front: feat: mostrar paginado y carga por cursor

### Tarea 44. Piezas genéricas de formulario y aviso

**Archivos:** crear ../ArquitecturaBaseMutitenantFront/src/shared/ui/{Banner,CheckboxField,ConfirmDialog,EmptyState,FormField,IconButton,MultiSelect,RadioGroupField}.tsx ← homónimos de ../ArquitecturaBaseFront/src/shared/ui/; crear/adaptar tests homónimos .test.tsx desde la misma carpeta base; crear ../ArquitecturaBaseMutitenantFront/src/shared/hooks/{useUnsavedChangesGuard.ts,useUnsavedChangesGuard.test.tsx} ← homónimos de ../ArquitecturaBaseFront/src/shared/hooks/.
**Respaldo:** plan maestro §Etapa 1, Front 8–9; frontend.md §4 “UI y pantallas”; arbol.md front “src/shared/ui”; rules/formularios.md, rules/pantallas-y-ui.md, rules/accesibilidad.md; tema.md §§Colores, Forma.
- [ ] Adaptar tests de textos traducidos, foco, labels, tokens y bloqueo de salida; npm test -- src/shared/ui/Banner.test.tsx src/shared/ui/ConfirmDialog.test.tsx src/shared/ui/FormField.test.tsx src/shared/hooks/useUnsavedChangesGuard.test.tsx en rojo.
- [ ] Copiar/adaptar ocho componentes y el guard con ConfirmDialog, con todos los --color-* sustituidos; repetir las pruebas en verde.
- [ ] Commit front: feat: agregar controles genéricos traducidos

### Tarea 45. Página, acciones e iconos

**Archivos:** crear ../ArquitecturaBaseMutitenantFront/src/shared/ui/{Page,RowActions,SearchInput,SegmentedControl,Spinner,VerificationBadge,icons}.tsx ← homónimos de ../ArquitecturaBaseFront/src/shared/ui/; crear/adaptar {Page,RowActions,SegmentedControl,VerificationBadge,icons}.test.tsx desde sus homónimos base; crear SearchInput.test.tsx y Spinner.test.tsx (nuevos).
**Respaldo:** plan maestro §Etapa 1, Front 8; frontend.md §4 “UI y pantallas”; arbol.md front “src/shared/ui”; rules/pantallas-y-ui.md y rules/accesibilidad.md; tema.md §§Forma, Colores.
- [ ] Probar banda Page con resumen/acciones, menú ⋮ con destructivas al final, búsqueda accesible y trazo SVG; npm test -- src/shared/ui/Page.test.tsx src/shared/ui/RowActions.test.tsx src/shared/ui/SearchInput.test.tsx src/shared/ui/icons.test.tsx en rojo.
- [ ] Copiar/adaptar las siete piezas y tokens; repetir pruebas de la carpeta en verde.
- [ ] Commit front: feat: agregar página acciones e iconos

### Tarea 46. Tabla tipada y adaptable

**Archivos:** crear ../ArquitecturaBaseMutitenantFront/src/shared/ui/{DataTable.tsx,DataTable.test.tsx} ← homónimos de ../ArquitecturaBaseFront/src/shared/ui/ (reescribir columnas type/mobile); crear ../ArquitecturaBaseMutitenantFront/src/shared/ui/columns-mobile.test.ts.
**Respaldo:** plan maestro §Etapa 1, Front 5 y 8; formatos.md §4; frontend.md §4 “Paginado” y “UI y pantallas”; arbol.md front “src/shared/ui”; rules/responsive.md y rules/paginado-y-listados.md.
- [ ] Probar type→ui/format, carga/vacío/error, aria-sort, 390 px solo primary/status/⋮, 768 sin low y exactamente una primary; npm test -- src/shared/ui/DataTable.test.tsx src/shared/ui/columns-mobile.test.ts en rojo.
- [ ] Adaptar DataTable sin formateo manual ni dos datos en una celda; repetir en verde.
- [ ] Commit front: feat: agregar tabla tipada y adaptable

### Tarea 47. Filtros, diálogo y hoja móvil

**Archivos:** modificar ../ArquitecturaBaseMutitenantFront/src/shared/ui/{FilterBar.tsx,dialog.tsx}; crear ../ArquitecturaBaseMutitenantFront/src/shared/ui/{Sheet.tsx,Sheet.test.tsx,FilterBar.test.tsx,dialog.mobile.test.tsx}.
**Respaldo:** plan maestro §Etapa 1, Front 8; frontend.md §4 “UI y pantallas”; arbol.md front “src/shared/ui”; rules/responsive.md; tema.md §Forma y tableros aprobados “Botones”/“Avisos”.
- [ ] Probar filtros bajo buscador a 390, diálogo como hoja desde abajo y 44×44; npm test -- src/shared/ui/FilterBar.test.tsx src/shared/ui/dialog.mobile.test.tsx src/shared/ui/Sheet.test.tsx en rojo.
- [ ] Adaptar primitivas E0 y crear Sheet; repetir en verde.
- [ ] Commit front: feat: adaptar filtros y diálogos al teléfono

### Tarea 48. Avisos transversales y AppShell

**Archivos:** crear ../ArquitecturaBaseMutitenantFront/src/layouts/{AppShell.tsx,AppShell.test.tsx,AGENTS.md,CLAUDE.md}; ../ArquitecturaBaseMutitenantFront/src/layouts/components/{OfflineBanner,NewVersionBanner}.tsx; ../ArquitecturaBaseMutitenantFront/src/shared/ui/{ConcurrencyBanner.tsx,ConcurrencyBanner.test.tsx}; crear ../ArquitecturaBaseMutitenantFront/src/shared/api/{useRetryAfterCountdown.ts,useRetryAfterCountdown.test.tsx} (usa `shared/hooks/useCountdown.ts` de E0); modificar ../ArquitecturaBaseMutitenantFront/src/app/{App.tsx,providers.tsx}; crear ../ArquitecturaBaseMutitenantFront/src/layouts/components/{OfflineBanner,NewVersionBanner}.test.tsx.
**Respaldo:** plan maestro §Etapa 1, Front 9; frontend.md §4 “Errores”; arbol.md front “src/layouts” y “src/shared/ui”; rules/errores.md, rules/formularios.md; arnes.md front §2; tablero aprobado “Avisos”; decisión 13 de este plan.
- [ ] Probar online/offline, fallo de chunk→franja sin recarga sola, 409 con dos acciones sin perder formulario y 429 con cuenta regresiva en el botón iniciador (sin reintento automático); npm test -- src/layouts/AppShell.test.tsx src/layouts/components/OfflineBanner.test.tsx src/layouts/components/NewVersionBanner.test.tsx src/shared/ui/ConcurrencyBanner.test.tsx src/shared/api/useRetryAfterCountdown.test.tsx en rojo.
- [ ] Crear avisos y composición, conectar solo las partes E1; sesión vencida corresponde a E3; repetir en verde.
- [ ] Commit front: feat: mostrar avisos transversales

### Tarea 49. Cierre del arnés backend

**Archivos:** modificar tests/ArquitecturaBaseMultitenant.ArchitectureTests/HarnessStage.cs; ajustar solo punteros/fichas back de carpetas E1 creadas en las tareas anteriores si un nombre definitivo cambió. No crear funcionalidad nueva.
**Respaldo:** plan maestro “Reglas para todas las etapas” 1–8; arnes.md back §5–6; arbol.md back, entradas [E1].
- [ ] Cambiar HarnessStage.Closed a 1 y ejecutar R/HarnessTests: rojo si queda una referencia E1 incumplida.
- [ ] Completar enlaces y tests faltantes; ejecutar R/HarnessTests en verde y puerta back.
- [ ] Commit back: chore: cerrar arnés de Etapa 1

### Tarea 50. Cierre del arnés frontend

**Archivos:** modificar ../ArquitecturaBaseMutitenantFront/src/test/HarnessStage.ts; ajustar solo punteros/fichas front de carpetas E1 creadas en tareas anteriores si cambió un nombre definitivo. No crear funcionalidad nueva.
**Respaldo:** plan maestro “Reglas para todas las etapas” 1–8 y “Etapa 1, Puerta”; arnes.md front §4; arbol.md front, entradas [E1].
- [ ] Cambiar HarnessStage a 1 y ejecutar npm test -- src/test/harness.test.ts: rojo si queda una referencia E1 incumplida.
- [ ] Completar enlaces y tests faltantes; ejecutar npm test -- src/test/harness.test.ts en verde y la puerta completa de abajo.
- [ ] Commit front: chore: cerrar arnés de Etapa 1

## Puerta completa de Etapa 1

1. Back: `dotnet build ArquitecturaBaseMultitenant.slnx` sin advertencias; `dotnet test` verde, incluidas pruebas de Result, validación, resources, localización, cada ErrorType con status y detail correctos en es-AR/en-US, JSON UTC/Money, tiempo, textos, OpenAPI, hosting, rutas explícitas, `ReferenceDataCatalogTests`, `ReferenceDataHardcodeTests` y las demás guardas de arquitectura.
2. Referencias: ejecutar `npm ci --prefix scripts/datos-de-referencia` y `node --test scripts/datos-de-referencia/generar.test.mjs` (fixtures sin red); regenerar desde snapshots/hashes y `habilitados.json`/`cultures.source.json`/`tax-id-types.source.json` fijados y comparar los cinco JSON byte a byte. Verificar versiones y URL de ISO 4217/CLDR/IANA/libphonenumber, incluido `cldr-region-validity.xml` de `release-48-2` con SHA-256 en el lock; comprobar 249 países ISO y que `AN`, `AA` y `XK` estén excluidos. Verificar nombres traducidos de culturas, FK internas no nulas, zonas multipaís con todos sus `CountryCodes[]` y `Countries.DefaultTimeZoneId` por pertenencia, vigencia/habilitación y JSON embebidos en el build. La habilitación inicial incluye zonas cuyo `CountryCodes[]` contiene algún país habilitado más UTC (`CountryCodes=[]`) y tipos fiscales de países habilitados, con overrides y `SortOrder` en `habilitados.json`; `ICurrencyCatalog` cubre existencia y unidades menores 0/2/3. E1 usa `JsonReferenceDataCatalog` sin tablas ni seed de base; `TimeZoneCountries` nace en E2.
3. API de referencias: `GET /api/reference-data` y cada ruta por catálogo son `[AllowAnonymous]`, devuelven solo habilitados y traducidos, con búsqueda, `ETag` y caché. `ExplicitRouteInventoryTests` cubre las seis rutas y todas las demás; la lista de `AccessDeclarationTests` queda anotada para E3.
4. Front: `npm run build`, `npm run lint` y `npm test` limpios; pruebas de i18n/paridad, `shared/referenceData`, selectores con carga, formatos, campos, paginado, tabla a 390/768/1440 y estados del tablero Avisos. `format-usage.test.ts` impide catálogos fijos y formato manual fuera de la carpeta autorizada.
5. Contratos: `docs/contracts/openapi.json` generado y commiteado; `npm run contracts` y `npm run contracts:check` reproducibles, `schema.d.ts` generado y commiteado. En local `contracts:check` y la prueba de formatos compartidos son obligatorios; en el CI del front se saltan con aviso si falta el repo hermano y en CI del back se verifica el OpenAPI.
6. Formatos: **para cada caso** de `docs/contracts/format-cases.json`, `DisplayFormatterTests` en back y `formatters.test.ts` en front producen **exactamente el mismo texto esperado** con la misma cultura y zona. El archivo cubre todos los tipos de formatos.md, `es-AR` y `en-US`, ARS/USD con `DisplaySymbol` cultural desde CLDR, monedas de 0/2/3 decimales y teléfono nacional/internacional. Los dos tests leen el mismo archivo del back y los perfiles del catálogo.
7. Arnés: todas las carpetas E1 del mapa tienen AGENTS.md + CLAUDE.md, las fichas enlazan archivos y tests reales, HarnessTests y harness.test.ts verdes; HarnessStage.Closed del back y HarnessStage del front pasan a **1** solo al cerrar la etapa.
8. Si se levantó Aspire para una comprobación, ejecutar `aspire stop`. Los repos quedan en main, con commits locales pequeños, sin push.
