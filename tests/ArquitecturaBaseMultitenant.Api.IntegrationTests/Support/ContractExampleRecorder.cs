using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;

/// <summary>
/// Graba las respuestas de una corrida real en <c>docs/contracts/examples/&lt;archivo&gt;</c> (api-http, desde la E12), con la forma que
/// valida el front con zod estricto: <c>{ "examples": [{ name, method, path, status, body? }] }</c>, con la ruta como la escribe
/// openapi.json. Lo que cambia entre corridas queda fijo para que dos corridas graben lo mismo: cada id pasa a uno numerado por orden
/// de aparición, cada fecha a una sola, cada <c>version</c> a un número por orden de aparición y el <c>traceId</c> a uno fijo.
/// </summary>
internal sealed partial class ContractExampleRecorder(string fileName)
{
    private const string FixedDate = "2026-11-27T12:00:00Z";
    private const string FixedTraceId = "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01";

    private static readonly JsonSerializerOptions Indented = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly JsonArray _examples = [];
    private readonly Dictionary<Guid, Guid> _ids = [];
    private readonly Dictionary<long, long> _versions = [];
    private readonly List<(string From, string To)> _texts = [];

    /// <summary>Un texto de la corrida que cambia cada vez (el nombre de una cuenta nueva) y se graba con uno fijo.</summary>
    internal void Replace(string from, string to) => _texts.Add((from, to));

    /// <summary>Graba la respuesta y devuelve su cuerpo tal como llegó, para seguir la corrida.</summary>
    internal async Task<JsonElement> RecordAsync(string name, string method, string path, HttpResponseMessage response,
        CancellationToken ct)
    {
        var text = await response.Content.ReadAsStringAsync(ct);
        var example = new JsonObject
        {
            ["name"] = name,
            ["method"] = method,
            ["path"] = path,
            ["status"] = (int)response.StatusCode,
        };
        JsonElement body = default;
        if (text.Length > 0 && response.Content.Headers.ContentType?.MediaType?.EndsWith("json", StringComparison.Ordinal) == true)
        {
            using var document = JsonDocument.Parse(text);
            body = document.RootElement.Clone();
            example["body"] = Normalize(JsonNode.Parse(text), null);
        }
        _examples.Add(example);
        return body;
    }

    internal string Render() =>
        new JsonObject { ["examples"] = _examples.DeepClone() }.ToJsonString(Indented).ReplaceLineEndings("\n") + "\n";

    /// <summary>
    /// Escribe el archivo y falla si cambió: lo publicado tiene que ser lo que graba la corrida de hoy. Después de un cambio de
    /// contrato, la corrida lo vuelve a grabar y alcanza con revisarlo y commitearlo.
    /// </summary>
    internal void Publish()
    {
        var path = Path.Combine(RepositoryRoot(), "docs", "contracts", "examples", fileName);
        var rendered = Render();
        var published = File.Exists(path) ? File.ReadAllText(path).ReplaceLineEndings("\n") : null;
        if (published == rendered) return;
        File.WriteAllText(path, rendered);
        Assert.Fail($"docs/contracts/examples/{fileName} cambió con esta corrida: quedó grabado, revisalo y commitealo.");
    }

    private JsonNode? Normalize(JsonNode? node, string? property)
    {
        switch (node)
        {
            case JsonObject value:
                foreach (var key in value.Select(pair => pair.Key).ToArray())
                    value[key] = Normalize(value[key]?.DeepClone(), key);
                return value;
            case JsonArray value:
                for (var index = 0; index < value.Count; index++)
                    value[index] = Normalize(value[index]?.DeepClone(), property);
                return value;
            case JsonValue value when value.TryGetValue<string>(out var text):
                if (property == "traceId") return FixedTraceId;
                if (Guid.TryParseExact(text, "D", out var id)) return Stable(id).ToString("D");
                if (DateTimeShape().IsMatch(text)) return FixedDate;
                return _texts.Aggregate(text, (current, pair) => current.Replace(pair.From, pair.To, StringComparison.Ordinal));
            case JsonValue value when property == "version" && value.TryGetValue<long>(out var version):
                if (!_versions.TryGetValue(version, out var stable)) _versions[version] = stable = _versions.Count + 1;
                return stable;
            default:
                return node;
        }
    }

    private Guid Stable(Guid id)
    {
        if (!_ids.TryGetValue(id, out var stable))
            _ids[id] = stable = Guid.ParseExact("00000000-0000-4000-8000-" + (_ids.Count + 1).ToString("D12", CultureInfo.InvariantCulture), "D");
        return stable;
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ArquitecturaBaseMultitenant.slnx"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d+)?Z$")]
    private static partial Regex DateTimeShape();
}
