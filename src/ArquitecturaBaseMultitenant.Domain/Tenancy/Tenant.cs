using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Domain.Tenancy;

/// <summary>Representa un espacio personal o una organización y controla las transiciones que permiten entrar a él.</summary>
public sealed class Tenant : Entity, IVersioned
{
    private Tenant()
    {
        Name = string.Empty;
    }

    private Tenant(TenantKind kind, string name, TenantStatus status)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (name.Length > TextLimits.OrganizationName)
        {
            throw new ArgumentOutOfRangeException(nameof(name));
        }

        Kind = kind;
        Name = name;
        Status = status;
    }

    private Tenant(Guid id, TenantKind kind, string name, TenantStatus status) : base(id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (name.Length > TextLimits.OrganizationName)
            throw new ArgumentOutOfRangeException(nameof(name));
        Kind = kind;
        Name = name;
        Status = status;
    }

    public TenantKind Kind { get; private set; }

    public TenantStatus Status { get; private set; }

    public string Name { get; private set; }

    public string? Slug { get; private set; }

    public uint Version { get; private set; }
    public DateTime? CreatedAtUtc { get; private set; }
    public Guid? RegisteredByUserId { get; private set; }
    public TenantRegistrationSource RegistrationSource { get; private set; }

    public Result RecordRegistration(Guid userId, TenantRegistrationSource source, DateTime createdAtUtc)
    {
        if (userId == Guid.Empty) throw new ArgumentException("A registering account is required.", nameof(userId));
        if (!Enum.IsDefined(source)) throw new ArgumentOutOfRangeException(nameof(source));
        if (createdAtUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("Registration must be UTC.", nameof(createdAtUtc));
        if (Kind != TenantKind.Business) return TenantErrors.InvalidTransition;
        if (CreatedAtUtc is not null)
            return RegisteredByUserId == userId && RegistrationSource == source && CreatedAtUtc == createdAtUtc
                ? Result.Success() : TenantErrors.ProvisioningConflict;
        RegisteredByUserId = userId;
        RegistrationSource = source;
        CreatedAtUtc = createdAtUtc;
        return Result.Success();
    }

    public static Tenant CreatePersonal(string name) => new(TenantKind.Personal, name, TenantStatus.Active);

    public static Tenant CreateBusiness(string name, bool requiresApproval) =>
        new(TenantKind.Business, name, requiresApproval ? TenantStatus.PendingApproval : TenantStatus.Provisioning);

    public static Tenant CreateBusiness(Guid id, string name, bool requiresApproval) =>
        new(id, TenantKind.Business, name,
            requiresApproval ? TenantStatus.PendingApproval : TenantStatus.Provisioning);

    public Result SetSlug(TenantSlug slug)
    {
        ArgumentNullException.ThrowIfNull(slug);
        if (Kind != TenantKind.Business) return TenantErrors.InvalidTransition;
        Slug = slug.Value;
        return Result.Success();
    }

    public Result Rename(string? name)
    {
        if (Kind != TenantKind.Business || Status == TenantStatus.Closed)
            return TenantErrors.InvalidTransition;
        if (string.IsNullOrWhiteSpace(name)) return TenantErrors.NameRequired;
        if (name.Trim().Length > TextLimits.OrganizationName) return TenantErrors.NameTooLong;

        Name = name.Trim();
        return Result.Success();
    }

    public Result Approve()
    {
        if (Kind != TenantKind.Business || Status != TenantStatus.PendingApproval)
        {
            return TenantErrors.InvalidTransition;
        }

        Status = TenantStatus.Provisioning;
        return Result.Success();
    }

    public Result Activate()
    {
        if (Kind != TenantKind.Business || Status is not (TenantStatus.Provisioning or TenantStatus.Suspended))
        {
            return TenantErrors.InvalidTransition;
        }

        Status = TenantStatus.Active;
        return Result.Success();
    }

    public Result Suspend()
    {
        if (Kind != TenantKind.Business || Status != TenantStatus.Active)
        {
            return TenantErrors.InvalidTransition;
        }

        Status = TenantStatus.Suspended;
        return Result.Success();
    }

    public Result Close()
    {
        var canClose = Kind == TenantKind.Personal
            ? Status == TenantStatus.Active
            : Status is TenantStatus.PendingApproval or TenantStatus.Suspended;
        if (!canClose)
        {
            return TenantErrors.InvalidTransition;
        }

        Status = TenantStatus.Closed;
        return Result.Success();
    }

    public Result CanEnter() => Status switch
    {
        TenantStatus.Active => Result.Success(),
        TenantStatus.PendingApproval => TenantErrors.PendingApproval,
        TenantStatus.Provisioning => TenantErrors.Provisioning,
        TenantStatus.Suspended => TenantErrors.Suspended,
        TenantStatus.Closed => TenantErrors.Closed,
        _ => TenantErrors.InvalidTransition,
    };
}
