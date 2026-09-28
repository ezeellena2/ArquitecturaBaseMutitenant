Money, CurrencyCode, CultureCode, Email y PhoneNumber son value objects del dominio: invariantes de forma y aritmética, sin catálogos ni infraestructura.
Los datos admitidos para una entrada nueva y los MinorUnits se obtienen en Application. El parser de números nacionales llega en E3 y TaxId en E6.
Antes de escribir, leé: [números y moneda](../../../docs/rules/numeros-y-moneda.md) · [emails](../../../docs/rules/emails.md) · [telefonos](../../../docs/rules/telefonos.md) · [datos de referencia](../../../docs/rules/datos-de-referencia.md) · [resultados y errores](../../../docs/rules/result-y-errores.md).
Copiá de: `Money.cs`, `Email.cs` y `PhoneNumber.cs` (E1).
