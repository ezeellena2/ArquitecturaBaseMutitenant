# Datos de referencia: monedas, países, zonas horarias, culturas e identificación fiscal

> Documento canónico de los datos **transversales** que usa todo el sistema (ADR 0036). Complementa a [`backend.md`](backend.md) §11, §12 y §18 y a `formatos.md` del front. **Regla de oro: nada de esto se escribe a mano en el código.** Son datos que salen de estándares oficiales, viven en tablas con sus traducciones y se cargan con un seed idempotente. Sumar una moneda, un país, una zona o un idioma es cambiar datos, nunca código.

## 1. Qué es dato de referencia y qué no

| Tema | Dónde vive | Fuente oficial |
|---|---|---|
| Monedas | tabla `platform.Currencies` + `CurrencyTranslations` | ISO 4217 (lista vigente) |
| Países | tabla `platform.Countries` + `CountryTranslations` | ISO 3166-1 |
| Zonas horarias | tabla `platform.TimeZones` + `TimeZoneTranslations` | IANA tzdb (zonas canónicas) + CLDR (nombres de ciudad) |
| Culturas (idioma + región) y su formato | tabla `platform.Cultures` | CLDR, con los ajustes del producto en la propia fila |
| Tipos de identificación fiscal | tabla `platform.TaxIdTypes` + `TaxIdTypeTranslations` | normativa de cada país (AFIP para Argentina) |
| Textos de la interfaz | `.resx` (back) y `src/locales/<idioma>/*.json` (front) | — (es la práctica estándar: no van a la base) |
| Reglas de teléfono | `libphonenumber` (back: `libphonenumber-csharp`; front: `libphonenumber-js`) | los metadatos de Google, que se actualizan con la librería |
| Offsets y horario de verano | ICU / `TimeZoneInfo` (back) e `Intl` (front) | tzdb, que se actualiza con el sistema |

**Lo que nunca se guarda:** el offset de una zona (cambia con el horario de verano, se calcula al mostrar), los formatos armados ("27/09/2026") y los tipos de cambio (la plantilla no convierte monedas).

## 2. Las tablas (esquema `platform`, globales, sin RLS)

Todas tienen `IsEnabled` (qué se ofrece en los selectores) y `SortOrder` opcional (para destacar las más usadas). Las traducciones tienen la clave `(código, Culture)` y caen a la cultura por defecto si falta una.

| Tabla | Columnas |
|---|---|
| `Currencies` | `Code` (`ARS`, PK, char(3)), `NumericCode` (`032`), `MinorUnits` (ARS 2, CLP 0, BHD 3), `Symbol` (`$`), `IsEnabled`, `SortOrder` |
| `CurrencyTranslations` | `CurrencyCode`, `Culture`, `Name` ("Peso argentino"), `NamePlural` |
| `Countries` | `Code` (`AR`, PK, alfa-2), `Alpha3` (`ARG`), `NumericCode`, `CallingCode` (`54`), `DefaultCurrencyCode` (FK), `DefaultTimeZoneId` (FK), `IsEnabled`, `SortOrder` |
| `CountryTranslations` | `CountryCode`, `Culture`, `Name` ("Argentina") |
| `TimeZones` | `Id` (`America/Argentina/Buenos_Aires`, PK), `CountryCode` (FK, opcional), `IsEnabled`, `SortOrder` |
| `TimeZoneTranslations` | `TimeZoneId`, `Culture`, `City` ("Buenos Aires") |
| `Cultures` | `Code` (`es-AR`, PK), `LanguageCode` (`es`), `CountryCode` (FK), `DatePattern` (`dd/MM/yyyy`), `TimePattern` (`HH:mm`), `DateTimePattern`, `LongDatePattern`, `DecimalSeparator`, `GroupSeparator`, `CurrencyPattern` (`$ n` / `-$ n`), `PercentPattern`, `FallbackCulture`, `IsEnabled`, `IsDefault` |
| `TaxIdTypes` | `Code` (`AR-CUIT`, PK), `CountryCode` (FK), `Label`, `Mask` (`99-99999999-9`), `ValidatorKey` (`ar-cuit-mod11`), `AppliesTo` (`Person` \| `Company` \| `Both`), `IsEnabled` |
| `TaxIdTypeTranslations` | `TaxIdTypeCode`, `Culture`, `Name` ("CUIT") |

