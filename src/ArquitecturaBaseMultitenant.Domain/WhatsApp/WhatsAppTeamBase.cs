using ArquitecturaBaseMultitenant.Domain.Common;

namespace ArquitecturaBaseMultitenant.Domain.WhatsApp;

/// <summary>
/// Un equipo de atención (ADR 0038): dice qué chats le llegan a cada persona; los roles dicen qué puede hacer. Los
/// integrantes se reemplazan enteros (hasta 200) y el aviso por correo es opcional (D6).
/// </summary>
public abstract class WhatsAppTeamBase : Entity, IVersioned, IAuditable
{
    private protected List<Guid> _members = [];

    public uint Version { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string NormalizedName { get; private set; } = string.Empty;
    public int? NotifyAfterMinutes { get; private set; }
    public IReadOnlyList<Guid> MemberIds => _members.AsReadOnly();
    public DateTime CreatedAtUtc { get; private set; }
    public Guid? CreatedBy { get; private set; }
    public DateTime? ModifiedAtUtc { get; private set; }
    public Guid? ModifiedBy { get; private set; }

    protected void Initialize(string name, int? notifyAfterMinutes, IEnumerable<Guid> memberIds) =>
        Update(name, notifyAfterMinutes, memberIds);

    public void Update(string name, int? notifyAfterMinutes, IEnumerable<Guid> memberIds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var trimmed = name.Trim();
        if (trimmed.Length > TextLimits.ShortName) throw new ArgumentException("The name is too long.", nameof(name));
        if (notifyAfterMinutes is { } minutes && !WhatsAppFeature.NotifyAfterMinutesOptions.Contains(minutes))
            throw new ArgumentOutOfRangeException(nameof(notifyAfterMinutes));
        var next = memberIds.Distinct().ToList();
        if (next.Count > WhatsAppFeature.MaxTeamMembers || next.Contains(Guid.Empty)) throw new ArgumentException("Invalid members.", nameof(memberIds));
        Name = trimmed; NormalizedName = Normalize(trimmed); NotifyAfterMinutes = notifyAfterMinutes; _members = next;
    }

    public static string Normalize(string name) => name.Trim().ToUpperInvariant();
}
