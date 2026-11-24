using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Infrastructure.BackgroundJobs;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenIddict.EntityFrameworkCore.Models;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Identity;

/// <summary>
/// Revisión adversarial de la B2 (ADR 0062 §3): «el log de una purga dice cuántas filas borró». La poda de OpenIddict
/// también es una purga: cuenta los tokens y las autorizaciones que borra, y el log lo tiene que decir.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class IdentityRetentionAdversarialTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_session_prune_logs_how_many_tokens_and_authorizations_it_removed()
    {
        using var client = factory.CreateClient();
        var nowUtc = factory.Services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
        var subject = Guid.NewGuid().ToString("D");
        // Un código viejo: su purga sí deja la cantidad en el log, y prueba que el log que se mira es el del worker.
        var code = LoginCode.Issue(
            LoginCodeDestination.ForEmail(Email.Create($"log-{Guid.NewGuid():N}@example.test").Value),
            LoginCodePurpose.Login, null, "hash", nowUtc.AddDays(-31), TimeSpan.FromMinutes(10), maxAttempts: 3);
        await WriteAsync(services => services.GetRequiredService<ILoginCodeRepository>().Add(code));
        var tokenId = Guid.NewGuid();
        await WriteAsync(services => services.GetRequiredService<ApplicationDbContext>().Add(
            new OpenIddictEntityFrameworkCoreToken<Guid>
            {
                Id = tokenId,
                Subject = subject,
                Type = TokenTypeIdentifiers.RefreshToken,
                Status = Statuses.Valid,
                CreationDate = nowUtc.AddDays(-75),
                ExpirationDate = nowUtc.AddDays(-45),
            }));
        var logger = new RecordingLogger();

        await ActivatorUtilities.CreateInstance<IdentityRetentionWorker>(factory.Services, logger).RunOnceAsync(Ct);

        // La poda corrió y borró el token, y la purga de los códigos dejó su cantidad en el log.
        await using (var scope = factory.Services.CreateAsyncScope())
            Assert.False(await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
                .Set<OpenIddictEntityFrameworkCoreToken<Guid>>().AnyAsync(token => token.Id == tokenId, Ct));
        Assert.Contains(logger.Messages, message => message.Contains("login codes", StringComparison.Ordinal));
        Assert.Contains(logger.Messages, message =>
            message.Contains("token", StringComparison.OrdinalIgnoreCase)
            || message.Contains("session", StringComparison.OrdinalIgnoreCase));
    }

    private async Task WriteAsync(Action<IServiceProvider> write)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ =>
        {
            write(services);
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct);
    }

    private sealed class RecordingLogger : ILogger<IdentityRetentionWorker>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            lock (Messages) Messages.Add(formatter(state, exception));
        }
    }
}
