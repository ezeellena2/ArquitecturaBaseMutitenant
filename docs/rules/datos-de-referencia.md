# Datos de referencia (monedas, países, zonas, culturas, identificación fiscal)

**Regla:** ninguna moneda, país, zona horaria, cultura ni tipo de identificación fiscal se escribe a mano en el código. Salen de las tablas de referencia de `platform`, cargadas desde los estándares oficiales (ISO 4217, ISO 3166, IANA y CLDR) con un seed idempotente, y se leen con su catálogo.

## Cómo se hace
- **Leer:** inyectá el catálogo (`ICurrencyCatalog`, `ICountryCatalog`, `ITimeZoneCatalog`, `ICultureCatalog` o `ITaxIdTypeCatalog`). Nunca leas las tablas directo desde un servicio.
- **Validar un código:** `ValidCurrency()`, `ValidCountry()`, `ValidTimeZone()`, `ValidCulture()` o `ValidTaxIdType()` de `ValidationRules`. Un código deshabilitado es inválido para un dato nuevo.
- **Redondear un monto:** `Money.Round()`, que usa los `MinorUnits` de la moneda.
- **Formatear:** siempre con `DisplayFormatter` o `shared/format`, que toman sus patrones de `Cultures`.
- **Una tabla nueva que guarda un código** (moneda, país, zona, cultura o tipo fiscal) lleva FK a su tabla de referencia.
- **Sumar datos:** regenerá con `scripts/datos-de-referencia/generar.mjs` o cambiá `IsEnabled` en el JSON fuente. Nunca edites un JSON generado a mano.
- **En el front:** leé los catálogos de `shared/referenceData` (`GET /api/reference-data`) y usá los selectores de `shared/ui/fields`.

## Prohibido
- Un `enum`, una constante o un `switch` con monedas, países, zonas o culturas (`if (currency == "ARS")`, `["ARS", "USD"]`).
- Guardar un offset o un texto ya formateado.
- Una lista de opciones escrita en un componente del front.
- Los decimales de una moneda escritos a mano (`Math.Round(x, 2)` sobre un monto).

## Copiá de
- `docs/architecture/datos-de-referencia.md` (el diseño completo) · `Application/Interfaces/ReferenceData/ICurrencyCatalog.cs` (E1) · `Infrastructure/Persistence/Seed/ReferenceDataSeeder.cs` (E2).

## Lo verifica
- `ReferenceDataHardcodeTests` (E1): ningún literal ISO de moneda, país o zona fuera de los JSON de referencia y de los tests.
- `ReferenceDataCatalogTests` (E1): los JSON son válidos, las FK internas existen (país → moneda y zona) y cada cultura habilitada tiene traducciones.
- `ReferenceDataSeederTests` (E2): el seed es idempotente y nunca borra.
- `DisplayFormatterTests` (E1) y `formatters.test.ts` (E1): mismo texto para cada caso de `format-cases.json`.

## Detalle
[datos-de-referencia.md](../architecture/datos-de-referencia.md) · [numeros-y-moneda](numeros-y-moneda.md) · [fechas-y-zonas](fechas-y-zonas.md) · [telefonos](telefonos.md) · [identificacion-fiscal](identificacion-fiscal.md) · [textos-y-traducciones](textos-y-traducciones.md)
