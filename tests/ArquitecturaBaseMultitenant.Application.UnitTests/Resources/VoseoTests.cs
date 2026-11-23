using System.Globalization;
using System.Text.RegularExpressions;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.Resources;

/// <summary>
/// textos-y-traducciones.md: español rioplatense con voseo, y «Organización», nunca «tenant». Es el espejo de
/// <c>src/locales/vocabulary.test.ts</c> del front, con la misma lista y la misma regla. Un texto que tutea se reconoce por
/// las formas que el voseo no usa: el presente de «tú» («puedes» y no «podés»), «tú», «contigo» y «para ti», los
/// imperativos irregulares («haz», «ten», «pon») y los imperativos con el pronombre pegado («inténtalo» y no «intentalo»,
/// «asegúrate» y no «asegurate»). El imperativo regular de «tú» se escribe igual que el presente de «él» («Revisa»,
/// «Elige»), que las descripciones usan («Crea roles y los asigna»): cuenta como tuteo solo al empezar una oración que le
/// habla a la persona («Revisa tu correo») o que pide repetir («Intenta de nuevo»); el voseo lleva la tilde.
/// </summary>
public sealed partial class VoseoTests
{
    private const string Tuteo =
        "puedes|tienes|quieres|debes|eres|necesitas|sabes|vienes|prefieres|recuerdas|piensas|encuentras|vuelves|conoces|"
        + @"escribes|eliges|pides|entiendes|pierdes|usas|haces|dices|tú|contigo|(?:para|a|de|en|por|sin|sobre|con)\s+ti|haz|ten|pon|"
        + "asegúrate|fíjate|acuérdate|inténtalo|revísalo|revísala|elígelo|elígela|guárdalo|guárdala|ingrésalo|ingrésala|escríbelo|"
        + "confírmalo|pruébalo|cámbialo|ábrelo|cópialo|agrégalo|búscalo|complétalo|descárgalo|selecciónalo|selecciónala";

    private const string Imperatives =
        "Ingresa|Elige|Selecciona|Confirma|Escribe|Revisa|Intenta|Vuelve|Agrega|Busca|Completa|Guarda|Prueba|Toca|Cambia|"
        + "Copia|Abre|Cierra|Recuerda|Verifica|Descarga|Carga|Sube|Acepta|Crea|Edita|Espera|Usa|Mira|Lee|Pide|Sigue|Olvida|"
        + "Indica|Borra|Envía|Manda";

    [Fact]
    public void Reads_the_core_and_module_texts_in_both_languages()
    {
        var spanish = ResourceTexts.Of(CultureInfo.InvariantCulture);
        var english = ResourceTexts.Of(CultureInfo.GetCultureInfo("en"));

        Assert.True(spanish.Count > 500, $"Solo se leyeron {spanish.Count} textos en español.");
        Assert.True(english.Count > 500, $"Solo se leyeron {english.Count} textos en inglés.");
        Assert.Contains(spanish, text => text.Resource.Contains(".Modules.", StringComparison.Ordinal));
    }

    [Fact]
    public void Spanish_texts_speak_with_voseo()
    {
        var offenders = ResourceTexts.Of(CultureInfo.InvariantCulture)
            .Where(text => Tutea(text.Value))
            .Select(text => $"{text.Resource} · {text.Key}: «{text.Value}»");

        Assert.Empty(offenders);
    }

    [Fact]
    public void No_text_says_tenant_in_either_language()
    {
        var offenders = ResourceTexts.Of(CultureInfo.InvariantCulture)
            .Concat(ResourceTexts.Of(CultureInfo.GetCultureInfo("en")))
            .Where(text => SaysTenant(text.Value))
            .Select(text => $"{text.Resource} · {text.Key}: «{text.Value}»");

        Assert.Empty(offenders);
    }

    [Theory]
    [InlineData("¿Puedes revisar esto?")]
    [InlineData("Si tienes una cuenta, ingresá.")]
    [InlineData("Lo elegiste tú.")]
    [InlineData("Es para ti")]
    [InlineData("Haz clic acá")]
    [InlineData("Asegúrate de guardar")]
    [InlineData("Inténtalo de nuevo")]
    [InlineData("Revisa tu correo.")]
    [InlineData("Te mandamos un código. Ingresa el código que te llegó.")]
    [InlineData("Intenta de nuevo")]
    [InlineData("Elige quién te atiende")]
    [InlineData("No se pudo. Vuelve a intentarlo más tarde, por favor te pedimos.")]
    public void Detector_finds_the_tuteo(string text) => Assert.True(Tutea(text), text);

    [Theory]
    [InlineData("¿Podés revisar esto?")]
    [InlineData("Revisá tu correo.")]
    [InlineData("Intentá de nuevo")]
    [InlineData("Asegurate de guardar")]
    [InlineData("Intentalo de nuevo")]
    [InlineData("Vuelve a entrar como persona y en sus organizaciones.")]
    [InlineData("Sigue en la organización.")]
    [InlineData("Crea roles y los asigna.")]
    [InlineData("Activa")]
    [InlineData("Prueba hasta {0}")]
    [InlineData("Espera aprobación")]
    [InlineData("Dale el rol a otra persona.")]
    [InlineData("Te mandamos un código a {destination}.")]
    [InlineData("Lo ve el Dueño.")]
    [InlineData("La lista completa")]
    [InlineData("Contenido")]
    public void Detector_does_not_confuse_voseo_or_a_description_with_tuteo(string text) => Assert.False(Tutea(text), text);

    [Theory]
    [InlineData("El tenant no existe.", true)]
    [InlineData("Tenants of the platform", true)]
    [InlineData("TENANT", true)]
    [InlineData("La organización {tenantName} está de baja.", false)]
    [InlineData("La organización no existe.", false)]
    public void Detector_finds_tenant_outside_the_placeholders(string text, bool tenant) =>
        Assert.Equal(tenant, SaysTenant(text));

    private static bool Tutea(string text) =>
        TuteoWord().IsMatch(text)
        || SentenceBreak().Split(text).Select(sentence => sentence.Trim())
            .Any(sentence => (Imperative().IsMatch(sentence) && ToYou().IsMatch(sentence)) || Again().IsMatch(sentence));

    // Un placeholder no se muestra: «{tenantName}» lo reemplaza el nombre de la organización.
    private static bool SaysTenant(string text) => Tenant().IsMatch(Placeholder().Replace(text, string.Empty));

    [GeneratedRegex(@"(?<!\p{L})(?:" + Tuteo + @")(?!\p{L})", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TuteoWord();

    [GeneratedRegex(@"^[¿¡""«]?(?:" + Imperatives + @")(?!\p{L})", RegexOptions.CultureInvariant)]
    private static partial Regex Imperative();

    [GeneratedRegex(@"(?<!\p{L})(?:tu|tus|te|ti|tuyo|tuya|tuyos|tuyas)(?!\p{L})", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ToYou();

    [GeneratedRegex(@"^[¿¡]?(?:" + Imperatives + @")\s+(?:de nuevo|otra vez|más tarde|nuevamente)(?!\p{L})",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Again();

    [GeneratedRegex(@"(?<=[.!?])\s+|\n", RegexOptions.CultureInvariant)]
    private static partial Regex SentenceBreak();

    [GeneratedRegex("tenant", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Tenant();

    [GeneratedRegex(@"\{[^{}]*\}", RegexOptions.CultureInvariant)]
    private static partial Regex Placeholder();
}
