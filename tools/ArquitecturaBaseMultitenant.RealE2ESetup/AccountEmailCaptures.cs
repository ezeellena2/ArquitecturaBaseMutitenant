using ArquitecturaBaseMultitenant.Application;
using ArquitecturaBaseMultitenant.Application.Common.Formatting;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using ArquitecturaBaseMultitenant.Application.Interfaces.ReferenceData;
using ArquitecturaBaseMultitenant.Application.Models.Notifications;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Infrastructure;
using ArquitecturaBaseMultitenant.Infrastructure.ReferenceData;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.RealE2ESetup;

/// <summary>
/// Compone los correos reales de verificación y avisos de cuenta con el catálogo embebido.
/// Guarda catorce archivos HTML en español e inglés para revisar su aspecto sin enviar mensajes.
/// </summary>
internal static class AccountEmailCaptures
{
    public static async Task<int> WriteAsync(string directory)
    {
        if (!Path.IsPathFullyQualified(directory)) throw new ArgumentException("Use an absolute capture directory.");
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { EnvironmentName = "Testing" });
        builder.Logging.ClearProviders();
        builder.Configuration["Email:AppName"] = "ArquitecturaBase";
        builder.Configuration["Email:LogoUrl"] = "https://ejemplo.com/favicon.svg";
        builder.Services.AddApplication().AddInfrastructure(builder.Configuration, builder.Environment);
        var catalog = new JsonReferenceDataCatalog();
        builder.Services.AddSingleton<ICultureCatalog>(catalog);
        builder.Services.AddSingleton<ICurrencyCatalog>(catalog);
        builder.Services.AddSingleton<ICountryCatalog>(catalog);
        builder.Services.AddSingleton<ITimeZoneCatalog>(catalog);
        builder.Services.AddSingleton<ITaxIdTypeCatalog>(catalog);
        using var host = builder.Build();
        await using var scope = host.Services.CreateAsyncScope();
        var renderer = scope.ServiceProvider.GetRequiredService<IEmailTemplateRenderer>();
        var occurred = new DateTime(2026, 9, 27, 17, 35, 0, DateTimeKind.Utc);
        var scheduled = occurred.AddDays(30);
        const string zone = "America/Argentina/Buenos_Aires";
        const string accountUrl = "https://ejemplo.com/cuenta";
        var notices = new Dictionary<string, AccountNotice>
        {
            ["metodo-agregado"] = new AccountNotice.LoginMethodChanged("Added", LoginMethodType.Email, "l***@gmail.com", occurred, zone, accountUrl) { RecipientName = "Lucía" },
            ["metodo-quitado"] = new AccountNotice.LoginMethodChanged("Removed", LoginMethodType.Email, "l***@delta.ejemplo.com", occurred, zone, accountUrl) { RecipientName = "Lucía" },
            ["principal-cambiado"] = new AccountNotice.LoginMethodChanged("Primary", LoginMethodType.Email, "l***@gmail.com", occurred, zone, accountUrl) { RecipientName = "Lucía" },
            ["baja-pedida"] = new AccountNotice.DeletionRequested(occurred, scheduled, zone, "https://ejemplo.com/login") { RecipientName = "Diego" },
            ["baja-cancelada"] = new AccountNotice.DeletionCancelled(occurred, zone, accountUrl) { RecipientName = "Diego" },
            ["cuenta-eliminada"] = new AccountNotice.AccountDeleted { RecipientName = "Diego" },
        };
        Directory.CreateDirectory(directory);
        foreach (var culture in new[] { "es-AR", "en-US" })
        {
            var profile = await new CultureProfiles(catalog).LoadAsync(culture, CancellationToken.None);
            var verify = renderer.RenderVerifyEmailCode("visual@example.test", "715204", 10, profile);
            await File.WriteAllTextAsync(Path.Combine(directory, $"verificar-metodo-{culture}.html"), verify.HtmlBody);
            foreach (var (key, notice) in notices)
            {
                var message = await renderer.RenderAccountNoticeAsync("visual@example.test", notice, profile, CancellationToken.None);
                await File.WriteAllTextAsync(Path.Combine(directory, $"{key}-{culture}.html"), message.HtmlBody);
            }
        }
        Console.WriteLine("14 correos reales compuestos en es/en, sin base ni envío.");
        return 0;
    }
}
