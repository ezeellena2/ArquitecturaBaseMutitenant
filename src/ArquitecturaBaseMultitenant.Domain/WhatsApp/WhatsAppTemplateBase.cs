using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ArquitecturaBaseMultitenant.Domain.Common;

namespace ArquitecturaBaseMultitenant.Domain.WhatsApp;

/// <summary>
/// Una plantilla que se manda a Meta para que la apruebe (ADR 0038). No se edita: una aprobada se duplica. El nombre que ve
/// la persona se guarda tal cual y el de Meta sale de él (minúsculas, sin acentos y con guiones bajos); la cultura es la del
/// catálogo (es-AR) y Meta la recibe con guion bajo (es_AR).
/// </summary>
public abstract partial class WhatsAppTemplateBase : Entity, IVersioned, IAuditable
{
    private protected List<WhatsAppTemplateVariable> _variables = [];

    public uint Version { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string MetaName { get; private set; } = string.Empty;
    public string Language { get; private set; } = string.Empty;
    public string MetaLanguage { get; private set; } = string.Empty;
    public WhatsAppTemplateCategory Category { get; private set; }
    public string Body { get; private set; } = string.Empty;
    public IReadOnlyList<WhatsAppTemplateVariable> Variables => _variables.AsReadOnly();
    public DateTime CreatedAtUtc { get; private set; }
    public Guid? CreatedBy { get; private set; }
    public DateTime? ModifiedAtUtc { get; private set; }
    public Guid? ModifiedBy { get; private set; }

    protected void Initialize(string name, string language, WhatsAppTemplateCategory category, string body, IEnumerable<WhatsAppTemplateVariable> variables)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(language);
        ArgumentException.ThrowIfNullOrWhiteSpace(body);
        var trimmed = name.Trim();
        if (trimmed.Length > TextLimits.ShortName) throw new ArgumentException("The name is too long.", nameof(name));
        if (category is not (WhatsAppTemplateCategory.Utility or WhatsAppTemplateCategory.Marketing)) throw new ArgumentOutOfRangeException(nameof(category));
        if (body.Length > WhatsAppTextLimits.TemplateBody) throw new ArgumentException("The body is too long.", nameof(body));
        var list = variables.ToList();
        if (list.Count != Placeholders(body) || list.Any(variable => variable is not { Label: { Length: > 0 and <= TextLimits.ShortName },
            Example: { Length: > 0 and <= WhatsAppTextLimits.TemplateVariable } })) throw new ArgumentException("Invalid variables.", nameof(variables));
        Name = trimmed; MetaName = ToMetaName(trimmed) ?? throw new ArgumentException("The name has no letters or digits.", nameof(name));
        Language = language.Trim(); MetaLanguage = ToMetaLanguage(Language); Category = category; Body = body; _variables = list;
    }

    /// <summary>
    /// Cuántos datos tiene el texto si están numerados desde {{1}} y sin saltear ninguno; -1 si no. Meta exige ese orden
    /// para los datos posicionales.
    /// </summary>
    public static int Placeholders(string body)
    {
        var numbers = PlaceholderPattern().Matches(body).Select(match => int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture)).ToList();
        var distinct = numbers.Distinct().Order().ToList();
        return distinct.Count == 0 ? 0 : distinct[0] == 1 && distinct[^1] == distinct.Count ? distinct.Count : -1;
    }

    /// <summary>El texto con los valores en lugar de {{1}}, {{2}}…: lo que queda en la conversación.</summary>
    public static string Render(string body, IReadOnlyList<string> values) => PlaceholderPattern().Replace(body, match =>
        int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) is var number && number >= 1 && number <= values.Count ? values[number - 1] : match.Value);

    /// <summary>El nombre para Meta (^[a-z0-9_]+$): null si no queda ninguna letra ni número.</summary>
    public static string? ToMetaName(string name)
    {
        var builder = new StringBuilder(name.Length);
        foreach (var character in name.Trim().Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark) continue;
            var lower = char.ToLowerInvariant(character);
            if (char.IsAsciiLetterLower(lower) || char.IsAsciiDigit(lower)) builder.Append(lower);
            else if (builder.Length > 0 && builder[^1] != '_') builder.Append('_');
        }
        var meta = builder.ToString().Trim('_');
        return meta.Length == 0 ? null : meta[..Math.Min(meta.Length, TextLimits.ShortName)];
    }

    public static string ToMetaLanguage(string culture) => culture.Trim().Replace('-', '_');

    /// <summary>El código de Meta como cultura del catálogo: Meta usa es_AR al crear y es-AR en algunos webhooks.</summary>
    public static string ToCulture(string metaLanguage) => metaLanguage.Trim().Replace('_', '-');

    [GeneratedRegex(@"\{\{\s*(\d{1,2})\s*\}\}", RegexOptions.CultureInvariant)]
    private static partial Regex PlaceholderPattern();
}

/// <summary>Un dato de la plantilla: cómo se llama y el ejemplo que Meta pide para revisarla.</summary>
public sealed record WhatsAppTemplateVariable(string Label, string Example);
