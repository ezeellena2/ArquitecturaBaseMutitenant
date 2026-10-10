using System.Globalization;
using ArquitecturaBaseMultitenant.Api.ErrorHandling;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Contracts;

[Collection(ApiTestGroup.Name)]
public sealed class AssistantErrorContractTests(ApiFactory factory)
{
    [Theory]
    [InlineData("es-AR", "El archivo pesa más de 10 MB.")]
    [InlineData("en-US", "The file is larger than 10 MB.")]
    public void Module_errors_preserve_fields_status_and_localized_safe_detail(string culture, string detail)
    {
        using var scope = factory.Services.CreateScope();
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
            var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
            var mapper = scope.ServiceProvider.GetRequiredService<ProblemDetailsFactory>();
            var large = ProblemDetailsMapper.FromError(AssistantErrors.AttachmentTooLarge, mapper, context);
            Assert.Equal(413, large.Status);
            Assert.Equal(detail, large.Detail);
            Assert.Equal("Assistant.Attachment.TooLarge", large.Extensions["code"]);
            Assert.Contains("file", Assert.IsAssignableFrom<IDictionary<string, string[]>>(large.Extensions["errors"]).Keys);
            var own = ProblemDetailsMapper.FromError(AssistantErrors.SettingsOwnKeyNotAllowed, mapper, context);
            Assert.Equal(403, own.Status);
            Assert.Contains("mode", Assert.IsAssignableFrom<IDictionary<string, string[]>>(own.Extensions["errors"]).Keys);
            var controller = new ProbeController { ControllerContext = new ControllerContext { HttpContext = context }, ProblemDetailsFactory = mapper };
            var reset = new DateTime(2026, 10, 10, 0, 0, 0, DateTimeKind.Utc);
            var result = Result.Failure(AssistantErrors.LimitDailyReached(60, 50, reset)).ToActionResult(controller);
            var daily = Assert.IsType<ProblemDetails>(Assert.IsType<ObjectResult>(result).Value);
            Assert.Equal(429, daily.Status);
            Assert.Equal(50, daily.Extensions["dailyLimit"]);
            Assert.Equal(reset, daily.Extensions["dailyResetAtUtc"]);
            Assert.Equal("60", context.Response.Headers.RetryAfter.ToString());
            Result.Failure(AssistantErrors.ProviderUnavailableWithRetry(15)).ToActionResult(controller);
            Assert.Equal("15", context.Response.Headers.RetryAfter.ToString());
        }
        finally { CultureInfo.CurrentUICulture = previous; }
    }
    private sealed class ProbeController : ControllerBase;
}
