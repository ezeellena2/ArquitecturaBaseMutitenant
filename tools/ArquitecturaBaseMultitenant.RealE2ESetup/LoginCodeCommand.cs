using System.Text.Json;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.RealE2ESetup;

/// <summary>Vence un desafío real propio del recorrido, exclusivamente en la base E2E.</summary>
internal static class LoginCodeCommand
{
    public static async Task TryExecuteAsync(IServiceProvider services, string readyFile, CancellationToken ct)
    {
        var commandFile = Path.ChangeExtension(readyFile, ".login-code.json");
        var completedFile = commandFile + ".done";
        if (!File.Exists(commandFile) || File.Exists(completedFile)) return;
        if (Environment.GetEnvironmentVariable("MT_E2E_ISOLATED") != "1")
            throw new InvalidOperationException("Login code commands require isolated E2E mode.");
        using var command = JsonDocument.Parse(await File.ReadAllTextAsync(commandFile, ct));
        var email = Email.Create(command.RootElement.GetProperty("email").GetString());
        if (command.RootElement.GetProperty("kind").GetString() != "expire"
            || email.IsFailure || !email.Value.Value.EndsWith("@example.test", StringComparison.Ordinal))
            throw new InvalidOperationException("Only an isolated test login code may be expired.");
        await using var scope = services.CreateAsyncScope();
        var provider = scope.ServiceProvider;
        var db = provider.GetRequiredService<ApplicationDbContext>();
        var nowUtc = provider.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
        var destination = email.Value.Value;
        var active = db.LoginCodes.Where(code => code.Destination == destination
            && code.Purpose == LoginCodePurpose.Login && code.ConsumedAtUtc == null
            && code.InvalidatedAtUtc == null && code.ExpiresAtUtc > nowUtc);
        // Igual que InvitationCommand: solo el fixture acorta la fecha, sin cambiar el reloj ni la API.
        var changed = await active.ExecuteUpdateAsync(
            setters => setters.SetProperty(code => code.ExpiresAtUtc, nowUtc.AddMinutes(-1)), ct);
        if (changed != 1) throw new InvalidOperationException("Exactly one active isolated login code is required.");
        var temporaryFile = completedFile + ".tmp";
        await File.WriteAllTextAsync(temporaryFile, JsonSerializer.Serialize(new { expired = true }), ct);
        File.Move(temporaryFile, completedFile);
    }
}
