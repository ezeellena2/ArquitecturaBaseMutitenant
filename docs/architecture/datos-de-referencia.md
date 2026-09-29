# Datos de referencia: monedas, países, zonas horarias, culturas e identificación fiscal

> Documento canónico de los datos **transversales** que usa todo el sistema (ADR 0036). Complementa a [`backend.md`](backend.md) §11, §12 y §18 y a `formatos.md` del front. **Regla de oro: nada de esto se escribe a mano en el código.** Son datos que salen de estándares oficiales, viven en tablas con sus traducciones y se cargan con un seed idempotente. Sumar una moneda, un país, una zona o un idioma es cambiar datos, nunca código.

## 1. Qué es dato de referencia y qué no

| Tema | Dónde vive | Fuente oficial |
|---|---|---|
| Monedas | tabla `platform.Currencies` + `CurrencyTranslations` | ISO 4217 (lista vigente) |
| Países | tabla `platform.Countries` + `CountryTranslations` | ISO 3166-1 |
| Zonas horarias | tablas `platform.TimeZones` + `TimeZoneCountries` + `TimeZoneTranslations` | IANA tzdb (zonas canónicas y países asociados) + CLDR (nombres de ciudad) |
| Culturas (idioma + región) y su formato | tabla `platform.Cultures` + `CultureTranslations` | CLDR, con los ajustes del producto en la propia fila |
| Tipos de identificación fiscal | tabla `platform.TaxIdTypes` + `TaxIdTypeTranslations` | normativa de cada país (AFIP para Argentina) |
| Textos de la interfaz | `.resx` (back) y `src/locales/<idioma>/*.json` (front) | — (es la práctica estándar: no van a la base) |
| Reglas de teléfono | `libphonenumber` (back: `libphonenumber-csharp`; front: `libphonenumber-js`) | los metadatos de Google, que se actualizan con la librería |
| Offsets y horario de verano | ICU / `TimeZoneInfo` (back) e `Intl` (front) | tzdb, que se actualiza con el sistema |

**Lo que nunca se guarda:** el offset de una zona (cambia con el horario de verano, se calcula al mostrar), los formatos armados ("27/09/2026") y los tipos de cambio (la plantilla no convierte monedas).

## 2. Las tablas (esquema `platform`, globales, sin RLS)

Los cinco catálogos principales tienen `IsEnabled` (qué se ofrece en los selectores) y `SortOrder` opcional donde corresponde (para destacar las más usadas). Las traducciones tienen la clave `(código, cultura de visualización)` y caen a la cultura por defecto si falta una; `CultureTranslations` llama `DisplayCulture` a esa segunda columna para distinguirla del código de cultura traducido. Un territorio ISO puede carecer de prefijo telefónico, moneda o zona propia: esos tres campos son anulables y no se sustituyen por valores supuestos. Los registros sin los datos necesarios para seleccionarse en una entrada nueva quedan `IsEnabled = false`; las FK se verifican solo cuando su valor no es `null`.

