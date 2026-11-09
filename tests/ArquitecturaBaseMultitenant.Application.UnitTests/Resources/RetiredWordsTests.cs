using System.Collections;
using System.Globalization;
using System.Resources;
using System.Text.RegularExpressions;
using ArquitecturaBaseMultitenant.Application.Resources;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.Resources;

/// <summary>
/// Las palabras de la base de la 12 en todos los textos en español, módulos incluidos: dejar de usar algo sin borrarlo es
/// «Dar de baja» y «Reactivar», con el estado «De baja», y borrar es «Borrar», con «Por borrar» para la cuenta propia.
/// Salen «Deshabilitar», «Inactivar», «Desactivar», «Eliminar», «Baja pedida» y «Volver a sumar».
/// </summary>
public sealed partial class RetiredWordsTests
{
    [Fact]
    public void Spanish_texts_do_not_use_the_retired_words()
    {
        var assembly = typeof(ErrorTexts).Assembly;
        var resources = assembly.GetManifestResourceNames()
            .Where(name => name.EndsWith(".resources", StringComparison.Ordinal))
            .Select(name => name[..^".resources".Length])
            .ToArray();
        Assert.Contains(resources, name => name.EndsWith(".Errors", StringComparison.Ordinal));

        var offenders = resources
            .SelectMany(name => Spanish(new ResourceManager(name, assembly)).Select(entry => (Resource: name, entry.Key, entry.Value)))
            .Where(entry => Retired().IsMatch(entry.Value))
            .Select(entry => $"{entry.Resource} · {entry.Key}: {entry.Value}");

        Assert.Empty(offenders);
    }

    [Theory]
    [InlineData("Deshabilitó a {0}", true)]
    [InlineData("La cuenta está inactiva.", true)]
    [InlineData("Desactivar la empresa", true)]
    [InlineData("Tu cuenta fue eliminada", true)]
    [InlineData("Baja pedida por la plataforma", true)]
    [InlineData("Volver a sumar a la empresa", true)]
    [InlineData("Dio de baja a {0}", false)]
    [InlineData("Tu cuenta está por borrarse.", false)]
    [InlineData("WhatsApp no está habilitado para ese país.", false)]
    public void Detector_finds_the_retired_words_and_keeps_the_new_ones(string text, bool retired) =>
        Assert.Equal(retired, Retired().IsMatch(text));

    private static IEnumerable<(string Key, string Value)> Spanish(ResourceManager resources)
    {
        using var set = resources.GetResourceSet(CultureInfo.InvariantCulture, createIfNotExists: true, tryParents: false);
        if (set is null) yield break;
        foreach (DictionaryEntry entry in set)
            if (entry.Value is string value) yield return ((string)entry.Key, value);
    }

    [GeneratedRegex(@"\b(?:deshabilit|inactiv|desactiv|elimin)\w*|\bbaja pedida\b|\bvolver a sumar\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Retired();
}
