using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Collections.Concurrent;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.ErrorHandling;

/// <summary>
/// Comprueba que los errores del framework también respondan como ProblemDetails. Evita filtrar detalles
/// internos de validación o formato del cuerpo.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class FrameworkErrorsTests(ApiFactory factory)
{
    [Theory]
    [InlineData("application/json")]
    [InlineData("text/html")]
    public async Task Handled_exception_never_logs_its_message_even_with_an_unsupported_accept(string accept)
    {
        var logs = new ExceptionLogCapture();
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureLogging(logging => logging.AddProvider(logs)));
        using var client = host.CreateClient();
        logs.Entries.Clear();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/test/throw");
        request.Headers.Accept.ParseAdd(accept);
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.False(string.IsNullOrWhiteSpace(body.RootElement.GetProperty("traceId").GetString()));
        Assert.DoesNotContain(logs.Entries, entry => entry.Message.Contains("PRIVATE_TEST_EXCEPTION", StringComparison.Ordinal)
            || entry.Exception?.ToString().Contains("PRIVATE_TEST_EXCEPTION", StringComparison.Ordinal) == true);
        Assert.Contains(logs.Entries, entry => entry.Message.Contains(nameof(InvalidOperationException), StringComparison.Ordinal)
            && entry.Message.Contains("Throw", StringComparison.Ordinal));
    }

    private sealed class ExceptionLogCapture : ILoggerProvider
    {
        internal ConcurrentQueue<(string Message, Exception? Exception)> Entries { get; } = new();
        public ILogger CreateLogger(string categoryName) => new Capture(this);
        public void Dispose() { }
        private sealed class Capture(ExceptionLogCapture owner) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Warning;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter) => owner.Entries.Enqueue((formatter(state, exception), exception));
        }
    }

    [Theory]
    [InlineData("/api/does-not-exist", "GET", HttpStatusCode.NotFound, "Http.NotFound")]
    [InlineData("/test/validation", "DELETE", HttpStatusCode.MethodNotAllowed, "Http.MethodNotAllowed")]
    [InlineData("/test/status/401", "GET", HttpStatusCode.Unauthorized, "Http.Unauthorized")]
    [InlineData("/test/status/403", "GET", HttpStatusCode.Forbidden, "Http.Forbidden")]
    [InlineData("/test/status/429", "GET", HttpStatusCode.TooManyRequests, "Http.TooManyRequests")]
    public async Task Empty_framework_errors_are_problem_details(
        string path, string method, HttpStatusCode status, string code)
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        request.Headers.AcceptLanguage.Add(new StringWithQualityHeaderValue("en-US"));

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var problem = document.RootElement;

        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(code, problem.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("title").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("detail").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("traceId").GetString()));
    }

    [Theory]
    [InlineData("{", "application/json", HttpStatusCode.BadRequest)]
    [InlineData("{\"value\":42}", "application/json", HttpStatusCode.BadRequest)]
    [InlineData("{}", "text/plain", HttpStatusCode.UnsupportedMediaType)]
    public async Task Invalid_or_unsupported_body_has_no_model_state_details(
        string body, string contentType, HttpStatusCode status)
    {
        using var client = factory.CreateClient();
        using var content = new StringContent(body, Encoding.UTF8, contentType);

        using var response = await client.PostAsync("/test/body", content, TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var problem = document.RootElement;

        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("Request.Invalid", problem.GetProperty("code").GetString());
        Assert.False(problem.TryGetProperty("errors", out _));
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("traceId").GetString()));
    }

    [Fact]
    public async Task Empty_body_without_content_type_is_a_400_problem()
    {
        using var client = factory.CreateClient();
        using var content = new ByteArrayContent([]);

        using var response = await client.PostAsync("/test/body", content, TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Request.Invalid", document.RootElement.GetProperty("code").GetString());
        Assert.False(document.RootElement.TryGetProperty("errors", out _));
    }

    [Fact]
    public async Task Unhandled_exception_is_a_generic_500_without_internal_message()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/test/throw", TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(body);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("General.Unexpected", document.RootElement.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(document.RootElement.GetProperty("traceId").GetString()));
        Assert.DoesNotContain("PRIVATE_TEST_EXCEPTION", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Successful_empty_response_is_not_replaced_by_a_problem()
    {
        using var client = factory.CreateClient();
        using var content = new StringContent("{\"value\":\"ok\"}", Encoding.UTF8, "application/json");

        using var response = await client.PostAsync("/test/body", content, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
    }
}
