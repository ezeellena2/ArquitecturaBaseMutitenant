using ArquitecturaBaseMultitenant.Domain.Results;
namespace ArquitecturaBaseMultitenant.Domain.WhatsApp;

public static class WhatsAppCodeErrors
{
    public const string DailyLimitCode = "WhatsApp.LoginCode.DailyLimit";
    /// <summary>Los códigos por WhatsApp que una cuenta pide desde Mi cuenta en las últimas 24 horas (B3).</summary>
    public const string AccountDailyLimitCode = "WhatsApp.LoginCode.AccountDailyLimit";

    public static Error AccountDailyLimit(int retryAfterSeconds) => Error.TooManyRequests(AccountDailyLimitCode,
        "The WhatsApp code limit of this account for the last 24 hours was reached.",
        new Dictionary<string, object?> { ["retryAfter"] = Math.Max(1, retryAfterSeconds) });

    public static Error DailyLimit(DateTime nowUtc) => Error.TooManyRequests(DailyLimitCode,
        "The daily WhatsApp code quota is exhausted.",
        new Dictionary<string, object?> { ["retryAfter"] = SecondsUntilNextDay(nowUtc) });

    /// <summary>Los segundos hasta que empieza el día UTC siguiente, con el ledger nuevo (al menos 1).</summary>
    public static int SecondsUntilNextDay(DateTime nowUtc)
    {
        var renewalUtc = DateOnly.FromDateTime(nowUtc).AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        return Math.Max(1, (int)Math.Ceiling((renewalUtc - nowUtc).TotalSeconds));
    }
}
