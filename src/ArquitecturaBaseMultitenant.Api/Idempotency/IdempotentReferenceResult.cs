using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Idempotency;
using ArquitecturaBaseMultitenant.Application.Models.Idempotency;
using Microsoft.AspNetCore.Mvc;

namespace ArquitecturaBaseMultitenant.Api.Idempotency;

/// <summary>Publishes a typed value and technical reference before JSON serialization.</summary>
public sealed class IdempotentReferenceResult(object value, IdempotentReference reference) : ObjectResult(value), IIdempotentReferencedResult
{
    public IdempotentReference Reference { get; } = reference;
    public override Task ExecuteResultAsync(ActionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        StatusCode = Reference.StatusCode;
        if (Reference.Location is { } location) context.HttpContext.Response.Headers.Location = location;
        return base.ExecuteResultAsync(context);
    }
}