| Tabla | Columnas |
|---|---|
| `Currencies` | `Code` (`ARS`, PK, char(3)), `NumericCode` (`032`), `MinorUnits` (ARS 2, CLP 0, BHD 3), `Symbol` (`$`), `IsEnabled`, `SortOrder` |
| `CurrencyTranslations` | `CurrencyCode`, `Culture`, `Name` ("Peso argentino"), `NamePlural`, `DisplaySymbol` (símbolo para esa cultura, derivado de CLDR; ARS: `$` en es-AR, `ARS` en en-US) |
| `Countries` | `Code` (`AR`, PK, alfa-2), `Alpha3` (`ARG`), `NumericCode`, `CallingCode?` (`54`), `DefaultCurrencyCode?` (FK), `DefaultTimeZoneId?` (FK), `IsEnabled`, `SortOrder` |
| `CountryTranslations` | `CountryCode`, `Culture`, `Name` ("Argentina") |
| `TimeZones` | `Id` (`America/Argentina/Buenos_Aires`, PK), `IsEnabled`, `SortOrder` |
| `TimeZoneCountries` | `TimeZoneId` (FK), `CountryCode` (FK), `IsEnabled`; clave compuesta `(TimeZoneId, CountryCode)` para conservar todos los países de una fila IANA |
| `TimeZoneTranslations` | `TimeZoneId`, `Culture`, `City` ("Buenos Aires") |
| `Cultures` | `Code` (`es-AR`, PK), `LanguageCode` (`es`), `CountryCode` (FK), `DatePattern` (`dd/MM/yyyy`), `TimePattern` (`HH:mm`), `DateTimePattern`, `LongDatePattern`, `DecimalSeparator`, `GroupSeparator`, `CurrencyPattern` (`$ n` / `-$ n`), `PercentPattern`, `FallbackCulture`, `IsEnabled`, `IsDefault`, `SortOrder?` |
| `CultureTranslations` | `CultureCode`, `DisplayCulture`, `Name` (por ejemplo, "Español (Argentina)" en es y "Spanish (Argentina)" en en); clave `(CultureCode, DisplayCulture)` |
| `TaxIdTypes` | `Code` (`AR-CUIT`, PK), `CountryCode` (FK), `Label`, `Mask` (`99-99999999-9`), `ValidatorKey` (`ar-cuit-mod11`), `AppliesTo` (`Person` \| `Company` \| `Both`), `IsEnabled`, `SortOrder?` |
| `TaxIdTypeTranslations` | `TaxIdTypeCode`, `Culture`, `Name` ("CUIT") |

- **Las demás tablas apuntan con FK**: `Money.Currency` → `Currencies.Code`; `TenantSettings.DefaultCulture` / `DefaultTimeZoneId` / `DefaultCurrency`, `Companies.TimeZoneId` y `Companies.TaxIdType` → sus tablas; `ApplicationUser.Culture` y `TimeZoneId` también.
- **Los algoritmos sí son código:** el dígito verificador del CUIT es una clase (`ArgentineCuitValidator`) registrada con su `ValidatorKey`. La tabla dice qué tipos existen y cuál valida cada uno. Un país nuevo suma su fila y, si hace falta, su validador.
- **Los patrones de formato de `Cultures`** son la única fuente de `DisplayFormatter` (back) y de `shared/format` (front). Reemplazan a los perfiles escritos a mano: son CLDR con los ajustes del producto (por ejemplo, 24 h en `es-AR`).
- **El símbolo visible de una moneda depende de la cultura:** `CurrencyTranslations.DisplaySymbol` acompaña al patrón de `Cultures`. El `Symbol` global de `Currencies` no sustituye ese dato. `format-cases.json` fija los textos exactos de ARS y USD en es-AR/en-US para ambos formateadores.
- **Espaciado monetario:** al aplicar `CurrencyPattern`, un `DisplaySymbol` alfabético contiguo al número recibe un espacio, según la regla de espaciado de CLDR; un símbolo gráfico como `$` conserva el patrón sin ese espacio. Así `en-US` muestra `ARS 1,234.50` y `$1,234.50` con el mismo perfil, sin una excepción por código en el formateador.

## 3. De dónde salen los datos (seed)

