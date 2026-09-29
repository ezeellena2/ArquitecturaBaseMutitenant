Fixture HTTP y de PostgreSQL para tests de integración. Usá la conexión administradora solo para migración y `IsolationSchema`; para comprobar RLS abrí `RuntimeRoleConnection` como `mt_app`.

Leé: [tests](../../../docs/rules/tests.md) · [multitenancy](../../../docs/rules/multitenancy.md) · [persistencia-ef](../../../docs/rules/persistencia-ef.md). Copiá de `ApiFactory.cs` y `../ArquitecturaBase/tests/ArquitecturaBase.Api.IntegrationTests/Support/ApiFactory.cs`.
