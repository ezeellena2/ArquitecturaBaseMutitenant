using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Tenancy;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence;

/// <summary>Contexto scoped del acceso activo y del alcance técnico temporal.</summary>
internal sealed class TenantContext(Func<bool>? hasActiveTransaction = null) : ITenantContext, ITenantScope, ITenantAccessInitializer
{
    private readonly Func<bool> _hasActiveTransaction = hasActiveTransaction ?? (() => false);
    private Scope? _activeScope;

    public Guid? TenantId { get; private set; }

    public TenantKind? TenantKind { get; private set; }

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

        if (_activeScope is not null || TenantId is not null || _hasActiveTransaction())
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
