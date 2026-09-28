namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;

[CollectionDefinition(Name)]
public sealed class ApiTestGroup : ICollectionFixture<ApiFactory>
{
    public const string Name = "Api";
}
