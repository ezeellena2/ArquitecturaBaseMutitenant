using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Domain.Tenancy;

namespace ArquitecturaBaseMultitenant.Domain.Assistant;

/// <summary>Technical discovery binding. No prompt, response, arguments, filenames or credentials.</summary>
public sealed class AssistantTurnWork : Entity
{
    private AssistantTurnWork() { }
    private AssistantTurnWork(Guid id) : base(id) { }
    public Guid OwnerUserId { get; private set; }
    public Access Access { get; private set; }
    public Guid? ScopeTenantId { get; private set; }
    public TenantKind? TenantKind { get; private set; }
    public DateTime? SessionStartedAtUtc { get; private set; }
    public string Culture { get; private set; } = string.Empty;
    public string TimeZoneId { get; private set; } = string.Empty;
    public string Status { get; private set; } = "Pending";
    public Guid? LeaseId { get; private set; }
    public DateTime? LeaseExpiresAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime DeadlineAtUtc { get; private set; }
    public bool StopRequested { get; private set; }
    public bool CallStarted { get; private set; }
    public int CallSequence { get; private set; }
    public Guid ModelId { get; private set; }
    public string ProviderModelId { get; private set; } = string.Empty;
    public string Effort { get; private set; } = string.Empty;
    public AssistantKeyFunding Funding { get; private set; }
    public static AssistantTurnWork Create(Guid turnId, Guid owner, Access access, Guid? tenantId, TenantKind? kind,
        string culture, string timeZoneId, DateTime? sessionStartedAtUtc, Guid modelId, string providerModelId,
        string effort, AssistantKeyFunding funding, DateTime nowUtc)
    {
        new AssistantFinancialBinding(owner, access, tenantId).Validate();
        if (turnId == Guid.Empty || modelId == Guid.Empty || nowUtc.Kind != DateTimeKind.Utc ||
            sessionStartedAtUtc is { Kind: not DateTimeKind.Utc } || !Enum.IsDefined(funding) ||
            (access == Access.Platform ? kind is not null : kind != (access == Access.Consumer ? Domain.Tenancy.TenantKind.Personal : Domain.Tenancy.TenantKind.Business)))
            throw new ArgumentException("A complete server work binding is required.");
        ArgumentException.ThrowIfNullOrWhiteSpace(culture);
        ArgumentException.ThrowIfNullOrWhiteSpace(timeZoneId);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerModelId);
        ArgumentException.ThrowIfNullOrWhiteSpace(effort);
        return new(turnId)
        {
            OwnerUserId = owner, Access = access, ScopeTenantId = tenantId, TenantKind = kind,
            Culture = culture, TimeZoneId = timeZoneId, SessionStartedAtUtc = sessionStartedAtUtc,
            ModelId = modelId, ProviderModelId = providerModelId, Effort = effort, Funding = funding,
            CreatedAtUtc = nowUtc, DeadlineAtUtc = nowUtc.AddMinutes(3),
        };
    }
    public bool IsTerminal => Status is "Complete" or "Stopped" or "Failed";
    public bool Claim(Guid lease, DateTime nowUtc)
    {
        if (lease == Guid.Empty || nowUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("A UTC lease is required.");
        if (IsTerminal || LeaseExpiresAtUtc > nowUtc) return false;
        LeaseId = lease; LeaseExpiresAtUtc = nowUtc.AddSeconds(45); Status = "Running";
        return true;
    }
    public void Renew(DateTime nowUtc) => LeaseExpiresAtUtc = nowUtc.AddSeconds(45);
    public void BeginCall(int sequence)
    {
        if (CallStarted || sequence != CallSequence + 1) throw new InvalidOperationException("A provider call cannot be redelivered.");
        CallStarted = true; CallSequence = sequence;
    }
    public void EndCall() => CallStarted = false;
    public void RequestStop() => StopRequested = true;
    public void Finish(string status)
    {
        if (status is not ("Complete" or "Stopped" or "Failed")) throw new ArgumentException("A terminal status is required.", nameof(status));
        Status = status; LeaseExpiresAtUtc = null; LeaseId = null;
    }
}
