using ArquitecturaBaseMultitenant.Api.ErrorHandling;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Domain.Results;
using Microsoft.AspNetCore.Mvc;

namespace ArquitecturaBaseMultitenant.Api.Modules.Assistant;

internal static class AssistantFileResults
{
    internal static IActionResult ToFileResult(this Result<AssistantFileResponse> result, ControllerBase controller)
    {
        if (result.IsFailure) return result.ToActionResult(controller);
        controller.Response.Headers.CacheControl = "no-store";
        controller.Response.Headers.XContentTypeOptions = "nosniff";
        return controller.File(result.Value.Content, result.Value.MediaType, result.Value.FileName, enableRangeProcessing: false);
    }
}
