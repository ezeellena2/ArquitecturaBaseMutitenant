using System.Data.Common;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.WhatsAppBrowser;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;

namespace ArquitecturaBaseMultitenant.RealE2ESetup;

/// <summary>
/// Órdenes del E2E real de Catálogos (E12, §15), que el control de WhatsApp y la pantalla no dan. «contactInbound» escribe al
/// número de la organización como un contacto, firmado como Meta y, si se pide, con el nombre de su perfil (81); lo procesa
/// después el control (<c>orgProcess</c>). «mark» y «explain» toman las lecturas de las opciones que hizo la API entre las dos
/// órdenes y las pasan por EXPLAIN ANALYZE con el recorrido secuencial apagado, como <c>FormCatalogVolumeTests</c>: ninguna
/// recorre toda la tabla ni trae más de una tanda.
/// </summary>
internal sealed partial class FormsCommand(FormsItemCapture capture, HttpClient client, string adminConnectionString)
{
    private const int LargestRead = 1_000;
    private const int MaxProfileName = 60;
    private const int MaxText = 200;

    public async Task<object> ExecuteAsync(JsonElement command, CancellationToken ct) => Text(command, "kind") switch
    {
        "contactInbound" => await ContactInboundAsync(command, ct),
        "mark" => Mark(),
        "explain" => await ExplainAsync(command, ct),
        _ => throw new InvalidOperationException("Unsupported isolated forms command."),
    };

    private object Mark()
    {
        capture.Arm();
        return new { marked = true };
    }

    /// <summary>Lo que escribe un contacto al número que conectó el control para ese escenario, con el nombre de su perfil.</summary>
    private async Task<object> ContactInboundAsync(JsonElement command, CancellationToken ct)
    {
        var scenario = Text(command, "scenario");
        var phone = Text(command, "phone");
        var text = Text(command, "text");
        var profileName = command.TryGetProperty("profileName", out var profile) && profile.ValueKind == JsonValueKind.String ? profile.GetString() : null;
        if (!ScenarioPattern().IsMatch(scenario)) throw new InvalidOperationException("The forms scenario is not valid.");
        if (!PhonePattern().IsMatch(phone)) throw new InvalidOperationException("The contact phone must be a synthetic fixture number.");
        if (text.Length is 0 or > MaxText || text.Any(char.IsControl)) throw new InvalidOperationException("The inbound text is not valid.");
        if (profileName is not null && (profileName.Length is 0 or > MaxProfileName || profileName.Any(char.IsControl)))
            throw new InvalidOperationException("The profile name is not valid.");

        var from = phone[1..];
        var message = new Dictionary<string, object>
        {
            ["from"] = from,
            ["id"] = "wamid.fixture.forms." + Guid.NewGuid().ToString("N"),
            ["timestamp"] = TimeProvider.System.GetUtcNow().ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
            ["type"] = "text",
            ["text"] = new { body = text },
        };
        var value = new Dictionary<string, object>
        {
            ["messaging_product"] = "whatsapp",
            ["metadata"] = new
            {
                display_phone_number = WhatsAppBrowserControl.OrganizationDisplayNumber[1..],
                phone_number_id = WhatsAppBrowserControl.OrganizationNumberPrefix + scenario,
            },
            ["messages"] = new[] { message },
        };
        if (profileName is not null) value["contacts"] = new[] { new { profile = new { name = profileName }, wa_id = from } };
        var payload = JsonSerializer.Serialize(new
        {
            @object = "whatsapp_business_account",
            entry = new[] { new { id = WhatsAppBrowserControl.OrganizationWabaId, changes = new[] { new { field = "messages", value } } } },
        });
        using var webhook = new HttpRequestMessage(HttpMethod.Post, new Uri("/webhooks/whatsapp", UriKind.Relative))
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
        };
        var signature = HMACSHA256.HashData(Encoding.UTF8.GetBytes(WhatsAppBrowserControl.AppSecret), Encoding.UTF8.GetBytes(payload));
        webhook.Headers.Add("X-Hub-Signature-256", "sha256=" + Convert.ToHexStringLower(signature));
        using var response = await client.SendAsync(webhook, ct);
        return new { status = (int)response.StatusCode };
    }

    /// <summary>
    /// El plan de cada lectura de las opciones desde «mark», con la organización en el alcance y el recorrido secuencial apagado:
    /// cuántas se leyeron, cuántas recorren toda la tabla y la tanda más grande. Antes actualiza las estadísticas de la tabla,
    /// como las deja el autovacuum después de una carga grande.
    /// </summary>
    private async Task<object> ExplainAsync(JsonElement command, CancellationToken ct)
    {
        var tenantId = Guid.Parse(Text(command, "tenantId"));
        var reads = capture.Reads();
        var itemUpdates = capture.ItemUpdates();
        capture.Disarm();
        await using var connection = new NpgsqlConnection(adminConnectionString);
        await connection.OpenAsync(ct);
        await using (var analyze = new NpgsqlCommand("""ANALYZE tenant."FormCatalogItems" """, connection))
            await analyze.ExecuteNonQueryAsync(ct);
        await using (var setup = new NpgsqlCommand("SET enable_seqscan = off; SELECT set_config('app.tenant_id', @tenant, false)", connection))
        {
            setup.Parameters.AddWithValue("tenant", tenantId.ToString("D"));
            await setup.ExecuteNonQueryAsync(ct);
        }
        var sequential = 0;
        var largest = 0d;
        foreach (var read in reads)
        {
            await using var explain = new NpgsqlCommand("EXPLAIN (ANALYZE, FORMAT JSON) " + read.Sql, connection);
            foreach (var (name, parameter) in read.Parameters) explain.Parameters.AddWithValue(name, parameter);
            var plan = (string)(await explain.ExecuteScalarAsync(ct))!;
            using var document = JsonDocument.Parse(plan);
            var root = document.RootElement[0].GetProperty("Plan");
            if (SequentialOnItems(root)) sequential++;
            largest = Math.Max(largest, root.GetProperty("Actual Rows").GetDouble());
        }
        return new { reads = reads.Count, sequential, largestRows = largest, bounded = sequential == 0 && largest <= LargestRead,
            itemUpdates };
    }

    private static bool SequentialOnItems(JsonElement node) =>
        node.GetProperty("Node Type").GetString() == "Seq Scan" && node.TryGetProperty("Relation Name", out var relation)
            && relation.GetString() == "FormCatalogItems"
        || node.TryGetProperty("Plans", out var children) && children.EnumerateArray().Any(SequentialOnItems);

    private static string Text(JsonElement command, string name) =>
        command.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && value.GetString() is { } text
            ? text : throw new InvalidOperationException($"The forms command requires {name}.");

    [GeneratedRegex("^[A-Za-z0-9_-]{1,40}$")]
    private static partial Regex ScenarioPattern();

    [GeneratedRegex(@"^\+54911345678\d{2}$")]
    private static partial Regex PhonePattern();
}

