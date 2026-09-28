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
    public async Task<string> FormatAsync(string type, JsonElement input, string culture, string timeZone,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        ArgumentException.ThrowIfNullOrWhiteSpace(culture);
        ArgumentException.ThrowIfNullOrWhiteSpace(timeZone);
        cancellationToken.ThrowIfCancellationRequested();

        var profile = await new CultureProfiles(cultures).LoadAsync(culture, cancellationToken);
        return type switch
        {
            "date" => FormatDate(input, profile, timeZone),
            "dateTime" => FormatDateTime(input, profile, timeZone),
            "time" => FormatTime(input, profile, timeZone),
            "dateLong" => FormatDateLong(input, profile, timeZone),
            "relative" => FormatRelative(input, profile, timeZone),
            "dateRange" => FormatDateRange(input, profile),
            "integer" => FormatNumber(input.GetDecimal(), 0, 0, profile),
            "decimal" => FormatFixedDecimal(input, profile),
            "quantity" => FormatNumber(input.GetDecimal(), 0, 3, profile),
            "percent" => FormatPercent(input, profile),
            "money" => await FormatMoneyAsync(input, profile, cancellationToken),
            "compact" => FormatCompact(input, profile),
            "fileSize" => FormatFileSize(input, profile),
            "duration" => FormatDuration(input, profile),
            "phone" => await FormatPhoneAsync(input, profile, cancellationToken),
            "timeZone" => await FormatTimeZoneAsync(input, profile, cancellationToken),
            "culture" => await FormatCultureAsync(input, profile, cancellationToken),
            "taxId" => await FormatTaxIdAsync(input, cancellationToken),
            "email" => FormatEmail(input),
            "enum" => FormatEnum(input, profile),
            "boolean" => FormattingTexts.Get(input.GetBoolean() ? "Boolean.True" : "Boolean.False", profile.Culture),
            "empty" => FormattingTexts.Get("Empty.Value", profile.Culture),
            "text" => input.GetString() ?? string.Empty,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown display format."),
        };
    }

    private string FormatDate(JsonElement input, CultureProfile profile, string timeZone) =>
        LocalDateTime(input, timeZone).ToString(profile.Entry.DatePattern, profile.Culture);

    private string FormatDateTime(JsonElement input, CultureProfile profile, string timeZone) =>
        LocalDateTime(input, timeZone).ToString(profile.Entry.DateTimePattern, profile.Culture);

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
            return FormattingTexts.Get("Relative.JustNow", profile.Culture);
        }

        var (count, key) = elapsed.TotalDays >= 1
            ? ((int)elapsed.TotalDays, "DaysAgo")
            : elapsed.TotalHours >= 1
                ? ((int)elapsed.TotalHours, "HoursAgo")
                : ((int)elapsed.TotalMinutes, "MinutesAgo");
        var template = FormattingTexts.Get($"Relative.{key}{(count == 1 ? "Singular" : "Plural")}", profile.Culture);
        return string.Format(profile.Culture, template, count);
    }

    private static string FormatDateRange(JsonElement input, CultureProfile profile)
    {
        var start = DateOnly.ParseExact(input.GetProperty("start").GetString()!, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        var end = DateOnly.ParseExact(input.GetProperty("end").GetString()!, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        return $"{start.ToString(profile.Entry.DatePattern, profile.Culture)} – {end.ToString(profile.Entry.DatePattern, profile.Culture)}";
    }

    private static string FormatFixedDecimal(JsonElement input, CultureProfile profile)
    {
        var digits = input.GetProperty("digits").GetInt32();
        if (digits is < 0 or > 28)
        {
            throw new ArgumentOutOfRangeException(nameof(input), "Decimal digits must be between 0 and 28.");
        }

        return FormatNumber(input.GetProperty("value").GetDecimal(), digits, digits, profile);
    }

    private static string FormatPercent(JsonElement input, CultureProfile profile)
    {
        var number = FormatNumber(input.GetDecimal() * 100m, 0, 2, profile);
        return profile.Entry.PercentPattern.Replace("{number}", number, StringComparison.Ordinal);
    }

    private async Task<string> FormatMoneyAsync(JsonElement input, CultureProfile profile, CancellationToken cancellationToken)
    {
        var code = input.GetProperty("currency").GetString()!;
        var currency = await currencies.FindAsync(code, cancellationToken)
            ?? throw new ArgumentException("The currency is not in the reference catalog.", nameof(input));
        var minorUnits = currency.MinorUnits
            ?? throw new InvalidOperationException("The currency has no numeric minor unit count.");
        var translation = profile.Translate(currency.Translations, item => item.Culture);
        var amount = input.GetProperty("amount").GetDecimal();
        var number = FormatNumber(Math.Abs(amount), minorUnits, minorUnits, profile);

        var pattern = profile.Entry.CurrencyPattern;
        if (pattern.Contains("{symbol}{number}", StringComparison.Ordinal) &&
            translation.DisplaySymbol.Length > 0 && char.IsLetter(translation.DisplaySymbol[^1]))
        {
            pattern = pattern.Replace("{symbol}{number}", "{symbol} {number}", StringComparison.Ordinal);
        }

        var positive = pattern.Replace("{symbol}", translation.DisplaySymbol, StringComparison.Ordinal)
            .Replace("{number}", number, StringComparison.Ordinal);
        return amount < 0 ? "-" + positive : positive;
    }

    private static string FormatCompact(JsonElement input, CultureProfile profile)
    {
        var value = input.GetDecimal();
        var magnitude = Math.Abs(value);
        var (divisor, unit) = magnitude >= 1_000_000_000m
            ? (1_000_000_000m, "Compact.Billion")
            : magnitude >= 1_000_000m
                ? (1_000_000m, "Compact.Million")
                : magnitude >= 1_000m
                    ? (1_000m, "Compact.Thousand")
                    : (1m, string.Empty);
        return FormatNumber(value / divisor, 0, divisor == 1m ? 0 : 1, profile)
            + (unit.Length == 0 ? string.Empty : FormattingTexts.Get(unit, profile.Culture));
    }

    private static string FormatFileSize(JsonElement input, CultureProfile profile)
    {
        var bytes = input.GetDecimal();
        var magnitude = Math.Abs(bytes);
        var (divisor, unit) = magnitude >= 1_000_000_000_000m
            ? (1_000_000_000_000m, "FileSize.TB")
            : magnitude >= 1_000_000_000m
                ? (1_000_000_000m, "FileSize.GB")
                : magnitude >= 1_000_000m
                    ? (1_000_000m, "FileSize.MB")
                    : magnitude >= 1_000m
                        ? (1_000m, "FileSize.KB")
                        : (1m, "FileSize.B");
        return FormatNumber(bytes / divisor, 0, divisor == 1m ? 0 : 1, profile)
            + FormattingTexts.Get(unit, profile.Culture);
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
            parts.Add(string.Format(profile.Culture, FormattingTexts.Get("Duration.Hours", profile.Culture), hours));
        }

        if (minutes > 0)
        {
            parts.Add(string.Format(profile.Culture, FormattingTexts.Get("Duration.Minutes", profile.Culture), minutes));
        }

        if (seconds > 0 || parts.Count == 0)
        {
            parts.Add(string.Format(profile.Culture, FormattingTexts.Get("Duration.Seconds", profile.Culture), seconds));
        }

        return string.Join(" ", parts);
    }

    private async Task<string> FormatPhoneAsync(JsonElement input, CultureProfile profile, CancellationToken cancellationToken)
    {
        var homeCountry = await countries.FindAsync(profile.Entry.CountryCode, cancellationToken)
            ?? throw new InvalidOperationException("The culture country is not in the reference catalog.");
        return phoneFormatter.Format(input.GetString()!, homeCountry.Code);
    }

    private async Task<string> FormatTimeZoneAsync(JsonElement input, CultureProfile profile, CancellationToken cancellationToken)
    {
        var id = input.GetString()!;
        var zone = await timeZones.FindAsync(id, cancellationToken)
            ?? throw new ArgumentException("The time zone is not in the reference catalog.", nameof(input));
        var city = profile.Translate(zone.Translations, item => item.Culture).City;
        var totalMinutes = (int)zoneService.GetUtcOffset(zone.Id).TotalMinutes;
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
        return FormattingTexts.Get(key, profile.Culture);
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
