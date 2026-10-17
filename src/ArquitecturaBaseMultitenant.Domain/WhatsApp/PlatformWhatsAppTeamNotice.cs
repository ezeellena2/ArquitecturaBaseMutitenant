namespace ArquitecturaBaseMultitenant.Domain.WhatsApp;

/// <summary>El aviso de un equipo de operadores de Plataforma («Consultas esperando»).</summary>
public sealed class PlatformWhatsAppTeamNotice : WhatsAppTeamNoticeBase
{
    private PlatformWhatsAppTeamNotice() { }

    public static PlatformWhatsAppTeamNotice Create(Guid teamId, DateTime nowUtc)
    {
        var notice = new PlatformWhatsAppTeamNotice();
        notice.Initialize(teamId, nowUtc);
        return notice;
    }
}