- **Las demás tablas apuntan con FK**: `Money.Currency` → `Currencies.Code`; `TenantSettings.DefaultCulture` / `DefaultTimeZoneId` / `DefaultCurrency`, `Companies.TimeZoneId` y `Companies.TaxIdType` → sus tablas; `ApplicationUser.Culture` y `TimeZoneId` también.
- **Los algoritmos sí son código:** el dígito verificador del CUIT es una clase (`ArgentineCuitValidator`) registrada con su `ValidatorKey`. La tabla dice qué tipos existen y cuál valida cada uno. Un país nuevo suma su fila y, si hace falta, su validador.
- **Los patrones de formato de `Cultures`** son la única fuente de `DisplayFormatter` (back) y de `shared/format` (front). Reemplazan a los perfiles escritos a mano: son CLDR con los ajustes del producto (por ejemplo, 24 h en `es-AR`).

## 3. De dónde salen los datos (seed)

- **Archivos fuente**, versionados en `src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/Seed/ReferenceData/`: `currencies.json`, `countries.json`, `time-zones.json`, `cultures.json` y `tax-id-types.json`, con las traducciones adentro.
- **Se generan con un script** (`scripts/datos-de-referencia/generar.mjs`) a partir de las fuentes oficiales: ISO 4217, ISO 3166 y los nombres de CLDR (vía `Intl.DisplayNames`), y la lista canónica de zonas IANA (`Intl.supportedValuesOf("timeZone")` más su país). La ciudad sale del último tramo del ID (`Buenos_Aires` → "Buenos Aires"), salvo que `scripts/datos-de-referencia/ciudades.<idioma>.json` la traduzca ("New_York" → "Nueva York"). Los patrones de `cultures.json` y los tipos fiscales se escriben a mano, porque son decisiones del producto y no hay fuente oficial. **Nunca se edita un JSON generado:** se regenera.
- **`ReferenceDataSeeder`** hace un upsert por clave en cada arranque, dentro del seed idempotente (ADR 0006). Nunca borra: una moneda que sale de ISO pasa a `IsEnabled = false`. Qué se habilita por defecto lo dice el propio JSON: el país y la moneda de la cultura por defecto, más las culturas soportadas.

## 4. Cómo se usan

- **Back:** un puerto por catálogo en `Application/Interfaces/ReferenceData/` (`ICurrencyCatalog`, `ICountryCatalog`, `ITimeZoneCatalog`, `ICultureCatalog`, `ITaxIdTypeCatalog`). Los lee `ReferenceDataReader`, con HybridCache `p:ref:<catálogo>`, que se invalida cuando el seed cambia algo.
  - `CurrencyCode.Create(code)` y `Money` validan contra `ICurrencyCatalog`, y `Money.Round()` usa sus `MinorUnits`.
  - `ValidationRules` suma `ValidCurrency()`, `ValidCountry()`, `ValidTimeZone()`, `ValidCulture()` y `ValidTaxIdType()`, siempre contra los catálogos: un código deshabilitado es inválido para un dato nuevo, y uno ya guardado se sigue mostrando.
