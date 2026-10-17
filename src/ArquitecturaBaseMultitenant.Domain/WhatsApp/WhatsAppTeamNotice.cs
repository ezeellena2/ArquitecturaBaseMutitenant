using ArquitecturaBaseMultitenant.Domain.Common;

namespace ArquitecturaBaseMultitenant.Domain.WhatsApp;

/// <summary>El aviso de un equipo de la organización.</summary>
public sealed class WhatsAppTeamNotice : WhatsAppTeamNoticeBase, ITenantOwned
{
    private WhatsAppTeamNotice() { }

    public Guid TenantId { get; private set; }

    public static WhatsAppTeamNotice Create(Guid teamId, DateTime nowUtc)
    {
        var notice = new WhatsAppTeamNotice();
        notice.Initialize(teamId, nowUtc);
        return notice;
    }
}