/// <summary>Un comando de la API que tocó las opciones: su texto y sus parámetros, para volver a pedir su plan.</summary>
internal sealed record FormsCapturedCommand(string Sql, (string Name, object Value)[] Parameters);

/// <summary>
/// Guarda los comandos de la API que tocan las opciones, solo entre «mark» y «explain»: las lecturas para su plan y las
/// escrituras para contarlas. Fuera de ese tramo no guarda nada, así una importación grande no llena la memoria.
/// </summary>
internal sealed class FormsItemCapture : DbCommandInterceptor
{
    private readonly List<FormsCapturedCommand> _commands = [];
    private bool _armed;

    internal void Arm()
    {
        lock (_commands)
        {
            _commands.Clear();
            _armed = true;
        }
    }

    internal void Disarm()
    {
        lock (_commands)
        {
            _commands.Clear();
            _armed = false;
        }
    }

    /// <summary>Las lecturas de una sola sentencia: las que se pueden volver a planear tal cual.</summary>
    internal IReadOnlyList<FormsCapturedCommand> Reads()
    {
        lock (_commands)
            return [.. _commands.Where(command => command.Sql.TrimStart().StartsWith("SELECT", StringComparison.Ordinal)
                && !command.Sql.TrimEnd().TrimEnd(';').Contains(';', StringComparison.Ordinal))];
    }

    /// <summary>Cuántas sentencias actualizaron una opción: guardar una escribe solo esa.</summary>
    internal int ItemUpdates()
    {
        lock (_commands)
            return _commands.Where(command => !command.Sql.TrimStart().StartsWith("SELECT", StringComparison.Ordinal))
                .SelectMany(command => command.Sql.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                .Count(statement => statement.StartsWith("UPDATE tenant.\"FormCatalogItems\"", StringComparison.Ordinal));
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
        InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        Capture(command);
        return ValueTask.FromResult(result);
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Capture(command);
        return ValueTask.FromResult(result);
    }

    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(DbCommand command, CommandEventData eventData,
        InterceptionResult<object> result, CancellationToken cancellationToken = default)
    {
        Capture(command);
        return ValueTask.FromResult(result);
    }

    private void Capture(DbCommand command)
    {
        if (!command.CommandText.Contains("\"FormCatalogItems\"", StringComparison.Ordinal)) return;
        lock (_commands)
            if (_armed) _commands.Add(new(command.CommandText, [.. command.Parameters.Cast<DbParameter>()
                .Select(parameter => (parameter.ParameterName, parameter.Value ?? DBNull.Value))]));
    }
}
