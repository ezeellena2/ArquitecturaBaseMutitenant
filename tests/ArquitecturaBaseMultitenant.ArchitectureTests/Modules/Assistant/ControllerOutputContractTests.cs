namespace ArquitecturaBaseMultitenant.ArchitectureTests;

public sealed partial class ControllerOutputContractTests
{
    [Fact]
    public void Installed_assistant_outputs_obey_the_same_contract_classifier()
    {
        Assert.True(IsAllowedOutput(typeof(ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models.AssistantStateResponse)));
        Assert.True(IsAllowedOutput(typeof(ArquitecturaBaseMultitenant.Api.Modules.Assistant.Contracts.AssistantStreamHttpResponse)));
        Assert.False(IsAllowedOutput(typeof(ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Services.IAssistantConversationService)));
    }
}
