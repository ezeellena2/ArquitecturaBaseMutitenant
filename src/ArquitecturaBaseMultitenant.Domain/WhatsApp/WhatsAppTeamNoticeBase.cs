using ArquitecturaBaseMultitenant.Domain.Common;

namespace ArquitecturaBaseMultitenant.Domain.WhatsApp;

/// <summary>
/// El correo «Chats esperando» ya salió para un equipo (D6): no se repite hasta que su bandeja se vacía y vuelve a llenarse.
/// Vive aparte del equipo para que avisar no cambie un dato auditado.
/// </summary>
public abstract class WhatsAppTeamNoticeBase : Entity
{
    public Guid TeamId { get; private set; }
    public DateTime SentAtUtc { get; private set; }

    protected void Initialize(Guid teamId, DateTime nowUtc)
    {
        if (teamId == Guid.Empty) throw new ArgumentException("A team is required.", nameof(teamId));
        if (nowUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("UTC required.", nameof(nowUtc));
        TeamId = teamId; SentAtUtc = nowUtc;
    }
}
