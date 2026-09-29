using System.ComponentModel.DataAnnotations;
using ArquitecturaBaseMultitenant.Application.Common.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.Isolation;

/// <summary>Ruta de prueba del contrato de edición y del error de concurrencia.</summary>
[ApiController]
[Route("test/widgets")]
public sealed class WidgetsController : ControllerBase
{
    [HttpPut("{id:guid}")]
    public IActionResult Update(Guid id, [FromBody] WidgetUpdateHttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(HttpContext);
        ArgumentNullException.ThrowIfNull(request);
        throw new ConcurrencyConflictException();
    }
}

public sealed record WidgetUpdateHttpRequest(string? Name, [param: Required] uint? Version);
