# Números, decimales, moneda y porcentajes

**Regla:** montos y cantidades son `decimal`, nunca `double`. Un importe es siempre `Money` (monto + moneda ISO 4217). Un porcentaje es una fracción. **El backend calcula y redondea; el front solo muestra.**

## Cómo se hace
- **Monto:** `Money` (`Domain/ValueObjects`). JSON `{ "amount": 1234.5, "currency": "ARS" }`; en la base, dos columnas: `numeric(19,4)` + `char(3)`.
- **Suma y resta de montos:** solo con la misma moneda (`Money.Add` lanza si difieren). Application lee `MinorUnits` de `ICurrencyCatalog` y llama `money.Round(minorUnits)`: `MidpointRounding.AwayFromZero`, **por línea y después el total**. Domain no depende de Application.
- **Código de moneda:** `CurrencyCode.Create(code)` en Domain valida únicamente la forma alfa-3. `ValidCurrency()` en Application consulta el catálogo ISO 4217 para aceptar un código habilitado en un dato nuevo; uno ya guardado se sigue leyendo si luego queda deshabilitado. Los códigos y decimales viven en los JSON E1 y en `platform.Currencies` E2, no en constantes.
- **Otros decimales** (cantidades, tasas): `decimal`, con `HasPrecision(p, s)` **explícito** en la configuración EF.
- **Porcentaje:** fracción `decimal` (`0.125` = 12,5 %), columna `numeric(9,6)`.
- **Sin dato:** `null`, nunca `0` ni `""`.
- **Moneda por defecto** de la organización: `TenantSettings.DefaultCurrency`. Sirve solo para precargar un formulario; el dato guardado lleva siempre su moneda.
- **Mostrar un número** en un correo o WhatsApp: crear una vez `ctx = await DisplayFormatter.CreateAsync(culture, timeZone)` y usar `await FormatMoneyAsync(money, ctx)`, `FormatDecimal(value, digits, ctx)` o `FormatPercent(fraction, ctx)`. Son métodos tipados; el despachador JSON es interno y se usa solo para `format-cases.json`. Los patrones salen de `ICultureCatalog` y `CurrencyTranslations.DisplaySymbol` da el símbolo para esa cultura. El símbolo global de `Currencies` no basta (ARS se muestra `$` en es-AR y `ARS` en en-US).

## Prohibido
- `double` o `float` en una entidad o un modelo.
- `decimal` sin `HasPrecision`.
- Un importe sin moneda.
- `Math.Round` sin `MidpointRounding`.
- Una lista o un `switch` de monedas o decimales en el código ([datos-de-referencia](datos-de-referencia.md)).
- Porcentajes como 12.5.
- `ToString("N2")`, `ToString("C")` o `string.Format` con números para mostrar.

## Copiá de
- `Domain/ValueObjects/Money.cs` (E1) · `Application/Common/Formatting/DisplayFormatter.cs` (E1) · `docs/contracts/format-cases.json` (E1)

## Lo verifica
- `DecimalPrecisionTests` (E1): ningún `decimal` sin precisión y ningún `double` o `float` en entidades.
- `MoneyTests` y `CurrencyCodeTests` (E1): sintaxis, suma y redondeo con `MinorUnits` explícitos. `ReferenceDataValidationTests` (E1): existencia y habilitación. `MoneyJsonTests` (E1): forma JSON y sintaxis inválida.
- `DisplayFormatterTests` (E1): los mismos casos que el front.
- `NoManualFormattingTests` (E1).

## Detalle
[backend.md §18](../architecture/backend.md#18-representación-y-formato-de-datos-unificado) · catálogo visual en el front: `docs/architecture/formatos.md`
