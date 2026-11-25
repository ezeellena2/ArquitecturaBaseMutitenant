using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Settings;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Platform;

/// <summary>
/// Revisión adversarial de la B18 (273): el logo de los correos es «vacío, o una dirección https://», y un logo vacío lo quita
/// (<see cref="PlatformSettings.UpdateRules"/>). Con <c>Email:LogoUrl</c> de arranque, quitar el logo en «Nombre y correos» tiene
/// que dejar los correos sin logo, como lo muestra la pantalla (el GET dice null), no volver al de arranque. Base propia: cambia
/// la configuración de Plataforma.
/// </summary>
public sealed class PlatformBrandingLogoAdversarialTests
{
    private const string BootstrapLogo = "https://cdn.example.test/arranque.png";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_logo_removed_in_settings_does_not_come_back_from_the_bootstrap_configuration()
    {
        await using var isolated = new ApiFactory();
        await isolated.InitializeAsync();
        await using var host = isolated.WithWebHostBuilder(builder => builder.UseSetting("Email:LogoUrl", BootstrapLogo));

        // Plataforma guarda un logo propio y después lo quita: lo guardado queda vacío.
        await SaveAsync(host.Services, settings => settings.UpdateRules(null, null, null, null, null, "https://cdn.example.test/plataforma.png"));
        await SaveAsync(host.Services, settings => settings.UpdateRules(null, null, null, null, null, string.Empty));

        await using var scope = host.Services.CreateAsyncScope();
        var branding = await scope.ServiceProvider.GetRequiredService<IPlatformSettingsReader>().FindBrandingAsync(Ct);
        Assert.Null(branding.EmailLogoUrl);
    }

    private static async Task SaveAsync(IServiceProvider root, Func<PlatformSettings, Result> change)
    {
        await using var scope = root.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var saved = await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
        {
            var db = services.GetRequiredService<ApplicationDbContext>();
            var settings = await db.Set<PlatformSettings>().SingleOrDefaultAsync(ct);
            if (settings is null)
            {
                settings = PlatformSettings.Create(ConsumerSignupMode.Open, BusinessSignupMode.Open, 1);
                db.Add(settings);
            }
            var changed = change(settings);
            if (changed.IsSuccess)
                db.Add(SecurityEvent.ForPlatformSettings(AuditActorKind.System, Guid.Empty, "Revisión adversarial B18",
                    services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime));
            return changed;
        }, CommitPolicy.OnSuccess, Ct);
        Assert.True(saved.IsSuccess);
    }
}
