using ArquitecturaBaseMultitenant.Api.Authorization;
using ArquitecturaBaseMultitenant.Api.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Users;
using Microsoft.AspNetCore.Mvc;
using Microsoft.FeatureManagement.Mvc;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.Features;

[ApiController]
[Route("test/features")]
[Access(Access.Business)]
public sealed class FeatureProbeController : ControllerBase
{
    [HttpGet("business")]
    [FeatureGate("test-business")]
    public IActionResult Business() => Ok();

    [HttpGet("no-permission")]
    [FeatureGate("test-business")]
    [HasPermission("Test.Feature.Restricted")]
    public IActionResult Restricted() => Ok();

    [HttpGet("emergency")]
    [FeatureGate("test-emergency")]
    public IActionResult Emergency() => Ok();
}
