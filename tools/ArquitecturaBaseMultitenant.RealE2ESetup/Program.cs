using ArquitecturaBaseMultitenant.Application;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Services.Auth;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Legal;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Settings;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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
    var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
    {
        EnvironmentName = "Testing",
    });
    builder.Logging.ClearProviders();
    builder.Services.AddApplication().AddInfrastructure(builder.Configuration, builder.Environment);
    builder.Services.AddSingleton<ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request.IPublicOrigin>(
        new E2EPublicOrigin());
    using var host = builder.Build();
    await using var scope = host.Services.CreateAsyncScope();
    var services = scope.ServiceProvider;
    var cancellationToken = CancellationToken.None;
    var personalSpaces = services.GetRequiredService<IPersonalSpaceProvisioner>();
    var draft = await personalSpaces.PrepareAsync(null, null, cancellationToken);
    var unitOfWork = services.GetRequiredService<IUnitOfWork>();
    var users = services.GetRequiredService<IUserRepository>();
    var methods = services.GetRequiredService<ILoginMethodRepository>();
    var nowUtc = services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;

    var created = await unitOfWork.ExecuteInTransactionAsync(async ct =>
    {
        var user = await users.CreateAsync("Cuenta E2E", draft.Culture, draft.TimeZoneId, ct);
        var method = LoginMethod.CreateEmail(user.Id, email.Value);
        method.Verify(nowUtc);
        if (method.MakePrimary().IsFailure)
            throw new InvalidOperationException("The E2E login method could not become primary.");
        methods.Add(method);
        await users.SetPrimaryEmailAsync(user.Id, email.Value, ct);
        var legal = services.GetRequiredService<ILegalRepository>();
        foreach (var kind in Enum.GetValues<LegalDocumentKind>())
        {
            var document = await legal.GetCurrentDocumentAsync(kind, nowUtc, ct)
                ?? throw new InvalidOperationException("The E2E legal documents have not been seeded.");
            legal.AddAcceptance(LegalAcceptance.Create(user.Id, document, nowUtc, null, null));
        }
        return Result.Success(user.Id);
    }, CommitPolicy.OnSuccess, cancellationToken);

    var tenantScope = services.GetRequiredService<ITenantScope>();
    using (tenantScope.Enter(draft.Tenant.Id))
    {
        await unitOfWork.ExecuteInTransactionAsync(ct =>
        {
            personalSpaces.Stage(draft, created.Value);
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, cancellationToken);
    }

    var business = Tenant.CreateBusiness("Empresa E2E", requiresApproval: false);
    if (business.Activate().IsFailure)
        throw new InvalidOperationException("The E2E organization could not be activated.");
    using (tenantScope.Enter(business.Id))
    {
        await unitOfWork.ExecuteInTransactionAsync(ct =>
        {
            services.GetRequiredService<TenantSpaceProvisioner>().Stage(business,
                TenantSettings.Create(draft.Settings.DefaultCulture,
                    draft.Settings.DefaultTimeZoneId, draft.Settings.DefaultCurrency), [created.Value]);
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, cancellationToken);
    }

    await using (var marker = new FileStream(readyFile, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        await marker.FlushAsync(cancellationToken);
    // El proceso conserva únicamente la conexión de la base efímera para la publicación legal del recorrido.
    using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(250),
        services.GetRequiredService<TimeProvider>());
    while (await timer.WaitForNextTickAsync(cancellationToken))
    {
        await LegalVersionCommand.TryExecuteAsync(host.Services, readyFile, cancellationToken);
        await InvitationCommand.TryExecuteAsync(host.Services, readyFile, business.Id, created.Value, cancellationToken);
    }
    return 0;
}
catch (PostgresException exception)
{
    Console.Error.WriteLine($"E2E setup failed (PostgresException {exception.SqlState}, constraint {exception.ConstraintName ?? "none"}).");
    return 1;
}
catch (Exception exception)
{
    Console.Error.WriteLine($"E2E setup failed ({exception.GetType().Name}).");
    return 1;
}
