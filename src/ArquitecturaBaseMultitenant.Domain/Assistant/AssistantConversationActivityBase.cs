using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Domain.Assistant;

/// <summary>Actividad técnica del chat, independiente de la versión que protege Guardar, Renombrar y Borrar.</summary>
public abstract class AssistantConversationActivityBase : Entity
{
    protected AssistantConversationActivityBase() { }

    protected AssistantConversationActivityBase(Guid conversationId, Guid ownerUserId, Access access, DateTime nowUtc)
        : base(conversationId)
    {
        if (ownerUserId == Guid.Empty) throw new ArgumentException("An owner is required.", nameof(ownerUserId));
        if (!Enum.IsDefined(access)) throw new ArgumentOutOfRangeException(nameof(access));
        OwnerUserId = ownerUserId;
        Access = access;
        Touch(nowUtc);
    }

    public Guid OwnerUserId { get; private set; }
    public Access Access { get; private set; }
    public string? AutomaticTitle { get; private set; }
    public DateTime LastActivityAtUtc { get; private set; }

    public void Touch(DateTime nowUtc)
    {
        if (nowUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("UTC is required.", nameof(nowUtc));
        LastActivityAtUtc = nowUtc;
    }

    public bool TrySetAutomaticTitle(string title, DateTime nowUtc)
    {
        if (AutomaticTitle is not null) return false;
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        if (title.Length > TextLimits.AssistantTitle) throw new ArgumentException("The title is too long.", nameof(title));
        Touch(nowUtc);
        AutomaticTitle = title;
        return true;
    }
}
