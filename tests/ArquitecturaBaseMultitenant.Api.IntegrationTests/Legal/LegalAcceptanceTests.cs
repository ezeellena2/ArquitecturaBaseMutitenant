using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Domain.Legal;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Seed;
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
}
