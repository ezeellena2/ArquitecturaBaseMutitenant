Las políticas de ingreso limitan por IP los pedidos y verificaciones de códigos. Usá los nombres de `RateLimitPolicies` en cada acción y conservá el 429 ProblemDetails con `retryAfter` y `Retry-After` iguales.
Leé `docs/rules/api-http.md` y `docs/architecture/backend.md` §17.
Lo verifica `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Auth/LoginRateLimitTests.cs`.
