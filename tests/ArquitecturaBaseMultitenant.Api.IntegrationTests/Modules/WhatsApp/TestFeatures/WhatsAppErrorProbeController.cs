using ArquitecturaBaseMultitenant.Api.ErrorHandling;
using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures;
[ApiController, Route("test/whatsapp-errors"), AllowAnonymous]
public sealed class WhatsAppErrorProbeController(ProblemDetailsFactory factory) : ControllerBase
{
    [HttpGet("{kind}")]
    public IActionResult Get(string kind)
    {
        Error error = kind == "country" ? FieldErrors.On(Error.Validation("WhatsApp.Phone.CountryNotAllowed", "Country is unavailable."), "phone")
            : WhatsAppCodeErrors.DailyLimit(TimeProvider.System.GetUtcNow().UtcDateTime);
        var problem = ProblemDetailsMapper.FromError(error, factory, HttpContext);
        return StatusCode(problem.Status!.Value, problem);
    }
}
