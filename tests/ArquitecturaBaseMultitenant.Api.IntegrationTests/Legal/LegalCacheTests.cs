using System.Collections.Concurrent;
using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Legal;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Infrastructure.BackgroundJobs;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Legal;

/// <summary>
/// KAN-114 y ADR 0052: las versiones legales vigentes y las aceptaciones de cada persona viven en la caché compartida con
/// generaciones automáticas. Con caché caliente el middleware no consulta <c>LegalDocuments</c> ni <c>LegalAcceptances</c>;
/// publicar una versión nueva o registrar una aceptación se refleja enseguida en el siguiente pedido sin invalidar a mano.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class LegalCacheTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Warm_authenticated_request_makes_no_queries_to_legal_documents_or_acceptances()
    {
        var log = new CommandLog();
        await using var isolated = new ApiFactory();
        await isolated.InitializeAsync();
        using var host = CreateHost(isolated, log);
        using var client = host.CreateClient(new WebApplicationFactoryClientOptions
        { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        await AccountJourney.RegisterAsync(host, client, Ct);

        // Primera lectura autenticada: calienta las dos entradas de caché de Legal.
        using (var first = await client.GetAsync("/api/me/login-methods", Ct))
            Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        log.Clear();
        using (var warm = await client.GetAsync("/api/me/login-methods", Ct))
            Assert.Equal(HttpStatusCode.OK, warm.StatusCode);

        var commands = log.Take();
        Assert.DoesNotContain(commands, ReadsLegalDocuments);
        Assert.DoesNotContain(commands, ReadsLegalAcceptances);
    }

    [Fact]
    public async Task Publishing_a_new_legal_document_or_recording_an_acceptance_takes_effect_immediately_through_cache_generations()
    {
        var log = new CommandLog();
        await using var isolated = new ApiFactory();
        await isolated.InitializeAsync();
        using var host = CreateHost(isolated, log);
        using var client = host.CreateClient(new WebApplicationFactoryClientOptions
        { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        await AccountJourney.RegisterAsync(host, client, Ct);

        using (var initial = await client.GetAsync("/api/me/login-methods", Ct))
            Assert.Equal(HttpStatusCode.OK, initial.StatusCode);

        var published = await PublishTermsAsync(host.Services);

        // Publicar avanzó la generación de documentos legales en el commit: bloquea enseguida y lee LegalDocuments en una consulta.
        log.Clear();
        using (var blocked = await client.GetAsync("/api/me/login-methods", Ct))
        {
            Assert.Equal(HttpStatusCode.Forbidden, blocked.StatusCode);
            using var problem = await blocked.Content.ReadFromJsonAsync<JsonDocument>(Ct);
            Assert.Equal(LegalErrors.AcceptanceRequiredCode, problem!.RootElement.GetProperty("code").GetString());
        }
        var afterPublish = log.Take();
        Assert.Equal(1, afterPublish.Count(ReadsLegalDocuments));
        Assert.Equal(0, afterPublish.Count(ReadsLegalAcceptances));

        // Aceptar avanzó la generación de aceptaciones de la persona en el commit: desbloquea enseguida en una consulta.
        using (var accepted = await AccountJourney.PostAsync(client, "/api/legal/accept",
                   new { documents = new[] { new { id = published.Id, version = published.Version } } }, Ct))
            Assert.Equal(HttpStatusCode.NoContent, accepted.StatusCode);

        log.Clear();
        using (var unblocked = await client.GetAsync("/api/me/login-methods", Ct))
            Assert.Equal(HttpStatusCode.OK, unblocked.StatusCode);
        var afterAccept = log.Take();
        Assert.Equal(0, afterAccept.Count(ReadsLegalDocuments));
        Assert.Equal(1, afterAccept.Count(ReadsLegalAcceptances));

        // El siguiente pedido con caché caliente vuelve a hacer 0 consultas a ambas tablas.
        log.Clear();
        using (var warmAgain = await client.GetAsync("/api/me/login-methods", Ct))
            Assert.Equal(HttpStatusCode.OK, warmAgain.StatusCode);
        var warmCommands = log.Take();
        Assert.DoesNotContain(warmCommands, ReadsLegalDocuments);
        Assert.DoesNotContain(warmCommands, ReadsLegalAcceptances);
    }

    private static WebApplicationFactory<Program> CreateHost(ApiFactory isolated, CommandLog log) =>
        isolated.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.ConfigureDbContext<ApplicationDbContext>(options => options.AddInterceptors(new CommandRecorder(log)));
            foreach (var worker in services.Where(service => service.ServiceType == typeof(IHostedService)
                         && service.ImplementationType?.IsSubclassOf(typeof(PeriodicWorker)) == true).ToArray())
                services.Remove(worker);
        }));

    private static async Task<LegalDocument> PublishTermsAsync(IServiceProvider root)
    {
        await using var scope = root.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var nowUtc = services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
        LegalDocument? document = null;
        await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
        {
            var repository = services.GetRequiredService<ILegalRepository>();
            await repository.LockKindAsync(LegalDocumentKind.Terms, ct);
            var nextVersion = await repository.GetLatestVersionAsync(LegalDocumentKind.Terms, ct) + 1;
            document = LegalDocument.Create(LegalDocumentKind.Terms, nextVersion, nowUtc);
            repository.AddDocument(document);
            repository.AddContent(LegalDocumentContent.Create(document.Id, "es-AR", "Términos nuevos de caché."));
            repository.AddContent(LegalDocumentContent.Create(document.Id, "en-US", "New cached terms."));
            return Result.Success();
        }, CommitPolicy.OnSuccess, Ct);
        return document!;
    }

    private static bool ReadsLegalDocuments(string command) =>
        command.Contains("\"LegalDocuments\"", StringComparison.Ordinal);

    private static bool ReadsLegalAcceptances(string command) =>
        command.Contains("\"LegalAcceptances\"", StringComparison.Ordinal);

    private sealed class CommandLog
    {
        private readonly ConcurrentQueue<string> _commands = new();

        internal void Add(string command) => _commands.Enqueue(command);

        internal void Clear() => _commands.Clear();

        internal string[] Take()
        {
            var commands = _commands.ToArray();
            _commands.Clear();
            return commands;
        }
    }

    private sealed class CommandRecorder(CommandLog log) : DbCommandInterceptor
    {
        public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result)
        {
            log.Add(command.CommandText);
            return base.ReaderExecuting(command, eventData, result);
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            log.Add(command.CommandText);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }
}
