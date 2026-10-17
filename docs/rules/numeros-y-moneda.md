# Números, decimales, moneda y porcentajes

**Regla:** montos y cantidades son `decimal`, nunca `double`. Un importe es siempre `Money` (monto + moneda ISO 4217). Un porcentaje es una fracción. **El backend calcula y redondea; el front solo muestra.**

## Cómo se hace
- **Monto:** `Money` (`Domain/ValueObjects`). JSON `{ "amount": 1234.5, "currency": "ARS" }`; en la base, dos columnas: `numeric(19,4)` + `char(3)`.
- **Suma y resta de montos:** solo con la misma moneda (`Money.Add` lanza si difieren). Application lee `MinorUnits` de `ICurrencyCatalog` y llama `money.Round(minorUnits)`: `MidpointRounding.AwayFromZero`, **por línea y después el total**. Domain no depende de Application.
- **Excepción técnica IA ([ADR 0051](../decisions/0051-precision-tecnica-de-costos-ia.md), autorizada por el usuario):** tarifas `Money` por millón y snapshots con hasta seis decimales (`numeric(18,6)`); costos y reservas técnicos `Money` con hasta doce (`numeric(24,12)`), normalizados con `AwayFromZero` a esa escala y acumulados sin redondear cada llamada a centavos. Validar escala de tarifas nuevas, sin truncamiento silencioso. Presupuesto disponible y umbrales usan el acumulado técnico persistido. Presupuestos ingresados y presentación siguen `MinorUnits` del catálogo y `DisplayFormatter`. Sólo estimaciones IA; no modifica importes comerciales, moneda explícita ni contrato HTTP.
- **Código de moneda:** `CurrencyCode.Create(code)` en Domain valida únicamente la forma alfa-3. `ValidCurrency()` en Application consulta el catálogo ISO 4217 para aceptar un código habilitado en un dato nuevo; uno ya guardado se sigue leyendo si luego queda deshabilitado. Los códigos y decimales viven en los JSON E1 y en `platform.Currencies` E2, no en constantes.
- **Otros decimales** (cantidades, tasas): `decimal`, con `HasPrecision(p, s)` **explícito** en la configuración EF.
- **Porcentaje:** fracción `decimal` (`0.125` = 12,5 %), columna `numeric(9,6)`.
- **Sin dato:** `null`, nunca `0` ni `""`.
- **Moneda por defecto** de la organización: `TenantSettings.DefaultCurrency`. Sirve solo para precargar un formulario; el dato guardado lleva siempre su moneda.
- **Mostrar un número** en un correo o WhatsApp: crear una vez `ctx = await DisplayFormatter.CreateAsync(culture, timeZone)` y usar `await FormatMoneyAsync(money, ctx)`, `FormatDecimal(value, digits, ctx)` o `FormatPercent(fraction, ctx)`. Son métodos tipados; el despachador JSON es interno y se usa solo para `format-cases.json`. Los patrones salen de `ICultureCatalog` y `CurrencyTranslations.DisplaySymbol` da el símbolo para esa cultura. El símbolo global de `Currencies` no basta (ARS se muestra `$` en es-AR y `ARS` en en-US).
- Al mostrar dinero, primero se redondea a los `MinorUnits` de la moneda y luego se decide el signo. Los formatos compactos y de tamaño promueven la unidad si la magnitud presentada llega a 1000 tras redondear.
- **Versiones de protocolo:** las versiones de concurrencia usadas en URLs y `ETag` se serializan con `VersionWireFormatter`, como dígitos invariantes sin separadores. Es un serializador técnico permitido explícitamente por `NoManualFormattingTests`; la presentación para una persona sigue en `DisplayFormatter`.
- **Números técnicos:** secuencias SSE, largos del digest multipart y tarifas que integran una revisión técnica se serializan con `VersionWireFormatter`: enteros sin separadores y decimales canónicos sin escala redundante. La excepción cerrada de `NoManualFormattingTests` alcanza sólo a ese serializador; los textos de alertas siguen usando `DisplayFormatter`.
- El JSON number admite hasta 13 dígitos enteros y 2 decimales para importes; superar ese límite requiere un ADR.

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
- `DecimalPrecisionTests` (E1), `NoManualFormattingTests` (E0)

## Detalle
[backend.md §18](../architecture/backend.md#18-representación-y-formato-de-datos-unificado) · catálogo visual en el front: `docs/rules/formatos.md`
