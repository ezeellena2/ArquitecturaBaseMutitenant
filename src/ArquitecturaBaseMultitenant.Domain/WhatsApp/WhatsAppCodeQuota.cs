using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Results;
namespace ArquitecturaBaseMultitenant.Domain.WhatsApp;

/// <summary>Ledger técnico global del día UTC; purgar una cuenta no devuelve capacidad.</summary>
public sealed class WhatsAppCodeQuota : Entity
{
    private WhatsAppCodeQuota() { }
    public string PhoneNumberId { get; private set; } = string.Empty;
    public DateOnly DayUtc { get; private set; }
    public int ReservedCount { get; private set; }
    public static WhatsAppCodeQuota Create(string phoneNumberId, DateOnly dayUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(phoneNumberId);
        return new WhatsAppCodeQuota { PhoneNumberId = phoneNumberId, DayUtc = dayUtc };
    }
    public Result Reserve(int limit, DateTime nowUtc)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);
        if (nowUtc.Kind != DateTimeKind.Utc || DateOnly.FromDateTime(nowUtc) != DayUtc)
            throw new ArgumentException("The quota requires its UTC day.", nameof(nowUtc));
        if (ReservedCount >= limit) return WhatsAppCodeErrors.DailyLimit(nowUtc);
        ReservedCount++;
        return Result.Success();
    }
}
