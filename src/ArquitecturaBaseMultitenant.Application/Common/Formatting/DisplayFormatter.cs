using System.Globalization;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Phones;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Time;
using ArquitecturaBaseMultitenant.Application.Interfaces.ReferenceData;
using ArquitecturaBaseMultitenant.Application.Resources;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.Common.Formatting;

/// <summary>Único formateador de datos para mensajes y documentos emitidos por el backend.</summary>
public sealed class DisplayFormatter(
    ICurrencyCatalog currencies,
    ICountryCatalog countries,
    ITimeZoneCatalog timeZones,
    ICultureCatalog cultures,
    ITaxIdTypeCatalog taxIdTypes,
    IPhoneNumberDisplayFormatter phoneFormatter,
    ITimeZoneService zoneService,
    TimeProvider clock)
{
    internal static string FormatResource(string template, CultureProfile profile, params object[] args) =>
        string.Format(profile.Culture, template, args);

    private static readonly (decimal Divisor, string Unit)[] CompactUnits =
    [
        (1m, string.Empty),
        (1_000m, "Compact.Thousand"),
        (1_000_000m, "Compact.Million"),
        (1_000_000_000m, "Compact.Billion"),
    ];

    private static readonly (decimal Divisor, string Unit)[] FileSizeUnits =
    [
        (1m, "FileSize.B"),
        (1_000m, "FileSize.KB"),
        (1_000_000m, "FileSize.MB"),
        (1_000_000_000m, "FileSize.GB"),
        (1_000_000_000_000m, "FileSize.TB"),
    ];

    public async Task<DisplayFormatContext> CreateAsync(string culture, string timeZone,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(culture);
        ArgumentException.ThrowIfNullOrWhiteSpace(timeZone);
        cancellationToken.ThrowIfCancellationRequested();
        return new DisplayFormatContext(
            await new CultureProfiles(cultures).LoadAsync(culture, cancellationToken), timeZone);
    }

    public string FormatInstant(DateTime instantUtc, DisplayFormatContext context)
    {
        return LocalInstant(instantUtc, context)
            .ToString(context.Profile.Entry.DateTimePattern, context.Profile.Culture);
    }

    public string FormatDate(DateTime instantUtc, DisplayFormatContext context) =>
        LocalInstant(instantUtc, context)
            .ToString(context.Profile.Entry.DatePattern, context.Profile.Culture);

    public string FormatTime(DateTime instantUtc, DisplayFormatContext context) =>
        LocalInstant(instantUtc, context)
            .ToString(context.Profile.Entry.TimePattern, context.Profile.Culture);

    private DateTime LocalInstant(DateTime instantUtc, DisplayFormatContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (instantUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("The instant must be UTC.", nameof(instantUtc));
        }

        return zoneService.ConvertToLocal(instantUtc, context.TimeZoneId);
    }

    // Keep one instance API for callers composing sync and async formatted fields.
#pragma warning disable CA1822
    public string FormatDecimal(decimal value, int digits, DisplayFormatContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (digits is < 0 or > 28)
        {
            throw new ArgumentOutOfRangeException(nameof(digits), "Decimal digits must be between 0 and 28.");
        }

        return FormatNumber(value, digits, digits, context.Profile);
    }

    public string FormatPercent(decimal fraction, DisplayFormatContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var number = FormatNumber(fraction * 100m, 0, 2, context.Profile);
        return context.Profile.Entry.PercentPattern.Replace("{number}", number, StringComparison.Ordinal);
    }
#pragma warning restore CA1822

    public async Task<string> FormatMoneyAsync(Money value, DisplayFormatContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(context);
        var currency = await currencies.FindAsync(value.Currency.Value, cancellationToken)
            ?? throw new ArgumentException("The currency is not in the reference catalog.", nameof(value));
        var minorUnits = currency.MinorUnits
            ?? throw new InvalidOperationException("The currency has no numeric minor unit count.");
        var translation = context.Profile.Translate(currency.Translations, item => item.Culture);
        var roundedAmount = Math.Round(value.Amount, minorUnits, MidpointRounding.AwayFromZero);
        var number = FormatNumber(Math.Abs(roundedAmount), minorUnits, minorUnits, context.Profile);

        var pattern = context.Profile.Entry.CurrencyPattern;
        if (pattern.Contains("{symbol}{number}", StringComparison.Ordinal) &&
            translation.DisplaySymbol.Length > 0 && char.IsLetter(translation.DisplaySymbol[^1]))
        {
            pattern = pattern.Replace("{symbol}{number}", "{symbol} {number}", StringComparison.Ordinal);
        }

        var positive = pattern.Replace("{symbol}", translation.DisplaySymbol, StringComparison.Ordinal)
            .Replace("{number}", number, StringComparison.Ordinal);
        return roundedAmount < 0 ? "-" + positive : positive;
    }

    public async Task<string> FormatPhoneAsync(PhoneNumber value, DisplayFormatContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(context);
        var homeCountry = await countries.FindAsync(context.Profile.Entry.CountryCode, cancellationToken)
            ?? throw new InvalidOperationException("The culture country is not in the reference catalog.");
        return phoneFormatter.Format(value.Value, homeCountry.Code);
    }

    /// <summary>Adaptador exclusivo del contrato compartido de format-cases.json.</summary>
    internal async Task<string> FormatAsync(string type, JsonElement input, string culture, string timeZone,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        var context = await CreateAsync(culture, timeZone, cancellationToken);
        var profile = context.Profile;
        return type switch
        {
            "date" => FormatDate(input, profile, timeZone),
            "dateTime" => FormatInstant(ParseInstant(input.GetString()!), context),
            "time" => FormatTime(input, profile, timeZone),
            "dateLong" => FormatDateLong(input, profile, timeZone),
            "relative" => FormatRelative(input, profile, timeZone),
            "dateRange" => FormatDateRange(input, profile),
            "integer" => FormatNumber(input.GetDecimal(), 0, 0, profile),
            "decimal" => FormatDecimal(input.GetProperty("value").GetDecimal(),
                input.GetProperty("digits").GetInt32(), context),
            "quantity" => FormatNumber(input.GetDecimal(), 0, 3, profile),
            "percent" => FormatPercent(input.GetDecimal(), context),
            "money" => await FormatMoneyAsync(ParseMoney(input), context, cancellationToken),
            "compact" => FormatCompact(input, profile),
            "fileSize" => FormatFileSize(input, profile),
            "duration" => FormatDuration(input, profile),
            "phone" => await FormatPhoneAsync(ParsePhone(input), context, cancellationToken),
            "timeZone" => await FormatTimeZoneAsync(input, profile, cancellationToken),
            "culture" => await FormatCultureAsync(input, profile, cancellationToken),
            "taxId" => await FormatTaxIdAsync(input, cancellationToken),
            "email" => FormatEmail(input),
            "enum" => FormatEnum(input, profile),
            "boolean" => FormattingTexts.Get(input.GetBoolean() ? "Boolean.True" : "Boolean.False", profile),
            "empty" => FormattingTexts.Get("Empty.Value", profile),
            "text" => input.GetString() ?? string.Empty,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown display format."),
        };
    }

    private string FormatDate(JsonElement input, CultureProfile profile, string timeZone) =>
        LocalDateTime(input, timeZone).ToString(profile.Entry.DatePattern, profile.Culture);

    private string FormatTime(JsonElement input, CultureProfile profile, string timeZone) =>
        LocalDateTime(input, timeZone).ToString(profile.Entry.TimePattern, profile.Culture);

    private string FormatDateLong(JsonElement input, CultureProfile profile, string timeZone) =>
        LocalDateTime(input, timeZone).ToString(profile.Entry.LongDatePattern, profile.Culture);

    private string FormatRelative(JsonElement input, CultureProfile profile, string timeZone)
    {
        var instantUtc = ParseInstant(input.GetString()!);
        var elapsed = clock.GetUtcNow().UtcDateTime - instantUtc;
        if (elapsed < TimeSpan.Zero || elapsed >= TimeSpan.FromDays(7))
        {
            return zoneService.ConvertToLocal(instantUtc, timeZone)
                .ToString(profile.Entry.DatePattern, profile.Culture);
        }

        if (elapsed < TimeSpan.FromMinutes(1))
        {
            return FormattingTexts.Get("Relative.JustNow", profile);
        }

        var (count, key) = elapsed.TotalDays >= 1
            ? ((int)elapsed.TotalDays, "DaysAgo")
            : elapsed.TotalHours >= 1
                ? ((int)elapsed.TotalHours, "HoursAgo")
                : ((int)elapsed.TotalMinutes, "MinutesAgo");
        var template = FormattingTexts.Get($"Relative.{key}{(count == 1 ? "Singular" : "Plural")}", profile);
        return string.Format(profile.Culture, template, count);
    }

    private static string FormatDateRange(JsonElement input, CultureProfile profile)
    {
        var start = DateOnly.ParseExact(input.GetProperty("start").GetString()!, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        var end = DateOnly.ParseExact(input.GetProperty("end").GetString()!, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        return $"{start.ToString(profile.Entry.DatePattern, profile.Culture)} – {end.ToString(profile.Entry.DatePattern, profile.Culture)}";
    }

    private static Money ParseMoney(JsonElement input)
    {
        var currency = CurrencyCode.Create(input.GetProperty("currency").GetString());
        if (currency.IsFailure)
        {
            throw new ArgumentException("The currency code is invalid.", nameof(input));
        }

        return new Money(input.GetProperty("amount").GetDecimal(), currency.Value);
    }

    private static PhoneNumber ParsePhone(JsonElement input)
    {
        var phone = PhoneNumber.Create(input.GetString());
        return phone.IsSuccess ? phone.Value
            : throw new ArgumentException("The phone number is invalid.", nameof(input));
    }

    private static string FormatCompact(JsonElement input, CultureProfile profile)
    {
        var value = input.GetDecimal();
        var (rounded, unit, digits) = SelectRoundedUnit(value, CompactUnits);
        return FormatNumber(rounded, 0, digits, profile)
            + (unit.Length == 0 ? string.Empty : FormattingTexts.Get(unit, profile));
    }

    private static string FormatFileSize(JsonElement input, CultureProfile profile)
    {
        var bytes = input.GetDecimal();
        var (rounded, unit, digits) = SelectRoundedUnit(bytes, FileSizeUnits);
        return FormatNumber(rounded, 0, digits, profile)
            + FormattingTexts.Get(unit, profile);
    }

    private static (decimal Rounded, string Unit, int Digits) SelectRoundedUnit(
        decimal value, (decimal Divisor, string Unit)[] units)
    {
        var magnitude = Math.Abs(value);
        var index = 0;
        while (index + 1 < units.Length && magnitude >= units[index + 1].Divisor)
        {
            index++;
        }

        while (true)
        {
            var digits = index == 0 ? 0 : 1;
            var rounded = Math.Round(value / units[index].Divisor, digits, MidpointRounding.AwayFromZero);
            if (index + 1 < units.Length && Math.Abs(rounded) >= 1_000m)
            {
                index++;
                continue;
            }

            return (rounded == 0m ? 0m : rounded, units[index].Unit, digits);
        }
    }

    private static string FormatDuration(JsonElement input, CultureProfile profile)
    {
        var totalSeconds = input.GetInt64();
        if (totalSeconds < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(input), "Duration cannot be negative.");
        }

        var hours = totalSeconds / 3600;
        var minutes = totalSeconds % 3600 / 60;
        var seconds = totalSeconds % 60;
        var parts = new List<string>();
        if (hours > 0)
        {
            parts.Add(string.Format(profile.Culture, FormattingTexts.Get("Duration.Hours", profile), hours));
        }

        if (minutes > 0)
        {
            parts.Add(string.Format(profile.Culture, FormattingTexts.Get("Duration.Minutes", profile), minutes));
        }

        if (seconds > 0 || parts.Count == 0)
        {
            parts.Add(string.Format(profile.Culture, FormattingTexts.Get("Duration.Seconds", profile), seconds));
        }

        return string.Join(" ", parts);
    }

    private async Task<string> FormatTimeZoneAsync(JsonElement input, CultureProfile profile, CancellationToken cancellationToken)
    {
        var id = input.GetString()!;
        var zone = await timeZones.FindAsync(id, cancellationToken)
            ?? throw new ArgumentException("The time zone is not in the reference catalog.", nameof(input));
        var city = profile.Translate(zone.Translations, item => item.Culture).City;
        var totalMinutes = (int)zoneService.GetUtcOffset(zone.Id, clock.GetUtcNow().UtcDateTime).TotalMinutes;
        var sign = totalMinutes < 0 ? '−' : '+';
        var absolute = Math.Abs(totalMinutes);
        var hours = (absolute / 60).ToString(CultureInfo.InvariantCulture);
        var minutes = absolute % 60;
        var offset = minutes == 0 ? hours : hours + ":" + minutes.ToString("00", CultureInfo.InvariantCulture);
        return $"{city} (GMT{sign}{offset})";
    }

    private async Task<string> FormatCultureAsync(JsonElement input, CultureProfile profile, CancellationToken cancellationToken)
    {
        var code = input.GetString()!;
        var entry = await cultures.FindAsync(code, cancellationToken)
            ?? throw new ArgumentException("The culture is not in the reference catalog.", nameof(input));
        return profile.Translate(entry.Translations, item => item.DisplayCulture).Name;
    }

    private async Task<string> FormatTaxIdAsync(JsonElement input, CancellationToken cancellationToken)
    {
        var code = input.GetProperty("type").GetString()!;
        var entry = await taxIdTypes.FindAsync(code, cancellationToken)
            ?? throw new ArgumentException("The tax ID type is not in the reference catalog.", nameof(input));
        var number = input.GetProperty("number").GetString()!;
        var position = 0;
        var output = new System.Text.StringBuilder(entry.Mask.Length);
        foreach (var character in entry.Mask)
        {
            if (character == '9')
            {
                if (position >= number.Length || !char.IsAsciiDigit(number[position]))
                {
                    throw new ArgumentException("The tax ID number does not match its mask.", nameof(input));
                }

                output.Append(number[position++]);
            }
            else
            {
                output.Append(character);
            }
        }

        if (position != number.Length)
        {
            throw new ArgumentException("The tax ID number does not match its mask.", nameof(input));
        }

        return output.ToString();
    }

    private static string FormatEmail(JsonElement input)
    {
        var email = Email.Create(input.GetString());
        return email.IsSuccess ? email.Value.DisplayValue : throw new ArgumentException("The email is invalid.", nameof(input));
    }

    private static string FormatEnum(JsonElement input, CultureProfile profile)
    {
        var key = "Enum." + input.GetProperty("enum").GetString() + "." + input.GetProperty("value").GetString();
        return FormattingTexts.Get(key, profile);
    }

    private DateTime LocalDateTime(JsonElement input, string timeZone)
    {
        var value = input.GetString()!;
        if (DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var civil))
        {
            return civil.ToDateTime(TimeOnly.MinValue);
        }

        if (TimeOnly.TryParseExact(value, "HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
        {
            return new DateOnly(2000, 1, 1).ToDateTime(time);
        }

        return zoneService.ConvertToLocal(ParseInstant(value), timeZone);
    }

    private static DateTime ParseInstant(string value)
    {
        if (!value.EndsWith('Z'))
        {
            throw new FormatException("An instant must use the UTC designator Z.");
        }

        return DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind).UtcDateTime;
    }

    private static string FormatNumber(decimal value, int minimumDigits, int maximumDigits, CultureProfile profile)
    {
        var rounded = Math.Round(value, maximumDigits, MidpointRounding.AwayFromZero);
        var pattern = "#,0" + (maximumDigits == 0
            ? string.Empty
            : "." + new string('0', minimumDigits) + new string('#', maximumDigits - minimumDigits));
        return rounded.ToString(pattern, profile.Numbers);
    }
}