- **Cinco salidas generadas**, versionadas en `src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/Seed/ReferenceData/`: `currencies.json`, `countries.json`, `time-zones.json`, `cultures.json` y `tax-id-types.json`, con las traducciones adentro. Cada zona en `time-zones.json` lleva `CountryCodes[]` con todos los códigos de la fila IANA, sin perder asociaciones compartidas. Sus entradas editables viven en `scripts/datos-de-referencia/`, junto al generador.
- **Se generan con un script** (`scripts/datos-de-referencia/generar.mjs`) a partir de fuentes fijadas: el snapshot de SIX ISO 4217 List One (`sources/iso4217-list-one.xml`), la validez de regiones de CLDR 48.2 (`sources/cldr-region-validity.xml`, tomado de `release-48-2/common/validity/region.xml`), `codeMappings`, `currencyData` y `primaryZones` de `cldr-core` 48.2, nombres y símbolos por cultura de CLDR, alias horarios de `cldr-bcp47` 48.2, prefijos de la versión fijada de libphonenumber y el snapshot IANA `sources/iana-zone1970.tab`. `sources.lock.json` registra URL, versión y SHA-256 de los tres snapshots. `package-lock.json` fija CLDR, libphonenumber y el parser XML; `.node-version` fija Node para ejecutar y probar el generador en local y CI. Los nombres de culturas salen de `cldr-localenames-full` (`languages.json`, `territories.json` y el patrón de locale), sin consultar `Intl.DisplayNames`. La generación normal lee las fuentes locales y verifica hashes, sin red; solo `--refresh` actualiza snapshots, lock y JSON desde los orígenes. Para `DefaultTimeZoneId`, un país con una sola zona recibe esa zona; con varias, recibe la principal de CLDR normalizada a IANA, o `null` si no existe. `habilitados.json` permite una zona predeterminada explícita. La ciudad se toma de `exemplarCity` de CLDR buscando el ID IANA y sus alias históricos; después puede prevalecer `ciudades.<idioma>.json` (por ejemplo, New_York → Nueva York), y el último tramo del ID es la reserva final. Los patrones de cultura y los tipos fiscales son decisiones del producto escritas en `cultures.source.json` y `tax-id-types.source.json`; `habilitados.json` declara habilitación y `SortOrder` de los catálogos. Los overrides pueden estar vacíos inicialmente porque se habilitan de forma derivada los países/monedas predeterminados, las culturas soportadas, sus zonas y tipos fiscales (regla siguiente). **Nunca se edita un JSON generado:** se regenera.
- **Países ISO 3166-1:** expandir los rangos del `id type="region" idStatus="regular"` de ese XML y cruzarlos con `codeMappings`: exigir `_alpha3` de tres letras y `_numeric` de tres dígitos menor que 900. El estado `regular` solo no alcanza porque CLDR incluye `XK`; `codeMappings` solo tampoco alcanza porque conserva `AN` y `AA`. Con las fuentes 48.2 fijadas, el cruce produce 249 códigos; `AN`, `AA` y `XK` quedan fuera. La cifra es una prueba de regresión para esa versión, no una lista manual de países.
- **Moneda nueva sin traducción CLDR:** la lista vigente de SIX también incluye fondos y puede adelantarse a CLDR. Se conserva el código con unidades menores numéricas; si falta su traducción para una cultura habilitada, se usa el nombre oficial de SIX y el código como símbolo de reserva, queda `IsEnabled = false` y un override no puede habilitarlo hasta completar las traducciones. Las filas con unidades `N.A.` no reciben decimales supuestos.
- **Reproducibilidad:** `generar.test.mjs` usa fixtures locales sin red, rechaza hashes incorrectos y compara byte a byte los cinco JSON al regenerar dos veces con las mismas fuentes fijadas. `.gitattributes` conserva los bytes originales de los snapshots (SIX publica XML con CRLF) y mantiene LF en los JSON generados, para que hashes y comparación sigan válidos tras un checkout en Windows. Para incorporar una versión nueva de SIX, CLDR, IANA o libphonenumber se ejecuta el refresh explícito, se revisa el diff de snapshots, lock y JSON y se corren esos tests. La versión de Node también se actualiza explícitamente, no por el ambiente del desarrollador.
- **`ReferenceDataSeeder`** (E2) hace un upsert por clave y nunca borra: una moneda que sale de ISO pasa a `IsEnabled = false`, y una asociación zona-país que deja de figurar en IANA queda deshabilitada en `TimeZoneCountries`. En E2 lo invoca `DatabaseBootstrapExtensions` en Development y en los tests; desde E3 `SeedExtensions` lo integra al seed idempotente de todos los ambientes (ADR 0006). Qué se habilita por defecto lo dice el JSON generado: país y moneda de la cultura por defecto, culturas soportadas, zonas cuya fila IANA incluya un país habilitado más `UTC`, y tipos fiscales del país habilitado. `habilitados.json` permite overrides de esas selecciones y de `SortOrder`; no hay listas de códigos en C#.
- El generador conserva los territorios ISO aunque falte un dato oficial: deja `CallingCode`, `DefaultCurrencyCode` o `DefaultTimeZoneId` en `null` y marca `IsEnabled = false` si el registro no alcanza para el selector. `ReferenceDataCatalogTests` valida relaciones cuando hay código y no acepta códigos inventados para llenar huecos.
- `UTC` entra como zona IANA sin país (`CountryCodes: []`): `zone1970.tab` lista zonas vinculadas a territorios y no trae esa zona global. El test del generador comprueba su presencia explícita, sin asignarle un país supuesto. `ReferenceDataCatalogTests` verifica que cada `CountryCodes[]` apunte a países del catálogo y que una zona compartida conserve todos los códigos.

