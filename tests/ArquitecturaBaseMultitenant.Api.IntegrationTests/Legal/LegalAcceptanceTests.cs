using System.Net;
using System.Net.Http.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Domain.Legal;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Legal;

[Collection(ApiTestGroup.Name)]
public sealed class LegalAcceptanceTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Seeded_legal_documents_are_readable_in_both_enabled_cultures()
    {
        await factory.Services.SeedDatabaseAsync(Ct);
        await using var scope = factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<ILegalService>();

        var terms = await service.GetCurrentAsync(LegalDocumentKind.Terms, "es-AR", Ct);
        var privacy = await service.GetCurrentAsync(LegalDocumentKind.Privacy, "en-US", Ct);

        Assert.True(terms.IsSuccess);
        Assert.True(privacy.IsSuccess);
        Assert.Equal(1, terms.Value.Version);
        Assert.Equal(1, privacy.Value.Version);
        Assert.StartsWith("DOCUMENTO DE DEMOSTRACIÓN.", terms.Value.Text, StringComparison.Ordinal);
        Assert.StartsWith("DEMONSTRATION DOCUMENT.", privacy.Value.Text, StringComparison.Ordinal);
        Assert.Equal("es-AR", terms.Value.Culture);
        Assert.Equal("en-US", privacy.Value.Culture);
    }

    [Fact]
    public async Task Unsupported_request_culture_uses_the_enabled_default_document()
    {
        await factory.Services.SeedDatabaseAsync(Ct);
        await using var scope = factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<ILegalService>();

        var result = await service.GetCurrentAsync(LegalDocumentKind.Terms, "fr-FR", Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal("es-AR", result.Value.Culture);
    }

    [Fact]
    public async Task Signup_records_the_current_terms_and_privacy_versions()
    {
        await factory.Services.SeedDatabaseAsync(Ct);
        using var client = factory.CreateClient();
        var address = $"current-legal-{Guid.NewGuid():N}@example.test";
        var requested = await client.PostAsJsonAsync("/test/auth/signup",
            new { email = address, acceptedTerms = true }, Ct);
        Assert.Equal(HttpStatusCode.Accepted, requested.StatusCode);
        var code = await PickupCodeReader.ReadAsync(factory.Services, address, Ct);
        var verified = await client.PostAsJsonAsync("/test/auth/signup/verify",
            new { email = address, code, acceptedTerms = true }, Ct);
        Assert.Equal(HttpStatusCode.NoContent, verified.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var nowUtc = scope.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
        var accepted = await (from method in context.LoginMethods
            where method.Value == address
            join acceptance in context.LegalAcceptances on method.UserId equals acceptance.UserId
            join document in context.LegalDocuments on acceptance.LegalDocumentId equals document.Id
            select new { acceptance.LegalDocumentId, acceptance.Version, acceptance.AcceptedAtUtc,
                document.Kind }).ToArrayAsync(Ct);
        Assert.Equal(2, accepted.Length);

        foreach (var kind in new[] { LegalDocumentKind.Terms, LegalDocumentKind.Privacy })
        {
            var current = await context.LegalDocuments.AsNoTracking()
                .Where(document => document.Kind == kind && document.EffectiveAtUtc <= nowUtc)
                .OrderByDescending(document => document.EffectiveAtUtc)
                .ThenByDescending(document => document.Version)
                .FirstAsync(Ct);
            var row = Assert.Single(accepted, acceptance => acceptance.Kind == kind);
            Assert.Equal(current.Id, row.LegalDocumentId);
            Assert.Equal(current.Version, row.Version);
            Assert.Equal(DateTimeKind.Utc, row.AcceptedAtUtc.Kind);
            Assert.True(row.AcceptedAtUtc >= current.EffectiveAtUtc);
        }
    }
}
