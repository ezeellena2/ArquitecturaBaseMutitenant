# Números, decimales, moneda y porcentajes

**Regla:** montos y cantidades son `decimal`, nunca `double`. Un importe es siempre `Money` (monto + moneda ISO 4217). Un porcentaje es una fracción. **El backend calcula y redondea; el front solo muestra.**

## Cómo se hace
- **Monto:** `Money` (`Domain/ValueObjects`). JSON `{ "amount": 1234.5, "currency": "ARS" }`; en la base, dos columnas: `numeric(19,4)` + `char(3)`.
- **Suma y resta de montos:** solo con la misma moneda (`Money.Add` lanza si difieren). Redondeo con `money.Round()`: `MidpointRounding.AwayFromZero`, a los decimales de la moneda, **por línea y después el total**.
- **Otros decimales** (cantidades, tasas): `decimal`, con `HasPrecision(p, s)` **explícito** en la configuración EF.
- **Porcentaje:** fracción `decimal` (`0.125` = 12,5 %), columna `numeric(9,6)`.
- **Sin dato:** `null`, nunca `0` ni `""`.
- **Moneda por defecto** de la organización: `TenantSettings.DefaultCurrency`. Sirve solo para precargar un formulario; el dato guardado lleva siempre su moneda.
- **Mostrar un número** en un correo o WhatsApp: `DisplayFormatter.Money(value, culture)` o `.Decimal(...)` / `.Percent(...)`, con cultura explícita.

## Prohibido
- `double` o `float` en una entidad o un modelo.
- `decimal` sin `HasPrecision`.
- Un importe sin moneda.
- `Math.Round` sin `MidpointRounding`.
- Porcentajes como 12.5.
- `ToString("N2")`, `ToString("C")` o `string.Format` con números para mostrar.

## Copiá de
- `Domain/ValueObjects/Money.cs` (E1) · `Application/Common/Formatting/DisplayFormatter.cs` (E1) · `docs/contracts/format-cases.json` (E1)

## Lo verifica
- `DecimalPrecisionTests` (E1): ningún `decimal` sin precisión y ningún `double` o `float` en entidades.
- `MoneyTests` (E1), `MoneyJsonTests` (E1).
- `DisplayFormatterTests` (E1): los mismos casos que el front.
- `NoManualFormattingTests` (E1).

## Detalle
[backend.md §18](../architecture/backend.md#18-representación-y-formato-de-datos-unificado) · catálogo visual en el front: `docs/architecture/formatos.md`