- **API:** `GET /api/reference-data` (anónimo, `[AllowAnonymous]`, en la lista de `AccessDeclarationTests`) devuelve los cinco catálogos habilitados, traducidos a la cultura del pedido, con `ETag` y caché del navegador. Hay además uno por catálogo (`/api/reference-data/currencies`…) para los selectores con búsqueda.
- **Front:** `shared/referenceData` los carga una vez al arrancar (TanStack Query, `staleTime: Infinity`) y los pasa a `useFormat` y a los campos (`CurrencySelect`, `CountrySelect`, `TimeZoneSelect`, `CultureSelect`, `TaxIdField`). Mientras no llegan, los campos muestran su estado de carga. Nada de listas escritas en el front.
- **Mostrar una zona:** "Buenos Aires (GMT−3)". La ciudad sale de la tabla y el offset se calcula al mostrar, con el signo menos tipográfico. Los selectores ordenan por `SortOrder`, después por offset y después por ciudad.
- **Mostrar un teléfono:** en formato nacional si su país es el de la cultura (`AR` para `es-AR`) y en internacional si no. Los países del selector salen de `Countries` y su prefijo de `CallingCode`, validado con `libphonenumber`.

## 5. Traducciones de la interfaz (no son datos de referencia)

- **Textos del back:** `.resx` por área (`Errors`, `Validation`, `Permissions`, `Notifications`, `Audit`…) con su `.en.resx`. **Textos del front:** `src/locales/<idioma>/<namespace>.json` con i18next.
- **Se habilitan las culturas de la tabla `Cultures`**: el back arma `RequestLocalizationOptions` desde `ICultureCatalog`, y el front toma la lista de `GET /api/reference-data`.
- **Paridad obligatoria:** `ResourceParityTests` y `parity.test.ts` exigen las mismas claves en todos los idiomas habilitados.
- **Cadena de caída:** cultura pedida → su `FallbackCulture` → la cultura por defecto.

## 6. Sumar un idioma, un país o una moneda

- **Una moneda o un país:** regenerar el catálogo si es nuevo en ISO, o pasar `IsEnabled` a `true` en el JSON. Ningún cambio de código.
- **Una zona horaria:** regenerar; si su ciudad necesita traducción, sumarla en `ciudades.<idioma>.json`.
- **Un tipo de identificación fiscal:** su fila en `tax-id-types.json` y, si trae un algoritmo nuevo, su validador registrado con el `ValidatorKey`.
- **Un idioma o una cultura:**
  1. su fila en `cultures.json`, con sus patrones;
  2. los `.resx` y la carpeta `src/locales/<idioma>`, con las mismas claves (lo exigen los tests de paridad);
  3. los nombres traducidos: regenerar los catálogos, que traen la columna de ese idioma;
  4. sus casos en `format-cases.json`;
  5. el texto legal en `LegalDocumentContents`;
  6. las plantillas de correo y de WhatsApp en ese idioma (`whatsapp-plantillas.md`).

## 7. `format-cases.json`

`docs/contracts/format-cases.json` es el contrato común de formato: `{ "now": "2026-09-27T15:00:00Z", "cases": [{ "id", "type", "culture", "timeZone", "input", "expected" }] }`, con `now` fijo para los tiempos relativos. Tiene al menos un caso por tipo de dato y por cultura habilitada, con monedas de 0, 2 y 3 decimales y teléfonos nacionales e internacionales. Lo recorren `DisplayFormatterTests` (back) y `formatters.test.ts` (front), y los dos tienen que dar exactamente el mismo texto. Los patrones salen de los mismos datos de `cultures.json`.

## 8. Etapas

- **E1:** los puertos, los JSON fuente con su script, un adaptador que lee los JSON (`JsonReferenceDataCatalog`), las validaciones, `GET /api/reference-data`, `format-cases.json` y el front consumiéndolos. Todo sin base.
- **E2:** las tablas, sus configuraciones EF y su migración, `ReferenceDataSeeder` y `ReferenceDataReader` con HybridCache, que reemplaza al adaptador de JSON. Los tests de la E1 siguen en verde sin cambios.
- **Desde la E3:** las FK de `ApplicationUser`, `TenantSettings` y `Companies` apuntan a estas tablas.
