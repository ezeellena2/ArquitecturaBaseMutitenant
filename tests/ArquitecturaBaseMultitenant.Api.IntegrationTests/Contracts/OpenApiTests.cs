using System.Net;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using Microsoft.AspNetCore.Hosting;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Contracts;

[Collection(ApiTestGroup.Name)]
public sealed class OpenApiTests(ApiFactory factory)
{
    private static readonly string[] ReferencePaths =
    [
        "/api/reference-data",
        "/api/reference-data/currencies",
        "/api/reference-data/countries",
        "/api/reference-data/time-zones",
        "/api/reference-data/cultures",
        "/api/reference-data/tax-id-types",
    ];

    [Fact]
    public async Task Swagger_and_document_are_available_in_development()
    {
        await using var development = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Development"));
        using var client = development.CreateClient();

        using var swagger = await client.GetAsync("/swagger/index.html", TestContext.Current.CancellationToken);
        using var openapi = await client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);
        var html = await swagger.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, swagger.StatusCode);
        Assert.Contains("swagger-ui", html, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.OK, openapi.StatusCode);
        Assert.Equal("application/json", openapi.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Reference_routes_have_success_schemas_and_problem_details_errors()
    {
        await using var development = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Development"));
        using var client = development.CreateClient();
        using var response = await client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var paths = document.RootElement.GetProperty("paths");
        foreach (var path in ReferencePaths)
        {
            var operation = paths.GetProperty(path).GetProperty("get");
            var responses = operation.GetProperty("responses");
            Assert.Equal("ReferenceData", operation.GetProperty("tags")[0].GetString());
            Assert.True(HasSchema(responses.GetProperty("200"), "application/json"), path);
            foreach (var error in responses.EnumerateObject()
                .Where(response => response.Name.StartsWith('4') || response.Name.StartsWith('5')))
            {
                Assert.True(IsProblemDetails(error.Value), $"{path} {error.Name}");
            }

            Assert.True(IsProblemDetails(responses.GetProperty("500")), path);
            Assert.False(responses.TryGetProperty("401", out _));
            Assert.False(responses.TryGetProperty("403", out _));
        }
    }

    [Fact]
    public async Task Reference_codes_have_string_schemas_for_generated_frontend_types()
    {
        await using var development = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Development"));
        using var client = development.CreateClient();
        using var response = await client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        var schemas = document.RootElement.GetProperty("components").GetProperty("schemas");
        Assert.Equal("string", schemas.GetProperty("ReferenceDataHttpResponse").GetProperty("properties")
            .GetProperty("culture").GetProperty("type").GetString());
        Assert.Equal("string", schemas.GetProperty("CurrencyReferenceHttpResponse").GetProperty("properties")
            .GetProperty("code").GetProperty("type").GetString());
    }

    [Theory]
    [InlineData("/swagger/index.html")]
    [InlineData("/openapi/v1.json")]
    public async Task Documentation_is_not_served_outside_development(string path)
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static bool HasSchema(JsonElement response, string contentType) =>
        response.TryGetProperty("content", out var content)
        && content.TryGetProperty(contentType, out var mediaType)
        && mediaType.TryGetProperty("schema", out _);

    private static bool IsProblemDetails(JsonElement response) =>
        response.TryGetProperty("content", out var content)
        && content.TryGetProperty("application/problem+json", out var mediaType)
        && mediaType.TryGetProperty("schema", out var schema)
        && schema.TryGetProperty("$ref", out var reference)
        && reference.GetString() == "#/components/schemas/ProblemDetails";
}
