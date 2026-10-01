namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;

/// <summary>
/// Agrupa los tests que comparten una ApiFactory administrada por xUnit. Permite reutilizar su servidor y
/// base de prueba durante la colección.
/// </summary>
[CollectionDefinition(Name)]
public sealed class ApiTestGroup : ICollectionFixture<ApiFactory>
{
    public const string Name = "Api";
}
