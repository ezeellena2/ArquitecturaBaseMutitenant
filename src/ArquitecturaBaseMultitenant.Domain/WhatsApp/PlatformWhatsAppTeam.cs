using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Common;

namespace ArquitecturaBaseMultitenant.Domain.WhatsApp;

/// <summary>Un equipo de operadores de Plataforma (por ejemplo, Soporte y Altas): sin bandeja; sus integrantes son cuentas.</summary>
public sealed class PlatformWhatsAppTeam : WhatsAppTeamBase, IGlobalSecurityAudited
{
    SecurityEventType IGlobalSecurityAudited.SecurityEventType => SecurityEventType.PlatformSettingsChanged;
    Guid? IGlobalSecurityAudited.SecurityEventTargetTenantId => null;

    private PlatformWhatsAppTeam() { }

    public static PlatformWhatsAppTeam Create(string name, int? notifyAfterMinutes, IEnumerable<Guid> accountIds)
    {
        var team = new PlatformWhatsAppTeam();
        team.Initialize(name, notifyAfterMinutes, accountIds);
        return team;
    }
}
