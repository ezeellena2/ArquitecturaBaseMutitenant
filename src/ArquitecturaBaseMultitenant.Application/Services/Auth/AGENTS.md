Casos de uso de ingreso, registro de personas y de empresas (`BusinessSignupService`, alta de empresa) y «Recuperar mi cuenta», con sus helpers. `UserCultureResolver` resuelve sólo culturas habilitadas del catálogo; no mantiene una lista fija.
Leé: [identidad](../../../../docs/features/identidad.md) · [organizaciones](../../../../docs/features/organizaciones.md) · [datos-personales](../../../../docs/rules/datos-personales.md) · [textos-y-traducciones](../../../../docs/rules/textos-y-traducciones.md) · [guardado](../../../../docs/rules/guardado.md) · [logs](../../../../docs/rules/logs.md).
Copiá de: `LoginCodeIssuer.cs`, `LoginCodeVerifier.cs` y `AccessSwitchPolicy.cs`.
Probá los casos con `Api.IntegrationTests/Auth/`.
