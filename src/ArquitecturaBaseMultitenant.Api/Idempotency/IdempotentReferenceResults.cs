using ArquitecturaBaseMultitenant.Api.ErrorHandling;
using ArquitecturaBaseMultitenant.Application.Models.Idempotency;
using ArquitecturaBaseMultitenant.Domain.Results;
using Microsoft.AspNetCore.Mvc;

namespace ArquitecturaBaseMultitenant.Api.Idempotency;

/// <summary>El resultado de una acción durable: el valor tipado con la referencia que completa la reserva.</summary>
internal static class IdempotentReferenceResults
{
    internal static IActionResult ToReferencedResult<T>(this Result<IdempotentReplayResponse<T>> result, ControllerBase controller) =>
        result.IsFailure ? result.ToActionResult(controller) : new IdempotentReferenceResult(result.Value.Value!, result.Value.Reference);
}
