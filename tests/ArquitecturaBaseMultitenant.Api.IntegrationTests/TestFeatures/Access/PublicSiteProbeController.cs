using ArquitecturaBaseMultitenant.Api.Tenancy;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Domain.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.BusinessAccess;

/// <summary>Expone ambos contextos para demostrar que el host nunca elige el acceso privado.</summary>
[ApiController]
[Route("test/access")]
public sealed class PublicSiteProbeController(IPublicSiteContext publicSite, ITenantContext tenant) : ControllerBase
{
    [HttpGet("public-site")]
    [PublicSite]
    [AllowAnonymous]
    public IActionResult PublicSite() => Ok(new
    {
        publicSite.BusinessTenantId,
        publicSite.Slug,
        privateTenantId = tenant.TenantId,
    });

    [HttpGet("private-context")]
    [Access(Access.Business)]
    public IActionResult PrivateContext() => Ok(new
    {
        tenant.TenantId,
        publicSite.BusinessTenantId,
    });
}
