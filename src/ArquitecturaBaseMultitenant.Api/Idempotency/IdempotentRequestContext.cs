using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Idempotency;
using ArquitecturaBaseMultitenant.Application.Models.Idempotency;

namespace ArquitecturaBaseMultitenant.Api.Idempotency;

internal sealed class IdempotentRequestContext : IIdempotentRequestContext
{
    public IdempotentRequest? Request { get; internal set; }
    internal Guid? ReservationId { get; set; }
    internal bool HasReference { get; set; }
}
