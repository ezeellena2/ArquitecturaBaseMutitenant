using ArquitecturaBaseMultitenant.Application;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using ArquitecturaBaseMultitenant.RealE2ESetup;

// Prepara la cuenta, el espacio personal y la empresa de los recorridos E2E en su base aislada.
// Señala al runner cuándo puede empezar y atiende su orden de publicar nuevos términos de prueba.
// Con --capture-emails genera archivos HTML de los correos reales para revisar su aspecto.
if (args is ["--capture-emails", var emailCaptureDirectory])
    return await AccountEmailCaptures.WriteAsync(emailCaptureDirectory);

if (Environment.GetEnvironmentVariable("MT_E2E_ISOLATED") != "1")
{
    Console.Error.WriteLine("E2E setup requires the isolated database mode.");
    return 1;
}

if (args is ["--whatsapp-browser", var browserScript])
{
    try
    {
        var repository = new DirectoryInfo(AppContext.BaseDirectory);
        while (repository is not null && !File.Exists(Path.Combine(repository.FullName, "ArquitecturaBaseMultitenant.slnx"))) repository = repository.Parent;
        if (repository is null) throw new InvalidOperationException("Repository root missing.");
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(30));
        return await ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.WhatsAppBrowser.WhatsAppBrowserFixture.RunAsync(repository.FullName, browserScript, timeout.Token);
    }
    catch (Exception exception) { Console.Error.WriteLine($"E8 browser fixture failed ({exception.GetType().Name})."); return 1; }
}

var configuredEmail = Environment.GetEnvironmentVariable("MT_E2E_BUSINESS_EMAIL");
var email = Email.Create(configuredEmail);
if (email.IsFailure || !email.Value.Value.EndsWith("@example.test", StringComparison.OrdinalIgnoreCase))
{
    Console.Error.WriteLine("E2E setup requires a valid test mailbox.");
    return 1;
}

var readyFile = Environment.GetEnvironmentVariable("MT_E2E_READY_FILE");
if (string.IsNullOrWhiteSpace(readyFile) || !Path.IsPathFullyQualified(readyFile)
    || !Path.GetFileName(readyFile).StartsWith("mt-e2e-", StringComparison.Ordinal)
    || !string.Equals(Path.GetExtension(readyFile), ".ready", StringComparison.Ordinal))
{
    Console.Error.WriteLine("E2E setup requires an absolute readiness file in the test workspace.");
    return 1;
}

try
{
    if (args is ["--recorrido-host"])
        return await RecorridoHost.RunAsync(email.Value, readyFile, CancellationToken.None);
    var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
    {
        EnvironmentName = "Testing",
    });
    builder.Logging.ClearProviders();
    builder.Services.AddApplication().AddInfrastructure(builder.Configuration, builder.Environment);
    builder.Services.AddSingleton<ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request.IPublicOrigin, E2EPublicOrigin>();
    using var host = builder.Build();
    await using var scope = host.Services.CreateAsyncScope();
    var services = scope.ServiceProvider;
    var cancellationToken = CancellationToken.None;
    var prepared = await E7Preparation.PrepareAsync(host.Services, email.Value, cancellationToken);
    await using (var marker = new FileStream(readyFile, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        await marker.FlushAsync(cancellationToken);
    // El proceso conserva únicamente la conexión de la base efímera para la publicación legal del recorrido.
    using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(250),
        services.GetRequiredService<TimeProvider>());
    while (await timer.WaitForNextTickAsync(cancellationToken))
    {
        await LegalVersionCommand.TryExecuteAsync(host.Services, readyFile, cancellationToken);
        await InvitationCommand.TryExecuteAsync(host.Services, readyFile, prepared.BusinessId, prepared.UserId, cancellationToken);
        await PlatformCommand.TryExecuteAsync(host.Services, readyFile, prepared.BusinessId, prepared.UserId, cancellationToken);
        await LegacyBrowserSessionCommand.TryExecuteAsync(host.Services, readyFile, cancellationToken);
        await LoginCodeCommand.TryExecuteAsync(host.Services, readyFile, cancellationToken);
    }
    return 0;
}
catch (DbUpdateException exception) when (exception.InnerException is PostgresException postgres)
{
    Console.Error.WriteLine($"E2E setup failed (PostgresException {postgres.SqlState}, constraint {postgres.ConstraintName ?? "none"}).");
    return 1;
}
catch (PostgresException exception)
{
    Console.Error.WriteLine($"E2E setup failed (PostgresException {exception.SqlState}, constraint {exception.ConstraintName ?? "none"}).");
    return 1;
}
catch (Exception exception)
{
    Console.Error.WriteLine($"E2E setup failed ({exception.GetType().Name}).");
    Console.Error.WriteLine($"E2E platform preparation phase: {PlatformCommand.Phase}.");
    return 1;
}
