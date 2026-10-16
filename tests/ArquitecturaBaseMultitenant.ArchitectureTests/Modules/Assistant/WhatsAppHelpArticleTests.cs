using System.Text.Json;

namespace ArquitecturaBaseMultitenant.ArchitectureTests.Modules.Assistant;

/// <summary>
/// D25 de la ADR 0048 para WhatsApp (decisión 14 de la E11): cada área del front con pantallas del módulo tiene su artículo
/// de ayuda en español y en inglés. Una pantalla nueva de WhatsApp sin su artículo rompe esta prueba. Vive con la ayuda del
/// asistente porque necesita los dos módulos: sale al quitar el asistente, y la guía de WhatsApp la retira con sus artículos.
/// </summary>
public sealed class WhatsAppHelpArticleTests
{
    /// <summary>El área de cada pantalla (sus primeros segmentos) y el artículo que la explica.</summary>
    private static readonly Dictionary<string, string> Areas = new(StringComparer.Ordinal)
    {
        ["/org/whatsapp/chats"] = "whatsapp.chats",
        ["/org/whatsapp/contactos"] = "whatsapp.contacts",
        ["/org/whatsapp/difusiones"] = "whatsapp.broadcasts",
        ["/org/whatsapp/numeros"] = "whatsapp.admin",
        ["/org/whatsapp/bot"] = "whatsapp.admin",
        ["/org/whatsapp/equipos"] = "whatsapp.admin",
        ["/org/whatsapp/plantillas"] = "whatsapp.admin",
        ["/plataforma/whatsapp"] = "whatsapp.platform.consultations",
        ["/plataforma/whatsapp/consultas"] = "whatsapp.platform.consultations",
        ["/plataforma/whatsapp/numero"] = "whatsapp.platform.admin",
        ["/plataforma/whatsapp/bot"] = "whatsapp.platform.admin",
        ["/plataforma/whatsapp/equipos"] = "whatsapp.platform.admin",
        ["/plataforma/whatsapp/plantillas"] = "whatsapp.platform.admin",
        ["/mensajes"] = "whatsapp.messages",
    };

    [Fact]
    public void Every_WhatsApp_screen_area_has_bilingual_help()
    {
        var routes = JsonSerializer.Deserialize<string[]>(File.ReadAllText(Path.Combine(SolutionRoot.FullPath, "docs", "contracts", "frontend-routes.json")))!;
        var areas = routes.Where(route => route.StartsWith("/org/whatsapp/", StringComparison.Ordinal) || route.StartsWith("/plataforma/whatsapp", StringComparison.Ordinal)
            || route == "/mensajes" || route.StartsWith("/mensajes/", StringComparison.Ordinal)).Select(Area).Distinct(StringComparer.Ordinal).ToArray();
        Assert.NotEmpty(areas);
        Assert.All(areas, area => Assert.True(Areas.ContainsKey(area), $"La pantalla {area} necesita su artículo de ayuda."));

        var articles = Path.Combine(SolutionRoot.FullPath, "src", "ArquitecturaBaseMultitenant.Infrastructure", "Modules", "Assistant", "Help", "Articles");
        using var index = JsonDocument.Parse(File.ReadAllText(Path.Combine(articles, "index.json")));
        foreach (var topic in areas.Select(area => Areas[area]).Distinct(StringComparer.Ordinal))
        {
            var entry = index.RootElement.EnumerateArray().SingleOrDefault(row => row.GetProperty("topicKey").GetString() == topic);
            Assert.True(entry.ValueKind == JsonValueKind.Object, $"Falta el artículo {topic} en index.json.");
            foreach (var language in new[] { "es", "en" })
            {
                Assert.False(string.IsNullOrWhiteSpace(entry.GetProperty("titles").GetProperty(language).GetString()));
                Assert.True(File.Exists(Path.Combine(articles, entry.GetProperty("files").GetProperty(language).GetString()!)), $"Falta {topic} en {language}.");
            }
        }
    }

    private static string Area(string route)
    {
        var segments = route.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var take = segments[0] == "mensajes" ? 1 : Math.Min(segments.Length, 3);
        return "/" + string.Join('/', segments.Take(take));
    }
}
