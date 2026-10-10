using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Legal;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Seed;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.Assistant;

[Collection(ApiTestGroup.Name)]
public sealed class AssistantLegalSeedTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public async Task Seed_preserves_published_and_future_revisions_and_historical_legal_consent(int existingVersion)
    {
        await using var isolated = new ApiFactory();
        await isolated.InitializeAsync();
        using var coreOnly = isolated.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            foreach (var registration in services.Where(row => row.ServiceType == typeof(IModuleDataSeedContributor)
                         && row.ImplementationType?.Name == "AssistantLegalDocumentSeed").ToArray()) services.Remove(registration);
        }));
        using var client = coreOnly.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var account = await AccountJourney.RegisterAsync(coreOnly, client, Ct);
        Guid customId;
        object[] before;
        using (var scope = coreOnly.Services.CreateScope())
        {
            var services = scope.ServiceProvider;
            var context = services.GetRequiredService<ApplicationDbContext>();
            Assert.Equal(2, await context.LegalDocuments.CountAsync(Ct));
            Assert.Equal(2, await context.LegalAcceptances.CountAsync(row => row.UserId == account.UserId, Ct));
            var custom = LegalDocument.Create(LegalDocumentKind.Terms, existingVersion,
                services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime.AddDays(existingVersion == 3 ? 2 : 0));
            customId = custom.Id;
            await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ =>
            {
                context.Add(custom);
                context.AddRange(LegalDocumentContent.Create(custom.Id, "es-AR", "Versión propia e inmutable."),
                    LegalDocumentContent.Create(custom.Id, "en-US", "Custom immutable revision."));
                return Task.FromResult(Result.Success());
            }, CommitPolicy.OnSuccess, Ct);
            before = await SnapshotAsync(context);
        }
        await isolated.Services.SeedDatabaseAsync(Ct);
        await isolated.Services.SeedDatabaseAsync(Ct);
        using var restart = isolated.WithWebHostBuilder(_ => { });
        await restart.Services.SeedDatabaseAsync(Ct);
        using var check = restart.Services.CreateScope();
        var db = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(4, await db.LegalDocuments.CountAsync(Ct));
        Assert.Equal(2, await db.LegalDocuments.CountAsync(row => row.Kind == LegalDocumentKind.Terms, Ct));
        Assert.Equal(existingVersion, (await db.LegalDocuments.AsNoTracking().SingleAsync(row => row.Id == customId, Ct)).Version);
        var all = await SnapshotAsync(db);
        foreach (var original in before) Assert.Contains(original, all);
        Assert.Equal(2, await db.LegalAcceptances.CountAsync(row => row.UserId == account.UserId, Ct));
        Assert.Equal(2, await db.LegalDocumentContents.CountAsync(row => row.LegalDocumentId == customId, Ct));
        Assert.False(await db.LegalDocuments.AnyAsync(row => row.Kind == LegalDocumentKind.Terms && row.Version != 1 && row.Version != existingVersion, Ct));
    }

    [Fact]
    public async Task Empty_seed_and_restart_publish_one_stable_assistant_revision_per_kind_without_changing_v1()
    {
        await using var isolated = new ApiFactory();
        await isolated.InitializeAsync();
        await isolated.Services.SeedDatabaseAsync(Ct);
        object[] before;
        using (var scope = isolated.Services.CreateScope()) before = await SnapshotAsync(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
        await isolated.Services.SeedDatabaseAsync(Ct);
        using var restart = isolated.WithWebHostBuilder(_ => { });
        await restart.Services.SeedDatabaseAsync(Ct);
        using var read = restart.Services.CreateScope();
        var db = read.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(before, await SnapshotAsync(db));
        var docs = await db.LegalDocuments.AsNoTracking().ToArrayAsync(Ct);
        Assert.Equal(4, docs.Length);
        foreach (var kind in Enum.GetValues<LegalDocumentKind>()) Assert.Equal([1, 2], docs.Where(row => row.Kind == kind).Select(row => row.Version).Order().ToArray());
        var contents = await db.LegalDocumentContents.AsNoTracking().ToArrayAsync(Ct);
        Assert.Equal(8, contents.Length);
        foreach (var document in docs)
        {
            var translations = contents.Where(row => row.LegalDocumentId == document.Id).ToArray();
            Assert.Equal(["en-US", "es-AR"], translations.Select(row => row.Culture).Order().ToArray());
            if (document.Version == 2) Assert.All(translations, row => Assert.Contains("Anthropic", row.Text, StringComparison.Ordinal));
            else Assert.All(translations, row => Assert.DoesNotContain("Anthropic", row.Text, StringComparison.Ordinal));
        }
    }

    private static async Task<object[]> SnapshotAsync(ApplicationDbContext db)
    {
        var documents = await db.LegalDocuments.AsNoTracking().OrderBy(row => row.Id)
            .Select(row => new { row.Id, row.Kind, row.Version, row.EffectiveAtUtc }).ToArrayAsync(Ct);
        var contents = await db.LegalDocumentContents.AsNoTracking().OrderBy(row => row.LegalDocumentId).ThenBy(row => row.Culture)
            .Select(row => new { row.LegalDocumentId, row.Culture, row.Text }).ToArrayAsync(Ct);
        return [.. documents, .. contents];
    }
}
