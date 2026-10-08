using ArquitecturaBaseMultitenant.Domain.Results;
namespace ArquitecturaBaseMultitenant.Domain.WhatsApp;
public static class WhatsAppCodeErrors
{
    public const string DailyLimitCode = "WhatsApp.LoginCode.DailyLimit";
    public static Error DailyLimit(DateTime nowUtc)
    {
        var renewalUtc = DateOnly.FromDateTime(nowUtc).AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        return Error.TooManyRequests(DailyLimitCode, "The daily WhatsApp code quota is exhausted.",
            new Dictionary<string, object?> { ["retryAfter"] = Math.Max(1, (int)Math.Ceiling((renewalUtc - nowUtc).TotalSeconds)) });
    }
}
