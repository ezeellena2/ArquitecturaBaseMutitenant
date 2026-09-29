using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Api.Json;
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

    [Fact]
    public async Task Page_size_options_are_published_as_an_integer_enum()
    {
        await using var development = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Development"));
        using var client = development.CreateClient();
        using var response = await client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        var schema = document.RootElement.GetProperty("components").GetProperty("schemas")
            .GetProperty("PagedRequest").GetProperty("properties").GetProperty("pageSize");
        Assert.Equal("integer", schema.GetProperty("type").GetString());
        Assert.Equal(PagedRequest.AllowedPageSizes,
            schema.GetProperty("enum").EnumerateArray().Select(value => value.GetInt32()).ToArray());

        var queryParameter = document.RootElement.GetProperty("paths").GetProperty("/test/paged")
            .GetProperty("get").GetProperty("parameters").EnumerateArray()
            .Single(parameter => parameter.GetProperty("name").GetString() == "pageSize");
        Assert.Equal(PagedRequest.AllowedPageSizes,
            queryParameter.GetProperty("schema").GetProperty("enum").EnumerateArray()
                .Select(value => value.GetInt32()).ToArray());
    }

    [Fact]
    public async Task Reference_schemas_preserve_nullable_strings_array_items_and_numeric_types()
    {
        await using var development = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Development"));
        using var client = development.CreateClient();
        using var response = await client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var schemas = document.RootElement.GetProperty("components").GetProperty("schemas");

        var country = schemas.GetProperty("CountryReferenceHttpResponse");
        var required = country.GetProperty("required").EnumerateArray()
            .Select(item => item.GetString()).ToArray();
        Assert.DoesNotContain("callingCode", required);
        Assert.DoesNotContain("defaultCurrencyCode", required);
        Assert.DoesNotContain("defaultTimeZoneId", required);
        var nullableStringTypes = country.GetProperty("properties").GetProperty("defaultTimeZoneId")
            .GetProperty("type").EnumerateArray().Select(item => item.GetString()).ToArray();
        Assert.Contains("string", nullableStringTypes);
        Assert.Contains("null", nullableStringTypes);
        Assert.Equal("string", schemas.GetProperty("TimeZoneReferenceHttpResponse")
            .GetProperty("properties").GetProperty("countryCodes").GetProperty("items")
            .GetProperty("type").GetString());

        foreach (var property in new[] { "minorUnits", "sortOrder" })
        {
            var types = schemas.GetProperty("CurrencyReferenceHttpResponse")
                .GetProperty("properties").GetProperty(property).GetProperty("type")
                .EnumerateArray().Select(item => item.GetString()).ToArray();
            Assert.Contains("integer", types);
            Assert.Contains("null", types);
            Assert.DoesNotContain("string", types);
        }

        using var payload = await client.GetAsync("/api/reference-data/currencies", TestContext.Current.CancellationToken);
        using var data = JsonDocument.Parse(await payload.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(HttpStatusCode.OK, payload.StatusCode);
        Assert.Contains(data.RootElement.EnumerateArray(), item =>
            item.GetProperty("minorUnits").ValueKind == JsonValueKind.Number);
    }

    [Fact]
    public void Json_options_reject_numbers_encoded_as_strings()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        JsonConfiguration.ConfigureJson(options);

        Assert.Equal(JsonNumberHandling.Strict, options.NumberHandling);
        Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<Dictionary<string, int>>("""{"value":"42"}""", options));
    }

    [Fact]
    public async Task Problem_responses_document_code_trace_fields_and_retry_after()
    {
        await using var development = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Development"));
        using var client = development.CreateClient();
        using var response = await client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var root = document.RootElement;
        var responses = root.GetProperty("paths").GetProperty("/test/result/{type}")
            .GetProperty("get").GetProperty("responses");

        foreach (var status in new[] { "400", "409", "429" })
        {
            Assert.True(IsProblemDetails(responses.GetProperty(status)), status);
        }

        var schema = root.GetProperty("components").GetProperty("schemas").GetProperty("ProblemDetails");
        var properties = schema.GetProperty("properties");
        Assert.Equal("string", properties.GetProperty("code").GetProperty("type").GetString());
        Assert.Equal("string", properties.GetProperty("traceId").GetProperty("type").GetString());
        Assert.Equal("array", properties.GetProperty("errors").GetProperty("additionalProperties")
            .GetProperty("type").GetString());
        Assert.Equal("string", properties.GetProperty("errors").GetProperty("additionalProperties")
            .GetProperty("items").GetProperty("type").GetString());
        var retryAfterTypes = properties.GetProperty("retryAfter").GetProperty("type").EnumerateArray()
            .Select(item => item.GetString()).ToArray();
        Assert.Contains("integer", retryAfterTypes);
        Assert.Contains("null", retryAfterTypes);
        var required = schema.GetProperty("required").EnumerateArray()
            .Select(item => item.GetString()).ToArray();
        Assert.Contains("code", required);
        Assert.Contains("traceId", required);
        Assert.DoesNotContain("errors", required);
        Assert.DoesNotContain("retryAfter", required);
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