## 4. Cómo se usan

- **Back:** un puerto por catálogo en `Application/Interfaces/ReferenceData/` (`ICurrencyCatalog`, `ICountryCatalog`, `ITimeZoneCatalog`, `ICultureCatalog`, `ITaxIdTypeCatalog`). En E1 los implementa `JsonReferenceDataCatalog` sobre los JSON versionados; en E2 los lee `ReferenceDataReader` de las tablas, con HybridCache `p:ref:<catálogo>`, que se invalida cuando el seed cambia algo.
  - **Límite entre capas:** `Domain/ValueObjects/CurrencyCode.Create(code)` valida solo la sintaxis alfa-3 y no referencia a Application. La existencia, vigencia y `IsEnabled` del código para un dato nuevo se verifican en Application mediante `ICurrencyCatalog` y `ValidCurrency()`. `Money.Round(minorUnits)` recibe desde Application los `MinorUnits` del catálogo; Domain no consulta catálogos ni infraestructura. Un código ya guardado se puede seguir leyendo y mostrando aunque luego quede deshabilitado.
  - `ValidationRules` suma `ValidCurrency()`, `ValidCountry()`, `ValidTimeZone()`, `ValidCulture()` y `ValidTaxIdType()`, siempre contra los catálogos: un código deshabilitado es inválido para un dato nuevo, y uno ya guardado se sigue mostrando.
- **API:** `GET /api/reference-data` reemplaza `GET /api/time-zones`: es anónimo (`[AllowAnonymous]`, en la lista de `AccessDeclarationTests`) y devuelve **todas** las filas de los cinco catálogos, traducidas a la cultura del pedido y con `isEnabled`, `ETag` y caché del navegador. Las rutas por catálogo (`/api/reference-data/currencies`…) también incluyen filas deshabilitadas en la búsqueda, para recuperar metadatos de valores ya guardados. El catálogo horario entrega ID IANA, `CountryCodes[]` y ciudad traducida; el offset se calcula al mostrar con el reloj actual, no se almacena.
- **Front:** `shared/referenceData` carga todas las filas una vez al arrancar (TanStack Query, `staleTime: Infinity`) y las pasa a `useFormat` y a los campos (`CurrencySelect`, `CountrySelect`, `TimeZoneSelect`, `CultureSelect`, `TaxIdField`). Los selectores filtran `isEnabled = true` para un dato nuevo; los formateadores consultan cualquier fila para mostrar un dato ya guardado. Mientras no llegan, los campos muestran su estado de carga. Nada de listas escritas en el front.
- **Mostrar una zona:** "Buenos Aires (GMT−3)". La ciudad sale de la tabla y el offset se calcula al mostrar, con el signo menos tipográfico. Los selectores ordenan por `SortOrder`, después por offset y después por ciudad.
- **Mostrar un teléfono:** en formato nacional si su país es el de la cultura (`AR` para `es-AR`) y en internacional si no. Los países del selector salen de `Countries` y su prefijo de `CallingCode`, validado con `libphonenumber`.

