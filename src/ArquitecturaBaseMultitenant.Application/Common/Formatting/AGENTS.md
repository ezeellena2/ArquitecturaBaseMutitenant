Presentación unificada del backend. `SupportedCultures` y `CultureProfiles` toman las culturas del catálogo; `DisplayFormatter` muestra los datos, sin listas fijas.
Leé: [datos-de-referencia](../../../../docs/rules/datos-de-referencia.md) · [textos-y-traducciones](../../../../docs/rules/textos-y-traducciones.md) · [numeros-y-moneda](../../../../docs/rules/numeros-y-moneda.md).
Copiá de `DisplayFormatter.cs` y `SupportedCultures.cs` (E1); lo prueba `DisplayFormatterTests`.
Las versiones técnicas de URLs y ETag se serializan con `VersionWireFormatter`; no son textos de presentación. Lo prueba `VersionWireFormatterTests`.
