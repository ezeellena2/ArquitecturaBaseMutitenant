using System.Net;
using System.Text;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Hosting;

/// <summary>
/// Comprueba qué rutas abren la SPA y cuáles pertenecen al backend. Evita devolver HTML de la aplicación
/// ante una ruta de API inexistente.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class SpaHostingTests(ApiFactory factory)
{
    private const string SpaMarker = "<!doctype html><title>spa test</title>";
    private const string AssetMarker = "export const marker = 'asset';";

    [Theory]
    [InlineData("app.localtest.me", "/")]
    [InlineData("app.localtest.me", "/login")]
    [InlineData("empresa.localtest.me", "/catalogo")]
    public async Task Browser_routes_on_main_and_subdomain_open_the_spa(string host, string path)
    {
        using var webRoot = new TestWebRoot();
        await using var api = CreateApi(webRoot.Path);
        using var client = CreateClient(api, host);

        using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(SpaMarker, html);
        Assert.Equal("no-cache", response.Headers.CacheControl?.ToString());
    }

    [Theory]
    [InlineData("app.localtest.me", "/api/no-existe")]
    [InlineData("empresa.localtest.me", "/api/no-existe")]
    [InlineData("empresa.localtest.me", "/account/no-existe")]
    [InlineData("empresa.localtest.me", "/connect/no-existe")]
    [InlineData("empresa.localtest.me", "/signin-google/no-existe")]
    [InlineData("empresa.localtest.me", "/.well-known/no-existe")]
    [InlineData("empresa.localtest.me", "/webhooks/no-existe")]
    [InlineData("empresa.localtest.me", "/health/no-existe")]
    [InlineData("empresa.localtest.me", "/alive/no-existe")]
    [InlineData("empresa.localtest.me", "/swagger/no-existe")]
    [InlineData("empresa.localtest.me", "/openapi/no-existe")]
    public async Task Backend_prefixes_return_problem_details_instead_of_spa(string host, string path)
    {
        using var webRoot = new TestWebRoot();
        await using var api = CreateApi(webRoot.Path);
        using var client = CreateClient(api, host);

        using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("Http.NotFound", problem.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Unknown_files_and_non_get_navigation_do_not_open_the_spa()
    {
        using var webRoot = new TestWebRoot();
        await using var api = CreateApi(webRoot.Path);
        using var client = CreateClient(api, "app.localtest.me");

        using var missingAsset = await client.GetAsync("/assets/missing.js", TestContext.Current.CancellationToken);
        using var post = await client.PostAsync("/login", content: null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, missingAsset.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, post.StatusCode);
        Assert.NotEqual("text/html", missingAsset.Content.Headers.ContentType?.MediaType);
        Assert.NotEqual("text/html", post.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Existing_static_file_is_served_as_a_file()
    {
        using var webRoot = new TestWebRoot();
        await using var api = CreateApi(webRoot.Path);
        using var client = CreateClient(api, "app.localtest.me");

        using var response = await client.GetAsync("/assets/main.js", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/javascript", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(AssetMarker, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Fallback_preserves_method_and_content_type_rejections()
    {
        using var webRoot = new TestWebRoot();
        await using var api = CreateApi(webRoot.Path);
        using var client = CreateClient(api, "app.localtest.me");
        using var plainText = new StringContent("{}", Encoding.UTF8, "text/plain");

        using var wrongMethod = await client.DeleteAsync("/test/body", TestContext.Current.CancellationToken);
        using var wrongContent = await client.PostAsync("/test/body", plainText, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.MethodNotAllowed, wrongMethod.StatusCode);
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, wrongContent.StatusCode);
    }

    [Theory]
    [InlineData("/swagger/index.html")]
    [InlineData("/openapi/v1.json")]
    public async Task Documentation_routes_exist_only_in_development(string path)
    {
        using var webRoot = new TestWebRoot();
        await using var testing = CreateApi(webRoot.Path);
        await using var development = CreateApi(webRoot.Path, "Development");
        using var testingClient = CreateClient(testing, "app.localtest.me");
        using var developmentClient = CreateClient(development, "app.localtest.me");

        using var hidden = await testingClient.GetAsync(path, TestContext.Current.CancellationToken);
        using var visible = await developmentClient.GetAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        Assert.Equal(HttpStatusCode.OK, visible.StatusCode);
    }

    private WebApplicationFactory<Program> CreateApi(string webRoot, string environment = "Testing") =>
        factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(environment);
            builder.UseWebRoot(webRoot);
        });

    private static HttpClient CreateClient(WebApplicationFactory<Program> api, string host) =>
        api.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri($"https://{host}"),
            AllowAutoRedirect = false,
        });

    private sealed class TestWebRoot : IDisposable
    {
        public string Path { get; } = Directory.CreateTempSubdirectory("multitenant-spa-").FullName;

        public TestWebRoot()
        {
            File.WriteAllText(System.IO.Path.Combine(Path, "index.html"), SpaMarker);
            Directory.CreateDirectory(System.IO.Path.Combine(Path, "assets"));
            File.WriteAllText(System.IO.Path.Combine(Path, "assets", "main.js"), AssetMarker);
        }

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
