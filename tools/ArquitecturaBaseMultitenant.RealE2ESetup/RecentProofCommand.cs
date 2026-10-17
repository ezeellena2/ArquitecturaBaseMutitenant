using System.Text.Json;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Infrastructure.Identity;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.RealE2ESetup;

/// <summary>
/// Vence la prueba reciente de las sesiones de Plataforma de un operador del recorrido (E9), exclusivamente en la base
/// E2E: así «Confirmá que sos vos» se prueba sin esperar los diez minutos reales.
/// </summary>
internal static class RecentProofCommand
{
    public static async Task TryExecuteAsync(IServiceProvider services, string readyFile, CancellationToken ct)
    {
        var commandFile = Path.ChangeExtension(readyFile, ".recent-proof.json");
        var completedFile = commandFile + ".done";
        if (!File.Exists(commandFile) || File.Exists(completedFile)) return;
        if (Environment.GetEnvironmentVariable("MT_E2E_ISOLATED") != "1")
            throw new InvalidOperationException("Recent proof commands require isolated E2E mode.");
        using var command = JsonDocument.Parse(await File.ReadAllTextAsync(commandFile, ct));
        var email = Email.Create(command.RootElement.GetProperty("email").GetString());
        if (command.RootElement.GetProperty("kind").GetString() != "expire"
            || email.IsFailure || !email.Value.Value.EndsWith("@example.test", StringComparison.Ordinal))
            throw new InvalidOperationException("Only an isolated test operator's recent proof may be expired.");
        await using var scope = services.CreateAsyncScope();
        var provider = scope.ServiceProvider;
        var user = await provider.GetRequiredService<UserManager<ApplicationUser>>().FindByEmailAsync(email.Value.Value);
        if (user is null || !user.IsPlatformOperator)
            throw new InvalidOperationException("The isolated operator must be prepared first.");
        var nowUtc = provider.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
        var active = provider.GetRequiredService<ApplicationDbContext>().PlatformSessions
            .Where(session => session.UserId == user.Id && session.RevokedAtUtc == null && session.RecentProofUntilUtc > nowUtc);
        // Como LoginCodeCommand: solo el fixture corre las fechas, sin cambiar el reloj ni la API. La ventana sigue
        // midiendo diez minutos, pero terminó hace uno.
        var changed = await active.ExecuteUpdateAsync(setters => setters
            .SetProperty(session => session.RecentProofAtUtc, nowUtc.AddMinutes(-11))
            .SetProperty(session => session.RecentProofUntilUtc, nowUtc.AddMinutes(-1)), ct);
        if (changed < 1) throw new InvalidOperationException("An isolated platform session with recent proof is required.");
        var temporaryFile = completedFile + ".tmp";
        await File.WriteAllTextAsync(temporaryFile, JsonSerializer.Serialize(new { expired = changed }), ct);
        File.Move(temporaryFile, completedFile);
    }
}
