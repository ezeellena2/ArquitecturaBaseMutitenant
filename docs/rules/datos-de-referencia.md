# Datos de referencia (monedas, países, zonas, culturas, identificación fiscal)

**Regla:** ninguna moneda, país, zona horaria, cultura ni tipo de identificación fiscal se escribe a mano en el código. En E1 salen de JSON versionados generados desde fuentes oficiales fijadas (ISO 4217, ISO 3166, IANA y CLDR); desde E2, de las tablas de `platform` cargadas con seed idempotente. Se leen siempre por su catálogo.

## Cómo se hace
- **Leer:** inyectá el catálogo (`ICurrencyCatalog`, `ICountryCatalog`, `ITimeZoneCatalog`, `ICultureCatalog` o `ITaxIdTypeCatalog`). Nunca leas las tablas directo desde un servicio.
- **Política de un canal:** `WhatsApp:AllowedCountries` configura un subconjunto de países habilitados, no reemplaza el catálogo. El valor inicial AR está establecido por el plan E8; la guarda admite únicamente ese default en `WhatsAppOptions` y la propiedad exacta de `Api/appsettings.json`. El país del destino se deriva del parser; las entradas y los selectores siguen usando referencias. Otros literales, listas y propiedades continúan prohibidos.
- **Restricción de facturación de un proveedor:** un módulo puede fijar la moneda exigida por su contrato en un resource de datos invariante, sin lista ni selector. `AssistantContractPolicy.BillingCurrency` lee `AssistantContractData`, compartido por validadores y ejemplos OpenAPI. Esa política no reemplaza `ICurrencyCatalog`: el servicio que guarda valida además la referencia habilitada y obtiene sus decimales. No se repite el literal ISO en C# ni se agrega una excepción por namespace a la guarda.
- **Validar un código nuevo:** `ValidCurrency()`, `ValidCountry()`, `ValidTimeZone()`, `ValidCulture()` o `ValidTaxIdType()` de `ValidationRules`, en Application y contra los catálogos. Un código deshabilitado es inválido para un dato nuevo, pero sigue siendo legible si ya estaba guardado. `CurrencyCode.Create(code)` en Domain solo valida la sintaxis alfa-3: nunca inyecta `ICurrencyCatalog` de Application.
- **Redondear un monto:** Application obtiene `MinorUnits` de `ICurrencyCatalog` y llama `Money.Round(minorUnits)`; Domain redondea `AwayFromZero` sin consultar el catálogo.
- **Formatear:** siempre con `DisplayFormatter` o `shared/format`, que toman sus patrones de `Cultures`.
- **Resolver cultura:** `CultureProfiles` es el único resolver del backend para la respuesta HTTP y el formato; compara códigos sin distinguir mayúsculas, ignora culturas pedidas o de caída deshabilitadas y acaba en la predeterminada habilitada. Traducciones de catálogo y `FormattingTexts` siguen la misma cadena.
- **Ordenar listas:** `SortOrder` es la clave principal; a igual valor, compará el nombre o la ciudad traducida con la cultura efectiva del perfil, no con orden ordinal.
- **Una tabla nueva que guarda un código** (moneda, país, zona, cultura o tipo fiscal) lleva FK a su tabla de referencia.
- **Sumar datos:** actualizá fuentes con `generar.mjs --refresh` cuando cambien ISO/IANA/CLDR, ajustá `scripts/datos-de-referencia/habilitados.json` para `IsEnabled`/`SortOrder`, o editá `cultures.source.json`/`tax-id-types.source.json` si cambia una decisión del producto; revisá snapshots y hashes en `sources.lock.json`, luego regenerá. Los cinco JSON en Infrastructure son salidas: nunca los edites a mano. La generación normal y `generar.test.mjs` corren sin red con Node, paquetes y snapshots fijados y comprueban salida byte a byte.
- **Filtrar países ISO:** usá `sources/cldr-region-validity.xml` de CLDR `release-48-2` (URL y SHA-256 en `sources.lock.json`), expandí solo sus regiones `regular` y cruzalas con `codeMappings` de `cldr-core` 48.2 que tengan alfa-3 y numérico de tres dígitos menor que 900. `AN`, `AA` y `XK` no son países ISO vigentes, aunque aparezcan en algún dato de CLDR. La versión fijada debe producir 249 países; no mantengas una lista manual.
- **Territorios incompletos:** si ISO no aporta prefijo, moneda o zona propia, el campo queda `null` y el registro no se habilita para selección nueva. Las FK se prueban solo para valores presentes; no se inventa un valor de reemplazo.
- **Zona predeterminada del país:** una sola zona IANA → esa zona; varias → `primaryZones` de CLDR normalizada con `cldr-bcp47`; sin principal → `null`, salvo `defaultTimeZoneId` explícito en `habilitados.json`. La ciudad sale de `exemplarCity` por ID o alias histórico CLDR; `ciudades.<idioma>.json` solo cubre excepciones del producto.
- **Zona compartida:** `time-zones.json` conserva todos los países de cada fila IANA en `CountryCodes[]`; en E2 `platform.TimeZoneCountries` los persiste con FK y deshabilita asociaciones que IANA retire. No reduzcas la relación a un solo `TimeZones.CountryCode`. `UTC` tiene `CountryCodes: []`.
- **Habilitación inicial:** generador habilita país y moneda de la cultura por defecto, culturas soportadas, zonas asociadas por IANA a un país habilitado más `UTC`, y tipos fiscales de país habilitado. `habilitados.json` puede cambiar estas selecciones sin un `switch` en código. `ReferenceDataCatalogTests` exige que TimeZoneSelect y TaxIdField tengan opciones iniciales.
- **En el front:** leé todas las filas de `shared/referenceData` (`GET /api/reference-data`), filtrá `isEnabled` en los selectores de `shared/ui/fields` y usá cualquier fila para mostrar un valor existente con `shared/format`.
- **Etapas:** E1 lee los JSON versionados con `JsonReferenceDataCatalog`; E2 agrega tablas, `ReferenceDataSeeder` y `ReferenceDataReader`, que también devuelve todas las filas. `GET /api/reference-data` reemplaza `GET /api/time-zones`, lleva `[AllowAnonymous]` e incluye `isEnabled` en cada fila.

## Prohibido
- Un `enum`, una constante o un `switch` con monedas, países, zonas o culturas (`if (currency == "ARS")`, `["ARS", "USD"]`).
- Guardar un offset o un texto ya formateado.
- Una lista de opciones escrita en un componente del front.
- Los decimales de una moneda escritos a mano (`Math.Round(x, 2)` sobre un monto).

## Copiá de
- `docs/architecture/datos-de-referencia.md` (el diseño completo) · `Application/Interfaces/ReferenceData/ICurrencyCatalog.cs` (E1) · `Infrastructure/Persistence/Seed/ReferenceDataSeeder.cs` (E2).

## Lo verifica
- `ReferenceDataHardcodeTests` (E1)

## Detalle
[datos-de-referencia.md](../architecture/datos-de-referencia.md) · [numeros-y-moneda](numeros-y-moneda.md) · [fechas-y-zonas](fechas-y-zonas.md) · [telefonos](telefonos.md) · [identificacion-fiscal](identificacion-fiscal.md) · [textos-y-traducciones](textos-y-traducciones.md)
