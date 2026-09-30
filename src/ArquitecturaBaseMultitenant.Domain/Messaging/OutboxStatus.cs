namespace ArquitecturaBaseMultitenant.Domain.Messaging;

public enum OutboxStatus
{
    Pending,
    Sent,
    Failed,
    Cancelled,
}
