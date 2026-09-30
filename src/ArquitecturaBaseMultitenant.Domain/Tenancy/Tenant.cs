using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Domain.Tenancy;

/// <summary>Representa un espacio personal o una organización y controla las transiciones que permiten entrar a él.</summary>
public sealed class Tenant : Entity
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

    public TenantKind Kind { get; private set; }

    public TenantStatus Status { get; private set; }

    public string Name { get; private set; }

    public string? Slug { get; private set; }

    public static Tenant CreatePersonal(string name) => new(TenantKind.Personal, name, TenantStatus.Active);

    public static Tenant CreateBusiness(string name, bool requiresApproval) =>
        new(TenantKind.Business, name, requiresApproval ? TenantStatus.PendingApproval : TenantStatus.Provisioning);

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