## 5. Traducciones de la interfaz (no son datos de referencia)

- **Textos del back:** `.resx` por área (`Errors`, `Validation`, `Permissions`, `Notifications`, `Audit`…) con su `.en.resx`. **Textos del front:** `src/locales/<idioma>/<namespace>.json` con i18next.
- **Se habilitan las culturas de la tabla `Cultures`**: el back arma `RequestLocalizationOptions` desde `ICultureCatalog`, y el front toma la lista de `GET /api/reference-data`.
- **Paridad obligatoria:** `ResourceParityTests` y `parity.test.ts` exigen las mismas claves en todos los idiomas habilitados.
- **Cadena de caída:** cultura pedida → su `FallbackCulture` → la cultura por defecto.

## 6. Sumar un idioma, un país o una moneda

- **Una moneda o un país:** actualizar las fuentes fijadas con `--refresh` si es nuevo en ISO, o modificar `scripts/datos-de-referencia/habilitados.json`; después regenerar los JSON. Ningún cambio de código de negocio ni edición manual de un JSON generado.
- **Una zona horaria:** regenerar; si su ciudad necesita traducción, sumarla en `ciudades.<idioma>.json`.
- **Un tipo de identificación fiscal:** su entrada en `scripts/datos-de-referencia/tax-id-types.source.json` y, si trae un algoritmo nuevo, su validador registrado con el `ValidatorKey`; regenerar `tax-id-types.json`.
- **Un idioma o una cultura:**
  1. su entrada en `scripts/datos-de-referencia/cultures.source.json`, con sus patrones; regenerar `cultures.json`;
  2. los `.resx` y la carpeta `src/locales/<idioma>`, con las mismas claves (lo exigen los tests de paridad);
  3. los nombres traducidos: regenerar los catálogos, que traen la columna de ese idioma;
  4. sus casos en `format-cases.json`;
  5. el texto legal en `LegalDocumentContents`;
  6. las plantillas de correo y de WhatsApp en ese idioma (`whatsapp-plantillas.md`).

## 7. `format-cases.json`

`docs/contracts/format-cases.json` es el contrato común de formato: `{ "now": "2026-09-27T15:00:00Z", "cases": [{ "id", "type", "culture", "timeZone", "input", "expected" | "error" }] }`, con `now` fijo para los tiempos relativos. Cada caso tiene exactamente uno de `expected` (texto) o `error` (código de validación esperado); los casos con `error` deben ser rechazados por ambos lados. Tiene al menos un caso válido por tipo de dato y por cultura habilitada, con monedas de 0, 2 y 3 decimales y teléfonos nacionales e internacionales. Lo recorren `DisplayFormatterTests` (back) y `formatters.test.ts` (front), y los dos tienen que dar exactamente el mismo texto en los casos válidos. Los patrones salen de los mismos datos de `cultures.json`.

## 8. Etapas

- **E1:** los puertos de Application, los JSON fuente con su script, un adaptador que lee los JSON (`JsonReferenceDataCatalog`), las validaciones de entradas nuevas en Application, `GET /api/reference-data` y sus rutas por catálogo con todas las filas e `isEnabled`, `format-cases.json` y el front consumiéndolos. `CurrencyCode` solo valida sintaxis y `Money.Round(minorUnits)` recibe los decimales; todo funciona sin base.
- **E2:** las tablas, sus configuraciones EF y su migración, `ReferenceDataSeeder` y `ReferenceDataReader` con HybridCache, que reemplaza al adaptador de JSON y conserva la lectura de todas las filas para la API. Los tests de la E1 siguen en verde sin cambios.
- **Desde la E3:** las FK de `ApplicationUser`, `TenantSettings` y `Companies` apuntan a estas tablas.
