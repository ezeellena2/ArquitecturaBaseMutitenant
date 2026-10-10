using ArquitecturaBaseMultitenant.Api.ErrorHandling;
using ArquitecturaBaseMultitenant.Api.Idempotency;
using ArquitecturaBaseMultitenant.Application.Models.Idempotency;
using ArquitecturaBaseMultitenant.Domain.Results;
using Microsoft.AspNetCore.Mvc;

namespace ArquitecturaBaseMultitenant.Api.Modules.Assistant;

internal static class AssistantReferenceResults
{
    internal static IActionResult ToReferencedResult<T>(this Result<IdempotentReplayResponse<T>> result, ControllerBase controller) =>
        result.IsFailure ? result.ToActionResult(controller) : new IdempotentReferenceResult(result.Value.Value!, result.Value.Reference);
}
