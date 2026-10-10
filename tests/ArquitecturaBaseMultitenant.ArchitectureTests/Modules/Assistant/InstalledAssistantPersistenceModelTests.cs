using ArquitecturaBaseMultitenant.Domain.Assistant;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.ArchitectureTests;

public sealed class InstalledAssistantPersistenceModelTests
{
    [Fact]
    public void Installed_assistant_maps_private_conversations_and_global_operator_conversations()
    {
        var entities = ArchitectureModel.EntityTypes().Where(value => value.ClrType.Namespace == typeof(AssistantErrors).Namespace).ToArray();
        Assert.Contains(entities, value => value.ClrType.Name == "AssistantConversation" && value.GetSchema() == "tenant");
        Assert.Contains(entities, value => value.ClrType.Name == "PlatformAssistantConversation" && value.GetSchema() == "platform");
    }

}
