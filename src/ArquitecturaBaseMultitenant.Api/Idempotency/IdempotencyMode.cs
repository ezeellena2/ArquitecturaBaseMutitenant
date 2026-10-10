

namespace ArquitecturaBaseMultitenant.Api.Idempotency;

public enum IdempotencyMode
{
    BufferedJson,
    DurableReference,
    DurableStream,
}
