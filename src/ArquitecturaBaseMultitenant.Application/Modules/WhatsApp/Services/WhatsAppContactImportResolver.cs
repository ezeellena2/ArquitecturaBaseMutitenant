using System.Globalization;
using System.Text;
using ArquitecturaBaseMultitenant.Application.Common.Formatting;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Phones;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.ReferenceData;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Resources;
using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Services;

/// <summary>
/// Lee la planilla de contactos y decide fila por fila, sin guardar. Las columnas se reconocen por su encabezado en cualquier
/// cultura habilitada del catálogo; el teléfono se interpreta con el país de la organización; sin «Sí» queda «Sin
/// respuesta». Una fila rechazada lleva su número en el archivo (el encabezado es la 1) y el motivo.
/// </summary>
internal sealed class WhatsAppContactImportResolver(IWhatsAppContactSpreadsheet spreadsheet, IPhoneNumberParser phones,
    ITenantSettingsReader settings, ICultureCatalog cultures)
{
    private const string NameHeader = "Import.Header.Name";
    private const string PhoneHeader = "Import.Header.Phone";
    private const string TagHeader = "Import.Header.Tag";
    private const string AcceptsHeader = "Import.Header.Accepts";

    internal async Task<Result<Plan>> ResolveAsync(ImportWhatsAppContactsRequest request, CancellationToken ct)
    {
        var sheet = await spreadsheet.ReadAsync(request.FileName, request.Content!, WhatsAppFeature.MaxImportRows, ct);
        if (sheet is null) return WhatsAppErrors.ImportInvalidFile;
        if (sheet.Rows.Count > WhatsAppFeature.MaxImportRows) return WhatsAppErrors.ImportTooManyRows;
        var words = await WordsAsync(ct);
        var headers = sheet.Headers.Select(Normalize).ToArray();
        int Column(string key) => Array.FindIndex(headers, header => words[key].Contains(header));
        var (name, phone, tag, accepts) = (Column(NameHeader), Column(PhoneHeader), Column(TagHeader), Column(AcceptsHeader));
        if (phone < 0) return WhatsAppErrors.ImportInvalidFile;
        var country = (await settings.FindCurrentAsync(ct))?.DefaultCulture is { } culture
            ? (await cultures.FindAsync(culture, ct))?.CountryCode : null;
        var extraTag = string.IsNullOrWhiteSpace(request.Tag) ? null : request.Tag.Trim();
        var rows = new List<Row>();
        var rejected = new List<WhatsAppImportRejectionResponse>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in sheet.Rows)
        {
            string? Cell(int index) => index >= 0 && index < row.Cells.Count && !string.IsNullOrWhiteSpace(row.Cells[index]) ? row.Cells[index]!.Trim() : null;
            if (row.Cells.All(string.IsNullOrWhiteSpace)) continue;
            var parsed = phones.Parse(country, Cell(phone), PhoneUsage.Mobile);
            if (parsed.IsFailure) { rejected.Add(new(row.Number, WhatsAppTexts.Get("WhatsApp.Import.Reason.InvalidPhone"))); continue; }
            if (!seen.Add(parsed.Value.Value)) { rejected.Add(new(row.Number, WhatsAppTexts.Get("WhatsApp.Import.Reason.Duplicate"))); continue; }
            var tags = new[] { Cell(tag), extraTag }.OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            var contactName = Cell(name);
            if (contactName?.Length > TextLimits.PersonDisplayName || tags.Any(value => value.Length > WhatsAppTextLimits.Tag))
            { rejected.Add(new(row.Number, WhatsAppTexts.Get("WhatsApp.Import.Reason.TooLong"))); continue; }
            rows.Add(new(parsed.Value, contactName, tags, Cell(accepts) is { } answer && words["Import.Yes"].Contains(Normalize(answer))));
        }
        return new Plan(sheet.Rows.Count, rows, rejected);
    }

    /// <summary>Los encabezados y el «Sí» de cada cultura habilitada, sin acentos ni mayúsculas.</summary>
    private async Task<IReadOnlyDictionary<string, HashSet<string>>> WordsAsync(CancellationToken ct)
    {
        var words = new[] { NameHeader, PhoneHeader, TagHeader, AcceptsHeader, "Import.Yes" }
            .ToDictionary(key => key, _ => new HashSet<string>(StringComparer.Ordinal));
        foreach (var entry in (await cultures.ListAsync(ct)).Where(entry => entry.IsEnabled))
        {
            var profile = await new CultureProfiles(cultures).LoadAsync(entry.Code, ct);
            foreach (var (key, values) in words) values.Add(Normalize(WhatsAppTexts.Get(key, profile)));
        }
        return words;
    }

    private static string Normalize(string? value)
    {
        var decomposed = (value ?? string.Empty).Trim().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark) builder.Append(char.ToLowerInvariant(character));
        return builder.ToString();
    }

    /// <summary>Lo que haría la importación: las filas válidas y las rechazadas, con la cantidad de filas leídas.</summary>
    internal sealed record Plan(int Rows, IReadOnlyList<Row> Valid, IReadOnlyList<WhatsAppImportRejectionResponse> Rejected);

    internal sealed record Row(PhoneNumber WaId, string? Name, IReadOnlyList<string> Tags, bool AcceptsPromotions)
    {
        public override string ToString() => nameof(Row);
    }
}
