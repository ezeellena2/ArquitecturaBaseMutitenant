using System.Security.Cryptography;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Application.Configuration.Auth;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Infrastructure.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ArquitecturaBaseMultitenant.RealE2ESetup;

/// <summary>Reproduce en el E2E aislado la cookie pública firmada del operador anterior a ADR 0044.</summary>
internal static class LegacyBrowserSessionCommand
{
    public static async Task TryExecuteAsync(IServiceProvider services, string readyFile, CancellationToken ct)
    {
        var commandFile = Path.ChangeExtension(readyFile, ".legacy-session.json");
        var completedFile = commandFile + ".done";
        if (!File.Exists(commandFile) || File.Exists(completedFile)) return;
        if (Environment.GetEnvironmentVariable("MT_E2E_ISOLATED") != "1")
            throw new InvalidOperationException("Legacy browser sessions require isolated E2E mode.");
        using var command = JsonDocument.Parse(await File.ReadAllTextAsync(commandFile, ct));
        if (command.RootElement.GetProperty("kind").GetString() != "issue-public-operator")
            throw new InvalidOperationException("Unsupported isolated legacy browser session command.");

        await using var scope = services.CreateAsyncScope();
        var provider = scope.ServiceProvider;
        var manager = provider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await manager.FindByEmailAsync("e2e-a8-operator@example.test");
        if (user is null || !user.IsPlatformOperator || user.Status != UserStatus.Active)
            throw new InvalidOperationException("The isolated legacy session operator must be prepared first.");
        var principal = await provider.GetRequiredService<IUserClaimsPrincipalFactory<ApplicationUser>>().CreateAsync(user);
        // La cuenta y las claves pertenecen a la base efímera. Solo el ticket reproduce
        // una sesión anterior a A3, todavía dentro de sus treinta días de vigencia.
        var startedAtUtc = provider.GetRequiredService<TimeProvider>().GetUtcNow().AddDays(-2);
        var properties = new AuthenticationProperties
        {
            IsPersistent = true, IssuedUtc = startedAtUtc, ExpiresUtc = startedAtUtc.AddDays(30),
        };
        properties.Items[BrowserSessionKeys.CookieSessionId] = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        properties.Items[BrowserSessionKeys.StartedAtUtc] = BrowserSessionKeys.FormatStartedAtUtc(startedAtUtc.UtcDateTime);
        var cookie = provider.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(IdentityConstants.ApplicationScheme);
        var value = cookie.TicketDataFormat.Protect(new AuthenticationTicket(
            principal, properties, IdentityConstants.ApplicationScheme));
        var name = cookie.Cookie.Name ?? throw new InvalidOperationException("The public cookie must have a name.");
        // El runner lee el secreto únicamente de este archivo local efímero; nunca se imprime.
        var temporaryFile = completedFile + ".tmp";
        await using (var output = new FileStream(temporaryFile, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            await JsonSerializer.SerializeAsync(output, new { name, value }, cancellationToken: ct);
        File.Move(temporaryFile, completedFile);
    }
}
