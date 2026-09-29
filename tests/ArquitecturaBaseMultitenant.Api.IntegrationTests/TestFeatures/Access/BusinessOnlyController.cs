using ArquitecturaBaseMultitenant.Api.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Users;
using Microsoft.AspNetCore.Mvc;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.BusinessAccess;

/// <summary>Prueba el acceso B2B sin crear una ruta productiva de alta de empresas.</summary>
[ApiController]
[Access(Access.Business)]
[Route("test/access/business-signup")]
public sealed class BusinessOnlyController : ControllerBase
{
    [HttpPost]
    public IActionResult Probe() => Ok();
}
