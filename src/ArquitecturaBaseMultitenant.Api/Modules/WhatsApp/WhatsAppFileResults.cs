using ArquitecturaBaseMultitenant.Api.ErrorHandling;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Domain.Results;
using Microsoft.AspNetCore.Mvc;

namespace ArquitecturaBaseMultitenant.Api.Modules.WhatsApp;

internal static class WhatsAppFileResults
{
    internal static IActionResult ToFileResult(this Result<WhatsAppFileResponse> result, ControllerBase controller)
    {
        if (result.IsFailure) return result.ToActionResult(controller);
        controller.Response.Headers.CacheControl = "no-store";
        controller.Response.Headers.XContentTypeOptions = "nosniff";
        return controller.File(result.Value.Content, result.Value.MediaType, result.Value.FileName, enableRangeProcessing: false);
    }
}
