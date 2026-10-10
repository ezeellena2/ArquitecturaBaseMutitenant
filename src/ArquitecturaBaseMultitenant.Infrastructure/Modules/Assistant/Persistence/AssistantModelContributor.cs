using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant.Persistence;

internal sealed class AssistantModelContributor : IPersistenceModelContributor
{
    public string ConfigurationNamespace => typeof(Configurations.AssistantConversationConfiguration).Namespace!;
    public IReadOnlyDictionary<Type, string> GlobalEntities { get; } = new Dictionary<Type, string>
    {
        [typeof(PlatformAssistantConversation)] = Schemas.Platform,
        [typeof(PlatformAssistantMessage)] = Schemas.Platform,
        [typeof(PlatformAssistantAttachment)] = Schemas.Platform,
        [typeof(PlatformAssistantTurn)] = Schemas.Platform,
        [typeof(PlatformAssistantUsageDaily)] = Schemas.Platform,
        [typeof(AssistantPlatformSettings)] = Schemas.Platform,
        [typeof(AssistantOrganizationPolicy)] = Schemas.Platform,
        [typeof(AssistantModelPrice)] = Schemas.Platform,
        [typeof(AssistantNoticeAcceptance)] = Schemas.Platform,
    };
    public void Configure(ModelBuilder builder) => builder.ApplyConfigurationsFromAssembly(GetType().Assembly,
        type => type.Namespace == ConfigurationNamespace);
}
