using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Legal;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Seed;
using ArquitecturaBaseMultitenant.RealE2ESetup;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Legal;

/// <summary>Ejecuta el comando real del preparador sobre el seed y el recorrido HTTP de aceptación.</summary>
[Collection(ApiTestGroup.Name)]
public sealed class LegalVersionCommandTests
{
    [Theory]
    [InlineData(false, false, 3)]
    [InlineData(false, true, 3)]
    [InlineData(true, false, 5)]
    [InlineData(true, true, 5)]
    public async Task Command_publishes_after_the_latest_seeded_or_scheduled_version_and_blocks_until_accepted(
        bool scheduledVersion, bool legacyCommand, int expectedVersion)
    {
        var ct = TestContext.Current.CancellationToken;
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 10, 12, 0, 0, TimeSpan.Zero));
        await using var isolated = new ApiFactory();
        await isolated.InitializeAsync();
        using var factory = isolated.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(clock);
        }));
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        await factory.Services.SeedDatabaseAsync(ct);
        // La revisión de partida es un dato del caso, independiente de los módulos instalados.
        await using (var seed = factory.Services.CreateAsyncScope())
        {
            await seed.ServiceProvider.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async workCt =>
            {
                var repository = seed.ServiceProvider.GetRequiredService<ILegalRepository>();
                await repository.LockKindAsync(LegalDocumentKind.Terms, workCt);
                if (await repository.GetLatestVersionAsync(LegalDocumentKind.Terms, workCt) < 2)
                {
                    var document = LegalDocument.Create(LegalDocumentKind.Terms, 2, clock.GetUtcNow().UtcDateTime);
                    repository.AddDocument(document);
                    repository.AddContent(LegalDocumentContent.Create(document.Id, "es-AR", "Revisión inicial del caso legal."));
                    repository.AddContent(LegalDocumentContent.Create(document.Id, "en-US", "Initial revision for the legal case."));
                }
                return Result.Success();
            }, CommitPolicy.OnSuccess, ct);
        }
        var account = await AccountJourney.RegisterAsync(factory, client, ct);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.True(await db.LegalAcceptances.AnyAsync(row => row.UserId == account.UserId
                && row.Version == 2 && db.LegalDocuments.Any(document => document.Id == row.LegalDocumentId
                    && document.Kind == LegalDocumentKind.Terms), ct));
            if (scheduledVersion)
            {
                await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(workCt =>
                {
                    var repository = scope.ServiceProvider.GetRequiredService<ILegalRepository>();
                    var document = LegalDocument.Create(LegalDocumentKind.Terms, 4, clock.GetUtcNow().UtcDateTime.AddDays(1));
                    repository.AddDocument(document);
                    repository.AddContent(LegalDocumentContent.Create(document.Id, "es-AR", "Términos programados de prueba."));
                    repository.AddContent(LegalDocumentContent.Create(document.Id, "en-US", "Scheduled test terms."));
                    return Task.FromResult(Result.Success());
                }, CommitPolicy.OnSuccess, ct);
            }
        }
        clock.Advance(TimeSpan.FromMinutes(1));
        var readyFile = Path.Combine(Path.GetTempPath(), $"mt-e2e-legal-{Guid.NewGuid():N}.ready");
        var commandFile = Path.ChangeExtension(readyFile, ".legal.json");
        var completedFile = commandFile + ".done";
        try
        {
            await File.WriteAllTextAsync(commandFile, legacyCommand
                ? "{\"kind\":\"Terms\",\"version\":2}" : "{\"kind\":\"Terms\"}", ct);
            await LegalVersionCommand.TryExecuteAsync(factory.Services, readyFile, ct);
            Assert.True(File.Exists(completedFile));
            Guid publishedId;
            int documentCount;
            await using (var read = factory.Services.CreateAsyncScope())
            {
                var db = read.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var published = await db.LegalDocuments.AsNoTracking().SingleAsync(row =>
                    row.Kind == LegalDocumentKind.Terms && row.Version == expectedVersion, ct);
                publishedId = published.Id;
                Assert.Equal(clock.GetUtcNow().UtcDateTime, published.EffectiveAtUtc);
                Assert.Equal(2, await db.LegalDocumentContents.CountAsync(row => row.LegalDocumentId == publishedId, ct));
                documentCount = await db.LegalDocuments.CountAsync(ct);
            }
            using var receipt = JsonDocument.Parse(await File.ReadAllTextAsync(completedFile, ct));
            Assert.Equal(publishedId, receipt.RootElement.GetProperty("id").GetGuid());
            Assert.Equal(expectedVersion, receipt.RootElement.GetProperty("version").GetInt32());
            using var me = await client.GetAsync("/api/me", ct);
            Assert.Equal(HttpStatusCode.OK, me.StatusCode);
            using var profile = await me.Content.ReadFromJsonAsync<JsonDocument>(ct);
            var pending = Assert.Single(profile!.RootElement.GetProperty("pendingLegalDocuments").EnumerateArray());
            Assert.Equal(publishedId, pending.GetProperty("id").GetGuid());
            Assert.Equal(expectedVersion, pending.GetProperty("version").GetInt32());
            using var blocked = await client.GetAsync("/api/me/login-methods", ct);
            Assert.Equal(HttpStatusCode.Forbidden, blocked.StatusCode);
            using var problem = await blocked.Content.ReadFromJsonAsync<JsonDocument>(ct);
            Assert.Equal("Legal.AcceptanceRequired", problem!.RootElement.GetProperty("code").GetString());
            using var accepted = await AccountJourney.PostAsync(client, "/api/legal/accept",
                new { documents = new[] { new { id = publishedId, version = expectedVersion } } }, ct);
            Assert.Equal(HttpStatusCode.NoContent, accepted.StatusCode);
            using var unblocked = await client.GetAsync("/api/me/login-methods", ct);
            Assert.Equal(HttpStatusCode.OK, unblocked.StatusCode);
            using var current = await client.GetAsync("/api/legal/terms", ct);
            Assert.Equal(HttpStatusCode.OK, current.StatusCode);
            using var documentResponse = await current.Content.ReadFromJsonAsync<JsonDocument>(ct);
            Assert.Equal(publishedId, documentResponse!.RootElement.GetProperty("id").GetGuid());
            Assert.Equal(expectedVersion, documentResponse.RootElement.GetProperty("version").GetInt32());
            await LegalVersionCommand.TryExecuteAsync(factory.Services, readyFile, ct);
            await using var replayRead = factory.Services.CreateAsyncScope();
            Assert.Equal(documentCount, await replayRead.ServiceProvider.GetRequiredService<ApplicationDbContext>().LegalDocuments.CountAsync(ct));
        }
        finally
        {
            File.Delete(commandFile);
            File.Delete(completedFile);
        }
    }
}
