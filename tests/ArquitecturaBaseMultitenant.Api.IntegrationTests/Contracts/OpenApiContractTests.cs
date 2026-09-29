using System.Net;
using System.Text.Json.Nodes;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using Microsoft.AspNetCore.Hosting;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Contracts;

[Collection(ApiTestGroup.Name)]
public sealed class OpenApiContractTests(ApiFactory factory)
{
    [Fact]
    public async Task Versioned_contract_matches_production_api_paths()
    {
        var root = FindRepositoryRoot();
        var contractPath = Path.Combine(root, "docs", "contracts", "openapi.json");
        Assert.True(File.Exists(contractPath), "The build must generate docs/contracts/openapi.json.");

        var versioned = JsonNode.Parse(await File.ReadAllTextAsync(contractPath, TestContext.Current.CancellationToken))!;
        await using var development = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Development"));
        using var client = development.CreateClient();
        using var response = await client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);
        var runtime = JsonNode.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))!;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var versionedPaths = versioned["paths"]!.AsObject();
        var runtimePaths = runtime["paths"]!.AsObject();
        var versionedApiPaths = versionedPaths.Where(path => path.Key.StartsWith("/api/", StringComparison.Ordinal))
            .Select(path => path.Key).Order(StringComparer.Ordinal).ToArray();
        var runtimeApiPaths = runtimePaths.Where(path => path.Key.StartsWith("/api/", StringComparison.Ordinal))
            .Select(path => path.Key).Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(runtimeApiPaths, versionedApiPaths);

        foreach (var path in runtimePaths.Where(path => path.Key.StartsWith("/api/", StringComparison.Ordinal)))
        {
            Assert.True(JsonNode.DeepEquals(versionedPaths[path.Key], path.Value), $"Stale OpenAPI path {path.Key}.");
        }

        Assert.True(JsonNode.DeepEquals(
            versioned["components"]?["schemas"]?["PagedRequest"],
            runtime["components"]?["schemas"]?["PagedRequest"]),
            "Stale OpenAPI PagedRequest schema.");

        Assert.DoesNotContain(versionedPaths, path => path.Key.StartsWith("/test/", StringComparison.Ordinal));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ArquitecturaBaseMultitenant.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
