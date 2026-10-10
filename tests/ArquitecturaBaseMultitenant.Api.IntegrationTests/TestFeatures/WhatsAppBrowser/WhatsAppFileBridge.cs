using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.Recorrido;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.WhatsAppBrowser;

/// <summary>Órdenes locales del recorrido; jamás publica controles HTTP.</summary>
public sealed class WhatsAppFileBridge
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly WhatsAppBrowserControl _control;
    private readonly string _commandFile;
    private int _pending;
    public WhatsAppFileBridge(IServiceProvider services, string readyFile, bool isolated)
    {
        if (!isolated || !services.GetRequiredService<IHostEnvironment>().IsEnvironment("Testing"))
            throw new InvalidOperationException("Private bridge requires isolated Testing.");
        RecorridoPaths.Validate(readyFile);
        _commandFile = Path.ChangeExtension(readyFile, ".whatsapp.json");
        _control = new WhatsAppBrowserControl(services);
    }

    public async Task<bool> TryExecuteAsync(HttpClient client, CancellationToken ct)
    {
        if (Interlocked.CompareExchange(ref _pending, 1, 0) != 0)
            throw new InvalidOperationException("Private bridge already has a pending command.");
        var done = _commandFile + ".done";
        var temporary = done + ".tmp";
        try
        {
            if (!File.Exists(_commandFile) || File.Exists(done)) return false;
            if ((File.GetAttributes(_commandFile) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException("Private bridge requires a regular command file.");
            await using var input = new FileStream(_commandFile, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (input.Length > 2048) throw new InvalidOperationException("Private bridge command is too large.");
            var request = await JsonSerializer.DeserializeAsync<WhatsAppBrowserRequest>(input, JsonOptions, ct)
                ?? throw new InvalidOperationException("Private bridge command missing.");
            var response = await _control.ExecuteAsync(request, client, ct);
            await using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await JsonSerializer.SerializeAsync(output, response, JsonOptions, ct);
                await output.FlushAsync(ct);
            }
            File.Move(temporary, done);
            return true;
        }
        finally
        {
            File.Delete(temporary);
            Volatile.Write(ref _pending, 0);
        }
    }
}
