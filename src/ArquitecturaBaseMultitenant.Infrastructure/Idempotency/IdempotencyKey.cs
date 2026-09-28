namespace ArquitecturaBaseMultitenant.Infrastructure.Idempotency;

internal sealed class IdempotencyKey
{
    private IdempotencyKey()
    {
        BodyHash = string.Empty;
        Route = string.Empty;
    }

    public Guid Id { get; private set; }
    public Guid? TenantId { get; private set; }
    public Guid UserId { get; private set; }
    public Guid Key { get; private set; }
    public string BodyHash { get; private set; }
    public string Route { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }
    public int? ResponseStatusCode { get; private set; }
    public string? ResponseBody { get; private set; }
}
