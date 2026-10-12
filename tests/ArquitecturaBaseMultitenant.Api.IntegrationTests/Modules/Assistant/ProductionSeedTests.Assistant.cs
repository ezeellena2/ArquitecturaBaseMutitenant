using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Legal;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Identity;

public sealed partial class ProductionSeedTests
{
    static partial void ContributeSeedEventCount(ref int count) => count++;
    static partial void AssertModuleSeedEvents(List<SecurityEvent> events) =>
        Assert.Single(events, item => item.Type == SecurityEventType.PlatformSettingsChanged
            && item.ActorKind == AuditActorKind.System && item.Reason == "Initial assistant reference seed");
    static partial void ContributeLegalVersions(List<int> versions) => versions.Add(2);
    static partial void AssertModuleLegalContents(List<LegalDocument> documents, List<LegalDocumentContent> contents)
    {
        var ids = documents.Where(document => document.Version == 2).Select(document => document.Id).ToHashSet();
        Assert.All(contents.Where(content => ids.Contains(content.LegalDocumentId)), content =>
        {
            Assert.Contains("Anthropic", content.Text, StringComparison.Ordinal);
            Assert.Contains("30", content.Text, StringComparison.Ordinal);
            Assert.Contains(content.Culture == "es-AR" ? "DEMOSTRACIÓN" : "DEMONSTRATION", content.Text, StringComparison.Ordinal);
        });
    }
}
