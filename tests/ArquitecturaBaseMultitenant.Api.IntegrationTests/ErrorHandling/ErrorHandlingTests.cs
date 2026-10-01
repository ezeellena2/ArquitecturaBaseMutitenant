using System.Net;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.ErrorHandling;

/// <summary>
/// Comprueba el status y el contrato ProblemDetails de cada tipo de error. Incluye errores por campo y la
/// traducción de fallas inesperadas.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class ErrorHandlingTests(ApiFactory factory)
{
    [Theory]
    [InlineData("Validation", HttpStatusCode.BadRequest, "Request.Invalid")]
    [InlineData("Unauthorized", HttpStatusCode.Unauthorized, "Http.Unauthorized")]
    [InlineData("Forbidden", HttpStatusCode.Forbidden, "Http.Forbidden")]
    [InlineData("NotFound", HttpStatusCode.NotFound, "Http.NotFound")]
    [InlineData("Conflict", HttpStatusCode.Conflict, "Http.Conflict")]
    [InlineData("TooManyRequests", HttpStatusCode.TooManyRequests, "Http.TooManyRequests")]
    [InlineData("Failure", HttpStatusCode.InternalServerError, "General.Unexpected")]
    public async Task Every_error_type_has_its_status_and_stable_problem_contract(
        string type, HttpStatusCode status, string code)
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync($"/test/result/{type}", TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var problem = document.RootElement;

        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(code, problem.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("traceId").GetString()));
        Assert.True(problem.GetProperty("title").GetString()!.Length > 0);
        Assert.True(problem.GetProperty("detail").GetString()!.Length > 0);
        if (status == HttpStatusCode.TooManyRequests)
        {
            Assert.Equal(12, problem.GetProperty("retryAfter").GetInt32());
        }
    }

    [Fact]
    public async Task Validation_errors_include_fields()
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/test/validation", TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Validation.Failed", document.RootElement.GetProperty("code").GetString());
        Assert.Equal("Ingresá un correo válido.",
            document.RootElement.GetProperty("errors").GetProperty("email")[0].GetString());
    }
}
