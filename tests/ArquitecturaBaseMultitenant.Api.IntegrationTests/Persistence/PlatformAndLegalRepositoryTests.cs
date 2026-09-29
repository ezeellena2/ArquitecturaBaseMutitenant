using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Legal;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Persistence;

[Collection(ApiTestGroup.Name)]
public sealed class PlatformAndLegalRepositoryTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Global_repositories_reject_writes_without_a_use_case_transaction()
    {
        using var client = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var nowUtc = services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;

        Assert.Throws<InvalidOperationException>(() =>
            services.GetRequiredService<IPlatformSettingsRepository>().Add(
                PlatformSettings.Create(ConsumerSignupMode.Open, BusinessSignupMode.RequiresApproval, 1)));
        Assert.Throws<InvalidOperationException>(() =>
            services.GetRequiredService<ISecurityEventRepository>().Add(
                SecurityEvent.ForAccount(SecurityEventType.LoginMethodChanged, Guid.CreateVersion7(), nowUtc)));
        Assert.Throws<InvalidOperationException>(() =>
            services.GetRequiredService<ILegalRepository>().AddDocument(
                LegalDocument.Create(LegalDocumentKind.Terms, 1, nowUtc)));
    }

    [Fact]
    public async Task Initial_settings_and_legal_documents_are_read_after_commit_by_culture()
    {
        using var client = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var unitOfWork = services.GetRequiredService<IUnitOfWork>();
        var settingsRepository = services.GetRequiredService<IPlatformSettingsRepository>();
        var settingsReader = services.GetRequiredService<IPlatformSettingsReader>();
        var legalRepository = services.GetRequiredService<ILegalRepository>();
        var legalReader = services.GetRequiredService<ILegalReader>();
        var securityEventRepository = services.GetRequiredService<ISecurityEventRepository>();
        var nowUtc = services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
        var terms = LegalDocument.Create(LegalDocumentKind.Terms, 1, nowUtc.AddMinutes(-1));
        var privacy = LegalDocument.Create(LegalDocumentKind.Privacy, 1, nowUtc.AddMinutes(-1));

        await unitOfWork.ExecuteInTransactionAsync(ct =>
        {
            settingsRepository.Add(PlatformSettings.Create(
                ConsumerSignupMode.Open, BusinessSignupMode.RequiresApproval, 1));
            securityEventRepository.Add(SecurityEvent.ForPlatformSettings(AuditActorKind.System,
                Guid.Empty, "Initial platform settings", nowUtc));
            legalRepository.AddDocument(terms);
            legalRepository.AddContent(LegalDocumentContent.Create(terms.Id, "es-AR", "Términos de prueba"));
            legalRepository.AddContent(LegalDocumentContent.Create(terms.Id, "en-US", "Test terms"));
            legalRepository.AddDocument(privacy);
            legalRepository.AddContent(LegalDocumentContent.Create(privacy.Id, "es-AR", "Privacidad de prueba"));
            legalRepository.AddContent(LegalDocumentContent.Create(privacy.Id, "en-US", "Test privacy"));
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct);

        Assert.Equal(ConsumerSignupMode.Open, (await settingsReader.FindAsync(Ct))?.ConsumerSignup);
        Assert.Equal("Términos de prueba",
            (await legalReader.FindCurrentAsync(LegalDocumentKind.Terms, "es-AR", nowUtc, Ct))?.Text);
        Assert.Equal("Test terms",
            (await legalReader.FindCurrentAsync(LegalDocumentKind.Terms, "en-US", nowUtc, Ct))?.Text);
        Assert.Equal("Test privacy",
            (await legalReader.FindCurrentAsync(LegalDocumentKind.Privacy, "en-US", nowUtc, Ct))?.Text);
    }
}
