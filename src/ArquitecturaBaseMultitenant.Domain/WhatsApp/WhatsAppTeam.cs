using ArquitecturaBaseMultitenant.Domain.Common;

namespace ArquitecturaBaseMultitenant.Domain.WhatsApp;

/// <summary>Un equipo de la organización: atiende una bandeja (Clientes o Internos) y sus integrantes son miembros.</summary>
public sealed class WhatsAppTeam : WhatsAppTeamBase, ITenantOwned
{
    private WhatsAppTeam() { }

    public Guid TenantId { get; private set; }
    public WhatsAppInbox Inbox { get; private set; }

    public static WhatsAppTeam Create(string name, WhatsAppInbox inbox, int? notifyAfterMinutes, IEnumerable<Guid> memberIds)
    {
        if (!Enum.IsDefined(inbox)) throw new ArgumentOutOfRangeException(nameof(inbox));
        var team = new WhatsAppTeam { Inbox = inbox };
        team.Initialize(name, notifyAfterMinutes, memberIds);
        return team;
    }
}
