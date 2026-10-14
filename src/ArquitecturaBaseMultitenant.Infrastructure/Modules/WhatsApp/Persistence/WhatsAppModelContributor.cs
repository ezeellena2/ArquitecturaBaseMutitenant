using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp.Persistence;
/// <summary>El módulo declara cada entidad global y aplica exclusivamente sus configuraciones.</summary>
internal sealed class WhatsAppModelContributor : IPersistenceModelContributor
{
    public string ConfigurationNamespace => typeof(WhatsAppModelContributor).Namespace!;
    public IReadOnlyDictionary<Type, string> GlobalEntities { get; } = new Dictionary<Type, string>
    {
        [typeof(WhatsAppContact)] = Schemas.Identity, [typeof(WhatsAppMessage)] = Schemas.Identity,
        [typeof(WhatsAppCodeQuota)] = Schemas.Identity,
        [typeof(WhatsAppCodeQuotaIp)] = Schemas.Identity,
        [typeof(WhatsAppChannel)] = Schemas.Identity,
        [typeof(WhatsAppChannelUsage)] = Schemas.Identity,
        [typeof(PlatformWhatsAppChat)] = Schemas.Platform,
        [typeof(PlatformWhatsAppChatMessage)] = Schemas.Platform,
        [typeof(PlatformWhatsAppBot)] = Schemas.Platform,
        [typeof(PlatformWhatsAppTeam)] = Schemas.Platform,
        [typeof(PlatformWhatsAppTeamNotice)] = Schemas.Platform,
        [typeof(PlatformWhatsAppTemplate)] = Schemas.Platform,
        [typeof(WhatsAppTemplateState)] = Schemas.Identity,
        [typeof(WhatsAppRate)] = Schemas.Platform,
    };
    public void Configure(ModelBuilder builder)
    {
        // El horario y el menú del bot son valores dentro de su jsonb, no entidades.
        builder.Ignore<WhatsAppBotHours>();
        builder.Ignore<WhatsAppBotOption>();
        builder.Ignore<WhatsAppTemplateVariable>();
        builder.ApplyConfigurationsFromAssembly(GetType().Assembly, type => type.Namespace == ConfigurationNamespace);
    }
}
