namespace ArquitecturaBaseMultitenant.ArchitectureTests;

public sealed partial class VersionedContractTests
{
    [Fact]
    public void Installed_assistant_quick_response_exposes_its_conversation_version()
    {
        Assert.Equal(typeof(uint), VersionType(typeof(ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models.AssistantQuickResponse), "Conversation.Version"));
    }
}
