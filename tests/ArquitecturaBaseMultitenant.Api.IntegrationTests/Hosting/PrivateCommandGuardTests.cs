using System.Text.Json;
using ArquitecturaBaseMultitenant.RealE2ESetup;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Hosting;

/// <summary>
/// B-4 de la puerta de la base 2: las órdenes privadas del recorrido corren en el bucle del host aislado. Una orden que falla
/// (una segunda <c>recent-proof</c> sin prueba vigente) le contesta su rechazo a quien la pidió y el bucle sigue con la
/// próxima, en lugar de cortar el host y dejar la API del entorno en 502. Sin base: solo los archivos de la orden.
/// </summary>
public sealed class PrivateCommandGuardTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("mt-e2e-guard-").FullName;
    private string Ready => Path.Combine(_directory, "mt-e2e-guard.ready");
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_failing_order_answers_its_rejection_and_the_next_order_still_runs()
    {
        await File.WriteAllTextAsync(Command("recent-proof"), """{"kind":"expire","email":"e2e-a3-operator@example.test"}""", Ct);
        await File.WriteAllTextAsync(Command("delivery"), """{"kind":"invitation","email":"x@example.test","outcome":"Uncertain"}""", Ct);
        JsonElement? received = null;

        // Un tick del bucle: la primera orden falla, la segunda corre igual.
        await PrivateCommandGuard.RunAsync(Ready, "recent-proof",
            () => throw new InvalidOperationException("An isolated platform session with recent proof is required."), Ct);
        await PrivateCommandGuard.ExecuteAsync(Ready, "delivery", (command, _) =>
        {
            received = command.Clone();
            return Task.FromResult<object>(new { outcome = "Uncertain" });
        }, Ct);

        using (var rejected = await DoneAsync("recent-proof"))
        {
            Assert.Equal("InvalidOperationException", rejected.RootElement.GetProperty("rejected").GetString());
            Assert.Equal("An isolated platform session with recent proof is required.", rejected.RootElement.GetProperty("detail").GetString());
        }
        Assert.Equal("invitation", received?.GetProperty("kind").GetString());
        using var answered = await DoneAsync("delivery");
        Assert.Equal("Uncertain", answered.RootElement.GetProperty("outcome").GetString());
        Assert.False(answered.RootElement.TryGetProperty("rejected", out _));
        Assert.Empty(Directory.EnumerateFiles(_directory, "*.tmp"));
    }

    [Fact]
    public async Task An_unexpected_failure_names_only_its_type_and_an_answered_order_is_not_answered_again()
    {
        await File.WriteAllTextAsync(Command("whatsapp"), """{"kind":"code"}""", Ct);
        await PrivateCommandGuard.RunAsync(Ready, "whatsapp", () => throw new IOException(@"C:\secret\e2e-person@example.test"), Ct);
        using (var rejected = await DoneAsync("whatsapp"))
        {
            Assert.Equal("IOException", rejected.RootElement.GetProperty("rejected").GetString());
            Assert.Equal(JsonValueKind.Null, rejected.RootElement.GetProperty("detail").ValueKind);
        }
        var first = await File.ReadAllTextAsync(Command("whatsapp") + ".done", Ct);

        // La orden ya tiene respuesta: un fallo más no la pisa; sin pedido no hay a quién contestar.
        await PrivateCommandGuard.RunAsync(Ready, "whatsapp", () => throw new InvalidOperationException("again"), Ct);
        Assert.Equal(first, await File.ReadAllTextAsync(Command("whatsapp") + ".done", Ct));
        await PrivateCommandGuard.RunAsync(Ready, "platform", () => throw new InvalidOperationException("no order"), Ct);
        Assert.False(File.Exists(Command("platform") + ".done"));
        await PrivateCommandGuard.ExecuteAsync(Ready, "assistant", (_, _) => throw new InvalidOperationException("never"), Ct);
        Assert.False(File.Exists(Command("assistant") + ".done"));
    }

    [Fact]
    public async Task An_oversized_order_is_rejected_without_running_and_stopping_the_host_still_cancels()
    {
        await File.WriteAllTextAsync(Command("delivery"), $$"""{"kind":"{{new string('a', 2100)}}"}""", Ct);
        var ran = false;
        await PrivateCommandGuard.ExecuteAsync(Ready, "delivery", (_, _) => { ran = true; return Task.FromResult<object>(new { }); }, Ct);
        Assert.False(ran);
        using (var rejected = await DoneAsync("delivery"))
            Assert.Equal("InvalidOperationException", rejected.RootElement.GetProperty("rejected").GetString());

        using var stopping = new CancellationTokenSource();
        await stopping.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => PrivateCommandGuard.RunAsync(Ready, "legal",
            () => Task.FromCanceled(stopping.Token), stopping.Token));
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private string Command(string suffix) => Path.ChangeExtension(Ready, $".{suffix}.json");

    private async Task<JsonDocument> DoneAsync(string suffix) => JsonDocument.Parse(await File.ReadAllTextAsync(Command(suffix) + ".done", Ct));
}
