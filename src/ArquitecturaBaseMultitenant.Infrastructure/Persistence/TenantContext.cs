using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Application.Models.Request;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence;

/// <summary>Contexto scoped del acceso activo y del alcance técnico temporal.</summary>
internal sealed class TenantContext(Func<bool>? hasActiveTransaction = null,
    Func<ExecutionActor?>? executionActor = null) : ITenantContext, ITenantScope, ITenantAccessInitializer
{
    private readonly Func<bool> _hasActiveTransaction = hasActiveTransaction ?? (() => false);
    private Scope? _activeScope;

    private Guid? _tenantId;
    private TenantKind? _tenantKind;

    internal bool HasBoundTenant => _tenantId is not null || _activeScope is not null;

    public Guid? TenantId { get => _tenantId ?? executionActor?.Invoke()?.TenantId; private set => _tenantId = value; }

    public TenantKind? TenantKind { get => HasBoundTenant ? _tenantKind : executionActor?.Invoke()?.TenantKind; private set => _tenantKind = value; }

    public Guid RequiredTenantId => TenantId ??
        throw new InvalidOperationException("The active access has no tenant.");

    internal void SetFromAccess(Guid tenantId, TenantKind kind)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("The tenant id cannot be empty.", nameof(tenantId));
        }

        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown tenant kind.");
        }

        if (_activeScope is not null || TenantId is not null || executionActor?.Invoke() is not null || _hasActiveTransaction())
        {
            throw new InvalidOperationException("The active access cannot change within this scope.");
        }

        TenantId = tenantId;
        TenantKind = kind;
    }

    void ITenantAccessInitializer.SetFromAccess(Guid tenantId, TenantKind kind) => SetFromAccess(tenantId, kind);

    public IDisposable Enter(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("The tenant id cannot be empty.", nameof(tenantId));
        }

        if (_hasActiveTransaction())
        {
            throw new InvalidOperationException("Enter must run before opening a transaction.");
        }

        if (executionActor?.Invoke() is not null)
        {
            throw new InvalidOperationException("A technical tenant scope cannot replace an execution actor binding.");
        }

        if (TenantId is { } currentTenantId && currentTenantId != tenantId)
        {
            throw new InvalidOperationException("A different tenant is already active in this scope.");
        }

        var scope = new Scope(this, TenantId, TenantKind, _activeScope);
        _activeScope = scope;
        TenantId = tenantId;
        TenantKind = null;
        return scope;
    }

    private sealed class Scope(
        TenantContext context,
        Guid? previousTenantId,
        TenantKind? previousTenantKind,
        Scope? parent) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            if (!ReferenceEquals(context._activeScope, this))
            {
                throw new InvalidOperationException("Tenant scopes must be disposed in reverse order.");
            }

            context._activeScope = parent;
            context.TenantId = previousTenantId;
            context.TenantKind = previousTenantKind;
            _disposed = true;
        }
    }
}
