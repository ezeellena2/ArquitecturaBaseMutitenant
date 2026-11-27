using ArquitecturaBaseMultitenant.Api.ErrorHandling;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Models;
using ArquitecturaBaseMultitenant.Domain.Results;
using Microsoft.AspNetCore.Mvc;

namespace ArquitecturaBaseMultitenant.Api.Modules.Forms;

/// <summary>La exportación y la plantilla de los catálogos (C10): el archivo sin guardarse en el navegador ni adivinar su tipo.</summary>
internal static class FormCatalogFileResults
{
    internal static IActionResult ToFileResult(this Result<FormCatalogFileResponse> result, ControllerBase controller)
    {
        if (result.IsFailure) return result.ToActionResult(controller);
        controller.Response.Headers.CacheControl = "no-store";
        controller.Response.Headers.XContentTypeOptions = "nosniff";
        return controller.File(result.Value.Content, result.Value.MediaType, result.Value.FileName, enableRangeProcessing: false);
    }
}
