using System.Text.Json;

namespace ArquitecturaBaseMultitenant.RealE2ESetup;

/// <summary>
/// Cada orden privada del recorrido corre en el bucle del host aislado. Una orden que falla le contesta a quien la pidió con
/// su rechazo, en su archivo de respuesta (<c>{ rejected, detail }</c>), y el bucle sigue con la próxima (B-4 de la puerta de
/// la base 2): antes una excepción de una sola orden cortaba el host y la API del entorno quedaba en 502.
/// </summary>
internal static class PrivateCommandGuard
{
    private const int MaxCommandBytes = 2048;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Una orden que escribe su propia respuesta: si falla, contesta el rechazo por ella.</summary>
    public static async Task RunAsync(string readyFile, string suffix, Func<Task> command, CancellationToken ct)
    {
        try { await command(); }
        catch (Exception exception) when (exception is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            await RejectAsync(readyFile, suffix, exception, ct);
        }
    }

    /// <summary>Una orden que solo hace su trabajo: lee el pedido, lo corre y contesta lo que devuelve, o el rechazo.</summary>
    public static Task ExecuteAsync(string readyFile, string suffix, Func<JsonElement, CancellationToken, Task<object>> execute,
        CancellationToken ct) => RunAsync(readyFile, suffix, async () =>
    {
        var (commandFile, doneFile) = Files(readyFile, suffix);
        if (!File.Exists(commandFile) || File.Exists(doneFile)) return;
        if ((File.GetAttributes(commandFile) & FileAttributes.ReparsePoint) != 0 || new FileInfo(commandFile).Length > MaxCommandBytes)
            throw new InvalidOperationException("A private command requires a bounded regular file.");
        using var command = JsonDocument.Parse(await File.ReadAllTextAsync(commandFile, ct));
        await AnswerAsync(doneFile, await execute(command.RootElement, ct), ct);
    }, ct);

    /// <summary>
    /// La respuesta de una orden que falló: el tipo y, si es una de las esperables, su frase técnica, que nunca lleva un
    /// correo, un código ni un token. Sin pedido o ya contestado no hay a quién contestar.
    /// </summary>
    private static async Task RejectAsync(string readyFile, string suffix, Exception exception, CancellationToken ct)
    {
        var type = exception.GetType().Name;
        Console.Error.WriteLine($"E2E private command {suffix} rejected ({type}).");
        var (commandFile, doneFile) = Files(readyFile, suffix);
        if (!File.Exists(commandFile) || File.Exists(doneFile)) return;
        var detail = exception is InvalidOperationException or ArgumentException or JsonException or KeyNotFoundException or FormatException
            ? exception.Message : null;
        try { await AnswerAsync(doneFile, new { rejected = type, detail }, ct); }
        catch (IOException) { Console.Error.WriteLine($"E2E private command {suffix} could not be answered."); }
    }

    private static async Task AnswerAsync(string doneFile, object response, CancellationToken ct)
    {
        var temporary = doneFile + ".tmp";
        try
        {
            await using (var output = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await JsonSerializer.SerializeAsync(output, response, Json, ct);
                await output.FlushAsync(ct);
            }
            File.Move(temporary, doneFile);
        }
        finally { File.Delete(temporary); }
    }

    private static (string Command, string Done) Files(string readyFile, string suffix)
    {
        var command = Path.ChangeExtension(readyFile, $".{suffix}.json");
        return (command, command + ".done");
    }
}
